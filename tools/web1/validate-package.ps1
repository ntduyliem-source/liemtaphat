[CmdletBinding()]
param([switch]$ExtractWeb)
$ErrorActionPreference='Stop'
$web1Packages=Get-Content artifacts/web1/package.json -Raw | ConvertFrom-Json -DateKind String
$web1Results=@()
foreach($web1Package in $web1Packages.packages){
    if((Get-FileHash -LiteralPath $web1Package.path).Hash -ne $web1Package.sha256){throw 'ZIP hash mismatch'}
    $web1Archive=[IO.Compression.ZipFile]::OpenRead((Join-Path (Get-Location) $web1Package.path))
    try{
        $web1ManifestEntry=$web1Archive.Entries | Where-Object { $_.FullName -eq ($web1Package.name+'/manifest.json') }
        if(-not $web1ManifestEntry){throw 'Missing package manifest'}
        $web1Reader=[IO.StreamReader]::new($web1ManifestEntry.Open())
        try{$web1Manifest=$web1Reader.ReadToEnd() | ConvertFrom-Json -DateKind String}finally{$web1Reader.Dispose()}
        foreach($web1File in $web1Manifest){
            $web1Entry=$web1Archive.GetEntry($web1Package.name+'/'+$web1File.path)
            if(-not $web1Entry -or $web1Entry.Length -ne $web1File.bytes){throw 'ZIP file missing or length mismatch'}
            $web1Stream=$web1Entry.Open()
            try{$web1Digest=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($web1Stream)).ToLowerInvariant()}finally{$web1Stream.Dispose()}
            if($web1Digest -ne $web1File.sha256){throw ('ZIP entry mismatch: '+$web1File.path)}
        }
        $web1Results+=@{name=$web1Package.name;files=@($web1Manifest).Count;status='PASSED'}
    }finally{$web1Archive.Dispose()}
}
$web1Extracted=$null
if($ExtractWeb){
    $web1Web=$web1Packages.packages | Where-Object { $_.name -like '*Web-static' }
    $web1Extract=Join-Path (Get-Location) ('artifacts/web1/local-trial/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    if(Test-Path -LiteralPath $web1Extract){throw 'Extraction directory already exists'}
    Expand-Archive -LiteralPath $web1Web.path -DestinationPath $web1Extract
    $web1Extracted=Join-Path $web1Extract $web1Web.name
}
@{status='PASSED';results=$web1Results;extractedWeb=$web1Extracted} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath artifacts/web1/package-validation.json -Encoding utf8NoBOM
$web1Results | ConvertTo-Json
