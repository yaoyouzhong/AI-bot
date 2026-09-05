# AI-bot

AI-bot is a local-first AI status clock. The target product includes Claude and Codex activity and account quotas, domestic-provider quotas, weather, stocks, system metrics, now playing, desktop pets, a screen saver, USB-first transport with Wi-Fi fallback, a Windows tray bridge, and a macOS menu-bar bridge.

Version `0.1.0` is an independent development baseline. The main Windows and firmware paths have been rebuilt, but live-account, physical-device, and macOS validation are still incomplete. The full feature set is mandatory rather than optional; current evidence for every capability is tracked in the [functional parity contract](docs/FUNCTIONAL_PARITY.md). Do not treat `0.1.0` as a functional replacement for the earlier product until that matrix reaches its required validation levels.

## Features

- Derives `working`, `idle`, and `offline` states for Codex and Claude Code.
- Exposes a read-only local endpoint at `127.0.0.1:8765/status`.
- Uses USB serial at 460800 baud.
- Implements ESP8266 handshake, rendering, and an eight-second offline state.
- Adds token-authenticated Wi-Fi fallback, NTP/holdover time, and a `PC OFF` standalone clock.
- Adds Open-Meteo weather and A/H/US quote data with last-successful caches.
- Adds numeric weather/stock pages, 15-second cycling, and four-row stock paging.
- Adds Claude/Codex quota parsing, last-successful caching, and a device quota page; live accounts and hardware remain unverified.
- Adds a normalized domestic-quota model, four response parsers, isolated WebView2 sign-in capture, and a device summary; MiniMax also supports an environment-key request path.
- Samples Windows CPU, physical-memory use, and aggregate active-interface traffic each second for a device system page.
- Reads Windows media-session title, artist, playback state, and progress; AUTO enters music while playing and resumes cycling after stop.
- Includes the original geometric pixel pet `BYTE SPROUT`, which walks or idles with Claude/Codex activity and imports no legacy sprites.
- Supports manual and idle-triggered screen saving, temporary AI/music event wake, and restoration after user input.
- Implements COBS resource chunks, per-chunk and whole CRC32, ACK/retry, and validated LittleFS replacement.
- Pre-renders a 232×44 CJK title/artist bitmap and 112×112 cover on track changes, transfers them reliably, and streams rows on-device.
- Imports a PNG/JPEG/BMP/GIF pet only beside a license notice, converts it to 112×112 RGB565, and sends it over USB without adding the source asset to the repository.
- Pre-renders CJK weather text and up to twenty stock names on Windows; the device reads the current page and falls back to symbols when assets are absent.
- Provides tray mode/brightness controls and a nine-page 240×240 Windows mirror with reproducible synthetic PNG validation.
- Adds tray display controls and an idle-time-driven Windows screen saver.
- Does not read conversation content. Quota access tokens are read only from local CLI sign-in files and sent only to the matching provider domain; they never enter cache, status, serial, or logs.
- Includes Windows and firmware CI plus tag-driven release scaffolding.

Still incomplete are live-account quota validation, physical-device validation of USB/resources/pages/screen saving/Wi-Fi fallback, Windows settings persistence and startup integration, plus the remaining macOS capabilities and a real macOS build. This README describes only current behavior; use the parity contract for progress. See [data sources and privacy](docs/DATA_SOURCES.md) for every outbound-data boundary.

## Layout

```text
windows-app/AIBotBridge/  Windows .NET 8 tray bridge
mac-app/                  macOS menu-bar bridge (independent foundation, platform-unverified)
firmware/                 PlatformIO + Arduino ESP8266 firmware
docs/                     Protocol and development documentation
```

## Windows

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj
```

Release archives require the .NET 8 Desktop Runtime.

Set `AIBOT_PORT` to constrain probing to one serial port.

The local endpoint defaults to `127.0.0.1:8765`. Set `AIBOT_HTTP_PORT` when a test needs another port. A device-only listener also binds to the selected private LAN address, but it requires the random token provisioned over a USB handshake; unauthenticated requests receive 401.

The tray's **Settings** command saves the weather city/coordinates, up to twenty stock symbols, automatic screen-saver delay, and a preferred serial port. Values are restricted to a non-secret allow-list in `%APPDATA%\AI-bot\settings.json` and take effect after restarting the bridge.

## Firmware

```powershell
python -m platformio run -d firmware
python -m platformio run -d firmware -t upload --upload-port COM7
```

Exit the Windows bridge before flashing so it releases the serial port. After flashing, verify the handshake, status refresh, and the offline page after USB is removed.

## macOS

The current Swift source includes a menu bar, Claude/Codex activity and account quotas, Open-Meteo weather, A/H/US stocks, CPU/memory/network metrics, Apple Music/Spotify metadata and progress, a localized music-text resource, last-successful caches, non-secret UserDefaults settings, a Keychain pairing token, authenticated LAN `/status`, `/dev/cu.*` discovery, a 460800-baud handshake, two-second status frames, USB provisioning for Wi-Fi fallback, menu controls for device pages and brightness, and idle-time screen-saver entry/restoration. Music access is disabled by default; enabling it from the menu may prompt for Automation permission, and the bridge queries only players that are already running. Validate it on macOS 13+:

```bash
swift test --package-path mac-app
swift build -c release --package-path mac-app
bash scripts/build_macos_app.sh
```

Music automation must be tested by launching the generated `artifacts/AIBotBridge.app`; the bare SwiftPM executable does not carry the Apple Events purpose string and Hardened Runtime entitlement. The script uses local ad-hoc signing for development validation, not a distribution identity.

The current Windows host has no Swift toolchain. Mac control frames, menu controls, CRC/ACK-retried binary transport, localized weather/stock/music resources, Apple Music/Spotify cover decoding, and license-gated pet import have source but have not been compiled or connected to a device. Domestic quota, mirror, device information, and Wi-Fi reset are still required; music AUTO behavior, Automation permission, player scripting fields, and cover art also require a real Mac. The existing source has not been checked with live accounts or system metrics, so it remains `platform-unverified`.

## Privacy boundary

The bridge checks only session-log modification times and does not read conversation content. Account quota requests read existing Claude/Codex CLI sign-in files; access tokens are used only with the matching official provider endpoint and are never written to AI-bot cache, status, serial, or logs. Domestic quota sign-in uses an isolated `%APPDATA%\AI-bot\quota-auth-profile` browser profile. Cookies remain in that WebView2 profile; the app parses display-only quota fields from explicitly allowed official-host responses and does not log response bodies, cookies, or tokens. The development endpoint binds only to loopback. The Wi-Fi fallback endpoint binds to the selected private adapter and requires a pairing token. Windows protects that token with the current user's DPAPI key; the device receives it only after a USB handshake. It is never stored in source, JSON settings, or logs.

## License

AI-bot source is available under the [MIT License](LICENSE). Dependencies retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). See [PROVENANCE.md](PROVENANCE.md) for origin details and the [asset policy](docs/ASSET_POLICY.md) for runtime pet imports.
