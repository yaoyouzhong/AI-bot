# `@AIBOT` protocol version 1

TAB5 .120 配套 Windows 桥接为 `open-recent` 成功回执增加可选 `draftEmpty`（true / false / null）。只读检查限于前台已确认目标的主输入框；true 表示确认空草稿，撤销该目标旧发送凭据并使设备恢复语音。false、null 或缺字段不重置同一目标原状态。请求重放沿用首次结果。仅 Windows 支持此检查；macOS 未实现此桌面操作。USB / Wi-Fi / BLE 共用字段，ESP8266 不变。

The .120 paired Windows bridge adds optional nullable `draftEmpty` to successful `open-recent` replies. Read-only inspection verifies the foreground target's main composer. Only true retires old target send tickets and restores device dictation; false, null or absence preserve same-target state. Replays retain the original observation. This Windows-only desktop operation is not implemented on macOS. USB, Wi-Fi and BLE share the field; ESP8266 is unchanged.

TAB5 uses separate `tab5_*` messages and `/tab5/v1/status`; see [TAB5 integration](TAB5.md). The ESP8266 protocol below is unchanged. TAB5 使用独立消息与接口，不改变下面的小屏协议。

## TAB5 .117 OTA transport compression

USB and BLE bulk OTA RPC ranges negotiate `acceptEncoding:"zlib"` against authenticated `rpcOtaZlib=1`, retaining the existing 48 KiB decoded limit, encryption, original offsets and raw fallback. Wi-Fi OTA uses the existing authenticated GET with `X-AIBot-OTA-Flow: tcp-v2` plus `X-AIBot-OTA-Encoding: aibot-zlib-blocks-v1`; the bridge only enables compression when both capabilities match and echoes the encoding header. Missing headers retain the raw image path. The general Wi-Fi RPC endpoint still rejects OTA ranges with `firmware_requires_ota_transport`; Wi-Fi firmware uses the streaming GET.

The HTTP body is a sequence of independent blocks: four little-endian uint32 fields (original offset, decoded bytes, payload bytes, encoding), then payload. Encoding 0 is raw; 1 is zlib. Decoded blocks are 49152 bytes except the last. Payload must be positive and no larger than decoded bytes; zlib must be smaller. Content-Length is the framed transfer length, while the offer's size and SHA-256 always describe the decoded image. Reject unknown/duplicate encoding headers, wrong offsets/sizes, truncation, trailing bytes and decode failure before accepting the image. Final full-image hash, identity and boot-partition checks remain mandatory.

TAB5 三通道统一协商压缩，旧端与不适合压缩的数据保持原文兼容。自动升级优先 Wi-Fi、USB、BLE；固定模式只使用所选通道，开始后不跨通道续传。显示中的镜像进度按解压后字节计算，另列传输数据量（Wi-Fi 含分块头，USB/BLE 计 RPC 数据载荷，不含加密与链路开销），不能把镜像大小解释为下载量。蓝牙入口显示“通过蓝牙升级”；首次安装仍使用 USB 完整安装包。

The Windows or macOS bridge and ESP8266 communicate at 460800 baud using UTF-8 JSON lines. Every frame is one line beginning with the ASCII prefix `@AIBOT `.

### Windows connection preference

Windows Device Center can select Automatic, USB only or Wi-Fi only for the registered ESP8266. This is host routing policy; version-1 frames and firmware remain unchanged. Automatic retains USB-first behavior and the firmware's 8-second freshness timeout. Wi-Fi only stops USB status, metrics and binary-resource delivery while still allowing USB pairing, device information and management commands. USB only rejects the legacy `/status` and `/resources` routes and legacy discovery responses; TAB5 endpoints are unaffected. The optional device-registry `ConnectionMode` field defaults to `Auto` for existing profiles and persists `Auto`, `Usb` or `Wifi`.

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

Allowed states are `working`, `idle`, and `offline`. Unknown values render as `offline`. After eight seconds without a valid status frame, USB is stale, Wi-Fi fallback starts, and `bridge_online` becomes false if Wi-Fi has not supplied status. The last page remains visible for up to 30 seconds while reconnecting; `page_data.display_cached` reports this interval. The device then enters its offline page. An explicit `host_going_away` frame enters offline immediately.

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
page is shown immediately. Manual selection disables cycling and takes priority
over input/completion alerts. Alerts do not navigate away from a manually selected page.
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

After a successful USB handshake, the bridge sends a `lan_config` frame containing its selected private IPv4 address, port, and a random pairing token. The device persists this record locally. The Windows bridge prefers an adapter on the device's reported IPv4 subnet; when adapters change, it starts the new LAN listener and resends `lan_config` over USB. Windows protects the token with the current user's data-protection key; macOS stores it in the current user's Keychain. Neither implementation writes it to source, JSON/UserDefaults settings, logs, status frames, or HTTP responses.

When USB status has been absent for eight seconds, the device may request `GET /status` from that exact address and must send the token in the `X-AIBot-Token` header. The LAN listener binds only to the selected private adapter address. Missing or incorrect tokens receive `401`; the loopback development endpoint remains independently available at `127.0.0.1`.

### Windows LAN rediscovery after both IP addresses change

After one USB pairing, the device and the Windows bridge retain the same pairing token. If USB and LAN status are both stale, the device broadcasts a small discovery datagram to UDP port `18766` on its current IPv4 subnet every five seconds. The request is ASCII `AIBOT_DISCOVER_V1|<16 lowercase hex nonce>|<64 lowercase hex HMAC-SHA256>`, where the HMAC key is the UTF-8 pairing token and the signed bytes are the prefix through the nonce. The token itself is never included in a discovery datagram.

The Windows bridge checks the HMAC, selects an active private adapter whose subnet contains the datagram source, and starts the authenticated LAN listener on that adapter before replying from that adapter's IP. The ASCII response is `AIBOT_BRIDGE_V1|<same nonce>|<IPv4 host>|<decimal port>|<64 lowercase hex HMAC-SHA256>`. Its HMAC covers every field before the final separator. The device accepts only a response with the outstanding nonce, a valid HMAC, and a sender IP equal to the advertised host. It requests authenticated `/status` from the candidate before replacing its saved bridge address. Failed probes leave the previous pairing intact. USB `lan_config` remains authoritative and cancels any pending LAN candidate.

Discovery is local subnet broadcast; it does not cross routed VLANs or an access point that filters client broadcasts. The bridge can still serve the last paired address while discovery is pending. macOS bridges retain the existing USB-provisioned fallback but do not yet answer this Windows discovery protocol.

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

TAB5 .078 accepts optional `music.artworkWidth` / `artworkHeight`, the original artwork dimensions supplied to the bridge. Valid dimensions (1..32768) with aspect ratio above 1.25 select a wider landscape presentation; missing/invalid dimensions retain the square layout. The resource remains a 336×336 RGB565 aspect-fit canvas, so these fields describe source aspect ratio rather than resource dimensions. Windows strips them from legacy ESP8266 USB frames; older TAB5 firmware ignores them. No protocol-version or resource-format change is required.
# 系统监控增量样本（兼容扩展）

`systemMetrics` / `metrics.data` 可含 `sampleSession`（进程会话标识）、`sampleSequence`（单调递增计数）和 `samples`（最近最多 12 个 `{upload,download}` 原始 250ms 样本）。顶层上传/下载数值是最近 4 样本平均值，用于数字显示；CPU/内存最多每秒重算。

Windows USB 心跳省略 `samples`，快速 metrics 帧携带样本尾部；LAN 状态保留尾部。设备按会话/序号去重入队，每 250ms 消费一条，积压超过 16 条时每次最多消费三条。最多保留 32 条待绘制样本，超出时丢最旧条以追上实时状态。没有这些可选字段的旧发送端继续按 `updatedAt` 去重，不能因此宣称旧发送端具备增量补采能力。

`device_info` 的 `page_data` 增加只读计数：`system_chart_frames`、`system_chrome_draws`、`system_number_draws`、`system_samples_consumed`、`system_queue_depth`，用于真实刷新验收，不含用户数据。

`page_data` also optionally reports `effective_mode` (resolved display mode),
`rendered_page` (last rendering branch; `offline` when the bridge is stale), and
`stock_draws` (completed stock-data redraws). The top-level `mode` remains the
requested mode; it alone is not evidence of which page was rendered. Older hosts
may ignore these additional diagnostic fields. Manual selection wins on Windows,
macOS and firmware; attention/completion navigation remains enabled in AUTO.


### Additional domestic model quotas (v0.2.0)

Optional v1 `domesticQuotas.stepFun` carries the StepFun CNY API wallet, not Step Plan. Optional `domesticQuotas.baidu` carries one explicit Qianfan model resource package: `planPercent` is used/total and `planResetsAt` means package expiry, not renewal. Cloud wallets are excluded. New modes: `domestic_stepfun`, `domestic_baidu`. Older firmware ignores these fields and requires updating for the new modes. macOS preserves both optional fields and accepts the modes; native provider acquisition remains unimplemented.

`domesticQuotas.xiaomi` is another optional v1 field, carrying only MiMo Token Plan `planPercent` (used Credits, 0–100), `plan`, `updatedAt`, and `stale`; it never fills weekly/five-hour or balance fields. Mode `domestic_xiaomi` renders it on updated firmware. Windows collects console responses; macOS currently preserves the field/mode only. Missing fields stay unknown.

USB may omit null-valued optional fields inside each domestic quota object to stay within the existing 6144-byte limit. Firmware treats missing and null quota values identically; zero values are retained. LAN snapshots retain their full schema.

## TAB5 voice development extension (2026-09-23)

Paired TAB5 clients can POST `/tab5/v1/voice`. AES-256-GCM request and response packets use the existing TAB5 key and request nonce as AAD. Proof text is `POST|/tab5/v1/voice|deviceId|nonce|sha256(packet)` (lowercase hash); maximum encrypted request is 16384 bytes. JSON fields: `session`, `issuedAt` (UTC milliseconds, ±15 seconds), `op`; start adds `taskId`, subsequent requests add `voiceId`. Operations: start/poll/audio/stop/cancel/tab5. Audio adds sequential `seq` starting at zero and `pcm` (Base64 PCM16-LE, 16 kHz mono, at most 6400 bytes). Nonces are replay protected; malformed and out-of-order audio is rejected.

Replies expose source dji/tab5, state, message and bounded UTF-8 draft text in the encrypted response only. DJI capture is preferred; absent/unavailable capture falls back to TAB5. VB-CABLE routes either source into the separately configured Doubao input method. Capture stops after 4 seconds without requests or 60 seconds total. No automatic task submission. Runtime is disabled until configured; this is source/simulation validation, not real audio, Doubao or TAB5 hardware acceptance. Existing ESP8266 endpoints and settings are unchanged.

TAB5 0.2.22 USB 网络管理：`tab5_wifi_list` 返回 ssid/label/connected/selected，最多 5 项，不返回密码；`tab5_wifi_forget` 按配对 deviceId 和真实 SSID 删除，返回 tab5_wifi_forgotten 或 tab5_wifi_rejected。仅 USB 接收。
# TAB5 日历详情增补（0.2.26）

`calendar` 保留现有今日/明日字段，新增可选 `detail`：`terms[{year,dates}]`（24 节气的 MMDD）、`holidayYear`、`holidays[{start,end,name}]`（MMDD 范围）、`workdays[]`（补班 MMDD）、`birthdays[{name,month,day,lunar,leap,remind}]`。生日最多 32 项，提醒 0–30 天；只走已有鉴权 TAB5 状态通道，不改变 ESP8266 字段。

生日在电脑托盘「内容设置 → 日历与生日」管理，仅保存到用户目录。无对应闰月时按普通月份，缺少农历三十或公历 2 月 29 日时按当月末日。TAB5 本地计算下一次生日及月历；缺少官方假期年份显示待更新，不将节日名称等同于放假安排。


## TAB5 0.2.26 回复内容标识

`codexTasks.replyView.hasText` 和加密读取回执内的 `replyView.hasText` 是可选布尔字段，表示当前分页有有效公开文字，false 表示等待/空内容占位。新版设备只在对应轮次的有效正文显示后清除未读，历史固定查看也适用；空占位继续重试。旧桥接缺少字段时设备兼容已知占位文字，旧设备忽略新增字段。读取轮询仍为每 3 秒，运行中的文字优先取桌面 IPC 公开消息。

## TAB5 OTA 接收端流控能力（0.2.29）

`GET /tab5/v1/ota/{sha256}` 保持 Device/Nonce/Proof 鉴权和镜像 SHA-256 校验。可选 `X-AIBot-OTA-Flow: tcp-v2` 开启 16 KiB 有界异步 TCP 写入，接收端包含合并写入和 P4 SDIO 包池修复；`tcp-v1`、缺失或未知能力值一律采用 2 KiB/64 ms 兼容流控。不能根据待安装版本或旧 USB 心跳启用全速。升级期间暂停 BLE 大帧，结束或失败均恢复。能力头不提供身份认证，不改变 ESP8266 协议。

TAB5 0.2.30 新增可选 `data.ota.notes`（纯文本，最多 1024 UTF-8 字节）。桥接读取固件旁 `<文件名>.bin.notes.json` 的 `{version,sha256,notes}`，版本及 SHA 均与镜像匹配才接受说明；未带说明文件的旧固件保持兼容。设备升级页先显示版本号，再显示可滚动的更新内容；缺失时显示「暂无更新说明」。

### TAB5 0.2.38 运行版本回报

`GET /tab5/v1/status` 在既有设备/nonce/GET HMAC 鉴权之外，可带 `X-Tab5-Firmware`（最多 31 个 ASCII 字母、数字、点、横线或下划线）和 `X-Tab5-Firmware-Proof`，后者是配对密钥对 `FIRMWARE|{nonce}|{version}` 的 HMAC-SHA256 小写十六进制。缺失/无效附加证明不采纳版本，但保持旧状态请求兼容。USB 使用已校验设备 ID 与序号的 `tab5_ack.firmware`。版本观察有效期 15 秒，仅用于桥接升级窗口描述，不作为 OTA 完成、镜像完整性或启动健康的证明，不触发自动取消或升级。


## TAB5 0.2.39 USB/BLE application RPC (candidate)

New GATT characteristic `7af50005-7f23-4a91-bc65-667a19320101` uses the voice mailbox wire framing independently: request id uint32 LE, offset/total uint16 LE, then bytes. USB advertises `rpcVersion:1` in hello/ack and tunnels the same fragments as base64 in `@AIBOT {version:1,type:tab5_rpc,deviceId,tag,data}` (null data means read); replies echo type/deviceId/tag plus ok and optional data. Request fragments are at most 480 bytes for BLE and 2048 for USB. Advance commands must match the previously read end. Response fragments require the same id, consecutive offsets and constant bounded total.

Requests contain nonce ASCII32 + HMAC ASCII64 + AES-256-GCM packet, at most 12288 bytes including authentication. HMAC input is `POST|/tab5/v1/rpc|deviceId|nonce|sha256(packet)`. The nonce is GCM AAD; the entire `{status,body}` response is encrypted with it (maximum 32768 bytes). Session, issuedAt (-90/+10 seconds) and replay checks precede dispatch. One RPC has a 75-second deadline. No cross-transport automatic resend.

### TAB5 0.2.40 telemetry

0.2.44 compatibility additions: USB/BLE `kind:codex, op:read, inlineReply:true, rpcReplyVersion:1` requests may receive `{taskId,text,replyView,...,rpcReplyVersion:1}` inside the existing nonce-bound encrypted RPC `{status,body}` envelope. This omits redundant inner encryption/base64; HTTP never enables this response format, and legacy RPC keeps `encrypted`. Old bridges remain readable by the new firmware. All authentication, session/time and replay checks remain mandatory.

TAB5 metrics and full snapshots now retain 64 raw 250 ms history samples, allowing delayed radio delivery to backfill approximately 16 seconds. On compression-safe receivers, `tab5_packed` may contain `tab5_metrics` as well as full state, with the same non-nesting, exact-size and session requirements. CPU/memory and instantaneous header values are independent of the device's one-point-per-second, two-second moving-average graph. Missing history beyond the retained window stays a gap.

Safety revision in 0.2.42: 0.2.40/0.2.41 used a ROM convenience inflater whose ~11 KiB stack workspace exceeded the receiver task stacks. The bridge must send those versions plain full snapshots, while metrics packets remain supported. Packed Wi-Fi responses require authenticated firmware version >=0.2.42; packed BLE responses require `telemetryVersion:2`. BLE capability 1 supports metrics but receives plain full state. The new receiver allocates its inflater workspace on the heap and requires DONE, exact output size and full compressed-input consumption.

0.2.41 upgrade policy: authenticated BLE RPC rejects `kind:ota` with encrypted status 409 / `firmware_requires_usb_or_wifi`; all other BLE operations remain available. OTA offers can still arrive over BLE. The device selects a fresh authenticated Wi-Fi link first, then a ready USB RPC link, independently of daily mode, and retains that transport for the entire installation. This is a preference among available links, not an automatic mid-download retry. A fresh Wi-Fi snapshot and an associated network are required; association alone is insufficient. Legacy 0.2.39/0.2.40 devices need manual Wi-Fi selection to bypass their USB-first routing.

USB hello/ack and BLE info advertise `telemetryVersion:1`. Wi-Fi selects the new format only after validating the existing firmware-version proof for 0.2.40 or newer. Legacy peers retain full status packets. `tab5_packed` carries `{version:1, sequence, size, payload}`: payload is base64 zlib of a full `tab5_status`; exact expanded size must be 1..32768 bytes and nested packed envelopes are rejected. Radio authentication/encryption is unchanged.

`tab5_metrics` carries `{version:1,deviceId,session,sequence,systemMetrics}`. It requires an existing full snapshot in the same paired session, rejects older sequence numbers, and acknowledges exact retransmissions. It does not refresh full-state heartbeat, replace task/session data or change OTA offers. USB returns `tab5_metrics_ack.sequence`; BLE retains the authenticated frame ACK. The producer shares the original 250 ms system sampler. USB has a separate bounded 250 ms worker; Wi-Fi polls at that target interval on the system page; BLE adapts to actual transfer completion and skips obsolete samples. Full status remains independent and is compressed on capable Wi-Fi/BLE receivers. Full authenticated snapshots now update OTA offers on all three transports.

`kind:codex` reuses existing operation validation/journal; `kind:image-chunk` binds taskId/requestId/size/sha256/offset/data, at most 6144 decoded bytes per chunk and 2 MiB total, with exact duplicate acceptance and existing image decoder/storage checks; `kind:ota` requires the active offerId/sha256 and an exact valid offset/count (1..8192). Withdrawal or replacement rejects later reads. The firmware performs all existing inactive-partition, total SHA, project/version, boot and health checks. Legacy devices and ESP8266 frames are unchanged. Old TAB5 firmware requires an initial USB installation or Wi-Fi OTA to gain the new receiver. These are candidate capabilities verified by simulations, not a claim of hardware acceptance.

## TAB5 0.2.70 bounded bulk RPC

This extension supersedes only the historical RPC size limits above when explicitly negotiated. Authenticated per-link state advertises both `data.rpcBinary=1` and `data.rpcBulk=1`. New requests set `binaryReply:true,bulkReply:true`; `T5R2` uses the same `magic[4] + metadataLength[u32 LE] + JSON + raw bytes` framing as `T5R1`, with metadata 2..4096 bytes and raw payload 1..49152 bytes. T5R1 retains 8192-byte uploads/16384-byte replies. JSON fallback and old-device behavior remain unchanged.

RPC mailbox total (including nonce32/proof64 on requests) is bounded by 65535, matching unsigned 16-bit offsets/total. Encrypted HTTP requests are bounded by 65439 and encrypted replies by 65535. Only image-chunk and benchmark accept request raw data; only negotiated bulk benchmark/OTA replies can exceed old limits. HMAC, AES-GCM, session/time, nonce replay, exact offsets, payload integrity and the 75-second deadline are unchanged. Photo/OTA retry a smaller same-channel block only after explicit `invalid_range`, never after an uncertain outcome. Voice keeps its original buffers and 12-second deadline.

BLE credit windows remain 1..8 packets, maximum 488-byte ATT payload. Shared host access is held per read/grant/response window. Response command writes are awaited in order; each window ends in WriteWithResponse. USB binary fragment size remains 16384 bytes (16393 with response header); larger logical requests use multiple existing fragments.

The corrected 0.2.71 bridge retains the original complete-window grant ordering. An early-refill experiment failed hardware acceptance and was withdrawn. Large inbound RPCs defer periodic BLE telemetry for at most three seconds; voice polling and response barriers remain independent. Local diagnostics record only notification counts and timing, with no payload content.

USB hello/ack adds `usbPacked:1`. Only this capability permits `tab5_packed` full status over USB, acknowledged as `tab5_ack` with matching device and sequence. Metrics remain plain `tab5_metrics`/`tab5_metrics_ack`. Receiver rejection returns `tab5_ack_rejected` or `tab5_metrics_ack_rejected`; no freshness is advanced on rejection. Expanded status remains bounded by 32768. Old receivers get raw status. See [implementation and validation](TRANSPORT-PERFORMANCE-070.md).

071c 本地诊断候选：仅设置进程环境 AIBOT_TAB5_BLE_THROUGHPUT=1 时，在已认证 BLE 连接内尝试 Windows 11 ThroughputOptimized；空闲 2 秒释放并恢复 Balanced，失败保留原通道行为。pcPreference 仅报告请求生命周期，不能作为吞吐验收结论。USB 启动核验通过后读取现有 tab5_crash_status 数值诊断，将 previousOtaMs=[总接收写入,读,写] 保存到本机诊断；读写并行，后两项不相加。未修改线上消息格式。

0.2.73 候选：撤回 071c 的 Windows 吞吐偏好实验，保留数值 OTA 核验诊断。新增 rpcMailboxWindow=32 表示 RPC 支持最多 32 片完整窗口；mailboxWindow=8 仍用于语音。新桥接在字段缺失时沿用旧窗口，旧桥接继续最多 8 片。命令 3 的 credit 仍有上限，语音端拒绝超过 8，RPC 端拒绝超过 32；每批完整接收后才授权下一批。mailboxStats=[已提交通知,分配失败,提交失败,全局空闲 mbuf 最低值]，最低值 65535 表示尚无通知。
073b 修正：.073 真机 32 片 RPC 测速发生通知提交失败；桥接将 rpcMailboxWindow 的实际协商上限限制为 8，即使固件声明 32。固件仍接受旧的 8 片请求。当前恢复方案等待真机复测，不将 32 片作为已验收能力。
0.2.74 候选新增 rpcNotifyWindow=32，设备在 host callout 按空闲 MSYS 容量分段提交授权通知；rpcMailboxWindow=8 仍限制电脑回写。通知和回写独立协商，新字段缺失时通知沿用最多 8。rpcPacing=[yield次数,终止次数,最近通知错误码]。单窗口最多 3 秒，断开/错误终止，不重发可能已部分发送的异常通知。等待真机验收。

0.2.83 将 BLE Info 身份属性限制为最多 512 字节（ATT 属性值上限，不随 MTU 517 增长）。保留全部认证、固件及资源校验字段；不再返回可选 link、mailboxStats、rpcPacing 性能统计，省略与 mailboxWindow=8 重复的 rpcMailboxWindow。现有 Windows 客户端在字段缺失时使用 mailboxWindow，因此回写仍为 8 片，rpcNotifyWindow 仍为 32。最大字段组合生产回复为 492 字节，格式化溢出返回资源错误，不能发送截断 JSON。
# TAB5 0.2.75 显示诊断扩展

`displayDiag` 保留原 52 列，追加索引 52～55：完整普通页面快照数、完整预旋转页面快照数、分片重建被更新打断的累计次数、当前 PSRAM 空闲字节数。计数描述已发布的完整图片，可能是手势允许复用的旧内容；不能把它解释为所有页面都已更新至最新状态。桥接沿用字符串透传，不改变状态协议版本或控制命令。

0.2.77 诊断候选保留前 56 列，追加 `displayDiag[56..74]`：schema（1）、上次 RTC 记录有效位、CPU0/CPU1 ROM 原始复位值，然后依次为 UI、刷新、LCD 提交/等待、缓存、Flash 五组 `(stage, atUs32, detail)`。同样的 19 个数字通过 `tab5_crash_status` 的 `trace.previousRuntime` 返回。UI/缓存/Flash 的阶段 1 为进入、0 为完成；刷新 1/2/3/0 为开始/渲染开始/渲染完成/刷新完成；LCD 1/2/3/0 为提交开始/提交返回/等待开始/等待完成。detail 为页面编号，Flash 为 0。各时间为上次运行低 32 位微秒，约 71.6 分钟回绕；独立通道是尽力保存的进度线索，不是调用栈，提交返回也不等于屏幕已完成传输。桥接继续透传字符串，老固件可以缺少尾部字段。

## TAB5 0.2.92 USB OTA extension

USB hello/ack adds `rpcUsbBinaryChunk:65526` while retaining `rpcUsbChunk:16384` for JSON and old bridges. Only `rpcUsbBinary:1` plus the new exact capability enables large fragments. `tab5_rpc_binary.chunk` accepts 2048, 16384 or 65526, defaults to 16384 when absent, and bounds writes to chunk+9 and reads to chunk+8. The AIB2 header and 65535-byte mailbox remain unchanged. A 48 KiB encrypted bulk OTA reply now fits one binary write. Old devices receive only their negotiated small fragments.

USB sends are bounded to three seconds. An incomplete binary send or expired receive rejects further bytes until a CDC close/open (DTR edge) or physical reconnect clears partial framing and cancels the old mailbox. OTA range reads alone use ten-second waits and at most two retries after no valid reply, retaining exact offer/hash/offset/count and generating fresh authentication. Permanent errors and offset/length mismatches stop immediately; the existing explicit invalid_range downgrade remains. No voice, draft, send or other mutation gains retries. Flash staging tries 64/16/8 KiB internal buffers while retaining 96 KiB free; all image checks and inactive-partition protections remain. These rules supersede only the historical USB fragment and OTA deadline/retry statements above.

USB 新能力只在明确协商后启用；旧桥接和旧固件继续使用小块。固件数据块缺少有效回复时最多重试两次，保持相同镜像与位置，不重试发送或语音操作。接收偏移错误或永久错误立即停止，保留原启动分区。0.2.92-ui 构建与故障模拟不代表真机速度或稳定性通过。

### 0.2.94 OTA buffering and erase scheduling

No wire-format change. The client owns two 48 KiB receive blocks, each matching one negotiated bulk range; older peers can fill them using smaller authenticated ranges. Internal flash staging prefers 48 KiB, then 16/8 KiB while retaining 96 KiB free. The validated image size is passed to `esp_ota_begin` to erase only the required backup-partition extent before reception, independently of staging size. Existing SDK rollback/running-partition guards and final SHA/image/boot validation remain. Retained OTA elapsed and flash-write timing now include this preparation erase; do not compare a download-only timer against whole upgrade time. Physical performance acceptance is pending.

## TAB5 0.2.90 电脑快捷控制

0.2.100-ui 配套桥接将 `stage-draft` 改为保留电脑原草稿并追加文字，仍不提交；重复请求只复用回执。快捷语音仅保留 TAB5 预览，不向 TAB5 本地编辑器回填。失败会返回 `composer_changed`、`composer_not_ready`、`composer_too_long` 或未确认写入/回读等具体原因。The .100 paired bridge appends stage-draft text to the existing desktop draft without submitting; request replay never appends twice. Quick Console recognition only uses its preview, leaving the TAB5 composer untouched. Structured error reasons distinguish changed, unavailable, oversized and unconfirmed drafts.

0.2.101-ui 配套桥接在 `stage-draft` 成功回执增加可选 `appended` 布尔字段：只有原输入框非空且合并正文回读确认时为真。固件还要求同一操作流程已成功回填过前一段，才显示追加提示。旧桥接没有该字段时保留通用检查提示，不推断追加。The .101 paired bridge adds optional `appended` to successful staging replies, true only when the original composer was nonempty and merged text was verified. Firmware also requires a prior successful take in the same flow before displaying the append hint; older replies fall back to generic review guidance.

0.2.101-ui 新增显式 `desktop.clear-draft`：UUID `requestId` 与明确 `taskId`，仅 TAB5 工具栏长按触发。电脑前台会话和主输入框必须与目标一致，不导航或发送回车；清空写入一次并回读确认，成功回执 `cleared=true`、`attempted=true/false`。实际尝试或确认已空后，原同会话的发送记录失效；相同请求 ID 复用回执，不重复清空后续新草稿。协议版本仍为 1，仅 Windows 支持。Candidate .101 adds explicit `desktop.clear-draft`, triggered by long press with a request UUID and explicit task ID. The current foreground target must match; it never navigates or sends Enter. A single write is verified by readback; confirmed empty inputs need no write. Attempted or confirmed clearing invalidates the old send tickets. Replayed requests return their receipt without clearing a subsequent draft. Protocol version remains 1; Windows only.

新增已认证 `kind=desktop` 的 `open-recent` / `stage-draft` 操作，以及语音回执 `canInsert` 字段。参数、去重窗口和失败语义见 [快捷控制协议](TAB5-QUICK-CONSOLE.md)。`stage-draft` 仅预填电脑草稿，不调用任务发送接口。USB、Wi-Fi、BLE 复用现有加密 RPC，ESP8266 协议不变；当前仅 Windows 桥接支持，macOS 不新增此能力。

0.2.100 配套桥接的语音回执增加可选 `sourceName`，来自实际选中并成功打开的 Windows 录音端点全称。自动回退时改为 `TAB5 内置麦克风`；`source` 仍为 `dji` / `tab5`，路由不变。旧端可忽略新增字段；没有名称时新固件保留原状态文案，不猜测型号。Codex 直达状态栏展示全称，过长时横向滚动。

0.2.91-ui 的直达按钮省略 `open-recent.taskId`，每次新点击在电脑解析最新会话；回执补充 `folder`、`updatedAt`，面板与语音绑定返回的 `taskId`。同一请求重放保持首次解析目标，`stage-draft` 仍要求明确目标。The 0.2.91-ui direct button omits taskId to resolve the latest conversation on each new click. Replies add folder and updatedAt; voice binds to the returned ID. Replays retain the original resolution, and staging always requires an explicit target.

0.2.106-ui 起，上一条/下一条和 Codex 按钮通过现有 `open-recent` 显式传入所选 `taskId`。箭头立即请求电脑切换，回执成功且目标一致后更新 TAB5 选中项；本次面板列表不因打开会话而重排，重新进入时按目录最新顺序初始化。失败或错目标回执保留原选中项，重新确认前阻止语音录入；同一已确认会话的再次唤起保留未发送的回填记录。沿用现有 Windows 显式目标能力，无新增协议字段或版本。From .106, arrows and the Codex key explicitly target the selected conversation using existing open-recent semantics. Commit selection only after matching foreground confirmation; preserve list order during a visit and refresh recency on reopening. Unconfirmed navigation blocks voice/input, and reopening the same confirmed target preserves its staged receipt. No wire-format or protocol-version change.

0.2.99-ui 开发候选新增显式 `desktop.submit-draft`：要求原 `taskId`、本次 UUID `requestId` 与成功回填的 `draftRequestId`。仅手动第三次点击触发一次受控回车；15 分钟内同会话的回填记录在实际输入尝试时消费，桥接重启后不保留。成功回执为 `submitted=true,attempted=true`（回车及输入框清空已确认）；不确定结果不自动重试。取消或重新录音不提交，电脑草稿不清除。协议版本仍为 1，仅 Windows 支持。Candidate .099 adds explicit submit-draft with the staged task and one-use draftRequestId. Only a separate click presses Enter; successful staging expires after 15 minutes or bridge restart, and uncertain input is never replayed. Confirmation means Enter plus a cleared composer, not model completion. Reset never clears a desktop draft or submits it.

During an OTA transfer the matching 0.2.96 desktop bridge suspends high-rate USB metrics, inline artwork and resource bursts. Full status heartbeats still renew the established session and clock; authentication, USB failover and boot checks are unchanged. Pending artwork resumes after the installed offered version is observed or the bounded transfer lease expires. USB diagnostics report cumulative and peak serial-gate wait in milliseconds. This is scheduling policy, not a wire-format change; physical effect requires a complete USB upgrade. Retained otaMs/previousOtaMs describe preparation through the end of receive/write, including preparation erase since 0.2.94; they exclude final image validation, completion dwell and reboot, so they are not end-to-end wall time.


### 0.2.97 OTA preparation and display continuity

USB hello/ack optionally adds `otaDiag:"prepareMs,bufferSlots,lcdUnderrunsDuringUpgrade,busy"`. Values describe the current boot/attempt and reset on reboot; capture them before restart. Old bridges ignore the field and new bridges display `--` when absent. Protocol version remains 1.

The receiver authenticates and fills a first owned 48 KiB block, then waits for the preparation overlay to render before calling the SDK known-size backup-partition erase. Reception continues through a bounded PSRAM pool, retaining 4 MiB for UI/transport when expanding beyond the original two-block fallback. ESP32-P4 PSRAM XIP keeps the instruction/rodata cache available during flash operations. SDK rollback/running-partition guards, ownership/join rules, final SHA/image/boot checks and user confirmation remain. An initial read, UI preparation or receiver-start failure causes no flash write/erase or boot-partition change.

首轮安装 0.2.97-ui 仍由旧接收程序执行；须在重启后的下一次真实升级测量新流程。构建及故障模拟通过不代表开头停滞或蓝闪已通过真机验收。

### 0.2.103 OTA phase timing

USB `tab5_crash_diagnostic.trace` optionally adds `otaPhases` and `previousOtaPhases`. Each is `null` when unavailable, otherwise a numeric object with `transport` (`-1` none, `0` USB, `1` Wi-Fi, `2` BLE), `prepareMs`, `writeMs`, `freeWaitMs`, `readyWaitMs`, `verifyMs` and `installMs`. Preparation is the SDK backup-partition erase; write excludes that preparation. Free wait is receiver backpressure waiting for a reusable block; ready wait is the writer waiting for a received block. Verify covers final SDK image validation, identity and boot-partition selection. Install spans worker start through successful boot-partition selection or the recorded failure, excluding the success-screen dwell, reboot and healthy boot. Reader and writer run concurrently: these values must not be summed. Existing `otaMs` and `previousOtaMs` retain their meanings. Unknown fields are ignored; no image, credentials or user text are added.

0.2.103-ui 增加上述可选分阶段耗时，重启后以 `previousOtaPhases` 保留，缺失时为 `null`。分区准备与纯写入分开统计；等待空闲缓冲表示接收端背压，等待已接收缓冲表示写入端等待数据。`installMs` 不含成功页面停留、重启和健康启动，接收与写入重叠，不能直接相加。首轮安装由旧接收程序执行，须再升级一次才能取得新计时；协议版本仍为 1，旧字段、鉴权、镜像校验及回滚规则不变。

0.2.108-ui adds optional root-level `tab5_crash_diagnostic.flashBenchmark`, outside `trace`. It is null before a manual local test or a numeric object with `state` (1 running, 2 complete, 3 failed, 4 cancelled), `sampleBytes` (131072), `partitionAddress`, `offset`, `jedecId`, `pageBytes`, signed `error`, and 0/1 `restored` / `modified`. `rows` contains exactly four entries in 8/16/48/64 KiB order, each with `chunkBytes`, `completed` (0–3), `skipped` (0/1), and three-element microsecond arrays `eraseUs` / `writeUs`. Interpret only completed rounds; zero slots may be unfinished. Results exist only in the current boot and are read through existing startup verification, never a new remote write command. The diagnostic line is bounded to 6144 bytes on the new bridge; ordinary small replies keep their 4096-byte cap. No user text, flash contents or credentials are exported. Old bridges ignore unknown fields; protocol remains version 1. See [measurement method and limits](TAB5-FLASH-BENCHMARK.md).

新增的 `flashBenchmark` 位于 USB 诊断根对象，只记录本次启动的数值，状态、跳过项、三轮擦除/纯写入微秒及恢复确认均显式给出。设备必须手动开始，测试仅触及经校验备用镜像之外的空白尾区；小样本不能证明持续写入上限。电脑按数值白名单读取，普通小消息限长不变；鉴权、OTA 镜像检查和回滚不变。

0.2.110-ui adds optional unsigned `phase` (0 checks, 1 blank-range scan/backup, 2 measurement, 3 restoration) and `scanned` (candidate windows checked). A scan read failure preserves its SDK error; no suitable all-FF 128 KiB window returns `ESP_ERR_NOT_FOUND` (261). Search windows are 64 KiB-aligned, beyond the verified spare image plus a 4 KiB guard. No mutation occurs during scanning. Older results without the optional fields remain accepted.

0.2.110-ui 增加可选无符号 `phase`（0 前置检查、1 空白区查找/备份、2 测量、3 恢复）及 `scanned`（已检查候选窗口数）。读取失败保留 SDK 原始错误，无合适空白区返回 261；只在已验证镜像及 4 KiB 保护间隔以外按 64 KiB 对齐寻找完整 128 KiB 全空白区，扫描不写入。缺少新字段的旧结果仍兼容。

0.2.107-ui 仅调整本地升级反馈：进度按成功写入闪存的字节计算（校验通过前最多 99），不再按网络接收量推进。下载完成后的剩余写入明确显示；最终 SHA/镜像/身份/启动检查单独显示“正在校验固件”与实时耗时，不附带 99% 文案。成功后才显示 100% 并等待原重启流程。协议、传输参数、完整性校验、回滚和既有分阶段计时语义不变；首轮装入 .107 仍使用旧接收程序，下一次升级才应用新反馈，不声明实际提速。

The .107 local progress display follows successfully written flash bytes, capped at 99 before verification. Explicitly show pending writes after completed reception and a timed verification stage without a stuck 99% label. Show 100 only after all existing checks and boot-partition selection succeed. Transport, protocol, integrity, rollback and phase-timing semantics are unchanged. Installing .107 still uses the previous receiver; the new feedback applies to the following upgrade and does not claim faster installation.

## Independent large-response write window (0.2.112-ui)

The BLE identity optionally advertises `rpcWriteWindow:32`, independently of `mailboxWindow:8` and `rpcNotifyWindow:32`. New Windows bridges accept integers 8..32 only; absent/invalid values retain the existing RPC write bound. Only RPC responses over 12288 bytes use this limit; old clients, voice, small replies and acknowledged-only peers retain their previous behavior. Within a negotiated window, at most seven native write commands are pending, each cohort drains before the next, and the final fragment uses WriteWithResponse. Any command/submission/cancellation failure drains pending operations and prevents further submission; no automatic replay. Device ID/cursor/total/encryption checks and response buffer size are unchanged. Notification grants and their pacing are unaffected. Identity remains limited to 512 bytes (the maximum-width .112 production case is exactly 512). Hardware preflight is required; this capability does not claim a measured speed.

BLE 身份新增可选 `rpcWriteWindow:32`，只协商超过 12288 字节的 RPC 回复回写窗口；与语音八片、通知三十二片独立。新桥接只接受 8..32 整数，缺失或无效则沿用旧限制；旧桥接忽略新字段。每组最多七个原生无响应写入，排空后才提交下一组；每窗口末片仍要求 ATT 确认，失败/取消不继续提交且先排空。身份最大组合 512 字节，接收缓冲、游标及鉴权规则不变。实际速度需短时预检。

## BLE OTA preflight / 蓝牙升级短时预检（0.2.111-ui）

### Optional write-window comparison / 可选确认窗口对照（0.2.114-ui）

.114 changes the optional identity value to `rpcWriteWindow:64`; its maximum-width identity still fits 512 bytes. New bridges accept offered bounds 8..64, default to `min(offer,64)`, and permit an explicit isolated RAM comparison at 32/64/32 only after observing an offer of at least 64. Older bridges treat 64 as unsupported and fall back to their prior eight-packet bound. No receive-buffer growth or change to outgoing notification windows. Keep seven native commands pending, drain each cohort and acknowledge the last fragment of each selected window. Freeze the selected limit at response offset zero, capped by the peer offer; a selector change affects the next response only. At MTU517 a 49317-byte response uses two barriers at 64 instead of four at 32. Authentication, offsets, full-byte checks and no-replay behavior are unchanged.

.114 仅将可选回写窗口能力改为 64，最大身份仍为 512 字节。新桥接接受 8..64，默认取设备能力与 64 的较小值；显式 32/64/32 隔离预检要求已观察到 64 能力。旧桥接回退原八片限制。接收缓冲和通知窗口不变，最多 7 个原生写入，仍按组排空并做窗口末尾确认；每个回复开始时固定窗口且不超过设备能力，中途选择变化只作用于下一回复。MTU517 下约 48 KiB 回复的确认从四次变为两次，真实速度需实测。

The in-memory comparison diagnostic is renamed `蓝牙参数对照` with schema2, `kind` (`nativeQueue`/`writeWindow`), `activeWriteWindow`, and per-sample `writeWindow` in addition to native concurrency and the existing result/radio/isolation/trace fields. Optional `gatt.WriteWindow` reports the actual window for each queued bulk reply; zero means unused. It is not a firmware/USB protocol field. Following two complete hardware window comparisons, production defaults to native7/window64 (capped by the peer offer) after success, incomplete data, exceptions or cancellation. The native-queue comparison still fixes its wire window at32, independently of the production default. Existing pass and total deadlines remain. / 本地诊断改名并升为 schema2，分别保存排队上限与确认窗口，`gatt.WriteWindow` 记录实际大回复窗口；它们不是设备线上字段；两轮真机对照通过后，生产默认窗口 64/并发 7，且不超过设备能力，各种退出均恢复此默认值。原生排队对照内部仍固定窗口 32。

### Host-only queue comparison / 仅主机排队对照（历史 schema1）

The explicit desktop comparison temporarily tests native pending-write limits 7/31/7 while retaining cohort draining, the negotiated wire window (at most 32 fragments), authentication and final ATT confirmation. Each pass uses the existing isolated preflight; no new wire action or firmware capability is introduced. Default seven is restored on every exit; incomplete data or Wi-Fi restoration stops remaining passes. Total cancellation budget is 120s plus bounded cleanup; existing per-pass limits remain. No persistent tuning or automatic OTA.

电脑显式排队对照仅临时采用 7/31/7 原生写入上限；每组排空、线上最多 32 片窗口、认证及最终确认不变。复用隔离预检，无新增固件协议；任何退出均恢复默认 7，结果或 Wi-Fi 恢复不完整时不继续下一组，总取消期限 120 秒加有界清理，不自动应用参数或刷机。

Local diagnostic `蓝牙排队对照` is schema 1 JSON with `state`, `group`, `activeNativeLimit`, `plannedLimits` and at most three `samples`. Each sample contains `nativeLimit`, raw `result`, `radio`, `wifiIsolation`, bounded `trace` JSON string, `firmwareBytes`, nullable `estimateSeconds` and `complete`. States are running/complete/incomplete/cancelled/failed; complete means all RAM checks and driver restoration succeeded, not the 120s OTA target. The optional per-RPC `gatt.NativeLimit` is configured capacity (zero when unused), not observed peak concurrency. Snapshots contain timing/count data only and are memory-only; trace coverage must be checked against device bytes.

本地诊断 JSON 独立保存最多三组数值结果；`complete` 仅指 RAM 检查及驱动恢复完整，不代表整包目标达标。`gatt.NativeLimit` 表示当次 RPC 使用的最大配置上限，未使用时为 0，不等同实际并发或空中包数。数据仅保留在内存，主机 trace 仍须核对字节覆盖后比较。

### Optional Wi-Fi isolation / 可选 Wi-Fi 隔离（0.2.113-ui）

USB `tab5_benchmark` adds capability `bleOtaProbeIsolation:true` and action `ble_ota_probe_isolated`. It requires both base/isolation capabilities, fixed BLE mode and physical USB; old clients ignore optional fields and unchecked new clients use the original action. Same RAM data/verification and 20s transfer budget; preparation/restoration each wait up to 6s, active request expiry is 35s and the host deadline is 40s. The existing Wi-Fi worker calls stop/start without changing saved settings. Cancellation, mode/USB loss, expiry and normal completion restore. Failed start retries every second; pending restoration prevents new tests and final success. Restored means driver started, not AP associated. Phase results `running;wifi_pause` / `running;wifi_restore` and failures `failed:isolation_mode`, `failed:isolation_busy`, `failed:isolation_prepare`, `failed:isolation_restore` are additive. A stopped/blocked driver call is not a hard real-time guarantee.

USB 新增隔离能力与动作；新主机先核对能力，旧设备可取消勾选沿用原预检。仅蓝牙并保留 USB，暂停和恢复各等待最多 6 秒，传输原预算不变；35 秒请求期限和 40 秒主机期限独立。取消、模式/USB 变化、期限或正常完成均触发恢复；失败时每秒重试，未恢复不开始新测、不报告最终成功。恢复仅确认驱动启动，仍需核对路由器关联。保存配置、BLE 身份长度与空中协议不变。

Optional USB reply CSV strings are numeric-only, at most 256 characters each; schema is the first field. Missing fields mean unsupported; wrong schema/field count/non-numeric content is invalid. They never carry credentials, IDs or payload text. / 可选 CSV 全部为数值，首字段为版本，最多 256 字符；缺失表示不支持，版本/字段数/格式错误表示无效，不含凭据、标识或正文。

| Field | Schema 1 values in order / 字段顺序 |
| --- | --- |
| `bleRadio` (16 fields) | `schema,connected,intervalUs,latency,txOctets,rxOctets,intervalRequestRc,dataLengthRequestRc,phyRequestRc,connectionUpdateStatus,rxFragments,rxBytes,rxHandlerUs,rxSpanMs,maxRxGapUs,rejectedFragments` |
| `wifiIsolation` (7 fields) | `schema,stage,stopRc,startRc,heldMs,restored,expired` |

`bleRadio` link values are live at USB readback, not frozen at transfer completion; zero octets means the data-length event has not been observed. Capture counters reset on each preflight, stop before Wi-Fi restoration and remain until the next capture. `rxBytes` excludes the nine-byte BLE fragment header but includes encrypted RPC framing. Handler timing starts after the flat copy and includes parameter requests/lock waits, not pure CPU time. `rxSpanMs` runs from first handler entry to last exit; gaps run from previous exit to next entry. Unsigned 32-bit timer subtraction handles wrap within the bounded test. Metrics overlap and must not be summed as independent phases.

`wifiIsolation.stage`: 0 idle, 1 arming, 2 stop requested, 3 stopped, 4 restoring. Stop/start return codes begin at -1, success is 0; `heldMs` measures confirmed stopped time before restarting, excluding later retry time. `restored=1` confirms successful driver start, `expired=1` records lease expiry. Values are per-field atomic snapshots, not a transaction. A normal (unchecked) run does not reset the last isolation history: retain the selected run mode when interpreting results.

连接字段为读取时实时值；接收计数在本轮预检开始清零、结束停采且保留，字节包含加密 RPC 数据、不含九字节分片头。处理耗时含锁等待及参数申请，不代表纯 CPU；各计时不可简单相加。隔离阶段 0/1/2/3/4 分别为空闲/准备/申请暂停/已暂停/恢复中，返回码初始 -1、成功 0；暂停毫秒不含后续恢复重试。恢复标志不是联网确认。每字段原子读取不保证事务快照；常规预检保留上次隔离记录，分析时必须同时记录本轮勾选状态。

`tab5_benchmark` adds USB action `ble_ota_probe` and optional response capability `bleOtaProbe:true`. New clients query status before starting and reject unsupported firmware. The device requires fixed BLE mode and a fresh paired link, downloads three 256 KiB samples only over BLE, and stops on mode/session change without fallback. A 20-second aggregate budget plus a five-second exchange bound limits normal completion/failure to about 25 seconds. No Flash write or automatic upgrade occurs. Existing start/status/cancel results remain compatible. Authenticated `kind=benchmark` requests may include `otaProbe:true`; this selects the device's shorter wait, not a new authorization or response format.

USB 增加 `ble_ota_probe` 动作及响应能力 `bleOtaProbe:true`，新主机先读取能力再发起。设备要求仅蓝牙模式及新鲜认证连接，三轮各 256 KiB 下载；通道/会话变化即停止，不回退。总预算 20 秒、单次等待上限 5 秒，不写 Flash、不自动升级。旧 start/status/cancel 保持兼容。认证 benchmark 请求的 `otaProbe:true` 仅选择本地短等待，不改变鉴权规则。电脑使用最慢完整轮次及真实镜像大小，增加 20% 余量和 25 秒写入/校验/重启预算；估算不超过 120 秒才满足预检目标，不能代替完整 OTA 验收。

### Idle voice polling during BLE bulk replies

The optional local preflight trace counter `voiceDeferredCount` counts first voice-mailbox polls that yielded without a native GATT read after acquiring the shared gate. These are excluded from `voicePollCount`/`voicePollWallMs`; RPC counts and byte coverage are unchanged. No device wire field or capability changes. A real idle probe is permitted once 1000ms has elapsed since its last actual read, or immediately on first use, recent voice activity, or absence of bulk work. This is not a guaranteed response latency; lock, polling and GATT timing still apply. Fragment reads within an active request never take this deferral path. Authentication, notification error handling, request execution/deduplication,64/native7 and final ACK checks remain unchanged.

本地预检新增可选 `voiceDeferredCount`，仅统计取得共享锁后未执行原生读取而让行的首次语音邮箱轮询；不计入真实 `voicePollCount`/`voicePollWallMs`，RPC 计数和字节覆盖口径不变。没有设备线上字段或能力变化。距上次真实读取达到 1000ms、首次使用、有近期语音活动或没有大传输时允许探测；实际响应还受轮询、排队和 GATT 影响。正在处理的请求分片不走延期路径，鉴权、通知错误、去重、窗口和最终确认保持原规则。


## BLE OTA range compression (internal candidate, 2026-10-05)

Authenticated per-link state optionally advertises `rpcOtaZlib=1`, alongside `rpcBinary=1,rpcBulk=1`. Only BLE bulk `kind:"ota"` requests opt in with `acceptEncoding:"zlib"`. Existing SHA-256, offer ID, raw `offset` and raw `count` (1..49152) keep their meaning. A useful compressed response uses T5R2 with body `{offset,encoding:"zlib",decodedSize:count}` and one independent zlib stream as its raw payload. Compression precedes the existing authenticated encryption. If compressed payload plus 96 metadata bytes is not smaller, return the original `{offset}` and raw bytes. USB, Wi-Fi, requests without opt-in, and older peers are unchanged.

The receiver validates exact offset and integral decodedSize, requires negotiated encoding, and bounds output to the requested count. It requires successful zlib completion, checksum, complete input consumption and exact output size; unknown encodings, trailing/truncated data, oversized output and allocation failure abort the range. Only decompressed bytes advance the image offset or reach Flash; final image SHA-256/identity/boot checks remain mandatory. Host tests and firmware compilation pass; this is not device OTA acceptance. See [internal evidence](TAB5-BLE-COMPRESSION.md).

已认证状态可声明 `rpcOtaZlib=1`；仅 BLE 批量 OTA 请求明确接受 zlib 时压缩，每个原始范围独立编码，保留原偏移、长度、重试及认证。元数据给出 `encoding` 和 `decodedSize`；不划算时回退原文。设备严格验证协商、偏移、整数原始长度、完整输入消费、精确输出长度和校验，异常不推进写入。旧设备及 USB/Wi-Fi 保持原路径。内部测试不等于真机升级通过。

### 完整固件 RAM 预检 / Full-image RAM preflight (.116)

`quotaDetails[].weeklyResetEpoch` is an optional UTC Unix timestamp in seconds from the provider's weekly reset time. The Codex single-weekly-limit card uses it for a locally ticking `距重置` countdown; missing values hide the countdown, and an elapsed deadline displays `等待重置同步` until refreshed. `weeklyReset` remains the formatted absolute time in the reset panel. No elapsed-cycle metric is added.

`quotaDetails[].weeklyResetEpoch` 为供应商周额度重置时刻的 UTC Unix 秒数，可缺失。Codex 单周额度卡片使用本地时钟更新“距重置”；缺失时隐藏，到期后显示“等待重置同步”，不自行推算下个周期。右侧 `weeklyReset` 仍显示绝对重置时间。

USB `tab5_benchmark` replies advertise `bleFirmwareProbe:true`. Actions `ble_firmware_probe` and `ble_firmware_probe_isolated` snapshot the authenticated BLE offer (`sha256`, `offerId`, `size`) and use the production range decoder without opening any Flash partition. Authenticated RPC `kind:"ota_probe"` has the same offer/range/authentication/compression constraints as `ota`, accepts BLE only and never acquires the 90-second OTA lease. The bridge pauses background assets/metrics for the active full-image preflight and releases that pause on exit.

The device pins the BLE session, requires USB for control, limits transfer to 110 seconds (an in-flight range can take up to three 10-second attempts), and validates streaming SHA-256 over all decoded bytes. Optional Wi-Fi isolation has a 125-second lease; success is withheld until the worker confirms restart. USB/mode loss, cancellation and expiry request restoration without changing saved networks. The controller waits at most 160 seconds including bounded cleanup; no action starts OTA automatically.

Terminal result: `complete;BLE,down,1,<rawBytes>,<ms>,0,<calls>,<encodeMs>,<exchangeMs>,<decodeMs>,<wireTx>,<wireRx>,<minInternal>,<minPsram>,<minLargest>;image,<sha256>,<size>;`. Failure/cancellation use their existing states, and negative error codes cannot pass. The bridge requires complete bytes and the exact selected image hash, then estimates full OTA as receive milliseconds / 1000 * 1.2 + 25. The 120-second threshold remains a screening estimate, not hardware OTA acceptance. The legacy three-round raw benchmark remains separate.

完整固件预检仅接收、解压并计算整包哈希；桥接严格绑定所选镜像，失败不出升级通过结论。接收超时、分段重试、Wi-Fi 恢复和控制超时均有界。安装 .116 后可以对同一镜像做 RAM 预检；正常 OTA 仍拒绝重复安装当前版本，完整 OTA 验收应选择下一份真实升级候选，不能绕过该保护。

## TAB5 .121 voice transport candidate

Auto selection follows USB > Wi-Fi > BLE; fixed modes do not switch channels. Wi-Fi/USB audio requests now use the existing `codec=ima-adpcm` (16 kHz mono) decoder; BLE retains `ima-adpcm8k`. Independent blocks contain LE16 sample count, LE16 predictor, index, reserved byte, then low nibble first. At 3200 samples the block is 1606 bytes before Base64 instead of 6400 raw PCM bytes. This is lossy coding, not byte-exact PCM. Sequence validation, 60-second limit, stop/tail draining and no automatic audio retry remain unchanged. Paired bridge required; hardware continuity/recognition quality pending.
# TAB5 daily gallery RPC extension

Authenticated `/tab5/v1/rpc` requests may use `kind: "gallery"`. Existing pairing, encrypted envelope, session, timestamp and replay validation apply unchanged. This is a device-initiated pull extension; it does not change the existing three asset slots or ESP8266 frames. Unsupported bridges return an error and the device retains its cached image.

- Common fields: `category` (`painting` / `calligraphy`), local `date` (`YYYY-MM-DD`), zero-based `frame`.
- Optional `orientation` is `landscape` (default) or `portrait`. It selects a second layout of the same work and frame, without changing daily order or pagination. Portrait artwork is composed at 720×1280 and encoded rotated into the existing 1280×720 JPEG transport. Both manifest and read must carry the same orientation; the manifest echoes it. Unsupported portrait assets return 503; invalid orientation returns 400. A portrait-capable device rejects a portrait response without the matching echo, preserving its cached content when paired with an older bridge. Existing landscape callers need no changes.
- `op: "manifest"` returns `id`, `title`, `author`, `source`, `license`, `date`, `size`, `sha256`, `frames`, `width:1280`, `height:720`.
- `op: "read"` additionally requires the manifest `sha256`, `offset`, and `count`. It requires `binaryReply`; `count` is at most 8192, or 49152 with `bulkReply`. The binary reply metadata carries `offset` and actual `count`. Stale SHA returns 409; invalid ranges/category/date/frame return 400. Missing local files return 503.
- The bridge returns 409 while foreground transfer or voice work has priority. The client checks the complete SHA-256 and exact decoded dimensions before presenting a new frame. Failure does not clear existing content. Artwork order is `(DateOnly.DayNumber - 2026-01-01.DayNumber) mod categoryCount`, normalized to a nonnegative index.

On TAB5, only daily-art screensavers use BMI270 orientation. A stable 350 ms gravity direction (sampled every 100 ms) selects either portrait direction or the original landscape layout; near-flat/diagonal/shaking/stale samples retain the current orientation. A dominant in-plane gravity component of at least 4000 LSB permits ordinary inclined stands; no Z-dominance restriction applies. The UI checks orientation on its 50 ms clock tick and keeps at most two decoded frames, prefetching the matching alternate orientation of the current page. Positive raw Y selects the 180-degree counterpart of the pre-rotated portrait bitmap (corrected after .127 hardware feedback). Touch-to-exit and all other pages retain the existing landscape coordinates. Local USB diagnostic `tab5_orientation_status` returns `tab5_orientation_diagnostic` with readiness, orientation (0 normal landscape, 2 inverted landscape, ±1 portrait), raw accelerometer values, error and sample age; it does not change settings.

每日图库通过既有配对加密 RPC 按需拉取，不扩大文件路径或网络地址访问权限。作品与落款同帧传送，校验、解码成功后整体替换；各屏保分别缓存，失败保留最近画面和日期。上述能力首版由 Windows 桥接提供。

USB hello/ack also exposes optional artOrientation as ready,orientation,rawX,rawY,rawZ,error, surfaced by the bridge diagnostics for live hardware verification. Older devices omit this field.

TAB5 .127 extends `artOrientation` CSV with `stage,i2cError,register,chipId,attempts` after the original six fields. Stage 1 identifies the chip; 2 loads Bosch configuration; 3/4 get/set acceleration configuration; 5 enables acceleration; 6 samples. `tab5_orientation_diagnostic.diagnostic` carries the same CSV. Initialization retries after 1 second for the first two failures, then every 30 seconds; ten consecutive sample failures trigger reinitialization. No orientation is trusted until a fresh sample succeeds.

TAB5 .130 uses local orientation 0 for normal landscape (positive raw X, verified in a fixed user-held pose), 2 for inverted landscape (negative raw X), and retains the verified raw-Y portrait signs. The earlier .129 X-sign assumption is superseded. Both landscape directions share the original landscape image and frame cache. The bitmap and date overlay rotate together by 180 degrees; no additional gallery asset or RPC orientation value is introduced.

Local .135 appends an optional `|gallery:` section to the opaque USB `displayDiag` string: stage, error, received/expected byte counts, attempts/successes, free PSRAM and its largest free block. Stages 1–9 identify manifest, transfer, hash, JPEG header, output allocation, cache synchronization, decoder creation, decode and completed output. Existing numeric diagnostics precede the delimiter. Windows surfaces the string as before; consumers that do not display diagnostic strings need no change. No command, image envelope or protocol version changes. Local Windows diagnostics also report the latest authenticated gallery request and result without logging pairing material or image bytes.

Local .136 also appends `|dsi:panel=...;clock=...;div=...;events=...;host0=...;host1=...` before the optional gallery section. The panel name comes from the initialized BSP; clock (integer MHz) and divider are read from the active clock registers. `events` counts frame callbacks observing nonzero DSI host error status, while `host0` and `host1` accumulate the corresponding hexadecimal status bits. These are separate from bridge FIFO underruns; a zero underrun count does not exclude DSI host or panel errors. The existing numeric prefix and opaque-string consumer contract are unchanged.

Local .137 adds `;hline=...` to the DSI section. Its shadow-register read returned zero on hardware and did not verify timing. Local .138 corrects it to the horizontal-line configuration register (lane byte-clock cycles); it is not a direct PHY bit-rate measurement or proof of physical-screen stability.

Local .138 adds the paired-USB-only diagnostic `tab5_display_pattern`, requiring `version=1`, matching `deviceId`, and numeric `enabled` of exactly 0 or 1. A successful 1 temporarily uses the DSI host color-bar generator, bypassing LVGL pixels/PSRAM scanout, and restores ordinary output after 20 seconds even if the USB client disconnects. 0 restores immediately. The `tab5_display_pattern_result` reply contains `error` and `timeoutMs=20000`; invalid requests are rejected without changing output. Existing bridge clients do not issue this command. The Windows diagnostic counterpart was `scripts/diagnose_tab5_display.py` (now archived under `artifacts/development/gallery-preview-sync/local-140/retired-display-experiments/`); `displayDiag` exposes `pattern` and `patternError`. No settings, image caches, credentials or firmware partitions are modified by the test.

Local .139 appends `;qos=DW,CPU,CACHE,DMA2D` read arbitration priorities to the DSI diagnostics. These values are read from AXI registers; they are not bandwidth measurements. The numeric diagnostic prefix and existing opaque-string consumers remain unchanged.

Local .140 retires the temporary .136–.139 DSI timing/QoS suffix and `tab5_display_pattern` command after the diagnostic experiments failed physical acceptance. It restores the .131 numeric display diagnostics; the optional `|gallery:` section remains for local art decoding. The old pattern script is historical and must not be used with .140. This rollback also failed physical acceptance and is not a confirmed blue-flash fix.

Local .141 also withdraws `|gallery:` and restores the .131 response format. Consumers must continue treating `displayDiag` as optional opaque diagnostics, rather than requiring fields from a failed experimental version. No protocol version or artwork RPC contract changes.

### Standard artwork collections (.143 / Windows 0.6.0)

Gallery RPC is included in standard Windows builds. Missing category collections return `404 {"error":"gallery_pack_missing","category":"painting|calligraphy"}`; missing image files return `503 {"error":"gallery_pack_incomplete"}`. Existing authenticated manifest/read operations and portrait/layout fields retain protocol version 1. Imported category catalogs under `%LOCALAPPDATA%/AI-bot/DailyArt/{category}` take priority over legacy app-folder galleries and reload after import without restarting. macOS does not implement this service.

TAB5 exposes optional opaque `galleryDiag` text alongside, separately from, `displayDiag`: six comma-separated integers are request count, worker active, category (0 painting / 1 calligraphy), portrait flag, stage and last status/error. Stages: 1 manifest, 2 JPEG allocation, 3 image chunks, 4 hash, 5 image metadata, 6 output allocation, 7 cache synchronization, 8 engine creation, 9 decode, 10 ready. Status is the last HTTP-like RPC status until a decoder/cache call supplies an ESP error; it must be interpreted with the stage. These are fixed atomic counters, with no heap traversal or display-driver changes. Counters do not by themselves prove successful visual rendering. Older clients ignore the additional field.

Finished failed requests release the UI's prior 60-second retry reservation. Per-slot retry delays are 2, 4, 8, then at most 15 seconds; successful images reset backoff. Only one worker runs at a time and the previous displayed frame remains available. This does not assert that every possible transfer or decode failure is resolved.

In .145, `galleryDiag` appends `frame,reused,outputAllocationAttempts` after the original six fields. `frame` is zero-based; `reused` reports whether the current worker received an existing decoder output buffer. The cumulative allocation-attempt counter distinguishes buffer reuse from repeatedly allocating a full RGB565 frame. Retired visible frames and failed-job outputs return to a one-slot atomic spare pool; the currently displayed bitmap is never lent to a worker. No bridge parsing change is required because the diagnostic string is forwarded verbatim.

.145 在原六字段后追加零基页码、当前任务是否复用缓冲、累计输出缓冲分配尝试次数。翻页退役图和失败任务缓冲进入一个原子备用槽，下一任务直接复用；当前可见图不得借给解码任务。桥接原样转发此诊断字符串，不改变图片 RPC 契约。
