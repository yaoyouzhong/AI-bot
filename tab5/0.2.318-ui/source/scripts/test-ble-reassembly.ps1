$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root '.tools\native-tests'
New-Item -ItemType Directory -Force $output | Out-Null
$source=[IO.File]::ReadAllText((Join-Path $root 'firmware\main\ble_link.c'))
function Read-Function([string]$name) {
    $match=[regex]::Match($source,'(?m)^[\w *]+\b'+$name+'\([^;]*?\)\s*\{')
    if(-not $match.Success){throw "Missing function: $name"}
    $start=$match.Index;$brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
    while($depth -gt 0 -and $end -lt $source.Length) {
        if($source[$end] -eq '{'){$depth++}
        if($source[$end] -eq '}'){$depth--}
        $end++
    }
    if($depth){throw "Unbalanced function: $name"}
    $source.Substring($start,$end-$start)
}
$device=[IO.File]::ReadAllText((Join-Path $root 'firmware\main\device.h'))
$frame=[regex]::Match($device,'(?m)^#define TAB5_MAX_FRAME .+$').Value
$capacity=[regex]::Match($source,'(?m)^#define PACKET_CAPACITY .+$').Value
if(-not $frame -or -not $capacity){throw 'Missing packet limits'}
[IO.File]::WriteAllText((Join-Path $output 'ble_reassembly_under_test.inc'),(@($frame,$capacity,(Read-Function 'packet_init'),(Read-Function 'access')) -join "`n"))
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs=& $vswhere -latest -products '*' -property installationPath
if(-not $vs){throw 'MSVC build tools are required'}
$devcmd=Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
Push-Location $output
try {
    $command='call "'+$devcmd+'" -arch=x64 >nul && cl /nologo /utf-8 /std:c11 /W4 /WX /I"'+$output+'" "'+$root+'\tests\ble-reassembly-test.c" /Fe:ble-reassembly-test.exe && ble-reassembly-test.exe'
    & cmd.exe /d /c $command
    if($LASTEXITCODE){throw 'BLE reassembly test failed'}
} finally {Pop-Location}
