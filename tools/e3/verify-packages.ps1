$ErrorActionPreference='Stop'
$e3Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$e3PackageInfo=Get-Content (Join-Path $e3Workspace 'artifacts/e3/packages.json') -Raw | ConvertFrom-Json
$e3Checks=@()
$e3ExtractRoot=[IO.Path]::GetFullPath((Join-Path $e3Workspace ('artifacts/e3/package-check/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))))
New-Item -ItemType Directory -Path $e3ExtractRoot | Out-Null
foreach($e3Package in $e3PackageInfo.packages){
    $e3Zip=Join-Path $e3Workspace $e3Package.path
    if((Get-FileHash -LiteralPath $e3Zip).Hash -ne $e3Package.sha256){throw 'ZIP hash mismatch'}
    Expand-Archive -LiteralPath $e3Zip -DestinationPath $e3ExtractRoot
    $e3Folder=Join-Path $e3ExtractRoot $e3Package.name
    $e3Manifest=Get-Content (Join-Path $e3Folder 'manifest.json') -Raw | ConvertFrom-Json
    foreach($e3File in $e3Manifest){
        $e3Path=[IO.Path]::GetFullPath((Join-Path $e3Folder $e3File.path))
        if(-not $e3Path.StartsWith($e3Folder+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest target outside package'}
        if((Get-FileHash -LiteralPath $e3Path).Hash.ToLowerInvariant() -ne $e3File.sha256 -or (Get-Item -LiteralPath $e3Path).Length -ne $e3File.bytes){throw "Artifact differs: $e3Path"}
    }
    $e3Checks+=@{id=$e3Package.name;status='PASS';files=@($e3Manifest).Count;sha256=$e3Package.sha256}
    if($e3Package.name.EndsWith('Web-static')){
        $e3Root=Join-Path $e3Folder 'wwwroot'
        $e3StaticJson=& node (Join-Path $e3Folder 'local/local.mjs') start --root $e3Root --port 4185 --state-directory (Join-Path $e3Folder '.locus-local')
        if($LASTEXITCODE -ne 0){throw 'Extracted Web failed to start'}
        try {
            $e3Health=Invoke-RestMethod http://127.0.0.1:4185/__locus_local/health
            $e3Html=(Invoke-WebRequest http://127.0.0.1:4185/).Content
            if($e3Health.build -ne $e3PackageInfo.build -or -not $e3Html.Contains($e3PackageInfo.build)){throw 'Extracted build mismatch'}
            foreach($e3Path in @((('/releases/{0}/worker/worker.js') -f $e3PackageInfo.build),(('/releases/{0}/_content/Locus.Editor/editor.js') -f $e3PackageInfo.build),'/service-worker.js')){
                if((Invoke-WebRequest ('http://127.0.0.1:4185'+$e3Path)).StatusCode -ne 200){throw 'Missing release file'}
            }
            $e3Checks+=@{id='extracted-web-start-and-assets';status='PASS';build=$e3Health.build}
        }finally{& node (Join-Path $e3Folder 'local/local.mjs') stop --root $e3Root --port 4185 --state-directory (Join-Path $e3Folder '.locus-local') | Out-Null}
    }
}
@{capturedAtUtc=[DateTime]::UtcNow.ToString('o');build=$e3PackageInfo.build;summary=@{checks=$e3Checks.Count;passed=$e3Checks.Count;failed=0};results=$e3Checks;extracted=$e3ExtractRoot} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $e3Workspace 'artifacts/e3/package-verification.json') -Encoding utf8NoBOM
Write-Output ('E3 package checks: '+$e3Checks.Count+' PASS')
