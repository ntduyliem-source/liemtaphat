[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $workspace
try {
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Word is already open. Keep that session intact; close it before preparing a fresh W0 trial.'}
    if(-not(Test-Path -LiteralPath 'src/Locus.Word/bin/Release/net48/Locus.Word.dll')){throw 'Build Locus.Word.sln before starting the trial.'}
    & tools/w0/register-probe.ps1 -Action Install
    $x86=Join-Path $env:WINDIR 'SysWOW64/WindowsPowerShell/v1.0/powershell.exe'
    $source="x^2`r"+('A paragraph for W0 scrolling.'+"`r")*100
    & $x86 -NoProfile -STA -ExecutionPolicy Bypass -File tools/w0/start-probe.ps1 -Source $source | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Could not start the owned Word trial.'}
    & $x86 -NoProfile -STA -ExecutionPolicy Bypass -File tools/w0/invoke-probe.ps1 -Action Select -Value 0 -End 3 -Label participant-badge-selection | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Could not select the trial formula.'}
    & $x86 -NoProfile -STA -ExecutionPolicy Bypass -File tools/w0/invoke-probe.ps1 -Action Badge -Value 1 -Label participant-badge-armed | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Could not arm the trial badge.'}
    $armed=Get-Content artifacts/w0/native/participant-badge-armed.json -Raw|ConvertFrom-Json
    if($armed.outcome -ne 'PASS'){throw 'Badge preparation was refused; inspect the recorded result.'}
    Write-Output 'W0 trial ready. Focus the Word editor to see fx; use the Locus W0 Ribbon tab for A/B. Guide: docs/w0/TRIAL-GUIDE.md'
} finally {Pop-Location}
