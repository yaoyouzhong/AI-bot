param([Parameter(Mandatory)][string]$BaselineDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot\BuildResources.ps1"
$scope=Enter-Tab5BuildResourceScope
try {
 $vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
 $vs=& $vswhere -latest -products '*' -property installationPath
 $devcmd=Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
 $cmake=(Get-ChildItem "$root/.tools/idf-tools/tools/cmake" -Filter cmake.exe -Recurse | Select-Object -First 1).FullName
 $ninja=(Get-ChildItem "$root/.tools/idf-tools/tools/ninja" -Filter ninja.exe -Recurse | Select-Object -First 1).DirectoryName
 $compiler=Join-Path $vs 'VC\Tools\Llvm\x64\bin\clang-cl.exe'
 $build=Join-Path $root 'pinyin-test-build'
 $env:PATH="$ninja;"+$env:PATH
 $baseline=(Resolve-Path -LiteralPath $BaselineDirectory).Path
 $command='call "'+$devcmd+'" -arch=x64 >nul && "'+$cmake+'" -S "'+$root+'\tests\pinyin" -B "'+$build+'" -G Ninja -DBASELINE="'+$baseline+'" -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER="'+$compiler+'" && "'+$cmake+'" --build "'+$build+'" --parallel '+$scope.Policy.WorkerCount
 & cmd.exe /d /c $command
 if($LASTEXITCODE){throw 'Pinyin test build failed'}
 & "$build\pinyin-test.exe"
 if($LASTEXITCODE){throw 'Pinyin regression failed'}
} finally {Exit-Tab5BuildResourceScope $scope}
