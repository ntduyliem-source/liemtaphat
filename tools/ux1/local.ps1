[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$uxWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$uxBuild=Get-Content -LiteralPath (Join-Path $uxWorkspace 'artifacts/ux1/current-build.json') -Raw | ConvertFrom-Json
& node (Join-Path $uxWorkspace 'tools/web1/local.mjs') $Action --root $uxBuild.web --port 4187 --state-directory (Join-Path $uxWorkspace 'artifacts/ux1/local')
if($LASTEXITCODE -ne 0){throw 'UX1 local operation failed'}
