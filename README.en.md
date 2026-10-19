# AI-bot

**Your AI status, all on one screen.** AI-bot is a desktop AI companion that works with your computer, bringing AI task status, account quotas across providers and everyday information to a dedicated display, with fewer trips between windows. Follow Claude Code / Codex progress and view plan usage or API balances for Chinese AI services including DeepSeek, Zhipu, Kimi, MiniMax and Alibaba Model Studio.

Beyond work, AI-bot serves as a desk clock and information display for weather, stocks, music and computer activity. Optional daily painting and calligraphy collections turn the TAB5 into an art display during quieter moments.

[Complete download center](DOWNLOADS.en.md) · [Downloads and device pairing](#downloads-and-device-pairing) · [Installation guide](docs/INSTALL.zh.md) · [Release highlights and details](docs/RELEASE-0.6.10.md) · [简体中文](README.md)

Published: Windows **0.6.10** and TAB5 **0.2.234-ui** add computer unlock and automatic component setup, and fix volume-drag recovery, bidirectional read sync, BLE boot verification and battery-display continuity. Related hardware acceptance is complete; see [the release notes](docs/RELEASE-0.6.10.md). The download center is updated; Mac, ESP8266 and collections retain independent versions.

## Choose your desktop display

AI-bot pairs a computer app with a desktop display. Choose the device that fits how you want to use it.

<table>
<tr>
<td width="50%" valign="top">
<p align="right"><a href="docs/FLASH_BUILD.zh.md#1-核对硬件并留好回退材料">Hardware details ↗</a></p>
<h3>ESP8266 compact display</h3>
<p><strong>Desktop status display</strong></p>
<p align="center"><img src="docs/assets/hardware/esp8266-product-composite.png" width="280" alt="Product composite of an ESP8266 SD2-style desktop display showing the AI-bot quota interface"></p>
<p>A compact ESP8266-based device with a 240 × 240 color screen. Keep AI task status, model quotas, weather and music information beside your computer, with settings managed from the computer app.</p>
<p><strong>Cable note:</strong> The included power cable supplies power only. Use a USB data cable for flashing or connecting the display to the computer over USB.</p>
<p><strong>Connections:</strong> USB / Wi-Fi<br><strong>Computer support:</strong> Windows / macOS<br><strong>Best for:</strong> keeping information visible at a glance.</p>
<p><a href="docs/INSTALL.zh.md#esp8266">Setup and connection</a> · <a href="docs/FEATURES.zh.md#esp8266">Interface gallery</a></p>
</td>
<td width="50%" valign="top">
<p align="right"><a href="https://docs.m5stack.com/en/core/Tab5">Hardware details ↗</a></p>
<h3>M5Stack TAB5 touchscreen</h3>
<p><strong>Interactive desktop companion</strong></p>
<p align="center"><img src="docs/assets/hardware/tab5-product-composite.png" width="280" alt="Composite of official M5Stack TAB5 hardware and the native AI-bot quota interface"></p>
<p><a href="https://shop.m5stack.com/pages/about-us">M5Stack</a> makes modular hardware for IoT development, including controllers, sensors and expansion modules. TAB5 is its 5-inch touchscreen terminal.</p>
<details>
<summary>View all four sides and the back (5 official photos)</summary>
<p align="center"><strong>USB side</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-official.png"><img src="docs/assets/hardware/tab5-side-official.png" width="360" alt="Official TAB5 side photo showing USB-A, USB-C, headset jack and PORT.A"></a></p>
<p align="center"><strong>GPIO / microSD side</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-gpio-official.png"><img src="docs/assets/hardware/tab5-side-gpio-official.png" width="360" alt="Official TAB5 side photo showing GPIO expansion, microSD slot, tripod mount and speaker"></a></p>
<p align="center"><strong>M5 logo side</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-logo-official.png"><img src="docs/assets/hardware/tab5-side-logo-official.png" width="360" alt="Official photo of the M5 logo side of TAB5"></a></p>
<p align="center"><strong>External antenna side</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-antenna-official.png"><img src="docs/assets/hardware/tab5-side-antenna-official.png" width="360" alt="Official TAB5 side photo showing external antenna connectors and product identification"></a></p>
<p align="center"><strong>Back</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-back-official.png"><img src="docs/assets/hardware/tab5-back-official.png" width="260" alt="Official TAB5 back photo showing the battery mount, M5-Bus and pin labels"></a></p>
<p><sub>All five are official M5Stack photos, cropped only to remove whitespace. Click to enlarge.</sub></p>
</details>
<p>A desktop device with a 5-inch touchscreen. View status and quotas, continue Codex conversations, dictate messages, add photo attachments and manage reminders, with clocks, paintings and calligraphy for everyday display.</p>
<p><strong>Connections:</strong> USB / Wi-Fi / Bluetooth<br><strong>Computer support:</strong> Windows<br><strong>Best for:</strong> touch interaction, task handling and a wider range of display options.</p>
<p><a href="docs/INSTALL.zh.md#tab5-first">Setup and connection</a> · <a href="docs/FEATURES.zh.md#tab5">Interface gallery</a></p>
</td>
</tr>
</table>

<sub>Both pictures combine hardware imagery with the AI-bot interface and demo data; they are not hardware photographs or shown to a common physical scale. <a href="docs/HARDWARE-IMAGES.md">Image sources and notes</a></sub>

The updated 116-second Chinese tour adds computer unlock, automatic TightVNC setup/import and bidirectional read synchronization, uses the actual .234 notes and retains agenda, clocks, quotas and artwork browsing. Native interfaces use isolated demo data, not hardware photographs.

https://github.com/user-attachments/assets/d745c087-9c9d-48c8-bd55-00a3879eff32

<sub>116-second Chinese tour. Play directly on this page. [Download MP4](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.10/AI-bot-product-intro-20261019-0610-234.mp4) · [Media credits](docs/PRODUCT_VIDEO.md)</sub>

## What it does

<table>
<thead><tr><th width="160" nowrap="nowrap">Feature</th><th nowrap="nowrap">Details</th></tr></thead>
<tbody>
<tr><td nowrap="nowrap"><strong>Task status</strong></td><td nowrap="nowrap">Claude / Codex working, idle and waiting states; completion alerts.</td></tr>
<tr><td nowrap="nowrap"><strong>Account quotas</strong></td><td nowrap="nowrap">Usage, API balances, resets and Codex credits; history on the computer.</td></tr>
<tr><td nowrap="nowrap"><strong>Daily information</strong></td><td nowrap="nowrap">Weather, clocks, stocks, computer load and media playback; dedicated NetEase and QQ Music adapters, Windows media sessions for Soda Music, Apple Music and other clients, and optional Chrome/Edge website playback. See <a href="docs/MEDIA-PLAYERS.md">support and validation limits</a>.</td></tr>
<tr><td nowrap="nowrap"><strong>PC controls</strong></td><td>Adjust volume, mute, lock or unlock from TAB5; follow lock state and open up to four local apps/folders with icons. Wi-Fi unlock automatically installs/imports its optional component.</td></tr>
<tr><td nowrap="nowrap"><strong>Display settings</strong></td><td nowrap="nowrap">Fixed page, cycle or smart switching; choose pages, order, interval and pets.</td></tr>
<tr><td nowrap="nowrap"><strong>Device management</strong></td><td nowrap="nowrap">Windows Device Center: accounts, data, devices; check, download and verify updates. <a href="docs/UPDATES.md">Guide</a></td></tr>
</tbody>
</table>

<table>
<thead><tr><th width="33%" nowrap="nowrap">Computer app</th><th width="33%" nowrap="nowrap">ESP8266 display</th><th width="33%" nowrap="nowrap">TAB5 touch display</th></tr></thead>
<tbody>
<tr>
<td align="center" valign="middle"><img src="docs/assets/screens/device-center.png" width="240" alt="Windows Device Center: My Devices and device settings"></td>
<td align="center" valign="middle"><img src="docs/assets/screens/codex.png" width="180" alt="ESP8266 display: Codex status and quotas"></td>
<td align="center" valign="middle"><img src="docs/assets/screens/tab5-overview.png" width="260" alt="TAB5 overview: task, quotas, clock and weather"></td>
</tr>
<tr><td align="center" nowrap="nowrap">Accounts, devices and updates</td><td align="center" nowrap="nowrap">Status, quotas and daily info</td><td align="center" nowrap="nowrap">Touch controls and overview</td></tr>
</tbody>
</table>

<sub>Current native interfaces with fixed demo data; the small display uses its computer mirror renderer. [Complete feature gallery](docs/FEATURES.zh.md)</sub>

<details>
<summary><strong>Explore TAB5 highlights: Codex Direct, Annual Dots and the floral calendar</strong></summary>

**Agenda and scheduled reminders** are included in TAB5 210: aligned Today/All filtering, centered empty-state creation, local storage, offline edits/deletions and reconnect synchronization, with repeat and advance reminders. Up to 16 items; ended items remain until a full-capacity add evicts the oldest eligible expired one-time entry. [Usage and hardware boundaries](docs/RELEASE-0.6.10.md)

**Codex Direct:** pick one of five recent sessions, read replies, dictate a draft and confirm before sending. Cancel, recover from failures or hold to clear. [Guide](docs/TAB5-QUICK-CONSOLE.md)

<table><tr>
<td align="center" width="33%"><strong>Codex Direct</strong><br><br><img src="docs/assets/screens/tab5-quick.png" width="280" alt="TAB5 Codex Direct: session selection and a dictated draft"></td>
<td align="center" width="33%"><strong>Annual Dots</strong><br><br><img src="docs/assets/screens/tab5-annual.png" width="280" alt="Annual Dots: progress across 365 or 366 days"></td>
<td align="center" width="33%"><strong>Floral calendar</strong><br><br><img src="docs/assets/screens/tab5-floral.png" width="280" alt="Monthly flowers, lunar dates and solar terms"></td>
</tr></table>

Annual Dots gives every day a dot, with leap-year support, automatic day/night colors plus a separate warm-gray choice. The floral calendar includes lunar dates, solar terms and holidays. Neither needs an extra collection. Paintings and calligraphy support landscape and portrait and retain the work title, artist and museum; verified Chinese titles appear on screen, with original collection titles retained otherwise.

[Portrait painting](docs/assets/screens/tab5-painting-portrait.png) · [Portrait calligraphy](docs/assets/screens/tab5-calligraphy-portrait.png) · [Complete screensaver guide](docs/FEATURES.zh.md#tab5)

</details>

<details>
<summary>TAB5 PC controls: volume, mute, lock state and launchers</summary>

![Native TAB5 controls with isolated demo data](docs/assets/screens/tab5-computer-control.png)

Configure up to four app/folder launchers in Windows Device Center → TAB5 → Computer control. Icons accompany names; mute uses a slashed speaker, and lock/unlock state follows the computer. Save up to ten Wi-Fi networks while retaining existing settings. [Usage](docs/COMPUTER-CONTROL.md)

</details>

## Downloads and device pairing

<!-- downloads:start -->
| Use | Version | Download | SHA-256 |
| --- | --- | --- | --- |
| Windows bridge · Windows 10/11 x64 | 0.6.10 | [Download · 27 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.10/AI-bot-bridge-v0.6.10-windows-x64-setup.exe) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.10/SHA256SUMS.txt) |
| Mac bridge · macOS 13+ / Apple Silicon | 0.6.7 | [Download · 14 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.7/AIBotBridge-0.6.7-local-candidate-macos-arm64.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.7/SHA256SUMS.txt) |
| ESP8266 display · first install / upgrade | 0.5.3 | [Download · 30 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.3/AI-bot-0.5.3-firmware-materials.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.3/SHA256SUMS.txt) |
| TAB5 · first install from factory system | 0.2.234-ui | [Download · 5 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/AI-bot-tab5-0.2.234-ui-first-install.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/SHA256SUMS.txt) |
| TAB5 · upgrade existing AI-bot | 0.2.234-ui | [Download · 5 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/AI-bot-tab5-0.2.234-ui-upgrade.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/SHA256SUMS.txt) |
| Daily Calligraphy · standard | 2026.1 | [Download · 869 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.205-ui/AI-bot-DailyCalligraphy-Curated-2026.10.15.1.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.205-ui/SHA256SUMS.txt) |
| Daily Calligraphy · complete HD | 2026.1 | [Download · 2.89 GiB](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#download) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#calligraphy) |
| Daily Painting · standard | 2026.1 | [Download · 191 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.194-ui/AI-bot-DailyPainting-Curated-2026.10.14.1.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.194-ui/SHA256SUMS.txt) |
| Daily Painting · complete HD | 2026.1 | [Download · 1.86 GiB](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#download) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#painting) |

Install the computer bridge, then choose firmware for your device. Artwork collections are optional. Components update independently; reinstalling an unchanged version is unnecessary.

- **Factory TAB5**: choose the first-install ZIP. **Existing AI-bot**: extract the upgrade ZIP and select `aibot_tab5.bin`, keeping its release-notes file beside it. Firmware is not interchangeable between devices.
- **Collections**: import ZIPs directly through Windows Software and Firmware Updates → Artwork Collections. App upgrades preserve collections. Requires Windows bridge 0.6.0+ and TAB5 0.2.145-ui+.
- **Choose an edition**: choose standard or complete HD for each collection. HD includes standard previews and guides, so a separate standard import is unnecessary. Bridge 0.6.7+ displays HD originals; TAB5 uses device previews. Follow the HD size links in the table to download, merge and import. Standard painting is retained.
- Windows supports ESP8266 and TAB5. Mac currently supports ESP8266; TAB5, unified updates and artwork import instructions apply to Windows. Windows setup is unsigned; Mac is ad-hoc signed and not notarized.
- The [unpublished Mac TAB5 candidate](docs/MAC-TAB5-CANDIDATE.md) extends to USB/Wi-Fi/Bluetooth, daily display/settings, resources/gallery, OTA, computer/music, Codex, photos, native local speech and first install/backup/restore, with module Mac CI and candidate-app checks. Hardware, permissions and playback acceptance remain pending. It is not included in the downloads above; distinguish candidate capabilities from published versions.

[Installation guide](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md) · [Collection sizes and licenses](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-PACKS.md) · [Earlier releases](https://github.com/yaoyouzhong/AI-bot/releases)
<!-- downloads:end -->

## Setup and future updates

1. **Install and open the computer app.** On Windows, right-click the tray icon and open Device Center. Existing users should exit the bridge and install to the original location.
2. **Add your device.** Use a USB data cable and follow the [illustrated guide](docs/INSTALL.zh.md) for initial flashing, registration and pairing. Devices already running AI-bot can be verified and added directly.
3. **Choose what to display.** Configure accounts, everyday information and cycling pages. Import a collection before using its TAB5 art screensaver; keep the computer and bridge running during initial synchronization. Completed TF libraries remain available offline.

For later updates, open Windows Device Center → Bridge Settings → Software/Firmware to reach **Software and Firmware Updates** to read each component's notes, download and verify its package, then follow the installation prompts. Enable automatic checks for new-version notifications; installation still requires your confirmation. Download and import artwork collections separately as needed. [Full update steps](docs/UPDATES.md)

## Further reading

- **Use and updates:** [Feature gallery](docs/FEATURES.zh.md) · [Changes by component](docs/RELEASE-0.6.10.md) · [Changelog](CHANGELOG.md) · [TAB5 hardware acceptance](docs/development/TAB5-CONTROL-215-ACCEPTANCE-20261017.md)
- **Data and privacy:** [Data sources](docs/DATA_SOURCES.md) · [Quota-history boundaries](docs/QUOTA_TRENDS.md). Activity statistics extract local status and token metadata; account quotas come from provider interfaces and are counted separately.
- **Development and source:** [Development guide](docs/DEVELOPMENT.md) · [Protocol](docs/PROTOCOL.md) · [TAB5 source and builds](docs/development/TAB5-SOURCE.md) · [Independent component versions](docs/COMPONENT-VERSIONS.md)
- **Materials and licenses:** [Provenance](PROVENANCE.md) · [Third-party notices](THIRD_PARTY_NOTICES.md) · [Screenshots and art credits](docs/SCREENSHOTS.md) · [Optional pet animation pack](docs/assets/pet/README.md)

Own code, including TAB5, is [MIT licensed](LICENSE). Third-party components and artworks retain their original licenses. This independent project has no affiliation with the named AI providers. [Report issues](https://github.com/yaoyouzhong/AI-bot/issues) with your platform, versions, hardware and reproduction steps; remove credentials and private logs.

The Windows 0.6.10 / TAB5 0.2.234-ui release adds computer unlock and automatic TightVNC setup/import without a manually entered component password. Volume-drag recovery, bidirectional Codex read sync, isolated BLE boot verification and battery display after reboot/USB changes have passed hardware acceptance. The installer, firmware and corresponding source are published. [Preparation and acceptance](docs/development/RELEASE-0610-229-CLOSEOUT-20261019.md).
