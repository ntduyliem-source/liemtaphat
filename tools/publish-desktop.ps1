[CmdletBinding()]
param([string]$Version = '0.2.0-alpha')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^[a-zA-Z0-9.-]+$') { throw 'Version must be a simple directory-safe label.' }
$workspaceRoot = Split-Path $PSScriptRoot -Parent
$releaseRoot = Join-Path $workspaceRoot 'artifacts/releases'
$packagePath = Join-Path $releaseRoot "Locus-$Version-win-x64"
Push-Location -LiteralPath $workspaceRoot
try {
    dotnet restore src/Locus.Desktop --locked-mode -p:SelfContained=true
    if ($LASTEXITCODE -ne 0) { throw 'Desktop locked restore failed.' }
    dotnet publish src/Locus.Desktop -c Release -r win-x64 --self-contained true --no-restore -o $packagePath --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Desktop publish failed.' }
    Copy-Item -LiteralPath (Join-Path $workspaceRoot 'docs/m2/QUICKSTART.md') -Destination (Join-Path $packagePath 'HUONG-DAN.md')
    $exePath = Join-Path $packagePath 'Locus.Desktop.exe'
    $smokePath = Join-Path $workspaceRoot 'artifacts/m2'
    $process = Start-Process -FilePath $exePath -ArgumentList @('--smoke-output', ('"' + $smokePath + '"')) -WindowStyle Hidden -PassThru
    if (-not $process.WaitForExit(30000)) { throw 'Package smoke timed out; inspect the named package process.' }
    if ($process.ExitCode -ne 0) { throw "Package smoke failed with $($process.ExitCode)." }
    $zipPath = "$packagePath.zip"
    Compress-Archive -LiteralPath $packagePath -DestinationPath $zipPath -Force
    $hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
    [ordered]@{ version=$Version; runtime='win-x64'; selfContained=$true; zip=$zipPath; bytes=(Get-Item -LiteralPath $zipPath).Length; sha256=$hash.Hash; capturedAtUtc=[DateTime]::UtcNow.ToString('o') } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $smokePath 'package.json') -Encoding utf8
    Write-Output "Desktop package: $zipPath"
} finally { Pop-Location }
