[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$sc1Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $sc1Workspace
try {
    $sc1Build=Join-Path $sc1Workspace ('artifacts/sc1/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($sc1Project in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$sc1Project" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $sc1Project"}
        $sc1Host=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$sc1Project]
        dotnet publish "src/$sc1Project" -c Release --no-restore -o (Join-Path $sc1Build $sc1Host)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $sc1Project"}
    }
    $sc1WorkerTarget=Join-Path $sc1Build 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $sc1WorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $sc1Build 'worker/wwwroot/_framework') -Destination $sc1WorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $sc1WorkerTarget
    node tools/web1/stage.mjs $sc1Build
    if($LASTEXITCODE -ne 0){throw 'Versioned static staging failed'}
    $sc1Info=@{root=$sc1Build;web=(Join-Path $sc1Build 'site');desktop=(Join-Path $sc1Build 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $sc1Info | ConvertTo-Json | Set-Content -LiteralPath artifacts/sc1/current-build.json -Encoding utf8NoBOM
    $sc1Info | ConvertTo-Json
} finally {Pop-Location}
