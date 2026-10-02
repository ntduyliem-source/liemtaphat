[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$e3Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $e3Workspace
try {
    $e3Build=Join-Path $e3Workspace ('artifacts/e3/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($e3Project in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$e3Project" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $e3Project"}
        $e3Host=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$e3Project]
        dotnet publish "src/$e3Project" -c Release --no-restore -o (Join-Path $e3Build $e3Host)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $e3Project"}
    }
    $e3WorkerTarget=Join-Path $e3Build 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $e3WorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $e3Build 'worker/wwwroot/_framework') -Destination $e3WorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $e3WorkerTarget
    node tools/web1/stage.mjs $e3Build
    if($LASTEXITCODE -ne 0){throw 'Versioned static staging failed'}
    $e3Info=@{root=$e3Build;web=(Join-Path $e3Build 'site');desktop=(Join-Path $e3Build 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $e3Info | ConvertTo-Json | Set-Content -LiteralPath artifacts/e3/current-build.json -Encoding utf8NoBOM
    $e3Info | ConvertTo-Json
} finally {Pop-Location}
