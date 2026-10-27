from pathlib import Path
import subprocess, hashlib, argparse
parser=argparse.ArgumentParser();parser.add_argument('--digits-only',action='store_true');args=parser.parse_args()
r=Path(__file__).resolve().parents[1]; src=r/'.tools/font-builder/NotoSansCJKsc-Regular.otf'; conv=r/'.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js'
assert hashlib.sha256(src.read_bytes()).hexdigest() == '2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b', 'Unexpected Noto font source'
assert hashlib.sha256((r/'firmware/managed_components/lvgl__lvgl/scripts/built_in_font/Montserrat-Medium.ttf').read_bytes()).hexdigest() == '421f26b23e2be6b98373d32acd3cb2897b154d4bf0a77d26534ce476e4cbed53', 'Unexpected Montserrat font source'
chars=set(chr(x) for x in range(32,127))
for a in range(0xa1,0xf8):
 for b in range(0xa1,0xff):
  try: chars.add(bytes([a,b]).decode('gb2312'))
  except UnicodeDecodeError: pass
chars.update('·℃→')
chars.update('¥€£—•')  # Match the reply-body font, including halfwidth yen.
chars.update('\u00a0\u2010\u2011\u2012\u2013\u202f\u2212')
for name,size,font,symbols in [('tab5_font_30',30,src,''.join(sorted(chars))),('tab5_digits_72',72,r/'firmware/managed_components/lvgl__lvgl/scripts/built_in_font/Montserrat-Medium.ttf','0123456789:.-% °C'),('tab5_digits_96',96,r/'firmware/managed_components/lvgl__lvgl/scripts/built_in_font/Montserrat-Medium.ttf','0123456789:.-% °C'),('tab5_timer_digits_192',192,r/'firmware/managed_components/lvgl__lvgl/scripts/built_in_font/Montserrat-Medium.ttf','0123456789:')]:
 if args.digits_only and name=='tab5_font_30': continue
 compression=[]  # LVGL lossless compression preserves all glyphs and their coverage.
 subprocess.run(['node',str(conv),'--font',str(font),'--symbols',symbols,'--size',str(size),'--format','lvgl','--bpp','2' if name=='tab5_font_30' else '4','--no-kerning','--lv-include','lvgl.h','--lv-font-name',name,'-o',str(r/f'firmware/main/{name}.c')]+compression,check=True)
 print(name)

