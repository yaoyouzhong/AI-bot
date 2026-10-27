"""Fail closed on missing, extra or corrupt candidate assets; write release metadata."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import zipfile
from component_versions import versions as declared_versions
from artifact_names import COMPONENT_KEYS, UPDATE_ALIASES, name as artifact_name, names as artifact_names, resolve

ROOT = Path(__file__).resolve().parents[1]


def prepare(directory: Path, version: str, commit: str, *, component="bundle", versions=None, tab5_source=None, desktop_platform="all"):
    if not re.fullmatch(r"[0-9a-f]{40}", commit):
        raise ValueError("Expected full source commit SHA")
    current = versions or {"bridge": version, "esp8266": version, "tab5": version}
    bridge, esp, tab = (current[name] for name in ("bridge", "esp8266", "tab5"))
    if desktop_platform not in ("all", "windows", "macos") or (desktop_platform != "all" and component != "bridge"):
        raise ValueError("Unsupported platform for component")
    keys = COMPONENT_KEYS["bridge"] + COMPONENT_KEYS["esp8266"] if component == "bundle" else COMPONENT_KEYS[component]
    if desktop_platform == "windows":
        keys = tuple(key for key in keys if key != "macos")
    elif desktop_platform == "macos":
        keys = ("macos", "bridge-source")
    archives = [resolve(directory, key, current["bridge" if key in COMPONENT_KEYS["bridge"] else "esp8266" if key == "esp8266" else "tab5"]) for key in keys]
    expected = {name + suffix for name in archives for suffix in ("", ".sha256")}
    if {p.name for p in directory.iterdir()} != expected:
        raise ValueError(f"Expected exactly {component} packages and their sidecar hashes: {', '.join(archives)}")
    for name in archives:
        archive = directory / name
        if archive.is_symlink() or not archive.is_file():
            raise ValueError(f"Invalid archive: {name}")
        actual = hashlib.sha256(archive.read_bytes()).hexdigest()
        checksum = (directory / (name + ".sha256")).read_text(encoding="ascii").strip()
        if checksum != f"{actual}  {name}":
            raise ValueError(f"Archive checksum mismatch: {name}")
    source_record = validate_tab5(directory, tab, source=tab5_source) if component == "tab5" else None
    for name in ("LICENSE", "THIRD_PARTY_NOTICES.md"):
        shutil.copyfile(ROOT / name, directory / name)
    (directory / "BUILD.json").write_text(json.dumps({
        "version": version, "component": component, "componentVersions": current, "sourceCommit": commit,
        "packageHashes": {name: hashlib.sha256((directory / name).read_bytes()).hexdigest() for name in archives},
        "tab5Source": source_record,
        "macOS": "arm64; ad-hoc signed; not notarized; interactive acceptance required" if component in ("bridge", "bundle") and desktop_platform != "windows" else "not included",
        "desktopPlatform": desktop_platform,
        "status": "candidate; publication requires separate acceptance"
    }, indent=2) + "\n", encoding="utf-8")
    lines = [f"{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}"
             for p in sorted(directory.iterdir())]
    (directory / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="ascii")


def validate_tab5(directory, version, *, source=None):
    with zipfile.ZipFile(directory / resolve(directory, "tab5-upgrade", version)) as upgrade:
        image = upgrade.read("aibot_tab5.bin")
        notes = json.loads(upgrade.read("aibot_tab5.bin.notes.json"))
        image_version = image[48:80].split(b"\0", 1)[0].decode("ascii")
        sha = hashlib.sha256(image).hexdigest()
        if image_version != version or notes["version"] != version or notes["sha256"] != sha:
            raise ValueError("TAB5 embedded version, sidecar and release version disagree")
    with zipfile.ZipFile(directory / resolve(directory, "tab5-first", version)) as first:
        record = json.loads(first.read("manifest.json"))
        factory = first.read("factory.bin")
        if record["version"] != version or hashlib.sha256(factory).hexdigest() != record["imageSha256"]:
            raise ValueError("TAB5 first-install version/image mismatch")
        app = next(s for s in record["segments"] if s["name"] == "application.bin")
        if app["sha256"] != sha or factory[app["offset"]:app["offset"]+app["size"]] != image:
            raise ValueError("TAB5 first-install and upgrade application differ")
    source = Path(source) if source else ROOT / "docs/development" / resolve(ROOT / "docs/development", "tab5-source", version)
    with zipfile.ZipFile(source) as snapshot:
        record = json.loads(snapshot.read("SOURCE-MANIFEST.json"))
        if record["version"] != version or record["applicationSha256"] != sha:
            raise ValueError("TAB5 public source snapshot does not match the application")
    return {"file": source.name, "sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "sourceCommit": record.get("sourceCommit")}


def user_payload(directory, output, component):
    """Stage only end-user downloads; retain source/build records in the audit directory."""
    if output.exists():
        raise ValueError("Use a new public payload directory")
    metadata = json.loads((directory / "BUILD.json").read_text(encoding="utf-8"))
    if metadata["component"] != component:
        raise ValueError("Payload component does not match validated build metadata")
    hashes = metadata["packageHashes"]
    expected = {name + suffix for name in hashes for suffix in ("", ".sha256")} | {"BUILD.json", "LICENSE", "THIRD_PARTY_NOTICES.md", "SHA256SUMS.txt"}
    if {p.name for p in directory.iterdir()} != expected:
        raise ValueError("Validated package directory changed")
    names = {}
    versions = metadata["componentVersions"]
    for key in ("windows", "macos", "esp8266", "tab5-first", "tab5-upgrade"):
        version = versions["bridge" if key in ("windows", "macos") else "esp8266" if key == "esp8266" else "tab5"]
        for source in artifact_names(key, version):
            if source not in hashes: continue
            names[artifact_name(key, version)] = source
            if key in UPDATE_ALIASES:
                names[artifact_names(key, version)[1]] = source
    # Recheck the validated bytes immediately before copying into the publication directory.
    for name in set(names.values()):
        digest = hashlib.sha256((directory / name).read_bytes()).hexdigest()
        if digest != hashes[name] or (directory / (name + ".sha256")).read_text(encoding="ascii").strip() != f"{digest}  {name}":
            raise ValueError(f"Package changed after validation: {name}")
    output.mkdir(parents=True)
    for target, source in sorted(names.items()):
        shutil.copyfile(directory / source, output / target)
    lines = [f"{hashlib.sha256((output / name).read_bytes()).hexdigest()}  {name}" for name in sorted(names)]
    (output / "SHA256SUMS.txt").write_text("\n".join(lines)+"\n", encoding="ascii")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--component", choices=("bundle", "bridge", "esp8266", "tab5"), default="bundle")
    parser.add_argument("--publish-directory", type=Path)
    parser.add_argument("--tab5-source", type=Path, help="Validated local source snapshot; defaults to the versioned repository archive")
    parser.add_argument("--desktop-platform", choices=("all", "windows", "macos"), default="all")
    args = parser.parse_args()
    current = declared_versions(ROOT)
    version = current["bridge" if args.component == "bundle" else args.component]
    if args.tab5_source and args.component != "tab5":
        parser.error("--tab5-source requires --component tab5")
    prepare(args.directory, version, args.commit, component=args.component, versions=current, tab5_source=args.tab5_source, desktop_platform=args.desktop_platform)
    if args.publish_directory:
        user_payload(args.directory, args.publish_directory, args.component)
    print("RELEASE_ASSETS_OK")
