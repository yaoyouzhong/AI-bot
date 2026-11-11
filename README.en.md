# AI-bot

**Your AI status, all on one screen.** AI-bot is a desktop AI companion that works with your computer, bringing AI task status, account quotas across providers and everyday information to a dedicated display, with fewer trips between windows. Follow Claude Code / Codex progress and view plan usage or API balances for Chinese AI services including DeepSeek, Zhipu, Kimi, MiniMax and Alibaba Model Studio.

Beyond work, AI-bot serves as a desk clock and information display for weather, stocks, music and computer activity. Optional daily painting and calligraphy collections turn the TAB5 into an art display during quieter moments.

[Downloads and device pairing](#downloads-and-device-pairing) · [Product video](#product-video) · [Complete downloads](DOWNLOADS.en.md) · [Illustrated first install](docs/FIRST-INSTALL.zh.md) · [Release notes](docs/RELEASE-0.6.19.md) · [简体中文](README.md)

**Source:** [Windows bridge](windows-app/) · [Mac bridge](mac-app/) · [TAB5 firmware](tab5/) · [ESP8266 firmware](esp8266/)

**This update:** Windows and Mac bridge **0.6.19** prefers the compact ESP8266 flash package and verifies it before handoff. A bridge update does not require reflashing either device. [Bridge release notes](docs/RELEASE-0.6.19.md) · [Installation guide](docs/INSTALL.zh.md)

**TAB5 0.2.319-ui** improves recent conversation navigation, Overview rendering and calendar colors, and adds the Claude working animation with headphones. [Firmware release](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.319-ui)

<p><img src="docs/assets/screens/tab5-companion-savers-headphones-preview.png" width="880" alt="Codex and the headphone-wearing Claude screensavers"></p>
<sub>The screensavers were rendered separately with a demonstration clock. On the right, Claude wears headphones while working in the TAB5 0.2.319-ui candidate. <a href="licenses/companion-art/NOTICE.md#native-preview-media">Image sources</a></sub>

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
<p>A desktop device with a 5-inch touchscreen. View status and quotas, continue Codex and Claude conversations, dictate messages, add photo attachments and manage reminders, with clocks, paintings and calligraphy for everyday display.</p>
<p><strong>Connections:</strong> USB / Wi-Fi / Bluetooth<br><strong>Computer support:</strong> Windows / macOS<br><strong>Best for:</strong> touch interaction, task handling and a wider range of display options.</p>
<p><a href="docs/INSTALL.zh.md#tab5-first">Setup and connection</a> · <a href="docs/FEATURES.zh.md#tab5">Interface gallery</a></p>
</td>
</tr>
</table>

<sub>Both pictures combine hardware imagery with the AI-bot interface and demo data; they are not hardware photographs or shown to a common physical scale. <a href="docs/HARDWARE-IMAGES.md">Image sources and notes</a></sub>

## Product video

This project introduction compares the shared features of TAB5 and the ESP8266 display, then presents TAB5 highlights including overview, conversations, voice drafts, camera attachments, computer controls, agenda and art screensavers. Native interface footage uses demonstration data and composite hardware images, not hardware photography.

Play the November 11, 2026 edition below (2:08). At 1:51, the desktop viewer shows Monet's *Water Lilies* and Su Shi's *The Cold Food Observance* fullscreen, then zooms in on the details.

https://github.com/user-attachments/assets/dd551941-fcd4-4a75-8466-b89158db99b8

[Web MP4](https://github.com/user-attachments/assets/dd551941-fcd4-4a75-8466-b89158db99b8) · [1080p MP4](https://github.com/user-attachments/assets/c59ef11c-2a93-4a07-b5a4-505361062766) · [Media credits](docs/PRODUCT_VIDEO.md)

## What it does

<table>
<thead><tr><th width="160" nowrap="nowrap">Feature</th><th>Details</th></tr></thead>
<tbody>
<tr><td nowrap="nowrap"><strong>AI task status</strong></td><td>Both displays show Claude / Codex working, idle and waiting states, with completion alerts.</td></tr>
<tr><td nowrap="nowrap"><strong>Model quotas</strong></td><td>Account usage, API balances, reset times and Codex reset cards; quota history on the computer.<br>Available fields depend on the provider and account authorization.</td></tr>
<tr><td nowrap="nowrap"><strong>Codex/Claude dialogue</strong></td><td>On TAB5, select a project and conversation, read public replies/history, and keep separate encrypted drafts.<br>Use voice input, add camera attachments and hold to clear text; review and confirm before sending.</td></tr>
<tr><td nowrap="nowrap"><strong>Agenda &amp; calendar</strong></td><td>TAB5 offers tasks, recurring and due reminders, a monthly calendar, lunar dates, solar terms and birthdays.</td></tr>
<tr><td nowrap="nowrap"><strong>Weather &amp; stocks</strong></td><td>Both displays show weather and watchlists; TAB5 also shows hourly and seven-day forecasts.</td></tr>
<tr><td nowrap="nowrap"><strong>Music &amp; PC status</strong></td><td>View media details, artwork and computer CPU, memory and network activity.<br>TAB5 can control supported players; availability varies by Windows, Mac and player. <a href="docs/MEDIA-PLAYERS.md">Media support</a></td></tr>
<tr><td nowrap="nowrap"><strong>Computer control</strong></td><td>TAB5 can adjust volume, mute, lock, unlock and open configured shortcuts.<br>Unlock setup and permissions differ on Windows and Mac. <a href="docs/COMPUTER-CONTROL.md">Guide</a></td></tr>
<tr><td nowrap="nowrap"><strong>Clocks &amp; screensavers</strong></td><td>Both devices show a clock. TAB5 adds clock faces, countdowns, annual dots, a floral calendar and landscape or portrait viewing for paintings and calligraphy.<br>Install the <a href="docs/GALLERY-HD.md">HD collections</a> to view original paintings and calligraphy fullscreen on your computer, zoom in on details and read the artwork guides.</td></tr>
<tr><td nowrap="nowrap"><strong>Pets &amp; display</strong></td><td>Show pets with AI activity; choose a fixed page, cycling or smart following, with page order and interval.</td></tr>
<tr><td nowrap="nowrap"><strong>Devices &amp; monitoring</strong></td><td>Manage connections and independent updates: ESP8266 uses USB / Wi-Fi; TAB5 also supports Bluetooth.<br>Windows runtime monitoring and TAB5 diagnostics show status, causes and advice. <a href="docs/RUNTIME-MONITOR.md">Runtime monitor</a></td></tr>
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

<sub>Current native interfaces with fixed demo data; the small display uses its computer mirror renderer. <a href="docs/FEATURES.zh.md">Complete feature gallery</a></sub>

[Native Mac gallery](docs/FEATURES.zh.md#mac-native): new 0.6.15 authorization guides plus management-window captures retaining their historical provenance, all using native controls and isolated demo data.

<details>
<summary><strong>Explore TAB5 highlights: shared dialogue, Codex Direct and calendars</strong></summary>

**Agenda and scheduled reminders** are included in TAB5 210: aligned Today/All filtering, centered empty-state creation, local storage, offline edits/deletions and reconnect synchronization, with repeat and advance reminders. Up to 16 items; ended items remain until a full-capacity add evicts the oldest eligible expired one-time entry. [Usage and hardware boundaries](docs/RELEASE-0.6.11.md)

**Dialogue workspace:** choose a Codex/Claude project and conversation, manage drafts/photos, and enter the exact current or unread conversation from Overview. Supported native Claude sessions can be located and controlled; sending is guarded when the target cannot be confirmed. Tool requests require computer approval. [Native interface preview with synthetic data](docs/assets/screens/tab5-dialogue-0318-r13-preview.png)

**Codex Direct:** choose a recent session and finish recording to fill the computer input automatically. Review or edit on the computer, then confirm from TAB5. Supports question cards, continued dictation, failure recovery and hold to clear. [Guide](docs/TAB5-QUICK-CONSOLE.md)

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
| Windows bridge · Windows 10/11 x64 | 0.6.19 | [Download · 31 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.19/AI-bot-bridge-v0.6.19-windows-x64-setup.exe) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.19/SHA256SUMS.txt) |
| Mac bridge · macOS 13+ / Apple Silicon | 0.6.19 | [Download · 19 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.19/AI-bot-bridge-v0.6.19-macos-arm64.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.19/SHA256SUMS.txt) |
| ESP8266 display · first install / upgrade | 0.5.8 | [Download · 384 KiB](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.8/AI-bot-esp8266-0.5.8-flash.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.8/AI-bot-esp8266-0.5.8-flash.zip.sha256) |
| TAB5 · first install from factory system | 0.2.319-ui | [Download · 5 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.319-ui/AI-bot-tab5-0.2.319-ui-first-install.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.319-ui/SHA256SUMS.txt) |
| TAB5 · upgrade existing AI-bot | 0.2.319-ui | [Download · 5 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.319-ui/AI-bot-tab5-0.2.319-ui-upgrade.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.319-ui/SHA256SUMS.txt) |

[Illustrated first install](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/FIRST-INSTALL.zh.md) · [English quick start](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md#english-quick-start)
<!-- downloads:end -->

[Artwork downloads and import](docs/GALLERY-PACKS.md#english)

- **Collections**: import ZIPs directly through Windows Software and Firmware Updates → Artwork Collections. App upgrades preserve collections. Requires Windows bridge 0.6.0+ and TAB5 0.2.145-ui+.
- **Choose an edition**: choose standard or complete HD for each collection. HD includes standard previews and guides, so a separate standard import is unnecessary. Bridge 0.6.7+ displays HD originals; TAB5 uses device previews. Follow the HD size links in the table to download, merge and import. Standard painting is retained.

## Setup and future updates

1. **Install and open the computer app.** On Windows, right-click the tray icon and open Device Center. Existing users should exit the bridge and install to the original location.
2. **Add your device.** Use a USB data cable and follow the [illustrated first-install guide](docs/FIRST-INSTALL.zh.md) for initial flashing, registration and pairing. Devices already running AI-bot can be verified and added directly.
3. **Choose what to display.** Configure accounts, everyday information and cycling pages. Import a collection before using its TAB5 art screensaver; keep the computer and bridge running during initial synchronization. Completed TF libraries remain available offline.

For later updates, open Windows Device Center → Bridge Settings → Software/Firmware to reach **Software and Firmware Updates** to read each component's notes, download and verify its package, then follow the installation prompts. Enable automatic checks for new-version notifications; installation still requires your confirmation. Download and import artwork collections separately as needed. [Full update steps](docs/UPDATES.md)

## Further reading

- **Use and updates:** [Feature gallery](docs/FEATURES.zh.md) · [Changes by component](docs/RELEASE-0.6.19.md) · [Changelog](CHANGELOG.md) · [TAB5 hardware acceptance](docs/development/TAB5-CONTROL-215-ACCEPTANCE-20261017.md)
- **Data and privacy:** [Data sources](docs/DATA_SOURCES.md) · [Quota-history boundaries](docs/QUOTA_TRENDS.md). Activity statistics extract local status and token metadata; account quotas come from provider interfaces and are counted separately.
- **Development and source:** [Development guide](docs/DEVELOPMENT.md) · [Protocol](docs/PROTOCOL.md) · [TAB5 source and builds](tab5/) · [Independent component versions](docs/COMPONENT-VERSIONS.md)
- **Materials and licenses:** [Provenance](PROVENANCE.md) · [Third-party notices](THIRD_PARTY_NOTICES.md) · [Screenshots and art credits](docs/SCREENSHOTS.md) · [Optional pet animation pack](docs/assets/pet/README.md)

Own code, including TAB5, is [MIT licensed](LICENSE). Third-party components and artworks retain their original licenses. This independent project has no affiliation with the named AI providers. [Report issues](https://github.com/yaoyouzhong/AI-bot/issues) with your platform, versions, hardware and reproduction steps; remove credentials and private logs.

[First-device setup video](docs/ONBOARDING_VIDEO.md): Windows onboarding and the Mac USB Pairing path.
