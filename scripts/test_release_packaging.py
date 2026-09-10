"""Packaging boundaries: reject corrupt/incomplete sets and stale release notes."""
import hashlib
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from prepare_release_assets import prepare, ROOT


class PackagingTests(unittest.TestCase):
    def seed(self, directory):
        for name in ("AIBotBridge-0.1.0-local-candidate-win-x64.zip",
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
            self.assertIn("Released change", (directory / "notes.md").read_text())


if __name__ == "__main__":
    unittest.main()
