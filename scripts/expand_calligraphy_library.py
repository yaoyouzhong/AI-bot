"""Collect additional official calligraphy photographs for explicit visual review.

This collector does not activate works. Sets, albums and scroll photographs need
an explicit selection in calligraphy-review.json before native rendering.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import json
from pathlib import Path
import re

from PIL import Image, ImageDraw, ImageFont, ImageOps
import requests

from expand_art_library import ROOT, ASSETS, RESEARCH, read, write, sha, source_key, HEADERS, creator_label

OUT = ROOT/'artifacts/development/calligraphy-expansion-2026-10-08'


def discover():
    OUT.mkdir(parents=True,exist_ok=True)
    used={source_key(p['source']) for w in read(ASSETS/'selection.json')
          for p in [w]+w.get('additionalSources',[]) if p.get('source')}
    listing=read(RESEARCH/'colbase/all-chinese-calligraphy.json')
    script=(RESEARCH/'colbase/a49992526a10b5e9fe44.js').read_text('utf-8')
    public_key=re.search(r'"x-api-key":"([^"]+)"',script).group(1)
    def metadata(row):
        key=row['organization_item_key'];org=row['organization_path_name']
        path=RESEARCH/'colbase'/f'{key}.json'
        if not path.exists():
            response=requests.get(f'https://colbase.nich.go.jp/colbaseapi/v2/collection_items/{org}/{key}',
                params={'locale':'zh'},headers={'x-api-key':public_key},timeout=(15,45))
            response.raise_for_status();write(path,response.json())
        return read(path)
    wanted=[r for r in listing if r.get('seisakuchi')=='中国' and
            source_key(f"https://colbase.nich.go.jp/collection_items/{r['organization_path_name']}/{r['organization_item_key']}") not in used]
    records=[];errors=[]
    def safely(row):
        try:return metadata(row)
        except Exception as exc:errors.append({'key':row['organization_item_key'],'error':str(exc)});return None
    with ThreadPoolExecutor(max_workers=3) as pool:
        records=[r for r in pool.map(safely,wanted) if r]
    works=[]
    for r in records:
        key=r['organization_item_key'];org=r['organization_path_name'];title=' '.join(r['title'].split())
        if not r.get('image_files') or any(s in title for s in ['砚','印章','书画鉴定','目录']):continue
        if len(r['image_files'])>50:continue  # Large bound volumes need a separate, complete review.
        works.append(dict(id=f'colbase-{org}-{key}',workId=f'colbase:{org}:{key}',category='calligraphy',
            title=title,author=r.get('sakusha') or '佚名',museum='东京国立博物馆',
            source=f'https://colbase.nich.go.jp/collection_items/{org}/{key}?locale=zh',
            license='CC BY 4.0',rightsSource='https://colbase.nich.go.jp/pages/term?locale=zh',
            credit='ColBase · 东京国立博物馆 · CC BY 4.0 · 经排版加工',
            period=r.get('jidai_seiki') or '',accession=key,museumAuthor=r.get('sakusha'),
            photographs=[dict(index=i,imageSource=p['url'],file=f'originals/colbase-{org}-{key}-{i}.jpg',main=p.get('main',False))
                         for i,p in enumerate(r['image_files'])]))
    for path in (RESEARCH/'met-records').glob('*.json'):
        r=read(path)
        if r.get('classification')!='Calligraphy' or not r.get('isPublicDomain') or not r.get('primaryImage') or source_key(r['objectURL']) in used:continue
        # Bound albums and incomplete fragments are held back; complete mounted works are reviewed below.
        if any(s in r['title'].lower() for s in ['fragment','album','poets henj','accompanying an over robe']):continue
        if len(r.get('additionalImages',[]))>25:continue
        key=str(r['objectID'])
        works.append(dict(id='met-'+key,workId='met:'+key,category='calligraphy',title=r['title'],
            author=r.get('artistDisplayName') or 'Unidentified artist',museum='大都会艺术博物馆',
            source=r['objectURL'],license='CC0',rightsSource='https://www.metmuseum.org/hubs/open-access',
            credit='The Metropolitan Museum of Art · CC0',period=r.get('objectDate') or '',accession=r['accessionNumber'],
            photographs=[dict(index=i,imageSource=url,file=f'originals/met-{key}-calligraphy-expansion-{i}.jpg',main=i==0)
                         for i,url in enumerate([r['primaryImage']]+r.get('additionalImages',[]))]))
    for path in (RESEARCH/'princeton-records').glob('*.json'):
        r=read(path);key=str(r['objectid']);source='https://artmuseum.princeton.edu/art/collections/objects/'+key
        if r.get('classification')!='Calligraphy' or r.get('restrictions') or r.get('displayculture')!='Chinese' or source_key(source) in used:continue
        # Individual components of collective albums do not become new independent works.
        if r['displaytitle']=='Letter' or 'Flowers and Calligraphy' in r['displaytitle'] or int(key) in [61899,61900,61901,61902,62058,62059,62065,62067]:continue
        if any(s in r.get('medium','').lower() for s in ['album of 56','seventy-three','album of ten leaves']):continue
        photos=[p for p in r.get('media',[]) if p.get('uri') and not p.get('restrictions')]
        if not photos:continue
        works.append(dict(id='princeton-'+key,workId='princeton:'+key,category='calligraphy',title=r['displaytitle'],
            author=r.get('displaymaker') or 'Unidentified artist',museum='普林斯顿大学艺术博物馆',
            source=source,license='Public domain',rightsSource='https://artmuseum.princeton.edu/art/our-collections/image-use-and-access',
            credit='Image courtesy of the Princeton University Art Museum',period=r.get('displaydate') or '',accession=r['objectnumber'],
            photographs=[dict(index=i,imageSource=p['uri']+'/full/max/0/default.jpg',
                file=f'originals/princeton-{key}-calligraphy-expansion-{i}.jpg',main=bool(p.get('isprimary')))
                for i,p in enumerate(photos)]))
    cma=read(OUT/'cma-calligraphy-api.json')['data']
    for r in cma:
        if not any(s in str(r.get('culture')) for s in ['China','Japan','Korea']) or source_key(r['url']) in used:continue
        if r.get('record_type') in ['component','part']:continue
        key=r['accession_number'];write(RESEARCH/'cma-records'/f'{key}.json',r)
        works.append(dict(id='cma-'+key,workId='cma:'+key,category='calligraphy',title=r['title'],author=creator_label(r),
            museum='克利夫兰艺术博物馆',source=r['url'],license='CC0',rightsSource='https://www.clevelandart.org/open-access',
            credit='Cleveland Museum of Art · CC0',period=r.get('creation_date') or '',accession=key,
            photographs=[dict(index=0,imageSource=r['images']['print']['url'],
                file=f'originals/cma-{key}-calligraphy-expansion-0.jpg',main=True)]))
    write(OUT/'discovery.json',works);write(OUT/'metadata-errors.json',errors)
    print(json.dumps({'candidateWorks':len(works),'photos':sum(len(w['photographs']) for w in works),'metadataErrors':len(errors)}),flush=True)


def discover_mia():
    response=requests.get('https://search.artsmia.org/calligraphy',params={'size':1000},headers=HEADERS,timeout=(15,60))
    response.raise_for_status();snapshot=response.json();write(OUT/'mia-search.json',snapshot)
    rows=[h['_source'] for h in snapshot['hits']['hits']]
    if snapshot['hits']['total']['value']>len(rows):
        response=requests.get('https://search.artsmia.org/calligraphy',params={'size':1000,'from':len(rows)},headers=HEADERS,timeout=(15,60))
        response.raise_for_status();second=response.json();write(OUT/'mia-search-page2.json',second)
        rows += [h['_source'] for h in second['hits']['hits']]
    rows=[r for r in rows if r.get('rights_type')=='Public Domain' and r.get('image')=='valid'
          and r.get('country') in ['China','Japan','Korea'] and 'Calligraphy' in r.get('classification','')]
    used={source_key(p['source']) for w in read(ASSETS/'selection.json') for p in [w]+w.get('additionalSources',[]) if p.get('source')}
    groups={};singles=[]
    for r in rows:
        source='https://collections.artsmia.org/art/'+str(r['id']);label=(r['title']+' '+str(r.get('object_name'))).lower()
        if source_key(source) in used:continue
        if 'couplet' in label or 'set of four' in label or 'set of three' in label or 'one of three' in label or 'pair]' in label or 'triptych' in label or re.search(r'page .* of two|\[pt\. [12],',label):
            groups.setdefault(r['accession_number'].rsplit('.',1)[0],[]).append(r)
        elif r['classification'].strip()=='Calligraphy' and not any(s in label for s in ['album','leaf','fragment','screen','set of','one of a pair','page from']):singles.append([r])
    for base,group in groups.items():
        label=' '.join(r['title'] for r in group).lower();expected=4 if 'four' in label else 3 if 'three' in label or 'triptych' in label else 2
        try:sequence=sorted(int(r['accession_number'].rsplit('.',1)[-1]) for r in group)
        except ValueError:continue
        if sequence==list(range(1,expected+1)):singles.append(sorted(group,key=lambda r:int(r['accession_number'].rsplit('.',1)[-1])))
    works=[]
    for group in singles:
        r=group[0];key=str(r['id']);photos=[]
        if not r.get('title'):continue
        for i,p in enumerate(group):
            write(RESEARCH/'mia-records'/f"{p['id']}.json",p)
            cache=p['Cache_Location'].replace('\\','/');name=p['Primary_RenditionNumber'].removesuffix('.jpg')
            photos.append(dict(index=i,imageSource=f'https://img.artsmia.org/web_objects_cache/{cache}/{name}_full.jpg',
                file=f"originals/mia-{p['id']}-calligraphy-expansion.jpg",main=i==0,
                source='https://collections.artsmia.org/art/'+str(p['id']),accession=p['accession_number']))
        title=r['title']
        if len(group)>1:
            title=re.sub(r',?\s*(one of a pair|from a set of four|one of three).*','',title,flags=re.I)
            title=re.sub(r'\s*\[.*?(triptych|set of|pair).*?\]','',title,flags=re.I)
            title=re.sub(r'\s*\(page .*? of two\)','',title,flags=re.I)
        works.append(dict(id='mia-'+key,workId='mia:'+(r['accession_number'].rsplit('.',1)[0] if len(group)>1 else r['accession_number']),
            category='calligraphy',title=title,author=r.get('artist') or 'Unidentified artist',museum='明尼阿波利斯艺术博物馆',
            source='https://collections.artsmia.org/art/'+key,license='Public domain',rightsSource='https://collections.artsmia.org/art/'+key,
            credit='Minneapolis Institute of Art · Public domain',period=r.get('dated') or '',accession=r['accession_number'],
            culture=r['country'],expectedParts=len(group),photographs=photos))
    write(OUT/'discovery-mia.json',works)
    print(json.dumps({'miaIndependentCandidates':len(works),'photos':sum(len(w['photographs']) for w in works),
        'Chinese':sum(w['culture']=='China' for w in works),'Japanese':sum(w['culture']=='Japan' for w in works)}),flush=True)


def download(supplement=False):
    # Prioritize complete short works before large bound volumes. Every candidate
    # and its unselected photograph URLs remain in the discovery record.
    tasks=[(w['id'],p) for w in read(OUT/('discovery-mia.json' if supplement else 'discovery.json')) if len(w['photographs'])<=20
           and not any(s in w['title'] for s in ['印材','石印','印洗','笔洗','金总管印','金万户侯印','金佛法僧宝印','金提控之印'])
           for p in w['photographs']]
    folder=OUT/'receipts';folder.mkdir(exist_ok=True)
    def one(task):
        key,p=task;receipt=folder/(Path(p['file']).stem+'.json')
        if receipt.exists():return read(receipt)
        try:
            target=RESEARCH/p['file']
            if not target.exists():
                response=requests.get(p['imageSource'],headers=HEADERS,timeout=(15,70));response.raise_for_status();target.write_bytes(response.content)
            with Image.open(target) as im:size=ImageOps.exif_transpose(im).size
            result=dict(id=key,**p,status='downloaded',sourceSize=list(size),originalSha256=sha(target),
                        qualified=min(size)>=600 and max(size)>=2400)
        except Exception as exc:result=dict(id=key,**p,status='failed',error=str(exc))
        write(receipt,result);return result
    with ThreadPoolExecutor(max_workers=6) as pool:
        results=[]
        for i,r in enumerate(pool.map(one,tasks),1):
            results.append(r)
            if i%50==0:print(f'DOWNLOAD {i}/{len(tasks)}',flush=True)
    write(OUT/('downloads-mia.json' if supplement else 'downloads-all.json'),results)
    print(json.dumps({'photographs':len(results),'qualified':sum(r.get('qualified',False) for r in results),'failed':sum(r['status']=='failed' for r in results)}),flush=True)


def sheets():
    works=read(OUT/'discovery.json');receipts={r['file']:r for r in read(OUT/'downloads-all.json')}
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',14);names=[]
    for w in works:
        photos=[(p,receipts[p['file']]) for p in w['photographs'] if receipts.get(p['file'],{}).get('status')=='downloaded']
        for start in range(0,len(photos),20):
            group=photos[start:start+20];sheet=Image.new('RGB',(1600,60+((len(group)+4)//5)*340),'#eee');draw=ImageDraw.Draw(sheet)
            draw.text((8,4),w['id']+' '+w['title'],font=font,fill='black')
            draw.text((8,26),w['author']+' '+w['period'],font=font,fill='black')
            for i,(p,r) in enumerate(group):
                x,y=i%5*320,60+i//5*340
                with Image.open(RESEARCH/p['file']) as im:thumb=ImageOps.contain(ImageOps.exif_transpose(im).convert('RGB'),(310,295))
                sheet.paste(thumb,(x+(320-thumb.width)//2,y));draw.text((x+4,y+300),f"{p['index']}  {r['sourceSize']} {'OK' if r['qualified'] else 'SMALL'}",font=font,fill='black')
            name=f"review-{w['id']}-{start//20:02}.jpg";sheet.save(OUT/name,quality=92);names.append(name)
    write(OUT/'review-sheets.json',names);print('CONTACT_SHEETS',len(names),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('stage',choices=['discover','download','sheets','mia','mia-download'])
    stage=parser.parse_args().stage
    if stage=='mia':discover_mia()
    elif stage=='mia-download':download(True)
    else:globals()[stage]()
