"""Download completeness and component release isolation regressions."""
import copy
import io
import unittest
from unittest.mock import patch

import download_catalog as catalog


class DownloadCatalogTests(unittest.TestCase):
    def setUp(self):
        self.data = catalog.load()

    def next_gallery_version(self):
        parts = max(tuple(map(int, self.data["downloads"][key]["version"].split(".")))
                    for key in ("painting", "calligraphy"))
        revision = parts[3] + 1 if len(parts) == 4 else 1
        return f"{parts[0]:04d}.{parts[1]:02d}.{parts[2]:02d}.{revision}"

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

    def test_gallery_content_editions_preserve_package_identity(self):
        before = copy.deepcopy(self.data)
        for language in ("zh", "en"):
            rows = catalog.table(self.data, language).splitlines()
            for key in ("painting", "calligraphy"):
                item = self.data["downloads"][key]
                index = next(i for i, row in enumerate(rows) if catalog.asset_url(item) in row)
                self.assertIn("| 2026.1 |", rows[index])
                self.assertIn("| 2026.1 |", rows[index + 1])
                self.assertIn(item["file"], rows[index])
            self.assertNotIn("| 2026.10.16 |", "\n".join(rows))
        self.assertEqual(before, self.data)
        self.assertEqual(catalog.gallery_edition("painting", "2026.10.18"), "2026.10.18")
        self.assertEqual(catalog.gallery_edition("windows", "0.6.8"), "0.6.8")

    def test_stable_discovery_ignores_drafts_prereleases_and_wrong_tags(self):
        releases = self.releases()
        for flags in ({"draft": True}, {"prerelease": True}, {"tag_name": "esp8266-v99.0.0"}):
            invalid = {"tag_name": "bridge-v99.0.0", "published_at": "2027-01-01", "draft": False, "prerelease": False,
                       "assets": [{"name": "AIBotBridge-99.0.0-setup-win-x64.exe", "size": 10, "digest": "sha256:" + "a"*64}], **flags}
            releases.append(invalid)
        self.assertEqual(catalog.discover(releases), self.data)

    def test_windows_only_release_preserves_current_mac_download(self):
        releases = self.releases() + [{"tag_name": "bridge-v99.0.0", "published_at": "2020-01-01", "draft": False, "prerelease": False,
            "assets": [{"name": "AIBotBridge-99.0.0-setup-win-x64.exe", "size": 10, "digest": "sha256:"+"a"*64}, {"name": "SHA256SUMS.txt"}]}]
        result = catalog.discover(releases)
        self.assertEqual(result["downloads"]["windows"]["version"], "99.0.0")
        self.assertEqual(result["downloads"]["macos"], self.data["downloads"]["macos"])
        for language in ("zh", "en"):
            rendered = catalog.table(result, language, tag="bridge-v99.0.0")
            self.assertIn(catalog.asset_url(self.data["downloads"]["macos"]), rendered)
            self.assertNotIn("macos-arm64.zip", "\n".join(row for row in rendered.splitlines() if "bridge-v99.0.0" in row))
            self.assertEqual(rendered.count("本次发布" if language == "zh" else "this release"), 1)

    def test_mac_only_release_preserves_current_windows_download(self):
        releases = self.releases() + [{"tag_name": "bridge-v99.0.0", "published_at": "2027-01-01", "draft": False, "prerelease": False,
            "assets": [{"name": "AI-bot-bridge-v99.0.0-macos-arm64.zip", "size": 10, "digest": "sha256:"+"b"*64}, {"name": "SHA256SUMS.txt"}]}]
        result = catalog.discover(releases)
        self.assertEqual(result["downloads"]["macos"]["version"], "99.0.0")
        self.assertEqual(result["downloads"]["windows"], self.data["downloads"]["windows"])

    def test_gallery_revision_is_discovered_and_preserves_other_components(self):
        version = self.next_gallery_version()
        release = {"tag_name": "tab5-v0.2.156-ui", "published_at": "2026-10-09", "draft": False,
                   "prerelease": False, "assets": [{"name": "SHA256SUMS.txt"}]}
        expected = copy.deepcopy(self.data)
        for key in ("painting", "calligraphy"):
            entry = expected["downloads"][key]
            entry.update(version=version, tag=release["tag_name"],
                         file=catalog.SPECS[key][1].format(version=version))
            release["assets"].append({"name": entry["file"], "size": entry["size"],
                                      "digest": "sha256:" + entry["sha256"]})
        self.assertEqual(catalog.discover(self.releases() + [release]), expected)
        release["draft"] = True
        self.assertEqual(catalog.discover(self.releases() + [release]), self.data)

    def test_curated_gallery_names_are_discovered_without_changing_firmware(self):
        version = self.next_gallery_version()
        release = {"tag_name": "tab5-v0.2.156-ui", "published_at": "2026-10-09", "draft": False,
                   "prerelease": False, "assets": [{"name": "SHA256SUMS.txt"}]}
        expected = copy.deepcopy(self.data)
        for key in ("painting", "calligraphy"):
            entry = expected["downloads"][key]
            entry.update(version=version, tag=release["tag_name"],
                         file=catalog.artifact_names(key, version)[1])
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
            self.assertEqual(rendered.count("[SHA-256]"), 9)
        self.assertEqual(self.data, catalog.load())

    def test_download_labels_use_matching_action_and_size_for_standard_and_hd(self):
        hd = catalog.load_hd()
        for language in ("zh", "en"):
            rows = catalog.table(self.data, language).splitlines()
            for key, collection in hd["collections"].items():
                standard = next(i for i, row in enumerate(rows) if catalog.asset_url(self.data["downloads"][key]) in row)
                row = rows[standard + 1]
                self.assertIn(catalog.HD_GUIDE + "#download", row)
                self.assertIn(catalog.HD_GUIDE + "#" + key, row)
                action = "下载" if language == "zh" else "Download"
                self.assertIn(f"[{action} · {collection['size'] / 1073741824:.2f} GiB]({catalog.HD_GUIDE}#download)", row)
                self.assertNotIn(".part", row)
                # Complete archives are merged locally, not single published ZIP assets.
                self.assertNotIn(catalog.asset_url(hd, collection["file"]), row)
            for item in self.data["downloads"].values():
                self.assertIn(f"[{action} · {item['size'] / 1048576:.0f} MiB]({catalog.asset_url(item)})", "\n".join(rows))

    def test_readmes_and_centers_match_index(self):
        catalog.sync(self.data, check=True)

    def test_canonical_names_win_over_compatibility_aliases_at_same_version(self):
        releases = self.releases()
        expected = copy.deepcopy(self.data)
        for key, entry in expected["downloads"].items():
            entry["file"] = catalog.artifact_names(key, entry["version"])[0]
            release = next(r for r in releases if r["tag_name"] == entry["tag"])
            release["assets"].append({"name": entry["file"], "size": entry["size"], "digest": "sha256:" + entry["sha256"]})
        self.assertEqual(catalog.discover(releases), expected)

    def test_published_release_keeps_verified_sizes_without_reusing_draft_sizes(self):
        for key in ("windows", "macos", "esp8266", "tab5-first", "tab5-upgrade"):
            item = self.data["downloads"][key]
            for language in ("zh", "en"):
                rendered = catalog.table(self.data, language, tag=item["tag"])
                row = next(line for line in rendered.splitlines() if catalog.asset_url(item) in line)
                self.assertIn(f"{item['size'] / 1048576:.0f} MiB", row)
        draft = catalog.table(self.data, tag="bridge-v99.0.0")
        for line in draft.splitlines():
            if "bridge-v99.0.0" in line:
                self.assertNotIn("MiB", line)


if __name__ == "__main__":
    unittest.main()
