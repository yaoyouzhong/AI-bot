# Windows 桥接开发交接：Mac 本轮实现与差异（2026-10-26）

本文件供 Windows 开发者直接排期和验收。对照对象是本分支本次累计提交中的两端源码；Mac 当前交付进展见 [账号与小屏记录](MAC-MODEL-ACCOUNTS-20261026.md)。较早的故障测量、TAB5 .273–.279 过程见 [详细对照记录](MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。这里将当前结论集中列出，历史文档中的“当前／待验”不能覆盖较新的验收记录。

**Windows 结论是源码对照，不是 Windows 实机验收。** 本轮不新增 Windows 产品实现，现有 Windows 预览／日历源码随累计改动提交。Mac 的通过不能替代 Windows 的构建、音频、串口、蓝牙、WinForms 和真实账户测试。无需复制 Mac 平台代码或第三方工程；按行为与边界在 Windows 现有架构中实现。

## 1. 建议排期

| 编号 | 优先级 | 分类 | Windows 后续工作 | 当前证据 |
| --- | --- | --- | --- | --- |
| W01 | P1 | 确认的解析缺口 | DeepSeek 网页总余额纳入同币种赠送钱包；与 API `total_balance` 区分，不能重复加赠送 | 活动网页解析在找到 `normal_wallets` 后直接返回，没有合并 `bonus_wallets`；API 路径已正确读取总余额 |
| W02 | P1 | 账号归属边界 | API 余额与网页消费默认独立存储／显示；没有同账号证明时禁止合并下发 | Windows `MergeDeepSeek` 会在币种和 API Key scope 不变时保留网页消费；Key scope 无法证明网页账户与 API 账户相同。用户最新选择是默认分开 |
| W03 | P1 | 确认的体验差异 | 已确认 TAB5 .277+ 的非 BLE 预览按能力启用更短冷却和窗口刷新 | Windows 窗口 1000ms，完整帧后 1000ms、unchanged 后 2000ms；Mac 已对兼容固件非 BLE 使用 100ms |
| W04 | P1 | 需复现后修复 | OTA／启动核验命令按实际发布帧时间约束通告有效期；核对旧提示覆盖进度 | Windows 命令直接输出原 Expires，Republish 重用旧 snapshot；Mac 已有帧时间边界模型与实机更新证据 |
| W05 | P1 | 调度缺口与实机复验 | 语音终态交付完成前保护串口资源调度；避免一个状态批次连续占用资源锁 | Windows 单状态批次可发送 16 个资源分片；BackgroundTransferPaused 主要覆盖固件操作，未看到与 Mac 同语音终态交付租约等价的状态批次门槛 |
| W06 | P2 | 明确的行为差异 | 仅对本次识别新增片段做段落合并，保留用户原草稿换行 | Windows `Snapshot()` 直接使用 `BoundText(editor.Text)`；BoundText 只截 UTF-8 长度，不做识别段落处理 |
| W07 | P2 | 回退路径健壮性 | 长 Codex 会话的 API 回退增加有界分页／响应与时间约束 | Windows owner／stored 主路径不同；API 回退仍 `thread/read(includeTurns:true)`，输出整行读取。不能宣称复现了 Mac 同款 503 |
| W08 | P2 | UI 补齐 | 模型账号网页增加每厂商缩放保存，解决小视口弹窗关闭按钮不可见 | Windows 活动 WebView2 账号窗口未找到对应 ZoomFactor 控件；Mac 60/75/90/100/125% 已实现，智谱 75% 实测可用 |

P1 表示建议先处理，不表示发现了未经验证的安全漏洞。W04/W05 必须先补 Windows 回归与测量，不能把静态差异直接当作 Windows 故障已复现。

## 2. 逐项开发入口与验收

### W01：DeepSeek 总余额与赠送余额

- Windows 活动入口：`TrayApplicationContext.ShowDomesticAuth` → `_runtime.Domestic.OpenAuthorization` → `MigratedDomestic/DomesticQuotaService.cs` 的 `FindDeepSeekBalance`。`DomesticQuotaService.cs` 也有独立 `ParseDeepSeek`；先确认调用范围，两处保持语义一致，不能只改未使用的解析器。
- Mac 参考：`MacDomesticSources.parse("deepseek", mode:"console")`、`MacModelAccountValidation.run`。
- 官方 API：`balance_infos[].total_balance` 已包括赠送与充值，直接采用；不要再加 `granted_balance`。
- 官方网页摘要：`normal_wallets` 是充值钱包，`bonus_wallets` 是赠送钱包，按币种分别合计；优先权威总余额，不把同一余额在嵌套响应中重复累加。采用十进制运算，最后投影到现有协议。
- 源字段依据：[DeepSeek 官方余额 API](https://api-docs.deepseek.com/api/get-user-balance/)；[DeepSeek 官方账号提供程序说明](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/credentials/deepseek-account-platform/README.md)。只参考字段语义，没有迁入该项目源码或认证流程。
- 合成验收：充值 10.10＋赠送 0.20＝10.30；CNY 不加 USD；赠送为零／`0E-16`；只有赠送；API total 与钱包同时存在不重复计算；缺失钱包分组、非法值、冲突重复值保留最近成功缓存。金额均用虚构测试值。
- 实机验收：同一网页的充值与赠送相加，与桥接总余额一致；API 已含赠送的路径无回归。记录字段是否存在及比较结果，不记录真实金额或凭据。

### W02：默认分开，不能用币种或 Key scope 冒充同账号证明

- 最新用户决定：**API 总余额与网页登录消费分别标明来源；设备不合并未验证数据。** 这取代此前考虑的手动关联／默认混合方案。
- 官方余额 API 没有可与网页登录身份核对的账号 ID。余额相近、币种相同、API Key 没换，都不能证明网页属于同一个账号。
- Windows 检查入口：`MigratedDomestic/DomesticQuotaService.cs` 的 `MergeDeepSeek`、网页 `FindDeepSeekUsedCost`、`LegacyDisplayCache.cs`、`MediaCostSelfTest.cs`、`OfficialQuotaApiSelfTest.cs`。现有“API 刷新保留网页消费”的测试须按最终账户隔离策略重新评估，不能直接删掉测试来掩盖行为变化。
- Mac 最终实现入口：`MacDomesticQuotaService.refresh/useSeparateDeepSeek`、`MacDomesticBrowser.receive`、`MacDataStore.updateDomestic/updateDeepSeekWeb`、`MacDomesticQuotaWindow`。
- 分别刷新、分别保存时间、分别显示失败。网页 401／限流不能将正常 API 余额标旧，API 失败也不应把网页成功时间冒充余额时间。
- 在分开模式下，设备现有统一 DeepSeek 卡只下发 API 总余额，`usedCost/cost` 留空；网页消费在电脑账号窗口独立展示。纯网页来源则可以同时下发同一响应中的合计余额和消费。未配 API Key 可使用纯网页来源。
- 验收必须用两个不同的虚构账户：API A 余额 90，网页 B 余额 12／消费 5，任何事件顺序、重启、换 Key、网页过期均不能让设备显示“A 余额＋B 消费”。再测试同账号也保持来源标识，没有自动合并特例。

### W03：准实时预览的主机节奏

- Windows：`Tab5ScreenPreview.cs` 的 Context、完成／unchanged 分支；`Tab5ScreenPreviewForm.cs` 的 Timer；`Tab5ScreenPreviewService.cs` 的能力投影。
- Mac：`MacTab5ScreenPreview.supportsResponsiveCapture`、`MacTab5ScreenPreviewWindow`、对应 validation。
- 仅对已确认 .277+ 兼容版本、USB／Wi-Fi 启用更短等待；未知版本、旧固件和 BLE 保留保守节奏。保持一个在途请求、原 30 秒硬截止、JPEG 首片保活、SHA／尺寸检查、相同像素不重绘。
- Windows 已有独立 USB RPC 循环、完成后立即续传和协商 ready-wait，不要照搬 Mac 的串口 pump。
- 实机：连续切页、横竖屏、关闭重开、最小化、录音／拍照／播放操作后恢复高清；同时记录排队、设备准备、传输、UI 呈现四段时间。unchanged 不算完整帧带宽样本。

### W04：升级命令和旧快照时间

- Windows：`Tab5RemoteOta.RemoteOtaCommand`、`Tab5WirelessBootVerification.BootVerifyCommand`、`Tab5Service.ConnectionHealthFor/Republish`。
- Mac：`MacTab5Firmware.advertisedExpiry/projection`、`MacFirmwareCommandValidation`。
- 按设备实际校验的帧时间控制通告；Mac 用 45 秒／15 秒通告边界，而原请求 30 秒／10 秒截止不延长。先核对 Windows 实际接收器及同字段约定，不直接照搬常数。
- 用旧 snapshot、时钟偏移、确认迟到、重复回执、取消、已经开始下载后旧命令过期等场景验证；最终成功必须是所选镜像的版本／摘要／启动证据，不只观察版本字符串。

### W05：语音、状态和资源争用

- Windows：`Tab5Service.RunUsbStatusAsync` 中 `_usbGate` 与 `budget=16` 的资源循环、`BackgroundTransferPaused`、`Tab5Telemetry.cs`、`Tab5UsbRpc.cs`、`Tab5BleVoice.cs`。
- 已有保护：状态与 RPC 分循环，串口单事务有 generation 校验；BLE 已有 interactive/bulk priority。不要在交接中误写成“Windows 完全没有优先级”。
- 差异：Mac 的语音优先延续至当前 voiceId／nonce 终态所有分片完成，不止到识别结束；还覆盖状态包内夹带的资源。参考 `MacVoiceTerminalPriorityValidation`、`MacVoiceSchedulingValidation`、`MacTab5USBVoicePriorityValidation`。
- Windows 先测资源锁等待与终态交付。如果批次影响语音，缩短／让出未提交的资源批次，不能暂停必需保活、放宽身份校验、延长 8 秒归属截止或重放已经发布的操作。
- 实机覆盖 20 秒含停顿录音、明确使用 TAB5 麦克风、单次停止、长历史读取、封面传输、真实断线与新会话。Mac 与 Windows 麦克风来源必须一致才比较延迟。

### W06：语音段落

- Windows：`Tab5VoiceSession.Snapshot/BoundText` 及语音编辑器的本次识别插入边界。
- Mac：`MacTab5SpeechTranscript` 与对应测试。
- 合并本次识别片段的停顿换行；英文保持词间空格、中文不硬加空格；纠错／重复片段只出现一次。不要删除原草稿或用户手动输入的换行。
- 同时检查中文、英文、CRLF、Unicode 分隔符、取消后恢复、2000 UTF-8 字节限制、分段修正。Windows 使用自身输入链路，不移植 Speech.framework。

### W07：长会话公开回复

- Windows：`Tab5CodexTasks.cs` 两处 `thread/read(includeTurns:true)`、`ReadOutputAsync` 的 `ReadLineAsync`、25 秒调用期限；保留现有 owner/stored 主路径。
- Mac：`MacCodexPublicHistory` 用 `itemsView:notLoaded` 列轮次，再按 turnId 分页读取 `thread/items/list`；仅保留公开 assistant 回复。相关 validation 覆盖分页、归属和边界。
- 先探测本机 CLI 接口能力，再设计回退；限制单回包、累计公开文字、游标次数、时间和取消。失败保留已成功的公开缓存，不能转读推理、工具原文、凭据或真实会话 fixture。
- Mac 原故障是 23 MB full 响应超过 8 MiB 接收限制，修复后原会话验收通过。这不证明 Windows 同样有 8 MiB 限制或相同故障。

### W08：网页缩放

- 活动 Windows 账号窗口在 `MigratedDomestic/DomesticQuotaService.cs`，不是只修改另一个同名简单表单。核对其 WebView2 初始化、切换厂商、关闭隐藏和后台刷新路径。
- 增加每厂商缩放菜单及非敏感设置保存；Mac 可参考 60/75/90/100/125% 和恢复默认。
- 不自动接受广告条款，不注入模拟关闭流程，不导出 Cookie。低分辨率与 125%／175% 桌面 DPI 下官方关闭按钮应可达；用户正在登录时不能后台强制刷新页面。

## 3. Windows 已有功能：不应重复开发

| 本轮 Mac 实现／修复 | Windows 当前状态与复验重点 |
| --- | --- |
| 统一模型账号、14 家入口、8 个额度来源 | 已有统一账号窗口；Mac 是补齐入口。6 个仅网页平台不能标为已接额度 |
| 智谱保持登录、后台刷新、失败保留缓存 | Windows 已有持久 WebView2 profile、关闭隐藏、后台刷新、401／429 处理；做真实登录／重启回归即可，不照搬 WKWebView |
| DeepSeek 累计消费 | Windows 已有 `total_costs` 及 24 小时消费缓存机制；新增工作是 W01 总余额和 W02 账户隔离，不是重复接入消费字段 |
| ESP 自动发现、USB 优先、Wi-Fi 回退 | Windows `LanDiscoveryServer`／协议与设备路径已有。Mac 补齐；Windows 做地址变化、旧端口、8 秒回退和恢复测试 |
| 完成提示 12 秒后恢复轮播 | Windows `DisplayPolicySelfTest` 已覆盖；Mac 本轮对齐 |
| ESP 心跳独立投影，避免大状态超长 | Windows 原有设备状态投影是 Mac 修复基线；保持小屏与 TAB5 数据分层 |
| 股票中文 21px、20px 行高、右对齐、灰色与末尾省略号 | Windows `LocalizedTextResources.RenderStockNames` 已有；代码共用固件 16px Font2。Mac 已与之对齐并获用户验收 |
| 天气空气徽标＋独立天气文字 | Windows 是原行为基线；Mac 已补齐。Windows 不需要移植 CoreText 绘图 |
| 单实例 | Windows `Program` 已有当前用户 Mutex；Mac 本轮补自己的实现 |
| Wi-Fi 已认证压缩、独立 USB RPC | Windows 已有；Mac 的对应修复不构成 Windows 缺失 |
| TAB5 预览接收、故障降级后重试高清 | 本次累计 Windows 源码已有 v2 接收和恢复模型；W03 是刷新节奏的额外差异 |
| 国内节气数据 | 两端均已有紫金山天文台日期事实读取源码；Windows 用 PdfPig，Mac 用 PDFKit。保留许可与来源并在 Windows 验证，不另换数据源 |
| 日程、生日、股票管理、天气测试、备份、通知、设备菜单、图库 | Mac 按已有 Windows 功能校准；此前逐项清单见 MAC-WINDOWS-FEATURE-PARITY-20261022.md，不再列为 Windows 新需求 |

## 4. 共用固件与平台专属边界

- ESP8266 0.5.6：重置卡 14px 细笔画保留、数量／日期对齐、73px 常规宽度、动画避让，Mac 已获实机确认。这属于共用固件，Windows 不应在桥接中重复绘制或改协议。
- TAB5 .273–.279：预览内存回收／复用、前台操作让位、相机／照片、录音触摸和显示路径由共用固件提供。Windows 在同版本上复验，不把仅换桥接当作设备修复。
- 原“只看截图低清”“长时间蓝闪”“BLE 两秒”等历史结论按最新各项证据区分，不能概括成长期稳定性全部通过。
- Mac 的 Keychain、WKWebView、CoreText、CoreAudio、Speech、Apple Music、Apple 锁屏／ARD 是平台实现。Windows 使用凭据管理器／DPAPI、WebView2、SMTC／自己的音频输入与登录链路；只对齐用户行为、失败保留和取消边界。
- 解锁重点复验取消、断线、旧请求不可继续输入和真实成功确认，不迁入 Mac AX 或账号认证代码。
- 封面重点复验切歌竞态、来源切换、无封面、迟到旧图、方向与 stride；Windows 已有歌曲身份和缓存，Mac 通过不替代 Windows。

## 5. Windows 开发验收清单

1. 从包含本交接文档的提交开始，先运行现有构建与自测，记录当前失败；不要把已有 Windows 预览／日历源码当作尚未编写。
2. 按 W01/W02、W03、W04/W05、W06/W07/W08 拆分提交。每项加入能复现差异的合成测试，再做该项 Windows 实机验收。
3. 至少执行仓库规定的 `dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release` 与 `dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once`；按入口运行既有 public、额度、预览、语音、RPC 和升级自测。
4. 凭据、Cookie、真实余额、原始录音、聊天正文与设备 Flash 备份不放进 fixture、日志、提交或发布包。只记阶段、数值计时、字段存在性和通过／失败。
5. 只改桥接时不要求设备重新刷机。涉及协议变更必须同时更新 Windows、设备和文档，保持身份、超时、单次发送、失败缓存与 USB 优先边界。
6. ESP 部署收尾恢复自动轮播，保留用户页面／顺序／间隔；TAB5 保留用户原展示设置。不能用 Mac 验收替代 Windows，也不能将构建成功写成实机成功。
7. 本次是提交与推送，不是正式发布。发布、标签、安装包和宣传材料的收尾按各自授权与仓库发布清单执行。

## English handoff summary

This review compares the accumulated branch source with Mac acceptance; it is not Windows hardware validation. Priorities are: include same-currency DeepSeek bonus wallets without double-counting API totals; keep API balance and web spending separate because the balance API cannot prove matching account identity; gate responsive preview pacing on compatible firmware; test old-snapshot OTA deadlines and terminal-voice resource scheduling; normalize only newly recognized speech; bound long-thread API fallbacks; and add per-provider web zoom. Windows already has unified model accounts, persistent/background WebView2 sessions, spending parsing, ESP discovery, 12-second completion expiry, correct stock rendering, single-instance protection, packed Wi-Fi and independent USB RPC. Shared firmware and Mac-specific OS mechanisms must not be mistaken for Windows feature gaps. Every proposed Windows fix needs its own regression and physical acceptance before release.
