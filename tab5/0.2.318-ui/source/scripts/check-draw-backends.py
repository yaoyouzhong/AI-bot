"""Check effective consumer flags and linked backends, not SDK defaults alone."""
from pathlib import Path
import json
import os
import subprocess
import sys

build=Path(sys.argv[1])
commands=json.loads((build/'compile_commands.json').read_text(encoding='utf8'))
flags=['LV_DRAW_SW_SUPPORT_L8=0','LV_DRAW_SW_SUPPORT_AL88=0','LV_DRAW_SW_SUPPORT_I1=0']
checked=0
for entry in commands:
    source=entry['file'].replace('\\','/')
    if '/lvgl__lvgl/src/' not in source and not source.endswith(('/main/ui.c','/lvgl9/esp_lvgl_port_disp.c')):
        continue
    for flag in flags:
        if '-D'+flag not in entry['command']:
            raise SystemExit('DRAW_BACKEND_FLAG_MISSING '+source+' '+flag)
    checked+=1
if checked<100:raise SystemExit('Missing LVGL compilation inventory')
nm=next(Path(os.environ['IDF_TOOLS_PATH'],'tools').rglob('riscv32-esp-elf-nm.exe'))
output=subprocess.check_output([str(nm),'-g','--defined-only',str(build/'aibot_tab5.elf')],text=True,encoding='utf8')
symbols={line.split()[-1] for line in output.splitlines() if line.split()}
for suffix in ['l8','al88','i1']:
    for kind in ['color','image']:
        name=f'lv_draw_sw_blend_{kind}_to_{suffix}'
        if name in symbols:raise SystemExit('UNUSED_DRAW_BACKEND_LINKED '+name)
for suffix in ['rgb565','rgb888','argb8888']:
    for kind in ['color','image']:
        name=f'lv_draw_sw_blend_{kind}_to_{suffix}'
        if name not in symbols:raise SystemExit('COLOR_DRAW_BACKEND_MISSING '+name)
print(f'DRAW_BACKEND_LINK_OK effectiveFlags={checked} sources; six gray entry points absent; six color entry points retained')
