"""Collect actual build dependencies and their notices; never read user profiles.

Outputs are new directories. Dependency/evidence drift fails closed. This validates
materials and byte identity, not legal compliance or functional device acceptance.
"""
from __future__ import annotations

import argparse
import configparser
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import zipfile

ROOT = Path(__file__).resolve().parents[1]


def filesystem_path(path: Path) -> Path:
    # Python/Win32 long-path support without changing machine policy.
    value = str(path.resolve())
    if os.name == "nt" and not value.startswith("\\\\?\\"):
        value = "\\\\?\\UNC\\" + value[2:] if value.startswith("\\\\") else "\\\\?\\" + value
    return Path(value)


def digest(path: Path) -> str:
    return hashlib.sha256(filesystem_path(path).read_bytes()).hexdigest()


def write_json(path: Path, value) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def copy_file(source: Path, target: Path) -> None:
    source, target = filesystem_path(source), filesystem_path(target)
    if not source.is_file() or source.is_symlink():
        raise ValueError(f"Missing or linked material: {source.name}")
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(source, target)


def verify_evidence():
    evidence = json.loads((ROOT / "licenses/materials.json").read_text(encoding="utf-8"))
    for name, expected in evidence["files"].items():
        path = ROOT / "licenses" / name
        if not path.is_file() or digest(path) != expected:
            raise ValueError(f"License evidence changed or missing: {name}")
    for name, expected in evidence.get("sourceAssets", {}).items():
        data = (ROOT / name).read_bytes()
        if Path(name).suffix == '.h':
            data = data.replace(b'\r\n', b'\n')
        if hashlib.sha256(data).hexdigest() != expected:
            raise ValueError(f"Reviewed source asset changed: {name}")
    return evidence


def notices_in(root: Path):
    return sorted(p for p in root.rglob("*") if p.is_file()
                  and re.match(r"^(license|licence|copying|notice|third[-_]party[-_]notices)([._-].*)?$",
                               p.name, re.I))


def copy_notices(root: Path, target: Path):
    notices = notices_in(root)
    if not notices:
        raise ValueError(f"No license text in {root.name}")
    for path in notices:
        copy_file(path, target / path.relative_to(root))
    return [p.relative_to(root).as_posix() for p in notices]


def package_at(folders, relative):
    for folder in folders:
        path = Path(folder) / relative
        if path.is_dir():
            return path
    raise ValueError(f"Dependency package unavailable: {relative}")


def collect_windows(stage: Path):
    stage = filesystem_path(stage)
    evidence = verify_evidence()
    if (stage / "licenses").exists():
        raise ValueError("Use a fresh Windows publish directory")
    assets = json.loads((ROOT / "windows-app/AIBotBridge/obj/project.assets.json").read_text(encoding="utf-8-sig"))
    records = []
    for name, library in assets["libraries"].items():
        if library["type"] != "package":
            continue
        package = package_at(assets["packageFolders"], library["path"])
        target = stage / "licenses" / name.replace("/", "-")
        texts = copy_notices(package, target)
        for spec in package.glob("*.nuspec"):
            copy_file(spec, target / spec.name)
        records.append({"package": name, "licenseFiles": texts})

    sdk_versions = {d["version"].strip("[]").split(",")[0].strip()
                    for f in assets["project"]["frameworks"].values()
                    for d in f.get("downloadDependencies", [])
                    if d["name"] == "Microsoft.Windows.SDK.NET.Ref"}
    if sdk_versions != {evidence["windowsSdk"]["version"]}:
        raise ValueError("Windows SDK version changed: review distribution evidence")
    version = next(iter(sdk_versions))
    name = f"Microsoft.Windows.SDK.NET.Ref/{version}"
    package = package_at(assets["packageFolders"], name.lower())
    target = stage / "licenses" / name.replace("/", "-")
    for path in (ROOT / "licenses/windows-sdk").iterdir():
        if path.is_file():
            copy_file(path, target / path.name)
    for path in package.glob("*.nuspec"):
        copy_file(path, target / path.name)
    binaries = {}
    for filename, expected in evidence["windowsSdk"]["binaries"].items():
        shipped, original = stage / filename, package / "lib/net8.0" / filename
        if digest(shipped) != expected or digest(original) != expected:
            raise ValueError(f"Targeting-pack binary changed: {filename}")
        binaries[filename] = expected
    records.append({"package": name, "binaries": binaries,
                    "licenseFiles": [p.name for p in target.iterdir()]})
    for filename in ("LICENSE", "THIRD_PARTY_NOTICES.md"):
        copy_file(ROOT / filename, stage / filename)
    copy_file(ROOT / "docs/DISTRIBUTION_TERMS.md", stage / "DISTRIBUTION_TERMS.md")
    copy_file(ROOT / "docs/WINDOWS_PACKAGE.md", stage / "README.md")
    copy_file(ROOT / "licenses/materials.json", stage / "licenses/materials.json")
    for name in ("DISTRIBUTION_TERMS.md", "FIRMWARE_PACKAGE.md"):
        copy_file(ROOT / "docs" / name, stage / "docs" / name)
    write_json(stage / "DEPENDENCIES.json", {"scope": "restored Windows publish dependencies",
                                             "packages": records})
    print(f"WINDOWS_DISTRIBUTION_MATERIALS_OK packages={len(records)}")


def copy_tree(source: Path, target: Path, dependency=False):
    source, target = filesystem_path(source), filesystem_path(target)
    for path in sorted(source.rglob("*")):
        relative = path.relative_to(source)
        if any(p in {".git", ".pio", "__pycache__", ".piopm"} for p in relative.parts):
            continue
        if dependency and (any(p.lower() in {"examples", "examplesv1", ".github", "tests", "test"}
                               for p in relative.parts)
                           or relative.as_posix().startswith(("tools/esptool/", "tools/pyserial/"))):
            continue
        if path.is_symlink():
            raise ValueError(f"Review symlink before distribution: {relative}")
        if path.is_file():
            copy_file(path, target / relative)


def manifest(stage: Path):
    lines = [f"{digest(p)}  {p.relative_to(stage).as_posix()}" for p in sorted(stage.rglob("*"))
             if p.is_file() and p.name != "FILES.sha256"]
    (stage / "FILES.sha256").write_text("\n".join(lines) + "\n", encoding="ascii")


def collect_firmware(stage: Path, core: Path):
    stage = filesystem_path(stage)
    evidence = verify_evidence()
    if stage.exists():
        raise ValueError("Use a new firmware package directory")
    firmware = ROOT / "firmware"
    binary = firmware / ".pio/build/nodemcuv2/firmware.bin"
    if not binary.is_file():
        raise ValueError("Build firmware from this source snapshot before packaging")
    stage.mkdir(parents=True)
    for filename in ("LICENSE", "THIRD_PARTY_NOTICES.md", "PROVENANCE.md", "VERSION"):
        copy_file(ROOT / filename, stage / filename)
    copy_file(ROOT / "docs/FIRMWARE_PACKAGE.md", stage / "README.md")
    copy_file(ROOT / "docs/DISTRIBUTION_TERMS.md", stage / "DISTRIBUTION_TERMS.md")
    for name in ("DISTRIBUTION_TERMS.md", "FIRMWARE_PACKAGE.md"):
        copy_file(ROOT / "docs" / name, stage / "docs" / name)
    copy_file(binary, stage / "firmware.bin")
    copy_tree(firmware, stage / "source/firmware")
    packages = {"framework-arduinoespressif8266": core / "packages/framework-arduinoespressif8266",
                "espressif8266": core / "platforms/espressif8266"}
    packages.update({name: firmware / ".pio/libdeps/nodemcuv2" / name
                     for name in ("TFT_eSPI", "ArduinoJson", "WiFiManager")})
    records = []
    for name, path in packages.items():
        meta = next((p for p in (path / "package.json", path / "platform.json", path / "library.json") if p.is_file()), None)
        if meta is None:
            raise ValueError(f"Missing dependency metadata: {name}")
        version = json.loads(meta.read_text(encoding="utf-8"))["version"]
        if version != evidence["firmware"][name]:
            raise ValueError(f"Review new firmware dependency: {name}/{version}")
        texts = copy_notices(path, stage / "licenses" / name)
        copy_file(meta, stage / "licenses" / name / meta.name)
        # Include the actual application, core and libraries for modifications and relinking.
        copy_tree(path, stage / "source/vendor" / name, dependency=True)
        records.append({"package": name, "version": version, "licenseFiles": texts})
    framework = packages["framework-arduinoespressif8266"]
    # lwIP keeps its BSD notice in headers rather than a standalone license file.
    copy_file(framework / "tools/sdk/lwip2/include/lwip/init.h", stage / "licenses/lwip2/init.h")
    copy_file(framework / "README.md", stage / "licenses/framework-arduinoespressif8266/README.md")
    toolchain = json.loads((core / "packages/toolchain-xtensa/package.json").read_text())
    if toolchain["version"] != evidence["firmware"]["toolchain-xtensa"]:
        raise ValueError("Review new firmware compiler runtime notices")
    copy_tree(ROOT / "licenses/firmware-toolchain", stage / "licenses/compiler-runtime")
    copy_file(ROOT / "licenses/materials.json", stage / "licenses/materials.json")
    records.append({"package": "toolchain-xtensa", "version": toolchain["version"],
                    "hostCompilerBundled": False, "runtimeNotices": "licenses/compiler-runtime"})
    config = configparser.ConfigParser(interpolation=None)
    config.read(firmware / "platformio.ini", encoding="utf-8")
    config["env:nodemcuv2"]["platform_packages"] = "framework-arduinoespressif8266 @ symlink://../vendor/framework-arduinoespressif8266"
    config["env:nodemcuv2"]["lib_deps"] = "\n" + "\n".join(
        f"symlink://../vendor/{name}" for name in ("TFT_eSPI", "ArduinoJson", "WiFiManager"))
    with (stage / "source/firmware/rebuild.ini").open("w", encoding="utf-8") as output:
        config.write(output)
    write_json(stage / "DEPENDENCIES.json", {"scope": "firmware and source materials; compiler installed separately",
                                             "packages": records})
    manifest(stage)
    # Stage names contain SemVer dots; with_suffix would truncate the version.
    archive = stage.parent / (stage.name + ".zip")
    if archive.exists():
        raise ValueError("Archive already exists")
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as output:
        for path in sorted(stage.rglob("*")):
            if path.is_file():
                output.write(path, path.relative_to(stage).as_posix())
    with zipfile.ZipFile(archive) as check:
        for line in (stage / "FILES.sha256").read_text().splitlines():
            expected, name = line.split("  ", 1)
            if hashlib.sha256(check.read(name)).hexdigest() != expected:
                raise ValueError(f"Firmware archive mismatch: {name}")
    archive.with_suffix(".zip.sha256").write_text(f"{digest(archive)}  {archive.name}\n", encoding="ascii")
    print(f"FIRMWARE_DISTRIBUTION_MATERIALS_OK zip={archive}")


def archive_source(archive: Path):
    from check_public_content import findings

    if archive.exists():
        raise ValueError("Source archive already exists")
    inventory = ROOT / "SOURCE_FILES.sha256"
    entries = []
    for line in inventory.read_text(encoding="utf-8-sig").splitlines():
        expected, name = line.split("  ", 1)
        path = (ROOT / name).resolve()
        if (Path(name).is_absolute() or '..' in Path(name).parts
                or not path.is_relative_to(ROOT.resolve()) or (ROOT / name).is_symlink()):
            raise ValueError("Source path escaped snapshot")
        data = path.read_bytes()
        if hashlib.sha256(data).hexdigest() != expected:
            raise ValueError(f"Source changed since verification: {name}")
        if list(findings(name, data)):
            raise ValueError(f"Source archive contains disallowed material: {name}")
        entries.append((name, data))
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as output:
        for name, data in entries:
            output.writestr(name, data)
        output.write(inventory, "SOURCE_FILES.sha256")
    with zipfile.ZipFile(archive) as check:
        for name, data in entries:
            if check.read(name) != data:
                raise ValueError(f"Source archive mismatch: {name}")
    archive.with_suffix(".zip.sha256").write_text(f"{digest(archive)}  {archive.name}\n", encoding="ascii")
    print(f"SOURCE_ARCHIVE_OK files={len(entries)} zip={archive}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("kind", choices=("windows", "firmware", "source", "verify"))
    parser.add_argument("--stage", type=Path)
    parser.add_argument("--platformio-core", type=Path,
                        default=Path(os.environ.get("PLATFORMIO_CORE_DIR", Path.home() / ".platformio")))
    args = parser.parse_args()
    if args.kind == "verify":
        verify_evidence()
        print("DISTRIBUTION_EVIDENCE_OK")
    elif args.stage is None:
        parser.error("--stage is required")
    elif args.kind == "windows":
        collect_windows(args.stage.resolve())
    elif args.kind == "source":
        archive_source(args.stage.resolve())
    else:
        collect_firmware(args.stage.resolve(), args.platformio_core.resolve())


if __name__ == "__main__":
    main()
