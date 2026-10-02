[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $phaseWorkspace
try {
    $phaseBuild=Get-Content artifacts/phase-e/current-build.json -Raw | ConvertFrom-Json
    $phaseId=Split-Path $phaseBuild.root -Leaf
    foreach($phaseProof in @('passive-fix/contracts/report.json','science-panel/report.json','transactions-20260915-232727/report.json','native-update-20260915-232508/report.json','passive-fix/content/content-tests.json')) {
        $phaseCheck=Get-Content (Join-Path 'artifacts/phase-e' $phaseProof) -Raw | ConvertFrom-Json
        if($phaseCheck.failed -ne 0){throw "Failed evidence: $phaseProof"}
    }
    foreach($phaseProof in @('core-verification.json','application-verification.json')) {
        $phaseCheck=Get-Content (Join-Path 'artifacts/phase-e/passive-fix/science' $phaseProof) -Raw | ConvertFrom-Json
        if($phaseCheck.summary.failed -ne 0){throw "Failed science evidence: $phaseProof"}
    }
    $phaseNative=Get-Content artifacts/phase-e/passive-fix/native/report.json -Raw | ConvertFrom-Json
    $phaseIsolated=Get-Content artifacts/phase-e/passive-fix/native-chem-direct/report.json -Raw | ConvertFrom-Json
    if($phaseNative.passed -ne 6 -or $phaseNative.failed -ne 1 -or $phaseIsolated.failed -ne 0 -or $phaseIsolated.passed -ne 1){throw 'Unexpected final native evidence'}
    if(@($phaseNative.results | Where-Object status -eq 'FAIL')[0].id -ne @($phaseIsolated.results)[0].id){throw 'Native failure is not covered by the isolated check'}
    $phasePerformance=Get-Content artifacts/phase-e/performance.json -Raw | ConvertFrom-Json
    if($phasePerformance.build -ne $phaseId -or -not $phasePerformance.binaryHashesMatch){throw 'Performance evidence does not match build'}
    foreach($phaseBinary in @('Locus.Word.dll','Locus.Core.dll')) {
        if((Get-FileHash (Join-Path $phaseBuild.word $phaseBinary)).Hash -ne (Get-FileHash (Join-Path 'tests/Locus.Word.Tests/bin/Release/net48' $phaseBinary)).Hash){throw 'Native test binary differs from release'}
    }
    $phasePackages=@()
    foreach($phaseKind in @('Web-static','Desktop-win-x64','Word-x86')) {
        $phaseName='Locus-E-'+$phaseId+'-review-local-'+$phaseKind
        $phaseRelease=Join-Path $phaseWorkspace ('artifacts/releases/'+$phaseName)
        if(Test-Path -LiteralPath $phaseRelease){throw "Immutable package already exists: $phaseName"}
        New-Item -ItemType Directory -Path $phaseRelease | Out-Null
        if($phaseKind -eq 'Web-static') {
            Copy-Item -LiteralPath $phaseBuild.web -Destination (Join-Path $phaseRelease 'wwwroot') -Recurse
            $phaseLocal=Join-Path $phaseRelease 'local'; New-Item -ItemType Directory -Path $phaseLocal | Out-Null
            Copy-Item -LiteralPath tools/web1/local.mjs,tools/web1/serve.mjs -Destination $phaseLocal
            foreach($phaseAction in @('Start','Stop')) {
                $phaseLauncher=(Get-Content ('tools/web1/'+$phaseAction+'-Locus-Web.cmd') -Raw).Replace('4183','4191').Replace(' start',' start --port 4191').Replace(' stop',' stop --port 4191')
                Set-Content -LiteralPath (Join-Path $phaseRelease ($phaseAction+'-Locus-Web.cmd')) -Value $phaseLauncher -Encoding ascii
            }
        } elseif($phaseKind -eq 'Desktop-win-x64') {
            Get-ChildItem -LiteralPath $phaseBuild.desktop | Where-Object {$_.Name -notlike '*.WebView2'} | Copy-Item -Destination $phaseRelease -Recurse
        } else {
            Copy-Item -LiteralPath (Join-Path $phaseBuild.word 'Locus.Word.dll'),(Join-Path $phaseBuild.word 'Locus.Core.dll'),tools/m3/register.ps1 -Destination $phaseRelease
        }
        Copy-Item -LiteralPath docs/phase-e/QUICKSTART.md,docs/phase-e/REPORT.md -Destination $phaseRelease
        if($phaseKind -ne 'Word-x86') {
            $phaseLicenses=Join-Path $phaseRelease 'licenses'; New-Item -ItemType Directory -Path $phaseLicenses | Out-Null
            $phaseNuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
            Copy-Item -LiteralPath (Join-Path $phaseNuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/LICENSE.TXT') -Destination (Join-Path $phaseLicenses 'DOTNET-LICENSE.TXT')
            Copy-Item -LiteralPath (Join-Path $phaseNuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $phaseLicenses 'DOTNET-THIRD-PARTY-NOTICES.TXT')
            Copy-Item -LiteralPath tools/sh/node_modules/mathjax/LICENSE -Destination (Join-Path $phaseLicenses 'MATHJAX-APACHE-2.0.txt')
            Copy-Item -LiteralPath tools/sh/node_modules/@mathjax/mathjax-newcm-font/package.json -Destination (Join-Path $phaseLicenses 'MATHJAX-NEWCM-PACKAGE.json')
        }
        $phaseCapability=@{build=$phaseId;kind=$phaseKind;status='review-local';SC1_09='manual-native-verified';SC1_10='pending-host-acceptance';pending=@('D-HOST-01','D-HOST-02','D-CLIP-01','D-FILE-01','SC1-08');network='formula-analysis-local';autoWord=$false}
        $phaseCapability | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $phaseRelease 'capabilities.json') -Encoding utf8NoBOM
        $phaseFiles=@(Get-ChildItem -LiteralPath $phaseRelease -Recurse -File | ForEach-Object {@{path=[IO.Path]::GetRelativePath($phaseRelease,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
        ConvertTo-Json -InputObject $phaseFiles -Depth 5 | Set-Content -LiteralPath (Join-Path $phaseRelease 'manifest.json') -Encoding utf8NoBOM
        $phaseZip=$phaseRelease+'.zip'; if(Test-Path -LiteralPath $phaseZip){throw 'Archive exists'}
        Compress-Archive -LiteralPath $phaseRelease -DestinationPath $phaseZip -CompressionLevel Optimal
        $phasePackages+=@{name=$phaseName;kind=$phaseKind;directory=$phaseRelease;path=$phaseZip;sha256=(Get-FileHash -LiteralPath $phaseZip).Hash.ToLowerInvariant();files=$phaseFiles.Count;bytes=(Get-Item -LiteralPath $phaseZip).Length}
    }
    @{build=$phaseId;status='review-local';packages=$phasePackages} | ConvertTo-Json -Depth 6 | Set-Content artifacts/phase-e/packages.json -Encoding utf8NoBOM
    $phasePackages | Select-Object name,files,bytes | ConvertTo-Json
} finally {Pop-Location}
