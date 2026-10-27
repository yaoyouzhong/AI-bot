"""Packaging boundaries: reject corrupt/incomplete sets and stale release notes."""
import hashlib
import shutil
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from prepare_release_assets import prepare, ROOT
from test_component_versions import ComponentVersionTests, ComponentPayloadTests


class PackagingTests(unittest.TestCase):
    def test_project_publish_notices_are_registered(self):
        from collect_distribution_materials import verify_publish_notices, verify_evidence
        with tempfile.TemporaryDirectory() as temp:
            stage = Path(temp)
            for name in ("pdfpig/LICENSE", "pdfpig/SOURCE.json", "pmo-calendar/NOTICE.md"):
                target = stage / "licenses" / name
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(ROOT / "licenses" / name, target)
            verify_publish_notices(stage, verify_evidence())

    def test_publish_notices_accept_only_reviewed_project_bytes(self):
        from collect_distribution_materials import verify_publish_notices
        with tempfile.TemporaryDirectory() as temp:
            stage = Path(temp)
            notice = stage / "licenses/pdfpig/LICENSE.txt"
            notice.parent.mkdir(parents=True)
            notice.write_bytes(b"reviewed license")
            evidence = {"files": {"pdfpig/LICENSE.txt": hashlib.sha256(notice.read_bytes()).hexdigest()}}
            verify_publish_notices(stage, evidence)
            notice.write_bytes(b"changed license")
            with self.assertRaises(ValueError):
                verify_publish_notices(stage, evidence)
            notice.write_bytes(b"reviewed license")
            (stage / "licenses/unexpected.txt").write_bytes(b"unreviewed")
            with self.assertRaises(ValueError):
                verify_publish_notices(stage, evidence)

    def test_license_manifest_preserves_long_paths_and_detects_changed_bytes(self):
        from collect_distribution_materials import filesystem_path, manifest
        with tempfile.TemporaryDirectory() as temp:
            stage = Path(temp)
            relative = '/'.join(['licenses'] + ['dependency-' + 'x' * 55] * 5 + ['NOTICE.txt'])
            notice = filesystem_path(stage / relative)
            try:
                notice.parent.mkdir(parents=True)
                notice.write_bytes(b'complete upstream license')
                manifest(stage)
                inventory = (stage / 'FILES.sha256').read_text(encoding='ascii').splitlines()
                expected = hashlib.sha256(notice.read_bytes()).hexdigest()
                self.assertEqual(inventory, [f'{expected}  {relative}'])
                notice.write_bytes(b'changed upstream license')
                self.assertNotEqual(expected, hashlib.sha256(notice.read_bytes()).hexdigest())
            finally:
                shutil.rmtree(filesystem_path(stage / 'licenses'))

    def test_standard_source_excludes_in_progress_artwork_text(self):
        import xml.etree.ElementTree as ET
        project = ET.parse(ROOT / 'windows-app/AIBotBridge/AIBotBridge.csproj')
        guides = [item for item in project.findall('.//EmbeddedResource')
                  if item.get('LogicalName') == 'AIBotBridge.ArtGuides']
        self.assertEqual(len(guides), 1)
        self.assertEqual(guides[0].get('Condition'), "'$(LocalArt)' == 'true'")
        verifier = (ROOT / 'scripts/verify_release_local.ps1').read_text(encoding='utf-8')
        self.assertIn("$_ -notlike 'docs/art-guides/*'", verifier)

    def seed(self, directory):
        for name in ("AIBotBridge-0.1.0-setup-win-x64.exe",
                     "AIBotBridge-0.1.0-local-candidate-win-x64.zip",
                     "AIBotBridge-0.1.0-local-candidate-macos-arm64.zip",
                     "AI-bot-0.1.0-source.zip", "AI-bot-0.1.0-firmware-materials.zip"):
            data = name.encode()
            (directory / name).write_bytes(data)
            (directory / (name + ".sha256")).write_text(
                f"{hashlib.sha256(data).hexdigest()}  {name}\n", encoding="ascii")

    def test_complete_set_and_manifest(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            self.seed(directory)
            prepare(directory, "0.1.0", "a" * 40)
            for line in (directory / "SHA256SUMS.txt").read_text().splitlines():
                expected, name = line.split("  ", 1)
                self.assertEqual(expected, hashlib.sha256((directory / name).read_bytes()).hexdigest())
            self.assertIn('"sourceCommit": "' + "a" * 40, (directory / "BUILD.json").read_text())

    def test_missing_extra_and_corrupt_fail_without_metadata(self):
        for mutation in ("missing", "extra", "corrupt"):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp)
                self.seed(directory)
                archive = directory / "AI-bot-0.1.0-source.zip"
                if mutation == "missing":
                    archive.unlink()
                elif mutation == "extra":
                    (directory / "private.txt").write_text("unexpected")
                else:
                    archive.write_bytes(b"corrupted")
                with self.assertRaises(ValueError):
                    prepare(directory, "0.1.0", "a" * 40)
                self.assertFalse((directory / "BUILD.json").exists())

    def test_pending_notes_block_tag_but_finalized_notes_work(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            for name, heading in (("CHANGELOG.md", "Unreleased"), ("CHANGELOG.zh.md", "未发布")):
                (directory / name).write_text(f"## {heading}\n\n- Pending change\n\n## 0.1.0 - 2026-09-10\n\n- Released change\n", encoding="utf-8")
            command = [sys.executable, str(ROOT / "scripts/extract_release_notes.py"),
                       "v0.1.0", "notes.md", "--require-final"]
            result = subprocess.run(command, cwd=directory, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse((directory / "notes.md").exists())
            for name in ("CHANGELOG.md", "CHANGELOG.zh.md"):
                path = directory / name
                path.write_text(path.read_text(encoding="utf-8").replace("- Pending change", ""), encoding="utf-8")
            result = subprocess.run(command, cwd=directory, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            notes = (directory / "notes.md").read_text(encoding="utf-8")
            self.assertIn("Released change", notes)
            self.assertIn("/blob/main/DOWNLOADS.md", notes)
            self.assertIn("AI-bot-bridge-v0.1.0-windows-x64-setup.exe", notes)
            self.assertIn("AI-bot-esp8266-0.1.0-firmware.zip", notes)
            self.assertIn("DailyCalligraphy-", notes)
            self.assertRegex(notes, r"/releases/download/tab5-v([0-9.]+-ui)/AI-bot-tab5-\1-first-install\.zip")

    def test_installer_missing_or_tampered_blocks_release(self):
        for mutation in ('missing', 'tampered'):
            with self.subTest(mutation=mutation), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp)
                self.seed(directory)
                installer = directory / 'AIBotBridge-0.1.0-setup-win-x64.exe'
                if mutation == 'missing':
                    installer.unlink()
                else:
                    installer.write_bytes(b'changed installer')
                with self.assertRaises(ValueError):
                    prepare(directory, '0.1.0', 'a' * 40)
                self.assertFalse((directory / 'BUILD.json').exists())


if __name__ == "__main__":
    unittest.main()
