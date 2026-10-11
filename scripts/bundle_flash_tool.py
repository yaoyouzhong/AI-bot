"""Bundle a deterministic subset of the pinned tool archive and matching source.

No tool is executed here. Runtime flashing never downloads dependencies.
"""
import argparse
import hashlib
import binascii
import io
import json
from pathlib import Path
import shutil
import struct
import tarfile
import urllib.request
import uuid
import zipfile

ROOT = Path(__file__).resolve().parents[1]
VERSION = "4.9.1"
SOURCE_URL = "https://codeload.github.com/espressif/esptool/tar.gz/refs/tags/v4.9.1"
SOURCE_HASH = "1ac5278579b6f47ff6aa63e9cf02ef6f00e09eaef34c9013690495af52f89622"
TOOLS = {
    "windows": ("windows-amd64.zip", "6d3d08187e21af57c6f2ba4ebbed9f1b53240d271a506ab888ddd0f5bfa8eaf7", "zip"),
    "macos": ("macos-arm64.tar.gz", "13d9e4667a0a61096d4f6650e87cf4d9d99b4d66bdcc58dace2a127016c38e49", "tar.gz"),
}
EXECUTABLE_HASHES = {
    "windows": "f8edeb5e81b9d36c0e181ca4d716644ff28495655fe66c97f08231bf3d00e73b",
    "macos": "dce9b3fbcad289f2e42c534143c47ebfbef5a87080c9a65f9f0019bd3e76c43f",
}
SUBSET_HASHES = {
    "windows": "1ae3e2fae3d23719956a9f0cd649dccd7e9bf524e073f613e1d7a189b509c25e",
    "macos": "606dd95c708cc6753fe09c7b2649192d71ae0cb39f0bfac503897e25a3cd43ec",
}


def compact_archive(platform, upstream):
    executable = "esptool.exe" if platform == "windows" else "esptool"
    selected = {executable, "LICENSE", "README.md"}
    members = {}
    if platform == "windows":
        with zipfile.ZipFile(upstream) as archive:
            for entry in archive.infolist():
                if not entry.is_dir() and Path(entry.filename).name in selected:
                    members[entry.filename] = archive.read(entry)
    else:
        with tarfile.open(upstream) as archive:
            for entry in archive.getmembers():
                if entry.isfile() and Path(entry.name).name in selected:
                    members[entry.name] = archive.extractfile(entry).read()
    if len(members) != 3 or {Path(name).name for name in members} != selected:
        raise ValueError("Missing or duplicate executable/license/readme in upstream archive")
    for name, data in members.items():
        if name.startswith("/") or ".." in Path(name).parts or "\\" in name:
            raise ValueError("Unsafe tool member")
        if Path(name).name == executable and hashlib.sha256(data).hexdigest() != EXECUTABLE_HASHES[platform]:
            raise ValueError("Unexpected esptool executable bytes")
    result = io.BytesIO()
    if platform == "windows":
        # The outer application package provides compression. Store inner
        # members so pinned bytes do not depend on the build host's zlib.
        with zipfile.ZipFile(result, "w", zipfile.ZIP_STORED) as archive:
            for name, data in sorted(members.items()):
                entry = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
                entry.compress_type = zipfile.ZIP_STORED
                entry.create_system = 3
                entry.external_attr = (0o100755 if Path(name).name == executable else 0o100644) << 16
                archive.writestr(entry, data)
    else:
        with tarfile.open(fileobj=result, mode="w", format=tarfile.USTAR_FORMAT) as archive:
            for name, data in sorted(members.items()):
                entry = tarfile.TarInfo(name)
                entry.size = len(data)
                entry.mode = 0o755 if Path(name).name == executable else 0o644
                archive.addfile(entry, io.BytesIO(data))
        # Canonical gzip header + uncompressed DEFLATE blocks. This retains
        # the existing tar.gz runtime contract with identical cross-host hashes.
        payload = result.getvalue()
        result = io.BytesIO(b"\x1f\x8b\x08\x00\x00\x00\x00\x00\x00\xff")
        result.seek(0, 2)
        for offset in range(0, len(payload), 65535):
            block = payload[offset:offset + 65535]
            result.write(bytes([int(offset + len(block) == len(payload))]))
            result.write(struct.pack("<HH", len(block), len(block) ^ 65535))
            result.write(block)
        result.write(struct.pack("<II", binascii.crc32(payload), len(payload)))
    return result.getvalue(), sorted(members)


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
    compact, selected = compact_archive(platform, archive)
    tool_hash = hashlib.sha256(compact).hexdigest()
    if tool_hash != SUBSET_HASHES[platform]:
        raise ValueError("Compact archive differs from the runtime-pinned bytes")
    (destination / f"esptool-{VERSION}.{extension}").write_bytes(compact)
    shutil.copyfile(source_path, destination / source_path.name)
    record = {"version": VERSION, "platform": platform, "toolURL": url, "upstreamToolSHA256": expected,
              "toolSHA256": tool_hash, "executableSHA256": EXECUTABLE_HASHES[platform], "selectedFiles": selected,
              "sourceURL": SOURCE_URL, "sourceSHA256": sha(source_path),
              "notice": "Deterministically repacked subset of the verified Espressif standalone archive. esptool executable, LICENSE and README bytes are unchanged; unused tools are omitted. Matching source and GPL license are included."}
    (destination / "ORIGIN.json").write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"FLASH_TOOL_BUNDLED platform={platform} checksum=OK")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("platform", choices=TOOLS)
    parser.add_argument("destination", type=Path)
    parser.add_argument("--cache", type=Path)
    args = parser.parse_args()
    bundle(args.platform, args.destination, args.cache)
