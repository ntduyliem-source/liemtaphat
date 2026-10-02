[CmdletBinding()]
param([switch]$SkipNpm)
$ErrorActionPreference='Stop'
$shWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $shWorkspace
try {
    if(-not $SkipNpm){npm --prefix tools/sh ci --ignore-scripts; if($LASTEXITCODE -ne 0){throw 'npm locked restore failed'}}
    node tools/sh/vendor.mjs
    if($LASTEXITCODE -ne 0){throw 'Renderer assets failed'}
    $shBuild=Join-Path $shWorkspace ('artifacts/sh/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($shProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$shProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $shProject"}
        $shHost=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$shProject]
        dotnet publish "src/$shProject" -c Release --no-restore -o (Join-Path $shBuild $shHost)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $shProject"}
    }
    $shWorkerTarget=Join-Path $shBuild 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $shWorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $shBuild 'worker/wwwroot/_framework') -Destination $shWorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $shWorkerTarget
    $shInfo=@{root=$shBuild;web=(Join-Path $shBuild 'web/wwwroot');desktop=(Join-Path $shBuild 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $shInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/sh/current-build.json -Encoding utf8NoBOM
    $shInfo | ConvertTo-Json
} finally {Pop-Location}
