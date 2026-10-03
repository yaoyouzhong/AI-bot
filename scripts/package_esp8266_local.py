"""Build and package ESP8266 alone from an isolated, guarded source snapshot."""
import argparse
import hashlib
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

from component_versions import ROOT, versions
from check_public_content import findings


def run(command, cwd):
    result = subprocess.run(command, cwd=cwd, capture_output=True, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    if cwd != ROOT:
        log = cwd / f"build-check-{len(list(cwd.glob('build-check-*.log')))+1}.log"
        log.write_bytes(result.stdout+result.stderr)
    output = (result.stdout+result.stderr).decode("utf-8", errors="replace")
    print(output[-6000:] if result.returncode else "\n".join(output.splitlines()[-8:]), flush=True)
    result.check_returncode()


def package(output):
    if output.exists():
        raise ValueError("Use a new output directory")
    run([sys.executable, "scripts/check_version.py", "--component", "esp8266"], ROOT)
    paths = subprocess.check_output(["git", "ls-files", "-z", "--cached", "--others", "--exclude-standard"],
                                   cwd=ROOT, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0)).decode().split("\0")
    artifacts = ROOT / "artifacts"
    artifacts.mkdir(exist_ok=True)
    audit = Path(tempfile.mkdtemp(prefix="esp8266-check-", dir=artifacts))
    inventory = []
    for name in sorted(set(paths)-{""}):
        source = ROOT / name
        if not source.is_file() or source.is_symlink() or not source.resolve().is_relative_to(ROOT.resolve()):
            raise ValueError(f"Invalid source file: {name}")
        data = source.read_bytes()
        if list(findings(name, data)):
            raise ValueError(f"Disallowed source material: {name}")
        destination = audit / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(data)
        inventory.append(f"{hashlib.sha256(data).hexdigest()}  {name}")
    (audit / "SOURCE_FILES.sha256").write_text("\n".join(inventory)+"\n", encoding="utf-8")
    run([sys.executable, "-m", "platformio", "run", "-d", "firmware"], audit)
    version = versions(audit)["esp8266"]
    stage = audit / f"AI-bot-{version}-firmware-materials"
    run([sys.executable, "scripts/collect_distribution_materials.py", "firmware", "--stage", str(stage)], audit)
    output.mkdir(parents=True)
    for suffix in (".zip", ".zip.sha256"):
        source = audit / (stage.name+suffix)
        shutil.copyfile(source, output / source.name)
    print(f"ESP8266_PACKAGE_OK version={version} output={output} evidence={audit}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    package(parser.parse_args().output.resolve())
