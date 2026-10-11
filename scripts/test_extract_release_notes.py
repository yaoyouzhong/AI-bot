"""Release pages must include both maintained languages for the chosen component."""
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

from component_versions import ROOT, components


class ReleaseNoteTests(unittest.TestCase):
    def run_extract(self, directory, tag):
        return subprocess.run(
            [sys.executable, str(ROOT / "scripts/extract_release_notes.py"), tag, "notes.md", "--require-final"],
            cwd=directory, capture_output=True, encoding="utf-8",
        )

    def seed(self, directory, component):
        record = components()[component]
        for index, filename in enumerate(record["changelogs"]):
            path = directory / filename
            path.parent.mkdir(parents=True, exist_ok=True)
            body = "### 功能\n\n- 正式中文内容" if index else "### Features\n\n- English release content"
            path.write_text(f"## 1.2.3 - 2026-10-11\n\n{body}\n\n## 1.2.2 - 2026-10-10\n\n- Earlier version\n", encoding="utf-8")
        return record

    def test_all_component_pages_include_chinese_first_and_matching_english(self):
        for component in components():
            with self.subTest(component=component), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp)
                record = self.seed(directory, component)
                result = self.run_extract(directory, record["tagPrefix"] + "1.2.3")
                self.assertEqual(result.returncode, 0, result.stderr)
                notes = (directory / "notes.md").read_text(encoding="utf-8")
                self.assertIn("#### 功能", notes)
                self.assertIn("#### Features", notes)
                self.assertLess(notes.index("正式中文内容"), notes.index("English release content"))
                self.assertNotIn("Earlier version", notes)

    def test_missing_chinese_version_cannot_generate_an_english_only_page(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            record = self.seed(directory, "bridge")
            path = directory / record["changelogs"][1]
            path.write_text("## 1.2.2 - 2026-10-10\n\n- 旧版本\n", encoding="utf-8")
            result = self.run_extract(directory, "bridge-v1.2.3")
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse((directory / "notes.md").exists())

    def test_mismatched_dates_cannot_generate_a_combined_page(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            record = self.seed(directory, "bridge")
            path = directory / record["changelogs"][1]
            path.write_text(path.read_text(encoding="utf-8").replace("2026-10-11", "2026-10-12"), encoding="utf-8")
            result = self.run_extract(directory, "bridge-v1.2.3")
            self.assertNotEqual(result.returncode, 0)
            self.assertFalse((directory / "notes.md").exists())

    def test_release_record_link_resolves_from_the_component_changelog(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            record = self.seed(directory, "tab5")
            path = directory / record["changelogs"][1]
            path.write_text(path.read_text(encoding="utf-8").replace("正式中文内容", "正式中文内容 [记录](../RELEASE.md#验证) [网址](https://example.com) [段落](#功能)"), encoding="utf-8")
            result = self.run_extract(directory, "tab5-v1.2.3")
            self.assertEqual(result.returncode, 0, result.stderr)
            notes = (directory / "notes.md").read_text(encoding="utf-8")
            self.assertIn("https://github.com/yaoyouzhong/AI-bot/blob/main/docs/RELEASE.md#验证", notes)
            self.assertIn("[网址](https://example.com)", notes)
            self.assertIn("[段落](#功能)", notes)


if __name__ == "__main__":
    unittest.main()
