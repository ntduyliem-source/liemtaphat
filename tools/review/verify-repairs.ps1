[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repairRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $repairRoot
try {
    $repairProjects=@('Locus.Core.Tests','Locus.Science.Tests','Locus.SmartChemistry.Tests','Locus.Application.Tests','Locus.Editor.Tests')
    foreach($repairProject in $repairProjects) {
        dotnet restore "tests/$repairProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $repairProject"}
        dotnet build "tests/$repairProject" -c Release --no-restore -p:UseSharedCompilation=false --nologo -v:q
        if($LASTEXITCODE -ne 0){throw "Build failed: $repairProject"}
    }
    foreach($repairProject in $repairProjects) {
        dotnet run --project "tests/$repairProject" -c Release --no-build
        if($LASTEXITCODE -ne 0){throw "Regression failed: $repairProject"}
    }
    foreach($repairGroup in @('--repair-compatibility','--detection-repairs','--content-only','--ct4-only','--balance-only','--studio-balance-only','--document-export-only','--plot-only','--geometry-only')) {
        dotnet run --project tests/Locus.Application.Tests -c Release --no-build -- $repairGroup
        if($LASTEXITCODE -ne 0){throw "Application regression failed: $repairGroup"}
    }
    Write-Output 'Portable repair regression passed. Browser, Desktop and Word runtime acceptance are recorded separately.'
} finally {Pop-Location}
