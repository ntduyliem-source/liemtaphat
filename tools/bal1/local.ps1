[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$balWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$balBuild=Get-Content -LiteralPath (Join-Path $balWorkspace 'artifacts/bal1/current-build.json') -Raw | ConvertFrom-Json
& node (Join-Path $balWorkspace 'tools/web1/local.mjs') $Action --root $balBuild.web --port 4188 --state-directory (Join-Path $balWorkspace 'artifacts/bal1/local')
if($LASTEXITCODE -ne 0){throw 'BAL1 local operation failed'}
