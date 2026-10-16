import unittest
from artifact_names import name, names, match


class ArtifactNamesTests(unittest.TestCase):
    def test_bridge_uses_v_but_firmware_does_not(self):
        self.assertEqual(name("windows", "0.6.8"), "AI-bot-bridge-v0.6.8-windows-x64-setup.exe")
        self.assertEqual(name("esp8266", "0.5.4"), "AI-bot-esp8266-0.5.4-firmware.zip")
        self.assertEqual(name("tab5-upgrade", "0.2.206-ui"), "AI-bot-tab5-0.2.206-ui-upgrade.zip")
        self.assertIsNone(match("esp8266", "AI-bot-esp8266-v0.5.4-firmware.zip"))
        self.assertIsNone(match("tab5-upgrade", "AI-bot-tab5-v0.2.206-ui-upgrade.zip"))

    def test_legacy_and_canonical_names_preserve_versions_and_reject_paths(self):
        for key, version in (("windows", "0.6.8"), ("macos", "0.6.8"), ("esp8266", "0.5.4"),
                             ("tab5-upgrade", "0.2.206-ui"), ("painting", "2026.10.16.1"), ("calligraphy-hd", "2026.10.16")):
            for filename in names(key, version):
                self.assertEqual(match(key, filename)[0], version)
            with self.assertRaises(ValueError):
                name(key, "../" + version)
        self.assertIsNone(match("windows", "AIBotBridge-0.6.8.1-setup-win-x64.exe"))
        self.assertIsNone(match("painting", "AI-bot-gallery-painting-hd-2026.10.16.zip"))


if __name__ == "__main__":
    unittest.main()
