#!/usr/bin/env python3
"""Package the provenance-pinned project robot ICO as a macOS application icon.

Reuse original PNG frames at matching sizes. Larger representations only resize
the largest supplied frame; no new artwork, cropping or background is added.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess
import tempfile


def build(output: Path) -> None:
    root = Path(__file__).resolve().parents[1]
    source = "windows-app/AIBotBridge/Assets/app-icon.ico"
    data = (root / source).read_bytes()
    expected = json.loads((root / "licenses/materials.json").read_text())["sourceAssets"][source]
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("Robot icon differs from the reviewed provenance asset")
    reserved, kind, count = struct.unpack_from("<HHH", data)
    if (reserved, kind) != (0, 1):
        raise ValueError("Expected the reviewed ICO source")
    frames = {}
    for index in range(count):
        width, height, _, _, _, _, size, offset = struct.unpack_from("<BBBBHHII", data, 6 + index * 16)
        width, height = width or 256, height or 256
        frame = data[offset:offset + size]
        if width == height and frame.startswith(b"\x89PNG\r\n\x1a\n") and len(frame) == size:
            frames[width] = frame
    if 256 not in frames:
        raise ValueError("The reviewed 256-pixel PNG frame is missing")
    output = output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="aibot-mac-icon-") as temporary:
        work = Path(temporary)
        original = work / "robot.png"
        original.write_bytes(frames[max(frames)])
        iconset = work / "AppIcon.iconset"
        iconset.mkdir()
        for points in [16, 32, 128, 256, 512]:
            for scale in [1, 2]:
                pixels = points * scale
                target = iconset / f"icon_{points}x{points}{'@2x' if scale == 2 else ''}.png"
                if pixels in frames:
                    target.write_bytes(frames[pixels])
                else:
                    subprocess.run(["/usr/bin/sips", "--resampleHeightWidth", str(pixels), str(pixels),
                                    str(original), "--out", str(target)], check=True, stdout=subprocess.DEVNULL)
        subprocess.run(["/usr/bin/iconutil", "-c", "icns", str(iconset), "-o", str(output)], check=True)
    print(output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    build(parser.parse_args().output)
