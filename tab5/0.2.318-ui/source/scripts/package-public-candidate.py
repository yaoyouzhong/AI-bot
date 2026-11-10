"""Package an already built public TAB5 candidate; no flashing or publication."""
import argparse
import hashlib
import importlib.util
import json
import re
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def git(*args, cwd=ROOT):
    return subprocess.check_output(['git', '-C', str(cwd), *args])


def sha(data):
    return hashlib.sha256(data).hexdigest()


def write_zip(path, files):
    with zipfile.ZipFile(path, 'x', zipfile.ZIP_DEFLATED) as archive:
        for name, data in sorted(files.items()):
            archive.writestr(name, data)
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
        assert all(archive.read(name) == data for name, data in files.items())
    path.with_suffix(path.suffix + '.sha256').write_text(sha(path.read_bytes()) + '  ' + path.name + '\n', encoding='ascii')


def package(output, notes_path, minimum_bridge_version='0.6.7'):
    if not re.fullmatch(r'\d+\.\d+\.\d+', minimum_bridge_version):
        raise ValueError('Expected a bridge semantic version')
    if git('status', '--porcelain').strip():
        raise ValueError('Commit the reviewed source before creating its candidate snapshot')
    build = ROOT / 'firmware/build'
    cache = (build / 'CMakeCache.txt').read_text(encoding='utf-8')
    if 'TAB5_LOCAL_ART:BOOL=ON' not in cache:
        raise ValueError('Expected the standard firmware with optional gallery support')
    spec = importlib.util.spec_from_file_location('first_install', ROOT / 'scripts/package-first-install.py')
    first = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(first)
    image = (build / 'aibot_tab5.bin').read_bytes()
    first.validate_image(image)
    version = image[48:80].split(b'\0')[0].decode('ascii')
    notes = json.loads(notes_path.read_text(encoding='utf-8-sig'))
    if notes['version'] != version or notes['sha256'] != sha(image):
        raise ValueError('Notes and built application disagree')
    output.mkdir(parents=True, exist_ok=False)
    first_path = output / f'TAB5-first-install-{version}.zip'
    first.package(build, first_path)
    with zipfile.ZipFile(first_path) as archive:
        licenses = {name: archive.read(name) for name in archive.namelist()
                    if name.startswith('licenses/') or name in ('LICENSE', 'THIRD_PARTY_NOTICES.md')}
    if not {'LICENSE', 'THIRD_PARTY_NOTICES.md'} <= licenses.keys():
        raise ValueError('Missing distribution notices')
    upgrade = dict(licenses)
    upgrade.update({'aibot_tab5.bin': image, 'aibot_tab5.bin.notes.json': notes_path.read_bytes(),
                    'EDITION.json': (json.dumps(dict(edition='standard', optionalArt=True, bundledArt=False,
                        minimumBridgeVersion=minimum_bridge_version, protocolVersion=1)) + '\n').encode(),
                    'README.txt': b'Public OTA application only. Use AI-bot TAB5 firmware upgrade.\n'
                                  b'Daily painting and calligraphy use separately downloaded optional collection packs.\n'
                                  b'Do not use the first-install ZIP for an existing installation.\n'})
    write_zip(output / f'TAB5-upgrade-{version}.zip', upgrade)
    source = dict(licenses)
    for raw in git('ls-files', '-z').split(b'\0'):
        if raw:
            name = raw.decode('utf-8')
            source['source/' + name] = (ROOT / name).read_bytes()
    source['source/firmware/sdkconfig'] = (ROOT / 'firmware/sdkconfig').read_bytes()
    sdk = ROOT / '.tools/esp-idf'
    source['ESP-IDF-local-changes.patch'] = git('diff', '--binary', 'HEAD', cwd=sdk)
    source['SOURCE-MANIFEST.json'] = (json.dumps({
        'version': version, 'edition': 'standard', 'optionalArt': True, 'bundledArt': False,
        'sourceCommit': git('rev-parse', 'HEAD').decode().strip(),
        'applicationSha256': sha(image), 'sdkCommit': git('rev-parse', 'HEAD', cwd=sdk).decode().strip(),
        'reproducibleBinaryVerified': False,
        'note': 'Standard firmware includes gallery support; optional artwork collections are distributed separately.'
    }, indent=2) + '\n').encode()
    source['FILES.sha256'] = ''.join(sha(data) + '  ' + name + '\n' for name, data in sorted(source.items())).encode()
    write_zip(output / f'TAB5-{version}-source.zip', source)
    print(f'TAB5_PUBLIC_CANDIDATE_OK version={version} imageSha256={sha(image)} sourceFiles={len(source)}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--notes', type=Path, required=True)
    parser.add_argument('--minimum-bridge-version', default='0.6.7')
    args = parser.parse_args()
    package(args.output.resolve(), args.notes.resolve(), args.minimum_bridge_version)
