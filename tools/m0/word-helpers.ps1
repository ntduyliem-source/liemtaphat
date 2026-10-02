# Helpers for word_probe.ps1. ASCII source for Windows PowerShell 5.1.
# These are M0 discovery utilities, not production connector code.

$script:MathNs = 'http://schemas.openxmlformats.org/officeDocument/2006/math'
$script:MetadataNs = 'urn:locus:m0:metadata:v1'
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:OpenDocuments = New-Object 'System.Collections.Generic.List[object]'

function Write-JsonFile {
    param([string]$Path, $Value)
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $Value -Depth 100), $script:Utf8NoBom)
}

function Save-Report {
    $script:Report.updatedAt = [DateTime]::UtcNow.ToString('o')
    Write-JsonFile (Join-Path $script:RunDirectory 'report.json') $script:Report
}

function Set-ProbeStage {
    param([string]$CaseId, [string]$Stage)
    $elapsed = [Math]::Round($script:Clock.Elapsed.TotalSeconds, 2)
    Write-JsonFile (Join-Path $script:RunDirectory 'progress.json') ([ordered]@{
        updatedAt = [DateTime]::UtcNow.ToString('o'); caseId = $CaseId; stage = $Stage
        elapsedSeconds = $elapsed; ownedWordPid = $script:OwnedWordPid
        timeoutAdvisorySeconds = $AdvisoryTimeoutSeconds
    })
    Write-Host ("[{0:N1}s] {1}: {2}" -f $elapsed, $CaseId, $Stage)
    if ($elapsed -gt $AdvisoryTimeoutSeconds) {
        Write-Warning 'Advisory timeout exceeded. A synchronous COM call cannot be forcibly timed out by this script. Inspect progress.json and ownership.json; do not terminate unrelated WINWORD processes.'
    }
}

function Record-Test {
    param([string]$CaseId, [ValidateSet('PASS','FAIL','UNTESTED')][string]$Status,
          [string]$Criterion, $Observations)
    [void]$script:Report.results.Add([ordered]@{
        caseId = $CaseId; status = $Status; criterion = $Criterion
        observations = $Observations; recordedAt = [DateTime]::UtcNow.ToString('o')
    })
    Save-Report
    Write-Host ("  {0} {1}" -f $Status, $CaseId)
}

function Assert-Observation {
    param([string]$CaseId, [bool]$Condition, [string]$Criterion, $Expected, $Actual)
    $status = if ($Condition) { 'PASS' } else { 'FAIL' }
    Record-Test $CaseId $status $Criterion ([ordered]@{ expected = $Expected; actual = $Actual })
}

function Get-ErrorDetails {
    param($ErrorRecord)
    $ex = $ErrorRecord.Exception
    $chain = New-Object 'System.Collections.Generic.List[object]'
    while ($null -ne $ex) {
        [void]$chain.Add([ordered]@{
            type = $ex.GetType().FullName; message = $ex.Message
            hresult = ('0x{0:X8}' -f ([long]$ex.HResult -band 0xffffffffL))
        })
        $ex = $ex.InnerException
    }
    return [ordered]@{ exceptionChain = @($chain.ToArray()); location = $ErrorRecord.InvocationInfo.PositionMessage; stack = $ErrorRecord.ScriptStackTrace }
}

function Release-Com {
    param($Value)
    if ($null -ne $Value -and [Runtime.InteropServices.Marshal]::IsComObject($Value)) {
        try { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($Value) } catch { }
    }
}

function Get-WordActivationPreflight {
    # RegistryView.Default is the current process view, including WOW64 redirection.
    # Read only. Never repair/re-register Office or launch a registry command string.
    $root = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::ClassesRoot, [Microsoft.Win32.RegistryView]::Default)
    $progidKey = $null; $serverKey = $null
    try {
        $progidKey = $root.OpenSubKey('Word.Application\CLSID', $false)
        if ($null -eq $progidKey) { throw 'Word.Application has no CLSID in this process registry view.' }
        $clsid = [string]$progidKey.GetValue('')
        $serverKey = $root.OpenSubKey(('CLSID\' + $clsid + '\LocalServer32'), $false)
        if ($null -eq $serverKey) { throw 'Word.Application CLSID has no LocalServer32 in this process registry view.' }
        $command = [Environment]::ExpandEnvironmentVariables([string]$serverKey.GetValue(''))
        $executable = $null
        if ($command -match '^\s*"([^"]+)"') { $executable = $Matches[1] }
        elseif ($command -match '^\s*(.+?\.exe)(?:\s|$)') { $executable = $Matches[1] }
        $exists = $null -ne $executable -and [IO.File]::Exists($executable)
        $company = $null; $product = $null
        if ($exists) {
            $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
            $company = [string]$version.CompanyName; $product = [string]$version.ProductName
        }
        $filenameMatches = $null -ne $executable -and [string]::Equals([IO.Path]::GetFileName($executable), 'WINWORD.EXE', [StringComparison]::OrdinalIgnoreCase)
        $companyMatches = [string]::Equals($company, 'Microsoft Corporation', [StringComparison]::OrdinalIgnoreCase)
        return [ordered]@{
            processBitness = [IntPtr]::Size * 8; registryView = 'Default (current process)'
            progId = 'Word.Application'; clsid = $clsid; localServer32 = $command
            executable = $executable; exists = $exists; companyName = $company; productName = $product
            isMicrosoftWord = $exists -and $filenameMatches -and $companyMatches
        }
    } finally {
        if ($null -ne $serverKey) { $serverKey.Dispose() }
        if ($null -ne $progidKey) { $progidKey.Dispose() }
        $root.Dispose()
    }
}

function Close-ProbeDocument {
    param($Document)
    if ($null -eq $Document) { return }
    try { $Document.Close(0) } finally {
        [void]$script:OpenDocuments.Remove($Document)
        Release-Com $Document
    }
}

function New-ProbeDocument {
    $doc = $script:Word.Documents.Add()
    [void]$script:OpenDocuments.Add($doc)
    return $doc
}

function Open-ProbeDocument {
    param([string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($script:RunDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to open a document outside this synthetic run directory.'
    }
    # ConfirmConversions, ReadOnly, AddToRecentFiles are all false.
    $doc = $script:Word.Documents.Open($full, $false, $false, $false)
    [void]$script:OpenDocuments.Add($doc)
    return $doc
}

function Save-ProbeDocument {
    param($Document, [string]$Path)
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($script:RunDirectory + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing to save outside this synthetic run directory.'
    }
    # wdFormatXMLDocument=12, LockComments=false, Password='', AddToRecentFiles=false.
    $Document.SaveAs2($full, 12, $false, '', $false)
}

function Find-ProbeRange {
    param($SearchRange, [string]$Text)
    $range = $SearchRange.Duplicate
    $find = $null
    try {
        $find = $range.Find
        $find.ClearFormatting()
        $find.Text = $Text.Replace('^', '^^')
        $find.Forward = $true
        $find.Wrap = 0 # wdFindStop
        $find.Format = $false
        $find.MatchCase = $true
        $find.MatchWholeWord = $false
        $find.MatchWildcards = $false
        if (-not $find.Execute()) { throw ("Exact source/token not found: {0}" -f $Text) }
        if ($range.Text -cne $Text) { throw 'Word Find did not return the exact requested text.' }
        return $range
    } catch {
        Release-Com $range
        throw
    } finally { Release-Com $find }
}

function New-FixtureDocument {
    param($Fixture, [string]$Label)
    $doc = New-ProbeDocument
    # Include UTF-16 surrogate and combining-character cases in the prefix.
    # Source locations are measured by Word Find, never inferred from .NET lengths.
    $prefix = 'Before ' + [char]::ConvertFromUtf32(0x1F642) + ' e' + [char]0x0301 + ' [' + $Label + '] | '
    $suffix = ' | after ' + [char]0x03A9 + ' [' + $Label + '].'
    $content = $doc.Content
    $sourceRange = $null; $prefixRange = $null; $suffixRange = $null
    try {
        $content.Text = $prefix + $Fixture.source + $suffix + "`r"
        $sourceRange = Find-ProbeRange $doc.Content $Fixture.source
        $prefixRange = $doc.Range(0, $sourceRange.Start)
        $suffixRange = $doc.Range($sourceRange.End, $doc.Content.End)
        $prefixRange.Font.Bold = -1
        $suffixRange.Font.Italic = -1
        return [pscustomobject]@{
            Document = $doc; Source = [string]$Fixture.source
            Prefix = [string]$prefixRange.Text; Suffix = [string]$suffixRange.Text
            PrefixBold = [int]$prefixRange.Font.Bold; SuffixItalic = [int]$suffixRange.Font.Italic
            SourceStart = [int]$sourceRange.Start; SourceEnd = [int]$sourceRange.End
            SessionId = [Guid]::NewGuid().ToString('D')
        }
    } finally {
        Release-Com $content; Release-Com $sourceRange; Release-Com $prefixRange; Release-Com $suffixRange
    }
}

function Get-SelectedCandidate {
    param($Fixture)
    $matches = @($Fixture.candidates | Where-Object { $_.candidateId -ceq $Fixture.selectedCandidateId })
    if ($matches.Count -ne 1) { throw 'Fixture must identify exactly one selected candidate.' }
    if ($Fixture.candidates.Count -gt 3) { throw 'Fixture has more than three candidates.' }
    return $matches[0]
}

function New-OoxmlPackage {
    param([string]$Omml)
    return '<pkg:package xmlns:pkg="http://schemas.microsoft.com/office/2006/xmlPackage">' +
        '<pkg:part pkg:name="/_rels/.rels" pkg:contentType="application/vnd.openxmlformats-package.relationships+xml"><pkg:xmlData>' +
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>' +
        '</pkg:xmlData></pkg:part><pkg:part pkg:name="/word/document.xml" pkg:contentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"><pkg:xmlData>' +
        '<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p>' + $Omml +
        '</w:p></w:body></w:document></pkg:xmlData></pkg:part></pkg:package>'
}

function Get-MathProperty {
    param($Node, [string]$Name, [string]$Default)
    foreach ($child in $Node.ChildNodes) {
        if ($child.NamespaceURI -eq $script:MathNs -and $child.LocalName.EndsWith('Pr')) {
            foreach ($p in $child.ChildNodes) {
                if ($p.NamespaceURI -eq $script:MathNs -and $p.LocalName -ceq $Name) {
                    return $p.GetAttribute('val', $script:MathNs)
                }
            }
        }
    }
    return $Default
}

function Get-CanonicalMathNode {
    param($Node)
    if ($Node.NodeType -ne [Xml.XmlNodeType]::Element) { return '' }
    if ($Node.NamespaceURI -ne $script:MathNs) { return '' }
    $name = $Node.LocalName
    if ($name.EndsWith('Pr') -or $name -eq 'ctrlPr') { return '' }
    if ($name -eq 't') { return [Security.SecurityElement]::Escape($Node.InnerText) }
    $content = ''
    foreach ($child in $Node.ChildNodes) { $content += Get-CanonicalMathNode $child }
    # Runs can be split/merged by Word without changing the math structure.
    if ($name -eq 'r') { return $content }
    $properties = ''
    $semantic = switch ($name) {
        'f' { @{ type = 'bar' } }
        'rad' { @{ degHide = '0' } }
        'nary' { @{ chr = [string][char]0x222B; subHide = '0'; supHide = '0'; limLoc = 'subSup' } }
        'd' { @{ begChr = '('; endChr = ')'; sepChr = '|' } }
        'acc' { @{ chr = [string][char]0x0302 } }
        default { @{} }
    }
    foreach ($key in @($semantic.Keys | Sort-Object)) {
        $value = Get-MathProperty $Node $key $semantic[$key]
        if ($key.EndsWith('Hide')) {
            if ($value -in @('true','on')) { $value = '1' }
            if ($value -in @('false','off')) { $value = '0' }
        }
        $properties += ' ' + $key + '="' + [Security.SecurityElement]::Escape($value) + '"'
    }
    return '<' + $name + $properties + '>' + $content + '</' + $name + '>'
}

function Get-MathSignature {
    param([string]$Xml)
    $parsed = New-Object Xml.XmlDocument
    $parsed.PreserveWhitespace = $true
    $parsed.LoadXml($Xml)
    $ns = New-Object Xml.XmlNamespaceManager($parsed.NameTable)
    $ns.AddNamespace('m', $script:MathNs)
    $maths = $parsed.SelectNodes('//m:oMath', $ns)
    if ($maths.Count -ne 1) { throw ("Expected one OMML oMath, found {0}." -f $maths.Count) }
    return Get-CanonicalMathNode $maths.Item(0)
}

function Get-DocumentMathXml {
    param($Document)
    # Do not call inside a custom Undo record or before the Undo/Redo probe.
    # WordOpenXML observation itself must not be assumed Undo-neutral.
    return [string]$Document.Content.WordOpenXML
}

function Get-OutsideSnapshot {
    param($Document)
    if ($Document.OMaths.Count -ne 1) { throw 'Outside comparison requires exactly one native equation.' }
    $math = $Document.OMaths.Item(1); $range = $math.Range
    $before = $Document.Range(0, $range.Start)
    $after = $Document.Range($range.End, $Document.Content.End)
    try {
        return [ordered]@{ prefix = [string]$before.Text; suffix = [string]$after.Text
            prefixBold = [int]$before.Font.Bold; suffixItalic = [int]$after.Font.Italic }
    } finally { Release-Com $before; Release-Com $after; Release-Com $range; Release-Com $math }
}

function Test-OutsideUnchanged {
    param($Info, $Actual)
    return $Actual.prefix -ceq $Info.Prefix -and $Actual.suffix -ceq $Info.Suffix -and
        $Actual.prefixBold -eq $Info.PrefixBold -and $Actual.suffixItalic -eq $Info.SuffixItalic
}

function Get-ExpectedEditedSignature {
    param([string]$Xml, [string]$Token, [string]$Replacement)
    $parsed = New-Object Xml.XmlDocument
    $parsed.PreserveWhitespace = $true; $parsed.LoadXml($Xml)
    $ns = New-Object Xml.XmlNamespaceManager($parsed.NameTable); $ns.AddNamespace('m', $script:MathNs)
    foreach ($node in $parsed.SelectNodes('//m:oMath//m:t', $ns)) {
        $index = $node.InnerText.IndexOf($Token, [StringComparison]::Ordinal)
        if ($index -ge 0) {
            $old = $node.InnerText
            $node.InnerText = $old.Substring(0, $index) + $Replacement + $old.Substring($index + $Token.Length)
            return Get-MathSignature $parsed.OuterXml
        }
    }
    throw 'Native edit token absent from OMML.'
}

function Edit-NativeToken {
    param($Document, [string]$Token, [string]$Replacement)
    $math = $Document.OMaths.Item(1); $mathRange = $math.Range
    $characters = $null; $found = $null
    try {
        $beforeText = [string]$mathRange.Text
        $characters = $mathRange.Characters
        $actualToken = $null; $characterIndex = $null
        for ($i = 1; $i -le $characters.Count; $i++) {
            $character = $characters.Item($i)
            $text = [string]$character.Text
            # Only match an already-known native token. Never normalize raw input
            # or derive Word offsets from the UTF-16 length of Range.Text.
            if ($text.Normalize([Text.NormalizationForm]::FormKC) -ceq $Token) {
                $found = $character; $actualToken = $text; $characterIndex = $i
                break
            }
            Release-Com $character
        }
        if ($null -eq $found) {
            throw ('Known native token not found in Word Characters: {0}; native Range.Text={1}' -f $Token, (ConvertTo-Json -InputObject $beforeText -Compress))
        }
        $start = [int]$found.Start; $end = [int]$found.End
        $found.Text = $Replacement
        return [ordered]@{
            requestedToken=$Token; actualNativeToken=$actualToken; replacement=$Replacement
            wordCharacterIndex=$characterIndex; rangeStart=$start; rangeEnd=$end; beforeNativeText=$beforeText
            beforeNativeUtf16Units=@($beforeText.ToCharArray() | ForEach-Object { 'U+{0:X4}' -f [int]$_ })
            matching='FormKC on a Word-provided native character range only; raw source is not normalized.'
        }
    } finally { Release-Com $found; Release-Com $characters; Release-Com $mathRange; Release-Com $math }
}

function Get-ManagedParts {
    param($Document)
    $selected = $Document.CustomXMLParts.SelectByNamespace($script:MetadataNs)
    $xmls = New-Object 'System.Collections.Generic.List[string]'
    try {
        for ($i = 1; $i -le $selected.Count; $i++) {
            $part = $selected.Item($i)
            try { [void]$xmls.Add([string]$part.XML) } finally { Release-Com $part }
        }
    } finally { Release-Com $selected }
    # Emit each XML string; callers wrap @() to preserve zero/one/many counts.
    return @($xmls.ToArray() | Sort-Object)
}

function Get-DocSnapshot {
    param($Document)
    # Intentionally no WordOpenXML read: the Undo suite needs minimal observations.
    $tags = New-Object 'System.Collections.Generic.List[string]'
    $controls = $Document.ContentControls
    try {
        for ($i = 1; $i -le $controls.Count; $i++) {
            $cc = $controls.Item($i)
            try { [void]$tags.Add([string]$cc.Tag) } finally { Release-Com $cc }
        }
    } finally { Release-Com $controls }
    $selection = $Document.ActiveWindow.Selection
    try {
        return [ordered]@{
            text = [string]$Document.Content.Text; oMathCount = [int]$Document.OMaths.Count
            controlTags = @($tags.ToArray() | Sort-Object); metadataXml = @(Get-ManagedParts $Document)
            selectionStart = [int]$selection.Start; selectionEnd = [int]$selection.End
            selectionStoryType = [int]$selection.StoryType
        }
    } finally { Release-Com $selection }
}

function Test-SameDocumentState {
    param($A, $B)
    return $A.text -ceq $B.text -and $A.oMathCount -eq $B.oMathCount -and
        (ConvertTo-Json -InputObject @($A.controlTags) -Compress) -ceq (ConvertTo-Json -InputObject @($B.controlTags) -Compress) -and
        (ConvertTo-Json -InputObject @($A.metadataXml) -Compress) -ceq (ConvertTo-Json -InputObject @($B.metadataXml) -Compress)
}

function New-MetadataXml {
    param($Fixture, [string]$EntryId, [string]$Signature)
    $candidateJson = ConvertTo-Json -InputObject @($Fixture.candidates) -Depth 100 -Compress
    $esc = { param($s) [Security.SecurityElement]::Escape([string]$s) }
    return '<l:entry xmlns:l="' + $script:MetadataNs + '" id="' + $EntryId + '" schemaVersion="m0-1" coreVersion="fixed-fixture-1">' +
        '<l:source>' + (& $esc $Fixture.source) + '</l:source><l:selectedCandidateId>' + (& $esc $Fixture.selectedCandidateId) +
        '</l:selectedCandidateId><l:candidateSet encoding="json">' + (& $esc $candidateJson) + '</l:candidateSet><l:nativeSignature>' +
        (& $esc $Signature) + '</l:nativeSignature></l:entry>'
}

function Read-MetadataEntry {
    param($Document)
    $parts = @(Get-ManagedParts $Document)
    if ($parts.Count -ne 1) { throw ("Expected one Locus metadata part, found {0}." -f $parts.Count) }
    $xml = New-Object Xml.XmlDocument; $xml.LoadXml($parts[0])
    $ns = New-Object Xml.XmlNamespaceManager($xml.NameTable); $ns.AddNamespace('l', $script:MetadataNs)
    return [pscustomobject]@{
        EntryId = $xml.DocumentElement.GetAttribute('id')
        Source = $xml.SelectSingleNode('/l:entry/l:source', $ns).InnerText
        SelectedCandidateId = $xml.SelectSingleNode('/l:entry/l:selectedCandidateId', $ns).InnerText
        CandidateJson = $xml.SelectSingleNode('/l:entry/l:candidateSet', $ns).InnerText
        NativeSignature = $xml.SelectSingleNode('/l:entry/l:nativeSignature', $ns).InnerText
    }
}

function Invoke-InjectedFault {
    param([string]$Expected, [string]$Current)
    if ($Expected -ceq $Current) { throw ("M0_INJECTED_FAULT:{0}" -f $Current) }
}

function Convert-ManagedEquation {
    param($Info, $Fixture, [string]$FaultAfter = 'None')
    $doc = $Info.Document
    $candidate = Get-SelectedCandidate $Fixture
    $source = Find-ProbeRange $doc.Content $Fixture.source
    $undo = $script:Word.UndoRecord; $recording = $false
    $math = $null; $mathRange = $null; $control = $null; $part = $null; $selection = $null
    $id = [Guid]::NewGuid().ToString('D')
    try {
        $undo.StartCustomRecord('Locus M0 conversion'); $recording = $true
        $source.InsertXML((New-OoxmlPackage $candidate.omml))
        Invoke-InjectedFault $FaultAfter 'NativeInserted'
        if ($doc.OMaths.Count -ne 1) { throw 'Native insertion did not produce exactly one equation.' }
        $math = $doc.OMaths.Item(1); $mathRange = $math.Range
        $control = $doc.ContentControls.Add(0, $mathRange) # Rich text content control.
        $control.Tag = 'locus:m0:' + $id; $control.Title = 'Locus M0 equation'
        Invoke-InjectedFault $FaultAfter 'ContentControlAdded'
        $metadata = New-MetadataXml $Fixture $id (Get-MathSignature $candidate.omml)
        $part = $doc.CustomXMLParts.Add($metadata)
        Invoke-InjectedFault $FaultAfter 'MetadataAdded'
        $selection = $doc.ActiveWindow.Selection
        $selection.SetRange($control.Range.End, $control.Range.End)
        Invoke-InjectedFault $FaultAfter 'SelectionMoved'
        return $id
    } finally {
        if ($recording) { $undo.EndCustomRecord() }
        Release-Com $source; Release-Com $mathRange; Release-Com $math; Release-Com $control
        Release-Com $part; Release-Com $selection; Release-Com $undo
    }
}

function Remove-ManagedParts {
    param($Document)
    $parts = $Document.CustomXMLParts.SelectByNamespace($script:MetadataNs)
    try {
        for ($i = $parts.Count; $i -ge 1; $i--) {
            $part = $parts.Item($i)
            try { $part.Delete() } finally { Release-Com $part }
        }
    } finally { Release-Com $parts }
}

function Get-WordRangeBoundaryEvidence {
    param($Document, $Range)
    $body=$null; $left=$null; $right=$null
    try {
        $body=$Document.Content
        $left=$Document.Range([int]$body.Start,[int]$Range.Start)
        $right=$Document.Range([int]$Range.End,[int]$body.End)
        return [ordered]@{
            start=[int]$Range.Start; end=[int]$Range.End; text=[string]$Range.Text
            bodyStart=[int]$body.Start; bodyEnd=[int]$body.End
            beforeRange=[string]$left.Text; afterRange=[string]$right.Text
            bodyText=[string]$body.Text
        }
    } finally {Release-Com $body; Release-Com $left; Release-Com $right}
}

function Invoke-RestoreSource {
    param($Document)
    $entry = Read-MetadataEntry $Document
    $controls = $Document.SelectContentControlsByTag('locus:m0:' + $entry.EntryId)
    if ($controls.Count -ne 1) { Release-Com $controls; throw 'Metadata association is not unique.' }
    $cc = $controls.Item(1); $range = $null; $controlRange = $null
    $trace=[ordered]@{}
    $undo = $script:Word.UndoRecord; $recording = $false
    try {
        $undo.StartCustomRecord('Locus M0 restore source'); $recording = $true
        $controlRange=$cc.Range
        $trace.controlBefore=Get-WordRangeBoundaryEvidence $Document $controlRange
        # Keep an actual collapsed Word range alive across wrapper deletion.
        # A stored integer start became stale when Word removed CC boundaries.
        $range=$controlRange.Duplicate
        $range.SetRange([int]$range.Start,[int]$range.Start)
        $trace.anchorBefore=Get-WordRangeBoundaryEvidence $Document $range
        $cc.Delete($true)
        $trace.anchorAfterDelete=Get-WordRangeBoundaryEvidence $Document $range
        if ($range.Start -ne $range.End) {throw 'Restore anchor did not remain collapsed after complete control deletion.'}
        $range.Text = $entry.Source
        $trace.inserted=Get-WordRangeBoundaryEvidence $Document $range
        Remove-ManagedParts $Document
    } finally {
        if ($recording) { $undo.EndCustomRecord() }
        Release-Com $undo; Release-Com $range; Release-Com $controlRange; Release-Com $cc; Release-Com $controls
    }
    return $trace
}

function Invoke-DetachEquation {
    param($Document)
    $entry = Read-MetadataEntry $Document
    $controls = $Document.SelectContentControlsByTag('locus:m0:' + $entry.EntryId)
    if ($controls.Count -ne 1) { Release-Com $controls; throw 'Metadata association is not unique.' }
    $cc = $controls.Item(1); $undo = $script:Word.UndoRecord; $recording = $false
    try {
        $undo.StartCustomRecord('Locus M0 detach'); $recording = $true
        $cc.Delete($false) # COM DeleteContents=false (Office.js has the inverse keepContent parameter).
        Remove-ManagedParts $Document
    } finally {
        if ($recording) { $undo.EndCustomRecord() }
        Release-Com $undo; Release-Com $cc; Release-Com $controls
    }
}

function Get-TextDigest {
    param([string]$Text)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text)))).Replace('-', '').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function New-AdmissionSnapshot {
    param($Info, $Fixture)
    return [ordered]@{
        sessionId = $Info.SessionId; sourceStart = $Info.SourceStart; sourceEnd = $Info.SourceEnd
        sourceText = $Info.Source; documentDigest = Get-TextDigest $Info.Document.Content.Text
        candidateDigest = Get-TextDigest ((Get-SelectedCandidate $Fixture).omml)
        configRevision = 1; expiresAtUtc = [DateTime]::UtcNow.AddSeconds(30)
    }
}

function Test-Admission {
    param($Document, $Expected, $Current)
    $reasons = New-Object 'System.Collections.Generic.List[string]'
    if ($Current.sessionId -cne $Expected.sessionId) { [void]$reasons.Add('document-session-changed') }
    if ($Current.candidateDigest -cne $Expected.candidateDigest) { [void]$reasons.Add('candidate-changed') }
    if ($Current.configRevision -ne $Expected.configRevision) { [void]$reasons.Add('config-changed') }
    if ($Current.inputState -cne 'Committed') { [void]$reasons.Add('input-not-committed') }
    if ($Current.focusState -cne 'DocumentEditor') { [void]$reasons.Add('focus-not-verified-editor') }
    if ([DateTime]::UtcNow -gt $Expected.expiresAtUtc) { [void]$reasons.Add('request-expired') }
    $range = $null
    try {
        if ($null -eq $Document) { throw 'Document session closed.' }
        if ((Get-TextDigest $Document.Content.Text) -cne $Expected.documentDigest) { [void]$reasons.Add('document-content-changed') }
        $range = $Document.Range($Expected.sourceStart, $Expected.sourceEnd)
        if ($range.Text -cne $Expected.sourceText) { [void]$reasons.Add('source-range-changed') }
    } catch { [void]$reasons.Add('document-or-range-unavailable') }
    finally { Release-Com $range }
    return [ordered]@{ allowed = $reasons.Count -eq 0; reasons = @($reasons.ToArray()) }
}
