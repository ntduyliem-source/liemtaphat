[CmdletBinding()]
param([switch]$SkipRegression)
$ErrorActionPreference='Stop'
$manualWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $manualWorkspace
$manualPriorSuite=$env:LOCUS_WORD_TEST_SUITE
$manualPriorFilter=$env:LOCUS_WORD_TEST_FILTER
$manualRegistered=$false
$manualRun=Join-Path $manualWorkspace ('artifacts/m3/run-'+[DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
try{
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Close Word first; this verifier creates and closes only synthetic documents.'}
    dotnet restore Locus.Word.sln --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Word restore failed.'}
    dotnet build Locus.Word.sln -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Word build failed.'}
    $env:LOCUS_WORD_TEST_SUITE='manual-panel';$env:LOCUS_WORD_TEST_FILTER=$null
    & tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe (Join-Path $manualRun 'panel')
    if($LASTEXITCODE -ne 0){throw 'Manual panel verification failed.'}
    & tools/m3/register.ps1 -Action Install
    $manualRegistered=$true
    $env:LOCUS_WORD_TEST_SUITE='manual';$env:LOCUS_WORD_TEST_FILTER=$null
    & tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe $manualRun
    if($LASTEXITCODE -ne 0){throw "Manual suite failed; inspect $manualRun"}
    $manualWait=[Diagnostics.Stopwatch]::StartNew()
    while((Get-Process WINWORD -ErrorAction SilentlyContinue) -and $manualWait.Elapsed.TotalSeconds -lt 10){Start-Sleep -Milliseconds 100}
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Word is still running; no process was terminated.'}
    & tools/m3/register.ps1 -Action Uninstall
    $manualRegistered=$false
    if(-not $SkipRegression){
        & tools/w0/verify.ps1
        if($LASTEXITCODE -ne 0){throw 'Shared Word regression failed.'}
        & tools/build.ps1
        if($LASTEXITCODE -ne 0){throw 'Core/Desktop regression failed.'}
    }
    Write-Output "M3 manual report: $manualRun"
}finally{
    $env:LOCUS_WORD_TEST_SUITE=$manualPriorSuite;$env:LOCUS_WORD_TEST_FILTER=$manualPriorFilter
    if($manualRegistered -and -not(Get-Process WINWORD -ErrorAction SilentlyContinue)){& tools/m3/register.ps1 -Action Uninstall}
    Pop-Location
}
