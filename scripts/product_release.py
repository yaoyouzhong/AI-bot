"""Build one product release from independently accepted component releases."""
from __future__ import annotations

import hashlib
import json
import re


_PRODUCT_TAG = re.compile(r"v(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)\Z")
_REPOSITORY = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\Z")
_SAFE = re.compile(r"[A-Za-z0-9][A-Za-z0-9_.-]*\Z")


def valid_product_tag(tag):
    return bool(_PRODUCT_TAG.fullmatch(tag))


def next_product_version(tags):
    """Choose the next product patch version, including reserved product tags."""
    versions = [tuple(map(int, match.groups())) for tag in tags if (match := _PRODUCT_TAG.fullmatch(tag))]
    if not versions:
        raise ValueError("Cannot determine the latest product version from existing product tags")
    major, minor, patch = max(versions)
    return f"{major}.{minor}.{patch + 1}"


def plan_identity(components):
    """Bind the product page to the exact immutable candidate scopes."""
    identities = []
    for item in components:
        candidate = item["candidate"]
        identities.append({"candidateId": candidate["candidateId"],
                           "minimalBridgeAssets": bool(item.get("minimal_bridge_assets")),
                           "minimalTab5Assets": bool(item.get("minimal_tab5_assets"))})
    if not identities or len({item['candidateId'] for item in identities}) != len(identities):
        raise ValueError("Product release requires distinct accepted candidates")
    return hashlib.sha256(json.dumps(sorted(identities, key=lambda x: x['candidateId']), sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def split_notes(notes, candidate):
    """Preserve both languages while omitting each component's generic download intro."""
    notes = notes.replace('\r\n', '\n').strip()
    label = {'bridge': '桌面桥接', 'tab5': 'TAB5', 'esp8266': 'ESP8266'}[candidate['component']]
    heading = '## ' + label + ' ' + candidate['version']
    if '\n## English\n' in notes:
        zh, english = notes.split('\n## English\n', 1)
        found = re.search(r'^' + re.escape(heading) + r'[ \t]*$', zh, re.M)
        index = found.start() if found else -1
        if (index < 0 or (index and not zh.startswith('## 下载\n'))
                or re.search(r'^## ', zh[index + len(heading):], re.M)):
            raise ValueError('Component notes do not match the standard release template')
        chinese = zh[index:].strip()
    elif '\n### 中文更新说明\n' in notes and '\n### English release notes\n' in notes:
        _, rest = notes.split('\n### 中文更新说明\n', 1)
        zh, english = rest.split('\n### English release notes\n', 1)
        chinese = heading + '\n\n' + re.sub(r'^(#{4,}) ', lambda m: m.group(1)[1:] + ' ', zh.strip(), flags=re.M)
        english = re.sub(r'^(#{4,}) ', lambda m: m.group(1)[1:] + ' ', english.strip(), flags=re.M)
    else:
        raise ValueError('Product release requires component notes with Chinese and English sections')
    english = english.strip()
    if not english or not chinese: raise ValueError('Component notes must contain both languages')
    return chinese, english


def render_product(repository, version, components):
    if not _REPOSITORY.fullmatch(repository) or not _PRODUCT_TAG.fullmatch('v' + version):
        raise ValueError('Invalid repository or product version')
    identity = plan_identity(components)
    categories = set()
    downloads, zh_sections, en_sections = [], [], []
    labels = {'bridge': ('桌面桥接', 'Desktop bridge'), 'tab5': ('TAB5 固件', 'TAB5 firmware'), 'esp8266': ('ESP8266 固件', 'ESP8266 firmware')}
    for item in sorted(components, key=lambda item: ({'bridge': 0, 'tab5': 1, 'esp8266': 2}.get(item['candidate']['component'], 9), item['candidate']['candidateId'])):
        candidate = item['candidate']
        component, tag = candidate['component'], candidate['tag']
        if component not in labels or component in categories or not _SAFE.fullmatch(tag):
            raise ValueError('Product release contains a duplicate or invalid component')
        categories.add(component)
        assets = item['downloads']
        if not assets or any(not _SAFE.fullmatch(a['name']) for a in assets):
            raise ValueError('Product release requires known component download assets')
        prefix = f'https://github.com/{repository}/releases/download/v{version}/'
        links = ' · '.join(f"[{a['label']}]({prefix}{a['name']})" for a in assets)
        downloads.append(f"- {labels[component][0]} {candidate['version']}：{links} · [组件发布页](https://github.com/{repository}/releases/tag/{tag})")
        chinese, english = split_notes(item['notes'], candidate)
        zh_sections.append(chinese)
        nested = re.sub(r'^(#{2,5}) ', lambda m: '#' + m.group(1) + ' ', english, flags=re.M)
        en_sections.append(f"### {labels[component][1]} {candidate['version']}\n\n{nested}")
    marker = '<!-- aibot-product-release:' + identity + ' -->'
    body = ('## 下载\n\n' + '\n'.join(downloads) + '\n\n' + '\n\n'.join(zh_sections)
            + '\n\n## English\n\n' + '\n\n'.join(en_sections) + '\n\n' + marker + '\n')
    return {'tag': 'v' + version, 'title': 'AI-bot v' + version, 'body': body, 'identity': identity}
