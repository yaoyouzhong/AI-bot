"""Verified, restartable GitHub release uploads; no credentials are persisted.

GitHub cannot resume bytes inside a release asset. Split large archives first,
then resume at an independently hashed part boundary. Uses the real console gh.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
import os
from pathlib import Path
import re
import threading
import time

from github_cli import run_cli


def digest(path):
    with path.open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def safe_name(name):
    if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]*', name):
        raise ValueError('Unsafe asset name: ' + name)
    return name


def split(files, output, chunk_bytes):
    output.mkdir(parents=True, exist_ok=True)
    groups = []
    for source in files:
        name = safe_name(source.name)
        parts = []
        whole = hashlib.sha256()
        with source.open('rb') as reader:
            number = 0
            while chunk := reader.read(chunk_bytes):
                number += 1
                part_name = f'{name}.part{number:03d}'
                sha = hashlib.sha256(chunk).hexdigest()
                target = output / part_name
                # An interrupted local split is resumed without rewriting good parts.
                if not target.exists() or target.stat().st_size != len(chunk) or digest(target) != sha:
                    temporary = target.with_suffix(target.suffix + '.partial')
                    temporary.write_bytes(chunk)
                    temporary.replace(target)
                parts.append({'name': part_name, 'bytes': len(chunk), 'sha256': sha})
                whole.update(chunk)
        groups.append({'name': name, 'bytes': source.stat().st_size,
                       'sha256': whole.hexdigest(), 'parts': parts})
        print('SPLIT_VERIFIED', name, len(parts), flush=True)
    return {'schema': 1, 'chunkBytes': chunk_bytes, 'archives': groups}


def upload(repository, tag, directory, manifest, state_path, workers=2, retries=5):
    # Validate all local payloads before the first network mutation.
    for name, expected in manifest.items():
        safe_name(name)
        path = directory / name
        if path.stat().st_size != expected['bytes'] or digest(path) != expected['sha256']:
            raise ValueError('Local asset mismatch: ' + name)
    lock = threading.Lock()
    state = {'repository': repository, 'tag': tag, 'verified': {}}

    def release():
        return json.loads(run_cli(['release', 'view', tag, '--repo', repository,
                                   '--json', 'databaseId,tagName,isDraft']).stdout)

    identity = release()
    if identity['tagName'] != tag:
        raise ValueError('Release tag mismatch')
    release_id = identity['databaseId']

    def api():
        result = json.loads(run_cli(['api', f'repos/{repository}/releases/{release_id}'], timeout=90).stdout)
        if result['tag_name'] != tag:
            raise ValueError('Release identity changed')
        return result

    def one(item):
        name, expected = item
        for attempt in range(1, retries + 1):
            try:
                asset = next((a for a in api()['assets'] if a['name'] == name), None)
                if asset and asset['size'] == expected['bytes'] and asset.get('digest') == 'sha256:' + expected['sha256']:
                    with lock:
                        state['verified'][name] = {'id': asset['id'], 'bytes': asset['size'], 'digest': asset['digest']}
                        temporary = state_path.with_suffix('.partial')
                        temporary.write_text(json.dumps(state, indent=2) + '\n', encoding='utf-8')
                        temporary.replace(state_path)
                    print('SERVER_DIGEST_VERIFIED', name, flush=True)
                    return
                if asset and asset['state'] == 'uploaded' and not identity['isDraft']:
                    raise ValueError('Refusing to replace a different published asset: ' + name)
                print('UPLOAD_START', name, 'attempt=' + str(attempt), flush=True)
                run_cli(['release', 'upload', tag, str(directory / name), '--repo', repository,
                         '--clobber'], timeout=1800)
                # Verify on the next loop, including when a prior upload succeeded
                # server-side but the connection dropped before receiving its response.
                asset = next(a for a in api()['assets'] if a['name'] == name)
                if asset['size'] != expected['bytes'] or asset.get('digest') != 'sha256:' + expected['sha256']:
                    raise ValueError('Server digest mismatch: ' + name)
                with lock:
                    state['verified'][name] = {'id': asset['id'], 'bytes': asset['size'], 'digest': asset['digest']}
                    temporary = state_path.with_suffix('.partial')
                    temporary.write_text(json.dumps(state, indent=2) + '\n', encoding='utf-8')
                    temporary.replace(state_path)
                print('SERVER_DIGEST_VERIFIED', name, flush=True)
                return
            except ValueError:
                raise
            except Exception as error:
                print('UPLOAD_RETRY', name, type(error).__name__, flush=True)
                if attempt == retries:
                    raise
                time.sleep(min(30, attempt * 3))
    with ThreadPoolExecutor(max_workers=workers) as pool:
        list(pool.map(one, sorted(manifest.items(), key=lambda i: i[1]['bytes'])))
    print('UPLOAD_COMPLETE', len(state['verified']), flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    create = sub.add_parser('split')
    create.add_argument('files', nargs='+', type=Path)
    create.add_argument('--output', required=True, type=Path)
    create.add_argument('--chunk-mib', type=int, default=128)
    send = sub.add_parser('upload')
    send.add_argument('--repo', required=True)
    send.add_argument('--tag', required=True)
    send.add_argument('--directory', type=Path, required=True)
    send.add_argument('--manifest', type=Path, required=True)
    send.add_argument('--state', type=Path, required=True)
    send.add_argument('--workers', type=int, choices=range(1, 5), default=2)
    send.add_argument('--direct', action='store_true', help='Ignore proxy variables in this process only')
    args = parser.parse_args()
    if args.command == 'split':
        if not 1 <= args.chunk_mib <= 1024:
            parser.error('chunk-mib must be between 1 and 1024')
        result = split(args.files, args.output, args.chunk_mib * 1024 * 1024)
        metadata = args.output / 'HD-Gallery-parts.json'
        metadata.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        assets = {part['name']: {'bytes': part['bytes'], 'sha256': part['sha256']}
                  for archive in result['archives'] for part in archive['parts']}
        assets[metadata.name] = {'bytes': metadata.stat().st_size, 'sha256': digest(metadata)}
        (args.output / 'upload-manifest.json').write_text(json.dumps(assets, indent=2) + '\n', encoding='utf-8')
    else:
        if args.direct:
            for key in list(os.environ):
                if 'proxy' in key.lower():
                    os.environ.pop(key)
        upload(args.repo, args.tag, args.directory.resolve(),
               json.loads(args.manifest.read_text(encoding='utf-8')), args.state.resolve(), args.workers)


if __name__ == '__main__':
    main()
