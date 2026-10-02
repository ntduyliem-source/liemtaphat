[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$native=Join-Path $root 'artifacts/w0/native'
$cases=[Collections.Generic.List[object]]::new()
function Read-Evidence([string]$name){Get-Content -LiteralPath (Join-Path $native ($name+'.json')) -Raw|ConvertFrom-Json}
function Check([bool]$condition,[string]$message){if(-not $condition){throw $message}}
function Test-Case([string]$id,[scriptblock]$check){
    try{& $check;$cases.Add([ordered]@{id=$id;status='PASS'})}
    catch{$cases.Add([ordered]@{id=$id;status='FAIL';error=$_.Exception.Message})}
}
Test-Case 'native-live/telex-whole-source-and-incomplete-tail' {
    $missing=Read-Evidence 'trial-b-fixed-incomplete'
    $squared=Read-Evidence 'trial-b-fixed-squared'
    $plus=Read-Evidence 'trial-b-fixed-plus-incomplete'
    $complete=Read-Evidence 'trial-b-fixed-complete'
    $inspection=Read-Evidence 'trial-b-fixed-inspect'
    Check ($missing.spaceTrial.raw -ceq 'x mũ ') 'Missing exponent source differs.'
    Check ($missing.spaceTrial.nativeUpdates -eq 1 -and $plus.spaceTrial.nativeUpdates -eq 2) 'Incomplete source was converted.'
    Check ($squared.spaceTrial.raw -ceq 'x mũ 2 ') 'Squared source differs.'
    Check ($complete.spaceTrial.active -and $complete.nativeCount -eq 1 -and $complete.controlCount -eq 1 -and $complete.spaceTrial.nativeUpdates -eq 3) 'Combined native shape differs.'
    Check ($complete.spaceTrial.raw -ceq 'x mũ 2 cộng 1 ') 'Whole source was split.'
    Check ($inspection.inspection[0].valid -and $inspection.inspection[0].source -ceq 'x mũ 2 cộng 1 ') 'Native metadata validation failed.'
}
Test-Case 'native-live/keyboard-undo-redo' {
    $undo=Read-Evidence 'trial-b-fixed-undo';$redo=Read-Evidence 'trial-b-fixed-redo-inspect'
    Check (-not $undo.spaceTrial.active -and $undo.nativeCount -eq 1 -and $undo.raw.Contains('cộng 1 ')) 'Undo lost typed tail or left trial active.'
    Check ($redo.inspection[0].valid -and $redo.inspection[0].source -ceq 'x mũ 2 cộng 1 ') 'Redo lost the selected source/native.'
}
Test-Case 'native-live/fraction-and-source-restore' {
    $missing=Read-Evidence 'trial-b-third-incomplete';$full=Read-Evidence 'trial-b-fraction-fixed';$restore=Read-Evidence 'trial-b-fraction-restored'
    Check ($missing.spaceTrial.active -and $missing.spaceTrial.raw -ceq '1 trên ' -and $missing.spaceTrial.nativeUpdates -eq 1) 'Incomplete fraction changed or stopped.'
    Check ($full.spaceTrial.active -and $full.nativeCount -eq 1 -and $full.spaceTrial.candidates[0].latex -ceq '\frac{1}{2}') 'Fraction native differs.'
    Check ($restore.outcome -eq 'PASS' -and $restore.after.nativeCount -eq 0 -and $restore.after.controlCount -eq 0 -and $restore.after.raw -ceq "Source: 1 trên 2  | Outside remains.`r`r") 'Original source restore differs.'
}
Test-Case 'native-live/root-repair-never-auto-selected' {
    $blocked=Read-Evidence 'trial-b-root-repair-blocked';$inspect=Read-Evidence 'trial-b-root-inspect'
    Check ($blocked.spaceTrial.active -and $blocked.spaceTrial.nativeUpdates -eq 1 -and $blocked.spaceTrial.raw -ceq 'căn x cộng 1 ') 'Repair modified native or lost session.'
    Check ($blocked.spaceTrial.contentEligibility -eq 'blocked' -and $blocked.spaceTrial.candidates[0].kind -eq 'direct' -and $blocked.spaceTrial.candidates[1].kind -eq 'repair') 'Candidate labels or policy differ.'
    Check ($inspect.inspection[0].valid -and $inspect.inspection[0].source -ceq 'căn x cộng 1 ' -and $inspect.after.nativeCount -eq 1) 'Explicit choice native/source differs.'
}
Test-Case 'keep-source/no-native-until-explicit-commit' {
    $source=Read-Evidence 'trial-a-complete-source';$inspect=Read-Evidence 'trial-a-inspect'
    Check ($source.nativeCount -eq 0 -and $source.controlCount -eq 0 -and $source.spaceTrial.raw -ceq 'x mũ 2 cộng 1 ') 'A replaced source before explicit choice.'
    Check ($inspect.inspection[0].valid -and $inspect.inspection[0].source -ceq 'x mũ 2 cộng 1 ' -and $inspect.after.nativeCount -eq 1) 'A commit native/source differs.'
}
Test-Case 'keyboard/escape-and-enter-preserve-word-action' {
    $escape=Read-Evidence 'trial-escape-kept';$enter=Read-Evidence 'trial-enter-kept'
    Check (-not $escape.spaceTrial.active -and $escape.nativeCount -eq 0 -and $escape.raw -ceq "Source: 1 trên | Outside remains.`r`r" -and $escape.input.Escapes -gt 0) 'Esc did not stop while keeping source.'
    Check (-not $enter.spaceTrial.active -and $enter.nativeCount -eq 0 -and $enter.raw -ceq "Source: x mũ 2`r | Outside remains.`r`r" -and $enter.input.Returns -gt 0) 'Enter swallowed paragraph or committed automatically.'
}
$names=@('trial-b-fixed-incomplete','trial-b-fixed-squared','trial-b-fixed-plus-incomplete','trial-b-fixed-complete','trial-b-fixed-inspect','trial-b-fixed-undo','trial-b-fixed-redo-inspect','trial-b-third-incomplete','trial-b-fraction-fixed','trial-b-fraction-restored','trial-b-root-repair-blocked','trial-b-root-inspect','trial-a-complete-source','trial-a-inspect','trial-escape-kept','trial-enter-kept','space-trial-keys-resumed','space-observations-final')
$files=@(foreach($name in $names){$path=Join-Path $native ($name+'.json');[ordered]@{path='artifacts/w0/native/'+$name+'.json';sha256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}})
$failed=@($cases|Where-Object status -eq 'FAIL').Count
$report=[ordered]@{schemaVersion='locus-w0-space-observations/1';capturedAtUtc=[DateTime]::UtcNow.ToString('o');passed=$cases.Count-$failed;failed=$failed;cases=$cases;evidence=$files;limits=@('Agent-operated Windows input in owned Word documents, not participant testing.','Power continuation used individual Telex keys; later fraction/root/A used literal Unicode input plus native Space keys, not composition proof.','The Telex/Undo check preceded the final observer reentrancy guard. Later keyboard checks and the full automated suite cover the later build.','The first segment of the corrected Telex key log was lost when the tool session reset; its Word snapshots remain.','All these Windows observations preceded the cancelled-close expiry guard; that guard has a separate assertion in the final seven-group UX suite.','Enter/Esc stop the research session without an extra commit; this is not an approved production UX decision.','Screenshots unavailable: SetIsBorderRequired 0x80004002. No screenshot or multiple-display acceptance is claimed.')}
$report|ConvertTo-Json -Depth 9|Set-Content -LiteralPath (Join-Path $native 'space-verification.json') -Encoding utf8
Write-Output "Native Space research: $($report.passed) PASS / $failed FAIL."
if($failed){throw 'One or more recorded observations failed validation.'}
