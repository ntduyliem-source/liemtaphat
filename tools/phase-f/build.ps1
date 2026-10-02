[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$phaseWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $phaseWorkspace
try {
    $phaseBuild=Join-Path $phaseWorkspace ('artifacts/phase-f/builds/'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
    dotnet restore src/Locus.Word --locked-mode
    if($LASTEXITCODE -ne 0){throw 'Word restore failed'}
    dotnet publish src/Locus.Word -c Release --no-restore -o (Join-Path $phaseBuild 'word')
    if($LASTEXITCODE -ne 0){throw 'Word publish failed'}
    Copy-Item -LiteralPath tools/m3/register.ps1 -Destination (Join-Path $phaseBuild 'word')
    $phaseInfo=@{root=$phaseBuild;word=(Join-Path $phaseBuild 'word');sdk=(& dotnet --version);createdUtc=[DateTime]::UtcNow.ToString('o');status='review-build-awaiting-native-acceptance';baseline='E-20260916-052303-165';version='0.5.0'}
    $phaseInfo | ConvertTo-Json | Set-Content -LiteralPath artifacts/phase-f/current-build.json -Encoding utf8NoBOM
    $phaseInfo | ConvertTo-Json
} finally {Pop-Location}
