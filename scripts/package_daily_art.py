"""Build independently installable artwork packs from the verified rendered catalog."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import zipfile

ROOT = Path(__file__).resolve().parents[1]
LICENSES = {
    "CC0": "https://creativecommons.org/publicdomain/zero/1.0/",
    "CC BY 4.0": "https://creativecommons.org/licenses/by/4.0/",
    "Public domain": "https://creativecommons.org/publicdomain/mark/1.0/",
}


def encode(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def digest(data):
    return hashlib.sha256(data).hexdigest()


def text_pages(text):
    pages, current, used, wire_used = [], [], 0, 0
    for char in text:
        size = len(char.encode("utf-8"))
        encoded = json.dumps(char,ensure_ascii=False)[1:-1]
        if char in '<>&\'+`"' or ord(char)>0xffff:
            wire_size = 12 if ord(char)>0xffff else 6
        else:
            wire_size = len(encoded.encode('utf-8'))
        if used + size > 4095 or wire_used + wire_size > 6144:
            pages.append("".join(current))
            current, used, wire_used = [], 0, 0
        current.append(char)
        used += size
        wire_used += wire_size
    pages.append("".join(current))
    if len(text.encode('utf-8'))>32760 or len(pages) > 32:
        raise ValueError("Original exceeds bounded reading page count")
    return pages


def portable_metadata(value):
    """Keep evidence URLs/digests; research paths are not package dependencies."""
    if isinstance(value, list):
        return [portable_metadata(item) for item in value]
    if not isinstance(value, dict):
        return value
    result = {}
    for key, item in value.items():
        if key in ('snapshot', 'retainedFrom', 'completeReferenceFile', 'completeReaderHtmlFile', 'candidatePath'):
            continue
        if isinstance(item, str) and re.match(r'^(?:artifacts|docs|sources|windows-app|reference-texts)/|^[A-Za-z]:[\\/]', item):
            continue
        result[key] = portable_metadata(item)
    return result


def select_guides(document, works, version):
    if document.get("schemaVersion") != 1 or document.get("language") != "zh-CN":
        raise ValueError("Unsupported guide schema or language")
    rows = document["works"]
    by_id = {row["id"]: row for row in rows}
    if len(by_id) != len(rows) or len({row["workId"] for row in rows}) != len(rows):
        raise ValueError("Duplicate guide identity")
    selected = []
    limits = {"workId": 80, "title": 256, "author": 128, "period": 96,
              "museum": 80, "brief": 256, "overview": 640, "looking": 384}
    for work in works:
        row = by_id.get(work["id"])
        if row is None or row["workId"] != work["workId"] or row["category"] != work["category"] or not isinstance(row.get("sources"), list) or not row["sources"]:
            raise ValueError("Guide identity or sources missing: " + work["id"])
        for field, limit in limits.items():
            value = row.get(field, "")
            if not isinstance(value, str) or "\0" in value or len(value.encode("utf-8")) >= limit:
                raise ValueError("Guide field exceeds device bounds: " + field)
        if not all(row.get(field, "").strip() for field in ("title", "author", "overview")):
            raise ValueError("Incomplete guide: " + work["id"])
        if row.get('looking', '').strip():
            basis = row.get('lookingBasis', {})
            sources = {s['id']: s for s in row['sources']}
            if (row.get('contentKinds', {}).get('looking') != 'work_specific_authoritative_analysis'
                    or basis.get('scope') != 'work_specific_authoritative_analysis'
                    or not basis.get('sourceIds') or not set(basis['sourceIds']) <= sources.keys()
                    or any(not sources[s].get('evidenceSha256') for s in basis['sourceIds'])):
                raise ValueError('Looking text lacks reviewed authoritative evidence: '+work['id'])
        original = row["transcription"]
        if original["status"] not in ("source_verified", "reference_verified", "not_available", "withheld_pending_review"):
            raise ValueError("Invalid original review status")
        if original["status"] in ("source_verified", "reference_verified"):
            if not original.get("text", "").strip():
                raise ValueError("Reviewed original text is empty")
            for field, limit in (("text", 32761 if "completeReferenceText" in original else 8192), ("explanation", 512)):
                value = original.get(field, "")
                if not isinstance(value, str) or "\0" in value or len(value.encode("utf-8")) >= limit:
                    raise ValueError("Original text exceeds device bounds")
            full = original.get("completeReferenceText", original["text"])
            if not isinstance(full, str) or not full.strip() or "\0" in full or len(full.encode("utf-8")) > 4095 * 8:
                raise ValueError("Complete original exceeds bounded reading pages")
            if original.get("completeReferenceTextSha256", digest(full.encode("utf-8"))) != digest(full.encode("utf-8")):
                raise ValueError("Complete original checksum mismatch")
            if len(original["text"].encode('utf-8'))>=8192 and original["text"]!=full:
                raise ValueError("Oversized original differs from complete text")
            pages = text_pages(full)
        reader = {name: row.get(name, "") for name in limits}
        reviewed = original["status"] in ("source_verified", "reference_verified")
        reader.update(transcription=pages[0] if reviewed and len(full.encode("utf-8")) >= 8192 else original.get("completeReferenceText",original.get("text", "")) if reviewed else "",
                      explanation=original.get("explanation", "") if reviewed else "")
        if reviewed and len(full.encode("utf-8")) >= 8192:
            reader.update(transcriptionPages=len(pages), transcriptionSha256=digest(full.encode("utf-8")))
        reply = {"status": 200, "body": {**{key: work[key] for key in ("id", "title", "author", "source", "license")},
                 "date": "2026-10-10", "size": 1048576, "sha256": "f" * 64, "frames": len(work["frames"]),
                 "width": 1280, "height": 720, "orientation": "portrait", "guideVersion": 3, "guide": reader}}
        # Gallery replies retain CJK UTF-8 while escaping HTML-sensitive text.
        # The production importer independently checks its exact wire encoding.
        wire = json.dumps(reply, ensure_ascii=False, separators=(",", ":")).replace('\\"', '\\u0022')
        for char in "<>&'+`":
            wire = wire.replace(char, f"\\u{ord(char):04x}")
        if len(wire.encode("utf-8")) > 16384:
            raise ValueError("Guide manifest exceeds device reply budget")
        # Research snapshot paths are workspace-local. Ship portable source URLs
        # and digests, with the actual reader content and review scope.
        packaged = copy.deepcopy({key: row[key] for key in
            ("id", "workId", "category", "title", "author", "period", "museum", "brief",
             "overview", "looking", "lookingBasis", "transcription", "sources", "contentKinds") if key in row})
        for source in packaged["sources"]:
            source.pop("snapshot", None)
            source.pop("retainedFrom", None)
        if original["status"] not in ("source_verified", "reference_verified"):
            packaged["transcription"]["text"] = ""
            packaged["transcription"]["explanation"] = ""
            packaged["transcription"].pop("completeReferenceText", None)
            packaged["transcription"].pop("completeReferenceTextSha256", None)
        elif "completeReferenceText" in original:
            packaged["transcription"]["completeReferenceTextSha256"] = digest(full.encode("utf-8"))
            if len(full.encode('utf-8'))>=8192:
                packaged["transcription"]["text"] = "这幅作品所写的诗文已补入完整原文。\n当前设备版本只支持短篇，请升级后阅读完整正文。"
        # References to archived local files never become install-time dependencies.
        for field in ("completeReferenceFile", "completeReaderHtmlFile"):
            packaged["transcription"].pop(field, None)
        selected.append(portable_metadata(packaged))
    return {"schemaVersion": 1, "language": "zh-CN", "edition": "pack-" + version,
            "status": "packaged", "works": selected}


def package(source, output, version, guides_path=None, category=None):
    if not re.fullmatch(r"\d{4}\.\d{2}\.\d{2}(?:\.\d+)?", version):
        raise ValueError("Invalid resource version")
    if output.exists():
        raise ValueError("Use a new output directory; existing packages are preserved")
    def read(name):
        return json.loads((source / name).read_text(encoding="utf-8-sig"))
    catalog = read("catalog.json")
    guides_path = guides_path or (source / "guides.zh.json" if (source / "guides.zh.json").exists()
                                  else ROOT / "docs/art-guides/catalog.zh.json")
    guides = json.loads(guides_path.read_text(encoding="utf-8-sig"))
    # Validate every selected row before creating either category's package.
    if category not in (None, "painting", "calligraphy"):
        raise ValueError("Unsupported pack category")
    packaged_works = [w for w in catalog if category is None or w["category"] == category]
    if not packaged_works:
        raise ValueError("No works in requested pack category")
    select_guides(guides, packaged_works, version)
    provenance, selection = read("provenance.json"), read("selection.json")
    ids = [w["id"] for w in catalog]
    if len(set(ids)) != len(ids) or len({w["workId"] for w in catalog}) != len(ids):
        raise ValueError("Duplicate artwork identity")
    for records in (provenance, selection):
        if len(records) != len(ids) or {w["id"] for w in records} != set(ids):
            raise ValueError("Source records do not match artwork identities")
    selected = {w["id"]: w for w in selection}
    origins = {w["id"]: w for w in provenance}
    listed = read("files.json")
    files = listed["files"]
    image_names = []
    for work in catalog:
        origin, item = origins[work["id"]], selected[work["id"]]
        if work["category"] not in ("painting", "calligraphy") or work["license"] not in LICENSES:
            raise ValueError("Unsupported category or undocumented image license")
        for name in ("title", "author", "museum", "source"):
            if not work.get(name):
                raise ValueError(f"Missing {name}: {work['id']}")
        if not item.get("imageSource") or not origin.get("originalSha256"):
            raise ValueError("Missing reproduction provenance")
        if item["source"] != work["source"] or item["license"] != work["license"] or origin["license"] != work["license"]:
            raise ValueError("Conflicting source/license records")
        if not work["frames"] or len(work["frames"]) != len(work["portraitFrames"]):
            raise ValueError("Missing matching portrait layouts")
        for name in work["frames"] + work["portraitFrames"]:
            if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]*\.jpg", name) or ".." in name:
                raise ValueError("Unsafe image path")
            data = (source / name).read_bytes()
            if digest(data) != files[name]:
                raise ValueError(f"Rendered image changed: {name}")
            image_names.append(name)
    if len(image_names) != len(set(image_names)) or set(image_names) != set(files) or listed["count"] != len(files):
        raise ValueError("Rendered file inventory mismatch")
    output.mkdir(parents=True)
    results = []
    for pack_category, english, chinese in (("painting", "Painting", "名画"), ("calligraphy", "Calligraphy", "书法")):
        works = [w for w in packaged_works if w["category"] == pack_category]
        if not works:
            continue
        work_ids = {w["id"] for w in works}
        names = sorted(n for w in works for n in w["frames"] + w["portraitFrames"])
        notices = [f"# AI-bot 每日{chinese} / Daily {english}\n",
                   "Images retain their individual licenses below. The project MIT license does not replace image rights. Museum names identify collections, not endorsement.\n",
                   "图片版权和许可见逐件记录。仅做缩放、裁切、排版和 JPEG 编码，无生成式修复或改色。作品数量按独立作品统计，分页和横竖屏布局不重复计数。\n",
                   "Display derivatives: resizing/cropping, caption layout and JPEG encoding. Caption font: LXGW WenKai (SIL OFL 1.1), no font binary distributed.\n"]
        for work in works:
            item = selected[work["id"]]
            credit = item.get("attribution") or item.get("credit") or work["museum"]
            if "princeton.edu" in work["source"]:
                credit += "; Image courtesy of the Princeton University Art Museum"
            notices.append(f"## {work['id']} — {work['title']}\n\n"
                           f"- 作者 / Artist: {work['author']}\n- 藏馆 / Museum: {work['museum']}\n"
                           f"- 署名 / Credit: {credit}\n- 馆藏 / Record: {work['source']}\n"
                           f"- 图像 / Reproduction: {item['imageSource']}\n"
                           f"- 许可 / License: {work['license']} — {LICENSES[work['license']]}\n"
                           f"- 权利来源 / Rights evidence: {item.get('rightsSource') or item['imageSource']}\n")
        metadata = {
            "catalog.json": encode(works),
            "guides.zh.json": encode(select_guides(guides, works, version)),
            "selection.json": encode([w for w in selection if w["id"] in work_ids]),
            "provenance.json": encode([w for w in provenance if w["id"] in work_ids]),
            "LICENSE": (ROOT / "LICENSE").read_bytes(),
            "THIRD_PARTY_NOTICES.md": "\n".join(notices).encode("utf-8"),
            "README.md": (f"# 每日{chinese}图库 {version}\n\n"
                          f"{len(works)} 件独立作品，{len(names)} 张横竖屏图片。\n\n"
                          "需要支持导读随包更新的新版 Windows AI-bot；旧桥接会拒绝本格式，请先升级。\n"
                          "图片需要 TAB5 0.2.145-ui 或更新版本；作品详情需要支持导读的固件。超过8,191 UTF-8字节的完整原文需要本次候选桥接及支持 guideVersion=3 的待构建固件；旧设备显示升级提示，不把节录当作全文。本包不含固件，不会部署设备。\n"
                          "在电脑端“检查更新 → 屏保图库”选择“导入图库 ZIP…”，直接选择本包，无需解压。\n"
                          "然后在 TAB5 设置中选择对应屏保。仅需下载喜欢的类别，其他功能无需图库。\n"
                          "图库保存在 %LOCALAPPDATA%/AI-bot/DailyArt，程序升级会保留。\n\n"
                          "简介、欣赏提示、已核实原文与逐件来源包含在 guides.zh.json，与图片一同校验、安装和更新。未核实原文保持缺省。\n"
                          "A bridge supporting pack-bundled guides is required; older bridges reject this schema and must be upgraded first. Images require TAB5 .145+, reading requires guide-capable firmware. Complete originals above 8,191 UTF-8 bytes require this candidate bridge and pending guideVersion=3 firmware; earlier devices show an upgrade notice. This pack includes no firmware and does not deploy a device. Import this ZIP through Check for updates → Artwork collections. No extraction is needed.\n"
                          "Introductions, viewing suggestions, reviewed originals and source records update with the images, without rebuilding the bridge for later text editions.\n"
                          "Each category is optional. Application upgrades preserve installed collections.\n"
                          "See THIRD_PARTY_NOTICES.md and per-work metadata for image rights; LICENSE covers project-authored material only.\n").encode("utf-8"),
        }
        if len(metadata["guides.zh.json"]) > 8 * 1024 * 1024:
            raise ValueError("Guide file exceeds importer limit")
        hashes = {n: digest(data) for n, data in metadata.items()} | {n: files[n] for n in names}
        manifest = {"schemaVersion": 2, "version": version, "category": pack_category, "works": len(works), "frames": len(names), "files": hashes}
        target = output / f"AI-bot-Daily{english}-{version}.zip"
        with zipfile.ZipFile(target, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
            archive.writestr("PACK.json", encode(manifest))
            for name, data in metadata.items():
                archive.writestr(name, data)
            for name in names:
                data = (source / name).read_bytes()
                if digest(data) != hashes[name]:
                    raise ValueError("Image changed during packaging")
                archive.writestr(name, data, compress_type=zipfile.ZIP_STORED)
        with target.open("rb") as stream:
            sha = hashlib.file_digest(stream, "sha256").hexdigest()
        (output / (target.name + ".sha256")).write_text(f"{sha}  {target.name}\n", encoding="ascii")
        results.append({"file": target.name, "sha256": sha, "bytes": target.stat().st_size, "works": len(works), "frames": len(names)})
    (output / "packs.json").write_bytes(encode(results))
    print(json.dumps(results, ensure_ascii=False))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / "windows-app/AIBotBridge/Assets/DailyArt")
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--version", default="2026.10.07")
    parser.add_argument("--guides", type=Path, help="Reviewed guide catalogue; defaults to source/guides.zh.json or the project catalogue")
    parser.add_argument("--category", choices=("painting", "calligraphy"), help="Build only one independently versioned category")
    args = parser.parse_args()
    package(args.source, args.output, args.version, args.guides, args.category)
