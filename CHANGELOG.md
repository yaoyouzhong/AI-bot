# Changelog

All notable changes to this project will be documented in this file.

## Unreleased — Windows Device Center

- Withdraw TAB5 firmware candidates 0.2.40/0.2.41 after hardware rollback exposed insufficient receiver stack capacity for compressed-state decoding. Candidate 0.2.42 uses a heap inflater workspace; the bridge stops sending compressed state to affected versions and adds BLE capability version 2. Full hardware acceptance remains required, and the intermittent USB transfer failure remains unresolved.

- Serialize complete TAB5 BLE status, voice and conversation RPC exchanges and retain the latest failure stage. Limit firmware downloads to USB / Wi-Fi while keeping BLE data and conversation features. Format release notes with a heading, bullets, aligned wrapping and scrolling. The 0.2.41 candidate shows received KB from the first download fragments; hardware acceptance is recorded separately.

- Recover from an IOException while disposing an unplugged TAB5 serial port instead of losing the reconnect worker or failing app shutdown. Add 250 ms system-metrics frames, compressed full state and legacy fallback for the 0.2.40 candidate; reduce idle BLE mailbox contention. Actual BLE cadence and firmware behavior still require hardware acceptance.

- Add authenticated USB / BLE RPC for the TAB5 0.2.39 candidate: firmware ranges, history, drafts, send receipts and chunked image uploads, preserving send deduplication and image validation. Older firmware needs an initial USB or Wi-Fi upgrade. Builds and simulations pass; hardware acceptance remains pending.

- Remove device-name hover tooltips from the tray menu so they cannot cover open submenus.
- Fix Codex completion notices holding the task page after the visible notice ends. Limit the interruption to 12 seconds; explicitly selecting Automatic Cycling in the preview or Display Settings dismisses the existing completion notice while preserving pages, order and interval. Pending input requests still show their interruption reason.
- Keep the preview open across focus changes and page-menu dismissal; retain explicit close, Escape and tray toggling. Add press feedback, a blue checked running state and started/restarted confirmation to Automatic Cycling, without reporting failed actions as successful.

- Redesign the preview popup with a unified light surface, single-row page navigation, an on-demand page menu, cycle progress and a slim brightness slider. Replace the radio button that ignored clicks when already selected with an explicit Automatic Cycling action that saves and restarts cycling on every click. Show the actual cycle position, countdown or task-alert interruption. Regression coverage includes repeated clicks, menu selection, previous/next navigation and keyboard brightness adjustment.

- Combine small-screen brightness, display mode and cycling in Display Settings without Apply buttons. Coalesce rapid slider changes and send the final pending value when closing. Read actual brightness without writing settings on opening; report send failures.
- Keep the preview page menu synchronized with checked cycle pages and their order, with Automatic Cycling resuming cycling and a fixed popup height regardless of page count. Remove the duplicate preview card. Order small-screen cards as display, appearance, data and connection; move upgrades into Manage and the gallery into Appearance. Order TAB5 cards as voice, calendar and birthdays, data, and connection and updates.

- Add per-screen Automatic, USB-only and Wi-Fi-only settings for ESP8266, defaulting to Automatic and applying without restarting device services or affecting TAB5. Wi-Fi only suppresses USB status, metrics and resources while retaining pairing/diagnostics. USB only blocks legacy LAN data and discovery. Connection status follows actual data activity; invalid HTTP routes no longer count as Wi-Fi heartbeats.

- Use the same two-column action cards in My Devices, Accounts and Data, and Bridge Settings; shorten the Chinese Weather and Location label.
- Rename Display Content / Data Selection to Data Settings. Explain the relationship between data categories and domestic model providers, show Chinese and English names with quota types, and disable provider choices without losing selections when model quotas are off.
- Keep Add responsive at the two-device limit, showing registered models, the one-per-model limit and replacement steps.

- Show device features after registration. A tray left-click opens the preview when an ESP8266 is enabled, otherwise Device Center. The first version supports one TAB5 and one ESP8266.
- Add a versioned registry, existing-pairing migration, explicit legacy-screen import, independent disable/remove operations and shared data-demand merging. Removal preserves settings, pairing recovery files and user content.
- Separate TAB5 data demand from ESP8266 display pages. Unregistered hardware does not start its USB/BLE/voice services; disabling one device does not restart the other.
- Bind device windows to a fixed target and block removal during recording, updates or installation. Legacy flashing/pairing entry points honor registration. Replacing retained TAB5 pairing requires confirmation in the add wizard and preserves an encrypted backup.

- Offer Device Center, screen preview, common settings grouped by registered device, and Exit in the tray menu. Keep shared accounts and data settings in Device Center. Preserve custom names, gray out disabled devices and bind each shortcut to its device. Right-click preview always opens it; left-click still toggles it. Keep full management and diagnostics in Device Center. Use the default name M5Stack TAB5 and remove duplicate model text from the device list.
- Use four-character Chinese labels for Accounts and Data and common Device Center actions.
- Put the small-screen brightness slider first in Display Settings and read the actual device brightness on opening; keep brightness controls in the preview popup.
- Fix a collapsed brightness-slider container under scaling. Connection settings show actual USB status without a COM input; align appearance controls by weather, shared pet, Claude and Codex.
- Arrange common Device Center actions in two columns, display full default device names and compact connection status; move small-screen information, diagnostics and network reset into Manage.
- Center settings windows in the current screen work area and avoid using a closed Device Center as the owner of birthday, voice or update dialogs, preventing disposed-object errors.
- Remove manual refresh and the quota-collection toggle. Collect quotas and record Codex history while the bridge runs; show service status as connection/data tables and move About into Bridge Settings.
- Show only the enable toggle and preferred microphone in voice settings by default. Move legacy shortcuts, recognition tests and audio checks into Troubleshooting while preserving microphone restoration.
- Recover changed TAB5 USB instance identifiers only after probing and matching the paired device ID. Preserve the pairing key and reject unrelated devices.
- Fix ESP8266 USB disconnections when shared weather included TAB5 hourly/daily forecasts and exceeded the 6144-byte heartbeat limit. Preserve current weather on the small screen and full TAB5 forecasts; expose the failed connection stage.

Windows builds, isolated migration, simulated service/protocol tests, installer fault tests and layout scaling checks passed. The September 29 revision restored TAB5 USB/BLE acknowledgements and confirmed ESP8266 bridge-online state, increasing USB counters and rendered cycle pages. No hardware flashing performed; full three-transport switching, three hardware combinations and actual cross-monitor DPI acceptance remain pending. See the [validation notes](docs/DEVICE-CENTER-VALIDATION.zh.md) (Chinese).

## 0.4.0 - 2026-09-23

- Keep the last valid device page visible for up to 30 seconds during brief host stalls while the eight-second USB-to-Wi-Fi fallback and offline diagnostics remain intact. Explicit host shutdown still shows offline immediately.
- Reduce Windows background activity scans under load so serial heartbeats and status updates have more room to run.
- Rebind the Windows LAN listener when the laptop's network address changes, choosing an adapter on the device's subnet and updating the device over USB when available.
- Add paired, HMAC-authenticated local discovery for Windows and ESP8266. When either or both IP addresses change without USB, the device can find the bridge on the current local subnet; it saves a new address only after an authenticated status request succeeds.

Windows Release and ESP8266 firmware builds passed. On the real device, a five-minute Wi-Fi-only status run, discovery from an obsolete bridge port, ordinary Wi-Fi fallback and USB recovery passed. A loopback test changed both simulated endpoint addresses. Simultaneous real DHCP address changes with the device on independent power remain unverified. Discovery requires reachable local broadcast and is not yet supported by the macOS bridge. Install both the new Windows bridge and firmware for address discovery; the Windows installer remains unsigned and the Apple Silicon app remains ad-hoc signed and not notarized.

## 0.3.0 - 2026-09-21

- Pause only USB during flashing/backup and resume on completion or flasher disconnect; keep the resident bridge and network listeners running without restarting any executable. Refine Windows typography, spacing, button states and collapsed details.
- Limit flashing progress display/log updates to once per second and stop stalled read-only backups after 30 seconds without output before compatibility retry.
- Stream backspace-delimited backup progress immediately; use 460800 baud with read-only compatibility fallback before writing.
- The flasher selects a single connected USB device without replugging, retains device identity across scan errors, uses a dedicated icon and connection panel, and bundles verified tools for offline flashing.
- Automatically select a single connected USB device and request unplug/replug only when multiple devices are ambiguous; hide technical port names in collapsed details, exclude Bluetooth on Windows, and stop if the selected USB identity changes.
- Add native Windows and Apple Silicon Mac firmware-flashing windows: automatically identify the device, select firmware ZIP/BIN, and use the bundled hash-pinned official tool to back up the full flash, write and read back for verification. Release USB ownership during flashing and restore the USB connection afterward.
- Consolidate download, installation, firmware and first-connection instructions into one illustrated guide.

Earlier Windows flashing and backup throughput were verified on hardware. The new USB-only handoff passed local regressions; hardware acceptance is pending. The new Mac window passed macOS CI tests and build, while Mac hardware acceptance remains pending. Published as a release; the acceptance limitations above still apply. Windows setup is unsigned; the Apple Silicon app is ad-hoc signed and not notarized. Devices already running v0.2.2 firmware do not need reflashing for this update.

## 0.2.2 - 2026-09-21

- Fix completion/input alerts overriding manually selected pages on the device and mirrors; automatic mode retains alert navigation. Device diagnostics now distinguish selected, effective and rendered pages.

- Windows: request real-time Hong Kong quotes, preserve three-decimal prices and index precision, and recover missing symbols individually without erasing cached quotes.
- Windows: queue display selections without blocking the UI, prioritize the latest selection between USB resource chunks, and prevent queued heartbeats from restoring an older selection.

Update both the bridge and ESP8266 firmware to receive the manual page-selection fix. Windows/device behavior has been verified on hardware and confirmed by the user. macOS shares the display-policy fix; interactive Mac acceptance remains pending. Windows setup is unsigned; the Apple Silicon app is ad-hoc signed and not notarized.

## 0.2.1 - 2026-09-17

### Windows fixes

- Retry transient DeepSeek balance failures once, renew idle connections between refresh rounds, and distinguish authentication, access, rate-limit and network errors while retaining the last successful balance.
- Wait for automatic recovery before showing quota warnings; cancel pending warnings after success and preserve specific failure reasons.
- Add bounded, credential-free API and browser-stage diagnostics to distinguish login redirects, capture timeouts and successful balance saves.
- Retry local and LAN listener startup when a port is temporarily unavailable. Persistent conflicts can use the existing AIBOT_HTTP_PORT override; USB pairing passes the selected port to the device.

### Validation and limitations

- Windows Release build, isolated public self-tests and status capture passed. Live DeepSeek refresh, Zhipu login followed by background balance refresh, USB device connectivity and preserved automatic cycling were verified.
- Zhipu still requires a valid embedded-browser login; login retention across another restart has not been verified. A port held by another process is not forcibly reclaimed.
- macOS and firmware behavior are unchanged from v0.2.0; this release does not require reflashing an already working v0.2.0 device. Their packages are rebuilt with this version.
- Pre-release. Windows setup remains unsigned; macOS is Apple Silicon/macOS 13+, ad-hoc signed and not notarized. Previously documented provider and platform acceptance limits remain in effect.

## 0.2.0 - 2026-09-16

- Complete MiMo console-session quota routing, strict parsing, persistence, mirror and optional firmware display. Restrict domestic background checks and warnings to selected pages with prior authorization or configured credentials; live MiMo acceptance remains pending.

- Add documented StepFun API-wallet and Baidu Qianfan model-package adapters, credential inputs, failure retention and optional device fields. Cloud wallets are excluded; live-account, hardware and macOS runtime acceptance remain pending. Other catalog providers remain unconnected.

- Prefer official DeepSeek/MiniMax quota APIs and the documented Kimi Code local usage API; add in-app credentials, browser-failure reminders, stale labels and domestic manual refresh. Ali Token Plan and Zhipu wallet keep browser fallback where no matching documented public API is confirmed.

- Add a lunar date line to Windows/macOS screensavers and the ESP8266 screensaver, with leap-month labels, offline device calculation and movement bounds that preserve the PC OFF area. Hardware and macOS runtime acceptance remain pending.

- Fix macOS 13 lunar-calendar compilation while preserving leap-month support.
- Improve domestic settings layout and long provider labels at high DPI; synchronize the nine-image homepage gallery and setup guides.

Pre-release: Windows adapters are included; new provider live-account acceptance and physical-device acceptance remain pending. macOS includes lunar dates and protocol compatibility, not the new Windows-only credential adapters. Install updated firmware for lunar dates and additional device pages. Windows setup remains unsigned; macOS is ad-hoc signed and not notarized.

## 0.1.3 - 2026-09-16

### Windows installation

- Add a Chinese online setup wizard, about 8 MB. Detect .NET 8 Desktop Runtime and WebView2; skip installed prerequisites and download missing components automatically.
- Show .NET download progress, support cancellation and retries, and verify the pinned SHA-256 before execution. Create shortcuts and preserve user settings and shared runtimes on uninstall.
- Include the setup EXE and checksums in release packaging. Compile-time download tests cover valid/corrupted/404 responses and detection without downloads.

### Guides

- Put the recommended Windows download first and add illustrated download selection, installation, firmware backup and flashing guides. Keep ZIP/manual and source-build instructions separate.
- Synchronize Chinese/English homepages, show six feature screenshots in a two-by-three grid, and update distribution notices and acceptance criteria.

This is a pre-release. The setup is unsigned; clean-machine prerequisite installation, visual acceptance, upgrade and uninstall remain unverified. Missing runtimes require internet access. Existing working firmware does not need reflashing for this installer update.

## 0.1.2 - 2026-09-16

Windows maintenance pre-release. Upgrade by exiting the bridge, extracting the entire new archive and retaining existing user AppData.

### Quota trends and window fixes

- Show verified daily quota increments even for incomplete days, with explicit coverage notes; missing intervals remain unknown and incomplete days do not enter the full-day average.
- Tolerate small reset-deadline jitter without hiding valid usage or falsely reporting resets. Retain checks for actual usage drops, changed windows and offline gaps.
- Retry transient history-save failures on the next sample while preserving unreadable original history files.
- Fix clipped mirror connection information at high DPI. Compact and center the trend window, improve table columns and chart labels, and clarify quota units.
- Fix the first trend-window opening being suppressed by hidden process startup; verify native window visibility before activation.

### Other Windows improvements

- Keep Windows USB heartbeats, LAN status and the tray responsive during slow session-log scans by publishing cached activity from a single background scan. Preserve the last successful sample on failure, skip unchanged lifecycle files, and expose loopback activity/device diagnostics.

- Add an About AI-bot tray menu with the running application's version, author yaoyouzhong & Codex, and a clickable GitHub repository link.

### Validation and limitations

- Windows build, status capture, quota regressions, 175% layout checks and hidden-start first-opening reproduction were verified locally. Live quota samples were saved after restart.
- Missing historical samples cannot be reconstructed. Recorded daily use may be lower than the full-day total.
- macOS and firmware behavior are unchanged; packages are rebuilt with the release. macOS 13+ Apple Silicon only, ad-hoc signed and not notarized; real-Mac acceptance remains pending. No new full physical-device acceptance is claimed.
- Windows requires .NET 8 Desktop Runtime x64 and Microsoft Edge WebView2 Runtime. Packages include license notices and SHA-256 checksums.

## 0.1.1 - 2026-09-11

First public GitHub pre-release. Version 0.1.0 was a development baseline; this release includes accumulated desktop, firmware and packaging work as well as the Windows startup fixes.

### Windows fixes

- Use a current-user interactive logon task with a five-second delay, avoiding the ordinary Run startup queue. No password or elevation; battery operation and unlimited runtime are supported. Toggle an older startup entry off and on once to migrate it, and register again after moving the app.
- Prevent duplicate bridge instances before initializing providers or opening USB/HTTP connections.
- Simplify tray hover text to the app name and click instructions, preserving display-page activity states. Normal startup does not flash a console.

### Included capabilities

- Windows tray and mirror with Claude/Codex activity, account quotas, local token accounting, completion/attention signals and Codex quota trends; domestic authorization for Alibaba, Kimi, MiniMax, DeepSeek and Zhipu.
- Weather, stocks, system metrics, music, automatic cycling and screen saving; retain last-successful provider data when requests fail.
- ESP8266 USB-first transport at 460800 baud, authenticated Wi-Fi fallback, standalone clock, device controls and acknowledged binary-resource transfer. Improve UART buffering, page decoding and RGB565 rendering.
- Original BYTE SPROUT pet, per-role animation selection/import and persistent resources. Private account data and personal artwork are excluded.
- Apple Silicon macOS test app with menu bar, mirror, activity/quotas, weather, stocks, metrics, music, pets and device controls, subject to platform limits.
- Bilingual installation/flashing guides and Windows, Mac, source and firmware-materials archives with license notices and SHA-256 checksums.

### Validation and limitations

- Locally verified Windows Release build, console-host status diagnostics, startup-task launch and three duplicate launches. Cycle pages/order/interval were preserved. A fresh reboot/logon and actual mouse-hover appearance were not observed in this acceptance run.
- Cloud build/tests and archive checks must pass before publication; automated tests do not establish real-account or physical-device acceptance.
- Mac requires macOS 13+ and Apple Silicon. It is ad-hoc signed, not Developer ID signed or notarized. Real-Mac first launch, permissions and device behavior remain unverified; this package does not support Intel.
- No ESP8266 USB port was available during the latest startup-fix acceptance. Complete page-by-page and Wi-Fi fallback acceptance remains pending. The protocol version is unchanged.
- Windows requires .NET 8 Desktop Runtime x64 and Microsoft Edge WebView2 Runtime. Extract the entire ZIP, stop the bridge before upgrading and preserve user AppData.

## 0.1.0 - 2026-09-04

### Features

- Add an independently implemented Windows tray bridge.
- Add an ESP8266 USB status-display firmware.
- Define the `@AIBOT` version 1 line protocol.
- Add CI and tag-driven release scaffolding.
