$ErrorActionPreference='Stop'
$manualWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manualNative=Join-Path $manualWorkspace 'artifacts/m3/native'
function Read-ManualEvidence([string]$Name){Get-Content -LiteralPath (Join-Path $manualNative ($Name+'.json')) -Raw -Encoding UTF8 | ConvertFrom-Json}
$manualInitial=Read-ManualEvidence 'release-initial'
$manualPreview=Read-ManualEvidence 'release-preview'
$manualConvert=Read-ManualEvidence 'release-convert'
$manualTyping=Read-ManualEvidence 'release-typing'
$manualUndoTyping=Read-ManualEvidence 'release-undo-typing'
$manualUndo=Read-ManualEvidence 'release-undo-convert'
$manualRedo=Read-ManualEvidence 'release-redo'
$manualManaged=Read-ManualEvidence 'release-managed'
$manualRestore=Read-ManualEvidence 'release-restore'
$manualRibbon=Read-ManualEvidence 'ribbon-preview'
$manualCancel=Read-ManualEvidence 'ribbon-cancel'
$manualChecks=@(
    @{id='api/preview-from-selection';pass=($manualInitial.text -ceq $manualPreview.text -and $manualPreview.native -eq 0 -and $manualPreview.controls -eq 0 -and $manualPreview.state.mode -eq 'source' -and $manualPreview.state.candidateId -ceq $manualPreview.state.previewCandidateId)}
    @{id='keyboard/confirm-native';pass=($manualConvert.state.message -eq 'converted' -and $manualConvert.native -eq 1 -and $manualConvert.controls -eq 1 -and $manualConvert.state.input.EditorFocus)}
    @{id='keyboard/type-outside-equation';pass=($manualTyping.nativeText -ceq $manualConvert.nativeText -and $manualTyping.tagSha256 -ceq $manualConvert.tagSha256 -and $manualTyping.text -cne $manualConvert.text -and $manualTyping.native -eq 1 -and $manualTyping.controls -eq 1)}
    @{id='keyboard/undo-typing';pass=($manualUndoTyping.text -ceq $manualConvert.text -and $manualUndoTyping.tagSha256 -ceq $manualConvert.tagSha256 -and $manualUndoTyping.native -eq 1)}
    @{id='keyboard/undo-convert';pass=($manualUndo.text -ceq $manualInitial.text -and $manualUndo.native -eq 0 -and $manualUndo.controls -eq 0 -and ($manualUndo.selection -join ',') -ceq ($manualInitial.selection -join ','))}
    @{id='keyboard/redo';pass=($manualRedo.text -ceq $manualConvert.text -and $manualRedo.tagSha256 -ceq $manualConvert.tagSha256 -and $manualRedo.native -eq 1)}
    @{id='keyboard/open-saved-snapshot';pass=($manualManaged.state.mode -eq 'managed' -and $manualManaged.state.source -ceq $manualPreview.state.source -and $manualManaged.state.selectedCandidateId -ceq $manualPreview.state.candidateId)}
    @{id='keyboard/restore-exact-source';pass=($manualRestore.text -ceq $manualInitial.text -and $manualRestore.native -eq 0 -and $manualRestore.controls -eq 0 -and $manualRestore.state.message -eq 'restored')}
    @{id='keyboard/ribbon-source-preview';pass=($manualRibbon.text -ceq $manualInitial.text -and $manualRibbon.state.mode -eq 'source' -and $manualRibbon.state.source -ceq $manualInitial.selectionText -and $manualRibbon.state.candidateId -ceq $manualPreview.state.candidateId -and $manualRibbon.state.candidateId -ceq $manualRibbon.state.previewCandidateId)}
    @{id='keyboard/cancel-preview';pass=($manualCancel.text -ceq $manualInitial.text -and $manualCancel.state.sessionId -eq $null -and -not $manualCancel.state.panelVisible -and $manualCancel.native -eq 0 -and $manualCancel.controls -eq 0)}
)
$manualFiles=@('release-initial','release-preview','release-convert','release-typing','release-undo-typing','release-undo-convert','release-redo','release-managed','release-restore','ribbon-preview','ribbon-cancel')
$manualResults=@($manualChecks | ForEach-Object {@{id=$_.id;status=$(if($_.pass){'PASS'}else{'FAIL'})}})
$manualFailed=@($manualChecks | Where-Object {-not $_.pass}).Count
$manualReport=@{
    schemaVersion='locus-m3-native-verification/1';capturedAtUtc=[DateTime]::UtcNow.ToString('o');passed=$manualChecks.Count-$manualFailed;failed=$manualFailed;results=$manualResults
    method='The packaged entry point opens preview through its public COM method. Native keyboard through Computer Use confirms, types, undoes, redoes, opens the managed formula and restores; read-only Word COM records assertions. The fixture is synthetic.'
    ribbon='Separate source-preview/cancel and managed-open checks use F10, L, C, then C or M. Selection setup uses the owned Word fixture.'
    assemblies=@('Locus.Word.dll','Locus.Core.dll') | ForEach-Object {@{name=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $manualWorkspace ('artifacts/releases/Locus-Word-0.3.0-alpha-x86/'+$_))).Hash}}
    files=@($manualFiles | ForEach-Object {@{path=('artifacts/m3/native/'+$_+'.json');sha256=(Get-FileHash -LiteralPath (Join-Path $manualNative ($_+'.json'))).Hash}})
    limits=@('No physical pointer-click or Windows screenshot acceptance; capture/click tools fail on this host.','Literal Unicode input is not a new Telex/VNI acceptance run. W0 IME evidence remains separate.','Native Redo restores document and metadata; the caret may be at the start of the equation. Move inside before opening a saved formula.')
}
$manualReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $manualNative 'verification.json') -Encoding UTF8
if($manualFailed -ne 0){throw "Native verification failed: $manualFailed"}
Write-Output "M3 native keyboard: $($manualChecks.Count) PASS."
