[CmdletBinding()]
param([switch]$SkipWebBuild)
$ErrorActionPreference='Stop'
$dsWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $dsWorkspace
try {
    if(-not $SkipWebBuild){ & tools/web1/build.ps1 -WebOnly -SkipNpm; if($LASTEXITCODE -ne 0){throw 'Web build failed'} }
    $dsWeb=Get-Content artifacts/web1/current-build.json -Raw | ConvertFrom-Json
    $dsOutput=Join-Path $dsWeb.root 'catalog'
    dotnet restore src/Locus.Catalog --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Catalog restore failed'}
    dotnet publish src/Locus.Catalog -c Release --no-restore -o $dsOutput
    if($LASTEXITCODE -ne 0){throw 'Catalog publish failed'}
    $dsWww=Join-Path $dsOutput 'wwwroot'
    $dsWorkerTarget=Join-Path $dsWww 'worker'
    $dsDocsTarget=Join-Path $dsWww 'docs'
    New-Item -ItemType Directory -Force -Path $dsWorkerTarget,$dsDocsTarget | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $dsWeb.root 'web/wwwroot/worker') | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $dsWorkerTarget -Recurse -Force}
    Get-ChildItem -LiteralPath 'docs/design-system' | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $dsDocsTarget -Recurse -Force}
    # This local catalog has no service worker and cannot replace a product release.
    @{web=$dsWeb.web;catalog=$dsWww;version='0.2.1';createdUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content artifacts/design-system/current-build.json -Encoding utf8NoBOM
    Get-Content artifacts/design-system/current-build.json
} finally {Pop-Location}
