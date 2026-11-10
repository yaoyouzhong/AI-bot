"""Transcode a user-supplied original Codex atlas for a local TAB5 build.

The original artwork is not part of this project's MIT license. The generated
include is intentionally ignored by Git; supply the source atlas explicitly.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path
from PIL import Image

CELLS = [(0, n) for n in range(6)] + [(7, n) for n in range(6)] + [(3, n) for n in range(4)] + [(8, n) for n in range(6)] + [(5, n) for n in range(4)] + [(6, 1)]
WIDTH, HEIGHT = 192, 208

def pack(data):
    output = bytearray()
    i = 0
    while i < len(data):
        run = 1
        while run < 128 and i + run < len(data) and data[i + run] == data[i]:
            run += 1
        if run >= 3:
            output.extend((0x80 | (run - 1), data[i]))
            i += run
            continue
        start = i
        i += run
        while i < len(data) and i - start < 128:
            run = 1
            while run < 3 and i + run < len(data) and data[i + run] == data[i]:
                run += 1
            if run >= 3:
                break
            i += min(run, 128 - (i - start))
        output.append(i - start - 1)
        output.extend(data[start:i])
    return bytes(output)

def array(name, ctype, values, width=16):
    lines = [f'static const {ctype} {name}[{len(values)}] = {{']
    for i in range(0, len(values), width):
        lines.append('    ' + ','.join(str(v) for v in values[i:i + width]) + ',')
    return '\n'.join(lines + ['};'])

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--expected-sha256')
    args = parser.parse_args()
    digest = hashlib.sha256(args.input.read_bytes()).hexdigest()
    if args.expected_sha256 and args.expected_sha256.lower() != digest:
        raise SystemExit('Original atlas SHA-256 mismatch')
    atlas = Image.open(args.input).convert('RGBA')
    if atlas.size not in ((1536, 1872), (1536, 2288)):
        raise SystemExit('Expected an 8-column Codex atlas with 192x208 cells')
    combined = Image.new('RGBA', (WIDTH, HEIGHT * len(CELLS)))
    for index, (row, column) in enumerate(CELLS):
        combined.paste(atlas.crop((column * WIDTH, row * HEIGHT, (column + 1) * WIDTH, (row + 1) * HEIGHT)), (0, index * HEIGHT))
    indexed = combined.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    rgba_palette = indexed.getpalette('RGBA')
    rgba_palette += [0] * (1024 - len(rgba_palette))
    palette, alpha = [], []
    for index in range(256):
        r, g, b, a = rgba_palette[index * 4:index * 4 + 4]
        palette.append(((r * 31 + 127) // 255 << 11) | ((g * 63 + 127) // 255 << 5) | ((b * 31 + 127) // 255))
        alpha.append(a)
    encoded = bytearray()
    records = []
    for index in range(len(CELLS)):
        raw = indexed.crop((0, index * HEIGHT, WIDTH, (index + 1) * HEIGHT)).tobytes()
        data = pack(raw)
        records.append((len(encoded), len(data)))
        encoded.extend(data)
    sections = [f'/* Original Codex artwork, SHA-256 {digest}. Local supplied asset; not AI-bot MIT. */',
                array('codex_pet_palette', 'uint16_t', palette), array('codex_pet_alpha', 'uint8_t', alpha),
                array('codex_pet_stream', 'uint8_t', list(encoded), 24),
                'static const struct { uint32_t offset, size; } codex_pet_frames[] = {\n' + ''.join(f'    {{{offset},{size}}},\n' for offset, size in records) + '};\n']
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text('\n'.join(sections), encoding='utf-8')
    manifest = {'owner': 'OpenAI', 'asset': 'Original Codex companion spritesheet', 'source': str(args.input.resolve()),
                'sourceSha256': digest, 'sourceSize': list(atlas.size), 'cellSize': [WIDTH, HEIGHT], 'frames': CELLS,
                'encodedBytes': len(encoded), 'paletteBytes': 768, 'frameTableBytes': len(records) * 8,
                'decodedFrameBytes': WIDTH * HEIGHT * 3, 'conversion': '256-entry RGBA palette, RGB565A8, bounded PackBits',
                'scope': 'User-supplied local build artwork; not covered by AI-bot MIT; redistribution rights not inferred from Codex source license.'}
    args.output.with_suffix('.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({key: manifest[key] for key in ('encodedBytes', 'paletteBytes', 'frameTableBytes', 'decodedFrameBytes')}))

if __name__ == '__main__':
    main()
