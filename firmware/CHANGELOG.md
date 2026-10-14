# ESP8266 firmware changelog

ESP8266 versions are independent of the desktop bridge and TAB5.

## 0.5.3 - 2026-10-12

- Report a persistent `device_id` derived from the station MAC in USB hello/pong, USB device information and authenticated Wi-Fi information. It remains available without a Wi-Fi connection and across USB sockets, COM changes and reboots.
- A supporting Windows bridge records this ID on the trusted connection and verifies it before automatically updating a changed USB binding. Existing firmware and bridges retain protocol version 1 compatibility.
- Locally installed with complete Flash backup and write/read verification. A real COM8-to-COM7 move retained the fixed ID and automatically restored USB data. A separate UART recovery test with no LAN configuration passed; device Wi-Fi was not disabled during acceptance. No public release was created.

## 0.5.2 - 2026-10-09

Published alongside bridge 0.6.5 and TAB5 .176; build and distribution-material checks pass. Hardware installation and acceptance remain pending.

- Share the trailing four measured 250 ms samples between system-page numbers and curves. Consume the incoming batch before drawing both, preventing numbers from advancing ahead of the chart.
- Clear old history when the bridge sampling session changes, average only measured samples at startup, and retain fonts, colors and the number-drawing allocation fallback.
- Preserve protocol version 1, 460800 baud, pairing and USB/Wi-Fi fallback.

## 0.5.1 - 2026-10-08

- Embed the component version from firmware/VERSION and report it through USB heartbeat, device information and authenticated Wi-Fi requests so the bridge can compare upgrades.
- Legacy screens without version reporting should install bridge 0.6.2 first, then use manual screen-upgrade preparation with complete USB backup, write and verification.
- Keep protocol version 1, existing pages, pairing and USB/Wi-Fi fallback. The repair was verified on real ESP8266 hardware using a local candidate; final 0.5.1 package installation is a separate acceptance step.

## 0.5.0 - 2026-10-03

- Firmware baseline distributed with the historical AI-bot v0.5.0 release.
- Future firmware versions use `firmware/VERSION` and `esp8266-vX.Y.Z` tags; unchanged firmware need not be republished for bridge or TAB5 updates.

The published v0.5.0 firmware remains unchanged. Source changes after this baseline require their own validation and release entry.
