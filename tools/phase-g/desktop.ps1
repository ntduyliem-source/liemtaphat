$ErrorActionPreference='Stop'
$gWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$gBuild=Get-Content -LiteralPath (Join-Path $gWorkspace 'artifacts/phase-g/current-build.json') -Raw | ConvertFrom-Json
$gProfile=Join-Path $gWorkspace 'artifacts/phase-g/desktop-profile'
$gReceipt=Join-Path $gWorkspace 'artifacts/phase-g/desktop-receipt.json'
Start-Process -FilePath (Join-Path $gBuild.desktop 'Locus.Desktop.Shared.exe') -WorkingDirectory $gBuild.desktop -ArgumentList @('--profile',('"'+$gProfile+'"'),'--receipt',('"'+$gReceipt+'"')) -WindowStyle Hidden
