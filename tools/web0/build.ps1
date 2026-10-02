[CmdletBinding()]
param([switch]$SkipHybrid)
$ErrorActionPreference='Stop'
$webWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $webWorkspace
try {
    npm --prefix tools/web0 ci --ignore-scripts
    if($LASTEXITCODE -ne 0){throw 'Locked npm restore failed.'}
    Copy-Item -LiteralPath tools/web0/node_modules/jsxgraph/distrib/jsxgraphcore.js -Destination prototypes/web0/Shared/wwwroot/vendor/jsxgraphcore.js
    Copy-Item -LiteralPath tools/web0/node_modules/jsxgraph/LICENSE.MIT -Destination prototypes/web0/Shared/wwwroot/vendor/JSXGraph-LICENSE.MIT
    dotnet restore Locus.Web.sln --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Locked .NET restore failed.'}
    dotnet run --project prototypes/web0/Native -c Release --no-restore -- artifacts/web/native
    if($LASTEXITCODE -ne 0){throw 'Native core baseline failed.'}
    # Publish can retain old fingerprinted assemblies. Clear only this verified,
    # generated output so payload measurements/packages contain one build.
    $webArtifacts=[IO.Path]::GetFullPath((Join-Path $webWorkspace 'artifacts/web'))
    $webPublished=[IO.Path]::GetFullPath((Join-Path $webArtifacts 'browser'))
    if((Split-Path $webPublished -Parent) -ne $webArtifacts -or (Split-Path $webPublished -Leaf) -ne 'browser'){throw 'Unexpected browser publish target.'}
    if(Test-Path -LiteralPath $webPublished){Remove-Item -LiteralPath $webPublished -Recurse -Force}
    dotnet publish prototypes/web0/Browser -c Release --no-restore -o artifacts/web/browser
    if($LASTEXITCODE -ne 0){throw 'Browser publish failed.'}
    if(-not $SkipHybrid){
        dotnet publish prototypes/web0/Hybrid -c Release --no-restore -o artifacts/web/hybrid
        if($LASTEXITCODE -ne 0){throw 'Hybrid publish failed; close the WEB0 prototype before publishing.'}
    }
} finally {Pop-Location}
