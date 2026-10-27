"""Generate the sourced poem table and calendar subsets from pinned OFL fonts."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
font = root / '.tools/font-builder/NotoSansCJKsc-Regular.otf'
if hashlib.sha256(font.read_bytes()).hexdigest() != '2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b':
    raise SystemExit('Unexpected Noto font source')
chars = set(chr(i) for i in range(32, 127))
for name in ('ui_monthly_saver.h', 'ui_calendar.h'):
    chars.update(c for c in (root / 'firmware/main' / name).read_text(encoding='utf-8') if ord(c) >= 128)
subprocess.run(['node', str(root / '.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js'),
    '--font', str(font), '--symbols', ''.join(sorted(chars)), '--size', '20', '--format', 'lvgl',
    '--bpp', '2', '--no-kerning', '--lv-include', 'lvgl.h',
    '--lv-font-name', 'tab5_calendar_text_20', '-o', str(root / 'firmware/main/tab5_calendar_text_20.c')], check=True)
print(f'CALENDAR_FONT_OK {len(chars)} glyphs at 20px')

classical = root / '.tools/font-builder/wenkai-1.522/LXGWWenKai-Regular.ttf'
if hashlib.sha256(classical.read_bytes()).hexdigest() != '39ad71264b588165b469e35e6afb162a378dacd1f95348160240ba9038ac3009':
    raise SystemExit('Unexpected LXGW WenKai 1.522 source')
faces = {
    'wenkai': ('tab5_calendar_verse_24', classical),
    'brush': ('tab5_calendar_brush_24', root / '.tools/font-builder/calligraphy/MaShanZheng-Regular.ttf'),
    'running': ('tab5_calendar_running_28', root / '.tools/font-builder/calligraphy/ZhiMangXing-Regular.ttf'),
}
for key, expected in (
    ('brush', '6d2546bb189c732a8ca29af9e22457b152387d158aa459e4ac2ce1e51788b7fb'),
    ('running', '644e0cae9b40f0b10ab729a01bd32032e3973bac22be3dccae01bf6ae7fde969'),
):
    if hashlib.sha256(faces[key][1].read_bytes()).hexdigest() != expected:
        raise SystemExit(f'Unexpected {key} source')
poems = json.loads((root / 'firmware/assets/calendar-poems.json').read_text(encoding='utf-8'))['poems']
terms = re.search(r'calendar_terms\[\]=\{([^}]+)', (root / 'firmware/main/ui_calendar.h').read_text(encoding='utf-8')).group(1)
assert [p['term'] for p in poems] == re.findall(r'"([^"]+)"', terms)
assert len({tuple(p['lines']) for p in poems}) == 24
for p in poems:
    assert len(p['lines']) == 4 and len(set(map(len, p['lines']))) == 1
    assert len(p['lines'][0]) in (5, 7) and p['source'].startswith('https://')
    assert p['font'] in faces
quote = lambda s: json.dumps(s, ensure_ascii=False)
table = ['// Generated from firmware/assets/calendar-poems.json; do not edit.',
         'typedef struct {const char *title,*byline,*lines[4];const lv_font_t *font;} monthly_poem;',
         'static const monthly_poem monthly_poems[]={']
for p in poems:
    table.append('    {' + quote(p['title']) + ',' + quote(p['dynasty'] + ' · ' + p['author']) + ',{' + ','.join(map(quote, p['lines'])) + '},&' + faces[p['font']][0] + '},')
table.append('};\n')
(root / 'firmware/main/calendar_poems.h').write_text('\n'.join(table), encoding='utf-8')
cover = (root / 'firmware/main/ui_monthly_saver.h').read_text(encoding='utf-8')
themes = cover.split('monthly_themes[]={', 1)[1].split('};', 1)[0]
verses = re.findall(r'"([^"\n]*，[^"\n]*)"', themes)
assert len(verses) == 12
verse_chars = set(''.join(verses) + '等待校时') - set('，。')
verse_chars.update(''.join(p['term'] for p in poems))
meta_chars = set(' ·' + ''.join(p['title'] + p['dynasty'] + p['author'] for p in poems))
subsets = [('tab5_calendar_month_48', 48, 4, classical, '一二三四五六七八九十月'),
           ('tab5_calendar_meta_18', 18, 4, classical, ''.join(sorted(meta_chars)))]
for key, (name, source) in faces.items():
    symbols = set(''.join(''.join(p['lines']) for p in poems if p['font'] == key))
    if key == 'wenkai':
        symbols.update(verse_chars)
    subsets.append((name, 28 if key == "running" else 24, 4, source, ''.join(sorted(symbols))))
for name, size, bpp, source, symbols in subsets:
    with TTFont(source) as font_source:
        missing = set(map(ord, symbols)) - set(font_source.getBestCmap())
        assert not missing, f'{name}: missing codepoints {sorted(missing)}'
    subprocess.run(['node', str(root / '.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js'),
        '--font', str(source), '--symbols', symbols, '--size', str(size), '--format', 'lvgl',
        '--bpp', str(bpp), '--no-kerning', '--lv-include', 'lvgl.h',
        '--lv-font-name', name, '-o', str(root / f'firmware/main/{name}.c')], check=True)
    print(f'CALENDAR_CLASSICAL_FONT_OK {name}: {len(symbols)} glyphs, {size}px, {bpp}bpp, lossless compression')
