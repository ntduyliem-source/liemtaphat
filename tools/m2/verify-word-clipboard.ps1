[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4 -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    throw 'Run with 32-bit Windows PowerShell -STA; this machine registers Microsoft Word in the 32-bit view.'
}
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspaceRoot 'tools/m0/word-helpers.ps1')
if (@(Get-Process WINWORD -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Existing Word detected; refusing to attach to user documents.' }
$activation = Get-WordActivationPreflight
if (-not $activation.isMicrosoftWord) { throw 'COM registration is not Microsoft Word.' }
$runDirectory = Join-Path $workspaceRoot ('artifacts/m2/clipboard/run-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ'))
[void][IO.Directory]::CreateDirectory($runDirectory)
$producer = Join-Path $workspaceRoot 'tests/Locus.Desktop.Tests/bin/Release/net10.0-windows/Locus.Desktop.Tests.exe'
Add-Type -AssemblyName System.Windows.Forms
$report = [ordered]@{ capturedAtUtc=[DateTime]::UtcNow.ToString('o'); runDirectory=$runDirectory; activation=$activation; wordVersion=$null; wordBuild=$null; results=@(); limits=@('Word COM API consumes the real Windows clipboard in new synthetic documents. This does not prove keyboard paste or native equation integration.', 'SVG clipboard deliberately includes a text fallback. Vector import is checked separately using the saved SVG file.') }
$word = $null
function Stage([string]$name) {
    Write-Output $name
    Write-JsonFile (Join-Path $runDirectory 'progress.json') @{stage=$name;time=[DateTime]::UtcNow.ToString('o')}
}
try {
    Stage 'create-word'
    $word = New-Object -ComObject Word.Application
    if (-not [string]::Equals([string]$word.Path, [IO.Path]::GetDirectoryName($activation.executable), [StringComparison]::OrdinalIgnoreCase)) { throw 'Activated application path mismatch.' }
    $report.wordVersion = [string]$word.Version; $report.wordBuild = [string]$word.Build
    $word.Visible = $false; $word.DisplayAlerts = 0
    foreach ($case in @(
        @{id='png-direct';format='Png';choice=0;operation='paste'},
        @{id='png-repair';format='Png';choice=1;operation='paste'},
        @{id='svg-import';format='Svg';choice=1;operation='import'},
        @{id='svg-text';format='Svg';choice=0;operation='text'},
        @{id='latex';format='Latex';choice=1;operation='text'},
        @{id='source';format='Source';choice=0;operation='text'},
        @{id='mathml';format='MathMl';choice=0;operation='text'},
        @{id='omml';format='Omml';choice=1;operation='text'}
    )) {
        $directory = Join-Path $runDirectory $case.id
        $document = $null; $range = $null
        $result = [ordered]@{ id=$case.id; status='FAIL'; directory=$directory; shapes=0; textMatches=$null; error=$null }
        try {
            Stage ($case.id + '/new-document')
            $document = $word.Documents.Add()
            $range = $document.Range(0,0)
            Stage ($case.id + '/produce-clipboard')
            & $producer --clipboard-fixture $directory $case.format $case.choice
            if ($LASTEXITCODE -ne 0) { throw 'Desktop clipboard producer failed.' }
            $fixture = Get-Content -LiteralPath (Join-Path $directory 'fixture.json') -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($null -ne $fixture.expectedText) {
                $result.unicodeClipboardMatches = [System.Windows.Forms.Clipboard]::GetText([System.Windows.Forms.TextDataFormat]::UnicodeText) -ceq $fixture.expectedText
                if (-not $result.unicodeClipboardMatches) { throw 'Clipboard text changed between producer and external consumer.' }
            }
            Stage ($case.id + '/consume-clipboard')
            if ($case.operation -eq 'import') {
                $shape = $document.InlineShapes.AddPicture((Join-Path $directory 'expected.svg'), $false, $true, $range)
                Release-Com $shape
            } elseif ($case.operation -eq 'text') {
                $range.PasteSpecial([Type]::Missing, $false, [Type]::Missing, $false, 2)
                $actual = [string]$document.Content.Text
                [IO.File]::WriteAllText((Join-Path $directory 'received-text.txt'), $actual, $script:Utf8NoBom)
                $result.textMatches = $actual.TrimEnd([char]13) -ceq $fixture.expectedText
                if (-not $result.textMatches) {
                    if ($case.format -eq 'MathMl' -and $document.OMaths.Count -eq 1) {
                        $observedMath = ([string]$document.Content.WordOpenXML).Normalize([Text.NormalizationForm]::FormKC)
                        $result.mathMatches = (Get-MathSignature $observedMath) -ceq (Get-MathSignature $fixture.expectedOmml)
                        $result.hostBehavior = 'Word automatically interprets MathML text as an equation, including with Paste Special Text.'
                        if (-not $result.mathMatches) { throw 'Word interpreted MathML with a different structure.' }
                    } else { throw 'Pasted text differs from the selected candidate export.' }
                }
            } else { $range.Paste() }
            $result.shapes = $document.InlineShapes.Count
            if ($case.operation -ne 'text' -and $result.shapes -ne 1) { throw 'Expected exactly one image.' }
            Write-JsonFile (Join-Path $directory 'paste-observation.json') $result
            Stage ($case.id + '/read-document-data')
            [IO.File]::WriteAllText((Join-Path $directory 'received-flat-opc.xml'), [string]$document.Content.WordOpenXML, $script:Utf8NoBom)
            $result.status = 'PASS'
        } catch { $result.error = $_.Exception.Message }
        finally {
            Release-Com $range
            if ($null -ne $document) { $document.Close(0); Release-Com $document }
            $report.results += $result
            Write-JsonFile (Join-Path $runDirectory 'report.json') $report
            Write-Output ($case.id + ': ' + $result.status + ' ' + $result.error)
        }
    }
} finally {
    if ($null -ne $word) { if ($word.Documents.Count -eq 0) { $word.Quit() }; Release-Com $word }
    Write-JsonFile (Join-Path $runDirectory 'report.json') $report
    Write-Output ('Report: ' + (Join-Path $runDirectory 'report.json'))
}
if (@($report.results | Where-Object status -ne 'PASS').Count -gt 0) { exit 1 }
