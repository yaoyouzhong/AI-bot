"""Collect museum evidence for the existing catalogue without changing it.

Reuses dated research snapshots, verifies accession/object identity, and fetches
missing public API records. Failures are retained explicitly, never empty success.
"""
import argparse
import hashlib
import json
import re
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path
from urllib.parse import urlparse

import requests
from bs4 import BeautifulSoup


ROOT = Path(__file__).resolve().parent.parent
RESEARCH = ROOT / 'artifacts/development/tab5-daily-art'
CATALOG = ROOT / 'windows-app/AIBotBridge/Assets/DailyArt/catalog.json'


def read(path):
    return json.loads(path.read_text('utf-8-sig'))


def clean(value):
    if value is None:
        return ''
    return BeautifulSoup(str(value), 'html.parser').get_text(' ', strip=True)


def collect(output):
    output.mkdir(parents=True, exist_ok=True)
    snapshots = output / 'sources'
    snapshots.mkdir(exist_ok=True)
    catalog = read(CATALOG)
    cma = {}
    for name in ['cleveland-paintings.json', 'cma-chinese-paintings.json',
                 'cleveland-calligraphy.json', 'cma-calligraphy.json']:
        path = RESEARCH / name
        if path.exists():
            doc = read(path)
            for record in doc.get('data', []) if isinstance(doc, dict) else doc:
                cma[record['accession_number']] = (record, path)
    for path in sorted((RESEARCH/'cma-records').glob('*.json')):
        record = read(path)
        cma[record['accession_number']] = (record, path)

    def one(work):
        source = work['source']
        host = urlparse(source).netloc
        key = urlparse(source).path.rstrip('/').split('/')[-1]
        path = None
        record = None
        data_url = source
        kind = ''
        error = None
        if 'clevelandart.org' in host:
            kind = 'cma'
            data_url = f'https://openaccess-api.clevelandart.org/api/artworks/{key}'
            if key in cma:
                record, path = cma[key]
        elif 'metmuseum.org' in host:
            kind = 'met'
            data_url = f'https://collectionapi.metmuseum.org/public/collection/v1/objects/{key}'
            path = RESEARCH / 'met-records' / f'{key}.json'
            if not path.exists():
                path = RESEARCH / f'met-{key}.json'
        elif 'artsmia.org' in host:
            kind = 'mia'
            data_url = f'https://search.artsmia.org/id/{key}'
            path = RESEARCH / 'mia-records' / f'{key}.json'
        elif 'artic.edu' in host:
            kind = 'artic'
            data_url = f'https://api.artic.edu/api/v1/artworks/{key}'
            path = RESEARCH / 'artic-records' / f'{key}.json'
        elif 'colbase' in host:
            kind = 'colbase'
            data_url = source
            path = RESEARCH / 'colbase' / f'{key}.json'
        elif 'princeton' in host:
            kind = 'princeton'
            path = RESEARCH / 'princeton-records' / f'{key}.json'
        elif 'parismusees' in host:
            kind = 'paris'
            number = work['id'].removeprefix('paris-')
            path = RESEARCH / 'paris-records' / f'{number}.json'
            data_url = f'https://apicollections.parismusees.paris.fr/iiif/{number}/manifest'
        elif 'dpm.org.cn' in host or 'npm.gov.tw' in host or 'rijksmuseum' in host:
            kind = 'html'
        try:
            method = 'retained_research_snapshot'
            if record is None and path and path.exists():
                record = read(path)
            if record is None:
                target = snapshots / (work['id'] + '.raw')
                if target.exists():
                    raw = target.read_bytes()
                    method = 'retained_current_fetch'
                else:
                    response = requests.get(data_url, timeout=30,
                                            headers={'User-Agent': 'AI-bot-art-guide-research/1.0'})
                    response.raise_for_status()
                    raw = response.content
                    target.write_bytes(raw)
                    method = 'direct_http'
                path = target
                record = {'html': raw.decode('utf-8')} if kind == 'html' else json.loads(raw)
            if kind in ['cma', 'artic'] and isinstance(record, dict) and 'data' in record:
                record = record['data']
            # Verify identity from official records, not cache filenames alone.
            if kind == 'cma':
                assert record['accession_number'] == key
            elif kind == 'met':
                assert str(record['objectID']) == key
            elif kind == 'mia':
                assert str(record['id']).rsplit('/', 1)[-1] == key
            elif kind == 'artic':
                assert str(record['id']) == key
            elif kind == 'colbase':
                assert record['organization_item_key'] == key
                assert record['organization_path_name'] == urlparse(source).path.split('/')[2]
            elif kind == 'princeton':
                assert str(record['objectid']) == key
            elif kind == 'paris':
                assert work['id'].removeprefix('paris-') in record['@id']
            result = normalize(kind, record)
            raw = json.dumps(record, ensure_ascii=False, indent=2).encode('utf-8')
            target = snapshots / (work['id'] + '.json')
            target.write_bytes(raw)
            result.update(id=work['id'], workId=work['workId'], category=work['category'],
                          source=source, dataUrl=data_url, method=method,
                          sourceKind=kind, evidenceSha256=hashlib.sha256(raw).hexdigest(),
                          snapshot=str(target.relative_to(output)),
                          retainedFrom=str(path.relative_to(ROOT)) if path else None,
                          checkedOn='2026-10-08', identityVerified=kind != 'html')
            if kind == 'html':
                text = result.get('description', '')
                if len(text) < 100 or any(t in text for t in ['Access Denied', 'Request blocked']):
                    raise ValueError('No museum content in response')
            return result
        except Exception as exc:
            error = type(exc).__name__ + ': ' + str(exc)
            return dict(id=work['id'], workId=work['workId'], category=work['category'],
                        source=source, dataUrl=data_url, sourceKind=kind,
                        identityVerified=False, error=error)

    with ThreadPoolExecutor(max_workers=4) as pool:
        records = list(pool.map(one, catalog))
    (output / 'museum-records.json').write_text(
        json.dumps(records, ensure_ascii=False, indent=2) + '\n', 'utf-8')
    print(json.dumps({'total': len(records), 'verified': sum(r['identityVerified'] for r in records),
                      'errors': [{k: r[k] for k in ['id', 'error']} for r in records if r.get('error')]},
                     ensure_ascii=False))


def normalize(kind, r):
    if kind == 'cma':
        return dict(title=r['title'], date=r.get('creation_date'), medium=r.get('technique'),
                    culture=r.get('culture'), dimensions=r.get('measurements'),
                    description=clean(r.get('description')), extra=clean(r.get('did_you_know')),
                    inscriptions=r.get('inscriptions', []), accession=r['accession_number'])
    if kind == 'met':
        return dict(title=r['title'], date=r.get('objectDate'), medium=r.get('medium'),
                    culture=r.get('period') or r.get('culture'), dimensions=r.get('dimensions'),
                    description='', extra='', inscriptions=[], accession=r['accessionNumber'],
                    form=r.get('objectName'), tags=[t['term'] for t in r.get('tags') or []])
    if kind == 'mia':
        return dict(title=r['title'], date=r.get('dated'), medium=r.get('medium'),
                    culture=r.get('country'), dimensions=r.get('dimension'),
                    description=clean(r.get('text')), extra=clean(r.get('description')),
                    inscriptions=[{'inscription': r.get('inscription'), 'signed': r.get('signed')}],
                    accession=r.get('accession_number'), form=r.get('object_name'))
    if kind == 'artic':
        return dict(title=r['title'], date=r.get('date_display'), medium=r.get('medium_display'),
                    culture=r.get('place_of_origin'), dimensions=r.get('dimensions'),
                    description=clean(r.get('description')), extra=clean(r.get('short_description')),
                    inscriptions=[{'inscription': r.get('inscriptions')}],
                    accession=r.get('main_reference_number'))
    if kind == 'colbase':
        return dict(title=r['title'], date=r.get('jidai_seiki'), medium=r.get('hinshitu_keijo'),
                    culture=r.get('seisakuchi'), dimensions=r.get('houryo'),
                    description='\n'.join(d['text'].strip() for d in r.get('descriptions', [])
                                          if d.get('locale') == 'zh' and d.get('text')),
                    extra='', inscriptions=[{'inscription': r.get('meibun')}],
                    accession=r['organization_item_key'])
    if kind == 'princeton':
        texts = r.get('texts') or []
        description = '\n'.join(clean(t.get('text', '')) for t in texts if isinstance(t,dict)) if isinstance(texts,list) else clean(texts)
        return dict(title=r['displaytitle'], date=r.get('displaydate'), medium=r.get('medium'),
                    culture=r.get('displayperiod'), dimensions=r.get('dimensions'),
                    description=description, extra=clean(r.get('caption')),
                    inscriptions=[{'inscription': r.get('inscribed'), 'signed': r.get('signed')}],
                    accession=r.get('objectnumber'))
    if kind == 'paris':
        meta = {m['label']: m['value'] for m in r.get('metadata', [])}
        def val(key):
            value = meta.get(key)
            return '\n'.join(clean(x) for x in value) if isinstance(value, list) else clean(value)
        return dict(title=clean(r['label']), date=val('Datation en siècle'),
                    medium=val('Matériaux et techniques'), culture='China',
                    dimensions=val('Dimensions'), description=val('Description'),
                    extra='', inscriptions=[{'inscription': val('Inscriptions')}],
                    accession=val('Numéro d’inventaire'))
    soup = BeautifulSoup(r['html'], 'html.parser')
    for el in soup(['script', 'style', 'nav', 'header', 'footer']):
        el.decompose()
    return dict(title='', date='', medium='', culture='', dimensions='',
                description=soup.get_text('\n', strip=True), extra='', inscriptions=[])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path,
                        default=ROOT / 'artifacts/development/art-guides-full')
    collect(parser.parse_args().output.resolve())
