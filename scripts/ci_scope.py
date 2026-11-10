"""Select product CI by changed paths; documentation/tooling never rebuild apps."""
import argparse
import json
import os
from pathlib import Path
import subprocess


def classify(paths):
    result = {key: False for key in ("windows", "macos", "firmware", "tools", "docs")}
    for path in paths:
        path = path.replace('\\', '/')
        result["tools"] = True
        if path.startswith("windows-app/"):
            result["windows"] = True
        elif path.startswith("mac-app/"):
            result["macos"] = True
        elif path.startswith(("esp8266/", "firmware/")):
            result["docs" if path.endswith(".md") else "firmware"] = True
        elif path == "VERSION":
            result["windows"] = result["macos"] = True
        elif path.startswith(("scripts/", "tools/", ".github/", "licenses/")) or path in ("release-manifest.json", "download-catalog.json", "gallery-hd-downloads.json"):
            pass
        elif path.startswith("docs/") or Path(path).suffix.lower() in (".md", ".png", ".jpg", ".jpeg", ".svg") or path in ("LICENSE", "THIRD_PARTY_NOTICES.md", "versions/TAB5", ".gitignore", ".gitattributes"):
            result["docs"] = True
        else:
            result["windows"] = result["macos"] = result["firmware"] = True
    return result


def changed(base, head):
    if not base or set(base) == {"0"}:
        return {key: True for key in ("windows", "macos", "firmware", "tools", "docs")}
    try:
        paths = subprocess.check_output(["git", "diff", "--no-renames", "--name-only", "-z", base, head], text=True).split('\0')
    except subprocess.CalledProcessError:
        return {key: True for key in ("windows", "macos", "firmware", "tools", "docs")}
    return classify(filter(None, paths))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", required=True)
    parser.add_argument("--head", default="HEAD")
    args = parser.parse_args()
    result = changed(args.base, args.head)
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
            for key, value in result.items():
                output.write(f"{key}={str(value).lower()}\n")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
