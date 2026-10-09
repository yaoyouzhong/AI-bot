"""Build a locked 366 + 366 editorial edition without changing the full library.

The checked-in selection is explicit. Rebuilds never silently replace works with
different candidates. Native image bytes, original page order and attribution
qualifiers are retained; guide material remains a separate review-only artifact.
"""
import argparse
from collections import Counter
import csv
import hashlib
import html
import json
from pathlib import Path
import shutil

from package_daily_art import package
from verify_daily_art_year import verify

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'windows-app/AIBotBridge/Assets/DailyArt'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def sha(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def build(manifest_path, output):
    manifest = read(manifest_path)
    if output.exists():
        raise ValueError('Choose a new output directory; previous editions are preserved')
    source_hashes = {name: sha(ASSETS / name) for name in
                     ('catalog.json', 'selection.json', 'provenance.json', 'files.json')}
    if source_hashes != manifest['sourceMetadataSha256']:
        raise ValueError('Full-library metadata differs from the reviewed selection baseline')
    catalog = read(ASSETS / 'catalog.json')
    by_id = {w['id']: w for w in catalog}
    rows = manifest['works']
    ids = {r['id'] for r in rows}
    if len(ids) != 732 or Counter(r['category'] for r in rows) != {'painting': 366, 'calligraphy': 366}:
        raise ValueError('Annual edition requires exactly 366 distinct works per category')
    if not ids <= by_id.keys():
        raise ValueError('Selected work missing from full library')
    for row in rows:
        work = by_id[row['id']]
        if any(row[k] != work[k] for k in ('category', 'title', 'author', 'source', 'museum')):
            raise ValueError('Selected identity or attribution changed: ' + row['id'])
        if row['pages'] != len(work['frames']):
            raise ValueError('Page count changed: ' + row['id'])
    if any(r['region'] != '中国' for r in rows if r['category'] == 'calligraphy'):
        raise ValueError('This edition contains only source-confirmed Chinese calligraphy')
    # The reviewed manifest locks the annual sequence as well as work identities.
    # Editing a viewing label must not silently change the daily rotation.
    ordered = rows
    chosen = [by_id[r['id']] for r in ordered]
    names = {n for w in chosen for n in w['frames'] + w['portraitFrames']}
    inventory = read(ASSETS / 'files.json')['files']
    output.mkdir(parents=True)
    stage = output / 'gallery'
    stage.mkdir()
    for name in sorted(names):
        if name != Path(name).name or sha(ASSETS / name) != inventory[name]:
            raise ValueError('Unsafe or changed native frame: ' + name)
        shutil.copyfile(ASSETS / name, stage / name)
    write(stage / 'catalog.json', chosen)
    for name in ('selection.json', 'provenance.json'):
        write(stage / name, [w for w in read(ASSETS / name) if w['id'] in ids])
    write(stage / 'files.json', {'count': len(names), 'files': {n: inventory[n] for n in sorted(names)}})
    annual = verify(stage)
    write(output / 'annual-validation.json', annual)
    if not annual['ready']:
        raise ValueError(annual['errors'])
    print('CURATED_ANNUAL_OK 366 paintings + 366 Chinese calligraphy works', flush=True)
    package(stage, output / 'packs', manifest['version'])
    packs = read(output / 'packs/packs.json')
    for info in packs:
        old = output / 'packs' / info['file']
        target = old.with_name(old.name.replace('-' + manifest['version'], '-Curated-' + manifest['version']))
        old.rename(target)
        old.with_name(old.name + '.sha256').rename(target.with_name(target.name + '.sha256'))
        target.with_name(target.name + '.sha256').write_text(info['sha256'] + '  ' + target.name + '\n', encoding='ascii')
        info['file'] = target.name
        info['edition'] = 'curated-annual'
    write(output / 'packs/packs.json', packs)
    guides = read(ROOT / 'docs/art-guides/catalog.zh.json')
    guide_by_id = {w['id']: w for w in guides['works']}
    guides['edition'] = 'curated-' + manifest['version']
    guides['works'] = [guide_by_id[w['id']] for w in chosen]
    write(output / 'guides.zh.json', guides)
    write(output / 'selection.json', dict(manifest, works=ordered))
    with (output / 'selection.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        fields = ['category', 'id', 'title', 'author', 'region', 'theme', 'script', 'pages', 'museum', 'source', 'reason']
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction='ignore')
        writer.writeheader()
        writer.writerows(ordered)
    cards = []
    for row in ordered:
        work = by_id[row['id']]
        esc = lambda value: html.escape(str(value), quote=True)
        images = ''.join(f'<img loading="lazy" src="gallery/{esc(n)}" alt="{esc(work["title"])} 第{i+1}页">'
                         for i, n in enumerate(work['frames']))
        guide = guide_by_id[row['id']]
        cards.append(f'<article data-category="{row["category"]}" data-search="{esc(row["title"]+" "+row["author"]+" "+row["theme"])}">'
                     f'<h2>{esc(row["title"])}</h2><p>{esc(row["author"])} · {esc(row["museum"])}</p>'
                     f'<p>{esc(row["region"])} · {esc(row["theme"] if row["category"]=="painting" else (row["script"] if row["script"]!="馆藏未细分书体" else "书法"))} · {row["pages"]}页</p>'
                     f'<details><summary>查看作品与导读</summary>{images}<p>{esc(guide["brief"])}</p>'
                     f'<p>{esc(guide["overview"])}</p><p>{esc(guide["looking"])}</p>'
                     f'<a href="{esc(work["source"])}" target="_blank" rel="noopener">馆藏来源</a></details></article>')
    (output / 'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>名画与书法 · 年度精选</title><style>body{margin:0;background:#f3eee4;color:#29251f;font:16px/1.8 system-ui}main{max-width:1100px;margin:auto;padding:40px 24px}h1{font-size:36px;font-weight:500}h2{font-size:20px;font-weight:500}header{position:sticky;top:0;background:#f3eee4ed;padding:12px 0;backdrop-filter:blur(8px)}input,select{padding:10px;margin-right:12px;border:1px solid #cabfae;border-radius:4px;background:#fffaf1}section{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px}article{border-top:1px solid #cbbfae;padding:12px}p{margin:6px 0}summary,a{color:#826242;cursor:pointer}img{display:block;width:100%;height:auto;margin:10px 0}[hidden]{display:none!important}</style><main>
<h1>名画与书法 · 年度精选</h1><p>绘画366件 · 中国书法366件。完整保留入选作品分页与横竖排版。</p>
<header><select id="category"><option value="">全部作品</option><option value="painting">绘画</option><option value="calligraphy">书法</option></select><input id="search" placeholder="搜索作品、作者或题材"><span id="count">732件</span></header><section>'''
        + ''.join(cards) + '''</section></main><script>const c=document.querySelector('#category'),q=document.querySelector('#search');function filter(){let n=0;document.querySelectorAll('article').forEach(a=>{a.hidden=!!((c.value&&a.dataset.category!==c.value)||!a.dataset.search.toLowerCase().includes(q.value.trim().toLowerCase()));if(!a.hidden)n++});document.querySelector('#count').textContent=n+'件'}c.onchange=filter;q.oninput=filter;</script></html>''', encoding='utf-8')
    summary = {'status': 'passed', 'edition': manifest['version'], 'counts': {}, 'packs': packs,
               'fullLibraryUnchanged': source_hashes == {n: sha(ASSETS / n) for n in source_hashes},
               'nativeImagesUnchanged': True, 'guideStatus': 'preview_only', 'hardwareVerified': False,
               'selectionManifestSha256': sha(manifest_path)}
    for category in ('painting', 'calligraphy'):
        group = [r for r in rows if r['category'] == category]
        summary['counts'][category] = {'works': len(group), 'pages': sum(r['pages'] for r in group),
            'regions': dict(Counter(r['region'] for r in group)),
            'pageDistribution': dict(sorted(Counter(r['pages'] for r in group).items())),
            'subjectsOrScripts': dict(Counter(r['theme'] if category == 'painting' else r['script'] for r in group))}
    if not summary['fullLibraryUnchanged']:
        raise ValueError('Full library changed during edition build')
    write(output / 'validation.json', summary)
    print(json.dumps(summary, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--selection', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    build(args.selection, args.output)
