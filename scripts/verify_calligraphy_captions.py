"""Check actual JPEG caption pixels against full reviewed text in the safe area."""
import argparse
import json
import re
from pathlib import Path
from functools import lru_cache

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from art_information_panel import draw_information_panel
from prepare_annual_calligraphy_layouts import FONT


@lru_cache(maxsize=64)
def font(size):
    return ImageFont.truetype(str(FONT),size)


def verify(directory):
    read=lambda name:json.loads((directory/name).read_text('utf-8'))
    guides={w['workId']:w for w in read('guides.zh.json')['works']}
    checked=[]
    for work in read('catalog.json'):
        if work['category']!='calligraphy':continue
        guide=guides[work['workId']]
        expected=Image.new('RGB',(1280,720),'#e7e0d1')
        placements=draw_information_panel(ImageDraw.Draw(expected),guide,font,'#332e27','#776e60','#964b39')
        for row in placements:
            x1,y1,x2,y2=row['bounds']
            if not(0<=x1<x2<500 and 0<=y1<y2<=622) or re.sub(r'\s','',row['text'])!=re.sub(r'\s','',guide[row['field']]):
                raise ValueError('Caption truncated or outside safe area: '+work['id'])
        expected=np.asarray(expected).astype(np.int16)
        mask=np.any(expected[:622,:500]!=[231,224,209],axis=2)
        for name in work['frames']:
            with Image.open(directory/name) as image:
                actual=np.asarray(image.convert('RGB')).astype(np.int16)
            # These are exact rendered text pixels, allowing only JPEG noise.
            error=np.abs(actual[:622,:500]-expected[:622,:500])[mask]
            mean=float(error.mean());bad=float(np.any(error>45,axis=1).mean())
            if mean>8 or bad>.01:
                raise ValueError(f'Caption pixels do not match reviewed text: {name} mean={mean} bad={bad}')
            checked.append(dict(file=name,meanPixelError=round(mean,3),badPixelFraction=round(bad,6)))
    return dict(works=len(guides),landscapePages=len(checked),allThreeFieldsVisibleAboveFooter=True,
                completeTextWithoutEllipsis=True,frames=checked)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('directory',type=Path);parser.add_argument('--report',type=Path,required=True)
    args=parser.parse_args();result=verify(args.directory)
    args.report.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n','utf-8')
    print('CALLIGRAPHY_CAPTION_PIXELS_OK works='+str(result['works'])+' pages='+str(result['landscapePages']))
