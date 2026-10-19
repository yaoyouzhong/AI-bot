#!/usr/bin/env bash
set -euo pipefail
if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "macOS is required; no Swift build or native UI pass can be reported on this host." >&2
    exit 2
fi
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
swift test --package-path "$repo_root/mac-app"
swift build -c release --package-path "$repo_root/mac-app"
git -C "$repo_root" diff --check -- mac-app scripts/mac_tab5_interop scripts/test_macos_tab5_contract.py scripts/verify_macos_tab5.sh
echo "MAC_TAB5_TEST_AND_BUILD_OK (hardware and native UI acceptance remain separate)"
