#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"
output="${1:?Usage: bash scripts/package_macos.sh NEW_OUTPUT_DIRECTORY}"
[[ ! -e "$output" ]] || { echo 'Use a new output directory' >&2; exit 1; }
[[ "$(uname -m)" == arm64 ]] || { echo 'This candidate targets Apple Silicon only' >&2; exit 1; }
python3 scripts/check_version.py
python3 scripts/check_public_content.py
python3 scripts/collect_distribution_materials.py verify
swift test --package-path mac-app
mkdir -p "$output"
mkdir -p "$repo_root/artifacts"
output="$(cd "$output" && pwd)"
stage="$(mktemp -d "$repo_root/artifacts/mac-package.XXXXXX")"
bash scripts/build_macos_app.sh "$stage/AIBotBridge.app"
lipo "$stage/AIBotBridge.app/Contents/MacOS/AIBotBridge" -verify_arch arm64
cp LICENSE THIRD_PARTY_NOTICES.md "$stage/"
cp docs/MAC_PACKAGE.md "$stage/README.md"
version="$(tr -d '\r\n' < VERSION)"
archive="$output/AIBotBridge-$version-local-candidate-macos-arm64.zip"
(cd "$stage" && find . -type f ! -name FILES.sha256 -exec shasum -a 256 {} \; > FILES.sha256)
ditto -c -k "$stage" "$archive"
unpacked="$(mktemp -d "$repo_root/artifacts/mac-unpacked.XXXXXX")"
ditto -x -k "$archive" "$unpacked"
(cd "$unpacked" && shasum -a 256 -c FILES.sha256)
plutil -lint "$unpacked/AIBotBridge.app/Contents/Info.plist"
codesign --verify --deep --strict --verbose=2 "$unpacked/AIBotBridge.app"
(cd "$output" && shasum -a 256 "$(basename "$archive")" > "$(basename "$archive").sha256")
echo "MAC_PACKAGE_OK zip=$archive; interactive acceptance pending; ad-hoc signed, not notarized"
