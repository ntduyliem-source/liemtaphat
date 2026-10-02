[CmdletBinding()]
param([switch]$KeepInstalled)
$ErrorActionPreference='Stop'
$manualWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manualPackage=Join-Path $manualWorkspace 'artifacts/releases/Locus-Word-0.3.0-alpha-x86'
$manualZip=$manualPackage+'.zip'
$manualOutput=Join-Path $manualWorkspace 'artifacts/m3/package-smoke'
$manualPriorSuite=$env:LOCUS_WORD_TEST_SUITE
$manualPriorFilter=$env:LOCUS_WORD_TEST_FILTER
$manualRegistry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry32)
$manualRoots=@('Software\Classes\Locus.Word.Manual','Software\Classes\CLSID\{B118E51E-D934-4805-858B-784B22816CDD}','Software\Microsoft\Office\Word\Addins\Locus.Word.Manual')
function Read-OtherAddins {
    $manualKey=$manualRegistry.OpenSubKey('Software\Microsoft\Office\Word\Addins')
    try{
        @($manualKey.GetSubKeyNames() | Where-Object {$_ -ne 'Locus.Word.Manual'} | Sort-Object | ForEach-Object {
            $manualChild=$manualKey.OpenSubKey($_)
            try{@{name=$_;values=@($manualChild.GetValueNames() | Sort-Object | ForEach-Object {@{name=$_;value=$manualChild.GetValue($_)}})}}finally{$manualChild.Dispose()}
        }) | ConvertTo-Json -Depth 6 -Compress
    }finally{if($manualKey){$manualKey.Dispose()}}
}
try {
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Close Word before package verification.'}
    foreach($manualRoot in $manualRoots){$manualKey=$manualRegistry.OpenSubKey($manualRoot);if($manualKey){$manualKey.Dispose();throw 'Uninstall the existing Manual copy first; package verification requires empty registration.'}}
    $manualOtherBefore=Read-OtherAddins
    $manualManifest=Get-Content -LiteralPath (Join-Path $manualPackage 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $manualArchive=[IO.Compression.ZipFile]::OpenRead($manualZip)
    try {
        if($manualArchive.Entries.Count -ne $manualManifest.files.Count+1){throw 'Unexpected ZIP entry count.'}
        foreach($manualFile in $manualManifest.files){
            if($manualFile.name -ne [IO.Path]::GetFileName($manualFile.name)){throw 'Unexpected package path.'}
            $manualEntry=$manualArchive.GetEntry($manualFile.name)
            if(-not $manualEntry){throw 'ZIP entry missing.'}
            $manualStream=$manualEntry.Open();$manualHash=[Security.Cryptography.SHA256]::Create()
            try{$manualZipHash=([BitConverter]::ToString($manualHash.ComputeHash($manualStream))).Replace('-','')}finally{$manualStream.Dispose();$manualHash.Dispose()}
            if($manualZipHash -ne $manualFile.sha256 -or (Get-FileHash -LiteralPath (Join-Path $manualPackage $manualFile.name)).Hash -ne $manualFile.sha256){throw 'Package hash mismatch.'}
        }
    }finally{$manualArchive.Dispose()}
    & (Join-Path $manualPackage 'register.ps1') -Action Install
    & (Join-Path $manualPackage 'register.ps1') -Action Install
    $manualKey=$manualRegistry.OpenSubKey($manualRoots[1]+'\InprocServer32')
    try{if($manualKey.GetValue('CodeBase') -ne ([Uri](Join-Path $manualPackage 'Locus.Word.dll')).AbsoluteUri){throw 'Wrong COM assembly path.'}}finally{$manualKey.Dispose()}
    $env:LOCUS_WORD_TEST_SUITE='manual';$env:LOCUS_WORD_TEST_FILTER='manual/convert-undo-restore/'
    & (Join-Path $manualWorkspace 'tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe') $manualOutput
    if($LASTEXITCODE -ne 0){throw 'Packaged add-in smoke failed.'}
    $manualWait=[Diagnostics.Stopwatch]::StartNew()
    while((Get-Process WINWORD -ErrorAction SilentlyContinue) -and $manualWait.Elapsed.TotalSeconds -lt 10){Start-Sleep -Milliseconds 100}
    if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Word has not exited; no process was terminated.'}
    & (Join-Path $manualPackage 'register.ps1') -Action Uninstall
    foreach($manualRoot in $manualRoots){$manualKey=$manualRegistry.OpenSubKey($manualRoot);if($manualKey){$manualKey.Dispose();throw 'Owned registration remains after uninstall.'}}
    if((Read-OtherAddins) -cne $manualOtherBefore){throw 'Other per-user Word add-in registration changed.'}
    if($KeepInstalled){& (Join-Path $manualPackage 'register.ps1') -Action Install}
    @{schemaVersion='locus-m3-package-check/1';capturedAtUtc=[DateTime]::UtcNow.ToString('o');status='PASS';zip='artifacts/releases/Locus-Word-0.3.0-alpha-x86.zip';sha256=(Get-FileHash -LiteralPath $manualZip).Hash;bytes=(Get-Item -LiteralPath $manualZip).Length;manifestFiles=$manualManifest.files.Count;smokeReport='artifacts/m3/package-smoke/report.json';installReinstallUninstall=$true;otherPerUserAddinsUnchanged=$true;leftInstalled=[bool]$KeepInstalled;installedDirectory=$(if($KeepInstalled){$manualPackage}else{$null});limits=@('Existing Word x86 / .NET Framework 4.8 host; not a clean-machine or signed-installer certification.')} | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $manualWorkspace 'artifacts/m3/package-check.json') -Encoding UTF8
}finally{
    $env:LOCUS_WORD_TEST_SUITE=$manualPriorSuite;$env:LOCUS_WORD_TEST_FILTER=$manualPriorFilter
    $manualRegistry.Dispose()
}
