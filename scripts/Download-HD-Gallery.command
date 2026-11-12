#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
printf 'Choose 1 for painting or 2 for calligraphy: '
read -r choice
case "$choice" in
1) archive='AI-bot-gallery-painting-hd-2026.11.12.1.zip'; whole='63cd2f20c0322db36615110e329e0f36cd8dc785d590420a6d4e2359aa266045'; names=('AI-bot-gallery-painting-hd-2026.11.12.1.zip.part101' 'AI-bot-gallery-painting-hd-2026.11.12.1.zip.part102'); hashes=('1f1d4e292798e1f8db06bab798f1dc4f8c4e6287d2269d57831c03d265f6f704' '8f144d5f48be9342f521445c70c2fe615623c1b5e71a70adc7c21ed0ae43b9f9');;
2) archive='AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip'; whole='f1ff5f59f54dce19341d8a3dcfe560771036979013b0836217a2c639828b608b'; names=('AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip.part101' 'AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip.part102'); hashes=('8e0b28f3624aede795391f2c29ee37a40ca6250fc453df4a387c9f4dce079de7' 'ebe70aa1728bf2473e57a688e66dfa546cdee68cc74d3c22352d7c4d16ae6bb3');;
*) echo 'Invalid selection'; exit 1;;
esac
base='https://github.com/yaoyouzhong/AI-bot/releases/download/gallery-2026.11.12.1'
if [ -e "$archive" ]; then
  actual=$(shasum -a 256 "$archive" | cut -d ' ' -f 1)
  [ "$actual" = "$whole" ] && { echo "Verified: $archive"; exit 0; }
  echo 'Existing ZIP differs. Move it before retrying.'; exit 1
fi
for i in 0 1; do
  name="${names[$i]}"
  if [ ! -e "$name" ]; then
    curl --fail --location --retry 5 --retry-delay 3 --continue-at - --output "$name.download" "$base/$name"
    mv "$name.download" "$name"
  fi
  actual=$(shasum -a 256 "$name" | cut -d ' ' -f 1)
  [ "$actual" = "${hashes[$i]}" ] || { echo "Checksum mismatch: $name"; exit 1; }
done
if [ -e "$archive.partial" ]; then echo "Move existing $archive.partial before retrying."; exit 1; fi
cat "${names[0]}" "${names[1]}" > "$archive.partial"
actual=$(shasum -a 256 "$archive.partial" | cut -d ' ' -f 1)
[ "$actual" = "$whole" ] || { echo 'ZIP checksum mismatch'; exit 1; }
mv "$archive.partial" "$archive"
echo "Verified: $archive. Import the ZIP in AI-bot Gallery settings."
