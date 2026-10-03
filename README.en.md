**2026-10-02 acceptance baseline**: TAB5 **0.2.89-ui** and the matching Windows bridge are deployed and boot-verified. This round fixes settings layouts, camera recovery, voice cursor insertion and disconnected-result recovery, attachment receipt queries, configuration restore and pointer visibility. See the [current acceptance record](docs/ACCEPTANCE-2026-10-02.md) for individual scopes. USB upgrade interruption investigation is paused at the user's request; physical keyboard, dual-device isolation and other environments remain untested. This is not full acceptance.

**Product introduction · 36 seconds**　[▶ Player stuck? Play directly](https://github.com/user-attachments/assets/dc69b524-0c0e-46cb-b92a-83d794968ccf)

The fixed local TAB5 firmware entry is `artifacts/firmware/tab5/latest/aibot_tab5.bin`; see the [firmware workflow](docs/TAB5-FIRMWARE-WORKFLOW.md) for preparation, archives and release-note previews. Historical .070 results for 18 three-channel bidirectional rounds remain in the [performance record](docs/TRANSPORT-PERFORMANCE-070.md) and do not establish current throughput or long-term stability. Fixed modes retain their selected channel. Versions 0.2.65, 0.2.40 and 0.2.41 were withdrawn; do not install them.

Voice inserts at the current TAB5 cursor and retains the recording task through temporary device focus loss or page changes. Version 089 passed three recovery checks covering short and 45-second USB disconnections, original insertion position, cancellation and isolation of the next recording. Untransmitted audio is not guaranteed and messages are never sent automatically. See [089 voice recovery](docs/VOICE-RECOVERY-089.md).

https://github.com/user-attachments/assets/dc69b524-0c0e-46cb-b92a-83d794968ccf


[▶ Download the 36-second product introduction (MP4, Chinese captions)](https://raw.githubusercontent.com/yaoyouzhong/AI-bot/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [Video provenance](docs/PRODUCT_VIDEO.md)

**v0.4.0 release:** reduces false `PC OFF` screens under host load. The Windows bridge and updated firmware use paired local discovery to recover Wi-Fi status after the laptop or device IP changes. See the [changelog](CHANGELOG.md).

**Windows development build (unreleased):** My Devices exposes features and runs connection services only for registered TAB5 / ESP8266 devices, while sharing accounts and data sources. The main window separates My Devices, Accounts and Data, and Bridge Settings; common actions use two columns, connection details stay together, and management and diagnostics live in a menu. Desktop checks and the current dual-device USB connection passed; remaining hardware combinations await acceptance. See the [device-center validation and rollback notes](docs/DEVICE-CENTER-VALIDATION.zh.md) (Chinese).

Device Center provides all features and device registration. Left-click opens the small-screen preview and brightness slider when an ESP8266 is enabled, otherwise Device Center. Right-click offers Device Center, screen preview, common settings grouped by device, and Exit. Shared accounts and data settings are available in Device Center. Only registered devices appear, using their custom names; disabled devices are grayed out. Device management and occasional diagnostics remain in Device Center. Data updates and Codex quota-history collection run automatically while the bridge is running, with service status displayed in tables. Voice settings show only the enable toggle and preferred microphone by default; other options are under Troubleshooting.

All three main pages share two-column action cards, with a compact Chinese label for Weather and Location. Data Settings separates data categories from domestic model providers, showing Chinese names, English names and the collected quota type. Provider choices follow the model-quota toggle. Add remains clickable when both models are registered and explains the one-device-per-model limit and replacement steps.

ESP8266 Connection Settings offers Automatic (default), USB only and Wi-Fi only. Automatic prefers USB. Wi-Fi only stops USB data delivery while keeping USB power, configuration and flashing available; USB only blocks the small screen's Wi-Fi data endpoints. Saving applies the choice to this screen without a firmware update.

Small-screen brightness, display mode and cycling are combined in Display Settings, where changes save and apply automatically. Preview uses previous/next navigation and a current-page menu containing only checked cycle pages in their saved order. Automatic Cycling runs on every click and shows the page position, switch countdown and task-alert interruptions. The popup keeps a fixed height and a slim brightness slider instead of stacking module buttons. Device Center orders small-screen actions as Display Settings, Appearance, Data Settings and Connection Settings, with firmware updates under Manage and the asset gallery under Appearance. Preview remains available from a tray left-click. TAB5 actions are ordered as Display Settings, Voice Settings, Calendar and Birthdays, Frequent Tasks, Data Settings and Connection and Updates.

<p align="center">
  <strong>AI status at a glance.</strong><br>
  Task status and account quotas, together on a small desktop display.<br>
  <strong>Windows · macOS (Apple Silicon test build)</strong>
</p>

<p align="center">
  <a href="https://github.com/yaoyouzhong/AI-bot/actions/workflows/ci.yml"><img src="https://github.com/yaoyouzhong/AI-bot/actions/workflows/ci.yml/badge.svg" alt="Live CI status"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/source-MIT-81dce6?style=flat-square&labelColor=142b43" alt="Own source: MIT"></a>
  <img src="https://img.shields.io/badge/ESP8266-240_%C3%97_240-a6b5ff?style=flat-square&labelColor=142b43" alt="ESP8266, 240 by 240 display">
  <img src="https://img.shields.io/badge/status-development-ffb183?style=flat-square&labelColor=142b43" alt="Development status">
</p>

<p align="center">
  <a href="README.md">简体中文</a> · <strong>English</strong><br>
  <a href="#a-live-window-on-your-desk">Features</a> ·
  <a href="#get-started">Get started</a> ·
  <a href="#current-status">Current status</a> ·
  <a href="#explore-the-project">Documentation</a>
</p>

---

The TAB5 0.2.39 development candidate adds USB / BLE firmware updates, history, sending and image attachments. Older firmware first needs a USB or Wi-Fi upgrade. Desktop builds and simulations passed; the new USB/BLE paths still await hardware acceptance.

## Stay with the work in front of you

Is your terminal still busy, or waiting for your next step? How much of this week's
quota have you used? AI-bot puts those signals on a small ESP8266 display.

The desktop bridge reads local Claude Code / Codex activity and provider quotas,
then sends them to the device. Windows connects over USB first and lives in the
system tray; a left click opens the screen mirror. Mac has a menu-bar test build with a mirror; real-Mac acceptance remains pending.

On Windows, Bridge service → Start at login uses a current-user scheduled task with a five-second delay, bypassing the ordinary startup queue. Repeated launches keep one bridge instance. Toggle an older startup registration off and on once to migrate it.

## A live window on your desk

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

<sub>Actual Windows mirror captures with synthetic data, presented with Chinese labels. These are not device photographs; existing product names and abbreviations are preserved. The Codex companion is the maintainer’s selected artwork.</sub>

| Keep track of work | Keep your desk informed |
| :--- | :--- |
| **AI activity**<br>Working, idle and offline states for Claude / Codex; attention signals and an explicit main-task completion chime. | **Weather and time**<br>Local conditions, an independent clock and automatic screen saving. Keep the last successful data during temporary outages. |
| **Quotas and balances**<br>Claude / Codex usage, resets and reset-credit details; Windows integrations for Alibaba, Kimi, MiniMax, DeepSeek and Zhipu; new MiMo, StepFun and Baidu adapters await live-account acceptance. | **Music and markets**<br>Now-playing title, artwork and progress; paged watchlists for mainland China, Hong Kong and US markets. |
| **System monitoring**<br>CPU, memory and network activity, with live traffic graphs and a screen mirror. | **Animated companions**<br>The original BYTE SPROUT, plus local images/GIFs with license notices. Choose pets independently for Claude and Codex. |

**Choose what stays on screen.** Pin a page or cycle through quotas, weather, stocks
and more in your preferred order. Work events can wake the screen saver. When the
PC goes offline and the device still has power, it shows an independent `PC OFF` clock.

<details>
<summary><strong>What quota history can and cannot tell you</strong></summary>

Account quotas come from providers; local Token counts cover only visible local
logs. They are separate metrics. Windows retains 90 days of quota history. Daily
figures sum verifiable observed increments by Beijing date. Partial days also show
recorded usage; today remains in progress and `--` means no comparable samples.
Only complete historical days enter averages. Gaps are not estimated, and an
unverified quota change does not prove the user performed a reset. See [quota trends](docs/QUOTA_TRENDS.md).

</details>

## Local-first, starting with the connection

```mermaid
flowchart LR
    A["Local AI activity"] --> B["Desktop bridge"]
    Q["Provider quotas · weather · markets"] --> B
    B ==>|"USB first"| C["ESP8266 desktop display"]
    B -.->|"Authenticated Wi-Fi fallback"| C
    B --> M["Tray and screen mirror"]
```

- **Direct USB:** automatic discovery and handshake at 460800 baud; basic USB operation does not require LAN reachability.
- **Bounded fallback:** attempts the paired LAN after USB goes stale. Windows and updated firmware can rediscover each other after either IP changes. Peers must be reachable on a local subnet that permits broadcast; simultaneous real DHCP renumbering remains unverified.
- **Useful data survives outages:** temporary provider failures preserve the last successful display state.

### Data moves only where it is needed

Session logs supply state, model, time and Token metadata; conversation text is not
uploaded. Account tokens are used only with matching provider endpoints, never in
display caches, serial data or logs. Domestic sign-in uses an isolated browser
profile. Private artwork, cookies, account caches and pairing data stay out of the
repository. See [data sources and privacy](docs/DATA_SOURCES.md) and [asset imports](docs/ASSET_POLICY.md).

## Get started

**Download → install → connect USB → choose firmware → start flashing.** Follow the [complete illustrated installation guide (Chinese)](docs/INSTALL.zh.md) for every Windows/Mac step on one page, without terminal commands.

| Windows 10/11 | Mac (Apple Silicon) |
| --- | --- |
| [Download installer](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.4.0/AIBotBridge-0.4.0-setup-win-x64.exe) | [Download Mac app](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.4.0/AIBotBridge-0.4.0-local-candidate-macos-arm64.zip) |

[![Firmware flashing window: connect the device and select firmware](docs/assets/screens/firmware-flasher.png)](docs/INSTALL.zh.md)

**Upgrading:** exit the bridge, install into the existing location and keep using your original shortcut. Do not launch copies from temporary extraction folders. Flashing tools are bundled; Windows setup downloads missing runtimes when needed.

BYTE SPROUT works out of the box; pet imports are optional. These are actual Windows mirror renders with fictional data. Codex and Claude screenshots show the maintainer’s currently selected pets with sample values; they are not photographs of a device.

<details>
<summary>More interface examples</summary>

<table>
<tr>
<td align="center"><img src="docs/assets/screens/codex.png" width="240" alt="Codex quota sample with the currently selected pet"><br><strong>Codex quota</strong></td>
<td align="center"><img src="docs/assets/screens/claude.png" width="240" alt="Claude quota sample with the currently selected pet"><br><strong>Claude quota</strong></td>
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

> **v0.4.0 release: Windows auto-discovery requires both the new bridge and firmware.** The Mac bridge does not yet support this discovery path. Missing runtimes require internet access.
> The installer is unsigned; clean-machine install, upgrade and uninstall remain unverified. Mac is an Apple Silicon test build awaiting real-device acceptance.

The screensaver supports lunar dates with leap-month labels. This release also requires updated firmware for the manual page-selection fix.

**Setup:** right-click the tray → Device Center → Accounts and Data → Model Accounts. Full provider and plan labels are shown; API keys stay in Windows Credential Manager. See the [updated settings screenshot and guide](docs/DOMESTIC_QUOTA_SETUP.md). Unconfigured, never-authorized or unselected providers do not trigger automatic checks or failure reminders.

### Prepare the hardware

An ESP8266 / ESP-12S with a 240×240 ST7789 display using the SD2 pin layout, plus a
**USB data cable**. Check the driver and pin mapping before using another board.
See [firmware configuration](firmware/platformio.ini).

### Windows: build a local candidate

Install Git, Python and the .NET 8 SDK, then run:

```powershell
git clone https://github.com/yaoyouzhong/AI-bot.git
cd AI-bot
powershell -NoProfile -File scripts/package_windows_local.ps1
```

The script creates a Windows setup EXE, a complete Windows ZIP and a public-source ZIP under `artifacts/`,
including notices and checksums, then tests the unpacked app. Add `-Firmware` to
also build firmware and its source-materials archive; PlatformIO 6.1.18 is required.

For normal installation, follow the [complete illustrated guide (Chinese)](docs/INSTALL.zh.md); building from source is optional.

[Windows installation and upgrades](docs/INSTALL.zh.md) · [Firmware and rebuilding](docs/FIRMWARE_PACKAGE.md) · [Full development reference](docs/REFERENCE.en.md)

### macOS: Apple Silicon test build

Targets macOS 13+ on M-series chips. Follow the [Mac section of the complete guide](docs/INSTALL.zh.md#mac) for installation, firmware and connection.
The app is ad-hoc signed, without Developer ID signing or notarization. First launch, permissions and device behavior need real-Mac acceptance; Intel is unverified.

## Current status

| Platform / stage | Current evidence | Still to validate |
| :--- | :--- | :--- |
| **Windows** | Online setup, host detection, download integrity checks and isolated public regressions pass | Fresh installation, live authorization, sustained operation and full interaction acceptance |
| **ESP8266** | Firmware build, five-minute Wi-Fi status run, discovery from an obsolete bridge port and USB recovery passed on hardware | Every physical page, music and simultaneous real DHCP renumbering |
| **macOS (Apple Silicon test build)** | Automated tests, Release compilation and app packaging run in the cloud; see Actions | First launch, permissions, devices and sustained operation; Developer ID signing and notarization; Intel unverified |
| **Distribution** | Setup EXE, Windows/Mac ZIPs, firmware materials and sources are packaged by tag with notices and SHA-256 | Fresh-logon startup, clean installation, real-Mac and complete device acceptance |

Candidate installation and acceptance: [Mac package](docs/INSTALL.zh.md) · [Release readiness](docs/RELEASE_READINESS.md) · [Candidate checklist](docs/CANDIDATE_ACCEPTANCE.md).

Documentation updated: 2026-09-23, **v0.4.0**.
Follow [Actions](https://github.com/yaoyouzhong/AI-bot/actions) and [release readiness](docs/RELEASE_READINESS.md) for updates.

## Explore the project

| What you need | Start here |
| :--- | :--- |
| Features, commands and platform differences | [Feature and development reference](docs/REFERENCE.en.md) |
| Architecture, data and caching | [Development](docs/DEVELOPMENT.md) · [Data sources](docs/DATA_SOURCES.md) |
| Serial, resources and fallback | [Protocol](docs/PROTOCOL.md) · [USB validation](docs/USB_VALIDATION.md) |
| Completion status and known limits | [Parity matrix](docs/FUNCTIONAL_PARITY.md) · [Acceptance audit](docs/FULL_PARITY_AUDIT_2026-09-09.md) |
| Origins and distribution | [Provenance](PROVENANCE.md) · [Third-party notices](THIRD_PARTY_NOTICES.md) · [Distribution terms](docs/DISTRIBUTION_TERMS.md) |
| Recent changes | [Changelog](CHANGELOG.md) |

Feedback is welcome in [Issues](https://github.com/yaoyouzhong/AI-bot/issues).
Include your platform, version, device and reproduction steps, with account details,
tokens and private log contents removed.

---

<p align="center">
  <strong>AI-bot</strong><br>
  A window into the AI work on your desk.<br><br>
  <a href="LICENSE">Own source: MIT</a> · Original BYTE SPROUT · Local-first<br>
  <sub>Independent project. No affiliation with or endorsement by named AI providers. Third-party components retain their own licenses.</sub>
</p>

## Installation

[Complete illustrated installation guide (Chinese)](docs/INSTALL.zh.md) — one page for downloads, Windows/Mac setup, device flashing, first connection, settings, upgrades and troubleshooting.
