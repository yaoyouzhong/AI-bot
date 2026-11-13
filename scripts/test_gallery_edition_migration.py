import ast
import hashlib
from pathlib import Path
import shutil
import tempfile
import unittest
import zipfile

script = Path(__file__).with_name('migrate_gallery_parts.py').read_text(encoding='utf-8')
tree = ast.parse(script)
function = next(node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name == 'corrected_archive')
namespace = {'hashlib': hashlib, 'shutil': shutil, 'zipfile': zipfile}
exec(compile(ast.Module(body=[function], type_ignores=[]), '<migration>', 'exec'), namespace)
corrected_archive = namespace['corrected_archive']


class GalleryMigrationTest(unittest.TestCase):
    def test_only_edition_and_its_checksum_change(self):
        with tempfile.TemporaryDirectory() as tmp:
            source, target = Path(tmp) / 'in.zip', Path(tmp) / 'out.zip'
            catalog = b'{"edition":"2026.11.12.1-annual","works":[1]}'
            checksum = (hashlib.sha256(catalog).hexdigest() + '  catalog.json\n').encode()
            with zipfile.ZipFile(source, 'w') as archive:
                archive.writestr('gallery/catalog.json', catalog)
                archive.writestr('gallery/SHA256SUMS.txt', checksum)
                archive.writestr('gallery/standard.zip', b'unchanged standard')
                archive.writestr('gallery/sources/image.jpg', b'unchanged image')
            corrected_archive(source, target)
            with zipfile.ZipFile(target) as archive:
                changed = archive.read('gallery/catalog.json')
                self.assertEqual(changed, b'{"edition":"2026.11.12.1","works":[1]}')
                self.assertEqual(archive.read('gallery/SHA256SUMS.txt'), (hashlib.sha256(changed).hexdigest() + '  catalog.json\n').encode())
                self.assertEqual(archive.read('gallery/standard.zip'), b'unchanged standard')
                self.assertEqual(archive.read('gallery/sources/image.jpg'), b'unchanged image')

    def test_rejects_unknown_edition(self):
        with tempfile.TemporaryDirectory() as tmp:
            source, target = Path(tmp) / 'in.zip', Path(tmp) / 'out.zip'
            with zipfile.ZipFile(source, 'w') as archive:
                archive.writestr('gallery/catalog.json', b'{"edition":"unknown"}')
                archive.writestr('gallery/SHA256SUMS.txt', b'')
            with self.assertRaises(ValueError):
                corrected_archive(source, target)


if __name__ == '__main__':
    unittest.main()
