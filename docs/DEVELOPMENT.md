# Development

## Architecture

`SessionActivityReader` computes a deterministic state from the newest `.jsonl` modification time under the Codex and Claude Code session roots. `LocalStatusServer` publishes the snapshot on loopback. `LanStatusServer` binds only to a selected private adapter and requires the per-user pairing token. `SerialPublisher` probes a device with `ping`, waits for `pong`, provisions the LAN address and token, and sends status every two seconds.

Firmware parses only prefixed version 1 frames. A fresh USB status frame always wins. After eight seconds without USB status it polls the paired LAN endpoint with `X-AIBot-Token`; if neither path is fresh, it keeps time from the latest bridge epoch or NTP and displays `PC OFF`. The selected display mode is not replaced by a transport failure.

The model is deliberately narrow: routing, retries, timeouts, framing, and state thresholds remain deterministic code.

## State thresholds

| State | Newest session-log age |
| --- | --- |
| `working` | less than 90 seconds |
| `idle` | 90 seconds through 15 minutes |
| `offline` | older than 15 minutes or no readable session log |

## Validation

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
python -m platformio run -d firmware
```

The `--status-once` command must emit valid JSON without starting the tray UI. Firmware validation must end with a successful PlatformIO build. Serial or display changes additionally require a real-device result.

The authenticated LAN listener can be checked without exposing a real token:

```powershell
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --self-test-lan
```

The self-test requires 401 for missing and incorrect tokens, then requires a version 1 snapshot for the correct synthetic token.
