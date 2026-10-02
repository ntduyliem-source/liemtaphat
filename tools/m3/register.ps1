[CmdletBinding()]
param([ValidateSet('Install','Uninstall')][string]$Action='Install', [string]$AssemblyPath)
$ErrorActionPreference='Stop'
if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Close Word before changing Locus Manual registration.'}
if(-not $AssemblyPath){
    $localAssembly=Join-Path $PSScriptRoot 'Locus.Word.dll'
    $AssemblyPath=if(Test-Path -LiteralPath $localAssembly){$localAssembly}else{Join-Path $PSScriptRoot '../../src/Locus.Word/bin/Release/net48/Locus.Word.dll'}
}
$manualAssembly=[IO.Path]::GetFullPath($AssemblyPath)
$manualOwner=Split-Path $manualAssembly -Parent
$manualClassId='{B118E51E-D934-4805-858B-784B22816CDD}'
$manualRoots=@('Software\Classes\Locus.Word.Manual',"Software\Classes\CLSID\$manualClassId",'Software\Microsoft\Office\Word\Addins\Locus.Word.Manual')
$manualRegistry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry32)
try {
    foreach($manualRoot in $manualRoots){
        $manualExisting=$manualRegistry.OpenSubKey($manualRoot)
        if($manualExisting){try{if($manualExisting.GetValue('LocusManualOwner') -ne $manualOwner){throw "Registration belongs to a different Locus folder; uninstall that copy first: $manualRoot"}}finally{$manualExisting.Dispose()}}
    }
    if($Action -eq 'Uninstall'){
        foreach($manualRoot in $manualRoots){$manualRegistry.DeleteSubKeyTree($manualRoot,$false)}
        Write-Output 'Removed only the three owned per-user Locus.Word.Manual registration trees.'
        return
    }
    if(-not(Test-Path -LiteralPath $manualAssembly)){throw 'Build or extract the Locus Word package first.'}
    $manualName=[Reflection.AssemblyName]::GetAssemblyName($manualAssembly)
    function Set-ManualKey([string]$Path,[hashtable]$Values){
        $manualKey=$manualRegistry.CreateSubKey($Path)
        try{foreach($manualValue in $Values.Keys){$manualKind=if($Values[$manualValue] -is [int]){[Microsoft.Win32.RegistryValueKind]::DWord}else{[Microsoft.Win32.RegistryValueKind]::String};$manualKey.SetValue($manualValue,$Values[$manualValue],$manualKind)}}finally{$manualKey.Dispose()}
    }
    foreach($manualRoot in $manualRoots){Set-ManualKey $manualRoot @{LocusManualOwner=$manualOwner}}
    Set-ManualKey $manualRoots[0] @{''='Locus.Word.ManualConnect'}
    Set-ManualKey ($manualRoots[0]+'\CLSID') @{''=$manualClassId}
    Set-ManualKey $manualRoots[1] @{''='Locus.Word.ManualConnect'}
    $manualManaged=@{Class='Locus.Word.ManualConnect';Assembly=$manualName.FullName;RuntimeVersion='v4.0.30319';CodeBase=([Uri]$manualAssembly).AbsoluteUri}
    Set-ManualKey ($manualRoots[1]+'\InprocServer32') ($manualManaged+@{''='mscoree.dll';ThreadingModel='Both'})
    Set-ManualKey ($manualRoots[1]+'\InprocServer32\'+$manualName.Version.ToString()) $manualManaged
    Set-ManualKey ($manualRoots[1]+'\ProgId') @{''='Locus.Word.Manual'}
    Set-ManualKey ($manualRoots[1]+'\Implemented Categories\{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}') @{}
    Set-ManualKey $manualRoots[2] @{FriendlyName='Locus - Manual formulas (alpha)';Description='Explicit selection, preview and confirmation. Native equations with Undo and source restore. No automatic conversion.';LoadBehavior=3;CommandLineSafe=0}
    Write-Output "Installed per-user Word x86 add-in Locus.Word.Manual from $manualOwner"
}finally{$manualRegistry.Dispose()}
