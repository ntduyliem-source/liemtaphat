[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$measureRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $measureRoot
try {
    $measureBuild=Get-Content -LiteralPath artifacts/phase-e/current-build.json -Raw | ConvertFrom-Json
    $measureOutput=Join-Path $measureRoot ('artifacts/phase-e/performance/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.json')
    dotnet run --project tools/phase-e/performance -c Release "-p:DeliveryRoot=$($measureBuild.desktop)" -- $measureOutput
    if($LASTEXITCODE -ne 0){throw 'Performance measurement failed'}
    $measureResult=Get-Content -LiteralPath $measureOutput -Raw | ConvertFrom-Json
    foreach($measureBinary in $measureResult.binaries) {
        $measureExpected=Get-FileHash -LiteralPath (Join-Path $measureBuild.desktop ($measureBinary.name+'.dll'))
        if($measureExpected.Hash -ne $measureBinary.sha256){throw 'Measured binary differs from delivery'}
    }
    @{build=(Split-Path $measureBuild.root -Leaf);report=$measureOutput;binaryHashesMatch=$true;scope='Native pipeline only; no UI latency claim'} | ConvertTo-Json | Set-Content -LiteralPath artifacts/phase-e/performance.json -Encoding utf8NoBOM
    Get-Content -LiteralPath artifacts/phase-e/performance.json
} finally {Pop-Location}
