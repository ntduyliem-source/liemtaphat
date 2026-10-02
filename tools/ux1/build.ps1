[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$uxWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $uxWorkspace
try {
    $uxBuild=Join-Path $uxWorkspace ('artifacts/ux1/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($uxProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$uxProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $uxProject"}
        $uxHost=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$uxProject]
        dotnet publish "src/$uxProject" -c Release --no-restore -o (Join-Path $uxBuild $uxHost)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $uxProject"}
    }
    $uxWorkerTarget=Join-Path $uxBuild 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $uxWorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $uxBuild 'worker/wwwroot/_framework') -Destination $uxWorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $uxWorkerTarget
    node tools/web1/stage.mjs $uxBuild
    if($LASTEXITCODE -ne 0){throw 'Static staging failed'}
    $uxInfo=@{root=$uxBuild;web=(Join-Path $uxBuild 'site');desktop=(Join-Path $uxBuild 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $uxInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/ux1/current-build.json -Encoding utf8NoBOM
    $uxInfo | ConvertTo-Json
} finally {Pop-Location}
