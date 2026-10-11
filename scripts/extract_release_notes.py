#!/usr/bin/env python3
"""Extract matching Chinese and English component release notes."""

from __future__ import annotations

import argparse
import pathlib
import re
from component_versions import components, release_target
from download_catalog import release_intro


def version_section(filename: str, version: str) -> str:
    changelog = pathlib.Path(filename).read_text(encoding="utf-8")
    pattern = re.compile(
        rf"(^## {re.escape(version)} - \d{{4}}-\d{{2}}-\d{{2}}\n.*?)(?=^## |\Z)",
        re.M | re.S,
    )
    match = pattern.search(changelog)
    if not match:
        raise SystemExit(f"no release notes found for {version} in {filename}")
    return match.group(1).rstrip()


def public_links(text: str, filename: str) -> str:
    def replace(match):
        target = match.group(1)
        if target.startswith(("#", "/")) or ":" in target:
            return match.group(0)
        path, separator, fragment = target.partition("#")
        try:
            relative = (pathlib.Path(filename).parent / path).resolve().relative_to(pathlib.Path.cwd().resolve())
        except ValueError:
            return match.group(0)
        return "](https://github.com/yaoyouzhong/AI-bot/blob/main/" + relative.as_posix() + separator + fragment + ")"
    return re.sub(r"\]\(([^)]+)\)", replace, text)


def bilingual_notes(files: list[str], version: str) -> str:
    english, chinese = [version_section(filename, version) for filename in files]
    header, en_body = english.split("\n", 1)
    zh_header, zh_body = chinese.split("\n", 1)
    if header != zh_header:
        raise SystemExit(f"Chinese and English release dates differ for {version}")
    sections = []
    for label, body, filename in (("中文更新说明", zh_body, files[1]), ("English release notes", en_body, files[0])):
        body = re.sub(r"^(#{3,}) ", r"#\1 ", body.strip(), flags=re.M)
        sections.append(f"### {label}\n\n" + public_links(body, filename))
    return header + "\n\n" + "\n\n".join(sections) + "\n"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("tag")
    parser.add_argument("output", type=pathlib.Path)
    parser.add_argument("--require-final", action="store_true")
    args = parser.parse_args()

    component, version = release_target(args.tag)
    target = "bridge" if component == "bundle" else component
    files = components()[target]["changelogs"]
    if args.require_final:
        for filename, heading in zip(files, ("Unreleased", "未发布")):
            text = pathlib.Path(filename).read_text(encoding="utf-8")
            pending = re.search(rf"^## {heading}\s*\n(.*?)(?=^## |\Z)", text, re.M | re.S)
            if pending and pending.group(1).strip():
                raise SystemExit(f"Finalize pending changes in {filename} before tagging")
    notes = bilingual_notes(files, version)
    args.output.write_text(release_intro(args.tag) + notes, encoding="utf-8")


if __name__ == "__main__":
    main()
