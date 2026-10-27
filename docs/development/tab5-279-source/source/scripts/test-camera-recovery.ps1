$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root '.tools\native-tests'
New-Item -ItemType Directory -Force $output | Out-Null
function Read-CFunction([string]$path,[string]$name) {
    $source=[IO.File]::ReadAllText($path)
    $match=[regex]::Match($source,'(?m)^[\w *]+\b'+$name+'\([^;]*?\)\s*\{')
    if(-not $match.Success){throw "Missing function: $name"}
    $start=$match.Index
    $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
    while($depth -gt 0 -and $end -lt $source.Length) {
        if($source[$end] -eq '{'){$depth++}
        if($source[$end] -eq '}'){$depth--}
        $end++
    }
    if($depth -ne 0){throw "Unbalanced function: $name"}
    return $source.Substring($start,$end-$start)
}
$bsp=Join-Path $root 'firmware\components\m5stack_tab5\m5stack_tab5.c'
$video=Join-Path $root 'firmware\components\esp_video\src\esp_video.c'
$ioctl=Join-Path $root 'firmware\components\esp_video\src\esp_video_ioctl.c'
$functions=@((Read-CFunction $bsp 'bsp_cam_osc_init'),(Read-CFunction $bsp 'bsp_display_brightness_init'),(Read-CFunction $video 'esp_video_open'),(Read-CFunction $video 'esp_video_get_format'),(Read-CFunction $video 'esp_video_set_format'),(Read-CFunction $video 'esp_video_setup_buffer'))
$functions+=(Read-CFunction $ioctl 'esp_video_ioctl_reqbufs')
[IO.File]::WriteAllText((Join-Path $output 'camera_driver_under_test.inc'),($functions -join "`n"))
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs=& $vswhere -latest -products '*' -property installationPath
if(-not $vs){throw 'MSVC build tools are required'}
$devcmd=Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
Push-Location $output
try {
    $command='call "'+$devcmd+'" -arch=x64 >nul && cl /nologo /utf-8 /std:c11 /W4 /WX /I"'+$output+'" "'+$root+'\tests\camera-driver-test.c" /Fe:camera-driver-test.exe && camera-driver-test.exe'
    & cmd.exe /d /c $command
    if($LASTEXITCODE){throw 'Camera driver recovery test failed'}
} finally {Pop-Location}
