"""Trusted local launcher for the native command plugin; credentials stay local."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from urllib.parse import urlparse
from coordination_checkpoint import require
from coordination_result_writeback import validate_config


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--config', required=True)
    args = parser.parse_args(); path = Path(args.config).resolve()
    config = json.loads(path.read_text(encoding='utf-8')); validate_config(config)
    native = json.loads(Path(config['nativeConfig']).read_text(encoding='utf-8'))
    watch = json.loads(Path(config['watchConfig']).read_text(encoding='utf-8'))
    parsed = urlparse(watch['hubUrl'])
    require(parsed.scheme == 'http' and parsed.hostname in ('127.0.0.1', '::1') and parsed.port
            and native['hub']['listen'] == parsed.netloc, 'Wrong native Hub')
    require(native['source']['name'] == config['nativeSource'], 'Wrong native source')
    env = os.environ.copy(); env['AIBOT_HUB_SOURCE_KEY'] = native['source']['key']
    result = subprocess.run([sys.executable, str(Path(__file__).with_name('coordination_result_writeback.py')),
        '--config', str(path)], env=env, capture_output=True, text=True, encoding='utf-8', timeout=170)
    if result.returncode:
        raise SystemExit('Result writeback failed; preserve existing receipt and check native source/remote availability')
    # This output is the adapter's public status, never the native summary or key.
    value = json.loads(result.stdout)
    if config.get('lastSuccess'):
        target = Path(config['lastSuccess']); require(target.is_absolute(), 'Fixed local audit path required')
        temp = target.with_suffix('.tmp'); temp.write_text(json.dumps(value, ensure_ascii=False)+'\n',encoding='utf-8'); temp.replace(target)
    print(json.dumps(value, ensure_ascii=False))


if __name__ == '__main__': main()
