[CmdletBinding()]
param([switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $workspaceRoot
try {
    if (-not $NoBuild) {
        dotnet build tests/Locus.RuntimeProbe -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Runtime probe build failed.' }
    }
    $outputRoot = Join-Path $workspaceRoot 'tests/Locus.RuntimeProbe/bin/Release'
    $modernOutput = @(dotnet (Join-Path $outputRoot 'net10.0/Locus.RuntimeProbe.dll'))
    if ($LASTEXITCODE -ne 0) { throw 'Modern runtime failed.' }
    $frameworkExe = Join-Path $outputRoot 'net48/Locus.RuntimeProbe.exe'
    $frameworkOutput = @(& $frameworkExe)
    if ($LASTEXITCODE -ne 0) { throw '.NET Framework runtime failed.' }
    $modernHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $outputRoot 'net10.0/Locus.Core.dll')).Hash
    $frameworkHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $outputRoot 'net48/Locus.Core.dll')).Hash
    $sameDll = $modernHash -ceq $frameworkHash
    $sameResults = ($modernOutput -join "`n") -ceq ($frameworkOutput -join "`n")
    $report = [ordered]@{schemaVersion='locus-m1-runtime-check/1';capturedAtUtc=[DateTime]::UtcNow.ToString('o');status=$(if($sameDll -and $sameResults){'PASS'}else{'FAIL'});sameAssembly=$sameDll;sameResults=$sameResults;sha256=$modernHash;sampleCount=13;outputLineCount=$modernOutput.Count;modernOutput=$modernOutput;frameworkOutput=$frameworkOutput;limits=@('Real core in two console hosts; no installed VSTO/Desktop UI or Word writes.','Tests candidate identity, structure exporters, normalization and snapshot roundtrip on 13 authored samples.')}
    $artifactDir = Join-Path $workspaceRoot 'artifacts/m1'
    [void](New-Item -ItemType Directory -Force -Path $artifactDir)
    $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $artifactDir 'runtime-check.json') -Encoding utf8
    if (-not ($sameDll -and $sameResults)) { throw 'Runtime outputs or core DLL hashes differ; see runtime-check.json.' }
    Write-Output "Shared production core: same DLL and results across .NET 10 / .NET Framework 4.8 x86 ($($modernOutput.Count) output lines)."
} finally { Pop-Location }
