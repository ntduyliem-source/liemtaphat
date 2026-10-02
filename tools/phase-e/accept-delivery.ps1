[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$deliveryWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $deliveryWorkspace
try {
    function Read-DeliveryJson([string]$Path) {Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json}
    $deliveryBuild=Read-DeliveryJson 'artifacts/phase-e/current-build.json'
    $deliveryId=Split-Path $deliveryBuild.root -Leaf
    $deliveryPackages=Read-DeliveryJson 'artifacts/phase-e/packages.json'
    $deliveryVerified=Read-DeliveryJson 'artifacts/phase-e/package-verification.json'
    $deliveryWord=Read-DeliveryJson 'artifacts/phase-e/delivery-word.json'
    $deliveryDesktop=Read-DeliveryJson 'artifacts/phase-e/passive-fix/desktop-startup.json'
    $deliveryWeb=Read-DeliveryJson 'artifacts/phase-e/passive-fix/browser-checks.json'
    $deliveryUnpacked=Read-DeliveryJson 'artifacts/phase-e/passive-fix/web-unpacked.json'
    $deliveryPerformance=Read-DeliveryJson 'artifacts/phase-e/performance.json'
    foreach($deliveryReceipt in @($deliveryPackages,$deliveryVerified,$deliveryWord,$deliveryDesktop,$deliveryWeb,$deliveryUnpacked,$deliveryPerformance)) {
        if($deliveryReceipt.build -ne $deliveryId){throw 'Delivery receipt belongs to another build'}
    }
    if($deliveryVerified.failed -ne 0 -or $deliveryVerified.passed -ne 3){throw 'Package verification incomplete'}
    $deliveryBinaries=@()
    foreach($deliveryName in @('Locus.Word.dll','Locus.Core.dll')) {
        $deliveryHash=(Get-FileHash -LiteralPath (Join-Path $deliveryBuild.word $deliveryName)).Hash
        if($deliveryHash -ne (Get-FileHash -LiteralPath (Join-Path 'tests/Locus.Word.Tests/bin/Release/net48' $deliveryName)).Hash){throw 'Native-tested binary differs'}
        $deliveryBinaries+=@{name=$deliveryName;sha256=$deliveryHash}
    }
    if($deliveryWord.wordSha256 -ne $deliveryBinaries[0].sha256 -or $deliveryWord.coreSha256 -ne $deliveryBinaries[1].sha256){throw 'Registered Word receipt differs'}
    foreach($deliveryPackage in $deliveryPackages.packages) {
        if((Get-FileHash -LiteralPath $deliveryPackage.path).Hash -ne $deliveryPackage.sha256){throw 'Archive changed since verification'}
    }
    $deliveryEvidence=@(
        'passive-fix/contracts/report.json','passive-fix/science/core-verification.json','passive-fix/science/application-verification.json',
        'passive-fix/content/content-tests.json','passive-fix/native/report.json','passive-fix/native-chem-direct/report.json',
        'passive-fix/browser-checks.json','passive-fix/web-unpacked.json','passive-fix/desktop-startup.json',
        'package-verification.json','delivery-word.json','performance.json'
    ) | ForEach-Object {
        $deliveryPath=Join-Path 'artifacts/phase-e' $_
        @{path=$_;sha256=(Get-FileHash -LiteralPath $deliveryPath).Hash}
    }
    $deliveryResult=@{
        capturedAtUtc=[DateTime]::UtcNow.ToString('o');build=$deliveryId;status='REVIEW_LOCAL_DELIVERED_HOST_GATES_OPEN'
        progress=@{SC1_09='DONE_MANUAL_SCOPE';SC1_10='DOING';completed=8;total=10;percentageScope='Task count within SC1, not overall project'}
        web=@{url='http://127.0.0.1:4191/';unpackedUrl='http://127.0.0.1:4192/';status=$deliveryWeb.status;extracted=$deliveryUnpacked.status}
        desktop=@{status=$deliveryDesktop.status;processReceipt='delivery-desktop-final-process.json'}
        word=@{status=$deliveryWord.status;codeBase=$deliveryWord.codeBase;binaries=$deliveryBinaries}
        packages=$deliveryPackages.packages;evidence=$deliveryEvidence
        retainedEvidence=@{transactions='transactions-20260915-232727/report.json';scope='Nine Word rollback cases on previous E build; Word transaction code unchanged. Not a new execution on current DLL.'}
        pending=@('D-HOST-01','D-HOST-02','D-CLIP-01','D-FILE-01','SC1-08','UI/WASM/Word latency and startup measurements')
        nativeRunCaveat='Final broad native run: 6 PASS / 1 FAIL after source selection/focus changed; isolated direct chemistry case: 1 PASS. Original failure report retained.'
        D_WORD_01='PASS; see artifacts/doc1/verification/word-final-20260915-232937/report.json'
    }
    $deliveryResult | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath artifacts/phase-e/delivery.json -Encoding utf8NoBOM
    $deliveryResult | Select-Object build,status,progress,pending | ConvertTo-Json -Depth 4
} finally {Pop-Location}
