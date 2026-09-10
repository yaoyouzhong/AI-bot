#!/usr/bin/env python3
"""Extract one version section from CHANGELOG.md."""

from __future__ import annotations

import argparse
import pathlib
import re


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("tag")
    parser.add_argument("output", type=pathlib.Path)
    parser.add_argument("--require-final", action="store_true")
    args = parser.parse_args()

    version = args.tag.removeprefix("v")
    changelog = pathlib.Path("CHANGELOG.md").read_text(encoding="utf-8")
    if args.require_final:
        for filename, heading in (("CHANGELOG.md", "Unreleased"), ("CHANGELOG.zh.md", "未发布")):
            text = pathlib.Path(filename).read_text(encoding="utf-8")
            pending = re.search(rf"^## {heading}\s*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
            if pending and pending.group(1).strip():
                raise SystemExit(f"Finalize pending changes in {filename} before tagging")
    pattern = re.compile(
        rf"(^## {re.escape(version)} - \d{{4}}-\d{{2}}-\d{{2}}\n.*?)(?=^## |\Z)",
        re.M | re.S,
    )
    match = pattern.search(changelog)
    if not match:
        raise SystemExit(f"no release notes found for {version}")

    args.output.write_text(match.group(1).rstrip() + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
