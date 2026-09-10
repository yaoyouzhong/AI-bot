"""Original, self-contained SVG illustrations for the GitHub README.

No fonts, photos, vendor logos or private runtime data are embedded. Screen values
are illustrative. The geometric pet follows AI-bot's own drawPixelPetBody function.
"""
from pathlib import Path
from html import escape

OUT = Path(__file__).resolve().parents[1] / "docs/assets"


def text(x, y, value, size=20, fill="#eaf6ff", weight=400, **attrs):
    extra = " ".join(f'{k.replace("_", "-")}="{v}"' for k, v in attrs.items())
    return f'<text x="{x}" y="{y}" font-size="{size}" fill="{fill}" font-weight="{weight}" {extra}>{escape(value)}</text>'


def pet(x, y, scale=1):
    return f'''<g transform="translate({x} {y}) scale({scale})" fill="#77e8f4">
      <path d="M27 0h4v10h-4zM23 0h12v4H23z"/>
      <rect x="8" y="10" width="42" height="35" rx="5"/>
      <rect x="14" y="17" width="30" height="19" fill="#071326"/>
      <path d="M20 23h5v6h-5zM34 23h5v6h-5z" fill="#effda1"/>
      <rect x="12" y="48" width="34" height="42" rx="5"/>
      <path d="M20 59h18v5H20z" fill="#071326"/>
      <path d="M5 54h7v27H5zM46 54h7v27h-7zM6 90h17v8H6zM32 90h17v8H32z"/>
    </g>'''


def svg(body, height, title, description):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="1120" height="{height}" viewBox="0 0 1120 {height}" role="img" aria-labelledby="title desc">
<title id="title">{escape(title)}</title><desc id="desc">{escape(description)}</desc>
<defs>
 <linearGradient id="base" x2="1" y2="1"><stop stop-color="#071426"/><stop offset="1" stop-color="#112e54"/></linearGradient>
 <linearGradient id="shell" x2=".8" y2="1"><stop stop-color="#385977"/><stop offset=".5" stop-color="#152c46"/><stop offset="1" stop-color="#0b1b2c"/></linearGradient>
 <linearGradient id="light"><stop stop-color="#75e5f3"/><stop offset="1" stop-color="#7088ff"/></linearGradient>
 <radialGradient id="halo"><stop stop-color="#285ac0" stop-opacity=".44"/><stop offset="1" stop-color="#285ac0" stop-opacity="0"/></radialGradient>
 <pattern id="grid" width="32" height="32" patternUnits="userSpaceOnUse"><path d="M32 0H0V32" fill="none" stroke="#9ec9eb" stroke-opacity=".07"/></pattern>
</defs>
<g font-family="'Segoe UI','Microsoft YaHei',Arial,sans-serif">{body}</g></svg>'''


def hero(english=False):
    b = ['<rect width="1120" height="540" rx="20" fill="url(#base)"/>',
         '<rect width="1120" height="540" rx="20" fill="url(#grid)"/>',
         '<ellipse cx="850" cy="235" rx="400" ry="340" fill="url(#halo)"/>',
         '<g fill="none" stroke="#759dce" stroke-opacity=".2"><circle cx="851" cy="271" r="221"/><circle cx="851" cy="271" r="255"/><path d="M591 271h515M851 20v502"/></g>',
         '<path d="M638 412L736 466H967L1066 408" fill="none" stroke="#84d9ef" stroke-opacity=".25"/>',
         '<path d="M58 46h12v12H58zM76 46h12v12H76zM58 64h12v12H58zM76 64h12v12H76z" fill="#7ce9f4"/>',
         text(103, 69, 'AI-bot / Desktop companion', 17, '#bdd8ed'),
         text(53, 178, 'AI-bot', 96, '#f1f9ff', 700, letter_spacing='-4')]
    lines = ('Your AI,', 'at a glance.') if english else ('AI 状态，', '抬眼可见。')
    b += [text(58, 252, lines[0], 49, '#f1f9ff', 650),
          text(58, 315, lines[1], 49, '#f1f9ff', 650),
          text(61, 367, 'A small screen for your AI workflow.' if english else '把终端里的工作状态，带到桌面。', 23, '#a9c6df'),
          '<path d="M61 415h445" stroke="#6a90b4" stroke-opacity=".4"/>',
          '<circle cx="66" cy="447" r="4" fill="#7ce9f4"/>',
          text(81, 453, 'Local-first', 18, '#d2e5f5'),
          text(224, 453, 'USB-first', 18, '#d2e5f5'),
          text(361, 453, 'Open source', 18, '#d2e5f5'),
          '<ellipse cx="858" cy="460" rx="153" ry="20" fill="#000" opacity=".3"/>',
          '<path d="M810 405h90l19 46H791z" fill="url(#shell)" stroke="#4f769a"/>',
          '<rect x="775" y="448" width="162" height="12" rx="6" fill="#274760" stroke="#577d98"/>',
          '<rect x="685" y="91" width="340" height="329" rx="40" fill="url(#shell)" stroke="#82b2d0" stroke-width="2"/>',
          '<rect x="699" y="104" width="312" height="299" rx="30" fill="#08121e" stroke="#203e58" stroke-width="2"/>',
          '<rect x="721" y="119" width="266" height="266" rx="17" fill="#071729" stroke="#416080"/>',
          '<path d="M737 162h234" stroke="#1d3a50"/>',
          text(740, 147, 'Codex', 18, '#dbeeff', 600),
          '<circle cx="897" cy="142" r="4" fill="#ffa774"/>',
          text(909, 147, 'working', 14, '#ffa774'),
          '<circle cx="854" cy="248" r="69" fill="none" stroke="#1a384f" stroke-width="2"/>',
          '<path d="M795 214a68 68 0 0 1 117 0" fill="none" stroke="url(#light)" stroke-width="3"/>',
          pet(822, 192, 1.14),
          text(745, 337, '5h', 15, '#b6cfe3'),
          '<rect x="781" y="328" width="143" height="6" rx="3" fill="#1a3549"/><rect x="781" y="328" width="62" height="6" rx="3" fill="#79dce9"/>',
          text(938, 337, '38%', 14, '#d5eefa'),
          text(745, 366, 'Week', 15, '#b6cfe3'),
          '<rect x="781" y="357" width="143" height="6" rx="3" fill="#1a3549"/><rect x="781" y="357" width="94" height="6" rx="3" fill="#8c9bff"/>',
          text(938, 366, '64%', 14, '#d5eefa'),
          '<circle cx="977" cy="401" r="3" fill="#73e5f3"/>',
          '<path d="M1025 258h45v86h50" fill="none" stroke="#78dce9" stroke-opacity=".55"/>',
          text(60, 509, 'ESP8266 / 240 × 240' , 14, '#8cacc7'),
          text(1060, 509, 'Original concept · illustrative data' if english else '原创概念示意 · 非实机截图', 14, '#8cacc7', text_anchor='end')]
    return svg(''.join(b), 540, 'AI-bot — Your AI, at a glance', 'Original desktop screen illustration with the procedural BYTE SPROUT pet. Fictional example values; not a photograph or screenshot.')


def scenes():
    b=['<rect width="1120" height="334" rx="18" fill="#0a1b30"/>',
       '<path d="M373 28v278M746 28v278" stroke="#27415a"/>']
    for x, label, sub in [(30,'Activity','Know when to step in'),(403,'Ambient','Your desk, in context'),(776,'System','A pulse on your machine')]:
        b += [text(x, 44, label, 24, '#e5f4ff', 600),text(x, 74, sub, 15, '#9dbbd2')]
    b += ['<rect x="57" y="103" width="260" height="179" rx="14" fill="#102942" stroke="#31516e"/>',
          text(78,132,'Claude + Codex',16,'#d8edfa'),
          pet(80,158,.95),text(171,187,'working',24,'#78e7f4',600),
          '<path d="M174 212h104M174 231h65" stroke="#547697" stroke-width="5" stroke-linecap="round"/>',
          '<circle cx="281" cy="183" r="4" fill="#ffa774"/>',
          text(435,173,'10:24',62,'#e5f4ff',600,letter_spacing='-2'),
          '<circle cx="665" cy="148" r="23" fill="none" stroke="#ffa774" stroke-width="3"/>',
          '<path d="M665 111v-8M665 193v-8M628 148h-8M710 148h-8M639 122l-6-6M697 180l-6-6M639 174l-6 6M697 116l-6 6" stroke="#ffa774" stroke-width="2"/>',
          text(439,221,'26°',34,'#78e7f4',600),text(509,218,'Weather · clock',17,'#a8c5dc'),
          '<path d="M439 249h226" stroke="#29455f"/>',
          text(439,274,'Music / markets / screen saver',15,'#a8c5dc'),
          text(791,137,'CPU',15,'#a8c5dc'),text(791,175,'24%',38,'#e8f6ff',600),
          text(940,137,'Memory',15,'#a8c5dc'),text(940,175,'48%',38,'#e8f6ff',600),
          '<path d="M791 260h278M791 233h278M791 206h278" stroke="#25415a" stroke-dasharray="2 6"/>',
          '<path d="M792 254l15-8 16 3 16-30 16 9 16-27 16 36 16-3 16 14 16-39 16 11 16-21 16 41 16-13 16 8 16-26 16 14" fill="none" stroke="#7ce9f4" stroke-width="2.5"/>',
          '<path d="M792 267l15-4 16 3 16-10 16 7 16-4 16 6 16-10 16 8 16-6 16 8 16-10 16 3 16-6 16 10 16-8 16 4" fill="none" stroke="#909cff" stroke-width="2"/>',
          text(1090,315,'Concept views · sample data',12,'#8aa9c0',text_anchor='end')]
    return svg(''.join(b),334,'Three ways to read your desk','Concept illustrations of AI activity, ambient information and system monitoring. All numbers are sample data.')


if __name__ == '__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    for name, content in {'hero.zh.svg':hero(), 'hero.en.svg':hero(True), 'scenes.svg':scenes()}.items():
        (OUT/name).write_text(content+'\n',encoding='utf-8',newline='\n')
        print(OUT/name)
