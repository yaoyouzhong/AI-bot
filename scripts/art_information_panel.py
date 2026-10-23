"""Readable artwork metadata above the TAB5's opaque y=622 footer."""


def panel_text(draw, text, font, width, size, minimum, height):
    for current in range(size, minimum - 1, -1):
        lines, line = [], ''
        for char in text:
            if char == '\n':
                lines.append(line); line = ''; continue
            if line and draw.textlength(line + char, font=font(current)) > width:
                space = line.rfind(' ')
                if space > 0 and line[-1].isascii() and line[-1].isalnum() and char.isascii() and char.isalnum():
                    lines.append(line[:space].rstrip()); line = line[space + 1:]
                else:
                    lines.append(line.rstrip()); line = ''
            if not line and char == ' ':
                continue
            line += char
        lines.append(line)
        value = '\n'.join(lines)
        bounds = draw.multiline_textbbox((0, 0), value, font=font(current), spacing=8)
        if bounds[3] - bounds[1] <= height:
            return value, font(current), bounds[3] - bounds[1]
    raise ValueError('Artwork metadata exceeds the readable panel: ' + text)


def draw_information_panel(draw, metadata, font, ink, muted, accent):
    if any(not metadata.get(key, '').strip() for key in ('title', 'author', 'period')):
        raise ValueError('Artwork information requires title, author and reviewed period')
    rows = [panel_text(draw, metadata[key], font, 350, size, minimum, height)
            for key, size, minimum, height in [('title', 40, 24, 220),
                                               ('author', 23, 18, 112),
                                               ('period', 20, 18, 84)]]
    total = sum(row[2] for row in rows) + 68
    top = max(90, (622 - total) // 2)
    draw.rectangle((102, top, 128, top + 3), fill=accent)
    y = top + 34
    placements = []
    for key, (text, typeface, height), color in zip(('title', 'author', 'period'), rows, (ink, muted, muted)):
        bounds = draw.multiline_textbbox((0, 0), text, font=typeface, spacing=8)
        draw.multiline_text((100, y - bounds[1]), text, font=typeface, fill=color, spacing=8)
        placements.append(dict(field=key,text=text,bounds=[100+bounds[0],y,100+bounds[2],y+height],fontSize=typeface.size))
        y += height + 17
    if y - 17 > 622:
        raise ValueError('Artwork information collides with device footer')
    return placements
