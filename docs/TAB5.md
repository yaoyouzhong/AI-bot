# TAB5 接入（开发中） / TAB5 integration (in development)

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

### 2026-09-26 草稿和发送恢复

- 同一配对加密协议新增 `op=draft-save`（turn 路由），`draft-load`、`receipt`（read 路由），以及显式的 `steer`、`interrupt`（turn 路由）。三类限速独立：草稿、读取、用户操作；nonce 仍共用防重放表。操作必须符合路由，畸形 op 拒绝；旧固件无 op 仍视为 send。
- 新请求带 UUID `requestId`；追加/停止还带 `expectedTurn`。202 只代表桌面接受，不代表模型完成。回复帧增加 `control={taskId,turnId,running,waiting}` 与 `draftView={taskId,requestId,text,pendingRequest,operation,expectedTurn}`；不把所有草稿装入状态帧。
- 当前用户 DPAPI 加密的 `tab5-codex.dat` 持久保存草稿、请求摘要和回执。派发前落盘，重启后的重放仅核对结果；不确定投递不能自动重发。接受时清除对应草稿，停止任务保留草稿，损坏文件不覆盖。512 条记录上限只淘汰旧 completed/rejected，80 份草稿不自动淘汰。
- 运行中追加与停止已按本机桌面 IPC 版本实现并通过合成服务验证；尚需设备新版操作验收。继续保留先审阅语音文字，再由用户手动发送。
# 2026-09-26 语音开始提醒

语音响应新增 `ready`：电脑完成快捷键确认、虚拟输出运行，并收到 TAB5 PCM 或 DJI 真实采集回调后才置为真；终止状态为假。TAB5 在此时显示“可以说话了”、开始计时，并播放一次短音。它确认音频通路，不代表豆包内部识别或最终文字成功。

同一次启动复用已枚举的 VB-CABLE 端点，避免重复枚举延迟；诊断日志仅记录焦点、音频打开、快捷键和就绪耗时，不记录录音、正文或凭据。
