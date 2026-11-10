#!/usr/bin/env python3
"""Apply or verify the checked TAB5 source overlay on a copied .318 project."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import zipfile

HERE = Path(__file__).resolve().parent
BASE = HERE.parent / "tab5-318-source" / "AI-bot-tab5-0.2.318-ui-source.zip"
OVERLAY = HERE / "source-overlay.zip"


def digest(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(root, name):
    path = PurePosixPath(name)
    if path.is_absolute() or not path.parts or any(p in (".", "..") for p in path.parts) or "\\" in name or ":" in name:
        raise ValueError(f"Unsafe path: {name}")
    result = root
    for part in path.parts:
        result = result / part
        if result.is_symlink():
            raise ValueError(f"Symlink not allowed: {name}")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("project", type=Path, help="copy of the original archive's source/ directory")
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--check", action="store_true", help="check every baseline file and all destinations without writing")
    group.add_argument("--apply", action="store_true", help="check everything, then apply the overlay")
    group.add_argument("--verify", action="store_true", help="verify the project after applying the overlay")
    args = parser.parse_args()
    root = args.project.resolve(strict=True)
    if not root.is_dir():
        raise ValueError("Project must be a directory")
    manifest = json.loads((HERE / "manifest.json").read_text(encoding="utf-8"))
    if digest(BASE.read_bytes()) != manifest["baselineArchiveSha256"] or digest(OVERLAY.read_bytes()) != manifest["overlaySha256"]:
        raise ValueError("Archive digest mismatch")
    changes = {row["path"]: row for row in manifest["files"]}
    if len(changes) != len(manifest["files"]):
        raise ValueError("Duplicate manifest paths")

    with zipfile.ZipFile(BASE) as base, zipfile.ZipFile(OVERLAY) as overlay:
        members = [i for i in base.infolist() if i.filename.startswith("source/") and not i.is_dir()]
        base_names = {i.filename[7:] for i in members}
        if len(base_names) != len(members) or set(overlay.namelist()) != set(changes) or len(overlay.namelist()) != len(changes):
            raise ValueError("Archive inventory mismatch")
        for row in changes.values():
            safe_path(root, row["path"])
            data = overlay.read(row["path"])
            if digest(data) != row["afterSha256"] or len(data) != row["bytes"]:
                raise ValueError(f"Overlay digest mismatch: {row['path']}")
            if (row["path"] in base_names) != (row["beforeSha256"] is not None):
                raise ValueError(f"Incorrect baseline entry: {row['path']}")
        for item in members:
            name = item.filename[7:]
            path = safe_path(root, name)
            expected = digest(base.read(item))
            if name in changes:
                if changes[name]["beforeSha256"] != expected:
                    raise ValueError(f"Baseline manifest mismatch: {name}")
                if args.verify:
                    expected = changes[name]["afterSha256"]
            if not path.is_file() or digest(path.read_bytes()) != expected:
                raise ValueError(f"Project file missing or changed: {name}")
        for name, row in changes.items():
            if name in base_names:
                continue
            path = safe_path(root, name)
            if args.verify:
                if not path.is_file() or digest(path.read_bytes()) != row["afterSha256"]:
                    raise ValueError(f"Overlay file missing or changed: {name}")
            elif path.exists():
                raise ValueError(f"New file already exists: {name}")
        if args.apply:
            for name in changes:
                path = safe_path(root, name)
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(overlay.read(name))
    print(f"TAB5_OVERLAY_{'VERIFIED' if args.verify else 'APPLIED' if args.apply else 'CHECKED'} {len(changes)} files; {len(base_names)} baseline files")
    print("Claude PCM provenance and licensing are documented in README.md.")


if __name__ == "__main__":
    main()
