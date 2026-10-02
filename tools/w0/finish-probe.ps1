$ErrorActionPreference='Stop'
if([IntPtr]::Size -ne 4){throw 'Use x86 Windows PowerShell -STA.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$owner=Get-Content -LiteralPath (Join-Path $root 'artifacts/w0/interactive-owner.json')|ConvertFrom-Json
$processes=@(Get-Process WINWORD -ErrorAction SilentlyContinue)
if($processes.Count -ne 1 -or $processes[0].Id -ne $owner.pid -or $processes[0].StartTime.ToUniversalTime().ToString('o') -ne $owner.startTimeUtc){throw 'Word process ownership differs.'}
$word=[Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
$connector=$word.COMAddIns.Item('Locus.Word.W0').Object
$documents=@($word.Documents)
foreach($document in $documents){$document.Activate();$state=$connector.GetState()|ConvertFrom-Json;if(-not $state.sandbox){throw 'Unowned document remains; no document was closed.'}}
$connector.SaveObservations()
foreach($document in $documents){$document.Close(0)}
if($word.Documents.Count -eq 0){$word.Quit(0)}
@{utc=[DateTime]::UtcNow.ToString('o');pid=$owner.pid;closedOwnedDocuments=$documents.Count;sourceOwner='interactive-owner.json'}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'artifacts/w0/interactive-cleanup.json') -Encoding utf8
Write-Output 'Closed only the verified sandbox documents and their empty Word instance.'
