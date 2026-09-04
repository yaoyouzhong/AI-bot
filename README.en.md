# AI-bot

AI-bot is a local-first AI status clock. The target product includes Claude and Codex activity and account quotas, domestic-provider quotas, weather, stocks, system metrics, now playing, desktop pets, a screen saver, USB-first transport with Wi-Fi fallback, a Windows tray bridge, and a macOS menu-bar bridge.

Version `0.1.0` is an independent development baseline. It currently completes only one verifiable loop: local session activity → Windows bridge → `@AIBOT` USB protocol → ESP8266 display. The full feature set is mandatory rather than optional; current evidence for every capability is tracked in the [functional parity contract](docs/FUNCTIONAL_PARITY.md). Do not treat `0.1.0` as a functional replacement for the earlier product until that matrix reaches its required validation levels.

## Features

- Derives `working`, `idle`, and `offline` states for Codex and Claude Code.
- Exposes a read-only local endpoint at `127.0.0.1:8765/status`.
- Uses USB serial at 460800 baud.
- Implements ESP8266 handshake, rendering, and an eight-second offline state.
- Does not read or upload OAuth tokens, API keys, cookies, or conversation content.
- Includes Windows and firmware CI plus tag-driven release scaffolding.

Weather, stocks, account quotas, domestic quotas, pets, the screen saver, Wi-Fi fallback, and macOS are in the mandatory acceptance scope but are not yet all implemented. This README describes only currently implemented behavior; use the parity contract for progress.

## Layout

```text
windows-app/AIBotBridge/  Windows .NET 8 tray bridge
mac-app/                  macOS menu-bar bridge (independent rewrite pending)
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

## Firmware

```powershell
python -m platformio run -d firmware
python -m platformio run -d firmware -t upload --upload-port COM7
```

Exit the Windows bridge before flashing so it releases the serial port. After flashing, verify the handshake, status refresh, and the offline page after USB is removed.

## Privacy boundary

The bridge checks only session-log modification times and does not read conversation content. The development endpoint binds only to loopback. The Wi-Fi fallback endpoint binds to the selected private adapter and requires a pairing token. Windows protects that token with the current user's DPAPI key; the device receives it only after a USB handshake. It is never stored in source, JSON settings, or logs.

## License

AI-bot source is available under the [MIT License](LICENSE). Dependencies retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). See [PROVENANCE.md](PROVENANCE.md) for origin details.
