# TAB5 接入（开发中） / TAB5 integration (in development)

## 0.2.53-ui 设置与阅读位置候选 / Settings and reading candidate

Windows 设备中心新增 TAB5 显示设置。协议仍为 v1，原 ESP8266 不变。加密状态的 `data.connectionHealth.displayCommand` 为 `null` 或 `{version:1,requestId,expiresAt,settings}`；`requestId` 为 32 位小写十六进制，`expiresAt` 为 Unix 毫秒，20 秒有效。`settings:null` 只读；写入需要最近读取的 `revision`。

Windows Device Center adds TAB5 display settings without changing ESP8266 or protocol v1. The encrypted snapshot carries the optional command above. A null settings object reads the device. Writes require the last observed revision; commands expire after 20 seconds.

设置对象包含 `revision`（8 位十六进制内容修订号）、`brightness`（10–100）、`volume`（0–100）、`muted`、`selected`（-1 自动或 0–7）、`cycle`、`interval`（10/15/30/60 秒）、`saverMinutes`（0/1/5/10/30/60，0 关闭）、`alerts`、`enabled[8]`（0/1 且至少一页）及 `order[8]`（0–7 排列）。页面顺序为总览、模型额度、天气、行情、电脑状态、音乐、桌宠、时钟。启用轮播时 `selected=-1`。

The settings schema covers brightness, volume/mute, selected page, cycling, interval, screensaver timeout, alerts and an eight-page mask/order. Page indices are Overview, Quotas, Weather, Stocks, System, Music, Pet and Clock. Cycling requires automatic selection. The revision detects changed values; it is not a security signature.

设备在 LVGL 线程校验、保存并形成 `{kind:"display-settings",session,issuedAt,requestId,ok,error,settings}` 回执；USB/BLE 复用 RPC，Wi-Fi 新增 `POST /tab5/v1/rpc`。使用既有 AES-GCM、nonce、session/time 防重放及 HMAC：`POST|/tab5/v1/rpc|deviceId|nonce|sha256(packet)`。HTTP 200 内部仍包含 RPC `status`，不等于保存成功。桥接只有收到匹配请求的 `ok:true` 有效设置才显示已保存；冲突、设备本机编辑或保存失败要求重新读取。相同请求跨通道重复到达不再次写入；回执每两秒重试，最多 20 秒。桥接等待 25 秒后显示未确认。

The LVGL task validates and persists settings, then reports through existing USB/BLE RPC or the new authenticated Wi-Fi route. HTTP success is not application success. The bridge requires a matching, valid successful report. Local editing, stale revisions and persistence errors are reported explicitly. Duplicate commands do not repeat writes; reports retry every two seconds for 20 seconds, while the host times out after 25 seconds.

任务帧增加可忽略的 `pinned` 布尔字段；常用任务优先进入限长列表并在项目中置顶，不改变总览任务选择。设备 NVS 保存最多 32 个阅读书签，不保存回复正文；更改每 15 秒提交，手势与 OTA 期间延后。窗口、协议、NVS 模拟与固件构建验证不代表已部署或真机通过，详见 [开发记录](DEVICE-DEVELOPMENT-2026-09-29.md)。

Task entries gain an optional `pinned` flag. Pins affect list priority, not the overview selection. The device persists up to 32 bookmarks without reply text, committing changes every 15 seconds outside gestures and OTA. Host/protocol/NVS simulations and firmware builds do not prove deployment or hardware acceptance; see the development record linked above.

Windows 新增“USB 配对 → 新设备首次安装”候选入口：选择独立 TAB5 项目生成的完整安装 ZIP，先备份并核验 P4 闪存，再安装、校验、重启检查，随后回到 USB 配对和 Wi-Fi 设置。已安装 AI-bot 的设备仍使用 OTA；恢复原固件只接受同一设备的有效备份。使用既有固定版本 esptool 包，不要求用户安装 Python。出厂设备完整安装及恢复仍待真机验收，尚未发布。

Windows adds a candidate first-install entry under USB pairing. It validates the full TAB5 ZIP, backs up and verifies P4 flash, installs and verifies it, then checks boot before returning to pairing/Wi-Fi setup. Existing installations use OTA; recovery requires a valid backup for the same device. The existing bundled esptool is reused without requiring Python. Physical stock-device installation and recovery acceptance are pending; this is not yet released.

电脑端新增 `Tab5Service`，由同一个 BridgeRuntime 提供状态，USB / Wi-Fi / BLE 使用独立协议。当前一台 TAB5 可与原 ESP8266 小屏共存。

The Windows bridge adds an independent TAB5 session using the shared BridgeRuntime. USB, Wi-Fi and BLE carry the same device snapshot. One TAB5 and the existing ESP8266 can coexist.

- 托盘入口：设备连接 → TAB5 连接。通过 USB 初次配对及写入 Wi-Fi；无线负载使用设备专属 AES-GCM 密钥。
- Tray entry: Device connection → TAB5 connection. Initial pairing and Wi-Fi provisioning use USB; wireless payloads use a device-specific AES-GCM key.
- 原小屏 `ping/pong`、`/status`、固件、页面设置不变；TAB5 新接口为 `/tab5/v1/status`，仅接受独立 HMAC 鉴权。
- Existing ESP8266 messages, `/status`, firmware and display preferences remain compatible; TAB5 uses `/tab5/v1/status` with separate HMAC authentication.
- 首阶段包括状态与额度、天气行情、系统及音乐文字信息。语音、桌宠资源迁移和 OTA 不在本次首阶段内。
- Initial scope: activity, quotas, weather, stocks, system metrics and music text. Voice, pet assets and OTA are later work.
- 第二阶段迁入十个平台统一额度与余额、重置时间、桌宠和封面分片、音乐进度、网速采样、农历日期。原小屏配置不进入 TAB5，TAB5 轮播/屏保设置由设备独立保存。真机验证见独立 TAB5 项目的验收记录。
- Phase two adds unified quotas/balances for ten providers, reset times, chunked pet/artwork resources, music progress, network samples and lunar dates. TAB5 stores its own cycle/screensaver preferences; existing device preferences remain separate. See the TAB5 project's acceptance record for hardware evidence.
- Windows 构建及协议测试不代表 USB/Wi-Fi/BLE 实机通过。macOS 尚未实现 TAB5 接入，不能宣称 Mac 支持。
- Windows builds and protocol tests do not prove hardware acceptance. TAB5 support is not yet implemented on macOS.
- Windows 桥接为 TAB5 下发项目列表及各项目最近会话；额度状态按实际来源区分 `API PAYG`、Coding Plan、额度窗口重置和明确的套餐到期日。
- The Windows bridge sends a project catalog and recent sessions per project to TAB5. Quota data keeps API PAYG, Coding Plan, reset windows, and explicit plan expiry dates separate.
- 已配对 TAB5 时，桥接刷新用户已选展示且已授权/配置 API 的厂商；临时固定到其他页面或关闭轮播不会停止这些厂商的刷新。曾授权但未选展示的厂商不自动采集、不发失败提醒。官方 API 每 2 分钟调度，网页每 65 秒轮换一家。失败保留最近一次成功数据并标记缓存。
- A paired TAB5 refreshes providers selected for display and authorized/configured, even while temporarily showing another fixed page or disabling cycling. Previously authorized but unselected providers are neither queried nor included in failure alerts. Official APIs run every two minutes; web capture rotates every 65 seconds. Failures retain the last successful values marked stale.
- 照片附件：独立加密接口 `/tab5/v1/codex/image` 暂存用户确认的图片，最多 3 张、每张 2 MiB。草稿与送达记录包含附件 ID；实际桌面提交使用 `localImage` 与文字同轮发送，收到桌面确认才清空草稿。存储卡端当前读取 JPG/JPEG，设备拍照直接编码 JPEG；不自动发送、不格式化存储卡。真机验收另记。
- Photo attachments use the separate encrypted `/tab5/v1/codex/image` route, with up to three images of 2 MiB each. Drafts and delivery fingerprints include image IDs. Desktop requests contain real `localImage` inputs alongside text; only a confirmed desktop acknowledgement clears the draft. TAB5 currently reads JPG/JPEG from SD and encodes camera captures as JPEG. No automatic submission or card formatting. Hardware acceptance is tracked separately.

### 0.2.61 传输优化候选 / Transport candidate

只有自动连接模式按业务可用通道择优；照片当前优先整包 Wi-Fi HTTP，其次 USB RPC、蓝牙 RPC。仅 USB/Wi-Fi/蓝牙严格固定，不跨通道补发；必须有同一桥接会话的有效近期数据，不能仅依据 Wi-Fi 关联。OTA 仍只支持 Wi-Fi/USB，固定蓝牙时提示切换；语音当前只支持 Wi-Fi/蓝牙，固定 USB 时明确提示不支持，不再暗中改走无线。新增通道能力与现有通道性能是两项验收，不混为已完成。

Only automatic mode selects among supported transports. Photos currently prefer whole-image Wi-Fi HTTP, then USB RPC, then BLE RPC, using fresh authenticated state from the same bridge session. Fixed modes never cross channels or automatically resubmit uncertain transfers. OTA supports Wi-Fi/USB; voice supports Wi-Fi/BLE. Unsupported fixed-mode combinations fail visibly instead of silently changing transport. Adding a transport is separate from optimizing existing transport performance.

USB/BLE 图片片段支持最多 8192 原始字节，固件仅在旧桥接明确拒绝 `invalid_range` 时回退到 6144。12 KiB RPC 上限、加密、身份/时效、偏移检查及幂等附件 ID 不变。BLE RPC 和语音特征增加可选无响应写能力：仅 7 字节游标 ACK 使用它，下一次读取校验 ID/偏移；应用响应继续确认写。固件请求 MTU 517，协商不足时继续兼容较小 MTU。USB hello/ack 可选 `photoUploadDiag` 格式为 `通道,原始字节,端到端毫秒,状态`；设备端完成回执显示通道/耗时。桥接记录公共 USB RPC、BLE RPC 和 BLE 语音的字节量及请求/处理/响应耗时。不将这些本地验证等同于硬件速率上限。

Image chunks carry up to 8192 raw bytes; firmware falls back to 6144 only after an explicit old-bridge `invalid_range` rejection. The 12 KiB RPC limit, encryption, identity/time checks, offsets and idempotent attachment IDs remain intact. BLE RPC and voice optionally advertise writes without response for seven-byte cursor ACKs only; the next read validates ID/offset, while application replies retain acknowledged writes. Firmware requests MTU 517 with smaller negotiated MTUs still supported. The optional USB `photoUploadDiag` field reports `transport,rawBytes,endToEndMs,status`. Device receipts show transport/duration; shared USB RPC, BLE RPC and BLE voice diagnostics report byte counts and phase timings. Local validation does not establish hardware throughput ceilings.

详细固件和协议位于独立 `m5stack TAB5` 项目。发布时桥接仍在 AI-bot，TAB5 固件另行发布，并记录兼容版本。现有 CI、版本和发布脚本未修改。

Firmware lives in the separate TAB5 project. Release the shared bridge from AI-bot and the TAB5 firmware independently with a compatibility manifest. Existing CI, versions and release scripts are unchanged.

验证命令 / Validation:

```powershell
dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release
dotnet windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.dll --self-test-tab5
```
# 本机验收 / Local acceptance

0.2.1-dev：TAB5 日历增加节气、节假日休息/调休补班与下一假期倒计时。节气表覆盖 2026–2028 年（香港天文台），中国大陆放假调休表覆盖 2026 年（国务院办公厅通知）；未覆盖年度显示待更新。当前和次日字段一起发送，设备跨零点切换日期。此扩展只影响 TAB5 消息，原小屏协议保持不变。

0.2.1-dev: TAB5 calendar metadata now includes solar terms, mainland China holiday/makeup workdays, and the next holiday countdown. Solar-term tables cover 2026–2028 (Hong Kong Observatory); official holiday arrangements cover 2026 (State Council General Office). Other years explicitly require updated data. Current and next-day fields support midnight rollover; the original small-screen protocol is unchanged.

2026-09-23：已在 ESP32-P4 TAB5 真机验证 USB 配对与 ACK、Wi-Fi 认证取数、BLE 加密传输与 ACK；用户确认仅 Wi-Fi、仅 BLE 和自动 USB 显示正常。原 ESP8266 同时保持在线，自动轮播页面/顺序/间隔保持不变。TAB5 USB 使用 DTR=true、RTS=false；蓝牙通过完整 GATT 服务枚举后筛选指定 UUID。仅本机 `/diagnostics/tab5` 提供不含凭据的连接状态。

2026-09-23: Hardware validation passed for TAB5 USB pairing/ACK, authenticated Wi-Fi status, and encrypted BLE delivery/ACK. The user confirmed Wi-Fi-only, BLE-only, and automatic USB display. The original ESP8266 remained online with its existing cycle configuration. TAB5 USB requires DTR=true and RTS=false; BLE discovers all GATT services before selecting the target UUID. Loopback-only `/diagnostics/tab5` exposes connection status without credentials.


0.2.2-dev：TAB5 传输每张重置卡到期日期和近七日 Codex 周额度增量。统计复用已有 QuotaHistory，保留缺失/部分采样语义，每 30 秒聚合；Codex 的 5 小时字段仅为 PLUS 映射。原 ESP8266 协议和显示设置保持不变。

0.2.3-dev：新增 `billingMode` 区分按量 API 余额/累计消费与订阅额度，新增 `epochMilliseconds` 供设备稳定走时。Codex 进程存在但未工作显示空闲。每分钟持续采样，额度先保存，重置卡接口失败不丢采样；同日可核实断档增量保留，短跨零点增量计入后一次采样日期。详见 [统计口径](QUOTA_TRENDS.md)。

0.2.3-dev adds PAYG/subscription display routing and millisecond clock synchronization.
Codex stays idle while its app runs without active work. Minute polling persists
usage before optional credit metadata; verified same-day differences survive gaps,
and short cross-midnight increments belong to the later sample's Beijing date.

0.2.4-dev：实际天气采集保留 AQI 数值/标准与 hPa 气压，缓存克隆和 TAB5 状态转换都保留这些值。和风 `pressure` 标为站点气压，Open-Meteo `pressure_msl` 标为海平面气压；空值不变成零。原 ESP8266 显示字段保持兼容。

0.2.4-dev preserves live AQI values/standards and pressure through provider cache
cloning and TAB5 mapping. QWeather station pressure and Open-Meteo sea-level
pressure keep distinct types, in hPa; missing values remain null.

0.2.2-dev: TAB5 now receives individual reset-credit expiry dates and seven days of observed Codex weekly-quota growth. Aggregation reuses QuotaHistory every 30 seconds, preserving missing/partial observations. Codex five-hour fields are mapped only for PLUS. The original ESP8266 protocol and display settings are unchanged.

### 0.2.5-dev 电脑输入唤醒 / PC input wake

Windows 桥接复用 GetLastInputInfo 向 TAB5 三通道提供单调输入时间戳。新键鼠活动退出屏保并重置闲置计时；不采集输入内容，不改变 ESP8266 屏保设置。首次连接建立基准，静止心跳、重复/旧帧和采集缺失不误唤醒。

The Windows bridge sends monotonic last-input timestamps over all TAB5 transports. New keyboard/mouse activity wakes the display and resets inactivity; input contents are never collected. Initial connection establishes a baseline. Idle heartbeats, duplicate/older frames and unavailable samples do not wake it. ESP8266 settings remain independent.

### 0.2.6-dev 金额合并与音乐时长 / Cost merge and music duration

DeepSeek 同时走官方余额接口和现有 WebView 网页采集；API 不再清空网页消费金额，也不更新其采集时间。凭据范围、币种变化清除旧金额；网页余额与最近 API 余额不一致拒绝合并；消费采样超过 24 小时隐藏。缺少网页登录时仍可更新 API 余额。

NetEase Cloud Music 系统媒体会话缺少总时长时，只读 Windows 自带 SQLite 接口查询本地 historyTracks（最多最近 32 条），严格核对歌名、歌手、专辑，不按同名歌曲猜版本。未获得有效进度时 timelineAvailable=false；TAB5 只显示总时长，不生成比例进度。本机真实数据库读取 285.827 秒已验证，当前播放器未运行、桌面访问被拒绝，实时播放/暂停/拖动验收仍待用户。

DeepSeek API balance refreshes preserve the separately dated web usage sample. Credential scope/currency changes clear usage; inconsistent wallets are not merged and usage older than 24 hours is hidden. NetEase duration fallback reads only bounded song metadata and matches title, artist and album. Unknown playback position stays unknown; no synthetic progress is generated. Other providers' daily usage charts are explicitly outside current requirements.

### TAB5 回复读取（2026-09-26 候选）

`POST /tab5/v1/codex/read` 复用 TAB5 配对密钥、AES-GCM 和正文摘要 HMAC。解密体为 `op: "read"`、`taskId`、`session`、`issuedAt`、`message: ""`。read 路由必须携带 read 操作；turn 路由拒绝 read 操作，防止跨路由重放变成提交。读取仍须目录中的合法任务 ID、90 秒有效期、nonce 防重放和每秒一次请求上限。

返回 200 只表示读取完成，正文随后通过加密状态帧 `codexTasks.responses[taskId]` 下发。查看中的任务优先进入最多 3 条回复的帧预算。失败不清空已有缓存。Windows 优先从 thread/read 获取最新轮次；部分桌面历史 items 为空时，只读对应本机会话文件末尾最多 2 MiB，匹配同一轮次的公开最终答复，绝不执行新轮次。

### 2026-09-26 Windows 桌面直连

TAB5 Codex 发送使用运行中 Codex 桌面的本地命名管道所有者，不再用独立 CLI 的 turn/start。协议为本地内部接口，按版本验证并在不兼容时明确失败；保持桌面权限审批、模型和线程上下文。202 仅表示桌面接收，最终完成由独立轮次跟踪产生 repliesReady；发送后断线/超时不自动重发。读取动态只取桌面对应轮次的公开内容，详情见 TAB5 项目的 docs/CODEX-REMOTE.md。设备 HTTP/AES-GCM 协议保持兼容。

### 0.2.89-ui 语音断线恢复 / Voice reconnect recovery

断线后停止收音，保留本轮会话与原光标，通过原通道 stop/poll 回收已识别文字；不重传不确定音频、不自动发送。桥接处理已收到的音频尾段，并将当前结果保留期延长到无请求 5 分钟。恢复可能不完整时明确提示补录；可取消恢复。配套桥接与固件已部署并通过启动核验，短时/45 秒 USB 断线恢复及取消后新录音隔离三项真机复验通过，原自动轮播已恢复并核验。此结论仅覆盖本轮场景，详见 [089 恢复记录](VOICE-RECOVERY-089.md)。

After a disconnect, capture stops and the existing take is recovered over its original transport using stop/poll, preserving the insertion anchor without replaying uncertain audio or automatically sending. The bridge drains received audio and retains the current result for five minutes without requests. Partial recovery is clearly indicated and can be cancelled. The paired bridge and firmware were deployed and boot-verified. Short and 45-second USB disconnect recovery, plus isolation of a new recording after cancellation, passed user acceptance. The original automatic display cycle was restored and verified. Acceptance is limited to these scenarios.

### 2026-09-26 草稿和发送恢复

- 同一配对加密协议新增 `op=draft-save`（turn 路由），`draft-load`、`receipt`（read 路由），以及显式的 `steer`、`interrupt`（turn 路由）。三类限速独立：草稿、读取、用户操作；nonce 仍共用防重放表。操作必须符合路由，畸形 op 拒绝；旧固件无 op 仍视为 send。
- 新请求带 UUID `requestId`；追加/停止还带 `expectedTurn`。202 只代表桌面接受，不代表模型完成。回复帧增加 `control={taskId,turnId,running,waiting}` 与 `draftView={taskId,requestId,text,pendingRequest,operation,expectedTurn}`；不把所有草稿装入状态帧。
- 当前用户 DPAPI 加密的 `tab5-codex.dat` 持久保存草稿、请求摘要和回执。派发前落盘，重启后的重放仅核对结果；不确定投递不能自动重发。接受时清除对应草稿，停止任务保留草稿，损坏文件不覆盖。512 条记录上限只淘汰旧 completed/rejected，80 份草稿不自动淘汰。
- 运行中追加与停止已按本机桌面 IPC 版本实现并通过合成服务验证；尚需设备新版操作验收。继续保留先审阅语音文字，再由用户手动发送。
# 2026-09-26 语音开始提醒

语音响应新增 `ready`：电脑完成快捷键确认、虚拟输出运行，并收到 TAB5 PCM 或 DJI 真实采集回调后才置为真；终止状态为假。TAB5 在此时显示“可以说话了”、开始计时，并播放一次短音。它确认音频通路，不代表豆包内部识别或最终文字成功。

同一次启动复用已枚举的 VB-CABLE 端点，避免重复枚举延迟；诊断日志仅记录焦点、音频打开、快捷键和就绪耗时，不记录录音、正文或凭据。

### 2026-09-30 BLE 窗口传输候选（0.2.63） / BLE window transport candidate

语音/RPC 特征新增可选 Notify。订阅成功才使用 `3:u8 | requestId:u32 | nextOffset:u16 | credits:u8`（小端，credits 1–4），接收相同 ID/偏移/总长头的最多四片通知；旧固件/订阅失败继续 read + ACK。主机队列有界，缺片/乱序/通知失败拒绝处理，认证加密和幂等规则不变。回复每四片与最后一片确认，分片上限 488 字节；大 MTU 状态帧改用 488 字节、八片窗口，图片期间仍四片。图片/语音期间后台状态节奏降为每秒一次，完整状态刷新和历史采样规则保留。固定连接模式仍不跨通道。

Voice/RPC characteristics optionally advertise Notify. After successful subscription, an eight-byte little-endian grant (`3:u8 | requestId:u32 | nextOffset:u16 | credits:u8`, 1–4 credits) requests a bounded notification window with the existing ID/offset/total header. Legacy firmware or a failed subscription retains read/ACK. Missing, out-of-order or failed notifications are rejected; authentication, encryption and idempotency remain unchanged. Replies require an ATT response every fourth fragment and on the final fragment, with a 488-byte value limit. Large-MTU status frames use eight-fragment windows (four during image uploads). Background status cadence becomes one second during image/voice activity; full-state refresh and sample history are preserved. Fixed modes never switch transports.

0.2.62 真机基线：97,559 字节照片，BLE 104,244 ms，成功但速度未通过。0.2.63 仅本地验证；实际速度与语音体验待安装后测量，不能视为达到硬件上限。协议细节与测试见相邻 TAB5 `docs/BLE-WINDOW-063.md`。

Hardware baseline on 0.2.62: a 97,559-byte photo completed over BLE in 104,244 ms; functionality passed but speed did not. Version 0.2.63 has local validation only; hardware throughput and voice behavior remain pending, and no hardware-limit claim is made. See the adjacent TAB5 repository's `docs/BLE-WINDOW-063.md` for protocol and test details.
# 2026-09-30 三通道整合候选

配套 0.2.65-ui 增加 BLE 已确认基线增量/协商窗口和无线参数诊断、USB 二进制邮箱/语音、Wi-Fi 二进制图片/RPC 长连接、BLE OTA 与 RAM 自动测速。固定通道不跨路，自动大数据选择 Wi-Fi > USB > BLE，日常自动顺序保持。尚未部署或通过真实性能验收；详见[整合记录](TRANSPORT-INTEGRATED-2026-09-30.md)及 TAB5 项目 `docs/TRANSPORT-INTEGRATED-065.md` 的协议边界。

# 0.2.67 公共性能扩展 / Common performance extension

认证状态 `rpcBinary=1` 协商 T5R1 元数据加原始数据格式；固定路由和原有认证、重放及业务约束保持。USB 原生有界读取、HTTP 有界预读、BLE 连续邮箱与协商窗口确认、Wi-Fi 临时性能策略、15 字段分阶段测速详见 [0.2.67 协议与证据](TRANSPORT-PERFORMANCE-067.md)。.067 启动核验、18 轮测速、仅蓝牙照片和短句语音已通过；当前 .068 见下节。

Authenticated `rpcBinary=1` negotiates the T5R1 metadata/raw-data envelope while retaining fixed routing, authentication, replay and business checks. Bounded native USB reads, bounded HTTP read-ahead, continuous BLE mailbox service, negotiated acknowledgement windows, temporary Wi-Fi bulk policy and 15-field phase measurements are documented in the [0.2.67 protocol and evidence](TRANSPORT-PERFORMANCE-067.md). Version .067 passed boot verification, 18 benchmark rounds, and BLE-only photo/short voice checks. Current .068 results follow below.

## 0.2.68 raw payload paths / 原始数据路径

T5R1 格式与大小边界不变。照片、测速和 OTA 的 RPC 调用可直接处理原始字节，减少内部 Base64/JSON 转换；旧节点保留 JSON 回退。Wi-Fi TCP 新连接启用 TCP_NODELAY，蓝牙语音使用完成通知唤醒。详见 [0.2.68 性能与验收](TRANSPORT-PERFORMANCE-068.md)。已部署并通过启动核验、18 轮测速及仅蓝牙照片/短句语音；真实照片未证实继续提速，USB 短暂重连及其余业务验收仍待关闭。

The T5R1 format and size limits are unchanged. Photo, benchmark and OTA RPC callers can pass raw bytes without intermediate Base64/JSON conversions; legacy peers retain JSON fallback. New Wi-Fi TCP connections use TCP_NODELAY, and BLE voice wakes on completion. See the [0.2.68 performance and acceptance record](TRANSPORT-PERFORMANCE-068.md). Deployment, boot verification, 18 benchmark rounds, and BLE-only photo/short voice checks passed. The real photo did not establish a further speed gain; transient USB reconnections and remaining workloads still need acceptance.

## Music artwork .080 candidate / 音乐封面 .080 候选

This extension keeps protocol version 1. On an authenticated firmware version at least
0.2.80, slot 2 may use resource encoding=jpeg, width 1..560, height 1..336, frames=1.
The ID is CRC32 of the packed JPEG, and 1024-byte aligned fragments still use total,
offset, bytes and delays. Other slots and older firmware keep RLE565.

A separate tab5_resources frame contains version, type, deviceId, session, sequence
and resources (1..8 chunk objects). A paired, established same-session snapshot is
required. Fragments do not replace snapshots, renew task freshness or trigger OTA.
Wi-Fi uses at most eight chunks, BLE four; USB remains its bounded dedicated stream.
BLE Info may expose firmware plus firmwareProof=HMAC(FIRMWARE|nonce|version).
The Windows bridge requires the verified proof before enabling this version's codec.

Windows optionally accepts POST /music/artwork on its loopback listener only, with
X-AIBot-Music-Key and an extension origin. This is a separate local browser pairing
key, never the TAB5 key. The companion sends current title/artist/artwork URLs and
optionally the current YouTube video ID; no browser history or cookies. Artwork is
matched exactly to the Windows title and artist. Downloads are bounded HTTPS images
from public hosts, without cookies or redirects; failures retain the last success.

协议版本仍为 1。JPEG 只用于 .080 及更新固件的音乐资源，原小屏和旧 TAB5
资源保持兼容。资源专用帧必须属于已建立的配对会话，不延长任务新鲜度。
本机浏览器接口使用独立授权，只有用户启用的网站会提供当前媒体信息；
不会保存特定视频规则或下载到固件包。候选及真机边界见 MUSIC-ARTWORK-080.md。

### .081 crash capture / 故障现场

`tab5_crash_status` retains its version-1 envelope. The optional `trace.stackGuard`
contains two `[rawStatus, triggerPc, minimumSp, maximumSp]` arrays. `taskName` and
`taskBounds` describe the current fixed task identifier and its saved bounds;
`irqStack` is `[depth, minimumSp, maximumSp]`. `interruptedContext` contains saved
PC/RA/SP candidates; `irqCauseCandidate` is the saved s1 register, not an asserted
interrupt cause or a backtrace. Older readers can ignore these additional fields.

Windows retrieves this trace after a panic boot under the existing USB transaction
gate and keeps eight records in RAM. `GET /diagnostics/tab5-crashes` is loopback-only
and rejects browser origins. No device pairing, snapshot freshness or resource codec
is changed by this capture. See CRASH-080.md for evidence and hardware limits.

新增字段只补充数字故障现场。原始任务位置属于候选线索，不能视为已展开调用栈。
桥接自动回收上次 panic；诊断版未被标记为崩溃修复。
