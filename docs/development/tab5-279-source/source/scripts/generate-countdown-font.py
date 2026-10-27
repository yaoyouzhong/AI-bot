"""Build a same-size, medium-weight countdown subset; keep regular CJK fallback."""
from pathlib import Path
import hashlib
import re
import subprocess
from fontTools.ttLib import TTFont

root = Path(__file__).resolve().parents[1]
source = root / '.tools/font-builder/NotoSansCJKsc-Medium.otf'
assert hashlib.sha256(source.read_bytes()).hexdigest() == 'ca094f6b0001fb048ca39ddd797a0cdb0179e1e55c6561e111c49c3e6a61d7b7'
output = root / 'firmware/main/tab5_countdown_24.c'
subprocess.run(['node', str(root / '.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js'),
    '--font', str(source), '--symbols', '0123456789dhm', '--size', '24', '--format', 'lvgl',
    '--bpp', '2', '--no-compress', '--no-kerning', '--lv-include', 'lvgl.h',
    '--lv-font-name', 'tab5_countdown_24', '--lv-fallback', 'tab5_font_24', '-o', str(output)], check=True)
# Match the existing label's line box and baseline, including its regular CJK fallback.
text = output.read_text(encoding='utf-8')
text = re.sub(r'\.line_height = \d+,', '.line_height = 31,', text)
text = re.sub(r'\.base_line = \d+,', '.base_line = 7,', text)
# Keep the countdown's original character advances so right alignment and gaps
# do not shift when only the stroke weight changes.
regular_path = root / '.tools/font-builder/NotoSansCJKsc-Regular.otf'
assert hashlib.sha256(regular_path.read_bytes()).hexdigest() == '2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b'
regular = TTFont(regular_path)
advances = iter([0] + [round(regular['hmtx'][regular.getBestCmap()[ord(c)]][0] * 24 * 16 / regular['head'].unitsPerEm)
                      for c in sorted('0123456789dhm')])
text = re.sub(r'\.adv_w = \d+', lambda m: '.adv_w = ' + str(next(advances)), text)
output.write_text(text, encoding='utf-8')
print('COUNTDOWN_FONT_OK Noto Medium 24px 2bpp; regular CJK/space fallback; 31px line/7px baseline')
