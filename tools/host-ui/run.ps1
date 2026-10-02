param([Parameter(Mandatory=$true,Position=0)][string]$Action,[Parameter(Position=1)][string]$Value,[Parameter(Position=2)][string]$Value2,[Parameter(Position=3)][string]$Value3,[Parameter(Position=4)][string]$Value4,[Parameter(Position=5)][string]$Value5,[string]$Receipt='artifacts/host-review/20260928-complete/desktop-receipt.json')
$ErrorActionPreference='Stop'
$hostArgs=@($Receipt,$Action)
foreach($hostValue in @($Value,$Value2,$Value3,$Value4,$Value5)){if($hostValue){$hostArgs+=$hostValue}}
$hostResult=& (Join-Path $PSScriptRoot 'bin/Release/net10.0-windows/Locus.HostUi.exe') @hostArgs
$hostExit=$LASTEXITCODE
$hostText=$hostResult -join "`n"
$hostLog=Join-Path (Split-Path $Receipt -Parent) 'actions.jsonl'
@{utc=[DateTime]::UtcNow.ToString('o');action=$Action;arguments=@($Value,$Value2,$Value3,$Value4);exitCode=$hostExit;result=($hostText | ConvertFrom-Json)} | ConvertTo-Json -Depth 15 -Compress | Add-Content -LiteralPath $hostLog -Encoding utf8NoBOM
$hostText
if($hostExit -ne 0){throw "Native host command failed: $Action"}
