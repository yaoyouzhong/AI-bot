"""Independent component versions; legacy vX.Y.Z tags identify bridge-led bundles."""
from pathlib import Path
import json
import re

ROOT = Path(__file__).resolve().parents[1]
SEMVER = re.compile(r"(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?")


def components(root=ROOT):
    manifest = json.loads((root / "release-manifest.json").read_text(encoding="utf-8"))
    if manifest.get("schemaVersion") != 1 or set(manifest.get("components", {})) != {"bridge", "esp8266", "tab5"}:
        raise ValueError("Expected bridge, esp8266 and tab5 component definitions")
    return manifest["components"]


def versions(root=ROOT):
    result = {}
    for name, record in components(root).items():
        value = (root / record["versionFile"]).read_text(encoding="utf-8").strip()
        if not SEMVER.fullmatch(value):
            raise ValueError(f"Invalid {name} version: {value!r}")
        result[name] = value
    return result


def release_target(tag, root=ROOT):
    for name, record in components(root).items():
        if tag.startswith(record["tagPrefix"]):
            version = tag[len(record["tagPrefix"]):]
            if SEMVER.fullmatch(version):
                return name, version
            raise ValueError(f"Invalid component release tag: {tag!r}")
    if tag.startswith("v") and SEMVER.fullmatch(tag[1:]):
        return "bundle", tag[1:]
    raise ValueError(f"Unknown release tag: {tag!r}")


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("component", choices=("bridge", "esp8266", "tab5", "all"))
    args = parser.parse_args()
    value = versions()
    print(json.dumps(value, ensure_ascii=False) if args.component == "all" else value[args.component])
