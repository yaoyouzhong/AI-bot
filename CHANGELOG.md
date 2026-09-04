# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

### Features

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

### Security

- Bind the device endpoint to one private adapter and require a DPAPI-protected pairing token.

## 0.1.0 - 2026-09-04

### Features

- Add an independently implemented Windows tray bridge.
- Add an ESP8266 USB status-display firmware.
- Define the `@AIBOT` version 1 line protocol.
- Add CI and tag-driven release scaffolding.
