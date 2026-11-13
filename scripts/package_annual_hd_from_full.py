"""Build separate annual standard and HD ZIPs from one installed full collection."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile

from package_daily_art import package


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def build(standard_root, desktop_root, output, category, version):
    if output.exists():
        raise ValueError('Use a new output directory')
    full = read(standard_root/'PACK.json')
    if full.get('collection') != 'full' or full.get('category') != category:
        raise ValueError('The standard source must be the matching full collection')
    desktop_root = desktop_root.resolve(strict=True)
    sums = {}
    for line in (desktop_root/'SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines():
        if len(line) < 67 or line[64:66] != '  ' or line[66:] in sums:
            raise ValueError('Invalid HD checksum inventory')
        sums[line[66:]] = line[:64].lower()
    for name in ('catalog.json','desktop-index.relative.json'):
        if digest(desktop_root/name) != sums.get(name):
            raise ValueError('HD metadata checksum mismatch: '+name)
    hd_catalog = read(desktop_root/'catalog.json')
    if hd_catalog.get('schema') != 1 or hd_catalog.get('category') != category:
        raise ValueError('HD category differs from full collection')
    hd = read(desktop_root/'desktop-index.relative.json')
    if hd.get('schema') != 1:
        raise ValueError('Unsupported HD index')
    output.mkdir(parents=True)
    standard_output = output/'standard'
    package(standard_root,standard_output,version,category=category,collection='annual')
    archives = list(standard_output.glob('*.zip'))
    if len(archives) != 1:
        raise ValueError('Annual standard archive missing')
    standard = archives[0]
    with zipfile.ZipFile(standard) as archive:
        manifest = json.loads(archive.read('PACK.json'))
        works = json.loads(archive.read('catalog.json'))
        license_text = archive.read('LICENSE')
        notices = archive.read('THIRD_PARTY_NOTICES.md')
    ids = {w['id'] for w in works}
    expected = {(w['id'],w['workId'],frame) for w in works for frame in w['frames']}
    pages = [p for p in hd['pages'] if p['category'] == category and p['id'] in ids]
    if len(expected) != len(pages) or {(p['id'],p['workId'],p['frame']) for p in pages} != expected:
        raise ValueError('HD pages do not match the annual catalogue')
    if any(manifest['files'].get(p['frame']) != p['previewSha256'] for p in pages):
        raise ValueError('HD pages do not match the annual display images')
    keys = {part['source'] for page in pages for part in page['parts']}
    sources = {key:hd['sources'][key] for key in keys}
    paths = {}
    for source in sources.values():
        name = source['path']
        if not isinstance(name,str) or '\\' in name or ':' in name or name.startswith('/'):
            raise ValueError('Unsafe HD source path')
        relative = Path(name)
        path = (desktop_root/relative).resolve(strict=True)
        if relative.is_absolute() or '..' in relative.parts or not path.is_relative_to(desktop_root):
            raise ValueError('Unsafe HD source path')
        if sums.get(name) != source['sha256'].lower():
            raise ValueError('HD source checksum is not in the inventory')
        paths[name] = (path,source['sha256'].lower())
    metadata = {
        'catalog.json':json.dumps({'schema':1,'category':category,'edition':version,'works':works},ensure_ascii=False,separators=(',',':')).encode(),
        'desktop-index.relative.json':json.dumps({'schema':1,'sources':sources,'pages':pages},ensure_ascii=False,separators=(',',':')).encode(),
        'LICENSE':license_text,
        'THIRD_PARTY_NOTICES.md':notices,
        'README.md':'年度高清图库由全量图库和年度索引生成，包含对应普通展示图。\n'.encode(),
    }
    target = output/f'AI-bot-gallery-{category}-annual-hd-{version}.zip'
    prefix = f'AI-bot-gallery-{category}-annual-hd/'
    checks = {}
    with zipfile.ZipFile(target,'x',compression=zipfile.ZIP_STORED,allowZip64=True) as archive:
        for name,data in metadata.items():
            archive.writestr(prefix+name,data)
            checks[name] = hashlib.sha256(data).hexdigest()
        for name,(path,expected_hash) in list(paths.items())+[('standard.zip',(standard,digest(standard)))]:
            h = hashlib.sha256()
            with path.open('rb') as source,archive.open(prefix+name,'w',force_zip64=True) as dest:
                while block := source.read(1024*1024):
                    h.update(block)
                    dest.write(block)
            if h.hexdigest() != expected_hash:
                raise ValueError('HD source changed: '+name)
            checks[name] = expected_hash
        archive.writestr(prefix+'SHA256SUMS.txt',''.join(f'{h}  {name}\n' for name,h in checks.items()).encode())
    with zipfile.ZipFile(target) as archive:
        if set(archive.namelist()) != {prefix+name for name in checks} | {prefix+'SHA256SUMS.txt'}:
            raise ValueError('Annual HD archive inventory differs')
        for name,expected_hash in checks.items():
            with archive.open(prefix+name) as stream:
                if hashlib.file_digest(stream,'sha256').hexdigest() != expected_hash:
                    raise ValueError('Annual HD archive checksum differs: '+name)
    result = {'category':category,'works':len(works),'pages':len(pages),'sources':len(paths),'sha256':digest(target),'archive':target.name}
    (target.with_suffix('.zip.sha256')).write_text(result['sha256']+'  '+target.name+'\n',encoding='ascii')
    (output/'validation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False),flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--standard-root',type=Path,required=True)
    parser.add_argument('--desktop-root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--category',choices=('painting','calligraphy'),required=True)
    parser.add_argument('--version',required=True)
    args = parser.parse_args()
    build(args.standard_root,args.desktop_root,args.output,args.category,args.version)
