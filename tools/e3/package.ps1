[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$e3Workspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $e3Workspace
try {
    $e3Build=Get-Content artifacts/e3/current-build.json -Raw | ConvertFrom-Json
    $e3Id=Split-Path $e3Build.root -Leaf
    foreach($e3Proof in @('core-verification.json','application-verification.json','runs/chromium/ui.json','runs/firefox/ui.json','runs/desktop/ui.json')){
        $e3Check=Get-Content (Join-Path 'artifacts/e3' $e3Proof) -Raw | ConvertFrom-Json
        if($e3Check.summary.failed -ne 0){throw "Incomplete acceptance: $e3Proof"}
    }
    $e3Native=Get-Content artifacts/e3/word-native-final/report.json -Raw | ConvertFrom-Json
    if($e3Native.failed -ne 0 -or $e3Native.passed -lt 33){throw 'Word E3 native checks are incomplete'}
    $e3Packages=@()
    foreach($e3Kind in @('Web-static','Desktop-win-x64','Word-x86')){
        $e3Name='Locus-E3-'+$e3Id+'-alpha-local-'+$e3Kind
        $e3Release=Join-Path $e3Workspace ('artifacts/releases/'+$e3Name)
        if(Test-Path -LiteralPath $e3Release){throw "Existing release is immutable: $e3Name"}
        New-Item -ItemType Directory -Path $e3Release | Out-Null
        if($e3Kind -eq 'Web-static'){
            Copy-Item -LiteralPath $e3Build.web -Destination (Join-Path $e3Release 'wwwroot') -Recurse
            $e3Local=Join-Path $e3Release 'local';New-Item -ItemType Directory -Path $e3Local | Out-Null
            Copy-Item -LiteralPath tools/web1/local.mjs,tools/web1/serve.mjs -Destination $e3Local
            foreach($e3Action in @('Start','Stop')){
                $e3Launcher=(Get-Content ('tools/web1/'+$e3Action+'-Locus-Web.cmd') -Raw).Replace('4183','4184').Replace(' start',' start --port 4184').Replace(' stop',' stop --port 4184')
                Set-Content -LiteralPath (Join-Path $e3Release ($e3Action+'-Locus-Web.cmd')) -Value $e3Launcher -Encoding ascii
            }
        } elseif($e3Kind -eq 'Desktop-win-x64') {
            Get-ChildItem -LiteralPath $e3Build.desktop | Where-Object { $_.Name -notlike '*.WebView2' } | Copy-Item -Destination $e3Release -Recurse
        } else {
            Copy-Item -LiteralPath artifacts/e3/word-trial/Locus.Word.dll,artifacts/e3/word-trial/Locus.Core.dll,tools/m3/register.ps1 -Destination $e3Release
        }
        Copy-Item -LiteralPath docs/e3/QUICKSTART.md,docs/e3/GRAMMAR.md,docs/e3/REPORT.md -Destination $e3Release
        if($e3Kind -ne 'Word-x86'){
            $e3Licenses=Join-Path $e3Release 'licenses';New-Item -ItemType Directory -Path $e3Licenses | Out-Null
            $e3Nuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
            Copy-Item -LiteralPath (Join-Path $e3Nuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/LICENSE.TXT') -Destination (Join-Path $e3Licenses 'DOTNET-LICENSE.TXT')
            Copy-Item -LiteralPath (Join-Path $e3Nuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $e3Licenses 'DOTNET-THIRD-PARTY-NOTICES.TXT')
            Copy-Item -LiteralPath tools/sh/node_modules/mathjax/LICENSE -Destination (Join-Path $e3Licenses 'MATHJAX-APACHE-2.0.txt')
            Copy-Item -LiteralPath tools/sh/node_modules/@mathjax/mathjax-newcm-font/package.json -Destination (Join-Path $e3Licenses 'MATHJAX-NEWCM-PACKAGE.json')
        }
        $e3Files=@(Get-ChildItem -LiteralPath $e3Release -Recurse -File | ForEach-Object { @{path=[IO.Path]::GetRelativePath($e3Release,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} })
        $e3Files | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $e3Release 'manifest.json') -Encoding utf8NoBOM
        $e3Zip=$e3Release+'.zip';if(Test-Path -LiteralPath $e3Zip){throw 'ZIP exists'}
        Compress-Archive -LiteralPath $e3Release -DestinationPath $e3Zip -CompressionLevel Optimal
        $e3Packages+=@{name=$e3Name;path=[IO.Path]::GetRelativePath($e3Workspace,$e3Zip).Replace('\','/');sha256=(Get-FileHash $e3Zip).Hash;files=$e3Files.Count;bytes=(Get-Item $e3Zip).Length}
    }
    @{build=$e3Id;packages=$e3Packages} | ConvertTo-Json -Depth 5 | Set-Content artifacts/e3/packages.json -Encoding utf8NoBOM
    $e3Packages | ConvertTo-Json
}finally{Pop-Location}
