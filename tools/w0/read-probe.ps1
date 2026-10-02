param([string]$Label='snapshot')
$ErrorActionPreference='Stop'
if($Label -notmatch '^[a-z0-9-]+$'){throw 'Label must be a simple artifact name.'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$owner=Get-Content -LiteralPath (Join-Path $root 'artifacts/w0/interactive-owner.json')|ConvertFrom-Json
$client=[IO.Pipes.NamedPipeClientStream]::new('.',('Locus.W0.'+$owner.pid),[IO.Pipes.PipeDirection]::In)
try {
    $client.Connect(3000)
    $reader=[IO.StreamReader]::new($client,[Text.Encoding]::UTF8)
    $task=$reader.ReadLineAsync()
    if(-not $task.Wait(3000)){throw 'Probe read timeout.'}
    $raw=$task.Result
    $state=$raw|ConvertFrom-Json
    if(-not $state.connected){throw 'Disconnected observer.'}
    $artifactDirectory=Join-Path $root 'artifacts/w0/native'
    [void][IO.Directory]::CreateDirectory($artifactDirectory)
    [IO.File]::WriteAllText((Join-Path $artifactDirectory ($Label+'.json')),$raw)
    $raw
}finally{$client.Dispose()}
