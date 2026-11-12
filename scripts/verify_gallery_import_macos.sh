#!/bin/bash
set -euo pipefail
: "${CATEGORY:?}" "${GH_TOKEN:?}"
case "$CATEGORY" in
  painting) full_sha=63cd2f20c0322db36615110e329e0f36cd8dc785d590420a6d4e2359aa266045 ;;
  calligraphy) full_sha=f1ff5f59f54dce19341d8a3dcfe560771036979013b0836217a2c639828b608b ;;
  *) exit 1 ;;
esac
repo=yaoyouzhong/AI-bot
temp="${RUNNER_TEMP:?}/gallery-$CATEGORY"
mkdir -p "$temp"
test_file=mac-app/Tests/AIBotBridgeTests/GalleryDeliveryImportTests.swift
cp scripts/GalleryDeliveryImportTests.swift "$test_file"
export GALLERY_TEST_CATEGORY="$CATEGORY"
standard="AI-bot-gallery-$CATEGORY-standard-2026.11.12.1.zip"
gh release download v0.6.20 --repo "$repo" --pattern "$standard" --dir "$temp"
gh release download v0.6.20 --repo "$repo" --pattern "$standard.sha256" --dir "$temp"
(cd "$temp" && shasum -a 256 -c "$standard.sha256")
export GALLERY_TEST_PACKAGE="$temp/$standard" GALLERY_TEST_KIND=standard
(cd mac-app && swift test --filter GalleryDeliveryImportTests/testPublishedPackageImport)
rm "$temp/$standard" "$temp/$standard.sha256"
name="AI-bot-gallery-$CATEGORY-hd-2026.11.12.1.zip"
for suffix in 101 102; do
  gh release download gallery-2026.11.12.1 --repo "$repo" --pattern "$name.part$suffix" --dir "$temp"
done
cat "$temp/$name.part101" "$temp/$name.part102" > "$temp/$name"
echo "$full_sha  $temp/$name" | shasum -a 256 -c -
rm "$temp/$name.part101" "$temp/$name.part102"
export GALLERY_TEST_PACKAGE="$temp/$name" GALLERY_TEST_KIND=hd
(cd mac-app && swift test --filter GalleryDeliveryImportTests/testPublishedPackageImport)
echo "MACOS_ACTUAL_GALLERY_IMPORT_OK $CATEGORY"
