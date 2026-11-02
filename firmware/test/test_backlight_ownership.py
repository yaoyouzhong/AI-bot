#!/usr/bin/env python3
"""Verify the production build leaves backlight GPIO ownership to the firmware."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
config = (root / 'platformio.ini').read_text()
source = (root / 'src/main.cpp').read_text()
driver = (root / '.pio/libdeps/nodemcuv2/TFT_eSPI/TFT_eSPI.cpp').read_text()
assert re.search(r'^\s*-D\s+AIBOT_TFT_BL=5\s*$', config, re.M)
assert not re.search(r'^\s*-D\s+TFT_BL(?:=|\s|$)', config, re.M)
assert 'constrain(brightness, 0, 100) * transitionBrightnessPercent' in source
assert 'analogWrite(AIBOT_TFT_BL, 100 - level)' in source
setup = source.split('void setup() {', 1)[1].split('\nvoid loop()', 1)[0]
assert setup.index('loadBrightness()') < setup.index('applyBrightness()') < setup.index('display.init()')
assert 'pinMode(AIBOT_TFT_BL, OUTPUT)' in setup
assert re.search(r'#if defined \(TFT_BL\) && defined \(TFT_BACKLIGHT_ON\).*?digitalWrite\(TFT_BL, TFT_BACKLIGHT_ON\)', driver, re.S)
print('ESP_BACKLIGHT_OWNERSHIP_OK')
