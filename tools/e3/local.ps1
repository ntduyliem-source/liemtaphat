[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$e3Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$e3Build=Get-Content -LiteralPath (Join-Path $e3Workspace 'artifacts/e3/current-build.json') -Raw | ConvertFrom-Json
& node (Join-Path $e3Workspace 'tools/web1/local.mjs') $Action --root $e3Build.web --port 4184 --state-directory (Join-Path $e3Workspace 'artifacts/e3/local')
if($LASTEXITCODE -ne 0){throw 'E3 local server operation failed'}
