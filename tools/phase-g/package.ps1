[CmdletBinding()]
param([ValidatePattern('^[a-z0-9-]+$')][string]$Label='alpha-local')
$ErrorActionPreference='Stop'
$gWorkspace=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location -LiteralPath $gWorkspace
try {
    $gBuild=Get-Content artifacts/phase-g/current-build.json -Raw | ConvertFrom-Json
    $gBuildId=Split-Path $gBuild.root -Leaf
    $gPlot=Get-Content artifacts/phase-g/verification/plot-verification.json -Raw | ConvertFrom-Json
    $gGeometry=Get-Content artifacts/phase-g/verification/geometry-verification.json -Raw | ConvertFrom-Json
    $gRegression=Get-Content artifacts/phase-g/regression-final/application-tests.json -Raw | ConvertFrom-Json
    $gUi=Get-Content artifacts/phase-g/verification/ui-browser.json -Raw | ConvertFrom-Json
    if($gPlot.failed -ne 0 -or $gGeometry.failed -ne 0 -or $gRegression.status -ne 'PASSED' -or $gRegression.failed -ne 0){throw 'Model/contract evidence is not green.'}
    if($gUi.build -ne $gBuildId -or $gUi.finalVerification.status -ne 'PASSED'){throw 'Browser evidence must match the packaged build.'}
    $gPortable=if($gBuild.desktopPortable){$gBuild.desktopPortable}else{Join-Path $gBuild.root 'desktop-portable'}
    if(!(Test-Path -LiteralPath (Join-Path $gPortable 'Locus.Desktop.Shared.exe'))){throw 'Self-contained Desktop publish is missing.'}
    $gNuget=if($env:NUGET_PACKAGES){$env:NUGET_PACKAGES}else{Join-Path $env:USERPROFILE '.nuget/packages'}
    $gPackages=@()
    foreach($gKind in @('Web-static','Desktop-win-x64-portable')) {
        $gName="Locus-G-$gBuildId-$Label-$gKind"
        $gRelease=Join-Path $gWorkspace "artifacts/releases/$gName"
        $gZip=$gRelease+'.zip'
        if((Test-Path -LiteralPath $gRelease) -or (Test-Path -LiteralPath $gZip)){throw "Existing release must not be overwritten: $gName"}
        New-Item -ItemType Directory -Path $gRelease | Out-Null
        if($gKind -eq 'Web-static') {
            Copy-Item -LiteralPath $gBuild.web -Destination (Join-Path $gRelease 'wwwroot') -Recurse
            $gLocal=Join-Path $gRelease 'local';New-Item -ItemType Directory -Path $gLocal | Out-Null
            Copy-Item -LiteralPath 'tools/web1/local.mjs','tools/web1/serve.mjs' -Destination $gLocal
            Copy-Item -LiteralPath 'tools/phase-g/Start-Locus-Web.cmd','tools/phase-g/Stop-Locus-Web.cmd' -Destination $gRelease
            @"
Locus G — Web local alpha

Giải nén toàn bộ ZIP. Cài Node.js 22 trở lên, rồi chạy Start-Locus-Web.cmd và mở http://127.0.0.1:4193/.
Dừng đúng server của gói bằng Stop-Locus-Web.cmd. Dữ liệu và nháp nằm trên thiết bị/trình duyệt này.

Bản này có Công thức Toán/Lý/Hóa, hỗ trợ cân bằng Hóa, Đồ thị y=f(x), editor Hình học 2D/3D, lưu .locus và xuất SVG/PNG. Đây chưa phải website Internet, chưa có tài khoản/hạn mức và không kèm connector Word.
"@ | Set-Content -LiteralPath (Join-Path $gRelease 'README.txt') -Encoding utf8NoBOM
        }
        else {
            Get-ChildItem -LiteralPath $gPortable | Where-Object {$_.Name -notlike '*.WebView2'} | Copy-Item -Destination $gRelease -Recurse
            Copy-Item -LiteralPath 'tools/phase-g/Start-Locus-Desktop.cmd' -Destination $gRelease
            @"
Locus G — Desktop Windows x64 portable alpha

Giải nén toàn bộ ZIP rồi chạy Locus.Desktop.Shared.exe hoặc Start-Locus-Desktop.cmd. .NET đã nằm trong gói; máy cần Microsoft Edge WebView2 Runtime (Windows 10/11 thường có sẵn).

Bản này có Công thức Toán/Lý/Hóa, hỗ trợ cân bằng Hóa, Đồ thị y=f(x), editor Hình học 2D/3D, lưu .locus và xuất SVG/PNG. Đây là bản portable, chưa phải bộ cài MSI/Setup: chưa tạo shortcut, Start Menu, tự cập nhật hay đăng ký Word add-in.
"@ | Set-Content -LiteralPath (Join-Path $gRelease 'README.txt') -Encoding utf8NoBOM
        }
        Copy-Item -LiteralPath 'docs/phase-g/QUICKSTART.md','docs/phase-g/REPORT.md' -Destination $gRelease
        Copy-Item -LiteralPath 'docs/host-review/20260930.md' -Destination (Join-Path $gRelease 'HOST-ACCEPTANCE.md')
        Copy-Item -LiteralPath 'artifacts/phase-g/verification/ui-browser.json' -Destination (Join-Path $gRelease 'UI-VERIFICATION.json')
        @{build=$gBuildId;kind=$gKind;createdUtc=[DateTime]::UtcNow.ToString('o');documentFormat=@{plot=10;geometry=9}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $gRelease 'VERSION.json') -Encoding utf8NoBOM
        $gLicenses=Join-Path $gRelease 'licenses';New-Item -ItemType Directory -Path $gLicenses | Out-Null
        Copy-Item -LiteralPath (Join-Path $gNuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/LICENSE.TXT') -Destination (Join-Path $gLicenses 'DOTNET-LICENSE.TXT')
        Copy-Item -LiteralPath (Join-Path $gNuget 'microsoft.netcore.app.runtime.mono.browser-wasm/10.0.11/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $gLicenses 'DOTNET-THIRD-PARTY-NOTICES.TXT')
        Copy-Item -LiteralPath (Join-Path $gNuget 'microsoft.aspnetcore.components.webassembly/10.0.11/THIRD-PARTY-NOTICES.txt') -Destination (Join-Path $gLicenses 'ASPNETCORE-THIRD-PARTY-NOTICES.txt')
        Copy-Item -LiteralPath 'tools/sh/node_modules/mathjax/LICENSE' -Destination (Join-Path $gLicenses 'MATHJAX-APACHE-2.0.txt')
        Copy-Item -LiteralPath 'tools/sh/node_modules/@mathjax/mathjax-newcm-font/package.json' -Destination (Join-Path $gLicenses 'MATHJAX-NEWCM-PACKAGE.json')
        if($gKind -ne 'Web-static') {
            Copy-Item -LiteralPath (Join-Path $gNuget 'microsoft.web.webview2/1.0.3179.45/LICENSE.txt') -Destination (Join-Path $gLicenses 'WEBVIEW2-LICENSE.txt')
            Copy-Item -LiteralPath (Join-Path $gNuget 'microsoft.web.webview2/1.0.3179.45/NOTICE.txt') -Destination (Join-Path $gLicenses 'WEBVIEW2-NOTICE.txt')
            Copy-Item -LiteralPath (Join-Path $gNuget 'microsoft.aspnetcore.components.webview.wpf/10.0.101/THIRD-PARTY-NOTICES.TXT') -Destination (Join-Path $gLicenses 'WEBVIEW-WPF-THIRD-PARTY-NOTICES.txt')
        }
        $gFiles=Get-ChildItem -LiteralPath $gRelease -Recurse -File | ForEach-Object {[ordered]@{path=[IO.Path]::GetRelativePath($gRelease,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}}
        @($gFiles) | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $gRelease 'manifest.json') -Encoding utf8NoBOM
        Compress-Archive -LiteralPath $gRelease -DestinationPath $gZip -CompressionLevel Optimal

        $gCheck=Join-Path $gWorkspace "artifacts/phase-g/package-check/$gName"
        if(Test-Path -LiteralPath $gCheck){throw "Package check directory already exists: $gCheck"}
        New-Item -ItemType Directory -Path $gCheck | Out-Null
        Expand-Archive -LiteralPath $gZip -DestinationPath $gCheck
        $gExtract=Join-Path $gCheck $gName
        foreach($gFile in $gFiles){$gActual=Join-Path $gExtract $gFile.path;if(!(Test-Path -LiteralPath $gActual) -or (Get-Item -LiteralPath $gActual).Length -ne $gFile.bytes -or (Get-FileHash -LiteralPath $gActual).Hash.ToLowerInvariant() -ne $gFile.sha256){throw "Package verification failed: $($gFile.path)"}}

        $gSmoke='VERIFIED_HASHES'
        if($gKind -eq 'Web-static') {
            $gListener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$gListener.Start();$gPort=([Net.IPEndPoint]$gListener.LocalEndpoint).Port;$gListener.Stop()
            $gState=Join-Path $gCheck '.server'
            & node (Join-Path $gExtract 'local/local.mjs') start --port $gPort --state-directory $gState
            if($LASTEXITCODE -ne 0){throw 'Extracted Web package did not start.'}
            try {$gHealth=Invoke-RestMethod "http://127.0.0.1:$gPort/__locus_local/health";if($gHealth.build -ne $gBuildId){throw 'Extracted Web package served the wrong build.'};$gSmoke='PASSED_LOCAL_SERVER'}
            finally {& node (Join-Path $gExtract 'local/local.mjs') stop --port $gPort --state-directory $gState | Out-Null}
        }
        else {
            $gProfile=Join-Path $gCheck '.profile';$gReceipt=Join-Path $gCheck 'desktop-receipt.json'
            $gProcess=Start-Process -FilePath (Join-Path $gExtract 'Locus.Desktop.Shared.exe') -WorkingDirectory $gExtract -ArgumentList @('--profile',('"'+$gProfile+'"'),'--receipt',('"'+$gReceipt+'"')) -WindowStyle Hidden -PassThru
            try {for($gTry=0;$gTry -lt 40;$gTry++){Start-Sleep -Milliseconds 250;$gProcess.Refresh();if((Test-Path -LiteralPath $gReceipt) -and !$gProcess.HasExited -and $gProcess.MainWindowHandle -ne 0){break}};if(!(Test-Path -LiteralPath $gReceipt) -or $gProcess.HasExited -or $gProcess.MainWindowHandle -eq 0 -or !$gProcess.Responding){throw 'Extracted Desktop package did not reach a responsive main window.'};$gDesktopReceipt=Get-Content $gReceipt -Raw | ConvertFrom-Json;if($gDesktopReceipt.pid -ne $gProcess.Id -or !(Test-Path -LiteralPath $gDesktopReceipt.executable)){throw 'Extracted Desktop startup receipt is invalid.'};$gSmoke='PASSED_DESKTOP_WINDOW'}
            finally {if(!$gProcess.HasExited){$null=$gProcess.CloseMainWindow();if(!$gProcess.WaitForExit(2000)){Stop-Process -Id $gProcess.Id -Force}}}
        }
        $gPackages+=@{name=$gName;kind=$gKind;directory=$gRelease;path=$gZip;bytes=(Get-Item -LiteralPath $gZip).Length;sha256=(Get-FileHash -LiteralPath $gZip).Hash.ToLowerInvariant();files=@($gFiles).Count;verification=$gSmoke;extracted=$gExtract}
    }
    $gReceipt=@{status='VERIFIED';build=$gBuildId;createdUtc=[DateTime]::UtcNow.ToString('o');tests=@{plot=$gPlot.passed;geometry=$gGeometry.passed;application=$gRegression.total};ui=$gUi.finalVerification;evidence=$gUi;packages=$gPackages}
    $gReceipt | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath artifacts/phase-g/packages.json -Encoding utf8NoBOM
    $gReceipt | ConvertTo-Json -Depth 9
} finally {Pop-Location}
