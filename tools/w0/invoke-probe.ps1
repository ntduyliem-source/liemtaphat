param([ValidateSet('State','Inspect','Create','Select','Convert','Zoom','Scroll','Badge','BadgeState','TrialA','TrialB','TrialState','TrialCommit','TrialText','TrialStop','SaveObservations','Disconnect','Reconnect','CloseOwned')][string]$Action='State',[string]$Source='x^2',[int]$Value=0,[int]$End=0,[string]$Label='api')
$ErrorActionPreference='Stop'
if([IntPtr]::Size -ne 4){throw 'Use x86 Windows PowerShell -STA.'}
if($Label -notmatch '^[a-z0-9-]+$'){throw 'Invalid artifact label.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$owner=Get-Content -LiteralPath (Join-Path $root 'artifacts/w0/interactive-owner.json')|ConvertFrom-Json
$processes=@(Get-Process WINWORD -ErrorAction SilentlyContinue)
if($processes.Count -ne 1 -or $processes[0].Id -ne $owner.pid -or $processes[0].StartTime.ToUniversalTime().ToString('o') -ne $owner.startTimeUtc){throw 'Word process ownership differs.'}
$word=[Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
$addin=$word.COMAddIns.Item('Locus.Word.W0')
if($Action -eq 'Reconnect'){$addin.Connect=$true}
$connector=$addin.Object
if($null -eq $connector){throw 'Connector is unavailable.'}
$before=$connector.GetState()|ConvertFrom-Json
$outcome='PASS';$errorText=$null
$inspection=$null
try {
    if($Action -in @('Inspect','Select','Convert','Zoom','Scroll','CloseOwned') -and -not $before.sandbox){throw 'Refusing unarmed document.'}
    switch($Action){
        'SaveObservations' {$connector.SaveObservations()}
        'TrialA' {[void]$connector.StartSpaceTrial('keep-source',$true)}
        'TrialB' {[void]$connector.StartSpaceTrial('native-live',$true)}
        'TrialState' {$inspection=$connector.GetSpaceTrialState()|ConvertFrom-Json}
        'TrialCommit' {$inspection=$connector.SpaceTrialAction('commit','',$Value)|ConvertFrom-Json}
        'TrialText' {$inspection=$connector.SpaceTrialAction('keep-text','',0)|ConvertFrom-Json}
        'TrialStop' {$inspection=$connector.SpaceTrialAction('stop','',0)|ConvertFrom-Json}
        'BadgeState' {$inspection=$connector.GetBadgeState()|ConvertFrom-Json}
        'Inspect' {
            $adapter=Join-Path $root 'src/Locus.Word/bin/Release/net48/Locus.Word.dll'
            $core=Join-Path $root 'src/Locus.Word/bin/Release/net48/Locus.Core.dll'
            $pia=Join-Path $env:WINDIR 'assembly/GAC_MSIL/Microsoft.Office.Interop.Word/15.0.0.0__71e9bce111e9429c/Microsoft.Office.Interop.Word.dll'
            Add-Type -Path $core
            Add-Type -Path $adapter
            Add-Type -ReferencedAssemblies @($adapter,$core,$pia) -TypeDefinition @'
public static class W0InspectBridge {
    public static Locus.Word.ManagedSnapshot Read(object document, object control) {
        return Locus.Word.WordOperations.ReadUnique((Microsoft.Office.Interop.Word.Document)document, (Microsoft.Office.Interop.Word.ContentControl)control);
    }
}
'@
            $document=$word.ActiveDocument
            $inspection=@(foreach($control in $document.ContentControls){
                $valid=$true;$reason=$null;$source=$null
                try{$snapshot=[W0InspectBridge]::Read($document,$control);$source=$snapshot.Candidates.OriginalReplacement}catch{$valid=$false;$reason=$_.Exception.Message}
                @{id=$control.ID;tag=$control.Tag;start=$control.Range.Start;end=$control.Range.End;text=$control.Range.Text;valid=$valid;reason=$reason;source=$source;outside=$document.Range($control.Range.End,$document.Content.End).Text}
            })
        }
        'Create' {[void]$connector.CreateSandbox($Source)}
        'Select' {$word.Selection.SetRange($Value,$End)}
        'Convert' {[void]$connector.ConvertSandboxSelection($Value,$before.session,$before.documentId,[long]$before.revision,[int]$before.selection[0],[int]$before.selection[1])}
        'Zoom' {$word.ActiveWindow.View.Zoom.Percentage=$Value}
        'Scroll' {$word.ActiveWindow.SmallScroll($Value)}
        'Badge' {$connector.SetBadgeEnabled([bool]$Value)}
        'Disconnect' {$connector.SaveObservations();$addin.Connect=$false}
        'CloseOwned' {$word.ActiveDocument.Close(0)}
    }
}catch{$outcome='REFUSED';$errorText=$_.Exception.Message}
$after=$connector.GetState()|ConvertFrom-Json
$result=@{action=$Action;outcome=$outcome;error=$errorText;utc=[DateTime]::UtcNow.ToString('o');before=$before;after=$after;inspection=$inspection}
$artifactDirectory=Join-Path $root 'artifacts/w0/native'
[void][IO.Directory]::CreateDirectory($artifactDirectory)
$result|ConvertTo-Json -Depth 15|Set-Content -LiteralPath (Join-Path $artifactDirectory ($Label+'.json')) -Encoding utf8
$result|ConvertTo-Json -Depth 15
