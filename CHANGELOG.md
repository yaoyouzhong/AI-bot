# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

- Complete MiMo console-session quota routing, strict parsing, persistence, mirror and optional firmware display. Restrict domestic background checks and warnings to selected pages with prior authorization or configured credentials; live MiMo acceptance remains pending.

- Add documented StepFun API-wallet and Baidu Qianfan model-package adapters, credential inputs, failure retention and optional device fields. Cloud wallets are excluded; live-account, hardware and macOS runtime acceptance remain pending. Other catalog providers remain unconnected.

- Prefer official DeepSeek/MiniMax quota APIs and the documented Kimi Code local usage API; add in-app credentials, browser-failure reminders, stale labels and domestic manual refresh. Ali Token Plan and Zhipu wallet keep browser fallback where no matching documented public API is confirmed.

- Add a lunar date line to Windows/macOS screensavers and the ESP8266 screensaver, with leap-month labels, offline device calculation and movement bounds that preserve the PC OFF area. Hardware and macOS runtime acceptance remain pending.

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
