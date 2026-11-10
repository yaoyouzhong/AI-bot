param([string]$OutputDirectory)
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
 $build=Join-Path $root 'test-build'
 $env:PATH="$ninja;"+$env:PATH
 $command='call "'+$devcmd+'" -arch=x64 >nul && "'+$cmake+'" -S "'+$root+'\tests\ui-preview" -B "'+$build+'" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER="'+$compiler+'" -DCMAKE_CXX_COMPILER="'+$compiler+'" && "'+$cmake+'" --build "'+$build+'" --target ui-preview runtime-diagnostics-test --parallel '+$scope.Policy.WorkerCount
 & cmd.exe /d /c $command
 if($LASTEXITCODE){throw 'Gallery test build failed'}
 & "$build\runtime-diagnostics-test.exe"
 if($LASTEXITCODE){throw 'Runtime regression failed'}
 $out=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $root 'artifacts/pinyin-ui-preview'}
 New-Item -ItemType Directory -Path $out -Force | Out-Null
 $env:TAB5_PINYIN_ONLY='1'
 Push-Location $out
 try { & "$build\ui-preview.exe"; if($LASTEXITCODE){throw 'Pinyin keyboard UI regression failed'} }
 finally {Pop-Location;Remove-Item Env:TAB5_PINYIN_ONLY -ErrorAction SilentlyContinue}
} finally {Exit-Tab5BuildResourceScope $scope}
