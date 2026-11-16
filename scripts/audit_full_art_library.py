"""Index the entire guide library and match official source material by work identity."""
import argparse
import csv
import hashlib
import json
import re
from collections import Counter
from pathlib import Path
from urllib.parse import urlparse


ROOT = Path(__file__).resolve().parents[1]
GUIDES = ROOT / 'docs/art-guides'
TEXT_FIELDS = ('description', 'did_you_know', 'artlens_description', 'early_education_description')
READABLE = {'source_verified', 'reference_verified'}


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest()


def write(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def retained_calligraphy_fields(path):
    record = read(path)
    if not isinstance(record, dict):
        return []
    fields = []
    for field in ('description', 'descriptions', 'text', 'meibun', 'inscription', 'inscriptions'):
        value = record.get(field)
        if value and len(str(value).strip()) >= 40:
            fields.append(field)
    return fields


def audit(output, snapshot_root, dataset_dir=None, editorial_round=None):
    catalog = read(GUIDES / 'catalog.zh.json')['works']
    annual = read(GUIDES / 'annual-materials-2026-10-12.zh.json')['works']
    index = {(w['category'], w['workId']): w for w in catalog}
    if len(index) != len(catalog):
        raise ValueError('Duplicate work identity in catalogue')
    annual_ids = {(w['category'], w['workId']) for w in annual}
    if len(annual_ids) != len(annual):
        raise ValueError('Duplicate work identity in annual selection')
    annex = [w for w in annual if (w['category'], w['workId']) not in index]
    dataset_dir = dataset_dir or output
    bulk = dataset_dir / 'cma-current.csv'
    url, expected = (dataset_dir / 'cma-current.source.txt').read_text(encoding='utf-8').splitlines()
    if not re.fullmatch(r'https://media\.githubusercontent\.com/media/ClevelandMuseumArt/openaccess/[0-9a-f]{40}/data\.csv', url):
        raise ValueError('Expected a pinned Cleveland Museum of Art dataset')
    if digest(bulk) != expected:
        raise ValueError('Cleveland dataset checksum mismatch')
    targets = {}
    for w in catalog:
        museum_url = w['sources'][0]['url']
        if urlparse(museum_url).hostname in {'clevelandart.org', 'www.clevelandart.org'}:
            targets[museum_url.rstrip('/').rsplit('/', 1)[-1]] = w
    csv.field_size_limit(10_000_000)
    cma = {}
    with bulk.open(encoding='utf-8-sig', newline='') as f:
        for r in csv.DictReader(f):
            accession = r['accession_number']
            if accession in targets:
                if accession in cma:
                    raise ValueError('Duplicate accession in official dataset: ' + accession)
                cma[accession] = r
    decisions = read(GUIDES / 'transcription-review-decisions.zh.json')['works']
    evidence, rows, counts, updated = [], [], Counter(), []
    for w in catalog + annex:
        category, work_id = w['category'], w['workId']
        in_annual = (category, work_id) in annual_ids
        archived = (category, work_id) not in index
        key = w['sources'][0]
        museum_url = key['url']
        lookup = museum_url.rstrip('/').rsplit('/', 1)[-1]
        facts = w.get('facts') or {}
        text_fields = []
        checked_cma = False
        if lookup in targets and targets[lookup]['workId'] == work_id:
            r = cma.get(lookup)
            checked_cma = r is not None
            if r is not None:
                if facts.get('accession') and facts['accession'] != lookup:
                    raise ValueError('Accession does not match guide: ' + work_id)
                texts = {f: r[f].strip() for f in TEXT_FIELDS if r[f].strip()}
                text_fields = list(texts)
                if texts:
                    evidence.append({'workId': work_id, 'id': w['id'], 'accession': lookup,
                                     'objectId': r['id'], 'museumUrl': museum_url,
                                     'museumTitle': r['title'], 'fields': texts})
        snapshot = key.get('snapshot')
        frozen = False
        calligraphy_fields = []
        if snapshot:
            path = (snapshot_root / snapshot.replace('\\', '/')).resolve()
            frozen = (snapshot_root in path.parents and path.is_file()
                      and digest(path) == key.get('evidenceSha256'))
            if frozen and category == 'calligraphy' and path.suffix == '.json':
                calligraphy_fields = retained_calligraphy_fields(path)
        overview = w.get('coverage', {}).get('level') == 'source_based_context' or bool(w.get('backgroundBasis'))
        looking = bool(w.get('looking'))
        original = w.get('transcription', {}).get('status', 'not_available') if category == 'calligraphy' else None
        editorial = bool(w.get('fullLibraryReview'))
        review_round = w.get('fullLibraryReview', {}).get('round', 1) if editorial else None
        in_requested_round = editorial and (editorial_round is None or review_round == editorial_round)
        if in_requested_round:
            updated.append((w, in_annual))
        scope = w.get('transcription', {}).get('scope')
        canonical_reference = scope == 'literary_reference_excerpt_not_object_transcription'
        reference_range = canonical_reference and bool(w['transcription'].get('completeReferenceText'))
        reference_excerpt = canonical_reference and not reference_range
        translation_reference = scope == 'museum_translation_paraphrase_not_object_transcription'
        object_excerpt = scope == 'artwork_main_body_excerpt'
        weak_candidate = (looking and not editorial
                          and (len(w['looking']) <= 60 or bool(re.search(
                              r'从整体.*细节|回到整体|从局部.*整体|结合年代和材质|体会这件作品', w['looking']))))
        gaps = [name for name, present in [('background', overview), ('analysis', looking)] if not present]
        if original is not None and original not in READABLE:
            gaps.append('readable_text')
        elif canonical_reference or translation_reference or object_excerpt:
            gaps.append('complete_text_or_object_collation')
        counts[(category, 'total')] += 1
        for name, present in [('annual', in_annual), ('annex', archived), ('background', overview),
                              ('analysis', looking), ('readable_text', original in READABLE),
                              ('explicit_reference_excerpt', reference_excerpt),
                              ('museum_translation_reference', translation_reference),
                              ('complete_canonical_reference_range', reference_range),
                              ('explicit_object_excerpt', object_excerpt),
                              ('this_revision', in_requested_round), ('cma_match', checked_cma),
                              ('nonannual_this_revision', in_requested_round and not in_annual),
                              ('existing_analysis_review_candidate', weak_candidate),
                              ('cma_has_text_candidate', bool(text_fields)),
                              ('cma_text_without_analysis', bool(text_fields) and not looking),
                              ('base_snapshot_verified', frozen)]:
            counts[(category, name)] += int(present)
        rows.append({'id': w['id'], 'workId': work_id, 'category': category, 'title': w['title'],
                     'author': w['author'], 'annual': in_annual, 'annex': archived,
                     'museumUrl': museum_url, 'museumRecordHashVerified': frozen,
                     'cmaDatasetMatched': checked_cma, 'cmaTextFields': text_fields,
                     'retainedCalligraphyTextFields': calligraphy_fields,
                     'previousOriginalReviewDecision': decisions.get(w['id'], {}).get('action'),
                     'existingBackground': overview, 'existingAnalysis': looking,
                     'transcriptionStatus': original, 'updatedInThisRevision': in_requested_round,
                     'editorialRound': review_round, 'existingAnalysisReviewCandidate': weak_candidate,
                     'openFields': gaps})
    checked_on = max((w['fullLibraryReview']['checkedOn'] for w, _ in updated),
                     default='2026-11-11')
    report = {
        'scope': 'Current main catalogue plus distinct work identities in the annual archive',
        'checkedOn': checked_on,
        'editorialRound': editorial_round,
        'cmaDataset': {'url': url, 'sha256': expected, 'license': 'CC0'},
        'categories': {c: {k: v for (category, k), v in counts.items() if category == c}
                       for c in ('painting', 'calligraphy')},
        'limitations': ['Museum text fields are candidates, not approved Chinese guides.',
                        'An empty field does not establish that no authority has written about the work.',
                        'Existing analysis means nonempty existing reviewed text, not a fresh quality review.',
                        'A review candidate is a text-length or phrase heuristic and does not establish low quality.'],
    }
    write(output / 'full-library-audit.json', {**report, 'works': rows})
    write(output / 'cma-work-specific-source-material.json',
          {'source': report['cmaDataset'], 'works': evidence})
    write_review(output / '本次新增与改写导读.md', updated, checked_on)
    print(json.dumps(report, ensure_ascii=False, indent=2))


def write_review(path, updated, checked_on):
    lines = ['# 书画导读：本次新增与改写', '',
             f'{checked_on}。收录本轮写入的年度内外作品。', '']
    for category, label in [('painting', '绘画'), ('calligraphy', '书法')]:
        group = [(w, annual) for w, annual in updated if w['category'] == category]
        lines += [f'## {label}（{len(group)}件）', '']
        for w, annual in group:
            revision_source = w['fullLibraryReview']['sourceId']
            source = next(s for s in w['sources'] if s['id'] == revision_source)
            lines += [f'### {w["title"]}', '',
                      f'{w["author"]} · {w["museum"]} · {w["id"]} · ' + ('年度入选' if annual else '年度外'), '',
                      w['overview'], '']
            if w.get('looking'):
                lines += ['**赏析**', '', w['looking'], '']
            t = w['transcription']
            if t.get('checkedOn') == w['fullLibraryReview']['checkedOn'] and t.get('status') in READABLE:
                ref = next(s for s in w['sources'] if s['id'] == t['sourceId'])
                fixed = f' · [固定版本原档]({ref["dataUrl"]})' if ref.get('dataUrl') else ''
                if (t.get('scope') == 'literary_reference_excerpt_not_object_transcription'
                        and t.get('completeReferenceText')):
                    suffix = '-reference.txt' if w['fullLibraryReview'].get('round') == 10 else '-cbeta.txt'
                    text_file = path.with_name(w['id'] + suffix)
                    text_file.write_text(t['completeReferenceText'], encoding='utf-8', newline='\n')
                    lines += [t['text'], '', f'[同文版本对应范围全文]({text_file.name})', '']
                else:
                    lines += [t['text'], '']
                lines += [t['explanation'], '', f'[文字来源]({ref["url"]})' + fixed, '']
            lines += [f'[{source["publisher"]}]({source["url"]}) · {source["locator"]}', '']
            for reference in source.get('supportingReferences', []):
                lines += [f'[{reference["title"]}]({reference["url"]}) · {reference["locator"]}', '']
    path.write_text('\n'.join(lines), encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dataset-dir', type=Path)
    parser.add_argument('--round', type=int)
    parser.add_argument('--snapshots', type=Path, default=ROOT / 'artifacts/development/art-guides-full')
    args = parser.parse_args()
    audit(args.output.resolve(), args.snapshots.resolve(), args.dataset_dir, args.round)
