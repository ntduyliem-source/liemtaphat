[CmdletBinding()]
param([switch]$WebOnly,[switch]$SkipNpm)
$ErrorActionPreference='Stop'
$web1Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $web1Workspace
try {
    if(-not $SkipNpm){npm --prefix tools/sh ci --ignore-scripts; if($LASTEXITCODE -ne 0){throw 'Locked renderer dependency restore failed'}}
    node tools/web1/vendor.mjs
    if($LASTEXITCODE -ne 0){throw 'Renderer assets failed'}
    $web1Build=Join-Path $web1Workspace ('artifacts/web1/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    $web1Projects=@('Locus.Web','Locus.Worker')
    if(-not $WebOnly){$web1Projects+='Locus.Desktop.Shared'}
    foreach($web1Project in $web1Projects) {
        dotnet restore "src/$web1Project" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $web1Project"}
        $web1Host=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$web1Project]
        dotnet publish "src/$web1Project" -c Release --no-restore -o (Join-Path $web1Build $web1Host)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $web1Project"}
    }
    $web1WorkerTarget=Join-Path $web1Build 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $web1WorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $web1Build 'worker/wwwroot/_framework') -Destination $web1WorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $web1WorkerTarget
    node tools/web1/stage.mjs $web1Build
    if($LASTEXITCODE -ne 0){throw 'Versioned static staging failed'}
    $web1Info=@{root=$web1Build;web=(Join-Path $web1Build 'site');desktop=(Join-Path $web1Build 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $web1Info | ConvertTo-Json | Set-Content -LiteralPath artifacts/web1/current-build.json -Encoding utf8NoBOM
    $web1Info | ConvertTo-Json
} finally {Pop-Location}
