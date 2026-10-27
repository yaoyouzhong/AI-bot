"""Build a credential-free TAB5 first-install ZIP from an ESP-IDF build.

This never reads a device backup. The entire 16 MiB image starts erased, then
only the four reviewed build outputs are copied into it. It is NOT an OTA ZIP.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import zipfile

ROOT = Path(__file__).resolve().parents[1]
FLASH_SIZE = 0x1000000
PARTITIONS = {
    'nvs': (1, 2, 0x9000, 0x6000), 'phy_init': (1, 1, 0xf000, 0x1000),
    'otadata': (1, 0, 0x10000, 0x2000), 'ota_0': (0, 0x10, 0x20000, 0x6e0000),
    'ota_1': (0, 0x11, 0x700000, 0x6e0000), 'assets': (1, 0x40, 0xe00000, 0x200000),
}
FILES = [('bootloader.bin', 'bootloader/bootloader.bin', 0x2000, 0x6000),
         ('partition-table.bin', 'partition_table/partition-table.bin', 0x8000, 0x1000),
         ('otadata.bin', 'ota_data_initial.bin', 0x10000, 0x2000),
         ('application.bin', 'aibot_tab5.bin', 0x20000, 0x6e0000)]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def validate_image(data):
    if len(data) < 48 or data[0] != 0xe9 or not 1 <= data[1] <= 16 or struct.unpack_from('<H', data, 12)[0] != 18:
        raise ValueError('Expected an ESP32-P4 image')
    pos, checksum = 24, 0xef
    for _ in range(data[1]):
        if pos + 8 > len(data):
            raise ValueError('Truncated image segment')
        size = struct.unpack_from('<I', data, pos + 4)[0]
        pos += 8
        if pos + size > len(data):
            raise ValueError('Truncated image data')
        for value in data[pos:pos + size]:
            checksum ^= value
        pos += size
    end = (pos | 15) + 1
    if end > len(data) or data[end - 1] != checksum:
        raise ValueError('Image checksum mismatch')
    if data[23] != 1 or len(data) != end + 32 or hashlib.sha256(data[:end]).digest() != data[end:]:
        raise ValueError('Expected unsigned image with valid appended SHA-256')


def validate_partitions(data):
    found = {}
    for offset in range(0, len(data) - 31, 32):
        magic = struct.unpack_from('<H', data, offset)[0]
        if magic == 0xebeb:
            if any(v != 0xff for v in data[offset + 2:offset + 16]):
                raise ValueError('Invalid partition checksum record')
            if data[offset + 16:offset + 32] != hashlib.md5(data[:offset]).digest():
                raise ValueError('Partition MD5 mismatch')
            if found != PARTITIONS or list(found) != list(PARTITIONS) or any(v != 0xff for v in data[offset + 32:]):
                raise ValueError('Unexpected partition layout or tail')
            return
        if magic != 0x50aa:
            break
        _, kind, subtype, start, size, label, flags = struct.unpack_from('<HBBII16sI', data, offset)
        name = label.split(b'\0')[0].decode('ascii')
        if name in found or flags:
            raise ValueError('Duplicate or encrypted partition')
        found[name] = (kind, subtype, start, size)
    raise ValueError('Missing partition MD5')


def package(build, output):
    build, output = Path(build).resolve(), Path(output).resolve()
    if output.exists() or output.with_suffix(output.suffix + '.sha256').exists():
        raise ValueError('Use a new output filename')
    image = bytearray(b'\xff' * FLASH_SIZE)
    segments = []
    version = None
    for name, path, offset, maximum in FILES:
        data = (build / path).read_bytes()
        if not 0 < len(data) <= maximum:
            raise ValueError(f'Invalid size: {name}')
        if name in ('bootloader.bin', 'application.bin'):
            validate_image(data)
        if name == 'partition-table.bin':
            validate_partitions(data)
        if name == 'otadata.bin' and (len(data) != 0x2000 or any(v != 0xff for v in data)):
            raise ValueError('OTA initialization must be erased')
        if name == 'application.bin':
            if len(data) < 288 or struct.unpack_from('<I', data, 28)[0] < 256 or struct.unpack_from('<I', data, 32)[0] != 0xabcd5432 or data[80:112].split(b'\0')[0] != b'aibot_tab5':
                raise ValueError('Wrong application identity')
            version = data[48:80].split(b'\0')[0].decode('ascii')
            if not version or len(version) >= 32 or any(ord(c) < 32 or ord(c) > 126 for c in version):
                raise ValueError('Invalid version')
        image[offset:offset + len(data)] = data
        segments.append(dict(name=name, offset=offset, size=len(data), sha256=sha(data)))
    manifest = dict(format=1, kind='aibot-tab5-first-install', chip='esp32p4', project='aibot_tab5',
                    version=version, flashBytes=FLASH_SIZE, imageSha256=sha(image), segments=segments)
    output.parent.mkdir(parents=True, exist_ok=True)
    # Create exclusively. A failed build must not leave a apparently usable ZIP.
    created = False
    checksum_created = False
    checksum_path = output.with_suffix(output.suffix + '.sha256')
    try:
        stream = output.open('xb')
        created = True
        with stream, zipfile.ZipFile(stream, 'w', zipfile.ZIP_DEFLATED) as archive:
            archive.writestr('manifest.json', json.dumps(manifest, indent=2) + '\n')
            archive.writestr('factory.bin', image)
            archive.writestr('README.txt', 'TAB5 first installation only. Open this ZIP in AI-bot > TAB5 > First installation.\n'
                             'Installing replaces the P4 factory application and all P4 flash settings.\n'
                             'Keep the tool-created private backup. Do not use this ZIP for OTA.\n'
                             'The C6 radio firmware is not included or changed.\n')
            for name in ('LICENSE', 'THIRD_PARTY_NOTICES.md', 'docs/FIRST-INSTALL.md'):
                if (ROOT / name).is_file():
                    archive.write(ROOT / name, name)
            for prefix, directory in [('components', ROOT / 'firmware/components'),
                                      ('managed', ROOT / 'firmware/managed_components'),
                                      ('fonts', ROOT / 'firmware/main'),
                                      ('dictionary', ROOT / 'firmware/third_party'),
                                      ('idf', ROOT / '.tools/esp-idf/components')]:
                if directory.is_dir():
                    for source in sorted(directory.rglob('*')):
                        if source.is_file() and (source.name.upper().startswith(('LICENSE', 'LICENCE', 'COPYING')) or 'OFL' in source.name or source.name == 'M5Stack-LICENSE'):
                            archive.write(source, 'licenses/' + prefix + '/' + source.relative_to(directory).as_posix())
            sdk_license = ROOT / '.tools/esp-idf/LICENSE'
            if sdk_license.is_file():
                archive.write(sdk_license, 'licenses/idf/LICENSE')
        with checksum_path.open('x', encoding='ascii') as checksum_file:
            checksum_created = True
            checksum_file.write(sha(output.read_bytes()) + '  ' + output.name + '\n')
    except Exception:
        if created:
            output.unlink(missing_ok=True)  # Only our exclusively created file.
        if checksum_created:
            checksum_path.unlink(missing_ok=True)
        raise
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--build', type=Path, default=ROOT / 'firmware/build')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = package(args.build, args.output)
    print(f"TAB5_FIRST_INSTALL_PACKAGE_OK version={result['version']} bytes={FLASH_SIZE} sha256={result['imageSha256']}")
