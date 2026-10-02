param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$locusRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $OutputPath) { $OutputPath = Join-Path $locusRoot 'artifacts\m0\shared-core.json' }
$locusProbe = Join-Path $locusRoot 'prototypes\m0-shared-core'
$locusLogs = @()
foreach ($locusProject in @('ModernHost','FrameworkHost')) {
    $locusBuild = @(& dotnet build (Join-Path $locusProbe "$locusProject\$locusProject.csproj") --nologo --verbosity minimal 2>&1)
    $locusCode = $LASTEXITCODE
    $locusLogs += @{ host=$locusProject; exitCode=$locusCode; output=@($locusBuild | ForEach-Object { [string]$_ }) }
    if ($locusCode -ne 0) { throw "Build failed for $locusProject : $($locusBuild -join [Environment]::NewLine)" }
}
$locusModernDirectory = Join-Path $locusProbe 'ModernHost\bin\Debug\net10.0'
$locusFrameworkDirectory = Join-Path $locusProbe 'FrameworkHost\bin\Debug\net48'
$locusModern = @(& dotnet (Join-Path $locusModernDirectory 'ModernHost.dll'))
if ($LASTEXITCODE -ne 0) { throw 'Modern host failed' }
$locusFramework = @(& (Join-Path $locusFrameworkDirectory 'FrameworkHost.exe'))
if ($LASTEXITCODE -ne 0) { throw 'Framework host failed' }
$locusModernHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $locusModernDirectory 'Locus.M0.SharedProbe.dll')).Hash
$locusFrameworkHash = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $locusFrameworkDirectory 'Locus.M0.SharedProbe.dll')).Hash
$locusMatch = ($locusModern.Count -eq 2 -and ($locusModern -join "`n") -ceq ($locusFramework -join "`n") -and $locusModernHash -ceq $locusFrameworkHash)
$locusDistinctRaw = ($locusModern.Count -eq 2 -and $locusModern[0] -cne $locusModern[1])
$locusReport = [ordered]@{
    schemaVersion='1'; capturedAtUtc=[DateTime]::UtcNow.ToString('o'); spike='S-07 shared assembly portion';
    status=$(if ($locusMatch -and $locusDistinctRaw) {'PASS'} else {'FAIL'});
    sharedTarget='netstandard2.0'; hosts=@('net10.0 process','net48 x86 process');
    identicalAssemblyBytes=($locusModernHash -ceq $locusFrameworkHash); sha256=$locusModernHash;
    identicalHostOutputs=$locusMatch; preservesDistinctNfcNfdSources=$locusDistinctRaw;
    modernOutput=$locusModern; frameworkOutput=$locusFramework; builds=$locusLogs;
    limits=@('Fixed fixture, not a parser','Separate console hosts, not a deployed VSTO add-in or Desktop app','IPC/reconnect/multiple Word windows not covered by this probe','Does not prove IME or commit safety')
}
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($locusReport | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Output ($locusReport | ConvertTo-Json -Depth 8)
if ($locusReport.status -ne 'PASS') { exit 1 }
