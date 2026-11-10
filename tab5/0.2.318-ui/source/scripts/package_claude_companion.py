"""Package explicitly supplied original Clawd frames for a private local build.

Artwork is Anthropic's, outside AI-bot's MIT source. Playback and task meanings
are defined by AI-bot. No upstream site program code is included or executed.
"""
import argparse
import hashlib
import json
import statistics
from pathlib import Path
from package_codex_companion import pack, array


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--expected-sha256', required=True)
    args = parser.parse_args()
    raw = args.input.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != args.expected_sha256.lower():
        raise SystemExit('Original frame fingerprint mismatch')
    source = json.loads(raw)
    groups = [('walk', source['RIDE_SPRITES']['walk']),
              ('pool', source['CLAWD_POOL']), ('kite', source['CLAWD_KITE']),
              ('skate', source['RIDE_SPRITES']['skate']), ('astronaut', source['CLAWD']),
              ('rest', dict(source['CLAWD_KITE'], frames=source['CLAWD_KITE']['frames'][:1]))]
    colors = list(dict.fromkeys(color.lower() for _, g in groups for color in g['palette']))
    if len(colors) >= 255:
        raise SystemExit('Palette exceeds lossless indexed capacity')
    palette, alpha = [0]*256, [0]*256
    for i, color in enumerate(colors):
        r, g, b = bytes.fromhex(color.lstrip('#'))
        palette[i] = ((r*31+127)//255 << 11) | ((g*63+127)//255 << 5) | ((b*31+127)//255)
        alpha[i] = 255
    stream, frames, records = bytearray(), [], []
    for name, group in groups:
        w, h = group['w'], group['h']
        width = w + w % 2  # RGB565 stride stays four-byte aligned.
        lookup = [colors.index(c.lower()) for c in group['palette']]
        first = len(frames)
        for frame in group['frames']:
            data = bytearray()
            for y in range(h):
                for x in range(width):
                    value = int(frame[str(y*w+x)]) if x < w else 255
                    if value != 255 and not 0 <= value < len(lookup):
                        raise SystemExit('Invalid official palette index')
                    data.append(255 if value == 255 else lookup[value])
            encoded = pack(data)
            frames.append((len(stream), len(encoded), width, h))
            stream.extend(encoded)
        # A fixed actor anchor preserves all original motion within each clip.
        body = [i % w for i in range(w*h) if int(group['frames'][0][str(i)]) != 255
                and group['palette'][int(group['frames'][0][str(i)])].lower() in ('#d97757', '#d87656', '#be684d', '#bf684c')]
        anchor = min(body) + max(body) + 1
        # Measure the actor separately from props. A clip-wide fixed median
        # retains the original floating/jumping motion instead of tracking it.
        centers = []
        for frame in group['frames']:
            remaining = {(i % w, i // w) for i in range(w*h) if int(frame[str(i)]) != 255
                         and group['palette'][int(frame[str(i)])].lower() in ('#d97757', '#d87656', '#be684d', '#bf684c')}
            largest = set()
            while remaining:
                queue = [remaining.pop()]
                component = set(queue)
                for x, y in queue:
                    for point in ((x-1,y),(x+1,y),(x,y-1),(x,y+1)):
                        if point in remaining:
                            remaining.remove(point); component.add(point); queue.append(point)
                if len(component) > len(largest):
                    largest = component
            centers.append(min(y for x,y in largest) + max(y for x,y in largest) + 1)
        anchor_y = int(statistics.median(centers) + .5)
        records.append(dict(name=name, first=first, count=len(group['frames']), width=width,
                            height=h, sourceWidth=w, anchorX2=anchor, anchorY2=anchor_y, fps=group['fps']))
    sections = [f'/* Anthropic original frames {digest}; private supplied artwork, not AI-bot MIT. */',
                array('claude_pet_palette', 'uint16_t', palette),
                array('claude_pet_alpha', 'uint8_t', alpha),
                array('claude_pet_stream', 'uint8_t', list(stream), 24),
                'static const struct { uint32_t offset; uint16_t size; uint8_t width,height; } claude_pet_frames[] = {\n' +
                ''.join('    {%d,%d,%d,%d},\n' % r for r in frames) + '};',
                'static const struct { uint16_t first,count; uint8_t anchor_x2,anchor_y2; } claude_pet_groups[] = {\n' +
                ''.join('    {%d,%d,%d,%d}, /* %s */\n' % (r['first'],r['count'],r['anchorX2'],r['anchorY2'],r['name']) for r in records) + '};']
    args.output.write_text('\n'.join(sections)+'\n', encoding='utf-8')
    metadata = dict(owner='Anthropic', source='https://claude.dev', inputSha256=digest,
                    encodedBytes=len(stream), frameCount=len(frames), groups=records,
                    conversion='Original indices and order; transparent right padding only; RGB565A8; bounded PackBits',
                    meanings='AI-bot defines task state mapping and playback; these are not official task semantics',
                    scope='Private supplied artwork; excluded from MIT source patch and public redistribution')
    args.output.with_suffix('.json').write_text(json.dumps(metadata, indent=2), encoding='utf-8')
    print('CLAUDE_OFFICIAL_FRAMES_READY', len(frames), len(stream), 'bytes')


if __name__ == '__main__':
    main()
