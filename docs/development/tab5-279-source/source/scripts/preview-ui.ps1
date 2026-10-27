param([switch]$Clang,[string]$NotesPath,[string]$FontBaselineDir,[string]$BuildDirectory,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
if($NotesPath) {
    $NotesPath=(Resolve-Path -LiteralPath $NotesPath).Path
    $release=Get-Content -LiteralPath $NotesPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if($release.notes -notmatch '(?m)^## .+' -or $release.notes -notmatch '(?m)^- .+') {
        throw 'Release notes require category headings and list items; do not offer a single plain paragraph.'
    }
    if([Text.Encoding]::UTF8.GetByteCount($release.notes) -gt 1024){throw 'Release notes exceed 1024 UTF-8 bytes.'}
    $imagePath=$NotesPath -replace '\.notes\.json$',''
    if($imagePath -eq $NotesPath -or (Get-FileHash -LiteralPath $imagePath).Hash -ne $release.sha256){throw 'Release notes do not match the firmware image.'}
}

$root=Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/BuildResources.ps1"
$buildResourceScope = Enter-Tab5BuildResourceScope
try {
& "$root/.tools/idf-tools/python_env/idf5.4_py3.12_env/Scripts/python.exe" "$PSScriptRoot/apply-camera-dma-memory.py" "$root/.tools/esp-idf"
if ($LASTEXITCODE) {throw 'CSI DMA memory patch failed'}
& "$root/.tools/idf-tools/python_env/idf5.4_py3.12_env/Scripts/python.exe" "$PSScriptRoot/apply-ble-tx-safety.py" "$root/.tools/esp-idf" "$root/firmware"
if ($LASTEXITCODE) {throw 'Hosted BLE transmit safety patch failed'}
& "$root/.tools/idf-tools/python_env/idf5.4_py3.12_env/Scripts/python.exe" "$PSScriptRoot/apply-jpeg-dma-memory.py" "$root/.tools/esp-idf"
if ($LASTEXITCODE) {throw 'JPEG DMA/cache patch failed'}
& "$root/.tools/idf-tools/python_env/idf5.4_py3.12_env/Scripts/python.exe" "$PSScriptRoot/apply-lvgl-render-cache.py" "$root/firmware"
if ($LASTEXITCODE) {throw 'LVGL cache hooks patch failed'}
$vswhere="${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs=& $vswhere -latest -products '*' -property installationPath
$devcmd=Join-Path $vs 'Common7\Tools\VsDevCmd.bat'
$cmake=(Get-ChildItem "$root/.tools/idf-tools/tools/cmake" -Filter cmake.exe -Recurse | Select-Object -First 1).FullName
$ninja=(Get-ChildItem "$root/.tools/idf-tools/tools/ninja" -Filter ninja.exe -Recurse | Select-Object -First 1).DirectoryName
$build=Join-Path $root '.tools/ui-preview-build'
if($Clang) {$build=Join-Path $root '.tools/ui-preview-clang64'}
if($BuildDirectory){$build=[IO.Path]::GetFullPath($BuildDirectory)}
$output=Join-Path $root 'artifacts/ui-preview'
if($OutputDirectory){$output=[IO.Path]::GetFullPath($OutputDirectory)}
New-Item -ItemType Directory -Force $build,$output | Out-Null
$env:PATH="$ninja;"+$env:PATH
$compiler=if($Clang){' -DCMAKE_C_COMPILER="'+(Join-Path $vs 'VC\Tools\Llvm\x64\bin\clang-cl.exe')+'"'}else{''}
if($Clang){$compiler+=' -DCMAKE_CXX_COMPILER="'+(Join-Path $vs 'VC\Tools\Llvm\x64\bin\clang-cl.exe')+'"'}
if($FontBaselineDir){$compiler+=' -DTAB5_FONT_BASELINE_DIR="'+((Resolve-Path -LiteralPath $FontBaselineDir).Path.Replace('\','/'))+'"'}
$command='call "'+$devcmd+'" -arch=x64 >nul && "'+$cmake+'" -S "'+$root+'\tests\ui-preview" -B "'+$build+'" -G Ninja -DCMAKE_BUILD_TYPE=Release'+$compiler+' && "'+$cmake+'" --build "'+$build+'" --parallel '+$buildResourceScope.Policy.WorkerCount
& cmd.exe /d /c $command
if($LASTEXITCODE) {throw 'LVGL preview build failed'}
} finally { Exit-Tab5BuildResourceScope $buildResourceScope }
Push-Location $output
$previousNotes=$env:TAB5_RELEASE_NOTES_JSON
try {
    $env:TAB5_RELEASE_NOTES_JSON=$NotesPath
    & "$build/ui-preview.exe"; if($LASTEXITCODE) {throw 'LVGL preview failed'}
} finally {$env:TAB5_RELEASE_NOTES_JSON=$previousNotes;Pop-Location}
