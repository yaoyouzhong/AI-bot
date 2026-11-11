"""Pack the authorized six-frame Claude working clip for the TAB5 renderer."""

import argparse
import hashlib
import json
import struct
from pathlib import Path

from package_codex_companion import array, pack


APET_SHA256 = "8c9e76f5f42e041ecd135df5c060dc29f6c69eb5ec6ec6cc6c15381d22fc03e5"
SOURCE_COMMIT = "5f52a39bd5c4245aff24bfa3a23cde2d62d57e3c"


def eye_pixels(frame, width, height):
    """Preserve the two enclosed black eyes while keying out the backdrop."""
    remaining = {position for position, color in enumerate(frame) if color == 0}
    eyes = []
    while remaining:
        component = [remaining.pop()]
        for position in component:
            x, y = position % width, position // width
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                neighbor = ny * width + nx
                if 0 <= nx < width and 0 <= ny < height and neighbor in remaining:
                    remaining.remove(neighbor)
                    component.append(neighbor)
        rows = [position // width for position in component]
        # The verified source uses pure black for eyes in these rows, as well
        # as for the outer background and the opening above the head.
        if min(rows) >= 56 and max(rows) <= 63:
            eyes.append(component)
    if len(eyes) != 2:
        raise ValueError("Original eye regions did not match the authorized clip")
    return {position for component in eyes for position in component}


def encode(raw: bytes):
    if hashlib.sha256(raw).hexdigest() != APET_SHA256:
        raise ValueError("Only the authorized promotional clip is accepted")
    magic, version, count, source_width, source_height, reserved = struct.unpack_from("<4sBBHHH", raw)
    delays = struct.unpack_from("<6H", raw, 12)
    if (magic, version, count, source_width, source_height, reserved, delays) != (
        b"APET", 2, 6, 111, 120, 0, (120,) * 6
    ):
        raise ValueError("Unexpected APET dimensions, frame count or timing")

    pixels = source_width * source_height
    frames = [struct.unpack_from(f"<{pixels}H", raw, 24 + index * pixels * 2) for index in range(count)]
    visible = [position for frame in frames for position, color in enumerate(frame) if color != 0]
    x_values = [position % source_width for position in visible]
    y_values = [position // source_width for position in visible]
    left, top, right, bottom = min(x_values), min(y_values), max(x_values) + 1, max(y_values) + 1
    visible_width = right - left
    width, height = visible_width + visible_width % 2, bottom - top

    colors = sorted(set().union(*(set(frame) for frame in frames)))
    if len(colors) > 255:
        raise ValueError("Too many RGB565 colors for lossless indexing")
    indices = {color: index for index, color in enumerate(colors)}
    palette = colors + [0] * (256 - len(colors))
    alpha = [255] * len(colors) + [0] * (256 - len(colors))
    stream = bytearray()
    offsets = [0]
    for frame in frames:
        eyes = eye_pixels(frame, source_width, source_height)
        cropped = bytearray()
        for y in range(top, bottom):
            for x in range(width):
                position = y * source_width + left + x
                color = frame[position] if x < visible_width else 0
                cropped.append(indices[color] if x < visible_width and (color or position in eyes) else 255)
        stream.extend(pack(cropped))
        offsets.append(len(stream))
    if len(stream) > 65535:
        raise ValueError("Packed clip exceeds the 16-bit frame index")

    source = [
        f"/* Third-party clip, esp8266-ai {SOURCE_COMMIT}. See licenses/companion-art/NOTICE.md. */",
        f"enum {{ HEADPHONES_WIDTH={width}, HEADPHONES_HEIGHT={height}, HEADPHONES_FRAMES={count}, HEADPHONES_DELAY_MS={delays[0]} }};",
        array("claude_headphones_palette", "uint16_t", palette),
        array("claude_headphones_alpha", "uint8_t", alpha),
        array("claude_headphones_stream", "uint8_t", stream, 24),
        array("claude_headphones_offsets", "uint16_t", offsets),
    ]
    meta = dict(sourceCommit=SOURCE_COMMIT, sourcePath="firmware/include/img/claude_sprite.h",
                inputSha256=APET_SHA256, frameCount=count, frameDelayMs=list(delays),
                sourceWidth=source_width, sourceHeight=source_height,
                crop=[left, top, right, bottom], width=width, height=height,
                opaqueColors=len(colors), encodedBytes=len(stream),
                conversion="Original RGB565 colors and frame order; black backdrop keyed while retaining both black eyes; union crop; indexed PackBits",
                authorization="2026-11-11 user confirmed public redistribution in AI-bot TAB5 firmware; outside MIT")
    return "\n".join(source) + "\n", meta


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    source, meta = encode(args.input.read_bytes())
    content = source.encode("utf-8")
    args.output.write_bytes(content)
    meta["includeSha256"] = hashlib.sha256(content).hexdigest()
    args.output.with_suffix(".json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")
    print(f"CLAUDE_HEADPHONES_READY {meta['frameCount']} frames, {meta['encodedBytes']} packed bytes")


if __name__ == "__main__":
    main()
