"""Verify the final ELF routes LVGL allocation to the PSRAM-only wrappers."""
import argparse
import json
import re
import subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument("elf", type=Path)
parser.add_argument("--objdump", type=Path, required=True)
parser.add_argument("--output", type=Path)
args = parser.parse_args()
expected = {
    "lv_malloc": "__wrap_lv_malloc_core",
    "lv_malloc_zeroed": "__wrap_lv_malloc_core",
    "lv_realloc": "__wrap_lv_realloc_core",
    "lv_free": "__wrap_lv_free_core",
    "__wrap_lv_malloc_core": "heap_caps_malloc",
    "__wrap_lv_realloc_core": "heap_caps_realloc",
    "__wrap_lv_free_core": "heap_caps_free",
}
checked = {}
for symbol, target in expected.items():
    body = subprocess.check_output(
        [str(args.objdump), "-d", "--disassemble=" + symbol, str(args.elf)], text=True
    )
    if f"<{target}>" not in body:
        raise SystemExit(f"Missing linked allocation path: {symbol} -> {target}")
    # ESP32-P4: MALLOC_CAP_SPIRAM | MALLOC_CAP_8BIT = 1028 (0x404).
    # malloc's caps are argument 2; realloc's are argument 3.
    reg = {"__wrap_lv_malloc_core": "a1", "__wrap_lv_realloc_core": "a2"}.get(symbol)
    if reg and not re.search(r"\bli\s+" + reg + r",\s*(?:1028|0x404)\b", body):
        raise SystemExit(f"PSRAM-only capability argument not verified: {symbol}")
    checked[symbol] = target
result = {"passed": True, "linked_calls": checked, "caps": 1028}
if args.output:
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print("UI_MEMORY_ELF_PASS: public LVGL allocators link to PSRAM-only allocation")
