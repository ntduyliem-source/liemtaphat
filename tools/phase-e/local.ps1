[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$phaseBuild=Get-Content -LiteralPath (Join-Path $phaseWorkspace 'artifacts/phase-e/current-build.json') -Raw | ConvertFrom-Json
& node (Join-Path $phaseWorkspace 'tools/web1/local.mjs') $Action --root $phaseBuild.web --port 4191 --state-directory (Join-Path $phaseWorkspace 'artifacts/phase-e/local')
if($LASTEXITCODE -ne 0){throw 'Phase E local operation failed'}
