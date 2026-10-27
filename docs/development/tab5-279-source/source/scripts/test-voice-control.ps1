$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs=& $vswhere -latest -products '*' -property installationPath
if (-not $vs) {throw 'MSVC build tools are required for native voice tests'}
$output=Join-Path $root '.tools\native-tests'
New-Item -ItemType Directory -Force $output | Out-Null
$devcmd=Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
Push-Location $output
try {
    $command = 'call "'+$devcmd+'" -arch=x64 >nul && cl /nologo /std:c11 /experimental:c11atomics /W4 /WX /I"'+$root+'\firmware\main" "'+$root+'\tests\voice-control-test.c" /Fe:voice-control-test.exe && voice-control-test.exe'
    & cmd.exe /d /c $command
    if ($LASTEXITCODE) {throw 'Native voice-control test failed'}
} finally {Pop-Location}
