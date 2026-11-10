"""Regenerate the vendored LVGL font using local Noto Sans CJK and lv_font_conv 1.5.3."""
from pathlib import Path
import hashlib
import subprocess
import argparse
import json
import re
import runpy
import tempfile
from fontTools.ttLib import TTFont

parser = argparse.ArgumentParser()
parser.add_argument('--art-guides', type=Path)
args = parser.parse_args()

root = Path(__file__).resolve().parents[1]
font = root / '.tools/font-builder/NotoSansCJKsc-Regular.otf'
expected = '2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b'
if hashlib.sha256(font.read_bytes()).hexdigest() != expected:
    raise SystemExit('Unexpected source font; verify provenance before regenerating.')
chars = set(chr(x) for x in range(32, 127))
for a in range(0xa1, 0xf8):
    for b in range(0xa1, 0xff):
        try:
            chars.add(bytes([a, b]).decode('gb2312'))
        except UnicodeDecodeError:
            pass
chars.update('·℃→')
chars.update('¥€£—•')  # Reply amounts and ordinary Unicode punctuation beyond GB2312.
chars.update('\u00a0\u2010\u2011\u2012\u2013\u202f\u2212')
chars.update('‹›↤')

# Gallery editions add characters independently of ordinary UI text. Keep all
# glyphs already present when refreshing from a newer catalogue.
output_font = root / 'firmware/main/tab5_font_24.c'
preserved_chars = set()
if output_font.exists():
    existing = output_font.read_text(encoding='utf-8')
    lists = {
        name: [int(value, 0) for value in re.findall(r'0x[0-9a-fA-F]+|\b\d+\b', body)]
        for name, body in re.findall(
            r'static const uint(?:8|16)_t\s+((?:unicode|glyph_id_ofs)_list_\d+)\[\]\s*=\s*\{(.*?)\};',
            existing, re.S)
    }
    for start, length, unicode_list, offsets, kind in re.findall(
        r'\.range_start = (\d+), \.range_length = (\d+),.*?\.unicode_list = (\w+), \.glyph_id_ofs_list = (\w+),.*?\.type = (LV_FONT_FMT_TXT_CMAP_\w+)',
        existing, re.S):
        start, length = int(start), int(length)
        if 'SPARSE' in kind:
            codepoints = (start + value for value in lists[unicode_list])
        elif kind.endswith('FORMAT0_TINY'):
            codepoints = range(start, start + length)
        elif kind.endswith('FORMAT0_FULL'):
            codepoints = (start + i for i, value in enumerate(lists[offsets]) if i == 0 or value)
        else:
            raise SystemExit('Unsupported existing font cmap: ' + kind)
        preserved_chars.update(chr(value) for value in codepoints)
    if not preserved_chars:
        raise SystemExit('Existing font has no readable cmaps; refusing to drop its glyphs.')
    chars.update(preserved_chars)
guide_chars = set()
if args.art_guides:
    guides = json.loads(args.art_guides.read_text(encoding='utf-8-sig'))['works']
    for guide in guides:
        for field in ('title', 'author', 'period', 'museum', 'brief', 'overview', 'looking'):
            guide_chars.update(guide.get(field, ''))
        original = guide.get('transcription', {})
        if original.get('status') in ('source_verified', 'reference_verified'):
            guide_chars.update(original.get('text', '') + original.get('explanation', ''))
    guide_chars.difference_update('\r\n')
    chars.update(guide_chars)
with TTFont(font) as source:
    available = set(source.getBestCmap())
fallback_chars = {c for c in chars if ord(c) not in available}
fallback_args = []
if fallback_chars:
    remaining = set(fallback_chars)
    for name, expected_sha in [
        ('plangothic-2.9.5795/PlangothicP1-Regular.ttf', '322a96735fd2ee13bb58337e338d891eda3c3a8231f866222e684c9a4b1b5833'),
        ('NotoSans-Regular.ttf', 'b85c38ecea8a7cfb39c24e395a4007474fa5a4fc864f6ee33309eb4948d232d5'),
        ('NotoSansMath-Regular.ttf', '80b61fd613d3519197e64fff6f7e71fdc7f3e6526440ea4115b554ef7fd59af7'),
    ]:
        if not remaining:
            break
        fallback = root / '.tools/font-builder' / name
        if hashlib.sha256(fallback.read_bytes()).hexdigest() != expected_sha:
            raise SystemExit('Unexpected supplemental font: ' + name)
        with TTFont(fallback) as source:
            supported = {c for c in remaining if ord(c) in source.getBestCmap()}
        if supported:
            fallback_args.extend(['--font', str(fallback), '--symbols', ''.join(sorted(supported))])
            remaining.difference_update(supported)
    if remaining:
        raise SystemExit('Unsupported required characters: ' + ', '.join(f'U+{ord(c):04X}' for c in sorted(remaining)))
previous_font = output_font.read_text(encoding='utf-8') if output_font.exists() else None
with tempfile.TemporaryDirectory(prefix='tab5-font-') as task_temp:
    generated_font = Path(task_temp) / 'tab5_font_24.c'
    subprocess.run([
    'node', str(root / '.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js'),
    '--font', str(font), '--symbols', ''.join(sorted(chars - fallback_chars)),
    *fallback_args, '--size', '24',
    '--format', 'lvgl', '--bpp', '2', '--no-kerning',
    '--lv-include', 'lvgl.h', '--lv-font-name', 'tab5_font_24',
    '-o', str(generated_font)
    ], check=True)
    generated = generated_font.read_text(encoding='utf-8').replace(str(generated_font), str(output_font))
    if previous_font:
        generated = runpy.run_path(str(root / 'scripts/preserve-font.py'))['extend_font'](previous_font, generated)
    output_font.write_text(generated, encoding='utf-8', newline='\n')
print(f'Generated font for {len(chars)} requested characters; all {len(guide_chars)} gallery characters covered; {len(fallback_chars)} supplemental glyphs; {len(preserved_chars)} existing glyphs retained.')
