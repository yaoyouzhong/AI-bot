"""Verify the TLS cleanup call is guarded in the actual P4 XIP ELF."""
import argparse
import json
import re
import subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('elf', type=Path)
p.add_argument('--objdump', type=Path, required=True)
p.add_argument('--output', type=Path)
a = p.parse_args()
table = subprocess.check_output([str(a.objdump), '-t', str(a.elf)], text=True)
symbols = {}
for line in table.splitlines():
    fields = line.split()
    if len(fields) >= 5 and re.fullmatch('[0-9a-f]{8}', fields[0]):
        symbols[fields[-1]] = {'address': int(fields[0], 16), 'section': fields[-3]}

start = symbols['_instruction_reserved_start']['address']
end = symbols['_instruction_reserved_end']['address']
assert 0x48000000 <= start < end < 0x4c000000, (start, end)
wrapper = '__wrap_esp_ptr_executable'
assert symbols[wrapper]['section'] == '.iram0.text', symbols[wrapper]
callback = 'pthread_cleanup_thread_specific_data_callback'
assert start <= symbols[callback]['address'] < end, symbols[callback]
body = subprocess.check_output([str(a.objdump), '-d', '--disassemble=vPortTCBPreDeleteHook', str(a.elf)], text=True)
assert '<' + wrapper + '>' in body, body
assert '<esp_ptr_executable>' not in body, body
guard = subprocess.check_output([str(a.objdump), '-d', '--disassemble=' + wrapper, str(a.elf)], text=True)
assert '<esp_ptr_executable>' in guard, guard  # original SDK fallback retained
result = {'passed': True, 'textStart': hex(start), 'textEndExclusive': hex(end),
          'callback': symbols[callback], 'wrapper': symbols[wrapper], 'disassembly': guard}
if a.output:
    a.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
print('EXECUTABLE_GUARD_ELF_PASS: IRAM guard called by TLS cleanup; pthread callback in linked PSRAM text; SDK fallback retained')
