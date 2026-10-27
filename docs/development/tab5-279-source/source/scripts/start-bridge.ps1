# Keep the executable path stable: Windows Firewall grants access by program path.
# Build candidates separately; copy verified files into this directory while stopped.
$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$runtime=Join-Path $project '.tools/bridge-voice-reply-20260926/AIBotBridge.exe'
if(-not (Test-Path -LiteralPath $runtime)){throw 'The verified TAB5 bridge runtime is not installed.'}
$existing=Get-Process AIBotBridge -ErrorAction SilentlyContinue | Where-Object {
    try {$_.Path -eq $runtime} catch {$false}
}
if($existing){$existing | Select-Object Id,Path;return}
$env:AIBOT_HTTP_PORT='18765'
$env:AIBOT_TAB5_DIAG_LOG=Join-Path $project '.tools/bridge-runtime-diagnostics.log'
Start-Process -FilePath $runtime -WindowStyle Hidden -PassThru | Select-Object Id,Path
