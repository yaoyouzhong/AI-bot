#requires -Version 7.0
# Recreate the locally approved TAB5 prompt with the same Windows voice as Codex.
param([Parameter(Mandatory)][string]$WaveOutput)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$destination=[IO.Path]::GetFullPath($WaveOutput)
if(Test-Path -LiteralPath $destination){throw 'Choose a new WAV output path.'}
$temporary=Join-Path ([IO.Path]::GetDirectoryName($destination)) ('huihui-'+[guid]::NewGuid().ToString('N')+'.wav')
$speech=[System.Speech.Synthesis.SpeechSynthesizer]::new()
try {
    $speech.SelectVoice('Microsoft Huihui Desktop')
    $speech.Rate=1
    $speech.Volume=100
    $format=[System.Speech.AudioFormat.SpeechAudioFormatInfo]::new(16000,
        [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,
        [System.Speech.AudioFormat.AudioChannel]::Mono)
    $speech.SetOutputToWaveFile($temporary,$format)
    $speech.Speak('Claude，任务已完成')
    $speech.SetOutputToNull()
    [IO.File]::Move($temporary,$destination)
} finally {$speech.Dispose();[IO.File]::Delete($temporary)}
