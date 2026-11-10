$ErrorActionPreference='Stop'
. "$PSScriptRoot\BuildResources.ps1"
$scope=Enter-Tab5BuildResourceScope
try {
 . "$PSScriptRoot\IdfEnvironment.ps1"
 $compiler=(Get-ChildItem "$env:IDF_TOOLS_PATH\tools" -Filter riscv32-esp-elf-gcc.exe -Recurse | Select-Object -First 1).DirectoryName
 $env:PATH="$compiler;"+$env:PATH
 python "$PSScriptRoot\compile-gallery-object.py" gallery_store.c
 if($LASTEXITCODE){throw 'Firmware object compilation failed'}
 $size=Join-Path $compiler 'riscv32-esp-elf-size.exe'
 $candidate=Join-Path (Split-Path $PSScriptRoot -Parent) 'firmware\build\esp-idf\main\CMakeFiles\__idf_main.dir\gallery_store.c.obj'
 $prior='C:\Users\yaououzhong\Work\AI-bot\artifacts\tab5-birthday-health-302-20261103\source\firmware\build\esp-idf\main\CMakeFiles\__idf_main.dir\gallery_store.c.obj'
 & $size $prior $candidate
} finally {Exit-Tab5BuildResourceScope $scope}
