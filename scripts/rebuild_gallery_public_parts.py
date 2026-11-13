"""Rebuild the candidate from verified public parts for read-only Mac CI."""
import ast
import hashlib
import json
from pathlib import Path
import shutil
import sys
import urllib.request
import zipfile

category, output = sys.argv[1], Path(sys.argv[2])
if category not in ('painting', 'calligraphy'):
    raise ValueError('Unexpected category')
base = 'https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.20/'

def fetch(name):
    request = urllib.request.Request(base + name, headers={'User-Agent': 'AI-bot-gallery-mac-validation'})
    return urllib.request.urlopen(request, timeout=120)

with fetch('HD-Gallery-parts.json') as response:
    data = response.read()
if hashlib.sha256(data).hexdigest() != '9b6870846a58b1515646746e3e438ea36a2a73d4c32f3f3eb390063fd59e471e':
    raise ValueError('Old gallery manifest differs from verified release')
name = f'AI-bot-gallery-{category}-hd-2026.11.12.1.zip'
old = next(a for a in json.loads(data)['archives'] if a['name'] == name)
new = next(a for a in json.loads(Path('scripts/Gallery-HD-candidate.json').read_text(encoding='utf-8'))['archives'] if a['name'] == name)
source = output / 'verified-original.zip'
whole = hashlib.sha256()
with source.open('wb') as writer:
    for part in old['parts']:
        digest, size = hashlib.sha256(), 0
        with fetch(part['name']) as response:
            while chunk := response.read(1024 * 1024):
                digest.update(chunk); whole.update(chunk); size += len(chunk); writer.write(chunk)
        if size != part['bytes'] or digest.hexdigest() != part['sha256']:
            raise ValueError('Original part failed verification: ' + part['name'])
if source.stat().st_size != old['bytes'] or whole.hexdigest() != old['sha256']:
    raise ValueError('Original archive failed verification')
tree = ast.parse(Path('scripts/migrate_gallery_parts.py').read_text(encoding='utf-8'))
function = next(n for n in tree.body if isinstance(n, ast.FunctionDef) and n.name == 'corrected_archive')
namespace = {'hashlib': hashlib, 'shutil': shutil, 'zipfile': zipfile}
exec(compile(ast.Module(body=[function], type_ignores=[]), '<migration>', 'exec'), namespace)
corrected = output / 'verified-corrected.zip'
namespace['corrected_archive'](source, corrected)
source.unlink()
with corrected.open('rb') as stream:
    for part in new['parts']:
        digest, left = hashlib.sha256(), part['bytes']
        with (output / part['name']).open('wb') as writer:
            while left:
                chunk = stream.read(min(left, 1024 * 1024))
                if not chunk: raise ValueError('Truncated corrected archive')
                writer.write(chunk); digest.update(chunk); left -= len(chunk)
        if digest.hexdigest() != part['sha256']:
            raise ValueError('Corrected part differs from uploaded asset: ' + part['name'])
    if stream.read(1): raise ValueError('Unexpected corrected archive bytes')
corrected.unlink()
print('PUBLIC_PARTS_REBUILT_AND_VERIFIED', category, flush=True)
