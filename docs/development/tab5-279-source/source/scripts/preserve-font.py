"""Extend a compressed LVGL font without rerasterizing existing glyphs."""
import re


def read_font(text):
    def array(name):
        return re.search(r'static\s+(?:LV_ATTRIBUTE_LARGE_CONST\s+)?const\s+\w+\s+' + name + r'\[\]\s*=\s*\{(.*?)\};', text, re.S)

    def numbers(body):
        body = re.sub(r'/\*.*?\*/', '', body, flags=re.S)
        return [int(x, 0) for x in re.findall(r'0x[0-9a-fA-F]+|\b\d+\b', body)]

    bitmap_match, glyph_match, cmap_match = (array(n) for n in ('glyph_bitmap', 'glyph_dsc', 'cmaps'))
    if not all((bitmap_match, glyph_match, cmap_match)):
        raise ValueError('Missing LVGL font arrays')
    bitmap = numbers(bitmap_match.group(1))
    descriptors = re.findall(r'\{[^{}]*\.bitmap_index\s*=\s*\d+[^{}]*\}', glyph_match.group(1))
    lists = {name: numbers(body) for name, body in re.findall(
        r'static const uint(?:8|16)_t\s+((?:unicode|glyph_id_ofs)_list_\d+)\[\]\s*=\s*\{(.*?)\};', text, re.S)}
    mapping = {}
    for start, length, gid, unicode_list, offsets, kind in re.findall(
        r'\.range_start = (\d+), \.range_length = (\d+), \.glyph_id_start = (\d+),\s*'
        r'\.unicode_list = (\w+), \.glyph_id_ofs_list = (\w+),.*?\.type = (LV_FONT_FMT_TXT_CMAP_\w+)', cmap_match.group(1), re.S):
        start, length, gid = int(start), int(length), int(gid)
        if 'SPARSE' in kind:
            points = lists[unicode_list]
            values = lists[offsets] if offsets != 'NULL' else range(len(points))
        else:
            points = range(length)
            values = lists[offsets] if offsets != 'NULL' else range(length)
        for point, offset in zip(points, values):
            if offsets != 'NULL' and 'FORMAT0_FULL' in kind and point and not offset:
                continue
            mapping[start + point] = gid + offset
    if not mapping or max(mapping.values()) >= len(descriptors):
        raise ValueError('Invalid LVGL font cmap')
    return bitmap, descriptors, mapping, bitmap_match, glyph_match, cmap_match


def extend_font(previous, generated):
    for field in ('bpp', 'bitmap_format'):
        a = re.search(r'\.' + field + r'\s*=\s*(\d+)', previous).group(1)
        b = re.search(r'\.' + field + r'\s*=\s*(\d+)', generated).group(1)
        if a != b:
            raise ValueError('Cannot combine different ' + field)
    old_bytes, old_desc, old_map, *_ = read_font(previous)
    new_bytes, new_desc, new_map, bitmap_match, glyph_match, cmap_match = read_font(generated)
    if not set(old_map).issubset(new_map):
        raise ValueError('Generator dropped existing codepoints')
    bitmap, descriptors, mapping = list(old_bytes), list(old_desc), dict(old_map)
    indices = [int(re.search(r'\.bitmap_index\s*=\s*(\d+)', d).group(1)) for d in new_desc]
    for cp in sorted(set(new_map) - set(old_map)):
        gid = new_map[cp]
        end = indices[gid + 1] if gid + 1 < len(indices) else len(new_bytes)
        mapping[cp] = len(descriptors)
        descriptors.append(re.sub(r'(\.bitmap_index\s*=\s*)\d+', lambda m: m.group(1) + str(len(bitmap)), new_desc[gid]))
        bitmap.extend(new_bytes[indices[gid]:end])
    if len(descriptors) >= 65536:
        raise ValueError('Too many glyphs for LVGL uint16 cmap offsets')
    groups = []
    for cp in sorted(mapping):
        if not groups or cp - groups[-1][0] >= 65535:
            groups.append([])
        groups[-1].append(cp)
    arrays, cmaps = [], []
    for i, points in enumerate(groups):
        start = points[0]
        for name, values in [('unicode_list', [cp - start for cp in points]), ('glyph_id_ofs_list', [mapping[cp] - 1 for cp in points])]:
            rows = [', '.join(str(v) for v in values[j:j + 16]) for j in range(0, len(values), 16)]
            arrays.append(f'static const uint16_t {name}_{i}[] = {{\n    ' + ',\n    '.join(rows) + '\n};\n')
        cmaps.append(f'    {{.range_start = {start}, .range_length = {points[-1] - start + 1}, .glyph_id_start = 1,\n'
                     f'     .unicode_list = unicode_list_{i}, .glyph_id_ofs_list = glyph_id_ofs_list_{i}, .list_length = {len(points)}, .type = LV_FONT_FMT_TXT_CMAP_SPARSE_FULL}}')
    replacement = 'static const lv_font_fmt_txt_cmap_t cmaps[] = {\n' + ',\n'.join(cmaps) + '\n};'
    # Remove the converter's old mapping arrays together with its cmap table.
    first_array = re.search(r'static const uint(?:8|16)_t (?:unicode|glyph_id_ofs)_list_\d+\[\]', generated)
    cmap_start = first_array.start() if first_array else cmap_match.start()
    sections = [
        (bitmap_match.start(1), bitmap_match.end(1), '\n' + '\n'.join('    ' + ', '.join(hex(v) for v in bitmap[j:j + 16]) + ',' for j in range(0, len(bitmap), 16)) + '\n'),
        (glyph_match.start(1), glyph_match.end(1), '\n    ' + ',\n    '.join(descriptors) + '\n'),
        (cmap_start, cmap_match.end(), '\n'.join(arrays) + '\n' + replacement),
    ]
    result = generated
    for start, end, value in sorted(sections, reverse=True):
        result = result[:start] + value + result[end:]
    result = re.sub(r'(\.cmap_num\s*=\s*)\d+', lambda m: m.group(1) + str(len(groups)), result)
    for field in ('line_height', 'base_line'):
        value = re.search(r'\.' + field + r'\s*=\s*(\d+)', previous).group(1)
        result = re.sub(r'(\.' + field + r'\s*=\s*)\d+', lambda m: m.group(1) + value, result)
    return compact_font('/* Existing glyph metrics and compressed bitmaps preserved; only new codepoints appended. */\n' + result)


def compact_font(text):
    """Reorder existing glyphs so the cmap needs no per-character glyph IDs.

    Copy encoded bitmaps and metrics verbatim; never rasterize or remove a glyph.
    Keep the original table if it is already smaller than the simple sparse map.
    """
    bitmap, descriptors, mapping, bitmap_match, glyph_match, cmap_match = read_font(text)
    assert re.search(r'\.kern_dsc\s*=\s*NULL', text), 'Kerning needs glyph ID remapping'
    assert set(mapping.values()) == set(range(1, len(descriptors))), 'Unexpected aliased or unused glyphs'
    first_array = re.search(r'static const uint(?:8|16)_t (?:unicode|glyph_id_ofs)_list_\d+\[\]', text)
    cmap_start = first_array.start() if first_array else cmap_match.start()
    old_arrays = text[cmap_start:cmap_match.start()]
    old_bytes = sum((1 if bits == '8' else 2) * len(re.findall(r'0x[0-9a-fA-F]+|\b\d+\b', re.sub(r'/\*.*?\*/', '', body, flags=re.S)))
                    for bits, body in re.findall(r'static const uint(8|16)_t \w+\[\]\s*=\s*\{(.*?)\};', old_arrays, re.S))
    old_bytes += int(re.search(r'\.cmap_num\s*=\s*(\d+)', text)[1]) * 24
    groups = []
    for cp in sorted(mapping):
        if not groups or cp - groups[-1][0] >= 65535:
            groups.append([])
        groups[-1].append(cp)
    if len(mapping) * 2 + len(groups) * 24 >= old_bytes:
        return text
    indices = [int(re.search(r'\.bitmap_index\s*=\s*(\d+)', d)[1]) for d in descriptors]
    packed, ordered = [], [descriptors[0]]
    arrays, cmaps = [], []
    for i, points in enumerate(groups):
        first_id = len(ordered)
        for cp in points:
            gid = mapping[cp]
            end = indices[gid + 1] if gid + 1 < len(indices) else len(bitmap)
            ordered.append(re.sub(r'(\.bitmap_index\s*=\s*)\d+', lambda m: m[1] + str(len(packed)), descriptors[gid]))
            packed.extend(bitmap[indices[gid]:end])
        offsets = [cp - points[0] for cp in points]
        rows = [', '.join(str(v) for v in offsets[j:j + 16]) for j in range(0, len(offsets), 16)]
        arrays.append(f'static const uint16_t unicode_list_{i}[] = {{\n    ' + ',\n    '.join(rows) + '\n};\n')
        cmaps.append(f'    {{.range_start = {points[0]}, .range_length = {points[-1] - points[0] + 1}, .glyph_id_start = {first_id},\n'
                     f'     .unicode_list = unicode_list_{i}, .glyph_id_ofs_list = NULL, .list_length = {len(points)}, .type = LV_FONT_FMT_TXT_CMAP_SPARSE_TINY}}')
    assert len(packed) == len(bitmap)
    sections = [
        (bitmap_match.start(1), bitmap_match.end(1), '\n' + '\n'.join('    ' + ', '.join(hex(v) for v in packed[j:j + 16]) + ',' for j in range(0, len(packed), 16)) + '\n'),
        (glyph_match.start(1), glyph_match.end(1), '\n    ' + ',\n    '.join(ordered) + '\n'),
        (cmap_start, cmap_match.end(), '\n'.join(arrays) + '\nstatic const lv_font_fmt_txt_cmap_t cmaps[] = {\n' + ',\n'.join(cmaps) + '\n};'),
    ]
    for start, end, replacement in sorted(sections, reverse=True):
        text = text[:start] + replacement + text[end:]
    return re.sub(r'(\.cmap_num\s*=\s*)\d+', lambda m: m[1] + str(len(groups)), text)


if __name__ == '__main__':
    import argparse
    from pathlib import Path
    parser = argparse.ArgumentParser(description='Compact a generated font losslessly without changing its coverage')
    parser.add_argument('font', type=Path)
    args = parser.parse_args()
    original = args.font.read_text(encoding='utf-8')
    result = compact_font(original)
    args.font.write_text(result, encoding='utf-8', newline='\n')
    print(f'FONT_CMAP_COMPACT_OK {args.font.name}: {len(original)} -> {len(result)} source characters; bitmap pixels retained')
