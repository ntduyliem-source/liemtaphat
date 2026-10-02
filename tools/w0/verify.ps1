[CmdletBinding()]
param([switch]$SkipDesktop,[switch]$IncludeUxResearch)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $workspace
$previousSuite=$env:LOCUS_WORD_TEST_SUITE
$previousFilter=$env:LOCUS_WORD_TEST_FILTER
$registered=$false
try {
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Close Word first; verification owns only fresh synthetic documents.'}
    dotnet restore Locus.Word.sln --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Word restore failed.'}
    dotnet build Locus.Word.sln -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Word build failed.'}
    if(-not $SkipDesktop){dotnet build src/Locus.Desktop -c Release; if($LASTEXITCODE -ne 0){throw 'Desktop build failed.'}}
    & tools/w0/register-probe.ps1 -Action Install;$registered=$true
    $env:LOCUS_WORD_TEST_FILTER=$null
    $suites=@('adapter','lifecycle');if(-not $SkipDesktop){$suites+= 'desktop'}
    if($IncludeUxResearch){$suites+='ux-research'}
    foreach($suite in $suites){
        $env:LOCUS_WORD_TEST_SUITE=$suite
        & tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe
        if($LASTEXITCODE -ne 0){throw "W0 $suite failed."}
        $shutdown=[Diagnostics.Stopwatch]::StartNew()
        while((Get-Process WINWORD -ErrorAction SilentlyContinue) -and $shutdown.Elapsed.TotalSeconds -lt 8){Start-Sleep -Milliseconds 100}
        if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Word has not finished shutdown; no process was terminated.'}
    }
}finally{
    $env:LOCUS_WORD_TEST_SUITE=$previousSuite;$env:LOCUS_WORD_TEST_FILTER=$previousFilter
    if($registered -and -not(Get-Process WINWORD -ErrorAction SilentlyContinue)){& tools/w0/register-probe.ps1 -Action Uninstall}
    Pop-Location
}
