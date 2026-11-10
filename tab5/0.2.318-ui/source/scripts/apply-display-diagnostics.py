"""Add read-only display/flash timing hooks to pinned ESP-IDF 5.4.2."""
from pathlib import Path
import sys

root = Path(sys.argv[1])
driver = root / 'components/esp_lcd/dsi/esp_lcd_panel_dpi.c'
source = driver.read_text(encoding='utf-8')
original = source
marker = '/* TAB5 display underrun diagnostic hook */'
if marker not in source:
    point = 'if (unlikely(error_status & MIPI_DSI_LL_EVENT_UNDERRUN)) {'
    if source.count(point) != 1:
        raise SystemExit('Unexpected ESP-IDF display driver; inspect before applying diagnostics.')
    source = source.replace(point, point + '\n        ' + marker +
        '\n        extern void tab5_lcd_underrun(void) __attribute__((weak));' +
        '\n        if (tab5_lcd_underrun) tab5_lcd_underrun();')
frame_marker = '/* TAB5 display frame timing hook */'
if frame_marker not in source:
    point = '    uint8_t fb_index = dpi_panel->cur_fb_index;'
    if source.count(point) != 1:
        raise SystemExit('Unexpected DPI frame callback; inspect before patching.')
    source = source.replace(point, '    ' + frame_marker +
        '\n    extern void tab5_lcd_frame_tick(void) __attribute__((weak));' +
        '\n    if (tab5_lcd_frame_tick) tab5_lcd_frame_tick();\n' + point)
if source != original:
    driver.write_text(source, encoding='utf-8')

# Observe the actual cache-disabled interval without changing flash locking,
# yielding, interrupt masks, or SDK recovery behavior.
flash_driver = root / 'components/spi_flash/spi_flash_os_func_app.c'
source = flash_driver.read_text(encoding='utf-8')
flash_marker = '/* TAB5 flash blackout timing hook */'
if flash_marker not in source:
    start = '    spi_flash_disable_interrupts_caches_and_other_cpu();'
    end = '    spi_flash_enable_interrupts_caches_and_other_cpu();'
    if source.count(start) != 1 or source.count(end) != 1:
        raise SystemExit('Unexpected SPI flash cache guards; inspect before patching.')
    source = source.replace(start, start + '\n    ' + flash_marker +
        '\n    extern void tab5_flash_guard_begin(void) __attribute__((weak));' +
        '\n    if (tab5_flash_guard_begin) tab5_flash_guard_begin();')
    source = source.replace(end,
        '    extern void tab5_flash_guard_end(void) __attribute__((weak));' +
        '\n    if (tab5_flash_guard_end) tab5_flash_guard_end();\n' + end)
    flash_driver.write_text(source, encoding='utf-8')
print('TAB5 display diagnostics hook ready')
