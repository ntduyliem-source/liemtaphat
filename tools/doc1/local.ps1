[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$docWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$docBuild=Get-Content -LiteralPath (Join-Path $docWorkspace 'artifacts/doc1/current-build.json') -Raw | ConvertFrom-Json
& node (Join-Path $docWorkspace 'tools/web1/local.mjs') $Action --root $docBuild.web --port 4189 --state-directory (Join-Path $docWorkspace 'artifacts/doc1/local')
if($LASTEXITCODE -ne 0){throw 'DOC1 local operation failed'}
