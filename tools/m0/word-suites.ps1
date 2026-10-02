# Suites are loaded by word_probe.ps1; do not execute this file directly.

function Test-NativeFixture {
    param($Fixture)
    $id = $Fixture.caseId; $doc = $null
    try {
        Set-ProbeStage $id 'create-synthetic-document'
        $info = New-FixtureDocument $Fixture $id; $doc = $info.Document
        $candidate = Get-SelectedCandidate $Fixture
        $expected = Get-MathSignature $candidate.omml
        $source = Find-ProbeRange $doc.Content $Fixture.source
        try { $source.InsertXML((New-OoxmlPackage $candidate.omml)) } finally { Release-Com $source }
        Assert-Observation "$id.native-object" ($doc.OMaths.Count -eq 1) 'Exactly one Word OMath after candidate OMML insertion.' 1 ([int]$doc.OMaths.Count)
        if ($doc.OMaths.Count -ne 1) { throw 'Cannot continue native checks without one OMath.' }
        $xml = Get-DocumentMathXml $doc
        [IO.File]::WriteAllText((Join-Path $script:RunDirectory ($id + '-inserted.xml')), $xml, $script:Utf8NoBom)
        $actual = Get-MathSignature $xml
        Assert-Observation "$id.structure" ($actual -ceq $expected) 'Canonical math tree and selected semantic properties equal the fixed candidate.' $expected $actual
        $outside = Get-OutsideSnapshot $doc
        Assert-Observation "$id.outside-range" (Test-OutsideUnchanged $info $outside) 'Prefix, suffix and sampled bold/italic formatting remain unchanged.' (@{prefix=$info.Prefix;suffix=$info.Suffix;prefixBold=$info.PrefixBold;suffixItalic=$info.SuffixItalic}) $outside
        $path = Join-Path $script:RunDirectory ($id + '.docx')
        Set-ProbeStage $id 'save-close-reopen'
        Save-ProbeDocument $doc $path
        Close-ProbeDocument $doc; $doc = $null
        $doc = Open-ProbeDocument $path
        $reopenedXml = Get-DocumentMathXml $doc
        $reopenedSignature = Get-MathSignature $reopenedXml
        Assert-Observation "$id.save-reopen" ($doc.OMaths.Count -eq 1 -and $reopenedSignature -ceq $expected) 'Native OMath and candidate structure survive DOCX save and reopen.' $expected (@{oMathCount=[int]$doc.OMaths.Count;signature=$reopenedSignature;file=$path})
        Set-ProbeStage $id 'edit-native-range'
        $editedExpected = Get-ExpectedEditedSignature $reopenedXml $Fixture.nativeEdit.find $Fixture.nativeEdit.replace
        $editObservation = Edit-NativeToken $doc $Fixture.nativeEdit.find $Fixture.nativeEdit.replace
        $editedXml = Get-DocumentMathXml $doc
        $editedActual = Get-MathSignature $editedXml
        $editedOutside = Get-OutsideSnapshot $doc
        Assert-Observation "$id.native-edit" ($doc.OMaths.Count -eq 1 -and $editedActual -ceq $editedExpected -and (Test-OutsideUnchanged $info $editedOutside)) 'COM editing inside the native equation changes only the intended first token and preserves equation structure and outside content. Keyboard UI editing is a separate untested criterion.' $editedExpected (@{signature=$editedActual;oMathCount=[int]$doc.OMaths.Count;outside=$editedOutside;edit=$editObservation})
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory ($id + '-edited.docx'))
    } catch {
        Record-Test "$id.exception" 'FAIL' 'The synthetic native probe should complete without a COM or fixture error; subsequent steps were not run.' (Get-ErrorDetails $_)
    } finally { if ($null -ne $doc) { try { Close-ProbeDocument $doc } catch { Record-Test "$id.close" 'FAIL' 'Close only the synthetic document.' (Get-ErrorDetails $_) } } }
}

function Test-ManagedUndo {
    param($Fixture)
    $doc = $null
    try {
        Set-ProbeStage 'managed-undo' 'create-synthetic-document'
        $info = New-FixtureDocument $Fixture 'managed-undo'; $doc = $info.Document
        $selection = $doc.ActiveWindow.Selection
        try { $selection.SetRange($info.SourceStart, $info.SourceEnd) } finally { Release-Com $selection }
        $doc.UndoClear() # This is exclusively a new synthetic document.
        $before = Get-DocSnapshot $doc
        $entryId = Convert-ManagedEquation $info $Fixture
        $converted = Get-DocSnapshot $doc
        Assert-Observation 'managed-undo.converted' ($converted.oMathCount -eq 1 -and $converted.controlTags.Count -eq 1 -and $converted.metadataXml.Count -eq 1) 'One native equation, one tagged control and one Locus custom XML part after conversion.' @{oMath=1;control=1;metadata=1} $converted
        Set-ProbeStage 'managed-undo' 'single-undo-and-redo-without-ooxml-reads'
        $undoReturn = [bool]$doc.Undo(1)
        $undone = Get-DocSnapshot $doc
        Assert-Observation 'managed-undo.single-undo' ($undoReturn -and (Test-SameDocumentState $before $undone)) 'One Undo restores exact source text, OMath count, control tags and Locus metadata. No compensating cleanup is hidden.' $before (@{undoReturned=$undoReturn;snapshot=$undone})
        Assert-Observation 'managed-undo.selection-exact' ($before.selectionStart -eq $undone.selectionStart -and $before.selectionEnd -eq $undone.selectionEnd -and $before.selectionStoryType -eq $undone.selectionStoryType) 'Experimental strict criterion: one Undo restores the exact original selection.' @{start=$before.selectionStart;end=$before.selectionEnd;story=$before.selectionStoryType} @{start=$undone.selectionStart;end=$undone.selectionEnd;story=$undone.selectionStoryType}
        $redoReturn = [bool]$doc.Redo(1)
        $redone = Get-DocSnapshot $doc
        Assert-Observation 'managed-undo.single-redo' ($redoReturn -and (Test-SameDocumentState $converted $redone)) 'One Redo restores equation/control/source metadata and their association.' $converted (@{redoReturned=$redoReturn;snapshot=$redone})
        Write-JsonFile (Join-Path $script:RunDirectory 'managed-undo-snapshots.json') @{before=$before;converted=$converted;undone=$undone;redone=$redone}
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'managed-undo.docx')
    } catch { Record-Test 'managed-undo.exception' 'FAIL' 'Complete the managed Undo/Redo experiment; later assertions were not run.' (Get-ErrorDetails $_) }
    finally { if ($null -ne $doc) { Close-ProbeDocument $doc } }
}

function Test-MetadataLifecycle {
    param($Fixture)
    $doc = $null
    try {
        Set-ProbeStage 'metadata' 'managed-save-close-reopen'
        $info = New-FixtureDocument $Fixture 'metadata'; $doc = $info.Document
        $entryId = Convert-ManagedEquation $info $Fixture
        $before = Read-MetadataEntry $doc
        $path = Join-Path $script:RunDirectory 'metadata-managed.docx'
        Save-ProbeDocument $doc $path
        Close-ProbeDocument $doc; $doc = $null
        $doc = Open-ProbeDocument $path
        $after = Read-MetadataEntry $doc
        $controls = $doc.SelectContentControlsByTag('locus:m0:' + $after.EntryId)
        try { $associationCount = [int]$controls.Count } finally { Release-Com $controls }
        $fixtureCandidates = ConvertTo-Json -InputObject @($Fixture.candidates) -Depth 100 -Compress
        $decodedCandidates = $after.CandidateJson | ConvertFrom-Json
        $savedCandidates = ConvertTo-Json -InputObject @($decodedCandidates) -Depth 100 -Compress
        Assert-Observation 'metadata.candidate-snapshot' ($after.Source -ceq $Fixture.source -and $after.SelectedCandidateId -ceq $Fixture.selectedCandidateId -and $savedCandidates -ceq $fixtureCandidates -and $associationCount -eq 1 -and $after.EntryId -ceq $before.EntryId) 'Source, selected candidate, the complete fixed candidate set and a unique control association survive save/reopen without reparsing.' @{source=$Fixture.source;selected=$Fixture.selectedCandidateId;candidateCount=$Fixture.candidates.Count;associations=1} @{source=$after.Source;selected=$after.SelectedCandidateId;candidateCount=@($decodedCandidates).Count;associations=$associationCount;entryId=$after.EntryId}
        Set-ProbeStage 'metadata' 'native-edit-and-drift-detection'
        $editObservation = Edit-NativeToken $doc $Fixture.nativeEdit.find $Fixture.nativeEdit.replace
        $editedSignature = Get-MathSignature (Get-DocumentMathXml $doc)
        $afterEdit = Read-MetadataEntry $doc
        Assert-Observation 'metadata.native-drift-detectable' ($editedSignature -cne $afterEdit.NativeSignature -and $afterEdit.Source -ceq $before.Source -and $afterEdit.CandidateJson -ceq $before.CandidateJson) 'A native edit makes the stored candidate fingerprint stale while original source/candidate history remains available. No production drift event handler is claimed.' @{storedSignature=$before.NativeSignature;originalSource=$before.Source} @{currentSignature=$editedSignature;storedSignature=$afterEdit.NativeSignature;source=$afterEdit.Source;edit=$editObservation}
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'metadata-native-edited.docx')
        Set-ProbeStage 'metadata' 'explicit-restore-original-source'
        $restoreTrace=Invoke-RestoreSource $doc
        $restored = Get-DocSnapshot $doc
        $expectedText = $info.Prefix + $Fixture.source + $info.Suffix
        Assert-Observation 'metadata.restore-source' ($restored.text -ceq $expectedText -and $restored.oMathCount -eq 0 -and $restored.controlTags.Count -eq 0 -and $restored.metadataXml.Count -eq 0) 'Explicit restore uses the exact original source, removes the native equation and removes only this synthetic Locus association.' $expectedText @{snapshot=$restored;boundaryTrace=$restoreTrace}
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'metadata-restored.docx')
    } catch { Record-Test 'metadata.exception' 'FAIL' 'Complete save/reopen, native drift and explicit source restore; later assertions were not run.' (Get-ErrorDetails $_) }
    finally { if ($null -ne $doc) { Close-ProbeDocument $doc } }

    $doc = $null
    try {
        Set-ProbeStage 'detach' 'remove-management-retain-native-equation'
        $info = New-FixtureDocument $Fixture 'detach'; $doc = $info.Document
        [void](Convert-ManagedEquation $info $Fixture)
        $signature = Get-MathSignature (Get-DocumentMathXml $doc)
        $text = [string]$doc.Content.Text
        Invoke-DetachEquation $doc
        $snapshot = Get-DocSnapshot $doc
        $afterSignature = Get-MathSignature (Get-DocumentMathXml $doc)
        Assert-Observation 'metadata.detach' ($snapshot.oMathCount -eq 1 -and $snapshot.controlTags.Count -eq 0 -and $snapshot.metadataXml.Count -eq 0 -and $snapshot.text -ceq $text -and $signature -ceq $afterSignature) 'Detach removes the control and metadata while preserving native equation structure and all document text.' @{signature=$signature;text=$text;nativeCount=1;controlCount=0;metadataCount=0} @{signature=$afterSignature;snapshot=$snapshot}
        Save-ProbeDocument $doc (Join-Path $script:RunDirectory 'metadata-detached.docx')
    } catch { Record-Test 'metadata.detach-exception' 'FAIL' 'Complete the detach experiment.' (Get-ErrorDetails $_) }
    finally { if ($null -ne $doc) { Close-ProbeDocument $doc } }
}

function Test-FaultInjection {
    param($Fixture)
    foreach ($stage in @('NativeInserted','ContentControlAdded','MetadataAdded','SelectionMoved')) {
        $doc = $null
        try {
            Set-ProbeStage ('fault-' + $stage) 'inject-and-attempt-single-undo-rollback'
            $info = New-FixtureDocument $Fixture ('fault-' + $stage); $doc = $info.Document
            $doc.UndoClear(); $before = Get-DocSnapshot $doc
            $injected = $false; $exception = $null
            try { [void](Convert-ManagedEquation $info $Fixture $stage) }
            catch {
                $exception = Get-ErrorDetails $_
                $injected = $_.Exception.Message -like ('*M0_INJECTED_FAULT:' + $stage + '*')
                if (-not $injected) { throw }
            }
            $partial = Get-DocSnapshot $doc
            $undoReturn = [bool]$doc.Undo(1)
            $rollback = Get-DocSnapshot $doc
            Assert-Observation ('fault.' + $stage) ($injected -and $undoReturn -and (Test-SameDocumentState $before $rollback)) 'After the specified injected failure, a single native Undo rolls back all source/equation/control/metadata changes. Partial state is reported; no silent compensating deletion.' $before @{faultObserved=$injected;undoReturned=$undoReturn;partial=$partial;rollback=$rollback;exception=$exception}
            Write-JsonFile (Join-Path $script:RunDirectory ('fault-' + $stage + '.json')) @{before=$before;partial=$partial;rollback=$rollback}
            Save-ProbeDocument $doc (Join-Path $script:RunDirectory ('fault-' + $stage + '-after-rollback.docx'))
        } catch { Record-Test ('fault.' + $stage + '.exception') 'FAIL' 'Reach the intended fault stage and observe rollback.' (Get-ErrorDetails $_) }
        finally { if ($null -ne $doc) { Close-ProbeDocument $doc } }
    }
}

function Test-StaleAdmission {
    param($Fixture)
    foreach ($scenario in @('unchanged','source-changed','insert-before','source-deleted','document-closed','document-session-switched','candidate-changed','config-changed','ime-composing','focus-find','focus-ribbon','focus-other-window','expired')) {
        $doc = $null
        try {
            Set-ProbeStage ('stale-' + $scenario) 'mutate-synthetic-state-and-validate-without-commit'
            $info = New-FixtureDocument $Fixture ('stale-' + $scenario); $doc = $info.Document
            $expected = New-AdmissionSnapshot $info $Fixture
            $current = [ordered]@{sessionId=$expected.sessionId;candidateDigest=$expected.candidateDigest;configRevision=1;inputState='Committed';focusState='DocumentEditor'}
            switch ($scenario) {
                'source-changed' { $range=$doc.Range($info.SourceStart,$info.SourceEnd); try {$range.Text='different input'} finally {Release-Com $range} }
                'insert-before' { $range=$doc.Range(0,0); try {$range.Text='Inserted before. '} finally {Release-Com $range} }
                'source-deleted' { $range=$doc.Range($info.SourceStart,$info.SourceEnd); try {$range.Delete()} finally {Release-Com $range} }
                'document-closed' { Close-ProbeDocument $doc; $doc=$null }
                'document-session-switched' { $current.sessionId=[Guid]::NewGuid().ToString('D') }
                'candidate-changed' { $current.candidateDigest='different-candidate-digest' }
                'config-changed' { $current.configRevision=2 }
                'ime-composing' { $current.inputState='Composing' }
                'focus-find' { $current.focusState='Find' }
                'focus-ribbon' { $current.focusState='Ribbon' }
                'focus-other-window' { $current.focusState='OtherWindow' }
                'expired' { $expected.expiresAtUtc=[DateTime]::UtcNow.AddSeconds(-1) }
            }
            $beforeValidation = if ($null -ne $doc) { [string]$doc.Content.Text } else { $null }
            $admission = Test-Admission $doc $expected $current
            $afterValidation = if ($null -ne $doc) { [string]$doc.Content.Text } else { $null }
            $expectedAllowed = $scenario -eq 'unchanged'
            Assert-Observation ('stale.' + $scenario) ($admission.allowed -eq $expectedAllowed -and $beforeValidation -ceq $afterValidation) 'The prototype admission validator rejects stale/incomplete state and performs no document mutation. Input/focus/session-switch flags are injected, not measured Windows/IME state.' @{allowed=$expectedAllowed;validatorWrites=0} @{allowed=$admission.allowed;reasons=$admission.reasons;before=$beforeValidation;after=$afterValidation;syntheticFlags=$current}
        } catch { Record-Test ('stale.' + $scenario + '.exception') 'FAIL' 'Complete the synthetic admission scenario.' (Get-ErrorDetails $_) }
        finally { if ($null -ne $doc) { Close-ProbeDocument $doc } }
    }
}
