[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start',[int]$Port=4193)
$ErrorActionPreference='Stop'
$gWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$gBuild=Get-Content -LiteralPath (Join-Path $gWorkspace 'artifacts/phase-g/current-build.json') -Raw | ConvertFrom-Json
& node (Join-Path $gWorkspace 'tools/web1/local.mjs') $Action --root $gBuild.web --port $Port --state-directory (Join-Path $gWorkspace 'artifacts/phase-g/local')
if($LASTEXITCODE -ne 0){throw 'Locus G local server did not complete the requested action.'}
