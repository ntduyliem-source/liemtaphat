param([ValidateSet('create','snapshot','zoom','save','close')][string]$Action='snapshot',[int]$Zoom=100,[string]$Output='word-state.json')
$ErrorActionPreference='Stop'
if([IntPtr]::Size -ne 4){throw 'Run in Windows PowerShell x86 to use Microsoft Word registration.'}
. (Join-Path $PSScriptRoot '../m0/word-helpers.ps1')
$activation=Get-WordActivationPreflight
if(!$activation.isMicrosoftWord){throw 'Registered host is not Microsoft Word.'}
$run=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../artifacts/host-review/20260928-complete'))
$docPath=Join-Path $run 'word-microsoft-native.docx'
if($Action -eq 'create'){
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Existing Word instance.'}
    $app=New-Object -ComObject Word.Application
    $app.Visible=$true
    $doc=$app.Documents.Add()
    $doc.Content.Text=[IO.File]::ReadAllText((Join-Path $run 'word-source.txt'))
    $doc.SaveAs2($docPath,12,$false,'',$false)
    $doc.Range(0,0).Select()
    $app.ActiveWindow.View.Zoom.Percentage=100
    $owned=Get-Process WINWORD
    if(@($owned).Count -ne 1 -or $owned.Path -ne $activation.executable){throw 'Unexpected Word process.'}
    @{pid=$owned.Id;startTimeUtc=$owned.StartTime.ToUniversalTime().ToString('o');executable=$owned.Path} | ConvertTo-Json | Set-Content (Join-Path $run 'word-receipt.json') -Encoding UTF8
}else{
    $app=[Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
    $doc=$app.ActiveDocument
    if($doc.FullName -ne $docPath){throw 'Active document is not the owned test document.'}
}
if($Action -eq 'zoom'){$app.ActiveWindow.View.Zoom.Percentage=$Zoom}
if($Action -eq 'save'){$doc.Save()}
if($Action -eq 'close'){$doc.Close(0);if($app.Documents.Count -eq 0){$app.Quit()};exit}
$scan=$null
try{$connector=$app.COMAddIns.Item('Locus.Word.Manual').Object;$scan=$connector.GetScanState() | ConvertFrom-Json}catch{}
$record=@{application=$app.Name;path=$app.Path;version=$app.Version;build=$app.Build;document=$doc.FullName;text=$doc.Content.Text;math=$doc.OMaths.Count;controls=$doc.ContentControls.Count;zoom=$app.ActiveWindow.View.Zoom.Percentage;selection=@{start=$app.Selection.Start;end=$app.Selection.End};scan=$scan}
[IO.File]::WriteAllText((Join-Path $run $Output),($record | ConvertTo-Json -Depth 15),[Text.UTF8Encoding]::new($false))
@{application=$record.application;path=$record.path;math=$record.math;controls=$record.controls;zoom=$record.zoom;scan=$scan} | ConvertTo-Json -Depth 15
