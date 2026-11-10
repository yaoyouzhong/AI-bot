$ErrorActionPreference='Stop'
. "$PSScriptRoot\BuildResources.ps1"
$scope=Enter-Tab5BuildResourceScope
try {
 . "$PSScriptRoot\IdfEnvironment.ps1"
 $compiler=(Get-ChildItem "$env:IDF_TOOLS_PATH\tools" -Filter riscv32-esp-elf-gcc.exe -Recurse | Select-Object -First 1).DirectoryName
 $env:PATH="$compiler;"+$env:PATH
 python "$PSScriptRoot\probe-stack.py" gallery_remote.c voice_remote.c
 if($LASTEXITCODE){throw 'Stack probe compile failed'}

} finally {Exit-Tab5BuildResourceScope $scope}
