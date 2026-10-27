#!/usr/bin/env bash
# Native counterpart to build.ps1: same pinned patches and task-local resource budget.
set -euo pipefail
if [[ $(uname -s) != Darwin ]]; then echo 'This wrapper requires macOS' >&2; exit 1; fi
if [[ ${1:-} != --resource-scope ]]; then
    exec /usr/bin/nice -n 10 /usr/sbin/taskpolicy -b /bin/bash "$0" --resource-scope "$@"
fi
shift
if [[ $# != 3 ]]; then echo 'Usage: build-macos.sh <IDF checkout> <IDF tools> <local candidate version>' >&2; exit 1; fi
export IDF_PATH=$(cd "$1" && pwd -P)
export IDF_TOOLS_PATH=$(cd "$2" && pwd -P)
tab5_version=$3
if [[ ! "$tab5_version" =~ ^0\.2\.(0|[1-9][0-9]*)-ui$ ]]; then
    echo 'Use canonical 0.2.N-ui: bridge capability checks require a numeric patch version' >&2; exit 1
fi
tab5_root=$(cd "$(dirname "$0")/.." && pwd -P)
if [[ $(git -C "$IDF_PATH" rev-parse HEAD) != f5c3654a1c2d2a01f7f67def7a0dc48e691f63c0 ]]; then
    echo 'Expected the archived ESP-IDF v5.4.2 revision' >&2; exit 1
fi
export CMAKE_BUILD_PARALLEL_LEVEL=2
# Background scheduling plus two shared compile/link slots is local to this
# process tree. macOS has no supported per-process physical-core affinity mask.
source "$IDF_PATH/export.sh" >/dev/null
for patch in display-diagnostics nvs-blob-safety usb-gpio-routing jpeg-dma-memory camera-dma-memory; do
    python "$tab5_root/scripts/apply-$patch.py" "$IDF_PATH"
done
tab5_args=(-C "$tab5_root/firmware" -B "$tab5_root/firmware/build-macos"
    -D TAB5_USB_HID=ON -D TAB5_LOCAL_ART=ON -D "TAB5_LOCAL_VERSION=$tab5_version"
    -D CMAKE_JOB_POOLS=tab5_build=2 -D CMAKE_JOB_POOL_COMPILE=tab5_build -D CMAKE_JOB_POOL_LINK=tab5_build)
python "$IDF_PATH/tools/idf.py" "${tab5_args[@]}" reconfigure
for patch in hosted-sdio-memory hosted-rx-memory hosted-tx-memory; do
    python "$tab5_root/scripts/apply-$patch.py" "$tab5_root/firmware"
done
python "$tab5_root/scripts/apply-ble-tx-safety.py" "$IDF_PATH" "$tab5_root/firmware"
python "$tab5_root/scripts/apply-lvgl-render-cache.py" "$tab5_root/firmware"
python "$IDF_PATH/tools/idf.py" "${tab5_args[@]}" build
