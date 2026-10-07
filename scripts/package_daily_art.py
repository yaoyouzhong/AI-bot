"""Build independently installable artwork packs from the verified rendered catalog."""
import argparse
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


def package(source, output, version):
    if not re.fullmatch(r"\d{4}\.\d{2}\.\d{2}(?:\.\d+)?", version):
        raise ValueError("Invalid resource version")
    if output.exists():
        raise ValueError("Use a new output directory; existing packages are preserved")
    def read(name):
        return json.loads((source / name).read_text(encoding="utf-8-sig"))
    catalog = read("catalog.json")
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
    for category, english, chinese in (("painting", "Painting", "名画"), ("calligraphy", "Calligraphy", "书法")):
        works = [w for w in catalog if w["category"] == category]
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
            "selection.json": encode([w for w in selection if w["id"] in work_ids]),
            "provenance.json": encode([w for w in provenance if w["id"] in work_ids]),
            "LICENSE": (ROOT / "LICENSE").read_bytes(),
            "THIRD_PARTY_NOTICES.md": "\n".join(notices).encode("utf-8"),
            "README.md": (f"# 每日{chinese}图库 {version}\n\n"
                          f"{len(works)} 件独立作品，{len(names)} 张横竖屏图片。\n\n"
                          "需要 Windows AI-bot 0.6.0 或更新版本及 TAB5 0.2.145-ui 或更新版本。\n"
                          "在电脑端“检查更新 → 屏保图库”选择“导入图库 ZIP…”，直接选择本包，无需解压。\n"
                          "然后在 TAB5 设置中选择对应屏保。仅需下载喜欢的类别，其他功能无需图库。\n"
                          "图库保存在 %LOCALAPPDATA%/AI-bot/DailyArt，程序升级会保留。\n\n"
                          "Windows bridge 0.6.0+ and TAB5 0.2.145-ui+ are required. Import this ZIP through Check for updates → Artwork collections. No manual extraction is needed.\n"
                          "Each category is optional. Application upgrades preserve installed collections.\n"
                          "See THIRD_PARTY_NOTICES.md and per-work metadata for image rights; LICENSE covers project-authored material only.\n").encode("utf-8"),
        }
        hashes = {n: digest(data) for n, data in metadata.items()} | {n: files[n] for n in names}
        manifest = {"schemaVersion": 1, "version": version, "category": category, "works": len(works), "frames": len(names), "files": hashes}
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
    args = parser.parse_args()
    package(args.source, args.output, args.version)
