#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
output="$1"
identity="$2"
binary="$repo_root/mac-app/.build/release/AIBotKeychainHelper"
helper_id=io.github.yaoyouzhong.aibot.bridge.keychain
mkdir -p "$(dirname "$output")"
if [[ "$identity" == "-" ]]; then
    install -m 755 "$binary" "$output"
    codesign --force --sign - --timestamp=none --options runtime --identifier "$helper_id" "$output"
    exit 0
fi
fingerprint="$(python3 - "$identity" <<'PY'
import re, subprocess, sys
identity = sys.argv[1]
if re.fullmatch(r'[0-9a-fA-F]{40}', identity):
    print(identity.lower())
else:
    rows = re.findall(r'^\s*\d+\)\s+([0-9A-Fa-f]{40})\s+"([^"]+)"', subprocess.check_output(['security', 'find-identity', '-p', 'codesigning'], text=True), re.M)
    matches = {fingerprint.lower() for fingerprint, name in rows if name == identity}
    if len(matches) != 1:
        raise SystemExit('Use an unambiguous certificate fingerprint for the Keychain helper')
    print(matches.pop())
PY
)"
# The helper is an independently versioned build input. Retain its exact signed
# bytes while only the bridge changes, including across checkout/build paths.
# This is a private build cache, not an installed helper or launchd service.
cache_key="$(python3 - "$repo_root" "$fingerprint" <<'PY'
import hashlib, pathlib, platform, subprocess, sys
root = pathlib.Path(sys.argv[1])
digest = hashlib.sha256(b'AIBotKeychainHelper-build-v1\0')
for folder in ['AIBotKeychainHelper', 'MacKeychainSupport']:
    for path in sorted((root / 'mac-app/Sources' / folder).glob('*.swift')):
        digest.update(path.name.encode() + b'\0' + path.read_bytes())
digest.update((root / 'scripts/build_macos_keychain_helper.sh').read_bytes())
digest.update((root / 'mac-app/Package.swift').read_bytes())
digest.update(sys.argv[2].encode())
digest.update(platform.machine().encode())
for command in [['swift', '--version'], ['xcrun', '--show-sdk-version']]:
    digest.update(subprocess.check_output(command, stderr=subprocess.STDOUT))
print(digest.hexdigest())
PY
)"
cache_root="$HOME/Library/Application Support/AI-bot/build-tools/keychain/$cache_key"
cached="$cache_root/AIBotKeychainHelper"
requirement="identifier \"$helper_id\" and certificate leaf = H\"$fingerprint\""
if [[ ! -f "$cached" ]]; then
    mkdir -p "$cache_root"
    chmod 700 "$cache_root"
    temporary="$(mktemp "$cache_root/.helper.XXXXXX")"
    trap 'rm -f "$temporary"' EXIT
    install -m 755 "$binary" "$temporary"
    codesign --force --sign "$identity" --timestamp=none --options runtime --identifier "$helper_id" "$temporary"
    codesign --verify --strict -R="${requirement}" "$temporary"
    mv "$temporary" "$cached"
    trap - EXIT
fi
# A broken cache is an error; never replace it silently with a newly authorized
# identity or fall back to direct reads from the changing main executable.
codesign --verify --strict -R="${requirement}" "$cached"
install -m 755 "$cached" "$output"
