#!/usr/bin/env python3
"""Generate stock glyph coverage from the firmware's pinned TFT_eSPI Font 4."""
import hashlib
from pathlib import Path
import re
import sys
from PIL import Image

source = Path(sys.argv[1])
assert hashlib.sha256(source.read_bytes()).hexdigest() == 'bb3af50de4aa4cc4d37b04f28bae6e99c0102d1eed9c881145cf024eff677910'
text = source.read_text(encoding='utf-8')
table = re.search(r'widtbl_f32\[96\].*?\{(.*?)\}', text, re.S).group(1)
widths = [int(n) for n in re.findall(r'\d+', re.sub(r'//[^\n]*', '', table))]
assert len(widths) == 96
lines = ['#pragma once', '#include <stdint.h>', '#if defined(ARDUINO)',
    '#include <pgmspace.h>', '#define STOCK_FONT_STORAGE PROGMEM',
    '#define STOCK_FONT_READ(p) pgm_read_byte(p)', '#else', '#define STOCK_FONT_STORAGE',
    '#define STOCK_FONT_READ(p) (*(p))', '#endif',
    '// Derived from TFT_eSPI 2.5.43 Font 4; see THIRD_PARTY_NOTICES.md.',
    '// 8x16, ASCII 32..126, 4-bit coverage, high nibble first.',
    'static const uint8_t stockCodeGlyphs[95][64] STOCK_FONT_STORAGE = {']
for code in range(32, 127):
    # Font 4 defines two alternatives for '$'; use the default (second) one.
    matches = re.findall(r'chr_f32_' + format(code, '02X') + r'\[\]\s*=\s*\{(.*?)\}', text, re.S | re.I)
    assert matches
    values = [int(v, 16) for v in re.findall(r'0x[0-9a-fA-F]+', matches[-1])]
    pixels = []
    for value in values:
        pixels.extend([255 if value & 128 else 0] * ((value & 127) + 1))
    width = widths[code - 32]
    assert len(pixels) == width * 26
    image = Image.new('L', (width, 26))
    image.putdata(pixels)
    # Keep the cell eight pixels wide but avoid stretching narrow glyphs such
    # as I, i and punctuation. Digits (14 pixels in Font 4) still fill the cell.
    glyph_width = min(8, max(1, (width * 16 + 13) // 26))
    image = image.resize((glyph_width, 16), Image.Resampling.LANCZOS)
    cell = Image.new('L', (8, 16))
    cell.paste(image, ((8 - glyph_width) // 2, 0))
    image = cell
    alpha = [(pixel * 15 + 127) // 255 for pixel in image.getdata()]
    packed = [alpha[i] * 16 + alpha[i + 1] for i in range(0, 128, 2)]
    lines.append('  {' + ','.join(f'0x{x:02x}' for x in packed) + '}, // ' + str(code))
lines += ['};', '']
Path('esp8266/src/StockCodeGlyphs.h').write_text('\n'.join(lines), encoding='utf-8')
