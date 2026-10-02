[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AdapterReport,
    [Parameter(Mandatory)][string]$LifecycleReport,
    [Parameter(Mandatory)][string]$DesktopLifecycleReport,
    [Parameter(Mandatory)][string]$UxResearchReport,
    [string]$DesktopRegressionReport='artifacts/w0/desktop-regression/verification.json'
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $workspace
try {
    function File-Evidence([string]$path) {
        $resolved=(Resolve-Path -LiteralPath $path).Path
        [ordered]@{path=[IO.Path]::GetRelativePath($workspace,$resolved).Replace('\','/');sha256=(Get-FileHash -LiteralPath $resolved -Algorithm SHA256).Hash}
    }
    function Report-Evidence([string]$path,[int]$expectedPassed,[int]$expectedObserved=0,[switch]$NestedSummary) {
        $report=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        $counts=if($NestedSummary){$report.summary}else{$report}
        $passes=@($report.results|Where-Object status -eq 'PASS').Count
        $failures=@($report.results|Where-Object status -eq 'FAIL').Count
        $observations=@($report.results|Where-Object status -eq 'OBSERVED').Count
        if($counts.passed -ne $expectedPassed -or $counts.failed -ne 0 -or $passes -ne $expectedPassed -or $failures -ne 0 -or $observations -ne $expectedObserved -or @($report.results).Count -ne ($expectedPassed+$expectedObserved)) {
            throw "Report does not match the W0 evidence scope: $path"
        }
        $entry=File-Evidence $path
        $entry.passed=$passes;$entry.failed=$failures;$entry.observed=$observations
        $entry
    }
    $runs=[ordered]@{
        adapter=Report-Evidence $AdapterReport 36 1
        addinLifecycle=Report-Evidence $LifecycleReport 7
        desktopLifecycle=Report-Evidence $DesktopLifecycleReport 2
        uxResearch=Report-Evidence $UxResearchReport 7
        desktopRegression=Report-Evidence $DesktopRegressionReport 40 -NestedSummary
    }
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Word is still running; record cleanup only after verification exits.'}
    $keys=@('Software\Classes\Locus.Word.W0','Software\Classes\CLSID\{D67B9C40-E18A-4BC3-8D29-8A78FA05E5D0}','Software\Microsoft\Office\Word\Addins\Locus.Word.W0')
    $registry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry32)
    try {
        foreach($keyPath in $keys){$key=$registry.OpenSubKey($keyPath);if($key){$key.Dispose();throw "Trial registration remains: $keyPath"}}
    } finally {$registry.Dispose()}
    $portable=File-Evidence 'artifacts/releases/Locus-0.2.0-alpha-win-x64.zip'
    if($portable.sha256 -ne '86D2DE0AF40DCACA573D00538A6493A38DBBBAEC60A7AAAF941C1B008FFFBE55'){throw 'M2 portable differs from the previously accepted artifact.'}
    $wordCore=File-Evidence 'src/Locus.Word/bin/Release/net48/Locus.Core.dll'
    $desktopCore=File-Evidence 'src/Locus.Desktop/bin/Release/net10.0-windows/Locus.Core.dll'
    if($wordCore.sha256 -ne $desktopCore.sha256){throw 'Word and Desktop builds do not contain the same core DLL.'}
    $paths=rg --files src/Locus.Core src/Locus.Word src/Locus.Desktop tests/Locus.Word.Tests tests/Locus.Desktop.Tests tools/w0
    if($LASTEXITCODE -ne 0){throw 'Could not enumerate source files.'}
    $sources=@(@($paths | Where-Object {$_ -match '\.(cs|csproj|json|ps1)$'}) + @('Locus.Word.sln','global.json') | Sort-Object | ForEach-Object {File-Evidence $_})
    $native=@('telex-x.json','telex-tilde.json','telex-complete.json','find-focus-fixed.json','find-refusal-fixed.json','editor-conversion-success.json','followup-verification.json','keyboard-followup-log.json','vni-complete.json','vni-backspace.json','vni-undo.json','vni-redo.json','vni-converted-exact.json','continuation-vni-inspect-final.json','clipboard-keys-same-document.json','clipboard-keys-cross-document.json','ribbon-refusal-keys.json','font-dialog-refusal.json','other-app-refusal.json')
    $followup=Get-Content 'artifacts/w0/native/followup-verification.json' -Raw|ConvertFrom-Json
    if($followup.passed -ne 8 -or $followup.failed -ne 0 -or @($followup.cases|Where-Object status -eq 'PASS').Count -ne 8){throw 'Native follow-up evidence is incomplete.'}
    $space=Get-Content 'artifacts/w0/native/space-verification.json' -Raw|ConvertFrom-Json
    if($space.passed -ne 6 -or $space.failed -ne 0 -or @($space.cases|Where-Object status -eq 'PASS').Count -ne 6){throw 'Native Space evidence is incomplete.'}
    foreach($item in $space.evidence){if((File-Evidence $item.path).sha256 -ne $item.sha256){throw "Space evidence changed: $($item.path)"}}
    $index=[ordered]@{
        schemaVersion='locus-w0-acceptance/1'
        capturedAtUtc=[DateTime]::UtcNow.ToString('o')
        milestone='W0'
        status='PARTIAL'
        baseline=[ordered]@{word='Microsoft Word x86 16.0.14026.20302';os='Windows x64';connectorRuntime='.NET Framework 4.8';progId='Locus.Word.W0'}
        tasks=[ordered]@{'W0-01'='DONE';'W0-02'='DONE';'W0-03'='DONE';'W0-04'='DONE';'W0-05'='DOING';'W0-06'='DOING'}
        progress=[ordered]@{completedResearchTasks=4;totalResearchTasks=6;percentRounded=67;meaning='Equal task count, not elapsed work or delivery-time estimate.'}
        gates=[ordered]@{CW='PASSED_MANUAL_BASELINE';G1='NOT_PASSED';G2='NOT_PASSED';G3='NOT_PASSED'}
        runs=$runs
        nativeObservations=@($native | ForEach-Object {File-Evidence (Join-Path 'artifacts/w0/native' $_)})
        nativeSpace=File-Evidence 'artifacts/w0/native/space-verification.json'
        pending=@('Visual fx placement/click acceptance and actual multiple-DPI/display testing; baseline geometry/scroll/zoom/window/source-expiry assertions pass.','Participant comparison of the two implemented native source-session variants and D-01 decision.','Production preview/confirmation flow and input-completion proof for auto modes belong to M3-M5.')
        limitations=@('All mutation tests use owned synthetic documents and execute on the Word UI thread inside the add-in.','Adapter clipboard/typing API checks are distinct from the separately recorded native Windows input checks.','The OBSERVED adapter row is supplementary diagnostic data; the 30-case continuation matrix has assertions.','Both source-session prototypes use the real core. Six native-input groups are agent-operated, not participant acceptance.','Synthetic DPI layout cases do not certify physical monitors or visual appearance. The final seven-group UX run required activating the actual Word editor; an earlier outside-editor failure is retained.','Desktop IPC is a diagnostic read-only entry point, not production candidate transport.','Native keyboard access recovered; screenshots still fail with SetIsBorderRequired 0x80004002 and clicks with unavailable coordinate geometry.','Earlier physical checks preceded specific guard additions, as recorded in their individual evidence reports; the latest full automated runs use the final source build.','Source hashes describe the files at indexing time; this index does not independently certify a source-to-binary build.','Production auto marker/Space remain disabled. Native-live Space exists only in an explicitly started W0 sandbox trial and uses an experimental quiet-period check.')
        cleanup=[ordered]@{wordRunning=$false;ownedRegistryKeysAbsent=$keys}
        unchangedM2Portable=$portable
        sharedCoreDllMatched=$true
        binaries=@(
            File-Evidence 'src/Locus.Word/bin/Release/net48/Locus.Word.dll'
            $wordCore
            $desktopCore
            File-Evidence 'src/Locus.Desktop/bin/Release/net10.0-windows/Locus.Desktop.dll'
            File-Evidence 'tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe'
        )
        sourceFiles=$sources
    }
    $index|ConvertTo-Json -Depth 12|Set-Content -LiteralPath 'artifacts/w0/acceptance.json' -Encoding utf8
    Write-Output 'W0 evidence indexed: 36 adapter PASS + 1 OBSERVED, 7 add-in PASS, 2 Desktop lifecycle PASS, 7 UX research PASS, 40 Desktop regression PASS, 8 earlier native follow-up groups and 6 native Space groups. W0 is 4/6 accepted tasks; visual/participant acceptance pending.'
} finally {Pop-Location}
