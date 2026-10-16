"""Review, render and activate a source-verified museum collection expansion.

Source originals stay in the research cache. Activation preserves all prior
artwork bytes and metadata; complete gallery validation runs before activation.
"""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image, ImageDraw, ImageFont, ImageOps
from expand_art_library import ROOT, ASSETS, RESEARCH, OUT, read, write, sha, source_key
from verify_daily_art_year import verify

FONT = ROOT.parent/'m5stack TAB5/.tools/font-builder/wenkai-1.522/LXGWWenKai-Regular.ttf'


def curate():
    originals = {p['originalSha256']: w['id'] for w in read(ASSETS/'provenance.json')
                 for p in [w]+w.get('additionalSources', [])}
    used = {source_key(w['source']) for w in read(ASSETS/'catalog.json')}
    rows = []
    if (OUT/'downloads.json').exists():
        receipts = {r['id']:r for r in read(OUT/'downloads.json')}
        rows += [w for w in read(OUT/'candidates.json') if receipts[w['id']]['status']=='downloaded']
    else:
        receipts={p.stem:read(p) for p in (OUT/'download-receipts').glob('*.json')}
        rows += [w for w in read(OUT/'candidates.json') if receipts.get(w['id'],{}).get('status')=='downloaded']
    rows += read(OUT/'calligraphy-candidates.json')
    if (OUT/'chinese-downloads.json').exists():
        chinese_receipts={r['id']:r for r in read(OUT/'chinese-downloads.json')}
        rows += [w for w in read(OUT/'chinese-candidates.json') if all(
            chinese_receipts.get(w['id'] if p is w else Path(p['file']).stem,{}).get('status')=='downloaded'
            for p in [w]+w.get('additionalSources',[]))]
    decisions = {r['id']:r for r in read(OUT/'calligraphy-review.json')}
    paintings={r['id']:r for r in read(OUT/'painting-review.json')} if (OUT/'painting-review.json').exists() else {}
    groups=read(OUT/'reviewed-groups.json') if (OUT/'reviewed-groups.json').exists() else {}
    overrides=read(OUT/'chinese-source-overrides.json') if (OUT/'chinese-source-overrides.json').exists() else {}
    accepted, exclusions = [], []
    for w in rows:
        if w['id'] in overrides:w=dict(w,**overrides[w['id']])
        reason = None
        fingerprint = sha(RESEARCH/w['file'])
        if source_key(w['source']) in used: reason = 'duplicate_museum_work'
        elif fingerprint in originals: reason = 'duplicate_original:'+originals[fingerprint]
        elif w['category']=='calligraphy' and decisions[w['id']]['decision']!='accept':
            reason = decisions[w['id']]['reason']
        elif w['id'] in paintings and paintings[w['id']]['decision']!='accept':reason=paintings[w['id']]['reason']
        if w['id'] in groups and not reason:w=dict(w,**groups[w['id']])
        fingerprints={sha(RESEARCH/p['file']) for p in [w]+w.get('additionalSources',[])}
        if not reason and any(h in originals for h in fingerprints):reason='duplicate_original_component'
        if reason:
            exclusions.append({'id':w['id'],'reason':reason}); continue
        w = dict(w)
        for field in ('id','workId','title','author','museum','source','license','imageSource','file'):
            if not w.get(field):raise ValueError(f'Missing reviewed {field}: {w.get("id")}')
        w.pop('sourceReviewRequired',None)
        w['originalSha256'] = fingerprint
        accepted.append(w); used.add(source_key(w['source']))
        used.update(source_key(p['source']) for p in w.get('additionalSources',[]) if p.get('source'))
        originals.update({h:w['id'] for h in fingerprints})
    write(OUT/'approved-selection.json',accepted)
    write(OUT/'curation-exclusions.json',exclusions)
    print(json.dumps(dict(approved=dict(Counter(w['category'] for w in accepted)),excluded=len(exclusions))),flush=True)


def sheets():
    rows = [w for w in read(OUT/'approved-selection.json') if w['category']=='painting']
    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',13)
    pages=[]
    for start in range(0,len(rows),48):
        group=rows[start:start+48]
        sheet=Image.new('RGB',(1800,((len(group)+5)//6)*240),'#eee'); draw=ImageDraw.Draw(sheet)
        for i,w in enumerate(group):
            x,y=i%6*300,i//6*240
            with Image.open(RESEARCH/w['file']) as im:
                thumb=ImageOps.contain(ImageOps.exif_transpose(im).convert('RGB'),(290,205))
            sheet.paste(thumb,(x+(300-thumb.width)//2,y+3))
            draw.text((x+4,y+212),w['id'],font=font,fill='black')
            draw.text((x+4,y+229),w['title'][:38],font=font,fill='black')
        name=f'painting-review-{start//48:02}.jpg'; sheet.save(OUT/name,quality=93); pages.append(name)
    write(OUT/'painting-sheets.json',pages)
    print(f'SOURCE_CONTACT_SHEETS {len(pages)}',flush=True)


def batches(complete_batches_only=False):
    rows=read(OUT/'approved-selection.json'); tasks=[]
    for category in ('painting','calligraphy'):
        group=[w for w in rows if w['category']==category]
        for start in range(0,len(group),32):
            batch=group[start:start+32]
            if complete_batches_only and category=='painting' and len(batch)<32:continue
            folder=OUT/'render'/f'{category}-{start//32:03}'
            folder.mkdir(parents=True,exist_ok=True)
            selection=folder/'selection.json'
            if selection.exists() and read(selection)!=batch:
                prior=read(selection)
                if [{k:v for k,v in w.items() if k not in ('rightsSource','museumOriginalAuthor')} for w in prior] != [
                    {k:v for k,v in w.items() if k not in ('rightsSource','museumOriginalAuthor')} for w in batch]:
                    raise ValueError('Render selection changed; use a fresh batch path')
            write(selection,batch)
            tasks.append((selection,folder))
    return tasks


def render(complete_batches_only=False):
    tasks=batches(complete_batches_only)
    def one(task):
        selection,folder=task
        for portrait in (False,True):
            dest=folder/('portrait' if portrait else 'landscape')
            if (dest/'catalog.json').exists():
                records=read(dest/'catalog.json')
                if [w['id'] for w in records]!=[w['id'] for w in read(selection)]:
                    raise ValueError('Cached rendering identity mismatch')
                if not all((dest/name).is_file() for w in records for name in w['frames']):
                    raise ValueError('Cached rendering incomplete')
                continue
            cmd=[sys.executable,str(ROOT/'scripts/prepare_daily_art.py'),str(selection),
                 '--font',str(FONT),'--output',str(dest),'--source-root',str(RESEARCH)]
            if portrait:cmd.append('--portrait')
            result=subprocess.run(cmd,encoding='utf-8',capture_output=True)
            if result.returncode:raise RuntimeError(result.stdout+'\n'+result.stderr)
        return folder.name
    with ThreadPoolExecutor(max_workers=4) as pool:
        for i,name in enumerate(pool.map(one,tasks),1):print(f'RENDERED_BATCH {i}/{len(tasks)} {name}',flush=True)


def integrate():
    rows=read(OUT/'approved-selection.json')
    decisions=read(OUT/'painting-review.json') if any(w['category']=='painting' for w in rows) else []
    reviewed={r['id'] for r in decisions if r['decision']=='accept'}
    if not {w['id'] for w in rows if w['category']=='painting'} <= reviewed:
        raise ValueError('Every painting requires an explicit complete-source review')
    backup=OUT/'before-expansion';backup.mkdir(exist_ok=True)
    for name in ('catalog.json','selection.json','provenance.json','files.json','NOTICE.md'):
        if not (backup/name).exists():shutil.copyfile(ASSETS/name,backup/name)
    previous=read(backup/'catalog.json'); existing={w['id'] for w in previous}
    if any(w['id'] in existing for w in rows):raise ValueError('Expansion overlaps prior identities')
    prior_files=read(backup/'files.json')['files']
    if any(sha(ASSETS/name)!=fingerprint for name,fingerprint in prior_files.items()):
        raise ValueError('An existing frame changed')
    print(f'PRIOR_NATIVE_FRAMES_VERIFIED {len(prior_files)}',flush=True)
    stage=OUT/'staged-gallery';stage.mkdir(exist_ok=True)
    for name in prior_files:
        if not (stage/name).exists():shutil.copyfile(ASSETS/name,stage/name)
    new_catalog=[];new_provenance=[]
    for _,folder in batches():
        horizontal=read(folder/'landscape/catalog.json');vertical=read(folder/'portrait/catalog.json')
        origins=read(folder/'landscape/provenance.json')
        for h,v,p in zip(horizontal,vertical,origins,strict=True):
            if h['id']!=v['id'] or h['id']!=p['id'] or len(h['frames'])!=len(v['frames']):
                raise ValueError('Horizontal / portrait mismatch')
            h['portraitFrames']=v['frames'];p['portraitFrames']=len(v['frames'])
            new_catalog.append(h);new_provenance.append(p)
        for orientation,works in [('landscape',horizontal),('portrait',vertical)]:
            for w in works:
                for name in w['frames']:
                    if name in prior_files:raise ValueError('Cannot overwrite an old frame')
                    shutil.copyfile(folder/orientation/name,stage/name)
    combined=previous+new_catalog
    combined=[w for w in combined if w['category']=='painting']+sorted(
        [w for w in combined if w['category']=='calligraphy'],key=lambda w:len(w['frames']))
    identities=[w['id'] for w in combined]
    selected={w['id']:w for w in read(backup/'selection.json')+rows}
    origins={w['id']:w for w in read(backup/'provenance.json')+new_provenance}
    inventory={name:sha(stage/name) for w in combined for name in w['frames']+w['portraitFrames']}
    write(stage/'catalog.json',combined)
    write(stage/'selection.json',[selected[i] for i in identities])
    write(stage/'provenance.json',[origins[i] for i in identities])
    write(stage/'files.json',dict(schemaVersion=1,version=RESOURCE_VERSION,count=len(inventory),files=dict(sorted(inventory.items()))))
    print(f'STAGED_CATALOGUE {len(combined)} works, {len(inventory)} native frames',flush=True)
    counts=dict(Counter(w['category'] for w in combined))
    for category,count in counts.items():
        works=[w for w in combined if w['category']==category]
        files=[f for w in works for f in w['frames']+w['portraitFrames']]
        if count>2000 or len(files)>8000 or len(files)+8>8192 or sum((stage/f).stat().st_size for f in files)>2*1024**3:
            raise ValueError('Expanded pack exceeds current importer limits')
    result=verify(stage)
    write(OUT/'annual-validation.json',result)
    if not result['ready']:raise ValueError(result['errors'])
    print('ANNUAL_NATIVE_VALIDATION_OK',flush=True)
    if any(inventory[n]!=h for n,h in prior_files.items()):raise ValueError('Previous artwork changed')
    old_notice=(backup/'NOTICE.md').read_text('utf-8')
    notice=old_notice+'\n\n## 2026-10-08 local expansion / 本地扩充\n\n'
    notice+=f"Current source catalogue: {counts['painting']} paintings and {counts['calligraphy']} calligraphy works. Pages and orientations are not additional works.\n\n"
    notice+='New originals come from official museum services; per-work image URLs, licenses and hashes are retained in selection.json and provenance.json. Images are resized and laid out only. This local expansion has not been installed or verified on hardware. Prior hardware acceptance remains historical.\n\n'
    notice+='| Work ID | Museum source | Image license |\n| --- | --- | --- |\n'
    notice+=''.join(f"| {w['id']} | {w['source']} | {w['license']} |\n" for w in rows)
    (stage/'NOTICE.md').write_text(notice,'utf-8')
    for name in inventory.keys()-prior_files.keys():
        if (ASSETS/name).exists() and sha(ASSETS/name)!=inventory[name]:raise ValueError('Target frame collision')
        if not (ASSETS/name).exists():shutil.copyfile(stage/name,ASSETS/name)
    for name in ('catalog.json','selection.json','provenance.json','files.json','NOTICE.md'):
        shutil.copyfile(stage/name,ASSETS/name)
    write(RESEARCH/'selection-year.json',read(stage/'selection.json'))
    if any(sha(ASSETS/name)!=h for name,h in inventory.items()):raise ValueError('Activation hash mismatch')
    write(OUT/'integration.json',dict(status='local_catalogue_integrated',counts=counts,
        added=dict(Counter(w['category'] for w in rows)),nativeFrames=len(inventory),
        previousWorks=len(previous),previousFramesUnchanged=len(prior_files),
        catalogSha256=sha(ASSETS/'catalog.json'),hardwareVerified=False))
    print(json.dumps(read(OUT/'integration.json'),ensure_ascii=False),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage',choices=['curate','sheets','render','integrate'])
    parser.add_argument('--complete-batches-only',action='store_true',help='Render settled painting batches while supplementary sources finish')
    parser.add_argument('--workspace',type=Path,default=OUT,help='Research and review folder for this expansion')
    parser.add_argument('--version',default='2026.10.08',help='Local artwork resource version')
    args=parser.parse_args()
    OUT=args.workspace.resolve()
    RESOURCE_VERSION=args.version
    if args.stage=='render':render(args.complete_batches_only)
    else:globals()[args.stage]()
