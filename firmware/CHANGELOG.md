# ESP8266 firmware changelog

ESP8266 versions are independent of the desktop bridge and TAB5.

## 0.5.1 - 2026-10-08

- Embed the component version from firmware/VERSION and report it through USB heartbeat, device information and authenticated Wi-Fi requests so the bridge can compare upgrades.
- Legacy screens without version reporting should install bridge 0.6.2 first, then use manual screen-upgrade preparation with complete USB backup, write and verification.
- Keep protocol version 1, existing pages, pairing and USB/Wi-Fi fallback. The repair was verified on real ESP8266 hardware using a local candidate; final 0.5.1 package installation is a separate acceptance step.

## 0.5.0 - 2026-10-03

- Firmware baseline distributed with the historical AI-bot v0.5.0 release.
- Future firmware versions use `firmware/VERSION` and `esp8266-vX.Y.Z` tags; unchanged firmware need not be republished for bridge or TAB5 updates.

The published v0.5.0 firmware remains unchanged. Source changes after this baseline require their own validation and release entry.
