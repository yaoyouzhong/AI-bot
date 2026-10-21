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
mkdir -p "$contents/Resources"
install -m 644 "$repo_root/LICENSE" "$repo_root/THIRD_PARTY_NOTICES.md" "$repo_root/docs/MAC-TAB5-CANDIDATE.md" "$contents/Resources/"
git -C "$repo_root" rev-parse HEAD > "$contents/Resources/SOURCE_SHA.txt"
if ! git -C "$repo_root" diff --quiet -- mac-app; then
    printf '%s\n' 'working-tree-changes=yes' >> "$contents/Resources/SOURCE_SHA.txt"
fi
python3 "$repo_root/scripts/bundle_flash_tool.py" macos "$contents/Resources/flash-tools"
plutil -lint "$contents/Info.plist"
sign_identity="${AIBOT_MAC_SIGN_IDENTITY:--}"
if [[ "$sign_identity" == "-" ]]; then
    echo "Ad-hoc candidate: changed builds may require fresh macOS authorization." >&2
fi
codesign --force --sign "$sign_identity" --options runtime \
    --entitlements "$package_root/AIBotBridge.entitlements" "$app_root"
codesign --verify --deep --strict --verbose=2 "$app_root"

echo "$app_root"
