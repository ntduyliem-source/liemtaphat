[CmdletBinding()]
param([switch]$SkipTests, [switch]$SkipRuntimeProbe)
$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $workspaceRoot
try {
    dotnet restore Locus.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    dotnet build Locus.sln --no-restore -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not $SkipTests) {
        dotnet run --project tests/Locus.Core.Tests -c Release --no-build -- --root $workspaceRoot
        if ($LASTEXITCODE -ne 0) { throw 'Core verification failed.' }
        dotnet run --project tests/Locus.Desktop.Tests -c Release --no-build -- artifacts/m2
        if ($LASTEXITCODE -ne 0) { throw 'Desktop verification failed.' }
        if (-not $SkipRuntimeProbe -and [Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
            & (Join-Path $PSScriptRoot 'verify-runtimes.ps1') -NoBuild
        }
    }
} finally { Pop-Location }
