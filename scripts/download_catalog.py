"""Maintain the complete public download index; never publishes or uploads files."""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path
import re
import urllib.request

from component_versions import release_target

ROOT = Path(__file__).resolve().parents[1]
REPO = "https://github.com/yaoyouzhong/AI-bot"
API = "https://api.github.com/repos/yaoyouzhong/AI-bot"
CATALOG = ROOT / "download-catalog.json"
CENTER = REPO + "/blob/main/DOWNLOADS.md"
# id: component, asset template, Chinese label, English label
SPECS = {
    "windows": ("bridge", "AIBotBridge-{version}-setup-win-x64.exe", "Windows 桥接 · Windows 10/11 x64", "Windows bridge · Windows 10/11 x64"),
    "macos": ("bridge", "AIBotBridge-{version}-local-candidate-macos-arm64.zip", "Mac 桥接 · macOS 13+ / Apple Silicon", "Mac bridge · macOS 13+ / Apple Silicon"),
    "esp8266": ("esp8266", "AI-bot-{version}-firmware-materials.zip", "ESP8266 小屏 · 首刷／升级通用", "ESP8266 display · first install / upgrade"),
    "tab5-first": ("tab5", "TAB5-first-install-{version}.zip", "TAB5 · 出厂系统首次安装", "TAB5 · first install from factory system"),
    "tab5-upgrade": ("tab5", "TAB5-upgrade-{version}.zip", "TAB5 · 已有 AI-bot 升级", "TAB5 · upgrade existing AI-bot"),
    "calligraphy": ("gallery", "AI-bot-DailyCalligraphy-{version}.zip", "每日书法 · 可选图库", "Daily Calligraphy · optional collection"),
    "painting": ("gallery", "AI-bot-DailyPainting-{version}.zip", "每日名画 · 可选图库", "Daily Painting · optional collection"),
}
START, END = "<!-- downloads:start -->", "<!-- downloads:end -->"


def request(url, method="GET"):
    req = urllib.request.Request(url, method=method, headers={"User-Agent": "AI-bot-download-check"})
    return urllib.request.urlopen(req, timeout=45)


def asset_url(item, filename=None):
    return f"{REPO}/releases/download/{item['tag']}/{filename or item['file']}"


def validate(data):
    if data.get("schemaVersion") != 1 or set(data.get("downloads", {})) != set(SPECS):
        raise ValueError("Download index must contain all seven supported downloads")
    entries = data["downloads"]
    for key, (component, template, _, _) in SPECS.items():
        item = entries[key]
        version = item["version"]
        pattern = r"\d{4}\.\d{2}\.\d{2}" if component == "gallery" else r"\d+\.\d+\.\d+" + (r"-ui" if component == "tab5" else "")
        if not re.fullmatch(pattern, version) or item["file"] != template.format(version=version):
            raise ValueError(f"Wrong version/package for {key}")
        tag_component, tag_version = release_target(item["tag"])
        if component != "gallery" and tag_component != "bundle" and (tag_component, tag_version) != (component, version):
            raise ValueError(f"Tag and package disagree: {key}")
        if component in ("bridge", "esp8266") and tag_component == "bundle" and tag_version != version:
            raise ValueError(f"Legacy tag and package disagree: {key}")
        if not re.fullmatch(r"[0-9a-f]{64}", item["sha256"]) or not isinstance(item["size"], int) or item["size"] <= 0:
            raise ValueError(f"Missing size or SHA-256: {key}")
    for left, right in (("windows", "macos"), ("tab5-first", "tab5-upgrade")):
        if any(entries[left][field] != entries[right][field] for field in ("tag", "version")):
            raise ValueError(f"Incomplete paired release: {left}, {right}")
    return data


def load():
    return validate(json.loads(CATALOG.read_text(encoding="utf-8")))


def public_releases():
    result = []
    for page in range(1, 101):
        with request(f"{API}/releases?per_page=100&page={page}") as response:
            batch = json.load(response)
        result.extend(r for r in batch if not r["draft"] and not r["prerelease"])
        if len(batch) < 100:
            return result
    raise ValueError("Release pagination limit reached")


def discover(releases):
    entries = {}
    for key, (component, template, _, _) in SPECS.items():
        pattern = re.escape(template).replace(re.escape("{version}"), r"(?P<version>\d+\.\d+\.\d+(?:-ui)?)")
        candidates = []
        for release in releases:
            if release.get("draft") or release.get("prerelease"):
                continue
            try:
                tag_component, tag_version = release_target(release["tag_name"])
            except ValueError:
                continue
            for asset in release["assets"]:
                match = re.fullmatch(pattern, asset["name"])
                if not match:
                    continue
                version = match["version"]
                if component != "gallery" and tag_component != "bundle" and (tag_component, tag_version) != (component, version):
                    continue
                if component in ("bridge", "esp8266") and tag_component == "bundle" and tag_version != version:
                    continue
                order = tuple(map(int, version.removesuffix("-ui").split(".")))
                candidates.append((order, release["published_at"], release, asset, version))
        if not candidates:
            raise ValueError(f"No published package for {key}")
        _, _, release, asset, version = max(candidates, key=lambda c: c[:2])
        if "SHA256SUMS.txt" not in {a["name"] for a in release["assets"]}:
            raise ValueError(f"Missing checksum file for {key}")
        entries[key] = {"version": version, "tag": release["tag_name"], "file": asset["name"],
                        "size": asset["size"], "sha256": (asset.get("digest") or "").removeprefix("sha256:")}
    return validate({"schemaVersion": 1, "downloads": entries})


def verify_online(data):
    latest = discover(public_releases())
    if latest != data:
        raise ValueError("Public downloads changed; run refresh and review the diff")
    sums = {}
    for key, item in data["downloads"].items():
        tag = item["tag"]
        if tag not in sums:
            with request(asset_url(item, "SHA256SUMS.txt")) as response:
                lines = response.read().decode("utf-8-sig").splitlines()
            sums[tag] = dict((m[2], m[1].lower()) for line in lines
                             if (m := re.fullmatch(r"([a-fA-F0-9]{64})\s+\*?(.+)", line)))
        if sums[tag].get(item["file"]) != item["sha256"]:
            raise ValueError(f"Published checksum mismatch: {key}")
        with request(asset_url(item), "HEAD") as response:
            if response.status != 200:
                raise ValueError(f"Download unavailable: {key}")
            length = response.headers.get("Content-Length")
            if length and int(length) != item["size"]:
                raise ValueError(f"Download size changed: {key}")
        print(f"OK {key}: public asset, checksum and download URL")


def table(data, language="zh", tag=None):
    entries = copy.deepcopy(data["downloads"])
    changed = set()
    if tag:
        component, version = release_target(tag)
        for key, (kind, template, _, _) in SPECS.items():
            if kind == component or component == "bundle" and kind in ("bridge", "esp8266"):
                entries[key] = {"tag": tag, "version": version, "file": template.format(version=version)}
                changed.add(key)
    zh = language == "zh"
    lines = ["| 用途 | 版本 | 下载 | SHA-256 |" if zh else "| Use | Version | Download | SHA-256 |", "| --- | --- | --- | --- |"]
    for key, spec in SPECS.items():
        item = entries[key]
        version = item["version"] + ((" · 本次发布" if zh else " · this release") if key in changed else "")
        label = "下载" if zh else "Download"
        if "size" in item:
            label += f" · {item['size'] / 1048576:.0f} MiB"
        lines.append(f"| {spec[2 if zh else 3]} | {version} | [{label}]({asset_url(item)}) | [SHA-256]({asset_url(item, 'SHA256SUMS.txt')}) |")
    return "\n".join(lines)


def guidance(language):
    if language == "zh":
        return f"""先安装电脑桥接，再按设备选择固件；书法、名画按需下载。各组件独立更新，已是所列版本无需重装或重刷。

- **首次使用 TAB5**：选择首刷 ZIP；**已有 AI-bot**：选择升级 ZIP，解压后选择 `aibot_tab5.bin`，保留旁边的更新说明文件。两种硬件的固件不能混用。
- **图库**：Windows「软件与固件更新 → 屏保图库」直接导入 ZIP，无需解压；升级电脑软件会保留图库。图库要求 Windows 桥接 0.6.0+、TAB5 0.2.145-ui+。
- Windows 支持 ESP8266 与 TAB5；Mac 当前支持 ESP8266，TAB5、统一更新与图库导入说明适用于 Windows。Windows 安装器未签名；Mac 为临时签名，未公证。

[安装与刷机步骤]({REPO}/blob/main/docs/INSTALL.zh.md) · [图库容量与许可]({REPO}/blob/main/docs/GALLERY-PACKS.md) · [历史发布]({REPO}/releases)"""
    return f"""Install the computer bridge, then choose firmware for your device. Artwork collections are optional. Components update independently; reinstalling an unchanged version is unnecessary.

- **Factory TAB5**: choose the first-install ZIP. **Existing AI-bot**: extract the upgrade ZIP and select `aibot_tab5.bin`, keeping its release-notes file beside it. Firmware is not interchangeable between devices.
- **Collections**: import ZIPs directly through Windows Software and Firmware Updates → Artwork Collections. App upgrades preserve collections. Requires Windows bridge 0.6.0+ and TAB5 0.2.145-ui+.
- Windows supports ESP8266 and TAB5. Mac currently supports ESP8266; TAB5, unified updates and artwork import instructions apply to Windows. Windows setup is unsigned; Mac is ad-hoc signed and not notarized.

[Installation guide]({REPO}/blob/main/docs/INSTALL.zh.md) · [Collection sizes and licenses]({REPO}/blob/main/docs/GALLERY-PACKS.md) · [Earlier releases]({REPO}/releases)"""


def block(data, language):
    return f"{START}\n{table(data, language)}\n\n{guidance(language)}\n{END}"


def sync(data, check=False):
    for lang, name, readme in (("zh", "DOWNLOADS.md", "README.md"), ("en", "DOWNLOADS.en.md", "README.en.md")):
        title = "# AI-bot 完整下载\n\n[English](DOWNLOADS.en.md)" if lang == "zh" else "# AI-bot downloads\n\n[简体中文](DOWNLOADS.md)"
        hint = "本页集中提供最新正式电脑软件、两种固件与可选图库。" if lang == "zh" else "All current stable apps, both device firmware packages and optional artwork collections."
        outputs = {ROOT / name: f"{title}\n\n{hint}\n\n{block(data, lang)}\n"}
        path = ROOT / readme
        text = path.read_text(encoding="utf-8")
        if text.count(START) != 1 or text.count(END) != 1:
            raise ValueError(f"Missing or duplicate generated download block in {readme}")
        outputs[path] = re.sub(re.escape(START) + r".*?" + re.escape(END), lambda _: block(data, lang), text, flags=re.S)
        for path, content in outputs.items():
            if check:
                if not path.exists() or path.read_text(encoding="utf-8") != content:
                    raise ValueError(f"Stale generated downloads: {path.name}; run sync")
            else:
                path.write_text(content, encoding="utf-8", newline="\n")


def release_intro(tag):
    data = load()
    return (f"## 完整下载 / All downloads\n\n[始终查看最新完整下载 / Latest complete downloads]({CENTER})\n\n"
            "标有「本次发布」的是本 Release 对应包，其余沿用已发布版本。草稿中的新包在正式发布后才能公开下载。\n"
            "Rows marked ‘this release’ refer to this release; other downloads retain published versions. New draft assets become public only after publication.\n\n"
            + table(data, tag=tag) + "\n\n" + table(data, "en", tag=tag)
            + "\n\n" + guidance("zh") + "\n\n---\n\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("refresh", "sync", "check", "verify-online"))
    args = parser.parse_args()
    if args.command == "refresh":
        data = discover(public_releases())
        verify_online(data)
        CATALOG.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    else:
        data = load()
        if args.command == "verify-online":
            verify_online(data)
        else:
            sync(data, check=args.command == "check")
    print("DOWNLOAD_CATALOG_OK " + args.command)


if __name__ == "__main__":
    main()
