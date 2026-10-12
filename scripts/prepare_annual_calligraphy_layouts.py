"""Render the reviewed 62 annual additions without modifying archival pixels.

Portrait display is composed upright then rotated into the existing 1280x720
JPEG wire format. One album/scroll remains one catalog work.
"""
import argparse
import hashlib
import json
from pathlib import Path
from functools import lru_cache

from PIL import Image, ImageDraw, ImageFont, ImageOps
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / 'artifacts/development/annual-calligraphy-installable-20261012/new-layouts'
DEFAULT_MATERIALS = ROOT / 'artifacts/development/annual-art-materials-20261012-final/annual-calligraphy-materials.zh.json'
FONT = ROOT.parent / 'm5stack TAB5/.tools/font-builder/wenkai-1.522/LXGWWenKai-Regular.ttf'
Image.MAX_IMAGE_PIXELS = 500_000_000


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')


def additions(materials):
    rows = [w for w in read(materials)['works'] if w['id'].startswith(('expansion-', 'annual-'))]
    assert len(rows) == 62
    return rows


def contact(rows, output):
    output.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype(str(FONT), 15)
    for start in range(0, len(rows), 16):
        group = rows[start:start+16]
        sheet = Image.new('RGB', (1600, ((len(group)+3)//4)*280), '#eee')
        draw = ImageDraw.Draw(sheet)
        for j, work in enumerate(group):
            with Image.open(ROOT/work['nativeImages'][0]['path']) as image:
                thumb = ImageOps.contain(image.convert('RGB'), (392, 235))
            x, y = j%4*400, j//4*280
            sheet.paste(thumb, (x+(400-thumb.width)//2, y))
            draw.text((x+3,y+237), work['id'], font=font, fill='black')
            draw.text((x+3,y+256), work['title'], font=font, fill='black')
        sheet.save(output/f'source-first-{start//16}.jpg', quality=95)


def longest_run(mask):
    starts = np.where(np.diff(np.r_[False, mask, False].astype(int)) == 1)[0]
    ends = np.where(np.diff(np.r_[False, mask, False].astype(int)) == -1)[0]
    if not len(starts):
        return 0, len(mask)
    selected = int(np.argmax(ends-starts))
    return int(starts[selected]), int(ends[selected])


def paper_crop(image):
    """Crop only a positively identified photographic color-card edge.

    Blue AND green card patches must share the same edge row/column. Paper
    color and dark lettering are deliberately not used to infer bounds: large
    black glyphs can falsely divide a page. The four-pixel inset removes card
    edges without cutting any paper. Black background alone is retained.
    """
    small = image.resize((600, round(image.height*600/image.width)))
    rgb = np.asarray(small).astype(float)
    blue=(rgb[:,:,2]>rgb[:,:,0]*1.4)&(rgb[:,:,2]>rgb[:,:,1]*1.1)&(rgb[:,:,2]>85)
    green=(rgb[:,:,1]>rgb[:,:,0]*1.2)&(rgb[:,:,1]>rgb[:,:,2]*1.1)&(rgb[:,:,1]>85)
    card_rows=(blue.sum(axis=1)>=3)&(green.sum(axis=1)>=3)
    card_cols=(blue.sum(axis=0)>=3)&(green.sum(axis=0)>=3)
    # After a card establishes which edge is photographic, keep the complete
    # span of warm mounted paper on that axis, including disjoint ink rows.
    # Do not use this profile on any edge without positive card evidence.
    paper=(rgb[:,:,0]-rgb[:,:,2]>8)&(rgb[:,:,1]-rgb[:,:,2]>4)&(rgb[:,:,0]>60)
    paper_rows=np.where(paper.mean(axis=1)>.46)[0]
    paper_cols=np.where(paper.mean(axis=0)>.46)[0]
    h,w=small.height,small.width
    top,bottom,left,right=0,h,0,w
    found=np.where(card_rows[:int(h*.2)])[0]
    if len(found):top=min(int(found[-1])+3, max(0,int(paper_rows[0])-1)) if len(paper_rows) else int(found[-1])+3
    found=np.where(card_rows[int(h*.8):])[0]
    if len(found):bottom=min(int(found[0])+int(h*.8)-2, int(paper_rows[-1])+2) if len(paper_rows) else int(found[0])+int(h*.8)-2
    found=np.where(card_cols[:int(w*.15)])[0]
    if len(found):left=max(int(found[-1])+3, int(paper_cols[0])-1) if len(paper_cols) else int(found[-1])+3
    found=np.where(card_cols[int(w*.85):])[0]
    if len(found):right=min(int(found[0])+int(w*.85)-2,int(paper_cols[-1])+2) if len(paper_cols) else int(found[0])+int(w*.85)-2
    bounds=(max(0,round(left*image.width/w)),max(0,round(top*image.height/h)),
            min(image.width,round(right*image.width/w)),min(image.height,round(bottom*image.height/h)))
    return bounds


def crop_contacts(rows, output):
    entries=[]
    for work in rows:
        by_path={str(Path(p['path'])):p for p in work.get('imagefiles',[])}
        for native in work['nativeImages']:
            with Image.open(ROOT/native['path']) as opened:
                original_size=opened.size
                opened.draft('RGB',(800,800))
                image=ImageOps.exif_transpose(opened).convert('RGB')
            small_crop=paper_crop(image)
            crop=tuple(round(v*original_size[i%2]/image.size[i%2]) for i,v in enumerate(small_crop))
            if work['id']=='annual-npm-he-liuping':
                # Six upright panels share this reviewed camera rectangle.
                # The complete mounting and every ruled text cell remain.
                crop=(610,0,1650,original_size[1])
            elif work['id']=='annual-npm-wangduo-nigu':
                # Pastel card patches did not meet the strict detector; the
                # source photographs show the top card ends before y=300.
                crop=(0,300,original_size[0],original_size[1])
            page=by_path.get(str(Path(native['path'])),{}).get('pdfPage')
            entries.append({'id':work['id'],'path':native['path'],'sha256':native['sha256'],
                            'pdfPage':page,'originalSize':original_size,'crop':crop})
    write(output/'crop-review.json',entries)
    font=ImageFont.truetype(str(FONT),13)
    for start in range(0,len(entries),24):
        group=entries[start:start+24]
        sheet=Image.new('RGB',(1600,((len(group)+3)//4)*265),'#eee');draw=ImageDraw.Draw(sheet)
        for j,row in enumerate(group):
            with Image.open(ROOT/row['path']) as image:
                original_size=image.size
                image.draft('RGB',(800,800))
                bounds=tuple(round(v*image.size[k%2]/original_size[k%2]) for k,v in enumerate(row['crop']))
                cropped=image.crop(bounds).convert('RGB')
                thumb=ImageOps.contain(cropped,(392,223))
            x,y=j%4*400,j//4*265
            sheet.paste(thumb,(x+(400-thumb.width)//2,y))
            draw.text((x+3,y+226),row['id'],font=font,fill='black')
            draw.text((x+3,y+243),f"PDF{row['pdfPage']} {row['crop']}",font=font,fill='black')
        sheet.save(output/f'crop-contact-{start//24:02}.jpg',quality=95)
    return entries


@lru_cache(maxsize=64)
def font(size):
    return ImageFont.truetype(str(FONT),size)


def fit(draw,text,width,size):
    # Same readable caption treatment as prepare_daily_art.py.
    text=' '.join(text.split())
    while size>17 and draw.textlength(text,font=font(size))>width:
        size-=1
    while draw.textlength(text,font=font(size))>width:
        text=text[:-2]+'…'
    return text,font(size)


ALBUM_OPENINGS = {
    'expansion-npm-authority-su-family','expansion-npm-authority-su-three',
    'expansion-npm-authority-song-twelve','expansion-npm-authority-yuan-hanmo',
    'expansion-npm-authority-yuan-fashu','expansion-npm-authority-yu-jijiu',
    'expansion-npm-authority-zhaoyong-thousand','expansion-npm-ni-poems',
    'expansion-npm-song-four-album','expansion-npm-zhu-tao','expansion-npm-wen-qingjing',
}


def views(image,label,identity):
    result=[(image,label,None)]
    if identity in ALBUM_OPENINGS:
        # Retain the complete opening first. Upright single-leaf details then
        # follow right-to-left; a blank leaf is still visible on the overview.
        for side,left,right in [('右页',image.width//2,image.width),('左页',0,image.width//2)]:
            bounds=(left,0,right,image.height)
            leaf=image.crop(bounds)
            rgb=np.asarray(leaf.resize((150,round(leaf.height*150/leaf.width)))).astype(float)
            ink=(rgb.max(axis=2)<125).mean()
            if ink>.005:
                result.append((leaf,label+' · '+side,bounds))
    elif image.width>image.height*3:
        width=round(image.height*1104/558);step=round(width*.9)
        starts=list(range(max(0,image.width-width),-1,-step))
        if starts[-1]!=0:
            starts.append(0)
        result += [(image.crop((x,0,min(x+width,image.width),image.height)),
                    label+f' · 局部 {i+1}/{len(starts)}',
                    (x,0,min(x+width,image.width),image.height)) for i,x in enumerate(starts)]
    elif image.height>image.width*2.2:
        height=round(image.width*1.6);step=round(height*.9)
        starts=list(range(0,max(1,image.height-height),step))
        if starts[-1]!=image.height-height:
            starts.append(image.height-height)
        result += [(image.crop((0,y,image.width,y+height)),
                    label+f' · 局部 {i+1}/{len(starts)}',
                    (0,y,image.width,y+height)) for i,y in enumerate(starts)]
    return result


def render_frame(art,work,label,portrait):
    frame=Image.new('RGB',(720,1280) if portrait else (1280,720),'#e7e0d1')
    draw=ImageDraw.Draw(frame);ink='#332e27';muted='#776e60'
    vertical=art.width<art.height*1.15
    x,y,w,h=(16,14,688,1124) if portrait else (500,42,730,578) if vertical else (64,34,1152,584)
    rendered=ImageOps.contain(art,(w,h),Image.Resampling.LANCZOS)
    frame.paste(rendered,(x+(w-rendered.width)//2,y+(h-rendered.height)//2))
    if portrait:
        title,title_font=fit(draw,work['title'],672,27)
        author,author_font=fit(draw,work['author'],672,22)
        draw.text((24,1152),title,font=title_font,fill=ink)
        draw.text((24,1186),author,font=author_font,fill=muted)
    elif vertical:
        draw.rectangle((102,183,128,186),fill='#964b39')
        title,title_font=fit(draw,work['title'],350,40)
        author,author_font=fit(draw,work['author'],350,23)
        draw.text((100,216),title,font=title_font,fill=ink)
        draw.text((104,283),author,font=author_font,fill=muted)
        period,period_font=fit(draw,work.get('period',''),350,20)
        draw.text((104,321),period,font=period_font,fill=muted)
    else:
        # The label has its own footer row so long titles cannot collide with it.
        title,title_font=fit(draw,work['title'],720,27)
        title_width=draw.textlength(title,font=title_font)
        author,author_font=fit(draw,work['author'],min(300,1020-title_width),20)
        draw.text((64,635),title,font=title_font,fill=ink)
        draw.text((90+title_width,641),author,font=author_font,fill=muted)
    credit,credit_font=fit(draw,work['museum']+' · '+work['license'],672 if portrait else 850,16)
    draw.text((24 if portrait else 64,1246 if portrait else 684),credit,font=credit_font,fill=muted)
    detail,detail_font=fit(draw,label,672 if portrait else 280,17)
    draw.text((696,1218) if portrait else (1216,686),detail,font=detail_font,fill=muted,anchor='ra')
    return frame.transpose(Image.Transpose.ROTATE_90) if portrait else frame


def render(rows,output,materials,only=None):
    gallery=output/'gallery';gallery.mkdir(exist_ok=True)
    crop_rows=read(output/'crop-review.json')
    crops={r['path']:r for r in crop_rows}
    catalog=[];provenance=[];selection=[];files={};view_audit=[]
    previous={}
    if only:
        previous={name:{w['id']:w for w in read(gallery/name)} for name in ['catalog.json','provenance.json','selection.json']}
        old_catalog=previous['catalog.json']
        files={name:sha for name,sha in read(gallery/'files.json')['files'].items()
               if not any(name in old_catalog[identity]['frames']+old_catalog[identity]['portraitFrames'] for identity in only)}
        view_audit=[r for r in read(output/'view-audit.json') if r['id'] not in only]
    manifest={r['id']:r for r in read(ROOT/'docs/art-guides/curated-calligraphy-2026-10-12.json')['works']}
    for work in rows:
        assert work['workId']==manifest[work['id']]['workId']
        if only and work['id'] not in only:
            catalog.append(previous['catalog.json'][work['id']])
            provenance.append(previous['provenance.json'][work['id']])
            selection.append(previous['selection.json'][work['id']])
            continue
        full=[];origins=[]
        for i,native in enumerate(work['nativeImages']):
            row=crops[native['path']]
            path=ROOT/native['path']
            sha=hashlib.sha256(path.read_bytes()).hexdigest()
            assert sha==native['sha256']==row['sha256'],path
            with Image.open(path) as opened:
                image=ImageOps.exif_transpose(opened).convert('RGB')
            assert list(image.size)==list(row['originalSize'])
            crop=row['crop'];art=image.crop(crop)
            assert min(art.size)>=600,(work['id'],art.size)
            role='正文'
            if work['id']=='expansion-npm-wenfu' and row['pdfPage']==6:
                role='正文起首与沈度题签'
            elif work['id']=='expansion-npm-wen-qingjing' and row['pdfPage']==1:
                role='经文起首与钱榖补画'
            elif work['id']=='expansion-npm-wen-qingjing' and row['pdfPage']==3:
                role='经文末段与周天球后跋'
            elif work['id']=='expansion-npm-zhu-poems95' and row['pdfPage']==19:
                role='正文末段与后跋'
            label=f'{role} {i+1}/{len(work["nativeImages"])}'
            for view,label,detail_crop in views(art,label,work['id']):
                full.append((view,label,native['path'],detail_crop))
            origins.append({'file':native['path'],'imageSource':work.get('rights',{}).get('downloadUrl') or work['source'],
                'originalSize':image.size,'displaySourceSize':art.size,'crop':crop,
                'cropBasis':'positive blue+green color-card edge plus complete mounted-paper extent on that axis; reviewed contact sheets',
                'pdfPage':row['pdfPage'],'originalSha256':sha})
        assert 0<len(full)<=64,(work['id'],len(full))
        frames=[];portraits=[]
        for j,(art,label,path,detail_crop) in enumerate(full):
            for portrait,target in [(False,frames),(True,portraits)]:
                name=f'{work["id"]}-{j:02}{"-portrait" if portrait else ""}.jpg'
                frame=render_frame(art,work,label,portrait)
                frame.save(gallery/name,quality=96,subsampling=0,optimize=True)
                data=(gallery/name).read_bytes();assert len(data)<=1048576,name
                target.append(name);files[name]=hashlib.sha256(data).hexdigest()
            view_audit.append({'id':work['id'],'page':j,'source':path,'detailCropWithinPaper':detail_crop,
                               'label':label,'landscape':frames[-1],'portrait':portraits[-1]})
        catalog.append({k:work[k] for k in ['id','workId','category','title','author','museum','source','license']}|
                       {'frames':frames,'portraitFrames':portraits})
        provenance.append({k:work[k] for k in ['id','source','license','museum']}|origins[0]|
                          {'frames':len(frames),'additionalSources':origins[1:],
                           'displayLayout':'reviewed-native-page-with-overlapping-detail',
                           'displayOrientation':'portrait composed 720x1280 then ROTATE_90; encoded 1280x720',
                           'mainBodyPdfPages':work['imageReview']['recommendedMainBodyPdfPages'],
                           'overlapPolicy':'continuous photographs retain reviewed shared text; no inferred stitching or source-pixel repainting'})
        rights=work.get('rights',{})
        selection.append({k:work[k] for k in ['id','workId','category','title','author','museum','source','license']}|
                         {'file':origins[0]['file'],'imageSource':rights.get('downloadUrl') or work['source'],
                          'rightsSource':rights.get('source') or work['source'],
                          'credit':rights.get('creditLine') or work['museum'],'crop':origins[0]['crop'],
                          'additionalSources':origins[1:]})
        print('LAYOUT_WORK_OK',work['id'],len(frames),flush=True)
    write(gallery/'catalog.json',catalog);write(gallery/'selection.json',selection)
    write(gallery/'provenance.json',provenance)
    write(gallery/'files.json',{'count':len(files),'files':dict(sorted(files.items()))})
    write(output/'view-audit.json',view_audit)
    write(output/'layout-validation.json',{'works':len(rows),'nativePhotographs':len(crops),
        'landscapeFrames':sum(len(w['frames']) for w in catalog),'portraitFrames':sum(len(w['portraitFrames']) for w in catalog),
        'encodedSize':[1280,720],'portraitComposedSize':[720,1280],
        'archivalOriginalsByteUnchanged':True,'noDeviceDeployment':True,
        'sourceMaterialsSha256':hashlib.sha256(materials.read_bytes()).hexdigest()})
    rendered_contacts(catalog,output)


def rendered_contacts(catalog,output):
    gallery=output/'gallery'
    view_audit=read(output/'view-audit.json')
    for start in range(0,len(catalog),12):
        group=catalog[start:start+12]
        sheet=Image.new('RGB',(1600,((len(group)+3)//4)*405),'#eee');draw=ImageDraw.Draw(sheet)
        for j,work in enumerate(group):
            x,y=j%4*400,j//4*405
            with Image.open(gallery/work['frames'][0]) as image:
                sheet.paste(image.resize((392,221)),(x+4,y))
            with Image.open(gallery/work['portraitFrames'][0]) as image:
                image=image.transpose(Image.Transpose.ROTATE_270)
                sheet.paste(image.resize((85,151)),(x+4,y+228))
            draw.text((x+98,y+232),work['title'],font=font(16),fill='black')
            draw.text((x+98,y+260),work['author'],font=font(14),fill='black')
            draw.text((x+98,y+288),f'{len(work["frames"])} 页 / 双方向',font=font(14),fill='black')
            draw.text((x+98,y+316),work['id'][-32:],font=font(11),fill='black')
        sheet.save(output/f'layout-contact-{start//12:02}.jpg',quality=95)
    focus=['expansion-npm-wenfu','expansion-npm-song-four-album','expansion-npm-huizong',
           'expansion-npm-authority-yu-thousand','annual-npm-he-liuping',
           'expansion-npm-wen-qingjing','expansion-npm-shupu']
    for identity in focus:
        work=next(w for w in catalog if w['id']==identity)
        pages=[0,min(1,len(work['frames'])-1),len(work['frames'])-1]
        if identity=='expansion-npm-song-four-album':
            pages=[0]+[r['page'] for r in view_audit if r['id']==identity and
                       ('p017-' in r['source'] or 'p018-' in r['source']) and r['detailCropWithinPaper'] is None]
        for i in sorted(set(pages)):
            sheet=Image.new('RGB',(2000,1280),'#ddd')
            with Image.open(gallery/work['frames'][i]) as image:
                sheet.paste(image,(0,280))
            with Image.open(gallery/work['portraitFrames'][i]) as image:
                sheet.paste(image.transpose(Image.Transpose.ROTATE_270),(1280,0))
            sheet.save(output/f'focus-{identity}-{i:02}.jpg',quality=95)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--materials', type=Path, default=DEFAULT_MATERIALS)
    parser.add_argument('--output', type=Path, default=DEFAULT_OUTPUT)
    parser.add_argument('--contacts-only', action='store_true')
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--only', nargs='+', help='Rerender specified reviewed additions in an existing staged gallery')
    args = parser.parse_args()
    rows = additions(args.materials)
    if args.render:
        render(rows,args.output,args.materials,set(args.only) if args.only else None)
    else:
        contact(rows,args.output)
        crop_contacts(rows,args.output)
        print('SOURCE_CONTACTS', len(rows), sum(len(w['nativeImages']) for w in rows))


if __name__ == '__main__':
    main()
