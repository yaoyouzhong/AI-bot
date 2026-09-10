#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
package_root="$repo_root/mac-app"
app_root="${1:-$repo_root/artifacts/AIBotBridge.app}"
if [[ -e "$app_root" ]]; then
    echo "Use a new app output path: $app_root" >&2
    exit 1
fi
contents="$app_root/Contents"

swift build -c release --package-path "$package_root"
mkdir -p "$contents/MacOS"
install -m 755 "$package_root/.build/release/AIBotBridge" "$contents/MacOS/AIBotBridge"
install -m 644 "$package_root/Info.plist" "$contents/Info.plist"
plutil -lint "$contents/Info.plist"
codesign --force --sign - --options runtime \
    --entitlements "$package_root/AIBotBridge.entitlements" "$app_root"
codesign --verify --deep --strict --verbose=2 "$app_root"

echo "$app_root"
