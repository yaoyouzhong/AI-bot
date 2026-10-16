"""Prepare a local caption repair for selected works; never modify installed packs.

Re-render from verified originals. Keep annual identity/order, frame names,
portrait JPEGs and all unselected works byte-for-byte unchanged.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image, ImageOps
from package_daily_art import package
from prepare_annual_calligraphy_layouts import render_frame, views, native_page_label, FONT
from verify_installable_calligraphy import verify


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def build(args):
    if args.output.exists() and not args.resume:
        raise ValueError('Choose a fresh candidate directory')
    catalog = read(args.baseline/'catalog.json')
    selections = read(args.baseline/'selection.json')
    origins = read(args.baseline/'provenance.json')
    guides = read(args.baseline/'guides.zh.json')
    hashes = read(args.baseline/'PACK.json')['files']
    selected = {w['id'] for w in catalog} if args.all else set(args.work_id or [])
    if any(w['category'] != 'calligraphy' for w in catalog):
        raise ValueError('Requires an independently installed calligraphy pack')
    if not selected or not selected <= {w['id'] for w in catalog}:
        raise ValueError('Unknown or empty work selection')
    for name, expected in hashes.items():
        if name != Path(name).name or sha(args.baseline/name) != expected:
            raise ValueError('Installed baseline integrity failed: '+name)
    source_rows = {w['id']: w for w in selections}
    provenance = {w['id']: w for w in origins}
    metadata = {w['workId']: w for w in guides['works']}
    overrides = read(args.metadata_overrides) if args.metadata_overrides else {}
    for guide in guides['works']:
        if guide['id'] in selected and guide['id'] in overrides:
            override = overrides[guide['id']]
            if override['workId'] != guide['workId'] or not override.get('source') or not override.get('snapshotSha256'):
                raise ValueError('Period correction requires identity and retained source evidence')
            guide['period'] = override['period']
            guide['periodBasis'] = override
    def source_path(name):
        for base in (args.source_root,args.legacy_source_root):
            if base is not None and (base/name).is_file():
                return (base/name).resolve()
        raise FileNotFoundError(name)
    # Resolve and authenticate every selected original before expensive rendering.
    for work in catalog:
        if work['id'] not in selected:continue
        row=source_rows[work['id']];origin=provenance[work['id']]
        guide=metadata[work['workId']]
        if any(not guide.get(key) for key in ('title','author','period','museum')):
            raise ValueError('Incomplete reviewed metadata: '+work['id'])
        for page,record in zip([row]+row.get('additionalSources',[]),[origin]+origin.get('additionalSources',[]),strict=True):
            if sha(source_path(page['file']))!=record['originalSha256']:
                raise ValueError('Original image changed: '+page['file'])
    args.output.mkdir(parents=True,exist_ok=True)
    stage = args.output/'gallery'; stage.mkdir(exist_ok=True)
    receipts=args.output/'receipts';receipts.mkdir(exist_ok=True)
    guide_path=args.output/'render-guides.json';write(guide_path,guides)
    plan=dict(baselineSha256=sha(args.baseline/'PACK.json'),selected=sorted(selected),guidesSha256=sha(guide_path),version=args.version)
    if (args.output/'plan.json').exists() and read(args.output/'plan.json')!=plan:
        raise ValueError('Resume plan differs from pinned baseline/metadata')
    write(args.output/'plan.json',plan)
    changed = {name for w in catalog if w['id'] in selected for name in w['frames']}
    image_hashes = {name: value for name, value in hashes.items() if name.endswith('.jpg')}
    for name in image_hashes:
        if name not in changed:
            if not (stage/name).exists():shutil.copyfile(args.baseline/name, stage/name)
    proof = []
    for work in catalog:
        if work['id'] not in selected:
            continue
        guide = metadata[work['workId']]
        if guide['id'] != work['id'] or any(not guide.get(key) for key in ('title','author','period','museum')):
            raise ValueError('Incomplete or mismatched reviewed metadata: '+work['id'])
        row = source_rows[work['id']]; origin = provenance[work['id']]
        receipt_path=receipts/(work['id']+'.json')
        if receipt_path.exists():
            receipt=read(receipt_path)
            if any(not(stage/name).is_file() or sha(stage/name)!=expected for name,expected in receipt['files'].items()):
                raise ValueError('Previously rendered page changed: '+work['id'])
        elif origin.get('sourceLayout',origin.get('displayLayout'))!='reviewed-native-page-with-overlapping-detail':
            # The original general renderer owns album/couplet grouping and
            # scroll detail order. Do not approximate that selection here.
            render_row=json.loads(json.dumps(row))
            for page in [render_row]+render_row.get('additionalSources',[]):
                page['file']=str(source_path(page['file']))
            selection_path=args.output/'render-selection.json';write(selection_path,[render_row])
            rendered_dir=args.output/'rendered'/work['id']
            with (args.output/'render.log').open('a',encoding='utf-8') as log:
                subprocess.run([sys.executable,str(Path(__file__).with_name('prepare_daily_art.py')),str(selection_path),
                                '--font',str(FONT),'--guides',str(guide_path),'--output',str(rendered_dir)],check=True,stdout=log,stderr=log)
            result=read(rendered_dir/'catalog.json')[0]
            if result['workId']!=work['workId'] or result['frames']!=work['frames']:
                raise ValueError('General renderer changed page sequence: '+work['id'])
            for name in work['frames']:shutil.copyfile(rendered_dir/name,stage/name)
        else:
            repair_native(work,row,origin,guide,source_path,stage)
        if origin.get('displayLayout')=='reviewed-native-page-with-overlapping-detail':
            origin['sourceLayout']=origin['displayLayout']
        origin['displayLayout'] = 'calligraphy-information-panel'
        origin['displayMetadata'] = {key: guide[key] for key in ('title','author','period','museum')}
        proof.append(dict(id=work['id'],workId=work['workId'],frames=work['frames'],metadata=origin['displayMetadata']))
        write(receipt_path,dict(files={name:sha(stage/name) for name in work['frames']}))
        print('CAPTION_WORK_OK',len(proof),len(selected),work['id'],flush=True)
    for name, value in [('catalog.json',catalog),('selection.json',selections),('provenance.json',origins),('guides.zh.json',guides)]:
        write(stage/name, value)
    inventory = {name: sha(stage/name) for name in image_hashes}
    if any(inventory[name] != expected for name,expected in image_hashes.items() if name not in changed):
        raise ValueError('An unselected/portrait image changed')
    write(stage/'files.json',dict(count=len(inventory),files=inventory))
    report = verify(stage,args.manifest); write(args.output/'annual-validation.json',report)
    package(stage,args.output/'packs',args.version,guides_path=stage/'guides.zh.json',category='calligraphy')
    write(args.output/'validation.json',dict(repaired=proof,changedLandscapePages=len(changed),
          retainedImages=len(inventory)-len(changed),annualOrderAndFramesUnchanged=True,
          verifiedOriginals=True,portraitAndOtherWorksUnchanged=True,installed=False,hardwareVerified=False))
    print('CALLIGRAPHY_CAPTION_REPAIR_OK pages='+str(len(changed)),flush=True)


def repair_native(work,row,origin,guide,source_path,stage):
    pages = [row]+row.get('additionalSources', [])
    records = [origin]+origin.get('additionalSources', [])
    rendered = []
    for index, (page, record) in enumerate(zip(pages, records, strict=True)):
        source = source_path(page['file'])
        if sha(source) != record['originalSha256']:
            raise ValueError('Original image changed: '+page['file'])
        with Image.open(source) as opened:
            art = ImageOps.exif_transpose(opened).convert('RGB')
        if list(art.size) != record['originalSize']:
            raise ValueError('Original dimensions changed')
        if page.get('crop'):
            art = art.crop(tuple(page['crop']))
        if page.get('rotation', row.get('rotation', 0)):
            raise ValueError('Use the original renderer for rotated source selections')
        label = page.get('detail') or native_page_label(work['id'],record.get('pdfPage'),index,len(pages))
        rendered.extend(views(art, label, work['id']))
    if len(rendered) != len(work['frames']):
        raise ValueError('Repair would change page sequence: '+work['id'])
    for name, (art, label, _) in zip(work['frames'], rendered, strict=True):
        render_frame(art, {**work,**guide}, label, False).save(stage/name, quality=96, subsampling=0, optimize=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('baseline','source-root','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    selection=parser.add_mutually_exclusive_group(required=True)
    selection.add_argument('--work-id',action='append')
    selection.add_argument('--all',action='store_true')
    parser.add_argument('--legacy-source-root',type=Path)
    parser.add_argument('--metadata-overrides',type=Path)
    parser.add_argument('--manifest',type=Path,default=Path(__file__).resolve().parents[1]/'docs/art-guides/curated-calligraphy-2026-10-12.json')
    parser.add_argument('--resume',action='store_true')
    parser.add_argument('--version',required=True)
    build(parser.parse_args())
