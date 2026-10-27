"""Run actual IDF FatFs with generated firmware configuration (Windows host)."""
import argparse
import os
from pathlib import Path
import subprocess
import tempfile

root = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser()
parser.add_argument('--sdkconfig', type=Path, default=root / 'firmware/build/config/sdkconfig.h')
args = parser.parse_args()
config = args.sdkconfig.read_text(encoding='utf-8')
if '#define CONFIG_FATFS_LFN_HEAP 1' not in config:
    raise SystemExit('Generated firmware configuration must enable heap LFN support')
sdk = root / '.tools/esp-idf/components/fatfs/src'
vswhere = Path(os.environ['ProgramFiles(x86)']) / 'Microsoft Visual Studio/Installer/vswhere.exe'
vs = Path(subprocess.check_output([str(vswhere), '-latest', '-products', '*', '-property', 'installationPath'], text=True).strip())
devcmd = vs / 'Common7/Tools/VsDevCmd.bat'
compiler = vs / 'VC/Tools/Llvm/x64/bin/clang-cl.exe'
with tempfile.TemporaryDirectory(prefix='tab5-fatfs-') as directory:
    work = Path(directory)
    (work / 'freertos').mkdir()
    (work / 'sys').mkdir()
    (work / 'sys/param.h').write_text('#define MIN(a,b) ((a)<(b)?(a):(b))\n#define MAX(a,b) ((a)>(b)?(a):(b))\n')
    (work / 'freertos/FreeRTOS.h').write_text('#define portTICK_PERIOD_MS 1\n')
    (work / 'freertos/semphr.h').write_text('/* Single-threaded host disk test. */\n')
    source = root / 'tests/ui-preview/fatfs_paths_test.c'
    for legacy in [True, False]:
        # Change only LFN selection to reproduce the prior firmware failure.
        selected = config
        if legacy:
            selected = selected.replace('#define CONFIG_FATFS_LFN_HEAP 1', '#define CONFIG_FATFS_LFN_NONE 1')
        (work / 'sdkconfig.h').write_text(selected, encoding='utf-8')
        name = 'legacy' if legacy else 'configured'
        command = [str(compiler), '/nologo', '/std:c11', '/W3', '/WX', '/O2', '/D_CRT_SECURE_NO_WARNINGS',
                   '/DEXPECT_LFN=' + str(int(not legacy)), '/I' + str(work), '/I' + str(sdk),
                   '/I' + str(root / 'firmware/main'), str(source), str(sdk / 'ff.c'), str(sdk / 'ffunicode.c'),
                   '/Fe:' + str(work / (name + '.exe'))]
        response = work / 'compile.rsp'
        response.write_text('\n'.join('"' + value + '"' for value in command[1:]), encoding='utf-8')
        launcher = work / 'compile.cmd'
        launcher.write_text('@echo off\ncall "' + str(devcmd) + '" -no_logo -arch=x64 >nul\n"' + str(compiler) + '" @"' + str(response) + '"\n', encoding='utf-8')
        subprocess.run(['cmd.exe', '/d', '/c', str(launcher)], cwd=work, check=True)
        subprocess.run([str(work / (name + '.exe'))], cwd=work, check=True)
