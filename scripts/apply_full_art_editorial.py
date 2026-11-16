"""Apply individually sourced Chinese edits to the library and its annual reader."""
import argparse
import csv
import hashlib
import json
import re
import subprocess
from collections import Counter, defaultdict
from functools import lru_cache
from pathlib import Path
from urllib.parse import urlparse


ROOT = Path(__file__).resolve().parents[1]
GUIDES = ROOT / 'docs/art-guides'
REVISION = '2026-11-11'
FINAL_REVIEW = '2026-11-12'
SOURCE = 'full_library_editorial_20261111'
SCOPE = 'work_specific_authoritative_analysis'
READINGS = 'reviewed-transcriptions.zh.json'
REVIEW_FIELDS = {'sources', 'readerTitle', 'replacesExcerptSha256', 'correctsReadingSha256'}
READABLE = {'source_verified', 'reference_verified'}
OBJECT_SCOPES = {'artwork_main_body_excerpt', 'artwork_main_body_transcription'}
MUSEUM_EXCERPT = 'work_identity_and_museum_transcribed_excerpt'
REFERENCE_SCOPES = {
    'literary_reference_excerpt_not_object_transcription',
    'literary_reference_not_object_transcription',
    'museum_translation_paraphrase_not_object_transcription',
}
PUBLISHERS = {
    'archive.artic.edu': '芝加哥艺术博物馆',
    'publications.artic.edu': '芝加哥艺术博物馆',
    'www.metmuseum.org': '大都会艺术博物馆',
    'www.clevelandart.org': '克利夫兰艺术博物馆',
    'clevelandart.org': '克利夫兰艺术博物馆',
    'emuseum.nich.go.jp': '日本国立文化财机构 e国宝／东京国立博物馆',
    'colbase.nich.go.jp': '日本国立文化财机构 ColBase／东京国立博物馆',
    'online.bunka.go.jp': '日本文化厅文化遗产在线／东京国立博物馆',
    'www.tnm.jp': '东京国立博物馆',
    'artplatform.go.jp': '日本文化厅 Art Platform Japan／东京国立博物馆',
    'www.parismuseescollections.paris.fr': '巴黎博物馆联盟／赛努奇博物馆',
    'parismuseescollections.paris.fr': '巴黎博物馆联盟／赛努奇博物馆',
    'new.artsmia.org': '明尼阿波利斯艺术博物馆',
    'collections.artsmia.org': '明尼阿波利斯艺术博物馆',
    'search.artsmia.org': '明尼阿波利斯艺术博物馆',
    'artmuseum.princeton.edu': '普林斯顿大学艺术博物馆',
}


def load_edits():
    edits = {}
    for round_number in range(1, 19):
        stem = 'full-library-editorial' + (f'-round{round_number}' if round_number > 1 else '')
        path = GUIDES / (stem + '.zh.tsv')
        evidence = json.loads((GUIDES / (stem + '-evidence.json')).read_text(encoding='utf-8'))
        round_ids = set()
        with path.open(encoding='utf-8', newline='') as f:
            for row in csv.DictReader(f, delimiter='\t'):
                if (None in row or not row['id'] or row['id'] in round_ids
                        or not row['locator'] or not row['context'] or not row['brief']):
                    raise ValueError(f'Invalid editorial record: {row}')
                round_ids.add(row['id'])
                if row['url']:
                    proof = evidence.pop(row['id'])
                    if proof['url'] != row['url']:
                        raise ValueError('Editorial source changed: ' + row['id'])
                    row['proof'] = proof
                row['round'] = round_number
                row['checkedOn'] = FINAL_REVIEW if round_number >= 10 else REVISION
                edits[row['id']] = row
        if evidence:
            raise ValueError('Unused editorial source records: ' + path.name)
    return edits


def apply_edits(works, edits, *, full=False):
    seen = set()
    for work in works:
        row = edits.get(work['id'])
        if row is None:
            continue
        seen.add(work['id'])
        museum = work['sources'][0]
        url = row['url'] or museum['url']
        parsed = urlparse(url)
        if parsed.scheme != 'https' or parsed.netloc not in PUBLISHERS:
            raise ValueError('Unrecognized official source: ' + url)
        if work['id'].startswith('cma-'):
            accession = work['id'].removeprefix('cma-')
            if parsed.path != '/art/' + accession or work['facts']['accession'] != accession:
                raise ValueError('Cleveland work identity mismatch: ' + work['id'])
        source = {'id': SOURCE, 'url': url, 'publisher': PUBLISHERS[parsed.netloc],
                  'locator': row['locator'], 'checkedOn': row['checkedOn'],
                  'scope': 'work_specific_context_and_analysis' if row['looking'] else 'work_specific_context'}
        if row['url']:
            source.update({k: row['proof'][k] for k in ('snapshot', 'evidenceSha256', 'method')})
            if row['proof'].get('supportingReferences'):
                source['supportingReferences'] = row['proof']['supportingReferences']
        else:
            record = next((s for s in work['sources'] if s['id'] != SOURCE
                           and s.get('url') == url and s.get('snapshot') and s.get('evidenceSha256')), None)
            if not record:
                raise ValueError('Missing exact source snapshot: ' + work['id'])
            source.update({k: record[k] for k in ('snapshot', 'evidenceSha256')})
            source['method'] = 'retained_museum_record'
        facts = work.get('facts', {})
        fields = [('年代', work.get('period')), ('材质与形制', facts.get('medium')),
                  ('尺寸', facts.get('dimensions')), ('馆藏编号', facts.get('accession'))]
        metadata = '\n'.join(f'{label}：{value}' for label, value in fields if value)
        overview = row['context'] + ('\n\n' + metadata if metadata else '')
        # Preserve explicit original-vs-rubbing caveats.
        if facts.get('reproduction') == 'rubbing':
            overview += '\n形制说明：拓本，原迹或碑刻的年代与拓印年代有别。'
        if len(overview.encode('utf-8')) >= 640:
            overview = row['context']  # Structured metadata remains on the record.
        for name, value, limit in [('brief', row['brief'], 255), ('overview', overview, 639),
                                   ('looking', row['looking'], 383)]:
            if len(value.encode('utf-8')) > limit:
                raise ValueError(f'{name} exceeds reader bounds: {work["id"]}')
        work['sources'] = [s for s in work['sources'] if s['id'] != SOURCE] + [source]
        work.update(brief=row['brief'], overview=overview,
                    fullLibraryReview={'checkedOn': row['checkedOn'], 'sourceId': SOURCE,
                                       **({'round': row['round']} if row['round'] > 1 else {})})
        work['coverage']['level'] = 'source_based_context'
        work['contentKinds']['overview'] = 'source_based_context'
        work.pop('backgroundBasis', None)
        work['contentKinds'].pop('background', None)
        if row['looking']:
            work['looking'] = row['looking']
            work['lookingBasis'] = {'scope': SCOPE, 'sourceIds': [SOURCE], 'checkedOn': row['checkedOn']}
            work['contentKinds']['looking'] = SCOPE
    if full and seen != set(edits):
        raise ValueError('Missing editorial identities: ' + ', '.join(sorted(set(edits) - seen)))
    return seen


def text_digest(text):
    return hashlib.sha256(text.encode('utf-8')).hexdigest()


def replaces_reviewed_text(old, new, review):
    if old == new:
        return True
    old_digest = text_digest(old['text'])
    scope = new['scope']
    if old_digest == review.get('correctsReadingSha256'):
        return old.get('scope') == scope and scope in OBJECT_SCOPES and bool(new['imageEvidence'])
    if old.get('scope') == 'artwork_main_body_excerpt' and old_digest == review.get('replacesExcerptSha256'):
        return scope == 'artwork_main_body_transcription' or (
            scope == 'literary_reference_excerpt_not_object_transcription'
            and bool(new['imageEvidence']) and bool(new.get('completeReferenceText')))
    return (scope == 'literary_reference_excerpt_not_object_transcription'
            and not old.get('completeReferenceText') and bool(new.get('completeReferenceText'))
            and all(old.get(key) == new.get(key) for key in
                    ('status', 'scope', 'text', 'sourceId', 'composition', 'imageEvidence')))


def apply_readings(works, reviews):
    seen = set()
    for work in works:
        key = work['id']
        if key not in reviews:
            continue
        review = reviews[key]
        entry = {k: v for k, v in review.items() if k not in REVIEW_FIELDS}
        scope, status = entry['scope'], entry['status']
        source_ids = {s['id'] for s in review['sources']}
        same_object_evidence = entry['imageEvidence'] or (
            scope == 'artwork_main_body_excerpt' and
            any(s['id'] == entry['sourceId'] and s['scope'] == MUSEUM_EXCERPT for s in review['sources']))
        if (work['category'] != 'calligraphy' or not entry['text'].strip() or not entry['explanation'].strip()
                or len(source_ids) != len(review['sources']) or entry['sourceId'] not in source_ids
                or not ((status == 'source_verified' and scope in OBJECT_SCOPES and same_object_evidence)
                        or (status == 'reference_verified' and scope in REFERENCE_SCOPES))):
            raise ValueError('Invalid reading source or scope: ' + key)
        complete = entry.get('completeReferenceText')
        if bool(complete) != bool(entry.get('completeReferenceTextSha256')) or (
                complete and (scope not in REFERENCE_SCOPES
                              or text_digest(complete) != entry['completeReferenceTextSha256'])):
            raise ValueError('Invalid complete reference text: ' + key)
        old = work['transcription']
        if old['status'] in READABLE and not replaces_reviewed_text(old, entry, review):
            raise ValueError('Would replace a reviewed reading text without matching evidence: ' + key)
        work['transcription'] = entry
        if review.get('readerTitle'):
            work['title'] = review['readerTitle']
        work['sources'] = [s for s in work['sources'] if s['id'] not in source_ids] + review['sources']
        seen.add(key)
    if seen != reviews.keys():
        raise ValueError('Unknown reviewed readings: ' + ', '.join(sorted(reviews.keys() - seen)))


@lru_cache(maxsize=None)
def file_digest(path):
    with path.open('rb') as handle:
        return hashlib.file_digest(handle, 'sha256').hexdigest()


def verify_reading_evidence(reviews, snapshots, roots):
    assets = defaultdict(list)
    wanted = {proof['asset'] for review in reviews.values() for proof in review['imageEvidence']}
    for root in roots:
        for path in root.rglob('*'):
            if path.name in wanted and path.is_file() and root in path.resolve().parents:
                assets[path.name].append(path)
    count = 0
    for key, review in reviews.items():
        if review['status'] == 'source_verified' and not review['imageEvidence']:
            source = next(s for s in review['sources'] if s['id'] == review['sourceId'])
            path = (snapshots / source['snapshot']).resolve()
            if (snapshots not in path.parents or source['scope'] != MUSEUM_EXCERPT
                    or not source.get('locator') or review['text'] not in path.read_text(encoding='utf-8-sig')):
                raise ValueError('Museum excerpt missing from source: ' + key)
        for proof in review['imageEvidence']:
            if not any(file_digest(path) == proof['evidenceSha256'] for path in assets[proof['asset']]):
                raise ValueError(f'Missing or changed reading image for {key}: {proof["asset"]}')
            count += 1
    return count


def summarize(works):
    calligraphy = [w['transcription'] for w in works if w['category'] == 'calligraphy']
    statuses = Counter(t['status'] for t in calligraphy)
    looking = sum(bool(w['looking']) for w in works)
    return {
        'totalWorks': len(works),
        'categories': dict(sorted(Counter(w['category'] for w in works).items())),
        'authoritativeLooking': looking,
        'omittedLooking': len(works) - looking,
        'transcriptions': dict(statuses),
        'readableCalligraphy': sum(statuses[s] for s in READABLE),
        'missingCalligraphyReading': sum(statuses.values()) - sum(statuses[s] for s in READABLE),
    }


def refresh_coverage(report, works, annual, applied):
    report.update(summarize(works))
    report['coverage'] = dict(sorted(Counter(w['coverage']['level'] for w in works).items()))
    gaps = []
    for work in works:
        key = work['id']
        status = work['transcription']['status']
        if work['category'] == 'calligraphy' and status not in READABLE:
            gaps.append({'id': key, 'field': 'transcription', 'status': status})
        if work['coverage']['level'] == 'verified_metadata':
            gaps.append({'id': key, 'field': 'context', 'status': 'no_reviewed_work_specific_context'})
        if not work['period']:
            gaps.append({'id': key, 'field': 'date', 'status': 'not_recorded'})
    report['gaps'] = gaps
    report['missingFields'] = dict(sorted(Counter(g['field'] for g in gaps).items()))
    report['fullLibraryEditorialRevision'] = max(w['fullLibraryReview']['checkedOn'] for w in works if w['id'] in applied)
    report['fullLibraryEditorialWorks'] = len(applied)
    identities = {(w['category'], w['workId']) for w in works}
    annual_only = [w for w in annual if (w['category'], w['workId']) not in identities]
    report['libraryIncludingAnnualOnly'] = {
        'annualOnlyWorks': len(annual_only), **summarize(works + annual_only),
    }


def encode(value):
    return json.dumps(value, ensure_ascii=False, indent=2) + '\n'


def from_git(base, name):
    path = (GUIDES / name).relative_to(ROOT).as_posix()
    return subprocess.check_output(['git', 'show', f'{base}:{path}'], cwd=ROOT).decode('utf-8-sig')


def sync(base, output, evidence_root, image_roots):
    from build_art_guides import verify_sources

    base = subprocess.check_output(['git', 'rev-parse', '--verify', '--end-of-options', f'{base}^{{commit}}'],
                                   cwd=ROOT, text=True).strip()
    edits = load_edits()
    names = ('catalog.zh.json', 'annual-materials-2026-10-12.zh.json',
             'coverage.json', 'annual-materials-2026-10-12.html')
    catalog, annual, coverage = (json.loads(from_git(base, name)) for name in names[:3])
    html = from_git(base, names[3])
    match = re.search(r'(<script id="dataset" type="application/json">)(.*?)(</script>)', html, re.S)
    if not match:
        raise ValueError('Missing annual reader dataset')
    view = json.loads(match[2])
    for dataset in (catalog, annual, view):
        rows = dataset['works']
        if (len({w['id'] for w in rows}) != len(rows)
                or len({(w['category'], w['workId']) for w in rows}) != len(rows)):
            raise ValueError('Duplicate guide identity')
    if [w['id'] for w in annual['works']] != [w['id'] for w in view['works']]:
        raise ValueError('Annual reader identities differ from annual guide data')
    applied = apply_edits(catalog['works'], edits, full=True)
    annual_applied = apply_edits(annual['works'], edits)
    apply_edits(view['works'], edits)
    previous_readings = json.loads(from_git(base, READINGS))['works']
    readings = json.loads((GUIDES / READINGS).read_text(encoding='utf-8-sig'))['works']
    if previous_readings.keys() - readings.keys():
        raise ValueError('Reviewed readings were removed from the index')
    changed = {key: review for key, review in readings.items() if review != previous_readings.get(key)}
    apply_readings(catalog['works'], changed)
    coverage['verifiedEvidenceRecords'] = verify_sources(catalog['works'], evidence_root)
    image_count = verify_reading_evidence(changed, evidence_root, image_roots)
    verify_sources([w for w in annual['works'] if w['id'] in annual_applied], evidence_root,
                   source_id=SOURCE)
    for data in (catalog, coverage):
        data['originalTextsRevision'] = max(review.get('checkedOn', data['originalTextsRevision'])
                                            for review in readings.values())
    coverage['verifiedReadingImages'] = image_count
    coverage['reconciledReadingScopes'] = dict(sorted(Counter(review['scope'] for review in changed.values()).items()))
    refresh_coverage(coverage, catalog['works'], annual['works'], applied)
    payload = encode(view).replace('</', '<\\/')
    rendered = html[:match.start(2)] + payload + html[match.end(2):]
    output.mkdir(parents=True, exist_ok=True)
    for name, content in zip(names, (encode(catalog), encode(annual), encode(coverage), rendered)):
        (output / name).write_text(content, encoding='utf-8', newline='\n')
    print(f'Generated {len(applied)} edited guides, {len(annual_applied)} annual updates, and {len(changed)} '
          f'changed readings. Verified {coverage["verifiedEvidenceRecords"]} catalogue source records '
          f'and {image_count} reading-image references.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--base-ref', required=True, help='Git commit with the unedited guide datasets')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--evidence', type=Path, default=ROOT / 'artifacts/development/art-guides-full')
    parser.add_argument('--image-root', type=Path, action='append',
                        help='Local image evidence root. May be supplied multiple times.')
    args = parser.parse_args()
    image_roots = args.image_root or [
        args.evidence, ROOT / 'artifacts/development/art-guides-full-20261111',
        ROOT / 'artifacts/development/tab5-daily-art',
    ]
    sync(args.base_ref, args.output.resolve(), args.evidence.resolve(), [p.resolve() for p in image_roots])
