"""Create a small flash ZIP from an existing full firmware materials ZIP."""
import argparse
import hashlib
import json
from pathlib import Path
import zipfile


def prepare(source, output, expected_sha256):
    source, output = Path(source), Path(output)
    if hashlib.sha256(source.read_bytes()).hexdigest() != expected_sha256.lower():
        raise ValueError('Materials archive digest mismatch')
    if output.exists():
        raise ValueError('Choose a new output path')
    with zipfile.ZipFile(source) as archive:
        image = archive.read('firmware.bin')
        component = json.loads(archive.read('COMPONENT.json'))
        if len(image) < 1024 or len(image) > 4 * 1024 * 1024 or image[0] != 0xe9 or component['component'] != 'esp8266':
            raise ValueError('Invalid ESP8266 firmware')
        version = component['version']
        if output.name != f'AI-bot-esp8266-{version}-flash.zip':
            raise ValueError('Output version/name mismatch')
        tag = f'esp8266-v{version}'
        materials = f'https://github.com/yaoyouzhong/AI-bot/releases/tag/{tag}'
        source_url = f'https://github.com/yaoyouzhong/AI-bot/tree/{tag}'
        readme = (f'ESP8266 {version} flash package / 刷机包\n\n'
                  f'Flash firmware.bin with the AI-bot bridge. / 使用 AI-bot 桥接刷写 firmware.bin。\n'
                  f'Full corresponding source, build materials and notices / 对应完整源码、构建材料及许可：{materials}\n'
                  f'AI-bot source / 项目源码：{source_url}\n').encode('utf-8')
        with zipfile.ZipFile(output, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as target:
            for name in ('firmware.bin', 'VERSION', 'COMPONENT.json', 'LICENSE', 'THIRD_PARTY_NOTICES.md'):
                target.writestr(name, archive.read(name))
            target.writestr('README.txt', readme)
    digest = hashlib.sha256(output.read_bytes()).hexdigest()
    output.with_name(output.name + '.sha256').write_text(f'{digest}  {output.name}\n', encoding='ascii')
    return {'version': version, 'firmwareSha256': hashlib.sha256(image).hexdigest(), 'packageSha256': digest, 'bytes': output.stat().st_size}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('materials'); parser.add_argument('output'); parser.add_argument('--expected-sha256', required=True)
    args = parser.parse_args()
    print(json.dumps(prepare(args.materials, args.output, args.expected_sha256), ensure_ascii=False))
