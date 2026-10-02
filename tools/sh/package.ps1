[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$shWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $shWorkspace
try {
    $shBuild=Get-Content artifacts/sh/current-build.json -Raw | ConvertFrom-Json
    $shPackages=@()
    foreach($shKind in @('Web-static','Desktop-win-x64')) {
        $shName='Locus-SH-'+$shKind
        $shRelease=Join-Path $shWorkspace ('artifacts/releases/'+$shName)
        $shZip=$shRelease+'.zip'
        if((Test-Path -LiteralPath $shRelease) -or (Test-Path -LiteralPath $shZip)){throw "Existing release must be versioned, not overwritten: $shName"}
        New-Item -ItemType Directory -Path $shRelease | Out-Null
        if($shKind -eq 'Web-static'){Copy-Item -LiteralPath $shBuild.web -Destination (Join-Path $shRelease 'wwwroot') -Recurse}
        else {Get-ChildItem -LiteralPath $shBuild.desktop | Copy-Item -Destination $shRelease -Recurse}
        $shNotices=Join-Path $shRelease 'licenses'; New-Item -ItemType Directory -Path $shNotices | Out-Null
        $shNuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
        Copy-Item -LiteralPath (Join-Path $shNuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/LICENSE.TXT') -Destination (Join-Path $shNotices 'DOTNET-LICENSE.TXT')
        Copy-Item -LiteralPath (Join-Path $shNuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $shNotices 'DOTNET-THIRD-PARTY-NOTICES.TXT')
        Copy-Item -LiteralPath (Join-Path $shNuget 'microsoft.aspnetcore.components.webassembly/10.0.11/THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $shNotices 'ASPNETCORE-THIRD-PARTY-NOTICES.txt')
        Copy-Item -LiteralPath 'tools/sh/node_modules/mathjax/LICENSE' -Destination (Join-Path $shNotices 'MATHJAX-APACHE-2.0.txt')
        Copy-Item -LiteralPath 'tools/sh/node_modules/@mathjax/mathjax-newcm-font/package.json' -Destination (Join-Path $shNotices 'MATHJAX-NEWCM-PACKAGE.json')
        Copy-Item -LiteralPath 'docs/sh/QUICKSTART.md' -Destination $shRelease
        @"
Locus SH — shared formula editor foundation, 2026-09-14
Web: serve wwwroot as the root of an HTTP(S) static website. Do not use file://.
Desktop: run Locus.Desktop.Shared.exe. Requires installed .NET 10 Desktop Runtime and Edge WebView2 Runtime on Windows x64.
Save .locus before reloading the page or exiting the app. Automatic draft recovery and offline reload are planned in WEB1.
Formula only. No Word connector, graph/geometry editor, Physics or Chemistry in this SH package.
Renderer: MathJax 4.1.3 and @mathjax/mathjax-newcm-font 4.1.3, Apache-2.0 per bundled package metadata. Bundles are copied unmodified.
Development/build commands in QUICKSTART.md refer to the repository and are not shipped as part of the static website.
"@ | Set-Content -LiteralPath (Join-Path $shRelease 'README.txt') -Encoding utf8NoBOM
        $shFiles=Get-ChildItem -LiteralPath $shRelease -Recurse -File | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($shRelease,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} }
        @($shFiles) | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $shRelease 'manifest.json') -Encoding utf8NoBOM
        Compress-Archive -LiteralPath $shRelease -DestinationPath $shZip -CompressionLevel Optimal
        $shPackages+=@{name=$shName;path=[IO.Path]::GetRelativePath($shWorkspace,$shZip).Replace('\','/');bytes=(Get-Item -LiteralPath $shZip).Length;sha256=(Get-FileHash -LiteralPath $shZip).Hash;files=@($shFiles).Count}
    }
    @{build=$shBuild;packages=$shPackages} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath artifacts/sh/package.json -Encoding utf8NoBOM
    $shPackages | ConvertTo-Json
} finally {Pop-Location}
