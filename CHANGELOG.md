# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

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
