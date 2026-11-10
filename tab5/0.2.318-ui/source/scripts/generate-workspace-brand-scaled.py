"""Pack LVGL-rendered workspace icons, preserving their antialiased pixels.

Run the ui-preview binary with TAB5_BRAND_ASSET_GEN_ONLY=1 in a fresh
directory first, then pass that directory here.  This uses project-owned brand
art and the exact LVGL version/scale used by the production controls.
"""
from pathlib import Path
from struct import pack
import sys
from lz4.block import compress, decompress

source = Path(sys.argv[1])
output = Path(__file__).resolve().parents[1] / 'firmware/main/workspace_brand_scaled.h'
lines = ['// Generated from the original brand images by TAB5_BRAND_ASSET_GEN_ONLY using LVGL.',
         '#pragma once', '#include "lvgl.h"', '']
for i, name in enumerate(('codex', 'claude')):
    for j, label in enumerate(('narrow', 'wide')):
        raw = (source / f'icon-{i}-{j}.bin').read_bytes()
        width = (36, 48)[j]
        if len(raw) != width * width * 4:
            raise ValueError('unexpected rendered icon size')
        packed = compress(raw, mode='high_compression', compression=12, store_size=False)
        if decompress(packed, uncompressed_size=len(raw)) != raw:
            raise ValueError('image compression mismatch')
        data = pack('<III', 2, len(packed), len(raw)) + packed
        symbol = f'workspace_{name}_{label}'
        lines.append(f'static const uint8_t {symbol}_pixels[] __attribute__((aligned(4))) = {{')
        for offset in range(0, len(data), 16):
            lines.append('    ' + ', '.join(f'0x{value:02x}' for value in data[offset:offset+16]) + ',')
        lines += ['};', f'static const lv_image_dsc_t {symbol}_image = {{',
                  f'    .header = {{.magic=LV_IMAGE_HEADER_MAGIC, .cf=LV_COLOR_FORMAT_ARGB8888, .flags=LV_IMAGE_FLAGS_COMPRESSED, .w={width}, .h={width}, .stride={width*4}}},',
                  f'    .data_size=sizeof({symbol}_pixels), .data={symbol}_pixels', '};', '']
output.write_text('\n'.join(lines), encoding='utf-8')
