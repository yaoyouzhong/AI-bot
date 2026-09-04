# AI-bot

AI-bot is a local-first activity clock for AI coding tools. A Windows tray bridge observes the most recent modification times in local Codex and Claude Code session directories, then sends status over USB serial to an ESP8266 with a 240x240 ST7789 display.

Version `0.1.0` is an independent foundation focused on one verifiable loop: local session activity → Windows bridge → `@AIBOT` USB protocol → ESP8266 display. It does not include images, pets, stocks, weather, provider quota scraping, or a macOS application from any earlier project.

## Features

- Derives `working`, `idle`, and `offline` states for Codex and Claude Code.
- Exposes a read-only local endpoint at `127.0.0.1:8765/status`.
- Uses USB serial at 460800 baud.
- Implements ESP8266 handshake, rendering, and an eight-second offline state.
- Does not read or upload OAuth tokens, API keys, cookies, or conversation content.
- Includes Windows and firmware CI plus tag-driven release scaffolding.

## Layout

```text
windows-app/AIBotBridge/  Windows .NET 8 tray bridge
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

The local endpoint defaults to `127.0.0.1:8765`. Set `AIBOT_HTTP_PORT` when a test needs another loopback port.

## Firmware

```powershell
python -m platformio run -d firmware
python -m platformio run -d firmware -t upload --upload-port COM7
```

Exit the Windows bridge before flashing so it releases the serial port. After flashing, verify the handshake, status refresh, and the offline page after USB is removed.

## Privacy boundary

The bridge checks only session-log modification times and does not read conversation content. Its HTTP endpoint binds only to loopback. The first device version supports USB only and has no network configuration or cloud service.

## License

AI-bot source is available under the [MIT License](LICENSE). Dependencies retain their own licenses; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). See [PROVENANCE.md](PROVENANCE.md) for origin details.
