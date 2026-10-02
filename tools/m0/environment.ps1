param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$locusRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $OutputPath) { $OutputPath = Join-Path $locusRoot 'artifacts\m0\environment.json' }
$locusUtf8 = New-Object System.Text.UTF8Encoding($false)
$locusResult = [ordered]@{ schemaVersion='1'; capturedAtUtc=[DateTime]::UtcNow.ToString('o'); purpose='Read-only M0 environment inventory; not a compatibility certification' }
$locusOs = Get-CimInstance Win32_OperatingSystem
$locusResult.os = @{ caption=$locusOs.Caption; version=$locusOs.Version; build=$locusOs.BuildNumber; architecture=$locusOs.OSArchitecture }
$locusOfficeKey = 'HKLM:\SOFTWARE\Microsoft\Office\ClickToRun\Configuration'
if (Test-Path -LiteralPath $locusOfficeKey) {
    $locusOffice = Get-ItemProperty -LiteralPath $locusOfficeKey
    $locusResult.office = @{ source=$locusOfficeKey; version=$locusOffice.VersionToReport; platform=$locusOffice.Platform; installationPath=$locusOffice.InstallationPath }
} else { $locusResult.office = @{ status='NOT_FOUND'; source=$locusOfficeKey } }
$locusWordPaths = @('C:\Program Files (x86)\Microsoft Office\root\Office16\WINWORD.EXE','C:\Program Files\Microsoft Office\root\Office16\WINWORD.EXE')
$locusResult.wordBinaries = @(foreach ($locusWordPath in $locusWordPaths) { if (Test-Path -LiteralPath $locusWordPath) { $locusInfo = (Get-Item -LiteralPath $locusWordPath).VersionInfo; @{ path=$locusWordPath; fileVersion=$locusInfo.FileVersion } } })
$locusResult.wordRunning = (@(Get-Process -Name WINWORD -ErrorAction SilentlyContinue).Count -gt 0)
$locusResult.comActivation = @{}
foreach ($locusRegistryView in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
    $locusClasses = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::ClassesRoot, $locusRegistryView)
    $locusProgIdKey = $null; $locusServerKey = $null
    try {
        $locusProgIdKey = $locusClasses.OpenSubKey('Word.Application\CLSID')
        if ($locusProgIdKey) {
            $locusClsid = [string]$locusProgIdKey.GetValue('')
            $locusServerKey = $locusClasses.OpenSubKey('CLSID\'+$locusClsid+'\LocalServer32')
            $locusResult.comActivation[$locusRegistryView.ToString()] = @{ clsid=$locusClsid; localServer=$(if ($locusServerKey) { $locusServerKey.GetValue('') } else { $null }) }
        }
    } finally {
        if ($locusServerKey) { $locusServerKey.Dispose() }
        if ($locusProgIdKey) { $locusProgIdKey.Dispose() }
        $locusClasses.Dispose()
    }
}
$locusResult.input = @{ runningHelpers=@(Get-Process -Name UniKeyNT,UniKey,EVKey -ErrorAction SilentlyContinue | ForEach-Object { $_.ProcessName }); activeHelperMode='UNTESTED'; realCompositionEvents='UNTESTED' }
$locusPreloadPath = 'HKCU:\Keyboard Layout\Preload'
if (Test-Path -LiteralPath $locusPreloadPath) {
    $locusPreload = Get-Item -LiteralPath $locusPreloadPath
    $locusResult.input.preloadedKeyboardIds = @($locusPreload.GetValueNames() | ForEach-Object { $locusPreload.GetValue($_) })
}
$locusResult.framework = @{}
foreach ($locusRuntimeKey in @('HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','HKLM:\SOFTWARE\WOW6432Node\Microsoft\VSTO Runtime Setup\v4R')) {
    if (Test-Path -LiteralPath $locusRuntimeKey) { $locusRuntimeValue = Get-ItemProperty -LiteralPath $locusRuntimeKey; $locusResult.framework[$locusRuntimeKey] = @{ version=$locusRuntimeValue.Version; release=$locusRuntimeValue.Release } }
}
$locusResult.development = @{}
foreach ($locusCommandName in @('dotnet','node','python','git')) {
    $locusCommand = Get-Command $locusCommandName -ErrorAction SilentlyContinue
    if ($locusCommand) { $locusResult.development[$locusCommandName] = @{ executable=$locusCommand.Source } }
}
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $locusResult.development.dotnet.sdks = @(& dotnet --list-sdks) }
if (Get-Command node -ErrorAction SilentlyContinue) { $locusResult.development.node.version = (& node --version) }
if (Get-Command python -ErrorAction SilentlyContinue) { $locusResult.development.python.version = (& python --version) }
$locusResult.unverified = @('WordApi runtime requirement checks','Office.js sideload/runtime','VSTO add-in deployment','UniKey Vietnamese mode and input method','Other Word builds/bitness','Windows 11','Cold offline startup','Native IME/focus guarantees')
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($OutputPath))) | Out-Null
[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath), ($locusResult | ConvertTo-Json -Depth 8), $locusUtf8)
Write-Output ('Environment report: ' + [IO.Path]::GetFullPath($OutputPath))
