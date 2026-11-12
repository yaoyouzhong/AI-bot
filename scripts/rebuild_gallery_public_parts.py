"""Reconstruct the verified two-part archive from publicly available old parts."""
import hashlib
import json
from pathlib import Path
import sys
import urllib.request

category, output = sys.argv[1], Path(sys.argv[2])
assert category in ("painting", "calligraphy")
expected = {
    "painting": ("1f1d4e292798e1f8db06bab798f1dc4f8c4e6287d2269d57831c03d265f6f704", "8f144d5f48be9342f521445c70c2fe615623c1b5e71a70adc7c21ed0ae43b9f9"),
    "calligraphy": ("8e0b28f3624aede795391f2c29ee37a40ca6250fc453df4a387c9f4dce079de7", "ebe70aa1728bf2473e57a688e66dfa546cdee68cc74d3c22352d7c4d16ae6bb3"),
}
base = "https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.20/"
def fetch(name):
    request = urllib.request.Request(base + name, headers={"User-Agent": "AI-bot-gallery-mac-validation"})
    return urllib.request.urlopen(request, timeout=120)
with fetch("HD-Gallery-parts.json") as response:
    data = response.read()
if hashlib.sha256(data).hexdigest() != "9b6870846a58b1515646746e3e438ea36a2a73d4c32f3f3eb390063fd59e471e":
    raise ValueError("Old gallery manifest differs from verified release")
name = f"AI-bot-gallery-{category}-hd-2026.11.12.1.zip"
archive = next(a for a in json.loads(data)["archives"] if a["name"] == name)
outputs = [(output / f"{name}.part10{i}").open("wb") for i in (1, 2)]
part_hashes = [hashlib.sha256(), hashlib.sha256()]
total = 0
try:
    for original in archive["parts"]:
        part_hash = hashlib.sha256()
        size = 0
        with fetch(original["name"]) as response:
            while chunk := response.read(1024 * 1024):
                size += len(chunk)
                part_hash.update(chunk)
                split = min(len(chunk), max(0, 1536 * 1024 * 1024 - total))
                if split:
                    outputs[0].write(chunk[:split]); part_hashes[0].update(chunk[:split])
                if split < len(chunk):
                    outputs[1].write(chunk[split:]); part_hashes[1].update(chunk[split:])
                total += len(chunk)
        if size != original["bytes"] or part_hash.hexdigest() != original["sha256"]:
            raise ValueError("Original part failed verification: " + original["name"])
finally:
    for stream in outputs:
        stream.close()
if total != archive["bytes"] or tuple(h.hexdigest() for h in part_hashes) != expected[category]:
    raise ValueError("Rebuilt parts differ from the new verified assets")
print("PUBLIC_PARTS_REBUILT_AND_VERIFIED", category, flush=True)
