[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $phaseWorkspace
try {
    $phaseBuild=Join-Path $phaseWorkspace ('artifacts/phase-e/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($phaseProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared','Locus.Word')) {
        dotnet restore "src/$phaseProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $phaseProject"}
        $phaseHost=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop';'Locus.Word'='word'}[$phaseProject]
        dotnet publish "src/$phaseProject" -c Release --no-restore -o (Join-Path $phaseBuild $phaseHost)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $phaseProject"}
    }
    $phaseWorker=Join-Path $phaseBuild 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $phaseWorker | Out-Null
    Copy-Item -LiteralPath (Join-Path $phaseBuild 'worker/wwwroot/_framework') -Destination $phaseWorker -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $phaseWorker
    node tools/web1/stage.mjs $phaseBuild
    if($LASTEXITCODE -ne 0){throw 'Static staging failed'}
    Copy-Item -LiteralPath tools/m3/register.ps1 -Destination (Join-Path $phaseBuild 'word')
    $phaseInfo=@{root=$phaseBuild;web=(Join-Path $phaseBuild 'site');desktop=(Join-Path $phaseBuild 'desktop');word=(Join-Path $phaseBuild 'word');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o');status='review-build-awaiting-host-acceptance'}
    $phaseInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/phase-e/current-build.json -Encoding utf8NoBOM
    $phaseInfo | ConvertTo-Json
} finally {Pop-Location}
