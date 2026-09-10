"""Small release-boundary guard; not a complete secret scanner or license audit.

Examines tracked and non-ignored untracked files. Reports locations, never values.
"""
from pathlib import Path
import re
import hashlib
import json
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
RULES = {
    "private-key": re.compile(rb"-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----"),
    "github-token": re.compile(rb"\b(?:gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{60,})\b"),
    "provider-key": re.compile(rb"\bsk-(?:proj-|ant-)?[A-Za-z0-9_-]{40,}\b"),
}
# Individually reviewed documentation captures. Path AND bytes must match the record.
REVIEWED_DOC_NAMES = frozenset({
    'activity.png',
    'authorization-empty.png',
    'claude.png',
    'codex.png',
    'completed.png',
    'cycle-settings.png',
    'device-control.png',
    'domestic_alibaba.png',
    'domestic_deepseek.png',
    'domestic_kimi.png',
    'domestic_minimax.png',
    'domestic_zhipu.png',
    'dual.png',
    'mirror-window.png',
    'music.png',
    'needs-input.png',
    'pet-gallery-empty.png',
    'pet.png',
    'quota-trend-empty.png',
    'screensaver.png',
    'settings.png',
    'stocks.png',
    'system.png',
    'tray-menu.png',
    'weather-settings.png',
    'weather.png',
})
DOC_ASSET_HASHES = json.loads((ROOT / "licenses/materials.json").read_text(encoding="utf-8")).get("sourceAssets", {})


def reviewed_doc_image(path, data):
    return (path.startswith("docs/assets/screens/")
            and path.removeprefix("docs/assets/screens/") in REVIEWED_DOC_NAMES
            and hashlib.sha256(data).hexdigest() == DOC_ASSET_HASHES.get(path))


PRIVATE_NAMES = {
    "settings.json", "auth.json", ".credentials.json", "pairing.dat",
    "usage-cache.json", "domestic-quota-cache.json", "domestic-provider-cache.json",
    "weather-cache.json", "weather-provider-cache.json", "stock-cache.json", "codex-quota-history.json",
}


def findings(path, data):
    normalized = path.replace('\\', '/').lower()
    name = Path(normalized).name
    if any(part in {'artifacts', '.pio', 'bin', 'obj', 'quota-auth-profile', 'page-logos', '__pycache__'}
           for part in normalized.split('/')):
        yield "private-or-generated-directory", 0
    if (Path(name).suffix in {'.exe', '.dll', '.bin', '.elf', '.o', '.a', '.pdb', '.zip',
                             '.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.ico'}
            and normalized != 'windows-app/aibotbridge/assets/app-icon.ico'
            and not reviewed_doc_image(normalized, data)):
        yield "unreviewed-binary-or-artwork", 0
    if (name in PRIVATE_NAMES or name == ".env" or name.startswith(".env.")
            and name not in {".env.example", ".env.sample"}
            or name.endswith((".apet", ".pfx", ".p12", ".key"))):
        yield "private-runtime-path", 0
    for rule, pattern in RULES.items():
        for match in pattern.finditer(data):
            yield rule, data[:match.start()].count(b"\n") + 1


def main():
    if "--self-test" in sys.argv:
        assert list(findings("auth.json", b"{}"))
        assert list(findings("source.cs", b"ghp_" + b"x" * 36))
        assert list(findings("source.cs", b"sk-proj-" + b"x" * 45))
        assert not list(findings(".env.example", b"KEY=placeholder"))
        assert list(findings("artifacts/private.txt", b"data"))
        assert list(findings("docs/screen.png", b"image"))
        assert not list(findings("windows-app/AIBotBridge/Assets/app-icon.ico", b"icon"))
        assert list(findings("docs/assets/screens/unreviewed.png", b"image"))
        assert list(findings("docs/assets/screens/codex.png", b"changed image"))
        known = "docs/assets/screens/codex.png"
        assert not list(findings(known, (ROOT / known).read_bytes()))
        print("PUBLIC_CONTENT_GUARD_SELF_TEST_OK")
        return 0
    if "--history" in sys.argv:
        git = ["git", "-c", f"safe.directory={ROOT.as_posix()}"]
        objects = subprocess.check_output(git + ["rev-list", "--objects", "--all"], cwd=ROOT).splitlines()
        failed = checked = 0
        for row in objects:
            parts = row.decode("utf-8").split(" ", 1)
            if len(parts) != 2:
                continue
            oid, path = parts
            if subprocess.check_output(git + ["cat-file", "-t", oid], cwd=ROOT).strip() != b"blob":
                continue
            data = subprocess.check_output(git + ["cat-file", "blob", oid], cwd=ROOT)
            checked += 1
            for rule, line in findings(path, data):
                print(f"{oid[:12]}:{path}:{line}: {rule}")
                failed += 1
        print(f"PUBLIC_HISTORY_GUARD blobs={checked} findings={failed}; locally reachable refs, limited patterns")
        return int(failed != 0)
    result = subprocess.run(["git", "-c", f"safe.directory={ROOT.as_posix()}",
                             "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                            cwd=ROOT, check=True, stdout=subprocess.PIPE)
    failed = 0
    paths = sorted(set(result.stdout.decode("utf-8").split("\0")) - {""})
    for path in paths:
        source = ROOT / path
        if source.is_symlink():
            print(f"{path}:0: symlink-requires-review")
            failed += 1
        elif source.is_file():
            for rule, line in findings(path, source.read_bytes()):
                print(f"{path}:{line}: {rule}")
                failed += 1
    print(f"PUBLIC_CONTENT_GUARD files={len(paths)} findings={failed}; current files only, limited patterns")
    return int(failed != 0)


if __name__ == "__main__":
    sys.exit(main())
