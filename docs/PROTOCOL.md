# `@AIBOT` protocol version 1

The Windows or macOS bridge and ESP8266 communicate at 460800 baud using UTF-8 JSON lines. Every frame is one line beginning with the ASCII prefix `@AIBOT `.

## Probe

Host request:

```json
{"version":1,"type":"ping"}
```

Device response:

```json
{"version":1,"type":"pong","device":"esp8266","ip":"192.168.1.42"}
```

The device may also emit a `hello` frame while it has not received status. Both `hello` and `pong` include the device's current IPv4 address when Wi-Fi is connected, or an empty `ip` when it is not. Hosts accept only version-1 `pong` frames whose `device` is `esp8266`. Older such responses with no `ip` remain transport-compatible, but a host may use the value for device administration only when it is a canonical RFC1918 address (`10/8`, `172.16/12`, or `192.168/16`).

## Status

```json
{"version":1,"type":"status","data":{"time":"14:30:05","epochUtc":1788503405,"utcOffsetSeconds":28800,"codex":{"state":"working","ageSeconds":2},"claude":{"state":"idle","ageSeconds":185},"weather":null,"stocks":null,"quotas":null,"domesticQuotas":null,"systemMetrics":null,"music":null}}
```

Allowed states are `working`, `idle`, and `offline`. Unknown values render as `offline`. The device enters its offline page when no valid status frame is received for eight seconds.

Windows may include `completionAt` (Unix seconds) and `completionSequence` on a
tool-state object. They are derived from explicit root-session completion events,
not file modification time. Older receivers ignore these fields; completion audio
is a Windows host feature. Conversation text is not transmitted.

`weather` is either null or the last available temperature, daily range, humidity, WMO code, PM2.5, AQI, source, update time, and stale flag. `stocks` is either null or an ordered quote array containing symbol, display code, name, price, signed change percentage, trend, update time, and stale flag. `quotas` is either null or contains optional `claude` and `codex` display snapshots. `domesticQuotas` is either null or contains optional `alibaba`, `kimi`, `miniMax`, `deepSeek`, and `zhipu` display snapshots. Quota snapshots contain plan, available utilization windows and reset time, or balance/cost/currency where appropriate, plus update time and stale state. They never contain an access token or API key. `systemMetrics` contains CPU and physical-memory percentages plus aggregate upload/download bytes per second. `music` contains title, artist, album, playback state, elapsed seconds, duration, and update time. Cover pixels are excluded from JSON and use binary resource kind `2`. Receivers must tolerate every optional payload being absent.

## Compatibility

Domestic providers add optional `planPercent` (used percentage of the whole plan)
and `planResetsAt` (ISO 8601 timestamp). These are independent of `primaryPercent`
(5H) and `weeklyPercent` (WK). The legacy-cache adapter must not fill WK from PLAN.
The current Alibaba total/surplus parser emits PLAN, not WK. Windows and firmware
choose the overview percentage from WK, otherwise PLAN; they never substitute 5H
for an unknown overview. Kimi, or a provider with a known 5H/WK window, uses the
windowed presentation. A plan-only page shows PLAN, remaining percentage and local
plan reset date; a windowed page shows WEEKLY, weekly reset and a 5H footer.
Absent values stay unknown. Older caches/messages without the additive fields
remain readable, but ambiguous old WK values are not silently reclassified.
macOS has the optional Codable contract and source tests only; it does not yet
acquire domestic vendor quotas or provide the matching vendor-page renderer.

Windows samples system metrics every 250 ms and may send additive
`{"version":1,"type":"metrics","data":{...systemMetrics...}}` frames between the
two-second status frames. Firmware accepts them only while USB status is fresh;
they do not update heartbeat timestamps or disable the eight-second Wi-Fi fallback.
The 224-point network graph deduplicates samples by `updatedAt`. LAN/older macOS
senders continue updating the graph through their normal status polling cadence.
In-memory Windows graph history is not serialized. USB status omits non-rendered
metadata and binary-backed stock names to remain within 6144 UTF-8 bytes; it retains
all 20 quotes, display policy, quota fields and reset-credit dates. Overlong fallback
text is bounded without truncating JSON; LAN/mirror snapshots remain complete.

Optional `displayPolicy` carries `selectedMode`, `cycleEnabled`, `intervalSeconds`
(10/15/30/60), ordered `pages`, and optional `cycleStartedAt` (Unix UTC seconds,
zero for older senders). Enabling or editing a cycle resets its anchor so its first
page is shown immediately. Manual selection disables cycling. Input/completion
alerts temporarily override fixed pages except explicit screensaver preview.
In automatic mode, alerts take priority, then an explicitly enabled cycle; only with
cycling disabled do music and working sessions determine the page. Windows, macOS
and firmware use the same anchor; negative elapsed time clamps to zero.
The shared mode catalog includes `claude`, `codex`, `dual`, `domestic_alibaba`,
`domestic_kimi`, `domestic_minimax`, `domestic_deepseek`, `domestic_zhipu`, `weather`, `stocks`,
`system`, `music`, `pet`, `screensaver`, `activity`, and `auto`. Legacy AI-bot
`quotas`/`domestic` aliases remain accepted. When policy is absent, direct display
commands remain effective (including older macOS senders).

Weather may additionally carry `animation` (`robot`, `house`, `plant`, `off`, `pet`),
`headerCenterX`, `dateCenterX`, `rangeY`, city-specific `utcOffsetSeconds`, and
`animationIcon` (0 sunny, 1 partly cloudy, 2 overcast, 3 fog, 4 rain, 5 snow,
6 thunder). `weatherCode` remains WMO, or -1 when unavailable; it is never reused
for provider-specific codes. Receivers map WMO to animation when the explicit
animation icon is absent (including macOS senders).

`quotas.codex.resetCreditsAvailable` is the total known available count; `resetCreditExpiresAt` is an ordered array of Unix UTC seconds, one entry per available credit with a known expiry. Duplicate dates are distinct credits and must not be deduplicated. Both bridges emit this field; firmware consumes it. Receivers apply `utcOffsetSeconds` for month/day. On the single Codex page, Windows and firmware show all supplied entries as separate R*1 rows in the legacy upper-right badge (x=153, width=68, row pitch=19); absent details produce one aggregate count without an invented date. Windows hides dates already expired. The pet-page footer retains its two-row/four-second pagination. When details are missing but the positive count is unchanged, bridges retain cached dates and mark the snapshot stale; a changed count does not inherit old dates. macOS single-page badge parity remains pending.

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

## Authenticated device administration

### USB administration (independent of LAN)

USB physical access has the same trust boundary as existing `lan_config`, display and brightness commands. No IP address or pairing token is needed for these USB requests. Hosts serialize requests with resource/heartbeat I/O and match the version, response type and nonzero `request_id`. Older firmware without these commands times out explicitly.

```json
{"version":1,"type":"device_info_request","request_id":7}
{"version":1,"type":"device_info","request_id":7,"ok":true,"data":{"device":"AI-bot","version":1,"ip":"","usb_active":true,"bridge_online":true,"mode":"weather","brightness":75,"uptime_ms":10000,"usb_status_count":4,"lan_status_count":0}}
{"version":1,"type":"reset_wifi","request_id":8,"confirm":true}
{"version":1,"type":"reset_wifi_ack","request_id":8,"ok":true}
```

`ip` is empty when Wi-Fi is disconnected; this does not prevent USB management. Information requests do not change status timestamps or counters. `usb_status_count` counts accepted USB status frames; `lan_status_count` counts successfully decoded version-1 LAN status responses. Counters and `uptime_ms` reset on reboot. Only boolean `confirm=true` permits USB Wi-Fi reset; otherwise the reply is `ok=false` and no reset occurs. Firmware flushes the ACK before reboot. Hosts must require an explicit user confirmation, send reset only once and never retry it over HTTP after an uncertain USB result.

The desktop fallback test pauses normal USB status, resource and control traffic while keeping power, the serial port and LAN services running. Read-only USB diagnostics remain available and do not refresh the eight-second heartbeat. The test samples counters after acquiring the pause, checks LAN progress after 12 seconds, then resumes USB and checks USB progress. Pause expires automatically after 30 seconds and is also cleared on failure/cancellation. This simulates loss of USB status traffic, not a physical cable disconnect.

### LAN administration

The firmware listens on port 80 only while Wi-Fi is connected. A host may contact only the exact private IPv4 address discovered from the USB handshake and must send the current pairing token in `X-AIBot-Token`.

- `GET /api/info` returns the device name, protocol version, IP address, USB/bridge state, display mode, and brightness.
- `POST /reset-wifi` clears the device's saved Wi-Fi configuration and restarts it. A UI must expose this only as an explicit user action with a destructive-action confirmation.

These endpoints are local HTTP because the constrained device does not terminate TLS. The strict private-address allow-list and pairing-token header are mandatory; hosts must not redirect, discover an address from an HTTP response, or send the token to a public, loopback, link-local, or user-entered host.

## Binary resources

Device information additionally exposes optional `page_data`: render-state availability
(`weather`, `claude`, `codex`, `alibaba`, `kimi`, `minimax`, `deepseek`, `zhipu`, `system`, `music`),
`stock_count`, `temperature`, and `cpu_percent`. These are decoded device values, not
host claims or optical verification. Older clients may ignore this additive field;
When GLM balance is available, `zhipu_balance` and `zhipu_currency` report its decoded display amount and currency for end-to-end checks.
page acceptance tests must reject its absence. No credentials are exposed.

Large resources use `NUL + COBS packet + NUL`, separate from JSON lines. A decoded version-1 packet is little-endian and contains:

| Offset | Size | Field |
| --- | ---: | --- |
| 0 | 4 | ASCII magic `AIB1` |
| 4 | 1 | protocol version `1` |
| 5 | 1 | resource kind: music text `1`, music cover `2`, static pet `3`, weather text `4`, stock labels `5`, weather header `6`, date `7`, air quality `8`, animated pet `9`, Claude pet `10`, Codex pet `11`, Claude logo `12`, Codex logo `13` |
| 6 | 4 | transfer ID |
| 10 | 2 | zero-based sequence |
| 12 | 2 | total chunk count |
| 14 | 4 | total decoded resource length |
| 18 | 2 | payload length, at most 768 bytes |
| 20 | 4 | whole-resource CRC32 |
| 24 | variable | payload |
| end - 4 | 4 | CRC32 of header and payload |

The device accepts chunks only in order, persists them to a temporary LittleFS file, and acknowledges each valid chunk with a JSON line. The final ACK is `ok=true` only after total length and whole-resource CRC pass and the new file replaces the prior resource. A lost ACK may cause the host to resend the same chunk; duplicate last-chunk acknowledgements are idempotent. The host retries each chunk at most three times.

The current music text bitmap is 232×44 RGB565; music covers and pet assets are 112×112; weather text is 232×24; and the twenty-row stock-label table is 156×400 (kind 5, 124800 bytes, displayed at x=70). The narrower 120×400 draft is accepted by firmware as a compatibility fallback at x=106. Windows/macOS now produce 156×400 to preserve the legacy name area. Pixels are stored little-endian so the ESP8266 can stream one native `uint16_t` row at a time without allocating a full-frame buffer.

The additive weather header/date/air-quality bitmaps are 122×26, 190×30, and
100×30 RGB565 respectively. Kind 4 remains available as a legacy text fallback.

Kinds 12/13 are optional private 40×40 RGB565 page logos, exactly 3200 bytes,
rendered at (14,18). Firmware rejects any other length before replacing the cached
file. They use the same USB chunk/CRC and authenticated LAN catalog paths. Bridges
publish them only after explicit local import; vendor pixels are not bundled.

Kinds 9 (legacy global custom pet), 10 (Claude pet), and 11 (Codex pet) are APET. Firmware stores the two provider resources independently and selects them by page; kind 9 is a fallback only when the selected provider resource is absent. Windows and macOS publish persistent effective selections as kinds 10/11, including restored defaults. Switching pages does not require retransmitting the animation. Mac source and tests require macOS validation. Older bridges emitting only kind 9 cannot override existing provider resources.
APET: ASCII `APET`, version byte `1` (112x112) or `2` (each dimension 1–120), frame-count byte (1–8), little-endian
UInt16 width and height, two reserved zero bytes, then one UInt16 duration
in milliseconds per frame (20–60000), followed by contiguous little-endian RGB565
frames. Exact resource length is `12 + 2*count + width*height*2*count`. The device validates
this structure before replacing its last animation and streams only one row at a time.
Status heartbeats can be interleaved between acknowledged resource chunks to avoid
entering offline mode during a large transfer. Old static kind 3 remains accepted.

```json
{"version":1,"type":"resource_ack","transferId":305419896,"sequence":2,"ok":true}
```

## Activity and authenticated resource fallback

Tool status adds `needsInput`, `completionActive`, `completionAt`, `completionSequence`, and
`tokensToday`. `domesticActivity` carries `activeProvider`, `state`, `needsInput`, and
`tokensToday`; Windows local status also has per-provider totals, omitted from compact USB frames.
Tokens describe today's local log records, including cache input tokens, not account-wide usage.
Manual display modes win; AUTO prioritizes attention, unacknowledged completion, music,
active tools and configured idle cycling. Permission attention expires after five minutes.
Codex completion is explicit, not inferred from `Stop`; acknowledging or starting work clears it.
The completion ring pulses five times at 70 ms per phase while the pet continues animating.

Loopback-only `POST /event` accepts JSON `{"agent":"claude","event":"PermissionRequest"}`;
`agent` may also be `codex`. Working hooks, Stop/SessionStart/SessionEnd, Elicitation,
permission notifications and explicit Codex TaskComplete are supported. `POST /completion/ack`
acknowledges completion. Browser Origin mutations and non-loopback hook calls are rejected.
Hook installation is a separate explicit operation; development does not edit CLI settings.

Authenticated `GET /resources` returns `{"version":1,"resources":[{"kind":10,"crc":123,"length":159876}]}`.
Authenticated `GET /resources/<kind>/<crc>` returns exact binary bytes; a changed revision returns
409 so the device retries the catalog. No filesystem path is accepted. Both require X-AIBot-Token.
After a successful Wi-Fi status poll the device verifies cached length/CRC, then fetches at most
one changed resource, checking final CRC and format before replacement. Failure keeps the old file;
USB recovery interrupts the download. This does not bypass LAN client isolation.

RGB565 resources use natural values serialized little-endian; firmware enables TFT_eSPI
`setSwapBytes(true)`. Explicit legacy header import first undoes the old pre-swapped words.
Music covers remain 112×112 on wire and render at 128×128 at (56,16); title/artist start at
y=154/178 and the 200×8 progress track at (20,210).
# AUTO follow parity (2026-09-09)

With explicit cycling disabled, playing music precedes working domestic activity, which precedes a uniquely working Claude/Codex tool. If both tools are working, alternate every 2 seconds; if neither is working, alternate every 6 seconds. New bridges keep a monotonic last-actual-switch timer and send optional `data.followApp` (`claude` or `codex`). Repeated samples do not restart this timer. Hidden pages do not advance it; returning to AUTO can switch once if the dwell time has expired. Mirror and firmware honor the same selection; explicit cycling and manual modes still take precedence. Two simultaneous input prompts use this selection instead of unconditionally preferring Codex. Old bridge payloads without the field retain the earlier fallback calculation; do not claim identical phase for that compatibility path. Hardware delivery latency remains unverified.

`music.hasArtwork` is an optional boolean: false suppresses cached cover display, true permits the validated cover resource, and absence retains old-bridge compatibility. Missing/null music clears device playback state and prevents stale cached title/artwork from being displayed. New bridges do not send a fabricated black cover for missing artwork. Windows tolerates two empty media samples, clearing on the third, and retries late artwork on subsequent refreshes. No cache deletion or Wi-Fi reset is required.
# 系统监控增量样本（兼容扩展）

`systemMetrics` / `metrics.data` 可含 `sampleSession`（进程会话标识）、`sampleSequence`（单调递增计数）和 `samples`（最近最多 12 个 `{upload,download}` 原始 250ms 样本）。顶层上传/下载数值是最近 4 样本平均值，用于数字显示；CPU/内存最多每秒重算。

Windows USB 心跳省略 `samples`，快速 metrics 帧携带样本尾部；LAN 状态保留尾部。设备按会话/序号去重入队，每 250ms 消费一条，积压超过 16 条时每次最多消费三条。最多保留 32 条待绘制样本，超出时丢最旧条以追上实时状态。没有这些可选字段的旧发送端继续按 `updatedAt` 去重，不能因此宣称旧发送端具备增量补采能力。

`device_info` 的 `page_data` 增加只读计数：`system_chart_frames`、`system_chrome_draws`、`system_number_draws`、`system_samples_consumed`、`system_queue_depth`，用于真实刷新验收，不含用户数据。
