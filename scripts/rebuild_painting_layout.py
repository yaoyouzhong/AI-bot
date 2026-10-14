"""Prepare a reversible gallery candidate with landscape painting information.

Preserves selected works, order, original reproductions, page count, portrait
JPEGs and every calligraphy JPEG. Never installs into the user's gallery.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

from package_daily_art import package
from verify_daily_art_year import verify


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def build(args):
    if args.output.exists():
        raise ValueError('Choose a fresh candidate directory; preserve previous assets')
    catalog=read(args.baseline/'catalog.json')
    selections=read(args.baseline/'selection.json')
    provenance=read(args.baseline/'provenance.json')
    if (args.baseline/'files.json').exists():
        old_hashes=read(args.baseline/'files.json')['files']
    else:
        # Installed category packs carry hashes in PACK.json rather than the
        # development gallery's combined files.json. Validate all declared
        # inputs before using its selected works and reviewed metadata.
        installed=read(args.baseline/'PACK.json')
        if not args.painting_only or installed['category']!='painting':
            raise ValueError('Installed baseline requires --painting-only')
        for name,expected in installed['files'].items():
            if name!=Path(name).name or sha(args.baseline/name)!=expected:
                raise ValueError('Installed pack integrity failed: '+name)
        old_hashes={name:value for name,value in installed['files'].items() if name.endswith('.jpg')}
    if args.painting_only and any(w['category']!='painting' for w in catalog):
        raise ValueError('--painting-only requires a painting-only baseline')
    paintings=[w for w in catalog if w['category']=='painting']
    selected={w['id']:w for w in selections}
    originals={w['id']:w for w in provenance}
    rows=[selected[w['id']] for w in paintings]
    # Reject a stale or changed source reproduction before rendering any pages.
    for row in rows:
        origin=originals[row['id']]
        if sha(args.source_root/row['file'])!=origin['originalSha256']:
            raise ValueError('Original image changed: '+row['id'])
        for page,record in zip(row.get('additionalSources',[]),origin.get('additionalSources',[]),strict=True):
            if sha(args.source_root/page['file'])!=record['originalSha256']:
                raise ValueError('Original album page changed: '+row['id'])
    args.output.mkdir(parents=True)
    write(args.output/'painting-selection.json',rows)
    rendered=args.output/'rendered'
    subprocess.run([sys.executable,str(Path(__file__).with_name('prepare_daily_art.py')),
                    str(args.output/'painting-selection.json'),'--source-root',str(args.source_root),
                    '--font',str(args.font),'--output',str(rendered),'--painting-info','--guides',str(args.guides)],check=True)
    updated=read(rendered/'catalog.json')
    for old,new in zip(paintings,updated,strict=True):
        if any(old[k]!=new[k] for k in ('id','category','title','author','source','license','workId','frames')):
            raise ValueError('Painting identity, order or page sequence changed: '+old['id'])
    stage=args.output/'gallery';stage.mkdir()
    changed={name for w in paintings for name in w['frames']}
    for name,expected in old_hashes.items():
        if name!=Path(name).name or sha(args.baseline/name)!=expected:
            raise ValueError('Baseline image integrity failed: '+name)
        shutil.copyfile((rendered if name in changed else args.baseline)/name,stage/name)
    new_origins={w['id']:w for w in read(rendered/'provenance.json')}
    for old in provenance:
        new=new_origins.get(old['id'])
        if new:
            if old['originalSha256']!=new['originalSha256'] or old['frames']!=new['frames']:
                raise ValueError('Original or page count changed: '+old['id'])
            old['displayLayout']=new['displayLayout'];old['displayMetadata']=new['displayMetadata']
    write(stage/'catalog.json',catalog)
    write(stage/'selection.json',selections)
    write(stage/'provenance.json',provenance)
    inventory={name:sha(stage/name) for name in old_hashes}
    write(stage/'files.json',{'count':len(inventory),'files':inventory})
    report=verify(stage,'painting' if args.painting_only else None);write(args.output/'annual-validation.json',report)
    if not report['ready']:raise ValueError(report['errors'])
    retained=set(old_hashes)-changed
    if any(inventory[name]!=old_hashes[name] for name in retained):
        raise ValueError('Portrait or calligraphy bytes changed')
    package(stage,args.output/'packs',args.version,guides_path=args.guides,
            category='painting' if args.painting_only else None)
    result={'paintings':len(paintings),'landscapePages':len(changed),
            'retainedImages':len(retained),'portraitAndCalligraphyUnchanged':True,
            'originalsVerified':True,'annualOrderAndPagesUnchanged':True,
            'guideSha256':sha(args.guides),'hardwareVerified':False,'installed':False}
    write(args.output/'validation.json',result)
    print(json.dumps(result,ensure_ascii=False),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('baseline','source-root','font','guides','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    parser.add_argument('--version',required=True)
    parser.add_argument('--painting-only',action='store_true',
                        help='Rebuild the installed painting category independently; leave calligraphy installed unchanged')
    build(parser.parse_args())
