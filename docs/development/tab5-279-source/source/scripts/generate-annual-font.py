"""Build native-size annual saver subsets from pinned OFL fonts."""
from pathlib import Path
import hashlib
import subprocess
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

root = Path(__file__).resolve().parents[1]
# Google Fonts commit 8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/nunito/Nunito[wght].ttf
source = root / '.tools/font-builder/Nunito-wght.ttf'
expected = 'bb55a5ca5c2042335b3991af27c4d0705d0ef41cac6164ac737fd8f2a1e85207'
if hashlib.sha256(source.read_bytes()).hexdigest() != expected:
    raise SystemExit('Unexpected Nunito source; verify provenance before generating.')
# https://github.com/CyanoHao/Resource-Han-Rounded/releases/download/v0.990/RHR-CN-0.990.7z
cjk = root / '.tools/font-builder/rhr-bold-0.990/ResourceHanRoundedCN-Bold.ttf'
if hashlib.sha256(cjk.read_bytes()).hexdigest() != 'f713907a21a10701cd68a7ce3e345ccdce46c789e1809d65ace54e095d7107c3':
    raise SystemExit('Unexpected Resource Han Rounded source; verify provenance before generating.')
weights = {}
for weight in (500, 800, 900):
    static = root / f'.tools/font-builder/Nunito-{weight}-annual.ttf'
    instantiateVariableFont(TTFont(source), {'wght': weight}, inplace=False).save(static)
    weights[weight] = str(static.relative_to(root))
converter = ['node', str(root / '.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js')]
for name, size, sources in [
    ('tab5_annual_digits_144', 144, ['--font', weights[900], '--symbols', '0123456789 -',
                                  '--font', weights[500], '--symbols', '%']),
    ('tab5_annual_text_26', 26, ['--font', weights[800], '-r', '0x20-0x7e',
                               '--font', str(cjk.relative_to(root)), '--symbols', '年度点阵等待校时一天格已过今之后还剩同步日期·，'])
]:
    # Lossless bitmap compression frees application space without changing
    # numeral outlines, grayscale or metrics. Leave the small text cacheable.
    compression = [] if name == 'tab5_annual_digits_144' else ['--no-compress']
    subprocess.run(converter + sources + compression + ['--size', str(size), '--format', 'lvgl', '--bpp', '4',
        '--no-kerning', '--lv-include', 'lvgl.h', '--lv-font-name', name,
        '-o', f'firmware/main/{name}.c'], check=True, cwd=root)
print('ANNUAL_FONT_OK digits900 percent500 at 144px; Nunito800/ResourceHanRounded700 labels26px; 4bpp')
