"""Validate offline subsets against real pinned archives and runtime expectations."""
import hashlib
import io
import json
import os
from pathlib import Path
import re
import tarfile
import tempfile
import unittest
from unittest.mock import patch
import zipfile

from bundle_flash_tool import ROOT, TOOLS, VERSION, SOURCE_HASH, EXECUTABLE_HASHES, SUBSET_HASHES, bundle, compact_archive


class FlashBundleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cache = Path(os.environ.get("AIBOT_FLASH_BUNDLE_CACHE", ROOT / "artifacts/flash-bundle-cache"))
        for platform in TOOLS:
            suffix, expected, _ = TOOLS[platform]
            path = cls.cache / f"esptool-v{VERSION}-{suffix}"
            # Obtain build inputs once; all runtime/offline tests below prohibit downloads.
            if not path.exists():
                bundle(platform, ROOT / "artifacts/flash-bundle-test-inputs" / platform, cls.cache)
            assert hashlib.sha256(path.read_bytes()).hexdigest() == expected

    def members(self, platform, data):
        if platform == "windows":
            with zipfile.ZipFile(io.BytesIO(data)) as archive:
                return {entry.filename: archive.read(entry) for entry in archive.infolist() if not entry.is_dir()}
        with tarfile.open(fileobj=io.BytesIO(data)) as archive:
            self.assertEqual(next(entry.mode for entry in archive.getmembers() if entry.name.endswith("/esptool")), 0o755)
            return {entry.name: archive.extractfile(entry).read() for entry in archive.getmembers() if entry.isfile()}

    def test_only_needed_tool_and_notices_with_identical_upstream_bytes(self):
        for platform, (suffix, _, _) in TOOLS.items():
            with self.subTest(platform=platform):
                original = self.cache / f"esptool-v{VERSION}-{suffix}"
                compact, names = compact_archive(platform, original)
                members = self.members(platform, compact)
                upstream = self.members(platform, original.read_bytes())
                executable = "esptool.exe" if platform == "windows" else "esptool"
                self.assertEqual({Path(name).name for name in names}, {executable, "LICENSE", "README.md"})
                self.assertEqual(len(members), 3)
                for name, value in members.items():
                    self.assertEqual(value, upstream[name])
                self.assertEqual(hashlib.sha256(compact).hexdigest(), SUBSET_HASHES[platform])
                self.assertLess(len(compact), len(original.read_bytes()) / 3)

    def test_repeat_builds_and_runtime_pins_agree(self):
        runtime = {"windows": ROOT / "windows-app/AIBotBridge/FirmwareFlasher.cs", "macos": ROOT / "mac-app/Sources/AIBotBridge/FirmwareFlasher.swift"}
        for platform, (suffix, _, _) in TOOLS.items():
            path = self.cache / f"esptool-v{VERSION}-{suffix}"
            a, _ = compact_archive(platform, path)
            b, _ = compact_archive(platform, path)
            self.assertEqual(a, b)
            self.assertIn('"' + hashlib.sha256(a).hexdigest() + '"', runtime[platform].read_text(encoding="utf-8"))
            self.assertNotEqual(hashlib.sha256(a + b"tampered").hexdigest(), SUBSET_HASHES[platform])

    def test_both_bundles_build_with_network_prohibited_and_include_matching_source(self):
        with tempfile.TemporaryDirectory() as temp, patch("urllib.request.urlopen", side_effect=AssertionError("offline test attempted a download")):
            for platform, (_, upstream_hash, extension) in TOOLS.items():
                target = Path(temp) / platform
                bundle(platform, target, self.cache)
                origin = json.loads((target / "ORIGIN.json").read_text(encoding="utf-8"))
                self.assertEqual(origin["upstreamToolSHA256"], upstream_hash)
                self.assertEqual(origin["executableSHA256"], EXECUTABLE_HASHES[platform])
                self.assertEqual(origin["sourceSHA256"], SOURCE_HASH)
                self.assertEqual(hashlib.sha256((target / f"esptool-{VERSION}.{extension}").read_bytes()).hexdigest(), SUBSET_HASHES[platform])
                self.assertEqual(hashlib.sha256((target / f"esptool-{VERSION}-source.tar.gz").read_bytes()).hexdigest(), SOURCE_HASH)

    def test_missing_notice_or_changed_executable_is_rejected(self):
        original = self.cache / f"esptool-v{VERSION}-{TOOLS['windows'][0]}"
        members = self.members("windows", original.read_bytes())
        with tempfile.TemporaryDirectory() as temp:
            for fault in ("missing-license", "changed-executable"):
                target = Path(temp) / (fault + ".zip")
                with zipfile.ZipFile(target, "w") as archive:
                    for name, value in members.items():
                        if fault == "missing-license" and name.endswith("/LICENSE"):
                            continue
                        if fault == "changed-executable" and name.endswith("/esptool.exe"):
                            value = value[:-1] + bytes([value[-1] ^ 1])
                        archive.writestr(name, value)
                with self.assertRaises(ValueError):
                    compact_archive("windows", target)


if __name__ == "__main__":
    unittest.main()
