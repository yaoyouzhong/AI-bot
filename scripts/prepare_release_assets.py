"""Fail closed on missing, extra or corrupt candidate assets; write release metadata."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil

ROOT = Path(__file__).resolve().parents[1]


def prepare(directory: Path, version: str, commit: str):
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Expected full source commit SHA")
    archives = [f"AIBotBridge-{version}-local-candidate-win-x64.zip",
                f"AIBotBridge-{version}-local-candidate-macos-arm64.zip",
                f"AI-bot-{version}-source.zip", f"AI-bot-{version}-firmware-materials.zip"]
    expected = {name + suffix for name in archives for suffix in ("", ".sha256")}
    if {p.name for p in directory.iterdir()} != expected:
        raise ValueError("Expected exactly Windows, Mac, source and firmware archives with sidecar hashes")
    for name in archives:
        archive = directory / name
        if archive.is_symlink() or not archive.is_file():
            raise ValueError(f"Invalid archive: {name}")
        actual = hashlib.sha256(archive.read_bytes()).hexdigest()
        checksum = (directory / (name + ".sha256")).read_text(encoding="ascii").strip()
        if checksum != f"{actual}  {name}":
            raise ValueError(f"Archive checksum mismatch: {name}")
    for name in ("LICENSE", "THIRD_PARTY_NOTICES.md"):
        shutil.copyfile(ROOT / name, directory / name)
    (directory / "BUILD.json").write_text(json.dumps({
        "version": version, "sourceCommit": commit,
        "macOS": "arm64; ad-hoc signed; not notarized; interactive acceptance required",
        "status": "candidate; publication requires separate acceptance"
    }, indent=2) + "\n", encoding="utf-8")
    lines = [f"{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}"
             for p in sorted(directory.iterdir())]
    (directory / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="ascii")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--commit", required=True)
    args = parser.parse_args()
    prepare(args.directory, (ROOT / "VERSION").read_text().strip(), args.commit)
    print("RELEASE_ASSETS_OK")
