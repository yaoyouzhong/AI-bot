$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs=& $vswhere -latest -products '*' -property installationPath
if (-not $vs) {throw 'MSVC build tools are required for native state-machine tests'}
$output=Join-Path $root '.tools\native-tests'
New-Item -ItemType Directory -Force $output | Out-Null
$devcmd=Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
Push-Location $output
try {
    $command = 'call "'+$devcmd+'" -arch=x64 >nul && cl /nologo /W4 /WX /D_CRT_SECURE_NO_WARNINGS /I"'+$root+'\firmware\main" "'+$root+'\tests\link_state_test.c" "'+$root+'\firmware\main\link_state.c" /Fe:link-state-test.exe && link-state-test.exe'
    & cmd.exe /d /c $command
    if ($LASTEXITCODE) {throw 'Native link-state test failed'}
    $command = 'call "'+$devcmd+'" -arch=x64 >nul && cl /nologo /W4 /WX /I"'+$root+'\firmware\main" "'+$root+'\tests\asset_codec_test.c" "'+$root+'\firmware\main\asset_codec.c" /Fe:asset-codec-test.exe && asset-codec-test.exe'
    & cmd.exe /d /c $command
    if ($LASTEXITCODE) {throw 'Native asset-codec test failed'}
} finally {Pop-Location}
