"""Render curated museum images as complete 1280x720 gallery frames.

This performs layout, resampling and JPEG encoding only. It never repaints artwork.
Requires Pillow. Source files and provenance are supplied by a reviewed selection.json.
"""
import argparse, hashlib, json, math
from functools import lru_cache
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

# Reviewed museum handscroll TIFFs can be very wide. This bound admits the
# complete official image rather than substituting a cropped web photograph.
Image.MAX_IMAGE_PIXELS = 500_000_000

def main():
    parser=argparse.ArgumentParser();parser.add_argument('selection',type=Path);parser.add_argument('--font',type=Path,required=True);parser.add_argument('--output',type=Path,required=True);parser.add_argument('--source-root',type=Path)
    parser.add_argument('--portrait',action='store_true',help='Add upright artwork layout, encoded as a rotated native-size frame')
    args=parser.parse_args();items=json.loads(args.selection.read_text(encoding='utf-8'));args.output.mkdir(parents=True,exist_ok=True)
    @lru_cache(maxsize=48)
    def font(size):return ImageFont.truetype(str(args.font),size)
    def fit(draw,text,width,size):
        text=' '.join(text.split())
        while size>17 and draw.textlength(text,font=font(size))>width:size-=1
        while draw.textlength(text,font=font(size))>width:text=text[:-2]+'…'
        return text,font(size)
    catalog=[];audit=[]
    identities=set()
    for work in items:
        identity=(work['category'],work.get('workId',work['source']))
        if identity in identities:raise ValueError(f'Duplicate work: {identity}')
        identities.add(identity)
        source=(args.source_root or args.selection.parent)/work['file']
        with Image.open(source) as opened:image=ImageOps.exif_transpose(opened).convert('RGB')
        original_size=image.size
        if 'crop' in work:image=image.crop(tuple(work['crop']))
        rotation=work.get('rotation',0)
        if rotation not in (0,180):raise ValueError(f"Unsupported rotation: {work['id']} {rotation}")
        # Apply reviewed orientation after the crop, whose coordinates refer
        # to the original photograph. Keep original source bytes unchanged.
        if rotation==180:image=image.transpose(Image.Transpose.ROTATE_180)
        calligraphy=work['category']=='calligraphy'
        if min(image.size)<600:raise ValueError(f"Insufficient detail: {work['id']} {image.size}")
        # Handscroll: show the whole work, then readable overlapping sections,
        # in the traditional right-to-left reading order. Never silently crop.
        def source_views(art,label='',detail_crop=None):
            result=[(art,label)]
            def detail(text):return f'{label} · {text}' if label else text
            if detail_crop:
                left,top,right,bottom=detail_crop
                if not (0<=left<right<=art.width and 0<=top<bottom<=art.height):
                    raise ValueError('Reviewed detail bounds exceed the source')
                # Keep the complete mounted work in its overview. Detail pages
                # may omit a reviewed empty roller/mounting margin.
                art=art.crop(tuple(detail_crop))
            if (calligraphy or work.get('detailViews')) and art.width>art.height*3:
                result[0]=(art,detail('全卷'))
                width=round(art.height*1104/558);step=round(width*.9)
                starts=list(range(max(0,art.width-width),-1,-step))
                if starts[-1]!=0:starts.append(0)
                result += [(art.crop((x,0,min(x+width,art.width),art.height)),detail(f'局部 {i+1}/{len(starts)}')) for i,x in enumerate(starts)]
            elif calligraphy and art.height>art.width*2.2:
                result[0]=(art,detail('全幅'))
                height=round(art.width*1.6);step=round(height*.9)
                starts=list(range(0,max(1,art.height-height),step))
                if starts[-1]!=art.height-height:starts.append(art.height-height)
                result += [(art.crop((0,y,art.width,y+height)),detail(f'局部 {i+1}/{len(starts)}')) for i,y in enumerate(starts)]
            return result
        views=source_views(image,work.get('detail',''),work.get('detailCrop'))
        # A complete album is one work. Its other original leaves remain
        # frames of that work, with their own source and integrity evidence.
        additional_sources=[]
        panels=[image]
        for page in work.get('additionalSources',[]):
            page_source=(args.source_root or args.selection.parent)/page['file']
            with Image.open(page_source) as opened:page_image=ImageOps.exif_transpose(opened).convert('RGB')
            page_size=page_image.size
            if 'crop' in page:page_image=page_image.crop(tuple(page['crop']))
            if min(page_image.size)<600:raise ValueError(f"Insufficient page detail: {work['id']} {page_image.size}")
            # Both halves of a couplet need readable detail views; neither
            # half becomes a separate work or is left as a tiny overview.
            panels.append(page_image)
            if work.get('layout')!='paired-panels':
                views.extend(source_views(page_image,page['detail']))
            additional_sources.append({'file':page['file'],'imageSource':page['imageSource'],
                'originalSize':page_size,'displaySourceSize':page_image.size,'crop':page.get('crop'),
                'originalSha256':hashlib.sha256(page_source.read_bytes()).hexdigest()})
        if work.get('layout')=='paired-panels':
            # Complete couplets / panel sets, two panels per display page;
            # a triptych stays together on its overview page.
            # Selection order is reading order (right panel, then left panel).
            # Downsample to a shared height, without stretching or upscaling.
            if not calligraphy or len(panels) not in (2,3,4,6,8,10,12):
                raise ValueError('Panel layout requires a triptych or two to twelve paired panels')
            views=[]
            group_size=3 if len(panels)==3 else 2
            for start in range(0,len(panels),group_size):
                group=panels[start:start+group_size]
                height=min(p.height for p in group)
                group=[p.resize((round(p.width*height/p.height),height),Image.Resampling.LANCZOS) for p in group]
                gap=round(height*.025)
                pair=Image.new('RGB',(sum(p.width for p in group)+gap*(len(group)-1),height),'#e7e0d1')
                x=0
                for panel in reversed(group):
                    pair.paste(panel,(x,0));x+=panel.width+gap
                label='上下联' if len(panels)==2 else f'第{start+1}–{start+len(group)}屏 / 共{len(panels)}屏'
                views.extend(source_views(pair,label))
        if len(views)>64:raise ValueError('Too many frames')
        frames=[]
        for page,(art,detail) in enumerate(views):
            bg='#e7e0d1' if calligraphy else '#111516';ink='#332e27' if calligraphy else '#edf0e9';muted='#776e60' if calligraphy else '#929a96'
            frame=Image.new('RGB',(720,1280) if args.portrait else (1280,720),bg);draw=ImageDraw.Draw(frame)
            portrait=calligraphy and art.width<art.height*1.15
            box=(16,14,688,1124) if args.portrait else (500,42,730,578) if portrait else (64,34,1152,584)
            x,y,w,h=box;rendered=ImageOps.contain(art,(w,h),Image.Resampling.LANCZOS)
            frame.paste(rendered,(x+(w-rendered.width)//2,y+(h-rendered.height)//2))
            if args.portrait:
                title,title_font=fit(draw,work['title'],672,27)
                author,author_font=fit(draw,work['author'],672,22)
                draw.text((24,1152),title,font=title_font,fill=ink)
                draw.text((24,1186),author,font=author_font,fill=muted)
            elif portrait:
                draw.rectangle((102,183,128,186),fill='#964b39')
                title,title_font=fit(draw,work['title'],350,40)
                author,author_font=fit(draw,work['author'],350,23)
                draw.text((100,216),title,font=title_font,fill=ink)
                draw.text((104,283),author,font=author_font,fill=muted)
                draw.text((104,321),work.get('period',''),font=font(20),fill=muted)
            else:
                title,title_font=fit(draw,work['title'],720,27)
                title_width=draw.textlength(title,font=title_font)
                author,author_font=fit(draw,work['author'],min(300,1020-title_width),20)
                draw.text((64,635),title,font=title_font,fill=ink)
                draw.text((90+title_width,641),author,font=author_font,fill=muted)
            notes=work.get('chineseNotes',{}) if not calligraphy else {}
            chinese=[]
            for field,label in [('title','作品'),('author','作者')]:
                note=notes.get(field,{})
                if note.get('status')=='verified_published_usage' and note.get('value') and note.get('sources'):
                    chinese.append(f"{label}：{note['value']}")
            credit_y=1246 if args.portrait else 674
            if chinese:
                caption,caption_font=fit(draw,' · '.join(chinese),672 if args.portrait else 1152,20)
                draw.text((24,1217) if args.portrait else (64,667),caption,font=caption_font,fill=ink)
                if not args.portrait:credit_y=696
            credit,credit_font=fit(draw,work.get('credit',''),672 if args.portrait else 1152,16)
            draw.text((24 if args.portrait else 64,credit_y),credit,font=credit_font,fill=muted)
            if detail:draw.text((696,1218) if args.portrait else (1120,641),detail,font=font(17),fill=muted,anchor='ra')
            if args.portrait:frame=frame.transpose(Image.Transpose.ROTATE_90)
            filename=f"{work['id']}-{page:02}{'-portrait' if args.portrait else ''}.jpg";frame.save(args.output/filename,quality=96,subsampling=0,optimize=True)
            if (args.output/filename).stat().st_size>1048576:raise ValueError('Frame exceeds transport limit')
            frames.append(filename)
        catalog.append({k:work[k] for k in ['id','category','title','author','source','license']}|{'workId':identity[1],'frames':frames})
        audit.append({k:work[k] for k in ['id','source','license']}|{'originalSize':original_size,'displaySourceSize':image.size,'crop':work.get('crop'),'imageSource':work.get('imageSource',work['source']),'originalSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'frames':len(frames)})
        if work.get('museum'):
            catalog[-1]['museum']=work['museum']
            audit[-1]['museum']=work['museum']
        if work.get('chineseNotes'):
            catalog[-1]['chineseNotes']=work['chineseNotes']
            audit[-1]['chineseNotes']=work['chineseNotes']
        if additional_sources:audit[-1]['additionalSources']=additional_sources
        if work.get('layout'):audit[-1]['layout']=work['layout']
        if work.get('detailCrop'):audit[-1]['detailCrop']=work['detailCrop']
        if rotation:audit[-1]['rotation']=rotation
    (args.output/'catalog.json').write_text(json.dumps(catalog,ensure_ascii=False,indent=2),encoding='utf-8')
    (args.output/'provenance.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
    print(f'GALLERY_RENDER_OK {len(catalog)} works, {sum(len(w["frames"]) for w in catalog)} frames')

if __name__=='__main__':main()
