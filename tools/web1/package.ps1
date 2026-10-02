[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]+$')][string]$Label='alpha-local')
$ErrorActionPreference='Stop'
$web1Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $web1Workspace
try {
    $web1Build=Get-Content artifacts/web1/current-build.json -Raw | ConvertFrom-Json -DateKind String
    $web1Ime=Get-Content artifacts/web1/ime/status.json -Raw | ConvertFrom-Json -DateKind String
    if($web1Ime.status -ne 'PASSED_REAL_TELEX_VNI' -or $web1Ime.build -ne (Split-Path $web1Build.root -Leaf)){throw 'Real OS IME evidence must match the packaged build'}
    $web1Packages=@()
    foreach($web1Kind in @('Web-static','Desktop-win-x64')) {
        $web1Name='Locus-WEB1-'+(Split-Path $web1Build.root -Leaf)+'-'+$Label+'-'+$web1Kind
        $web1Release=Join-Path $web1Workspace ('artifacts/releases/'+$web1Name)
        $web1Zip=$web1Release+'.zip'
        if((Test-Path -LiteralPath $web1Release) -or (Test-Path -LiteralPath $web1Zip)){throw "Existing release must not be overwritten: $web1Name"}
        New-Item -ItemType Directory -Path $web1Release | Out-Null
        if($web1Kind -eq 'Web-static'){
            Copy-Item -LiteralPath $web1Build.web -Destination (Join-Path $web1Release 'wwwroot') -Recurse
            $web1Launcher=Join-Path $web1Release 'local'; New-Item -ItemType Directory -Path $web1Launcher | Out-Null
            Copy-Item -LiteralPath 'tools/web1/local.mjs','tools/web1/serve.mjs' -Destination $web1Launcher
            Copy-Item -LiteralPath 'tools/web1/Start-Locus-Web.cmd','tools/web1/Stop-Locus-Web.cmd' -Destination $web1Release
        }
        else {Get-ChildItem -LiteralPath $web1Build.desktop | Where-Object { $_.Name -notlike '*.WebView2' } | Copy-Item -Destination $web1Release -Recurse}
        $web1Notices=Join-Path $web1Release 'licenses'; New-Item -ItemType Directory -Path $web1Notices | Out-Null
        $web1Nuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
        Copy-Item -LiteralPath (Join-Path $web1Nuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/LICENSE.TXT') -Destination (Join-Path $web1Notices 'DOTNET-LICENSE.TXT')
        Copy-Item -LiteralPath (Join-Path $web1Nuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $web1Notices 'DOTNET-THIRD-PARTY-NOTICES.TXT')
        Copy-Item -LiteralPath (Join-Path $web1Nuget 'microsoft.aspnetcore.components.webassembly/10.0.11/THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $web1Notices 'ASPNETCORE-THIRD-PARTY-NOTICES.txt')
        Copy-Item -LiteralPath 'tools/sh/node_modules/mathjax/LICENSE' -Destination (Join-Path $web1Notices 'MATHJAX-APACHE-2.0.txt')
        Copy-Item -LiteralPath 'tools/sh/node_modules/@mathjax/mathjax-newcm-font/package.json' -Destination (Join-Path $web1Notices 'MATHJAX-NEWCM-PACKAGE.json')
        Copy-Item -LiteralPath 'docs/web1/QUICKSTART.md' -Destination $web1Release
        Copy-Item -LiteralPath 'docs/web1/IME.md' -Destination $web1Release
        'Locus WEB1 local alpha. Online publishing is deferred by user choice. Web: extract the whole folder and run Start-Locus-Web.cmd (Node.js 22+ required); stop with Stop-Locus-Web.cmd. Use http://127.0.0.1:4183/ in the same browser profile to retain drafts. Desktop: run Locus.Desktop.Shared.exe (.NET 10 Desktop and WebView2 Runtime required). Actual UniKey Telex/VNI keys were verified on published WASM in Windows WebView2; see IME.md for the precise baseline. Formula editor only; no Word connector, graph/geometry editor, Physics or Chemistry.' | Set-Content -LiteralPath (Join-Path $web1Release 'README.txt') -Encoding utf8NoBOM
        $web1Files=Get-ChildItem -LiteralPath $web1Release -Recurse -File | ForEach-Object { [ordered]@{path=[IO.Path]::GetRelativePath($web1Release,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} }
        @($web1Files) | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $web1Release 'manifest.json') -Encoding utf8NoBOM
        Compress-Archive -LiteralPath $web1Release -DestinationPath $web1Zip -CompressionLevel Optimal
        $web1Packages+=@{name=$web1Name;path=[IO.Path]::GetRelativePath($web1Workspace,$web1Zip).Replace('\','/');bytes=(Get-Item -LiteralPath $web1Zip).Length;sha256=(Get-FileHash -LiteralPath $web1Zip).Hash;files=@($web1Files).Count}
    }
    @{build=$web1Build;packages=$web1Packages} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath artifacts/web1/package.json -Encoding utf8NoBOM
    $web1Packages | ConvertTo-Json
} finally {Pop-Location}
