"""Source-checked, resumable expansion of the current museum artwork library.

Discovery and downloads are separate from activation. Complete-source review,
native rendering, deduplication and acceptance precede catalogue integration.
"""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import re
import threading
import time
from urllib.parse import urlparse

import requests
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT/'windows-app/AIBotBridge/Assets/DailyArt'
RESEARCH = ROOT/'artifacts/development/tab5-daily-art'
OUT = ROOT/'artifacts/development/art-expansion-2026-10-08'
HEADERS = {'User-Agent':'AI-bot-art-library/1.0 (museum open-access research)'}


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf-8')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_key(url):
    u=urlparse(url)
    return (u.netloc.removeprefix('www.'),u.path.rstrip('/'))


def creator_label(record):
    labels=[]
    chinese=record.get('department')=='Chinese Art'
    for c in record.get('creators') or []:
        if not c.get('use_in_caption') or not c.get('description'):continue
        original=c.get('name_in_original_language') or ''
        name=original if chinese and re.search(r'[\u3400-\u9fff]',original) else c['description'].split(' (')[0]
        qualifier=c.get('qualifier') or ''
        if chinese and name==original:
            if qualifier=='attributed to':name='传'+name
            elif qualifier=='copy after':name='仿'+name+'（摹本，作者未详）'
            elif qualifier=='style of':name+='风格（作者未详）'
            elif qualifier:name=qualifier+' '+name
        elif qualifier:name=qualifier+' '+name
        if c.get('include_extent') and c.get('extent'):
            name+=('（书）' if chinese and c['extent']=='calligraphy by' else ' · '+c['extent'])
        labels.append(name)
    return ' / '.join(labels) or ('佚名' if chinese else 'Unidentified artist')


def discover():
    OUT.mkdir(parents=True,exist_ok=True)
    used={source_key(w['source']) for w in read(ASSETS/'catalog.json')}
    raw=[]
    skip=0
    while True:
        page=OUT/'cma-api'/f'painting-{skip:04}.json'
        if page.is_file(): doc=read(page)
        else:
            reply=requests.get('https://openaccess-api.clevelandart.org/api/artworks/',
                               params={'type':'Painting','cc0':1,'has_image':1,'limit':1000,'skip':skip},
                               headers=HEADERS,timeout=(15,60))
            reply.raise_for_status();doc=reply.json();write(page,doc)
        rows=doc['data'];raw.extend(rows)
        print(f'CMA metadata {len(raw)}/{doc["info"]["total"]}',flush=True)
        if not rows or len(raw)>=doc['info']['total']:break
        skip+=len(rows)
    # Dedicated official records let guide collection reuse the same evidence.
    for r in raw:write(RESEARCH/'cma-records'/(r['accession_number']+'.json'),r)
    candidates=[];excluded=[]
    for r in raw:
        key='cma-'+r['accession_number'];reason=None
        picture=(r.get('images') or {}).get('print') or {}
        form=(r.get('technique') or '')+' '+r['title']
        if source_key(r['url']) in used:reason='already_in_library'
        elif r.get('share_license_status')!='CC0' or r.get('type')!='Painting':reason='unsupported_rights_or_type'
        elif r.get('cover_accession_number'):reason='part_of_catalogued_group_requires_complete_source_review'
        elif re.search(r'album|manuscript|folio|leaf|leaves|page\b|fragment|verso|recto|screen|\bbook\b|\bvolume\b',form,re.I):reason='multipart_or_partial_source_requires_review'
        elif not picture.get('url'):reason='no_official_print_image'
        elif min(int(picture.get('width') or 0),int(picture.get('height') or 0))<1000:reason='short_dimension_below_1000'
        elif max(int(picture.get('width') or 0),int(picture.get('height') or 0))<2400:reason='long_dimension_below_2400'
        if reason:
            excluded.append({'id':key,'reason':reason});continue
        creators=[x for x in r.get('creators') or [] if x.get('use_in_caption') and x.get('description')]
        author=creator_label(r)
        title=r.get('title_in_original_language') or r['title']
        candidates.append({'id':key,'workId':'cma:'+r['accession_number'],'category':'painting',
            'title':title,'author':author,'museum':'克利夫兰艺术博物馆','file':'originals/'+key+'.jpg',
            'source':r['url'],'imageSource':picture['url'],'license':'CC0',
            'credit':'Cleveland Museum of Art · CC0','period':r.get('creation_date') or '',
            'rightsSource':'https://www.clevelandart.org/open-access',
            'museumTitle':r['title'],'accession':r['accession_number'],
            'metadataEvidence':'cma-records/'+r['accession_number']+'.json',
            'metadataSha256':sha(RESEARCH/'cma-records'/(r['accession_number']+'.json'))})
    write(OUT/'candidates.json',candidates);write(OUT/'discovery-exclusions.json',excluded)
    print(json.dumps({'officialPaintingRecords':len(raw),'eligibleNewPaintings':len(candidates)},ensure_ascii=False),flush=True)


def chinese():
    """Add complete Chinese scrolls and complete, officially grouped albums."""
    raw=[r for p in sorted((OUT/'cma-api').glob('painting-*.json')) for r in read(p)['data']]
    used={source_key(w['source']) for w in read(ASSETS/'catalog.json')+read(OUT/'candidates.json')}
    candidates=[];special_candidates=[]
    def eligible(r):
        p=(r.get('images') or {}).get('print') or {}
        return r.get('share_license_status')=='CC0' and p.get('url') and min(int(p.get('width',0)),int(p.get('height',0)))>=600 and max(int(p.get('width',0)),int(p.get('height',0)))>=2400
    def image(r):
        return dict(file='originals/cma-'+r['accession_number']+'.jpg',imageSource=r['images']['print']['url'],
                    source=r['url'],license='CC0',metadataEvidence='cma-records/'+r['accession_number']+'.json',
                    metadataSha256=sha(RESEARCH/'cma-records'/(r['accession_number']+'.json')))
    for r in raw:
        if r.get('department')!='Chinese Art' or r.get('cover_accession_number') or source_key(r['url']) in used or not eligible(r):continue
        form=r['title']+' '+r.get('technique','')
        calligraphic=r['accession_number']=='1999.174'
        special=r['accession_number'] in ('1979.27','1999.260','1999.174')
        if re.search(r'fragment|\bbook\b',form,re.I) or ('calligraphy' in form.lower() and not special):continue
        children=[]
        if calligraphic:
            expected=6
            children=[c for c in raw if c.get('cover_accession_number')==r['accession_number']]
            if len(children)!=expected or not all(eligible(c) and source_key(c['url']) not in used for c in children):continue
            children.sort(key=lambda c:int(c['accession_number'].split('.')[-1]))
        elif re.search(r'album',r.get('technique',''),re.I):
            match=re.search(r'(eight|ten|twelve|eighteen|twenty-two|fifty) leaves',r['technique'],re.I)
            if not match:continue
            expected={'eight':8,'ten':10,'twelve':12,'eighteen':18,'twenty-two':22,'fifty':50}[match[1].lower()]
            children=[c for c in raw if c.get('cover_accession_number')==r['accession_number'] and c.get('record_type')!='cover']
            if r['accession_number']=='1979.27':children=[c for c in children if len(c['accession_number'].split('.'))==4]
            painted=[c for c in children if 'colophon' not in c['title'].lower()]
            if len(painted)!=expected or not all(eligible(c) and source_key(c['url']) not in used for c in children):continue
            def order(c):
                if r['accession_number']=='1955.302':return (re.search(r'Leaf ([A-H])',c['title'])[1],)
                return tuple((0,int(x)) if x.isdigit() else (1,x) for x in c['accession_number'].split('.')[2:])
            children.sort(key=order)
        elif re.search(r'leaf|leaves|page|manuscript|folio|screen',form,re.I):continue
        creators=[x for x in r.get('creators') or [] if x.get('use_in_caption') and x.get('description')]
        author=creator_label(r)
        w=dict(id='cma-'+r['accession_number'],workId='cma:'+r['accession_number'],category='calligraphy' if calligraphic else 'painting',
               title=r.get('title_in_original_language') or r['title'],author=author,museum='克利夫兰艺术博物馆',
               source=r['url'],license='CC0',credit='Cleveland Museum of Art · CC0',period=r.get('creation_date') or '',
               rightsSource='https://www.clevelandart.org/open-access',museumTitle=r['title'],accession=r['accession_number'],
               sourceMinimumShortDimension=600,metadataEvidence='cma-records/'+r['accession_number']+'.json',
               metadataSha256=sha(RESEARCH/'cma-records'/(r['accession_number']+'.json')))
        first=children[0] if children else r
        w.update(file=image(first)['file'],imageSource=image(first)['imageSource'])
        if children:
            w['detail']='第1页 / 共'+str(len(children))+'页'
            w['completePanelCount' if calligraphic else 'completeAlbumLeaves']=expected
            w['componentAccessions']=[c['accession_number'] for c in children]
            w['additionalSources']=[dict(image(c),detail=f'第{i+2}页 / 共{len(children)}页') for i,c in enumerate(children[1:])]
        if 'handscroll' in r.get('technique','').lower():w['detailViews']=True
        (special_candidates if special else candidates).append(w)
    candidates.extend(special_candidates)
    write(OUT/'chinese-candidates.json',candidates)
    print(json.dumps({'additionalChineseWorks':len(candidates),'completeAlbums':sum('completeAlbumLeaves' in w for w in candidates)}),flush=True)


def download(chinese_only=False):
    selected=read(OUT/('chinese-candidates.json' if chinese_only else 'candidates.json'))
    rows=[]
    for w in selected:
        rows.append(w)
        rows.extend(dict(p,id=Path(p['file']).stem,sourceMinimumShortDimension=w.get('sourceMinimumShortDimension',1000)) for p in w.get('additionalSources',[]))
    stop=threading.Event()
    receipts=OUT/'download-receipts';receipts.mkdir(exist_ok=True)
    def one(w):
        receipt=receipts/(w['id']+'.json')
        original=RESEARCH/w['file']
        if receipt.is_file():
            result=read(receipt)
            if result.get('status')=='downloaded' and original.is_file() and sha(original)==result['sha256']:return result
        result={'id':w['id'],'imageSource':w['imageSource']}
        try:
            if not original.is_file():
                if stop.is_set():return dict(result,status='deferred_museum_access_limit')
                response=requests.get(w['imageSource'],headers=HEADERS,timeout=(15,60))
                if response.status_code in (403,429):
                    stop.set();return dict(result,status='museum_access_limit',httpStatus=response.status_code)
                response.raise_for_status()
                # Never overwrite an existing original or activate a partial response.
                partial=OUT/(w['id']+'.download-partial')
                partial.write_bytes(response.content)
                with Image.open(partial) as im:im.verify()
                original.parent.mkdir(exist_ok=True)
                partial.replace(original)
            with Image.open(original) as im:
                size=im.size
                if min(size)<w.get('sourceMinimumShortDimension',1000) or max(size)<2400:raise ValueError('Insufficient source resolution')
                if im.width*im.height>100_000_000:raise ValueError('Source exceeds review memory bound')
            result.update(status='downloaded',size=list(size),sha256=sha(original),bytes=original.stat().st_size)
        except Exception as exc:
            result.update(status='failed',error=type(exc).__name__+': '+str(exc))
        write(receipt,result)
        return result
    results=[]
    with ThreadPoolExecutor(max_workers=4) as pool:
        for index,result in enumerate(pool.map(one,rows),1):
            results.append(result)
            if index%25==0:print(f'Downloaded/reviewable {sum(r["status"]=="downloaded" for r in results)}/{index}/{len(rows)}',flush=True)
    write(OUT/('chinese-downloads.json' if chinese_only else 'downloads.json'),results)
    print(json.dumps({'reviewableOriginals':sum(r['status']=='downloaded' for r in results),'attempted':len(rows),'accessLimit':stop.is_set()},ensure_ascii=False),flush=True)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage',choices=['discover','download','chinese','download-chinese'])
    args=parser.parse_args()
    {'discover':discover,'download':download,'chinese':chinese,'download-chinese':lambda:download(True)}[args.stage]()
