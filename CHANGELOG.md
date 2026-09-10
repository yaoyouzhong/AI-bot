# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

- Add illustrated installation/flashing guides and an offline interface gallery. Use built-in BYTE SPROUT when no custom pet exists in Windows, firmware and Mac source; preserve custom selections. Windows build/public regressions and firmware build pass; new firmware is not deployed and Mac remains unverified.

- Redesign the bilingual repository homepage with original device illustrations, a concise feature overview, verified platform status and installation links; move detailed reference material into dedicated documents.

- Close out public source and distribution materials: recheck migration provenance, exclude private/generated files and unreviewed artwork, correct quota-trend semantics, and pin the currently resolved firmware dependencies.
- Include original Windows SDK terms, official REDIST evidence and DLL hash checks; Windows candidates carry dependency notices/terms, firmware materials include corresponding sources/licenses/rebuild configuration, and source archives carry SHA-256 manifests. No push, release or complete product acceptance is implied.

- Add local Codex quota observations and a separate trend window without changing device pages; handle resets, missing samples and damaged history. Retry missing private pet resources when opening the mirror, without overwriting existing selections.

- Add isolated Windows candidate packaging with NuGet license texts, installation guidance, per-file/ZIP checksums and extracted-package regressions. Ship only the synthetic three-pixel import fixture, never private pet caches.

- Build the Windows bridge as WinExe and remove startup console detachment to prevent terminal flashing. Run automated diagnostics through the dotnet console host to preserve output and exit-code checks.

- Add a fresh-profile public regression entry point, isolated local-build script, limited current/history content guard, and Windows/macOS CI checks. Reconcile release-readiness documentation; no version bump or publication. Historical deployment statements below describe their original implementation stages, not current runtime status.

- Add attention/completion acknowledgement, local Token accounting, automatic priority/wake and authenticated Wi-Fi resource synchronization, with loopback HTTP/CRC/activity/accounting regressions. Align music geometry and weather seconds; fix legacy pre-swapped pet colors and TFT RGB565 byte order.
- Integrate macOS gallery, mirror, role caches/restoration, events/accounting, cycling and XCTest cases. Windows/firmware validation is local only; Mac remains uncompiled/unverified, with no stable-device replacement or release.

- Add an independently implemented petdex picker with legacy search/preview/role/motion layout, bounded downloads, cached manifest fallback, native Windows WebP decoding and per-role persistence. Verify real Boba decoding and nine-motion/two-role offline fixtures; no USB deployment.

- Preserve original-size local Claude/Codex pets in APET v2, add independent firmware slots, role-specific import/reset menus, restart persistence and recoverable selection backups. Validate slot isolation and restore resource revisions offline; no device deployment.

### Page data decoding and familiar tray navigation

- Migrate provenance-reviewed maintainer weather and domestic authorization code, geometric weather animations, robot icon and synthesized completion cue into isolated AI-bot adapters. Keep the running legacy app/firmware unchanged.
- Restore single/dual quotas, provider selection, cycle settings, countdowns, stock layout/paging, network graphs, GIF frame playback and persistent Windows pet pixels; add offline rendering, policy/cache/lifecycle/animation tests. Default sprite and per-state parity remain incomplete.
- Add an isolated legacy-weather layout preview and reject unknown CLI commands without starting a bridge. The new repository's renderer is updated; no candidate is deployed.

- Fix read-only ArduinoJson object/array checks that silently skipped weather, stocks, quota, system and music data in firmware.
- Read back decoded page state in hardware acceptance tests; add a separate production-runtime live-data test.
- Restore legacy tray grouping and left-click mirror toggle, sharing persistent display policy with device controls and LAN snapshots.

### Hardware fixes and legacy screen saver

- Allocate an 8192-byte ESP8266 UART receive buffer before serial initialization to retain complete JSON/resource frames during synchronous drawing and flash writes.
- Add real-device resource/page/brightness/USB-expiry acceptance tests with explicit failure reporting and restoration.
- Restore the legacy 204×76 cyan seven-segment screen-saver clock, yellow colon, calendar/weekday and five-second movement in firmware and the Windows mirror.

### Recent baseline alignment

- Display every Codex reset-credit expiry in firmware and the Windows mirror, preserving duplicate dates and paginating long lists; reserve a separate pet footer to prevent animation overwrite.
- Preserve last-known reset-credit dates as stale when the count is unchanged and details are unavailable, on Windows and macOS.
- Align balance/currency baselines with differentiated font sizes; cache Windows network interfaces for 30 seconds and calculate per-interface deltas without overlapping samples.
- Record the exact legacy working-tree baseline and remaining validation boundaries in `docs/ALIGNMENT_2026-09-08.md`.

### Features

- Add LAN-independent USB device information and explicitly confirmed Wi-Fi reset on Windows/macOS, with correlated replies and no destructive retry.
- Add powered USB traffic-pause fallback tests, device USB/LAN counters, automatic resume and Windows hardware/synthetic test commands.

- Add authenticated USB-provisioned Wi-Fi fallback and standalone clock behavior.
- Add Open-Meteo weather and A/H/US stock data with last-successful caches.
- Add weather/stock device pages, automatic cycling, paging, and Windows screen-saver control.
- Add Claude/Codex account-quota parsing, last-successful caching, and a device quota page.
- Add normalized Alibaba/Kimi/MiniMax/DeepSeek quota parsing, MiniMax API refresh, and a device summary page.
- Add live Windows CPU, memory, upload, and download metrics with a device system page.
- Add Windows media-session metadata, progress, a music page, and AUTO playback override.
- Add the original geometric `BYTE SPROUT` activity pet without imported sprite assets.
- Wake automatic screen saving temporarily for new AI work or music, then restore the prior mode after input.
- Add COBS binary-resource framing, per-chunk and whole CRC32, ACK retry, and validated LittleFS replacement.
- Send pre-rendered CJK music text and cover RGB565 resources and render them row-by-row from LittleFS.
- Add license-gated runtime pet image import, RGB565 conversion, USB transfer, and device rendering.
- Add pre-rendered CJK weather text and paged stock-name resources with symbol fallback.
- Add the independent macOS menu-bar, activity reader, Keychain pairing token, and authenticated LAN status foundation.
- Add a nine-page 240×240 Windows mirror, synthetic PNG visual test, and mode/brightness control window.
- Add isolated WebView2 sign-in capture for Alibaba, Kimi, MiniMax, and DeepSeek display-only quota fields.
- Add a Windows settings dialog and allow-listed persistence for weather, stocks, screen saving, and the preferred serial port.
- Add macOS Open-Meteo weather, A/H/US stocks, last-successful caches, and non-secret data-source settings.
- Add macOS CPU, physical-memory, and active-interface traffic sampling through public Darwin/Mach APIs.
- Add macOS Claude/Codex account-quota parsing, fixed official-host requests, and display-only caching.
- Add macOS USB-serial discovery, a 460800-baud control-frame transport, Keychain-generated pairing, and Wi-Fi fallback provisioning.
- Add macOS menu controls for device pages, brightness, and explicit Wi-Fi fallback reprovisioning.
- Add macOS idle-time screen-saver entry with restoration of the previously selected device page.
- Add the macOS COBS/CRC32 binary-resource protocol with per-chunk ACK retries.
- Render macOS weather and stock labels into revision-deduplicated RGB565 device resources.
- Add license-gated macOS pet import with first-frame scaling and USB delivery.
- Add opt-in macOS Apple Music/Spotify metadata, progress, localized text delivery, and an Automation-ready development app bundle.
- Add bounded macOS current-artwork decoding and 112×112 RGB565 cover delivery with stale-cover clearing.
- Add macOS automatic-screen-saver temporary wake for new music playback and AI work.
- Add USB discovery of the device's private address plus authenticated macOS device information and confirmed Wi-Fi reset.

### Security

- Bind the device endpoint to one private adapter and require a DPAPI-protected pairing token.
- Restrict macOS device-administration requests to fixed paths on the USB-discovered RFC1918 address.

## 0.1.0 - 2026-09-04

### Features

- Add an independently implemented Windows tray bridge.
- Add an ESP8266 USB status-display firmware.
- Define the `@AIBOT` version 1 line protocol.
- Add CI and tag-driven release scaffolding.
