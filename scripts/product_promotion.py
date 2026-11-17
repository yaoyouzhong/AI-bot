"""Publish accepted components through one independently numbered product release."""
from __future__ import annotations

import hashlib
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

import release_pipeline as pipeline
from artifact_names import ESP_FLASH_TEMPLATE, UPDATE_ALIASES, names
from product_release import PRODUCT_ONLY_MARKER, next_product_version, plan_identity, render_product, valid_product_tag

LABELS = {'windows': 'Windows 安装器', 'macos': 'Mac 应用', 'esp8266': 'ESP8266 固件',
          'tab5-first': 'TAB5 首次安装', 'tab5-upgrade': 'TAB5 升级'}


def component_plan(entry):
    directory = Path(entry['directory'])
    candidate, acceptance, pending = pipeline.readiness(directory, entry['acceptance'], expected_source=entry.get('expected_source'))
    allowed = entry.get('allow_deferred') and all(acceptance['checks'].get(c, {}).get('result') == 'deferred' and acceptance['checks'][c].get('reason', '').strip() for c in pending)
    if pending and not allowed:
        raise ValueError('Pending or failed acceptance: ' + ', '.join(pending))
    if candidate['component'] == 'bridge':
        candidate = {**candidate, 'tag': 'bridge-v' + candidate['version']}
    build = pipeline.read(directory / 'BUILD.json')
    bridge = bool(entry.get('minimal_bridge_assets'))
    tab5 = bool(entry.get('minimal_tab5_assets'))
    available = pipeline.publication_assets(candidate, build, minimal_bridge_assets=bridge, minimal_tab5_assets=tab5)
    downloads, sources = [], {}
    for key in ('windows', 'macos', 'esp8266', 'tab5-first', 'tab5-upgrade'):
        filenames = names(key, candidate['version'])
        name = filenames[0]
        if name in available:
            downloads.append({'name': name, 'label': LABELS[key]})
            for filename in filenames if key in UPDATE_ALIASES else (name,):
                sources[filename] = available[filename]
    if candidate['component'] == 'esp8266':
        flash = ESP_FLASH_TEMPLATE.format(version=candidate['version'])
        if flash in available:
            downloads.insert(0, {'name': flash, 'label': 'ESP8266 精简刷机包'})
            sources.update({flash: available[flash], flash + '.sha256': available[flash + '.sha256']})
    if build.get('tab5Source') and not tab5:
        source = build['tab5Source']['file']
        downloads.append({'name': source, 'label': 'TAB5 对应源码'})
        sources[source] = available[source]
    return {'candidate': candidate, 'downloads': downloads,
            'notes': (directory / 'RELEASE-NOTES.md').read_text(encoding='utf-8-sig'),
            'directory': directory, 'sources': sources,
            'minimal_bridge_assets': bridge, 'minimal_tab5_assets': tab5}


def public_releases(repository):
    result = []
    for page in range(1, 11):
        rows = pipeline.gh_json(['api', f'repos/{repository}/releases?per_page=100&page={page}'])
        if not isinstance(rows, list): raise ValueError('Invalid GitHub release list')
        result.extend(rows)
        if len(rows) < 100: return result
    raise ValueError('Too many releases to allocate a product version safely')


def product_tags():
    lines = pipeline.git('ls-remote', '--tags', 'origin', 'refs/tags/v*').splitlines()
    return sorted({line.split('\t', 1)[1].removeprefix('refs/tags/') for line in lines
                   if '\trefs/tags/v' in line and not line.endswith('^{}')})


def matching_product(releases, identity):
    marker = '<!-- aibot-product-release:' + identity + ' -->'
    matches = [r for r in releases if marker in (r.get('body') or '')]
    if len(matches) > 1: raise ValueError('The same component set has multiple product releases')
    return matches[0] if matches else None


def ensure_product_tag(repository, tag, identity):
    remote = pipeline.git('remote', 'get-url', 'origin').removesuffix('.git')
    if remote not in (f'https://github.com/{repository}', f'git@github.com:{repository}'):
        raise ValueError('Git remote and requested repository disagree')
    source = pipeline.sha(pipeline.git('rev-parse', 'HEAD'))
    remote_main = pipeline.git('ls-remote', 'origin', 'refs/heads/main').splitlines()
    if len(remote_main) != 1 or remote_main[0].split()[0] != source:
        raise ValueError('Product tag must use the exact published main commit')
    workflow = pipeline.git('show', source + ':.github/workflows/release.yml')
    if '# aibot-product-version-tags: 1' not in workflow:
        raise ValueError('Product tag source predates product-only tag validation')
    existing = pipeline.git('ls-remote', 'origin', 'refs/tags/' + tag, 'refs/tags/' + tag + '^{}')
    if existing:
        raise ValueError('Product tag already exists without a matching release; inspect its reservation')
    if pipeline.git('tag', '--list', tag):
        if (pipeline.git('cat-file', '-t', 'refs/tags/' + tag) != 'tag'
                or pipeline.git('rev-parse', tag + '^{}') != source
                or pipeline.git('for-each-ref', '--format=%(contents)', 'refs/tags/' + tag) != 'AI-bot product release ' + identity):
            raise ValueError('Local product tag differs from the accepted reservation')
    else:
        subprocess.run(['git', 'tag', '-a', tag, source, '-m', 'AI-bot product release ' + identity], cwd=pipeline.ROOT, check=True)
    subprocess.run(['git', 'push', 'origin', 'refs/tags/' + tag], cwd=pipeline.ROOT, check=True)


def orphan_matches(repository, tag, identity):
    """Recognize our own annotated tag if release creation was interrupted."""
    ref = pipeline.gh_json(['api', f'repos/{repository}/git/ref/tags/{tag}'])
    obj = ref.get('object', {})
    if obj.get('type') != 'tag': return False
    annotation = pipeline.gh_json(['api', f"repos/{repository}/git/tags/{obj['sha']}"])
    return (annotation.get('message', '').strip() == 'AI-bot product release ' + identity
            and annotation.get('object', {}).get('type') == 'commit')


def verify_product_release(release, product):
    if (release['tag_name'] != product['tag'] or release['name'] != product['title']
            or '<!-- aibot-product-release:' + product['identity'] + ' -->' not in (release.get('body') or '')
            or release['draft'] and (release.get('body') or '').replace('\r\n', '\n') != product['body']
            or release.get('prerelease')):
        raise ValueError('Existing product release differs from accepted component scope')


def reserve_product(repository, components, manifest):
    identity = plan_identity(components)
    releases = public_releases(repository)
    matching = matching_product(releases, identity)
    if matching:
        tag = matching['tag_name']
        if not valid_product_tag(tag): raise ValueError('Matching product release has an invalid tag')
        number = tuple(map(int, tag[1:].split('.')))
        if matching['draft'] and any(not r.get('draft') and not r.get('prerelease') and valid_product_tag(r.get('tag_name', '')) and tuple(map(int, r['tag_name'][1:].split('.'))) > number for r in releases):
            raise ValueError('A newer product release is already public; do not publish this older draft as Latest')
        if not orphan_matches(repository, tag, identity):
            raise ValueError('Product release tag does not match its accepted component reservation')
        product = render_product(repository, tag[1:], components)
        verify_product_release(matching, product)
        if matching['draft'] and {a['name'] for a in matching['assets']} - set(manifest):
            raise ValueError('Product draft contains assets outside the accepted scope')
        return product, matching
    tags = product_tags()
    outstanding = [r for r in releases if r.get('draft') and valid_product_tag(r.get('tag_name', ''))
                   and '<!-- aibot-product-release:' in (r.get('body') or '')]
    if outstanding:
        raise ValueError('Another product release draft is already reserved; finish or resolve it first')
    version = next_product_version(tags)
    # No accepted product may be silently renumbered after a tag-only partial failure.
    release_tags = {r.get('tag_name') for r in releases}
    stable_tags = [tag for tag in tags if valid_product_tag(tag)]
    if stable_tags:
        highest = max(stable_tags, key=lambda tag: tuple(map(int, tag[1:].split('.'))))
        if highest not in release_tags:
            if not orphan_matches(repository, highest, identity):
                raise ValueError('Latest product tag has no release; inspect the existing reservation before retrying')
            version = highest[1:]
    product = render_product(repository, version, components)
    if product['tag'] in release_tags:
        raise ValueError('An unrelated release already reserves the next product version')
    if product['tag'] not in tags:
        ensure_product_tag(repository, product['tag'], identity)
    with tempfile.TemporaryDirectory(prefix='aibot-product-release-') as directory:
        notes = Path(directory) / 'notes.md'
        notes.write_text(product['body'], encoding='utf-8', newline='\n')
        pipeline.run_cli(['release', 'create', product['tag'], '--repo', repository, '--verify-tag', '--draft', '--latest=false', '--title', product['title'], '--notes-file', str(notes)], timeout=120)
    release = pipeline.remote_identity(repository, product['tag'])
    verify_product_release(release, product)
    if not orphan_matches(repository, product['tag'], identity):
        raise ValueError('Product tag does not match its accepted component reservation')
    if not release['draft']: raise ValueError('New product release must initially be a draft')
    return product, release


def stage_product(components, output):
    """Copy only accepted install/update packages and create a product checksum list."""
    output = Path(output)
    output.mkdir(parents=True, exist_ok=True)
    selected = {}
    for item in components:
        candidate = item['candidate']
        for name, source_name in item['sources'].items():
            pipeline.safe_name(name)
            source = item['directory'] / source_name
            expected = candidate['files'][source_name]
            if name in selected or pipeline.file_record(source) != expected:
                raise ValueError('Product package duplicates or differs from accepted bytes: ' + name)
            selected[name] = (source, expected)
    if not selected: raise ValueError('Product release has no accepted install or update packages')
    if {p.name for p in output.iterdir()} - (set(selected) | {'SHA256SUMS.txt'}):
        raise ValueError('Unexpected product publication payload')
    for name, (source, expected) in selected.items():
        target = output / name
        if target.exists() and pipeline.file_record(target) != expected:
            raise ValueError('Existing product payload differs: ' + name)
        if not target.exists(): shutil.copyfile(source, target)
        if pipeline.file_record(target) != expected: raise ValueError('Copied product package differs: ' + name)
    sums = ''.join(f"{expected['sha256']}  {name}\n" for name, (_, expected) in sorted(selected.items()))
    checksum = output / 'SHA256SUMS.txt'
    if checksum.exists() and checksum.read_text(encoding='ascii').replace('\r\n', '\n') != sums:
        raise ValueError('Existing product checksum list differs')
    if not checksum.exists(): checksum.write_text(sums, encoding='ascii', newline='\n')
    return {p.name: pipeline.file_record(p) for p in output.iterdir()}


def historical_manifest(components, manifest):
    """Verify already-published product pages from the old, smaller asset layout."""
    filenames = {
        names(key, item['candidate']['version'])[0]
        for item in components
        for key in LABELS
    }
    expected = {name: record for name, record in manifest.items() if name in filenames}
    sums = ''.join(f"{record['sha256']}  {name}\n" for name, record in sorted(expected.items())).encode('ascii')
    return {**expected, 'SHA256SUMS.txt': {'bytes': len(sums), 'sha256': hashlib.sha256(sums).hexdigest()}}


def promote_product(entries, repository, *, execute=False, publish=False, commit_closeout=False, scope_reason=None):
    if not re.fullmatch(r'[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+', repository):
        raise ValueError('Invalid GitHub repository')
    if not entries: raise ValueError('Product release requires at least one component')
    if commit_closeout and not publish: raise ValueError('Committing closeout requires publication')
    components = [component_plan(entry) for entry in entries]
    candidates = [item['candidate'] for item in components]
    if len({candidate['tag'] for candidate in candidates}) != len(candidates):
        raise ValueError('Product release contains duplicate component tags')
    # Validate notes and exact scope before any tag, draft or upload is created.
    preview = render_product(repository, '0.0.0', components)
    scope = pipeline.require_release_scope(candidates, online=execute, reason=scope_reason) if publish else None
    if commit_closeout and execute: pipeline.check_closeout_workspace()
    report = {'execute': execute, 'publish': publish, 'identity': preview['identity'],
              'productVersion': 'resolved from existing product tags' if not execute else None,
              'components': [candidate['tag'] for candidate in candidates], 'releaseScope': scope}
    if not execute: return report
    payload = Path(entries[0]['directory']).resolve().parent / ('publication-product-' + preview['identity'][:16])
    manifest = stage_product(components, payload)
    product, release = reserve_product(repository, components, manifest)
    report.update(productVersion=product['tag'][1:], productTag=product['tag'], productTitle=product['title'])
    if release['draft']:
        for candidate in candidates:
            pipeline.ensure_tag(repository, candidate)
    release = pipeline.remote_identity(repository, product['tag'])
    verify_product_release(release, product)
    if release['draft']:
        if {a['name'] for a in release['assets']} - set(manifest):
            raise ValueError('Product draft contains assets outside the accepted scope')
        pipeline.upload(repository, product['tag'], payload, manifest,
                        payload.parent / (payload.name + '-upload-state.json'), workers=2, require_draft=True)
        release = pipeline.remote_identity(repository, product['tag'])
    if not release['draft'] and PRODUCT_ONLY_MARKER not in release['body']:
        manifest = historical_manifest(components, manifest)
    pipeline.verify_remote(release, manifest)
    if publish and release['draft']:
        pipeline.run_cli(['release', 'edit', product['tag'], '--repo', repository, '--draft=false', '--latest=true'], timeout=120)
        release = pipeline.remote_identity(repository, product['tag'])
        verify_product_release(release, product)
        pipeline.verify_remote(release, manifest)
        if release['draft']: raise ValueError('Product release remained a draft')
    report['published'] = not release['draft']
    report['url'] = release['html_url']
    try:
        if publish:
            report['closeout'] = pipeline.closeout(repository=repository, commit=commit_closeout)
    finally:
        pipeline.write(payload.parent / ('promotion-product-' + preview['identity'][:16] + '.json'), report)
    return report


def promote_product_set(plan, *, execute=False, publish=False, commit_closeout=False, allow_deferred=False, scope_reason=None):
    plan = Path(plan).resolve()
    data = pipeline.read(plan)
    if data.get('schemaVersion') != 1 or not data.get('candidates'):
        raise ValueError('Expected a nonempty release-set plan')
    entries = []
    for item in data['candidates']:
        entries.append({'directory': (plan.parent / item['directory']).resolve(),
                        'acceptance': (plan.parent / item['acceptance']).resolve(),
                        'expected_source': item.get('sourceCommit'), 'allow_deferred': allow_deferred,
                        'minimal_bridge_assets': item.get('minimalBridgeAssets', False),
                        'minimal_tab5_assets': item.get('minimalTab5Assets', False)})
    return promote_product(entries, data['repository'], execute=execute, publish=publish,
                           commit_closeout=commit_closeout, scope_reason=scope_reason)
