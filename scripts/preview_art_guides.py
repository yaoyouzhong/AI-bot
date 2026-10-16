"""Build a local, offline art-guide preview from reviewed text and existing artwork.

This is a preview tool, not a gallery installer. Artwork is only resized for UI
layout; existing catalog order, assets, firmware and user settings are untouched.
Requires Pillow. See docs/art-guides/README.md for content and source policy.
"""
import argparse
import hashlib
import json
from functools import lru_cache
from pathlib import Path
from urllib.parse import urlparse

from PIL import Image, ImageDraw, ImageFont, ImageOps

Image.MAX_IMAGE_PIXELS = 500_000_000


@lru_cache(maxsize=32)
def get_font(path, size):
    return ImageFont.truetype(str(path), size)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def wrap(draw, text, font, width):
    lines = []
    for paragraph in text.split('\n'):
        line = ''
        for char in paragraph:
            if line and draw.textlength(line + char, font=font) > width:
                lines.append(line)
                line = ''
            line += char
        lines.append(line)
    return lines


def text_block(draw, text, xy, width, size, color, font_path, bottom):
    font = get_font(str(font_path), size)
    lines = wrap(draw, text, font, width)
    x, y = xy
    step = round(size * 1.48)
    if y + len(lines) * step > bottom:
        raise ValueError(f'Text exceeds layout: {text}')
    for line in lines:
        draw.text((x, y), line, font=font, fill=color)
        y += step
    return y


def render(work, art, portrait, font_path, dest):
    calligraphy = work['category'] == 'calligraphy'
    bg = '#e7e0d1' if calligraphy else '#111516'
    ink = '#332e27' if calligraphy else '#edf0e9'
    muted = '#625b50' if calligraphy else '#b5bdb9'
    image = Image.new('RGB', (720, 1280) if portrait else (1280, 720), bg)
    draw = ImageDraw.Draw(image)
    if portrait:
        box = (32, 24, 656, 890)
        x, y, width, bottom = 36, 936, 648, 1188
    else:
        box = (32, 30, 866, 616)
        x, y, width, bottom = 940, 74, 304, 560
        draw.line((922, 54, 922, 632), fill=muted, width=1)
    # Fit the whole block together; long catalogue titles/authors stay complete.
    sizes = None
    for candidate in [(34,23,26),(30,21,25),(28,20,24),(26,19,23),(24,18,22)]:
        height = 33 + sum(len(wrap(draw, work[key], get_font(str(font_path), size), width))
                          * round(size*1.48) for key,size in zip(['title','author','brief'],candidate))
        if y+height <= bottom:
            sizes = candidate
            break
    if sizes is None:
        # Longer official titles receive additional text space, while artwork
        # keeps its original proportions. No title or artist is truncated.
        sizes = (24,18,22)
        height = 33 + sum(len(wrap(draw,work[key],get_font(str(font_path),size),width))
                          * round(size*1.48) for key,size in zip(['title','author','brief'],sizes))
        if portrait and height <= 500:
            y = bottom-height
            box = (32,24,656,y-48)
        elif not portrait and height <= 600:
            y,bottom = 30,638
        else:
            raise ValueError(f'Whole text block does not fit: {work["id"]}')
    bx, by, bw, bh = box
    shown = ImageOps.contain(art, (bw, bh), Image.Resampling.LANCZOS)
    image.paste(shown, (bx + (bw-shown.width)//2, by + (bh-shown.height)//2))
    y = text_block(draw, work['title'], (x, y), width, sizes[0], ink, font_path, bottom)
    y = text_block(draw, work['author'], (x, y+8), width, sizes[1], muted, font_path, bottom)
    y = text_block(draw, work['brief'], (x, y+25), width, sizes[2], ink, font_path, bottom)
    # Museum attribution is always preserved; full sources are in the reader.
    text_block(draw, work['museum'], (36, 1210) if portrait else (34, 661),
               648 if portrait else 870, 19, muted, font_path, 1254 if portrait else 706)
    # Navigation belongs to the interactive UI, never the static artwork image.
    image.save(dest, compress_level=3)
    return image


def main():
    root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--samples', type=Path, default=root/'docs/art-guides/samples.zh.json')
    parser.add_argument('--gallery', type=Path, default=root/'windows-app/AIBotBridge/Assets/DailyArt')
    parser.add_argument('--source-root', type=Path, required=True, help='Parent of reviewed originals/ paths')
    parser.add_argument('--font', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--reuse-baseline', type=Path,
                        help='Verified previous preview: works, checks, layoutHashes and inputHashes')
    parser.add_argument('--previous-selection', type=Path,
                        help='Frozen selection used by that baseline')
    args = parser.parse_args()
    out = args.output.resolve()
    if out == args.gallery.resolve() or args.gallery.resolve() in out.parents:
        raise ValueError('Preview output must be outside the installed/source gallery')
    guide_bytes = args.samples.read_bytes()
    doc = json.loads(guide_bytes.decode('utf-8'))
    catalog = {w['id']: w for w in json.loads((args.gallery/'catalog.json').read_text('utf-8'))}
    selection = {w['id']: w for w in json.loads((args.gallery/'selection.json').read_text('utf-8'))}
    provenance = {w['id']: w for w in json.loads((args.gallery/'provenance.json').read_text('utf-8'))}
    watched = [args.gallery/n for n in ['catalog.json','selection.json','provenance.json','files.json']]
    before = {str(p): digest(p) for p in watched}
    out.mkdir(parents=True, exist_ok=True)
    (out/'images').mkdir(exist_ok=True)
    reuse, reuse_checks, reuse_hashes, previous_selection = {}, {}, {}, {}
    if args.reuse_baseline:
        if not args.previous_selection:
            raise ValueError('Reuse requires a frozen previous selection')
        baseline=json.loads(args.reuse_baseline.read_text('utf-8'))
        reuse={w['id']:w for w in baseline['works']}
        reuse_checks={w['id']:w for w in baseline['checks']}
        reuse_hashes=baseline['layoutHashes']
        previous_selection={w['id']:w for w in json.loads(args.previous_selection.read_text('utf-8'))}
    reused_layouts=0
    data, checks, seen = [], [], set()
    for work in doc['works']:
        c, s = catalog[work['id']], selection[work['id']]
        identity = (c['category'], c['workId'])
        if identity in seen:
            raise ValueError(f'Duplicate work identity: {identity}')
        seen.add(identity)
        for key in ['workId','title','author','category','museum']:
            if work[key] != c[key]:
                raise ValueError(f'Catalog mismatch: {work["id"]} {key}')
        limit = 75 if doc['edition'].startswith('full-') else 60
        if not 30 <= len(work['brief']) <= limit:
            raise ValueError(f'Brief length outside 30–{limit}: {work["id"]}')
        if not work['overview'] or not work['sources']:
            raise ValueError(f'Incomplete guide: {work["id"]}')
        for source in work['sources']:
            if urlparse(source['url']).scheme != 'https' or source['url'] != c['source']:
                raise ValueError(f'Unexpected source: {work["id"]}')
        if work.get('transcription', {}).get('status') == 'source_verified':
            if not work['transcription'].get('text') or work['transcription'].get('sourceId') != 'museum':
                raise ValueError(f'Transcription lacks evidence: {work["id"]}')
        original = (args.source_root / s['file']).resolve()
        if args.source_root.resolve() not in original.parents:
            raise ValueError('Original escapes source root')
        original_sha = digest(original)
        if original_sha != provenance[work['id']]['originalSha256']:
            raise ValueError(f'Original hash mismatch: {work["id"]}')
        old=reuse.get(work['id'])
        old_selection=previous_selection.get(work['id'])
        can_reuse=bool(old and old_selection and
                      all(old[k]==work[k] for k in ['title','author','brief','museum','category']) and
                      all(old_selection.get(k)==s.get(k) for k in ['file','crop','rotation']) and
                      reuse_checks[work['id']]['originalSha256']==original_sha)
        if not can_reuse:
            with Image.open(original) as opened:
                art = ImageOps.exif_transpose(opened).convert('RGB')
            if s.get('crop'):
                art = art.crop(tuple(s['crop']))
            if s.get('rotation') == 180:
                art = art.transpose(Image.Transpose.ROTATE_180)
        entry = dict(work)
        entry['previews'] = {}
        for portrait in [False, True]:
            orientation = 'portrait' if portrait else 'landscape'
            filename = f'{work["id"]}-{orientation}.png'
            target=out/'images'/filename
            if can_reuse:
                relative='images/'+filename
                if digest(target)!=reuse_hashes[relative]:
                    raise ValueError(f'Previous layout changed: {relative}')
                with Image.open(target) as im:
                    if im.size!=((720,1280) if portrait else (1280,720)):
                        raise ValueError('Previous layout size mismatch')
                reused_layouts+=1
            else:
                render(work, art, portrait, args.font, target)
            entry['previews'][orientation] = 'images/'+filename
        # Copy existing, already-curated pages byte for byte, including native
        # portrait encoding; the web reader rotates portrait frames upright.
        entry['existingFrames'] = {}
        for orientation, field in [('landscape','frames'), ('portrait','portraitFrames')]:
            frames = []
            for filename in c[field]:
                if Path(filename).name != filename:
                    raise ValueError('Unsafe gallery filename')
                source = args.gallery/filename
                payload = source.read_bytes()
                target = out/'images'/filename
                if source.resolve() == target.resolve():
                    raise ValueError('Cannot overwrite source frame')
                if not target.exists() or digest(target)!=hashlib.sha256(payload).hexdigest():
                    target.write_bytes(payload)
                if digest(source) != digest(target):
                    raise ValueError('Frame copy mismatch')
                before[str(source)] = digest(source)
                frames.append('images/'+filename)
            entry['existingFrames'][orientation] = frames
        entry['imageLicense'] = c['license']
        data.append(entry)
        checks.append({'id':work['id'],'workId':c['workId'],'originalSha256':original_sha,
                       'briefCharacters':len(work['brief']),'overviewCharacters':len(work['overview']),
                       'guideCharacters':len(work['overview'])+len(work['looking']),
                       'existingFrames':len(c['frames']),'layout':'both_fit_without_truncation'})
        if len(data)%50 == 0:
            print(f'Rendered {len(data)}/{len(doc["works"])} works',flush=True)
    counts = {category:sum(w['category']==category for w in data) for category in ['painting','calligraphy']}
    if doc['edition'].startswith('full-'):
        if {w['id'] for w in data} != set(catalog):
            raise ValueError('Full preview must match every current catalogue work')
    elif counts != {'painting':10,'calligraphy':10}:
        raise ValueError(f'Pilot requires ten works per category: {counts}')
    payload = json.dumps(data,ensure_ascii=False).replace('</','<\\/')
    template = (root/'docs/art-guides/preview.html').read_text('utf-8')
    template = template.replace('/* ART_GUIDES_COUNTS */',f'{counts["painting"]}件名画，{counts["calligraphy"]}件书法')
    (out/'index.html').write_text(template.replace('/* ART_GUIDES_DATA */',payload),'utf-8')
    (out/'samples.zh.json').write_bytes(guide_bytes)
    changed = [p for p,sha in before.items() if digest(Path(p)) != sha]
    if changed:
        raise ValueError(f'Gallery inputs changed during preview: {changed}')
    # Bounded sheets cover every composition, with complete IDs as index labels.
    sheets=[]
    for start in range(0,len(data),32):
        group=data[start:start+32]
        sheet=Image.new('RGB',(1280,((len(group)+3)//4)*235),'#f7f8f6')
        draw=ImageDraw.Draw(sheet)
        font=ImageFont.truetype(str(args.font),16)
        for index,work in enumerate(group):
            x,y=(index%4)*320,(index//4)*235
            with Image.open(out/work['previews']['landscape']) as opened:
                thumb=opened.resize((312,176),Image.Resampling.LANCZOS)
            sheet.paste(thumb,(x+4,y+4))
            draw.text((x+8,y+188),work['id'],font=font,fill='#24352f')
        filename='contact-sheet.png' if start==0 else f'contact-sheet-{start//32+1:02}.png'
        sheet.save(out/filename,compress_level=3)
        sheets.append(filename)
    report={'status':'preview_validated','edition':doc['edition'],'counts':counts,
            'guideDataSha256':hashlib.sha256(guide_bytes).hexdigest(),
            'uniqueWorks':len(seen),'generatedLayouts':len(data)*2,'contactSheets':sheets,
            'newlyRenderedLayouts':len(data)*2-reused_layouts,'reusedVerifiedLayouts':reused_layouts,
            'sourceGalleryUnchanged':True,'runtimeIntegrated':False,'hardwareVerified':False,
            'transcriptions':{status:sum(w.get('transcription',{}).get('status')==status for w in data if w['category']=='calligraphy')
                              for status in ['source_verified','not_available','withheld_pending_review']},
            'checks':checks,'inputHashes':before}
    (out/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n','utf-8')
    print(json.dumps({k:v for k,v in report.items() if k not in ['checks','inputHashes']},ensure_ascii=False))


if __name__ == '__main__':
    main()
