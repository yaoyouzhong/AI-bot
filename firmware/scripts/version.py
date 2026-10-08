"""Embed the component version from firmware/VERSION in every build."""
from pathlib import Path
import re

Import("env")
version = (Path(env.subst("$PROJECT_DIR")) / "VERSION").read_text(encoding="utf-8").strip()
if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", version):
    raise ValueError("Invalid firmware/VERSION")
env.Append(CPPDEFINES=[("AIBOT_FIRMWARE_VERSION", env.StringifyMacro(version))])
