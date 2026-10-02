[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$balWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $balWorkspace
try {
    $balBuild=Join-Path $balWorkspace ('artifacts/bal1/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($balProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$balProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $balProject"}
        $balHost=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$balProject]
        dotnet publish "src/$balProject" -c Release --no-restore -o (Join-Path $balBuild $balHost)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $balProject"}
    }
    $balWorkerTarget=Join-Path $balBuild 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $balWorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $balBuild 'worker/wwwroot/_framework') -Destination $balWorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $balWorkerTarget
    node tools/web1/stage.mjs $balBuild
    if($LASTEXITCODE -ne 0){throw 'Static staging failed'}
    $balInfo=@{root=$balBuild;web=(Join-Path $balBuild 'site');desktop=(Join-Path $balBuild 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $balInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/bal1/current-build.json -Encoding utf8NoBOM
    $balInfo | ConvertTo-Json
} finally {Pop-Location}
