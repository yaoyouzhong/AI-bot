#!/usr/bin/env python3
"""Verify the complete TAB5 .318 archive and optional resolved build inputs."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath

ROOT = Path(__file__).resolve().parent

def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def verify(root, rows):
    for row in rows:
        rel = PurePosixPath(row['path'])
        if rel.is_absolute() or '..' in rel.parts or '\\' in str(rel) or ':' in str(rel):
            raise ValueError('Unsafe manifest path')
        path = root.joinpath(*rel.parts)
        if not path.is_file() or path.is_symlink():
            raise ValueError(f'Missing file or symlink: {rel}')
        if digest(path) not in [row['sha256'], *row.get('equivalentLineEndingSha256', [])]:
            raise ValueError(f'SHA-256 mismatch: {rel}')
        if 'bytes' in row and path.stat().st_size != row['bytes']:
            raise ValueError(f'Size mismatch: {rel}')
    return len(rows)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--resolved-project', type=Path)
    parser.add_argument('--sdk', type=Path)
    parser.add_argument('--firmware', type=Path)
    parser.add_argument('--elf', type=Path)
    args = parser.parse_args()
    manifest = json.loads((ROOT / 'SOURCE-MANIFEST.json').read_text(encoding='utf-8'))
    checksum_rows = []
    for line in (ROOT / 'FILES.sha256').read_text(encoding='utf-8').splitlines():
        value, relative = line.split('  ', 1)
        checksum_rows.append({'path': relative, 'sha256': value})
    expected = {row['path'] for row in checksum_rows} | {'FILES.sha256'}
    actual = {p.relative_to(ROOT).as_posix() for p in ROOT.rglob('*') if p.is_file()}
    if expected != actual:
        raise ValueError('Archive file inventory mismatch')
    count = verify(ROOT, checksum_rows)
    verify(ROOT, manifest['files'])
    print(f'TAB5_318_ARCHIVE_OK {count} hashed files')
    if args.resolved_project:
        rows = json.loads((ROOT / 'RESOLVED-INPUTS.json').read_text(encoding='utf-8'))
        print(f'TAB5_318_RESOLVED_INPUTS_OK {verify(args.resolved_project, rows)} files')
    if args.sdk:
        print(f'TAB5_318_SDK_CHANGES_OK {verify(args.sdk, manifest["sdk"]["modifiedFiles"])} files')
    for path, field in ((args.firmware, 'applicationSha256'), (args.elf, 'elfSha256')):
        if path:
            if digest(path) != manifest[field]:
                raise ValueError(f'{field} mismatch')
            print(f'TAB5_318_IDENTITY_OK {field}={manifest[field]}')

if __name__ == '__main__':
    main()
