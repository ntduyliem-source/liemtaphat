$ErrorActionPreference='Stop'
if(Get-Process WINWORD -ErrorAction SilentlyContinue){throw 'A Word process is already open; this demo only creates its own fresh instance.'}
$manualWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$manualDirectory=Join-Path $manualWorkspace 'artifacts/m3/native'
New-Item -ItemType Directory -Path $manualDirectory -Force | Out-Null
$manualPreviousSuite=$env:LOCUS_WORD_TEST_SUITE
try {
    $env:LOCUS_WORD_TEST_SUITE='manual-demo'
    & (Join-Path $manualWorkspace 'tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe') $manualDirectory
    if($LASTEXITCODE -ne 0){throw 'Demo setup failed.'}
}finally{$env:LOCUS_WORD_TEST_SUITE=$manualPreviousSuite}
