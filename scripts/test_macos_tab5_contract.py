"""Windows-host checks; these do NOT claim a Swift build or Mac hardware pass."""
import argparse
import importlib.util
import json
import struct
import subprocess
import sys
from pathlib import Path

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

ROOT = Path(__file__).resolve().parents[1]
FIXTURE = ROOT / "mac-app/Tests/AIBotBridgeTests/Fixtures/tab5-v1.json"


def run(command):
    result = subprocess.run([str(p) for p in command], cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(f"command failed ({result.returncode}):\n{result.stdout}\n{result.stderr}")
    return result.stdout


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-dll", type=Path, required=True)
    parser.add_argument("--compiler", type=Path, required=True, help="Local tcc.exe or zig.exe; no global install required")
    parser.add_argument("--firmware-main", type=Path, required=True)
    parser.add_argument("--python-deps", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/mac-tab5-20261019/checks")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    sys.path.insert(0, str(args.python_deps))
    from tree_sitter import Language, Parser
    import tree_sitter_swift
    swift_parser = Parser(Language(tree_sitter_swift.language()))
    sources = list((ROOT / "mac-app").rglob("*.swift"))
    errors = [str(path.relative_to(ROOT)) for path in sources if swift_parser.parse(path.read_bytes()).root_node.has_error]
    assert not errors, errors
    print(f"SWIFT_SYNTAX_OK files={len(sources)} (syntax only; not type checking)")

    spec = importlib.util.spec_from_file_location("fixture_generator", ROOT / "scripts/mac_tab5_interop/generate_fixture.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    fixture = json.loads(FIXTURE.read_text(encoding="utf-8"))
    assert fixture == module.generate(), "synthetic vectors drifted"
    reference = run(["dotnet", "run", "--project", ROOT / "scripts/mac_tab5_interop/Reference.csproj", "-c", "Release", "--",
                     args.reference_dll.resolve(), FIXTURE])
    result = json.loads(next(line for line in reference.splitlines() if line.startswith("{")))
    packet = bytes.fromhex(result.pop("responsePacketHex"))
    clear = AESGCM(bytes.fromhex(fixture["keyHex"])).decrypt(packet[:12], packet[28:] + packet[12:28], fixture["challenge"].encode())
    assert clear == bytes.fromhex(fixture["clearHex"])
    print("WINDOWS_PYTHON_AES_HMAC_DISCOVERY_SETTINGS_OK")

    # Exercise the real firmware mailbox at short, multi-fragment and phase-one
    # maximum request sizes, checking IDs, offsets, lengths and final bytes.
    executable = args.output / "mailbox-reference.exe"
    compiler = args.compiler.resolve()
    compile_command = [compiler] + (["cc"] if compiler.stem == "zig" else [])
    run(compile_command + ["-I", args.firmware_main.resolve(), ROOT / "scripts/mac_tab5_interop/mailbox_reference.c", "-o", executable.resolve()])
    tests = []
    for size in [124, 2048, 2049, 5000, 12384, 49152, 65535]:
        request = bytes(i % 251 for i in range(size))
        response = bytes((i + 31) % 251 for i in range(max(28, size - 96)))
        incoming = [struct.pack("<IHH", 0x12345678, offset, size) + request[offset:offset + 2048] for offset in range(0, size, 2048)]
        outgoing = [struct.pack("<BIHH", 2, 0x12345678, offset, len(response)) + response[offset:offset + 2048] for offset in range(0, len(response), 2048)]
        encoded = struct.pack("<II", size, len(response)) + request + response
        for parts in [incoming, outgoing]:
            encoded += struct.pack("<I", len(parts))
            for part in parts:
                encoded += struct.pack("<I", len(part)) + part
        vector = args.output / f"mailbox-{size}.bin"
        vector.write_bytes(encoded)
        tests.append(run([executable.resolve(), vector.resolve()]).strip())
    print(f"FIRMWARE_MAILBOX_INTEROP_OK scenarios={len(tests)}")
    report = dict(swiftSyntaxFiles=len(sources), windowsReference=result, firmwareMailbox=tests,
                  swiftTests="NOT RUN: macOS required", macReleaseBuild="NOT RUN: macOS required",
                  nativeUI="NOT RUN: macOS required", deviceAcceptance="NOT RUN: no deployment authorized")
    (args.output / "results.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
