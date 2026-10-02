[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$balReceiptWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $balReceiptWorkspace
try {
    $balReceiptBuild=Get-Content -LiteralPath 'artifacts/bal1/current-build.json' -Raw | ConvertFrom-Json
    $balReceiptId=Split-Path $balReceiptBuild.root -Leaf
    foreach($balCheck in @('artifacts/bal1/verification/balance-tests.json','artifacts/bal1/regression-content/content-tests.json','artifacts/bal1/regression/application-tests.json')) {
        $balResult=Get-Content -LiteralPath $balCheck -Raw | ConvertFrom-Json
        if($balResult.status -ne 'PASSED' -or $balResult.failed -ne 0){throw "Incomplete verification: $balCheck"}
    }
    $balBrowser=Get-Content -LiteralPath 'artifacts/bal1/verification/browser-checks.json' -Raw | ConvertFrom-Json
    if($balBrowser.deliveryBuild -ne $balReceiptId){throw 'Browser observations target a different build'}
    function Get-BalHashes([string[]]$Paths) {
        foreach($balPath in $Paths){[ordered]@{path=$balPath;sha256=(Get-FileHash -LiteralPath $balPath -Algorithm SHA256).Hash.ToLowerInvariant()}}
    }
    $balSourcePaths=@(
        'src/Locus.Application/ContentBalanceWire.cs','src/Locus.Application/AnalysisScheduling.cs',
        'src/Locus.Application/ContentDocument.cs','src/Locus.Application/ContentHistory.cs',
        'src/Locus.Application/FormulaSession.cs','src/Locus.Application/FormulaSession.Content.cs',
        'src/Locus.Application/FormulaSession.Balance.cs','src/Locus.Application/FormulaSession.ContentProducts.cs',
        'src/Locus.Application/FormulaSession.Assistance.cs','src/Locus.Application/WorkspaceDocument.cs','src/Locus.Application/EditorPreferences.cs',
        'src/Locus.Editor/BrowserAnalysisScheduler.cs','src/Locus.Editor/Workspace.razor','src/Locus.Editor/Workspace.Core.cs',
        'src/Locus.Editor/Workspace.Content.cs','src/Locus.Editor/Workspace.Balance.cs','src/Locus.Editor/Workspace.Assistance.cs',
        'src/Locus.Editor/Workspace.Ghost.cs','src/Locus.Editor/Workspace.razor.cs',
        'src/Locus.Editor/wwwroot/editor.js','src/Locus.Editor/wwwroot/chemistry-ghost.js','src/Locus.Editor/wwwroot/ux1.css',
        'tests/Locus.Application.Tests/BalanceVerification.cs','tests/Locus.Application.Tests/Program.cs',
        'tools/bal1/build.ps1','tools/bal1/local.ps1','tools/bal1/receipt.ps1'
    )
    $balEvidencePaths=@(
        'artifacts/bal1/current-build.json','artifacts/bal1/final-build.log',
        'artifacts/bal1/verification/balance-tests.json','artifacts/bal1/verification/browser-checks.json',
        'artifacts/bal1/verification/products-history.locus','artifacts/bal1/verification/mixed-balanced.locus',
        'artifacts/bal1/regression-content/content-tests.json','artifacts/bal1/regression/application-tests.json','artifacts/bal1/regression/core-contracts.json',
        'docs/bal1/REPORT.md','docs/bal1/QUICKSTART.md','docs/doc1/MODEL.md','docs/ux1/BALANCE-INTERACTION.md',
        'docs/NEXT-STEPS.md','docs/BACKLOG.md','docs/ROADMAP.md','README.md'
    )
    $balPublishedPaths=@('Locus.Application.dll','Locus.Editor.dll','Locus.Core.dll','Locus.Desktop.Shared.dll') | ForEach-Object {"artifacts/bal1/builds/$balReceiptId/desktop/$_"}
    $balReceipt=[ordered]@{
        schema='locus-phase-c-delivery/1';capturedUtc=[DateTime]::UtcNow.ToString('o');build=$balReceiptId
        status='COMPLETED_SCOPED_C';tasks=@('BAL1-01','BAL1-02','BAL1-03','BAL1-04');next='DOC1-03'
        scope='Shared balance commands/model/editor and Web UI; Desktop published. Native clipboard/IME/tray, real host file transfer and Word remain their existing tasks.'
        sources=@(Get-BalHashes $balSourcePaths);evidence=@(Get-BalHashes $balEvidencePaths);desktopAssemblies=@(Get-BalHashes $balPublishedPaths)
        previousBaselines=@(Get-BalHashes @('artifacts/sc1/current-build.json','artifacts/ux1/current-build.json','artifacts/ux1/verification/receipt.json'))
    }
    $balReceipt | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath 'artifacts/bal1/verification/receipt.json' -Encoding utf8NoBOM
    [ordered]@{status=$balReceipt.status;build=$balReceiptId;next=$balReceipt.next;sources=$balReceipt.sources.Count;evidence=$balReceipt.evidence.Count} | ConvertTo-Json
} finally {Pop-Location}
