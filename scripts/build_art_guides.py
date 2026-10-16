"""Build Chinese guides for every current work from checked museum evidence.

No model calls, guessed narratives, image mutation, or installation. Editorial
paraphrases are reviewed inputs; metadata-only works remain a separate coverage
level. Ancient text enters the reader only through an explicit reviewed record.
"""
import argparse
import csv
import hashlib
import json
import re
from collections import Counter
from pathlib import Path

from collect_art_guide_sources import CATALOG, ROOT, clean, normalize, read

GUIDES = ROOT / 'docs/art-guides'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify_sources(works, evidence):
    pilot = ROOT/'artifacts/development/art-guides-pilot/sources'
    pilot_hashes = {sha(p) for p in pilot.iterdir() if p.is_file()}
    checked = 0
    for work in works:
        for source in work['sources']:
            fingerprint = source.get('evidenceSha256')
            snapshot = source.get('snapshot')
            if snapshot:
                path = (evidence/snapshot).resolve()
                if evidence not in path.parents or not path.is_file() or sha(path) != fingerprint:
                    raise ValueError(f'Missing or changed source evidence: {work["id"]}')
            elif not fingerprint or fingerprint not in pilot_hashes:
                raise ValueError(f'Missing reviewed pilot evidence: {work["id"]}')
            checked += 1
    return checked


def editorial(path, prefix=''):
    with path.open(encoding='utf-8', newline='') as handle:
        rows = list(csv.DictReader(handle, delimiter='\t'))
    result = {prefix + row['id']: row['overview'] for row in rows}
    if len(result) != len(rows):
        raise ValueError(f'Duplicate editorial identity: {path}')
    return result


def date_zh(value):
    text = clean(value)
    for a, b in [('undated', ''), ('Undated', ''), ('probably', '可能'),
                 ('possibly', '可能'), ('or later', '或更晚'), ('or earlier', '或更早'),
                 ('early', '早期'), ('mid-', '中期'), ('mid', '中期'), ('late', '晚期'),
                 ('first half', '上半叶'), ('second half', '下半叶'),
                 ('Qing dynasty', '清代'), ('Ming dynasty', '明代'),
                 ('Southern Song dynasty', '南宋'), ('Northern Song dynasty', '北宋'),
                 ('Song dynasty', '宋代'), ('Yuan dynasty', '元代'), ('Tang dynasty', '唐代'),
                 ('Republic period', '民国时期'), ('Republic of China', '中华民国'),
                 ('ca.', '约'), ('c.', '约')]:
        text = text.replace(a, b)
    text = re.sub(r'(\d+)(?:st|nd|rd|th) century', r'\1世纪', text)
    text = re.sub(r'(\d+)e(?:-\d+e)? siècle', lambda m: m[0].replace('e siècle','世纪').replace('e-','—'), text)
    text = text.replace('2e moitié du ', '下半叶，').replace('1ère moitié du ', '上半叶，')
    text = re.sub(r'(?<!\d)(\d{2})00s', lambda m: str(int(m[1])+1)+'世纪', text)
    text = text.replace('BCE', '公元前').replace('CE', '公元').replace('–', '—')
    return re.sub(r'\s+', ' ', text).strip(' ,;')


def medium_zh(value):
    text = clean(value).lower()
    translations = [
        ('pair of hanging scrolls', '对联'), ('round fan mounted as album leaf', '团扇装册页'),
        ('round fan mounted as an album leaf', '团扇装册页'),
        ('folding fan mounted as an album leaf', '折扇装册页'),
        ('fan mounted as an album leaf', '扇面装册页'),
        ('gold-flecked paper', '洒金纸'), ('gold-flecked silk', '洒金绢'),
        ('gold paper', '金笺'), ('gold satin', '金色缎'),
        ('ink and light color', '水墨淡设色'), ('ink and color', '水墨设色'),
        ('ink and colours', '水墨设色'), ('ink and colors', '水墨设色'),
        ('hanging scroll', '立轴'), ('handscroll', '手卷'), ('album leaf', '册页'),
        ('album of', '册，含'), ('wood panel', '木板'), ('encre noire', '墨'),
        ('papier de soie', '薄纸'), ('encre', '墨'), ('papier', '纸'), ('soie', '绢'),
        ('oil', '油彩'), ('ink', '墨'), ('canvas', '画布'), ('fabric', '织物'),
        ('paper', '纸'), ('silk', '绢'), ('satin', '缎'), ('copper', '铜板'),
        ('wood', '木'), ('board', '板'), ('panel', '板'), ('watercolor', '水彩'),
        ('gouache', '不透明水彩'), ('mounted on', '托裱于'), (' on ', '，材质：'),
        (' and ', '与'), ('; ', '；'), ('fan', '扇面')]
    for a, b in translations:
        text = text.replace(a, b)
    return text


def dimensions_zh(value):
    text = clean(value)
    if not text:
        return ''
    # Prefer the unframed object; retain framing/support qualifiers explicitly.
    parts = [p.strip() for p in re.split(r';|\r|\n', text) if p.strip()]
    first = next((p for p in parts if p.lower().startswith('unframed:')), parts[0])
    metric = re.search(r'(\d+(?:\.\d+)?(?:\s*[×x]\s*\d+(?:\.\d+)?){1,2})\s*cm', first)
    if metric:
        label = ('含框：' if first.lower().startswith('framed:') else
                 '未装框：' if first.lower().startswith('unframed:') else
                 '图面：' if first.lower().startswith('image:') else
                 '纸张：' if first.lower().startswith('sheet:') else
                 '装裱：' if first.lower().startswith('mounted:') else '')
        return label + metric[1].replace('x', '×') + '厘米'
    if '厘米' in text or '公分' in text:
        return text
    single = re.search(r'(\d+(?:\.\d+)?)\s*cm', first)
    if single and 'width' in first.lower():
        return '宽：'+single[1]+'厘米'+('（不含卷轴）' if 'without roller' in first.lower() else '')
    return ''


def build(evidence, output):
    catalog = read(CATALOG)
    catalog_before = sha(CATALOG)
    records = {r['id']: r for r in read(evidence/'museum-records.json')}
    selection = {w['id']: w for w in read(CATALOG.parent/'selection.json')}
    pilots = {w['id']: w for w in read(GUIDES/'samples.zh.json')['works']}
    supplements = read(GUIDES/'supplemental.zh.json')['works']
    original_review = read(GUIDES/'reviewed-transcriptions.zh.json')
    transcriptions = original_review['works']
    looking_reviews = read(GUIDES/'reviewed-looking.zh.json')['works']
    background_path = GUIDES/'reviewed-background.zh.json'
    background_reviews = read(background_path)['works'] if background_path.exists() else {}
    period_path = GUIDES/'reviewed-periods.zh.json'
    period_reviews = read(period_path) if period_path.exists() else {}
    narratives = editorial(GUIDES/'editorial.zh.tsv')
    narratives.update(editorial(GUIDES/'colbase-editorial.zh.tsv', 'colbase-tnm-'))
    narratives.update(editorial(GUIDES/'expansion-editorial.zh.tsv'))
    narratives.update(editorial(GUIDES/'chinese-painting-expansion.zh.tsv'))
    narratives.update(editorial(GUIDES/'calligraphy-expansion.zh.tsv'))
    met_notes = editorial(GUIDES/'met-editorial.zh.tsv')
    known = {w['id'] for w in catalog}
    if (set(narratives)-known or set(supplements)-known or set(transcriptions)-known
            or set(looking_reviews)-known or set(background_reviews)-known):
        raise ValueError('Editorial work not in current catalogue')
    # Web-tool pages are evidence, not modern prose to redistribute in the guide.
    met_web = {}
    for path in sorted((evidence/'sources').glob('met-detail-*.txt')):
        for block in path.read_text('utf-8').split('--------------------------------------------------------------------------------'):
            match = re.search(r'collection/search/(\d+)', block)
            if match:
                met_web[match[1]] = path
    works, gaps = [], []
    for c in catalog:
        r = dict(records[c['id']])
        s = selection[c['id']]
        if c['id'] == 'princeton-23232':
            path = evidence/'sources/princeton-23232-api.json'
            raw = read(path)
            if raw['objectid'] != 23232:
                raise ValueError('Princeton identity mismatch')
            r.update(normalize('princeton', raw), identityVerified=True, error=None,
                     snapshot=str(path.relative_to(evidence)), evidenceSha256=sha(path),
                     dataUrl='https://data.artmuseum.princeton.edu/objects/23232', method='direct_http')
        if c['id'] == 'rijks-AK-MAK-1422':
            path = evidence/'sources/rijks-AK-MAK-1422.json'
            raw = json.loads(read(path)['html'])
            identifiers = [x.get('content','') for x in raw['identified_by'] if x['type']=='Identifier']
            if raw['id'] != c['source'] or 'AK-MAK-1422-B' not in identifiers:
                raise ValueError('Rijksmuseum source-component identity mismatch')
            r.update(title='Calligraphy', date='c. 1900', medium='ink on paper',
                     dimensions='', description='', accession='AK-MAK-1422-B', identityVerified=True)
        supplement = supplements.get(c['id'], {})
        if supplement:
            r.update(supplement, error=None, identityVerified=True)
            path = evidence/'sources'/('npm-su-web-evidence.txt' if '000001' in c['id'] else 'npm-web-evidence.txt')
            r.update(snapshot=str(path.relative_to(evidence)), evidenceSha256=sha(path))
        if not r.get('identityVerified') and c['id'] not in pilots:
            raise ValueError(f'Unverified source identity: {c["id"]}')
        period = date_zh(r.get('date')) or date_zh(s.get('period'))
        if not period and r['sourceKind'] in ['met','princeton']:
            era = clean(r.get('culture'))
            if re.search(r'dynasty|century|Republic period', era, re.I):
                period = date_zh(era)
        medium = medium_zh(r.get('medium'))
        is_rubbing = bool(s.get('mediumNote', '').startswith('拓本'))
        if is_rubbing:
            medium = '拓本' + ('；'+medium if medium else '')
        dims = dimensions_zh(r.get('dimensions'))
        facts = {k: v for k, v in [('date', period), ('medium', medium), ('dimensions', dims),
                                  ('accession', r.get('accession'))] if v}
        # Keep metadata qualifications intact and avoid inventing a creation story.
        metadata = '\n'.join(label+'：'+value for label, value in
                             [('馆方著录年代' if is_rubbing else '年代', period), ('材质与形制', medium), ('尺寸', dims)] if value)
        if is_rubbing:
            facts['reproduction'] = 'rubbing'
            metadata += '\n形制说明：拓本，原迹或碑刻的年代与拓印年代有别。'
        story = supplement.get('overview') or narratives.get(c['id'])
        met_id = c['source'].rstrip('/').rsplit('/', 1)[-1] if r['sourceKind'] == 'met' else None
        if met_id in met_notes:
            if met_id not in met_web:
                raise ValueError(f'Missing Met narrative evidence: {met_id}')
            story = met_notes[met_id]
        if story:
            overview = story + ('\n\n'+metadata if metadata else '')
            level = 'source_based_context'
        else:
            overview = (metadata+'\n' if metadata else '') + '收藏：'+c['museum']
            if facts.get('accession'):
                overview += '\n馆藏编号：'+facts['accession']
            level = 'verified_metadata'
        looking, looking_basis = '', {'kind': 'not_available', 'sourceIds': []}
        if level == 'source_based_context':
            # Derive a concise caption from a complete sentence, never truncate it.
            sentences = re.findall(r'[^。！？]+[。！？]?', story)
            brief = ''.join(sentences[:1])
            if len(brief) < 30 and len(sentences) > 1:
                brief += sentences[1]
            if len(brief) > 75:
                brief = '从文字、形制与细节进入这件作品，结合创作背景体会它在艺术传统中的位置。'
        else:
            brief = ('先读整幅，再看笔画与留白的呼应；从文字、形制和用笔进入这件书法。'
                     if c['category'] == 'calligraphy' else
                     '先看整体，再沿光线与形体进入细节；结合年代和材质，体会这件作品的绘画语言。')
        source = {'id': 'museum', 'url': c['source'], 'dataUrl': r['dataUrl'],
                  'publisher': c['museum'], 'accessed': '2026-10-08',
                  'method': r.get('method', 'direct_http'), 'evidenceSha256': r.get('evidenceSha256'),
                  'snapshot': r.get('snapshot'), 'scope': 'work_metadata_and_context' if story else 'work_metadata'}
        sources = [source]
        if met_id in met_notes:
            path = met_web[met_id]
            sources.append(dict(source, id='museum_web', method='web_tool', dataUrl=c['source'],
                                evidenceSha256=sha(path), snapshot=str(path.relative_to(evidence))))
        transcription = transcriptions.get(c['id']) or supplement.get('transcription', {'status': 'not_available'})
        candidate_count = sum(bool(re.search(r'[\u3400-\u9fff]{4}', str(x.get('inscription', ''))))
                              for x in r.get('inscriptions') or [])
        if candidate_count and transcription['status'] not in ('source_verified', 'reference_verified'):
            transcription = {'status': 'withheld_pending_review'}
        w = {k: c[k] for k in ['id', 'workId', 'category', 'title', 'author', 'museum']}
        w.update(period=period, brief=brief, overview=overview, looking=looking, facts=facts,
                 transcription=transcription, sources=sources, coverage={'level': level},
                 contentKinds={'overview': level, 'looking': 'not_available'},
                 lookingBasis=looking_basis,
                 research={'sourceHasContext': bool(r.get('description') or story),
                           'transcriptionCandidateRecords': candidate_count,
                           'originalDate': r.get('date'), 'originalMedium': r.get('medium')})
        if c['id'] in pilots:
            # Previously reviewed guides and authorship/transcription decisions win.
            pilot = pilots[c['id']]
            for key in ['period', 'brief', 'overview', 'transcription', 'contentKinds']:
                if key in pilot:
                    w[key] = pilot[key]
            w['sources'] = pilot['sources']
            if w.get('period'):
                w['facts']['date'] = w['period']
            else:
                w['facts'].pop('date', None)
            w['coverage'] = {'level': 'source_based_context'} if c['id'] != 'princeton-23232' else {'level': 'verified_metadata'}
        # Explicit new reviews also apply to pilot works; pilot context remains
        # intact. Literature sources prove the reference text, not every glyph
        # written on the object, and are preserved separately from its museum.
        if c['id'] in transcriptions:
            reviewed = transcriptions[c['id']]
            w['transcription'] = {k:v for k,v in reviewed.items() if k not in ('sources', 'readerTitle')}
            if reviewed.get('readerTitle'):
                w['title'] = reviewed['readerTitle']
            w['sources'].extend(reviewed.get('sources', []))
            if reviewed.get('sourceId') not in {s['id'] for s in w['sources']}:
                raise ValueError('Transcription source identity missing: '+c['id'])
        # Encyclopedia background is labelled secondary evidence in the overview.
        # It never fills the separately reviewed authoritative-looking field.
        if c['id'] in background_reviews:
            review = background_reviews[c['id']]
            if (review['scope'] not in ('secondary_encyclopedic_background_reference',
                                       'museum_work_background_reference')
                    or not review['text'].strip() or len(review['text'].encode('utf-8')) > 450
                    or not review['sourceIds'] or not review['sources']
                    or not set(review['sourceIds']) <= {s['id'] for s in review['sources']}):
                raise ValueError('Invalid reviewed background: '+c['id'])
            label = ('百科参考' if review['scope'] == 'secondary_encyclopedic_background_reference'
                     else '馆方资料')
            w['overview'] += '\n\n作者与作品背景（' + label + '）：\n' + review['text'].strip()
            w['backgroundBasis'] = {k: review[k] for k in ('scope', 'sourceIds', 'checkedOn')}
            w['contentKinds']['background'] = review['scope']
            w['sources'].extend(review['sources'])
        # A museum metadata URL does not establish an editorial interpretation.
        # Only individually reviewed, source-backed analysis enters this field.
        w['looking'] = ''
        w['contentKinds']['looking'] = 'not_available'
        if c['id'] in looking_reviews:
            review = looking_reviews[c['id']]
            if review.get('review', {}).get('analysisProvidedByMuseum') is False:
                raise ValueError('Editorial image observation is not authoritative analysis: '+c['id'])
            if review['scope'] != 'work_specific_authoritative_analysis' or not review['text'].strip():
                raise ValueError('Unreviewed looking text: '+c['id'])
            if len(review['text'].encode('utf-8')) >= 384:
                raise ValueError('Looking text exceeds reader bounds: '+c['id'])
            if not review['sourceIds'] or not review['sources']:
                raise ValueError('Looking text lacks specific sources: '+c['id'])
            if not set(review['sourceIds']) <= {s['id'] for s in review['sources']}:
                raise ValueError('Looking source identity missing: '+c['id'])
            w['looking'] = review['text']
            w['lookingBasis'] = {k: review[k] for k in ('scope', 'sourceIds', 'checkedOn')}
            w['contentKinds']['looking'] = review['scope']
            w['sources'].extend(review['sources'])
        if c['category'] == 'calligraphy' and w['transcription']['status'] not in ('source_verified', 'reference_verified'):
            gaps.append({'id': c['id'], 'field': 'transcription', 'status': w['transcription']['status']})
        if w['coverage']['level'] == 'verified_metadata':
            gaps.append({'id': c['id'], 'field': 'context', 'status': 'no_reviewed_work_specific_context'})
        if c['id'] in period_reviews:
            reviewed = period_reviews[c['id']]
            snapshot = ROOT/reviewed['snapshot']
            if reviewed['workId'] != w['workId'] or sha(snapshot) != reviewed['snapshotSha256']:
                raise ValueError('Reviewed period source identity changed: '+c['id'])
            w['period'] = reviewed['period']
            w['facts']['date'] = w['period']
            w['periodBasis'] = reviewed
        if not w['period']:
            gaps.append({'id': c['id'], 'field': 'date', 'status': 'not_recorded'})
        works.append(w)
    identities = [(w['category'], w['workId']) for w in works]
    if len(identities) != len(set(identities)) or len(works) != len(catalog):
        raise ValueError('Catalogue coverage or independent identity mismatch')
    for w in works:
        if not all(w.get(k) for k in ['brief', 'overview', 'sources']):
            raise ValueError('Empty content')
        if re.search(r'资料有限|本轮|尚未核对|待复核', w['overview']+w['brief']+w['looking']):
            raise ValueError('Research note leaked into reading content')
    checked_sources = verify_sources(works, evidence)
    report = {'schemaVersion': 1, 'checkedOn': original_review['checkedOn'],
              'originalTextsRevision': original_review['checkedOn'], 'catalogSha256': catalog_before,
              'verifiedEvidenceRecords': checked_sources,
              'totalWorks': len(works), 'categories': dict(Counter(w['category'] for w in works)),
              'coverage': dict(Counter(w['coverage']['level'] for w in works)),
              'authoritativeLooking': sum(bool(w['looking']) for w in works),
              'omittedLooking': sum(not w['looking'] for w in works),
              'transcriptions': dict(Counter(w['transcription']['status'] for w in works if w['category']=='calligraphy')),
              'sourcesByKind': dict(Counter(r['sourceKind'] for r in records.values())),
              'missingFields': dict(Counter(g['field'] for g in gaps)), 'gaps': gaps,
              'runtimeIntegrated': False, 'hardwareVerified': False}
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps({'schemaVersion':1, 'language':'zh-CN', 'edition':'full-2026-10-08',
                                 'originalTextsRevision':original_review['checkedOn'],
                                 'status':'preview_only', 'works':works},ensure_ascii=False,indent=2)+'\n','utf-8')
    (output.parent/'coverage.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n','utf-8')
    if sha(CATALOG) != catalog_before:
        raise ValueError('Catalogue changed during guide build')
    print(json.dumps({k:v for k,v in report.items() if k not in ['gaps','sourcesByKind']},ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, default=ROOT/'artifacts/development/art-guides-full')
    parser.add_argument('--output', type=Path, default=GUIDES/'catalog.zh.json')
    args = parser.parse_args()
    build(args.evidence.resolve(), args.output.resolve())
