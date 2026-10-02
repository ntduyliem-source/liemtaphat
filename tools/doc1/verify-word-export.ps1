[CmdletBinding()]
param([string]$Directory='artifacts/doc1/verification')
$ErrorActionPreference='Stop'
if([IntPtr]::Size -ne 4 -or [Threading.Thread]::CurrentThread.ApartmentState -ne 'STA'){throw 'Use 32-bit Windows PowerShell -STA for this Word x86 baseline.'}
if(@(Get-Process WINWORD -ErrorAction SilentlyContinue).Count -gt 0){throw 'Existing Word process; this probe only uses a separate clean instance.'}
$dWordRoot=(Resolve-Path -LiteralPath $Directory).Path
$dRunRoot=Join-Path $dWordRoot ('word-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))
[void][IO.Directory]::CreateDirectory($dRunRoot)
function Write-Stage([string]$Name){$dMessage=[DateTime]::UtcNow.ToString('o')+' '+$Name;[IO.File]::AppendAllText((Join-Path $dRunRoot 'stages.log'),$dMessage+[Environment]::NewLine);Write-Output $dMessage}
$dWord=$null
$dAnchor=$null
$dWordChecks=@()
try {
    # Document API on a separate application instance; no keyboard/UI automation or user document changes.
    Write-Stage 'Activate Word'
    $dWord=New-Object -ComObject Word.Application
    $dWord.Visible=$false
    $dWord.DisplayAlerts=0
    $dAnchor=$dWord.Documents.Add()
    foreach($dFixture in @(@{name='mixed-native';count=5},@{name='products-ignore';count=1})) {
        $dDoc=$null
        try {
            $dInput=Join-Path $dWordRoot ($dFixture.name+'.docx')
            Write-Stage ('Open '+$dFixture.name)
            $dDoc=$dWord.Documents.Open($dInput,$false,$true,$false)
            if($dDoc.OMaths.Count -ne $dFixture.count){throw "Wrong Word OMath count: $($dDoc.OMaths.Count)"}
            $dText=$dDoc.Content.Text
            if($dFixture.name -eq 'mixed-native' -and (-not $dText.Contains('hoa-[H2SO4]') -or -not $dText.Contains('https://example.org/bai') -or -not $dText.Contains('Giữ câu chữ <b> & dấu tab:'))){throw 'Word lost plain text'}
            if($dFixture.name -eq 'products-ignore' -and (-not $dText.Contains('3') -or -not $dText.Contains('H') -or -not $dText.Contains('O'))){throw 'Product result missing'}
            Write-Stage ('Render '+$dFixture.name)
            $dDoc.ExportAsFixedFormat((Join-Path $dRunRoot ($dFixture.name+'.pdf')),17)
            $dNative=@()
            for($dIndex=1;$dIndex -le $dDoc.OMaths.Count;$dIndex++){$dNative+=@{index=$dIndex;text=$dDoc.OMaths.Item($dIndex).Range.Text;type=[int]$dDoc.OMaths.Item($dIndex).Type}}
            Write-Stage ('SaveAs '+$dFixture.name)
            $dDoc.SaveAs2((Join-Path $dRunRoot ($dFixture.name+'-word-roundtrip.docx')),16)
            $dDoc.Close(0)
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($dDoc)
            Write-Stage ('Reopen '+$dFixture.name)
            $dDoc=$dWord.Documents.Open((Join-Path $dRunRoot ($dFixture.name+'-word-roundtrip.docx')),$false,$true,$false)
            if($dDoc.OMaths.Count -ne $dFixture.count -or $dDoc.Content.Text -ne $dText){throw 'Word roundtrip changed formulas/text'}
            $dWordChecks+=@{name=$dFixture.name;status='PASS';nativeEquations=$dNative;paragraphs=$dDoc.Paragraphs.Count;pages=$dDoc.ComputeStatistics(2)}
        } finally {if($null -ne $dDoc){try{$dDoc.Close(0)}catch{};try{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($dDoc)}catch{}}}
    }
    $dReport=@{status='PASSED';wordVersion=$dWord.Version;wordBuild=$dWord.Build;checks=$dWordChecks;artifacts=$dRunRoot;scope='Native document API open, OMath inspection, PDF render and save/reopen; not Word connector or keyboard acceptance'} | ConvertTo-Json -Depth 8
    [IO.File]::WriteAllText((Join-Path $dWordRoot 'word-export-tests.json'),$dReport,[Text.UTF8Encoding]::new($false))
    'Word native export checks passed'
} catch {
    [IO.File]::WriteAllText((Join-Path $dWordRoot 'word-export-error.txt'),($_ | Out-String))
    throw
} finally {if($null -ne $dAnchor){try{$dAnchor.Close(0)}catch{};try{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($dAnchor)}catch{}};if($null -ne $dWord){try{$dWord.Quit(0)}catch{};try{[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($dWord)}catch{}}}
