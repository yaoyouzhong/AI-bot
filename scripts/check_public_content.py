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
    'mac-menu.png',
    'mac-device-center.png',
    'mac-accounts.png',
    'mac-bridge-settings.png',
    'mac-tab5-preview.png',
    'esp8266-reset-text-057.png',
    'tab5-draft-clear-before-281.png',
    'tab5-draft-clear-empty-281.png',
    'tab5-draft-clear-retyped-281.png',
    'tab5-quota-281.png',
    'model-accounts-0611.png',

    'tab5-wifi-unlock-candidate.png',
    'tab5-firmware-notes-234.png',
    'tab5-firmware-notes-282.png',
    'tab5-music-controls-282.png',
    'windows-runtime-monitor-0612.png',
    'tab5-firmware-notes-234-scrolled.png',
    'tab5-music-idle.png',
    'tab5-computer-control.png',
    'tab5-computer-control-muted.png',
    'tab5-computer-control-locked.png',
    'tab5-network-ten.png',
    'computer-control-settings.png',
    'tab5-agenda.png',
    'tab5-agenda-editor.png',
    'tab5-agenda-reminder.png',
    'tab5-agenda-empty.png',
    'tab5-classic-day.png',
    'tab5-classic-night.png',
    'bridge-monitor.png',
    'tab5-runtime-health.png',
    'tab5-runtime-health-detail.png',
    'tab5-settings-info.png',
    'tab5-clock-geometric.png',
    'tab5-clock-star.png',
    'tab5-clock-0.png',
    'tab5-apps.png',
    'tab5-camera.png',
    'tab5-clock-1.png',
    'tab5-clock-2.png',
    'tab5-clock-3.png',
    'tab5-clock-4.png',
    'tab5-clock-solar.png',
    'tab5-clock-lcd.png',
    'tab5-clock-picker.png',
    'tab5-clock-countdown.png',
    'tab5-date-countdown.png',
    'tab5-countdown-settings.png',
    'tab5-deepseek.png',
    'tab5-hourly.png',
    'tab5-music.png',
    'tab5-overview.png',
    'tab5-pet.png',
    'tab5-photo-draft.png',
    'tab5-reply.png',
    'tab5-settings-connection.png',
    'tab5-settings-content.png',
    'tab5-settings-firmware.png',
    'tab5-settings-screen.png',
    'tab5-settings-storage.png',
    'tab5-seven-days.png',
    'tab5-stocks.png',
    'tab5-system.png',
    'tab5-tasks.png',
    'tab5-voice-recording.png',
    'tab5-voice-review.png',
    'tab5-weather.png',
    'tab5-zhipu.png',
    'tab5-annual.png',
    'tab5-annual-night.png',
    'tab5-annual-warm.png',
    'tab5-quick.png',
    'tab5-floral.png',
    'tab5-painting.png',
    'tab5-calligraphy.png',
    'tab5-painting-portrait.png',
    'tab5-calligraphy-portrait.png',
    'update-center.png',
    'gallery-packs.png',
    'gallery-browser.png',
    'gallery-fullscreen.png',
    'device-center.png',
    'device-center-esp8266.png',
    'device-center-accounts.png',
    'device-center-bridge.png',
    'add-device.png',
    'tab5-first-install.png',
    'tab5-upgrade.png',
    'tab5-transfer-test.png',
    'tab5-usb-pairing.png',
    'tab5-connection.png',
    'tab5-quota.png',
    'tab5-activity.png',
    'tab5-voice.png',
    'tab5-calendar.png',

    'activity.png',
    'api-settings.png',
    'authorization-empty.png',
    'claude.png',
    'codex.png',
    'completed.png',
    'cycle-settings.png',
    'device-control.png',
    'firmware-flasher.png',
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


REVIEWED_PRODUCT_MEDIA = frozenset({
    "docs/assets/hardware/esp8266-product-composite.png",
    "docs/assets/hardware/tab5-product-composite.png",
    "docs/assets/hardware/tab5-side-official.png",
    "docs/assets/hardware/tab5-side-gpio-official.png",
    "docs/assets/hardware/tab5-side-logo-official.png",
    "docs/assets/hardware/tab5-side-antenna-official.png",
    "docs/assets/hardware/tab5-back-official.png",
    "docs/assets/product-intro/AI-bot-product-intro.mp4",
    "docs/assets/product-intro/AI-bot-cover.png",
    "docs/assets/product-intro/AI-bot-cover-4x3.png",
    "docs/assets/product-intro/AI-bot-cover-3x4.png",
    "docs/assets/product-intro/AI-bot-onboarding-cover.png",
})

REVIEWED_PET_MEDIA = frozenset({
    "docs/assets/pet/AI-bot-mascot.png",
    "docs/assets/pet/AI-bot-mascot.gif",
    "docs/assets/pet/AI-bot-pet.zip",
})


def reviewed_pet_media(path, data):
    return path in REVIEWED_PET_MEDIA and hashlib.sha256(data).hexdigest() == DOC_ASSET_HASHES.get(path)


def reviewed_product_media(path, data):
    return path in REVIEWED_PRODUCT_MEDIA and hashlib.sha256(data).hexdigest() == DOC_ASSET_HASHES.get(path)


# Exact binary inputs from the reviewed .279 corresponding-source snapshot.
# Every input matches the already published .234 source archive byte for byte.
REVIEWED_TAB5_SOURCE_ASSETS = frozenset({
    'docs/development/tab5-279-source/source/firmware/assets/brands/alibaba-source.ico',
    'docs/development/tab5-279-source/source/firmware/assets/brands/alibaba-verified-20261010.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/alibaba.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/baidu-source.ico',
    'docs/development/tab5-279-source/source/firmware/assets/brands/baidu.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/claude-source.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/claude.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/codex.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/deepseek-verified-20261010.ico',
    'docs/development/tab5-279-source/source/firmware/assets/brands/deepseek.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/doubao-app.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/doubao-ime.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/kimi-source.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/kimi.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/minimax-source.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/minimax.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/openai.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/stepfun.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/volcengine-source.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/volcengine.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/xiaomi-source.ico',
    'docs/development/tab5-279-source/source/firmware/assets/brands/xiaomi.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/zhipu-verified-20261010.png',
    'docs/development/tab5-279-source/source/firmware/assets/brands/zhipu.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/01-plum.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/02-apricot.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/03-peach.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/04-peony.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/05-paeonia.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/06-gardenia.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/07-lotus.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/08-lily.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/09-osmanthus.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/10-chrysanthemum.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/11-camellia.png',
    'docs/development/tab5-279-source/source/firmware/assets/calendar-flowers/12-narcissus.png',
    'docs/development/tab5-279-source/source/firmware/components/esp_ipa/lib/esp32p4/libesp_ipa.a',
    'docs/development/tab5-279-source/source/firmware/components/esp_lvgl_port/docs/frame_buffer_settings.png',
    'docs/development/tab5-279-source/source/firmware/components/esp_lvgl_port/examples/rgb_lcd/main/images/esp_logo.png',
    'docs/development/tab5-279-source/source/firmware/components/esp_lvgl_port/examples/touchscreen/main/images/esp_logo.png',
    'docs/development/tab5-279-source/source/firmware/components/esp_lvgl_port/images/img_cursor.png',
    'docs/development/tab5-279-source/source/firmware/components/esp_lvgl_port/images/img_cursor_20px.png',
    'docs/development/tab5-279-source/source/firmware/main/assets/health_notice.pcm',
    'docs/development/tab5-279-source/source/firmware/main/assets/reply_notice.pcm',
})


def reviewed_tab5_source_asset(path, data):
    return path in REVIEWED_TAB5_SOURCE_ASSETS and hashlib.sha256(data).hexdigest() == DOC_ASSET_HASHES.get(path)


REVIEWED_SOURCE_ARCHIVE = "docs/development/TAB5-0.2.89-ui-source.zip"


def reviewed_source_archive(path, data):
    return path == REVIEWED_SOURCE_ARCHIVE and hashlib.sha256(data).hexdigest() == DOC_ASSET_HASHES.get(path)


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
                             '.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp', '.ico', '.pcm', '.mp4', '.mov', '.webm'}
            and normalized != 'windows-app/aibotbridge/assets/app-icon.ico'
            and not (path.replace(chr(92), '/') == 'windows-app/AIBotBridge/Assets/flash-icon.ico'
                     and hashlib.sha256(data).hexdigest() == DOC_ASSET_HASHES.get('windows-app/AIBotBridge/Assets/flash-icon.ico'))
            and not reviewed_doc_image(normalized, data)
            and not reviewed_product_media(path.replace(chr(92), "/"), data)
            and not reviewed_pet_media(path.replace(chr(92), "/"), data)
            and not reviewed_tab5_source_asset(path.replace(chr(92), "/"), data)
            and not reviewed_source_archive(path.replace(chr(92), "/"), data)):
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
        assert list(findings("docs/assets/screens/tab5-transfer-test.png", b"changed image"))
        speed_image = "docs/assets/screens/tab5-transfer-test.png"
        assert not list(findings(speed_image, (ROOT / speed_image).read_bytes()))
        known = "docs/assets/screens/codex.png"
        assert not list(findings(known, (ROOT / known).read_bytes()))
        assert list(findings("docs/assets/product-intro/unknown.mp4", b"video"))
        assert list(findings("docs/assets/pet/unknown.gif", b"animation"))
        assert list(findings("docs/assets/pet/AI-bot-mascot.gif", b"changed animation"))
        assert list(findings("docs/assets/pet/selected.apet", b"private runtime resource"))
        for media in REVIEWED_PET_MEDIA:
            assert not list(findings(media, (ROOT / media).read_bytes()))
        assert list(findings("docs/assets/product-intro/AI-bot-product-intro.mp4", b"changed video"))
        for media in REVIEWED_PRODUCT_MEDIA:
            assert not list(findings(media, (ROOT / media).read_bytes()))
        assert list(findings("docs/development/tab5-279-source/source/unknown.png", b"image"))
        for name in REVIEWED_TAB5_SOURCE_ASSETS:
            assert not list(findings(name, (ROOT / name).read_bytes()))
            assert list(findings(name, b"changed archived input"))
        assert list(findings("docs/development/unknown.zip", b"archive"))
        assert list(findings(REVIEWED_SOURCE_ARCHIVE, b"changed source archive"))
        assert not list(findings(REVIEWED_SOURCE_ARCHIVE, (ROOT / REVIEWED_SOURCE_ARCHIVE).read_bytes()))
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
