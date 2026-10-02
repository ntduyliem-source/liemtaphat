[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$manualWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manualBuild=Join-Path $manualWorkspace 'src/Locus.Word/bin/Release/net48'
$manualPackage=Join-Path $manualWorkspace 'artifacts/releases/Locus-Word-0.3.0-alpha-x86'
$manualZip=$manualPackage+'.zip'
if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'Close Word before updating the trial package.'}
New-Item -ItemType Directory -Path $manualPackage -Force | Out-Null
foreach($manualFile in @('Locus.Word.dll','Locus.Core.dll')){
    Copy-Item -LiteralPath (Join-Path $manualBuild $manualFile) -Destination (Join-Path $manualPackage $manualFile) -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'register.ps1') -Destination (Join-Path $manualPackage 'register.ps1') -Force
Copy-Item -LiteralPath (Join-Path $manualWorkspace 'docs/m3/QUICKSTART.md') -Destination (Join-Path $manualPackage 'QUICKSTART.md') -Force
$manualManifest=@{
    version='0.3.0-alpha';progId='Locus.Word.Manual';wordBitness='x86';runtime='.NET Framework 4.8';autoAllowed=$false
    files=@(Get-ChildItem -LiteralPath $manualPackage -File | Where-Object Name -ne 'manifest.json' | ForEach-Object { @{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
}
$manualManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $manualPackage 'manifest.json') -Encoding UTF8
Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $manualPackage -File | Select-Object -ExpandProperty FullName) -DestinationPath $manualZip -Force
Write-Output $manualZip
