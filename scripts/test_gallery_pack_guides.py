"""Validate bundled guides and generate small real-image packs for the C# consumer."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

from package_daily_art import ROOT, package, select_guides


def run(output):
    if output.exists():
        raise ValueError("Use a new verification directory")
    assets = ROOT / "windows-app/AIBotBridge/Assets/DailyArt"
    read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
    catalog = read(assets / "catalog.json")
    document = read(ROOT / "docs/art-guides/catalog.zh.json")
    select_guides(document, catalog, "2026.10.10")
    wanted = {"https://www.artic.edu/artworks/16568", "colbase:tnm:TB-363"}
    chosen = []
    for category in ("painting", "calligraphy"):
        category_works = [w for w in catalog if w["category"] == category]
        primary = next(w for w in category_works if w["workId"] in wanted)
        secondary = next(w for w in category_works if w["id"] != primary["id"])
        chosen.extend([primary, secondary])
    guides = select_guides(document, chosen, "2026.10.10")
    rejected = 0
    def reject(mutator):
        nonlocal rejected
        bad = copy.deepcopy(guides)
        mutator(bad)
        try:
            select_guides(bad, chosen, "2026.10.10")
        except ValueError:
            rejected += 1
            return
        raise AssertionError("Invalid guide accepted by packager")
    reject(lambda d: d["works"].pop())
    reject(lambda d: d["works"].append(copy.deepcopy(d["works"][0])))
    reject(lambda d: d["works"][0].update(workId="unrelated"))
    reject(lambda d: d["works"][0].update(category="calligraphy"))
    reject(lambda d: d["works"][0].update(overview="文" * 214))
    reject(lambda d: d["works"][0].update(looking=123))
    reject(lambda d: d["works"][0].update(looking="没有作品依据的通用欣赏模板", lookingBasis={}, contentKinds={"looking":"editorial_viewing_suggestion"}))
    reject(lambda d: d["works"][0].update(sources=[]))
    reject(lambda d: d["works"][0].update(transcription={"status": "source_verified", "text": "文" * 2731}))
    reject(lambda d: d["works"][0].update(transcription={"status": "source_verified", "text": "<" * 2730}))
    reject(lambda d: d["works"][0].update(transcription={"status": "source_verified", "text": ""}))
    reject(lambda d: d["works"][0].update(transcription={"status": "unknown"}))
    without_looking = copy.deepcopy(guides)
    for row in without_looking['works']:
        row['looking'] = ''
        row['contentKinds']['looking'] = 'not_available'
    assert all(not row['looking'] for row in select_guides(without_looking, chosen, '2026.10.10')['works'])
    output.mkdir(parents=True)
    stage = output / "source"
    stage.mkdir()
    encode = lambda value: (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode()
    (stage / "catalog.json").write_bytes(encode(chosen))
    ids = {w["id"] for w in chosen}
    for name in ("provenance.json", "selection.json"):
        (stage / name).write_bytes(encode([w for w in read(assets / name) if w["id"] in ids]))
    names = sorted(n for w in chosen for n in w["frames"] + w["portraitFrames"])
    inventory = read(assets / "files.json")["files"]
    for name in names:
        shutil.copyfile(assets / name, stage / name)
    (stage / "files.json").write_bytes(encode({"count": len(names), "files": {n: inventory[n] for n in names}}))
    (stage / "guides.zh.json").write_bytes(encode(guides))
    package(stage, output / "packs", "2026.10.10")
    # A later calligraphy-only edition can use only calligraphy guide records.
    single_guides = select_guides(guides, [w for w in chosen if w["category"] == "calligraphy"], "2026.10.11")
    (output / "calligraphy-guides.zh.json").write_bytes(encode(single_guides))
    package(stage, output / "calligraphy-only", "2026.10.11", output / "calligraphy-guides.zh.json", "calligraphy")
    assert len(list((output / "calligraphy-only").glob("*.zip"))) == 1
    assert not list((output / "calligraphy-only").glob("*Painting*"))
    for path in (output / "packs").glob("*.zip"):
        with zipfile.ZipFile(path) as archive:
            manifest = json.loads(archive.read("PACK.json"))
            assert manifest["schemaVersion"] == 2
            for name, sha in manifest["files"].items():
                assert hashlib.sha256(archive.read(name)).hexdigest() == sha
            packed = json.loads(archive.read("guides.zh.json"))
            selected = [w for w in chosen if w["category"] == manifest["category"]]
            assert packed == select_guides(guides, selected, "2026.10.10")
            assert all(w["category"] == manifest["category"] for w in packed["works"])
    report = {"fullGuideIdentityAndBounds": len(catalog), "packagerRejections": rejected,
              "realWorks": len(chosen), "realImages": len(names), "categoryScopedGuidesAndHashes": True, "independentCalligraphyEdition": True}
    (output / "verification.json").write_bytes(encode(report))
    print("GALLERY_GUIDE_PACKAGER_OK " + json.dumps(report))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    run(parser.parse_args().output)
