[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$webWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$staticRoot=Join-Path $webWorkspace 'artifacts/web/browser/wwwroot'
if(-not (Test-Path -LiteralPath (Join-Path $staticRoot 'index.html'))){throw 'Run tools/web0/build.ps1 first.'}
$webRelease=Join-Path $webWorkspace 'artifacts/releases/Locus-Web-WEB0-static'
$webZip=Join-Path $webWorkspace 'artifacts/releases/Locus-Web-WEB0-static.zip'
if(Test-Path -LiteralPath $webRelease){throw 'This WEB0 package directory already exists; choose a new version before packaging another release.'}
[void](New-Item -ItemType Directory -Path $webRelease)
Copy-Item -LiteralPath $staticRoot -Destination (Join-Path $webRelease 'wwwroot') -Recurse
$notices=Join-Path $webRelease 'licenses'
[void](New-Item -ItemType Directory -Path $notices)
$nugetRoot=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
Copy-Item -LiteralPath (Join-Path $nugetRoot 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/LICENSE.TXT') -Destination (Join-Path $notices 'DOTNET-LICENSE.TXT')
Copy-Item -LiteralPath (Join-Path $nugetRoot 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $notices 'DOTNET-THIRD-PARTY-NOTICES.TXT')
Copy-Item -LiteralPath (Join-Path $nugetRoot 'microsoft.aspnetcore.components.webassembly/10.0.11/THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $notices 'ASPNETCORE-THIRD-PARTY-NOTICES.txt')
Copy-Item -LiteralPath (Join-Path $webWorkspace 'prototypes/web0/Shared/wwwroot/vendor/JSXGraph-LICENSE.MIT') -Destination $notices
Copy-Item -LiteralPath (Join-Path $webWorkspace 'docs/web/QUICKSTART.md') -Destination (Join-Path $webRelease 'QUICKSTART.md')
Set-Content -LiteralPath (Join-Path $webRelease 'README.txt') -Encoding utf8 -Value @'
Locus WEB0 — static architecture prototype, 2026-09-14
Serve wwwroot as the root of an HTTP(S) website; do not open index.html via file://.
The browser runs the C# core locally. No .NET analysis server, account or Word is required.
Not a public production release: no persistent drafts, offline reload/PWA, formula SVG/PNG, Chemistry/Physics, full graph/geometry editor or Word integration.
Example with an existing static server: use its document root option to serve ./wwwroot.
QUICKSTART.md contains repository development commands; tools are in the repository, not in this static bundle.
JSXGraph and .NET third-party notices are retained under licenses/ and wwwroot/_content/.
'@
Compress-Archive -LiteralPath $webRelease -DestinationPath $webZip -CompressionLevel Optimal
$package=[ordered]@{capturedAtUtc=[DateTime]::UtcNow.ToString('o');path='artifacts/releases/Locus-Web-WEB0-static.zip';bytes=(Get-Item -LiteralPath $webZip).Length;sha256=(Get-FileHash -LiteralPath $webZip -Algorithm SHA256).Hash;scope='WEB0 static prototype only; not hosted online or a WEB1 alpha'}
$package | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $webWorkspace 'artifacts/web/package.json') -Encoding utf8
$package | ConvertTo-Json
