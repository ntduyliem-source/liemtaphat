$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$shPackage=Get-Content artifacts/sh/package.json -Raw | ConvertFrom-Json
$shResults=@()
foreach($shItem in $shPackage.packages) {
    if((Get-FileHash -LiteralPath $shItem.path).Hash -ne $shItem.sha256){throw 'ZIP checksum differs'}
    $shArchive=[IO.Compression.ZipFile]::OpenRead((Resolve-Path $shItem.path))
    try{
        $shPrefix=$shItem.name+'/'
        $shManifest=$shArchive.GetEntry($shPrefix+'manifest.json')
        $shReader=[IO.StreamReader]::new($shManifest.Open());try{$shFiles=$shReader.ReadToEnd() | ConvertFrom-Json}finally{$shReader.Dispose()}
        foreach($shFile in $shFiles){$shEntry=$shArchive.GetEntry($shPrefix+$shFile.path);if(-not $shEntry -or $shEntry.Length -ne $shFile.bytes){throw "Missing asset: $($shFile.path)"};$shStream=$shEntry.Open();try{$shHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($shStream)).ToLowerInvariant()}finally{$shStream.Dispose()};if($shHash -ne $shFile.sha256){throw "Wrong asset: $($shFile.path)"}}
        if($shArchive.Entries.Where({$_.Length -gt 0}).Count -ne $shFiles.Count+1){throw 'Unexpected package files'}
        $shResults+=@{package=$shItem.path;files=$shFiles.Count;status='PASSED'}
    }finally{$shArchive.Dispose()}
}
$shResults | ConvertTo-Json | Set-Content artifacts/sh/package-validation.json -Encoding utf8NoBOM
$shResults | ConvertTo-Json
