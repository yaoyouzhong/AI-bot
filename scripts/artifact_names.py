"""Canonical distribution names and read compatibility with published assets."""
import argparse
from pathlib import Path
import re

TEMPLATES = {
    "windows": ("AI-bot-bridge-v{version}-windows-x64-setup.exe", "AIBotBridge-{version}-setup-win-x64.exe"),
    "windows-zip": ("AI-bot-bridge-v{version}-windows-x64.zip", "AIBotBridge-{version}-local-candidate-win-x64.zip"),
    "macos": ("AI-bot-bridge-v{version}-macos-arm64.zip", "AIBotBridge-{version}-local-candidate-macos-arm64.zip"),
    "bridge-source": ("AI-bot-bridge-v{version}-source.zip", "AI-bot-{version}-source.zip"),
    "esp8266": ("AI-bot-esp8266-{version}-firmware.zip", "AI-bot-{version}-firmware-materials.zip"),
    "tab5-first": ("AI-bot-tab5-{version}-first-install.zip", "TAB5-first-install-{version}.zip"),
    "tab5-upgrade": ("AI-bot-tab5-{version}-upgrade.zip", "TAB5-upgrade-{version}.zip"),
    "tab5-source": ("AI-bot-tab5-{version}-source.zip", "TAB5-{version}-source.zip"),
    "painting": ("AI-bot-gallery-painting-standard-{version}.zip", "AI-bot-DailyPainting-Curated-{version}.zip", "AI-bot-DailyPainting-{version}.zip"),
    "calligraphy": ("AI-bot-gallery-calligraphy-standard-{version}.zip", "AI-bot-DailyCalligraphy-Curated-{version}.zip", "AI-bot-DailyCalligraphy-{version}.zip"),
    "painting-hd": ("AI-bot-gallery-painting-hd-{version}.zip", "AI-bot-Gallery-HD-Painting-{version}.zip"),
    "calligraphy-hd": ("AI-bot-gallery-calligraphy-hd-{version}.zip", "AI-bot-Gallery-HD-Calligraphy-{version}.zip"),
}
COMPONENT_KEYS = {"bridge": ("windows", "windows-zip", "macos", "bridge-source"),
                  "esp8266": ("esp8266",), "tab5": ("tab5-first", "tab5-upgrade")}
# These aliases keep existing installed bridges able to discover the next update.
UPDATE_ALIASES = {"windows", "esp8266", "tab5-upgrade"}


def names(key, version):
    pattern = r"\d{4}\.\d{2}\.\d{2}(?:\.\d+)?" if key.startswith(("painting", "calligraphy")) else r"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?"
    if not re.fullmatch(pattern, version):
        raise ValueError("Invalid artifact version")
    return tuple(template.format(version=version) for template in TEMPLATES[key])


def name(key, version):
    return names(key, version)[0]


def match(key, filename):
    for priority, template in enumerate(TEMPLATES[key]):
        pattern = re.escape(template).replace(re.escape("{version}"), r"(?P<version>\d+(?:\.\d+){2,3}(?:-[0-9A-Za-z.-]+)?)")
        found = re.fullmatch(pattern, filename)
        if found:
            try:
                if filename in names(key, found["version"]):
                    return found["version"], priority
            except ValueError:
                continue
    return None


def resolve(directory: Path, key, version):
    found = [candidate for candidate in names(key, version) if (directory / candidate).is_file()]
    if len(found) != 1:
        raise ValueError(f"Expected one {key} package; found {len(found)}")
    return found[0]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("key", choices=TEMPLATES)
    parser.add_argument("version")
    args = parser.parse_args()
    print(name(args.key, args.version))
