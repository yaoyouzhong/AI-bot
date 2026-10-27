#!/usr/bin/env python3
"""Verify archived bytes; optionally verify a resolved project and patched SDK."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def verify(root, rows):
    failures = []
    for row in rows:
        relative = Path(row['path'])
        if relative.is_absolute() or '..' in relative.parts:
            raise ValueError('Unsafe manifest path')
        path = root / relative
        if not path.is_file() or path.is_symlink():
            failures.append(row['path'] + ': missing or symlink')
            continue
        data = path.read_bytes()
        if hashlib.sha256(data).hexdigest() != row['sha256']:
            failures.append(row['path'] + ': SHA-256 mismatch')
        elif 'bytes' in row and len(data) != row['bytes']:
            failures.append(row['path'] + ': size mismatch')
    if failures:
        raise ValueError('\n'.join(failures))
    return len(rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resolved-project', type=Path,
                        help='Project after locked dependency download and managed overrides')
    parser.add_argument('--sdk', type=Path, help='Patched ESP-IDF checkout')
    args = parser.parse_args()
    manifest = json.loads((ROOT / 'SOURCE.json').read_text(encoding='utf-8'))
    count = verify(ROOT, manifest['files'])
    print(f'TAB5_279_ARCHIVE_OK {count} payload files')
    if args.resolved_project:
        frozen = json.loads((ROOT / 'FROZEN-INPUTS.json').read_text(encoding='utf-8'))
        print(f'TAB5_279_RESOLVED_INPUTS_OK {verify(args.resolved_project, frozen)} files')
    if args.sdk:
        print(f'TAB5_279_SDK_CHANGES_OK {verify(args.sdk, manifest["sdk"]["modifiedFiles"])} files')


if __name__ == '__main__':
    main()
