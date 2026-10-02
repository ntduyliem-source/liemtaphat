[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $phaseWorkspace
try {
    $phasePackages=Get-Content artifacts/phase-e/packages.json -Raw | ConvertFrom-Json
    $phaseRun=Join-Path $phaseWorkspace ('artifacts/phase-e/unpacked-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
    $phaseResults=@()
    foreach($phasePackage in $phasePackages.packages) {
        if((Get-FileHash -LiteralPath $phasePackage.path).Hash.ToLowerInvariant() -ne $phasePackage.sha256){throw 'Archive hash changed'}
        Expand-Archive -LiteralPath $phasePackage.path -DestinationPath $phaseRun
        $phaseRoot=[IO.Path]::GetFullPath((Join-Path $phaseRun $phasePackage.name))
        $phaseManifest=Get-Content -LiteralPath (Join-Path $phaseRoot 'manifest.json') -Raw | ConvertFrom-Json
        foreach($phaseFile in $phaseManifest) {
            $phaseTarget=[IO.Path]::GetFullPath((Join-Path $phaseRoot $phaseFile.path))
            if(-not $phaseTarget.StartsWith($phaseRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Manifest path escaped package'}
            if((Get-Item -LiteralPath $phaseTarget).Length -ne $phaseFile.bytes -or (Get-FileHash -LiteralPath $phaseTarget).Hash.ToLowerInvariant() -ne $phaseFile.sha256){throw "Package file mismatch: $($phaseFile.path)"}
        }
        if((Get-ChildItem -LiteralPath $phaseRoot -Recurse -File).Count -ne $phaseManifest.Count+1){throw 'Unlisted package file'}
        $phaseResults+=@{kind=$phasePackage.kind;status='PASS';files=$phaseManifest.Count;root=$phaseRoot}
    }
    @{capturedAtUtc=[DateTime]::UtcNow.ToString('o');build=$phasePackages.build;passed=$phaseResults.Count;failed=0;scope='ZIP extraction and all file hashes; host interaction acceptance separate';results=$phaseResults} | ConvertTo-Json -Depth 6 | Set-Content artifacts/phase-e/package-verification.json -Encoding utf8NoBOM
    $phaseResults | ConvertTo-Json
} finally {Pop-Location}
