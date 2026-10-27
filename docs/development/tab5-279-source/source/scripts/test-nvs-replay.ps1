param([Parameter(Mandatory)][string]$BackupPath,[string]$BuildDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$BackupPath=(Resolve-Path -LiteralPath $BackupPath).Path
if((Get-Item -LiteralPath $BackupPath).Length -ne 24576){throw 'Expected a private 24 KiB pre-202 NVS backup.'}
$python=Join-Path $root '.tools/idf-tools/python_env/idf5.4_py3.12_env/Scripts/python.exe'
& $python "$PSScriptRoot/apply-nvs-blob-safety.py" "$root/.tools/esp-idf"
if($LASTEXITCODE){throw 'NVS safety patch failed'}
$vs=& "${env:ProgramFiles(x86)}/Microsoft Visual Studio/Installer/vswhere.exe" -latest -products '*' -property installationPath
$cmake=(Get-ChildItem "$root/.tools/idf-tools/tools/cmake" -Filter cmake.exe -Recurse|Select-Object -First 1).FullName
$ninja=(Get-ChildItem "$root/.tools/idf-tools/tools/ninja" -Filter ninja.exe -Recurse|Select-Object -First 1).DirectoryName
if(!$BuildDirectory){$BuildDirectory=Join-Path $root '.tools/nvs-replay-build'}
$BuildDirectory=[IO.Path]::GetFullPath($BuildDirectory)
$env:PATH="$ninja;"+$env:PATH
$compiler=Join-Path $vs 'VC/Tools/Llvm/x64/bin/clang-cl.exe'
$command='call "'+$vs+'\Common7\Tools\VsDevCmd.bat" -arch=x64 >nul && "'+$cmake+'" -S "'+$root+'/tests/nvs-replay" -B "'+$BuildDirectory+'" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER="'+$compiler+'" -DCMAKE_CXX_COMPILER="'+$compiler+'" && "'+$cmake+'" --build "'+$BuildDirectory+'" --parallel 8'
& cmd.exe /d /c $command
if($LASTEXITCODE){throw 'NVS replay build failed'}
& "$BuildDirectory/nvs-replay.exe" $BackupPath
if($LASTEXITCODE){throw 'NVS replay failed; no device or private backup was modified'}
