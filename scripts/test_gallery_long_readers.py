"""Verify annual complete text survives packaging, with bounded UTF-8 pages."""
import copy
import argparse
import hashlib
import json
from pathlib import Path
from package_daily_art import ROOT, select_guides, text_pages


def run(output=None):
    document = json.loads((ROOT / 'docs/art-guides/annual-materials-2026-10-12.zh.json').read_text(encoding='utf-8-sig'))
    rows = [row for row in document['works'] if len(row.get('transcription', {}).get('completeReferenceText', '').encode()) >= 8192]
    assert len(rows) == 7
    # Text-transport verification is independent of appreciation-source review.
    for row in rows:
        row['looking'] = ''
    works = [{**row, 'frames': ['fixture.jpg'], 'source': row['sources'][0]['url'], 'license': 'CC0'} for row in rows]
    packaged = select_guides(document, works, '2026.10.12')
    proof = []
    native = []
    for source, row in zip(rows, packaged['works']):
        original = row['transcription']
        full = original['completeReferenceText']
        assert full == source['transcription']['completeReferenceText']
        assert hashlib.sha256(full.encode()).hexdigest() == original['completeReferenceTextSha256']
        pages = text_pages(full)
        assert ''.join(pages) == full and all(len(page.encode()) <= 4095 for page in pages)
        assert not original.get('completeReferenceFile')
        proof.append({'id': row['id'], 'bytes': len(full.encode()), 'pages': len(pages), 'sha256': original['completeReferenceTextSha256']})
        reader = {key: row.get(key, '') for key in ('workId', 'title', 'author', 'period', 'museum', 'brief', 'overview', 'looking')}
        reader.update(transcription=pages[0], explanation=original['explanation'], transcriptionPages=len(pages), transcriptionSha256=original['completeReferenceTextSha256'])
        native.append({'id': row['id'], 'date': f'2026-01-{len(native)+1:02d}', 'guide': reader, 'fullText': full,
                       'pages': [{'workId': row['workId'], 'page': index, 'pages': len(pages), 'sha256': original['completeReferenceTextSha256'],
                                  'pageSha256': hashlib.sha256(page.encode()).hexdigest(), 'text': page} for index, page in enumerate(pages)]})
    bad = copy.deepcopy(packaged)
    bad['works'][0]['transcription']['completeReferenceTextSha256'] = '0' * 64
    try:
        select_guides(bad, works, '2026.10.12')
        raise AssertionError('Changed complete text accepted')
    except ValueError:
        pass
    unreviewed = copy.deepcopy(packaged)
    unreviewed['works'][0]['transcription']['status'] = 'withheld_pending_review'
    assert 'completeReferenceText' not in select_guides(unreviewed, works, '2026.10.12')['works'][0]['transcription']
    hostile = '<>&"\'+`\\\n𠀀文' * 1000
    assert ''.join(text_pages(hostile)) == hostile
    if output:
        output.mkdir(parents=True, exist_ok=False)
        (output / 'native-source-preflight.json').write_text(json.dumps(native, ensure_ascii=False), encoding='utf-8')
        (output / 'verification.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
    print('ANNUAL_LONG_TEXT_PACKAGER_OK ' + json.dumps(proof))


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,help='Optional source preflight fixture; final acceptance uses the production imported-pack fixture')
    run(parser.parse_args().output)
