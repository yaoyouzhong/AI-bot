#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
printf 'Choose 1 for painting or 2 for calligraphy: '
read -r choice
case "$choice" in
1) archive='AI-bot-gallery-painting-hd-2026.11.12.1.zip'; whole='5173ef666cac71a2b13cfa8a85c952b2cc391d03afcf96342a17caf36326228b'; names=('AI-bot-gallery-painting-hd-2026.11.12.1.zip.part201' 'AI-bot-gallery-painting-hd-2026.11.12.1.zip.part202'); hashes=('2ddae33755db944b0a2aea7c689321c8b7738e92f1e4f6722c34b99bb3dfcdc6' '04ecc7e691c7e6d198f51467dce4399c84ede49e7eecf0b41932311ffa435d47');;
2) archive='AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip'; whole='5fc40849c20fb033faf9222d3a48a2f75377ccb6b64d8283530a9cef45a7176a'; names=('AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip.part201' 'AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip.part202'); hashes=('3a8c941f7e82b3474d08944bc6d74d5bbe04aeed5da26cd892846bb61a508043' '55266564e2b20812e15284d26d908d922e81e0784fe7b3980ee889acbe7050e2');;
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
