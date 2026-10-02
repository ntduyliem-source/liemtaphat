[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $phaseWorkspace
try {
    $phaseBuild=Get-Content artifacts/phase-f/current-build.json -Raw | ConvertFrom-Json
    $phaseId=Split-Path $phaseBuild.root -Leaf
    $phaseAcceptance=Get-Content artifacts/phase-f/host-acceptance.json -Raw | ConvertFrom-Json
    if($phaseAcceptance.build -ne $phaseId -or $phaseAcceptance.status -ne 'PASSED'){throw 'Acceptance must identify the packaged build.'}
    foreach($phaseProof in @($phaseAcceptance.currentReports)+@($phaseAcceptance.baselineReports | ForEach-Object {$_.path})) {
        $phaseResult=Get-Content $phaseProof -Raw | ConvertFrom-Json
        if($phaseResult.failed -ne 0 -or $phaseResult.passed -le 0){throw "Unmet evidence: $phaseProof"}
    }
    foreach($phaseBinary in @('Locus.Word.dll','Locus.Core.dll')) {
        if((Get-FileHash (Join-Path $phaseBuild.word $phaseBinary)).Hash -ne (Get-FileHash (Join-Path tests/Locus.Word.Tests/bin/Release/net48 $phaseBinary)).Hash){throw 'Test binaries differ from build'}
        if((Get-FileHash (Join-Path $phaseBuild.word $phaseBinary)).Hash -ne $phaseAcceptance.binaries.$phaseBinary){throw 'Acceptance binaries differ from build'}
    }
    $phaseName='Locus-F-'+$phaseId+'-review-local-Word-x86'
    $phaseRelease=Join-Path $phaseWorkspace ('artifacts/releases/'+$phaseName)
    $phaseZip=$phaseRelease+'.zip'
    if((Test-Path -LiteralPath $phaseRelease) -or (Test-Path -LiteralPath $phaseZip)){throw 'Immutable package already exists'}
    New-Item -ItemType Directory -Path $phaseRelease | Out-Null
    Copy-Item -LiteralPath (Join-Path $phaseBuild.word 'Locus.Word.dll'),(Join-Path $phaseBuild.word 'Locus.Core.dll'),tools/m3/register.ps1,docs/phase-f/QUICKSTART.md,docs/phase-f/REPORT.md -Destination $phaseRelease
    Copy-Item -LiteralPath artifacts/phase-f/host-acceptance.json -Destination $phaseRelease
    @{build=$phaseId;version='0.5.0';kind='Word-x86';status='review-local';scan='explicit-body-or-selection';panel='native-verified';inline=$phaseAcceptance.inline;pending=$phaseAcceptance.pending;autoWord=$false;maxRegions=64;maxWordPositions=100000;maxGapUtf16=4096} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $phaseRelease 'capabilities.json') -Encoding utf8NoBOM
    $phaseFiles=@(Get-ChildItem -LiteralPath $phaseRelease -File | ForEach-Object {@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}})
    ConvertTo-Json -InputObject $phaseFiles -Depth 4 | Set-Content -LiteralPath (Join-Path $phaseRelease 'manifest.json') -Encoding utf8NoBOM
    Compress-Archive -LiteralPath $phaseRelease -DestinationPath $phaseZip -CompressionLevel Optimal
    $phaseUnpacked=Join-Path $phaseWorkspace ('artifacts/phase-f/unpacked-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    Expand-Archive -LiteralPath $phaseZip -DestinationPath $phaseUnpacked
    $phaseExtracted=Join-Path $phaseUnpacked $phaseName
    foreach($phaseFile in $phaseFiles) {
        $phaseItem=Join-Path $phaseExtracted $phaseFile.path
        if((Get-FileHash -LiteralPath $phaseItem).Hash.ToLowerInvariant() -ne $phaseFile.sha256 -or (Get-Item -LiteralPath $phaseItem).Length -ne $phaseFile.bytes){throw "ZIP mismatch: $($phaseFile.path)"}
    }
    $phaseInfo=@{build=$phaseId;name=$phaseName;directory=$phaseRelease;path=$phaseZip;sha256=(Get-FileHash -LiteralPath $phaseZip).Hash.ToLowerInvariant();bytes=(Get-Item -LiteralPath $phaseZip).Length;files=$phaseFiles.Count}
    $phaseInfo | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath artifacts/phase-f/packages.json -Encoding utf8NoBOM
    @{build=$phaseId;passed=$true;fileHashesChecked=$phaseFiles.Count;unpacked=$phaseExtracted;wordSha256=(Get-FileHash (Join-Path $phaseExtracted 'Locus.Word.dll')).Hash;coreSha256=(Get-FileHash (Join-Path $phaseExtracted 'Locus.Core.dll')).Hash} | ConvertTo-Json | Set-Content -LiteralPath artifacts/phase-f/package-verification.json -Encoding utf8NoBOM
    $phaseInfo | ConvertTo-Json
} finally {Pop-Location}
