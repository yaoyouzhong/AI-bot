"""Original AI-bot flasher icon: teal screen with a white downward arrow.

Drawn from geometric primitives; no external artwork. Requires Pillow to rebuild.
"""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
image = Image.new("RGBA", (1024, 1024))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((40, 40, 984, 984), radius=220, fill="#087F83")
draw.rounded_rectangle((176, 224, 848, 736), radius=72, fill="#073D48")
draw.rounded_rectangle((208, 256, 816, 704), radius=44, outline="#81E1D4", width=24)
draw.rounded_rectangle((416, 780, 608, 828), radius=24, fill="#CCF7EC")
draw.polygon([(470, 326), (554, 326), (554, 474), (644, 474),
              (512, 610), (380, 474), (470, 474)], fill="white")
assets = root / "windows-app/AIBotBridge/Assets"
image.save(assets / "flash-icon.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
# Text source above is the provenance; PNG is only a local review artifact.
preview = root / "artifacts/flash-icon-preview.png"
preview.parent.mkdir(parents=True, exist_ok=True)
image.resize((256, 256), Image.Resampling.LANCZOS).save(preview)
