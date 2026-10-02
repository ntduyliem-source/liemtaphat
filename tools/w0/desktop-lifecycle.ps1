$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
Push-Location -LiteralPath $root
$previous=$env:LOCUS_WORD_TEST_SUITE
try {
    $env:LOCUS_WORD_TEST_SUITE='desktop'
    & tests/Locus.Word.Tests/bin/Release/net48/Locus.Word.Tests.exe
    if($LASTEXITCODE -ne 0){throw 'Desktop/Word lifecycle verification failed.'}
}finally{$env:LOCUS_WORD_TEST_SUITE=$previous;Pop-Location}
