param([string]$Label='state',[switch]$Close,[switch]$OpenPreview,[switch]$SelectSource)
$ErrorActionPreference='Stop'
if([IntPtr]::Size -ne 4){throw 'Run using Windows PowerShell x86.'}
$manualDirectory=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/m3/native'))
$manualOwned=Get-Content -LiteralPath (Join-Path $manualDirectory 'ownership.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$manualProcess=@(Get-Process WINWORD -ErrorAction SilentlyContinue)
if($manualProcess.Count -ne 1 -or $manualProcess[0].Id -ne $manualOwned.pid -or $manualProcess[0].StartTime.ToUniversalTime().ToString('o') -ne $manualOwned.startTimeUtc){throw 'Owned Word process mismatch.'}
$manualApp=[Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
if($manualApp.Documents.Count -ne 1 -or $manualApp.ActiveDocument.FullName -ne $manualOwned.path){throw 'Owned demo document mismatch; no action taken.'}
$manualConnector=$manualApp.COMAddIns.Item('Locus.Word.Manual').Object
if($SelectSource){
    $manualInitial=Get-Content -LiteralPath (Join-Path $manualDirectory 'release-initial.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $manualRange=$manualApp.ActiveDocument.Content
    if(-not $manualRange.Find.Execute($manualInitial.selectionText)){throw 'Original source is not present in the owned fixture.'}
    $manualRange.Select()
}
if($OpenPreview){[void]$manualConnector.OpenSelectionPreview()}
$manualState=$manualConnector.GetManualState() | ConvertFrom-Json
$manualDocument=$manualApp.ActiveDocument
$manualResult=@{utc=[DateTime]::UtcNow.ToString('o');state=$manualState;text=$manualDocument.Content.Text;selection=@($manualApp.Selection.Start,$manualApp.Selection.End);selectionText=$manualApp.Selection.Text;native=$manualDocument.OMaths.Count;controls=$manualDocument.ContentControls.Count}
if($manualDocument.OMaths.Count -eq 1){$manualResult.nativeText=$manualDocument.OMaths.Item(1).Range.Text}
if($manualDocument.ContentControls.Count -eq 1){
    $manualHash=[Security.Cryptography.SHA256]::Create()
    try{$manualResult.tagSha256=[Convert]::ToBase64String($manualHash.ComputeHash([Text.Encoding]::Unicode.GetBytes($manualDocument.ContentControls.Item(1).Tag)))}finally{$manualHash.Dispose()}
}
$manualJson=$manualResult | ConvertTo-Json -Depth 12
if($Label -notmatch '^[a-zA-Z0-9-]+$'){throw 'Invalid artifact label.'}
$manualJson | Set-Content -LiteralPath (Join-Path $manualDirectory ($Label+'.json')) -Encoding UTF8
$manualJson
if($Close){$manualConnector.CancelPreview();$manualDocument.Close(0);$manualApp.Quit(0)}
