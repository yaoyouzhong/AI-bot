"""Run the real GitHub CLI without a visible Windows console or PATH GUI shim."""
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys


def _console_executable(path):
    try:
        with path.open('rb') as source:
            header = source.read(64)
            if header[:2] != b'MZ' or len(header) != 64:
                return False
            source.seek(struct.unpack_from('<I', header, 60)[0])
            pe = source.read(94)
            return pe[:4] == b'PE\0\0' and len(pe) == 94 and struct.unpack_from('<H', pe, 92)[0] == 3
    except (OSError, struct.error):
        return False


def executable():
    if os.name != 'nt':
        found = shutil.which('gh')
        if found:
            return found
    else:
        candidates = [Path(base)/'GitHub CLI/gh.exe' for base in
                      (os.environ.get('ProgramFiles', ''), os.environ.get('ProgramW6432', ''),
                       os.environ.get('ProgramFiles(x86)', '')) if base]
        candidates += [Path(base)/'gh.exe' for base in os.get_exec_path() if base]
        for path in candidates:
            if _console_executable(path):
                return str(path.resolve())
    raise FileNotFoundError('No console GitHub CLI executable found; GUI shims are not used.')


def command(args):
    return [executable(), *args]


def run_cli(args, *, cwd=None, check=True, timeout=None):
    options = {}
    if os.name == 'nt':
        startup = subprocess.STARTUPINFO()
        startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
        startup.wShowWindow = subprocess.SW_HIDE
        options = {'creationflags': subprocess.CREATE_NO_WINDOW, 'startupinfo': startup}
    env = {**os.environ, 'GH_PROMPT_DISABLED': '1', 'GH_PAGER': 'cat'}
    return subprocess.run(command(args), cwd=cwd, stdin=subprocess.DEVNULL, capture_output=True,
                          check=check, timeout=timeout, env=env, **options)


if __name__ == '__main__':
    result = run_cli(sys.argv[1:], check=False)
    sys.stdout.buffer.write(result.stdout)
    sys.stderr.buffer.write(result.stderr)
    raise SystemExit(result.returncode)
