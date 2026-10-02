<#
.SYNOPSIS
  Reproducible, synthetic Word COM discovery. Run with Windows PowerShell -STA.
.EXAMPLE
  powershell.exe -NoProfile -STA -File .\tools\m0\word_probe.ps1 -ValidateOnly
.EXAMPLE
  powershell.exe -NoProfile -STA -File .\tools\m0\word_probe.ps1 -Suite Native -CaseId 02-fraction
.EXAMPLE
  powershell.exe -NoProfile -STA -File .\tools\m0\word_probe.ps1
.NOTES
  Refuses to start if any WINWORD process exists. Never attaches ActiveDocument.
  Creates one Word.Application and new synthetic documents only. Opens/saves only
  files generated under artifacts/m0/word/run-*. Does not kill Word processes.
  A synchronous COM call can hang: monitor progress.json and ownership.json from
  an external supervisor. AdvisoryTimeoutSeconds is not a hard COM timeout.
#>
[CmdletBinding()]
param(
    [ValidateSet('All','Native','Lifecycle','Faults','Stale','Tag')][string]$Suite = 'All',
    [string[]]$CaseId = @(),
    [switch]$ValidateOnly,
    [switch]$Visible,
    [ValidateRange(10,3600)][int]$AdvisoryTimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'word-helpers.ps1')
. (Join-Path $PSScriptRoot 'word-suites.ps1')
. (Join-Path $PSScriptRoot 'word-tag-suites.ps1')
$script:Clock = [Diagnostics.Stopwatch]::StartNew()
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$fixturePath = Join-Path $workspace 'fixtures\m0\word\candidates.json'
$runRoot = Join-Path $workspace 'artifacts\m0\word'
$script:RunDirectory = Join-Path $runRoot ('run-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,6))
[void][IO.Directory]::CreateDirectory($script:RunDirectory)
$script:Word = $null
$script:OwnedWordPid = $null
$script:OwnershipVerified = $false
$script:Report = [ordered]@{
    schemaVersion = 'locus-m0-word-report-1'; startedAt = [DateTime]::UtcNow.ToString('o'); updatedAt = $null
    state = 'RUNNING'; suite = $Suite; requestedCaseIds = @($CaseId); validateOnly = [bool]$ValidateOnly
    runDirectory = $script:RunDirectory; fixturePath = $fixturePath
    environment = [ordered]@{os=[Environment]::OSVersion.VersionString;processBitness=([IntPtr]::Size*8);powerShell=$PSVersionTable.PSVersion.ToString();apartment=[Threading.Thread]::CurrentThread.ApartmentState.ToString()}
    activationPreflight = $null; ownership = $null; results = (New-Object 'System.Collections.Generic.List[object]')
    limitations = @(
        'Fixed candidate fixtures do not implement or validate the natural-language parser.',
        'Native edit is performed through the Word COM equation Range; actual keyboard/UI edit requires observation.',
        'Canonical comparison ignores visual styling/run boundaries but checks structure and selected semantic properties; it is not complete OOXML equivalence.',
        'Stale-state input/focus flags are synthetic. Real UniKey/IME commit, Windows focus, Space continuation and inline fx positioning are untested.',
        'The prototype owns synthetic documents exclusively; it does not prove safe coexistence with user documents, coauthoring or add-ins.',
        'Advisory timeout is checked between steps, not enforced inside synchronous COM calls.'
    )
    summary = $null
}
Save-Report
Write-Host ('Run directory: ' + $script:RunDirectory)
$fatal = $false
try {
    $fixtures = Get-Content -LiteralPath $fixturePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $all = @($fixtures.cases)
    $chosen = @(if ($CaseId.Count -gt 0) { $all | Where-Object { $_.caseId -in $CaseId } } else { $all })
    if ($chosen.Count -eq 0) { throw 'No matching fixtures.' }
    foreach ($id in $CaseId) { if ($id -notin @($all | ForEach-Object {$_.caseId})) { throw ('Unknown fixture: ' + $id) } }
    foreach ($fixture in $all) {
        $candidate = Get-SelectedCandidate $fixture
        foreach ($item in $fixture.candidates) { [void](Get-MathSignature $item.omml); [xml](New-OoxmlPackage $item.omml) | Out-Null }
        Record-Test ($fixture.caseId + '.fixture') 'PASS' 'Fixed candidate IDs are unique for selection, candidate count is bounded, and OMML/package XML parses.' @{selected=$candidate.candidateId;candidateCount=$fixture.candidates.Count}
    }
    Copy-Item -LiteralPath $fixturePath -Destination (Join-Path $script:RunDirectory 'candidates-used.json')
    if (-not $ValidateOnly) {
        if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') { throw 'Run this script in Windows PowerShell with -STA.' }
        $existing = @(Get-Process -Name WINWORD -ErrorAction SilentlyContinue)
        if ($existing.Count -gt 0) {
            throw ('REFUSED_EXISTING_WORD: Close Word before the probe. Existing PIDs: ' + (($existing | ForEach-Object {$_.Id}) -join ', '))
        }
        $activation = Get-WordActivationPreflight
        $script:Report.activationPreflight = $activation
        Write-JsonFile (Join-Path $script:RunDirectory 'activation-preflight.json') $activation
        Save-Report
        if (-not $activation.isMicrosoftWord) {
            throw ('REFUSED_NON_MICROSOFT_WORD_REGISTRATION: {0}-bit process resolves Word.Application to {1}; company={2}. No COM instance was created. Inspect the registry view or use the verified Microsoft Word process bitness; this probe does not alter registration.' -f $activation.processBitness, $activation.executable, $activation.companyName)
        }
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LocusM0Native {
    [DllImport("user32.dll", SetLastError=true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
'@
        Set-ProbeStage 'ownership' 'create-new-word-application'
        $launchAt = [DateTime]::UtcNow
        $script:Word = New-Object -ComObject Word.Application
        $bootstrap = New-ProbeDocument
        $window = $bootstrap.ActiveWindow
        try {
            [uint32]$windowPid = 0
            $hwnd = [IntPtr]$window.Hwnd
            [void][LocusM0Native]::GetWindowThreadProcessId($hwnd, [ref]$windowPid)
            if ($windowPid -eq 0) { throw 'Could not resolve the created document window to a process.' }
            $process = Get-Process -Id ([int]$windowPid)
            $processNameMatches = [string]::Equals($process.ProcessName, 'WINWORD', [StringComparison]::OrdinalIgnoreCase)
            $startedAfterLaunch = $process.StartTime.ToUniversalTime() -ge $launchAt.AddSeconds(-2)
            $script:Report.ownership = [ordered]@{
                verified = $false; pid = [int]$windowPid; processName = [string]$process.ProcessName
                processStartUtc = $process.StartTime.ToUniversalTime().ToString('o'); launchRequestedUtc = $launchAt.ToString('o')
                processNameMatches = $processNameMatches; startedAfterLaunch = $startedAfterLaunch
                method = 'No pre-existing WINWORD; new COM application; new synthetic document window HWND resolves to a newly started WINWORD PID.'
                wordVersion = [string]$script:Word.Version; wordBuild = [string]$script:Word.Build
                wordPath = [string]$script:Word.Path; hwnd = $hwnd.ToInt64(); quitRequested = $false; exited = $null
            }
            Write-JsonFile (Join-Path $script:RunDirectory 'ownership.json') $script:Report.ownership
            if (-not $processNameMatches -or -not $startedAfterLaunch) {
                throw ('Could not prove ownership. ProcessName={0}; ProcessStartUtc={1}; LaunchRequestedUtc={2}; PID={3}. See ownership.json.' -f $process.ProcessName, $process.StartTime.ToUniversalTime().ToString('o'), $launchAt.ToString('o'), $windowPid)
            }
            $script:OwnedWordPid = [int]$windowPid
            $script:OwnershipVerified = $true
            $script:Report.ownership.verified = $true
            Write-JsonFile (Join-Path $script:RunDirectory 'ownership.json') $script:Report.ownership
        } finally { Release-Com $window }
        $script:Word.Visible = [bool]$Visible
        $script:Word.DisplayAlerts = 0
        Close-ProbeDocument $bootstrap
        Save-Report
        if ($Suite -in @('All','Native')) { foreach ($fixture in $chosen) { Test-NativeFixture $fixture } }
        $managedFixture = @($all | Where-Object {$_.caseId -eq '03-precedence'})[0]
        if ($Suite -in @('All','Lifecycle')) { Test-ManagedUndo $managedFixture; Test-MetadataLifecycle $managedFixture }
        if ($Suite -in @('All','Faults')) { Test-FaultInjection $managedFixture }
        if ($Suite -in @('All','Stale')) { Test-StaleAdmission $managedFixture }
        if ($Suite -in @('All','Tag')) {
            $tagFixture = @($all | Where-Object {$_.caseId -eq '02-fraction'})[0]
            Test-TagMetadata $tagFixture
            Test-TagFaults $managedFixture
            Test-TagLifecycle $managedFixture
            Test-TagUnicodeStorage $tagFixture
        }
        foreach ($id in @('native-keyboard-ui','visual-fidelity','real-ime-commit','real-editor-focus','space-continuation','inline-fx-position','clipboard-cross-document','save-as-association','startup-addin-lifecycle','multi-document-coauthoring')) {
            Record-Test ('manual.' + $id) 'UNTESTED' 'This criterion requires a separate targeted/manual host experiment; successful COM operations are not evidence for it.' @{reason='Outside this automated probe.'}
        }
    } else {
        Record-Test 'word-com-execution' 'UNTESTED' 'ValidateOnly performs XML/fixture checks and never creates Word.Application.' @{reason='ValidateOnly requested.'}
    }
} catch {
    $fatal = $true
    Record-Test 'probe.fatal' 'FAIL' 'The probe must start and finish under the declared safety preconditions.' (Get-ErrorDetails $_)
} finally {
    foreach ($doc in @($script:OpenDocuments.ToArray())) {
        try { Close-ProbeDocument $doc } catch { Record-Test 'cleanup.document' 'FAIL' 'Close only a synthetic document created/opened by this run.' (Get-ErrorDetails $_) }
    }
    if ($null -ne $script:Word) {
        if ($script:OwnershipVerified) {
            try {
                $script:Report.ownership.quitRequested = $true
                $saveChanges = 0
                $script:Word.Quit([ref]$saveChanges)
            } catch { Record-Test 'cleanup.word-quit' 'FAIL' 'Quit only the Word.Application whose process ownership was verified.' (Get-ErrorDetails $_) }
        } else {
            Record-Test 'cleanup.unverified-application' 'UNTESTED' 'Do not call Quit or kill a Word process whose ownership could not be proved.' @{reason='Ownership not verified; inspect the environment manually.'}
        }
        Release-Com $script:Word; $script:Word = $null
        [GC]::Collect(); [GC]::WaitForPendingFinalizers(); [GC]::Collect(); [GC]::WaitForPendingFinalizers()
        if ($script:OwnershipVerified) {
            Start-Sleep -Milliseconds 500
            $remaining = Get-Process -Id $script:OwnedWordPid -ErrorAction SilentlyContinue
            $script:Report.ownership.exited = $null -eq $remaining
            Write-JsonFile (Join-Path $script:RunDirectory 'ownership.json') $script:Report.ownership
            if ($null -ne $remaining) { Record-Test 'cleanup.process-exit' 'FAIL' 'The owned WINWORD process should exit after Quit. This script never force-kills it.' @{ownedPid=$script:OwnedWordPid} }
        }
    }
    $pass = @($script:Report.results | Where-Object {$_.status -eq 'PASS'}).Count
    $fail = @($script:Report.results | Where-Object {$_.status -eq 'FAIL'}).Count
    $untested = @($script:Report.results | Where-Object {$_.status -eq 'UNTESTED'}).Count
    $script:Report.state = if ($fatal) {'ABORTED'} elseif ($fail -gt 0) {'COMPLETED_WITH_FAILURES'} elseif ($untested -gt 0) {'COMPLETED_WITH_UNTESTED'} else {'COMPLETED'}
    $script:Report.summary = [ordered]@{pass=$pass;fail=$fail;untested=$untested;elapsedSeconds=[Math]::Round($script:Clock.Elapsed.TotalSeconds,2)}
    Save-Report
    Write-Host ('Report: ' + (Join-Path $script:RunDirectory 'report.json'))
    Write-Host ("PASS={0} FAIL={1} UNTESTED={2}" -f $pass,$fail,$untested)
}
if ($fatal -or $fail -gt 0) { exit 2 }
exit 0
