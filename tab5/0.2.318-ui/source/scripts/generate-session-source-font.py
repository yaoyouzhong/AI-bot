"""Existing licensed Noto subset for source metadata and unread labels."""
from pathlib import Path
import hashlib, subprocess
root=Path(__file__).resolve().parents[1]
font=root/'.tools/font-builder/NotoSansCJKsc-Regular.otf'
assert hashlib.sha256(font.read_bytes()).hexdigest()=='2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b'
symbols=''.join(chr(i) for i in range(32,127))+'来源未知原生终端多端打开后台会话未读'
subprocess.run(['node',str(root/'.tools/font-builder/node_modules/lv_font_conv/lv_font_conv.js'),
    '--font',str(font),'--symbols',symbols,'--size','20','--format','lvgl','--bpp','2','--no-kerning',
    '--lv-include','lvgl.h','--lv-font-name','tab5_source_20','-o',str(root/'firmware/main/tab5_source_20.c')],check=True)
