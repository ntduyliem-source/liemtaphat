# Optional alternative-metadata experiment. Loaded by the M0 runner's Tag suite.
# Success here only proves the tested ContentControl.Tag payload on this host.
# Failure must not be worked around by omitting candidate/source/version fields.

function New-TagSnapshotPayload {
    param($Fixture, [int]$TargetLength)
    $snapshot = [ordered]@{
        schemaVersion = 'm0-tag-snapshot-1'
        coreVersion = 'fixed-fixture-1'
        source = [string]$Fixture.source
        selectedCandidateId = [string]$Fixture.selectedCandidateId
        candidates = @($Fixture.candidates)
        padding = ''
    }
    $base = ConvertTo-Json -InputObject $snapshot -Depth 100 -Compress
    if ($TargetLength -gt $base.Length) { $snapshot.padding = '.' * ($TargetLength - $base.Length) }
    return ConvertTo-Json -InputObject $snapshot -Depth 100 -Compress
}

function Test-TagMetadata {
    param($Fixture)
    foreach ($targetLength in @(0,1024,4096,16384,65536)) {
        $label = if ($targetLength -eq 0) { 'small-complete-snapshot' } else { [string]$targetLength }
        $id = 'tag.' + $label
        $doc = $null
        try {
            Set-ProbeStage $id 'create-synthetic-tag-snapshot'
            $info = New-FixtureDocument $Fixture $id; $doc = $info.Document
            $payload = New-TagSnapshotPayload $Fixture $targetLength
            $candidate = Get-SelectedCandidate $Fixture
            $doc.UndoClear(); $before = Get-DocSnapshot $doc
            $range = Find-ProbeRange $doc.Content $Fixture.source
            $undo = $script:Word.UndoRecord; $recording = $false
            $math = $null; $mathRange = $null; $cc = $null
            $writeError = $null; $readBack = $null
            try {
                $undo.StartCustomRecord('Locus M0 tag snapshot'); $recording = $true
                $range.InsertXML((New-OoxmlPackage $candidate.omml))
                $math = $doc.OMaths.Item(1); $mathRange = $math.Range
                $cc = $doc.ContentControls.Add(0, $mathRange)
                $cc.Title = 'Locus M0 tag snapshot'
                try { $cc.Tag = $payload; $readBack = [string]$cc.Tag }
                catch { $writeError = Get-ErrorDetails $_ }
            } finally {
                if ($recording) { $undo.EndCustomRecord() }
                Release-Com $range; Release-Com $math; Release-Com $mathRange; Release-Com $cc; Release-Com $undo
            }
            $receivedLength = if ($null -eq $readBack) { $null } else { $readBack.Length }
            $roundTrip = $null -eq $writeError -and $readBack -ceq $payload
            Assert-Observation ($id + '.write-readback') $roundTrip 'The complete source, candidate set and version JSON must round-trip exactly through ContentControl.Tag; no truncation or field omission is accepted.' @{targetUtf16Length=$targetLength;actualUtf16Length=$payload.Length;utf8Bytes=[Text.Encoding]::UTF8.GetByteCount($payload);payload=$payload} @{receivedUtf16Length=$receivedLength;payload=$readBack;writeError=$writeError}
            $converted = Get-DocSnapshot $doc
            $undoReturned = [bool]$doc.Undo(1)
            $undone = Get-DocSnapshot $doc
            Assert-Observation ($id + '.single-undo') ($undoReturned -and (Test-SameDocumentState $before $undone)) 'One Undo restores source and removes equation/control including any accepted or rejected tag write.' $before @{returned=$undoReturned;snapshot=$undone}
            $redoReturned = [bool]$doc.Redo(1)
            $redone = Get-DocSnapshot $doc
            Assert-Observation ($id + '.single-redo') ($redoReturned -and (Test-SameDocumentState $converted $redone)) 'One Redo reproduces the complete observed post-write native/control/tag state.' $converted @{returned=$redoReturned;snapshot=$redone}
            $path = Join-Path $script:RunDirectory ('tag-' + $label + '.docx')
            Save-ProbeDocument $doc $path
            Close-ProbeDocument $doc; $doc = $null
            $doc = Open-ProbeDocument $path
            if ($roundTrip -and $doc.ContentControls.Count -eq 1) {
                $cc = $doc.ContentControls.Item(1)
                try { $savedTag = [string]$cc.Tag } finally { Release-Com $cc }
                $parsed = $savedTag | ConvertFrom-Json
                $savedCandidates = ConvertTo-Json -InputObject @($parsed.candidates) -Depth 100 -Compress
                $expectedCandidates = ConvertTo-Json -InputObject @($Fixture.candidates) -Depth 100 -Compress
                Assert-Observation ($id + '.save-reopen') ($savedTag -ceq $payload -and $doc.OMaths.Count -eq 1 -and $parsed.source -ceq $Fixture.source -and $savedCandidates -ceq $expectedCandidates) 'The complete accepted JSON snapshot and native equation survive DOCX save/reopen.' @{payload=$payload;nativeCount=1} @{payload=$savedTag;nativeCount=[int]$doc.OMaths.Count}
            } else {
                Record-Test ($id + '.save-reopen') 'UNTESTED' 'Payload persistence is not claimed when the initial exact write failed or Redo did not restore a control.' @{writeRoundTrip=$roundTrip;controlCount=[int]$doc.ContentControls.Count}
            }
        } catch { Record-Test ($id + '.exception') 'FAIL' 'Complete the tag metadata experiment without suppressing errors.' (Get-ErrorDetails $_) }
        finally { if ($null -ne $doc) { Close-ProbeDocument $doc } }
    }
}

function Convert-TagEquation {
    param($Info, $Fixture, [string]$Payload, [string]$FaultAfter='None')
    $doc=$Info.Document; $candidate=Get-SelectedCandidate $Fixture
    $source=Find-ProbeRange $doc.Content $Fixture.source
    $undo=$script:Word.UndoRecord; $recording=$false
    $math=$null; $range=$null; $cc=$null; $selection=$null
    try {
        $undo.StartCustomRecord('Locus M0 tag conversion'); $recording=$true
        $source.InsertXML((New-OoxmlPackage $candidate.omml))
        Invoke-InjectedFault $FaultAfter 'NativeInserted'
        $math=$doc.OMaths.Item(1); $range=$math.Range
        $cc=$doc.ContentControls.Add(0,$range); $cc.Title='Locus M0 tag snapshot'
        Invoke-InjectedFault $FaultAfter 'ContentControlAdded'
        $cc.Tag=$Payload
        Invoke-InjectedFault $FaultAfter 'TagWritten'
        $selection=$doc.ActiveWindow.Selection
        $selection.SetRange($cc.Range.End,$cc.Range.End)
        Invoke-InjectedFault $FaultAfter 'SelectionMoved'
    } finally {
        if ($recording) {$undo.EndCustomRecord()}
        Release-Com $source; Release-Com $undo; Release-Com $math; Release-Com $range; Release-Com $cc; Release-Com $selection
    }
}

function Get-TagPayload {
    param($Document)
    if ($Document.ContentControls.Count -ne 1) {throw 'Tag probe requires one unambiguous content control.'}
    $cc=$Document.ContentControls.Item(1)
    try {return [string]$cc.Tag} finally {Release-Com $cc}
}

function Invoke-TagRestore {
    param($Document)
    $payload=Get-TagPayload $Document; $entry=$payload | ConvertFrom-Json
    if ($entry.schemaVersion -cne 'm0-tag-snapshot-1') {throw 'Unknown tag snapshot schema.'}
    $cc=$Document.ContentControls.Item(1); $range=$null; $controlRange=$null
    $trace=[ordered]@{}
    $undo=$script:Word.UndoRecord; $recording=$false
    try {
        $undo.StartCustomRecord('Locus M0 tag restore'); $recording=$true
        $controlRange=$cc.Range
        $trace.controlBefore=Get-WordRangeBoundaryEvidence $Document $controlRange
        $range=$controlRange.Duplicate
        $range.SetRange([int]$range.Start,[int]$range.Start)
        $trace.anchorBefore=Get-WordRangeBoundaryEvidence $Document $range
        $cc.Delete($true) # DeleteContents=true, unlike the keep-content detach.
        $trace.anchorAfterDelete=Get-WordRangeBoundaryEvidence $Document $range
        if ($range.Start -ne $range.End) {throw 'Restore anchor did not remain collapsed after complete control deletion.'}
        $range.Text=[string]$entry.source
        $trace.inserted=Get-WordRangeBoundaryEvidence $Document $range
    } finally {
        if ($recording) {$undo.EndCustomRecord()}
        Release-Com $cc; Release-Com $range; Release-Com $controlRange; Release-Com $undo
    }
    return $trace
}

function Invoke-TagDetach {
    param($Document)
    [void](Get-TagPayload $Document)
    $cc=$Document.ContentControls.Item(1); $undo=$script:Word.UndoRecord; $recording=$false
    try {
        $undo.StartCustomRecord('Locus M0 tag detach'); $recording=$true
        $cc.Delete($false)
    } finally {
        if ($recording) {$undo.EndCustomRecord()}
        Release-Com $cc; Release-Com $undo
    }
}

function Test-TagFaults {
    param($Fixture)
    $payload=New-TagSnapshotPayload $Fixture 4096
    foreach ($stage in @('NativeInserted','ContentControlAdded','TagWritten','SelectionMoved')) {
        $doc=$null
        try {
            Set-ProbeStage ('tag-fault-'+$stage) 'inject-and-single-undo'
            $info=New-FixtureDocument $Fixture ('tag-fault-'+$stage); $doc=$info.Document
            $doc.UndoClear(); $before=Get-DocSnapshot $doc
            $faultObserved=$false
            try {Convert-TagEquation $info $Fixture $payload $stage}
            catch {
                $faultObserved=$_.Exception.Message -like ('*M0_INJECTED_FAULT:'+$stage+'*')
                if (-not $faultObserved) {throw}
            }
            $partial=Get-DocSnapshot $doc
            $undoReturned=[bool]$doc.Undo(1); $rollback=Get-DocSnapshot $doc
            Assert-Observation ('tag-fault.'+$stage) ($faultObserved -and $undoReturned -and (Test-SameDocumentState $before $rollback)) 'After the injected failure, one native Undo restores source and removes all equation/control/tag metadata changes, without compensation.' $before @{faultObserved=$faultObserved;undoReturned=$undoReturned;partial=$partial;rollback=$rollback}
        } catch {Record-Test ('tag-fault.'+$stage+'.exception') 'FAIL' 'Complete the exact tag fault stage and observe rollback.' (Get-ErrorDetails $_)}
        finally {if ($null -ne $doc) {Close-ProbeDocument $doc}}
    }
}

function Test-TagLifecycle {
    param($Fixture)
    $doc=$null; $copyDoc=$null
    try {
        Set-ProbeStage 'tag-lifecycle' 'complete-multi-candidate-snapshot-save-as'
        $info=New-FixtureDocument $Fixture 'tag-lifecycle'; $doc=$info.Document
        $payload=New-TagSnapshotPayload $Fixture 4096
        Convert-TagEquation $info $Fixture $payload
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'tag-lifecycle-original.docx')
        $saveAsPath=Join-Path $script:RunDirectory 'tag-lifecycle-save-as.docx'
        Save-ProbeDocument $doc $saveAsPath
        Close-ProbeDocument $doc; $doc=$null
        $doc=Open-ProbeDocument $saveAsPath
        $readBack=Get-TagPayload $doc; $decoded=$readBack | ConvertFrom-Json
        Assert-Observation 'tag-lifecycle.save-as-candidate-set' ($readBack -ceq $payload -and @($decoded.candidates).Count -eq @($Fixture.candidates).Count -and $doc.OMaths.Count -eq 1) 'The complete multi-candidate tag snapshot stays associated with native math after Save As and reopen.' @{payload=$payload;candidateCount=@($Fixture.candidates).Count} @{payload=$readBack;candidateCount=@($decoded.candidates).Count;oMathCount=[int]$doc.OMaths.Count}

        Set-ProbeStage 'tag-lifecycle' 'native-edit-drifts-from-stored-candidate'
        $originalSignature=Get-MathSignature ((Get-SelectedCandidate $Fixture).omml)
        $edit=Edit-NativeToken $doc $Fixture.nativeEdit.find $Fixture.nativeEdit.replace
        $editedSignature=Get-MathSignature (Get-DocumentMathXml $doc)
        $readBack=Get-TagPayload $doc
        Assert-Observation 'tag-lifecycle.native-drift' ($editedSignature -cne $originalSignature -and $readBack -ceq $payload -and $doc.OMaths.Count -eq 1) 'A native edit remains authoritative current content; source and complete candidate history stay unchanged and drift is detectable by signature.' @{storedSignature=$originalSignature;payload=$payload} @{currentSignature=$editedSignature;payload=$readBack;edit=$edit}
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'tag-lifecycle-native-edited.docx')

        Set-ProbeStage 'tag-lifecycle' 'copy-range-to-another-synthetic-document'
        $copyDoc=New-ProbeDocument
        $sourceControl=$doc.ContentControls.Item(1); $sourceRange=$sourceControl.Range
        $targetRange=$copyDoc.Range(0,0)
        try {
            $sourceControlId=[string]$sourceControl.ID
            # This tests COM FormattedText copy, and does not read/change Clipboard.
            $targetRange.FormattedText=$sourceRange.FormattedText
        } finally {Release-Com $sourceControl; Release-Com $sourceRange; Release-Com $targetRange}
        $copyCount=[int]$copyDoc.ContentControls.Count; $copyTag=$null; $copyControlId=$null
        if ($copyCount -eq 1) {
            $copyControl=$copyDoc.ContentControls.Item(1)
            try {$copyTag=[string]$copyControl.Tag; $copyControlId=[string]$copyControl.ID} finally {Release-Com $copyControl}
        }
        Assert-Observation 'tag-lifecycle.cross-document-range-copy' ($copyDoc.OMaths.Count -eq 1 -and $copyCount -eq 1 -and $copyTag -ceq $payload) 'Copy through Word Range.FormattedText preserves a native equation with its complete tag snapshot in another synthetic document. This is not a Clipboard/UI copy claim.' @{native=1;controls=1;payload=$payload} @{native=[int]$copyDoc.OMaths.Count;controls=$copyCount;payload=$copyTag;sourceControlId=$sourceControlId;copiedControlId=$copyControlId}
        Save-ProbeDocument $copyDoc (Join-Path $script:RunDirectory 'tag-lifecycle-cross-document-copy.docx')
        Close-ProbeDocument $copyDoc; $copyDoc=$null

        Set-ProbeStage 'tag-lifecycle' 'restore-original-source-and-undo-redo'
        $managed=Get-DocSnapshot $doc
        $restoreTrace=Invoke-TagRestore $doc
        $restored=Get-DocSnapshot $doc
        $expectedText=$info.Prefix+$Fixture.source+$info.Suffix
        Assert-Observation 'tag-lifecycle.restore-source' ($restored.text -ceq $expectedText -and $restored.oMathCount -eq 0 -and $restored.controlTags.Count -eq 0 -and $restored.metadataXml.Count -eq 0) 'Explicit restore returns exact original source and removes equation/control metadata, including after a native edit.' $expectedText @{snapshot=$restored;boundaryTrace=$restoreTrace}
        $undoReturned=[bool]$doc.Undo(1); $restoreUndone=Get-DocSnapshot $doc
        Assert-Observation 'tag-lifecycle.restore-single-undo' ($undoReturned -and (Test-SameDocumentState $managed $restoreUndone)) 'One Undo of restore reinstates the current edited native equation plus the complete original tag history.' $managed @{returned=$undoReturned;snapshot=$restoreUndone}
        $redoReturned=[bool]$doc.Redo(1); $restoreRedone=Get-DocSnapshot $doc
        Assert-Observation 'tag-lifecycle.restore-single-redo' ($redoReturned -and (Test-SameDocumentState $restored $restoreRedone)) 'One Redo of restore reinstates the exact restored source state.' $restored @{returned=$redoReturned;snapshot=$restoreRedone}
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'tag-lifecycle-restored.docx')

        # A new fixture is used for detach so SaveAs/read observations cannot alter
        # the preceding restore Undo stack or create a false dependent success.
        Close-ProbeDocument $doc; $doc=$null
        $info=New-FixtureDocument $Fixture 'tag-detach'; $doc=$info.Document
        Convert-TagEquation $info $Fixture $payload
        $beforeDetach=Get-DocSnapshot $doc
        Invoke-TagDetach $doc
        $detached=Get-DocSnapshot $doc
        Assert-Observation 'tag-lifecycle.detach' ($detached.text -ceq $beforeDetach.text -and $detached.oMathCount -eq 1 -and $detached.controlTags.Count -eq 0 -and $detached.metadataXml.Count -eq 0) 'Detach removes management and preserves current native equation text.' $beforeDetach $detached
        $undoReturned=[bool]$doc.Undo(1); $detachUndone=Get-DocSnapshot $doc
        Assert-Observation 'tag-lifecycle.detach-single-undo' ($undoReturned -and (Test-SameDocumentState $beforeDetach $detachUndone)) 'One Undo of detach restores the complete tag snapshot and control without changing native content.' $beforeDetach @{returned=$undoReturned;snapshot=$detachUndone}
        $redoReturned=[bool]$doc.Redo(1); $detachRedone=Get-DocSnapshot $doc
        Assert-Observation 'tag-lifecycle.detach-single-redo' ($redoReturned -and (Test-SameDocumentState $detached $detachRedone)) 'One Redo of detach again removes all managed metadata.' $detached @{returned=$redoReturned;snapshot=$detachRedone}
        $detachedSignature=Get-MathSignature (Get-DocumentMathXml $doc)
        Assert-Observation 'tag-lifecycle.detach-structure' ($detachedSignature -ceq $originalSignature) 'Detached content remains the same native mathematical structure.' $originalSignature $detachedSignature
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'tag-lifecycle-detached.docx')
    } catch {Record-Test 'tag-lifecycle.exception' 'FAIL' 'Complete the extended tag lifecycle; later criteria are not claimed.' (Get-ErrorDetails $_)}
    finally {
        if ($null -ne $copyDoc) {Close-ProbeDocument $copyDoc}
        if ($null -ne $doc) {Close-ProbeDocument $doc}
    }
}

function Test-TagUnicodeStorage {
    param($Fixture)
    $doc=$null
    try {
        Set-ProbeStage 'tag-unicode' 'store-raw-unicode-history-without-normalization'
        $info=New-FixtureDocument $Fixture 'tag-unicode'; $doc=$info.Document
        $raw='NFC:'+[char]0x00E9+' NFD:e'+[char]0x0301+' emoji:'+[char]::ConvertFromUtf32(0x1F642)+"`r`nnext`tline "
        $payloadObject=(New-TagSnapshotPayload $Fixture 0) | ConvertFrom-Json
        # Storage stress data is deliberately independent of the fixed math fixture;
        # this tests preservation, not whether this text is a valid formula source.
        $payloadObject.source=$raw
        $payload=ConvertTo-Json -InputObject $payloadObject -Depth 100 -Compress
        Convert-TagEquation $info $Fixture $payload
        $path=Join-Path $script:RunDirectory 'tag-unicode-source.docx'
        Save-ProbeDocument $doc $path
        Close-ProbeDocument $doc; $doc=$null
        $doc=Open-ProbeDocument $path
        $readBack=Get-TagPayload $doc; $decoded=$readBack | ConvertFrom-Json
        Assert-Observation 'tag-unicode.raw-source' ($readBack -ceq $payload -and $decoded.source -ceq $raw) 'JSON metadata preserves NFC, NFD, supplementary emoji, CRLF, tab and trailing space exactly through Tag and DOCX save/reopen. Storage-only synthetic source; no parsing/body normalization claim.' @{source=$raw;utf16Units=@($raw.ToCharArray()|ForEach-Object {'U+{0:X4}' -f [int]$_})} @{source=$decoded.source;utf16Units=@($decoded.source.ToCharArray()|ForEach-Object {'U+{0:X4}' -f [int]$_})}
    } catch {Record-Test 'tag-unicode.exception' 'FAIL' 'Complete the exact Unicode metadata storage experiment.' (Get-ErrorDetails $_)}
    finally {if ($null -ne $doc) {Close-ProbeDocument $doc}}
}
