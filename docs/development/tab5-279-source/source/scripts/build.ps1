param([string]$Port, [switch]$Flash, [switch]$Legacy, [switch]$NoHid, [switch]$CrashTrace, [switch]$LocalArt, [string]$LocalVersion)
if($LocalVersion -and !$LocalArt){throw 'LocalVersion requires LocalArt; public version comes from CMakeLists.txt'}
. "$PSScriptRoot\BuildResources.ps1"
$buildResourceScope = Enter-Tab5BuildResourceScope
try {
. "$PSScriptRoot\IdfEnvironment.ps1"
$env:IDF_PYTHON_ENV_PATH = Join-Path $env:IDF_TOOLS_PATH 'python_env\idf5.4_py3.12_env'
$toolPaths = @("$env:IDF_PYTHON_ENV_PATH\Scripts")
foreach ($executable in @('cmake.exe','ninja.exe','riscv32-esp-elf-gcc.exe')) {
    $tool = Get-ChildItem "$env:IDF_TOOLS_PATH\tools" -Filter $executable -Recurse | Select-Object -First 1
    if (-not $tool) { throw "Missing build dependency: $executable" }
    $toolDirectory = Get-Tab5ToolDirectory $tool.DirectoryName
    $toolPaths += $toolDirectory
}
$env:PATH = ($toolPaths -join ';') + ';' + $env:PATH
python "$PSScriptRoot/apply-display-diagnostics.py" "$env:IDF_PATH"
if ($LASTEXITCODE) { throw 'Display diagnostics patch failed' }
python "$PSScriptRoot/apply-nvs-blob-safety.py" "$env:IDF_PATH"
if ($LASTEXITCODE) { throw 'NVS blob safety patch failed' }
python "$PSScriptRoot/apply-usb-gpio-routing.py" "$env:IDF_PATH"
if ($LASTEXITCODE) { throw 'USB GPIO routing patch failed' }
python "$PSScriptRoot/apply-jpeg-dma-memory.py" "$env:IDF_PATH"
if ($LASTEXITCODE) { throw 'JPEG DMA memory patch failed' }
python "$PSScriptRoot/apply-camera-dma-memory.py" "$env:IDF_PATH"
if ($LASTEXITCODE) { throw 'CSI DMA memory patch failed' }
# A fresh checkout has no managed components. Resolve the pinned dependencies
# before applying patches to their sources; never commit the generated directory.
$managedSources = @(
    "$idfProjectRoot/firmware/managed_components/espressif__esp_hosted/host/drivers/transport/sdio/sdio_drv.c",
    "$idfProjectRoot/firmware/managed_components/lvgl__lvgl/src/core/lv_refr.c"
)
if (@($managedSources | Where-Object { -not (Test-Path -LiteralPath $_) }).Count) {
    python "$env:IDF_PATH/tools/idf.py" -C "$idfProjectRoot/firmware" -D ('TAB5_USB_HID=' + $(if ($NoHid) { 'OFF' } else { 'ON' })) reconfigure
    if ($LASTEXITCODE) { throw 'Initial dependency resolution failed' }
}
python "$PSScriptRoot/apply-hosted-sdio-memory.py" "$idfProjectRoot/firmware"
if ($LASTEXITCODE) { throw 'SDIO streaming memory patch failed' }
python "$PSScriptRoot/apply-hosted-rx-memory.py" "$idfProjectRoot/firmware"
if ($LASTEXITCODE) { throw 'SDIO receive packet memory patch failed' }
python "$PSScriptRoot/apply-hosted-tx-memory.py" "$idfProjectRoot/firmware"
if ($LASTEXITCODE) { throw 'SDIO packet pool memory patch failed' }
python "$PSScriptRoot/apply-ble-tx-safety.py" "$env:IDF_PATH" "$idfProjectRoot/firmware"
if ($LASTEXITCODE) { throw 'Hosted BLE transmit safety patch failed' }
python "$PSScriptRoot/apply-lvgl-render-cache.py" "$idfProjectRoot/firmware"
if ($LASTEXITCODE) { throw 'LVGL cache hooks patch failed' }
Push-Location "$idfProjectRoot\firmware"
try {
    # Let ESP-IDF resolve the compiler from PATH. Explicit short-path compiler
    # overrides conflict with CMake's canonical cache paths on reconfigure.
    $buildArgs = @()
    if ($Legacy) {
        $legacyConfig = Join-Path $idfProjectRoot '.tools\sdkconfig-ui-legacy'
        $configText = Get-Content "$idfProjectRoot\firmware\sdkconfig" -Raw
        $configText = $configText.Replace('partitions.csv','partitions-legacy.csv').Replace('CONFIG_BOOTLOADER_APP_ROLLBACK_ENABLE=y','# CONFIG_BOOTLOADER_APP_ROLLBACK_ENABLE is not set').Replace('CONFIG_APP_ROLLBACK_ENABLE=y','# CONFIG_APP_ROLLBACK_ENABLE is not set')
        if ($CrashTrace) {
            $configText = $configText.Replace('CONFIG_ESP_CONSOLE_SECONDARY_NONE=y','# CONFIG_ESP_CONSOLE_SECONDARY_NONE is not set').Replace('# CONFIG_ESP_CONSOLE_SECONDARY_USB_SERIAL_JTAG is not set','CONFIG_ESP_CONSOLE_SECONDARY_USB_SERIAL_JTAG=y').Replace('CONFIG_ESP_SYSTEM_PANIC_REBOOT_DELAY_SECONDS=0','CONFIG_ESP_SYSTEM_PANIC_REBOOT_DELAY_SECONDS=5')
        }
        if (-not (Test-Path $legacyConfig) -or [IO.File]::ReadAllText($legacyConfig) -ne $configText) {
            [IO.File]::WriteAllText($legacyConfig,$configText)
        }
        $buildArgs += @('-B','build-ui-legacy','-D',"SDKCONFIG=$legacyConfig")
    }
    $buildArgs += @('-D',('TAB5_USB_HID=' + $(if ($NoHid) { 'OFF' } else { 'ON' })))
    $buildArgs += @('-D','TAB5_LOCAL_ART=ON', '-D',('TAB5_LOCAL_VERSION='+$LocalVersion))
    # IDF invokes Ninja directly, so CMAKE_BUILD_PARALLEL_LEVEL is not sufficient.
    # One shared pool bounds concurrent compiler/linker processes to our CPU budget.
    $buildArgs += @('-D',('CMAKE_JOB_POOLS=tab5_build='+$buildResourceScope.Policy.WorkerCount),
        '-D','CMAKE_JOB_POOL_COMPILE=tab5_build','-D','CMAKE_JOB_POOL_LINK=tab5_build')
    python "$env:IDF_PATH\tools\idf.py" @buildArgs build
    if ($LASTEXITCODE) { throw 'TAB5 build failed' }
    if ($Flash) {
        throw 'Full flashing is disabled: preserve NVS/assets with migrate-ota.py for first migration; use OTA or verified application-only flashing afterwards.'
    }
} finally { Pop-Location }
} finally { Exit-Tab5BuildResourceScope $buildResourceScope }
