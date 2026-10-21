"""Check the bundled helper identity across a changed app, without reading credentials."""
import argparse
import hashlib
import json
from pathlib import Path
import plistlib
import re
import shutil
import subprocess
import sys
import tempfile


def run(*args, success=True, **kwargs):
    result = subprocess.run(args, capture_output=True, timeout=30, **kwargs)
    if success and result.returncode != 0:
        raise RuntimeError('Identity fixture command failed: ' + str(args[0]))
    return result


def cdhash(path):
    text = run('codesign', '-d', '--verbose=4', str(path)).stderr.decode()
    return re.search(r'^CDHash=([a-f0-9]+)$', text, re.M).group(1)


def probe(app, expected):
    result = run(str(app / 'Contents/MacOS/AIBotBridge'), '--self-test-keychain-helper', success=False)
    assert (result.returncode == 0) == expected
    if expected:
        assert b'MAC_KEYCHAIN_HELPER_OK' in result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('app', type=Path)
    parser.add_argument('--identity', required=True)
    args = parser.parse_args()
    assert sys.platform == 'darwin' and args.identity != '-'
    app = args.app.resolve()
    helper = app / 'Contents/Helpers/AIBotKeychainHelper'
    run('codesign', '--verify', '--deep', '--strict', str(app))
    probe(app, True)
    request = json.dumps({'operation': 'probe', 'account': 'pairing-token'}).encode()
    rejected = run(str(helper), input=request, success=False)
    assert rejected.returncode != 0 and not rejected.stdout and not rejected.stderr
    original_hash = hashlib.sha256(helper.read_bytes()).hexdigest()
    original_cdhash = cdhash(app)
    with tempfile.TemporaryDirectory(prefix='aibot-keychain-identity-') as folder:
        changed = Path(folder) / 'AIBotBridge.app'
        shutil.copytree(app, changed)
        info = changed / 'Contents/Info.plist'
        values = plistlib.loads(info.read_bytes())
        values['CFBundleVersion'] = '999.0.1'
        info.write_bytes(plistlib.dumps(values))
        run('codesign', '--force', '--sign', args.identity, '--timestamp=none', '--options', 'runtime', str(changed))
        run('codesign', '--verify', '--deep', '--strict', str(changed))
        changed_helper = changed / 'Contents/Helpers/AIBotKeychainHelper'
        assert original_cdhash != cdhash(changed)
        assert original_hash == hashlib.sha256(changed_helper.read_bytes()).hexdigest()
        assert cdhash(helper) == cdhash(changed_helper)
        probe(changed, True)
        # A helper signed by a different identity cannot trigger direct fallback.
        run('codesign', '--force', '--sign', '-', '--timestamp=none', str(changed_helper))
        run('codesign', '--force', '--sign', args.identity, '--timestamp=none', '--options', 'runtime', str(changed))
        probe(changed, False)
    print('MAC_KEYCHAIN_IDENTITY_OK changed main CDHash; stable helper bytes/CDHash; wrong parent and helper rejected; no credentials accessed')


if __name__ == '__main__':
    main()
