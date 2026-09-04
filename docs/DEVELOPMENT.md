# Development

## Architecture

`SessionActivityReader` computes a deterministic state from the newest `.jsonl` modification time under the Codex and Claude Code session roots. `LocalStatusServer` publishes the snapshot on loopback. `LanStatusServer` binds only to a selected private adapter and requires the per-user pairing token. `SerialPublisher` probes a device with `ping`, waits for `pong`, provisions the LAN address and token, and sends status every two seconds.

Firmware parses only prefixed version 1 frames. A fresh USB status frame always wins. After eight seconds without USB status it polls the paired LAN endpoint with `X-AIBot-Token`; if neither path is fresh, it keeps time from the latest bridge epoch or NTP and displays `PC OFF`. The selected display mode is not replaced by a transport failure.

`BridgeRuntime` owns the longer-lived data sources and exposes one immutable snapshot to loopback HTTP, authenticated LAN HTTP, USB, the tray, and diagnostics. `WeatherService` refreshes Open-Meteo every 15 minutes; `StockService` refreshes configured A/H/US symbols every five seconds. Both replace their cache only after a successful parse and return the last successful snapshot with `stale=true` after failure.

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
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --self-test-data
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --self-test-live-data
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --self-test-mirror
```

The LAN self-test requires 401 for missing and incorrect tokens, then requires a version 1 snapshot for the correct synthetic token. The data self-test uses embedded synthetic responses. The live-data self-test uses a fixed public Beijing coordinate and the Shanghai Composite symbol; it deliberately does not read personal settings or write caches.

The mirror self-test renders nine synthetic 240×240 pages into ignored `artifacts/mirror-self-test.png`. It is visual evidence for the Windows renderer only; it does not prove device pixel parity.

## macOS

`mac-app` is an independent Swift Package targeting macOS 13. Its current foundation uses AppKit for the menu bar, Security.framework for the pairing token, Network.framework for the authenticated LAN status listener, and Darwin termios calls for serial I/O. Session activity is derived from `.jsonl` file metadata using the same 90-second/15-minute thresholds as Windows.

The serial worker considers only recognized `/dev/cu.*` USB-serial families, configures 460800-baud raw I/O, requires the version-1 `ping`/`pong` handshake, then sends `lan_config` once and `status` every two seconds. The LAN configuration is withheld when the authenticated listener failed to start or no private IPv4 address is available. Pairing material is generated from `SecRandomCopyBytes`, stored only in Keychain, and never included in diagnostics. Binary-resource transfer is not implemented on macOS yet.

Validation must run on macOS:

```bash
swift test --package-path mac-app
swift build -c release --package-path mac-app
```

No Swift toolchain is present in the Windows development environment, so static review on Windows is not build evidence. Account quotas, weather, stocks, system metrics, and USB control frames still require a Mac build; music, binary-resource transport, mirror UI, and device controls remain required Mac scope.
