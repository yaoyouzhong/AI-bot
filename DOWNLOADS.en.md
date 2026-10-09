# AI-bot downloads

[简体中文](DOWNLOADS.md)

All current stable apps, both device firmware packages and optional artwork collections.

<!-- downloads:start -->
| Use | Version | Download | SHA-256 |
| --- | --- | --- | --- |
| Windows bridge · Windows 10/11 x64 | 0.6.3 | [Download · 57 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.3/AIBotBridge-0.6.3-setup-win-x64.exe) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.3/SHA256SUMS.txt) |
| Mac bridge · macOS 13+ / Apple Silicon | 0.6.3 | [Download · 46 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.3/AIBotBridge-0.6.3-local-candidate-macos-arm64.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.3/SHA256SUMS.txt) |
| ESP8266 display · first install / upgrade | 0.5.1 | [Download · 30 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.1/AI-bot-0.5.1-firmware-materials.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.1/SHA256SUMS.txt) |
| TAB5 · first install from factory system | 0.2.156-ui | [Download · 4 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/TAB5-first-install-0.2.156-ui.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/SHA256SUMS.txt) |
| TAB5 · upgrade existing AI-bot | 0.2.156-ui | [Download · 4 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/TAB5-upgrade-0.2.156-ui.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/SHA256SUMS.txt) |
| Daily Calligraphy · optional collection | 2026.10.09 | [Download · 426 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/AI-bot-DailyCalligraphy-Curated-2026.10.09.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/SHA256SUMS.txt) |
| Daily Painting · optional collection | 2026.10.09 | [Download · 211 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/AI-bot-DailyPainting-Curated-2026.10.09.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.156-ui/SHA256SUMS.txt) |

Install the computer bridge, then choose firmware for your device. Artwork collections are optional. Components update independently; reinstalling an unchanged version is unnecessary.

- **Factory TAB5**: choose the first-install ZIP. **Existing AI-bot**: extract the upgrade ZIP and select `aibot_tab5.bin`, keeping its release-notes file beside it. Firmware is not interchangeable between devices.
- **Collections**: import ZIPs directly through Windows Software and Firmware Updates → Artwork Collections. App upgrades preserve collections. Requires Windows bridge 0.6.0+ and TAB5 0.2.145-ui+.
- Windows supports ESP8266 and TAB5. Mac currently supports ESP8266; TAB5, unified updates and artwork import instructions apply to Windows. Windows setup is unsigned; Mac is ad-hoc signed and not notarized.

[Installation guide](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md) · [Collection sizes and licenses](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-PACKS.md) · [Earlier releases](https://github.com/yaoyouzhong/AI-bot/releases)
<!-- downloads:end -->
