param([Parameter(Mandatory=$true)][string]$OwnershipPath)
$ErrorActionPreference='Stop'
$locusRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\artifacts\m0\word'))
$locusOwnershipFull=[IO.Path]::GetFullPath($OwnershipPath)
if (-not $locusOwnershipFull.StartsWith($locusRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Ownership file must be inside this workspace Word artifacts' }
$locusOwner=Get-Content -LiteralPath $locusOwnershipFull -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $locusOwner.verified) { throw 'Ownership was not verified' }
$locusOwned=Get-Process -Id $locusOwner.pid -ErrorAction SilentlyContinue
if (-not $locusOwned) { Write-Output 'Owned process already exited'; exit 0 }
if ($locusOwned.ProcessName -ine 'WINWORD' -or $locusOwned.StartTime.ToUniversalTime().ToString('o') -cne $locusOwner.processStartUtc) { throw 'PID/start time no longer matches the recorded owned Word process' }
$locusAllWord=@(Get-Process -Name WINWORD)
if ($locusAllWord.Count -ne 1) { throw 'Other Word processes exist; cannot use unique ROT recovery' }
$locusRecoveryApp=$null
try {
    $locusRecoveryApp=[Runtime.InteropServices.Marshal]::GetActiveObject('Word.Application')
    if ([string]$locusRecoveryApp.Path -ine [string]$locusOwner.wordPath) { throw 'ROT points to a different application' }
    if ($locusRecoveryApp.Documents.Count -ne 0) { throw 'Documents remain open; do not close or discard them' }
    [object]$locusNoSave=0
    $locusRecoveryApp.Quit([ref]$locusNoSave)
    Write-Output 'Gracefully quit the unique verified owned Word instance with no open documents'
} finally {
    if ($null -ne $locusRecoveryApp) { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($locusRecoveryApp) }
}
