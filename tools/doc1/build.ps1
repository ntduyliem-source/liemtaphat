[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$docWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $docWorkspace
try {
    $docBuild=Join-Path $docWorkspace ('artifacts/doc1/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    foreach($docProject in @('Locus.Web','Locus.Worker','Locus.Desktop.Shared')) {
        dotnet restore "src/$docProject" --locked-mode
        if($LASTEXITCODE -ne 0){throw "Restore failed: $docProject"}
        $docHost=@{'Locus.Web'='web';'Locus.Worker'='worker';'Locus.Desktop.Shared'='desktop'}[$docProject]
        dotnet publish "src/$docProject" -c Release --no-restore -o (Join-Path $docBuild $docHost)
        if($LASTEXITCODE -ne 0){throw "Publish failed: $docProject"}
    }
    $docWorkerTarget=Join-Path $docBuild 'web/wwwroot/worker'
    New-Item -ItemType Directory -Force -Path $docWorkerTarget | Out-Null
    Copy-Item -LiteralPath (Join-Path $docBuild 'worker/wwwroot/_framework') -Destination $docWorkerTarget -Recurse
    Copy-Item -LiteralPath 'src/Locus.Worker/worker.js' -Destination $docWorkerTarget
    node tools/web1/stage.mjs $docBuild
    if($LASTEXITCODE -ne 0){throw 'Static staging failed'}
    $docInfo=@{root=$docBuild;web=(Join-Path $docBuild 'site');desktop=(Join-Path $docBuild 'desktop');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o')}
    $docInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/doc1/current-build.json -Encoding utf8NoBOM
    $docInfo | ConvertTo-Json
} finally {Pop-Location}
