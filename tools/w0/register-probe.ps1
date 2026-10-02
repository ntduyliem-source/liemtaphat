param([ValidateSet('Install','Uninstall')][string]$Action='Install')
$ErrorActionPreference='Stop'
if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Close Word before changing this trial registration.'}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$assemblyPath=Join-Path $workspace 'src/Locus.Word/bin/Release/net48/Locus.Word.dll'
$classId='{D67B9C40-E18A-4BC3-8D29-8A78FA05E5D0}'
$roots=@('Software\Classes\Locus.Word.W0',"Software\Classes\CLSID\$classId",'Software\Microsoft\Office\Word\Addins\Locus.Word.W0')
$registry=[Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::CurrentUser,[Microsoft.Win32.RegistryView]::Registry32)
try {
    foreach($root in $roots){$existing=$registry.OpenSubKey($root);if($existing){try{if($existing.GetValue('LocusW0Owner') -ne $workspace){throw "Refusing a registration not owned by this workspace: $root"}}finally{$existing.Dispose()}}}
    if($Action -eq 'Uninstall') {
        foreach($root in $roots){$registry.DeleteSubKeyTree($root,$false)}
        Write-Output 'Removed the three owned per-user W0 registration trees.'
        return
    }
    if(-not(Test-Path -LiteralPath $assemblyPath)){throw 'Build Locus.Word first.'}
    $assemblyName=[Reflection.AssemblyName]::GetAssemblyName($assemblyPath)
    function Set-ProbeKey([string]$path,[hashtable]$values){$key=$registry.CreateSubKey($path);try{foreach($name in $values.Keys){$value=$values[$name];$kind=if($value -is [int]){[Microsoft.Win32.RegistryValueKind]::DWord}else{[Microsoft.Win32.RegistryValueKind]::String};$key.SetValue($name,$value,$kind)}}finally{$key.Dispose()}}
    foreach($root in $roots){Set-ProbeKey $root @{LocusW0Owner=$workspace}}
    Set-ProbeKey $roots[0] @{''='Locus.Word.Connect'}
    Set-ProbeKey ($roots[0]+'\CLSID') @{''=$classId}
    Set-ProbeKey $roots[1] @{''='Locus.Word.Connect'}
    $managed=@{Class='Locus.Word.Connect';Assembly=$assemblyName.FullName;RuntimeVersion='v4.0.30319';CodeBase=([Uri]$assemblyPath).AbsoluteUri}
    Set-ProbeKey ($roots[1]+'\InprocServer32') ($managed+@{''='mscoree.dll';ThreadingModel='Both'})
    Set-ProbeKey ($roots[1]+'\InprocServer32\'+$assemblyName.Version.ToString()) $managed
    Set-ProbeKey ($roots[1]+'\ProgId') @{''='Locus.Word.W0'}
    Set-ProbeKey ($roots[1]+'\Implemented Categories\{62C8FE65-4EBB-45E7-B440-6E39B2CDBF29}') @{}
    Set-ProbeKey $roots[2] @{FriendlyName='Locus W0 research connector';Description='Local research observer. Writes only to its own explicitly created test documents.';LoadBehavior=3;CommandLineSafe=0}
    $receipt=@{utc=[DateTime]::UtcNow.ToString('o');registryHive='HKCU';registryView=32;roots=$roots;assembly=$assemblyName.FullName;path=$assemblyPath;sha256=(Get-FileHash -LiteralPath $assemblyPath).Hash}
    $receipt|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $workspace 'artifacts/w0/registration.json') -Encoding utf8
    Write-Output 'Installed per-user x86 trial add-in Locus.Word.W0.'
} finally {$registry.Dispose()}
