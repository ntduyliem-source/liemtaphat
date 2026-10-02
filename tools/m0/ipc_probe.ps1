param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$locusRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $OutputPath) { $OutputPath = Join-Path $locusRoot 'artifacts\m0\ipc.json' }
$locusProbeRoot = Join-Path $locusRoot 'prototypes\m0-shared-core'
foreach ($locusProject in @('ModernHost','FrameworkHost')) {
    & dotnet build (Join-Path $locusProbeRoot "$locusProject\$locusProject.csproj") --nologo --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $locusProject" }
}
$locusServerExe = Join-Path $locusProbeRoot 'FrameworkHost\bin\Debug\net48\FrameworkHost.exe'
$locusClientDll = Join-Path $locusProbeRoot 'ModernHost\bin\Debug\net10.0\ModernHost.dll'
$locusPipe = 'locus-m0-' + [Guid]::NewGuid().ToString('N')
$locusRounds = @()
foreach ($locusSession in @('session-1','session-2')) {
    $locusServerProcess = Start-Process -FilePath $locusServerExe -ArgumentList @('server',$locusPipe,$locusSession) -WindowStyle Hidden -PassThru
    try {
        $locusOutput = @(& dotnet $locusClientDll client $locusPipe $locusSession)
        $locusExitCode = $LASTEXITCODE
        if (-not $locusServerProcess.WaitForExit(5000)) { throw 'Owned synthetic pipe server did not exit after shutdown' }
        $locusRounds += @{ session=$locusSession; clientExitCode=$locusExitCode; serverExitCode=$locusServerProcess.ExitCode; observations=$locusOutput }
        if ($locusExitCode -ne 0) { throw 'IPC client assertions failed' }
    } finally {
        if (-not $locusServerProcess.HasExited) { $locusServerProcess.Kill(); $locusServerProcess.WaitForExit() }
        $locusServerProcess.Dispose()
    }
}
$locusReport = [ordered]@{ schemaVersion='1'; capturedAtUtc=[DateTime]::UtcNow.ToString('o'); spike='S-07 transport portion'; status='PASS'; transport='Local named pipe'; client='net10.0'; server='net48 x86'; rounds=$locusRounds; limits=@('Synthetic host state; focus/composition are test values, not observed Word signals','No Word writes or production parser','Production pipe ACL/authentication/message limits and cancellation still need design','Reconnect is a real server-process restart but not a Word/add-in restart') }
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($locusReport | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
Write-Output ($locusReport | ConvertTo-Json -Depth 8)
