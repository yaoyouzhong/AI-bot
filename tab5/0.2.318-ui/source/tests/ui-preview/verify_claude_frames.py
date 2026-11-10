"""Compare a native decoder dump against the explicitly supplied official data."""
import argparse
import json
import struct
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--original',type=Path,required=True)
parser.add_argument('--decoded',type=Path,required=True)
args=parser.parse_args()
source=json.loads(args.original.read_bytes())
groups=[source['RIDE_SPRITES']['walk'],source['CLAWD_POOL'],source['CLAWD_KITE'],
        source['RIDE_SPRITES']['skate'],source['CLAWD'],dict(source['CLAWD_KITE'],frames=source['CLAWD_KITE']['frames'][:1])]
raw=args.decoded.read_bytes();cursor=count=0
for group in groups:
    for frame in group['frames']:
        w,h=struct.unpack_from('<II',raw,cursor);cursor+=8
        assert h==group['h'] and w==group['w']+group['w']%2
        expected_colors=bytearray();expected_alpha=bytearray()
        for y in range(h):
            for x in range(w):
                index=int(frame[str(y*group['w']+x)]) if x<group['w'] else 255
                color=0
                if index!=255:
                    r,g,b=bytes.fromhex(group['palette'][index].lstrip('#'))
                    color=(int(r/255*31+.5)<<11)|(int(g/255*63+.5)<<5)|int(b/255*31+.5)
                expected_colors.extend(struct.pack('<H',color))
                expected_alpha.append(0 if index==255 else 255)
        expected=expected_colors+expected_alpha
        assert raw[cursor:cursor+len(expected)]==expected, f'Official frame mismatch {count}'
        cursor+=len(expected);count+=1
assert cursor==len(raw) and count==378
print('CLAUDE_ORIGINAL_PIXELS_OK',count,'frames; source order, colors, opacity, transparent padding')
