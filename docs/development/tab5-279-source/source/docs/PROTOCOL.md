# TAB5 协议 1

## TAB5 .177 TF 整库同步 / Complete TF collection synchronization

2026-10-15 增量优化候选：`manifest` 的 `includeGuide:true,guideVersion:3` 可增加 `guideRevision`（当前认证类别摘要）及 `guideWorkId`（设备已有完整导读的作品身份）。桥接仅在当前摘要和作品身份完全一致时返回 `guideUnchanged:true,workId,guide:null`；其他情况返回完整正文。设备先验证缓存日期、版本、作品和完整原文，拒绝错误作品／版本的引用，深拷贝分页正文。旧桥接忽略新字段，旧设备继续完整导读路径；图片 SHA、认证、范围和协议版本不变。

TF 内容更新时，未变 JPEG 不再整文件重写：新增 `<记录槽>.aig.meta` 两槽元数据文件，`AIBART03` 头只包含元数据及图片校验值，JSON 的 `sourceSlot/sourceSerial` 精确引用已有 `AIBART01/02` 图片槽。读取仍验证元数据、源槽身份及 JPEG SHA；图片槽损坏时拒绝引用并回退上一完整记录。图片替换保护当前引用的源槽，元数据使用临时文件、fsync、两槽提交。旧图片文件无需迁移，照片及其他目录不修改。

English: optional exact revision/work guide hints suppress repeated manifest text only when a complete matching guide is already held. Old peers retain full replies. Alternating `AIBART03` `.aig.meta` sidecars rebind unchanged JPEGs without payload writes; source-slot serial, work identity and image SHA remain checked. Existing AIG1/AIG2 records remain readable, and interrupted writes retain a complete survivor.

卡内全部项目数据统一位于 `aibot/`；图库采用 `gallery/<类别>/works/<作品索引键>/<页码>-<方向>.<记录槽>.aig`，进度为类别目录的 `catalog.0/.1`。照片、其他持久数据和可重建缓存分别使用 `photos/`、`data/<模块>/`、`cache/<模块>/`，按需创建。完整归属及兼容规则见 [TF 卡目录约定](TF-STORAGE.md)。

All project writers use the `aibot/` namespace. Gallery records are grouped by category and canonical work key, with category-local alternating progress records; future photos, persistent module data and caches use the documented separate namespaces. See [TF storage layout](TF-STORAGE.md).

卡内全部项目数据统一位于 `aibot/`；图库采用 `gallery/<类别>/works/<作品索引键>/<页码>-<方向>.<记录槽>.aig`，进度为类别目录的 `catalog.0/.1`。照片、其他持久数据和可重建缓存分别使用 `photos/`、`data/<模块>/`、`cache/<模块>/`，按需创建。完整归属及兼容规则见 [TF 卡目录约定](TF-STORAGE.md)。

All project writers use the `aibot/` namespace. Gallery records are grouped by category and canonical work key, with category-local alternating progress records; future photos, persistent module data and caches use the documented separate namespaces. See [TF storage layout](TF-STORAGE.md).

TF 卡保存设备已验证的 JPEG 与完整导读；RPC 请求、认证、图片哈希和二进制分片上限不变。设备使用已有 `connectionHealth.galleryPacks` 校验值匹配卡记录，纯文字更新同样排除旧记录。没有桥接版本信息时可复用最近保存的完整记录，重新连接后以新的认证内容版本为准。卡记录采用自有格式，不将任意卡路径作为远端图库请求参数。

可选 `galleryDiag` 字符串保留前 15 个诊断计数，尾部增加 `tfState,tfHits,tfWrites,tfWriteFailures,paintingDone,paintingTotal,calligraphyDone,calligraphyTotal`；`tfState` 为 0 未探测、1 已挂载、-1 暂不可用／等待重试。字段只记录数字，不包含文件路径、原文、账号或凭据。现有桥接按诊断字符串显示，旧接收端继续兼容；正式 .176 不含此能力。

The local TF cache stores verified JPEGs and complete optional guides without changing RPC/authentication/range contracts. Existing authenticated category digests select records, including text-only revisions; offline reads may reuse the last complete record until a new digest arrives. Optional `galleryDiag` retains its first 15 counters and appends TF mount state, hits, writes and write failures. These numeric diagnostics contain no paths or private content. Public .176 does not include this local change.

Windows .6.6 authenticated state advertises optional `connectionHealth.gallerySyncVersion=1`. Supporting devices can request `{kind:"gallery",op:"catalog",category}` using the existing authenticated RPC. Replies bind `syncVersion=1,works` (1..4096), `revision` (the same category SHA-256 as `galleryPacks`) and `baseDate="2026-01-01"`. Stable canonical dates from that base enumerate each installed work once; normal manifest/read and v2-guide limits remain unchanged. Reject counts/catalogs/revisions that disagree. Old bridges and Mac omit this capability and retain their existing paths. A device stores a checked category count/cursor/revision; canonical date modulo count preserves daily selection across years. Whole-library copying uses USB/Wi-Fi, while BLE retains individual fetches. Advance the durable cursor only after both layouts of all pages in a work are saved; reuse checked partial pages on restart. New category revisions refresh metadata and reuse an old JPEG only after its bytes match the authenticated new manifest hash. Numeric diagnostics append completed/total work counts. Offline use and incomplete updates never imply full hardware acceptance.

## 本地图库内容版本 / Local gallery content revisions

认证状态回复可附带 `data.connectionHealth.galleryPacks`，其中 `painting`、`calligraphy` 分别为当前类别包的64位小写十六进制 SHA-256 内容校验值。纯文字变更也改变该值。设备等活动请求、预填充退出及图像结果消费结束后，清除受影响类别的 JPEG、导读和解码缓存；保留另一类别和旧显示位图，直到新图片就绪。缺失、非法或未变化的值不清缓存。只通过正常认证状态消费，包导入不自动重启设备。

新版包内导读优先于内置导读，图片与文字作为同一个类别整体替换。`includeGuide:true,guideVersion:2` 请求使用完整原文（8192 UTF-8 字节缓冲，含终止字节）；整个清单最多16 KiB，RPC现有32 KiB回复上限及图片分片元数据上限不变。JSON以UTF-8编码并保留必要转义。旧端忽略可选内容版本字段，保持原缓存行为。此能力仅在本地源码和主机测试中验证，未部署到.167。

Authenticated state replies may contain `data.connectionHealth.galleryPacks`, with lowercase 64-character SHA-256 digests for each category, including text-only changes. Once active work and result consumption finish, the reader invalidates only that category's JPEG, guide and decoded caches, preserving the other category and the displayed bitmap until fresh content arrives. Missing, invalid or unchanged digests do nothing. Pack imports do not restart devices. V2 guide requests retain complete originals within the 8192-byte UTF-8 buffer and the 16-KiB manifest budget; RPC authentication, image hashes and binary metadata limits remain unchanged. Legacy devices ignore optional fields. These changes are local and are not installed on .167.

## 本地 .161 USB 拒绝原因

USB 拒收确认的可选 `rejectReason`：1=JSON 解析，2=压缩展开／分配，3=信封，4=身份／未配对，5=序号／会话；缺失或 0 表示旧端。接收语义、鉴权、上限与 version=1 保持不变。配套 Windows 桥接仅对压缩完整状态的 0/1/2 拒收，持锁发送同一原文与序号一次，仍要求正确设备／序号 ACK；其他拒绝、未知代码、原文失败、超时、I/O 与取消保持失败。只有数字和阶段进入诊断，不记录正文。候选本地测试与实机稳定性分开。

English: optional numeric USB reject reasons distinguish parse (1), unpack/allocation (2), envelope (3), identity (4) and sequence/session (5). The matching bridge permits one same-publication raw fallback after packed rejection 0/1/2 only; authenticated ACK checks and all other failure handling remain intact. Content-free diagnostics do not prove hardware acceptance.

## 本地 .160 导读与 USB 诊断

2026-10-10 本地扩展：请求 `includeGuide:true,guideVersion:2` 获得v2完整原文，`transcription` 缓冲扩为含终止字节的8192个UTF-8字节，其余字段上限不变。新端接受v1/v2回复；缺省、非数值或不支持的请求版本保持v1。核实的作品正文与明确标注的通行原文分别登记。旧端遇到超过511字节的正文收到短升级提示，不截断诗文。完整v2清单限制在16 KiB以内，沿用32 KiB RPC回复上限。图片、分页、哈希和认证协议不变；本次新增内容仍在本地，未安装验收。

English: opt-in `guideVersion:2` expands only the original-text buffer to 8192 UTF-8 bytes including NUL. Complete reviewed object bodies and labelled literary references share the reader. v1 receives a short upgrade notice for long texts, without truncation; the new reader accepts both versions. Entire manifests remain below 16 KiB. Local validation is separate from firmware installation and hardware acceptance.

`kind:"gallery",op:"manifest",includeGuide:true` 可返回 `guideVersion:1` 与可空 `guide`，字段为 `workId,title,author,period,museum,brief,overview,looking,transcription,explanation`。桥接按稳定作品身份匹配审定数据，只提供已核实释文；旧请求不返回正文。UTF-8 缓冲（含终止字节）依次为 80/256/128/96/80/256/640/384/512/512。字段过长则拒绝整份导读，图片成功后才发布与分类／日期／作品身份匹配的文字。图片、分页、哈希、认证与协议版本保持原契约。

USB hello/full ACK 的可选 `rpcDiag` 含 USB/Wi-Fi/BLE 三组 `failureCount,lastStage,lastError,replyBytes`；阶段 1–5 为无传输回复、解密认证、信封／JSON 解码、回复状态／正文验证、解码时连接代次改变。-1001 至 -1006 对应边界、未配对、分配、信封、旧代次、无效回复；加密库错误保留原值。`usbDiag` 为 `cdcEpoch,rxBytes,jsonLines,lineOverflows,binaryExpirations,incompleteWrites`。仅数值，不含内容或认证材料；旧端忽略，诊断不能替代真机稳定性验收。

English: .160 adds opt-in reviewed artwork guides bound to stable work identity and numeric USB/RPC failure diagnostics. Unknown or oversized guides are hidden without disturbing the image. Existing gallery bytes, authentication and version 1 remain compatible. The matching Windows service implements this extension; hardware acceptance remains separate.

## .117 三通道压缩升级

USB 与 BLE 批量 OTA 范围均在 `rpcOtaZlib=1` 能力下请求 `acceptEncoding:"zlib"`；48 KiB 原始范围、认证加密、偏移和原文回退不变。Wi-Fi 的认证 GET 同时发送 `X-AIBot-OTA-Flow: tcp-v2` 和 `X-AIBot-OTA-Encoding: aibot-zlib-blocks-v1`，收到同名响应编码才按压缩流读取；旧桥接未返回编码时按原始镜像读取。

每块由四个小端 uint32（原始偏移、解压长度、载荷长度、编码）和载荷组成；编码 0 原文、1 zlib，解压长度除末块外固定 49152。载荷大于 0 且不大于解压长度，zlib 必须更短。HTTP Content-Length 为含分块头的传输长度，offer.size/SHA-256 始终是原始镜像。未知或重复编码头、偏移/长度错误、截断、尾随数据、解压失败均拒绝；整包哈希、镜像身份、备用分区与启动校验保持原流程。

自动 OTA 优先 Wi-Fi、USB、BLE，固定模式不跨通道。界面开放“通过蓝牙升级”，压缩升级同时显示镜像进度和传输数据量；Wi-Fi 计分块头，USB/BLE 计 RPC 载荷，不含加密或底层链路开销。首次安装仍使用 USB 完整包。配套桥接普通 Wi-Fi RPC 以 `firmware_requires_ota_transport` 拒绝固件范围，Wi-Fi OTA 走独立 GET。实现与 Windows 端协议说明同步；完整三通道真机验收单独记录。

## 语音输入开发扩展（2026-09-23）

`POST /tab5/v1/voice` 仅面向已配对设备；body 使用同一 AES-256-GCM 包装，最大 16384 字节。HMAC 输入为 `POST|/tab5/v1/voice|deviceId|nonce|sha256(packet)`；绑定路径避免与文字任务接口交叉使用。成功鉴权后的状态和文字响应也加密，nonce 作为 AAD，不在公共 `/status` 返回语音草稿。

明文 JSON 公共字段：`session`、`issuedAt`（UTC 毫秒，允许 ±15 秒）、`op`。开始请求携带 `taskId`；后续请求携带 `voiceId`。操作为 `start`、`poll`、`audio`、`stop`、`cancel`、`tab5`。音频请求增加 `seq`（从 0 连续递增）及 `pcm`（Base64，16kHz/16bit/单声道，小端，至多 6400 字节）。不自动重试音频包；篡改、重复 nonce、乱序包、旧会话及超限长度会拒绝。

响应包含 `voiceId`、`taskId`、`source`（`dji` / `tab5`）、`state`（`recording` / `draining` / `recognizing` / `review` / `error` / `cancelled`）、`message`、`text`。`text` 至多 2000 UTF-8 字节，仅作为草稿。`stop` 排空音频后结束豆包识别，`cancel` 丢弃本次草稿。电脑 4 秒无请求停止收音，单次最多 60 秒；TAB5 离开任务页取消。

此接口不调用 `turn/start`，用户仍须点击发送。实际音频/豆包/真机验证边界见 [VOICE-INPUT.md](VOICE-INPUT.md)。

2026-10-15 本地候选复用 `op=tab5` 支持从直连大疆手动切换内置麦克风：桥接先结束其拥有的大疆段，等待空闲及文字稳定，再借用内置通道；保持 voiceId、owner、已有文字及总时限。切换准备沿用 recording、ready=false，最终明确停止后返回合并草稿，内置段用完归还原音源。没有新增字段或协议版本。The local candidate reuses op=tab5 for direct-DJI handoff, committing the first segment before borrowing the built-in channel and preserving identity/text. It retains recording/ready=false while preparing, returns the combined draft after explicit stop, and restores the previous input. This remains unflashed and requires real capture/restoration acceptance.

状态：首阶段实现，真机验收结果见 `ACCEPTANCE.md`。Windows 接入源码位于相邻 AI-bot 项目的 `Tab5*.cs`。原 ESP8266 的消息、接口和显示策略不变。

## 身份、传输与界面

- `deviceId`：P4 eFuse MAC 的 12 位小写十六进制，不依赖 COM 号或 BLE 随机地址。
- USB：460800，UTF-8，`@AIBOT ` + 单行 JSON + LF。每行最多 32768 字节，溢出丢弃到下一个 LF。
- `tab5_ping` -> `tab5_hello`（version、deviceId、firmware）。与原小屏 `ping/pong` 明确区分。
- 用户在电脑端选择真实 USB 设备并配对。`tab5_pair` 写入 deviceId、32 字节随机密钥（Base64）、电脑 LAN host/port。`tab5_paired` 确认。
- `tab5_wifi` 仅通过 USB 接收 SSID/password，写入 TAB5 NVS，确认 `tab5_wifi_saved`。电脑不持久化 Wi-Fi 密码。
- `tab5_host` 仅通过 USB 更新 RFC1918 IPv4 和端口；地址变化后可重新接 USB 同步。首次版本不宣称无线自动发现新电脑地址。
- Windows 配对文件使用当前用户 DPAPI。当前支持一台 TAB5 与一台原小屏共存。

## 业务帧

`tab5_status` 包含 version=1、deviceId、session（桥接启动随机标识）、sequence（每次公共快照递增）、data。
data 包含 time、epochUtc、utcOffsetSeconds、capturedAt、codex、claude、quotas、weather、stocks、systemMetrics、music。
缺失值保持 null，供应商 stale 标识保留；不携带原小屏 displayPolicy、账户凭据、对话正文。
两种设备共用 BridgeRuntime 的数据服务；TAB5 不额外请求供应商接口。
0.2.0-dev：股票最多 20 条，系统指标携带最近 16 个采样；USB、Wi-Fi、BLE 同源快照具有相同 session/sequence。
新增 `quotaDetails`（十个平台的统一额度、余额、重置时间和本机 Token）、`domesticActivity`、`followApp`、`calendar`（本地日期、星期、当前/次日农历）。这些字段与原小屏独立。
`quotas` 仅保留总览需要的百分比和 stale；平台详细数值统一放在 `quotaDetails`，不重复发送完整 domesticQuotas 和本机活动日志字典。

0.2.32-ui：`weather.hourly` 最多 24 项，包含 `time`（预报地点本地 ISO 时间）、`condition`、`temperature`、`rainProbability`（0–100，未知为 null）；`weather.daily` 最多 7 项，包含 `date`、`condition`、`low`、`high`、`rainProbability`、`precipitationMm`。降水量与概率分别展示，不能互相转换。`hourlyStale`/`dailyStale` 标记分项缓存；同一地区/供应商的小时请求失败时保留前一次小时数据，不清空已经成功获取的每日/实况数据。旧桥接缺少数组时，TAB5 显示“暂无预报”。公共 ESP8266 小屏帧保持原结构。

数据仍由公共电脑桥接按原 15 分钟周期统一获取。和风城市预报使用 `/v7/weather/24h` 与 `/v7/weather/7d`；Open-Meteo 备用接口请求 7 天并提取当前小时起的 24 小时。日期以供应商预报地点的时区为准，不按电脑所在地重新解释。官方字段：[和风小时预报](https://dev.qweather.com/docs/api/weather/weather-hourly-forecast-webapi-v7/)、[和风每日预报](https://dev.qweather.com/docs/api/weather/weather-daily-forecast-webapi-v7/)、[Open-Meteo](https://open-meteo.com/en/docs)。

2026-10-11 本地开发扩展：`weather.daily` 可选 `sunrise`／`sunset` 为预报城市本地 `HH:mm`，`daylightSeconds` 为 0–86400 秒或 null。设备按 `weather.utcOffsetSeconds` 匹配城市当天日期，将事件时刻换算到主时钟时区以判断当前昼夜及白昼进度；独立事件标签仍显示城市当地时间。当日有效缓存标注“缓存”，当天不可用或时间非法时使用默认 06:00／18:00 并标注“默认”。明确的 0／86400 秒识别极夜／极昼，不根据缺字段推断，也不虚构事件时间。旧桥接与旧缓存兼容默认回退；三通道共用可选字段，小屏协议不变。Windows 复用现有请求，扩展尚未部署到真机。

The development solar extension uses city-local `sunrise`/`sunset` and nullable `daylightSeconds`, matching the city date before projecting event times onto the existing clock timezone for day/night and daylight progress. Separate labels remain city-local. Same-day cached values remain usable; missing/invalid data falls back to labelled 06:00/18:00 defaults. Polar states require explicit duration endpoints and do not invent events. TAB5 USB/Wi-Fi/BLE share the fields; legacy frames and authentication are unchanged. This extension is locally validated, not hardware accepted.

USB 正确接收后返回 `tab5_ack`，包含 deviceId 和 sequence。重复帧可以确认，但不得延长有效心跳。
TAB5 原生 USB Serial/JTAG 在此 Windows 设备上需要 DTR=true、RTS=false；此设置仅用于 TAB5，原 ESP8266 串口配置不变。
每条链路独立记录最新序号与接收时间；自动选择 USB > Wi-Fi > BLE，8 秒无新状态失效。
仅 USB/仅 Wi-Fi/仅 BLE 模式不自动换到其他通道。切换保留 UI 页面，离线保留缓存并标记，时钟继续走时。

## Wi-Fi

`GET /tab5/v1/status HTTP/1.1`，与原 `/status` 并存。
请求头：X-AIBot-Device、X-AIBot-Nonce（随机 16 字节的小写 hex）、X-AIBot-Proof。
proof = hex(HMAC-SHA256(deviceKey, UTF8(`GET|deviceId|nonce`)))。
响应为 AES-256-GCM：随机 IV[12] + tag[16] + ciphertext，AAD 为 nonce 的 32 字节 ASCII。
密钥不经 HTTP 发送，状态负载加密；拒绝重定向，限制响应 32768 字节。
认证失败、未配对或电脑公共快照已超过 6 秒未更新均不返回状态。

## BLE

- Service：`7af50001-7f23-4a91-bc65-667a19320101`
- Info (read)：`7af50002-7f23-4a91-bc65-667a19320101`
- Input (write with response)：`7af50003-7f23-4a91-bc65-667a19320101`

每次连接创建随机 nonce。Info 返回 deviceId、nonce、ack 和 HMAC(key, ASCII(`nonce|ack`))，ack 初始为 0。
电脑只向通过配对密钥验证的设备发送数据；输入使用同一 AES-GCM 格式，AAD 为当前连接 nonce。
BLE 数据前加 uint32 little-endian 包长，再按 ATT MTU 分片写入（最少 20 字节）。最大包 32768 字节，片段间隔超过 5 秒丢弃未完成包。
完整帧解密并处理后，Info 的 ack 更新为序号。电脑读取并校验确认，不能把 GATT 写入成功直接当作业务接收成功。
USB 首次配对及应用层加密不依赖传统蓝牙串口，也不向 BLE 发送配对密钥。

## 图片资源（0.2.0-dev）

`data.resource` 为可选的一个图片分片，`data.assetIds` 为当前三个资源的标识，空值表示该槽位没有资源。
槽位 0/1 是 Claude/Codex 桌宠，最大尺寸 120×120、1–8 帧。槽位 2 是音乐封面；自 0.2.11-ui 起支持最大 336×336、单帧，与界面显示尺寸一致；仍接受旧桥接的 112×112 封面。公共桥接为 ESP8266 保留独立的 112×112 封面。

- 字段：`slot`、`id`、`width`、`height`、`frames`、`delays`、`total`、`offset`、`bytes`。
- RGB565 little-endian，所有帧顺序拼接。RLE16 编码：每组 uint16 little-endian 重复次数 + uint16 像素值；禁止零次和超出预期像素数。
- `id` 为压缩字节 CRC32 的八位小写十六进制；无线传输的真实性仍由外层 AES-GCM 保证。
- `bytes` 为 Base64，每片最多 1024 个压缩字节；`offset` 按 1024 对齐，末片可不足。压缩总长上限 524288。
- 只发送设备缺失或 ID 已变化的资源。待传分片每轮打乱次序，设备支持乱序和重复分片；收齐、CRC 与精确像素数均通过才替换旧资源。
- USB 可另发 `tab5_resource`（version、deviceId、resource），返回 `tab5_resource_ack`。每轮最多 16 片，给普通心跳留出时间；这类消息不写配对 NVS。
- 显示资源 ID 必须与 `assetIds` 一致；更换曲目时不会暂用旧封面。音乐封面只缓存 RAM，同一封面不重复传送。
- 桌宠持久化到独立 `assets` 分区（0xb00000、2 MiB）。每个角色两份 512 KiB 交替保存；校验资源和元数据，先写数据、最后提交头部。后台写入，不在接收回调中擦写闪存。启动时选取最新有效副本，缓存无效时重新同步。此区域部署前已只读验证全为擦除态，原应用和配对 NVS 地址不变。
- USB ACK 的 `assetIds` 是三个 ID 或 `-` 组成的逗号分隔字符串。Wi-Fi 使用 `X-AIBot-Assets` 和 `X-AIBot-Assets-Proof`；BLE Info 使用 `assetIds` 和 `assetProof`。资源报告 proof = hex(HMAC-SHA256(key, ASCII(`ASSETS|nonce|ids`)))。未通过认证的报告不会使桥接停止发送。
- 私有桌宠/封面不进入固件或发布包。设备 ACK 可报告 `assetsReady`、`assetsCached` 位图、`uiPage`、`uptimeMs`、`resetReason`。缓存命中后状态帧的 `resource=null`；桥接重启也不重新批量传送资源。

## 能力边界

### 0.2.1 显示补充

`calendar` 保留原有日期、星期、农历和次日字段，新增 `solarTermLabel`、`holidayLabel`、`nextSolarTermLabel`、`nextHolidayLabel`。TAB5 跨零点使用匹配日期的字段，超过已收到的次日后提示待更新。

电脑本地保存节气日期表（香港天文台 2026–2028）和中国大陆 2026 放假调休表，正常运行不访问日历网站。年度未覆盖时明确提示待更新。数据来源见 `UI-DESIGN.md`。

三款时钟布局和全部图表由 TAB5 绘制，不新增图片资源传输。表盘选择保存为独立 NVS `clock_style` 键，不改变原有 `ui` blob 格式。

首阶段是状态显示，没有远程执行电脑命令、语音对话、固件 OTA。新增能力使用独立消息及能力协商，不能把原小屏设置写入 TAB5。

## 额度显示扩展（0.2.2-dev）

- `quotaDetails[].creditExpiries`：逐卡到期时间数组，按设备时区格式化为 `yyyy-MM-dd HH:mm`；保留同日多张卡。`credits` 为可用张数，日期缺失不猜测。
- Codex `primary`/`primaryReset` 及总览 `primaryPercent` 仅向 PLUS 套餐映射，PRO 不展示 5 小时限制；原小屏协议未改。
- `quotaTrend`：`provider=codex`，`unit=weekly_percentage_points`，`timezone=Asia/Shanghai`，`days` 为近 7 个自然日的 `{date,label,used,partial}`。`used=null` 表示没有可比较采样；`partial=true` 表示当天未结束或有缺口/重置等，不当作完整全天消耗。`storageWarning` 提示桥接历史存储异常。
- 聚合复用公共桥接 QuotaHistory，每 30 秒刷新一次，不另建历史文件，不发送账户指纹。
- 表盘点击循环仍使用原 `clock_style` NVS 键，已有选择保留。

## 0.2.3-dev 补充

- `quotaDetails[].billingMode` 为 `payg` 或 `subscription`。按量付费显示 `balance`、`cost`、`currency`，`cost` 是数据源提供的账户累计消费，未知为 null；Coding Plan 使用周期额度。`planExpiry` 仅由明确的套餐到期字段填充，未知为 `--`，不得由 5 小时、周额度或套餐重置时间推算。旧消息缺失该字段时保持订阅布局。
- `epochMilliseconds` 为电脑状态采集时的 UTC 毫秒时间；旧固件可忽略，新固件缺失该值时回退 `epochUtc`。TAB5 用单调时钟持续走时，普通心跳的两秒内差异不重置相位，较大校时重新同步。
- 公共桥接每分钟记录额度，成功即保存，不受重置卡请求失败影响。同日同账户同周期断档差值保留并标记部分记录。按用户约定，五分钟内跨零点增量计入后一次采样的北京时间日期；跨天长断档不摊派。

## 0.2.4-dev 天气与本地设置

- `weather.pressureHpa`：可空气压数值；`pressureKind=station` 为和风站点气压，`sea_level` 为 Open-Meteo 海平面气压。缺失不是 0。
- `weather.airQualityIndex` 贯通实际采集值，`airQualityStandard` 标识 `cn-mee`、`us-epa` 或 `qaqi`，界面同时保留类别文字。
- `clock_style` 仍为独立字节，范围扩展到 0–4，原 2 对应的新布局为轨道圆盘。总览独立字节 `home_models` 三位分别对应 Codex/DeepSeek/智谱，默认 7，至少选一项。不改变旧 `ui` blob 或原小屏配置。
- 屏保只影响本机显示与背光，触摸恢复原页面/亮度，数据采集继续。

## 0.2.5-dev 电脑输入活动

TAB5 data.pcInput 包含 session、sequence、lastInputTickMs（Windows 开机单调毫秒时间戳，可为空）。桥接只读取 GetLastInputInfo 的活动时间，不采集按键内容、鼠标坐标。USB/Wi-Fi/BLE 使用同一状态帧。

设备首次见到会话建立基准；同会话仅接收递增序号，只有 lastInputTickMs 前进才重置无操作计时并退出屏保。重复或较旧序号、缺失值、未变化的心跳不唤醒。桥接重启重新建立基准。没有新桥接字段时仍保留触摸屏保与唤醒。

USB 诊断新增 saverActive（0/1）、pcWakeCount（本次开机电脑输入唤醒次数），便于分别核对数据通道和实际屏保行为。

## 0.2.6-dev 音乐时间线有效性

music.timelineAvailable=false 表示当前没有有效播放进度；durationSeconds 仍可携带本地匹配歌曲的总时长。TAB5 显示 --:-- / 总时长，隐藏比例进度且不自行累加播放位置。旧桥接缺少字段时沿用既有行为。旧 ESP8266 USB 帧在此状态下继续保持 0/0，避免将已知总时长误当作已知播放进度。

## Codex 文字任务入口（开发中）

状态帧可增加 `data.codexTasks={projects:[{id,name}],tasks:[{id,title,folder,updatedAt,projectId}],receipts:{taskId:statusText}}`。项目仅从 Codex 侧栏已保存的项目根目录读取；项目 ID 为保存项目 ID 的 SHA-256 前 16 位小写十六进制，最多 80 条会话，每项目最多 8 条；用户先选项目，再显示对应会话。Codex 管理的同名 worktree 会话归入其保存项目。旧桥接只有 `tasks` 时设备显示“最近任务”。资源帧达到上限时先延后资源分片，必要时减少会话数量并保留当前查看任务与各项目入口，继续遵守 32768 字节限制。

回复扩展：`responses:{taskId:text}` 当前只携带一份正文；`replyView:{taskId,turnId,page,pageCount}` 标识当前动态段；`repliesReady:{taskId:turnId}` 标记 TAB5 提交轮次的完成回复。`POST /tab5/v1/codex/read` 沿用任务加密鉴权，请求体 `{op:"read",session,taskId,message:"",issuedAt,page:-1}`，page=-1 表示最新段，非负整数从 0 开始。每段不超过 2200 UTF-8 字节；老固件省略 page 时读取最新段。返回 200 后正文随状态帧同步。读取不会发送或续写任务。设备每 3 秒最多一个只读请求，历史翻页不会重复发出完成提醒。

新增 `POST /tab5/v1/codex/turn`，请求体最多 8192 字节，使用原 AES-256-GCM 封包布局及 nonce AAD。解密 JSON 为 `{session,taskId,message,issuedAt}`；`issuedAt` 为来自电脑状态帧时钟的 Unix 毫秒时间。

请求头沿用 `X-AIBot-Device`、`X-AIBot-Nonce`、`X-AIBot-Proof`。Proof 是配对密钥对 `POST|deviceId|nonce|sha256(ciphertext_packet)` 的 HMAC-SHA256（小写 hex）。电脑校验当前桥接 session、消息时间（过去 90 秒至未来 10 秒）、nonce 重放与每秒最多 1 次提交。1024 项 nonce 窗口覆盖整个有效时间段。返回不包含原始输入的 JSON：202 已提交；400 参数/过期会话；401 未授权；404 任务不存在；409 重复/任务忙/状态未知；429 过快；503 Codex 不可用；504 提交结果不确定。

设备不自动重发任务。只在具有有效轮次 ID 的 202 后清空草稿；提交后的断线、超时或不完整回执返回 504，提示先在电脑查看。语音使用独立的 `/tab5/v1/voice` 扩展（见本文语音章节及 [双音源语音](VOICE-INPUT.md)）；尚无照片或通用电脑键鼠注入协议。具体完成边界见 [Codex 任务入口](CODEX-REMOTE.md)。

2026-09-26 恢复与运行中操作扩展：

| op | 路由 | 新增字段 | 成功含义 |
| --- | --- | --- | --- |
| send | turn | requestId（旧固件可省略） | 202，桌面已接收 |
| steer | turn | requestId、expectedTurn | 202，桌面已接收追加 |
| interrupt | turn | requestId、expectedTurn，message 可空 | 202，停止请求已接受；不清草稿 |
| draft-save | turn | requestId，message 为草稿 | 200，草稿已落盘 |
| draft-load | read | requestId | 200，draftView 随后下发 |
| receipt | read | 原 requestId | 202 已接收；504 仍未确认；404 没有记录不等于发送失败 |

所有操作保留 session、taskId、issuedAt、message 字段及原加密鉴权。草稿、读取、显式操作各自每秒一次限速；错误路由和非字符串 op 拒绝。固定请求编号与正文摘要绑定，跨重启只能核对同一请求，不重新派发。摘要包括 taskId、message、op、expectedTurn。

`codexTasks.control={taskId,turnId,running,waiting}` 用于运行中按钮；`draftView={taskId,requestId,text,pendingRequest,operation,expectedTurn}` 只含最近请求的一份草稿，设备核对会话、请求编号及本机编辑版本后再应用。空白且尚未加载的输入框不能被当成清空指令上传。

## OTA 扩展

用户在电脑桥接窗口明确选择本项目应用镜像后，已加密状态帧的 `data.ota` 为 `{sha256,size,version,offerId}`；未选择或撤销时为 `null`。`offerId` 是每次手动选择生成的 32 位小写十六进制编号，设备对每个编号只在用户确认后尝试一次；重新选择同一镜像可发起新一次尝试。TAB5 接受已鉴权状态帧并由用户确认后请求 `GET /tab5/v1/ota/{sha256}`。头 `X-AIBot-Device`、`X-AIBot-Nonce`、`X-AIBot-Proof` 中的 Proof 为配对密钥对 `GET|/tab5/v1/ota/{sha256}|deviceId|nonce` 的 HMAC-SHA256。只有与当前准备镜像的 SHA 一致且鉴权成功才返回完整应用二进制；镜像本身由设备计算 SHA-256 并与状态帧比对。首次双分区迁移和失败回滚见 [OTA.md](OTA.md)。
可选头 `X-AIBot-OTA-Flow: tcp-v2` 表示接收端具备 16 KiB 合并写入及 P4 SDIO 包池修复；仅在原有鉴权成功后，桥接以 16 KiB 有界异步写入并依赖 TCP 背压。无此头、`tcp-v1` 或未知值采用 2 KiB/64 ms 兼容传输。该头只选择流控方式，不承担鉴权；不能用新版目标镜像或旧接收端 USB 心跳替代能力声明。

`data.ota.notes` 为可选纯文本更新说明，最多 1024 UTF-8 字节。桥接从所选镜像同路径的 `<文件名>.bin.notes.json` 读取 `{version,sha256,notes}`，版本及 SHA 必须匹配镜像，错误时不提供该包；旧包无说明文件仍可选择。TAB5 0.2.30 起在升级页第一行显示新版本及可用状态，下方展示可滚动说明；字段缺失或超长时显示「暂无更新说明」。旧设备忽略新增字段，取消提供会清除版本和说明。

# 2026-09-26 语音就绪字段

`/tab5/v1/voice` 加密响应增加布尔 `ready`，默认为 `false`，只在 `state=recording` 且快捷键已确认、音频输入和虚拟输出就绪时为 `true`。TAB5 路径需至少一段 PCM 被接收；大疆路径需发生真实采集回调。停止、失败、取消均返回 `false`。

固件还检查本机首段音频请求成功、无停止命令、无采集错误，才显示就绪和启动计时。连接/准备期间不展示录音计时。该字段不声明豆包内部识别服务状态。需要配套桥接；旧桥接缺失字段时不会误发就绪提示。

语音响应现增加 `level`（0–100，-60 至 0 dBFS 显示刻度）与 `silent`（音频通路就绪后连续约 6 秒未检测到声音）。数据来自实际选择的大疆或 TAB5 输入，不保存录音；静音只提示，不自行结束。停止或未就绪时电平为 0。

## 照片附件

独立 `/tab5/v1/codex/image` 加密上传与 `images` 草稿/发送字段见 [PHOTO-ATTACHMENTS.md](PHOTO-ATTACHMENTS.md)。图片先暂存到电脑，用户明确点击发送后才作为真正的 `localImage` 与文字一起进入所选 Codex 会话。

# 0.2.21 Wi-Fi 备注扩展

USB `tab5_wifi` 可附加可选 `label`（最多 48 UTF-8 字节、无控制字符），缺省保留已有备注。USB `tab5_wifi_label` 携带 `version=1`、已配对 `deviceId`、真实 `ssid` 与 `label`，仅更新已保存网络的备注，成功回执为 `tab5_wifi_label_saved`；空备注表示清除。未知 SSID 不创建网络，不修改密码或当前连接。

备注命令与配网一样仅由 USB 配置路径接受。v2 保存格式位于独立 NVS 键 `wifi_profiles2`，兼容读取旧网络列表，配对结构与状态协议版本不变。

# 0.2.22 USB 网络管理扩展

电脑先验证配对设备身份，再发送 `version=1,type=tab5_wifi_list,deviceId`。返回同名类型、版本、设备 ID 和 `networks` 数组，最多 10 项（216 起），每项只包含 `ssid,label,connected,selected`，不返回密码或密钥。

删除命令为 `version=1,type=tab5_wifi_forget,deviceId,ssid`，按真实 SSID 定位。成功返回 `tab5_wifi_forgotten`；设备忙、网络不存在或持久化失败返回 `tab5_wifi_rejected`。删除在显示锁内检查录音、拍照、发送及升级状态，提交与设备端删除共用的网络存储方法。修改备注和密码继续复用 `tab5_wifi_label`、`tab5_wifi`。

以上命令仅由 USB 接收，不扩大无线配置权限。


## TAB5 0.2.26 回复内容标识

`codexTasks.replyView.hasText` 和加密读取回执内的 `replyView.hasText` 是可选布尔字段，表示当前分页有有效公开文字，false 表示等待/空内容占位。新版设备只在对应轮次的有效正文显示后清除未读，历史固定查看也适用；空占位继续重试。旧桥接缺少字段时设备兼容已知占位文字，旧设备忽略新增字段。读取轮询仍为每 3 秒，运行中的文字优先取桌面 IPC 公开消息。
# 0.2.65 三通道扩展（未部署）

状态增量、USB 二进制邮箱、Wi-Fi 二进制图片、USB 语音、BLE OTA、窗口协商与测速协议见 [TRANSPORT-INTEGRATED-065.md](TRANSPORT-INTEGRATED-065.md)。旧端按能力兼容，固定模式不可跨路。构建与协议回归不等于真机性能通过。

## 0.2.70 有界大数据传输

已认证的对应通道状态同时包含 `rpcBinary=1,rpcBulk=1` 时启用 `T5R2`：4 字节魔数、4 字节小端元数据长度、2..4096 字节 JSON、1..49152 字节原始数据。请求设置 `binaryReply:true,bulkReply:true`；T5R1 的 8192 字节上传、16384 字节回复边界不变。邮箱总长最多 65535 字节（请求含 nonce32/proof64），HTTP 加密请求最多 65439 字节；认证、时效、防重放、偏移与哈希检查不变。只允许照片与测速请求带原始数据，OTA/测速批量回复可增大；不确定失败不跨通道重试。

USB 二进制线缆片段仍为 16 KiB，逻辑大块拆成多个原有片段。USB hello/ack 的 `usbPacked=1` 允许压缩完整状态，展开仍限制 32768 字节，成功回复 `tab5_ack`，指标仍是原格式；拒绝分别回复 `tab5_ack_rejected`/`tab5_metrics_ack_rejected`。不保留分发器与接收器两棵完整 JSON 树。

BLE 通知与响应窗口仍最多 8 片、ATT 有效载荷最多 488 字节；电脑锁在一次读/授权/响应窗口后释放，窗口内顺序写，末片确认。独立蓝牙语音缓冲和时限保持。仅 USB 语音界面已开放既有后端能力；真实识别待验收。

0.2.71 修正桥接保持原整窗口收齐后授权下一窗口的顺序；提前授权实验真机失败，已撤回。大 RPC 接收期间状态刷新最多延后 3 秒，语音轮询独立；本地诊断记录通知数与时间，不记录载荷。固件仅在批量传输时请求 7.5 ms 连接间隔，实际取值由电脑协商并在 link 诊断回报，空闲恢复原间隔。

每方向三轮 256 KiB RAM 基准，协商新端每轮 6 个 RPC、旧端 32 个，按相同总有效字节比较。详见 [0.2.70 实现与验证](TRANSPORT-PERFORMANCE-070.md)；构建与模拟不是设备验收。

071c 本地诊断候选：仅设置进程环境 AIBOT_TAB5_BLE_THROUGHPUT=1 时，在已认证 BLE 连接内尝试 Windows 11 ThroughputOptimized；空闲 2 秒释放并恢复 Balanced，失败保留原通道行为。pcPreference 仅报告请求生命周期，不能作为吞吐验收结论。USB 启动核验通过后读取现有 tab5_crash_status 数值诊断，将 previousOtaMs=[总接收写入,读,写] 保存到本机诊断；读写并行，后两项不相加。未修改线上消息格式。

0.2.73 候选：撤回 071c 的 Windows 吞吐偏好实验，保留数值 OTA 核验诊断。新增 rpcMailboxWindow=32 表示 RPC 支持最多 32 片完整窗口；mailboxWindow=8 仍用于语音。新桥接在字段缺失时沿用旧窗口，旧桥接继续最多 8 片。命令 3 的 credit 仍有上限，语音端拒绝超过 8，RPC 端拒绝超过 32；每批完整接收后才授权下一批。mailboxStats=[已提交通知,分配失败,提交失败,全局空闲 mbuf 最低值]，最低值 65535 表示尚无通知。
073b 修正：.073 真机 32 片 RPC 测速发生通知提交失败；桥接将 rpcMailboxWindow 的实际协商上限限制为 8，即使固件声明 32。固件仍接受旧的 8 片请求。当前恢复方案等待真机复测，不将 32 片作为已验收能力。
0.2.74 候选新增 rpcNotifyWindow=32，设备在 host callout 按空闲 MSYS 容量分段提交授权通知；rpcMailboxWindow=8 仍限制电脑回写。通知和回写独立协商，新字段缺失时通知沿用最多 8。rpcPacing=[yield次数,终止次数,最近通知错误码]。单窗口最多 3 秒，断开/错误终止，不重发可能已部分发送的异常通知。等待真机验收。

0.2.112-ui 身份新增可选 `rpcWriteWindow=32`，仅协商电脑到设备的大 RPC 回复（超过 12288 字节）的回写确认间隔，与 `mailboxWindow=8` 和 `rpcNotifyWindow=32` 独立。新桥接只接受 8..32 整数，缺失/无效沿用旧窗口；旧桥接忽略字段。电脑按游标顺序分组，每组最多挂起七个原生无响应写入，全部完成后才提交下一组；窗口末片仍使用 ATT 确认。错误或取消先排空已提交操作，不继续提交、不自动重放。语音、小消息、仅支持有响应写入的对端、通知节流和接收缓冲/鉴权均不变。身份最大字段组合恰为 512 字节，不能再直接加字段。本地验证不能代替实际 RAM 预检；整包 120 秒目标仍待验收。
# TAB5 0.2.75 显示诊断扩展

`displayDiag` 保留原 52 列，追加索引 52～55：完整普通页面快照数、完整预旋转页面快照数、分片重建被更新打断的累计次数、当前 PSRAM 空闲字节数。计数描述已发布的完整图片，可能是手势允许复用的旧内容；不能把它解释为所有页面都已更新至最新状态。桥接沿用字符串透传，不改变状态协议版本或控制命令。

0.2.77 诊断候选保留前 56 列，追加 `displayDiag[56..74]`：schema（1）、上次 RTC 记录有效位、CPU0/CPU1 ROM 原始复位值，然后依次为 UI、刷新、LCD 提交/等待、缓存、Flash 五组 `(stage, atUs32, detail)`。同样的 19 个数字通过 `tab5_crash_status` 的 `trace.previousRuntime` 返回。UI/缓存/Flash 的阶段 1 为进入、0 为完成；刷新 1/2/3/0 为开始/渲染开始/渲染完成/刷新完成；LCD 1/2/3/0 为提交开始/提交返回/等待开始/等待完成。detail 为页面编号，Flash 为 0。各时间为上次运行低 32 位微秒，约 71.6 分钟回绕；独立通道是尽力保存的进度线索，不是调用栈，提交返回也不等于屏幕已完成传输。桥接继续透传字符串，老固件可以缺少尾部字段。

TAB5 .078 accepts optional `music.artworkWidth` / `artworkHeight`, the original artwork dimensions supplied to the bridge. Valid dimensions (1..32768) with aspect ratio above 1.25 select a wider landscape presentation; missing/invalid dimensions retain the square layout. The resource remains a 336×336 RGB565 aspect-fit canvas, so these fields describe source aspect ratio rather than resource dimensions. Windows strips them from legacy ESP8266 USB frames; older TAB5 firmware ignores them. No protocol-version or resource-format change is required.

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

## 0.2.92 USB OTA extension

The 0.2.94 receiver later changes buffering and erase scheduling without changing this wire format: two owned 48 KiB receive blocks match individual bulk ranges, internal staging prefers 48/16/8 KiB with the same 96 KiB reserve, and `esp_ota_begin` receives the validated image size to prepare the required backup-partition extent before reception. SDK rollback/running-partition guards and SHA/image/boot checks remain. OTA total/flash timing includes preparation erase. Physical performance acceptance is pending.

USB hello/ack advertises `rpcUsbBinaryChunk:65526` alongside legacy `rpcUsbChunk:16384`. Large fragments require both the exact new value and `rpcUsbBinary:1`. Binary commands accept `chunk` 2048/16384/65526, default to 16384 when absent, and bound write/read bodies to chunk+9/chunk+8; AIB2 and the 65535-byte mailbox are unchanged. A 48 KiB bulk reply fits one write. Partial binary sends poison the stream until CDC close/open (DTR edge) or physical reconnect cancels the old mailbox and discards partial framing. OTA-only reads wait ten seconds and retry a missing response at most twice at the same offer/hash/offset/count with fresh authentication; permanent errors and malformed replies stop. Other operations retain their original behavior. Flash staging falls back from 64 KiB to 16/8 KiB internal buffers while retaining 96 KiB free. All SHA, identity and boot checks remain. Hardware acceptance is pending.

新 USB 大块能力须明确协商，旧协议继续兼容；仅对同一固件数据块做有限重试。串口关闭、重开后恢复帧边界，写入小缓冲保留原校验和启动保护。编译和故障模拟不代表真机通过。

## 0.2.90 desktop quick controls

Authenticated `kind=desktop` RPC adds `open-recent` (requestId, optional taskId) and `stage-draft` (requestId, explicit taskId, text up to 2000 UTF-8 bytes). Replies acknowledge foreground or staged draft, never submission. With the .100 paired bridge, staging appends to existing desktop text; repeated request IDs do not append again. Quick Console recognition updates its preview only and never writes into the TAB5 composer. Specific errors distinguish changed, oversized or unconfirmed desktop drafts. Voice replies add `canInsert` for an explicitly stopped, complete and untruncated result. USB / Wi-Fi / BLE use the existing envelope; no automatic cross-link retry. See [bridge protocol and acceptance limits](../../AI-bot/docs/TAB5-QUICK-CONSOLE.md). Windows only; existing clients may ignore the additive field.

The .100 paired bridge also includes optional `sourceName`: the full friendly name of the successfully opened Windows capture endpoint, or `TAB5 内置麦克风` after fallback. The routing field `source` remains `dji` / `tab5`. Quick Console displays the name and scrolls long status text; missing names retain the original message without inventing a model. Older clients can ignore this additive field.

In 0.2.91-ui, the direct button omits taskId to resolve the latest conversation on each new click. Replies add folder and updatedAt; the panel and voice bind to the returned taskId. Replays retain the original resolution. Staging requires an explicit target and never redirects a missing target. 直达按钮每次重新解析最新会话，回执同步实际目标；语音仍绑定明确会话并保持未发送草稿。

From 0.2.106-ui, Previous/Next and the Codex key supply the displayed target taskId to the existing open-recent operation. Arrows dispatch navigation immediately and commit the panel selection only after a matching successful foreground reply. List order remains stable until the panel is reopened; reopening defaults to the latest catalog entry. Failure or a wrong-target reply retains the prior panel selection and blocks voice/input until navigation is confirmed again. Reopening the same confirmed target retains its unsubmitted staging receipt. Wire format and protocol version remain unchanged; existing Windows target-selection support is used. 0.2.106-ui 起，上一条/下一条同步切换电脑，Codex 按钮打开当前选中项；成功确认后更新面板，失败不改选中项，未确认目标时不录入；重新进入才按最新目录重建列表。

During an OTA transfer the matching 0.2.96 desktop bridge suspends high-rate USB metrics, inline artwork and resource bursts. Full status heartbeats still renew the established session and clock; authentication, USB failover and boot checks are unchanged. Pending artwork resumes after the installed offered version is observed or the bounded transfer lease expires. USB diagnostics report cumulative and peak serial-gate wait in milliseconds. This is scheduling policy, not a wire-format change; physical effect requires a complete USB upgrade. Retained otaMs/previousOtaMs describe preparation through the end of receive/write, including preparation erase since 0.2.94; they exclude final image validation, completion dwell and reboot, so they are not end-to-end wall time.


### 0.2.97 OTA preparation and display continuity

USB hello/ack optionally adds `otaDiag:"prepareMs,bufferSlots,lcdUnderrunsDuringUpgrade,busy"`. Values describe the current boot/attempt and reset on reboot; capture them before restart. Old bridges ignore the field and new bridges display `--` when absent. Protocol version remains 1.

The receiver authenticates and fills a first owned 48 KiB block, then waits for the preparation overlay to render before calling the SDK known-size backup-partition erase. Reception continues through a bounded PSRAM pool, retaining 4 MiB for UI/transport when expanding beyond the original two-block fallback. ESP32-P4 PSRAM XIP keeps the instruction/rodata cache available during flash operations. SDK rollback/running-partition guards, ownership/join rules, final SHA/image/boot checks and user confirmation remain. An initial read, UI preparation or receiver-start failure causes no flash write/erase or boot-partition change.

首轮安装 0.2.97-ui 仍由旧接收程序执行；须在重启后的下一次真实升级测量新流程。构建及故障模拟通过不代表开头停滞或蓝闪已通过真机验收。
## OTA phase timing / OTA 分阶段计时

0.2.103-ui 的 USB `tab5_crash_diagnostic.trace` 增加可选 `otaPhases` / `previousOtaPhases`，不可用时为 `null`。数值对象包含 `transport`（-1 无、0 USB、1 Wi-Fi、2 BLE）、`prepareMs`、`writeMs`、`freeWaitMs`、`readyWaitMs`、`verifyMs`、`installMs`。分区准备与纯写入分开；两种等待分别表示接收端等待空闲缓冲、写入端等待已接收缓冲；校验包含最终镜像检查、身份和启动分区选择。安装耗时从升级 worker 开始到选择启动分区或失败，不含成功画面停留、重启和健康启动。接收与写入并行，数值不能相加；旧三项计时字段语义保留。首轮安装仍用旧接收程序，后续一次升级才测到新计时。

Optional `otaPhases` and `previousOtaPhases` retain numeric OTA phase timing across reboot; absent records are null. Preparation and pure writing are separate, free/ready waits identify buffer backpressure and data waits, and install time excludes completion dwell/reboot/healthy boot. Concurrent phases must not be summed. The existing timing, authentication, integrity and rollback semantics remain; the first installation still runs the old receiver. Full field definitions match the adjacent AI-bot protocol.

0.2.107-ui 仅调整本地升级反馈：进度按成功写入闪存的字节计算（校验通过前最多 99），不再按网络接收量推进。下载完成后的剩余写入明确显示；最终 SHA/镜像/身份/启动检查单独显示“正在校验固件”与实时耗时，不附带 99% 文案。成功后才显示 100% 并等待原重启流程。协议、传输参数、完整性校验、回滚和既有分阶段计时语义不变；首轮装入 .107 仍使用旧接收程序，下一次升级才应用新反馈，不声明实际提速。

The .107 local progress display follows successfully written flash bytes, capped at 99 before verification. Explicitly show pending writes after completed reception and a timed verification stage without a stuck 99% label. Show 100 only after all existing checks and boot-partition selection succeed. Transport, protocol, integrity, rollback and phase-timing semantics are unchanged. Installing .107 still uses the previous receiver; the new feedback applies to the following upgrade and does not claim faster installation.

0.2.108-ui 的 USB `tab5_crash_diagnostic` 根对象增加可选 `flashBenchmark`（不在 `trace` 内），未手动测试时为 null。数值字段为 `state`（1 测试中、2 完成、3 失败、4 已取消）、`sampleBytes`（131072）、`partitionAddress`、`offset`、`jedecId`、`pageBytes`、有符号 `error`、0/1 `restored` 和 `modified`。`rows` 固定四项，按 8/16/48/64 KiB 顺序，包含 `chunkBytes`、`completed`（0–3）、`skipped`（0/1）、三元素的微秒数组 `eraseUs` / `writeUs`。仅解释已完成轮次，未完成项可为零。本次启动内有效，重启即清除；通过现有 USB 启动核验读取，不增加远程写测试命令。配套桥接仅为该诊断放宽单行上限至 6144 字节，普通消息仍为 4096。只允许数值，不输出 Flash 内容、凭据或用户文字。

The optional root-level `flashBenchmark` exports current-boot numeric results for an explicit local 128 KiB tail test, including three-round erase/write API timing, actual JEDEC identity, skipped buffers and restoration confirmation. Full field definitions match the adjacent AI-bot protocol. The spare image is validated and the unused all-FF tail backed up before modification, restored and reverified afterwards. A small-sample result cannot prove sustained hardware limits; compare full-image OTA writing. No remote test-start command or change to protocol version, authentication, integrity or rollback is added.

0.2.110-ui 增加可选无符号 `phase`（0 前置检查、1 空白区查找/备份、2 测量、3 恢复）及 `scanned`（已检查候选窗口数）。读取失败保留 SDK 原始错误，无合适空白区返回 `ESP_ERR_NOT_FOUND`（261）；只在已验证镜像及 4 KiB 保护间隔以外按 64 KiB 对齐寻找完整 128 KiB 全空白区，扫描不写入。缺少新字段的旧结果仍兼容。

0.2.110-ui adds optional unsigned `phase` (0 checks, 1 scan/backup, 2 measurement, 3 restoration) and `scanned` (candidate window count). Read errors retain their SDK code; no suitable all-FF range returns 261. Scan 64 KiB-aligned windows beyond the verified image plus a 4 KiB guard without mutation. Older results lacking these fields remain accepted.

## BLE OTA preflight / 蓝牙升级短时预检（0.2.111-ui）

0.2.113-ui 增加可选 USB 能力 `bleOtaProbeIsolation:true` 与动作 `ble_ota_probe_isolated`，仅蓝牙且保留 USB 时临时停用 Wi-Fi，并由原 Wi-Fi 任务在完成、取消、连接模式/USB 变化或 35 秒期限后恢复。准备/恢复各等最多 6 秒，恢复失败继续重试并阻止新测；成功仅确认驱动重启，不等于路由器关联。保存网络不变，32 片回写窗口、三轮 RAM 下载和原传输预算不变。旧动作继续保留。

新增 USB 数值 CSV `bleRadio`（schema 1，16 字段）和 `wifiIsolation`（schema 1，7 字段），不扩大 BLE 身份。前者连接值为读取时实时值，接收计数仅预检时累计；后者记录阶段、返回码、暂停时间和恢复/超时标志。完整字段顺序、单位、重叠计时边界及兼容规则以相邻公共桥接 [AI-bot 协议](../../AI-bot/docs/PROTOCOL.md) 的 .113 节为准。

.113 adds optional USB capability/action for an isolated BLE RAM preflight. The existing Wi-Fi worker temporarily stops and restores Wi-Fi while preserving configuration; completion, cancellation, mode/USB loss and a 35-second lease expiry request restoration. Preparation/restoration each wait up to six seconds; failed restarts retry and prevent new tests or false success. Driver restart is distinct from AP association. USB numeric `bleRadio`/`wifiIsolation` schemas expose live connection values and retained receive/stop metrics without expanding BLE identity. Existing wire windows, data checks, transfer budget and legacy action remain compatible. See the adjacent common bridge protocol for exact field order and timing semantics; hardware coexistence benefit is not yet verified.

`tab5_benchmark` adds USB action `ble_ota_probe` and optional response capability `bleOtaProbe:true`. New clients query status before starting and reject unsupported firmware. The device requires fixed BLE mode and a fresh paired link, downloads three 256 KiB samples only over BLE, and stops on mode/session change without fallback. A 20-second aggregate budget plus a five-second exchange bound limits normal completion/failure to about 25 seconds. No Flash write or automatic upgrade occurs. Existing start/status/cancel results remain compatible. Authenticated `kind=benchmark` requests may include `otaProbe:true`; this selects the device's shorter wait, not a new authorization or response format.

USB 增加 `ble_ota_probe` 动作及响应能力 `bleOtaProbe:true`，新主机先读取能力再发起。设备要求仅蓝牙模式及新鲜认证连接，三轮各 256 KiB 下载；通道/会话变化即停止，不回退。总预算 20 秒、单次等待上限 5 秒，不写 Flash、不自动升级。旧 start/status/cancel 保持兼容。认证 benchmark 请求的 `otaProbe:true` 仅选择本地短等待，不改变鉴权规则。电脑使用最慢完整轮次及真实镜像大小，增加 20% 余量和 25 秒写入/校验/重启预算；估算不超过 120 秒才满足预检目标，不能代替完整 OTA 验收。


## BLE OTA range compression (internal candidate, 2026-10-05)

Authenticated per-link state optionally advertises `rpcOtaZlib=1`, alongside `rpcBinary=1,rpcBulk=1`. Only BLE bulk `kind:"ota"` requests opt in with `acceptEncoding:"zlib"`. Existing SHA-256, offer ID, raw `offset` and raw `count` (1..49152) keep their meaning. A useful compressed response uses T5R2 with body `{offset,encoding:"zlib",decodedSize:count}` and one independent zlib stream as its raw payload. Compression precedes the existing authenticated encryption. If compressed payload plus 96 metadata bytes is not smaller, return the original `{offset}` and raw bytes. USB, Wi-Fi, requests without opt-in, and older peers are unchanged.

The receiver validates exact offset and integral decodedSize, requires negotiated encoding, and bounds output to the requested count. It requires successful zlib completion, checksum, complete input consumption and exact output size; unknown encodings, trailing/truncated data, oversized output and allocation failure abort the range. Only decompressed bytes advance the image offset or reach Flash; final image SHA-256/identity/boot checks remain mandatory. Host tests and firmware compilation pass; this is not device OTA acceptance. See AI-bot `docs/TAB5-BLE-COMPRESSION.md`.

已认证状态可声明 `rpcOtaZlib=1`；仅 BLE 批量 OTA 请求明确接受 zlib 时压缩，每个原始范围独立编码，保留原偏移、长度、重试及认证。元数据给出 `encoding` 和 `decodedSize`；不划算时回退原文。设备严格验证协商、偏移、整数原始长度、完整输入消费、精确输出长度和校验，异常不推进写入。旧设备及 USB/Wi-Fi 保持原路径。内部测试不等于真机升级通过。

### 完整固件 RAM 预检 / Full-image RAM preflight (.116)

`quotaDetails[].weeklyResetEpoch` 为供应商周额度重置时刻的 UTC Unix 秒数，可缺失。Codex 单周额度卡片使用本地时钟更新“距重置”；缺失时隐藏，到期后显示“等待重置同步”，不自行推算下个周期。右侧 `weeklyReset` 仍显示绝对重置时间。不增加“本周期已过”。

USB `tab5_benchmark` replies advertise `bleFirmwareProbe:true`. Actions `ble_firmware_probe` and `ble_firmware_probe_isolated` snapshot the authenticated BLE offer (`sha256`, `offerId`, `size`) and use the production range decoder without opening any Flash partition. Authenticated RPC `kind:"ota_probe"` has the same offer/range/authentication/compression constraints as `ota`, accepts BLE only and never acquires the 90-second OTA lease. The bridge pauses background assets/metrics for the active full-image preflight and releases that pause on exit.

The device pins the BLE session, requires USB for control, limits transfer to 110 seconds (an in-flight range can take up to three 10-second attempts), and validates streaming SHA-256 over all decoded bytes. Optional Wi-Fi isolation has a 125-second lease; success is withheld until the worker confirms restart. USB/mode loss, cancellation and expiry request restoration without changing saved networks. The controller waits at most 160 seconds including bounded cleanup; no action starts OTA automatically.

Terminal result: `complete;BLE,down,1,<rawBytes>,<ms>,0,<calls>,<encodeMs>,<exchangeMs>,<decodeMs>,<wireTx>,<wireRx>,<minInternal>,<minPsram>,<minLargest>;image,<sha256>,<size>;`. Failure/cancellation use their existing states, and negative error codes cannot pass. The bridge requires complete bytes and the exact selected image hash, then estimates full OTA as receive milliseconds / 1000 * 1.2 + 25. The 120-second threshold remains a screening estimate, not hardware OTA acceptance. The legacy three-round raw benchmark remains separate.

完整固件预检仅接收、解压并计算整包哈希；桥接严格绑定所选镜像，失败不出升级通过结论。接收超时、分段重试、Wi-Fi 恢复和控制超时均有界。安装 .116 后可以对同一镜像做 RAM 预检；正常 OTA 仍拒绝重复安装当前版本，完整 OTA 验收应选择下一份真实升级候选，不能绕过该保护。

## TAB5 .121 voice transport candidate

### Codex desktop read synchronization

Optional `codexTasks.repliesRead` maps task IDs to completed turn IDs (maximum 80) on the shared USB/Wi-Fi/BLE state path. The bridge reads the current desktop account and standard local host state after a five-second persistence settling period. Missing or unsupported state produces no acknowledgements. The device clears only matching task/turn notices and immediately recalculates the top-right unread count, reply action and overview, even when the reduced task catalog omits those notices. Old acknowledgements cannot clear a newer turn. Device-local reading remains acknowledged when matching public text renders; no desktop data is written back.

Auto selection follows USB > Wi-Fi > BLE; fixed modes do not switch channels. Wi-Fi/USB audio requests now use the existing `codec=ima-adpcm` (16 kHz mono) decoder; BLE retains `ima-adpcm8k`. Independent blocks contain LE16 sample count, LE16 predictor, index, reserved byte, then low nibble first. At 3200 samples the block is 1606 bytes before Base64 instead of 6400 raw PCM bytes. This is lossy coding, not byte-exact PCM. Sequence validation, 60-second limit, stop/tail draining and no automatic audio retry remain unchanged. Paired bridge required; hardware continuity/recognition quality pending.

TAB5 .127 extends `artOrientation` CSV with `stage,i2cError,register,chipId,attempts` after the original six fields. Stage 1 identifies the chip; 2 loads Bosch configuration; 3/4 get/set acceleration configuration; 5 enables acceleration; 6 samples. `tab5_orientation_diagnostic.diagnostic` carries the same CSV. Initialization retries after 1 second for the first two failures, then every 30 seconds; ten consecutive sample failures trigger reinitialization. No orientation is trusted until a fresh sample succeeds.

TAB5 .128 checks art orientation every 50 ms, with a 350 ms stable sample window, and prefetches the alternate orientation of the same frame within the existing two-frame cache limit. Positive raw Y uses the 180-degree portrait counterpart, correcting the hardware-observed inversion.

TAB5 .130 uses local orientation 0 for normal landscape (positive raw X, verified in a fixed user-held pose), 2 for inverted landscape (negative raw X), and retains the verified raw-Y portrait signs. The earlier .129 X-sign assumption is superseded. Both landscape directions share the original landscape image and frame cache. The bitmap and date overlay rotate together by 180 degrees; no additional gallery asset or RPC orientation value is introduced.

## TAB5 runtime monitoring (optional, version 1)

`tab5_status.data.runtimeHealth` is an optional Windows-collected object; absent/null means no available monitor. Existing ESP8266 frames and macOS collectors keep their current contracts. Older TAB5 firmware can ignore the additional field. No input, node-selection or client-restart command is added.

The object carries `session`, alarm `sequence`, `updatedAt` / `epochUtc`, `checks`, `alerts`, optional `pressure`, `latencyHistory`, cached small-transfer throughput. Checks identify `cpu`, `memory`, `queue`, `disk`, `vpn`, `pac` and optionally `settings`; state is `ok`, `observing`, `warning`, `error`, `unknown` or `disabled`. Each check has a short summary, full detail/advice and observation timestamp. Successful response latency/time remains available when a later probe fails. `pressure` exposes processor queue, physical-disk I/O queue/latency, page input, available physical MB and logical processor count. Null counters are unknown, never zero.

`latencyHistory` has at most ten `{epochUtc, milliseconds, reachable}` points from actual probe rounds. Missing/failed samples render as gaps; repeated frames never create graph points. The main page shows only a short confirmed alarm title, while System → Runtime monitor groups graphical indicators and opens concise causes, key evidence and suggested actions on demand. Full raw diagnostic text remains available on the Windows bridge.

Alarm IDs belong to `session`; `sequence` increases only when a confirmed anomaly begins or changes severity. Repeat frames reuse IDs. System pressure must remain beyond the configured threshold; network anomalies require consecutive probe rounds. Confirmed recovery removes the alert. Device freshness is checked independently: an expired bridge frame or old monitor `epochUtc` removes healthy state and the live graph. Monitoring does not invoke a Codex model or export credentials/PAC query parameters.

Alerts expose full `advice` and optional `shortAdvice` for compact views. Receivers may fall back to `advice` if the short form is absent. Entering System defaults to the existing computer statistics and upload/download graph; the monitor is a secondary entry.

See [runtime monitoring](../../AI-bot/docs/RUNTIME-MONITOR.md) for defaults, diagnostics, validation and the unflashed-preview boundary.

Additional optional checks `codex` and `bridge` report actual authenticated/model-connection observations and independent bridge/UI/collector health. The `pressure` object can add `commitPercent`, `committedMemoryMb`, and `commitLimitMb`; missing values remain unknown. Optional alert/check `handledExternally` only suppresses duplicate Windows notifications already handled by the independent guard; devices continue presenting the alert normally. Guarded presentation removes stale counters and latency history even when the HTTP publisher remains alive. No new device command or model request is introduced.

## TAB5 Agenda v1 (local candidate)

`data.agenda` is optional: `{version:1, revision:<32 lowercase hex>, count:0..16}`. Definitions are fetched through authenticated `kind:"agenda", op:"sync"` on the existing RPC envelope; no ESP8266 field or command changes. USB, Wi-Fi and BLE share the existing session, timestamp, nonce, HMAC and AES-GCM checks. Unknown operations return 400; unavailable agenda storage returns 503 and must not clear the device cache.

The request may carry up to 32 actions (a completed occurrence and a later snooze per item): `{id,edit,occurrence,op:"complete"|"snooze",until}`. The response is `{version:1,revision,items:[...],conflicts:[id,...]}`. An item has `{id,edit,title,due,end,repeat,lead,done,snoozeOccurrence,snoozeUntil}`. IDs and editions are 32 lowercase hex digits; titles contain at most 32 characters/96 UTF-8 bytes. `due` is the start and `end` is the end of the base occurrence. New dated definitions require `due < end <= due + 86400`. `due=0,end=0` means an undated to-do (occurrence sentinel 1); it does not expire automatically. Legacy preview definitions with no end expire at due. `done` and complete actions remain for protocol compatibility; current UIs only snooze or dismiss. `repeat` is 0=once, 1=daily, 2=Monday-Friday, 3=weekly on the original weekday; `lead` is 0..60 minutes.

All schedule/occurrence timestamps are **local civil seconds**, represented by treating the host's local date/time as UTC for arithmetic. They are not UTC instants. The authenticated clock's separate `utcOffsetSeconds` remains unchanged. An unset RTC must not trigger alarms. The host and device use identical rules for early reminders across midnight, snoozes across midnight and advancing repeated items. A newer definition receives a new `edit`; stale edition actions are rejected as conflicts. Completion is monotonic and idempotent, and cannot erase a newer snooze. An occurrence expires at `occurrence + end - due` (at occurrence for a missing end); repeats advance automatically, including across midnight. Expiry is derived from time without Flash writes. Alerting is suppressed after expiry; snooze must be strictly before expiry. Successful snoozes are persisted before acknowledgement. A new revision never means empty data unless a complete, valid response explicitly contains an empty list.

中文：日程使用独立可选摘要和原有加密 RPC，最多 16 项。事项按电脑本地时间计时，设备先校准 RTC；开始与结束时间独立，提前提醒相对开始时间；到结束时间自动结束，重复日程转到下一次，过期提醒不补响。界面无手工完成；延后不得越过结束时间，先保存再同步。重复请求幂等，旧编辑版本不覆盖电脑改期。损坏或未确认响应保留原缓存，详情与验收边界见 [日程待办说明](AGENDA.md)。

Agenda sync additionally accepts at most 16 `edits:[{baseEdit,item}]`. The device generates a new 32-hex edition and persists definitions before acknowledging local saves; baseEdit remains the last confirmed host edition across repeated offline edits. An empty baseEdit creates a new item. A matching baseEdit replaces its definition; an already identical edition is an idempotent retry. Concurrent edits/deletion preserve a copy with `id=item.edit`, returning `forks:[{id,newId}]`; retries reuse that copy. Capacity or copy collisions return 409 and keep pending device data. Responses advertise `editSupport:1`; the device refuses to replace a pending-definition cache with an older bridge response lacking this flag. Same-edition alert deduplication survives normal and forked acknowledgements. Definitions and snoozes share the authenticated transport; no credentials enter item data.

设备端新增／编辑先本地持久化，离线可用。同步使用 baseEdit 做并发核对；冲突保留两份，满额或旧桥接保留本机修改，不静默覆盖。列表仅显示开始时间，编辑页可设结束时间。名称语音使用现有 voice 协议的独立 owner UUID `a1b07a65-0000-4000-8000-00000000a5ed` 及 `source:tab5`；只回填本地名称草稿，不创建／发送 Codex 任务。

### Agenda 204 bounded retention / 满额新增时整理

204 sends and requires `retentionSupport:2` on agenda sync. Ended one-offs remain in the document: reads, clock ticks, reboot and edits do not delete them. An insertion requiring space beyond 16 selects the oldest-ended one-off (legacy missing end uses due; ties use ordinal ID). Repeat rules, future and undated items are excluded. No victim returns a capacity error with the original data intact.

Device eviction and insertion share one atomic compact NVS transaction, including the existing `{id,edit,baseEdit}` deletion receipt. The 16-receipt bound remains: reconnect is required when full, rather than dropping unacknowledged removals. Sync applies deletions before edits, preserves incoming editions and concurrent conflict copies, and reclaims only the excess count from pre-existing expired host entries. Expired incoming edits still require precise acknowledgement; lost responses cannot cause additional eviction. 204 rejects snapshots from older bridges without the retention capability. The compact `agenda_v3` encoding introduced by 203 remains unchanged (maximum 3,574 bytes), with no new history archive.

中文：过期先保留，仅满 16 条且新增时清理最早结束的一条；不足 16 条、编辑及重启不清理。新建与清理原子保存，离线删除回执补同步，待同步队列满时提示连接电脑，失败保留原记录。“全部”可查看已结束事项。电脑批量合并保护本批离线编辑及冲突副本，只清理补足容量所需的旧过期记录；两端声明 retentionSupport=2，204 不采纳旧桥接响应。沿用 203 紧凑存储，不增加持久历史备份。

## 0.2.216 saved Wi-Fi capacity / Wi-Fi 保存上限

最多保存 10 个网络，保留五项旧格式的固定布局并迁移到版本 3 独立 NVS 键；迁移保留 SSID、密码、备注、首选项及空列表。第 11 个不同网络被拒绝，已有网络仍可更新。旧键保留以供固件回退。

Up to ten networks are saved in a version 3 NVS key. Fixed five-slot v1/v2 migration preserves SSIDs, passwords, labels, preference and empty lists. The eleventh distinct network is rejected; existing entries can still be updated. Legacy keys remain for rollback.

Optional USB wifiHttpDiag: nine numeric values (version, attempts, ESP open error, socket errno, HTTP status, authenticated valid replies, destination IPv4 host-order uint32, port, local IPv4 host-order uint32). No credentials or auth headers are exported.

## TAB5 217 connection path diagnostics / 连接路径诊断

Optional wifiPathDiag contains 17 numeric fields: version=1, IPv4 netmask, gateway IPv4, AP channel, RSSI, STA MAC as uint48, BSSID as uint48, station-is-default boolean, probe count, gateway TCP/80 errno, host TCP/configured-port errno, transport ESP error, TLS code, TLS flags, Wi-Fi power-save mode, AP query result, netif MAC as uint48. IPv4 values are host-order uint32. Zero probe errno means connect succeeded. On failed bridge HTTP connects only, the Wi-Fi worker samples at most once per 30 seconds, using nonblocking sockets with one-second connect waits; USB only copies the snapshot. Probes close their sockets, send no HTTP request, skip OTA, and do not change network settings.

中文：桥接连接失败时对网关与电脑做有界 TCP 对照，并记录掩码、网关、信号和底层错误；不修改网络、密码或配对。诊断新增不等于连通问题已修复。

## TAB5 BLE keyboard unlock candidate / 蓝牙键盘解锁候选

`0.2.217-ble-unlock` adds the standard HID service `0x1812` and Device Information `0x180A` beside the existing private GATT service. Report ID 1 carries an 8-byte keyboard input report and LED output; report and boot keyboard modes are supported. Advertising keeps the existing private UUID and adds HID UUID and keyboard appearance within the 31-byte limit. No existing JSON/RPC fields or protocol version change.

HID input is permitted only on an encrypted, MITM-authenticated, bonded connection with notifications enabled, recent authenticated bridge data on that same BLE connection, and fresh locked-state evidence. PIN is never an RPC payload or a persisted value. Pairing needs explicit on-device numeric comparison; repeated pairing needs separate consent before removing that peer's old bond. A submitted sequence is one-shot and cleared on use/cancel/error. Enter waits for fresh post-digit locked state; success requires observed unlocked state, not a successful notification call.

候选仅增加标准蓝牙键盘服务，保留现有桥接、语音和数据协议。复用应用控制页的锁屏按钮，每次由用户输入数字 PIN，不保存、不经 HTTP/RPC 发送、不自动重试。蓝牙绑定密钥与 Windows PIN 分开处理。当前为刷机前候选，编译、模拟和界面预览均不代表真机解锁已通过。验收计划见 `docs/development/TAB5-BLE-UNLOCK-20261017.md`。


## Optional Wi-Fi keyboard candidate / 可选 Wi-Fi 键盘候选

`connectionHealth.wifiKeyboardVersion=1` means the current user configured the optional local adapter; it is not an unlock or service health acknowledgement. Firmware selects it only at opening the existing unlock panel with fresh Wi-Fi and USB/BLE absent. The choice stays fixed during the transaction.

`kind=wifi-keyboard` uses the existing paired-device encrypted RPC envelope and adds `op=begin|submit|step|finish|cancel`. `begin(confirmPinPage=true)` returns a one-use `offer`, AES-256 key and 15-second deadline. `submit(offer,sealed)` stages 4–64 numeric bytes encrypted as nonce[12]+tag[16]+ciphertext, with ASCII offer ID as AAD. No clear PIN field is accepted. `step(offer,next)` advances exactly one sequential numeric key press/release and renews a 2-second lease; replay or wrong sequence stops the transaction. `finish` requires all digits consumed and rechecks console lock state before one Enter. Cancellation and expiry wipe staged bytes and close the local connection. No operation is automatically retried or forwarded to USB/BLE. Only fresh unlocked computer state confirms success.

本候选复用现有锁屏按钮和加密通信，Windows 普通权限桥接仅对接经过身份检查的本机厂商服务；不启动旧的临时 SYSTEM 辅助程序，不更改登录认证方式。可选服务独立授权安装；PIN 不保存，独立组件访问密码用当前用户 DPAPI 保存。协议与验收说明见 `TAB5-WIRELESS-UNLOCK-20261018.md`（公共桥接目录在 `docs/development/`）。

## Conservative BLE lock-screen preparation / 保守蓝牙锁屏封面准备

The separate conservative BLE cover-preparation candidate adds read-only `kind=computer-control, op=lock-screen` on the existing authenticated RPC envelope. Response: `{version:1, locked:true|false|null, lockScreenCover:true|null}`; null may be omitted. `true` requires a locked current session and a stable foreground CoreWindow owned by the Windows SystemApps LockApp image. Denied, unknown, another window and transitional states never confirm a PIN field/provider/focus. Older bridges return an unsupported-operation error; devices continue with manual PIN-page preparation and send no Space.

Only an explicit opening of the existing BLE unlock modal requests this sample. The reply is bound to that modal's request ID and expires 1,500 ms after request dispatch on the device's monotonic clock, including round-trip time. With fresh positive cover evidence and the existing secure BLE/HID/bridge/lock/connection-generation gates, TAB5 may enqueue exactly one Space press/release, never Enter or PIN as preparation. Key-down rechecks the same evidence; key-up remains best effort. No background poll, reconnect, cancellation, timeout or late reply initiates/retries preparation. This is a best-effort observation across asynchronous Windows/radio transitions, not atomic PIN-focus verification. Full hardware acceptance remains separate.

中文：保守蓝牙候选仅增加一次只读封面检查，不安装组件、不操作安全桌面、不读取 PIN。复用原解锁按钮与面板，只有明确的封面证据才发送一次蓝牙空格；过期、失败、旧桥接及未知状态均不发，也不宣称 PIN 框或焦点已确认。每次 PIN 仍由用户输入，不保存。此准备步骤不用于 Wi-Fi 键盘路径；候选说明见 `docs/development/TAB5-BLE-COVER-20261018.md`。



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
`7af50007-7f23-4a91-bc65-667a19320101` 返回 `{version:1,hostId}`；非当前电脑断开待命，
桥接延后重连，不发送状态或执行 RPC。此公开字段只用于待命判断，不能代替原有密钥鉴权。
状态可附加 `connectionHealth.hostBindingVersion=1,hostId,hostName`；有 hostId 时必须匹配。
旧无线桥接可在设备明确选中其已保存密钥时继续使用原加密协议；接收与 HID 授权还必须匹配
解密开始时的电脑代次，防止切换竞态。新固件的 USB 自动接管需要对应桥接升级。

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
