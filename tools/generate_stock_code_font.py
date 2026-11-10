#!/usr/bin/env python3
"""Regenerate the previous Roboto Mono table (the current design uses Font 4)."""
import hashlib
from pathlib import Path
import sys
from PIL import Image, ImageDraw, ImageFont

source = Path(sys.argv[1])
assert hashlib.sha256(source.read_bytes()).hexdigest() == '66a80e79d17e4c7cabd162e2916578a4cc08fd19eef6e2a643305eae9c567b2b'
font = ImageFont.truetype(str(source), 14)
font.set_variation_by_name('Medium')
lines = ['#pragma once', '#include <stdint.h>', '#if defined(ARDUINO)', '#include <pgmspace.h>', '#define STOCK_FONT_STORAGE PROGMEM', '#define STOCK_FONT_READ(p) pgm_read_byte(p)', '#else', '#define STOCK_FONT_STORAGE', '#define STOCK_FONT_READ(p) (*(p))', '#endif', '// Roboto Mono, SIL OFL 1.1; provenance and license in THIRD_PARTY_NOTICES.md.', '// 8x16, ASCII 32..126, 4-bit coverage, high nibble first.', 'static const uint8_t stockCodeGlyphs[95][64] STOCK_FONT_STORAGE = {']
for code in range(32, 127):
    glyph = chr(code)
    bounds = font.getbbox(glyph, anchor='ls')
    source_width = max(8, bounds[2] - min(0, bounds[0]))
    image = Image.new('L', (source_width, 16))
    ImageDraw.Draw(image).text((-min(0, bounds[0]), 13), glyph, font=font, fill=255, anchor='ls')
    if source_width > 8:
        image = image.resize((8, 16), Image.Resampling.LANCZOS)
    alpha = [(pixel * 15 + 127) // 255 for pixel in image.getdata()]
    packed = [alpha[i] * 16 + alpha[i + 1] for i in range(0, 128, 2)]
    lines.append('  {' + ','.join(f'0x{x:02x}' for x in packed) + '}, // ' + str(code))
lines += ['};', '']
Path('esp8266/src/StockCodeGlyphs.h').write_text('\n'.join(lines))
