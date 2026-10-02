[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $phaseWorkspace
try {
    $phaseBuild=Join-Path $phaseWorkspace ('artifacts/phase-g/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($phaseProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$phaseProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $phaseProject"}
        $phaseHost=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$phaseProject]
        dotnet publish "src/$phaseProject" -c Release --no-restore -o (Join-Path $phaseBuild $phaseHost)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $phaseProject"}
    }
    # The review directory above stays framework-dependent for fast iteration. This second publish is the portable Windows delivery.
    dotnet restore src/Locus.Desktop.Shared -r win-x64 --force-evaluate
    if($LASTEXITCODE -ne 0){throw 'Portable Desktop restore failed'}
    dotnet publish src/Locus.Desktop.Shared -c Release -r win-x64 --self-contained true --no-restore -o (Join-Path $phaseBuild 'desktop-portable')
    if($LASTEXITCODE -ne 0){throw 'Portable Desktop publish failed'}
    # RID restore changes shared lock files. Return them to the repository's cross-host lock state.
    foreach($phaseProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$phaseProject" --force-evaluate
        if($LASTEXITCODE -ne 0){throw "Lock normalization failed: $phaseProject"}
    }
    $phaseWorker=Join-Path $phaseBuild 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $phaseWorker | Out-Null
    Copy-Item -LiteralPath (Join-Path $phaseBuild 'worker/wwwroot/_framework') -Destination $phaseWorker -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $phaseWorker
    node tools/web1/stage.mjs $phaseBuild
    if($LASTEXITCODE -ne 0){throw 'Static staging failed'}
    $phaseInfo=@{root=$phaseBuild;web=(Join-Path $phaseBuild 'site');desktop=(Join-Path $phaseBuild 'desktop');desktopPortable=(Join-Path $phaseBuild 'desktop-portable');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o');status='G-E1-E2-alpha-candidate'}
    $phaseInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/phase-g/current-build.json -Encoding utf8NoBOM
    $phaseInfo | ConvertTo-Json
} finally {Pop-Location}
