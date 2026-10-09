"""Regression checks for the release boundary using synthetic packages only."""
import hashlib
import json
import shutil
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import collect_distribution_materials as materials


class DistributionTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.root_patch = patch.object(materials, 'ROOT', self.root)
        self.root_patch.start()
        self.addCleanup(self.root_patch.stop)
        self.stage = self.root / 'stage'
        self.stage.mkdir()
        for name in ('LICENSE', 'THIRD_PARTY_NOTICES.md', 'docs/DISTRIBUTION_TERMS.md',
                     'docs/WINDOWS_PACKAGE.md', 'docs/FIRMWARE_PACKAGE.md',
                     'docs/WINDOWS_INSTALLER.md', 'docs/DOMESTIC_QUOTA_SETUP.md',
                     'docs/assets/screens/api-settings.png',
                     'docs/INSTALL.zh.md', 'docs/FLASH.zh.md', 'docs/FLASH_MAC.zh.md',
                     'docs/FLASH_BUILD.zh.md', 'docs/TAB5-FIRMWARE-WORKFLOW.md', 'docs/TAB5-LICENSE-SCOPE.md',
                     'docs/assets/screens/device-center.png', 'docs/assets/screens/firmware-flasher.png',
                     'docs/assets/screens/tab5-first-install.png', 'docs/assets/screens/tab5-upgrade.png',
                     'licenses/windows-sdk/sdk_license.rtf', 'licenses/windows-sdk/REDIST.html',
                     'licenses/netease-clock-reference/LICENSE', 'licenses/netease-clock-reference/ORIGIN.md',
                     'licenses/qqmusic-clock-reference/LICENSE', 'licenses/qqmusic-clock-reference/ORIGIN.md'):
            self.write(name, 'synthetic fixture')
        self.package = self.root / 'packages/sample/1.0'
        self.package.mkdir(parents=True)
        (self.package / 'LICENSE').write_text('synthetic package license')
        self.sdk = self.root / 'packages/microsoft.windows.sdk.net.ref/10.0.19041.56'
        (self.sdk / 'lib/net8.0').mkdir(parents=True)
        self.hashes = {}
        for name in ('Microsoft.Windows.SDK.NET.dll', 'WinRT.Runtime.dll'):
            data = f'synthetic {name}'.encode()
            (self.stage / name).write_bytes(data)
            (self.sdk / 'lib/net8.0' / name).write_bytes(data)
            self.hashes[name] = hashlib.sha256(data).hexdigest()
        evidence = {'files': {'windows-sdk/sdk_license.rtf': materials.digest(self.root / 'licenses/windows-sdk/sdk_license.rtf')},
                    'windowsSdk': {'version': '10.0.19041.56', 'binaries': self.hashes}}
        for reference in ('netease-clock-reference', 'qqmusic-clock-reference'):
            for name in ('LICENSE', 'ORIGIN.md'):
                relative = reference + '/' + name
                evidence['files'][relative] = materials.digest(self.root / 'licenses' / relative)
        self.write('licenses/materials.json', json.dumps(evidence))
        assets = {'packageFolders': {str(self.root / 'packages'): {}},
                  'libraries': {'sample/1.0': {'type': 'package', 'path': 'sample/1.0'}},
                  'project': {'frameworks': {'net8.0': {'downloadDependencies': [
                      {'name': 'Microsoft.Windows.SDK.NET.Ref', 'version': '[10.0.19041.56, 10.0.19041.56]'}]}}}}
        self.write('windows-app/AIBotBridge/obj/project.assets.json', json.dumps(assets))

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def test_complete_materials_preserve_original_dlls(self):
        materials.collect_windows(self.stage)
        for reference in ('netease-clock-reference', 'qqmusic-clock-reference'):
            for name in ('LICENSE', 'ORIGIN.md'):
                relative = Path('licenses') / reference / name
                self.assertEqual((self.stage / relative).read_bytes(), (self.root / relative).read_bytes())
        self.assertTrue((self.stage / 'licenses/Microsoft.Windows.SDK.NET.Ref-10.0.19041.56/sdk_license.rtf').is_file())
        self.assertTrue((self.stage / 'DEPENDENCIES.json').is_file())
        for name in ('WINDOWS_INSTALLER.md', 'DOMESTIC_QUOTA_SETUP.md', 'assets/screens/api-settings.png'):
            self.assertEqual((self.stage / 'docs' / name).read_bytes(), (self.root / 'docs' / name).read_bytes())
        for name, expected in self.hashes.items():
            self.assertEqual(materials.digest(self.stage / name), expected)

    def test_reviewed_notice_fills_package_omission(self):
        (self.package / 'LICENSE').unlink()
        self.write('licenses/sample/LICENSE.txt', 'reviewed upstream notice')
        path = self.root / 'licenses/materials.json'
        evidence = json.loads(path.read_text())
        evidence['files']['sample/LICENSE.txt'] = materials.digest(self.root / 'licenses/sample/LICENSE.txt')
        evidence['windowsPackageNotices'] = {'sample/1.0': 'sample/LICENSE.txt'}
        path.write_text(json.dumps(evidence))
        materials.collect_windows(self.stage)
        self.assertEqual((self.stage / 'licenses/sample-1.0/LICENSE.txt').read_text(), 'reviewed upstream notice')
        self.write('licenses/sample/LICENSE.txt', 'tampered')
        with self.assertRaisesRegex(ValueError, 'evidence changed'):
            materials.verify_evidence()

    def test_unverified_notice_blocks_packaging(self):
        (self.package / 'LICENSE').unlink()
        path = self.root / 'licenses/materials.json'
        evidence = json.loads(path.read_text())
        evidence['windowsPackageNotices'] = {'sample/1.0': 'sample/LICENSE.txt'}
        path.write_text(json.dumps(evidence))
        with self.assertRaisesRegex(ValueError, 'Unverified package notice'):
            materials.collect_windows(self.stage)

    def test_tampered_dll_blocks_packaging(self):
        (self.stage / 'WinRT.Runtime.dll').write_bytes(b'changed')
        with self.assertRaisesRegex(ValueError, 'binary changed'):
            materials.collect_windows(self.stage)

    def test_missing_dependency_license_blocks_packaging(self):
        (self.package / 'LICENSE').unlink()
        with self.assertRaisesRegex(ValueError, 'No license text'):
            materials.collect_windows(self.stage)

    def test_changed_evidence_blocks_packaging(self):
        self.write('licenses/windows-sdk/sdk_license.rtf', 'changed')
        with self.assertRaisesRegex(ValueError, 'evidence changed'):
            materials.collect_windows(self.stage)

    def test_changed_targeting_pack_version_blocks_packaging(self):
        path = self.root / 'windows-app/AIBotBridge/obj/project.assets.json'
        path.write_text(path.read_text().replace('10.0.19041.56', '10.0.19041.99'))
        with self.assertRaisesRegex(ValueError, 'version changed'):
            materials.collect_windows(self.stage)

    def test_source_archive_detects_change_after_verification(self):
        name = 'source.cs'
        self.write(name, 'public source')
        expected = materials.digest(self.root / name)
        self.write('SOURCE_FILES.sha256', f'{expected}  {name}\n')
        self.write(name, 'changed after build')
        with self.assertRaisesRegex(ValueError, 'Source changed'):
            materials.archive_source(self.root / 'source.zip')

    def test_source_archive_rejects_private_tracked_file(self):
        self.write('auth.json', '{}')
        expected = materials.digest(self.root / 'auth.json')
        self.write('SOURCE_FILES.sha256', f'{expected}  auth.json\n')
        with self.assertRaisesRegex(ValueError, 'disallowed material'):
            materials.archive_source(self.root / 'source.zip')

    def test_source_archive_rejects_parent_path(self):
        self.write('SOURCE_FILES.sha256', '0' * 64 + '  ../outside.cs\n')
        with self.assertRaisesRegex(ValueError, 'escaped snapshot'):
            materials.archive_source(self.root / 'source.zip')

    def test_notice_copy_supports_long_paths(self):
        subtree = self.root / ('a' * 80)
        target = subtree / ('b' * 80) / ('c' * 80) / 'LICENSE'
        try:
            materials.copy_file(self.package / 'LICENSE', target)
            self.assertEqual(materials.digest(target), materials.digest(self.package / 'LICENSE'))
        finally:
            # tempfile's short Win32 path cannot clean this intentionally long tree.
            self.assertTrue(subtree.resolve().is_relative_to(self.root.resolve()))
            if subtree.exists():
                shutil.rmtree(materials.filesystem_path(subtree))


if __name__ == '__main__':
    unittest.main()
