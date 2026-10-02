$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $root
try {
    $evidence='artifacts/host-review/20260928-complete'
    $final=Join-Path $evidence 'word-final-ui'
    $build=Get-Content artifacts/phase-f/current-build.json -Raw | ConvertFrom-Json
    function Read-Proof([string]$Name){Get-Content (Join-Path $final ($Name+'.json')) -Raw | ConvertFrom-Json}
    $initial=Read-Proof 'ready';$fx=Read-Proof 'first-fx';$click=Read-Proof 'fx-click'
    $cancel=Read-Proof 'escape-cancel';$converted=Read-Proof 'completed';$undo=Read-Proof 'undo'
    $checks=@(
        @{name='verified-microsoft-word-x86';passed=($initial.path -like '*Microsoft Office*Office16')},
        @{name='first-fx-at-source';passed=($fx.scan.badge.visible -and $fx.scan.badge.bounds.Top -eq $fx.scan.badge.formula.Top -and $fx.scan.badge.bounds.Left -ge $fx.scan.badge.formula.Right)},
        @{name='physical-fx-opens-four-region-panel';passed=($click.scan.visible -and !$click.scan.stale -and $click.scan.regions.Count -eq 4 -and $click.math -eq 0)},
        @{name='physical-escape-cancels-before-write';passed=($cancel.math -eq 0 -and $cancel.controls -eq 0 -and $cancel.text -ceq $initial.text -and $cancel.scan.stale)},
        @{name='physical-batch-four-native-equations';passed=($converted.math -eq 4 -and $converted.controls -eq 4 -and $converted.scan.message -eq 'completed:4' -and !$converted.scan.stale)},
        @{name='physical-one-undo-restores-exact-source';passed=($undo.math -eq 0 -and $undo.controls -eq 0 -and $undo.text -ceq $initial.text)}
    )
    $failed=@($checks | Where-Object {!$_.passed}).Count
    if($failed){throw 'Word native UI evidence did not meet its declared checks.'}
    $id=Split-Path $build.root -Leaf
    $report=@{build=$id;status='PASSED';recordedUtc=[DateTime]::UtcNow.ToString('o');passed=$checks.Count;failed=$failed;checks=$checks;host=@{path=$initial.path;version=$initial.version;build=$initial.build;dpi=96};scope='Receipt-pinned physical Windows UI actions; Word COM used only to create the synthetic fixture and observe results. Single monitor at 96 DPI.'}
    $report | ConvertTo-Json -Depth 7 | Set-Content (Join-Path $final 'report.json') -Encoding utf8NoBOM
    $current=@('word-final-regression/contracts/report.json','word-final-regression/native/report.json','word-final-regression/smart/report.json','word-final-ui/report.json') | ForEach-Object {Join-Path $evidence $_}
    foreach($path in $current){$proof=Get-Content $path -Raw | ConvertFrom-Json;if($proof.failed -ne 0 -or $proof.passed -le 0){throw "Unmet evidence: $path"}}
    $binaries=@{};foreach($name in @('Locus.Word.dll','Locus.Core.dll')){$binaries[$name]=(Get-FileHash (Join-Path $build.word $name)).Hash}
    @{build=$id;status='PASSED';binaries=$binaries;currentReports=@($current);baselineReports=@(@{build='20260917-010535-286';path='artifacts/phase-f/transactions-delivery/report.json';scope='8 rollback/transaction cases on the unchanged transaction implementation; not rerun for the UI patch.'});inline='experimental-96dpi-local-verified';pending=@('WD1-02-multiple-highlights-and-DPI-matrix','WD1-05-multiple-monitors-and-IME-during-commit','W0-A-B-user-choice');autoWord=$false} | ConvertTo-Json -Depth 7 | Set-Content artifacts/phase-f/host-acceptance.json -Encoding utf8NoBOM
    $report | ConvertTo-Json -Depth 5
} finally {Pop-Location}
