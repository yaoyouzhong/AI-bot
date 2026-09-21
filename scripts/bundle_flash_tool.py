"""Build-time only: bundle the unmodified, pinned tool and matching source.

No tool is executed here. Runtime flashing never downloads dependencies.
"""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
VERSION = "4.9.1"
SOURCE_URL = "https://codeload.github.com/espressif/esptool/tar.gz/refs/tags/v4.9.1"
SOURCE_HASH = "1ac5278579b6f47ff6aa63e9cf02ef6f00e09eaef34c9013690495af52f89622"
TOOLS = {
    "windows": ("windows-amd64.zip", "6d3d08187e21af57c6f2ba4ebbed9f1b53240d271a506ab888ddd0f5bfa8eaf7", "zip"),
    "macos": ("macos-arm64.tar.gz", "13d9e4667a0a61096d4f6650e87cf4d9d99b4d66bdcc58dace2a127016c38e49", "tar.gz"),
}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fetch(url, path, expected):
    if path.is_file() and sha(path) == expected:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    partial = path.with_name(path.name + "." + uuid.uuid4().hex + ".download")
    try:
        with urllib.request.urlopen(url, timeout=120) as response, partial.open("wb") as output:
            shutil.copyfileobj(response, output)
        if sha(partial) != expected:
            raise ValueError("Tool/source checksum mismatch")
        partial.replace(path)
    finally:
        partial.unlink(missing_ok=True)


def bundle(platform, destination, cache=None):
    suffix, expected, extension = TOOLS[platform]
    cache = cache or ROOT / "artifacts/flash-bundle-cache"
    archive = cache / ("esptool-v" + VERSION + "-" + suffix)
    url = f"https://github.com/espressif/esptool/releases/download/v{VERSION}/{archive.name}"
    fetch(url, archive, expected)
    # Include the exact-version source distribution and its LICENSE, rather than
    # relying solely on an upstream web link for corresponding source.
    source_path = cache / f"esptool-{VERSION}-source.tar.gz"
    fetch(SOURCE_URL, source_path, SOURCE_HASH)
    destination.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(archive, destination / f"esptool-{VERSION}.{extension}")
    shutil.copyfile(source_path, destination / source_path.name)
    record = {"version": VERSION, "platform": platform, "toolURL": url, "toolSHA256": expected,
              "sourceURL": SOURCE_URL, "sourceSHA256": sha(source_path),
              "notice": "Unmodified Espressif standalone archive; original dependency notices retained inside. esptool source and GPL license included in the source archive."}
    (destination / "ORIGIN.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"FLASH_TOOL_BUNDLED platform={platform} checksum=OK")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("platform", choices=TOOLS)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--cache", type=Path)
    args = parser.parse_args()
    bundle(args.platform, args.destination, args.cache)
