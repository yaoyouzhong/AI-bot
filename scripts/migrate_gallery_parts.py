"""Migrate verified 2026.2 gallery parts to a separate draft release."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.request


REPOSITORY = "yaoyouzhong/AI-bot"
OLD = "v0.6.20"
NEW = "gallery-2026.11.12.1"
MANIFEST_SHA = "9b6870846a58b1515646746e3e438ea36a2a73d4c32f3f3eb390063fd59e471e"
EXPECTED = {
    "AI-bot-gallery-painting-hd-2026.11.12.1.zip": (3141300917, "63cd2f20c0322db36615110e329e0f36cd8dc785d590420a6d4e2359aa266045"),
    "AI-bot-gallery-calligraphy-hd-2026.11.12.1.zip": (3104757745, "f1ff5f59f54dce19341d8a3dcfe560771036979013b0836217a2c639828b608b"),
}
PART_BYTES = 1536 * 1024 * 1024


def gh(*args):
    return subprocess.check_output(["gh", *args, "--repo", REPOSITORY], text=True)


def release():
    value = json.loads(gh("release", "view", NEW, "--json", "tagName,isDraft,assets"))
    if value["tagName"] != NEW or not value["isDraft"]:
        raise ValueError("Target must remain the intended draft")
    return value


def digest(file):
    with file.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def fetch(name, target):
    url = f"https://github.com/{REPOSITORY}/releases/download/{OLD}/{name}"
    request = urllib.request.Request(url, headers={"User-Agent": "AI-bot-gallery-migration"})
    for attempt in range(4):
        try:
            with urllib.request.urlopen(request, timeout=120) as response, target.open("wb") as output:
                while chunk := response.read(1024 * 1024):
                    output.write(chunk)
            return
        except (OSError, TimeoutError):
            if attempt == 3:
                raise
            time.sleep(3 * (attempt + 1))


def upload(file, expected_sha):
    remote = next((a for a in release()["assets"] if a["name"] == file.name), None)
    if remote is None:
        for attempt in range(4):
            process = subprocess.run(["gh", "release", "upload", NEW, str(file), "--repo", REPOSITORY])
            remote = next((a for a in release()["assets"] if a["name"] == file.name), None)
            if remote is not None:
                break
            if attempt == 3:
                raise RuntimeError(f"Upload failed: {file.name} ({process.returncode})")
            time.sleep(3 * (attempt + 1))
    if remote["size"] != file.stat().st_size or remote["digest"] != "sha256:" + expected_sha:
        raise ValueError("Remote asset differs: " + file.name)
    print("UPLOADED", file.name, flush=True)


with tempfile.TemporaryDirectory(prefix="aibot-gallery-") as temp:
    directory = Path(temp)
    old_manifest = directory / "old.json"
    fetch("HD-Gallery-parts.json", old_manifest)
    if digest(old_manifest) != MANIFEST_SHA:
        raise ValueError("Original manifest differs from the accepted release")
    original = json.loads(old_manifest.read_text(encoding="utf-8"))
    archives = original["archives"]
    if {a["name"] for a in archives} != set(EXPECTED):
        raise ValueError("Unexpected archives")
    result = []
    for archive in archives:
        name = archive["name"]
        if (archive["bytes"], archive["sha256"]) != EXPECTED[name]:
            raise ValueError("Original archive identity differs")
        whole = hashlib.sha256()
        parts = []
        for number in (1, 2):
            file = directory / f"{name}.part10{number}"
            parts.append({"name": file.name, "bytes": 0, "sha256": ""})
        outputs = [(directory / item["name"]).open("wb") for item in parts]
        total = 0
        try:
            for part in archive["parts"]:
                local = directory / "old.part"
                fetch(part["name"], local)
                if local.stat().st_size != part["bytes"] or digest(local) != part["sha256"]:
                    raise ValueError("Original part failed verification: " + part["name"])
                with local.open("rb") as source:
                    while chunk := source.read(1024 * 1024):
                        whole.update(chunk)
                        split = min(len(chunk), max(0, PART_BYTES - total))
                        if split:
                            outputs[0].write(chunk[:split])
                        if split < len(chunk):
                            outputs[1].write(chunk[split:])
                        total += len(chunk)
        finally:
            for output in outputs:
                output.close()
        if (total, whole.hexdigest()) != EXPECTED[name]:
            raise ValueError("Full archive failed verification")
        for part in parts:
            file = directory / part["name"]
            part["bytes"], part["sha256"] = file.stat().st_size, digest(file)
            if not 0 < part["bytes"] < 2 * 1024 * 1024 * 1024:
                raise ValueError("Invalid GitHub attachment size")
            upload(file, part["sha256"])
        result.append({"name": name, "bytes": total, "sha256": whole.hexdigest(), "parts": parts})
        for part in parts:
            (directory / part["name"]).unlink()
    manifest = directory / "HD-Gallery-parts-2.json"
    manifest.write_text(json.dumps({"schema": 1, "chunkBytes": PART_BYTES, "archives": result}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    upload(manifest, digest(manifest))
    for archive in result:
        full = hashlib.sha256()
        total = 0
        for part in archive["parts"]:
            subprocess.run(["gh", "release", "download", NEW, "--repo", REPOSITORY, "--pattern", part["name"], "--dir", str(directory)], check=True)
            local = directory / part["name"]
            if local.stat().st_size != part["bytes"] or digest(local) != part["sha256"]:
                raise ValueError("Downloaded part failed verification: " + part["name"])
            with local.open("rb") as stream:
                while chunk := stream.read(1024 * 1024):
                    full.update(chunk)
                    total += len(chunk)
            local.unlink()
        if (total, full.hexdigest()) != EXPECTED[archive["name"]]:
            raise ValueError("Downloaded parts did not reconstruct accepted archive")
        print("DOWNLOADED_AND_MERGED_VERIFIED", archive["name"], flush=True)
    print("CLOUD_MIGRATION_OK", flush=True)
