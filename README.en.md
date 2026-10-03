# AI-bot

**AI status at a glance.** A local-first desktop companion for Claude Code / Codex activity, account quotas and everyday information, on an **ESP8266 display or M5Stack TAB5**.

[**Download v0.5.0**](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.5.0) · [Installation and flashing guide](docs/INSTALL.zh.md#english-summary) · [简体中文](README.md)

Windows and ESP8266 use **0.5.0**; TAB5 uses its independent **0.2.89-ui** firmware. The maintainer confirmed hardware acceptance on 2026-10-03, and v0.5.0 is the current stable release. See [release records](docs/RELEASE-0.5.0.md).

![CI](https://github.com/yaoyouzhong/AI-bot/actions/workflows/ci.yml/badge.svg)
[![MIT](https://img.shields.io/badge/own_source-MIT-81dce6)](LICENSE)
![Version](https://img.shields.io/badge/release-v0.5.0-blue)
![Hardware](https://img.shields.io/badge/hardware-ESP8266_%7C_TAB5-a6b5ff)

[![54-second product overview](docs/assets/product-intro/AI-bot-cover.png)](docs/assets/product-intro/AI-bot-product-intro.mp4)

[▶ Product overview (54 seconds, Chinese captions)](docs/assets/product-intro/AI-bot-product-intro.mp4) · [Media provenance](docs/PRODUCT_VIDEO.md)

## A live window on your desk

The bridge reads local AI activity and provider quotas and sends them to your device. Windows lives in the system tray; macOS provides a menu-bar app and screen mirror. Device features differ by platform.

| Feature | What it shows |
| --- | --- |
| AI activity | Claude / Codex working, idle, offline and waiting for input; task-completion reminders |
| Quotas and balances | Account usage, reset times, supported provider balances and recorded quota trends |
| Everyday information | Weather, clock, stocks, system monitoring and now-playing music |
| Companions and display | Original BYTE SPROUT pet, brightness, selected pages and automatic cycling |
| TAB5 interaction | Touch interface, activity/history, voice input, calendar/birthdays and frequent tasks |

<table>
<tr>
<td align="center" width="33%"><strong>AI 额度与桌宠</strong><br><br><img src="docs/assets/screens/codex.png" width="240" height="240" alt="AI 额度与桌宠：实际程序界面，演示数据"><br>额度、重置时间与动画角色</td>
<td align="center" width="33%"><strong>天气时钟</strong><br><br><img src="docs/assets/screens/weather.png" width="240" height="240" alt="天气时钟：实际程序界面，演示数据"><br>城市天气、温度与湿度</td>
<td align="center" width="33%"><strong>股票行情</strong><br><br><img src="docs/assets/screens/stocks.png" width="240" height="240" alt="股票行情：实际程序界面，演示数据"><br>自选股票、报价与涨跌幅</td>
</tr>
<tr>
<td align="center" width="33%"><strong>系统监控</strong><br><br><img src="docs/assets/screens/system.png" width="240" height="240" alt="系统监控：实际程序界面，演示数据"><br>处理器、内存与实时网速</td>
<td align="center" width="33%"><strong>音乐播放</strong><br><br><img src="docs/assets/screens/music.png" width="240" height="240" alt="音乐播放：实际程序界面，演示数据"><br>歌曲信息与播放进度</td>
<td align="center" width="33%"><strong>农历屏保</strong><br><br><img src="docs/assets/screens/screensaver.png" width="240" height="240" alt="农历屏保：实际程序界面，演示数据"><br>时间、星期与农历日期</td>
</tr>
</table>

<sub>Native Windows mirror captures with synthetic data and the original BYTE SPROUT. These are not hardware photographs.</sub>

### Two devices, one Windows Device Center

**My Devices / Accounts and Data / Bridge Settings** separates device settings from shared accounts. You can register one ESP8266 and one TAB5 together, manage their connections independently and share data sources.

- **ESP8266:** 240×240 ST7789 display using the SD2 pin configuration; USB-first automatic mode with paired Wi-Fi fallback, or a selected USB/Wi-Fi mode.
- **TAB5:** touch display and USB / Wi-Fi / BLE connections. Automatic mode prefers USB, then Wi-Fi, then BLE; factory installation uses USB.
- Tray left-click opens the enabled ESP8266 preview, or Device Center otherwise; right-click provides device settings. Start at login is under **Device Center → Bridge Settings**.

![Windows Device Center](docs/assets/screens/device-center.png)

![TAB5 quota page, synthetic firmware preview](docs/assets/screens/tab5-quota.png)

<details>
<summary>More interface examples</summary>

<table>
<tr>
<td align="center"><img src="docs/assets/screens/codex.png" width="240" alt="Codex quota sample with the original pet"><br><strong>Codex quota</strong></td>
<td align="center"><img src="docs/assets/screens/claude.png" width="240" alt="Claude quota sample with the original pet"><br><strong>Claude quota</strong></td>
<td align="center"><img src="docs/assets/screens/dual.png" width="240" alt="Dual quota sample"><br><strong>Dual quotas</strong></td>
</tr>
<tr>
<td align="center"><img src="docs/assets/screens/domestic_deepseek.png" width="240" alt="DeepSeek official API balance with fictional data"><br><strong>Domestic model balance (DeepSeek)</strong></td>
<td align="center"><img src="docs/assets/screens/weather.png" width="240" alt="Weather sample"><br><strong>Weather &amp; clock</strong></td>
<td align="center"><img src="docs/assets/screens/system.png" width="240" alt="System monitor sample"><br><strong>System monitor</strong></td>
</tr>
<tr>
<td align="center"><img src="docs/assets/screens/music.png" width="240" alt="Music sample"><br><strong>Music playback</strong></td>
<td align="center"><img src="docs/assets/screens/stocks.png" width="240" alt="Stock quote sample"><br><strong>Stocks</strong></td>
<td align="center"><img src="docs/assets/screens/screensaver.png" width="240" alt="Screensaver clock with lunar date"><br><strong>Lunar screensaver clock</strong></td>
</tr>
</table>

</details>

### Local data and clear quota boundaries

Activity collection extracts status, model, timestamps and token metadata from local logs. It does not upload conversation text. Account quotas come from provider interfaces; local token totals cover only visible local logs and are kept separate. Failed requests retain the last successful display data.

Windows quota history retains up to 90 days of observed samples. Missing periods are not estimated; ambiguous changes remain uncertain. Provider availability depends on a valid supported account and authorization. See [quota trends](docs/QUOTA_TRENDS.md), [data sources](docs/DATA_SOURCES.md) and [asset policy](docs/ASSET_POLICY.md).

## Get started

**Install the app → choose your hardware → select factory installation or upgrade → back up, flash and verify → register and pair.** See the [illustrated guide](docs/INSTALL.zh.md).

| Download | Platform or purpose |
| --- | --- |
| [Windows installer](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AIBotBridge-0.5.0-setup-win-x64.exe) | Windows 10/11 x64; flashing tools included |
| [Mac application](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AIBotBridge-0.5.0-local-candidate-macos-arm64.zip) | macOS 13+, Apple Silicon |
| [ESP8266 firmware](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AI-bot-0.5.0-firmware-materials.zip) | Firmware and corresponding source/build materials |
| [TAB5 factory-install ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/TAB5-first-install-0.2.89-ui.zip) | Factory devices: full installation image |
| [TAB5 upgrade ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/TAB5-upgrade-0.2.89-ui.zip) | Existing AI-bot devices: extract and select `aibot_tab5.bin` |

[SHA-256 checksums](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/SHA256SUMS.txt) cover all five packages. Their licenses and notices are included. Historical versions are available under [Releases](https://github.com/yaoyouzhong/AI-bot/releases).

**TAB5 first installation also uses 0.2.89-ui.** Select the complete first-install ZIP, which includes bootloader, partitions and application. An application BIN alone cannot initialize a factory device. Keep the notes sidecar alongside the upgrade BIN.

Use a USB data cable. ESP8266 requires the matching ST7789/SD2 wiring; TAB5 uses its USB-C data connector. Finish by verifying the actual screen and connections and preserving automatic cycling, pages, order and interval.

Existing Windows users should exit the bridge, install to the original location and use their existing shortcut. Windows setup is unsigned. The Mac application is ad-hoc signed and not notarized; Intel Macs are unverified. Mac's flashing window currently supports ESP8266; the TAB5 factory-install, pairing and upgrade flows described here are Windows features.

## Current status

| Component | Current release scope |
| --- | --- |
| Windows 0.5.0 | Device Center, both hardware models, shared accounts and separate device settings |
| ESP8266 0.5.0 | Display pages, clock, pet, USB / Wi-Fi and automatic cycling |
| TAB5 0.2.89-ui | Touch, activity/history, voice, calendar and USB / Wi-Fi / BLE |
| macOS 0.5.0 | Apple Silicon menu-bar bridge, mirror and ESP8266 flashing |

The maintainer confirmed hardware acceptance on 2026-10-03. Windows build/public regressions, macOS tests/build and ESP8266 build passed the [release workflow](https://github.com/yaoyouzhong/AI-bot/actions/runs/37091169252). Release status does not add new performance measurements or erase historical issues; details remain in [release records](docs/RELEASE-0.5.0.md).

## Explore the project

| Topic | Reference |
| --- | --- |
| Installation and hardware flashing | [Illustrated guide](docs/INSTALL.zh.md) |
| Features and platform differences | [Reference](docs/REFERENCE.en.md) |
| Architecture, data and protocol | [Development](docs/DEVELOPMENT.md) · [Data sources](docs/DATA_SOURCES.md) · [Protocol](docs/PROTOCOL.md) |
| TAB5 source and build snapshot | [Developer source guide](docs/development/TAB5-SOURCE.md) · [Firmware workflow](docs/TAB5-FIRMWARE-WORKFLOW.md) |
| Source and licenses | [Provenance](PROVENANCE.md) · [Third-party notices](THIRD_PARTY_NOTICES.md) · [TAB5 license scope](docs/TAB5-LICENSE-SCOPE.md) |
| Changes | [Changelog](CHANGELOG.md) |

Own code, including TAB5, is **MIT licensed**. Third-party components, fonts and marks retain their original terms. This is an independent project with no affiliation with the named AI providers.

Feedback: [Issues](https://github.com/yaoyouzhong/AI-bot/issues). Include platform, version, hardware and reproduction steps; remove credentials and private logs.
