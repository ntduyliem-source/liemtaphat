$ErrorActionPreference='Stop'
$manualWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manualOutput=Join-Path $manualWorkspace 'artifacts/m3'
function Read-ManualReport([string]$Path) {Get-Content -LiteralPath (Join-Path $manualWorkspace $Path) -Raw -Encoding UTF8 | ConvertFrom-Json}
function Manual-Artifact([string]$Path) {@{path=$Path;sha256=(Get-FileHash -LiteralPath (Join-Path $manualWorkspace $Path)).Hash}}
$manualExpected=@(
    @{name='manual';path='artifacts/m3/manual-final/report.json';passed=38}
    @{name='panel';path='artifacts/m3/panel-final/report.json';passed=7}
    @{name='native';path='artifacts/m3/native/verification.json';passed=10}
    @{name='packageSmoke';path='artifacts/m3/package-smoke/report.json';passed=7}
    @{name='sharedWord';path='artifacts/w0/adapter-20260914T015837775Z/report.json';passed=37}
    @{name='sharedLifecycle';path='artifacts/w0/adapter-20260914T020016765Z/report.json';passed=7}
    @{name='desktopWordResearch';path='artifacts/w0/desktop-lifecycle-20260914T020028871Z/report.json';passed=2}
)
$manualRuns=[ordered]@{}
foreach($manualExpectedRun in $manualExpected){
    $manualReport=Read-ManualReport $manualExpectedRun.path
    if($manualReport.failed -ne 0 -or $manualReport.passed -ne $manualExpectedRun.passed){throw "Run not accepted: $($manualExpectedRun.path)"}
    $manualRun=Manual-Artifact $manualExpectedRun.path
    $manualRun.passed=$manualReport.passed;$manualRun.failed=$manualReport.failed
    if($null -ne $manualReport.observed){$manualRun.observed=$manualReport.observed}
    $manualRuns[$manualExpectedRun.name]=$manualRun
}
$manualRegression=Join-Path $manualOutput 'regression'
New-Item -ItemType Directory -Path $manualRegression -Force | Out-Null
foreach($manualRegressionCase in @(
    @{name='core';source='artifacts/m1/verification.json';passed=238},
    @{name='desktop';source='artifacts/m2/verification.json';passed=40}
)) {
    $manualReport=Read-ManualReport $manualRegressionCase.source
    if($manualReport.summary.failed -ne 0 -or $manualReport.summary.passed -ne $manualRegressionCase.passed){throw 'Core/Desktop regression mismatch.'}
    $manualPath='artifacts/m3/regression/'+$manualRegressionCase.name+'.json'
    Copy-Item -LiteralPath (Join-Path $manualWorkspace $manualRegressionCase.source) -Destination (Join-Path $manualWorkspace $manualPath) -Force
    $manualRun=Manual-Artifact $manualPath;$manualRun.passed=$manualReport.summary.passed;$manualRun.failed=0
    $manualRuns[$manualRegressionCase.name]=$manualRun
}
$manualRuntime=Read-ManualReport 'artifacts/m1/runtime-check.json'
if($manualRuntime.status -ne 'PASS' -or -not $manualRuntime.sameAssembly -or -not $manualRuntime.sameResults -or $manualRuntime.sampleCount -ne 13){throw 'Two-runtime core comparison is not accepted.'}
Copy-Item -LiteralPath (Join-Path $manualWorkspace 'artifacts/m1/runtime-check.json') -Destination (Join-Path $manualRegression 'runtimes.json') -Force
$manualRuns.runtime=Manual-Artifact 'artifacts/m3/regression/runtimes.json'
$manualRuns.runtime.sampleCount=13
$manualPackage=Read-ManualReport 'artifacts/m3/package-check.json'
if($manualPackage.status -ne 'PASS' -or $manualPackage.sha256 -ne (Get-FileHash -LiteralPath (Join-Path $manualWorkspace $manualPackage.zip)).Hash){throw 'Package receipt differs from the ZIP.'}
$manualNative=Read-ManualReport 'artifacts/m3/native/verification.json'
foreach($manualAssembly in $manualNative.assemblies){
    foreach($manualFolder in @('src/Locus.Word/bin/Release/net48/','artifacts/releases/Locus-Word-0.3.0-alpha-x86/')){
        if((Get-FileHash -LiteralPath (Join-Path $manualWorkspace ($manualFolder+$manualAssembly.name))).Hash -ne $manualAssembly.sha256){throw 'Current/packaged DLL differs from the native keyboard run.'}
    }
}
foreach($manualEvidence in $manualNative.files){if((Get-FileHash -LiteralPath (Join-Path $manualWorkspace $manualEvidence.path)).Hash -ne $manualEvidence.sha256){throw 'Native evidence changed after verification.'}}
$manualSources=@(
    Get-ChildItem -LiteralPath (Join-Path $manualWorkspace 'src/Locus.Word'),(Join-Path $manualWorkspace 'src/Locus.Core'),(Join-Path $manualWorkspace 'tests/Locus.Word.Tests'),(Join-Path $manualWorkspace 'tools/m3'),(Join-Path $manualWorkspace 'docs/m3') -Recurse -File |
        Where-Object {$_.FullName -notmatch '\\(bin|obj)\\' -and $_.Extension -in '.cs','.csproj','.json','.ps1','.md'}
    Get-Item -LiteralPath (Join-Path $manualWorkspace 'src/Locus.Desktop/Rendering/FormulaRenderer.cs')
) | Sort-Object FullName -Unique | ForEach-Object {Manual-Artifact ($_.FullName.Substring($manualWorkspace.Length+1).Replace('\','/'))}
$manualAcceptance=[ordered]@{
    schemaVersion='locus-m3-acceptance/1';capturedAtUtc=[DateTime]::UtcNow.ToString('o');milestone='M3';status='PASSED_ALPHA_BASELINE'
    baseline=@{os='Windows 10 x64 build 19045';word='Microsoft Word x86 16.0.14026.20302';runtime='.NET Framework 4.8';progId='Locus.Word.Manual';version='0.3.0-alpha';core='locus-core/0.1';grammar='vi-math-m0-proposal-0.1'}
    tasks=@{'M3-01'='DONE';'M3-02'='DONE';'M3-03'='DONE';'M3-04'='DONE'}
    progress=@{completedTasks=4;totalTasks=4;percent=100;meaning='M3 alpha tasks on the declared baseline only; not total project progress or certification of all Word environments.'}
    gates=@{CW='PASSED_MANUAL_BASELINE';G1='PASSED_MANUAL_BASELINE';G2='NOT_PASSED';G3='NOT_PASSED'}
    autoAllowed=$false;runs=$manualRuns;package=$manualPackage;packageReceipt=(Manual-Artifact 'artifacts/m3/package-check.json');assemblies=$manualNative.assemblies;sources=$manualSources
    uiEvidence=@{keyboard='PASS';sourceSelectionSetup='Word COM fixture';previewAndManagedRibbon='F10, L, C, then C or M';controlBitmapReview='PASS at 740x430 and 580x380; software-rendered controls, not Word capture';windowsScreenshot='UNVERIFIED: SetIsBorderRequired / 0x80004002';pointerClick='UNVERIFIED: coordinate input geometry is unavailable';physicalDpiAndMultiMonitor='UNVERIFIED'}
    history=@{w0Unchanged='artifacts/w0/acceptance.json';w0CompletedTasks=4;w0TotalTasks=6;participantAB='DEFERRED_BY_USER';historicalFailedManualRuns='Retained; invalid Find idMso and harness/foreground issues resolved in manual-final.'}
    limits=@('Only explicit selection and confirmation in ordinary main-story text; no marker/Space automation.','Read-only/protection/Track Changes and unsupported structures are refused.','Source <=4096 UTF-16 code units; separate parser/render/metadata bounds, <=500000 Word main-story positions, two-minute preview expiry.','Physical Telex/VNI evidence remains W0; M3 literal Unicode input is not new IME completion proof.','Native may be edited when the add-in is disconnected; no clean-machine certification.','Desktop M2 connection tab remains W0 research; production Desktop edit loop is M4/M6.','Same assembly includes the W0 class, but the package registers only the Manual entry point.','Whole-story text fingerprint does not detect every formatting-only edit.','Word native Redo can leave the caret at the start; move inside before opening the formula again.')
    next=@{task='WEB0-01';status='READY_NOT_STARTED';objective='Run the existing C# core locally inside the browser through WebAssembly, then compare semantics and prototype shared UI.'}
}
$manualAcceptance | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $manualOutput 'acceptance.json') -Encoding UTF8
Write-Output 'M3 alpha acceptance written after checking reports, package, native evidence and current DLL hashes.'
