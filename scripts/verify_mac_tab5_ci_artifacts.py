"""Inspect downloaded Mac CI artifacts without executing or extracting binaries."""
import argparse
import hashlib
import io
import json
import plistlib
import struct
import tarfile
import xml.etree.ElementTree as ET
from pathlib import Path, PurePosixPath


def require(condition, message):
    if not condition:
        raise ValueError(message)


def member(archive, name):
    matches = [item for item in archive.getmembers() if item.name.removeprefix("./") == name]
    require(len(matches) == 1 and matches[0].isfile() and matches[0].size < 100_000_000, "Missing/ambiguous archive member: " + name)
    return archive.extractfile(matches[0]).read()


def macho(data):
    require(len(data) >= 32, "Truncated Mach-O")
    magic, cpu, _, kind = struct.unpack_from("<IIII", data)
    require((magic, cpu, kind) == (0xFEEDFACF, 0x0100000C, 2), "Expected arm64 Mach-O executable")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--sha", required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    require(len(args.sha) == 40 and all(c in "0123456789abcdef" for c in args.sha), "Invalid source SHA")
    root = args.directory
    paths = {}
    for path in root.rglob("*"):
        if path.is_file():
            require(path.name not in paths, "Use one artifact directory, not duplicate report/build copies")
            paths[path.name] = path
    for name in ["environment.txt", "test-results.xml", "tests.log", "release.log", "bundle.log", "flash-tool-check.log", "ui-check.log", "BUILD_SHA256SUMS.txt"]:
        require(name in paths, "Missing report: " + name)
    require(paths["environment.txt"].read_text(encoding="utf-8").splitlines()[0] == args.sha, "Wrong environment source")
    tests = ET.parse(paths["test-results.xml"])
    cases = tests.findall(".//testcase")
    require(len(cases) >= 105 and not tests.findall(".//failure") and not tests.findall(".//error") and not tests.findall(".//skipped"), "Incomplete/failed/skipped test suite")
    require(any("CodexRPC" in (case.get("classname", "") + case.get("name", "")) for case in cases), "Real pipe regression missing")
    require("MAC_FLASH_BUNDLE_OK" in paths["flash-tool-check.log"].read_text(encoding="utf-8"), "Tool smoke check missing")
    require("MAC_TAB5_NATIVE_UI_RENDER_OK" in paths["ui-check.log"].read_text(encoding="utf-8"), "Native UI check missing")
    for name in ["connection.png", "connection-full.png", "features.png", "features-full.png", "resources.png", "resources-full.png", "first-install.png", "display-settings.png"]:
        data = paths[name].read_bytes()
        require(data[:8] == b"\x89PNG\r\n\x1a\n" and len(data) > 1000, "Invalid native PNG: " + name)
        width, height = struct.unpack_from(">II", data, 16)
        require(width >= 100 and height >= 100, "Empty native view: " + name)
    for line in paths["BUILD_SHA256SUMS.txt"].read_text(encoding="utf-8").splitlines():
        digest, name = line.split("  ", 1)
        require(Path(name).name == name and hashlib.sha256(paths[name].read_bytes()).hexdigest() == digest, "Outer build checksum mismatch")
    executable = None
    for name in ["mac-tab5-release-build.tar.gz", "mac-tab5-candidate-app.tar.gz"]:
        with tarfile.open(fileobj=io.BytesIO(paths[name].read_bytes())) as archive:
            for item in archive.getmembers():
                path = PurePosixPath(item.name)
                require(not path.is_absolute() and ".." not in path.parts and not item.isdev(), "Unsafe candidate entry")
            prefix = "AIBotBridge.app/Contents/Resources/" if "candidate" in name else ""
            require(member(archive, prefix + "SOURCE_SHA.txt").decode().strip() == args.sha, "Wrong candidate source")
            require(member(archive, prefix + "LICENSE") and member(archive, prefix + "THIRD_PARTY_NOTICES.md"), "License material missing")
            binary = member(archive, "AIBotBridge.app/Contents/MacOS/AIBotBridge" if prefix else "AIBotBridge")
            macho(binary)
            if executable is not None:
                # Ad-hoc app signing may change its embedded Mach-O signature.
                require(len(binary) >= 100_000, "Truncated signed app")
            else:
                executable = hashlib.sha256(binary).hexdigest()
                for line in member(archive, "SHA256SUMS.txt").decode().splitlines():
                    digest, file = line.split("  ", 1)
                    require(hashlib.sha256(member(archive, file)).hexdigest() == digest, "Inner binary checksum mismatch")
            if prefix:
                require(member(archive, prefix + "MAC-TAB5-CANDIDATE.md"), "Offline candidate guide missing")
                info = plistlib.loads(member(archive, "AIBotBridge.app/Contents/Info.plist"))
                require(info["LSMinimumSystemVersion"] == "13.0", "Wrong deployment target")
                for key in ["NSBluetoothAlwaysUsageDescription", "NSAppleEventsUsageDescription", "NSMicrophoneUsageDescription", "NSSpeechRecognitionUsageDescription"]:
                    require(info.get(key), "Missing permission description: " + key)
    result = {"source": args.sha, "tests": len(cases), "failures": 0, "skipped": 0, "nativeUI": "fixture render verified", "release": "arm64 executable and app verified", "hardware": "not performed", "deployment": "not performed"}
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
