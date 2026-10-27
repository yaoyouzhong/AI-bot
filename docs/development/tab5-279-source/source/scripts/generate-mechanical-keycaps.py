"""Original procedural materials for the Codex Direct mechanical keys.

No photographed or downloaded textures. LZ4 losslessly compresses RGB565 and
its alpha plane; deterministic dithering avoids visible 16-bit gradient steps.
Re-run with Pillow and lz4 installed.
"""
from math import atan2, cos, exp, hypot, pi, sin
from pathlib import Path
from struct import pack
from PIL import Image
from lz4.block import compress, decompress

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "firmware/main/mechanical_keycaps.h"
PREVIEW = ROOT / "artifacts/ui-preview"


def clamp(value, lo=0, hi=255):
    return max(lo, min(hi, value))


def mix(a, b, t):
    return tuple(x * (1 - t) + y * t for x, y in zip(a, b))


def grain(x, y):
    # A repeatable fine grain, with no random dependency or generated file state.
    return (((x * 73856093) ^ (y * 19349663)) & 255) / 255 - .5


def segment_distance(x, y, ax, ay, bx, by):
    t = clamp(((x-ax)*(bx-ax)+(y-ay)*(by-ay))/((bx-ax)**2+(by-ay)**2), 0, 1)
    return hypot(x-ax-t*(bx-ax), y-ay-t*(by-ay))


def collar(x, y):
    dx, dy = x - 125.5, y - 125.5
    r, a = hypot(dx, dy), atan2(dy, dx)
    # Soft contact shadow and the lowered black outer barrel.
    rs = hypot(dx, (dy - 10) * 1.025)
    alpha = 170 * exp(-max(0, rs - 119) ** 2 / 24)
    color = (2, 5, 8)
    if rs < 121:
        color = mix((16, 20, 24), (5, 8, 11), clamp((dy + 80) / 220, 0, 1))
        alpha = 255
    if r < 122:
        # Reflected soft boxes make the machined metal read as metal.
        light = .40 + .36 * cos(a + 2.25) + .18 * cos(2 * a - .3)
        light = clamp(light, 0, 1)
        brush = 2.2 * sin(r * 6.4) + 1.4 * grain(x, y)
        if r > 118:
            color = mix((51, 58, 64), (228, 233, 234), light)
        elif r > 107:
            steel = .22 + .68 * light + .10 * cos((r - 107) * pi / 11)
            color = mix((40, 48, 56), (193, 204, 210), steel)
        elif r > 103:
            color = mix((14, 20, 25), (144, 159, 169), 1 - light)
        elif r > 101:
            color = mix((39, 48, 55), (216, 224, 226), light)
        else:
            color = mix((3, 7, 11), (20, 26, 32), clamp((dy + 100) / 190, 0, 1))
        color = tuple(c + brush for c in color)
        alpha = clamp((122.6 - r) * 255) if r > 121.6 else 255
    return color, alpha


def cap(x, y, red):
    blue = red == "blue"
    side_colors = ((15, 49, 127), (7, 20, 65)) if blue else (((99, 14, 19), (37, 4, 10)) if red else ((30, 38, 48), (7, 12, 19)))
    edge_colors = ((10, 47, 147), (153, 203, 255)) if blue else (((83, 6, 16), (248, 137, 128)) if red else ((17, 24, 33), (139, 155, 169)))
    dx, dy = x - 99.5, y - 99.5
    r = hypot(dx, dy)
    rs = hypot(dx, dy - 10)
    alpha = 145 * exp(-max(0, rs - 94) ** 2 / 12)
    color = (1, 3, 5)
    if rs < 96:
        # Visible cylindrical side wall, independent of the moving top highlight.
        side = clamp((dy + 20) / 130, 0, 1)
        color = mix(*side_colors, side)
        alpha = 255
    if r < 96:
        a = atan2(dy, dx)
        light = clamp(.48 + .48 * cos(a + 2.2), 0, 1)
        if r > 92:
            t = (r - 92) / 4
            edge = .25 + .65 * light - .23 * t
            color = mix(*edge_colors, edge)
        else:
            nx, ny = dx / 96, dy / 96
            dome = max(0, 1 - (nx * nx + ny * ny)) ** .42
            illumination = .64 + .27 * dome - .24 * ny - .12 * nx
            color = tuple(c * illumination for c in ((40, 110, 240) if blue else (161, 22, 30) if red else (38, 50, 65)))
            # Broad upper-left reflected window, fading before the central icon.
            window = exp(-((dx + 28) / 66) ** 2 - ((dy + 61) / 20) ** 2)
            sheen = exp(-((dx + 57) / 21) ** 2 - ((dy + 30) / 48) ** 2)
            color = mix(color, (204, 230, 255) if blue else (255, 207, 189) if red else (153, 177, 199), window * .49 + sheen * .18)
            color = tuple(c + grain(x, y) * 1.6 for c in color)
            if blue:
                # Reference send button: sampled solid sRGB #3A83F7. Keep the
                # physical collar/edge while matching the face's exact blue.
                color = (58, 131, 247)
                # A 10px round-ended arrow, geometrically centered on the face.
                # Rasterize with the cap so partial refresh cannot clip its legs.
                distance = min(segment_distance(x,y,67.5,99.5,99.5,67.5),
                               segment_distance(x,y,99.5,67.5,131.5,99.5),
                               segment_distance(x,y,99.5,67.5,99.5,131.5))
                color = mix(color, (255,255,255), clamp(5.5-distance,0,1))
        alpha = clamp((96.6 - r) * 255) if r > 95.6 else 255
    return color, alpha


def broom_sample(x, y):
    # Original emoji-like broom: a wooden handle and broad golden bristles.
    # Its diagonal silhouette stays recognizable in the actual 24px control.
    if segment_distance(x, y, 11.1, 11.5, 21.0, 2.0) < 1.25:
        edge = segment_distance(x, y, 11.1, 11.5, 21.0, 2.0)
        return mix((225, 166, 94), (143, 85, 43), edge / 1.25), 255
    points = ((10.0, 9.0), (14.5, 13.5), (8.0, 22.2), (0.8, 15.0))
    if all((bx-ax)*(y-ay)-(by-ay)*(x-ax) >= 0
           for (ax, ay), (bx, by) in zip(points, points[1:]+points[:1])):
        if x+y < 26.3:
            return (168, 111, 45) if x+y < 24.8 else (211, 141, 56), 255
        color = mix((250, 205, 105), (219, 157, 62), clamp((x+y-26)/5, 0, 1))
        for bx, by in ((2.0, 15.8), (3.8, 17.6), (5.6, 19.4), (7.4, 21.2)):
            if segment_distance(x, y, 11.8, 12.4, bx, by) < .32:
                color = mix(color, (164, 108, 37), .55)
        return color, 255
    return (0, 0, 0), 0


def broom(x, y):
    # Supersample only this small silhouette; existing key textures are unchanged.
    samples = [broom_sample(x+dx, y+dy)
               for dy in (-.375, -.125, .125, .375)
               for dx in (-.375, -.125, .125, .375)]
    opacity = sum(a for _, a in samples)
    color = tuple(sum(c[i]*a for c, a in samples)/opacity for i in range(3)) if opacity else (0, 0, 0)
    return color, opacity/len(samples)


def render(name, width, height, shader):
    rgba, rgb565, alpha = bytearray(), bytearray(), bytearray()
    # Ordered noise before quantization is also present in the actual firmware.
    for y in range(height):
        for x in range(width):
            color, opacity = shader(x, y)
            dither = grain(x + 317, y + 911)
            r, g, b = (clamp(round(c + dither * step)) for c, step in zip(color, (7, 3, 7)))
            packed = (int(r) >> 3) << 11 | (int(g) >> 2) << 5 | (int(b) >> 3)
            rgb565.extend((packed & 255, packed >> 8))
            opacity = int(clamp(round(opacity)))
            alpha.append(opacity)
            rgba.extend((int(r), int(g), int(b), opacity))
    image = Image.frombytes("RGBA", (width, height), bytes(rgba))
    image.save(PREVIEW / f"{name}-material.png")
    raw = bytes(rgb565 + alpha)
    compressed = compress(raw, mode="high_compression", compression=12, store_size=False)
    assert decompress(compressed, uncompressed_size=len(raw)) == raw
    data = pack("<III", 2, len(compressed), len(raw)) + compressed
    lines = [f"static const uint8_t {name}_pixels[] __attribute__((aligned(4))) = {{"]
    for offset in range(0, len(data), 24):
        lines.append("    " + ",".join(f"0x{v:02x}" for v in data[offset:offset + 24]) + ",")
    lines += ["};", f"static const lv_image_dsc_t {name}_image = {{",
              "    .header = {.magic = LV_IMAGE_HEADER_MAGIC, .cf = LV_COLOR_FORMAT_RGB565A8, .flags = LV_IMAGE_FLAGS_COMPRESSED,",
              f"               .w = {width}, .h = {height}, .stride = {width * 2}}},",
              f"    .data_size = sizeof({name}_pixels), .data = {name}_pixels,", "};", ""]
    return lines, len(data)


PREVIEW.mkdir(parents=True, exist_ok=True)
lines = ["// Original procedural material; generated by scripts/generate-mechanical-keycaps.py.",
         "#pragma once", '#include "lvgl.h"',
         "#if !LV_USE_LZ4_INTERNAL || !LV_BIN_DECODER_RAM_LOAD",
         '#error "Mechanical key materials require the bundled LVGL LZ4 RAM decoder."', "#endif", ""]
total = 0
for name, width, height, shader in (
    ("quick_collar", 252, 264, collar),
    ("quick_graphite_cap", 200, 212, lambda x, y: cap(x, y, False)),
    ("quick_red_cap", 200, 212, lambda x, y: cap(x, y, True)),
    ("quick_blue_cap", 200, 212, lambda x, y: cap(x, y, "blue")),
    ("quick_broom", 24, 24, broom),
):
    block, size = render(name, width, height, shader)
    lines.extend(block)
    total += size
OUTPUT.write_text("\n".join(lines), encoding="utf-8")
print(f"MECHANICAL_MATERIALS_READY bytes={total} original_procedural_rgb565_alpha_lossless_lz4")
