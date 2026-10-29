## 可选权威欣赏提示 / Optional authoritative viewing notes

`guide.looking` 可以为空字符串，表示没有逐件审定的权威赏析；不得由标题关键词生成通用段落。非空文字在图库来源中保存权威出处与证据。协议字段、版本及 UTF-8 上限不变；设备收到空字段时正文为空，当前固件仍保留“欣赏提示”标签。隐藏空标签未在本轮改动或设备验证。

`guide.looking` may be empty when no authoritative work-specific analysis has been reviewed. Nonempty notes retain source provenance in the pack. Fields, protocol versions and UTF-8 bounds are unchanged. Current firmware shows an empty body under its existing tab; hiding that tab is outside this content/validation change and is not claimed as verified.

# `@AIBOT` protocol version 1

## Optional TAB5 Wi-Fi connection diagnostics

USB health replies may contain `wifiDiag`, a comma-separated numeric schema with 16 fields:
`schema,initialized,connected,setConfigRc,connectRc,disconnectReason,connectRequests,scanRc,scanRuns,matchedAPs,bestRssi,bestChannel,getConfigRc,ssidMatches,stationChannel,bssidLocked`.
API return codes begin at -1 (not sampled); success is 0. `matchedAPs=-1` means no completed scan, 0 means the targeted scan found no matching AP. RSSI/channel are 0 without a match. The scan runs only after `NO_AP_FOUND`, no more than once per 30 seconds, while disconnected. Readback does not trigger a scan or change saved credentials. Fields are atomic observations, not one transaction.

The Windows bridge validates the exact numeric shape. Older bridge versions and the macOS ESP8266 receiver ignore this optional TAB5 diagnostic. Existing protocol versions, authentication and network configuration contracts remain unchanged. No SSID, password, BSSID or credential is exported.

中文：USB 健康回复可附带 `wifiDiag`，依次记录版本、初始化、联网、设置返回值、连接返回值、断开原因、连接请求次数、扫描返回值、扫描次数、匹配热点数、最强信号、信道、配置读回返回值、网络名是否匹配、固定信道、是否锁定热点。扫描只在未联网且确实报“未找到热点”时执行，每30秒最多一次。数字读回不触发扫描、不更改已保存网络；未采样返回值及热点数为-1，成功返回值为0。各字段不是同一事务快照。诊断不含网络名称、密码或硬件地址，旧接收端兼容。

TAB5 203 optionally adds `wifiRecoveryDiag` with eight numeric fields:
`schema,connectionPhase,timeoutPhase,timeoutCount,stationRestarts,stopRc,startRc,restartPending`.
Phase 1 is association, 2 is DHCP; timeout phase 3 identifies manual reconnect and 4 prolonged successful scans with no matching AP. Association and DHCP deadlines are 30 and 60 seconds. The Wi-Fi worker restarts only the station driver on a stall, with a 120-second automatic cooldown; failed stop/start calls retry after 10/2 seconds. A healthy connection is protected, BLE isolation finishes before recovery and credentials are not rewritten. Manual reconnect uses the same driver restart. The companion bridge validates all eight fields; older peers ignore this optional field. No device reboot or C6 power cycle is added.

中文：203 新增可选数字字段 `wifiRecoveryDiag`，记录版本、连接阶段、超时阶段、超时次数、站点重启次数、停止／启动返回值及待启动状态。连接与 DHCP 分别等待 30／60 秒；长期扫描找不到热点也可触发恢复，自动重启间隔至少 120 秒。停止／启动失败分别间隔 10／2 秒重试。手动重新连接采用同一站点重启路径，健康连接保持运行，BLE 临时隔离先结束，不重写密码、不重启设备或给 C6 断电。旧桥接可忽略此字段。

## ESP8266 fixed USB identity (local 0.5.3)

ESP8266 `hello`/`pong` and `device_info.data` add optional `device_id`, formatted `esp8266-` followed by 12 lowercase hexadecimal digits from the factory station MAC. Authenticated `/api/info` returns the same field. This ID is independent of Wi-Fi association, IP address, COM number and USB socket. Protocol version 1 and 460800 baud remain unchanged; older receivers ignore the field and older firmware can omit it.

Windows learns the ID only on the registered USB path or an explicitly configured initial port, then stores non-secret `EspDeviceId` beside `UsbIdentity`. On reconnection it probes matching USB-adapter models, including multiple candidates, and sends no pairing/configuration, state or resources to a mismatched or ID-less candidate. Only an exact ID match updates the USB instance binding, atomically preserving user data/settings. Ordinary settings saves cannot revert that binding. Legacy firmware keeps its existing binding until a trusted connection learns the new ID. macOS decodes the optional field for compatibility; automatic persistent rebinding is currently Windows-specific.

中文：新增固定 `device_id`，没有 Wi-Fi、换 USB 插口和 COM 号、重启后仍可通过 USB 上报。Windows 在已确认的连接登记设备 ID；换口后逐一探测同类 USB 设备，仅向固定 ID 一致的小屏发送配置与数据并自动更新绑定。其他小屏或旧固件缺少 ID 时不能替代已登记设备。保留协议版本、波特率、已有配对及循环设置；macOS 同步兼容字段解析，尚未提供同等持久重绑定功能。0.5.3 已本地安装，通过真实 COM8 → COM7 换口和不配置 LAN 的实际串口恢复测试；验收期间未关闭设备 Wi-Fi。

## TAB5 .177 TF 整库同步 / Complete TF collection synchronization

卡内全部项目数据统一位于 `aibot/`；图库采用 `gallery/<类别>/works/<作品索引键>/<页码>-<方向>.<记录槽>.aig`，进度为类别目录的 `catalog.0/.1`。照片、其他持久数据和可重建缓存分别使用 `photos/`、`data/<模块>/`、`cache/<模块>/`，按需创建。完整归属及兼容规则见 [TF 卡目录约定](TF-STORAGE.md)。

All project writers use the `aibot/` namespace. Gallery records are grouped by category and canonical work key, with category-local alternating progress records; future photos, persistent module data and caches use the documented separate namespaces. See [TF storage layout](TF-STORAGE.md).

TF 卡保存设备已验证的 JPEG 与完整导读；RPC 请求、认证、图片哈希和二进制分片上限不变。设备使用已有 `connectionHealth.galleryPacks` 校验值匹配卡记录，纯文字更新同样排除旧记录。没有桥接版本信息时可复用最近保存的完整记录，重新连接后以新的认证内容版本为准。卡记录采用自有格式，不将任意卡路径作为远端图库请求参数。

可选 `galleryDiag` 字符串保留前 15 个诊断计数，尾部增加 `tfState,tfHits,tfWrites,tfWriteFailures,paintingDone,paintingTotal,calligraphyDone,calligraphyTotal`；`tfState` 为 0 未探测、1 已挂载、-1 暂不可用／等待重试。字段只记录数字，不包含文件路径、原文、账号或凭据。现有桥接按诊断字符串显示，旧接收端继续兼容；正式 .176 不含此能力。

The local TF cache stores verified JPEGs and complete optional guides without changing RPC/authentication/range contracts. Existing authenticated category digests select records, including text-only revisions; offline reads may reuse the last complete record until a new digest arrives. Optional `galleryDiag` retains its first 15 counters and appends TF mount state, hits, writes and write failures. These numeric diagnostics contain no paths or private content. Public .176 does not include this local change.

Windows .6.6 authenticated state advertises optional `connectionHealth.gallerySyncVersion=1`. Supporting devices can request `{kind:"gallery",op:"catalog",category}` using the existing authenticated RPC. Replies bind `syncVersion=1,works` (1..4096), `revision` (the same category SHA-256 as `galleryPacks`) and `baseDate="2026-01-01"`. Stable canonical dates from that base enumerate each installed work once; normal manifest/read and v2-guide limits remain unchanged. Reject counts/catalogs/revisions that disagree. Old bridges and Mac omit this capability and retain their existing paths. A device stores a checked category count/cursor/revision; canonical date modulo count preserves daily selection across years. Whole-library copying uses USB/Wi-Fi, while BLE retains individual fetches. Advance the durable cursor only after both layouts of all pages in a work are saved; reuse checked partial pages on restart. New category revisions refresh metadata and reuse an old JPEG only after its bytes match the authenticated new manifest hash. Numeric diagnostics append completed/total work counts. Offline use and incomplete updates never imply full hardware acceptance.

## TAB5 .160 artwork guides and USB diagnostics

Local gallery-package change: schema-2 packs require hashed `guides.zh.json`, scoped to the installed category and validated against catalogue `id/category/workId`. Existing `includeGuide` and `guideVersion` replies read the installed pack's text instead of the embedded catalogue. Schema-1 packages without guides retain the embedded fallback. Import swaps images and guides together and invalidates running bridge caches. Gallery JSON uses UTF-8 while retaining JSON escaping; image hashes, authentication and binary chunk limits are unchanged. Authenticated state replies optionally carry `data.connectionHealth.galleryPacks` with `painting` and `calligraphy` SHA-256 content digests. A supporting device refreshes only the affected category's JPEG, guide and decoded-image cache after its active transfer/consumer finishes; it retains the previous bitmap until fresh data arrives. Missing, invalid or unchanged digests preserve caches. Legacy devices ignore these fields and fetch their cached day again after a restart. The extension is not deployed on .167.

中文：两类新版图库包必须含已校验的导读；简介、欣赏提示、已核实原文随图片整体导入并即时重读。旧包保持内置文字回退，新包缺少导读时报错。认证状态可附带 `connectionHealth.galleryPacks` 两类别的64位小写十六进制内容校验值，纯文字更新也改变校验值。支持此字段的设备等活动传输和结果消费结束后，只清理受影响类别的当天缓存；另一类别保持可用，旧位图显示到新数据就绪。缺失、非法或未变的值不清缓存。旧设备继续原行为，重启后获取新内容。新增能力尚未部署到 .167。

2026-10-10 local extension: a manifest request with `includeGuide:true,guideVersion:2` receives v2 guides containing complete reviewed object text or explicitly labelled literary reference text. The v2 transcription buffer is 8192 UTF-8 bytes including NUL; other field bounds remain unchanged. Both reply versions are accepted by the new reader. Absent, non-numeric or unsupported requested versions receive v1. Text exceeding the old 512-byte bound is replaced by a short whole-text upgrade notice for v1 readers, never a truncated poem. Entire v2 manifests stay within 16 KiB of the existing 32 KiB RPC reply limit. The new catalogue/reader is locally validated; .167 hardware acceptance does not cover this extension.

中文：新增本地 `guideVersion:2` 协商，将原文缓冲扩为含终止字节的8192字节；简介、欣赏提示等字段上限不变。核实的作品正文与明确标注的通行原文分开登记。旧端仍用v1，长文显示完整原文需升级的短提示，不截断正文。三通道共用相同字段，图片、哈希与认证协议不变；本次新增内容尚未安装验收。

Authenticated `kind:"gallery", op:"manifest", includeGuide:true` requests optionally receive `guideVersion:1` and `guide`. The reviewed guide is joined by stable catalogue `workId`, never by a daily index or a translated title. Its strings are `workId,title,author,period,museum,brief,overview,looking,transcription,explanation`; missing or unknown guides are null. Only source-verified transcriptions and their explanations are supplied. Requests omitting the flag receive no guide text. Existing image size/hash, JPEG bytes, ranges, date selection, encryption and protocol version are unchanged. Windows implements this optional service; macOS does not.

Device UTF-8 buffers include the terminating byte: 80/256/128/96/80/256/640/384/512/512 bytes in field order. Oversized fields reject the guide without truncation. A guide becomes visible only after the matching image succeeds; category/date/work identity must match the displayed frame. Reading pins the displayed work and orientation, and returns to that frame. Missing text hides the Details control and preserves the image.

USB hello/full ACK optionally add content-free `rpcDiag` and `usbDiag` strings. `rpcDiag` contains three semicolon-separated channel groups (USB/Wi-Fi/BLE), each `failureCount,lastStage,lastError,replyBytes`. Stages 1–5 mean absent transport reply, decrypt/authentication, envelope/JSON decoding, reply status/body validation and a connection-generation change during decoding. Local errors -1001/-1002/-1003/-1004/-1005/-1006 mean bounds/unpaired/allocation/envelope/stale-generation/invalid-reply respectively; cryptographic library errors retain their original values. `usbDiag` is `cdcEpoch,rxBytes,jsonLines,lineOverflows,binaryExpirations,incompleteWrites`. These counters carry no packet content, keys or nonces and do not establish hardware stability. Older peers ignore the fields.

中文：.160 导读按稳定作品身份提供，同一作品分页共用文字；只显示审定的简介、欣赏提示和已核实释文。字段按 UTF-8 字节检查，过长时隐藏入口而不截断、不覆盖成功图片。新增 USB／RPC 数值诊断用于区分收包、认证、解码和连接代次失败；旧端兼容，图片与认证协议不变，诊断本身不代表真机通过。

TAB5 .120 配套 Windows 桥接为 `open-recent` 成功回执增加可选 `draftEmpty`（true / false / null）。只读检查限于前台已确认目标的主输入框；true 表示确认空草稿，撤销该目标旧发送凭据并使设备恢复语音。false、null 或缺字段不重置同一目标原状态。请求重放沿用首次结果。仅 Windows 支持此检查；macOS 未实现此桌面操作。USB / Wi-Fi / BLE 共用字段，ESP8266 不变。

The .120 paired Windows bridge adds optional nullable `draftEmpty` to successful `open-recent` replies. Read-only inspection verifies the foreground target's main composer. Only true retires old target send tickets and restores device dictation; false, null or absence preserve same-target state. Replays retain the original observation. This Windows-only desktop operation is not implemented on macOS. USB, Wi-Fi and BLE share the field; ESP8266 is unchanged.

TAB5 uses separate `tab5_*` messages and `/tab5/v1/status`; see [TAB5 integration](TAB5.md). The ESP8266 protocol below is unchanged. TAB5 使用独立消息与接口，不改变下面的小屏协议。

## TAB5 .117 OTA transport compression

USB and BLE bulk OTA RPC ranges negotiate `acceptEncoding:"zlib"` against authenticated `rpcOtaZlib=1`, retaining the existing 48 KiB decoded limit, encryption, original offsets and raw fallback. Wi-Fi OTA uses the existing authenticated GET with `X-AIBot-OTA-Flow: tcp-v2` plus `X-AIBot-OTA-Encoding: aibot-zlib-blocks-v1`; the bridge only enables compression when both capabilities match and echoes the encoding header on a stream response. Missing headers retain the raw image path. The later `rpcOtaResume=1` extension described below permits authenticated Wi-Fi OTA ranges after initial HTTP unavailability or interruption.

The HTTP body is a sequence of independent blocks: four little-endian uint32 fields (original offset, decoded bytes, payload bytes, encoding), then payload. Encoding 0 is raw; 1 is zlib. Decoded blocks are 49152 bytes except the last. Payload must be positive and no larger than decoded bytes; zlib must be smaller. Content-Length is the framed transfer length, while the offer's size and SHA-256 always describe the decoded image. Reject unknown/duplicate encoding headers, wrong offsets/sizes, truncation, trailing bytes and decode failure before accepting the image. Final full-image hash, identity and boot-partition checks remain mandatory.

TAB5 三通道统一协商压缩，旧端与不适合压缩的数据保持原文兼容。自动升级优先 Wi-Fi、USB、BLE；固定模式只使用所选通道，开始后不跨通道续传。显示中的镜像进度按解压后字节计算，另列传输数据量（Wi-Fi 含分块头，USB/BLE 计 RPC 数据载荷，不含加密与链路开销），不能把镜像大小解释为下载量。蓝牙入口显示“通过蓝牙升级”；首次安装仍使用 USB 完整安装包。

The Windows or macOS bridge and ESP8266 communicate at 460800 baud using UTF-8 JSON lines. Every frame is one line beginning with the ASCII prefix `@AIBOT `.

### Windows connection preference

Windows Device Center can select Automatic, USB only or Wi-Fi only for the registered ESP8266. This is host routing policy; version-1 frames and firmware remain unchanged. Automatic retains USB-first behavior and the firmware's 8-second freshness timeout. Wi-Fi only stops USB status, metrics and binary-resource delivery while still allowing USB pairing, device information and management commands. USB only rejects the legacy `/status` and `/resources` routes and legacy discovery responses; TAB5 endpoints are unaffected. The optional device-registry `ConnectionMode` field defaults to `Auto` for existing profiles and persists `Auto`, `Usb` or `Wifi`.

## TAB5 development clock solar data

Optional `weather.daily` fields `sunrise` and `sunset` contain validated city-local `HH:mm`; `date` identifies that city's forecast day. `daylightSeconds` is nullable and ranges from 0 to 86400; explicit endpoints identify polar night/day. Windows retains these fields from its existing QWeather daily request and adds sunrise/sunset/daylight duration to the existing Open-Meteo request. No extra request, authentication change or protocol-version change is introduced. USB, Wi-Fi and BLE share the TAB5 snapshot fields; legacy ESP8266 USB frames continue to omit forecast series.

中文：昼夜环按 `weather.utcOffsetSeconds` 匹配当地当天的每日预报，将事件时刻换算到时钟原有时区以判断当前昼夜及白昼进度；独立日出日落标签仍显示天气城市当地时间。当日有效缓存可继续显示并标注“缓存”；缺少、过期日期或非法时间时使用当地默认日出 06:00、日落 18:00 并标注“默认”，不显示“待更新”。极昼／极夜仅由明确的 86400／0 秒数据识别，不能从缺少时间推断，也不虚构极昼／极夜的事件时间。旧桥接、旧缓存或未提供此扩展的平台可继续使用默认时间。本扩展仅本地验证，尚未部署验收。

The clock matches today's date in the weather city's timezone and projects event times onto the clock's existing timezone to determine day/night and daylight progress. Separate event labels remain city-local. Valid same-day cached values are labelled as cached; absent, invalid or wrong-date data uses local defaults 06:00/18:00, labelled as default. Polar states require explicit duration endpoints and do not invent event times. Missing fields from older bridges/caches/platforms remain compatible with this fallback. Locally validated; no deployment or hardware acceptance claimed. Provider fields: [Open-Meteo](https://open-meteo.com/en/docs), [QWeather daily forecast](https://dev.qweather.com/en/docs/api/weather/weather-daily-forecast/).

## Probe

Unreleased ESP8266 update compatibility: `pong` includes optional `firmware:"X.Y.Z"`; `device_info.data.firmware` and authenticated `GET /status` requests' `X-AIBot-Firmware` header carry the same value, embedded from `firmware/VERSION` by PlatformIO. `version:1` remains the protocol number. Old firmware omits these fields; old hosts ignore them. Windows records the USB version after a validated ESP8266 pong and the Wi-Fi version only after successful token authentication on `/status`, using the currently active transport. USB disconnect and pairing reset clear the respective observations. Missing fields denote legacy unknown versions; malformed values are invalid, never version zero. Only connected legacy ESP8266 targets may use explicitly confirmed manual preparation; unknown TAB5/bridge versions remain blocked and unknown versions never trigger newer-version reminders.

小屏待发布修复新增可选固件版本字段，版本来源为 `firmware/VERSION`，不改变协议版本 1。USB 心跳和设备信息携带 `firmware`，Wi-Fi 状态请求携带 `X-AIBot-Firmware`，桥接只接受已认证请求的版本。旧固件缺字段时保留未知状态，不推断当前版本；已连接旧小屏可手动准备升级，仍需明确确认、USB 备份与校验。该路径不触发“有更新”自动提醒。

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

The Windows ESP8266 LAN response omits `runtimeHealth` and weather `hourly`, `daily`, `hourlyStale`, and `dailyStale`, which ESP8266 does not consume. This avoids spending its limited JSON heap on desktop/TAB5-only views. Current weather, full stock labels, quota data, display policy and incremental system samples are retained. The shared snapshot, desktop loopback response and TAB5 protocol remain complete. `GET /diagnostics/esp-lan` on the loopback listener exposes bounded request/authentication/send counts, byte size and timestamps only; it never exposes headers, tokens or status bodies. HTTP delivery counters alone do not prove device parsing or rendering: acceptance must also observe increasing device LAN frames and USB recovery.

### Paired LAN rediscovery after both IP addresses change

After one USB pairing, the device and the Windows bridge retain the same pairing token. If USB and LAN status are both stale, the device broadcasts a small discovery datagram to UDP port `18766` on its current IPv4 subnet every five seconds. The request is ASCII `AIBOT_DISCOVER_V1|<16 lowercase hex nonce>|<64 lowercase hex HMAC-SHA256>`, where the HMAC key is the UTF-8 pairing token and the signed bytes are the prefix through the nonce. The token itself is never included in a discovery datagram.

The Windows bridge checks the HMAC, selects an active private adapter whose subnet contains the datagram source, and starts the authenticated LAN listener on that adapter before replying from that adapter's IP. The ASCII response is `AIBOT_BRIDGE_V1|<same nonce>|<IPv4 host>|<decimal port>|<64 lowercase hex HMAC-SHA256>`. Its HMAC covers every field before the final separator. The device accepts only a response with the outstanding nonce, a valid HMAC, and a sender IP equal to the advertised host. It requests authenticated `/status` from the candidate before replacing its saved bridge address. Failed probes leave the previous pairing intact. USB `lan_config` remains authoritative and cancels any pending LAN candidate.

Discovery is local subnet broadcast; it does not cross routed VLANs or an access point that filters client broadcasts. The bridge can still serve the last paired address while discovery is pending.

The local Mac candidate now answers the same ESP8266 protocol through its existing shared TAB5/ESP UDP listener on 18766. It reads, never generates or replaces, the existing Keychain token; validates the exact request prefix, lowercase nonce/HMAC and 160-byte bound; and selects a currently active, non-tunnel private IPv4 interface on the sender’s subnet. The response UDP socket is explicitly bound to the advertised address. Mac's existing shared HTTP listener is wildcard-bound and follows address changes without restarting TAB5 connections; discovery advertises only its ready port. Disabled/USB-only ESP, missing token, unavailable HTTP service or no matching subnet receives no reply. The device still verifies the nonce, HMAC and source address, and commits the candidate only after authenticated status succeeds. No firmware, Windows wire-format, pairing or display-setting change is required. Address changes have synthetic policy coverage plus real loopback UDP/HTTP validation; ESP8266 hardware acceptance remains pending.

中文：Mac 本地候选已补齐小屏认证自动发现，与 TAB5 共用 18766 监听。只使用原钥匙串密钥，按设备来源网段选择有效本机地址，从该地址回复；HTTP 未就绪、仅 USB、停用或认证失败时不应答。设备成功取得认证状态后才保存新地址，失败保留旧配对。本轮不改固件及 Windows 协议，真实小屏改 IP 后恢复仍待硬件验收。

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
TAB5 .080+ also accepts the existing slot-2 resource with `encoding:"jpeg"`, one frame, `width`1..560, `height`1..336 and encoded `total`<=524288 bytes. The resource ID is the eight-digit CRC32 of the encoded JPEG; chunk payloads remain<=1024 bytes. Decoded dimensions must match the envelope. Windows and Mac select JPEG only for supporting observed firmware and keep the ESP8266 resource separate; Mac falls back to112×112 RGB565 for old/unknown TAB5 firmware. This documents the existing JPEG contract rather than adding a new wire version.

TAB5 .080起既有封面资源槽2支持 JPEG：单帧，宽1..560、高1..336，编码后最多524288字节，资源ID为JPEG编码字节的8位CRC32，单块载荷仍最多1024字节；解码尺寸须与声明相符。Windows／Mac 按实际固件能力选择，小屏资源独立，Mac 对旧／未知 TAB5 固件回退112×112 RGB565。不新增协议版本。

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

Mac local selection (2026-10-28): the saved capture preference selects `pc` or `tab5` for each new take, including agenda/date owners; absent a saved preference, existing request-directed routing remains. A settings change does not replace the active take. The receiver already follows the returned `source`, including when it differs from its start hint. `sourceName` carries the confirmed Mac/DJI endpoint or TAB5 microphone plus the recognizer. Selected-device failure is not an automatic fallback. This reuses the existing Mac fields and receiver behavior; no wire-version or Windows/ESP8266 change is required. Device recording-label deployment is tracked separately in [the local record](development/MAC-VOICE-SOURCE-20261028.md).

2026-10-15 local candidate: existing `op=tab5` also supports direct shared DJI input. The host stops its owned DJI segment and waits for idle/stable committed text before borrowing the built-in channel, preserving voiceId, owner and draft; final explicit stop returns both segments. During handoff the existing recording state remains with ready=false. No new field/version is needed. 普通大疆直连不切换豆包音源；手动 `tab5` 请求先结束并保留大疆文字，再临时借用／归还内置麦克风通道，保持原会话及完整草稿。此候选尚未部署，真实切换验收另记。

TAB5 0.2.22 USB 网络管理：`tab5_wifi_list` 返回 ssid/label/connected/selected，最多 10 项（216 起；旧固件为 5 项），不返回密码；`tab5_wifi_forget` 按配对 deviceId 和真实 SSID 删除，返回 tab5_wifi_forgotten 或 tab5_wifi_rejected。仅 USB 接收。
# TAB5 日历详情增补（0.2.26）

`calendar` 保留现有今日/明日字段，新增可选 `detail`：`terms[{year,dates}]`（24 节气的 MMDD）、`holidayYear`、`holidays[{start,end,name}]`（MMDD 范围）、`workdays[]`（补班 MMDD）、`birthdays[{name,month,day,lunar,leap,remind}]`。生日最多 32 项，提醒 0–30 天；只走已有鉴权 TAB5 状态通道，不改变 ESP8266 字段。

生日在电脑托盘「内容设置 → 日历与生日」管理，仅保存到用户目录。无对应闰月时按普通月份，缺少农历三十或公历 2 月 29 日时按当月末日。TAB5 本地计算下一次生日及月历；缺少官方假期年份显示待更新，不将节日名称等同于放假安排。


## TAB5 0.2.26 回复内容标识

`codexTasks.replyView.hasText` 和加密读取回执内的 `replyView.hasText` 是可选布尔字段，表示当前分页有有效公开文字，false 表示等待/空内容占位。新版设备只在对应轮次的有效正文显示后清除未读，历史固定查看也适用；空占位继续重试。旧桥接缺少字段时设备兼容已知占位文字，旧设备忽略新增字段。读取轮询仍为每 3 秒，运行中的文字优先取桌面 IPC 公开消息。

## Codex 桌面已读同步（可选字段）

`codexTasks.repliesRead` 为 `{taskId: turnId}`，最多 80 项，与 `repliesReady` 共用完成轮次标识；通过现有 USB/Wi-Fi/BLE 鉴权状态通道传输。桥接只读当前账户、标准本地主机的 Codex 桌面未读记录。完成轮次及桌面未读写入稳定至少 5 秒后，已读任务才生成确认；新轮次重新等待，文件不可用、账户/主机不匹配或未知 schema 不生成确认。

`codexTasks.repliesUnread` 使用相同结构，表示当前桌面明确标为未读的完成轮次。TAB5 首次接收时恢复未读数字且不播放历史回复提示音；同一桌面未读状态不会反复覆盖设备上的本地已读。桌面确认已读后再次标为未读，可重新恢复提醒。未知桌面状态返回空映射，不据此清除设备未读。

TAB5 USB 心跳中的可选 `codexNotice` 是界面线程从实际 LVGL 右上角控件读回的打包诊断值：低 8 位为队列未读数，8–15 位为数字标签（10 表示 `9+`），第 16 位为控件可见状态。桥接在设备诊断中显示此值，缺少字段时显示 `--`；该值只用于验收，不参与已读状态同步。

设备收到确认后，仅清除 taskId 与 turnId 同时匹配的提醒，并立即重算右上角未读数、回复按钮和概览状态；旧轮次确认不能清除新轮次。确认独立于缩减的任务目录，避免传输裁剪留下旧数字。缺失字段不清除，旧客户端忽略字段。设备阅读仍按有效正文显示确认。旧固件仅支持电脑到设备的同步；以下可选回执增加设备到电脑的同步。

Windows 0.6.10 / TAB5 0.2.230-ui 本地候选：加密读取的 `replyView.readReceipt` 是可选 UUID，只为当前最新且已结束、含有效公开文字的回复生成，绑定配对设备、taskId、turnId、Codex 账户和本地主机，五分钟内有效。历史预取和读取响应本身不修改桌面已读状态。设备在匹配正文实际显示于打开的回复页后，才通过现有鉴权 RPC 发送 `kind:codex, op:read-ack, taskId, turnId, requestId:readReceipt, message:""`；会话、时间和 nonce 校验保持不变。无回执的旧桥接无需发送。

桥接再次核对最新轮次、结束状态和账户，再通过桌面 `thread-read-state-changed` v3 IPC 广播发送 `hasUnreadTurn:false` 及桌面验证的 identity/executionHostKey 上下文，不直接改写 Codex 状态文件，不打开会话或发送消息。只有读回相同账户/主机的已读状态后才返回 200；旧回执、账号或更新轮次冲突返回 409，桌面未确认返回 503。设备仅对幂等已读回执有界重试，不重放消息或按键。该桌面 IPC 属于版本化内部协议，升级不兼容时保留未确认结果。

English: the local Windows 0.6.10 / TAB5 .230 candidate adds an optional, five-minute `replyView.readReceipt` bound to device, task, latest terminal turn and Codex identity/host. Loading or prefetching never writes read state. After matching public content is rendered in the open reply view, the device sends authenticated `codex/read-ack` with its task/turn and receipt in `requestId`. The bridge rechecks identity and the latest terminal turn, broadcasts the version-3 desktop read-state contract, and confirms the desktop-owned persisted state before returning 200. Conflicts return 409; unavailable confirmation returns 503. No direct state-file writes, foreground navigation, new message or keyboard input occurs. Only this idempotent receipt may retry; unknown IPC compatibility fails closed.

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

TAB5 metrics and full snapshots retain 64 raw 250 ms history samples, allowing delayed radio delivery to backfill approximately 16 seconds. On compression-safe receivers, `tab5_packed` may contain `tab5_metrics` as well as full state, with the same non-nesting, exact-size and session requirements. Bridge 0.6.3 publishes an immutable network snapshot once per second: headers use a trailing four-sample mean, and history and sample sequence remain held between publications. CPU/memory retain their separate two-second sampling interval. TAB5 0.2.156-ui and ESP8266 0.5.2 use the same trailing mean for numbers and the current curve endpoint, consuming each incoming batch immediately without a playback reserve; duplicate sequences do not advance the curve. Raw sampling continues at 250 ms and protocol fields remain unchanged. TAB5 history missing beyond the retained window stays a gap; displayed numbers use the current available contiguous window, without averaging across that gap. / 桥接每秒发布一份完整网络快照，上传／下载数字与曲线末端共用最近四个原始采样的 1 秒均值；两次发布之间数字、历史和序号保持，设备按新序号整批同步显示。内部原始采样仍为 250 毫秒，CPU／内存仍每 2 秒采集一次，协议字段不变。

Safety revision in 0.2.42: 0.2.40/0.2.41 used a ROM convenience inflater whose ~11 KiB stack workspace exceeded the receiver task stacks. The bridge must send those versions plain full snapshots, while metrics packets remain supported. Packed Wi-Fi responses require authenticated firmware version >=0.2.42; packed BLE responses require `telemetryVersion:2`. BLE capability 1 supports metrics but receives plain full state. The new receiver allocates its inflater workspace on the heap and requires DONE, exact output size and full compressed-input consumption.

Mac 本地候选补用已定义的 BLE `tab5_packed` 完整状态格式：当前配对确认、设备／nonce／ACK 证明、已知 `telemetryVersion` 2 或 3 及同 nonce 的固件版本证明均有效，且版本为 `0.2.42-ui` 或更高的 `0.2.x-ui` 时才启用。使用标准 zlib，只有包含 base64 与元数据的整个封装更小时才替代原文；明文展开仍不超过 Mac 既有 32740 字节上限。缺少能力、旧版本和未知版本保留原格式。资源、归属证明和 RPC 不套用此封装；加密、分片、序号及原整帧签名 ACK 不变。没有新协议字段或固件要求，实际耗时另记实机结果。

The local Mac candidate now uses the existing BLE tab5_packed full-state format only for confirmed pairing, matching device/nonce/ACK proof, known telemetryVersion 2 or 3 and a valid firmware proof under the same nonce for 0.2.42-ui or later 0.2.x-ui. Standard zlib is used only when the complete base64/metadata envelope is smaller. Expanded plaintext retains Mac's existing 32740-byte limit. Missing capabilities and old/unknown versions retain the original format. Resources, ownership proofs and RPC are not wrapped; encryption, fragments, sequence and signed whole-frame ACK remain unchanged. There are no new wire fields or firmware requirements; physical timing is recorded separately.

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

本地 .161 候选为 USB 拒绝确认增加可选数值 `rejectReason`：1=JSON 解析，2=压缩展开或工作区分配，3=无效信封，4=未配对或身份不符，5=旧序号或无效会话；0/缺失表示旧端未说明。配套桥接保持串口锁，仅对压缩完整状态的 0/1/2 拒绝尝试一次同一原文与序号，仍校验完整 ACK 的设备和序号。身份、序号、未知新代码、原文再次拒绝、I/O、超时或取消均不能就地重试，继续正常断连恢复。新诊断只记录数字与阶段，不保存正文；`USB 状态恢复` 分别统计尝试与已确认数，恢复成功不抹去稳定性验收中的故障。

English: the local .161 candidate adds optional numeric USB `rejectReason` (JSON=1, unpack/allocation=2, envelope=3, full-state identity=4, full-state sequence/session=5; absent/0 is legacy). Metrics/delta prerequisite failures can report the generic envelope code. Its matching bridge retries the exact raw full state once, under the same serial gate, only after a packed full-state rejection with reason 0/1/2. The normal device-ID/sequence ACK check still applies. Identity, sequence, unknown codes, raw rejection and transport/cancellation failures remain failures. Recovery counters are content-free and cannot establish hardware stability.

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

`open-recent` 在 Windows 桌面不可交互时返回 409，`error=desktop_not_interactive`、`foreground=false`；拒绝发生在启动或唤起前。该回执沿用请求 ID 去重，解锁后通过新点击重试。此错误值兼容现有通用 409 处理，不改变协议版本。When the Windows desktop cannot receive input, `open-recent` returns 409 with `error=desktop_not_interactive` and `foreground=false` before navigation. Replay preserves the rejection; retry requires a new click after unlock. Existing clients retain generic 409 handling without a protocol-version change.

0.2.100 配套桥接的语音回执增加可选 `sourceName`，来自实际选中并成功打开的 Windows 录音端点全称。自动回退时改为 `TAB5 内置麦克风`；`source` 仍为 `dji` / `tab5`，路由不变。旧端可忽略新增字段；没有名称时新固件保留原状态文案，不猜测型号。Codex 直达状态栏展示全称，过长时横向滚动。

0.2.91-ui 的直达按钮省略 `open-recent.taskId`，每次新点击在电脑解析最新会话；回执补充 `folder`、`updatedAt`，面板与语音绑定返回的 `taskId`。同一请求重放保持首次解析目标，`stage-draft` 仍要求明确目标。The 0.2.91-ui direct button omits taskId to resolve the latest conversation on each new click. Replies add folder and updatedAt; voice binds to the returned ID. Replays retain the original resolution, and staging always requires an explicit target.

0.2.106-ui 起，上一条/下一条和 Codex 按钮通过现有 `open-recent` 显式传入所选 `taskId`。箭头立即请求电脑切换，回执成功且目标一致后更新 TAB5 选中项；本次面板列表不因打开会话而重排，重新进入时按目录最新顺序初始化。失败或错目标回执保留原选中项，重新确认前阻止语音录入；同一已确认会话的再次唤起保留未发送的回填记录。沿用现有 Windows 显式目标能力，无新增协议字段或版本。From .106, arrows and the Codex key explicitly target the selected conversation using existing open-recent semantics. Commit selection only after matching foreground confirmation; preserve list order during a visit and refresh recency on reopening. Unconfirmed navigation blocks voice/input, and reopening the same confirmed target preserves its staged receipt. No wire-format or protocol-version change.

Mac 适配将原生 `recencyAt` 最近活动秒数映射到既有 Codex 任务 `updatedAt` 排序值，缺少有效活动时间时回退原生 `updatedAt`。这与 Mac 侧栏的最近顺序一致，避免设备进入直达面板后再次按记录更新时间重排；同名日期也使用同一排序值。字段名、数值类型、任务 ID、认证与导航回执不变，Windows 和固件无需改动。The Mac adapter maps native recent-activity seconds (`recencyAt`, falling back to valid native `updatedAt`) to the existing Codex task `updatedAt` sort value. This matches sidebar recency and prevents the device's own quick-console sort from restoring record-update order; duplicate-title dates use the same value. Field names/types, task IDs, authentication and navigation receipts are unchanged; no Windows or firmware update is required.

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

`quotaDetails[].weeklyResetEpoch` is an optional UTC Unix timestamp in seconds from the provider's weekly reset time. The Codex single-weekly-limit card uses it for a locally ticking `距重置` countdown; missing values hide the countdown, and an elapsed deadline displays `等待重置同步` until refreshed. `weeklyReset` remains the formatted absolute time in the reset panel.

`quotaDetails[].weeklyResetEpoch` 为供应商周额度重置时刻的 UTC Unix 秒数，可缺失。Codex 单周额度卡片使用本地时钟更新“距重置”；缺失时隐藏，到期后显示“等待重置同步”，不自行推算下个周期。右侧 `weeklyReset` 仍显示绝对重置时间。

TAB5 .159 additionally accepts optional `primaryResetEpoch` / `weeklyResetEpoch` / `planResetEpoch` and their matching `primaryWindowSeconds` / `weeklyWindowSeconds` / `planWindowSeconds`. Durations are positive whole seconds, at most 2,147,483,647. Codex retains the API's actual `limit_window_seconds`; Claude's explicitly named `five_hour` and `seven_day` windows are 18,000 and 604,800 seconds. Domestic adapters preserve explicit API durations, documented named windows and actual quota-period bounds through capture, cache reload and conversion. Named legacy five-hour/weekly fields recover their durations; generic package fields do not. An explicit unknown/invalid duration stays unknown. Older firmware ignores these additional fields. See [domestic cycle policies](DOMESTIC_QUOTA_SETUP.md#额度周期与-tab5-159).

TAB5 .159 增加可选的 `primaryResetEpoch`／`weeklyResetEpoch`／`planResetEpoch` 及对应的 `primaryWindowSeconds`／`weeklyWindowSeconds`／`planWindowSeconds`。周期长度为正整数秒，最大 2,147,483,647；Codex 保留接口实际返回的 `limit_window_seconds`，Claude 的明确命名窗口 `five_hour`／`seven_day` 分别为 18,000／604,800 秒。国产适配器在采集、缓存重载及转换时保留实际 API 长度、已确认的命名窗口或额度起止时刻。旧缓存的明确五小时／周字段可恢复周期，通用套餐字段不推算；显式未知或无效长度仍保持未知。旧固件可忽略新增字段，详见[国产周期规则](DOMESTIC_QUOTA_SETUP.md#额度周期与-tab5-159)。

Each visible quota bar independently computes elapsed time as `(nowUtc - (resetEpoch - windowSeconds)) / windowSeconds` using the bridge UTC sample and elapsed device clock time. A small clock face and vertical line show its position without extra text or an external ruler. Short limits support minute comparisons; weekly/package limits support hour comparisons, with second precision internally. Unknown, invalid, not-yet-started or expired windows hide the marker; retained cached quota does not imply a new cycle. All providers share internal divisions: five parts for package percentages, seven for weekly percentages; a known integral one-to-seven-hour primary window has one part per hour, otherwise five parts. Its existing label uses actual hours/minutes when available. These divisions measure usage fractions, not an inferred package duration. PAYG balances do not receive quota bars or a time marker.

每条可见额度进度条独立用 `(nowUtc - (resetEpoch - windowSeconds)) / windowSeconds` 计算已过时间，当前 UTC 来自桥接采样与设备经过时间。以小表盘和竖线表示位置，不增加文字或底部刻度；短周期按分钟、周及套餐周期按小时比对，内部保留秒级精度。信息不足、无效、尚未开始或已到期时隐藏标记；保留的额度缓存不表示开始了新周期。所有供应商统一使用条内等分：套餐百分比分 5 份，周额度分 7 份；明确为 1 至 7 个整小时的短周期每小时一份，其他短周期仍为 5 份，原有标签按实际小时／分钟显示。这些分隔表示用量比例，不推断套餐周期。按量余额页面不显示额度条或时间标记。

### TAB5 overview provider selection / 总览供应商选择（0.2.158-ui）

Windows appends an optional eleventh `quotaDetails` entry with `id="volcengine"`, `name="火山方舟"` and `plan="待接入"`. Existing indices 0–9 remain unchanged. The quota fields are null: the existing console entry has no verified model-specific quota adapter. Do not substitute whole-cloud wallets or local token counts. Older TAB5 firmware ignores the appended entry; the new UI also accepts a ten-entry payload and shows Ark as pending when manually selected. macOS currently does not produce TAB5 `quotaDetails` payloads.

Windows 在 `quotaDetails` 末尾追加可选第十一项：`id="volcengine"`、`name="火山方舟"`、`plan="待接入"`，保留原 0–9 索引。模型额度字段保持 null；已有控制台入口没有经过验证的模型专属额度适配，不得使用云钱包或本机 token 记录代替。旧 TAB5 忽略追加项；新 UI 兼容十项旧消息，手动选择方舟时显示待接入。macOS 当前不生成 TAB5 `quotaDetails` 消息。

USB `tab5_benchmark` replies advertise `bleFirmwareProbe:true`. Actions `ble_firmware_probe` and `ble_firmware_probe_isolated` snapshot the authenticated BLE offer (`sha256`, `offerId`, `size`) and use the production range decoder without opening any Flash partition. Authenticated RPC `kind:"ota_probe"` has the same offer/range/authentication/compression constraints as `ota`, accepts BLE only and never acquires the 90-second OTA lease. The bridge pauses background assets/metrics for the active full-image preflight and releases that pause on exit.

The device pins the BLE session, requires USB for control, limits transfer to 110 seconds (an in-flight range can take up to three 10-second attempts), and validates streaming SHA-256 over all decoded bytes. Optional Wi-Fi isolation has a 125-second lease; success is withheld until the worker confirms restart. USB/mode loss, cancellation and expiry request restoration without changing saved networks. The controller waits at most 160 seconds including bounded cleanup; no action starts OTA automatically.

Terminal result: `complete;BLE,down,1,<rawBytes>,<ms>,0,<calls>,<encodeMs>,<exchangeMs>,<decodeMs>,<wireTx>,<wireRx>,<minInternal>,<minPsram>,<minLargest>;image,<sha256>,<size>;`. Failure/cancellation use their existing states, and negative error codes cannot pass. The bridge requires complete bytes and the exact selected image hash, then estimates full OTA as receive milliseconds / 1000 * 1.2 + 25. The 120-second threshold remains a screening estimate, not hardware OTA acceptance. The legacy three-round raw benchmark remains separate.

完整固件预检仅接收、解压并计算整包哈希；桥接严格绑定所选镜像，失败不出升级通过结论。接收超时、分段重试、Wi-Fi 恢复和控制超时均有界。安装 .116 后可以对同一镜像做 RAM 预检；正常 OTA 仍拒绝重复安装当前版本，完整 OTA 验收应选择下一份真实升级候选，不能绕过该保护。

## TAB5 .121 voice transport candidate

Auto selection follows USB > Wi-Fi > BLE; fixed modes do not switch channels. Wi-Fi/USB audio requests now use the existing `codec=ima-adpcm` (16 kHz mono) decoder; BLE retains `ima-adpcm8k`. Independent blocks contain LE16 sample count, LE16 predictor, index, reserved byte, then low nibble first. At 3200 samples the block is 1606 bytes before Base64 instead of 6400 raw PCM bytes. This is lossy coding, not byte-exact PCM. Sequence validation, 60-second limit, stop/tail draining and no automatic audio retry remain unchanged. Paired bridge required; hardware continuity/recognition quality pending.
# TAB5 daily gallery RPC extension

Optional incremental guide reuse (2026-10-15 candidate): a `manifest` request with `includeGuide:true,guideVersion:3` may add `guideRevision` (the authenticated category content digest) and `guideWorkId` (the complete guide already held for that work/date). Only an exact current digest/work match returns `guideUnchanged:true,workId,guide:null`. All other requests receive the complete guide as before. The device must validate its cached date, revision, work identity and complete original-text pages before sending hints, reject a reference with an unexpected work/version, and retain independent text ownership. Older bridges ignore the hints; older devices never send them. JPEG hashes, range authentication and protocol version 1 remain unchanged.

中文：同一作品后续分页／横竖屏可携带 `guideRevision`、`guideWorkId` 请求复用已校验导读；只有当前内容版本及作品身份同时相同，才允许 `guideUnchanged:true` 省略重复正文。版本、作品或日期不匹配时获取完整导读；旧桥接／旧设备保持完整正文路径，不省略原文校验。

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


## TAB5 gallery continuous response and USB state delta (181 candidate)

中文：Windows 在配对状态的 `data.connectionHealth.galleryStream=1` 中声明可选连续响应能力。仅 Wi-Fi 的 `gallery/read`、`binaryReply=true`、`bulkReply=true` 和 `streamReply=true` 共同生效时，允许请求最多 1 MiB 的 SHA 绑定图片范围；USB／BLE 保留原始范围上限。请求继续使用原 `/tab5/v1/rpc` 的设备、会话、时间、nonce 防重放、HMAC 和 AES-GCM 校验。

HTTP 响应为 `T5S1`、u32LE 起始偏移、u32LE 图片字节数、u32LE 块数，随后连续发送多个 `u32LE 密文长度 + AES-GCM 密文包`。每块仍用 T5R2，JPEG 载荷最多 49152 字节；每次加密使用独立随机 IV，AAD 为该次请求 nonce。鉴权后的 `body` 包含 `offset,count,streamOffset,streamBytes,block,blocks`，所有位置、数量、次序和总长必须匹配请求与预期，否则拒绝；最终仍检查完整图片 SHA-256 与 JPEG 尺寸。外层标记不构成鉴权依据。错误／忙碌／重放沿用普通有界加密响应。旧桥接未声明能力时继续使用原范围协议。

接收块逐个鉴权，验证通过的前缀才交给并行写卡任务；窗口有界，慢写卡产生背压。断线保留已校验并落盘的前缀，不把暂存当成完成作品。可选 `galleryTransferDiag` 为四个非负整数：当前任务本次验证接收的 JPEG 字节、检查点写入的 JPEG 字节、检查点写入累计毫秒、接收回调与写入任务重叠的次数。它用于真机区分传输与写卡，不能单独作为速度验收。

USB hello 可声明 `usbDelta=1`，桥接仅对本条仍打开且已确认设备身份的 CDC 会话使用已 ACK 的状态作为增量基线，按原状态发布频率发送 `tab5_delta`／压缩增量。设备沿用既有完整状态 ACK 和序号校验；重连、会话变化或基线不匹配恢复完整状态，旧固件继续完整帧。正常状态循环不再对同一打开句柄重复执行身份 ping；新连接及显式配置操作仍核对身份。图库保存不会暂停未读数字、设置或音乐状态更新。

English: optional authenticated `connectionHealth.galleryStream=1` enables one continuous HTTP response for SHA-bound Wi-Fi gallery reads of up to 1 MiB. The original device/session/time/replay/HMAC/AES-GCM request checks apply. USB/BLE and unnegotiated peers retain their existing limits. T5S1 carries three u32LE fields (offset, bytes, blocks), followed by length-prefixed independently encrypted T5R2 blocks with <=49152 data bytes, independent random AES IVs and request-nonce AAD. Each authenticated body binds its range, total stream range and ordinal; validate all of them and the final JPEG hash/dimensions. Ordinary encrypted error responses remain bounded. Verified blocks feed a concurrent, bounded TF writer with backpressure and durable resume. The four-field content-free `galleryTransferDiag` records received bytes, checkpoint data bytes, checkpoint write milliseconds and receive/write overlaps, pending physical throughput acceptance.

An identity-validated USB hello may advertise `usbDelta=1`; the bridge then sends state deltas against the exact ACKed baseline on that live CDC handle, retaining the usual publication cadence and ACK checks. Reset the baseline on reconnect; legacy peers get full frames. Initial connection and explicit configuration still verify identity. Firmware source tests and builds do not establish hardware throughput or Wi-Fi stability.

### BLE standby display / 蓝牙待命显示

Fresh authenticated USB ownership also pauses Windows idle BLE voice/RPC mailbox polls between complete requests. An active response drains before standby; USB expiry resumes polling. Session/link checks remain active, and the protocol, authentication and notification windows are unchanged.

USB 已鉴权且有效时，Windows 在完整请求之间暂停蓝牙语音／RPC 空闲轮询；进行中的响应完成后才待命，USB 失效后恢复轮询。保留会话／物理连接检查，不改变协议、鉴权或通知窗口。

认证状态的可选 `connectionHealth.bleStandby` 布尔值仅说明显示状态。Windows 只有在 USB 为当前通道、USB 确认和本地已鉴权 BLE 活连接确认均未过期时才声明 `true`。设备还必须确认 BLE 物理连接、USB 数据新鲜，且此标记来自未过期的 USB 状态，才显示浅蓝色待命图标。正在接收 BLE 数据仍显示绿色，物理连接等待数据显示黄色，断开显示灰色。标记不刷新 BLE 数据时间，不改变路由或授权；缺失字段（包括旧桥接和未实现此能力的 Mac）沿用原状态显示。

The optional authenticated `connectionHealth.bleStandby` boolean is display-only. Windows reports true only while USB owns the active state and both its ACK and the authenticated live BLE session confirmation remain fresh. The device additionally requires a connected BLE link, fresh USB data and a fresh USB snapshot carrying this flag before showing pale-blue standby. Fresh BLE data stays green, a physical connection awaiting data amber, and a disconnected link grey. This flag neither renews BLE data nor changes routing or authorization. Missing fields retain legacy display behavior.


### Gallery receive/write acceptance samples / 图库同步测速

The paired USB `tab5_benchmark` control accepts `gallery_start`, `gallery_status`, and `gallery_cancel` when the reply advertises `gallerySpeed: true` (introduced in TAB5 0.2.182-ui; use 0.2.183-ui for completed-library memory preparation). This is opt-in diagnostics, separate from the RAM-only channel benchmark. Eight real manifests/JPEGs (two categories, two dates, both orientations) use the production authenticated download, bounded concurrent card writer, whole-image hash and committed image/text store. Automatic mode prefers Wi-Fi; USB-only mode measures USB. BLE is not a bulk-library test transport. The original link mode and credentials are never changed. The gallery measurement holds the benchmark gate while allowing its own authenticated gallery RPCs; OTA, photo uploads and unrelated probes remain exclusive.

Results are `state;gallery,transport,completed,receivedBytes,totalMs,receiveMs,writeMs,overlapCount,error`. `totalMs` includes sample preparation, inter-sample scheduling, validation and durable commit from first sample start; receive and write times overlap and must not be added. Only `complete` with eight samples is a successful run. A 180-second device deadline and explicit cancellation bound the task; partial/failure results are not throughput acceptance.

测速需要已启用 TF 卡及已安装图库。样本使用 `/aibot/cache/gallery-speed/` 下的独立存储根，复用正式图库目录格式；原图库图片、导读、断点和进度均不修改。每次重新接收样本，不把缓存命中算作传输。测试记录固定为 8 个键、每键两槽，连同临时分段最多约 24 MiB，并由后续测试复用。桥接“TAB5 连接 → 测速 → 图库同步测速”显示实际字节、端到端时间、接收/写卡阶段与重叠次数。失败、中止、代码回归和模拟文件写入均不算真机速度验收。

TAB5 0.2.184-ui optionally appends `;gallery_io,readBytes,writeBytes,readMs,writeMs,syncMs,syncCalls,bulkBytes,edgeBytes;gallery_rpc,encodeMs,exchangeMs,decodeMs,streamBackpressureMs,calls`. Counters reset at the first isolated sample and snapshot only between completed samples. Card times include production reads/verification, writes and durable fsync; bulk/edge count both reads and writes. Exchange includes transport and bridge response wait; Wi-Fi streaming excludes measured decrypt and writer-backpressure callbacks. Stream backpressure is zero on non-streaming USB. Stages overlap and cannot be summed as wall time. Older nine-field results remain valid.

0.2.184-ui 可追加上述读写和 RPC 分段统计：首个独立样本开始时清零，每个样本结束后快照。读卡包含校验读取，落盘统计保留正式 fsync；批量与边缘字节包含读和写。传输等待包含桥接响应耗时，Wi-Fi 流式统计排除已记录的解密与写卡背压，非流式 USB 的流式背压为零。各阶段可能重叠，不能相加代替总时间；旧九字段结果仍兼容。

## TAB5 runtime monitoring (optional, version 1)

`tab5_status.data.runtimeHealth` is an optional host-collected object; absent/null means no available monitor. Existing ESP8266 frames keep their current contract. Older TAB5 firmware can ignore the additional field. No input, node-selection or client-restart command is added.

The Mac producer supplies observed CPU/memory, completed disk-I/O latency and bridge sampling health using these same fields. Its monitor epoch comes from CPU/VM observation, not later publication. Sustained high occupancy is observed for 30 seconds before a warning; recovery requires 10 seconds of lower observations, with gaps resetting the window. App-owned, unauthenticated HEAD probes to the same two Windows targets run every 30 seconds via the system network path. Network checks retain their own observation time and expire after 75 seconds; at most ten actual rounds populate latency history, with failed rounds represented as gaps. PAC checks compare resolved proxy/direct types with URLSession transaction metrics, not exact nodes or VPN tunnel state. Missing PAC configuration is `disabled`; absent evidence remains unknown/unconfirmed. CPU queue length remains omitted because no equivalent public Mac counter is available. No proxy setting, model invocation or new device command is introduced.

Mac 使用相同字段提供实际 CPU／内存、已完成磁盘 I/O 耗时及桥接采样检查；监控时间取实际采样，不随发布续期。持续高占用观察 30 秒后警告，较低采样持续 10 秒后恢复，断档重新观察。App 内每 30 秒沿系统网络路径对 Windows 相同的两个目标执行无登录 HEAD 探测；网络项保留独立采样时间，75 秒后过期，历史最多十个真实探测轮次，失败留空。PAC 只比较解析结果与 URLSession 实际事务的代理／直连类型，不证明具体节点或 VPN 隧道状态；未配置 PAC 标为 disabled，证据缺失仍待确认。CPU 等待队列没有等价的公开 Mac 计数，继续省略。本项不修改代理设置、不调用模型、不新增设备命令。

The object carries `session`, alarm `sequence`, `updatedAt` / `epochUtc`, `checks`, `alerts`, optional `pressure`, `latencyHistory`, cached small-transfer throughput. Checks identify `cpu`, `memory`, `queue`, `disk`, `vpn`, `pac` and optionally `settings`; state is `ok`, `observing`, `warning`, `error`, `unknown` or `disabled`. Each check has a short summary, full detail/advice and observation timestamp. Successful response latency/time remains available when a later probe fails. `pressure` exposes processor queue, physical-disk I/O queue/latency, page input, available physical MB and logical processor count. Null counters are unknown, never zero.

`latencyHistory` has at most ten `{epochUtc, milliseconds, reachable}` points from actual probe rounds. Missing/failed samples render as gaps; repeated frames never create graph points. The main page shows only a short confirmed alarm title, while System → Runtime monitor groups graphical indicators and opens concise causes, key evidence and suggested actions on demand. Full raw diagnostic text remains available on the Windows bridge.

Alarm IDs belong to `session`; `sequence` increases only when a confirmed anomaly begins or changes severity. Repeat frames reuse IDs. System pressure must remain beyond the configured threshold; network anomalies require consecutive probe rounds. Confirmed recovery removes the alert. Device freshness is checked independently: an expired bridge frame or old monitor `epochUtc` removes healthy state and the live graph. Monitoring does not invoke a Codex model or export credentials/PAC query parameters.

Alerts expose full `advice` and optional `shortAdvice` for compact views. Receivers may fall back to `advice` if the short form is absent. Entering System defaults to the existing computer statistics and upload/download graph; the monitor is a secondary entry.

See [runtime monitoring](RUNTIME-MONITOR.md) for defaults, diagnostics, validation and the unflashed-preview boundary.

Additional optional checks `codex` and `bridge` report actual authenticated/model-connection observations and independent bridge/UI/collector health. The `pressure` object can add `commitPercent`, `committedMemoryMb`, and `commitLimitMb`; missing values remain unknown. Optional alert/check `handledExternally` only suppresses duplicate Windows notifications already handled by the independent guard; devices continue presenting the alert normally. Guarded presentation removes stale counters and latency history even when the HTTP publisher remains alive. No new device command or model request is introduced.

### BLE bulk response replay (local optimization candidate)

A paired TAB5 can advertise optional numeric `rpcReplay=1` in its existing GATT identity. Missing, malformed or unknown values disable replay. A supporting Windows bridge serializes native write submissions and confirms the end of each negotiated response window (at most 64 fragments). An ATT `0x0E` rejection allows one replay of that same window using individually acknowledged writes; other errors and cancellation terminate it. The sender retains the original request ID, total length and encrypted bytes, and holds the shared GATT gate through recovery.

Only the BLE receiver accepts replay, and only when the request ID and total length match the active response, the repeated fragment lies entirely inside the accepted prefix, and every byte matches. New data must still start at the exact receive cursor. Gaps, altered duplicates, overlapping new bytes and stale requests are rejected. Completion, authentication and decryption still require the whole response. USB retains its strict duplicate rejection. Old bridges keep their existing wire format; old devices use confirmed bulk writes without replay.

BLE 通道可通过现有身份信息的可选整数 `rpcReplay=1` 协商窗口重传。正常传输按窗口确认，接收状态拒绝时仅将当前窗口逐片确认补传一次。设备只接受同一请求、同一总长度、已接收范围内逐字节相同的重复分片；缺口、变更内容、交叠的新数据和过期请求仍被拒绝。完整响应的认证和解密检查保持有效。USB 保持严格拒绝重复分片，未协商能力的旧设备不使用重传。此段描述本地候选，实机速度和稳定性需另外验收。

## TAB5 Agenda v1 (local candidate)

`data.agenda` is optional: `{version:1, revision:<32 lowercase hex>, count:0..16}`. Definitions are fetched through authenticated `kind:"agenda", op:"sync"` on the existing RPC envelope; no ESP8266 field or command changes. USB, Wi-Fi and BLE share the existing session, timestamp, nonce, HMAC and AES-GCM checks. Unknown operations return 400; unavailable agenda storage returns 503 and must not clear the device cache.

The request may carry up to 32 actions (a completed occurrence and a later snooze per item): `{id,edit,occurrence,op:"complete"|"snooze",until}`. The response is `{version:1,revision,items:[...],conflicts:[id,...]}`. An item has `{id,edit,title,due,end,repeat,lead,done,snoozeOccurrence,snoozeUntil}`. IDs and editions are 32 lowercase hex digits; titles contain at most 32 characters/96 UTF-8 bytes. `due` is the start and `end` is the end of the base occurrence. New dated definitions require `due < end <= due + 86400`. `due=0,end=0` means an undated to-do (occurrence sentinel 1); it does not expire automatically. Legacy preview definitions with no end expire at due. `done` and complete actions remain for protocol compatibility; current UIs only snooze or dismiss. `repeat` is 0=once, 1=daily, 2=Monday-Friday, 3=weekly on the original weekday; `lead` is 0..60 minutes.

All schedule/occurrence timestamps are **local civil seconds**, represented by treating the host's local date/time as UTC for arithmetic. They are not UTC instants. The authenticated clock's separate `utcOffsetSeconds` remains unchanged. An unset RTC must not trigger alarms. The host and device use identical rules for early reminders across midnight, snoozes across midnight and advancing repeated items. A newer definition receives a new `edit`; stale edition actions are rejected as conflicts. Completion is monotonic and idempotent, and cannot erase a newer snooze. An occurrence expires at `occurrence + end - due` (at occurrence for a missing end); repeats advance automatically, including across midnight. Expiry is derived from time without Flash writes. Alerting is suppressed after expiry; snooze must be strictly before expiry. Successful snoozes are persisted before acknowledgement. A new revision never means empty data unless a complete, valid response explicitly contains an empty list.

中文：日程使用独立可选摘要和原有加密 RPC，最多 16 项。事项按电脑本地时间计时，设备先校准 RTC；开始与结束时间独立，提前提醒相对开始时间；到结束时间自动结束，重复日程转到下一次，过期提醒不补响。界面无手工完成；延后不得越过结束时间，先保存再同步。重复请求幂等，旧编辑版本不覆盖电脑改期。损坏或未确认响应保留原缓存，详情与验收边界见 [日程待办说明](AGENDA.md)。

Agenda sync additionally accepts at most 16 `edits:[{baseEdit,item}]`. The device generates a new 32-hex edition and persists definitions before acknowledging local saves; baseEdit remains the last confirmed host edition across repeated offline edits. An empty baseEdit creates a new item. A matching baseEdit replaces its definition; an already identical edition is an idempotent retry. Concurrent edits/deletion preserve a copy with `id=item.edit`, returning `forks:[{id,newId}]`; retries reuse that copy. Capacity or copy collisions return 409 and keep pending device data. Responses advertise `editSupport:1`; the device refuses to replace a pending-definition cache with an older bridge response lacking this flag. Same-edition alert deduplication survives normal and forked acknowledgements. Definitions and snoozes share the authenticated transport; no credentials enter item data.

设备端新增／编辑先本地持久化，离线可用。同步使用 baseEdit 做并发核对；冲突保留两份，满额或旧桥接保留本机修改，不静默覆盖。列表仅显示开始时间，编辑页可设结束时间。名称语音使用现有 voice 协议的独立 owner UUID `a1b07a65-0000-4000-8000-00000000a5ed` 及 `source:tab5`；只回填本地名称草稿，不创建／发送 Codex 任务。
## TAB5 status capacity 64 KiB / TAB5 状态容量扩展

Agenda 200 adds `repeat:4` for Chinese official workdays; existing codes 0..3 retain their meanings (including Monday-Friday). `lead` is minutes, including 1 and 3, never a dropdown index. Updated devices send `workdaySupport:1`; responses include that capability and `workdayYears:[{year,holidays:[{start,end,name}],workdays:[104,214]}]`, with integer month/day values and published annual data. A client without the capability receives 409 `agenda_workdays_requires_update_keep_local` when stored or submitted items require repeat 4; no definitions are dropped. Devices retain up to three annual bitsets in `agenda_days_v1`, independently from unchanged `agenda_v3`. Invalid/incomplete updates or failed writes preserve the last-good cache. Unknown future years display calendar pending and pause alarms rather than guessing weekdays.

日程 200 新增 repeat=4，按中国官方节假日和补班计算，原 0..3 编号不变。新设备声明 workdaySupport=1，桥接返回已公布 workdayYears；旧客户端遇到工作日事项返回 409 并保留本机数据。lead 为实际分钟数，包含 1 和 3。年度缓存独立持久保存，未知年份暂停提醒，成功同步后自动恢复。

Agenda 202 accepts a complete six-range year when holiday names cover all seven categories (for example a combined National Day/Mid-Autumn break). Annual calendar refresh and item acknowledgement are independent: a failed calendar update preserves the prior calendar and reports its own warning while a valid, matching item snapshot can clear pending edits. Invalid/missing item acknowledgements still retain local edits and retry. Existing item fields remain unchanged. The local-first snapshot described below adds durable deletion metadata.

日程 202 支持名称覆盖七类节日的六区间完整年度安排，例如国庆与中秋合并。年度日历刷新与事项回执独立处理：日历失败保留原缓存并单独提示，有效且匹配的事项回执仍可确认同步；无效或缺失的事项回执继续保留本机修改并重试。既有事项字段不变；本地优先快照增加下述持久删除记录。

TAB5 `0.2.198-ui` expands the encrypted status ceiling to **65,536 bytes**, including the 12-byte nonce and 16-byte GCM tag. Plain status and decompressed JSON must therefore fit **65,508 bytes**. New bridges aim for **49,152 bytes including encryption** (49,124 plain bytes), leaving 16 KiB of headroom. The BLE status length prefix is uint32 little-endian; individual ATT fragments and RPC mailbox lengths retain their existing bounds.

Capacity negotiation reuses existing identities: a matching validated USB hello/ACK with firmware `0.2.198-ui` or later, or the existing HMAC `FIRMWARE|<nonce>|<version>` for Wi-Fi and BLE. Missing, unknown, unsigned or older firmware stays at 32 KiB. Do not add fields to the existing near-512-byte BLE Info value. Firmware version alone from an unauthenticated Wi-Fi/BLE header cannot enable expansion. The bridge retains a bounded legacy status view, including decompressed JSON; compression does not make an oversized expanded state compatible with an old receiver. Old bridges can keep sending 32 KiB packets to new firmware.

The firmware aligns CDC lines, BLE reassembly, Wi-Fi replies, GCM, decompression and delta validation. Larger payload buffers use PSRAM; delta trees remain bounded at 8,192 nodes, 65,536 retained string bytes and depth 16. RPC/voice retain independent limits; the existing 65,535-byte RPC mailbox ceiling is not the status-packet ceiling.

Publication budgets task maps with the task catalog. Excess payload automatically falls back to valid current ordering/clock fields and available sections while preserving cached values and original sample times. It retries normal publication every cycle and restores full content automatically. Cached upgrade/display commands are not replayed. Test vectors and physical-device acceptance must be reported separately.

中文：新固件状态加密帧上限为 64 KiB，明文及解压后 JSON 最多 65,508 字节；桥接日常目标为含加密开销 48 KiB。USB 使用身份匹配的固件回报，Wi-Fi／BLE 复用现有固件版本 HMAC，确认配套版本后启用；旧版、未知或鉴权缺失维持 32 KiB。保留旧容量的解压后状态视图，避免仅凭压缩后变小就向旧设备发送大状态。接收／解密／解压／增量边界同步更新，大缓冲使用 PSRAM；RPC、语音沿用独立限制。超限自动降级并持续重试，缓存保留原采样时间，恢复不要求重启。此协议说明不表示固件已刷入或 64 KiB 三通道已完成真机验收。


### Agenda 202 local-first deletion / 本地优先删除

TAB5 acknowledges create, edit, snooze and delete only after its local NVS commit. Connectivity does not gate local management or calibrated offline reminders. The companion Windows UI remains an auxiliary editor and synchronized copy; no Windows agenda notification is added. A pending sync is shown as “已保存，待同步电脑”, separately from a failed local write.

`op:"sync"` additionally accepts at most 16 `deletions:[{id,edit,baseEdit}]`; IDs/editions use the existing format, and `baseEdit` may be empty. An item cannot appear in both deletions and edits/actions in the same envelope. Supporting bridges respond with `deleteSupport:1,deleted:[{id,edit}]`. Deletions are staged before definitions and committed together with them. Already absent editions are acknowledged without rewriting; lost receipts can be retried with a fresh authenticated nonce. Both the original edition and a matching `id=edit` conflict copy from a lost earlier edit receipt are removed. A concurrent desktop definition is retained under `id=desktop.edit` and reported through `forks`; capacity/copy collisions return 409 without committing the staged snapshot.

The device requires all exact deletion receipts, the capability, a valid full item snapshot and absence of deleted editions before clearing its queue. Unknown bridges, malformed/lost receipts and failed writes preserve the active local list and queued deletions. A local delete frees its active slot immediately; up to 16 deletions can wait for sync. A full deletion queue rejects the next deletion with a reconnect instruction while preserving its record. Queue entries are never evicted by age.

Active items plus deletion metadata are stored as one 5,872-byte versioned envelope in the existing `agenda_v3` key (original item document: 4,272 bytes), so a power failure cannot persist only half of a delete. Old exact-size documents are accepted without changing their items; the first successful write upgrades the envelope atomically. Corrupt/unknown snapshots stop loading and preserve their bytes. The previous 201 reader cannot read this new envelope: downgrade requires restoring the pre-upgrade NVS backup or a verified synchronized copy, rather than treating an old firmware boot as data-compatible. Partition layout, pairing and Windows `agenda.json` schema are unchanged.

中文：TAB5 的增删改、延后和离线提醒以本机为准，保存完成不再等待电脑确认。删除后立即从本机列表移除，同时原子保存待同步删除记录；重启后继续补同步，旧桥接或缺少精确回执时不会让旧记录重新出现。两端并发修改保留电脑修改副本并提示。待同步删除最多 16 条，满额时明确提示连接电脑后重试，不丢弃旧队列。升级沿用原 NVS 键，旧记录首次成功写入时原子升级存储封套；不修改分区和电脑 JSON 格式。201 不支持新封套，降级需恢复升级前 NVS 备份或经核验的同步副本。

### Agenda 204 bounded retention / 满额新增时整理

204 sends and requires `retentionSupport:2` on agenda sync. Ended one-offs remain in the document: reads, clock ticks, reboot and edits do not delete them. An insertion requiring space beyond 16 selects the oldest-ended one-off (legacy missing end uses due; ties use ordinal ID). Repeat rules, future and undated items are excluded. No victim returns a capacity error with the original data intact.

Device eviction and insertion share one atomic compact NVS transaction, including the existing `{id,edit,baseEdit}` deletion receipt. The 16-receipt bound remains: reconnect is required when full, rather than dropping unacknowledged removals. Sync applies deletions before edits, preserves incoming editions and concurrent conflict copies, and reclaims only the excess count from pre-existing expired host entries. Expired incoming edits still require precise acknowledgement; lost responses cannot cause additional eviction. 204 rejects snapshots from older bridges without the retention capability. The compact `agenda_v3` encoding introduced by 203 remains unchanged (maximum 3,574 bytes), with no new history archive.

中文：过期先保留，仅满 16 条且新增时清理最早结束的一条；不足 16 条、编辑及重启不清理。新建与清理原子保存，离线删除回执补同步，待同步队列满时提示连接电脑，失败保留原记录。“全部”可查看已结束事项。电脑批量合并保护本批离线编辑及冲突副本，只清理补足容量所需的旧过期记录；两端声明 retentionSupport=2，204 不采纳旧桥接响应。沿用 203 紧凑存储，不增加持久历史备份。

## TAB5 BLE keyboard unlock candidate / 蓝牙键盘解锁候选

`0.2.217-ble-unlock` adds the standard HID service `0x1812` and Device Information `0x180A` beside the existing private GATT service. Report ID 1 carries an 8-byte keyboard input report and LED output; report and boot keyboard modes are supported. Advertising keeps the existing private UUID and adds HID UUID and keyboard appearance within the 31-byte limit. No existing JSON/RPC fields or protocol version change.

HID input is permitted only on an encrypted, MITM-authenticated, bonded connection with notifications enabled, recent authenticated bridge data on that same BLE connection, and fresh locked-state evidence. PIN is never an RPC payload or a persisted value. Pairing needs explicit on-device numeric comparison; repeated pairing needs separate consent before removing that peer's old bond. A submitted sequence is one-shot and cleared on use/cancel/error. Enter waits for fresh post-digit locked state; success requires observed unlocked state, not a successful notification call.

候选仅增加标准蓝牙键盘服务，保留现有桥接、语音和数据协议。复用应用控制页的锁屏按钮，每次由用户输入数字 PIN，不保存、不经 HTTP/RPC 发送、不自动重试。蓝牙绑定密钥与 Windows PIN 分开处理。当前为刷机前候选，编译、模拟和界面预览均不代表真机解锁已通过。验收计划见 `docs/development/TAB5-BLE-UNLOCK-20261017.md`。


## Conservative BLE lock-screen preparation / 保守蓝牙锁屏封面准备

The separate conservative BLE cover-preparation candidate adds read-only `kind=computer-control, op=lock-screen` on the existing authenticated RPC envelope. Response: `{version:1, locked:true|false|null, lockScreenCover:true|null}`; null may be omitted. `true` requires a locked current session and a stable foreground CoreWindow owned by the Windows SystemApps LockApp image. Denied, unknown, another window and transitional states never confirm a PIN field/provider/focus. Older bridges return an unsupported-operation error; devices continue with manual PIN-page preparation and send no Space.

Only an explicit opening of the existing BLE unlock modal requests this sample. The reply is bound to that modal's request ID and expires 1,500 ms after request dispatch on the device's monotonic clock, including round-trip time. With fresh positive cover evidence and the existing secure BLE/HID/bridge/lock/connection-generation gates, TAB5 may enqueue exactly one Space press/release, never Enter or PIN as preparation. Key-down rechecks the same evidence; key-up remains best effort. No background poll, reconnect, cancellation, timeout or late reply initiates/retries preparation. This is a best-effort observation across asynchronous Windows/radio transitions, not atomic PIN-focus verification. Full hardware acceptance remains separate.

中文：保守蓝牙候选仅增加一次只读封面检查，不安装组件、不操作安全桌面、不读取 PIN。复用原解锁按钮与面板，只有明确的封面证据才发送一次蓝牙空格；过期、失败、旧桥接及未知状态均不发，也不宣称 PIN 框或焦点已确认。每次 PIN 仍由用户输入，不保存。此准备步骤不用于 Wi-Fi 键盘路径；候选说明见 `docs/development/TAB5-BLE-COVER-20261018.md`。

## Optional Wi-Fi keyboard candidate / 可选 Wi-Fi 键盘候选

`connectionHealth.wifiKeyboardVersion=1` means the current user configured the optional local adapter; it is not an unlock or service health acknowledgement. Firmware selects it only at opening the existing unlock panel with fresh Wi-Fi and USB/BLE absent. The choice stays fixed during the transaction.

`kind=wifi-keyboard` uses the existing paired-device encrypted RPC envelope and adds `op=begin|submit|step|finish|cancel`. `begin(confirmPinPage=true)` returns a one-use `offer`, AES-256 key and 15-second deadline. `submit(offer,sealed)` stages 4–64 numeric bytes encrypted as nonce[12]+tag[16]+ciphertext, with ASCII offer ID as AAD. No clear PIN field is accepted. `step(offer,next)` advances exactly one sequential numeric key press/release and renews a 2-second lease; replay or wrong sequence stops the transaction. `finish` requires all digits consumed and rechecks console lock state before one Enter. Cancellation and expiry wipe staged bytes and close the local connection. No operation is automatically retried or forwarded to USB/BLE. Only fresh unlocked computer state confirms success.

本候选复用现有锁屏按钮和加密通信，Windows 普通权限桥接仅对接经过身份检查的本机厂商服务；不启动旧的临时 SYSTEM 辅助程序，不更改登录认证方式。可选服务独立授权安装；PIN 不保存，独立组件访问密码用当前用户 DPAPI 保存。协议与验收说明见 `TAB5-WIRELESS-UNLOCK-20261018.md`（公共桥接目录在 `docs/development/`）。


## BLE display-wake preparation candidate / 蓝牙熄屏唤醒准备候选

The undeployed `0.2.219-ble-wake` candidate extends the existing authenticated read-only `computer-control / lock-screen` reply with optional `displayState:0|1|2|null` (Off, On, Dim, unknown). The ordinary interactive bridge tracks `GUID_SESSION_DISPLAY_STATUS` notifications in memory; registration failure, startup without a notification and suspend invalidate the state. Missing fields from older bridges are unknown; the new firmware does not fall back to unchecked PIN input. Existing firmware ignores the optional field. Protocol version and authentication envelopes stay unchanged; the probe never accepts or carries a PIN.

Opening the existing BLE unlock panel and explicitly confirming submission each start a bounded preparation (8 seconds). Fresh Off evidence, bound to the current request and secure connection, permits one modifier-only Left Shift press/release (`report[0]=0x02`, all character slots zero). TAB5 then reads the state again; On alone does not establish the page. Only a fresh positive LockApp cover observation permits one Space press/release, followed by another read. Waiting may repeat read-only queries, but never repeats either wake key. Key-down has a 500 ms dispatch deadline and rechecks evidence; request age is at most 1,500 ms including RPC time. Key-up remains best effort when evidence changes or cancellation races a send.

The user-confirmed PIN stays only in the device's volatile buffer while preparation is pending, and starts exactly once after fresh On/Dim evidence. The backend rechecks that same request before the first digit. Cancel, close, expiry, link/session change, unknown state or failure clears pending PIN and stops the action; there is no automatic PIN replay. A lit page with unknown cover is not positive PIN-field/focus evidence: the existing user prompt to confirm the PIN page remains. Display events and radio processing are asynchronous; this is not atomic secure-desktop focus verification or a sleep/resume guarantee.

中文：候选新增独立的屏幕电源状态，先以无字符的 Shift 唤醒，再重新检测是否为壁纸页，仅明确壁纸时发一次空格。面板打开后再次熄屏也由提交前检查覆盖，准备失败清空并停止，不自动重发 PIN。复用原按钮，不保存 PIN、不改变系统电源策略、不安装新组件，也不接入未安装的 Wi-Fi 解锁组件。完整记录见 `docs/development/TAB5-BLE-SCREEN-WAKE-20261018.md`；此候选尚未刷入，真机结果单独验收。

## BLE early-screen-off observation candidate / 蓝牙早期熄屏观察候选

The firmware-only `0.2.220-ui` candidate reuses the deployed `.219` bridge and the same authenticated read-only reply. Its explicit open/submit preparation budget is 12 seconds. An initial On/Dim sample, before a completed wake, is observed for approximately 6 seconds to accommodate delayed screen-off notifications; a fresh Off sample can trigger the existing modifier-only wake sooner. Unknown/expired reads and transient HID readiness may only cause bounded read-only retries. Peer disconnection, loss of security, connection-generation change, positive unlocked state, expiry, or uncertainty in an accepted key transaction stops preparation. At the user's request, a completed Shift transaction, including release, is followed by at least 1,500 ms settling and a NEW read. Only another fresh Off response may authorize one extra modifier-only Shift: two at most per explicit preparation. On/Dim skips the extra Shift; unknown state cannot authorize a key, and a failed/uncertain first transaction cannot be retried. Space is still accepted at most once, PIN is never automatically replayed, and rejected pre-admission attempts need new evidence. Freshness, dispatch deadlines, backend authentication, wire fields and protocol version are unchanged.

中文：`.219` 已部署，但刚黑屏约5秒的一次点击仍未通过用户验收；“请确认电脑已显示PIN登录页”不证明已发Shift。新候选让打开时未就绪的面板也进入准备，初始亮屏最多观察约6秒、准备最多12秒。按用户追加要求，第一次Shift事务完成后等待1.5秒，再重新查询；只有仍明确熄屏才最多补发一次，绝不连续盲发或发送第三次。空格仍最多一次，PIN不自动重发；未知状态只查询，失败/不确定的按键事务停止。原本已亮PIN页的打开与确认也可能各增加约6秒等待；通知延迟不受该取值保证，未知封面仍不表示已确认PIN焦点。未刷入、不安装Wi-Fi解锁组件，精确修订候选与验证见 `docs/development/TAB5-BLE-PREPARATION-20261018.md`。


## BLE simple unlock feedback and fast confirmation candidate / 蓝牙解锁简化反馈与快速提交候选

The local `0.2.221-ui` candidate adds optional `displayState:0|1|2|null` to ordinary authenticated `computer-control / status` replies, using the existing Windows session-display notification reader. The firmware caches it for UI feedback for less than 4,000 ms from request dispatch, including RPC latency. Missing, invalid or expired values are unknown. This does not add a poll, protocol version, RPC operation, service, driver, or Wi-Fi unlock adapter. Older firmware ignores it. This UI cache can disable local editing but cannot authorize HID: the separate `lock-screen` request ID, 1,500 ms evidence age, 500 ms dispatch deadline and all existing secure-peer/locked-state gates still apply.

When connection, lock state and preparation are ready, the panel only asks for the current Windows PIN and says it is not saved. Processing uses one brief wait message; failure asks the user to close and reopen the panel. Display-state diagnostics are not shown in the normal flow. A transient HID/status freshness gap clears local PIN and disables editing; recovery on the same secure peer can restore local editing. Disconnect, security loss, connection-generation change or a positive unlocked state revokes preparation. Confirmation always starts a NEW request.

Opening retains the `.220` bounded early-screen-off observation (about 6 seconds within a 12-second preparation). Confirmation removes the repeated fixed 6-second observation: a fresh positive reply can immediately proceed, while unknown/missing evidence stops and clears the confirmed action. A fresh Off or cover reply can extend the confirmation budget once for the existing wake preparation. Shift remains at most twice with at least 1,500 ms settling and a fresh Off read between attempts; Space remains at most once. There is no automatic PIN replay. Windows notifications may lag physical screen transitions; fresh RPC data is not atomic secure-desktop PIN-focus proof. The user-requested tradeoff is quicker submission and explicit close/reopen after failure, not a guaranteed physical-state oracle.

中文：正常仅显示“请输入 Windows PIN（不保存）”；处理中一句“请稍候”，异常才提示关闭面板重试。常规状态的可选 displayState 仅用于界面反馈和本地编辑门控，4秒内过期，不扩大按键授权；提交仍用新的请求绑定证据，去掉重复的固定6秒等待。短暂未就绪恢复可重新输入，PIN在未就绪、取消、失败等路径清除，断连／安全丢失／连接代次改变必须重新打开。打开面板的早期熄屏观察和有限Shift／Space保持。系统通知可能滞后，不宣称精确确认PIN焦点。候选未部署、未刷入，不安装Wi-Fi组件；审查与本地证据见 `docs/development/TAB5-BLE-SIMPLE-UNLOCK-20261018.md`，不能以构建或模拟代替新版本真机验收。

### TAB5 USB / BLE one-click unlock local candidate (0.2.224-ui)

本地未部署候选新增可选 `computer-control/status` 字段 `oneClickUnlock:bool`。旧桥接缺少字段按 false 处理，旧固件忽略新字段。只在一键解锁开关打开且当前配对设备有可用的 PC 加密 PIN 记录时为 true，不包含 PIN、长度或账户信息。关闭开关保留记录，但新的 USB/BLE unlock-pin 请求不能取得 PIN（返回 404）。

新增 `kind=unlock-pin, op=read, requestId=<UUID-D>`，仅在 USB/BLE 的既有鉴权 RPC 内可用；Wi-Fi 返回 400。沿用 session、issuedAt、nonce、AES-GCM 和请求 proof 校验。成功 body 为 `{version:1,requestId,packet:<base64>}`。`packet` 格式为 12 字节 IV + 16 字节 GCM tag + 4–64 字节数字 PIN 密文；AES-256-GCM 密钥为设备配对密钥，AAD 为 requestId 去掉连字符后的 32 字节 ASCII。请求 ID 为一次性，重复返回 409；未配置返回 404，无法解密记录返回 503，屏幕未就绪返回 409。设备单次取用，6 秒超时和取消后迟到回复都不能产生输入。协议版本仍为 1。

USB hello/ACK 增加可选 `usbHidState:uint32` 状态位，仅包含连接、同代次认证、就绪、忙、失败和解锁 UI 阶段。不包含 PIN、按键、输入长度。共享诊断位 10 为一键模式，22 为 USB 输入，31 为一键请求失败。USB 就绪证明来自加密 RPC 回复，不能由普通 CDC 状态 JSON 获得。

English: the undeployed local candidate adds optional `oneClickUnlock` to computer status and USB/BLE-only authenticated `unlock-pin/read`. Its encrypted response packet uses the pairing key and the 32 ASCII characters of the hyphen-free request UUID as GCM AAD; no plaintext PIN enters JSON or diagnostics. Duplicate request IDs, stale/cancelled responses and Wi-Fi retrieval are rejected. `usbHidState` is optional, credential-free readiness/UI telemetry. Existing peers may omit or ignore these fields. See `development/TAB5-USB-ONECLICK-20261018.md` for boundaries and acceptance.

The independent PC enable switch must be on as well as a valid pairing-bound PIN being configured. Saving a PIN alone does not enable one-click unlock; turning the switch off retains the PIN and rejects new credential grants on both USB and BLE. No firmware or protocol-version change is needed for this switch revision.

### TAB5 Wi-Fi one-click unlock candidate (0.2.227-ui)

本地未部署候选新增可选 `connectionHealth.wifiUnlockVersion=1`；只有电脑 Wi-Fi 解锁独立开关已开、当前配对 PIN 可用时通告，否则为 0。旧对端可以忽略。保持协议 version=1 及原配对 AES-GCM、HMAC、nonce、session、issuedAt 和二进制包检查。

新增 `kind=wifi-unlock`，仅 Wi-Fi。`begin {requestId:<UUID-D>}` 返回 `{ok,offer:<32hex>,next:0,stage:"preparing",unlocked:false}`；`poll {requestId,offer,next}` 消耗确切 next，返回新的 next 与 `preparing/sending/confirming`。`cancel {requestId,offer}` 撤销。Windows 实际未锁定且数字发送完整后才返回 `{ok:true,unlocked:true,stage:"unlocked"}`。响应不含 PIN、加密 PIN、密钥、账户或窗口文本，网络不能指定键值。

requestId 按配对身份与桥接会话单次消费；保留 120 秒，容量 256，满时拒绝。offer 绑定相同身份/会话/配对记录。40 秒总预算、2500ms 活动租约、250ms 清理周期、2000ms I/O 超时。重复 next、过期、取消或错误停止且不重发；已接受的在途单个输入无法保证撤回。已有 `unlock-pin/read` 仍仅 USB/BLE，Wi-Fi 拒绝。

English: optional `wifiUnlockVersion=1` advertises explicitly enabled Wi-Fi one-click unlock with a usable pairing-bound saved PIN. Wi-Fi-only authenticated `wifi-unlock/begin,poll,cancel` carries UUID/offer/sequence and status only, never a PIN or key. Each poll consumes its sequence before side effects, with bounded preparation, a 40s total budget and 2.5s lease. The PC uses its local saved PIN through an authenticated loopback vendor service; actual session unlock is required for success. USB/BLE PIN export policy is unchanged. This is an undeployed candidate; see `development/TAB5-WIFI-ONECLICK-20261018.md` for implementation and acceptance limits.

### TAB5 unlock history candidate (0.2.228-ui, local)

Mac implementation note (2026-10-23, unpublished): the saved Mac unlock candidate reuses the existing Wi-Fi-only `wifi-unlock` contract and `wifiUnlockVersion=1`, with no new fields or protocol-version change. A separate local enable switch and verified current-user/pairing-bound Keychain record are required. Passwords never enter device requests or replies; USB/BLE `unlock-pin` is not implemented for Mac and `oneClickUnlock` stays false. Enrollment verifies the account locally. The r66 Mac candidate binds the signed local login secure field and its window, writes once, then resolves and presses a unique explicit/default login button in that same window. Button discovery follows writing because the button was only observed with a nonempty field. Unsupported submission stops after that single write without replay or fallback; a field-level AXConfirm and screen-sharing session are not used. Unknown focus permits bounded display-wake preparation only, followed by independent loginwindow verification. Only two seconds of continuously confirmed current-console desktop return may report `unlocked=true`. Lease, sequence, cancellation and no-retry rules remain. See [Mac integration candidate](development/MAC-UNLOCK-DOUBAO-INTEGRATION-20261023.md).

#### Mac USB／蓝牙本机解锁候选 / Mac USB/BLE host-side unlock candidate (2026-10-24)

此扩展已完成固件构建及隔离软件检查；2026-10-24 的 `.267`＋ble-priority Mac 普通锁屏 USB／蓝牙首点、提示及 USB 拔线切蓝牙验收通过，Wi-Fi 回归通过，未覆盖整机睡眠或 Computer Use 外层交接。新增可选 `connectionHealth.hostUnlockVersion=1`，仅在独立 USB／蓝牙开关已开、当前账户及配对绑定的本机密码可用时通告；否则省略。`wifiUnlockVersion` 仍由原 Wi-Fi 开关决定，`oneClickUnlock=false` 不变，不能把此能力当作 Windows `unlock-pin` 授权。旧固件忽略新字段，不能据此视为已支持 Mac USB／蓝牙解锁。

- 已认证 USB／BLE RPC 使用 `kind="host-unlock"`，操作为 `begin`、`poll`、`cancel`。复用原 UUID `requestId`、32 位十六进制 `offer`、严格递增 `next`、`session` 与 `issuedAt` 结构；密码及按键内容均不经过设备。`host-unlock` 走 Wi-Fi 返回 `409 host_unlock_requires_usb_ble`，`wifi-unlock` 走 USB／BLE 仍返回 `409 unlock_requires_wifi`。
- 同一请求从开始到结束固定当前配对、会话与实际 USB／BLE 通道；固件另外绑定选定电脑 epoch、物理连接代数及设备连接模式。取消、超时、断连、重新连接、切换电脑或停用时结束，不改走 Wi-Fi、不重发 begin、不回退到 HID。即使请求 ID、offer 相同，也不能跨通道 poll 或 cancel。
- Mac 三个通道共用一个本机执行器及请求去重表，防止并发或跨通道重放。每次最多一次输入／提交，仍需本机确认桌面恢复。40 秒总时限、2.5 秒轮询租约保留；固件 `host-unlock` 的单次 RPC 上限为 2 秒。USB／BLE 断连只撤销匹配通道的本次请求，不撤销正在进行的 Wi-Fi 请求。
- USB／BLE `begin` 先完成权限、配对、会话、去重及连接代数检查，保留请求并返回 `preparing`，再在可取消的工作线程核对锁屏焦点，避免界面查询阻塞两秒回包窗口。核验前后均检查租约与取消，明确未锁屏时不执行输入；实际密码输入仍须独立确认可信登录控件。已有 Wi-Fi 前置核验次序保持。
- `.266` 本地修正：host-unlock 在同一两秒 RPC 总预算内，最多等待一秒让在途请求释放邮箱，等待发生在发布 nonce／请求字节之前；超时、主机变化或连接代数变化停止，不重发。只有本机 host-unlock busy 时暂停普通电脑状态轮询，终态后恢复以获取独立状态；既有 Wi-Fi／Windows 路由与预算不变。
- Mac USB／蓝牙开关默认关闭；已有 Keychain 密码可复用，启用它不更改 Wi-Fi 开关。Windows 不通告此能力，收到此 kind 明确返回 `409 host_unlock_unsupported`，不读取或返回 PIN，原 USB／BLE PIN/HID 和 Wi-Fi 流程保持。
- Mac 本机输入等待补记：可信 AX 目标的直接替换使用 150 ms 新鲜焦点稳定观察，仍要求至少三秒实际物理键鼠静止；接入验证／临时屏幕共享保留完整三秒输入源稳定。Wi-Fi 仍连续两秒确认桌面；USB／蓝牙 feedback 候选使用至少 0.5 秒及三次不同单调时刻的独立桌面核验，未知重新计时、回锁或取消停止。单次输入、租约及设备额外的新鲜电脑状态确认保持。阶段耗时只留在 Mac 内存诊断，不加入设备协议。
- Mac 后续本地候选：普通前台／桥接本进程菜单栏的系统 AX 桌面焦点只作显示状态证据。冷启动无解锁观察时，还需完整且没有可见已验证 loginwindow 的窗口样本、当前控制台和明确无外层保护；锁定观察优先，采样不完整不报告未锁屏。密码输入／后端成功核验保留原 `lockedState()` 规则，不使用上述显示辅助证据。已结束的 USB／BLE 请求可保留最多 2.5 秒终态读取窗口（仍限总计 40 秒），输入期间租约及 Wi-Fi 行为不变。现有 `unlockHistory` 只作最多八条有界数字诊断，不授权输入或判定成功。蓝牙真实控制回包后的一秒内优先后续控制，空／重复读不续期；四秒后台心跳机会和语音规则保留，协议字段不变。

English: a later local Mac candidate uses matching regular/own-accessory AX desktop focus for display only. Cold start additionally requires complete absence of visible verified loginwindow windows, the current console and explicit protection absence. Locks veto; incomplete samples cannot report unlocked. Credential and backend-success checks retain the original `lockedState()` path. Completed USB/BLE requests may retain a terminal receipt for up to 2.5 seconds within the original 40-second total budget; active input and Wi-Fi leases are unchanged. Existing `unlockHistory` is bounded numeric diagnostics only. Real completed BLE control requests receive a one-second continuation priority without renewal from empty/duplicate reads; four-second background fairness and voice rules remain. No wire fields change.

- Mac 熄屏准备补记：保护状态未知时只允许有界显示器活动声明，唤醒后必须重新明确确认无外层保护，才进入原可信登录控件及单次输入流程。已知保护直接拒绝；取消、超时及失败释放临时声明，不退出 Computer Use 或修改全局设置。
- 此扩展保留 Computer Use 外层保护阻止输入的规则，也不解决整机睡眠时桥接不可运行的问题；不修改系统全局设置。实现、固件补丁与验收边界见 [USB／蓝牙候选记录](development/MAC-USB-BLE-UNLOCK-20261024.md)。

English: this TAB5 candidate adds optional `connectionHealth.hostUnlockVersion=1` only for an explicitly enabled, usable current-user/pairing-bound Mac USB/BLE unlock configuration. The Wi-Fi capability and legacy `oneClickUnlock=false` remain independent. Authenticated USB/BLE `host-unlock/begin,poll,cancel` carries intent and progress only, using the existing UUID/offer/sequence/session/time envelope. The attempt is pinned to the same host, session, physical connection and selected transport; uncertainty cancels without HID, HTTP fallback or retry. All three Mac transports share one executor and replay journal, while USB/BLE disconnect only cancels its matching attempt. The 40-second budget and 2.5-second lease remain; the new firmware RPC has a two-second bound. Windows explicitly rejects this optional kind without returning a PIN and retains its existing routes. Old firmware cannot use the new feature. Computer Use protection, full-system sleep limitations and all global settings remain unchanged. Full firmware compilation and isolated software checks pass. Ordinary-lock first taps/results on USB/BLE, unplug-to-BLE handoff and Wi-Fi regression pass with the 2026-10-24 `.267`/ble-priority pairing; full sleep and Computer Use protection handoff remain outside that acceptance.

USB/BLE begin acknowledges the reserved, permission/session/pairing/replay/connection-checked attempt before asynchronous focus inspection. The cancellable worker checks the lease before and after that inspection, rejects an explicitly unlocked session and retains independent trusted-login-control checks before secret input. This avoids holding the two-second mailbox reply for UI inspection. The accepted Wi-Fi preflight order is unchanged. The local `.266` correction allows up to one second of mailbox admission inside the unchanged two-second total budget, before publishing any request bytes, and suppresses ordinary status polling only while host unlock is busy. Polling resumes at termination for independent state confirmation; no request is replayed. A dark display may leave protection evidence unknown: the Mac-only follow-up permits bounded display activity, then requires a fresh explicit absence of protection before any keyboard event or secret input. Known protection still rejects immediately; cancellation/timeouts release the temporary assertion. It does not dismiss Computer Use or alter global settings. The direct AX path uses 150 ms of fresh focus stability while retaining three seconds of physical-input quiet; enrollment/RFB preparation retains the full source-stability interval. Wi-Fi retains two-second stable-desktop confirmation. The USB/BLE feedback candidate requires at least 0.5 seconds plus three distinct-time independent desktop observations; unknown evidence restarts stability and relock/cancellation reject success. Stage timing is local metadata only, with no new wire fields.

Mac 实现补记（2026-10-23，未发布）：复用上述纯 Wi-Fi 请求与能力字段，不改字段或协议版本。独立开关及已验证、绑定当前账户／配对的钥匙串记录齐备才通告能力；密码不进入设备请求或回包。Mac 不实现 USB／BLE 密码导出，`oneClickUnlock=false`。接入时本机验证账户。r66 候选先核验 Apple 登录安全字段及所属窗口，仅写入一次，再定位并按下同一窗口的默认或唯一明确标注的登录按钮；实测仅在非空框观察到该按钮，因此不在写入前要求按钮已出现。提交不受支持则在单次写入后停止，不重填、不换路径，不使用字段 AXConfirm 或屏幕共享会话。未知焦点只允许有界唤醒显示器，必须重新核验登录窗口后才允许输入。连续两秒确认当前控制台回到桌面才可返回成功；租约、序号、取消及不重试规则保留。真实设备验收见候选记录。

The temporary r65 read-only probe has been removed in r66. No raw control snapshots are persisted or exported; protocol fields remain unchanged. Two r66 prepared-field unlocks succeeded; no-field, black-screen and Computer Use scenarios failed. r67 adds one public process-targeted nonprinting Tab before revalidating the secure field. Positive lock observations survive session invalidation; an optional undocumented boolean session hint may veto unlocked status but never authorize input or confirm success. Insufficient evidence returns the existing null lock state. No wire fields change. Physical acceptance remains pending.

r66 已移除 r65 临时只读采集路径，不持久化或导出原始控件快照，网络字段不变；r66 已准备密码页的两次解锁成功，无密码页、黑屏及 Computer Use 场景失败。r67 用公开进程定向接口发送一次非打印 Tab 后重验安全字段；会话失效不再清除锁定观察，可选未文档化布尔会话标记仅否决未锁定结论，不授权输入或证明成功。证据不足返回既有 null 状态；网络字段不变，实测待完成。

Local r70 supersedes the r68 probe/r69 raise preparation: after independent signed-loginwindow focus, current-user console and existing-event-permission checks, it posts one password-free unmodified Tab pair to the public system input stream, never replaying it. This differs from r67 process-targeted posting. Held modifiers/Tab reject preparation; cancellation/change/timeout after key-down still releases. Fresh secure-field/window validation precedes the retained single write/button submission. Event delivery cannot prove readiness or unlock. Existing null/boolean lock-state rules, network fields, authentication and no-retry semantics remain; no global settings change.

本地 r70 取代 r68 采集／r69 置前准备：仅在独立核验签名登录焦点、当前账户控制台与已有事件权限后，向公开系统输入队列投递一次无密码、无修饰的 Tab 按下／释放，不重发；与 r67 进程定向投递不同。按住修饰键／Tab 则拒绝，按下后即使取消／变化／超时也释放。重新核验安全字段和窗口后才走单次写入／按钮提交，事件投递不证明就绪或成功。既有状态 null／布尔、网络字段、认证及不重试规则不变，不改全局设置。

`computer-control` / `status` optionally carries `unlockHistory`: `1;` followed by 1–8 semicolon-separated rows, each exactly nine 8-digit hexadecimal uint32 words (maximum 585 characters). The authenticated, session-bound RPC envelope and replay validation remain mandatory. Other computer-control operations reject this field. This is numeric troubleshooting data only; it never authorizes input. Fields, flags and acknowledgement semantics are documented in [the candidate record](development/TAB5-WIFI-ONECLICK-20261018.md). Candidate firmware requires its matching bridge decoder. PIN, keycodes, account data and text are excluded.

TAB5 0.2.228-ui 本地候选在 `computer-control/status` 中可携带严格定长、最多 8 条的 `unlockHistory` 诊断，复用原有鉴权与会话/重放校验；字段与确认规则见上述候选记录。诊断不参与解锁授权，不包含凭据、按键或文本。

TAB5 216 expands saved Wi-Fi profiles to ten. NVS wifi_profiles3 stores version 3 with ten slots; migration reads fixed five-slot v1/v2 blobs without changing the pairing/config ABI or removing the old keys. SSIDs, passwords, labels, preferred selection and an authoritative empty list are preserved. A full list rejects an eleventh distinct SSID while allowing updates to existing entries.

Optional USB wifiHttpDiag contains nine numeric fields: version=1, status attempt count, ESP open error, socket errno, HTTP status, authenticated valid-status count, destination IPv4 as host-order uint32, destination port, local IPv4 as host-order uint32. It contains no credentials or authentication headers; old clients ignore it.

## TAB5 217 connection path diagnostics / 连接路径诊断

Optional wifiPathDiag contains 17 numeric fields: version=1, IPv4 netmask, gateway IPv4, AP channel, RSSI, STA MAC as uint48, BSSID as uint48, station-is-default boolean, probe count, gateway TCP/80 errno, host TCP/configured-port errno, transport ESP error, TLS code, TLS flags, Wi-Fi power-save mode, AP query result, netif MAC as uint48. IPv4 values are host-order uint32. Zero probe errno means connect succeeded. On failed bridge HTTP connects only, the Wi-Fi worker samples at most once per 30 seconds, using nonblocking sockets with one-second connect waits; USB only copies the snapshot. Probes close their sockets, send no HTTP request, skip OTA, and do not change network settings.

中文：桥接连接失败时对网关与电脑做有界 TCP 对照，并记录掩码、网关、信号和底层错误；不修改网络、密码或配对。诊断新增不等于连通问题已修复。

## Extended unlock credentials / 扩展解锁凭据（本地候选）

Windows 桥接通过 `connectionHealth.unlockCredentialVersion=2` 通告扩展凭据支持。
USB/BLE 固件仅在收到该能力后，在既有 `kind="unlock-pin", op="read"` 请求中加入
`credentialVersion: 2`；未通告的旧桥接沿用原请求字段。配对、HMAC、AES-GCM、session、
issuedAt、requestId 一次性消费与内层加密保持不变，不在 JSON、回执或日志中写入凭据。

版本 2 接受 **1–64 字节可打印 ASCII（0x20–0x7e）**：大小写英文字母、数字、空格与英文符号。
首尾空格原样保留；拒绝空字符串、控制字符、DEL 和非 ASCII，不进行替换或截断。
没有声明版本 2 的旧固件只可取得原有 4–64 位纯数字凭据；其他值返回
`409 credential_upgrade_required`。数字凭据原有 DPAPI 文件及独立启用开关保持兼容，
不迁移或重新保存用户凭据。

TAB5 手动输入每次默认数字键盘，可依次切换小写、大写、符号并回到数字。
确认按钮从一字符起可用，显示内容仅为遮罩；取消、超时、发送完成或失败均按原流程清除。
USB/BLE 按英文美式键盘映射输出 HID usage 和 Left Shift，并依据主机 Caps Lock LED
调整字母的 Shift；每次按下后独立松开。Wi-Fi 由 Windows 本地 RFB 服务输入原始 ASCII
keysym，只允许可打印字符与一次 Enter；不自动重试或换通道。登录方式仍须与 Windows
当前选中的 PIN/密码框一致，不自动切换登录方式。

The Windows bridge advertises `connectionHealth.unlockCredentialVersion=2`. New firmware
adds `credentialVersion: 2` to the existing authenticated `unlock-pin/read` request only
when advertised; older bridges keep the original request. Version 2 accepts 1–64 printable
ASCII bytes, preserves case and edge spaces, and rejects empty/control/non-ASCII input.
Legacy firmware receives only its original 4–64 numeric credentials; incompatible requests
return `409 credential_upgrade_required`. Existing DPAPI records remain compatible.

The manual keypad always opens in numeric mode and can switch through lowercase, uppercase
and symbols. USB/BLE use the host English US layout with per-character Shift, host Caps Lock
LED compensation and key releases. Wi-Fi types printable ASCII keysyms through the local
Windows adapter. Existing authenticated, one-shot, wipe-on-exit and no-replay rules apply.
Windows must already have the matching sign-in method selected. macOS is unchanged.

## TAB5 trusted computers / TAB5 可信电脑（本地候选）

设备可保存最多 8 台可信电脑，每台首次通过 USB 明确配对，使用其原有独立密钥。
`trusted_hosts1` 是独立版本化 NVS blob；保留原 config ABI 与密钥供回退，迁入已有配对，
容量满时不淘汰记录，当前记录损坏时不从旧记录复活。电脑端 DPAPI / Keychain 记录和
密码绑定保持不变。当前电脑的选择持久保存；断开 USB 后继续使用该电脑的无线通道，
不会因其他电脑广播而自动改选。网络诊断可选择可信电脑；控制页显示当前电脑。

`hostId = hex(HMAC-SHA256(pairKey, UTF8("AIBOT_TAB5_HOST_V1"))[0:16])`，固定 32 位小写十六进制。
电脑名称为 1–63 UTF-8 字节，不允许控制字符或 `|`；密钥不出现在公开列表和诊断。
USB hello / ACK 增加 `hostBindingVersion:1`, `hostNonce`, `currentHostId`,
`usbHostAuthenticated`。新桥接发送 `tab5_host_claim`，字段为 `version:1,deviceId,hostId,
nonce,host,port,name,proof`。`host` 是私有 IPv4 或空字符串，`port` 是 0–65535 整数。
签名消息为 `TAB5_HOST_V1|deviceId|hostId|nonce|host|port|name` 的 UTF-8，HMAC-SHA256 使用
该电脑独立密钥，输出 64 位小写十六进制。nonce 由设备产生，绑定本次 CDC 代次，成功后消耗。
`tab5_host_claimed` 回执包含 `deviceId,hostId,proof`，签名消息为
`TAB5_HOST_ACK_V1|deviceId|hostId|nonce`。未受信任/错误签名/旧 nonce 均不接管。

认证成功的 USB 电脑优先接管；仅串口连通不构成认证。8 秒内的有效状态/认证 RPC 续期，
其他电脑不能覆盖其无线目标。正在刷写时，另一台电脑的接管返回 `device_busy`。
切换先阻止新按键并取消、排空旧输入，再更换代次、清空旧状态/RPC，并断开旧 BLE 中央设备；
系统蓝牙 bond 保留。USB HID、BLE HID 和无线解锁均绑定电脑代次，旧回调不能给新电脑授权。
除首次配对、接管握手和只读诊断外，USB 配置、快捷键、资源、状态和 RPC 均须已认证。
旧式无签名 `tab5_host` 不能改写其他电脑的地址。

Wi-Fi 仅使用当前电脑的密钥/地址发现与鉴权。BLE 的只读特征
`7af50007-7f23-4a91-bc65-667a19320101` 返回 `{version:1,hostId,bleProofRequired:1}`；非当前电脑断开待命，
桥接延后重连，不发送状态或执行 RPC。此公开字段只用于待命判断，不能代替原有密钥鉴权。
状态可附加 `connectionHealth.hostBindingVersion=1,hostId,hostName`；有 hostId 时必须匹配。
旧无线桥接可在设备明确选中其已保存密钥时继续使用原加密协议；接收与 HID 授权还必须匹配
解密开始时的电脑代次，防止切换竞态。新固件的 USB 自动接管需要对应桥接升级。

0.2.237-ui 将蓝牙物理连接与有效电脑通道分开：同一连接必须提交当前所选电脑密钥加密的归属证明或有效状态帧，才开放 RPC/语音并成为有效 BLE 通道。Windows 在进入 USB 待命前先完成短归属证明及签名 ACK 确认；旧固件未声明 bleProofRequired 时保持原流程。仅发现、系统 bond 或读取公开 hostId 不能给连接授权。未验证连接在 20 秒后断开，已识别的其他电脑身份立即拒绝。

短证明只有五个字段：`{version:1,type:"tab5_ble_owner",deviceId,hostId,sequence}`。`sequence` 为 1–9007199254740991 的整数，使用桥接共享序列分配器，`deviceId` 与 `hostId` 必须精确匹配当前目标。通过现有连接 nonce 的 AES-GCM 和长度前缀/分片协议传输；确认仍为 info 特征的 `nonce|ack` HMAC，ack 必须等于本次 sequence。证明不发布显示状态、不刷新 USB 租约；迟到连接或电脑代次变化均不得恢复授权。桥接先证明再进入 USB 待命，避免低 MTU 下发送完整状态超过首次认证时限。

`host_ble_v1` 单独保存可信电脑 hostId 与稳定蓝牙身份地址的映射，不改 trusted_hosts1 或 config ABI，不输出地址或密钥。映射只能由同一连接上的有效当前电脑状态帧建立，旧电脑代次、保存失败或损坏记录均不放行；地址尚未解析的 RPA 不持久化。已知目标使用控制器连接白名单，换电脑时断开旧连接并更新名单；系统 bond 保留。选定电脑的 Wi-Fi 与 BLE 可同时可用；无权归属当前电脑的物理连接不能阻挡其 Wi-Fi 解锁，USB 输入就绪时仍优先 USB。任何通道不可用都不会改选其他电脑，USB 插入未绑定电脑不改变当前目标；已绑定 USB 的签名接管仍是自动切换信号。

Trust up to eight computers independently after explicit first USB enrollment. Preserve the old config
ABI, each existing pairing key, DPAPI/Keychain data, and credential bindings. A separate versioned NVS
store migrates the current pairing; full/corrupt stores never silently evict or resurrect records.
Persist one selected computer. Authenticated USB takes ownership; unplugging retains its wireless
target, and other computers cannot win by advertising. A live USB owner blocks another target;
an ongoing firmware write returns `device_busy` to a different claimant.

Derive the 32-lowercase-hex host ID from the first 16 HMAC-SHA256 bytes using the existing pairing key
and domain `AIBOT_TAB5_HOST_V1`. The one-use device nonce is scoped to the CDC generation. Authenticate
the exact UTF-8 claim and ACK strings specified above, including endpoint and name. A raw serial
connection, owner hint or unsigned legacy endpoint update never authorizes control. Status/RPC
heartbeats renew the eight-second USB lease. Names are 1–63 UTF-8 bytes without controls or `|`.

Before publishing another owner, block keyboard admission and drain/cancel input, invalidate status
and RPC generations, and disconnect the previous BLE central while retaining OS bonds. Bind state
commit, decrypted replies, and HID authorization to the original owner generation. The optional BLE
owner characteristic above lets unselected Windows/Mac bridges disconnect and wait; cryptographic
authentication remains mandatory. Optional authenticated state adds `hostBindingVersion,hostId,
hostName` under connectionHealth. Legacy wireless peers remain usable only with the selected key;
USB takeover requires the updated bridge. Protocol version 1, encryption, nonce/ACK/replay rules,
transport selection and unrelated device display preferences remain unchanged.

In 0.2.237-ui, BLE ownership requires an encrypted ownership proof or accepted status from the selected computer on the same connection before RPC/voice access. `bleProofRequired:1` requests proof and a signed ACK before USB standby. The short proof has exactly five fields: `{version:1,type:"tab5_ble_owner",deviceId,hostId,sequence}`. Match both IDs and allocate an integral sequence in 1–9007199254740991 from the shared bridge sequence source. Use existing connection-nonce AES-GCM, length-prefix/fragments, and the signed info `nonce|ack` acknowledgement. Do not publish display state or renew USB ownership from this proof. Unproven connections expire after 20 seconds; known foreign identities are rejected. A separate `host_ble_v1` store maps host IDs to stable peer identities without changing existing trust/config blobs or exposing keys/addresses. Learn only from the authenticated connection, reject stale generations and persistence failures, and do not persist unresolved RPAs. Filter known connections to the selected peer while retaining OS bonds. Selected-host Wi-Fi may coexist with BLE; unrelated physical connections cannot block Wi-Fi unlock. Never switch computers because a transport is unavailable. Only an explicitly selected target or an authenticated, already-trusted USB takeover changes ownership; an unbound USB computer does not.


### Mac BLE voice window candidate / Mac 蓝牙语音窗口候选

Mac 本地候选接入已有语音特征的 type=3 通知授权。仅在当前配对身份认证成功、`mailboxWindow` 为 1..8 整数、Notify 属性存在且订阅成功时启用；缺失能力或订阅失败保持原逐片读取。每窗最多八片／每片最多 488 字节，保留请求 ID、连续偏移、恒定总长、12 KiB 语音上限、原 HMAC／AES-GCM 与去重；通知四秒、语音请求十二秒内完成，错误或不确定结果不重发。先收到通知不等于授权写入已确认，两者均满足才处理下一窗或完整请求。RPC 接收不变。

后台状态／资源发送只在已确认且非最后的分片边界检查语音邮箱，约一秒未检查时发起一次只读，不推进邮箱游标或在半帧中执行语音。设备现有半帧五秒限制使完整语音交换不能任意插入状态帧；状态仍须完整签名 ACK。近期成功语音请求获得短间隔邮箱优先；控制邮箱约每秒探测一次，语音、空邮箱或控制查询结束后在这一短租期内继续调度，空／重复请求不续租。每个后台帧确认后先检查语音邮箱，避免排入另一资源帧。最多将原状态发送触发从完成后两秒延后到四秒，资源等待近期语音空闲。这是 Mac 本地实现变化，不新增字段、不更改固件或扩大权限；候选实机延迟仍待验。

The local Mac candidate uses the existing voice characteristic's type-3 notification grant only after current pairing authentication, an integer mailboxWindow of 1..8, Notify support and successful subscription. Missing capability or failed subscription retains legacy reads. Each window is bounded to eight packets of at most 488 bytes, retaining exact IDs/offsets/totals, the 12 KiB voice limit, HMAC/AES-GCM and deduplication. Notification windows expire after four seconds and voice requests after twelve; uncertain outcomes are not replayed. Early notifications cannot advance handling before the grant write is acknowledged. RPC receiving is unchanged.

Background status/resource writes probe the voice mailbox only at acknowledged nonfinal fragment boundaries when about one second has elapsed, without advancing its cursor or executing a voice exchange mid-frame. The existing five-second partial-frame limit prohibits arbitrarily inserting a whole exchange; signed whole-frame ACK remains required. Recent successful voice traffic briefly prioritizes voice, probing control about once per second. Voice, idle and control completions continue scheduling within that short lease; idle/duplicate polls do not renew it. Each background ACK first probes voice rather than immediately queueing another resource frame. The normal status trigger is deferred from two to at most four seconds after completion, while resources wait for recent voice inactivity. These are local Mac implementation changes without new fields, firmware changes or wider permissions; physical candidate latency remains pending.


### Mac BLE existing state-delta candidate / Mac 蓝牙既有状态增量候选

Mac 本地候选补用既有 `tab5_delta`，不新增 wire 字段或设备要求。仅在当前配对／nonce／ACK 及固件版本证明通过、`telemetryVersion=3` 且版本为已检查维护者接收器的 `0.2.234-ui` 或更高 `0.2.x-ui` 时启用。每条 BLE 连接独立保存一份已获精确签名序号 ACK 的完整逻辑状态；发送尝试、资源 ACK、Wi-Fi 或 USB 状态均不推进该基线。断线／归属或配对重置丢弃基线，首次仍发完整状态。设备／会话变化、非递增序号、OTA 或显示命令待处理／清除过渡、字段或树界限不满足时仍用既有完整状态路径。更新数组整体替换，null 是普通值，删除显式使用 `remove=true`；路径深度最多 16、键非空且 UTF-8 不超过 128 字节、最多 256 项，树保留原 8192 节点／65536 字符串字节界限。布尔值与数字严格区分。增量也使用既有 `tab5_packed` 压缩：先比较原始／压缩增量，再与压缩完整帧比较，选择整个封装更小者。展开后的增量仍受 32740 字节上限约束；仅已验证 telemetry-v3 和基线的调用方显式允许压缩增量，嵌套压缩与其他消息不启用。超限或无收益回退；加密、总帧上限、分片、ACK 验证、发布调度和失败不重放保持不变。此为现有协议的 Mac 实现，不更改 Windows 或固件；合成测试不代表蓝牙延迟实机通过。

The local Mac candidate adopts existing tab5_delta without new wire fields or receiver requirements. It requires the current authenticated pairing/nonce/ACK, firmware proof, telemetryVersion 3 and the inspected maintainer receiver version 0.2.234-ui or later 0.2.x-ui. Each live BLE connection retains one complete logical state only after its exact signed sequence ACK. Sending attempts, resource ACKs and Wi-Fi/USB states never advance that baseline. Disconnect, ownership/pairing reset discard it, so initial delivery remains full. Identity/session changes, non-increasing sequence, pending/cleared OTA or display commands, and invalid tree/path limits retain full-state delivery. Arrays replace atomically, null is a value and removal is explicit; paths have at most sixteen nonempty keys of at most 128 UTF-8 bytes, changes at most 256 operations, and trees retain the 8192-node/65536-string-byte limits. Booleans and numbers remain distinct. Deltas also use existing tab5_packed compression: choose between raw/packed delta and the packed full state by complete envelope size. Expanded deltas retain the 32740-byte limit; only the authenticated telemetry-v3/baseline path explicitly enables delta packing, never nested packing or unrelated messages. Oversized or non-beneficial deltas fall back. Encryption, frame limits, fragmentation, authenticated ACKs, scheduling and no replay on failure remain unchanged. This implements an existing protocol on Mac without Windows/firmware edits; synthetic tests do not establish physical latency.

### Mac BLE status and image scheduling / Mac 蓝牙状态与图片调度

Mac BLE 状态帧保留最新状态、完整 `assetIds` 与原心跳节奏，但不再夹带可选 `data.resource` 图片分片；仅查询资源标识不会推进轮转位置或分片偏移。图片沿用既有独立 `tab5_resources`，每个空闲发送机会只携带一片（最多 1024 原始字节），而不是连续四片。录音活动期间原资源退让规则因此同时覆盖所有 BLE 图片字节，状态与控制仍按原调度更新；空闲后继续传输，收到原资源证明才标为完成。USB／Wi-Fi 保留原默认行为。未新增字段、改变设备接收或身份／序号 ACK 校验；这是 Mac 发送方调度修正，实际延迟另行实测。

Mac BLE status retains current data, complete assetIds and the existing heartbeat cadence, but omits optional inline data.resource image chunks. Metadata-only projection does not advance slot rotation or byte offsets. Images use the existing standalone tab5_resources format, with one chunk (at most 1024 raw bytes) per idle opportunity instead of four. The existing voice-activity deferral therefore covers all BLE image bytes, while status/control retain their scheduling. Idle transfer resumes normally and completion still requires the existing resource proof. USB/Wi-Fi defaults are unchanged. No new fields, receiver behavior or identity/sequence ACK rules are introduced; this is a Mac producer scheduling correction, with physical latency evaluated separately.

Mac 对已验证 BLE `voice/stop` 的首次本地结束提供最多 200 ms 的最终识别等待；仅收到实际最终回调才在同一停止响应返回 `review`，慢识别仍返回 `recognizing` 并沿用查询，不提升部分文字。等待不持有语音锁，完成、错误、取消会唤醒；回复前重新核对功能许可、录音身份和后端代次，不能返回取消后新录音的正文。USB／Wi-Fi 不增加等待，认证、字段、最后 ATT 确认和设备端停止排空规则不变。

For the first locally handled stop of an authenticated BLE voice request, Mac allows at most 200 ms for a final recognition callback. Only an actual final result can return review in the same stop response; slower recognition retains recognizing and normal polling, without promoting partial text. The wait releases the voice lock and is signaled by completion, error or cancellation. Consent, capture identity and backend generation are checked again before replying, preventing a replacement take from leaking into a cancelled request. USB/Wi-Fi add no wait; authentication, response fields, final ATT confirmation and device audio-draining rules remain unchanged.


Mac 通知订阅在当前 BLE 连接首个完整状态的精确签名 ACK 之后执行；info 读取通过并不等于设备已经验证这台电脑。旧设备无单独 owner 证明时，延后订阅用于确保先完成电脑认证；现场仍收到 ATT/8，不能将此顺序视为已解决订阅错误。标准订阅返回 ATT/8 时，只读发现并读取该语音特征的唯一 CCCD；仅合法整数值 1 允许在同一已认证连接上再次调用一次标准 setNotifyValue(true)。关闭、未知、格式异常、其他错误、二次失败均保留逐片读取。读回值本身绝不授权通知窗口，仍必须收到无错误且 isNotifying 的系统回调；不直接写 CCCD、不清理 bond、不重放语音／用户操作。连接重置清除确认和本次核对状态，不绕过 owner／pairing／nonce 校验，不改变设备协议。

Mac subscribes to voice notifications only after the current BLE connection's first full state has its exact signed ACK. Verifying info alone does not establish that the device has authenticated this computer. This ordering establishes computer authentication first on legacy peers without separate owner proof; physical ATT/8 persists, so ordering alone is not a verified subscription fix. An ATT/8 subscription failure triggers read-only discovery/read of the voice characteristic’s unique CCCD. Only exact integer 1 permits one further standard setNotifyValue(true) call on the same authenticated connection. Disabled/unknown/malformed readback, other errors or a second failure keep legacy reads. Readback itself never admits notification windows: a successful system callback with isNotifying is still required. No direct CCCD write, bond deletion or voice/user-operation replay occurs. Reset clears confirmation/reconciliation state; owner/pairing/nonce checks and the wire protocol remain unchanged.


Mac 已确认语音通知订阅时，既有 type-3 窗口授权在特征支持 WriteWithoutResponse 且长度适合时使用 command，并遵守 CoreBluetooth `canSendWriteWithoutResponse`／ready 回调。单窗口只提交一次；本地提交不代表设备接受，只有完整且 id／位置／总长一致的受限通知才能推进认证处理。丢 grant／通知超时不重发；不支持 command 则等待原 ATT 应答。语音回复末片仍需 ATT 确认，owner／AES-GCM／序号／整帧签名 ACK 不变。这与现有 Windows 发送方式一致，不增加设备协议或固件要求。

After confirmed voice subscription, Mac sends the existing type-3 grant as a command when the characteristic supports WriteWithoutResponse and the grant fits, obeying CoreBluetooth flow control/ready callbacks. Each window submits once. Local submission is not acceptance: complete bounded notifications matching id/offset/total must arrive before authenticated processing. Lost grant/notifications time out without replay; unsupported commands retain acknowledged grants. The final voice-response fragment still needs an ATT reply; owner/AES-GCM/sequence/signed whole-frame ACK checks remain unchanged. This matches existing Windows sending behavior without new wire or firmware requirements.


### Mac BLE immediate continuation / Mac 蓝牙新请求立即续传

Mac 在新语音请求的最后回复片获得 ATT 成功回调后，立即回到原有调度器，不再额外等待 15 ms；Windows 已采用相同的新请求续传原则。空／重复邮箱和控制查询保留短租期内的 15 ms 退让，空闲不续租；控制与状态的原有公平性规则不变。失败、取消配对、超时仍不推进完成序号或重发。仅调整 Mac 本地调度，不新增字段或改变设备协议。新增有界诊断区分窗口授权提交到首片通知、后续通知片间隔，包含系统回调等待，不声称测得纯空口时延。候选仍需实机验收。

After the final reply fragment of a new voice request receives its successful ATT callback, Mac immediately resumes the existing scheduler instead of adding 15 ms, matching Windows' new-request continuation principle. Empty/duplicate mailboxes and control queries retain their 15 ms active-lease backoff without renewing it; control/status fairness remains unchanged. Failures, pairing changes and timeouts do not advance completion or replay operations. Only local Mac scheduling changes, with no wire/firmware changes. Bounded diagnostics separate grant submission to first notification from later inter-notification gaps, including system callback waits rather than claiming pure over-air latency. Physical acceptance remains pending.

### Mac module support · 2026-10-22 / Mac 模块实现范围

The local Mac candidate now produces the existing birthday detail, agenda summary/RPC and pinned Codex-task fields. Agenda supports edit/delete/retention/workday capability negotiation, preserves conflicting edits and unknown-year calendars, and uses the existing civil-time representation. Eight domestic providers project their observed balance/subscription windows into existing quotaDetails fields; no field is inferred from local token counts. Per-device source filtering applies before publication and disabling a registered device stops its transport activity and discovery replies. This supersedes historical statements that Mac does not produce quotaDetails or implement gallery RPC. No new wire fields, command, protocol version or receiver requirement is introduced.

Mac 本地候选现已生成已有的生日详情、日程摘要／RPC 和 Codex 常用任务字段。日程协商编辑／删除／保留／工作日能力，保留冲突编辑与未知年份状态，沿用本地民用时间表示。八个国产来源的真实余额／订阅窗口投影到已有 quotaDetails；不使用本机 token 数估算。按设备过滤后发布，停用登记设备会停止其传输活动与发现响应。本说明更新早期“Mac 不生成 quotaDetails／不支持图库 RPC”的历史限制；没有新增协议字段、命令、版本或接收端要求。

Runtime monitoring keeps the existing observation timestamps. Mac settings now control enable/network flags, explicit proxy expectation, 15–300-second probe intervals, slow-response/CPU/memory/sustained thresholds and optional bounded 128-KiB sampling every five minutes. Network freshness is the configured interval plus 45 seconds. Settings never change system proxy configuration; queue counters remain unknown. Missing measurements are never zero-filled. Device acceptance remains separate from host compilation and synthetic validation.

运行监控继续使用真实采样时间；Mac 设置控制监控／网络开关、显式代理预期、15–300 秒间隔、慢响应／CPU／内存／持续阈值，以及可选每五分钟最多 128 KiB 测量。网络新鲜度为所选间隔加 45 秒，不改系统代理，处理器队列保持未知，缺失采样不补零。实机验收与编译、合成测试分开记录。


### macOS adoption of .250 runtime diagnostics and quota carry-over (2026-10-22)

Mac implements the existing Windows development-branch contract from `79ffe7f`; protocol `version=1` is unchanged. `connectionHealth.runtimeDiagnosticCommand` is optional: `{version:1, requestId:<32 lowercase hex>, transport:"Wi-Fi"|"BLE", expiresAt:<epoch milliseconds>}`. It targets only the current paired host. The bridge uses a ten-second deadline and an eight-second first wait; only the same host's other live wireless link may be retried once, with a new request ID.

The existing authenticated RPC receives `{kind:"runtime-diagnostics", requestId, session, issuedAt, snapshot}`. HMAC/AES-GCM/session/time/nonce validation precedes dispatch. The pending device, host, transport, request and expiry must match. Snapshots are limited to 8192 UTF-8 bytes; `diagnosticVersion=1`, `sampleAgeMs=0..8000`, nonnegative bounded counters, and required `displayDiag`, `cameraDiag`, `photoDiag`, `photoUploadDiag` strings (1..1024 ASCII bytes, letters/digits or `,;-_.:/ `) are validated. Duplicate/late replies cannot replace the last-good sample. USB full status ACKs feed the same display; resource ACKs do not clear it. No credentials or arbitrary payloads are logged. Firmware before `0.2.250-ui` remains USB-only for detailed diagnostics.

Mac quota day projection accepts optional `carryOver` in weekly percentage points, matching Windows. A same-account/same-plan valid cumulative baseline may cross midnight; only observed growth or a verified new-window reading is counted. Positive carry-over marks the day partial and excludes it from the complete-day average. Missing samples remain unknown. Local observation bounds, sample/reset/gap counts are UI details and are not transmitted. No historical readings are rewritten.

Mac 已适配 .250 无线诊断既有协议：只对当前绑定设备发出短期请求，首个通道超时可向同一电脑的另一在线通道重试一次；认证、过期、重复与大小检查后才接收，失败保留旧样本并显示年龄。额度跨日累计补记使用可选 `carryOver`，明确标记部分采样，不推算缺失用量、不改写历史观测。

### macOS connection/gallery desktop parity (2026-10-22)

Mac now consumes the existing USB `tab5_wifi_list`, `tab5_wifi_label`, `tab5_wifi_forget` and `tab5_benchmark` commands. Identity/version and network bounds are validated; authenticated benchmark RPCs validate the existing byte pattern for JSON and negotiated binary/bulk traffic. Gallery benchmarks retain production gallery RPCs while preventing competing OTA/photo/voice operations. No new fields or firmware changes are introduced. Desktop-only fullscreen reading does not change the device date, page, mode or rotation settings.

Mac 已接入既有 USB Wi-Fi 管理与测速指令，校验身份、版本与网络范围；测速数据遵循现有字节模式和二进制协商。图库测速允许生产图库 RPC，阻止并发升级、照片和语音；没有新增协议字段或固件修改。电脑全屏阅读不改变设备日期、页面、模式或轮播设置。

Existing firmware's USB `tab5_benchmark` reply omits `version`. After the existing serial identity and host authentication, Mac accepts that one legacy reply only with the exact device/type, absent version and a bounded string result. An explicit invalid version (including null/string/boolean), a different device/type or a missing/oversized legacy result is rejected. Other management replies still require numeric `version:1`. This is receiver compatibility with the existing Windows/device contract, not a new wire format or an authentication change.

既有固件的 USB `tab5_benchmark` 回包未带 `version`。Mac 保留每次命令前的串口身份与主机认证，仅对设备／类型精确匹配、版本字段确实缺省且结果为有界字符串的这一类旧回包兼容；显式错误版本（含 null／字符串／布尔值）、不同身份／类型、缺失或超长结果仍拒绝。其余管理回包继续要求数值 `version:1`。这是兼容 Windows／设备既有行为，没有新增线上格式或改变鉴权。

During either RAM or gallery benchmarking, Mac keeps ordinary authenticated status frames and benchmark RPCs available. Image chunks are omitted from status and separate resource batches are paused; existing configuration/OTA/photo/voice gates remain. A BLE resource turn encountering the temporary busy gate defers without dropping its authenticated connection. Completion/cancellation releases the gate and normal resource synchronization resumes.

通信／图库测速期间，Mac 保留正常认证状态帧和测速 RPC，状态不夹带图片，独立资源批次暂停；配置／升级／照片／语音的既有互斥保持。BLE 资源轮遇到暂时 busy 仅退让，不丢弃已认证连接；结束／取消后恢复正常资源同步。

Mac BLE standby follows the existing USB acknowledgement's `active` field, as on Windows: only a fresh, authenticated USB connection reporting `active: "USB"` defers BLE state/voice polling. USB control remains usable while the device selects BLE or Wi-Fi. Missing/invalid active information, disconnect, authentication rejection or an expired acknowledgement cannot retain USB display ownership. No wire fields or device settings change.

Mac 蓝牙待命与 Windows 一样遵循既有 USB 回执的 `active` 字段：只有已认证且未过期的 USB 回执明确报告 `USB` 活动通道时，才暂停蓝牙状态／语音轮询。设备选择 BLE 或 Wi-Fi 时仍可保留 USB 管理通道；活动字段缺失／无效、断线、认证拒绝或回执过期均不保留 USB 显示归属。不新增协议字段或修改设备设置。

Mac's general BLE RPC receiver also uses the existing type-3 notification grant after successful subscription. A valid `rpcNotifyWindow` offers 1..32 fragments; absent/invalid offers retain a valid legacy `mailboxWindow` of at most eight. Each bounded window requires exact ID, total and cursor, consumes only its granted fragments, and must finish before the next grant. A four-second local deadline, pairing generation and the original whole encrypted request checks remain; errors stop without operation replay. Rejected/unavailable subscriptions keep cursor-ACK reads. Voice retains its separate existing eight-fragment path and timing.

Mac 通用 BLE RPC 接收在订阅成功后使用既有命令 3 批量通知授权。有效 rpcNotifyWindow 最多 32 片，缺失／无效时仅沿用有效且不超过八片的 mailboxWindow。每个窗口校验精确 ID、总长及游标，只收已授权片数，完整结束后才开下一窗口；四秒本地时限、配对代次和整个加密请求校验保持，异常停止、不重放操作。订阅不支持或失败时保留逐片读／游标确认。语音仍使用独立原八片窗口与现有时序。

The general RPC subscription uses the same bounded recovery as voice: only ATT insufficient-authorization followed by an exact enabled CCCD readback permits one standard CoreBluetooth reconciliation. Readback alone never enables notification grants; a successful subscription callback is mandatory. Other errors, disabled/invalid descriptors and repeated failures retain the legacy path. Local diagnostics expose only bounded capability, subscription and numeric error metadata.

通用 RPC 订阅沿用语音已实现的有界恢复：仅 ATT 授权不足且只读回查 CCCD 精确为启用时，允许一次标准 CoreBluetooth 协调；回查本身不能启用通知授权，仍须成功订阅回调。其他错误、禁用／无效描述符或再次失败均保留旧路径。本机诊断只展示有界能力、订阅状态及数值错误，不含正文或身份。


r70 physical follow-up: system-stream Tab did not expose the password page. A visible password field still permits successful unlock, including after display wake. There is no new protocol capability: device BLE HID enumeration does not mean mixed Wi-Fi/BLE preparation is supported. Existing `lockScreenCover`/`displayState` unknown values remain null; no Windows-style wake authorization may be inferred from them. Full-system sleep wake remains unimplemented.

r70 实测补充：系统输入队列 Tab 未能显示密码页；已显示密码框时仍可解锁，包括熄屏唤醒后。未新增协议能力，枚举到设备 BLE HID 不等于已支持 Wi-Fi／BLE 协同准备。`lockScreenCover`／`displayState` 未知仍为 null，不得据此授权 Windows 风格的准备按键；整机睡眠唤醒仍未实现。


r71 is a local preparation-only diagnostic, not a new capability or protocol. It retains authenticated Wi-Fi begin/poll/cancel, existing progress stages and terminal failure; no credentials or key commands reach TAB5. A short existing Apple loopback session sends one non-secret Tab and closes before any password-field write; no login submission occurs. There is no BLE/USB preparation or firmware change. Local diagnostic wording distinguishes this intentional incomplete result from a claimed unlock.

r71 仅为本地准备诊断，不新增协议能力。仍使用已认证 Wi-Fi begin／poll／cancel、原进度阶段及终态失败，不向 TAB5 下发凭据或按键命令。已有 Apple 本机短连接只发送一次非秘密 Tab，在任何密码框写入之前断开且不提交登录；不使用 BLE／USB 准备，不改固件。本机诊断明确说明预期未完成，不报解锁成功。


### r72 当前范围 / Current scope

r71 本机认证、Tab 发送及断开后观察均完成，但用户确认密码框未出现，准备方案未通过。r72 已移除该诊断和无效的系统 Tab 准备，恢复有既往实测成功记录的 AX 单次填入／按钮提交路径；需先在 Mac 显示空白密码框。仍只使用 Wi-Fi，请求与认证协议不变，设备不接收密码，不改全局系统设置。此恢复不代表自动密码页、整机睡眠唤醒或 Computer Use 状态误报已修复。当前不安排重复实测。

r71 authenticated, sent Tab and completed post-close observation, but the user saw no password field. r72 removes that probe and ineffective system-Tab preparation, restoring the previously successful single AX write/button submission path; a blank password field must first be shown on the Mac. Wi-Fi-only request/authentication semantics are unchanged, credentials stay local and global settings stay untouched. This restoration does not fix automatic page reveal, full-system wake or Computer Use state errors. No repetitive hardware trial is scheduled.


### r73 单次字符诊断 / One-use character probe

普通 Wi-Fi 解锁保留 r72 路径。仅本机“下一次仅测试字符触发…”按钮可准备两分钟有效的当前配对诊断；下一次已认证 begin 只发固定公开字符 a，不用密码、不按回车、不删除，用户自行清空。过期时该请求拒绝，不自动改为密码输入；重启不保留。没有新协议字段／设备能力，仍返回原有阶段和未完成结果。该实验检验可打印键与 Tab 的差异，不声称已解决自动密码页、Computer Use 或整机唤醒。没有驱动安装或全局设置改动。

Ordinary Wi-Fi unlock retains r72. Only the local one-use probe button arms a current-pair diagnostic for two minutes. One authenticated begin sends fixed public a without password, Return or deletion; the user clears it. Expired probes reject rather than entering credentials, and arms do not survive restart. No new protocol fields/capabilities; existing progress/incomplete responses remain. This tests printable input versus Tab and does not claim page reveal, Computer Use or full-system wake fixes. No driver installation or global settings change.


Mac r74 local implementation note: r73's public-character preparation was physically observed to reveal the password page. Ordinary Wi-Fi unlock now prepares with one fixed public character, then requires same-window login-button readiness (and exact zero-to-one counts when supported) before the retained single full-field password replacement/button submission. Event posting is not acknowledgement. The new local preparation stage maps to existing `preparing`; credentials, sample text and field observations are never added to device messages. The one-use local probe remains separate. No wire/capability/authentication/lease changes, no RFB or global policy changes; complete unlock acceptance remains pending.

Mac r74 本地实现补记：r73 公开字符已实测唤出密码页。普通 Wi-Fi 解锁先发一个固定公开字符，确认同一窗口登录按钮就绪（有计数接口时还要求零到一）后，才整体替换密码字段并提交一次。投递本身不算确认，新本地阶段映射既有 `preparing`；不向设备增加密码、样本文字或字段观察数据。一次性本地诊断入口独立保留；网络字段、能力、鉴权和租约不变，不连接 RFB 或改全局策略。完整解锁仍待实测。


r74 acceptance update: one ordinary Wi-Fi request without manual password-page preparation returned to the desktop and remained unlocked for 30 seconds, confirmed by the user and local completion receipt. Actual system-sleep wake, explicit display-off start and Computer Use are not covered by this result. No protocol changes.

r74 验收补记：一次普通 Wi-Fi 请求在无需手动准备密码页的情况下回到桌面，30 秒未回锁，用户与本地完成回执均确认。该结果不覆盖真正整机睡眠、明确黑屏起点或 Computer Use；协议不变。


r74 physical acceptance clarification: the user's latest correction confirms successful wake and unlock from display-off as well. The corrected no-wake answer is not a failure. This does not validate actual system-sleep wake or Computer Use behavior; no protocol or system-policy changes.

r74 实测澄清：用户最新更正确认黑屏后唤醒并解锁也成功；被更正的未亮屏回复不作为失败。真正整机睡眠与 Computer Use 尚未验证，不改协议或系统策略。


Mac r76 local-state refinement (unpublished): an independently observed active local protection cover may keep ordinary computer-control status locked=true even when the automation session emits an unlocked notification. Incomplete protection evidence cannot report false. This display evidence never authorizes credential entry or a successful unlock result; the Mac local backend stops while protection remains active/unknown. Existing nullable locked, wifi-unlock fields, pairing/lease/sequence rules and unsupported Mac USB/BLE PIN behavior are unchanged. No firmware/Windows schema change. Physical verification remains pending.

Mac r76 本机状态候选（未发布）：独立观察到活跃外层保护时，常规电脑控制可继续返回 locked=true，即使自动化会话已发出未锁通知；保护证据不完整时不返回 false。这类显示证据不授权凭据输入或成功回执，保护存在／未知时本机后端停止。既有可空 locked、Wi-Fi 解锁字段、配对／租约／顺序规则及 Mac 不支持 USB／BLE PIN 的边界不变，无固件／Windows 协议结构变更。实机验证仍待完成。


### Mac 旧会话草稿保全 / Mac orphan draft preservation

Mac 本地候选允许已配对并启用 Codex 草稿同步的设备使用现有 `draft-save` 保存目录外的有效 UUID 草稿；场景为切换电脑或旧会话退出近期目录。内容仍按设备／任务绑定加密保存；新目录外草稿在该设备已有 128 条草稿时拒绝新增，已有草稿可继续保存。200 回执仅在实际持久化成功后返回，文本仍限 2000 UTF-8 字节，图片仍核对已有任务／设备所有权，失败保留设备未同步状态。`draft-load`、公开回复、导航、receipt、send/steer/interrupt 的目录和授权规则保持。没有新增字段或操作，也不请求固件绕过 OTA 草稿保护；Windows 的既有未知任务拒绝行为保持。

The local Mac adapter accepts an existing authenticated `draft-save` for a well-formed task UUID outside its current catalog, preserving drafts retained after host/catalog changes. Storage stays encrypted and device/task scoped. New orphan entries stop at 128 existing drafts for that device; existing entries remain writable. Acknowledgement follows durable storage, with the existing 2000-byte text bound and image ownership checks. Loading, reading, navigation, receipts and mutations retain their catalog/authorization rules. This introduces no wire field or command and does not bypass firmware OTA protection. Windows retains its existing unknown-task rejection.


### TAB5 设备预览镜像 / TAB5 device preview mirror (source-only, unpublished)

ESP8266 原有预览与协议保持。TAB5 在认证状态的 `connectionHealth.screenPreviewCommand` 接收可选 `{version:1,streamId,requestId,transport,expiresAt,width,height,encoding:"rgb565-rle1"}`。两个 ID 为 32 位小写十六进制，时间为主机 epoch 毫秒；USB／Wi-Fi 请求 640×360，BLE 请求 320×180。命令绑定当前配对设备、电脑、session 和通道，一次仅取一帧，最长 30 秒；查看窗口每秒续租，主机 5 秒未续租即清除命令和画面。关闭、停用、维护或更换设备／电脑后拒绝旧帧。旧固件忽略新可选字段，协议主版本仍为 1。

设备先发送现有认证 RPC `{kind:"screen-preview",op:"begin",streamId,requestId,session,issuedAt}`；主机返回 200 和 `accepted:true` 后才采集。真实 LCD RGB565 缓冲只读缩采样，不改变页面、展示设置或相机所有权，不建立额外全屏缓冲。采样与相机切换串行；DMA 更新期间跳过。原始缩略图、编码、任务栈使用 PSRAM，失败不回退耗用内部大块内存；录音、照片处理和 OTA 优先。每块检查原电脑归属、连接和截止时间。

`op:"frame"` 额外带 `width,height,encoding,size,sha256,offset` 与现有 T5R1/T5R2 原始数据（JSON `data` base64 兼容）。编码最多 524288 字节；主机块上限 4096 字节，BLE 设备发 1024 字节。`rgb565-rle1` 是小端 RGB565 字 PackBits：小端 16 位控制字低 15 位为 1..32767 像素数，高位为 1 时紧跟一个重复像素，为 0 时紧跟指定数量的字面像素。禁止零计数、越界、缺像素及尾随字节。`sha256` 是完整编码的小写 SHA-256。按顺序接收，完全相同的已收前缀可重试；返回 `nextOffset` 及 `complete`。仅在长度、摘要、解码像素总数全部匹配后原子显示；损坏或未完整帧不替换最近成功画面。旧画面明确标记未更新。

`op:"error"` 仅接收固定类别 `busy/memory/capture/encoding/connection`。原有 HMAC、AES-GCM、session、时间、nonce 去重均先于镜像分派；不增加未认证端口。帧只在内存中，不进入照片附件、磁盘缓存或日志。关闭后停止后续请求，已开始的有限采样／网络步骤在下一次回执拒绝时结束，不承诺中途强制停止一次显示内存拷贝。`.267` 未刷写，不支持此扩展；源码和对象编译通过不代表实体镜像通过。

The ESP8266 preview and protocol remain unchanged. The optional authenticated `connectionHealth.screenPreviewCommand` carries the fields above without changing protocol version 1. IDs are 32 lowercase hex characters; expiry is host epoch milliseconds. A visible viewer renews a 5-second lease and requests one frame at a time, with a 30-second request bound. USB/Wi-Fi use 640×360, BLE 320×180. Pairing, host, session, transport, request and lease must match; close, disable, maintenance or identity changes reject old frames.

An authenticated `screen-preview/begin` must receive an active-viewer acknowledgement before read-only sampling from the actual LCD buffer. Sampling serializes with camera buffer selection and skips unstable DMA state. Only reduced pixels, bounded encoding and the worker stack use PSRAM; voice, photos and OTA take priority. Every upload chunk rechecks host ownership, link and deadline. `frame` uses existing binary RPC or base64, an encoded size cap of 524288 bytes, SHA-256, sequential offsets and at most 4096 bytes per chunk (firmware BLE: 1024). The lossless little-endian RGB565 word codec uses a 16-bit control: low 15 bits count 1..32767, high bit repeat-one-word versus literal words. Exact bytes and pixel count are required. Only complete hash-verified decoded frames replace the displayed image; identical received-prefix retries are idempotent. Fixed error categories carry no screen content. Existing authentication and replay checks precede dispatch. Frames remain memory-only, outside attachments, logs and caches. Closing prevents further requests and rejects subsequent chunks; bounded in-flight sampling/network work may finish. Existing `.267` ignores this extension; hardware acceptance awaits a combined firmware image, which has not been generated.

### TAB5 preview extension v2 / 预览扩展第二版

预览子协议`version:2`仅在桥接确认固件为`0.2.270-ui`或后续兼容版本后协商；总协议版本仍为1，旧固件继续使用v1。USB／Wi-Fi请求1280×720的`encoding:"jpeg"`，BLE保留320×180的`rgb565-rle1`。设备在`begin`携带实际`width,height,encoding`；JPEG仅可按当前横竖方向将请求的1280×720转为720×1280。桥接确认该几何后才采集，帧必须精确匹配确认的尺寸和编码。JPEG仍受524288字节、顺序分片、SHA-256和完整帧原子替换限制；解码前核验JPEG头部尺寸，最大921600像素。使用项目既有RGB565逐行软件JPEG编码器，quality85／YUV420；不新建全屏RGB888副本。

设备对最近一次**完整确认**的帧只保留流身份、几何和摘要。采样像素未变时，以`op:"unchanged"`回传`width,height,encoding,sha256`，不带像素；`sha256`必须匹配同一流最近完整帧的编码摘要。桥接仅在已有匹配帧时更新新鲜度，保留同一像素对象；没有旧帧、摘要或尺寸不符均返回409，设备清除摘要并在下一请求传完整帧。未变化时约两秒后再检查，不反复编码和上传。帧和摘要不进入附件、磁盘缓存或诊断日志。

Mac／Windows均在有效查看租约内保留最近完整画面，后台分片不反复切换进度提示或重绘相同像素。高清 JPEG 在首个数据片之前允许15秒无进展；传输开始后USB／Wi-Fi连续5秒、BLE连续10秒无进展时放弃该请求，约1秒后发布新请求，旧请求随后被拒；全请求期限仍不超过30秒。发布到状态快照的`expiresAt`取原期限与该快照epoch加30秒的较小值，避免新请求搭载稍旧快照时被设备上界检查拒绝。关闭或身份／租约失效仍立即撤销后续接收。语音、照片和OTA优先。

Preview subprotocol v2 is negotiated only for firmware270 or a later compatible version; the main protocol stays version1 and older devices retain preview v1. USB/Wi-Fi request JPEG at1280×720, while BLE retains320×180 lossless RLE. A v2 `begin` declares the actual encoding and dimensions; JPEG may transpose the requested panel geometry to720×1280 before sampling. The host validates that geometry before capture. The existing524288-byte, ordered-chunk, complete-SHA and atomic-display bounds remain. JPEG dimensions are validated before raster allocation, with at most921600 pixels. The existing quality85/YUV420 scanline encoder avoids another full RGB888 buffer.

For a fully acknowledged frame, the device retains only stream/geometry/digest metadata. Unchanged pixels produce an authenticated `unchanged` receipt carrying the existing encoded-frame SHA and no pixel bytes. The host requires its matching last frame, refreshes freshness and retains the same pixel object;409 clears the device's digest so the next request sends a complete frame. Unchanged frames are checked about every two seconds. Images and hashes remain outside attachments, disk caches and diagnostic logs. Under an active lease, repeated pixels and background chunk progress do not redraw the image. HD JPEG allows fifteen seconds without progress before its first payload; after payload begins, five seconds without USB/Wi-Fi progress or ten seconds on BLE abandons a request; a fresh request follows after about one second, retaining the30-second total bound. Snapshot publication narrows expiry to the earlier of the original deadline and snapshot epoch plus30seconds. Close/identity/lease invalidation and voice/photo/OTA priority remain enforced.


内存受限时，两端桥接在收到当前请求的`memory`错误后，使用现有v2格式依次请求640×360、320×180的`rgb565-rle1`，并明确标注标准画质／小图预览；至少完成3帧且冷却30秒后自动重试高清；再次内存失败将冷却加倍至最多300秒，高清成功重置为30秒。重试时保留上一完整画面，BLE保持小图。错误帧不覆盖已有完整画面，也不改变用户展示设置。旧v1请求不自动改变尺寸。

On a current v2 request's authenticated `memory` error, both hosts retry with existing640×360 then320×180 RLE geometry, explicitly labeling standard/small quality. After at least three successful fallback frames and a 30-second cooldown, USB/Wi-Fi retry HD. Repeated memory failures double the cooldown up to 300 seconds; successful HD resets it to 30. Retain the last complete frame during retry; BLE remains small. Error responses preserve the previous complete frame and user display settings. Legacy v1 requests retain their original geometry.


Mac USB在语音传输优先期间，先完成既有电脑归属认证再处理RPC，随后维护正常状态与既有完整状态保活；空闲时保留原状态→RPC顺序。状态包只投影资源元数据，独立资源片也暂停，同一轮RPC开始录音后再次检查优先状态。优先级持续到当前voiceId及nonce对应的终态全部分片获USB确认，旧take／迟到回包不能释放；最后有效设备请求后30秒有界释放，禁用立即清除，原voice.busy语义不改。期间后台Codex read／read-ack返回现有503忙状态；read-ack仍须核验当前设备、task、turn、有效receipt及最新turn。恢复后不前移被暂停的资源偏移。认证或串口失败沿用原中断／重连，不重放请求，不增加协议字段，也不放宽8秒语音请求截止线。后续源码在检查取消和原截止之后，优先交付已准备好的RPC结果，再决定是否等待状态保活；仍在执行的请求保持原2秒保活。

During voice transport priority, Mac USB performs the existing host claim and RPC before ordinary status, retaining status/full-state ownership maintenance; idle ordering stays status then RPC. Inline image bytes and standalone chunks defer, rechecking priority after a start in the same step. Priority lasts through matching current-voice/nonce terminal fragment acknowledgements; old/late replies cannot release a newer take. It expires within30seconds of the last valid peer request or immediately on disable, without changing voice.busy. Background Codex read/read-ack yields existing503 busy responses; receipts still require device/task/turn/valid-receipt/latest-turn validation. Paused resource offsets do not advance. Authentication/serial failures retain disconnect/reconnect without replay, new fields or a longer eight-second voice deadline. The source-only follow-up delivers a ready RPC result before due status maintenance, after cancellation/deadline checks; pending operations retain two-second ownership maintenance.

### USB 回执容量保全 / USB reply capacity preservation (source-only)

USB hello／完整状态ACK沿用原4KiB行界限及字段。当可选诊断计数无法全部容纳，或追加会遗漏主机认证字段时，设备改发精简回执：保留同一type／deviceId／firmware／sequence、RPC与USB能力、通道状态、资产确认、hostBindingVersion／hostNonce／currentHostId／usbHostAuthenticated及HID／解锁失败回执。精简回包不含uptimeMs，Mac沿用已有完整诊断缓存，不把省略当作零值；Windows既有4KiB文本解析器无需扩大界限。接受状态、序号验证、租约、认证及断线处理保持。实际格式化器的边界构造检查与两端解析器隔离测试不代替实体USB验收。

USB hello/full-status acknowledgements retain their existing4KiB line bound and schema. If optional diagnostics do not fit or would omit host-binding fields, an essential reply retains the same type/device/firmware/sequence, RPC/USB capabilities, channel state, asset acknowledgements, binding nonce/identity/authentication and HID/unlock-failure receipts. It omits uptimeMs, so Mac retains its last complete diagnostic sample; omitted counters are not zeros. The existing Windows4KiB text parser needs no larger limit. State acceptance, sequence validation, leases, authentication and disconnect handling stay unchanged. Actual-formatter boundary tests and isolated host-parser checks are not physical USB acceptance.


### Mac recording paragraph and USB ownership maintenance / Mac 录音单段与 USB 归属维护

2026-10-25 本地桥接后续：本地识别的停顿分段和识别结果内部行分隔统一拼为单段，保留英文词边界所需空格；音频区间纠正／重复合并规则与设备草稿插入保持不变，用户手工换行不受影响。对当前身份匹配、usbDelta=1且规范版本为0.2.269-ui或更高0.2.x-ui的已检查接收器，Mac的原2秒归属维护允许发送既有状态增量；首次／基线失效仍完整状态，旧版和未知版本仍完整续期。仅精确序号ACK推进基线和维护时刻，拒收回退／重连／8秒归属与语音边界不变，不新增字段或修改Windows／设备协议。本地超时诊断仅保留固定阶段、字节数、有界读写毫秒及空读／EAGAIN次数，不记录串口内容或任意错误字符串。实机结果单独登记。

Local Mac follow-up: recognition pauses and internal line separators produce one paragraph, preserving necessary English word boundaries. Audio-range correction/repetition and insertion into manually edited drafts remain unchanged. Existing two-second USB ownership maintenance may use the existing status delta only for an identity-matched usbDelta=1 receiver with canonical inspected version0.2.269-ui or later0.2.x-ui. Initial/invalid baselines remain full, as do old/unknown receivers. Exact sequence acknowledgements alone advance baselines/maintenance; rejection fallback, reconnects and eight-second ownership/voice limits remain. No wire fields or Windows/receiver protocol change. Local timeout diagnostics contain fixed phases, byte counts, bounded read/write milliseconds and zero-read/EAGAIN counts, without serial contents or arbitrary error text. Physical acceptance is recorded separately.

### Preview encoding progress and diagnostic scheduling / 预览编码进展与诊断调度

.273 的 JPEG 编码期间约每秒重复既有认证 `begin`，不新增操作、字段或会话。相同请求只更新进展时间，不重置接收偏移、不延长原30秒截止；关闭／失效回执终止后续传输。像素输出从128KiB起按需倍增至原512KiB上限。仅在UI渲染间隙回收未发布 staging，手势进行中不回收，已发布可见快照、照片和草稿保持。普通完成帧间隔约1秒，未变帧保持约2秒；桥接不因相同像素或状态文本重复绘制。

周期诊断不再调用整堆最大块扫描：既有相关数值字段使用0表示未采样，不能把0解释为内存耗尽。RPC最小连续块保持既有未知值。一次性的实际分配准入检查保留。无线任务每轮及每个至多2048字节HTTP正文片让出1 tick，正文读取受3秒总期限及电脑归属变化约束；仅完整、认证且归属有效的响应更新缓存。HTTP连接及头部仍使用各自SDK超时，不承诺整个请求总计3秒。

Firmware273 repeats the existing authenticated `begin` about once per second during JPEG encoding, with no new operation, fields or session. An identical request updates progress without resetting offsets or extending its original30-second deadline. Revocation rejects further transfer. Encoded output starts at128KiB and doubles only on actual capacity exhaustion up to the existing512KiB cap. UI-thread reclamation releases unpublished staging between render passes, refusing during gestures and preserving published visible snapshots, photos and drafts. Completed frames are spaced about one second; unchanged frames retain two seconds. Identical pixels/status do not redraw.

Periodic diagnostics no longer traverse the whole heap for its largest block: existing zero values mean not sampled, never exhausted memory; RPC minimum-largest keeps its existing unknown sentinel. Actual one-shot allocation admission remains. The wireless worker yields one tick per iteration and at most2048-byte HTTP body chunk; body reads have a three-second total budget and host-ownership checks. Only complete authenticated valid-owner responses update state. Connect/header SDK timeouts remain separate, so three seconds is not an end-to-end request guarantee.

### Preview stack observation / 预览任务栈观察

.274 在既有 `displayDiag` 字符串末尾增加可选 `;sp,1,busy,finishedJobs,minStackBytes,lastMemoryPhase`，前面的所有字段保持原义。`busy`为0/1；任务结束后累计 `finishedJobs`；`minStackBytes`为本次启动已完成预览任务的最小剩余栈字节数，0表示尚无样本；`lastMemoryPhase`为最近任务的内存失败阶段：0无、1原始像素、2编码输出、3JPEG初始化／内部内存。它只含数值，没有像素、识别文本或身份信息。成功或非内存失败的后续任务会将lastMemoryPhase重置为0；该字段记录最近完成任务，不是保留至重启的故障记录，不能用后续0否定此前内存失败。Mac沿用有界ASCII诊断字符串校验与显示，Windows沿用原字符串显示；旧桥接可忽略新后缀。任务改用16KiB PSRAM栈和PSRAM中的录音状态对象；30秒期限、鉴权、上传和完整帧语义不变。

Firmware274 appends the optional `;sp,1,busy,finishedJobs,minStackBytes,lastMemoryPhase` suffix to existing `displayDiag` without changing earlier fields. Completed workers update their count and minimum remaining stack bytes; zero stack means unsampled. Last memory phase is0 none,1 raw pixels,2 output,3 JPEG initialization/internal allocation. Only numeric observations are added. A later successful or non-memory-failing job resets the phase to0; it describes the latest completed job, not a sticky crash/error record. A post-success0 cannot rule out an earlier memory failure. Mac's bounded ASCII validation/display and Windows' existing raw-string display already accept the suffix; old hosts can ignore it. The worker uses a16KiB PSRAM stack and a PSRAM-owned voice-state object. Authentication,30-second expiry, transfer and complete-frame rules remain unchanged.


### Preview unpublished admission / 预览未发布请求准入

.275将CDC/GATT mailbox在发布前遇忙的本地结果区分为`RPC_STATUS_UNPUBLISHED_BUSY`（-1011）。该结果仅由尚未复制请求、分配新active serial的准入分支产生；它不是新线协议字段或主机回执。预览worker仅对此结果在原请求截止、电脑归属及通道仍有效时有界重试，等待期间检查语音／照片／升级优先。已发布请求的超时、断线、归属变化或不确定回执不重放；接收端仍仅接受当前stream/request的完整校验帧。原100ms单次准入、3s单次交换、30s预览请求和既有租约不延长。Mac／Windows接收器和原ESP8266预览无行为变更。

Firmware275 distinguishes locally unpublished CDC/GATT admission contention as`RPC_STATUS_UNPUBLISHED_BUSY`(-1011), set only before request bytes or a new active serial are published. It is not a new wire field or host acknowledgement. The preview worker retries only this result while its original deadline, owner and link hold, with foreground voice/photo/OTA preemption. Published timeouts, disconnects, owner changes and uncertain responses are never replayed. Existing100ms per-attempt admission,3s exchange,30s preview request and viewer lease bounds are unchanged. Mac/Windows receivers and ESP8266 preview remain unchanged.


### Lease-scoped capture scratch / 按预览租约复用采样缓冲

.276在慢begin之前预留JPEG的1843200字节RGB565采样缓冲，并在成功、归属和期限有效且前台未抢占的任务之间复用。每次请求仍须在authenticated begin接受后重新完整采样；缓冲复用不等于复用可发布画面。worker期间的所有权由原running原子准入保护，UI只能回收闲置缓冲；任务完成后空闲5秒自动释放，录音／相机／升级入口可主动回收，前台忙时worker不保留。关闭后不再产生新请求；既有有界在途任务结束，闲置scratch按上述期限回收。拒绝或不确定回执不保留scratch、不重放请求。像素仍仅在内存，不进入磁盘、照片附件或诊断。已有JPEG/RLE、SHA、尺寸、30秒期限和主机5秒续租语义不变，两端接收器兼容。

Firmware276 reserves the1843200-byte RGB565 JPEG scratch before slow begin admission and reuses it only after successful replies with valid owner/deadline and no foreground preemption. Every request still requires a newly complete capture after authenticated begin; scratch reuse never authorizes stale pixels. Existing atomic worker admission protects ownership: UI evicts idle scratch only, automatically after5 idle seconds or proactively for voice/camera/OTA. Active workers release on foreground priority. Closing stops new requests; bounded in-flight work and subsequent idle reclamation remain. Rejected/uncertain replies neither retain scratch nor replay. Pixels remain RAM-only, outside disk, attachments and diagnostics. JPEG/RLE/hash/geometry/30-second request/5-second viewer lease semantics are unchanged; both host receivers remain compatible.


### Mac preview pacing / Mac 预览调度

Mac本地R22沿用原USB CDC及v2预览格式。当前USB预览产生新requestId时，下轮状态发送可提前，不等普通状态周期；只有实际发送的完整帧获得精确设备／序号ACK，才记为已发布。录音优先时不为预览加急。USB仍单队列，每轮最多连续处理4个RPC／60ms；遇空／已完成邮箱不重放，并让位到期状态、录音、配置、停用及归属变化。

仅在当前USB预览有效、无录音或前台配置占用且最近250ms确有RPC完成时，邮箱检查从100ms缩为10ms；空／重复读取不续期，编码等待、关闭和重连回到100ms。每笔交换仍保留原请求期限、认证和2秒归属维护，串口仍460800。非BLE完整帧完成后的主机间隔为250ms，窗口每250ms检查已完整校验的图像；BLE仍为原1秒间隔，unchanged仍为2秒冷却。相同像素不重建显示图像，关闭仍清除内存画面和租约。此为Mac实现节奏，不新增协议字段或要求Windows采用同一队列；设备尺寸、编码、SHA、截止与前台优先规则不变。

Local Mac R22 preserves CDC and preview v2. A new USB preview request may advance the next status send; only the actual whole frame's exact device/sequence ACK marks its request published. Voice priority suppresses preview urgency. One serial queue drains at most4 RPCs/60ms, yields to status/foreground/ownership changes and never replays empty/completed mailbox IDs. Polling is10ms only within250ms of a real RPC completion for an active USB viewer without foreground voice/configuration; empty reads cannot extend it, while encoding/idle/close/reconnect retain100ms. Existing per-operation deadlines, authentication, two-second ownership maintenance and460800baud remain. Non-BLE completed frames wait250ms, viewer checks250ms, BLE completion waits1s and unchanged cooldown stays2s. Identical pixels reuse the image object; close revokes the lease and image. No new wire fields or Windows scheduling requirement; geometry, codec, SHA and foreground rules stay unchanged.


### Responsive capture scheduling / 预览采样调度

.277设备仅对预览原始像素和编码帧的SHA校验使用专用调度入口；SHA算法、完整输入、摘要及1024字节内部DMA安全上下文／暂存缓冲不变。最多处理16KiB，或处理约1ms后（单次1KiB更新结束时检查）让出并核对原期限、电脑归属以及录音／相机／照片／升级优先。取消或校验错误清零摘要并释放内部工作区，不发布部分帧。照片上传和普通RPC的加密／SHA仍保持原来每1KiB让出。全尺寸LCD采样省去比例不变时的逐像素64位除法；旋转／缩采样映射逐点与旧实现相同，不更改显示缓冲写入或相机所有权。

配套Mac R23仅在已确认的兼容.277及以后固件上，将非BLE完整帧／unchanged后等待缩为100ms；窗口100ms检查完整帧，未完成请求不被下一次刷新替换，同像素仍复用显示图像。旧固件保留R22周期，BLE保留原1秒／2秒冷却。原v2帧格式、1280×720、JPEG质量85、512KiB上限、SHA及30秒截止保持。Windows旧接收器兼容共同设备改动，其主机刷新策略可按同一已确认版本门槛单独优化；不是新的线协议版本。

Firmware277 adds preview-only SHA scheduling while retaining the complete SHA input/digest and1024-byte internal DMA-safe work/staging. At most16KiB, or approximately1ms checked after each1KiB update, precedes a yield and original deadline/owner/foreground checks. Cancellation/errors clear the digest and release workspace without publishing partial frames. Photo and ordinary RPC encryption/SHA keep their original1KiB pacing. Full-resolution sampling omits redundant64-bit scale division with bit-identical rotation/downsample mapping and unchanged framebuffer/camera ownership. R23 enables100ms completion/unchanged waits only for confirmed compatible277+ non-BLE capture; its100ms viewer never replaces an in-flight request or redraws identical pixels. Legacy/BLE cooldowns and v2 geometry/quality/limits/SHA/deadlines remain. Windows remains wire-compatible and can independently gate its faster host policy by confirmed firmware.


### Mac Wi-Fi packed status parity / Mac Wi-Fi 压缩状态补齐

Mac R24补齐已定义的Wi-Fi `tab5_packed` 状态格式：仅在本次GET已通过设备／nonce／HMAC鉴权，且同nonce下的固件证明有效、规范版本为0.2.42-ui或更新0.2.x-ui时启用。完整含元数据的zlib/base64封装更小才替代原文，再使用原nonce／tag／ciphertext和challenge进行AES-GCM加密；没有证明、旧版本、未知版本及不节省的内容保留原格式。解压大小仍不超过Mac原32740字节，设备精确解压／完整输入／非嵌套及全部帧鉴权规则保持。USB／BLE格式和鉴权不改，Windows既有实现已支持同一Wi-Fi协商格式。现场约26KB回包降至约11KB后有效Wi-Fi回包恢复，用户确认正常；原失败的具体接收／解密／验帧阶段未唯一定位。

Mac R24 now uses the already-defined Wi-Fi packed status only after the current GET's device/nonce/HMAC authentication and a valid same-nonce canonical0.2.42-ui-or-later0.2.x-ui firmware proof. Only a smaller complete zlib/base64 envelope replaces raw state, before unchanged AES-GCM wrapping. Missing/old/unknown/non-beneficial cases remain raw; existing32740-byte expansion and exact, non-nested, complete-input receiver checks remain. Windows already supports this Wi-Fi format. Physical~26KB-to~11KB responses restore valid packet growth and the user confirms recovery, without uniquely proving the old receive/decrypt/frame failure stage.

### Mac upgrade command lifetime / Mac 升级命令时限

Mac R25将`otaCommand`和`bootVerifyCommand`的通告截止分别限制在实际承载状态帧epoch后45秒／15秒内，并且不晚于原请求截止。不能满足该窗口的帧不发布命令；不会通过新状态帧延长原请求、放宽接收器检查或自动重发。实际固件字节已经提供或设备已确认开始后，旧开始请求到期不再覆盖下载／等待启动核验状态。观察到目标版本仅表示版本在线，仍需原精确ELF、VALID分区与三个实时UI样本核验；后续版本采样不覆盖已通过的精确核验。设备拒绝原因只映射固定白名单提示，未知原始错误不进入UI。原120秒Wi-Fi提供租约、90秒RPC提供租约、30秒开始请求和10秒启动核验请求保持；无新协议字段。离线实际模型通过，电脑端开始的实机闭环须在下一次确有固件更新时核对，不为该测试重刷同版本。

R25 caps OTA/boot command advertisement to45s/15s relative to its actual status-frame epoch and the original request deadline; unusable frames omit the command. Status refresh cannot extend the request, relax receiver checks or retry automatically. Real image serving or a started receipt prevents an old start-request expiry from replacing download/verification status. A matching observed version is not exact boot acceptance; the existing ELF/VALID/three-live-UI-sample verification remains and later version observations preserve it. Only fixed known rejection labels enter UI. Original120s Wi-Fi/90s RPC serving leases and30s start/10s boot requests remain. Actual-model offline checks pass; desktop-start physical acceptance awaits the next legitimate update, without reflashing the same version for testing.

### TAB5 computer control admission during preview (278)

设备 .278 在 USB／BLE 邮箱发布电脑控制请求前最多等待 1 秒准入；只等待未发布阶段，不重复指令，不扩大原响应截止。电脑状态读取或显式操作已保留前台时，预览暂停下一段尚未发布的工作，并在相同 stream／request／归属及原期限内恢复。已发布分片的不确定回执不重放，录音／相机／升级优先级不变。无线 HTTP 和既有 version=1、screen-preview v2 线格式不变。

TAB5 .278 bounds pre-publication computer-control mailbox admission to1s on USB/BLE. Preview yields unpublished work while computer control is busy, then resumes with the same owner and request deadline. It neither replays uncertain published requests nor extends authorization; foreground voice/camera/update priorities and existing wire formats remain unchanged.
## TAB5 runtime diagnostics v1 (.250)

`connectionHealth.runtimeDiagnosticCommand` is optional and sent only to the currently selected paired device: `{version:1,requestId:<32 lowercase hex>,transport:"Wi-Fi"|"BLE",expiresAt:<epoch milliseconds>}`. The command lasts 10 seconds; the device requires a status received less than 6 seconds ago, a valid pcInput session, matching active transport, integral expiry and no more than 15 seconds future allowance. A started request is consumed once. No USB ownership or host selection is changed.

The device replies through the existing authenticated `/tab5/v1/rpc` or BLE RPC with `{kind:"runtime-diagnostics",requestId,session,issuedAt,snapshot}`. All existing paired-device HMAC, AES-GCM, session, time and nonce-replay checks precede dispatch. The snapshot contains `diagnosticVersion:1,deviceId,hostId,firmware,uptimeMs,sampleAgeMs,uiAgeMs,flushCount,flushErrors,lcdUnderruns`, plus numeric memory/health fields and existing bounded diagnostic strings (`displayDiag,cameraDiag,photoDiag,photoUploadDiag,...`). No photo/audio/conversation payloads or credentials. The bridge validates a pending matching device/host/request/channel/expiry, bounds the snapshot to 8192 characters and each diagnostic string to 1024. Retry age is preserved, duplicate completion is rejected, and failures keep the previous sample visibly stale. The Windows loopback-only `/diagnostics/tab5` entry coalesces refresh requests and retains its browser-origin restriction. Old hosts omit the command; old firmware is reported as USB-only for detailed diagnostics. Mac adoption uses this same contract; no Mac implementation change is included here.

中文：.250 新增按需无线运行诊断，复用已绑定当前电脑的加密状态与 RPC，不新增公开诊断端口，也不依赖固件升级。请求和回复必须匹配设备、电脑、会话、通道及限时请求 ID；切换电脑后中止旧代次发送。采样仅包含现有运行数字与诊断串。Windows 统一显示 USB/Wi-Fi/BLE 来源、接收时间和数据年龄，10 秒后标记过期，读取失败保留旧样本。旧 Mac 桥接需要按本节实现请求与接收，不能仅靠刷入固件获得该入口。


## Quota carry-over and diagnostic transport fallback (.251 candidate)

`quotaTrend.days[]` adds optional nonnegative `carryOver` (weekly quota percentage points, default zero). It is the portion of `used` recovered from a verified account/window interval crossing a Beijing midnight with a sampling gap over five minutes. It belongs to the resumed collection date under the requested attribution rule, is counted once, and marks the day partial. No-sample dates stay unknown. Confirmed resets start a new segment at the first observed usage; do not fill the preceding window to 100. Missing/changed identity, plan or unverified reset windows must not be bridged. Older hosts omit this field; older firmware may ignore it. No stored history schema or Mac source change is required.

中文：每天接续前一次有效周额度读数；超过五分钟的跨日差额标为“含跨日补记”，归入恢复采集当天，不进入完整日均。重置按可验证窗口分段，只计已观察增量。TAB5 .251–.253 在相应数字后显示 `*`；.254 起去掉数字旁星号及标题的跨日补记括号说明，但保留协议中的 carryOver 记录和累计口径。旧桥接缺省为零。

The Windows loopback diagnostic request permits at most two eight-second probes. If the first live wireless path times out, it tries the other currently live path to the SAME paired device and host with a fresh request ID and expiry. A standby Wi-Fi poll must not permanently hide a device forced to BLE. Reject late replies to superseded challenges. Only this loopback endpoint has a 20-second HTTP response budget; existing binding, session, replay and privacy rules remain unchanged.

`displayDiag` may append `;bw,1,active,leases,releases,appliedPriority,appliedReadQos`. These are numeric lease/readback counters for the temporary LCD memory-read priority during photo preparation, not a pass/fail proof of visible flicker. Unknown suffixes may be ignored.

## Windows quota evidence / TAB5 .253 candidate

The wire `quotaTrend` contract remains unchanged. Windows-only local daily diagnostics add `windowChanges`; a known same-account/plan, nondecreasing interval with two still-future changed deadlines retains the observed difference as a partial lower bound. Existing `carryOver` still denotes long cross-day attribution. A deadline change alone never establishes a reset or a card use. Local `resetCreditHistoryStatus{checkedAt,complete,error}` distinguishes successfully fetched empty history from unavailable/partial history; it says nothing about automatic-reset coverage.

Windows optionally reads `GET /wham/rate-limit-reset-credits/history` (`events[{id,kind,occurred_at}],next_cursor`) using existing account authentication. Read at most four pages in four seconds; only `kind=redeemed` is evidence of card use. Persist a hashed event ID and actual event time once under the matching account's successful observation, never a raw ID. The loopback quota diagnostic adds `resetCreditHistory[{redeemedAt,firstObservedAt,source:"provider"}]`. Events can resolve a hidden changed-window reset across a gap; missing/expired/granted credits do not. Official automatic/paid resets are absent from this history; observed resets with no source event stay unattributed. Existing records load without event metadata. Mac and device wire fields remain compatible; Mac acquisition logic is not changed.

TAB5 credential RPC waits at most 1000 ms for an occupied USB/BLE mailbox before publishing its single request. This consumes the original exchange deadline, checks host/mailbox generations, disconnect and host switching, and never retries a nonce, credential reply or HID input. Other RPC admission remains immediate. Existing authorization/session and six-second credential deadlines are unchanged.

中文：额度累计保留已知同账户/套餐在未来重置日期变化时的非负观测差额，单独标记待核实与跨日补记；明确兑换事件可核对断档内的卡片重置。官方自动重置只能按有效账户采样中的回落与周期变化分段，不能推断触发方。蓝牙/USB 取凭据仅在首次发布前限时等待被普通请求占用的信箱，沿用原总时限和电脑代次，不自动重发。


## Windows bridge OTA withdrawal during delivery (local candidate)

No new wire fields: existing authenticated OTA GET/range requests remain unchanged. Windows withdraws the exact offer and closes incomplete raw/compressed HTTP streams; later USB/BLE ranges receive `offer_changed`. Firmware retains the current boot partition through existing incomplete-download cleanup. A lifecycle lock excludes cancellation once the final range is released. Uncommitted intents can stop locally; late ready returns `request_expired`. Completed/rejected requests cannot be committed again. Mac and existing firmware remain compatible.

A committed transfer is stopped only after a new confirmation request started strictly after cancellation. Wireless must complete its unique authenticated runtime challenge; cached samples and pre-stop probes are insufficient. USB uses the exact post-stop status sequence plus matching `currentHostId` and `usbHostAuthenticated:true`. Both require the same bound device, original firmware and `otaDiag` busy=0. For ready commits without any actual data delivery, confirmation must start after the original command expiry plus a 6000 ms freshness grace; pre-install idle is not an abort acknowledgement. Disconnect/timeout never implies success or rearms the offer.

`otaDiag` retains four numbers: `prepareElapsedMs,bufferSlots,lcdUnderrunDelta,busy`, with busy 0 or 1. These are observations, not permission to cancel after the final byte. Generic health reports alone cannot confirm stopping.

中文：不增加线协议字段。桥接撤回 offer，终止未完成 HTTP 流，后续 USB/BLE 分段返回原有 `offer_changed`；固件清理备用分区并保留当前启动分区。最后段交出后拒绝取消。未 commit 的意图可本地撤销，已结束或 rejected 请求不可被晚到 ready 重提。

停止确认只能来自停止之后开始的唯一新请求：无线完成鉴权诊断挑战；USB 匹配新 status 精确 sequence、`currentHostId` 与 `usbHostAuthenticated:true`；缓存、旧 probe、普通 ACK 均不能确认。还需原设备/host/固件及 `otaDiag` busy=0。ready 已 commit 但无数据时等原命令到期并额外留 6000ms，避免误认尚未开始的空闲。断连/超时不能伪报成功或重新提供固件。`otaDiag` 四项为准备耗时毫秒、接收槽数、显示欠载增量、busy。仅修改 Windows，Mac 与旧固件兼容。

## TAB5 BLE photo recovery and diagnostics (.255 local candidate)

Authenticated v1 status adds optional top-level `data.imageChunkRetry:1`. Only exact numeric 1 from the selected host's authenticated BLE snapshot advertises idempotent `image-chunk` handling, including duplicate prefixes and the final saved-image result. This is not an authentication substitute. Missing/other values disable automatic recovery; old peers may omit/ignore it. The Windows candidate advertises it; Mac source is unchanged.

Only BLE `image-chunk` uses a 15000 ms exchange deadline. A transport failure without a valid reply (local status 0) may use at most one recovery opportunity per complete image, waiting up to 20000 ms for the same host generation, bridge session and connection mode. The recovered request sends exactly the same SHA-256/offset/bytes with a fresh nonce, retaining AES-GCM/HMAC/session/time/replay checks; never replay the nonce or encrypted packet. Host/session/mode changes, unrecovered security loss, explicit authentication/validation responses and exhausted budget end the upload. No automatic computer/transport switch or retries for Codex commands, credential/HID input, USB or Wi-Fi are added. The 15000 ms value is a chunk deadline, not a total-photo latency guarantee.

`photoUploadDiag` retains `channel,imageBytes,elapsedMs,status` for the latest upload and optionally appends `;f,failureCount,firstOffset,firstStatus,firstFailureMs,firstPhase;r,retryCount`. f/r counters are cumulative since firmware startup; first* retains the first recorded failure instead of being overwritten by later successes. firstPhase 1 means host/session/mode changed, 2 a failed chunk RPC, and 4 an invalid acknowledged offset. firstFailureMs is measured from that failed upload's chunk-loop start, not wall-clock time. There are no image bytes, filenames, conversation contents, credentials or host addresses. Receivers tolerate the old four-field prefix without suffixes.

`cameraDiag` retains 15 comma-separated values: `stage,error,internalFree,internalLargest,frames,previousStage,previousError,prepareMs,lockWaitMs,lockHoldMs,captureMs,presented,composeUs,presentUs,mode`. Mode adds 3 for static full-screen still hold; existing 0=ordinary rendering, 1=live direct viewport and 2=restore/fence fault remain. Ownership telemetry does not prove absence of visible flashing.

中文：可选 `data.imageChunkRetry=1` 仅从当前绑定电脑已鉴权 BLE 状态读取，缺失/其他值不恢复；不扩大鉴权、不改变协议版本。BLE 照片分块 15 秒超时，一张最多一次恢复机会，最多等 20 秒后用新 nonce 重发相同 SHA/offset/字节。身份/session/模式改变、明确错误或预算用尽即结束，不自动改连电脑/通道、不重发消息/凭据/HID，USB/Wi-Fi 保持原路径。诊断 f/r 自启动累计，first* 保留首次失败；15 秒不是整张总时限。相机 mode=3 表示静态整屏 hold，其他字段和编号兼容。所有字段不含照片/凭据，蓝闪和首次 BLE 上传的真机验收待安装。

## Same-boot OTA recovery (.257 local candidate)

Optional authenticated data.rpcOtaResume=1 enables Wi-Fi kind=ota range RPC after an interrupted raw/compressed HTTP stream. The existing /tab5/v1/rpc authentication, selected-host identity, session/time, fresh nonce, HMAC/AES-GCM, offerId/sha256, exact offset/count and binary/zlib envelope are unchanged. Only the exact advertised capability enables the new Wi-Fi fallback. USB/BLE continue the same immutable range protocol. Each retry uses a new request/nonce; no command, credential or ciphertext replay is added.

Within the same installer and boot, completed blocks, the flash handle and running image hash remain. Discard only an incomplete compressed block. Per-gap attempts are capped at eight with 250–4000 ms backoff; no new retry starts after 90 seconds in a gap, 15 minutes for the installer or 32 total retries. In-flight calls retain their existing bounded deadlines. Invalid/authentication/corrupt responses, a selected-host/mode change or a newer authenticated offer withdrawal stop recovery. Older cross-channel status publications cannot withdraw a newer offer. Full hash/image/project/version validation still precedes boot-partition selection.

中文：三通道在同一次开机、同一升级内保留已完成块，断线后从失败块继续。Wi-Fi 先用高速压缩流，IO 中断后按能力声明转为同一电脑/同一 Wi-Fi 的鉴权分段；不跨电脑、不跨所选通道。主动停止仍撤回 offer，设备确认空闲前不重开，末段交出后仍不能停止。断电、重启、更换镜像或恢复预算耗尽后重新开始，不声明持久化续传。Mac 未改；旧 Mac 未声明能力时仍用原 HTTP 路径，不能把 Windows 的 Wi-Fi 恢复验收写成 Mac 已支持。

Mac 2026-10-28 OTA 候选补齐已有 `rpcOtaZlib=1`、`rpcOtaResume=1` 和严格 `tcp-v2`＋`aibot-zlib-blocks-v1` 双向协商。48 KiB 独立 zlib 块、原样回退、范围恢复及镜像 SHA／ELF 校验沿用 Windows／TAB5 既有定义；不新增字段或设备版本要求。压缩在选择镜像时缓存；已鉴权 HTTP OTA 使用六分钟有界发送期限，旧接收端继续小块限速，普通状态／RPC 不沿用 OTA 的期限。发送提交计数不等于设备接收或刷机成功。

The October 28 Mac OTA candidate implements the existing rpcOtaZlib/rpcOtaResume capabilities and exact tcp-v2 plus aibot-zlib-blocks-v1 negotiation. Independent 48 KiB zlib blocks, raw fallback, immutable range recovery and image SHA/ELF validation retain the Windows/TAB5 definitions. No new wire fields or device version are required. Compression is cached during selection; authenticated HTTP OTA has a bounded six-minute deadline and legacy pacing. Submitted bytes do not prove device receipt or installation.

Mac R10 在确认当前设备为支持恢复的 `0.2.257-ui` 或更新的 `0.2.x-ui`，且已协商上述压缩头时，校验身份与目标镜像后返回空 HTTP 503，让接收端直接进入既有 `rpcOtaResume=1` 分块路径，避免先等待长流超时。版本必须来自同一绑定设备的既有身份／签名校验路径，不根据待安装镜像版本推断。未知、旧版或未协商请求保留流式兼容；鉴权失败仍为 401，镜像不匹配仍为 404。此处复用既有初始失败恢复行为，Windows 和固件协议无需新增字段。

Mac R10 authenticates the request and exact image, then returns an empty initial HTTP 503 for the same confirmed device running `0.2.257-ui` or later `0.2.x-ui` with the existing compression negotiation. The receiver immediately uses its existing `rpcOtaResume=1` range path instead of waiting for a stalled stream. Capability is inferred only from the current peer's identity/signature-checked version, never the target image. Unknown, older or unnegotiated peers retain streaming; invalid authentication remains 401 and an unavailable image remains 404. This reuses the existing receiver behavior without new Windows/firmware wire fields.

Mac R6 的本地发送调整不改变协议：新接收端和普通 HTTP 回包从首字节起按最多 1024 字节、至少 2 ms 间隔连续提交；旧 OTA 的 2048 字节／64 ms 节奏保留。不修改系统 MTU、配对、鉴权、镜像偏移或哈希规则。实机对照的重传与 RAM 测速记录见 `docs/development/MAC-VOICE-SOURCE-20261028.md`；发送计数仍不等于设备收妥或升级成功。

Mac R6 changes only local send scheduling: modern OTA and normal HTTP replies submit at most 1024 bytes per step with a minimum 2 ms gap from the first byte; legacy OTA retains 2048-byte/64-ms pacing. System MTU, pairing, authentication, offsets and image hashes are unchanged. See the development record for real retransmission/RAM results; submitted bytes still do not prove receipt or installation.

### Explicit same-image reinstall / 明确同镜像重装（TAB5 .286）

0.2.286-ui 起，同版本／相同 SHA 的固件仍可作为待安装提供；只有原有设备确认或已认证的电脑升级命令能开始写入。删除版本与已运行镜像的短路检查，不修改 SHA、项目／版本、备用分区或启动校验。桥接每次明确开始分配新的 offerId／requestId，不自动重发；相同版本重装期间的版本心跳不得清除命令、传输租约或冒充完成。旧接收端仍需先升级一次；Mac／Windows 显示明确原因。沿用现有字段及版本 1，无新增协议字段。

From .286, an offer may match the running version and bytes. Only the existing explicit device confirmation or authenticated desktop command starts writing. Remove the version/hash early exit while retaining hash, project/version, inactive-partition and boot checks. Each deliberate host start allocates fresh offer/request IDs; no automatic repeat occurs. Matching version heartbeats cannot consume a reinstall command, clear its lease or prove completion. Older receivers need one upgrade first, with a specific host explanation. Existing fields and protocol version 1 remain unchanged.

### Mac automatic post-upgrade boot verification (2026-10-28)

Mac starts read-only boot verification after delivering the final image range and allowing the receiver to reboot and complete its healthy-boot window. It reuses existing `bootVerifyCommand` / `boot-verify` over Wi-Fi or BLE, or the existing USB hello/diagnostic exchange. Three independent live UI samples, the selected ELF hash and a VALID application partition remain required. Automatic checks also require uptime shorter than time elapsed since the current upgrade request; unchanged pre-reboot firmware versions cannot pass. At most six bounded checks are attempted. Checks do not resend an OTA command, retry installation, change display preferences, or equate a version heartbeat with success. No wire field or Windows receiver behavior changes. Manual verification remains available.

Mac 在提供最后一块镜像并等待健康启动后自动核验，复用原 Wi-Fi／BLE 启动核验和 USB 身份／诊断消息；精确 ELF、VALID 分区、三个独立实时 UI 样本不变，自动流程另校验本次请求后重启的运行时间。最多六次只读核验，不自动重发升级、重刷或改展示设置；保留手动核验入口。

TAB5 .287 keeps the authenticated HTTP range connection after a complete successful response. Failed requests still close the connection and use the existing offset, ownership and retry budgets; worker cleanup closes the remaining connection. Every range retains independent authentication. No framing, authentication or protocol version changes.

TAB5 .287 在成功完整读取分块后复用 HTTP 连接；失败仍关闭并按原偏移、归属与重试预算恢复，退出时清理连接。每块独立认证，不修改消息格式或协议版本。


### TAB5 .288 segmented flash pipeline (2026-10-28)

The shared TAB5 receiver initially erases at most 64 KiB through `esp_ota_begin`, then erases subsequent aligned blocks of the inactive partition just before writing them. The independent receiver continues concurrently. Existing Windows and Mac OTA bytes, authentication, resume offsets and final integrity/boot checks are unchanged. `prepareMs` is the cumulative segmented erase time; `writeMs` excludes it. These counters are not a measurement of healthy-boot completion. Same-image Wi-Fi range acceptance is 25.921 s; Mac continuous streaming remains an unaccepted experiment because real-device retransmission dominates.

TAB5 .288 先通过 SDK 擦除最多 64 KiB，再在写入前逐段擦除备用分区，独立接收任务并行工作。Windows、Mac 的镜像格式、认证、续传偏移及最终镜像／启动核验不变。`prepareMs` 累计各次擦除，`writeMs` 不包含擦除；二者不能替代健康启动完成时间。实机同镜像 Wi-Fi 分块安装 25.921 秒；Mac 连续流仍因实机重传未获性能验收。


### Mac R18 native TCP sender (2026-10-29)

Mac now uses public BSD sockets for the same authenticated HTTP protocol. The .288+ identity-matched receiver with the existing `tcp-v2`/zlib negotiation receives the continuous image using awaited 16 KiB writes. Older range-capable receivers retain direct authenticated range routing; existing stream-to-range recovery remains. The change is internal to the Mac listener/sender; no wire fields, Windows requirements or device image changes are introduced. Same-image .288 Wi-Fi acceptance records 21.666 s installation and 32 ms data wait, with exact automatic boot verification. See `docs/development/MAC-TAB5-OTA-TCP-20261029.md` for the packet-level observations, comparison limits and lifecycle tests.

Mac R18 通过公开 BSD socket 提供原认证 HTTP 协议；身份匹配且已协商 `tcp-v2`／zlib 的 .288 及之后 0.2.x 接收端恢复连续流，按 16 KiB 等待写入。较旧的分块兼容设备保留直接鉴权分块，流失败后的原偏移续传也保留。此为 Mac 内部传输实现变更，无新增协议字段或设备镜像变化。相同 .288 实机 Wi-Fi 安装 21.666 秒、等待数据 32 ms，自动精确启动核验通过；证据与边界见上述记录。

## Mac BLE response scheduling (2026-10-29)

Mac adopts the existing Windows RPC response windows: regular response windows up to 8, bulk `rpcWriteWindow` 8–64, and numeric `rpcReplay:1`. Negotiated bulk replay sends the same immutable response window once with confirmed writes only after ATT 0x0e; it never re-executes the request. Peers without replay use confirmed bulk fragments. CoreBluetooth readiness bounds native command submission; the final ATT response remains the cursor barrier. No new fields or firmware/Windows changes are required. See `docs/development/MAC-TAB5-BLE-OTA-20261029.md` for acceptance and platform limits.

Mac 对齐现有 Windows RPC 回复窗口，复用 `rpcWriteWindow`／`rpcReplay`；仅 ATT 0x0e 可触发当前密文窗口的一次逐片确认补发，不重做请求。旧设备批量回复逐片确认，CoreBluetooth 原生背压及窗口末尾确认保留。协议字段、固件与 Windows 无需变更；实测及平台边界见上述记录。


## TAB5 291: bounded OTA admission and optional BLE radio lease

Authenticated status `data.rpcOtaBleRadioLease=1` opts into a temporary Wi-Fi radio pause during immutable `kind=ota` bulk reads on the already selected BLE transport, including automatic connection mode. Windows and Mac advertise the same optional capability; older devices ignore it and older bridges retain their existing behavior. It does not select BLE, change saved connection preferences, affect USB/Wi-Fi OTA, or enable isolation for ordinary AUTO traffic or explicit comparison probes.

The Wi-Fi worker alone stops/restarts the driver. A lease is bounded by 35 seconds of inactivity, renewed only after a successfully authenticated range. A 600 ms idle gap, failed range, disconnect, loss of host authorization, or incompatible mode change restores Wi-Fi. Existing diagnostic/gallery owners cannot be displaced. A failed radio-pause acquisition leaves the ordinary BLE path available.

OTA and OTA-control RPCs wait up to 2 seconds for mailbox admission within their existing total deadlines; boot verification waits up to 1 second within its two-second total deadline. `RPC_STATUS_UNPUBLISHED_BUSY=-1011` means no request was published; only immutable OTA reads use the existing bounded recovery policy for this result. Command/credential replay, authentication, nonce validation and image/boot verification are unchanged.

中文：291 新增可选能力 `data.rpcOtaBleRadioLease=1`，Mac 与 Windows 定义一致。仅当已选择蓝牙执行不可变固件块读取时，允许在自动连接模式临时暂停 Wi-Fi；不更改已保存连接模式，也不影响 USB/Wi-Fi 升级、普通自动模式流量或显式对照预检。Wi-Fi 驱动仍只由原工作线程操作；35 秒无成功块、600 毫秒空闲、失败、断开、归属失效或不兼容模式切换均恢复无线。不能抢占图库/预检持有的暂停权限，暂停获取失败仍可走普通蓝牙。

升级读取及升级控制在原总超时内最多等待 2 秒获取 RPC 通道；启动核验在原 2 秒总超时内最多等待 1 秒。`-1011` 明确表示请求尚未发布、通道忙，仅不可变固件块允许进入已有有界恢复流程；不增加命令或凭据重放，不改变身份、随机数、镜像与启动核验。


Mac R23 本地候选沿用 TAB5 voice 的既有字段：TAB5 音频的第一块有效 `audio` 被接收并有界保留后，`ready=true` 表示设备收音及桥接接收已就绪，不表示识别完成。最多保留 128000 个 16 kHz 采样；只有实际豆包输入设备确认后才转送。提前停止转为 `recognizing`，待缓存转送后停止识别器；准备失败、超限、取消或未转送完就提前结束均不能插入文字。Mac 实体麦克风仍须实际输入 IO 就绪。既有 .291 的 `voice_capture_ready` 同时要求本机音频 ACK，因此无需新增设备字段；Windows 原有 `InputReady` 与已接收 PCM 判定保持独立兼容。

The local Mac R23 candidate retains the existing voice fields. For TAB5 capture, accepting and retaining the first valid audio block may establish ready capture before recognition is ready; up to 128000 samples at 16 kHz are buffered. Forwarding requires the verified Doubao input route. An early stop waits in recognizing until queued audio is forwarded, then ends recognition. Failure, overflow, cancellation or premature completion cannot authorize text insertion. Physical Mac input still requires actual input IO. Existing .291 firmware additionally requires its local audio acknowledgement; Windows keeps its existing InputReady plus received-PCM rule. No new peer fields are required.

Mac 的后台 `codex/read` 可沿用 429 重试响应，在单个后台任务完成前释放设备邮箱；成功页按设备、桥接会话、任务及分页参数隔离，短时刷新不清空上次成功文字。发送／停止任务和草稿保存均不进入此重试机制。

Mac may return the existing 429 retry status for a pending background codex/read, releasing the mailbox while a single bounded job runs. Successful pages are isolated by device/session/task/pagination, and a transient refresh failure retains recent public text. Task mutations and draft saves are not replayed by this mechanism.

Mac R24 在 USB／BLE 的语音 recognizing 查询中最多等待 350 ms，最终确认、取消或停止会唤醒等待。等待时释放语音状态锁，返回前复核 voiceId、backend generation 和功能启用状态；沿用既有响应和原请求截止线。

Mac R24 may hold a USB/BLE recognizing poll for up to 350 ms to return a confirmed final without another round trip. It releases the voice state lock while waiting, wakes on completion/cancel/stop and revalidates voice ID, backend generation and enablement before replying. Existing response fields and request deadlines remain unchanged.

### Mac desktop read synchronization (local implementation)

Mac 桥接读取本机 Codex 已读元数据，按账户、主机与已知完成轮次生成现有 `repliesRead`，并移除对应 `repliesUnread`。桌面状态缺失不是“全部已读”，暂时失败保留最近确认，新轮次不继承旧确认；最多等待 5 秒桌面写入稳定。TAB5 继续仅以相同 task/turn 的回执清除通知，Windows 已有对应实现。本变更不增加协议字段、不写 Codex 桌面状态，也不改变设备渲染回执权限。

The Mac bridge now derives existing `repliesRead` entries from account/host-scoped local desktop metadata and an exact known completed turn, after a five-second settling interval. Missing state is unknown; transient failures preserve previous confirmation and new turns cannot inherit it. TAB5 retains exact task/turn matching; Windows already implements the counterpart. There are no new wire fields, desktop-state writes or expanded device receipt permissions.

Mac follow-up: a desktop `systemError` snapshot may expose a failed public reply and its exact read receipt without enabling send/steer/interrupt. `repliesRead` retains up to 80 exact receipts even when a transport reduces the visible task catalog; the receiver already clears matching old notifications across catalog reductions. No new wire fields or Windows/firmware changes are required.

Mac 后续：桌面 `systemError` 快照可用于读取失败轮次的公开回复及精确已读回执，不开放发送／追加／停止操作。状态帧缩减任务目录时仍保留最多 80 个精确 `repliesRead` 回执，设备已有跨目录缩减清除对应旧通知的行为。无需新字段或 Windows／固件改动。


Mac R27 implementation note (2026-10-29, unpublished): local Codex catalog construction excludes normalized `.openclaw` workspace directory components, since OpenClaw integration sessions share the vscode App Server source kind. Ordinary titles mentioning OpenClaw and normal project directories remain eligible. A nonempty explicit `desktop.stage-draft` may acquire the unique, freshly verified main composer in the current foreground Codex window when initial focus is elsewhere. Bounded complete traversal excludes dialogs and embedded documents; task identity, structure, current focus and exact existing draft are rechecked before the existing single write/readback. No app activation, navigation, automatic send, request field or receipt change; clearing and submitting keep their original focus/receipt requirements.

Mac R27 本地实现说明：先按规范化 `.openclaw` 目录组件排除集成会话，再生成 Codex 目录。仅显式非空 `desktop.stage-draft` 在原焦点不就绪时尝试聚焦当前 Codex 前台文档中唯一且重新核对任务身份的主输入框；有界完整扫描排除弹窗及嵌入文档，写入前继续核对焦点和精确草稿。不激活其他应用、不导航或自动发送，无线协议及回执字段不变。


Mac R28 implementation note (2026-10-29, unpublished): the existing desktop operations may address the current verified mid-turn question editor. The local bridge captures its target on a valid voice start following successful navigation, before IME focus transfer; no new wire field is added. Question staging and a later explicit submit bind the encrypted receipt to the same device, task, process-local question/editor identity and exact text. Submission consumes the receipt before one native Next/Send action, never the ordinary turn-start command. Changed questions retain previews; unsupported/ambiguous cards do not fall back to the main editor. Native UI acknowledgment does not establish model execution completion. Windows and receiver contracts are unchanged.

Mac R28：现有直达操作支持当前已核验的中途提问回答框；成功定位后的合法录音开始先记录目标。问题回填和后续显式提交在本地加密回执中绑定同设备、任务、当前题／编辑器身份及精确文字；先消费回执，再单次调用题卡自己的下一步／发送，不调用普通新轮次发送。切题保留预览，未知卡片不能回退主输入框。本地 UI 响应不代表模型执行完成。无线协议及 Windows／设备字段不变。

Mac R30 implementation note (2026-10-29, unpublished): main-composer `desktop.submit-draft` consumes the same encrypted receipt before invoking the verified composer's unique enabled native Send/Steer/Queue action once. This respects the current desktop submission mode for active tasks without pre-clearing text or falling back to turn-start IPC after an uncertain action. Exact device/task/target/text/expiry and opt-in checks remain. A cleared original composer acknowledges local UI response only; downstream delivery and model completion are not implied. Unavailable controls use the existing 409 `composer_changed`; an attempted unconfirmed action uses 504 `desktop_result_unknown`. Retries query receipts and never replay submission. Full-title ambiguity is reported in local diagnostics, while an exact live task URL still takes precedence. No request/response field, Windows behavior or receiver change is required.

Mac R30：主输入框的 `desktop.submit-draft` 沿用原加密回执，先消费，再单次执行该输入框唯一且启用的发送／引导／排队按钮，遵循电脑当前提交方式。不先清空文字，结果未知时不回退新轮次接口。同设备、任务、目标、精确文字、期限与授权检查保持；原输入框清空仅确认本地 UI 响应，不保证下游送达或模型完成。按钮未确认沿用 409 `composer_changed`，已尝试但结果未知沿用 504 `desktop_result_unknown`；重复请求不重复操作。重名原因在本机诊断明确显示，精确当前任务地址仍优先。请求／响应字段、Windows 行为和设备端不变。


Mac R31 local follow-up: native main submission scopes button discovery to the verified editor's nearest ComposerLayoutBody, which contains its adaptive footer, avoiding dependence on an optional presentational outer Root. Revalidation checks the same body. Pre-action failures preserve precise fixed local diagnostics; the existing 409/504 responses, receipts and all wire fields are unchanged.

Mac R31 本地补充：主框提交仅在已核验编辑器最近的 ComposerLayoutBody（包含其底部操作区）内查找按钮，避免依赖可能被辅助功能树省略的外层排版 Root；执行前复核同一 Body。预动作失败保留具体固定诊断，既有 409／504、回执及全部线协议字段不变。


Mac R32 local follow-up: one native main-composer press is followed by bounded read-only acknowledgment checks even when the AX action response times out. Two consecutive verified empty reads of the original task/editor/body/window confirm local consumption. Temporary unreadable snapshots can settle; no second press or send fallback occurs. Post-action diagnostics remain specific, and a later successful submit clears obsolete local failure text. The existing response fields and receipt semantics remain unchanged.

Mac R32 本地补充：主框原生动作只执行一次，即使 AX 响应超时也继续有界只读核对；原任务、编辑器、正文容器与窗口连续两次确认清空才确认本地消费。临时快照不可读允许恢复，不补点或回退发送。后动作诊断保留具体原因，后续成功提交清除本地旧失败提示；响应字段与回执语义不变。


Mac R33 local follow-up: an explicit main submit may reconcile uncertain UI acknowledgment against the same local desktop owner's version-11 snapshot. A pre-action identity baseline and exact input comparison require a newly accepted message, excluding historical identical messages and preexisting aliases. Snapshot data is not exported; no second native/IPC send is issued. This adds no wire fields and preserves encrypted idempotency, opt-ins and conditional draft cleanup. Queue acceptance by the UI remains distinct from server receipt and model completion.

Mac R33 本地补充：显式主框提交可用同一本地 desktop owner 的 v11 快照补足未知 UI 确认；动作前标识基线及精确输入比较要求新增已接受消息，排除历史同文及旧别名。快照不导出，不进行第二次原生或 IPC 发送。无线协议字段、加密防重回执、授权和条件草稿清理不变；UI 入队、服务端收到及模型完成仍区分。

Mac R35 local follow-up (2026-10-29): reliable retention in Codex's queue also establishes successful `desktop.submit-draft` delivery (`200`, `submitted:true`), without implying model execution or completion. Before the single native action, the bridge captures ephemeral identity baselines for accepted input, the committed local queue and the read-only `thread/queue/list` server queue. After unknown UI acknowledgment, new exact-text queue entries may reconcile success only for the same task/owner, excluding all preexisting aliases and cross-queue migrations. Failed/unknown states, partial attachment matches, unavailable/incomplete queue reads and stale identities cannot prove delivery. The server read has a 600ms deadline and never waits behind history reads; the local file is bounded, owner-checked and never written. Only identity/text hashes remain in memory; raw queue contents are not logged, exported or added to the encrypted delivery journal. No second press, fallback send or queue mutation occurs. Existing fields and .292 firmware remain compatible; its generic success message denotes handoff to Codex, including queueing. Native bridge diagnostics distinguish queued retention. Windows parity work is listed in `docs/development/WINDOWS-FOLLOWUP-FROM-MAC-20261029.md`; this change adds no peer field.

Mac R35：Codex 已可靠保存到队列也算 `desktop.submit-draft` 发送成功，沿用 `200 + submitted:true`，不代表模型开始或执行完成。单次原生动作前获取已接受消息、本地已保存队列及只读 `thread/queue/list` 服务端队列的内存标识基线；UI 确认未知后，只有同任务／owner、完整文字相符且别名全部不在原基线的新条目才能确认成功。旧队列跨来源迁移、失败／未知状态、附件部分匹配、不可读／未完整分页及旧标识不能冒充本次交付。服务端读取限 600ms、不等待历史查询锁；本地文件有大小与所有者检查且从不写入。只留内存摘要，不记录或导出队列正文，不加入持久回执正文。没有第二次点击、备用发送或队列修改。既有字段与 .292 兼容，其通用成功提示表示已交给 Codex，含排队；桥接本机诊断区分“已保存排队消息”。Windows 对齐清单见上述文档，本改动不增加对端字段。
