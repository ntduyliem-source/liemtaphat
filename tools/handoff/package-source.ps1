[CmdletBinding()]
param([ValidatePattern('^Locus-source-[A-Za-z0-9-]+$')][string]$Name='Locus-source-20260930-G-alpha')
$ErrorActionPreference='Stop'
$handoffWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$handoffOutput=Join-Path $handoffWorkspace 'artifacts/handoff'
$handoffStage=Join-Path $handoffOutput "staging/$Name"
$handoffZip=Join-Path $handoffOutput "$Name.zip"
$handoffCheck=Join-Path $handoffOutput "check/$Name"
if((Test-Path -LiteralPath $handoffStage) -or (Test-Path -LiteralPath $handoffZip) -or (Test-Path -LiteralPath $handoffCheck)){
    throw "Existing snapshot must be preserved; choose another name: $Name"
}
$handoffDirectories=@('src','tests','tools','corpus','fixtures','prototypes','docs')
$handoffRootFiles=@('.editorconfig','.gitignore','global.json','Locus.sln','Locus.Web.sln','Locus.Word.sln','README.md')
$handoffSkipSegments=@('bin','obj','node_modules','vendor','.vs','EBWebView','__pycache__')
$handoffSkipExtensions=@('.exe','.dll','.pdb','.zip','.7z','.log','.db','.sqlite','.pfx','.p12','.pem','.key','.bak','.user')
$handoffInputs=New-Object System.Collections.Generic.List[string]
foreach($handoffDirectory in $handoffDirectories){
    $handoffSource=Join-Path $handoffWorkspace $handoffDirectory
    foreach($handoffFile in Get-ChildItem -LiteralPath $handoffSource -Recurse -File -Force){
        $handoffRelative=[IO.Path]::GetRelativePath($handoffWorkspace,$handoffFile.FullName)
        $handoffParts=$handoffRelative.Replace('\','/').Split('/')
        if(@($handoffParts | Where-Object {$handoffSkipSegments -contains $_}).Count -gt 0){continue}
        if($handoffSkipExtensions -contains $handoffFile.Extension.ToLowerInvariant()){continue}
        if($handoffFile.Name -match '^(\.env|\.env\..+|.*\.secret)$'){continue}
        $handoffInputs.Add($handoffFile.FullName)
    }
}
foreach($handoffFile in $handoffRootFiles){
    $handoffPath=Join-Path $handoffWorkspace $handoffFile
    if(!(Test-Path -LiteralPath $handoffPath)){throw "Required source file is missing: $handoffFile"}
    $handoffInputs.Add($handoffPath)
}
if($handoffInputs.Count -lt 400){throw "Source snapshot is unexpectedly small: $($handoffInputs.Count) files"}
[IO.Directory]::CreateDirectory($handoffStage) | Out-Null
$handoffManifest=foreach($handoffFile in $handoffInputs){
    $handoffRelative=[IO.Path]::GetRelativePath($handoffWorkspace,$handoffFile).Replace('\','/')
    $handoffTarget=Join-Path $handoffStage $handoffRelative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($handoffTarget)) | Out-Null
    Copy-Item -LiteralPath $handoffFile -Destination $handoffTarget
    [ordered]@{path=$handoffRelative;bytes=(Get-Item -LiteralPath $handoffFile).Length;sha256=(Get-FileHash -LiteralPath $handoffFile -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$handoffManifest=@($handoffManifest | Sort-Object path)
$handoffManifest | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $handoffStage 'SOURCE-MANIFEST.json') -Encoding utf8NoBOM
[ordered]@{
    kind='Locus source snapshot for independent local testing'
    createdUtc=[DateTime]::UtcNow.ToString('o')
    sourceFiles=$handoffManifest.Count
    sourceBuild='20260930-042902-862'
    handoff='docs/handoff/DOT-LOCAL-TEST.md'
    excluded=@('artifacts/','.git/','bin/','obj/','node_modules/','src/Locus.Editor/wwwroot/vendor/','WebView profiles and caches')
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $handoffStage 'SNAPSHOT.json') -Encoding utf8NoBOM
[IO.Directory]::CreateDirectory($handoffOutput) | Out-Null
[IO.Compression.ZipFile]::CreateFromDirectory($handoffStage,$handoffZip,[IO.Compression.CompressionLevel]::Optimal,$true)
[IO.Directory]::CreateDirectory($handoffCheck) | Out-Null
[IO.Compression.ZipFile]::ExtractToDirectory($handoffZip,$handoffCheck)
$handoffExtract=Join-Path $handoffCheck $Name
foreach($handoffFile in $handoffManifest){
    $handoffExtracted=Join-Path $handoffExtract $handoffFile.path
    if(!(Test-Path -LiteralPath $handoffExtracted) -or
       (Get-Item -LiteralPath $handoffExtracted).Length -ne $handoffFile.bytes -or
       (Get-FileHash -LiteralPath $handoffExtracted -Algorithm SHA256).Hash.ToLowerInvariant() -ne $handoffFile.sha256){
        throw "Extracted source mismatch: $($handoffFile.path)"
    }
}
$handoffExtractCount=@(Get-ChildItem -LiteralPath $handoffExtract -Recurse -File).Count
if($handoffExtractCount -ne $handoffManifest.Count+2){throw "Unexpected extracted file count: $handoffExtractCount"}
$handoffReceipt=[ordered]@{
    status='VERIFIED'
    name=$Name
    path=$handoffZip
    sha256=(Get-FileHash -LiteralPath $handoffZip -Algorithm SHA256).Hash.ToLowerInvariant()
    bytes=(Get-Item -LiteralPath $handoffZip).Length
    sourceFiles=$handoffManifest.Count
    verifiedExtract=$handoffExtract
    createdUtc=[DateTime]::UtcNow.ToString('o')
}
$handoffReceipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $handoffOutput "$Name.receipt.json") -Encoding utf8NoBOM
$handoffReceipt | ConvertTo-Json -Depth 4
