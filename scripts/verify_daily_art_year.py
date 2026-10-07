"""Release acceptance for a full year of distinct, reviewed daily artworks.

This is deliberately separate from transport self-tests: a working transfer of
20 sample images does not establish a complete annual collection.
"""
import argparse
import json
from collections import Counter
from pathlib import Path
from PIL import Image


def verify(directory: Path):
    catalog = json.loads((directory / 'catalog.json').read_text(encoding='utf-8'))
    provenance = {x['id']: x for x in json.loads((directory / 'provenance.json').read_text(encoding='utf-8'))}
    errors, counts = [], {}
    for category in ('painting', 'calligraphy'):
        works = [x for x in catalog if x['category'] == category]
        identities = [x.get('workId', x['source']) for x in works]
        count = len(set(identities))
        counts[category] = count
        if count < 366:
            errors.append(f'{category}: {count}/366 distinct works; missing {366-count}')
        if len(identities) != count:
            errors.append(f'{category}: duplicate work identity (detail views are not separate works)')
        if category == 'calligraphy':
            pages = [len(work['frames']) for work in works]
            if pages != sorted(pages):
                errors.append('calligraphy: daily catalog must prioritize fewer display frames')
        for work in works:
            for field in ('title', 'author', 'museum'):
                if not isinstance(work.get(field), str) or not work[field].strip():
                    errors.append(f"{work['id']}: missing {field}")
            for field, note in work.get('chineseNotes', {}).items():
                if field not in ('title', 'author'):
                    continue
                if note.get('status') == 'verified_published_usage':
                    if not note.get('value') or not note.get('sources') or any(
                        not s.get('url', '').startswith('https://') for s in note['sources']
                    ):
                        errors.append(f"{work['id']}: Chinese {field} note lacks name/source")
                elif note.get('status') != 'original_retained_not_verified' or note.get('value') is not None:
                    errors.append(f"{work['id']}: unsupported Chinese {field} note")
        for field in ('id', 'source'):
            duplicates = [value for value, n in Counter(x[field] for x in works).items() if n > 1]
            if duplicates:
                errors.append(f'{category}: repeated {field}: {duplicates}')
    hashes = {}
    for work in catalog:
        evidence = provenance.get(work['id'])
        if not evidence:
            errors.append(f"{work['id']}: missing provenance")
            continue
        for source in [evidence, *evidence.get('additionalSources', [])]:
            if min(source['displaySourceSize']) < 600:
                errors.append(f"{work['id']}: insufficient source detail")
            digest = source['originalSha256']
            if digest in hashes:
                errors.append(f"{work['id']}: same original as {hashes[digest]}")
            hashes[digest] = work['id']
        portrait = work.get('portraitFrames', [])
        if portrait and len(portrait) != len(work['frames']):
            errors.append(f"{work['id']}: portrait frame count differs")
        for name in [*work['frames'], *portrait]:
            if name != Path(name).name:
                errors.append(f"{work['id']}: invalid frame path")
                continue
            file = directory / name
            if not file.is_file():
                errors.append(f"{work['id']}: missing frame {name}")
                continue
            with Image.open(file) as frame:
                frame.load()
                if frame.size != (1280, 720):
                    errors.append(f"{work['id']}: frame size {frame.size}")
            if file.stat().st_size > 1048576:
                errors.append(f"{work['id']}: frame exceeds transfer limit")
    return {'ready': not errors, 'requiredPerCategory': 366,
            'distinctWorks': counts, 'errors': errors}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory', type=Path)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    result = verify(args.directory)
    output = json.dumps(result, ensure_ascii=False, indent=2)
    if args.report:
        args.report.write_text(output + '\n', encoding='utf-8')
    print(output)
    raise SystemExit(0 if result['ready'] else 1)
