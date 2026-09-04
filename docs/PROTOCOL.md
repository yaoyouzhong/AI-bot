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
