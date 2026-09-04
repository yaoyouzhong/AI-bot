# Changelog

All notable changes to this project will be documented in this file.

## Unreleased

### Features

- Add authenticated USB-provisioned Wi-Fi fallback and standalone clock behavior.
- Add Open-Meteo weather and A/H/US stock data with last-successful caches.
- Add weather/stock device pages, automatic cycling, paging, and Windows screen-saver control.
- Add Claude/Codex account-quota parsing, last-successful caching, and a device quota page.
- Add normalized Alibaba/Kimi/MiniMax/DeepSeek quota parsing, MiniMax API refresh, and a device summary page.

### Security

- Bind the device endpoint to one private adapter and require a DPAPI-protected pairing token.

## 0.1.0 - 2026-09-04

### Features

- Add an independently implemented Windows tray bridge.
- Add an ESP8266 USB status-display firmware.
- Define the `@AIBOT` version 1 line protocol.
- Add CI and tag-driven release scaffolding.
