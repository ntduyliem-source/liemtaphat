param([string]$Source='x^2')
$ErrorActionPreference='Stop'
if([IntPtr]::Size -ne 4){throw 'Run with SysWOW64 Windows PowerShell (x86) and -STA.'}
if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Refusing existing Word. This launcher owns only a fresh process.'}
. (Join-Path $PSScriptRoot '../m0/word-helpers.ps1')
$preflight=Get-WordActivationPreflight
if(-not $preflight.isMicrosoftWord){throw ($preflight|ConvertTo-Json -Depth 5)}
$started=[DateTime]::UtcNow
$word=New-Object -ComObject Word.Application
$word.Visible=$true
$addin=$word.COMAddIns.Item('Locus.Word.W0')
$addin.Connect=$true
$connector=$addin.Object
if($null -eq $connector){throw 'Word did not expose the trial connector object.'}
$id=$connector.CreateSandbox($Source)
$owned=Get-Process WINWORD|Where-Object {$_.StartTime.ToUniversalTime() -ge $started.AddSeconds(-2)}
$receipt=@{pid=$owned.Id;startTimeUtc=$owned.StartTime.ToUniversalTime().ToString('o');directory=$connector.ObservationDirectory();documentId=$id;state=($connector.GetState()|ConvertFrom-Json)}
$receipt|ConvertTo-Json -Depth 12|Set-Content -LiteralPath (Join-Path $PSScriptRoot '../../artifacts/w0/interactive-owner.json') -Encoding utf8
$receipt|ConvertTo-Json -Depth 12
# Word remains visible. The read-only pipe and add-in live inside Word, not this launcher.
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($connector)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($addin)
[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($word)
