# `@AIBOT` protocol version 1

The Windows bridge and ESP8266 communicate at 460800 baud using UTF-8 JSON lines. Every frame is one line beginning with the ASCII prefix `@AIBOT `.

## Probe

Host request:

```json
{"version":1,"type":"ping"}
```

Device response:

```json
{"version":1,"type":"pong","device":"esp8266"}
```

The device may also emit a `hello` frame while it has not received status.

## Status

```json
{"version":1,"type":"status","data":{"time":"14:30:05","codex":{"state":"working","age_seconds":2},"claude":{"state":"idle","age_seconds":185}}}
```

Allowed states are `working`, `idle`, and `offline`. Unknown values render as `offline`. The device enters its offline page when no valid status frame is received for eight seconds.

## Compatibility

- Receivers must ignore unknown JSON fields.
- A receiver must reject unsupported protocol versions.
- A frame must not contain secrets or conversation content.
- Protocol changes require synchronized Windows, firmware, test, and documentation updates.

## Authenticated Wi-Fi fallback

After a successful USB handshake, the bridge sends a `lan_config` frame containing its selected private IPv4 address, port, and a random pairing token. The device persists this record locally. The Windows token is protected with the current user's Windows data-protection key and is never written to source, JSON settings, logs, or HTTP responses.

When USB status has been absent for eight seconds, the device may request `GET /status` from that exact address and must send the token in the `X-AIBot-Token` header. The LAN listener binds only to the selected private adapter address. Missing or incorrect tokens receive `401`; the loopback development endpoint remains independently available at `127.0.0.1`.

```json
{"version":1,"type":"lan_config","data":{"host":"192.168.1.20","port":8765,"token":"runtime-secret"}}
```

The literal token above is illustrative only. Real tokens must never appear in documentation, test fixtures, screenshots, or diagnostics.
