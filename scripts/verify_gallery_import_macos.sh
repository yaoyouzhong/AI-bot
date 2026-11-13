#!/bin/bash
set -euo pipefail
: "${CATEGORY:?}" "${GH_TOKEN:?}"
for candidate in /Applications/Xcode_26.3.app /Applications/Xcode_26.2.app /Applications/Xcode_26.1.1.app /Applications/Xcode_26.0.1.app; do
  if [ -d "$candidate/Contents/Developer" ]; then
    export DEVELOPER_DIR="$candidate/Contents/Developer"
    break
  fi
done
swift --version
case "$CATEGORY" in painting|calligraphy) ;; *) exit 1 ;; esac
full_sha=$(python3 -c 'import json,sys; d=json.load(open("scripts/Gallery-HD-candidate.json")); print(next(a["sha256"] for a in d["archives"] if a["name"].startswith("AI-bot-gallery-"+sys.argv[1]+"-")))' "$CATEGORY")
repo=yaoyouzhong/AI-bot
temp="${RUNNER_TEMP:?}/gallery-$CATEGORY"
mkdir -p "$temp"
test_file=mac-app/Tests/AIBotBridgeTests/GalleryDeliveryImportTests.swift
cp scripts/GalleryDeliveryImportTests.swift "$test_file"
export GALLERY_TEST_CATEGORY="$CATEGORY"
standard="AI-bot-gallery-$CATEGORY-standard-2026.11.12.1.zip"
gh release download v0.6.20 --repo "$repo" --pattern "$standard" --dir "$temp"
gh release download v0.6.20 --repo "$repo" --pattern "$standard.sha256" --dir "$temp"
expected_standard=$(tr -d '\r' < "$temp/$standard.sha256" | cut -c 1-64)
actual_standard=$(shasum -a 256 "$temp/$standard" | cut -d ' ' -f 1)
[ "$actual_standard" = "$expected_standard" ]
export GALLERY_TEST_PACKAGE="$temp/$standard" GALLERY_TEST_KIND=standard
(cd mac-app && swift test --filter GalleryDeliveryImportTests/testPublishedPackageImport)
rm "$temp/$standard" "$temp/$standard.sha256"
name="AI-bot-gallery-$CATEGORY-hd-2026.11.12.1.zip"
cp scripts/Download-HD-Gallery.command "$temp/Download-HD-Gallery.command"
if [ "$CATEGORY" = painting ]; then choice=1; else choice=2; fi
(cd "$temp" && printf '%s\n' "$choice" | bash ./Download-HD-Gallery.command)
echo "$full_sha  $temp/$name" | shasum -a 256 -c -
rm "$temp/$name.part201" "$temp/$name.part202"
export GALLERY_TEST_PACKAGE="$temp/$name" GALLERY_TEST_KIND=hd
(cd mac-app && swift test --filter GalleryDeliveryImportTests/testPublishedPackageImport)
echo "MACOS_ACTUAL_GALLERY_IMPORT_OK $CATEGORY"
