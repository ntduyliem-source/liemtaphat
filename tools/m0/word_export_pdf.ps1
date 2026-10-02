param([Parameter(Mandatory=$true)][string]$SourcePath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$locusArtifactRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\artifacts\m0'))
$locusSource=[IO.Path]::GetFullPath($SourcePath)
$locusOutput=[IO.Path]::GetFullPath($OutputPath)
foreach ($locusPath in @($locusSource,$locusOutput)) {
    if (-not $locusPath.StartsWith($locusArtifactRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Only workspace M0 synthetic artifacts are accepted' }
}
if (@(Get-Process -Name WINWORD -ErrorAction SilentlyContinue).Count -gt 0) { throw 'Refuse while Word is already running' }
$locusServerKey=Get-Item 'Registry::HKEY_CLASSES_ROOT\CLSID\{000209FF-0000-0000-C000-000000000046}\LocalServer32'
if ($locusServerKey.GetValue('') -notmatch '(?i)WINWORD\.EXE') { throw 'Current COM registration is not Microsoft Word; use the verified launcher bitness' }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class LocusM0PdfOwner {
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
}
'@
$locusExportApp=$null; $locusExportDocument=$null; $locusOwned=$false
$locusStart=[DateTime]::UtcNow
try {
    $locusExportApp=New-Object -ComObject Word.Application
    $locusExportDocument=$locusExportApp.Documents.Open($locusSource,$false,$true,$false)
    [uint32]$locusExportPid=0
    [void][LocusM0PdfOwner]::GetWindowThreadProcessId([IntPtr]$locusExportDocument.ActiveWindow.Hwnd,[ref]$locusExportPid)
    $locusProcess=Get-Process -Id $locusExportPid
    if ($locusProcess.ProcessName -ine 'WINWORD' -or $locusProcess.StartTime.ToUniversalTime() -lt $locusStart.AddSeconds(-2)) { throw 'Unable to verify new Microsoft Word ownership' }
    $locusOwned=$true
    $locusExportApp.Visible=$false
    $locusExportApp.DisplayAlerts=0
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($locusOutput)) | Out-Null
    $locusExportDocument.ExportAsFixedFormat($locusOutput,17)
    if (-not (Test-Path -LiteralPath $locusOutput)) { throw 'Word did not produce a PDF' }
    Write-Output ('Word-native PDF exported from read-only synthetic document: '+$locusOutput)
} finally {
    if ($null -ne $locusExportDocument) { $locusExportDocument.Close(0); [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($locusExportDocument) }
    if ($null -ne $locusExportApp) {
        if ($locusOwned) { [object]$locusDoNotSave=0; $locusExportApp.Quit([ref]$locusDoNotSave) }
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($locusExportApp)
    }
    [GC]::Collect(); [GC]::WaitForPendingFinalizers()
}
