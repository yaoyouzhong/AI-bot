# `@AIBOT` protocol version 1

The Windows or macOS bridge and ESP8266 communicate at 460800 baud using UTF-8 JSON lines. Every frame is one line beginning with the ASCII prefix `@AIBOT `.

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
{"version":1,"type":"status","data":{"time":"14:30:05","epochUtc":1788503405,"utcOffsetSeconds":28800,"codex":{"state":"working","ageSeconds":2},"claude":{"state":"idle","ageSeconds":185},"weather":null,"stocks":null,"quotas":null,"domesticQuotas":null,"systemMetrics":null,"music":null}}
```

Allowed states are `working`, `idle`, and `offline`. Unknown values render as `offline`. The device enters its offline page when no valid status frame is received for eight seconds.

`weather` is either null or the last available temperature, daily range, humidity, WMO code, PM2.5, AQI, source, update time, and stale flag. `stocks` is either null or an ordered quote array containing symbol, display code, name, price, signed change percentage, trend, update time, and stale flag. `quotas` is either null or contains optional `claude` and `codex` display snapshots. `domesticQuotas` is either null or contains optional `alibaba`, `kimi`, `miniMax`, and `deepSeek` display snapshots. Quota snapshots contain plan, available utilization windows and reset time, or balance/cost/currency where appropriate, plus update time and stale state. They never contain an access token or API key. `systemMetrics` contains CPU and physical-memory percentages plus aggregate upload/download bytes per second. `music` contains title, artist, album, playback state, elapsed seconds, duration, and update time. Cover pixels are excluded from JSON and use binary resource kind `2`. Receivers must tolerate every optional payload being absent.

## Compatibility

- Receivers must ignore unknown JSON fields.
- A receiver must reject unsupported protocol versions.
- A frame must not contain secrets or conversation content.
- Protocol changes require synchronized Windows, firmware, test, and documentation updates.

## Authenticated Wi-Fi fallback

After a successful USB handshake, the bridge sends a `lan_config` frame containing its selected private IPv4 address, port, and a random pairing token. The device persists this record locally. Windows protects the token with the current user's data-protection key; macOS stores it in the current user's Keychain. Neither implementation writes it to source, JSON/UserDefaults settings, logs, status frames, or HTTP responses.

When USB status has been absent for eight seconds, the device may request `GET /status` from that exact address and must send the token in the `X-AIBot-Token` header. The LAN listener binds only to the selected private adapter address. Missing or incorrect tokens receive `401`; the loopback development endpoint remains independently available at `127.0.0.1`.

```json
{"version":1,"type":"lan_config","data":{"host":"192.168.1.20","port":8765,"token":"runtime-secret"}}
```

The literal token above is illustrative only. Real tokens must never appear in documentation, test fixtures, screenshots, or diagnostics.

## Binary resources

Large resources use `NUL + COBS packet + NUL`, separate from JSON lines. A decoded version-1 packet is little-endian and contains:

| Offset | Size | Field |
| --- | ---: | --- |
| 0 | 4 | ASCII magic `AIB1` |
| 4 | 1 | protocol version `1` |
| 5 | 1 | resource kind: music text `1`, music cover `2`, pet asset `3`, weather text `4`, stock labels `5` |
| 6 | 4 | transfer ID |
| 10 | 2 | zero-based sequence |
| 12 | 2 | total chunk count |
| 14 | 4 | total decoded resource length |
| 18 | 2 | payload length, at most 768 bytes |
| 20 | 4 | whole-resource CRC32 |
| 24 | variable | payload |
| end - 4 | 4 | CRC32 of header and payload |

The device accepts chunks only in order, persists them to a temporary LittleFS file, and acknowledges each valid chunk with a JSON line. The final ACK is `ok=true` only after total length and whole-resource CRC pass and the new file replaces the prior resource. A lost ACK may cause the host to resend the same chunk; duplicate last-chunk acknowledgements are idempotent. The host retries each chunk at most three times.

The current music text bitmap is 232×44 RGB565; music covers and pet assets are 112×112; weather text is 232×24; and the twenty-row stock-label table is 120×400. Pixels are stored little-endian so the ESP8266 can stream one native `uint16_t` row at a time without allocating a full-frame buffer.

```json
{"version":1,"type":"resource_ack","transferId":305419896,"sequence":2,"ok":true}
```
