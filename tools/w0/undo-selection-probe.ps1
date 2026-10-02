[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4) { throw 'Use x86 Windows PowerShell -STA.' }
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspaceRoot 'tools/m0/word-helpers.ps1')
if (@(Get-Process WINWORD -ErrorAction SilentlyContinue).Count) { throw 'Refusing existing Word processes.' }
$activation = Get-WordActivationPreflight
if (-not $activation.isMicrosoftWord) { throw 'Microsoft Word registration required.' }
$runDirectory = Join-Path $workspaceRoot ('artifacts/w0/undo-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
[void][IO.Directory]::CreateDirectory($runDirectory)
$report = [ordered]@{capturedAtUtc=[DateTime]::UtcNow.ToString('o');results=@();limits=@('New synthetic main-story documents only; no keyboard Undo, header/table or crash claim.')}
$fixture = (Get-Content (Join-Path $workspaceRoot 'fixtures/m0/word/candidates.json') -Raw -Encoding UTF8 | ConvertFrom-Json).cases | Where-Object caseId -eq '03-precedence'
$candidate = Get-SelectedCandidate $fixture
$xml = New-OoxmlPackage $candidate.omml
$word = $null
try {
    $word = New-Object -ComObject Word.Application
    $word.Visible=$false; $word.DisplayAlerts=0
    $report.wordVersion=[string]$word.Version; $report.wordBuild=[string]$word.Build
    $report.processes=@(Get-Process WINWORD | Select-Object Id,StartTime,Path)
    Write-JsonFile (Join-Path $runDirectory 'ownership.json') $report.processes
    foreach ($variant in @('range-xml','selection-xml','selection-delete-xml','selection-type-empty-xml','cc-before-xml','single-xml-range','single-xml-selection')) {
        $doc=$null; $selection=$null; $undo=$null; $cc=$null; $range=$null
        $row=[ordered]@{variant=$variant;status='FAIL';content=$false;metadata=$false;selection=$false;redo=$false;error=$null}
        try {
            Write-Output ('Testing '+$variant)
            Write-JsonFile (Join-Path $runDirectory 'progress.json') @{variant=$variant;stage='new-document'}
            $doc=$word.Documents.Add()
            $raw=[string]$fixture.source; $prefix='Before: '; $suffix=' After.'+[char]13
            $doc.Content.Text=$prefix+$raw+$suffix
            $selection=$doc.ActiveWindow.Selection; $selection.SetRange($prefix.Length, $prefix.Length+$raw.Length)
            $beforeStart=[int]$selection.Start; $beforeEnd=[int]$selection.End; $beforeText=[string]$doc.Content.Text
            $doc.UndoClear(); $undo=$word.UndoRecord; $undo.StartCustomRecord('Locus W0 '+$variant)
            try {
                if ($variant -like 'single-xml-*') {
                    [xml]$package=$xml; $ns=New-Object Xml.XmlNamespaceManager($package.NameTable); $ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
                    $paragraph=$package.SelectSingleNode('//w:body/w:p',$ns)
                    $sdt=$package.CreateElement('w','sdt',$ns.LookupNamespace('w'))
                    $pr=$package.CreateElement('w','sdtPr',$ns.LookupNamespace('w')); $tag=$package.CreateElement('w','tag',$ns.LookupNamespace('w'))
                    $tag.SetAttribute('val',$ns.LookupNamespace('w'),'locus-w0-complete-payload'); [void]$pr.AppendChild($tag); [void]$sdt.AppendChild($pr)
                    $content=$package.CreateElement('w','sdtContent',$ns.LookupNamespace('w'))
                    foreach ($child in @($paragraph.ChildNodes)) { if ($child.LocalName -ne 'pPr') { [void]$content.AppendChild($child) } }
                    [void]$sdt.AppendChild($content); [void]$paragraph.AppendChild($sdt)
                    if ($variant -eq 'single-xml-selection') { $selection.InsertXML($package.OuterXml) }
                    else { $range=$doc.Range($beforeStart,$beforeEnd); $range.InsertXML($package.OuterXml) }
                } else {
                    if ($variant -eq 'selection-delete-xml') { [void]$selection.Delete() }
                    if ($variant -eq 'selection-type-empty-xml') { $selection.TypeText('') }
                    if ($variant -eq 'cc-before-xml') {
                        $cc=$doc.ContentControls.Add(0,$selection.Range); $cc.Tag='locus-w0-complete-payload'; $cc.Range.InsertXML($xml)
                    } else {
                        if ($variant -eq 'range-xml') { $range=$doc.Range($beforeStart,$beforeEnd); $range.InsertXML($xml) } else { $selection.InsertXML($xml) }
                        $cc=$doc.ContentControls.Add(0,$doc.OMaths.Item(1).Range); $cc.Tag='locus-w0-complete-payload'
                    }
                }
                $selection.SetRange($doc.ContentControls.Item(1).Range.End,$doc.ContentControls.Item(1).Range.End)
            } finally { $undo.EndCustomRecord() }
            $convertedText=[string]$doc.Content.Text
            $convertedCount=[int]$doc.OMaths.Count; $convertedControlCount=[int]$doc.ContentControls.Count
            $undone=[bool]$doc.Undo(1)
            $row.content=$undone -and ([string]$doc.Content.Text -ceq $beforeText) -and $doc.OMaths.Count -eq 0
            $row.metadata=$doc.ContentControls.Count -eq 0
            $row.selection=([int]$selection.Start -eq $beforeStart -and [int]$selection.End -eq $beforeEnd)
            $row.beforeSelection=@($beforeStart,$beforeEnd); $row.afterUndoSelection=@([int]$selection.Start,[int]$selection.End)
            $redone=[bool]$doc.Redo(1)
            $row.redo=$redone -and ([string]$doc.Content.Text -ceq $convertedText) -and $doc.OMaths.Count -eq $convertedCount -and $doc.ContentControls.Count -eq $convertedControlCount
            if ($row.content -and $row.metadata -and $row.selection -and $row.redo) { $row.status='PASS' }
        } catch { $row.error=$_.Exception.Message }
        finally {
            Release-Com $cc; Release-Com $range; Release-Com $selection; Release-Com $undo
            if ($doc) {$doc.Close(0); Release-Com $doc}
            $report.results+=$row; Write-JsonFile (Join-Path $runDirectory 'report.json') $report
            Write-Output ($variant+': '+$row.status+' selection='+$row.selection+' content='+$row.content+' '+$row.error)
        }
    }
} finally { if ($word) { if ($word.Documents.Count -eq 0) {$word.Quit()}; Release-Com $word }; Write-Output $runDirectory }
