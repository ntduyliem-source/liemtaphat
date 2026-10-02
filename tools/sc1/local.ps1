[CmdletBinding()]
param([ValidateSet('start','stop','status')][string]$Action='start')
$ErrorActionPreference='Stop'
$sc1Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sc1Build=Get-Content -LiteralPath (Join-Path $sc1Workspace 'artifacts/sc1/current-build.json') -Raw | ConvertFrom-Json
if($Action -in @('stop','status')) {
    # A newly published build does not own the previous build's running server.
    # The launcher still verifies its receipt, instance, root hash and stop token.
    try {
        $sc1Health=Invoke-RestMethod 'http://127.0.0.1:4185/__locus_local/health' -TimeoutSec 2
        if($sc1Health.application -eq 'locus-web1-local/1' -and [regex]::IsMatch([string]$sc1Health.build,'\A[0-9]{8}-[0-9]{6}-[0-9]{3}\z')) {
            $sc1Served=Join-Path $sc1Workspace ('artifacts/sc1/builds/'+$sc1Health.build+'/site')
            if(Test-Path -LiteralPath $sc1Served){$sc1Build.web=$sc1Served}
        }
    } catch { }
}
& node (Join-Path $sc1Workspace 'tools/web1/local.mjs') $Action --root $sc1Build.web --port 4185 --state-directory (Join-Path $sc1Workspace 'artifacts/sc1/local')
if($LASTEXITCODE -ne 0){throw 'SC1 local server operation failed'}
