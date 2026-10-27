# Regenerate the fixed, offline notification; Windows PowerShell 5.1 + installed Huihui voice.
param([string]$WaveOutput)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Speech
$wavPath=Join-Path $env:TEMP ('tab5-reply-'+[Guid]::NewGuid().ToString('N')+'.wav')
$speech=New-Object System.Speech.Synthesis.SpeechSynthesizer
try {
    $speech.SelectVoice('Microsoft Huihui Desktop');$speech.Rate=1
    $format=New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000,[System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen,[System.Speech.AudioFormat.AudioChannel]::Mono)
    $speech.SetOutputToWaveFile($wavPath,$format)
    $speech.Speak('Codex任务已完成')
    $speech.SetOutputToNull()
    $bytes=[IO.File]::ReadAllBytes($wavPath)
    if($WaveOutput){[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($WaveOutput),$bytes)}
    for($offset=12;$offset+8 -le $bytes.Length;) {
        $size=[BitConverter]::ToInt32($bytes,$offset+4)
        if([Text.Encoding]::ASCII.GetString($bytes,$offset,4) -eq 'data') {
            $pcm=New-Object byte[] $size;[Array]::Copy($bytes,$offset+8,$pcm,0,$size)
            [IO.File]::WriteAllBytes((Join-Path $PSScriptRoot '../firmware/main/assets/reply_notice.pcm'),$pcm)
            break
        }
        $offset+=8+$size+($size%2)
    }
} finally {$speech.Dispose();if(Test-Path -LiteralPath $wavPath){Remove-Item -LiteralPath $wavPath}}
