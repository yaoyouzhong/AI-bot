"""Download completeness and component release isolation regressions."""
import copy
import io
import unittest
from unittest.mock import patch

import download_catalog as catalog


class DownloadCatalogTests(unittest.TestCase):
    def setUp(self):
        self.data = catalog.load()

    def releases(self):
        releases = {}
        for entry in self.data["downloads"].values():
            release = releases.setdefault(entry["tag"], {"tag_name": entry["tag"], "published_at": "2026-10-08", "draft": False,
                "prerelease": False, "assets": [{"name": "SHA256SUMS.txt"}]})
            release["assets"].append({"name": entry["file"], "size": entry["size"], "digest": "sha256:" + entry["sha256"]})
        return list(releases.values())

    def test_missing_download_and_mismatched_pair_are_rejected(self):
        for key in catalog.SPECS:
            data = copy.deepcopy(self.data)
            del data["downloads"][key]
            with self.assertRaises(ValueError):
                catalog.validate(data)
        data = copy.deepcopy(self.data)
        data["downloads"]["tab5-first"].update(version="99.0.0-ui", tag="tab5-v99.0.0-ui", file="TAB5-first-install-99.0.0-ui.zip")
        with self.assertRaisesRegex(ValueError, "Incomplete paired release"):
            catalog.validate(data)

    def test_stable_discovery_ignores_drafts_prereleases_and_wrong_tags(self):
        releases = self.releases()
        for flags in ({"draft": True}, {"prerelease": True}, {"tag_name": "esp8266-v99.0.0"}):
            invalid = {"tag_name": "bridge-v99.0.0", "published_at": "2027-01-01", "draft": False, "prerelease": False,
                       "assets": [{"name": "AIBotBridge-99.0.0-setup-win-x64.exe", "size": 10, "digest": "sha256:" + "a"*64}], **flags}
            releases.append(invalid)
        self.assertEqual(catalog.discover(releases), self.data)

    def test_newer_incomplete_bridge_is_not_silently_hidden(self):
        releases = self.releases() + [{"tag_name": "bridge-v99.0.0", "published_at": "2020-01-01", "draft": False, "prerelease": False,
            "assets": [{"name": "AIBotBridge-99.0.0-setup-win-x64.exe", "size": 10, "digest": "sha256:"+"a"*64}, {"name": "SHA256SUMS.txt"}]}]
        with self.assertRaisesRegex(ValueError, "Incomplete paired release"):
            catalog.discover(releases)

    def test_gallery_revision_is_discovered_and_preserves_other_components(self):
        release = {"tag_name": "tab5-v0.2.156-ui", "published_at": "2026-10-09", "draft": False,
                   "prerelease": False, "assets": [{"name": "SHA256SUMS.txt"}]}
        expected = copy.deepcopy(self.data)
        for key in ("painting", "calligraphy"):
            entry = expected["downloads"][key]
            entry.update(version="2026.10.08.1", tag=release["tag_name"],
                         file=catalog.SPECS[key][1].format(version="2026.10.08.1"))
            release["assets"].append({"name": entry["file"], "size": entry["size"],
                                      "digest": "sha256:" + entry["sha256"]})
        self.assertEqual(catalog.discover(self.releases() + [release]), expected)
        release["draft"] = True
        self.assertEqual(catalog.discover(self.releases() + [release]), self.data)

    def test_curated_gallery_names_are_discovered_without_changing_firmware(self):
        release = {"tag_name": "tab5-v0.2.156-ui", "published_at": "2026-10-09", "draft": False,
                   "prerelease": False, "assets": [{"name": "SHA256SUMS.txt"}]}
        expected = copy.deepcopy(self.data)
        for key in ("painting", "calligraphy"):
            entry = expected["downloads"][key]
            entry.update(version="2026.10.09", tag=release["tag_name"],
                         file=catalog.SPECS[key][1].format(version="Curated-2026.10.09"))
            release["assets"].append({"name": entry["file"], "size": entry["size"],
                                      "digest": "sha256:" + entry["sha256"]})
        self.assertEqual(catalog.discover(self.releases() + [release]), expected)
        release["draft"] = True
        self.assertEqual(catalog.discover(self.releases() + [release]), self.data)

    def test_missing_checksum_and_corrupt_published_checksum_are_rejected(self):
        releases = self.releases()
        releases[0]["assets"] = [a for a in releases[0]["assets"] if a["name"] != "SHA256SUMS.txt"]
        with self.assertRaisesRegex(ValueError, "Missing checksum"):
            catalog.discover(releases)
        with patch.object(catalog, "public_releases", return_value=self.releases()), patch.object(catalog, "request", return_value=io.BytesIO(b"invalid\n")):
            with self.assertRaisesRegex(ValueError, "checksum mismatch"):
                catalog.verify_online(self.data)

    def test_component_release_changes_only_its_own_downloads(self):
        for tag, changed in (("bridge-v99.0.0", {"windows", "macos"}), ("esp8266-v99.0.0", {"esp8266"}),
                             ("tab5-v99.0.0-ui", {"tab5-first", "tab5-upgrade"})):
            rendered = catalog.table(self.data, tag=tag)
            for key, item in self.data["downloads"].items():
                if key not in changed:
                    self.assertIn(catalog.asset_url(item), rendered)
                else:
                    self.assertNotIn(catalog.asset_url(item), rendered)
            self.assertEqual(rendered.count("本次发布"), len(changed))
            self.assertEqual(rendered.count("[SHA-256]"), 7)
        self.assertEqual(self.data, catalog.load())

    def test_readmes_and_centers_match_index(self):
        catalog.sync(self.data, check=True)


if __name__ == "__main__":
    unittest.main()
