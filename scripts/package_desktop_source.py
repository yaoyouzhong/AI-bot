"""Prepare the verified desktop source archive for a Mac-only release."""
import argparse
import hashlib
from pathlib import Path
import re
import shutil
import subprocess
import tempfile

import collect_distribution_materials as materials
from artifact_names import name
from component_versions import versions

ROOT = Path(__file__).resolve().parents[1]
EXCLUDED = re.compile(
    r"^(?:windows-app/AIBotBridge/Assets/DailyArt/|docs/art-guides/|"
    r"docs/development/(?:CALLIGRAPHY-ORIGINALS|ART-LOOKING-AUDIT|TAB5-CALLIGRAPHY-ORIGINALS)|"
    r"docs/TAB5-(?:CALLIGRAPHY|PAINTING|CHINESE-ART)-|"
    r"scripts/(?:prepare_daily_art|verify_daily_art_year)\.py$)"
)


def package(output: Path):
    output = output.resolve()
    if output.exists():
        raise ValueError("Use a new source output directory")
    subprocess.run(["python", str(ROOT / "scripts/check_public_content.py")], cwd=ROOT, check=True)
    materials.verify_evidence()
    files = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode("utf-8").split("\0")
    with tempfile.TemporaryDirectory(prefix="aibot-source-") as temp:
        stage = Path(temp)
        lines = []
        for relative in sorted(p for p in files if p and not EXCLUDED.match(p)):
            source = ROOT / relative
            if source.is_symlink() or not source.is_file():
                raise ValueError(f"Missing or linked source: {relative}")
            target = stage / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            lines.append(f"{hashlib.sha256(target.read_bytes()).hexdigest()}  {relative}")
        (stage / "SOURCE_FILES.sha256").write_text("\n".join(lines) + "\n", encoding="utf-8")
        output.mkdir(parents=True)
        materials.ROOT = stage
        materials.archive_source(output / name("bridge-source", versions(ROOT)["bridge"]))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    package(parser.parse_args().output)
