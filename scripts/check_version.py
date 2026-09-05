#!/usr/bin/env python3
"""Verify that the repository's declared versions agree."""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
import xml.etree.ElementTree as ET


ROOT = pathlib.Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", help="Optional tag such as v0.1.0")
    args = parser.parse_args()

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?", version):
        raise SystemExit(f"VERSION is not valid SemVer: {version!r}")

    project = (ROOT / "windows-app" / "AIBotBridge" / "AIBotBridge.csproj").read_text(
        encoding="utf-8"
    )
    match = re.search(r"<Version>([^<]+)</Version>", project)
    if not match or match.group(1) != version:
        raise SystemExit("AIBotBridge.csproj version does not match VERSION")

    plist = ET.parse(ROOT / "mac-app" / "Info.plist").getroot()
    plist_dict = plist.find("dict")
    values = list(plist_dict) if plist_dict is not None else []
    mac_version = next(
        (values[index + 1].text for index, item in enumerate(values[:-1])
         if item.tag == "key" and item.text == "CFBundleShortVersionString"),
        None,
    )
    if mac_version != version:
        raise SystemExit("mac-app/Info.plist version does not match VERSION")

    for name in ("CHANGELOG.md", "CHANGELOG.zh.md"):
        text = (ROOT / name).read_text(encoding="utf-8")
        if not re.search(rf"^## {re.escape(version)} - \d{{4}}-\d{{2}}-\d{{2}}$", text, re.M):
            raise SystemExit(f"{name} has no section for {version}")

    if args.tag and args.tag != f"v{version}":
        raise SystemExit(f"tag {args.tag!r} does not match v{version}")

    print(f"version {version} verified")
    return 0


if __name__ == "__main__":
    sys.exit(main())
