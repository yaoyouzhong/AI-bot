# Development

## Architecture

`SessionActivityReader` computes a deterministic state from the newest `.jsonl` modification time under the Codex and Claude Code session roots. `LocalStatusServer` publishes the same snapshot on loopback. `SerialPublisher` probes a device with `ping`, waits for `pong`, and sends status every two seconds. Firmware parses only prefixed version 1 frames and renders a simple text UI.

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
