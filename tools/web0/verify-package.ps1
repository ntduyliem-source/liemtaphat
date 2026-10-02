$ErrorActionPreference='Stop'
$webWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$package=Get-Content -LiteralPath (Join-Path $webWorkspace 'artifacts/web/package.json') -Raw | ConvertFrom-Json
$webZip=Join-Path $webWorkspace $package.path
if((Get-FileHash -LiteralPath $webZip -Algorithm SHA256).Hash -ne $package.sha256){throw 'Package archive hash changed.'}
$expected=Get-Content -LiteralPath (Join-Path $webWorkspace 'artifacts/web/payload-manifest.json') -Raw | ConvertFrom-Json
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive=[IO.Compression.ZipFile]::OpenRead($webZip)
try {
    $count=0
    foreach($file in $expected){
        $entry=$archive.GetEntry(('Locus-Web-WEB0-static/wwwroot/'+$file.path))
        if($null -eq $entry){throw ('Missing package asset: '+$file.path)}
        $stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create()
        try {$actual=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}
        finally {$stream.Dispose();$sha.Dispose()}
        if($actual -ne $file.sha256 -or $entry.Length -ne $file.bytes){throw ('Package asset mismatch: '+$file.path)}
        $count++
    }
    $notices=@($archive.Entries | Where-Object {$_.FullName -match '/licenses/.+' -and $_.Length -gt 0})
    if($notices.Count -lt 4){throw 'Expected redistribution notices in archive.'}
    [ordered]@{status='PASS';capturedAtUtc=[DateTime]::UtcNow.ToString('o');assetsVerified=$count;licenseFiles=$notices.Count;archiveSha256=$package.sha256} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $webWorkspace 'artifacts/web/package-validation.json') -Encoding utf8
    Write-Output "Static package verified: $count assets, $($notices.Count) notices."
} finally {$archive.Dispose()}
