#!/usr/bin/env python3
"""Verify metadata within each independently versioned component."""

from __future__ import annotations

import argparse
import pathlib
import re
import sys
import xml.etree.ElementTree as ET
from component_versions import components, versions, release_target


ROOT = pathlib.Path(__file__).resolve().parents[1]


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--tag", help="Optional tag such as v0.1.0")
    parser.add_argument("--component", choices=("bridge", "esp8266", "tab5", "all"), default="all")
    args = parser.parse_args()

    current = versions(ROOT)
    targets = set(current) if args.component == "all" else {args.component}
    if args.tag:
        component, tagged = release_target(args.tag, ROOT)
        target = "bridge" if component == "bundle" else component
        if tagged != current[target]:
            raise SystemExit(f"tag {args.tag!r} does not match {target} {current[target]}")
        if args.component != "all" and target not in targets:
            raise SystemExit("Tag component does not match --component")
        if args.component == "all":
            targets = {"bridge", "esp8266"} if component == "bundle" else {target}
    version = current["bridge"]

    if "bridge" in targets:
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

    for component in sorted(targets):
        for name in components(ROOT)[component]["changelogs"]:
            text = (ROOT / name).read_text(encoding="utf-8")
            if not re.search(rf"^## {re.escape(current[component])} - \d{{4}}-\d{{2}}-\d{{2}}$", text, re.M):
                raise SystemExit(f"{name} has no section for {current[component]}")
        print(f"{component} version {current[component]} verified")
    return 0


if __name__ == "__main__":
    sys.exit(main())
