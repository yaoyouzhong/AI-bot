"""Regress independent version lines, tag routing and per-component payloads."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

import check_version
from component_versions import ROOT, components, versions, release_target
from prepare_release_assets import prepare, user_payload, validate_tab5


class ComponentVersionTests(unittest.TestCase):
    def test_independent_versions_and_tag_routing(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            records = components()
            (root / "release-manifest.json").write_text(json.dumps({"schemaVersion": 1, "components": records}))
            expected = {"bridge": "2.0.0", "esp8266": "0.5.0", "tab5": "0.2.89-ui"}
            for name, record in records.items():
                path = root / record["versionFile"]
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(expected[name])
                for filename in record["changelogs"]:
                    path = root / filename
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_text(f"## {expected[name]} - 2026-10-03\n\n- Fixture.\n")
            self.assertEqual(expected, versions(root))
            for component, tag in (("bridge", "bridge-v2.0.0"), ("esp8266", "esp8266-v0.5.0"), ("tab5", "tab5-v0.2.89-ui")):
                self.assertEqual((component, expected[component]), release_target(tag, root))
            self.assertEqual(("bundle", "2.0.0"), release_target("v2.0.0", root))
            for tag in ("tab5-vbroken", "unknown-v2.0.0", "esp8266-v../0.5.0"):
                with self.assertRaises(ValueError):
                    release_target(tag, root)
            project = root / "windows-app/AIBotBridge/AIBotBridge.csproj"
            project.parent.mkdir(parents=True)
            project.write_text("<Project><Version>2.0.0</Version></Project>")
            plist = root / "mac-app/Info.plist"
            plist.parent.mkdir()
            plist.write_text("<plist><dict><key>CFBundleShortVersionString</key><string>2.0.0</string></dict></plist>")
            with patch.object(check_version, "ROOT", root), patch.object(sys, "argv", ["check_version", "--tag", "bridge-v2.0.0"]):
                self.assertEqual(0, check_version.main())
            with patch.object(check_version, "ROOT", root), patch.object(sys, "argv", ["check_version", "--tag", "esp8266-v2.0.0"]):
                with self.assertRaises(SystemExit):
                    check_version.main()
            # An unrelated bridge release still being prepared must not block an ESP tag.
            project.unlink()
            plist.unlink()
            for filename in records["bridge"]["changelogs"]:
                (root/filename).unlink()
            with patch.object(check_version, "ROOT", root), patch.object(sys, "argv", ["check_version", "--tag", "esp8266-v0.5.0"]):
                self.assertEqual(0, check_version.main())


class ComponentPayloadTests(unittest.TestCase):
    current = {"bridge": "0.6.0", "esp8266": "0.5.0", "tab5": "0.2.89-ui"}
    bridge_names = ["AIBotBridge-0.6.0-setup-win-x64.exe", "AIBotBridge-0.6.0-local-candidate-win-x64.zip",
                    "AIBotBridge-0.6.0-local-candidate-macos-arm64.zip", "AI-bot-0.6.0-source.zip"]
    esp_names = ["AI-bot-0.5.0-firmware-materials.zip"]

    def seed(self, directory, names):
        for name in names:
            data = name.encode()
            (directory / name).write_bytes(data)
            (directory / (name+".sha256")).write_text(f"{hashlib.sha256(data).hexdigest()}  {name}\n")

    def test_bridge_only_and_esp_only_need_no_other_component_packages(self):
        for component, names, count in (("bridge", self.bridge_names, 3), ("esp8266", self.esp_names, 2)):
            with self.subTest(component=component), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp) / "audit"
                directory.mkdir()
                self.seed(directory, names)
                prepare(directory, self.current[component], "a"*40, component=component, versions=self.current)
                output = Path(temp) / "public"
                user_payload(directory, output, component)
                self.assertEqual(count, len(list(output.iterdir())))
                self.assertFalse(any("source.zip" in p.name or p.suffix == ".sha256" for p in output.iterdir()))

    def test_legacy_bundle_uses_independent_firmware_name(self):
        with tempfile.TemporaryDirectory() as temp:
            directory = Path(temp)
            self.seed(directory, self.bridge_names+self.esp_names)
            prepare(directory, "0.6.0", "a"*40, versions=self.current)
            record = json.loads((directory/"BUILD.json").read_text())
            self.assertEqual(self.current, record["componentVersions"])

    def test_payload_rejects_added_files_or_rewritten_hashes(self):
        for change in ("extra", "rewrite"):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as temp:
                directory = Path(temp) / "audit"
                directory.mkdir()
                self.seed(directory, self.esp_names)
                prepare(directory, "0.5.0", "a"*40, component="esp8266", versions=self.current)
                if change == "extra":
                    (directory/"private.txt").write_text("unexpected")
                else:
                    name = self.esp_names[0]
                    data = b"rewritten package"
                    (directory/name).write_bytes(data)
                    (directory/(name+".sha256")).write_text(f"{hashlib.sha256(data).hexdigest()}  {name}\n")
                output = Path(temp)/"public"
                with self.assertRaises(ValueError):
                    user_payload(directory, output, "esp8266")
                self.assertFalse(output.exists())

    def test_tab5_image_notes_factory_and_public_source_must_match(self):
        version = "0.2.89-ui"
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            directory = root/"audit"
            directory.mkdir()
            image = bytearray(128)
            image[48:48+len(version)] = version.encode()
            sha = hashlib.sha256(image).hexdigest()
            notes = {"version": version, "sha256": sha}
            with zipfile.ZipFile(directory/f"TAB5-upgrade-{version}.zip", "w") as z:
                z.writestr("aibot_tab5.bin", image)
                z.writestr("aibot_tab5.bin.notes.json", json.dumps(notes))
            first = {"version": version, "imageSha256": sha, "segments": [{"name": "application.bin", "sha256": sha, "offset": 0, "size": len(image)}]}
            with zipfile.ZipFile(directory/f"TAB5-first-install-{version}.zip", "w") as z:
                z.writestr("factory.bin", image)
                z.writestr("manifest.json", json.dumps(first))
            source = root/f"docs/development/TAB5-{version}-source.zip"
            source.parent.mkdir(parents=True)
            with zipfile.ZipFile(source, "w") as z:
                z.writestr("SOURCE-MANIFEST.json", json.dumps({"version": version, "applicationSha256": sha}))
            with patch("prepare_release_assets.ROOT", root):
                validate_tab5(directory, version)
                notes["version"] = "0.2.90-ui"
                with zipfile.ZipFile(directory/f"TAB5-upgrade-{version}.zip", "w") as z:
                    z.writestr("aibot_tab5.bin", image)
                    z.writestr("aibot_tab5.bin.notes.json", json.dumps(notes))
                with self.assertRaises(ValueError):
                    validate_tab5(directory, version)


if __name__ == "__main__":
    unittest.main()
