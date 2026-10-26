# Mac TAB5 现场验收 / Local acceptance — 2026-10-20

接续 [交接文档](../MAC-TAB5-HANDOFF.md)，在测试分支 `codex/mac-tab5-phase1-ci` 执行。初始 HEAD 为 `bf16f1f9becd172407c51297fadb0b0bce57fb8f`，工作区干净，远端测试分支与本地一致。用户确认 TAB5 已连接 Mac，Windows 桥接已退出或暂停。

本轮只进行本地候选运行、测试、联调与修复；不合并或推送 main、不安装常驻服务、不部署、不发布、不写入 TAB5 固件。真实发送、追加或中断 Codex 任务仍须指定测试任务。TAB5 展示设置保持原值。

## 当前检查点

当前设备版本补充：用户在本轮确认实际为 `.247`；此前 `.236` 记录为旧信息，不能据此认定所有历史样本所用版本。手头接收器归档仍为 `.234`，迁移指导只覆盖 `.236/.237`。订阅顺序候选曾持续收到 ATT/8；后续只读证实 CCCD 已开启，Mac 有条件重新确认候选已获系统通知订阅成功，实测 44 个音频通知，用户确认结束回显缩短到约十秒以内、文字完整且一遍；仍未达到 Windows 约两秒。最新命令窗口候选实测仍约十秒：23 次免应答窗口授权确已使用，但音频交换总计 10039 ms，未证明继续提速；文字完整且一遍。用户已退出未保存编辑并恢复 Automatic。用户随后说明蓝牙适配未改、应与 `.234` 一致，并实测同一设备 Windows 仅蓝牙约两秒结束收音。继续以 `.234` 与对应 ESP-IDF 5.4.2 蓝牙栈只读排查 Mac 差异，不将缺少 `.247` 源码作为继续工作的前提；固件不动。

Current device-version correction: the user confirms `.247` in this round. Earlier `.236` records are historical and do not identify every earlier sample's firmware. The available receiver archive is `.234`, and migration guidance covers `.236/.237`. The ordering candidate continued to receive ATT/8; readback then confirmed an enabled CCCD. Conditional reconciliation achieved successful subscription and 44 actual audio notifications; the user reports finishing within about ten seconds with complete single-copy text, still slower than the roughly two-second Windows observation. The command-grant retest still takes roughly ten seconds: all 23 grants actually use commands, but audio exchanges total 10039 ms without demonstrated further improvement; text is complete and single-copy. The user left the unsaved editor and restored Automatic. The user subsequently reports unchanged Bluetooth adaptation since `.234` and approximately two-second finishing on Windows in Bluetooth-only mode using the same device. Continue comparing the Mac path against `.234` and its ESP-IDF 5.4.2 stack; matching `.247` source is not a prerequisite for this investigation. Firmware stays untouched.

2026-10-22 最新：正常运行未提交候选 `artifacts/mac-tab5-20261022/ble-final-reply/AIBotBridge.app`，七项功能开关保持开启。此前 reply-framing 实机确认当前对话的新消息、运行状态和停止按钮可见；实际发送／中断在指定验收会话独立通过。长回复接收重复扫描已修复，同期 Wi-Fi 读取请求桥接处理约 302–576 ms，不含传输／设备渲染。当前停止回复、图片分离、立即续传、压缩增量、订阅与窗口授权候选源码 189 个实际测试体通过断言适配器、Release／签名及离线自检通过；完整本机 XCTest 仍缺模块。未修改或刷写固件。

已通过的近期分项还包括常用程序入口、三市场行情、原版双宠物外观与工作动画、天气地名、语音顺序与去重、约三十秒含停顿录音、取消后重录隔离、快捷回填／发送，以及已绑定 Mac／Windows USB 接管、无线手选、失联保持和重启记忆。旧段落中的待验状态按其当时上下文保留，不能据旧清单重复要求已通过项。

纯 BLE TAB5 麦克风仍未通过停止延迟验收：压缩增量候选最新用户估计为 5–10 秒，文字完整且只有一遍，已退出未保存日程并恢复 Automatic。17 块／3.4 秒音频交换累计 7251 ms，后台状态与资源 1515 ms，本地停止至最终识别 65 ms；不同长度样本不能作为精确提速比。只读系统记录已关联到提供 TAB5 服务的唯一设备连接：本轮前生效的间隔为 30 ms、peripheral latency 为 0；本轮首次音频窗口期间另见 min=max=6、lat=0、mul=400 的拒绝，匹配归档设备固定 7.5 ms 请求，但该拒绝行自身没有设备标识。这形成具体的参数兼容性验证点，不等于已证明全部延迟根因或保证调整后两秒达标。此前把参数线索当作下一步必须改固件的依据，结论过早，已撤回该排查前提；继续优先对照 Mac 桥接与 Windows。新 Mac 候选仅移除每个已确认新语音请求后的额外 15 ms 等待，并细分通知计时；已完成本机构建检查并启动，等待设备连接后实测，固件不动。列表偏移的永久修复、同步提示自动恢复、固件工具写入和相机分配问题继续暂缓；长期稳定性、真实断流恢复与首次权限分支仍未关闭。

Latest on 2026-10-22: the normal uncommitted ble-final-reply candidate runs with all seven opt-ins retained. The previous reply-framing candidate physically passed fresh messages, runtime state and Stop visibility; actual send/interrupt passed separately in the designated acceptance chat. Repeated scanning of large stdio replies is fixed, with concurrent Wi-Fi read processing around 302–576 ms excluding transfer/device rendering. All 189 stop-reply/resource-separation/immediate-continuation/packed-delta/subscription/grant assertion-adapter bodies, Release/signing and offline checks pass; full local XCTest remains unavailable. No firmware edit/flash occurred.

Recent physical passes also include application launching, three stock markets, both original pet appearances/working animation, weather place labels, speech order/deduplication, approximately thirty-second paused capture, cancellation/re-record isolation, quick fill/send and existing Mac/Windows USB takeover, manual wireless switching, offline ownership and reboot persistence. Older pending entries retain their historical context and must not trigger redundant acceptance requests.

BLE-only TAB5-microphone stopping latency remains unpassed. The latest packed-delta take is user-estimated at 5–10 seconds, with complete single-copy text and restoration of Automatic after leaving the unsaved agenda editor. Seventeen blocks/3.4 seconds required 7251 ms of audio exchanges and 1515 ms of background status/resources; local stop-to-final recognition took 65 ms. Different-length samples do not establish a precise speed ratio. Read-only system events now associate the unique TAB5-service device connection with effective 30 ms interval/peripheral latency zero before this take. A rejected min=max=6, lat=0, mul=400 request during its first audio window matches the archived fixed 7.5 ms request, although that rejection line itself lacks a device identifier. This is a concrete compatibility check, not proof of the entire latency cause or a guaranteed two-second result after adjustment. The earlier conclusion that the next step requires firmware changes was premature and is withdrawn as an investigation prerequisite. Continue prioritizing Mac-versus-Windows bridge comparison. A new Mac candidate removes the extra 15 ms after each confirmed new voice request and separates notification timing. Local build/checks pass and it is running, awaiting device reconnection for physical validation. Firmware remains untouched. Permanent list-offset repair, syncing-hint auto-recovery, firmware writes and camera allocation remain deferred; long-term stability, real-stream interruption recovery and first-permission paths stay open.

2026-10-21 最新：正常运行未提交候选 `artifacts/mac-tab5-20261021/stock-codes/AIBotBridge.app`，七项已授权功能开关保持开启。当前对话此前进展恢复／运行中实时刷新、语音两段顺序／各一遍回填，以及 Mac 默认麦克风约 30 秒含两次停顿的录音与文字保留均获实机通过。较早的偶发语音中断本轮未复现，根因尚未确定，保留诊断继续观察；未改固件或设备设置。后续各候选、失败与修正证据见文末；本节以下旧检查点按当时上下文保留。

Latest on 2026-10-21: the normal uncommitted candidate is `artifacts/mac-tab5-20261021/stock-codes/AIBotBridge.app`, retaining all seven authorized opt-ins. Earlier public progress/live updates, two-utterance order and single-copy staging, and an approximately 30-second Mac-default-microphone take with two pauses and complete text retention have physical confirmation. The earlier intermittent speech disconnection did not recur in this take; its cause remains unknown and diagnostics are retained for observation. Firmware/device settings are unchanged. Candidate-specific failures and corrections appear at the end; earlier checkpoints below retain their historical context.

用户最新授权保留 Codex 日常只读，已启用并连接 29 个近期会话；此前“临时验收后关闭”记录为历史。完整 Codex 操作现已获开发请求，仍待实现／验证，真实发送只针对指定测试任务。

Latest authorization retains daily Codex read-only access, now enabled with 29 recent sessions; earlier opt-out notes are historical. Full Codex controls are newly requested development work, not verified functionality; live sends require a designated test task.

当前运行 `06dd164`，完整 [Mac CI 39400345443](https://github.com/yaoyouzhong/AI-bot/actions/runs/39400345443) 与附件核验通过：190 项测试、0 失败／跳过，本机 Release／签名包通过。实体已确认网络曲线、首页工作日、DeepSeek／智谱余额及微信二维码，Open-Meteo 当前／24 小时／7 天天气，以及和风来源／实时天气／AQI 等级／站点气压／24 小时／7 天预报均正常。用户已返回原日常总览，保留其选择的和风和自动定位；USB／Wi-Fi／BLE 正常。未记录位置、密钥、余额或天气具体数值，未修改或刷写固件。下文保留各阶段当时的待验与失败记录。

Running candidate `06dd164` passed CI 39400345443 and full artifacts (190 tests, zero failures/skips), plus local Release/signing. Physical network curves, workday text, DeepSeek/GLM balances and WeChat QR passed. Open-Meteo current/24-hour/seven-day weather passed; QWeather source/current conditions, AQI category, station pressure and both forecast tabs also passed. The user returned to the normal overview, retaining QWeather and automatic location; USB/Wi-Fi/BLE remain healthy. No location, credentials, balances or specific weather values were recorded; firmware was unchanged. Historical pending/failure checkpoints remain below.

以下音乐检查点保留其当时结果：

当前运行程序为 `3fc848b`，[Mac CI 39388819221](https://github.com/yaoyouzhong/AI-bot/actions/runs/39388819221) 与完整下载附件已核验：173 项测试、0 失败／跳过，音乐 ASan/UBSan、Release、候选包／工具、8 张原生 PNG、源码及包哈希通过。用户处理钥匙串后新候选 USB 数据确认、Wi-Fi 认证请求与 BLE USB 优先待命均自动恢复。`604ac8c`／`a3bd642` 的 Codex 只读与阅读位置保持验收结论仍保留，不因额度或音乐修复重新开启会话访问。

本轮补齐 Mac 原来遗漏的七日额度历史、逐张重置卡到期信息和实际窗口长度；真实接口预检返回可用周额度及完整重置卡日期，用户已确认实体卡片信息正常。首个历史样本尚不能计算增量，七日图显示未知；已自动观察到相隔 120 秒的两次真实采样，用户对“今天显示 0、前六天仍为 --”反馈正常，该显示路径通过；后续用户又确认当日数值随实际采样增加，动态累计显示正常。过去未采集的日期保持未知，不反推或填造历史。

音乐重新按 Windows 发送路径排查：有有效时间轴时，Mac USB 从 2 秒改为 500 ms，保留相位及阻塞保护；不修改或刷写固件。新候选已临时开启音乐、导入并播放原本地测试曲目，其他交互开关仍关闭。`d93c318` 出现当前播放时间间歇消失、总时长不变，未通过。只读进程采样定位主线程活动扫描阻塞音乐 Apple Events；`3fc848b` 改为后台扫描后累计 281 帧均可用、最大样本年龄 0.53 秒，用户确认当前时间显示及逐秒前进“正常了”，本轮 USB 音乐计时通过，详见[音乐调查](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md)。自动在 Music 恢复播放后再暂停，累计 370 个生成帧仍无不可用时间轴、最大样本年龄 0.53 秒。随后关闭音乐开关并确认“尚未启用”，仅将原测试曲从资料库移除并放入可恢复废纸篓，退出 Music；其他交互开关均关闭。Mac 已返回 TAB5 主窗口，三通道正常；用户已确认设备返回原日常总览页。下表与旧段落保留先前结果。

| 交接阶段 | 当前结论与剩余项 |
| --- | --- |
| 1 本机基础 | Release、候选签名、正常运行／窗口／退出与 CI 173 项通过；本机 XCTest 工具链仍受阻 |
| 2 USB | 配对、数据、设置读写、拔插与桥接重启恢复通过 |
| 3 Wi-Fi | 纯 Wi-Fi 认证与设置读写、USB 返回通过；配网、地址恢复、固定模式和八秒阈值精测待验 |
| 4 BLE | 实际认证／数据／设置、开关重连、睡眠恢复、USB 优先、系统撤权恢复通过；首次直接拒绝与精确延时确认待验 |
| 5 展示与资源 | 亮度持久化、设备声音、固定页／轮播／屏保、图库导入／失败保留／分页／方向通过；USB Apple Music 均匀计时本轮通过；日常数据逐项、桌宠及其他音乐通道待验 |
| 6 系统与音乐 | Apple Music 标题／封面／单次控制、系统音量／静音／取消静音、文件夹入口通过；Spotify、程序入口、锁屏／辅助功能拒绝恢复待验 |
| 7 Codex | 公开 socket 缺失与 proxy 传输假设已查清；不部署服务的标准 stdio 只读连接、实体首页任务、公开回复和阅读位置保持通过。实时状态／发送／追加／中断仍不可用 |
| 8 照片与语音 | 语音授权、空录音停止提示通过；HTTP 长连接修复后纯 Wi-Fi 单次成功识别，收到 7.4 秒音频；静默提示短停顿复验通过；USB 单次录音及取消保留原名称通过；BLE 语音／长期稳定性、逐字准确率、照片／任务附件待验 |
| 9 固件工具 | 官方包离线检查、正常 App 版本／SHA／说明预览、当前镜像只读 USB 启动核验通过；实际 OTA／首刷／恢复均未执行 |

临时音乐页／开关／曲目、系统音量／静音和文件夹入口均已收尾。语音测试开关已关闭，用户退出未保存测试日程返回日常页面；USB 已接回自动确认，蓝牙已恢复认证及 USB 优先待命。最终只读设置与阶段基线一致，表单取消未保存。不将整体九阶段写成完成。

## Codex 只读上机后续

`604ac8c` 正常候选已自动启动，用户处理钥匙串，并明确允许临时只读验收、完成后关闭。助手自动启用后，交互窗口显示“只读已连接本机 Codex · 29 个近期会话”；一个桥接自有 CLI 子进程与单一 8765／USB 串口所有者匹配。USB、Wi-Fi、BLE 均恢复。实体 TAB5 首页任务卡已显示 Codex，AI-bot 项目任务公开回复正常显示；历史／分页操作反馈正常显示，但没有独立确认持续停留所选位置。发送、追加、中断、resume、桌面输入框及其他交互开关均保持关闭。随后关闭 Codex 只读，正常界面恢复“尚未启用”。未修改设备展示设置或固件。

自动合成检查发现四秒刷新会重置阅读轮次／页码；补充修正保留成功显示的位置、失败导航不覆盖选择，明确回到最新后继续跟随新轮次。本机 Debug 合成复验与 Release／签名候选通过；本机 XCTest 仍缺模块未执行。补充修正 `a3bd642` 的 Mac CI 及完整包核验为 164 项、0 失败／跳过，新增回归实际执行通过。新候选实际位置保持已复验通过，详见[Codex 链路后续](MAC-TAB5-CODEX-LINK-20261020.md)。以下旧记录保留其当时上下文。

`a3bd642` 正常候选启动后，USB 数据、Wi-Fi 认证请求、BLE 认证及 USB 优先待命均自动恢复；只读启用时只有一个桥接自有 CLI 子进程，8765 与 `/dev/cu.usbmodem101` 均由同一桥接独占。用户对“公开回复显示、上一轮／第二页停留至少十二秒且不跳回”明确检查反馈“都是正常的”，因此当前候选公开回复与历史阅读位置保持实机通过。助手随后关闭只读，确认界面“尚未启用”、自有 CLI 退出、全部交互开关关闭、入口为空，主窗口三通道仍正常。用户返回原日常总览页；未保存测试内容或修改展示设置、连接模式、图库、声音及固件。关闭只读后首页回退电脑状态为预期。

## 音乐计时与 Codex 后续

- 音乐已准备原创主机 C 修正参考、生命周期回归与精确 `.234` 源码的接入补丁生成器。变化延迟回放由原表达式 900–1100 ms 改善为候选 1000 ms；本机 UBSan、CI ASan/UBSan 通过，错误来源包被拒绝，补丁只在临时源码副本检查应用。**参考头文件未进入固件构建，没有固件镜像／ESP-IDF 构建／设备写入，实体音乐均匀计时仍未通过**。350 ms 内跳转与抖动不可可靠区分的限制已记录，见[音乐后续](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md)。
- 本机 CLI 导出的公开 schema 确认 App Server 的 `active` 与设备 `working` 词汇不同；现已映射运行／等待／未知状态，首页优先实际运行任务，并在小列表中保留阅读与首页任务。代理握手前退出等错误采用安全分类，失败保留公开缓存、撤下控制。四项新回归通过。实际 RPC 对象与已安装 CLI 在空临时 CODEX_HOME 的短暂 stdio 隔离探测中通过初始化／空列表／不存在合成任务的拒绝；未读取用户会话、未登录或调用模型，也未创建真实任务。当前桌面公开代理仍因 socket 缺失受阻，未部署 daemon，见[Codex 后续](MAC-TAB5-CODEX-LINK-20261020.md)。
- 用户询问首页从 Codex 变为“电脑状态”。精确运行源码的 `render_home_task` 已确认：没有有效任务时，同一任务卡片回退 CPU／内存；当前 Codex 开关关闭且代理未连通，因而无可用任务。没有改首页布局或展示设置。
- 正常退出旧 `4e0b253` 后，自动启动精确 `ef773a0` 候选；用户处理同类钥匙串授权。随后助手自动选取正常窗口确认 USB 数据 ACK、Wi-Fi 已认证请求、BLE 已认证且 USB 优先待命。单一候选进程、8765 监听及串口独占已核对。全部交互开关仍为关闭、入口为空；未启用真实任务读写，未保存设备设置，未新增日程或重放音乐夹具。窗口已返回 TAB5 主界面。没有为文档／离线参考变动再次更换签名候选。

实际程序 `ef773a06ee902520f24461ce517d2dece1cda77e` 的 [CI 39381673453](https://github.com/yaoyouzhong/AI-bot/actions/runs/39381673453) 也通过；小报告核对 156 项、4 项新增回归及音乐日志。`1788914521bd8349baeeb829bcca8699ce3fd6c1` 只增加音乐参考的失联恢复／旧样本边界，最新完整附件核验汇总保存在忽略目录 `ci-1788914-verification.json`。本机 XCTest 仍未执行，不把隔离检查或 CI 记作真实任务与设备计时通过。

## 环境与基础检查

| 检查 | 实际结果 |
| --- | --- |
| 架构 | `arm64`（Apple Silicon） |
| macOS | `27.2`，build `26B5112a`（`sw_vers` 实测） |
| Swift | Apple Swift `6.2.3`，当前 developer directory 为 `/Library/Developer/CommandLineTools` |
| `swift test --package-path mac-app` | **受阻，未执行任何测试**：测试目标编译报 `no such module 'XCTest'`。当前工具链也没有 `xctest`，未找到已安装的 Xcode。此结果不能记为测试通过或应用测试失败。 |
| Release | **通过**：`scripts/build_macos_app.sh` 内执行 `swift build -c release --package-path mac-app` |
| 候选包 | **通过**：Info.plist、固定版本刷机工具包 SHA、ad-hoc 签名及严格签名验证 |
| 内置刷机工具 | **通过**：正常候选包的 `--self-test-flash-tool`，仅帮助/版本检查，无设备访问 |
| 原生界面夹具 | **通过**：`--self-test-tab5-ui` 生成 8 张 PNG；不是正常启动、权限或硬件验收 |
| 构建警告 | Swift 6.2.3 报 `SerialBridge` 跨 `@Sendable` 闭包捕获的既有警告；未靠添加未经证明的 Sendable 声明压制 |
| 正常 App 运行 | **启动、菜单栏、正常退出与重启通过，人工反馈**：用户从访达打开初始候选，确认菜单栏 `C:… A:…`；后正常退出，改开 `f3f9dd6` 候选。新旧进程路径、8765 监听和串口所有者已核对。界面控制工具选择候选 App 和访达均超时，不能据此认定 App 启动失败；其余窗口与权限仍单独验收 |
| 实际 HTTP 鉴权 | **通过**：已运行候选的 `/status` 与 `/tab5/v1/status` 对无认证本机请求均返回 HTTP 401；未读取状态正文或配对材料 |

本机候选与离线证据位于忽略目录 `artifacts/mac-tab5-20261020/`，未安装到 Applications。不得将该目录的本地运行数据加入 Git 或 CI 附件。

初始 HEAD 的 [Mac CI 39353897262](https://github.com/yaoyouzhong/AI-bot/actions/runs/39353897262) 已成功；交接程序基线仍为 `f41a429`。CI 结果不替代本机尚未执行的测试。

## USB 与设备

| 检查 | 实际结果 |
| --- | --- |
| 端口 | `/dev/cu.usbmodem101` |
| USB 描述符 | `TAB5 Control`，vendor `TAB5 development`，VID `0x303a`，PID `0x4005` |
| 只读身份检查 | **通过**：以 `460800` 发送一条 `@AIBOT ` v1 `tab5_ping`，收到目标 `tab5_hello`；未配对、未改 Wi-Fi、未写固件 |
| 实际固件 | `0.2.234-ui` |
| 能力 | `rpcVersion=1`、`rpcUsbBinary=1`、`usbPacked=1`、`usbDelta=1` |
| 初次 UI 诊断样本 | `uptimeMs=56152410`、`uiAgeMs=13`、`flushCount=34381`、`flushErrors=0`；单份样本不构成启动健康验收 |
| 正常 App 配对、状态 ACK | **通过，人工反馈**：用户明确选择端口并配对，窗口显示 USB“数据已确认”；真实屏幕数据仍待观察 |
| 实际数据与屏幕 | **基本画面正常，人工反馈**；具体 Mac 数据、页面与触摸仍待逐项观察 |
| USB 拔插 | **通过，人工反馈**：断开约 12 秒后重新连接，未点击重新配对，USB 自动恢复“数据已确认” |
| 桥接正常退出与重启恢复 | **通过，人工反馈**：从初始候选切换 `f3f9dd6`，未重新配对，自动恢复 USB“数据已确认” |

显示设置的真机读取已通过。首次保持值不变保存时，设备仍处于本机设置页，收到“TAB5 正在编辑本机设置，请退出设备设置页后重试。”，记录为设备忙碌拒绝，未记为保存成功。用户退出设备设置页后，重新读取并原值保存，收到“TAB5 已确认保存显示设置。”，确认匹配成功回执；未改变用户配置。亮度、声音及重启保持的后续实机结果见下方；其他显示设置仍逐项验收。

不将真实设备配对材料、网络配置或原始串口流写入记录。ESP8266 未连接或执行硬件验收，不宣称通过。

## 显示设置与设备声音实机验收

在 `791d898` 正常候选中，先读取设备实际设置，并仅将恢复基线保存在忽略的本机证据目录，不提交用户完整配置。

- **亮度效果、正常开关机持久化及恢复通过，人工反馈**：只降低亮度，收到匹配保存回执，实体屏幕明显变暗。用户通过正常电源操作关机再开机后，USB 自动恢复“数据已确认”，画面正常；重新读取确认临时亮度值仍被保留。随后恢复原亮度并收到保存成功，用户确认屏幕亮度恢复。未进入下载模式、未写入固件。
- **设备声音开启试听通过，人工反馈**：仅降低 TAB5 设备提示音量，保持非静音，收到保存回执；在实体“设置 → 屏幕声音”点试听，用户明确听到提示音。此项不涉及 Mac 系统输出音量，也没有将“听到”推断为音量精确比例正确。
- **设备静音设置与试听禁用通过，人工反馈**：Mac 勾选静音保存成功后，用户看到实体试听按钮变灰。公开 `.234` 设备端 `sound_refresh` 在静音或音量为零时禁用试听，与观察一致；不将禁用按钮写成已执行的无声试听。随后恢复原设备音量并取消静音，用户确认保存成功。其他显示与资源结果分别记录如下。
- **固定页切换通过，人工反馈**：仅将固定页临时改为时钟，收到保存成功，实体 TAB5 切到时钟页。
- **轮播页面与切换通过，人工反馈**：临时启用两个页面、调整顺序并选择十秒间隔，用户确认保存成功，随后观察反馈“正常在切换”。加入第三个页面后，针对指定三页顺序的观察请求，用户再次反馈正常切换。没有仪器测量精确间隔，也没有单独取得第三次保存回执的原文，不扩大为精确计时验收。
- **屏保进入与触摸退出通过，人工反馈**：恢复原固定页、页面勾选、顺序、间隔及轮播开关后，仅临时缩短屏保等待，收到保存成功。无触摸／键鼠操作约七十秒的观察请求后，用户确认自动进入屏保；轻触后反馈正常退出，具体返回页和亮度没有另行详述。随后恢复原屏保等待并收到保存成功，本轮临时显示设置均已按基线恢复。

## 图库导入、分页与方向

`791d898` 正常候选导入已校验的官方名画 ZIP，用户确认成功导入 366 件作品；只读核对启用的清单与官方包逐字节一致（826 张图）。这与此前仅在隔离目录做的离线验证分开记录。

当天作品只有单页，不能据此宣称图片翻页通过。因此从同一官方包复制一件明确标为 CC0 的多页作品，保留原始图片、来源、许可和导读，生成仅在忽略目录使用的临时验收子集（1 件作品，横竖屏各 10 页，共 20 张图）；它不是正式交付包，不改系统或设备日期。用户在正常 App 成功导入，实体选择名画预览后确认图片、翻页箭头和页码正常，前后翻页也正常。随后竖屏翻页再转回横屏，用户确认两种方向的图片、文字与触摸均正常，无黑屏或异常裁切。

仅改动一字节图片、保持清单 SHA 不变的本地错误包，在隔离加载器与正常 App 中都被拒绝。正常 App 提示“图库文件校验失败”；前后 28 个已安装资源文件的 SHA 全部一致，用户随后明确确认实体仍可正常翻页。失败保留旧图库与可用画面通过。临时子集、错误包和来源文件均未加入 Git。用户随后重新导入完整官方包并确认成功；只读核对清单恢复为 366 件作品、826 张图且与官方包一致。最后用户恢复原屏保类型和自动旋转，退出设备设置页回到总览，临时资源与显示设置收尾通过。

## 本轮修复：音乐读取失败保留成功缓存

源码复核发现，自动化权限拒绝、超时或无效歌曲结果都返回空读取；原轮询将其计作无播放，累计三次后清空最近成功的歌曲和封面。使用真实 `MacMusicService` 编译对象与合成读取后端，已复现四次读取失败清除预置成功缓存。此检查没有启动音乐 App 或发送 Apple Events。

轮询现在区分读取失败与已确认停止／程序退出：若没有成功选中的歌曲且任一运行中播放器读取失败，保留最近成功数据和封面，并重置连续空样本计数；连续三次已确认无播放仍清理，用户明确关闭音乐功能仍立即清理。播放器选择顺序、Apple Events、权限、单次控制与读回逻辑保持。四个回归覆盖连续失败、另一播放器停止时仍有失败、失败打断空样本计数，以及明确关闭不探测播放器。

独立本机检查已确认修复后缓存保留、已确认退出仍清理、明确关闭仍清理且不探测；Release 构建通过。它不代替 XCTest 或真实音乐验收。本机 `swift test` 仍在编译测试目标时报缺少 XCTest，没有测试执行。程序提交 `50240016ba810f1f302a6e77e598f7b8c7ffbcf8` 的 [Mac CI 39362895479](https://github.com/yaoyouzhong/AI-bot/actions/runs/39362895479) 与完整下载附件核验通过：122 项测试，0 失败、0 跳过，Release、候选包、工具、8 张原生 PNG、源码与内外层包哈希一致。本机候选包及签名通过，位于 `artifacts/mac-tab5-20261020/5024001/AIBotBridge.app`。新正常候选的自动化授权、播放／暂停实机回归待继续。

## 本轮修复：音乐播放控制遵循独立授权

进入音乐实机验收前，用户确认电脑控制与音乐两个开关原本均关闭。新候选 `5024001` 启动后，USB“数据已确认”、蓝牙“已认证，USB 优先待命”；只读进程核对仅该候选运行，无认证的两个状态路由仍返回 HTTP 401。用户启用音乐后看到自动化授权弹窗，但因没有订阅，尚未播放曲目；没有将弹窗出现写成播放通过。已准备带原创几何图形封面的原创本地测试音频，不需要订阅，不读取用户现有媒体；导入、播放与后续从资料库移除分别跟踪。

另一个独立复现显示：仅打开音乐开关时，`computer-control` 的全局电脑开关检查返回 403，播放动作没有执行，且状态未通告共有控制通道，与界面“允许读取并控制音乐”不一致。现在音乐开关单独授权媒体动作，并在它或电脑开关启用时通告已有控制版本。音量、静音、锁屏和常用入口仍只由电脑控制开关授权；音乐单独启用时隐藏这些能力和数值，电脑单独启用时隐藏媒体控制。请求去重、执行前权限复核、实际播放器绑定和结果读回保持。现有 v1 消息和字段未改变，已对照 TAB5 的能力标志消费逻辑。

四个回归覆盖音乐独立控制及电脑动作拒绝、反向授权隔离、撤销后拒绝旧请求，以及共有通道能力随开关变化。使用实际编译对象的合成后端检查先复现 403，再确认媒体动作成功且音量写入仍拒绝；未发送真实 Apple Events。Release 通过；本机全量 XCTest 仍缺少模块、未执行。程序提交 `c51a1bb8aa59afcaea9cd2735071da96b0fb8b72` 的 [Mac CI 39364551773](https://github.com/yaoyouzhong/AI-bot/actions/runs/39364551773) 及完整下载附件核验通过：126 项测试，0 失败、0 跳过，Release、候选 App、工具、8 张原生 PNG、源码 SHA 与包哈希一致。本机候选 `artifacts/mac-tab5-20261020/c51a1bb/AIBotBridge.app` 构建、随包工具与签名检查通过。正常 App 的后续自动操作见下节；实体音乐显示、播放／暂停及拒绝恢复仍待继续，没有将 CI 写成实机控制通过。

## 恢复 Mac 界面自动化后的音乐实测

用户提出应尽可能自动操作 Mac，避免将常规导入和窗口操作全部交给人工。重新检查后，Computer Use 已可读取和操作音乐、桥接设置与系统活动监视器窗口；此前的超时不再作为所有 Mac 操作均需人工的理由。系统授权和实体 TAB5 的屏幕／声音／触摸仍与软件自动检查分开记录。

已通过正常音乐 App 的“文件 → 导入”自动导入只含原创测试 M4A 的文件夹，歌曲列表确认只有该测试曲目，时长十分钟。自动播放后，实际窗口显示测试封面和暂停按钮，展开播放中视图后确认经过时间已推进。部分无障碍滑块标识仍为静态“未在播放”，未单独把这些标识当作播放器真值。测试曲目无需订阅，未读取其他用户媒体；资料库临时条目仍待收尾移除。

正常 `5024001` 候选中，自动重新读取显示设置，仅临时切换到音乐页，已从固定底部读到“TAB5 已确认保存显示设置。”。实体曲名、封面和触摸控制尚无反馈，不将 Mac 封面或保存回执算成设备显示通过。本阶段 fresh read 的显示模式与较早人工基线不同，已在忽略目录另存本阶段恢复值；后续只恢复本阶段改动，不凭旧记录覆盖当前设置。

随后通过活动监视器对精确匹配的旧候选选择“退出”，未选择强制退出；确认旧桥接进程消失后，用 Computer Use 启动精确 `c51a1bb` 候选。该方式按 [Apple 说明](https://support.apple.com/en-ie/guide/activity-monitor/actmntr1002/10.14/mac/15.0) 走正常退出流程。新进程存在，但系统安全授权尚未完成、状态监听尚未启动，不能记为新候选启动或通道恢复通过。Computer Use 明确禁止访问 SecurityAgent，已要求用户仅在 Mac 上处理匹配的钥匙串提示并打开一次 TAB5 窗口；没有读取、输入或记录密码。常规窗口操作将在此后继续由助手完成。只读进程采样进一步确认主线程仍在 `SecItemCopyMatching` 等待系统钥匙串服务；只检查调用栈，不读取钥匙串内容。测试曲目已自然播放结束一次，未打开循环播放。

## 本轮修复：音乐进度回退与控制延迟

用户完成 `c51a1bb` 系统授权并打开 TAB5 窗口后，自动读取确认 USB 数据、Wi-Fi 认证请求和蓝牙 USB 优先待命均恢复；唯一桥接进程路径正确，未认证的两个状态路由均返回 401。音乐开关保持开启，电脑控制关闭。原创测试曲目自动重新播放后，用户确认实体曲名与蓝橙封面正常；实体各点击一次暂停、播放，用户反馈状态切换，Mac 正常音乐窗口也分别显示已暂停／播放中与相反动作按钮。因此封面显示和单次控制效果通过。

用户随后明确报告控制延迟、进度跳秒及往回跳，音乐计时验收记为未通过，不能由前述控制效果覆盖。源码确认原音乐轮询与总状态发布均为两秒，轮询异步启动但总状态立即发布旧缓存；USB 还会重复发送该缓存，且 Apple Events 将进度取整。公开 `.234` 的界面在每次收到状态后，以该进度重新开始本地计时，故重复旧采样会造成回退。控制后实际播放器读回原来仅用于判断成功，没有立即回填状态。

修复保留小数进度，独立每半秒轮询（同一轮未完成时不重入），取包时使用最新音乐采样，并按采样时间投影到实际发送时刻。仅最近三秒的有效时钟允许推进，过期／未来采样沿用既有 `timelineAvailable=false` 隐藏未知进度，保留最近成功曲目与封面。暂停不推进，真实回退跳转仍按播放器值生效。确认的控制读回立即更新音乐缓存；USB 对曲目、播放状态和真实跳转优先发送，普通进度保留原状态传输频率。旧轮询和延迟封面不能覆盖较新控制读回，也不能将旧曲封面挂到新曲。没有新消息类型、协议版本或设备固件变更。

新增八项回归覆盖重复帧的设备计时契约、暂停／真实跳转、过期／未来采样、小数及曲末边界、USB 优先条件、迟到元数据／封面，音乐绕过旧总状态缓存，以及曲名与封面同步取自同一最新采样。使用实际编译对象的独立检查在修复前复现重复位置，在修复后确认重复发送按采样年龄前进且不修改原缓存；没有访问设备或发送 Apple Events。本机 Release 通过，全量 XCTest 仍因缺少模块未执行。新候选的正常 App 播放／暂停、计时与延迟实机复验待继续；此时仍运行 `c51a1bb`。

`754fd02` 正常候选已自动构建、签名、正常退出旧候选并启动，用户完成匹配系统授权；自动检查再次确认 USB、Wi-Fi 认证与 BLE 待命恢复、唯一精确进程和两个未认证状态路由 401。其 [Mac CI 39367861614](https://github.com/yaoyouzhong/AI-bot/actions/runs/39367861614) 与完整下载附件核验通过：134 项测试，0 失败、0 跳过，Release／App／工具、8 张原生 PNG 和源码／包哈希一致。

该候选的三十秒观察首先得到“仍有回退或明显跳秒”，进一步询问后用户明确为“只向前跳秒，没有往回走”。因此本次观察未再报告回退，但向前跳秒仍未通过，不能写成整体计时通过。继续对照设备将整秒基值与收包后的整秒增量相加的逻辑，发现 USB 原来在 ACK 完成后再等两秒，传输／回调耗时会累积到发送相位。修复为按原截止时刻推进发送计划；控制变更另起相位，过长阻塞后跳过错过的时隙，避免补发突发。新增两项回归覆盖多轮 ACK／回调延迟不累积、控制及长阻塞。合成相位模型可出现短时间相邻秒变化，固定相位消除该模型现象；它不是真机计时证据，实机复验仍待继续。

固定节奏程序提交 `3c82bcce4df49af2b3e1c64445664500eabf7a43` 的 [Mac CI 39368358000](https://github.com/yaoyouzhong/AI-bot/actions/runs/39368358000) 与完整附件核验通过：136 项测试，0 失败、0 跳过，Release／App／工具、8 张原生 PNG、源码与包哈希一致。实际编译对象的独立节奏检查也通过二十轮不同 ACK 耗时、控制另起相位及长阻塞不突发；没有访问设备。本机 Release、候选与签名通过，XCTest 仍缺少模块、未执行。

助手通过正常退出切换到精确 `3c82bcc` 候选，用户完成系统授权并确认打开 TAB5 窗口；唯一精确进程和两个状态路由的未认证 401 已自动核对。该轮桥接窗口的界面工具选取连续超时，重建工具连接也未恢复，不能擅自写入 USB／BLE 状态文字已复核。只读进程调用栈包含正常 AppKit 事件循环、音乐轮询与 USB 工作，未证明持续卡死，也没有读取钥匙串内容。音乐 App 的自动操作仍可用，测试曲目已重新播放。

实体四十五秒观察后，用户反馈“不跳秒了，但是时间前进的不均匀”。因此跳秒现象改善，但音乐均匀计时仍未通过。随后实体分别点击一次暂停、播放，用户均反馈一秒内生效；Mac 音乐窗口也分别确认已暂停／播放中，以及相反动作按钮。这是人工估计的响应时间，不是仪器测量。对应 `.234` 固件音乐计时的整数截断／每包重设相位逻辑、合成回放证据及后续修正要求，见[音乐计时固件后续项](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md)；未修改或写入固件。

用户将 TAB5 窗口置前后，界面工具恢复选取，自动核对 USB 数据确认、Wi-Fi 已认证请求和蓝牙 USB 优先待命。随后重新读取设备设置，只把本阶段临时音乐页恢复为进入本阶段时的页面模式，收到固定底部匹配保存回执；轮播及其他值未改。音乐开关恢复关闭，电脑／Codex／语音开关仍关闭。测试曲目先暂停，再通过 Music 的正常资料库删除流程移入可恢复的废纸篓，歌曲列表不再含该条目；原创源文件留在忽略目录，未清空废纸篓。Music 正常退出已用进程检查核对。本阶段音乐临时设置和媒体收尾完成；系统权限授权保持原系统管理方式，未放宽钥匙串 ACL。

## 正常 App 的固件预览与只读启动核验

在同一 `3c82bcc` 正常候选中，自动从“资源与固件”选择官方 `.234` 升级 ZIP。窗口确认版本 `0.2.234-ui`、精确应用 SHA-256 `1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8` 和含分类标题／列表的实际 sidecar 说明，并明确显示尚未开始升级。随后只点击“核验所选固件的启动状态”，收到“USB 启动核验通过：精确 ELF、VALID 分区和三个实时界面样本一致。”。这是已运行镜像的只读核验，不是本轮写入／OTA 验收，也没有新的屏幕触摸观察。最后点击“停止提供所选固件”，确认“尚未选择固件 / 已停止提供固件。”。没有点击请求开始升级、重启或 Flash 写入。

## 本轮修复：重新打开与窗口快捷键

窗口置前可恢复界面工具，但关闭子窗口后工具再次无法选取桥接，`Command-W` 也没有关闭窗口。源码中的 accessory App 没有重新打开处理或主菜单。补充正常的 App reopen 委托：再次打开已运行的 App 时显示 TAB5 主窗口；补充标准窗口菜单、`Command-W` 关闭和 `Command-Q` 退出。关闭仍由窗口既有忙碌保护决定，退出仍经过现有 App 委托保护；没有自动批准权限、外部控制接口或部署变更。

本机 Release 编译通过，既有 SerialBridge Sendable 警告未变；本机 `swift test` 仍因缺少 XCTest 模块在测试执行前受阻。此前文档提交 `61e86ac` 的 [Mac CI 39374024338](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374024338) 和完整附件核验通过 136 项测试、0 失败／跳过，Release／App／工具、源码和包哈希一致；窗口修复的 CI 与正常 App 检查另行记录。

程序提交 `3db70a3dece89c39acd71743007b8213ae366600` 的 [Mac CI 39374705006](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374705006) 和完整附件核验通过：136 项测试，0 失败／跳过，Release／App／工具、8 张原生 PNG、源码与包哈希一致。本机正常候选构建／严格签名检查通过；用户处理匹配的钥匙串授权后，助手自动打开 TAB5，核对实际三通道状态与未认证 401。`Command-W` 关闭交互子窗口并回到主窗口通过；标准窗口菜单包含关闭与 TAB5 入口；`Command-Q` 退出后旧 PID 消失。随后自动启动同一候选，首次选取因尚无窗口超时，再次打开已运行 App 即自动显示 TAB5；无需用户手动打开窗口，USB 随后自动恢复数据确认。保留菜单栏 App 首次启动不主动弹窗的行为，不能据此宣称所有工具超时均已消除。

## Mac 系统音量与静音实机验收

在正常 `3db70a3` 中自动启用电脑控制，音乐仍关闭。使用实际产品的 CoreAudio 只读路径取得操作前音量／静音恢复基线，不通过测试后端发送控制。用户在实体“应用 → 控制”拖动后反馈 17%；Mac CoreAudio 读回与正常系统声效面板均为 17%。实体分别点击一次静音、取消静音后，用户确认按钮切换，CoreAudio 分别读回 true／false，原音量保持。因此真实 TAB5 → Mac 的音量、静音及取消静音控制通过；没有用模拟请求代替实体触摸。

助手随后通过正常系统设置恢复操作前音量，CoreAudio 再次确认音量和非静音均已恢复；正常退出临时打开的系统设置，关闭电脑控制开关并核对其他开关保持关闭。重新读取设备显示设置与阶段恢复值一致，只取消表单，不再保存。此项与 TAB5 自身提示声音、锁屏／辅助功能、常用入口、Spotify 分别记录。

**常用文件夹入口通过**：原入口列表为空，助手在正常 App 临时添加本轮新建的空目录 `TAB5-LaunchTest` 并启用电脑控制。用户在实体控制页点一次该入口后，访达从原候选目录窗口切为该测试目录，显示零个项目；未直接在 Mac 打开该目录来替代设备操作。随后关闭测试访达窗口、从正常 App 移除唯一临时入口并关闭电脑控制，确认列表再次为空，最后移除助手创建的空目录。应用程序入口、锁屏／辅助功能和权限拒绝恢复仍单独待验，不以文件夹打开代替。

## 语音授权与提前中断排查

正常 `3db70a3` 中由助手触发语音授权，用户完成系统确认，窗口与人工反馈均为“语音权限已授权；开始时仍会检查本地语言模型是否可用。”。电脑、音乐和 Codex 开关保持关闭。日程名称使用设备已有的独立语音 owner，不需要启用或发送 Codex 任务；测试只进入未保存的名称编辑。

用户报告能识别，但多次在尚未来得及说话时自动结束，提示连接中断，因此稳定性未通过，未取得准确最终文字，不写成完整识别验收。对照实际 `.234` 源码，`voice_remote.c` 在请求没有可接受回复时进入连接中断／恢复；Mac 识别器回调错误则使用另一条“识别中断”提示，不能将两者混为静音结束。

助手先在正常 App 关闭语音、正常退出桥接，确认无进程／串口所有者后，以 460800 只发送一次 `tab5_ping`，读取并仅输出白名单数字／阶段码：最近语音请求 2 ms、seq 24，USB RPC 历史计数 83、最近阶段 1、状态 0／零回复字节。计数覆盖此前运行，不足以对应每次失败或锁定竞争／重置的根因。未记录原始串口流、音频、识别文字或配对材料，未重置／写入设备。随后正常恢复同一候选，等待 USB 数据确认后才启用语音。

一次三秒静默、手动停止的定点检查得到“识别中断，文字可能不完整，请检查后重新录入”；用户明确它在点击停止后才出现，此轮没有复现提前结束。两个 Mac 窗口采样均显示 USB 数据确认，不代表覆盖所有瞬时变化。原实现丢弃 NSError 的领域／编号，将所有识别错误合为同一提示；[Apple 错误说明](https://developer.apple.com/documentation/speech/sfspeechrecognitiontask/error) 将无语音、资源、授权和服务故障分开列出。

修复保留受控错误分类：只有用户明确停止、没有部分文字且得到无语音结果时，进入不能插入的空预览；提前结束、连接超时或未确认的部分文字仍失败。资源／权限／识别器忙碌／本地服务连接与未知数字错误码分别提示，不显示原始错误描述。交互窗口新增收音／本地识别／待检查／错误状态，不显示转录、任务或设备标识，便于助手直接核对。协议字段、强制本地识别及停止／取消界限保持。

新增五项回归覆盖空录音停止、提前无语音、部分文字不确认、按领域分类、连接超时不伪装为手动停止，以及窗口摘要不泄露转录／错误原文。本机 Release 通过，XCTest 仍缺少模块、未执行；新正常 App／CI 复验另记。当前语音开关已恢复关闭；原始连接中断未被此错误提示修复宣称解决。

### `b012fe6` 复验

程序提交 `b012fe63861d1f524607bcbec045472252ad2810` 的 [Mac CI 39376374679](https://github.com/yaoyouzhong/AI-bot/actions/runs/39376374679) 与完整下载附件核验通过：141 项测试，0 失败／跳过，Release、候选 App、工具、8 张原生 PNG 和源码／包哈希一致。新增语音状态行在完整原生截图及正常 App 中可见，没有遮挡后续控件。本机 XCTest 工具链限制不变。

正常退出旧候选后自动启动该精确路径，用户处理同类钥匙串授权，助手自动打开 TAB5 并核对 USB 数据确认、Wi-Fi 已认证请求及蓝牙 USB 优先待命。随后自动启用已授权的本地语音，其他控制开关保持关闭。一次三秒静默、手动停止后，用户反馈“没有识别到文字，请准备好后重录。”；Mac 状态也为“没有识别到文字，请重新录入”。本次空录音提示通过，没有把它记作原提前连接中断已修复。随后正常说话复验中，用户反馈“要么提示未检测到声音，要么提示已取回识别文字，本段可能不完整，请检查后补录”，并明确无法从头到尾录完。因此正常录音仍失败；Mac 曾显示识别结束，不能据此覆盖设备不完整结果。语音开关再次关闭，未保存日程。

### 晚到音频覆盖识别终态的竞争

继续对照调用方与协议对端：设备语音工作线程只接受成功状态的回包，拒绝回包会进入断流恢复；Windows 的既有 `audio` 分支在非收音状态返回原状态。Mac 原实现却将非收音状态合并进 `invalid_voice_audio`，因此识别器在两块音频之间结束后，仍在途的下一块会被返回 400，设备只能恢复取字并提示可能不完整。链接本机实际 debug 对象的独立合成检查已复现 `status=400`；它没有访问设备、麦克风或 Speech 服务，不当作实机根因的唯一证据。

最小修复在既有启用、设备／voiceId／session 校验之后，对非收音状态的晚到 `audio` 返回当前状态，不再投喂音频、不重开采集、不替换结果或错误。新增三项回归覆盖最终／错误状态、停止／取消和权限／原会话隔离；实际录音中的其他提前结束原因仍需新候选复验。没有协议、Windows 或固件变更。修复后同一实际对象合成检查通过最终／错误回包及不重复投喂。本机 Release 通过，测试源码语法检查通过；`swift test` 仍因缺少 XCTest 在执行前受阻，完整回归交由 Mac CI。

暂停语音、正常退出并确认串口独占后，又只读 ping 一次：最近请求 83 ms、seq 27，USB RPC 历史失败计数由 83 增至 91，最近仍为阶段 1／状态 0／零回复字节。这显示另有未收到回包的历史事件，不能以终态回包修复概括所有连接中断；它也不能精确关联用户每次试录。未输出原始流、音频、转录或配对材料。

### `4466022` 的实际结果与接收诊断

程序提交 `4466022f40cf379a6cf0c3dc0c5aa10f04f89086` 的 [Mac CI 39377149298](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377149298) 与完整下载附件核验通过：144 项测试，0 失败／跳过，Release／候选／工具、8 张 PNG 及源码／包哈希一致。正常退出旧候选并启动该精确路径后，用户完成匹配钥匙串授权，助手自动打开窗口核对 USB、Wi-Fi 和 BLE 待命；两个无认证状态路由均返回 401。

用户在完整句子复验中仍反馈连接中断或录音中未检测到声音，完整录音未通过。为隔离通道，助手关闭语音和桥接蓝牙，用户断开 USB；端口确实消失，正常 App 显示 USB 未确认／Wi-Fi 已认证／蓝牙关闭。纯 Wi-Fi 读回设备实际设置成功，值仍为自动页面、轮播关闭、亮度 79、设备音量 70、非静音、15 秒／5 分钟、原页面顺序和选择；表单只取消，不保存。随后用户的纯 Wi-Fi 单次录音直接提示“连接已中断，收音已停止”。因此不能将全部故障归因于 USB。语音再次关闭，用户已退出未保存日程编辑回到日常页面。

链接实际 debug 对象、注入合成配对和识别后端，自动验证真实 HTTP 解析／路由／证明校验／解密／RPC：开始、连续 50 块 IMA 音频、停止和最终回包均通过。没有监听端口、真实设备、麦克风、Speech 服务或私人配对访问；此结果不能代替现场连接验收。

下一步增加仅本机窗口的接收诊断：已接收块数／时长、最近及峰值电平、固定操作名与受限数字状态码；新录音清空旧峰值／失败原因，关闭语音隐藏诊断。不展示或保存音频、转录、设备／任务／会话标识或认证材料，不增加协议字段或未认证诊断接口。新增回归覆盖去重计数／峰值、新录音重置、固定标签与关闭隐藏，以及 HTTP 鉴权失败可见性；本机 Release、测试语法检查及实际对象的 50 块完整 HTTP／诊断检查通过；XCTest 仍在执行前受阻。正常候选和新 CI 结果另记。

### 零音频现场与语音 HTTP 连接修复

程序 `888086b22fca492cc7f81ce0518873499ed6b901` 的 [Mac CI 39377742095](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377742095) 与完整附件核验通过：147 项测试，0 失败／跳过，Release／App／工具、8 张原生 PNG、源码／包哈希一致。正常退出旧候选后启动精确新路径，用户处理匹配钥匙串提示，助手自动接管窗口；纯 Wi-Fi 已认证、USB 不在、BLE 关闭，两个未认证状态路由仍为 401。

新的一次实体日程录音仍显示“连接中断，收音已停止；重连后取回文字”。Mac 正常窗口实际捕获：已接收 **0 块／0.0 秒**，最近／峰值电平均为 0，恢复停止与后续查询为 Wi-Fi 200，没有记录到被拒请求。此处零电平只证明没有音频到达，不能据此判断实体麦克风损坏。动态诊断布局正常、没有遮挡后续控件。语音随后关闭，未保存日程，当前再次处于未保存编辑供后续复验。

进一步只读对照：设备使用同一 HTTP client 连续执行开始／音频／停止，原 Mac 每次回包均为 `Connection: close` 并取消 socket。普通设置读写只需单次请求，不能覆盖这条连续流。使用实际服务器与合成配对／后端、不会自动重连的 loopback TCP 客户端，旧实现复现连续交换失败；修复后同一个 socket 成功完成开始、50 块音频和停止。此证据支持纯 Wi-Fi 的连接生命周期兼容问题，不将其外推为所有 USB 中断已解决。ESP HTTP 客户端的持久连接及显式关闭规则可参见 [官方说明](https://docs.espressif.com/projects/esp-idf/en/v5.3/esp32/api-reference/protocols/esp_http_client.html)。

Mac 仅对 HTTP/1.1、成功且逐次鉴权的语音 POST 保持顺序连接；每次重新解析及检查 nonce／证明，失败关闭。显式 `Connection: close`（含逗号列表）、HTTP/1.0、其他路由保持关闭。每连接最多 640 请求／120 秒，读取空闲上限仍为 10 秒；既有请求体／队列上限及组合报文拒绝规则保持。新增真实 loopback socket 回归覆盖同连接多块／停止、后续错误证明关闭、显式关闭／HTTP1.0和普通状态路由关闭。没有自动重发音频或改动固件／协议字段。新正常候选与 CI 另记。

### 长连接实机成功与静默提示去抖

程序提交 `7ad1c240e27ca82e9b633f2aacf82596f32ada6a` 的 [Mac CI 39378349053](https://github.com/yaoyouzhong/AI-bot/actions/runs/39378349053) 及完整附件核验通过：150 项测试，0 失败／跳过，含真实 TCP 回归；Release／候选／工具、8 张原生 PNG 与源码／包哈希一致。本机 Release、候选严格签名及随包工具检查通过，本机 XCTest 仍在执行前受阻。正常退出旧候选、启动该精确路径并由用户处理同类钥匙串提示后，助手自动接管，核对纯 Wi-Fi、其他开关关闭以及两个无认证路由 401。

本次用户反馈识别成功；正常 Mac 窗口同时显示 **37 块／7.4 秒**音频、最近电平 4%／峰值 100%、最终待检查状态和 Wi-Fi 查询 200，没有记录到拒绝。相较旧候选的零音频，此次持续收音与识别实际通过一次；用户未给出逐字最终文字，因此不宣称准确率或长期／USB 稳定性全部通过。未保存日程。

用户同时反馈收音中频繁“检测不到语音”，影响使用。原静默标志在总收音一秒后，只要当前约 200 ms 块的峰值取整为零就立即触发，正常短停顿也会切换提示。修复按去重后接受的 PCM 时长累计连续低电平，满三秒才设置既有 `silent` 标志；一有信号立即清除，新录音归零，轮询与重复块不推进计时。提示不自动停止录音，音频和本地识别策略保持。新增两项回归覆盖连续静默／短停顿／信号恢复／新会话，以及重复与轮询不累计。实际对象合成 PCM 检查、本机 Release 和测试语法检查通过；本机 XCTest 仍在执行前受阻。下一候选 CI 与实机结果另记。

### `4e0b253` 静默提示实机复验

程序 `4e0b253fa768f413e19db6610d30f3ba3b715b88` 的 [Mac CI 39378763038](https://github.com/yaoyouzhong/AI-bot/actions/runs/39378763038) 与完整附件核验通过：152 项测试，0 失败／跳过，Release／候选／工具、8 张原生 PNG 与源码／包哈希一致。本机 Release、严格签名及随包工具通过，XCTest 仍因本机工具链在执行前受阻。旧候选正常退出，新候选精确路径启动；用户完成匹配钥匙串授权后，助手自动接管窗口，正常应用两个未认证状态路由均为 401，其他控制开关保持关闭。

在仍为纯 Wi-Fi 的条件下，用户清空本轮未保存测试名，录入短句并停顿约一秒后继续，对识别和不再频繁静默提示的检查回复“这一次 OK 了”。Mac 实际状态为 **46 块／9.2 秒**、最近电平 2%／峰值 100%、最终待检查和 Wi-Fi 查询 200，无拒绝记录。因此短停顿提示去抖实机通过；准确逐字全文没有单独提供，不宣称准确率测试完成。随后语音暂停，用户接回 USB，助手确认自动恢复“数据已确认”；BLE 暂关，USB 语音正在单独复验，不以 Wi-Fi 的成功代替。

### 同候选 USB 语音与连接恢复

保持 `4e0b253`，助手自动启用语音后，用户只试录一次短句并反馈“是 OK 的”。Mac 窗口独立确认实际通道为 **USB**，收到 **25 块／5.0 秒**、最近电平 2%／峰值 100%，进入最终待检查，查询 200、无拒绝。因此 USB 单次完整收音与识别通过；此前的历史失败仍保留记录，不宣称覆盖所有长期竞争条件。随后恢复本轮进入前启用的桥接蓝牙，正常窗口确认 USB 数据已确认、Wi-Fi 已认证、蓝牙已认证且 USB 优先待命。设备连接模式／展示设置未改变，当前仅测试取消本段录音保留原名称。

### 语音取消与本阶段收尾

蓝牙已恢复 USB 待命的同一候选中，用户再开始一段、说话后点击麦克风旁垃圾桶取消，对取消提示和原名称不变的检查回复“正常的”。Mac 独立显示 **USB 取消 200**、已取消收音，收到 13 块／2.6 秒，没有把本段写入名称。因此真实取消与原名称保留通过，不仅是合成后端结果。

助手关闭语音，正常窗口确认电脑、音乐、Codex 读取／发送、桌面输入框及语音开关均关闭，常用入口仍为空。用户退出未保存测试日程回到原日常页面，没有创建或保存测试事项。USB 数据已确认，Wi-Fi 已认证，桥接蓝牙已恢复原启用状态并认证、USB 优先待命，端口列表已刷新为原设备。最终再次读回实际显示设置：亮度 79、设备音量 70、非静音、自动页面、轮播关闭、间隔 15 秒、屏保 5 分钟、原顺序与前五页选择均一致；只点取消，未再次保存。CoreAudio 只读核对 Mac 为音量 25／未静音，与阶段基线一致。未改变配网、连接模式、图库、屏保类型或固件。本轮临时打开的活动监视器已正常退出；系统设置和 Music 均未运行，只有精确 `4e0b253` 候选仍临时运行。

## 本轮修复：正式固件包的许可证目录

使用当前 Mac 代码的固件加载器读取 SHA 已核对的官方 `.234` 升级 ZIP，实际复现“ZIP 含重复或不安全的文件名”。官方包的七个许可证路径有九段，例如 `licenses/idf/bt/host/nimble/nimble/ext/tinycrypt/LICENSE`；原 `safePath` 最多接受八段，导致合法升级包与同类首刷包无法进入预览。

将嵌套 ASCII 路径上限调整为 16 段，并增加 720 字节总长度上限。继续逐段验证文件名，拒绝绝对路径、空段、点段、穿越、反斜线和解压工具通配符；重复项、符号链接、条目数和文件大小校验保持。依然只通过 `unzip -p` 读取明确条目，不按包内路径解压。没有协议变更。

新增包含同类九段许可证路径的合成升级包回归及边界/危险路径检查，首刷回归也加入该目录。测试资源为独立合成内容，不收录第三方许可证或二进制。

修复后的本机加载器已成功读取真实升级 ZIP，核对版本 `0.2.234-ui`、应用 SHA `1c4fa6261243d3d1d9ac5c5fa54ac6e005f3988ebab9b125ddc5358c37a4f4c8`、ELF SHA `9e6d0e4523381385b721a46389c87ec02dd6ea492225260771f38b3d7bbc29fb` 以及真实 sidecar 的分类标题和列表。真实完整首刷 ZIP（SHA `aec51f9d21f8f9ce2f7ad3ec7cf98604969710b7aee3b80615bb4889549a17e3`）也通过清单、布局、空白区、全部分段 SHA 与精确应用提取校验。这些离线检查未创建桥接服务、未访问设备、未提供升级。

修复后 Release、候选包与签名再次通过。首个修复提交的候选为 `artifacts/mac-tab5-20261020/3cf5fa8/AIBotBridge.app`。初次启动、配对及早期 USB/Wi-Fi 验收使用 `bf16f1f` 候选；随后正常退出并切换包含全部当前修复的 `artifacts/mac-tab5-20261020/f3f9dd6/AIBotBridge.app`，随包 `SOURCE_SHA.txt` 为 `f3f9dd6ba4d7aad391ab2292555a1f5e6a8cd7e1`，已确认身份自动恢复。

`3cf5fa8` 的 [Mac CI 39356044460](https://github.com/yaoyouzhong/AI-bot/actions/runs/39356044460) **通过**。下载完整 CI 附件后，`scripts/verify_mac_tab5_ci_artifacts.py` 核对 112 项测试、0 失败、0 跳过，Release arm64 可执行文件、候选 App、内外层哈希、源码 SHA、许可证、工具检查与 8 张原生 PNG 均通过。本机 XCTest 仍因缺少 Xcode 受阻，不用 CI 结果改写该本机结论。

## 本轮修复：操作回执可见性

用户反馈保存后表单关闭但没有可见成功或失败提示。滚动主窗口到底后，才看到设备忙碌拒绝。原结果标签在长表单末尾；现移到滚动区域之外的固定底部，保存等待、成功和错误均持续可见。最小窗口尺寸的原生夹具渲染已验证标签位于窗口内，实际显示文字为合成回执，不是真机保存截图。用户在 `f3f9dd6` 正常候选再次原值保存，确认无需滚到底即可直接看见固定底部成功回执，真机复验通过。

`f3f9dd6` 的 [Mac CI 39357054680](https://github.com/yaoyouzhong/AI-bot/actions/runs/39357054680) 已通过，完整附件下载复核为 **114 项测试、0 失败、0 跳过**；Release、候选 App、源码 SHA、内外层哈希、许可文件、离线工具与 8 张原生 PNG 均通过。该候选本机 Release、签名和原生渲染也通过；本机 XCTest 仍因缺少完整 Xcode 未执行。

## Codex 本机探测与限制

本机 CLI 为 `0.163.0-alpha.10.2`，实际位于 `ChatGPT.app/Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex`，原自动查找的 `Codex.app`、Homebrew 和 `/usr/local/bin` 均不存在。现按 `com.openai.codex` 的运行中或已注册应用目录查找新旧两种随包 CLI 布局，并保留用户显式路径优先及终端安装回退。独立本机探测已确认新解析器找到实际安装的 CLI；新增当前布局、应用重命名、用户覆盖、旧布局和无效路径回归。

CLI 帮助确认提供 `app-server proxy`，但只读 `initialize` 探测在握手前结束，默认 socket 返回 `ENOENT`；`daemon version` 同样不可达。当前桌面子进程使用 stdio。**因此这台 Mac 的 Codex 会话能力当前受阻**，不能把“找到 CLI”记为连接成功。未启动或部署另一套 daemon，未修改模型、权限、审批和服务参数，也没有列出、读取或向真实任务发送消息。交互窗口补充持续更新的 Codex 连接状态，避免静默失败。

初始化接口依据 [官方 App Server 说明](https://learn.chatgpt.com/docs/app-server)，本机安装布局、命令帮助和失败结果以实际 CLI 探测为准。没有从文档推断当前桌面已开放代理。

## BLE 首次权限与重新启用问题

用户实际首次选择了允许蓝牙，随后关闭再开启。窗口无论开启或关闭都显示“蓝牙已关闭”。因此当时已发生首次授权，但拒绝/恢复路径没有执行，连接与收发尚未确认。

源码定位到重新启用复用已有 `CBCentralManager`，状态不变时系统不再触发状态回调，导致旧的关闭提示一直保留。现重新启用时显式处理管理器的当前状态，同时忽略关闭后迟到的状态回调，避免反向覆盖“已关闭”或维护暂停提示。扫描、配对认证、USB 优先及传输协议保持不变；已对照 `.234` 固件确认广告的服务 UUID 与 Mac 相符。

用户正常退出旧候选并打开精确的 `ffdf43b` 候选，关闭再开启蓝牙后，状态正确变为“正在寻找已配对 TAB5”，随后反馈“已认证 · USB 优先待命”。重新启用提示、真实 BLE 配对认证和 USB 活跃时待命已通过。曾提出排查 Windows 系统蓝牙连接占用，但用户没有确认是否关闭了 Windows 蓝牙，不能据此归因。

用户确认设备原连接模式为“自动”，随后临时设为“仅蓝牙”并退出实体设置页，断开 Mac USB、保持独立供电。约 20 秒后 Mac 蓝牙行显示“数据已确认”；保持该隔离条件读取并原值保存显示设置，收到“TAB5 已确认保存显示设置。”。因此真实 BLE 状态帧及显示设置读写通过，无 Wi-Fi 路由混入。用户随后在相同隔离条件关闭蓝牙约 5 秒再开启，约 20 秒后自动恢复“数据已确认”，未重新配对，断线重连通过。本轮尚未改变亮度、声音、页面或轮播。隔离复验结束后，用户已明确将连接方式恢复到原先“自动选择”并退出设置页。

修复提交 `ffdf43b` 的 [Mac CI 39357782391](https://github.com/yaoyouzhong/AI-bot/actions/runs/39357782391) 与完整附件复核通过：114 项测试、0 失败、0 跳过，Release、候选包、工具、原生夹具、源码 SHA 和包哈希一致。本机 Release 与签名通过，精确候选为 `artifacts/mac-tab5-20261020/ffdf43b/AIBotBridge.app`；本机 XCTest 仍受缺少完整 Xcode 阻塞。

## 本轮修复：睡眠后恢复系统已有的 BLE 连接

在仅蓝牙、USB 断开条件下，用户让 Mac 睡眠约 15 秒并唤醒，等待约 20 秒后仍显示“正在寻找已配对 TAB5”；继续等待后再次确认提示不变。初次结果记录为未通过，后续 `791d898` 已复验通过。只读 `system_profiler SPBluetoothDataType -json` 检查确认 TAB5 位于系统的 `device_connected` 列表；诊断只输出是否匹配该设备及所在分类，不打印其他设备、蓝牙地址或配对材料。

Mac 原来只扫描广播以发现设备，而 `.234` 固件在已有 BLE 连接时不继续广播。现每次发现周期先通过 `retrieveConnectedPeripherals(withServices:)` 查找提供 TAB5 专有服务的系统已连接设备，再调用正常连接、服务发现和设备 ID/随机数/HMAC 认证路径；无匹配结果时沿用广播扫描。该公开 API 的本机 SDK 说明明确要求应用对系统已有连接再发起本地连接。未按设备名称信任连接，未检索其他 HID 设备，也没有协议或固件变更。本机 Release 与候选包签名通过；全量 XCTest 仍在执行前受阻。实际睡眠恢复结果见下文新候选复验，不能将修复代码或 CI 本身记为硬件通过。

## 本轮修复：重复钥匙串授权

用户补充：同一轮连续要求输入本机密码 3～4 次。随后提供的截图确认：AI-bot Bridge 请求访问“登录”钥匙串中服务名为 `io.github.yaoyouzhong.AI-bot` 的机密信息，按钮为“始终允许／拒绝／允许”；用户每次选择“允许”。这属于钥匙串授权，不能当作蓝牙权限弹窗；截图没有区分同一服务下的具体 account。切换 `5e91c29` 后用户仍报告输入 4 次，因此尚不能宣称重复弹窗消除。未复制截图或读取任何真实密码。

源码确认旧 LAN 令牌在启动、HTTP 鉴权、USB LAN 配置与镜像窗口中重复调用 `SecItemCopyMatching`；拒绝或读取失败还会被当作不存在并尝试创建/更新令牌。现将该项目的首次读取结果（包括失败）串行缓存在当前进程内，正常后台访问不再重复请求授权。只有明确不存在才创建；拒绝、解码错误和短记录都不会替换已有记录。显式保存成功才更新内存值，失败保留旧值。密钥仍只持久化到原钥匙串项目，不改变 ACL、不读出真实秘密、不写入偏好或诊断。

新增四项回归覆盖多调用方复用、拒绝后不重复授权/不轮换、仅缺失时创建、显式轮换与写入失败保留。程序提交 `5e91c293586b439ddd16e8bb32635179e4295563` 的 [Mac CI 39359379573](https://github.com/yaoyouzhong/AI-bot/actions/runs/39359379573) 与完整附件核验通过：**118 项测试，0 失败、0 跳过**，Release、候选包、工具、原生渲染、源码 SHA 和内外层包哈希一致。精确本地候选位于 `artifacts/mac-tab5-20261020/5e91c29/AIBotBridge.app`，已只读确认新进程运行且旧候选退出；正常界面与授权结果仍以人工反馈为准。TAB5 配对项目本来只在服务启动及用户明确重新配对时访问；它和旧 LAN 项目仍互相独立，临时签名新候选也可能各自需要一次正常授权。因此不能宣称所有系统授权弹窗已消除。按 [Apple 官方说明](https://support.apple.com/zh-cn/guide/keychain-access/kyca1243/mac)，“允许”仅授权一次，“始终允许”记录该 App 对相应项目的后续许可；正在使用同一候选验证该正常授权流程，未放宽为允许所有 App 访问。

本机使用实际缓存源码配合合成存储后端执行独立检查，上述四组行为（含 32 次并发读取）均通过；未访问真实钥匙串。全量 `swift test` 仍因缺少 XCTest 在执行前受阻。缓存仅在当前进程有效，外部直接修改钥匙串需要重启 App 才会重新读取；App 内显式保存会立即更新缓存。

用户按同一个 `5e91c29` 候选重新启动并选择“始终允许”，反馈需要确认两次。随后关闭蓝牙、正常退出并重新打开同一候选，确认没有再出现钥匙串弹窗；同一候选的正常授权和重启记忆路径通过。启动会读取旧 LAN 令牌和 TAB5 配对两项，但截图未显示具体 account，不对每张弹窗逐次强行归因。程序没有修改钥匙串 ACL；用户通过系统弹窗为当前候选授权属于正常流程。更换临时签名构建后的首次授权、拒绝和恢复仍不能据此宣称全部通过。

## 本轮修复：系统蓝牙配对确认被提前取消

用户另提供系统蓝牙数字比较截图，并确认配对弹窗约十几秒后再次出现。它与钥匙串弹窗不同，配对码没有写入记录。Mac 连接和服务认证原共用 15 秒期限，超时会取消连接，三秒后自动重连；`.234` 设备端数字比较允许 30 秒，并且需要在实体 TAB5 上确认。因此在用户处理两端弹窗时，桥接可能先取消正常配对并触发第二次请求。

将连接/初始认证等待调整为 60 秒，并在连接状态明确提示核对两端数字。服务发现只请求 TAB5 专有服务，不再请求全部服务（包括未使用的 HID）；仍保留同一认证路径。数据帧和 RPC 的已有期限、USB 优先、HMAC 认证、固件的配对确认与持久化规则都不改变；不自动批准数字比较、不删除配对记录。程序提交 `791d898c306b7232dc69c0432a51a6d55b7cfc2c` 的 [Mac CI 39360266984](https://github.com/yaoyouzhong/AI-bot/actions/runs/39360266984) 与完整附件核验通过：118 项测试，0 失败、0 跳过，Release、候选包、工具、8 张原生 PNG、源码 SHA 和包哈希一致。本机 Release 与签名也通过，XCTest 仍因缺少完整 Xcode 受阻。精确候选为 `artifacts/mac-tab5-20261020/791d898/AIBotBridge.app`；系统配对弹窗与睡眠恢复分别跟踪，复验前用户曾暂停 Mac 蓝牙。隔离测试后用户已恢复 TAB5 原先“自动选择”并退出设置页，用户接回 USB 后明确反馈 USB“数据已确认”、蓝牙“已认证，USB 优先待命”，优先级回归通过。

用户已正常打开 `791d898`，初始保持蓝牙关闭；只读进程核对确认旧候选退出且仅新候选运行，未认证的两个状态 HTTP 路由仍返回 401。启用蓝牙后，用户反馈出现 macOS“键盘设置助理”；固件同时广播 HID 服务，Mac 的 TAB5 状态与设置功能不需要配置键盘布局。用户退出助理后确认“数据已确认”，新候选真实 BLE 状态传输通过；首次配对弹窗保持时间没有明确反馈，不将精确延迟确认复验写为通过。用户随后明确确认本次睡眠唤醒与 USB 接回期间没有再次出现数字配对弹窗，正常重连不重复配对通过。随后保持 USB 断开与仅蓝牙，让 Mac 睡眠约 15 秒并唤醒，约 20 秒后用户回复“显示数据已确认”，此前失败的睡眠恢复项真机复验通过。候选使用说明已双语补充两端数字确认、键盘设置助理和钥匙串授权的区别。

用户在系统设置中关闭当前候选的蓝牙权限，App 显示权限被拒绝并提示到系统设置允许；重新开启同一权限后，用户确认“已认证，USB 优先待命”，撤销授权后的拒绝提示与恢复通过。首次权限弹窗当时选择的是允许，首次直接拒绝的分支仍没有执行，不将两者混为同一项。

## 后续项目

以下是尚未完成或超出当前写入授权的项目，不以 CI、预检或其他通道的成功代替：

- Wi-Fi：USB 配网、地址恢复、固定模式及八秒回退阈值的精确测量；本轮保留原网络配置。
- BLE：首次权限弹窗直接拒绝、首次数字配对精确延时确认，以及 BLE 语音；撤权／恢复、状态／设置、睡眠唤醒和 USB 优先已经通过。
- 展示与资源：日常数据逐项对照、桌宠；音乐均匀计时仍按[固件后续项](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md)处理。高清完整图库原作浏览器不属于当前 Mac 候选能力，当前仅 standard 子集。
- 系统与音乐：Spotify、应用程序入口、锁屏／辅助功能权限拒绝后的恢复；音量／静音、Apple Music 单次控制和文件夹入口已通过。
- Codex：当前已安装 CLI 的默认本地公开代理在握手前退出；任务列表／精确任务／公开回复／草稿／送达／快捷控制台未完成。尚无明确指定的真实测试任务，未发送、追加或中断任何任务。
- 照片与语音：照片／任务附件、本机默认麦克风、BLE 语音、长时间及真实断流恢复、逐字准确率；TAB5 麦克风的 USB／Wi-Fi 短录音、短停顿提示和真实取消已通过，不能外推为全部稳定性验收。
- 固件工具：正常 App 选包／真实说明预览及当前镜像只读启动核验已通过；OTA、首刷、恢复 Flash 未执行，仍需单独明确授权。当前不改写设备。

## English

The quota/music candidate `d93c318` passed [CI 39387247309](https://github.com/yaoyouzhong/AI-bot/actions/runs/39387247309) and full artifact verification: 171 tests, zero failures/skips, Release/app/tools/PNGs/sanitizers and hashes. The user authorized Keychain startup; USB, Wi-Fi and Bluetooth standby recovered normally. Physical reset-credit expiry details passed. After two actual quota samples 120 seconds apart, the user also confirmed today's zero and unknown earlier days. The user later confirmed the current-day value increased with subsequent usage sampling, validating live accumulation. Historical data is not invented.

Only the music opt-in was temporarily enabled and the original local fixture imported/played. The earlier 500 ms USB candidate `d93c318` failed music acceptance: elapsed time intermittently disappears while total duration remains. Stack sampling identified main-thread activity scanning blocking music Apple Events. The background-sampling repair `3fc848b` passed [CI 39388819221](https://github.com/yaoyouzhong/AI-bot/actions/runs/39388819221) and full artifact verification with 173 tests, zero failures/skips; normal-app revalidation followed. The user confirmed elapsed-time visibility and uniform progression now normal on USB; diagnostics counted 281 generated frames with zero unavailable timelines and maximum age 0.53 seconds. After automated Music resume/pause, the counters reached 370 generated frames with zero unavailable timelines and maximum age 0.53 seconds. Music access was disabled, the fixture removed from the library into recoverable Trash, and Music quit; all interaction opt-ins are off. The Mac TAB5 window shows all three transports healthy; the user confirmed returning the device to its original daily overview. Firmware and display settings remain unchanged. The following paragraphs retain earlier checkpoints.

The latest physically checked program is `a3bd642`, with [CI 39385101526](https://github.com/yaoyouzhong/AI-bot/actions/runs/39385101526) and full artifacts verifying 164 tests, zero failures/skips, Release/app/tools, eight PNGs, music sanitizers and source/package hashes. Local Release/app/fixture checks passed; local XCTest remained unavailable.

With explicit temporary read-only consent, the normal owned-stdio reader listed 29 recent conversations. On `604ac8c` the user confirmed a Codex home card and public replies. A synthetic check then reproduced the timer resetting historical turns/pages. The repair preserves successful selections, rejects invalid navigation without replacing them and follows new turns after an explicit latest read. Its regression passed CI; on normal `a3bd642`, the user also confirmed public reply display and remaining on an older turn/page two for the requested twelve-second observation without resetting. USB/Wi-Fi/BLE and exclusive process/listener/serial ownership were checked.

The assistant disabled the reader, confirmed its child exited, every interaction switch off, the launcher list empty and the listener/serial owner healthy, and returned the Mac window to TAB5. The user returned the device to its original daily overview. No task mutation, desktop input, test-content save, display-setting change or firmware write occurred. The home card falling back to computer status while the feature is off is expected. Older paragraphs below preserve historical context rather than overriding these results.

The running app is now `ef773a0`; `1788914` only tightens the offline music reference. [CI 39381792622](https://github.com/yaoyouzhong/AI-bot/actions/runs/39381792622) and complete downloaded artifacts verified 156 tests, zero failures/skips, music ASan/UBSan, Release/app/tools, eight native PNGs and source/package hashes. The running program's own CI also passed and its small reports verified the same 156 tests including four new regressions. Local XCTest remains blocked before execution.

The original host-only music reference preserves fractional phase and covers playback lifecycle, seeks, duplicates/out-of-order samples, unknown/lost clocks and recovery boundaries. Bounded-jitter synthetic dwell is 1000 ms versus 900–1100 ms for the published expression. An exact-source-SHA/context generator verifies an unapplied review patch only in a disposable copy and rejects incorrect archives. It is not linked into firmware; no firmware image or ESP-IDF build was produced and physical timing remains unaccepted. Sub-350 ms seeks remain indistinguishable from jitter.

Codex active/waiting/unknown state translation, working-task home selection, bounded reader/home retention and safe connection failure classification now have four passing regressions. Actual RPC objects and the installed CLI passed an empty temporary-home stdio probe without real conversation access, login, inference or task creation. The desktop public proxy still exits before initialization because its socket is missing; no daemon/internal IPC workaround or real send/steer/interrupt was used. Running .234 intentionally falls back to CPU/memory on its home task card when no valid task exists; neither home layout nor display preferences changed. After a normal old-app quit and the user's Keychain handling, the new exact-path candidate recovered USB ACKs, authenticated Wi-Fi and BLE USB-priority standby. A single process/listener/serial owner and all interaction switches off were confirmed automatically. No settings, agenda or music fixture was saved or replayed in this follow-up. See the linked music/Codex follow-up documents for evidence and remaining conditions.

Current verified program is `4e0b253`; older pending statements retain their historical context. USB and actual BLE settings/reconnection/sleep recovery passed, as did pure-Wi-Fi settings and USB return. Provisioning/address recovery/fixed modes/exact fallback timing remain pending. Display controls/persistence, gallery import/failure retention/pagination/orientation passed; individual daily data, pets and even music timing remain unfinished. Apple Music metadata/artwork/control, system volume/mute/unmute and folder launch passed; Spotify, application launch and lock/Accessibility denial/recovery remain pending. Codex proxy initialization is blocked before handshake, and no real task send/append/interrupt was executed. Voice permissions and one 7.4-second Wi-Fi take passed after the HTTP persistence repair; short-pause no-sound hint acceptance also passed; one USB take and cancellation preserving the original name also passed. BLE voice, sustained capture stability, transcript accuracy and photo/task attachments remain pending. Normal-app firmware preview and read-only current-image USB boot verification passed; no firmware write occurred. Temporary music/media, system audio and folder-launcher cleanup completed; voice is disabled again and the user left the unsaved test agenda without saving. USB is acknowledged, BLE restored to authenticated USB-priority standby, and final read-only display settings matched the phase baseline; the form was cancelled without saving. The full nine-stage acceptance is not complete.

Fixed-cadence program commit `3c82bcce4df49af2b3e1c64445664500eabf7a43` passed [Mac CI 39368358000](https://github.com/yaoyouzhong/AI-bot/actions/runs/39368358000) and full artifact verification: 136 tests, zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. An actual-object synthetic cadence check passed twenty varying ACKs, control rephasing and blocked-write burst suppression without device access. Local Release/bundle/signature passed; local XCTest remains unavailable. The previous candidate was normally quit before exact `3c82bcc` launch. The user handled system authorization and confirmed opening TAB5; the unique process and both unauthenticated HTTP 401 routes were verified. Bridge-window selection repeatedly timed out, including after reconnecting the UI tool, so USB/BLE row text is not independently reconfirmed for this run. Read-only stack symbols showed AppKit event processing, music polling and USB work; no persistent freeze was established and no Keychain contents were inspected. Music UI automation remained available and restarted the fixture.

After the forty-five-second observation, the user reported no skipped seconds but uneven progress. This is an improvement, not full timeline acceptance. Subsequent single physical pause and play each reportedly took under one second; normal Music UI independently confirmed both real states and opposite action buttons. These durations are user estimates, not instrumented measurements. See the [firmware timing follow-up](MAC-TAB5-MUSIC-TIMING-FOLLOWUP.md) for the running `.234` renderer's integer truncation/per-packet phase reset, synthetic replay evidence and required lifecycle checks. No firmware was changed or written.

Bringing TAB5 to the foreground restored UI-tool selection, allowing acknowledged USB, authenticated Wi-Fi and USB-priority BLE standby to be reconfirmed. Fresh settings were read and only the temporary music-page mode was restored to this phase's entry mode, with a matching fixed-footer save receipt; cycling and all other values stayed unchanged. Music opt-in was restored off; computer/Codex/voice remained off. After pausing, normal Music library removal moved only the fixture copy to recoverable Trash, and the library no longer contained its entry. The original source stays ignored; Trash was not emptied. Normal Music quit was independently confirmed by process inspection. Temporary music-page/opt-in/media cleanup is complete. No Keychain ACL was weakened.

In the same normal `3c82bcc` app, selecting the official `.234` upgrade ZIP showed version `0.2.234-ui`, the exact application SHA above, structured actual sidecar notes and an explicit not-started state. Only Verify Boot was invoked; it reported USB verification of exact ELF, VALID partition and three live UI samples. This checks the already running image, not an OTA/write performed in this session or a new physical screen/touch observation. Stop Providing Firmware then returned to no selected firmware with the stop receipt. No upgrade-start, reset or flash-write action was invoked.

Foregrounding restored bridge selection, but closing a child window again left it unselectable and Command-W did not close the window. The accessory app lacked a reopen delegate and main menu. Reopening the running app now opens TAB5; a standard Window menu adds Command-W and Command-Q follows the existing quit path. Existing window-busy and application termination guards remain. No automatic permission approval, external control interface or deployment was added.

Local Release passed with the unchanged SerialBridge Sendable warning; local XCTest remained blocked before execution. Earlier documentation commit `61e86ac` passed [Mac CI 39374024338](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374024338), and complete artifacts verified 136 tests with zero failures/skips, Release/app/tools and matching source/package hashes. The window change's CI and normal-app checks are tracked separately.

Program `3db70a3dece89c39acd71743007b8213ae366600` passed [Mac CI 39374705006](https://github.com/yaoyouzhong/AI-bot/actions/runs/39374705006); complete artifacts verified 136 tests, zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. The normal local candidate passed bundle/strict-signature checks. After the user handled expected Keychain authorization, automated window selection confirmed actual transport status and unauthenticated 401. Command-W closed the feature child and returned to TAB5; the Window menu exposed Close and TAB5. Command-Q exited with the old PID absent. Automated relaunch initially timed out with no window; reopening that running app then showed TAB5 without manual window assistance, and USB automatically recovered. Initial menu-only launch remains intentional, so not every UI-tool timeout is claimed eliminated.

With computer control temporarily enabled and music off, a read-only production CoreAudio path captured the fresh restoration baseline. The user dragged physical TAB5 volume to 17%; CoreAudio and normal Sound settings both read 17%. Single physical mute/unmute clicks changed the device buttons and CoreAudio to true/false with volume retained. Real TAB5-to-Mac volume/mute/unmute passed without synthetic control requests. Normal Sound settings then restored the original volume, independently confirmed with the unmuted baseline. Temporary System Settings was quit and computer opt-in restored off. Fresh device display settings matched this phase's restored values; the form was cancelled without another save. Device speaker audio, lock/Accessibility, launchers and Spotify remain separate checks.

Folder launcher passed separately. Starting with an empty launcher list, normal app selection temporarily added the newly created empty `TAB5-LaunchTest` directory and enabled computer control. After one physical device click, Finder switched from the old candidate folder to that exact empty test directory; the assistant did not open it directly on the Mac to substitute for device input. The temporary Finder window was closed, the entry removed through the app and computer control disabled; the empty list was verified and the assistant-created empty directory removed. Application launchers, lock/Accessibility and permission denial/recovery remain separate pending checks.

Normal `3db70a3` voice authorization passed via the user's system confirmation and matching app receipt, with computer/music/Codex off. An unsaved agenda-name editor uses the existing independent voice owner. The user could recognize speech but reported ending before speaking with a connection-interruption message; accurate final text was not supplied, so full recognition/stability is not accepted. Published `.234` firmware distinguishes missing/unacceptable transport replies from the Mac recognizer-error message. After disabling voice and normally quitting, the assistant verified exclusive serial ownership and sent only a 460800-baud ping. Whitelisted diagnostics retained 2 ms/seq 24 for the recent voice request and historical USB RPC count 83, stage 1/status 0/zero reply bytes. Historical counters do not correlate every failure or prove contention/reset. No raw stream, audio, transcript or pairing material was saved; no reset/write occurred. The same app was restarted and USB confirmation awaited before another voice check.

A single silent three-second take reported the generic recognition error only after the user pressed Stop; premature completion was not reproduced in that check. Both UI samples showed acknowledged USB, without claiming continuous coverage. The old backend discarded NSError classification. The repair distinguishes manual empty no-speech review from premature/timeout/partial failures, and reports controlled resource/permission/busy/service/unknown-code errors without raw error descriptions. A live Mac status summarizes capture, local recognition, review and errors without transcripts or identities. Existing wire fields and local-only recognition remain. Five regressions cover these behaviors and summary privacy. Local Release passed; XCTest remains unavailable and CI/normal-app checks follow separately. Voice was disabled again. The original transport interruption is not claimed fixed by this error-reporting repair.

Program `b012fe63861d1f524607bcbec045472252ad2810` passed [Mac CI 39376374679](https://github.com/yaoyouzhong/AI-bot/actions/runs/39376374679) and full downloaded-artifact verification: 141 tests, zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. The new speech status is visible in full native rendering and the normal app without covering following controls. Local XCTest remains unavailable. The old candidate was normally quit before exact-path launch; after the user handled expected Keychain authorization, automated TAB5 selection reconfirmed acknowledged USB, authenticated Wi-Fi and USB-priority BLE standby. Local voice was enabled with existing authorization while other controls stayed off. A three-second silent take stopped manually now reports no recognized text on both device and Mac, passing this empty-take message check. It does not establish that the original premature transport interruption is fixed. The subsequent spoken retest still failed: the user reported either no detected sound or recovered text explicitly marked possibly incomplete, and confirmed a complete take could not finish. A Mac review status cannot override that device result. Voice was disabled again and no agenda was saved.

A further caller/peer review found a terminal-state race. Firmware treats a non-success voice reply as transport failure/recovery; Windows already returns the original state for in-flight audio outside recording. Mac instead returned `invalid_voice_audio`/400 if its recognizer had ended between audio requests, causing a possibly-incomplete recovery despite a stored final result. An isolated check linked to actual local debug objects reproduced status 400 without device, microphone or Speech-service access. This establishes a code defect, not every hardware failure's cause. The minimal repair returns the current state for late audio only after the existing opt-in and exact device/voice/session checks; it neither feeds audio nor reopens capture or changes results/errors. Three regressions cover final/error, stop/cancel and owner/consent isolation. Protocol, Windows and firmware are unchanged. The same actual-object check passed after the repair; local Release and test-source parsing passed, while local swift test still stops before execution because XCTest is missing. Full regressions require Mac CI. After disabling voice and normally quitting with exclusive serial ownership verified, another read-only ping reported a recent 83 ms/seq 27 request and historical USB failure count increasing from 83 to 91, still at stage 1/status 0/zero reply bytes. These uncorrelated counters indicate other missing-reply history, so terminal handling is not claimed to resolve all connection failures. No raw stream/audio/transcript/pairing data was output. Physical retest remains required.

Program `4466022f40cf379a6cf0c3dc0c5aa10f04f89086` passed [Mac CI 39377149298](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377149298) and full artifact verification: 144 tests, zero failures/skips, Release/app/tools, eight PNGs and matching source/package hashes. Normal exact-path launch after old-app quit, expected user Keychain approval, automatic window selection, USB/Wi-Fi/BLE recovery and both unauthenticated 401 routes passed. Full spoken capture still failed with connection interruption/no-signal reports. Disabling voice/BLE and physically disconnecting USB confirmed an absent port and Wi-Fi-only authenticated status. Actual device settings were read successfully, matched the restored baseline and were cancelled without saving. The single Wi-Fi voice check immediately reported connection interruption/capture stopped, so USB alone cannot explain the fault. Voice was disabled again and the user left the unsaved agenda editor without saving.

A separate actual-object check with synthetic pairing/backend passed the real HTTP parser/route/proof/decryption/RPC path for start, fifty IMA audio blocks, stop and final reply. It opened no listener and accessed no device, microphone, Speech service or private pairing. It is not physical transport acceptance. Additional local-window diagnostics show accepted block count/duration, recent/peak level and fixed operation labels/bounded status numbers, resetting old peak/failure on a new take and hiding when opted out. They expose no audio/transcript/device/task/session/authentication data, add no wire fields or unauthenticated endpoint, and have regressions for deduplication/peaks/reset, fixed labels/opt-out and HTTP authentication failures. Local Release, test-source parsing and the actual-object fifty-block HTTP/diagnostic check passed; local XCTest remains blocked before execution. New candidate/CI checks follow separately.

Program `888086b22fca492cc7f81ce0518873499ed6b901` passed [Mac CI 39377742095](https://github.com/yaoyouzhong/AI-bot/actions/runs/39377742095) and full artifact verification: 147 tests, zero failures/skips, Release/app/tools, eight PNGs and matching hashes. Exact-path normal launch, expected user Keychain handling, automated selection, authenticated Wi-Fi with USB absent/BLE off and both unauthenticated 401 routes were verified. Another physical take still reported connection interruption/recovery. The normal Mac window showed zero accepted audio blocks/seconds and zero recent/peak level, with recovery Stop/later Poll returning Wi-Fi 200 and no recorded rejection. Zero level here means no audio arrived; it does not establish microphone hardware failure. Dynamic diagnostic layout remained readable. Voice was disabled again; no agenda was saved and the editor remains open for retest.

The device streams start/audio/stop through one HTTP client, but Mac previously returned Connection: close and cancelled every socket; single-call settings acceptance does not cover this stream. An actual-server synthetic-pairing/backend loopback client with automatic reconnection disabled reproduced failed sequential exchanges before the repair, then completed start, fifty audio blocks and stop on one socket afterward. This supports a Wi-Fi connection-lifecycle incompatibility, not resolution of all USB failures; see the [official ESP HTTP client guidance](https://docs.espressif.com/projects/esp-idf/en/v5.3/esp32/api-reference/protocols/esp_http_client.html). Only successful individually authenticated HTTP/1.1 voice POSTs now persist. Each request is freshly parsed and nonce/proof checked; failures, explicit Close tokens, HTTP/1.0 and other routes close. Connections are limited to 640 requests/120 seconds with the existing ten-second read idle timeout, body/queue bounds and combined-message rejection intact. Three live loopback regressions cover same-socket audio/stop and subsequent invalid proof, explicit-close/HTTP1.0 and ordinary status closure. No audio replay, wire-field or firmware change was introduced. New normal candidate/CI checks follow separately.

Program `7ad1c240e27ca82e9b633f2aacf82596f32ada6a` passed [Mac CI 39378349053](https://github.com/yaoyouzhong/AI-bot/actions/runs/39378349053) and full artifact verification: 150 tests, zero failures/skips including live TCP regressions, Release/app/tools, eight PNGs and matching hashes. Local Release/bundle/signature/tool checks passed while local XCTest remains unavailable. The previous candidate was normally quit, exact new path launched, expected Keychain permission handled by the user, and automated selection reconfirmed Wi-Fi-only transport, other opt-ins off and both unauthenticated 401 routes. The user reported successful recognition; the normal Mac window independently showed 37 blocks/7.4 seconds, recent level 4%/peak 100%, final review and Wi-Fi Poll 200 with no rejection recorded. This is one actual successful capture/recognition after the earlier zero-audio failure. Exact final text was not supplied, so accuracy, long-term or USB stability is not claimed passed. No agenda was saved.

The user still found repeated no-speech hints disruptive. Previously, after one second of audio, any current approximately 200 ms block with rounded-zero peak immediately set silent, including normal pauses. The repair requires three continuous quiet seconds of accepted, deduplicated PCM; any signal clears it, new takes reset it, and polls/duplicate chunks do not advance it. Existing silent wire semantics, capture and local recognition stay intact; the hint does not stop recording. Two regressions cover silence/pauses/signal/new take and duplicates/polls. Actual-object synthetic PCM checks, local Release and test-source parsing passed; local XCTest remains blocked before execution. New CI and physical results follow separately.

Program `4e0b253fa768f413e19db6610d30f3ba3b715b88` passed [Mac CI 39378763038](https://github.com/yaoyouzhong/AI-bot/actions/runs/39378763038) and full artifact verification: 152 tests, zero failures/skips, Release/app/tools, eight PNGs and matching hashes. Local Release/signature/tool checks passed; local XCTest remains unavailable. Normal old-app quit, exact new-path launch, expected user Keychain authorization, automated selection, both unauthenticated 401 routes and other opt-ins off were verified. In pure Wi-Fi, the user cleared the unsaved test name and spoke with an approximately one-second pause, then confirmed this take and the no-repeated-hint check were OK. Mac independently showed 46 blocks/9.2 seconds, recent level 2%/peak 100%, final review and Wi-Fi Poll 200 with no rejection. Short-pause hint acceptance passed; exact final transcript/accuracy was not independently supplied. Voice was paused before USB reconnection, which automatically recovered acknowledged data. BLE remains temporarily off while USB voice is checked separately.

On the same `4e0b253` candidate, one physical USB take was reported OK. Mac independently confirmed USB as the actual channel, 25 blocks/5.0 seconds, recent level 2%/peak 100%, final review and Poll 200 with no rejection. One complete USB capture/recognition passed without erasing historical failures or claiming all sustained-contention conditions covered. Bridge BLE was restored to its original enabled state; the normal window reconfirmed acknowledged USB, authenticated Wi-Fi and authenticated USB-priority BLE standby. Device connection/display preferences remain unchanged. Cancellation preserving the existing unsaved name is under check.

With BLE restored to USB standby, the user started another take and used the microphone-adjacent trash icon to cancel, confirming normal cancellation and unchanged original name. Mac independently showed USB Cancel 200, cancelled capture and 13 blocks/2.6 seconds. Physical cancellation/name retention passed. Voice was disabled again; all computer/music/Codex read/send/desktop/voice opt-ins were confirmed off with the launcher list empty. The user left the unsaved test agenda for the original daily page without saving or creating an item. USB/Wi-Fi authentication and the original enabled BLE USB-priority standby were restored, and the port list refreshed. Final actual settings matched brightness 79, device volume 70/unmuted, Auto page, cycling off, 15-second interval, five-minute saver and original page order/first-five selection; only Cancel was clicked. Read-only CoreAudio confirmed the unchanged Mac 25/unmuted baseline. Network profiles, connection mode, gallery, saver type and firmware were unchanged. Temporary Activity Monitor was normally quit; System Settings and Music were not running, and only the exact 4e0b253 candidate remains temporarily running.

Remaining acceptance includes network provisioning/address/fixed-mode/exact fallback timing; initial BLE denial/delayed pairing confirmation; daily-data comparisons/pets; Spotify/application launch/lock and Accessibility denial recovery; photo/task attachments, default Mac microphone, BLE voice, longer capture/interruption recovery and transcript accuracy. Even music timing needs the separate firmware follow-up. The installed Codex public proxy still exits before handshake and no exact real test task has been specified, so no task was sent, appended or interrupted. The HD original-art browser is outside this candidate; only the standard subset is supported. Actual firmware OTA/first flash/restoration remains outside current write authorization; read-only preview/boot verification already passed.

Normal candidate `754fd02` passed local bundle/signature/tool checks and was automatically launched after normal quit of the previous app; the user handled its expected system authorization. USB, authenticated Wi-Fi, BLE standby, the unique exact process and both unauthenticated HTTP 401 checks recovered. Its [Mac CI 39367861614](https://github.com/yaoyouzhong/AI-bot/actions/runs/39367861614) and complete downloaded artifacts verified 134 tests with zero failures/skips, Release/app/tools, eight native PNGs and source/package hashes. The initial thirty-second report selected backwards-or-skipped seconds; clarification specifically reported forward skips with no backwards movement. No rewind was reported in this observation, but forward stepping remains unpassed. Firmware adds a whole-second base to whole seconds since receipt. The old USB cadence waited two seconds after each ACK, accumulating transport/callback time into phase drift. The follow-up keeps original deadlines, restarts phase for control changes and skips missed slots after a blocked write instead of bursting. Two regressions cover repeated ACK/callback delays, control and blocked writes. A synthetic phase model can produce rapid adjacent second changes with ACK-based drift and avoids them with fixed phase; this is not hardware timing evidence and physical revalidation is pending.

After the user authorized normal candidate `c51a1bb` and opened TAB5, UI automation confirmed acknowledged USB data, authenticated Wi-Fi activity and authenticated USB-priority BLE standby. Only the expected candidate process was present; both unauthenticated status routes returned 401. With music enabled and computer control disabled, the user confirmed the physical test title/artwork, then clicked pause and play once each. Normal Music UI independently showed paused/playing state and the corresponding opposite action. Physical cover display and single control effects passed. The user subsequently reported latency, skipped seconds and backwards progress; timeline acceptance remains failed, separate from those effects.

The old two-second music poll was launched asynchronously while general status immediately published its older cache. USB could retransmit that cache, and player positions were rounded to integers. Published `.234` UI resets its local playback anchor on every status, so old positions cause rewinds. Confirmed player control readback was also discarded. The repair keeps fractional positions, polls independently every half-second without overlapping refreshes, samples current music at frame creation and projects only a recent sample to send time. Samples older than three seconds or from the future use existing `timelineAvailable=false` while retaining metadata/artwork. Pause stays fixed and genuine seeks are honored. Confirmed control readback immediately updates the cache; USB prioritizes track/state/seek changes without raising ordinary full-status frequency. Late polls/artwork cannot undo newer control observations or attach an old cover to a new song. No new message, wire version or firmware change is introduced. Eight regressions cover the actual device-clock contract, pause/seek, stale/future samples, fractions/end bounds, USB urgency, late results, current music overriding the general cache and artwork bound to that same sample. An actual-object synthetic check reproduced cached-position reset before the repair and passed afterward without accessing a device or Apple Events. Local Release passed; XCTest remains unavailable. A new normal-App timing/latency/play-pause retest is pending; `c51a1bb` is still running at this checkpoint.

After the user asked for less manual routine work, Computer Use was retried and could operate Music, bridge windows and Activity Monitor. Normal Music File Import added only the original test M4A; its ten-minute library entry, blue/orange artwork, pause control and advancing elapsed-time text were observed automatically. Some slider accessibility identifiers remained stale, so they were not used alone as playback truth. This local track needs no subscription and no other user media was read. Library cleanup remains pending. On normal `5024001`, the assistant read fresh device settings, temporarily selected Music and observed the matching save receipt. Physical title/artwork and touch control remain unconfirmed. This fresh display-mode read differed from the earlier manual baseline; its restoration value stays ignored and later cleanup must preserve current settings.

The exact old candidate was normally quit through Activity Monitor, using Quit rather than Force Quit as described by [Apple](https://support.apple.com/en-ie/guide/activity-monitor/actmntr1002/10.14/mac/15.0). Its absence was verified before Computer Use launched exact candidate `c51a1bb`. The new process exists, but system security authorization is pending and the status listener has not started. Startup and transport recovery are not marked passed. Computer Use explicitly forbids SecurityAgent access; the user was asked only to handle the matching expected prompt on the Mac and open TAB5 once. No password was read, entered or recorded. Routine Mac-window work will continue automatically afterward. A read-only process sample confirms the main thread is still waiting in `SecItemCopyMatching`; only stack symbols were inspected, with no Keychain contents read. The fixture completed once naturally and repeat playback was not enabled.

Before music acceptance, the user confirmed both computer-control and music opt-ins were off. Normal candidate `5024001` recovered acknowledged USB data and authenticated BLE standby; only that candidate was running and both unauthenticated status routes returned 401. Enabling music showed an Automation prompt, but no song played because the user lacks a subscription. Prompt appearance is not playback acceptance. Original local audio with a synthetic cover was prepared without accessing existing media; import/playback and later library removal are tracked separately.

A separate synthetic-backend reproduction found that the music-only opt-in returned 403 before media execution and omitted the shared control transport. Media commands now follow the music opt-in independently. The existing control-version capability is advertised when either feature is enabled. Volume, mute, lock and launchers remain gated by computer permission and are masked from music-only status; computer-only status masks media controls. Deduplication, pre-effect permission recheck, exact-player binding and readback remain. Existing v1 messages/fields are unchanged and firmware capability consumers were checked. Four regressions cover both opt-in directions, revocation of a recorded request and transport advertisement. The actual-object synthetic check failed before the repair and passed afterward, with volume writes still denied and no real Apple Events. Release passed; local XCTest remains blocked before execution. Program commit `c51a1bb8aa59afcaea9cd2735071da96b0fb8b72` passed [Mac CI 39364551773](https://github.com/yaoyouzhong/AI-bot/actions/runs/39364551773); complete downloaded artifacts verified 126 tests with zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. Local candidate `artifacts/mac-tab5-20261020/c51a1bb/AIBotBridge.app` passed build, bundled-tool and signature checks. Subsequent normal-app automation is recorded above. Physical music display, play/pause and denial/recovery checks remain pending; CI is not physical control acceptance.

On normal candidate `791d898`, the user imported the official painting archive and confirmed 366 works. Its active manifest independently matched the official package, including 826 images. Today's work has one page, so physical pagination used a temporary one-work CC0 subset from that same archive, preserving original images, source/license records and guides: ten pages per orientation, twenty images total. No clock/date was changed and this local-only subset is not a release artifact. The user confirmed normal preview, next/previous images and page numbers, then normal images/text/paging in portrait and landscape without black frames or abnormal clipping. An otherwise identical local archive with one changed image byte and unchanged manifest SHA was rejected by both the isolated loader and normal app. All 28 installed resource-file hashes stayed unchanged and the user confirmed physical paging still worked after rejection. Failure retention passed. These archives/source assets remain ignored. The user subsequently confirmed successful full official-pack reimport; its manifest independently matched 366 works/826 images and the original archive. The user then restored the original saver type and automatic orientation, left device settings and returned to overview. Temporary resource/display restoration passed. Artwork covers, pets and the HD full-bundle path remain separate checks.

The fixed-page save was acknowledged and physically switched TAB5 to its clock page. A temporary two-page cycle was saved and the user reported normal switching; after adding a third page, the user again reported normal switching in response to the requested three-page order. Exact intervals were not instrumented and no separate third-save receipt text was supplied. Original fixed page, cycle toggle, page selection/order and interval were then restored with a successful save, temporarily shortening only the saver wait. The user confirmed automatic saver entry after the approximately seventy-second idle request and normal physical touch exit; return page and brightness were not separately detailed. Restoring the original saver wait was acknowledged. All temporary display settings from this phase have been restored to the local baseline.

Source review and a synthetic backend using the actual compiled `MacMusicService` reproduced a separate cache defect: four failed player reads erased previously successful metadata and artwork. The poller now distinguishes failure from confirmed stop or application exit. With no selected successful track, any running-player read failure preserves cached data and resets the empty-poll count. Three consecutive confirmed empty polls still clear playback; explicit feature disable clears immediately without probing players. Four regressions cover failures, a stopped second player, interrupted empty-poll debounce and opt-out. A local independent check passed cache preservation, confirmed exit and explicit disable after failing before the repair. It started no music app and sent no Apple Events. Release passed. Local XCTest is still blocked before execution. Program commit `50240016ba810f1f302a6e77e598f7b8c7ffbcf8` passed [Mac CI 39362895479](https://github.com/yaoyouzhong/AI-bot/actions/runs/39362895479); complete downloaded artifacts verified 122 tests with zero failures/skips, Release/app/tools, eight native PNGs and matching source/package hashes. Local app/signature checks also passed; the exact candidate is `artifacts/mac-tab5-20261020/5024001/AIBotBridge.app`. Normal-candidate Automation/play-pause acceptance remains pending.

On the normal `791d898` candidate, reducing only TAB5 brightness produced a matching save receipt and a visibly dimmer screen. After a normal power-off/on, USB recovered acknowledged data and the picture was normal; fresh settings readback retained the temporary brightness. Restoring the original brightness was acknowledged and visibly confirmed, passing effect, persistence and restoration without firmware writes. Lowering only device notification volume while unmuted was acknowledged and the physical preview tone was heard. Muting was then saved and the user observed a disabled preview button, matching the published `.234` UI logic. This verifies the muted control state, not a performed silent preview or exact volume ratio. The user subsequently acknowledged successful restoration of the original device volume and unmuted state. The private restoration baseline stays only in ignored local evidence. Mac system audio, artwork covers and pets remain separate checks; the later page/cycle/saver results are recorded above.

The user revoked Bluetooth permission in System Settings and observed the app reporting denied permission with recovery guidance. Restoring that permission returned to authenticated USB-priority standby. Revocation handling and recovery passed; rejecting the original first-use prompt remains untested because the user originally chose Allow.

The user opened the exact `791d898` candidate with Bluetooth initially off. Only that candidate process remained; unauthenticated HTTP status routes still returned 401. After enabling Bluetooth, macOS opened Keyboard Setup Assistant, consistent with the firmware advertising HID. The user quit that assistant and reported acknowledged BLE data, passing actual status transfer on the new candidate. The initial pairing-dialog hold time was not explicitly reported, so the exact delayed-confirmation check is not marked passed. The user subsequently confirmed no numeric-comparison dialog reappeared during sleep/wake or USB return, passing normal reconnection without repeated pairing. The user then slept the Mac for about fifteen seconds under the same Bluetooth-only isolation and observed acknowledged data about twenty seconds after wake. Sleep recovery passed physical revalidation. The bilingual candidate guide now distinguishes two-ended pairing approval, Keyboard Setup Assistant and Keychain authorization.

On the same `5e91c29` candidate, choosing Always Allow required two confirmations. The user then disabled Bluetooth, normally quit and reopened that same candidate and confirmed there were no Keychain prompts. Normal authorization and persistence across relaunch passed for that candidate; new signatures and denial/recovery remain separate checks. Startup reads the legacy LAN token and TAB5 pairing record, but the screenshot does not identify each prompt's account. The separate Bluetooth numeric-comparison dialog reappeared after roughly fifteen seconds. The host's original fifteen-second connect/authentication deadline could cancel the link and retry after three seconds, while the firmware permits thirty seconds for explicit approval on TAB5. The initial handshake window is now sixty seconds, with a status message directing the user to compare and approve both ends; service discovery requests only the dedicated TAB5 service, leaving unused HID services undiscovered by this app. Data/RPC deadlines, USB priority, HMAC authentication and device pairing policy are unchanged. No pairing code is recorded, no approval is automated and no bond is deleted. Local Release and app signing passed, and the linked `791d898` CI plus full downloaded artifacts verified 118 tests with zero failures/skips, matching source/package hashes, tools and eight native PNGs. The exact candidate is in `artifacts/mac-tab5-20261020/791d898/AIBotBridge.app`; local XCTest is still unavailable. System-pairing timing remains separately tracked. Bluetooth was paused before the retest; after isolation the user restored the original Auto connection mode and left device settings. After reconnecting USB, the user confirmed acknowledged USB data and authenticated USB-priority BLE standby, passing return priority.

The user reported three to four consecutive local-password prompts. A later screenshot confirmed AI-bot Bridge requesting confidential information for service `io.github.yaoyouzhong.AI-bot` in the login Keychain, with Always Allow, Deny and Allow buttons. The user selected Allow each time and still reported four prompts on `5e91c29`; that one-time-access run did not establish elimination of repeated prompts; the later same-candidate Always Allow/relaunch check passed. The screenshot does not identify the account within that shared service. No screenshot or password was copied into the repository. Source review found repeated legacy LAN-token Keychain reads across startup, HTTP authentication, USB provisioning and the mirror window. A denied read also previously looked like a missing record and could trigger a replacement attempt. One serialized process-local cache now retains the initial read result, including failures, so background consumers do not re-prompt. Only a missing item is created; denial, invalid encoding and short stored credentials do not replace existing records. Explicit successful saves update memory; failed writes retain the previous value. Persistence remains in the existing Keychain item, with no ACL change or secret diagnostics. Four regressions cover these flows; an independent local check using the actual cache source and synthetic storage passed all four groups, including 32 concurrent reads, without accessing Keychain. Full local XCTest is still blocked before execution. External edits to the Keychain item require restarting the process; explicit in-app saves update the cache immediately. The separate TAB5 pairing item and authorization for a newly signed candidate can still require normal access approval. Apple documents Allow as one-time access and Always Allow as persistent permission for that app/item; the same candidate is being retested with this normal system authorization. No access for all applications is enabled.

Under Bluetooth-only isolation with USB disconnected, sleeping the Mac for about 15 seconds and waking it left the app searching after the requested wait and a later confirmation. Read-only system profiling still classified TAB5 as connected; no addresses or pairing data were printed. The Mac previously relied solely on advertisements, while the firmware stops advertising during a live connection. Discovery now first retrieves system-connected peripherals implementing the dedicated TAB5 service and uses the existing local connect, service discovery and device-ID/nonce/HMAC authentication path. It falls back to scanning when none are found. No name-based trust, unrelated HID retrieval, protocol change or firmware change is introduced. Local Release and app signing passed. The linked `5e91c29` CI and complete downloaded artifacts verified **118 tests with zero failures/skips**, matching source/package hashes, tools and native fixture renders. Only the new candidate process remains running; The later physical sleep-recovery result is recorded above; firmware-package CI alone is not hardware acceptance. Local XCTest remains blocked before execution.

For the first Bluetooth prompt, the user actually chose Allow, then disabled and re-enabled the feature. The row continued to say Bluetooth was off. Reusing a central manager does not produce a new state callback when its state is unchanged; re-enabling now explicitly processes the current state, and late state callbacks after disable are ignored. This does not change scanning, pairing, USB priority or wire formats. The `.234` advertised service UUID matches the Mac. The linked `ffdf43b` CI and full artifacts passed verification with 114 tests and zero failures/skips; local Release/signature checks passed, while local XCTest remains unavailable. After opening the exact `ffdf43b` candidate and toggling Bluetooth, the user observed searching followed by authenticated USB-priority standby. The toggle repair, actual BLE authentication and standby passed. Permission revocation/recovery and the later `791d898` sleep/wake retest passed; denying the original first-use prompt remains untested. Windows Bluetooth contention was suggested for diagnosis, but no user confirmation establishes whether Windows Bluetooth was disabled or caused the initial delay.

The device's original connection mode was Auto. The user temporarily selected Bluetooth only, left its settings page, disconnected Mac USB and retained independent power. The Mac then reported acknowledged BLE data; display settings readback and saving unchanged values also received the matching success receipt under the same isolated conditions. Actual BLE status/settings transfer passed without Wi-Fi routing. Disabling Bluetooth for about five seconds and re-enabling it restored acknowledged data without re-pairing, passing reconnection under the same isolation. Brightness, sound, pages and cycling were unchanged; the user restored the original Auto connection mode after the isolation checks.

Two additional repairs follow the first firmware-package CI: operation receipts now stay in a fixed footer outside the scrolling setup form, verified by minimum-size native fixture rendering; Codex CLI discovery now handles the actual nested ChatGPT desktop layout and renamed/moved registered applications while preserving explicit overrides and legacy fallbacks. The interaction window now displays live Codex connection status. On this host, CLI `0.163.0-alpha.10.2` exposes the proxy command but its default socket is missing (`ENOENT`), initialization exits before a handshake and the desktop child uses stdio. Codex conversation acceptance is therefore blocked even after executable discovery succeeds. No alternate daemon was deployed, no task methods were called and no model/approval configuration changed. Release/app/signature/native rendering and the linked `f3f9dd6` CI passed; full downloaded artifacts verified 114 tests with zero failures/skips and matching source/package hashes. The user normally quit the initial candidate, opened the exact committed `f3f9dd6` app and confirmed USB recovery without re-pairing. The process, listener and serial owner were independently verified. The user repeated an unchanged-settings save in the new candidate and confirmed the success receipt was directly visible in the fixed footer without scrolling; physical revalidation passed.

With TAB5 independently powered and USB disconnected, the user observed an authenticated Wi-Fi request within approximately 15 seconds. Readback and an unchanged-settings save also succeeded over Wi-Fi; reconnecting USB restored acknowledged data and a normal device picture. The exact eight-second threshold was not measured. Existing Wi-Fi configuration was retained; discovery, provisioning, address recovery and fixed modes remain pending. The actual curated ZIP first passed isolated validation for 366 works and 826 frames; later normal-app import and physical subset results are recorded above.

Acceptance resumed on the test branch from clean HEAD `bf16f1f`, matching the remote. The user confirmed the TAB5 USB connection and the Windows bridge was stopped or paused. This session permits temporary local candidate execution and related repairs, with no main merge/push, persistent installation, deployment, publication or firmware writes. Preserve TAB5 display settings; real Codex sends/appends/interrupts require a designated test task.

The host reports Apple Silicon, macOS 27.2 (`26B5112a`) and Swift 6.2.3. Local tests are **blocked before execution**: the selected Command Line Tools lack both the XCTest module and xctest, and no Xcode installation was found. Release, pinned bundled-tool hashes, ad-hoc signing, strict signature verification, offline tool-help checks and eight native fixture PNGs passed. The existing SerialBridge Sendable capture warning remains recorded. Native UI automation timed out for both the candidate and Finder. The user subsequently launched the initial candidate normally and confirmed its menu bar; its process, listener and HTTP 401 responses for both unauthenticated status routes were independently checked. The user also confirmed explicit USB pairing and the window's acknowledged-data status. Detailed screen data, remaining windows and permissions are tracked separately; the normal quit and restart result is recorded above.

USB descriptor and a read-only 460800-baud v1 ping confirmed a TAB5 running `0.2.234-ui`, with RPC v1/binary and packed/delta capabilities. The single UI sample had age 13 ms and zero flush errors; it does not establish boot health. The user confirmed automatic USB recovery after a 12-second cable disconnect without re-pairing. Display settings readback passed; saving unchanged settings was first correctly rejected while the device settings page was open, then acknowledged after leaving that page. The later brightness/persistence and device-audio results are recorded above; remaining display settings are tracked separately. No ESP8266 was tested.

An actual official `.234` upgrade archive exposed a loader defect: seven dependency-license paths contain nine components, exceeding the previous eight-component ASCII limit. Bounded support now allows 16 components and at most 720 bytes while retaining all component, traversal, duplicate, symlink and size checks. Entries are still streamed by exact name, never extracted to archive-provided paths. Synthetic upgrade/first-install ZIP regressions cover the published layout and reject unsafe paths. The repaired loader successfully validated the real upgrade image and its bound sidecar version, application/ELF SHA and structured notes. The real first-install ZIP also passed its manifest, layout, erased-gap, segment-hash and exact-application checks. These offline checks created no bridge service and accessed no hardware. Updated Release/app/signature checks passed; the committed candidate embeds source SHA `3cf5fa8`. Its linked Mac CI and downloaded full artifacts passed verification: 112 tests, zero failures/skips, executable/app/source hashes, licenses, tools and eight fixture PNGs. Local XCTest remains blocked. Startup and first pairing used the initial `bf16f1f` candidate; recovery on the repaired candidate has since passed, while normal-app firmware preview and all firmware writes remain untested. Evidence stays in the ignored local artifact directory; no credentials, private network configuration or raw runtime streams belong in Git.

## 继续非固件实机验收：程序入口 / Continued non-firmware acceptance: application launcher

用户要求继续测试其他未通过项，涉及固件的先不动。当前仍运行 codex-submit-place-final，七项功能开关开启。先验常用程序入口：正常 Mac 功能窗口确认入口列表为空，经系统 OpenPanel 添加系统 Calculator.app（只保存入口，未启动程序）；页面确认入口已保存。进程元数据确认设备点击前计算器未运行。已请用户从实体 TAB5 应用／控制页点一次该入口，效果待反馈；不以在 Mac 主动启动程序替代设备验收。临时入口与本次打开的计算器将在验收后单独收尾，七项日常开关保持开启。未改源码、设备设置或固件。

The user requests continuing remaining acceptance while deferring firmware work. The current codex-submit-place-final candidate retains all seven opt-ins. For the application launcher test, the normal feature UI showed an empty list; the system OpenPanel added Calculator.app as a temporary launcher without starting it. The UI confirms saving, and process metadata confirms Calculator was not running before the requested physical device click. One TAB5 application/control-page launch is requested; feedback is pending. Direct Mac launch is not substituted for device acceptance. The temporary launcher/test-opened Calculator will be cleaned up separately while keeping daily opt-ins on. No code, device-settings or firmware change occurred.

用户确认点击 TAB5 的 Calculator.app 后计算器打开。元数据核对由点击前未运行变为点击后运行，随后才用 CUA 选取其已有窗口，确认真实计算器界面；没有先从 Mac 主动启动替代设备效果。常用应用程序入口本轮实机通过。正常退出测试打开的计算器，桥接移除唯一临时入口后显示列表为空／入口已移除；未清除计算器已有表达式或修改系统 App，七项日常开关保持开启。

The user confirms that the physical TAB5 launcher opens Calculator. Process metadata changes from not running before the click to running afterward; only then is the existing Calculator window inspected through CUA. Application launching passes physical acceptance without a substitute Mac-side launch. The test-opened app is normally quit, and removing the sole temporary launcher restores the empty list with confirmation. Existing calculator data/system app and seven enabled daily preferences are preserved.


## 2026-10-21 公开进展缺失与延迟修复 / Missing and delayed public progress

用户补充：运行时的进展没有显示，直到选择提示暂停时才出现该提示，前面文字仍缺失。只读对比同一轮历史：`summary` 返回 1 条最终消息，`full` 返回 9 条公开消息（包括进展），无需固件变更即可复现摘要丢失。原 Mac owner IPC 虽读取实时快照，但只保留状态与轮次，没有投影公开文字；`page=-1` 也被错误映射到第一页。

Mac 现读取完整条目，立即剔除非公开类型及额外字段；验证现有 owner 后补充本轮公开进展，用条目 ID 合并并避免滞后快照缩短已保存文字。尚未保存的新轮次可显示，自动更新保持跟随尾页，显式历史／页码保持固定。不读取或导出原始 rollout、思考、工具输出或用户消息；只读子进程仍不能发送或 resume，没有自动回复任何选择。协议与固件未变。

Release 通过。本机 `swift test` 仍因缺少 XCTest 模块而未执行；断言适配器执行实际生产源码与 62 个现有／新增测试体通过，覆盖实时文字、完整历史、隐私白名单、实时轮次早于落盘、尾页持续跟随和历史位置保持，也回归此前桌面草稿与天气修改。这不是 XCTest／完整 CI 或实机通过。只读实际 owner 探测取得 active/inProgress 及非空公开进展；没有保存原文。候选切换与 TAB5 动态显示待实机反馈。

The user reported missing progress until a selection pauses execution, with earlier text still absent. Read-only comparison of one turn returned one final message in summary mode versus nine public messages in full mode. The Mac owner snapshot previously retained runtime only, discarding public text; page -1 incorrectly selected the first page. Mac now retains complete public items, merges validated owner progress, handles a live turn before persistence, follows the tail page and preserves explicit historical positions. Non-public types and extra fields are discarded; no protocol/firmware changes, automatic sends or approvals occurred. Release passed. Local XCTest remains unavailable; an assertion adapter ran 62 real test bodies against production sources successfully, which is not full XCTest/CI or hardware acceptance. A read-only real owner probe returned active/inProgress with public text, without recording the text. Candidate switch and physical live-update acceptance remain pending.


本轮实际候选为 `artifacts/mac-tab5-20261021/codex-live-public/AIBotBridge.app`（未提交工作区叠加候选）。打包、严格签名校验、无凭据 helper 身份探测和离线刷机工具自检均通过，helper 与前一正常候选逐字节相同。正常退出旧候选、通过界面启动新候选后，核对只有一个桥接进程与监听者；交互窗口 31 个会话、七项开关全部保持启用，随后 USB 数据与认证 Wi-Fi 恢复，蓝牙正在寻找。未更改任何设备设置。生产 reader 的只读集成探测在 active/inProgress 时返回 200、当前实时轮次、非空公开文本与正确尾页；没有记录正文。已请用户在当前任务查看之前的进展，并观察运行时新文字，尚未收到这一轮实机反馈。

The exact uncommitted candidate is `artifacts/mac-tab5-20261021/codex-live-public/AIBotBridge.app`. Packaging, strict signature verification, credential-free helper identity and offline bundled-tool checks passed; helper bytes match the previous normal candidate. After a normal old-app quit and UI launch, one bridge/listener, 31 conversations and all seven opt-ins were confirmed. Acknowledged USB and authenticated Wi-Fi recovered; BLE was searching. Device settings were unchanged. A read-only integration probe through the production reader returned HTTP 200, the current live turn, public text and the correct tail page during active/inProgress. No body text was recorded. Physical progress/history and live-update feedback remain pending.


用户已在新候选上选择当前任务并回到最新，确认本轮前面指定的两段进展“都能看到”。先前公开文字缺失的恢复已获实机反馈；仍单独询问后续两段是否在点击选择之前自动出现，实时刷新验收尚未确认。

On the new candidate, the user selected the current task/latest view and confirmed both specified earlier progress messages were visible. Recovery of previously missing public text has physical confirmation. Whether subsequent progress appeared automatically before selecting an answer remains a separate pending check.


随后按两段各约 20 秒的运行中观察验收，用户先明确“是实时更新的”，再确认两段均在选择问题出现前自动显示。此前进展文字恢复与运行中自动刷新两项均实机通过；不把这项结果扩大为无限历史、逐 token 延迟或全部通道延迟指标。当前候选继续运行，七项已授权功能开关保持启用，固件及设备设置不变。

During a follow-up with two progress messages and approximately 20 seconds of observation after each, the user explicitly confirmed live updates and that both messages appeared automatically before the selection question. Recovery of earlier progress and automatic updates during execution both pass physical acceptance. This does not establish unlimited history, per-token latency or timing across every transport. The current candidate remains running with all seven authorized features enabled; firmware and device settings are unchanged.


继续非固件验收：已请求在指定“TAB5 操作验收”快捷控制台进行约 20 秒、含约 3 秒停顿的录音，只预览／核对草稿，不发送；核对录音是否中断、两段是否保留，以及实际声源标签。用户尚未提供此项结果，不能将此前连续追加／清空通过扩大为长录音或默认麦克风来源验收。

Non-firmware acceptance continues with a requested approximately 20-second recording, including a three-second pause, in the designated test chat. It is preview/draft-only with no send. Interruption, retention of both segments and the actual source label remain awaiting user feedback; earlier continuous-append/clear acceptance does not establish these results.


## 2026-10-21 长录音失败与分段修复 / Long-recording failure and segmented text

用户实际反馈：第一段被第二段覆盖，中途偶发连接中断，因此这次约 20 秒／含停顿测试未通过。不能扩大此前连续追加、清空或短录音结果。检查时正常候选为 review、最后请求 USB 查询 200、没有保留识别服务异常历史，无法据此确定之前断开的具体原因；已询问原提示／错误码，无需重复录音来回忆。

原后端直接发送每次 bestTranscription.formattedString，状态层直接替换预览。新后端使用 Apple 文档定义的音频 timestamp/duration 区分不重叠的新段与同段修正；累计结果覆盖旧范围时替换该范围避免重复。空结果不抹掉已有文字，无时间证据时不猜新段。仍按完整合并文字执行 2000 UTF-8 字节上限；实际识别异常保留预览，不允许把不完整文字自动确认为可插入草稿。取消及同一 voiceId 的麦克风切换新增 generation 校验，防止旧回调误结束或污染新收音。后端回调释放锁后再进入 owner，避免新合并锁与停止／feed 锁顺序冲突。

增加固定枚举诊断：最近识别异常跨后续成功录音保留，结束原因区分用户停止、设备八秒无有效请求、一分钟上限和识别器结束；不记录正文、原始错误内容或录音。真实断流仍停止收音，未放宽超时、自动重启或采用云端回退。原偶发连接中断尚未定位／验收。

Release 通过；本机 XCTest 仍缺模块未执行。断言适配器针对实际生产源码运行 92 个测试体通过，包括分段保留、错字修正、累计结果去重、重复词保留、相邻边界、缺少时间、独立新录音、旧麦克风回调、20 秒正常轮询与真实超时，以及识别异常预览／诊断。它不是 XCTest／完整 CI／硬件录音通过。未改变固件或协议。官方时间字段来源：[Apple SFTranscriptionSegment](https://developer.apple.com/documentation/speech/sftranscriptionsegment)。

The user reported that the second utterance replaced the first, plus intermittent disconnections. The long-recording/pause check therefore failed. Inspection found review state and the last USB poll returning 200, with no retained speech-service error history; the previous disconnection cause remains unknown. A prompt/error-code clarification is pending without requiring another take just to recall it. The backend now retains non-overlapping utterances using documented audio positions, accepts corrections within a range and replaces covered ranges on cumulative recovery. Empty callbacks preserve text, unknown timing does not invent boundaries and the complete merged transcript remains limited to 2000 UTF-8 bytes. Generation checks reject cancelled/switched microphone callbacks, and backend locks are released before entering the voice owner. Fixed-label failure/end-reason diagnostics retain no transcript, raw error or audio. Genuine disconnects still stop capture, with no relaxed timeout, automatic restart or cloud fallback. Release passed; local XCTest remains unavailable. An assertion adapter ran 92 actual test bodies against production sources successfully, which is not full XCTest/CI or physical recording acceptance. Firmware/protocol are unchanged. Intermittent disconnection remains unlocated and unaccepted.


用户记不清中断原文／错误码，不要求为回忆再录。补充仅数值的本地识别诊断：回调次数、含有效音频位置的次数和保留段数，结束后保留最后统计、不包含正文。最终候选为 `artifacts/mac-tab5-20261021/voice-segments-checked/AIBotBridge.app`；前一 `voice-segments` 包只完成离线构建，未运行。最终包 Release／严格签名、helper 身份及离线随包工具自检通过，helper 与前一正常候选逐字节相同。最终源码的 92 个断言适配测试体通过，最终 XCTest 重试仍缺模块未执行。

退出前正常窗口确认上一轮已取消，最后 USB 取消请求 200；正常退出 `codex-live-public` 后界面启动新候选，唯一运行进程确认，USB 数据及认证 Wi-Fi 恢复，31 个会话、七项开关保持启用，蓝牙正在寻找。未变更展示、配网或固件。已请求相同指定测试任务进行两段／约 3 秒停顿复验，只预览／核对草稿、不发送；若发生中断，保留现场诊断，暂不重复录音或取消。当前实机结果待反馈。

The user could not recall the interruption message/code; no extra take was requested merely to recall it. Added transcript-free numeric diagnostics for callback count, callbacks with usable audio positions and retained utterance count. Final candidate: `artifacts/mac-tab5-20261021/voice-segments-checked/AIBotBridge.app`; the earlier `voice-segments` bundle was built offline only and never launched. Final Release/signature/helper identity/offline-tool checks and 92 assertion-adapter test bodies passed; final local XCTest retry still failed before execution due to its missing module. The old normal app showed cancelled capture and USB cancel 200 before normal quit. UI launch of the new exclusive candidate recovered acknowledged USB/authenticated Wi-Fi, 31 chats and all seven opt-ins; BLE was searching. Device settings/firmware are unchanged. A two-utterance/pause retest in the designated chat is pending, preview/draft-only with no send. On failure, the user was asked to retain the diagnostic scene rather than retry/cancel immediately.


### 零时间临时结果的实机补充 / Zero-position provisional callbacks

`voice-segments-checked` 实机仍未通过：用户确认第一段已保留，但第二段出现两次，顺序为第二／第一／第二，且三段已回填到 Mac Codex 输入框；没有授权或执行发送。仅正文外的原生桥接诊断取得 17 次结果、3 次含有效音频位置、保留 3 段；最近为 USB 查询 200、用户停止收音，没有本轮识别异常记录。这不能证明偶发中断消失。

初版合并错误地接受 0/0 占位音频范围，导致临时的第二段被插入到第一段前，之后又追加带时间的第二段。后续修正将无有效时间的结果作为独立临时预览，带时间的结果到达后替换该临时副本，并只按有效音频范围维护稳定顺序。预览副本去重不用于决定真实分段身份；相同词语在不同时间仍保留。新增零时间 → 带时间 → 第二段零时间 → 第二段带时间的回归，另覆盖临时累计副本和真实重复语句。生产源码断言适配器 95 个测试体通过，Release 通过；不是完整 XCTest／CI 或实机通过。当前重复测试草稿不自动删除、不发送，下一次复验由用户清空指定验收草稿。

The first segmented candidate still failed physically: the first utterance survived, but the second appeared twice in second/first/second order, and all three had reached the Mac composer. No send was authorized or performed. Transcript-free native diagnostics recorded 17 callbacks, three with usable audio positions and three retained ranges, with USB poll 200 and an explicit user stop. No recognition failure was recorded for that take; this does not establish that intermittent disconnections are resolved. The first merge incorrectly accepted 0/0 placeholder positions. The correction keeps untimed results as a separate provisional preview, replaces that copy when timed results arrive and orders stable text only by usable audio ranges. Display-copy reconciliation does not define utterance identity; real repetition at different audio times survives. New regressions cover the observed zero/timed callback sequence, untimed cumulative copies and repeated utterances. All 95 assertion-adapter test bodies and Release passed; full XCTest/CI and physical acceptance remain separate. The existing test draft is neither automatically removed nor sent; the user will clear only the designated acceptance draft before retesting.


零时间修正候选 `artifacts/mac-tab5-20261021/voice-partials-reconciled/AIBotBridge.app` 已完成打包／严格签名、无凭据 helper 身份及离线工具检查，helper 与前一候选逐字节一致；本机 XCTest 仍缺模块未执行。正常退出旧候选后界面启动新候选，确认唯一进程、USB 数据和认证 Wi-Fi、31 个会话及七项启用状态；蓝牙正在寻找。只读取得的旧候选诊断已记录，无需保留旧预览来诊断。已请用户清空指定任务上一轮测试草稿，再录第一／第二段，中间约三秒，停止后比较 TAB5 最终预览与 Mac 回填的顺序和次数，不发送；当前待反馈。固件和设备设置不变，偶发中断仍保留待验。

The zero-position correction candidate `artifacts/mac-tab5-20261021/voice-partials-reconciled/AIBotBridge.app` passed packaging/strict signing, credential-free helper identity and offline bundled-tool checks, with unchanged helper bytes. Local XCTest still failed before execution because the module is unavailable. After normal replacement through the UI, exclusive process ownership, acknowledged USB/authenticated Wi-Fi, 31 chats and all seven opt-ins were confirmed; BLE was searching. Previous transcript-free diagnostics are recorded. The user was asked to clear only the previous designated test draft, record the first/second utterances with a three-second pause and compare final TAB5 preview/Mac composer order and multiplicity, without sending. Feedback is pending. Firmware/device settings are unchanged; intermittent disconnection remains pending.


用户确认零时间修正版在 TAB5 最终预览和 Mac 输入框中均按第一／第二段排列、各一遍，分段保留、顺序及去重回填实机通过。随后单独请求同一任务约 30 秒、含两次约三秒停顿的录音稳定性验收，只核对草稿、不发送，结果待反馈；原偶发中断不因文字合并通过而自动记为修复。

The user confirmed correct first/second order with exactly one copy each in both final TAB5 preview and the Mac composer. Utterance retention, order and deduplicated staging pass physical acceptance on the reconciled candidate. A separate approximately 30-second take with two three-second pauses was then requested in the same test chat, draft-only/no send, with feedback pending. The original intermittent disconnection is not considered fixed solely from the text-merge result.


30 秒稳定性复验用户确认“没有中断，文字都保留”。期间原生桥接只读状态两次取得“正在收音 · Mac 默认麦克风”、USB 查询 200；最终为 review／用户停止，93 次识别结果、4 次含有效音频位置、3 个已定位范围，没有识别异常记录。未读取或保存正文／音频，未点击发送、清空用户草稿或修改设备设置。此候选的 Mac 默认麦克风、含停顿较长录音与前后文字保留本轮实机通过；此前偶发中断没有在本轮重现，未确定根因，不推定为所有通道、最长一分钟、真实断流恢复或长期稳定性全部通过。当前候选继续正常运行，七项开关保留开启。

The user confirmed no interruption and complete text retention during the requested 30-second/pause retest. Two transcript-free native observations showed active capture from the Mac default microphone with USB poll 200; the final state was review after an explicit user stop, with 93 result callbacks, four having usable audio positions and three timed ranges. No recognition failure was recorded. No transcript/audio was read or stored, no send/user-draft cleanup was performed and device settings were unchanged. Mac-default capture, longer capture with pauses and retention pass this physical take. The earlier intermittent failure did not recur and remains without an established root cause; other transports, the full one-minute limit, genuine disconnect recovery and long-term stability are not inferred. The candidate remains running with all seven opt-ins enabled.


继续非固件验收进入行情。检查现有 7 项自选发现一条美股指数显示代码为空，Mac 改为按规范化 symbol 的市场后缀生成代码，已构建／正常启动 `stock-codes` 候选。98 个断言适配测试体及 Release／签名／离线检查通过，XCTest 仍缺模块未执行。真实成功缓存 7 项全部齐全，字段缺失从 1 项降为 0；不记录行情数值或名单。USB／Wi-Fi 与七项授权保持；实体三栏结果待反馈，见[日常数据记录](MAC-TAB5-DAILY-DATA-20261020.md)。

Non-firmware acceptance moved to stock display. One empty US-index display code among seven selected quotes was repaired using normalized symbol identity. The stock-codes candidate is running normally. All 98 assertion-adapter bodies, Release/signature/offline checks passed; local XCTest remains unavailable. Fresh actual cache completeness improved from one missing display field to zero, with all seven quotes present. No quote values/watchlist were recorded. USB/Wi-Fi and all seven opt-ins remain; physical three-column feedback is pending in the daily-data record.


用户确认行情三栏均正常，三市场的现有自选／名称／价格／涨跌幅显示实机通过。下一项为现有桌宠动画与状态页，不导入／替换资源，不更改展示默认或固件。

The user confirmed normal stock display across all three markets, passing the existing-watchlist/name/price/change check. Next is the existing pet animation/status page, without importing/replacing resources or changing display defaults/firmware.


桌宠预检只读取当前缓存的结构，不导入或替换文件。Claude／Codex 两栏均未选本机缓存，使用项目原创 MacByteSprout 默认动画（112×112、2 帧）；消费者在 working 时播放，idle／offline 时静止为预期。已请求用户查看左右图像是否完整、方向正常，以及 Codex 工作时动作，待实机反馈。未切换默认显示模式、下载外部图库或发送真实测试任务。

Pet preflight inspected cache structure without importing/replacing files. Both roles use the project's original MacByteSprout fallback (112×112, two frames), with no selected local cache. The device animates working state and keeps idle/offline still. Physical visibility/orientation and Codex working animation feedback are pending. No default display mode, external gallery download or real task send occurred.


2026-10-22 用户确认桌宠两只均正常、Codex 工作时有动作，当前默认桌宠的可见性／朝向／工作动画实机通过；同时明确外观与 Windows 不同。Mac 当前无角色缓存，使用原创默认机器人；Windows 优先级为已选动画、通用动画、本机旧版缓存，实际所用资源仍需确认。外观一致性不计为通过；先核对资源来源和迁入方式，不改固件、不擅自替换当前选择。

On 2026-10-22 the user confirmed both pets render normally and Codex animates while working, passing current fallback visibility/orientation/working animation. They also reported a different appearance from Windows. Mac has no role cache and uses its original fallback; Windows prioritizes selected/common/imported local animations, with its actual choice still to be confirmed. Cross-platform appearance parity is not accepted. Resource origin/import paths are being checked without firmware changes or an unsolicited replacement.


### 原版桌宠私有恢复 / Private restoration of original pets — 2026-10-22

用户确认 Windows 使用原来的 Claude／Codex 两套宠物，并指向 GitHub。只读查到个人旧仓库 `yaoyouzhong/esp8266-ai`，固定版本 `9814403e997346885c651a64f6b186ac101c5931` 的 `firmware/include/img/{claude,codex}_sprite.h`。两个文件的 Git 历史最后变更均来自上游作者的 [5f52a39](https://github.com/yaoyouzhong/esp8266-ai/commit/5f52a39bd5c4245aff24bfa3a23cde2d62d57e3c)，不能视为维护者独立创作或加入本项目分发。只下载这两个数据文件到仓库外 `~/Library/Application Support/AI-bot/private-imports/legacy-pets-9814403`，不执行其中代码，不迁入第三方 Git 历史，不加入源码或发布包。

下载字节与 GitHub blob 标识核对一致：Claude `4939f3c78acd3630ab48614350ece730247e096a`，Codex `3e57a1d2b320f36b951c07165560d4dd7d10a6e6`。结构确认 Claude 111×120、Codex 120×120，各 6 帧且帧编号按 0–5 排列。使用现有生产 `MacPetCache.importDefaults(from:)`，通过临时本机辅助程序先导入仓库外隔离缓存；每个 RGB565 像素与 Windows `LegacyPetImport` 的字节序规则、每帧 120ms 全部核对一致，再调用同一生产方法写入正常私有 `pets/*-default.apet`。写入后逐字节复核，两角色原先均无 selected/default 缓存，未创建或覆盖 selected 文件。辅助程序不启动桥接服务、不访问凭据／音频／Codex 正文、不操作固件；未修改生产代码或更换正在运行的 stock-codes 候选。

七项功能开关仍开启。当前原生桥接显示 USB 未确认／Wi-Fi 等待连接，已请用户接回 TAB5 并打开桌宠页。私有缓存导入和数据校验通过；设备资源同步、与 Windows 外观一致性仍待实体反馈，不能计为实机通过。没有改变 TAB5 展示／配网设置，也未执行固件构建或刷写。

The user confirmed the original Claude/Codex pair on Windows and suggested GitHub. Read-only inspection located both sprite headers at the pinned old-repository revision above. Their latest file changes originate from upstream commit 5f52a39, so they are not maintainer-authored project assets or redistribution inputs. Only these two data files were downloaded outside the repository into private Application Support storage; no external code/history was executed or migrated. Git blob identities, dimensions, ordered six-frame structure, every RGB565 pixel and each 120ms delay were verified. A temporary local helper invoked the existing production import method against an isolated private cache, then the normal private default cache, followed by byte-for-byte verification. Both roles previously had no selected/default cache; selected files remain absent. Production code and the running stock-codes candidate are unchanged, and all seven opt-ins remain enabled. USB/Wi-Fi are currently disconnected, so the user was asked to reconnect and inspect the pet page. Cache restoration passes data checks; actual synchronization and Windows appearance parity remain pending. No display/network preferences or firmware were changed.

用户接回数据线并打开桌宠后，Mac 仍显示 USB 未确认、Wi-Fi 等待、蓝牙配对认证失败。只读确认仅当前桥接持有所选串口，通过正常界面执行一次现有设备 USB 配对核对；生产方法复用已保存的同一密钥，并拒绝不同设备，未创建新设备授权或修改 Wi-Fi／展示设置。界面确认 USB 配对成功；用户随后明确反馈“两只都已一致”，原版双宠外观恢复实机通过。后续连接概览仍显示 USB 未确认／Wi-Fi 等待／蓝牙认证失败，因此不能把外观通过扩大为持续连接或蓝牙恢复通过；继续单独核对实时数据。

After reconnecting and opening the pet page, the user confirmed that both original appearances now match Windows, passing physical appearance restoration. The Mac initially still reported unconfirmed USB, waiting Wi-Fi and failed BLE authentication. Only the current bridge held the selected serial port. One normal UI pairing check reused the existing key for the same device; no new device authorization, Wi-Fi or display preference was introduced, and the UI confirmed pairing. A later connection overview still reported unconfirmed USB/waiting Wi-Fi/failed BLE authentication. Appearance acceptance therefore does not establish sustained connectivity or BLE recovery; live data will be checked separately.

### 用户在 Windows 更新固件后的连接检查 / Connection after a user-performed Windows firmware update

用户随后确认 TAB5 未连接，并补充刚在 Windows 的另一个 Codex 任务构建、刷入了新固件。Mac 的只读发现三次取得相同已知设备身份和 `0.2.236-ui`；本任务没有构建、刷写或回退固件。正常重启原 stock-codes 桥接未恢复，仅一个正常桥接持有串口；再次正常 USB 配对显示确认，但日常状态仍不被确认。不能用这些旧式配对回执认定新固件的完整连接通过。

Mac 增加固定枚举的 USB 失败阶段及 0–5／未知拒收代码，禁止输出原始异常、报文或凭据。诊断候选 `artifacts/mac-tab5-20261022/usb-diagnostic/AIBotBridge.app` 的 Release、严格签名、helper 身份、随包工具离线自检通过；helper 与 stock-codes 逐字节相同。98 个实际测试体通过断言适配器，本机 XCTest 仍因缺少模块未执行。这是诊断改动，不是新握手实现，也不等于连接修复。界面定位到“状态回执：超时”。

正常退出桥接、确认串口释放后，只发送发现查询及无用户正文的填充发现查询：约 53、4096、8192、16384、24000、32704 字节均收到正确身份应答，设备接收计数按长度增长，无溢出／二进制过期／发送不完整计数。一次 version=0、data=null 的无效状态边界查询未得回执，随后发现查询正常；未发送可覆盖显示缓存的合成状态或执行设备操作。检查仅保留数值、布尔值、固件版本、固定字段名和资源版本标识，不保留 nonce、主机标识值、密钥、报文正文或日志正文。

新 hello 暴露 `hostBindingVersion`、`hostNonce`、`currentHostId` 和 `usbHostAuthenticated`。三次只读结果均为 `hostBindingVersion=1`、`usbHostAuthenticated=false`，nonce 仅校验长度／十六进制形状；此 Mac 源码与可访问的已同步 GitHub 源码均无该握手定义，当前缺少新主机认证兼容。已请用户从 Windows 构建任务提供协议命令／字段／HMAC 消息格式或代码链接，不请求实际密钥或认证报文；不能猜测签名格式、绕开绑定或将旧配对确认扩大为认证成功。完整状态未被接受的最终归因仍需新协议定义和适配后的实机复验。此时原版宠物可能来自 Windows 已存的设备缓存，先前外观一致的实体反馈继续有效，但不能作为 Mac 已完成新固件资源传输的证据。

The user reported no connection and clarified that a separate Windows Codex task had just built and flashed new firmware. Read-only discovery identifies the known device running 0.2.236-ui; this task performed no firmware build/flash/rollback. A normal restart and one normal legacy pairing check did not restore daily data. A diagnostics-only Mac candidate adds fixed failure phases and bounded rejection codes without payload/error-description logging. Release/signature/helper/offline-tool checks and 98 assertion-adapter test bodies pass; local XCTest remains unavailable. The actual failure is status-ACK timeout. Read-only padded discovery requests from 53 through 32704 bytes all succeed, with matching receive counts and no reported overflow/expiry/incomplete-transmit events. An invalid-version/null-data boundary query got no status reply; discovery remained responsive. No synthetic valid display snapshot or device operation was sent. New hello fields consistently advertise host-binding v1 and unauthenticated USB, while the current Mac and accessible synchronized sources lack this handshake definition. The user was asked for the Windows task's protocol fields/commands/HMAC format or source link, never real keys or authentication packets. Implementing authenticated compatibility and physical retesting remain pending; no guessed signature or binding bypass is attempted. The confirmed pet appearance may reflect device caches restored by Windows, so it does not establish resource transfer from Mac on this new firmware.

只读检查结束后已正常启动 usb-diagnostic 候选，确认唯一桥接进程、七项开关保持开启、31 个 Codex 会话可读；返回连接概览，仍是状态回执超时，等待新主机认证协议定义。没有把无法连接描述为已修复，也没有代替用户联系 Windows 的任务。

After read-only probing, the diagnostics candidate was restored normally as the sole bridge process. All seven opt-ins and 31 readable Codex chats are retained; the connection overview still reports status-ACK timeout pending the new host-authentication definition. Connection is not claimed fixed, and no message was sent to the separate Windows task on the user's behalf.

用户提供 Downloads 的 mac-binding 迁移包后，已校验指导文件、在仓库外备份现有源码并按语义移植 USB 主机认证和 BLE 归属证明，未套用旧补丁或改固件。114 个断言适配测试体及 Release／签名／离线检查通过，完整 XCTest 仍缺模块。正常启动 host-binding 候选后，用户只接回 Mac 数据线，没有重新点配对；UI 确认 USB 数据、Wi-Fi 认证请求及蓝牙 USB 优先待命。用户随后确认当前任务能看到“连接已恢复”的新文字，Mac 已绑定自动接管及实时回复本轮实机通过。正在分项验证拔线保持／两电脑归属；完整备份、实现、验证与未验项见 [绑定迁移记录](MAC-TAB5-HOST-BINDING-20261022.md)。

After the user supplied the Downloads migration package, guidance/checksums and external source backup were completed, then USB claims and BLE ownership proof were adapted semantically without applying old patches or changing firmware. All 114 assertion-adapter bodies and Release/signature/offline checks pass; full local XCTest remains unavailable. The host-binding candidate authenticated the existing Mac after the user only reconnected USB, without re-pairing. UI confirmed USB data, Wi-Fi authentication and BLE standby, and the user confirmed this chat's new recovery message appears on TAB5. Existing-Mac takeover and live replies pass this physical take; unplugged/two-computer ownership checks remain separate in the linked migration record.

后续双机实测通过：Mac 拔线后仍无线连接 Mac；USB 接至 Windows 自动接管，再接回 Mac 自动恢复并能读到新回复；Mac 真实退出约 20 秒时 Windows 仍运行，设备等待 Mac、不自动切他机，桥接重启自动恢复；无 USB 时手动 Windows → Mac 两次切换及新回复均正常；TAB5 正常重启保留 Mac 当前目标和两条可信记录、自动无线恢复。最终 Mac UI 为 Wi-Fi 已认证、蓝牙数据已确认，七项开关开启、31 会话可读。未做首次新电脑／未知电脑、满列表、升级忙碌或真实迟到输入等故障注入，不标为通过。没有实际发送 Codex 消息、操作 Windows 输入、修改／刷写固件或改变展示／网络偏好。

Subsequent two-computer checks pass unplugged Mac retention, USB takeover in both directions with fresh Mac replies, no switch to online Windows during a confirmed approximately 20-second Mac-process outage, automatic bridge restart recovery, manual Windows-to-Mac wireless selection and TAB5 reboot persistence of Mac selection/both records with wireless recovery. Final native state shows authenticated Wi-Fi and acknowledged BLE, seven opt-ins and 31 chats. New/unknown computers, full-store/upgrade-busy cases and real stale-input fault injection remain unverified. No actual Codex send, Windows input, firmware change/flash or display/network preference edit occurred.

用户继续要求仅测非固件未通过项。按最新结果排除已通过的日历、行情、常用入口、桌宠、直达／发送、分段录音和主要绑定流程，下一项选定取消录音后重录的实机隔离检查，后续再独立核对无线语音等未覆盖通道。尚未发起录音：预检发现 Mac 无有效 USB／Wi-Fi／BLE 数据，已请用户确认 TAB5 当前仍选 Mac，并排除 Windows USB 接管后再核对本轮新回复。不把此时的未连接泛化为绑定回归，亦不在目标未确认时开始录音、清空草稿或发送。固件、功能开关和运行候选均未修改。

The user requested more unfinished non-firmware checks. Latest acceptance takes precedence over old pending entries. The next selected check is physical cancel/re-record isolation, followed separately by uncovered wireless voice paths. Recording has not begun: preflight currently sees no active Mac USB/Wi-Fi/BLE data, so the user was asked to confirm Mac remains selected, exclude Windows USB takeover and verify fresh replies first. No binding regression is inferred without confirming the selected target, and no recording/draft clearing/send starts against an uncertain target. Firmware, opt-ins and the running candidate are unchanged.

用户确认已选 Mac、新回复正常；预检 UI 同时确认 USB 数据与 BLE USB 待命、七项开关开启、语音等待收音。已请求仅在“TAB5 操作验收”执行取消→约三秒→重新录入→停止的实机检查，先由用户清理该指定任务上一轮测试草稿，核对最终 TAB5 预览和 Mac 输入框中取消内容不出现、重录内容仅一遍，不发送。初次只读诊断观察尚未见开始请求；当前结果待反馈，不能将请求步骤计为已执行／已通过。没有自动点击录音、操作 Codex 界面、读取正文或保存音频。

The user confirmed Mac selection and fresh replies; native preflight confirms acknowledged USB, BLE standby, seven retained opt-ins and idle enabled speech. A physical cancel/three-second wait/re-record/stop check was requested only in the designated acceptance chat, with user cleanup of its previous synthetic test draft and no send. Final device preview/composer should exclude cancelled content and contain one new utterance. The first read-only diagnostic observation had not yet seen capture start; execution/results remain pending. No recording click, Codex UI automation, transcript read or audio storage was performed.


取消／重录检查的新反馈：用户澄清取消正常，异常发生在第二次回填；“和 Codex 一起工作”是 Codex 空输入框的提示，被当成已有草稿一起回填。未将此归因为取消录音串入。只读检查本机安装资源确认主 ProseMirror 空段落的 `placeholder` 装饰及 CSS 生成文字；Mac 读取和写入前比较改用同一语义快照，仅在该结构标记、完整提示正文匹配且没有其他正文时将其视为空框。无标记的同名用户正文保留；混合、过期、不匹配或不完整标记拒绝写入；精确会话、单次写入、原始回读和发送收据保护保留。新增三项回归测试覆盖两种语言的空框回填、同名真实正文和异常结构。117 个真实测试体通过断言适配器；完整 `swift test` 仍因缺少 XCTest 模块失败。候选构建及实机回填验证进行中，暂不标记通过。未改固件、未清除用户当前草稿、未发送。

Cancel/re-record feedback was clarified: cancellation works; the second fill copied the empty composer's “Work with Codex” hint into the draft. This is not treated as cancelled-audio leakage. Read-only installed-resource inspection confirmed the main ProseMirror empty-paragraph `placeholder` decoration and CSS text. Mac reads and pre-write comparisons now use the same semantic snapshot, treating it as empty only with the structural marker, matching entire hint and no other body text. Identical user-authored words without the decoration remain intact; mixed, stale, unmatched or incomplete decorations reject edits. Exact chat binding, single-write/readback and submission-receipt guards remain. Three regression methods cover localized hint fills, authored hint text and invalid structures. All 117 actual test bodies pass the assertion adapter; full local XCTest still fails because its module is absent. Candidate packaging and physical fill verification are in progress, not yet passed. No firmware change, user draft cleanup or send was performed.


提示回填候选 `artifacts/mac-tab5-20261022/composer-placeholder/AIBotBridge.app` 已通过 Release 构建、严格签名、同一 Keychain helper 字节比较、helper 无凭据自检及刷机工具离线自检（未访问设备）。旧候选正常退出后已启动唯一新进程；原生 UI 确认七项功能开关均保留开启、31 个会话连接正常。启动时 USB 无可用串口，Wi-Fi 等待、BLE 寻找，后续需确认设备连接再进行回填。实机结果待反馈。

The composer-placeholder candidate passed Release packaging, strict signature validation, unchanged Keychain-helper byte comparison, credential-free helper self-test and offline flash-tool self-test without device access. The old candidate exited normally and the sole new process is running; native UI confirms all seven opt-ins retained and 31 connected chats. At startup there was no available USB port, with Wi-Fi waiting and BLE searching; device connectivity must be established before the physical fill check. Physical results remain pending.


用户在 composer-placeholder 候选上确认：按要求接回 Mac、在指定验收会话清理上一轮测试草稿并重新录入后，Mac 主输入框只有本次录入句，显示正常，没有混入空框提示和前置空行。空框提示误回填本轮实机通过；修复后的取消→重录完整流程仍需单独回归，不把单次回填等同于该组合流程通过。未发送、未改固件。

On the composer-placeholder candidate, the user confirmed the requested physical fill displays only the new utterance in the Mac composer, with no empty-editor hint or leading blank lines. The placeholder-copy bug passes this physical take. The complete cancel/re-record sequence after the fix remains a separate pending regression; a single successful fill does not establish that sequence. No send or firmware change occurred.


composer-placeholder 候选取消／重录组合实机通过：用户确认完成首句录音中取消、等待约三秒、重新录入第二句并停止；TAB5 预览和 Mac 主输入框都只有第二句、各一遍，没有首句、空框提示或前置空行。本项按最新反馈标记通过。后续独立检查 USB 拔线后的无线语音回填，不重复已通过的本地分段录音稳定性。未发送、未改固件或偏好。

The complete cancel/re-record sequence passes on the composer-placeholder candidate: the user cancelled during the first take, waited approximately three seconds, recorded the second take and stopped. Both the TAB5 preview and Mac composer contain only the second utterance once, without cancelled text, empty-editor hints or leading blank lines. This item is now passed. Unplugged wireless voice filling remains a separate check; previously passed local segmented-recording stability need not be repeated. No send, firmware or preference change occurred.


已请求当前候选的无线快捷控制台回填：保持供电拔除 Mac USB，先确认 Mac 目标和新回复，再在指定验收会话由用户清理上轮测试草稿、确认 Codex、录短句并停止，不发送；同时反馈实际麦克风来源。此项待反馈，不能据拔线推定纯 BLE 或 TAB5 音频传输；旧候选的纯 Wi-Fi 短录音已经通过，不重复声称其未验。

A wireless quick-console fill check was requested for the current candidate: unplug Mac USB while retaining power, verify Mac selection/fresh replies, then have the user clean the designated test draft, confirm Codex, record a short phrase and stop without sending. Actual microphone source is requested as well. Results are pending; unplugging alone proves neither BLE-only transport nor TAB5 audio upload. Earlier pure-Wi-Fi short recording already passed and is not reclassified as untested.


无线快捷回填反馈尚不足以标记通过：用户确认来源为“Mac 默认麦克风”，另反馈无线比 USB 切换慢且不稳定，出现“正在同步会话”会一直停留，需再点左侧定位会话才可录音。当前原生桥接为无 USB、Wi-Fi 已认证、BLE 数据已确认；最近一次主页面目标核对 4 ms，语音最近请求为 Wi-Fi 取消 200。此数据只说明最近一次核对完成快，不代表整个切换耗时或该无线回填完成。

只读检查归档 `.234` 的调用方：`quick_voice` 在绑定未完成或 `codex_reading/codex_syncing/codex_load_waiting` 时直接写同步提示并返回；`quick_tick` 无对应的同步完成提示复位，而重新定位会话会走 `quick_bind` 并重写提示。该路径与反馈相符，但归档不等同于当前设备源码，不能直接宣称已确定当前固件全部原因。遵守不动固件要求，先给 Mac 桥接添加本机有界请求耗时诊断：只记录定位／草稿读写／回复读取的固定标签、通道、HTTP 排队和桥接处理毫秒数、状态码；不存正文、会话或请求标识、密钥、原始数据，不进入设备状态或协议。用于实测区分桥接阻塞与设备等待／提示残留，暂不声称修复稳定性。

Wireless quick-fill is not yet passed. The user identified Mac-default-microphone capture and reported slower, less reliable wireless switching than USB, with a persistent syncing hint requiring another left locate-chat click before recording. Native state shows no USB, authenticated Wi-Fi and acknowledged BLE; the most recent foreground target check took 4 ms and the latest speech request was a successful Wi-Fi cancel. This is not an end-to-end navigation measurement or proof that filling completed.

Read-only archived .234 caller inspection found that quick_voice writes the syncing hint and returns while binding/read/draft-sync flags are pending. quick_tick has no corresponding sync-complete hint reset; locating again rebinds and replaces the hint. This matches the report but does not establish the exact current device implementation. Firmware remains untouched. A bounded Mac-local timing diagnostic is being added for fixed navigation/draft/read operation labels, transport, HTTP queue time, bridge processing time and status only. It excludes payloads, chat/request IDs, secrets and raw data, and is not exported in device snapshots/protocol. Physical measurements will distinguish bridge blocking from device waiting/stale feedback; stability is not claimed fixed.


无线同步诊断候选已完成并正常启动：`artifacts/mac-tab5-20261022/wireless-sync/AIBotBridge.app`，唯一进程，七项开关保留、31 会话可读。122 个实际测试体通过断言适配器；新增覆盖排队／处理计时分离、固定标签与数值、八项有界历史与迟到回调、未认证请求不生成诊断、真实合成加密草稿读取与重放不重复记录。完整 XCTest 仍缺模块；Release、严格签名、helper 字节一致和无凭据／离线工具自检通过，未访问刷机设备。未修改功能性请求调度或设备协议，不把诊断候选描述为修复。已请求仅一次无线切换→语音键检查：同步提示卡住时保留现场、不再定位；若正常收音则取消，不发送。启动时无线尚在恢复，未将启动预检作为无线实机通过。

The wireless-sync diagnostics candidate is now the sole normally launched process, with all seven opt-ins retained and 31 readable chats. All 122 actual assertion-adapter test bodies pass, including separate queue/processing timing, fixed-label/numeric privacy, bounded history/late completion, unauthenticated-call exclusion and synthetic authenticated encrypted draft-load/replay coverage. Full local XCTest remains unavailable. Release/signature/helper identity and credential-free/offline-tool checks pass without flash-device access. Functional scheduling and the device protocol are unchanged; this is a diagnostic candidate, not a claimed fix. One wireless switch/voice-key reproduction was requested, retaining a stuck hint without another locate click or cancelling if capture starts normally, never sending. Wireless was still recovering at startup and is not marked physically passed from preflight alone.


无线诊断复现的新结果：用户本次能切换到会话，未出现同步提示，但按录音键提示“电脑状态未刷新，请稍后重试”。Mac 原生状态证实语音尚未收到开始请求；最近八项会话请求中定位约 1.9–2.3 秒、草稿读取约 1.6–2.0 秒，HTTP 排队均为 0，处理返回 200；最近主页面确认 26 ms。归档设备调用方的该提示来自最后一次有效状态超过 6000 ms 的本地前置检查，不能把未到达 Mac 的录音错误说成识别服务失败，也不放宽设备状态期限。

受控回归已在修改前复现：阻塞后台会话刷新时，目录有效性检查和本地加密草稿操作都在同一 `gate` 后等待，测试未能在明确释放后台请求前完成。Mac 修复将已发布目录读取放在短 `stateLock` 下，本地草稿事务与远程刷新分离；发送收据、草稿读写仍共用单独 `journalGate` 保持事务串行，设备／任务／请求和授权检查、30 秒目录有效性、发送去重与不确定收据均保留。旧的草稿读写协议和状态投影不变，未修改固件、BLE 参数、HTTP 超时或任何新鲜度阈值。修复后的断言验证及候选构建进行中；不能把锁等待复现等同于无线录音／全部卡顿根因已确定。

In the new wireless take, chat switching succeeded without the syncing hint, but the recording button reported stale computer status. Native diagnostics show no speech-start request reached Mac. Recent locate operations took roughly 1.9–2.3 seconds and local draft loads 1.6–2.0 seconds, all with zero HTTP queue time and successful processing; the last primary-document check itself took 26 ms. The archived caller emits that error before contacting Mac when the last accepted state is older than 6000 ms. This is not a recognition-service failure, and the freshness guard remains unchanged.

A controlled pre-fix regression reproduced foreground catalog checks/local encrypted drafts waiting behind a deliberately blocked background refresh on the same gate. The Mac repair reads the published catalog under a short state lock and separates local draft transactions from remote refresh. A separate journal gate still serializes drafts and send receipts, preserving device/task/request/consent checks, 30-second catalog freshness, exactly-once submission and uncertain receipts. Draft wire fields/projections, firmware, BLE settings, HTTP timeouts and freshness thresholds are unchanged. Post-fix assertions and candidate packaging are pending; reproducing the lock wait alone does not establish every wireless failure's cause.


锁分离修复验证完成：修改前受控用例未能在后台请求释放前完成，修改后同一用例在后台仍阻塞时完成目录检查、两设备独立草稿保存与读取；磁盘解密结果保留两条绑定。第二项覆盖关闭授权、未知任务、无效请求、过期目录仍可恢复未发送草稿、不同设备草稿隔离。加上既有发送去重／不确定收据、历史／实时回复、语音、绑定及提示识别测试，共 124 个实际测试体通过断言适配器；完整 `swift test` 仍在 XCTest 模块导入前失败，未等同于 CI。Release 候选正在构建；实机耗时下降及无线录音未标记通过。

The lock-separation regression now passes while the background request remains deliberately blocked: catalog validation and two device-bound draft save/load transactions complete, and decrypted storage retains both bindings. The second test covers revoked consent, unknown task/invalid request, stale-catalog recovery of unsent local drafts and cross-device isolation. All 124 actual assertion-adapter bodies pass, including existing exactly-once/uncertain receipts, history/live replies, speech, host binding and placeholder tests. Full local swift test still fails to import XCTest and is not equated with CI. Release packaging is in progress; physical latency improvement and wireless recording remain unverified.


`artifacts/mac-tab5-20261022/codex-sync-priority/AIBotBridge.app` 已完成 Release／严格签名、helper 字节一致与无凭据自检、刷机工具离线自检（无设备访问）。旧诊断候选正常退出，新候选为唯一进程，七项开关保留、31 会话连接正常。未插 USB 的预检已看到 Wi-Fi 已认证和 BLE 数据确认；第一项真实 Wi-Fi 草稿读取成功，HTTP 排队 0 ms、桥接处理 74 ms，相比旧候选多项 1610–1967 ms 是初步处理耗时改善证据，单个样本不外推全部稳定性。已请求仅一次会话切换后按语音键验证；可收音则取消、失败保留现场，不发送。无线快捷语音、六秒状态过期和持续同步提示的实机修复结果仍待反馈。

The codex-sync-priority candidate passed Release/strict signing, identical-helper/credential-free checks and offline flash-tool validation without device access. The old diagnostics app exited normally; the new sole process retains seven opt-ins and 31 chats. Unplugged preflight confirms authenticated Wi-Fi and acknowledged BLE. Its first real Wi-Fi draft load succeeded with zero HTTP queue time and 74 ms bridge processing, preliminary evidence of reduced processing versus prior 1610–1967 ms samples, not broad stability proof. One switch/voice-key check is pending, cancelling on successful capture or retaining failure state without sending. Physical fixes for wireless capture, stale-state refusal and persistent syncing remain unconfirmed.


codex-sync-priority 候选本轮用户确认：USB 拔除后切换会话能直接开始录音，已取消，没有反馈持续同步或电脑状态过期。本次无线切换后录音入口实机通过，尚不外推为长期／全部通道稳定。随后桥接原生诊断显示 Wi-Fi 两次定位处理 627／445 ms、回复读取 56 ms、草稿读取 74 ms，排队均为 0、状态 200；七项开关保持开启。语音只读状态此时为用户停止后的 review、BLE 查询 200；该快照不作为本次取消请求已由后端确认的独立证据，不读取正文或代替用户反馈。接下来仅补测一次会话往返并在收音开始时立即取消，不发送、不改固件。

On codex-sync-priority, the user confirmed unplugged chat switching allows recording immediately and reported cancelling, with no persistent syncing or stale-status warning. This physical recording-entry take passes, without claiming long-term/all-channel stability. Native diagnostics subsequently show two Wi-Fi locate operations at 627/445 ms, reply read at 56 ms and draft load at 74 ms, all zero queue time/status 200, with seven opt-ins retained. The later speech snapshot is review after user stop with a successful BLE poll; it is not independent proof of the specific cancellation receipt and no transcript was read. One chat round trip with immediate capture cancellation is the next bounded check, without sending or firmware changes.


用户补充确认两次会话往返都能直接开始录音，但启动到可说话及停止到回填均感觉超过三秒；随后切换时同步提示再次出现，因此无线稳定性不能按前两次成功关闭。保留现场的原生记录显示最新草稿读取已成功、处理不足 1 ms，定位 286–356 ms，最后目标核对 4 ms；等待约十秒、不重新定位、再按一次红色语音键后，用户确认能录音并已取消。当前表现收敛为同步未就绪时的提示不自动恢复，下一次语音点击可继续，与归档快捷控制台提示路径相符；登记为设备端提示／同步衔接待复核，按用户要求不改固件，不再要求重新定位作为必需恢复步骤。

同时修正诊断的证据边界：旧的 `/codex/read`、`/codex/turn` 和语音 HTTP 入口未传入已测排队时长，默认显示零不能证明这些请求没有排队。原先已测的桥接处理耗时仍有效，但文中这些旧入口的零排队值不再作为无排队结论；修正把排队时长传至所有相关已认证路径，缺少测量时显示“未测”，USB/BLE 也不冒充 HTTP 排队零。

语音分段诊断开发中：固定标签记录开始／停止／取消／回填处理耗时，过滤高频查询，保留八项有界历史；Mac 本地记录启动、首段结果（明确含用户等待说话）、停止至最终结果、最终结果至首次响应生成，后者不表示设备已收妥。计时不改变录音、停止、取消、最终识别／预览门槛、发送、协议或 BLE 调度，不含语音正文／原始音频／标识。用于下一次短录音测量，而非声称三秒延迟已优化。

The user confirmed both chat round trips start recording directly, but perceived more than three seconds for both startup and stop-to-fill. A subsequent switch showed the syncing hint again, so earlier successful takes do not close wireless stability. Retained native diagnostics show successful local draft load below 1 ms, locate processing 286–356 ms and a 4 ms target check. After roughly ten seconds, pressing red voice again without locating allowed recording/cancellation. The current evidence matches a sync-not-ready hint that does not clear itself, recoverable with the next voice click, consistent with the archived quick-console path. Device feedback/sync handoff remains pending review under the no-firmware-change boundary; another locate click is not established as required.

Diagnostic caveat corrected: legacy Codex read/turn and voice HTTP adapters had not forwarded measured queue duration, so their default zero was not evidence of no queue. Measured processing durations remain valid; earlier legacy-path queue zeros must not support a no-queue conclusion. The repair forwards timing through all relevant authenticated adapters and displays unmeasured when absent, including USB/BLE instead of inventing HTTP queue zero.

Stage timing is being added without changing functional behavior: fixed-label start/stop/cancel/fill processing metrics with bounded history and no high-frequency poll flooding, plus Mac-local startup, first-result time explicitly including user speech wait, stop-to-final and final-to-first-response-generation timing. Response generation is not device delivery confirmation. No transcript/audio/identifier is retained, and recording/cancel/final-review/send/protocol/BLE behavior is unchanged. The next short physical take will measure latency; the three-second delay is not claimed optimized.


`artifacts/mac-tab5-20261022/voice-latency/AIBotBridge.app` 已正常替换上一候选并无线恢复：唯一进程、Wi-Fi 已认证、BLE 数据确认、七项开关开启、31 会话可读。128 个实际断言适配测试体通过，新增涵盖启动／最终结果／响应生成计时分离、取消后迟到回调隔离与新录音重置、语音边界与回填固定标签、高频查询不挤掉记录、缺失排队测量不显示零；合成认证测试另确认旧会话端点及语音端点的排队计时转发，全部使用假音频后端与合成密钥。完整 XCTest 仍缺模块；Release／严格签名／helper 字节比较及无凭据自检／刷机工具离线自检通过，无刷机设备访问。

未以计时改动声称提速。已请求保持无线、在指定验收会话由用户清理上一轮测试草稿并点 Codex 确认，短句录音停止后保持 Mac Codex 前台，等待回填完成或失败提示再反馈，避免诊断 UI 抢前台影响回填。不发送；若同步提示仍残留，等待后再按红色语音键，固件提示逻辑继续待复核。下一次分段实机数据待回报。

The voice-latency candidate normally replaced its predecessor and restored wireless operation: sole process, authenticated Wi-Fi, acknowledged BLE, seven opt-ins and 31 chats. All 128 actual assertion-adapter bodies pass, including stage-timing separation, late-callback isolation/reset, fixed voice/fill labels without poll flooding and honest unknown queue display. Synthetic authenticated tests also verify legacy Codex/voice queue forwarding using a fake speech backend and synthetic key. Full XCTest is still unavailable; Release/strict signing/identical helper/credential-free and offline-tool checks pass without flash-device access.

Instrumentation is not claimed as a speed fix. One short wireless take was requested in the designated acceptance chat after user-only test-draft cleanup and explicit Codex targeting, keeping Mac Codex foreground until filling succeeds or fails before replying so the diagnostic UI cannot steal focus mid-fill. No send; a persistent syncing hint can be retried with the red voice key after waiting, while its device-side feedback remains pending review. Physical stage timings await feedback.


voice-latency 候选短句实测：用户确认文字已回填且未发送，随后明确反馈“这次倒是很快”。原生 UI 的本次 Wi-Fi 请求：开始处理 77 ms、停止处理 48 ms、回填处理 124 ms，均 200、已测 HTTP 排队 0 ms；本地启动 77 ms，停止至最终识别 71 ms，最终结果至首次响应生成 422 ms。首段结果 1502 ms 含用户等待说话，不能用作纯识别计算耗时。该次无线快捷语音／回填的速度与功能按用户反馈通过；不外推长期无抖动，也不把设备同步提示自动恢复项关闭。保留现有 Mac 锁分离与诊断，不再无依据调整语音算法／无线调度；未读取或保存正文、未发送或修改固件。

On voice-latency, the user confirmed successful fill without sending and explicitly reported the take felt fast. Native Wi-Fi processing times were start 77 ms, stop 48 ms and fill 124 ms, all successful with measured zero HTTP queue. Mac-local startup was 77 ms, stop-to-final 71 ms and final-to-first-response-generation 422 ms. First-result time of 1502 ms includes waiting for user speech and is not pure recognition compute latency. This wireless quick-voice/fill take passes functionality and perceived speed. Long-term jitter and device syncing-hint auto-recovery are not closed. Existing lock separation/diagnostics are retained without speculative recognizer/radio tuning. No transcript storage/read, send or firmware change occurred.


继续非固件未验项，下一项选择真实“停止任务”验收。已只读核对 Mac 的精确任务／轮次中断校验与归档设备“查看回复 → 停止任务”调用。请用户仅在已指定“TAB5 操作验收”手工启动一段不调用工具、不改文件的合成文章输出，再在 TAB5 只点一次停止并反馈实际状态／提示；没有按钮或不确定时不重复请求。助手未向其他会话发送消息、未创建任务、未代替用户发送或中断。实机结果待反馈。

The next unfinished non-firmware check is a real task interruption. Mac exact-task/turn checks and the archived reply-overlay Stop Task caller were inspected read-only. The user was asked to manually start synthetic prose without tools/file edits only in the designated acceptance chat, then click Stop Task on TAB5 once and report its actual state/message, without retry on missing/uncertain controls. The assistant did not message another chat, create a task, send or interrupt on the user's behalf. Physical results are pending.


停止任务实机反馈：用户在指定验收会话确认正常发送与停止，但第一次输出已经结束，停止按钮才出现；第二次输出较长，按钮出现后能真正停止。本轮“发送／中断动作”通过，按钮及时出现未通过。桥接随后最近八项公开回复请求均成功，处理约 8–51 ms；不把按钮延迟归因为已证实的慢回复请求。只读调用链确认已有 `control` 主要在 `page` 完整读取后更新，后台亦随四秒目录刷新和回复读取更新；归档设备按 `control.running` 显示停止按钮，读回复期间还会禁用它。固件不改。

Mac 增加独立 `MacTab5LiveControl`：只针对已认证请求明确选中的已知任务，每秒通过现有严格桌面 owner 快照接口只读核对，不依赖目录／回复历史的长锁；查看页请求续期十五秒观察租约，离开后不无限轮询。仅发布现有 control 字段，不带正文或路径，三秒未确认即隐藏操作状态；每次真正发送／中断仍走原来的精确任务／轮次、授权、持久收据和不确定结果保护。任务切换、过期租约、迟到选择请求、旧观察和关闭会使旧结果无效；其他任务的后台草稿保存不切换目标。回复页面可为同一任务补充新快照，但较旧读取不能覆盖独立核对的新状态，也不移动阅读位置。诊断只附固定状态与处理毫秒数，并补记停止任务操作标签。

最终源码 135 个实际测试体通过断言适配器；新七项覆盖提前发布／过期、不同任务和旧结果、离开／停止后的迟到回调、权限与失败、过慢快照、迟到选择，以及实际 Codex 集成中阻塞公开历史仍能取得可中断状态、后台草稿不改变目标、原历史位置保留、目录过期屏蔽操作。完整 XCTest 仍缺模块。Release 候选构建中；未宣称停止按钮实机时机已修复，未由助手发送／中断任务或改写固件。

The user confirmed real send/interrupt actions work in the acceptance chat. The first response finished before Stop Task appeared; a longer second response could be stopped once the button appeared. Actions pass, timely availability does not. Recent public-reply requests processed successfully in roughly 8–51 ms, so slow reply processing is not established as the cause. Existing control projection is mainly updated after full page reads and the four-second catalog/history refresh. The archived device displays Stop from control.running and disables it while reading replies. Firmware remains unchanged.

Mac now independently reads the explicitly selected known task through the existing verified owner snapshot endpoint once per second. A fifteen-second viewing lease bounds extra probes; snapshots older than three seconds cannot advertise controls. Only existing metadata control fields are published, with no text/paths. Actual mutations retain exact task/turn checks, consent and durable/uncertain receipts. Generations, request ordering and timestamps reject late selections/observations after switches, lease expiry or shutdown; another task's background draft save cannot change selection. Reply snapshots may seed the monitor without overwriting newer state or moving reading position. Diagnostics expose fixed status/duration only and include interrupt operations.

All 135 actual assertion-adapter bodies pass. Seven new methods cover early control publication/expiry, cross-task and stale results, lease/shutdown, permissions/failure, slow snapshots, reordered selections and actual Codex integration with blocked public history, preserved history, unrelated draft writes and stale catalogs. Full local XCTest remains unavailable. Release packaging is in progress; button timing is not physically passed, no assistant-initiated send/interrupt or firmware write occurred.


live-control 候选 `artifacts/mac-tab5-20261022/live-control/AIBotBridge.app` 已通过 Release 构建、严格签名、与 voice-latency 的 helper 字节一致比较、无凭据 helper 自检和刷机工具离线自检（无设备访问），`git diff --check` 通过。亦只读核对 Windows 既有 control 的 taskId／turnId／running／waiting 字段，未修改消息格式或 Windows／固件源码。

准备通过正常菜单退出旧候选时，原生界面工具报告 Mac 已锁屏、无法自动解锁；尚未退出旧候选或启动新候选。旧 voice-latency 仍运行，不能把新包描述为已运行或按钮时机实机通过。已请用户手动解锁再继续正常切换；未索取密码、强行结束 App 或用其他界面途径绕过锁屏。拟在新候选启动后先仅观察当前运行对话的停止按钮，不点击中断当前任务，也无需再次生成长测试文章。

The live-control candidate passed Release/strict signing, identical-helper comparison against voice-latency, credential-free helper and offline flash-tool checks without device access, plus diff validation. Existing Windows taskId/turnId/running/waiting control fields were checked read-only; no wire format, Windows or firmware source was changed.

Before normal menu switching, the native UI tool reported the Mac locked and automatic unlock unavailable. The old app has not exited and the new candidate has not launched; voice-latency remains running. New runtime/button-timing acceptance is not claimed. The user was asked to unlock manually; no password request, force quit or alternate lock bypass was attempted. The planned next check is read-only visibility of Stop Task while this conversation is still active, without interrupting this task or generating another long test article.


用户确认手动解锁后，已通过正常菜单退出 voice-latency，进程检查确认退出，再正常启动 live-control；新候选为唯一桥接进程，七项功能开关保持开启，Codex 连接正常、31 个近期会话可读。启动后原生设备窗口尚显示 USB 不可用、Wi-Fi 等待、BLE 寻找设备，不能将其描述为已恢复无线或按钮时机已通过。已请用户核对当前电脑为 Mac、避免 Windows USB 接管，再仅观察当前运行对话的停止按钮，不点击中断；待设备实际恢复和用户反馈。

After the user manually unlocked the Mac, voice-latency exited through its normal menu and its process was confirmed gone before live-control launched. The new candidate is the sole bridge process, retains all seven opt-ins and reads 31 recent Codex chats. Native device state still reports unavailable USB, waiting Wi-Fi and BLE discovery, so wireless recovery and button timing are not yet passed. The user was asked to verify Mac selection without Windows USB takeover, then only observe Stop Task in the active conversation without interrupting it. Actual reconnection and physical feedback remain pending.


live-control 后续已无线恢复：原生 UI 确认 Wi-Fi 已认证、BLE 数据确认。用户第一次查看时上一轮已结束，显示“已完成”、无停止按钮是符合状态的，不能当作失败；随后保持本轮运行、请用户点“最新”只看不点击停止，用户确认“显示运行中，停止按钮已出现”。本次运行中按钮显示实机通过；结合先前指定验收会话的实际停止成功，分别记录显示与中断动作，不外推短任务全部时机或仪器级延迟。

只读耗时排查另发现长对话回复请求约 4.1–9.7 秒，而独立状态最近处理 154 ms。对桥接进程进行三秒调用栈采样（不读消息或内存内容）发现 stdio 接收线程 1568/1573 个样本停在 `MacTab5CodexRPC.swift:72` 的整缓冲 `Data.firstIndex(of:)` 换行扫描；回复读取线程等待该子进程响应。将为分块输入改为仅扫描新到字节，保留八 MiB 缓冲上限、只读方法白名单、请求／授权策略及消息格式；实机提速尚未验证。采样仅存系统临时目录，不含会话正文。

Wireless connectivity later recovered with authenticated Wi-Fi and acknowledged BLE. The user's first observation followed the previous turn's completion, so completed status without Stop was expected and is not a failure. With this turn kept active and the user selecting Latest, the user confirmed running status and a visible Stop Task button without clicking it. Runtime visibility passes this take; earlier actual interruption in the designated acceptance chat remains separate evidence, without claiming every short-turn timing or instrumented latency.

A separate read-only investigation found long-chat reply requests taking roughly 4.1–9.7 seconds while the independent status check last processed in 154 ms. Three-second bridge stack sampling, without message or memory-content reads, found 1568/1573 receiver samples in the whole-buffer Data.firstIndex newline scan at MacTab5CodexRPC.swift:72, with the reply reader waiting for that response. Incremental new-byte scanning is planned, preserving the eight-MiB buffer cap, read-only allowlist, request/consent policy and wire format. Physical speed improvement is not yet established. The stack-only sample is outside the repository in the system temporary directory.


Mac stdio 分行修复已完成：已扫描且未消费的部分不再重复搜索，完整行才送入原 JSON 处理；空行／跨块 UTF-8／同块多行及尾部半行均保留原语义，累计输入仍不得超过八 MiB。源码实际分行器的优化编译合成对比（4093 字节分块）：一 MiB 旧 451 ms、新不足一 ms；四 MiB 旧 7117 ms、新 2 ms。此数值仅是内存合成分行，不是识别、网络、完整 JSON 解析或设备端到端耗时。

145 个实际测试体通过断言适配器，新增三项覆盖逐字节中文／转义／表情及半行边界、八 MiB 精确边界／超限，以及真实假 stdio 子进程分块返回四 MiB 完整结果、复用连接和禁止变更。另将原七项 RPC 测试加入本轮适配验证，确认握手一次、拒绝变更、审批留给电脑、故障不自动重放及子进程退出。完整 `swift test` 仍因本地缺少 XCTest 失败，未冒充完整套件。Release 候选构建中，运行中的 live-control 尚未替换，设备耗时改善待验证。未改协议、Windows、固件、开关或用户草稿。

The Mac stdio framer now scans only newly arrived bytes and passes complete lines to the existing JSON handler. Empty lines, fragmented UTF-8, multiple lines and a partial suffix retain their semantics, with the same eight-MiB accumulated-input cap. An optimized synthetic comparison of the actual framer using 4093-byte chunks measured 451 ms versus under one ms at one MiB, and 7117 ms versus 2 ms at four MiB. These are in-memory framing measurements, not recognition, network, full JSON parsing or device end-to-end latency.

All 145 actual assertion-adapter test bodies pass. Three new cases cover byte-fragmented Unicode/escapes/emoji and partial suffixes, exact/overflow size boundaries, and a real fake stdio child delivering an intact four-MiB reply while retaining read-only connection reuse. Seven existing RPC tests were also added to this adapter run, checking single initialization, mutation denial, computer-handled approvals, no replay and child shutdown. Full local swift test still lacks XCTest and is not claimed passed. Release packaging is in progress; live-control still runs and physical latency improvement remains unverified. Protocol, Windows, firmware, opt-ins and user drafts are unchanged.


reply-framing 候选 `artifacts/mac-tab5-20261022/reply-framing/AIBotBridge.app` 已通过 Release／严格签名、与 live-control helper 字节一致比较、无凭据 helper 自检及刷机工具离线自检（无设备访问）、`git diff --check`。旧 live-control 已正常菜单退出，确认旧进程消失后启动新候选，唯一桥接进程、七项开关保持、31 会话可读。切换前原生诊断还观察到一项回复处理 24277 ms，作为另一个慢请求样本保留，不把单次值当作纯分行耗时。

新候选启动时设备尚待无线连接，当前未获得修复后的真实读取耗时；已请用户保持 Mac 目标，读取同一长对话的新内容并只观察运行中按钮。固件和设备偏好未改，未发起发送、中断或清空草稿。接收修复的实际提速及新候选按钮回归均待反馈。

The reply-framing candidate passed Release/strict signing, byte-identical helper comparison, credential-free helper and offline flash-tool checks without device access, plus diff validation. Live-control exited through its normal menu and was confirmed gone before the sole new bridge launched, retaining seven opt-ins and 31 chats. A further pre-switch native sample showed one reply request processing in 24277 ms; this is another slow-request observation, not a pure-framing measurement.

Wireless reconnection is still pending at startup, so there is no post-fix physical latency measurement yet. The user was asked to retain Mac selection, read fresh content in the same long chat and observe the running button without clicking it. Firmware/device preferences are unchanged, and no send, interrupt or draft clear was initiated. Physical speed and button regression results for the new candidate remain pending.


reply-framing 本轮实机结果更新：用户最初反馈“已完成”、没有新消息及停止按钮，未据此直接判定新候选失败；Mac 原生诊断随后确认已收到真实 Wi-Fi 回复读取，最近三次均 200、HTTP 排队 0 ms、桥接处理 302／313／576 ms，独立状态最近处理 303 ms。此为同期请求处理耗时，不含传输或设备渲染，也未采集任务标识作为严格单会话计时。相比之前约 4.1–9.7 秒及另一个 24.3 秒样本，桥接处理已明显改善；不将合成分行 2 ms 冒充实际完整读取耗时。

用户随后主动确认“现在任务状态是运行中，看到停止任务了，也能看到最新的消息”。据此，新候选的当前对话新内容、运行状态和停止按钮可见性按本轮实机通过。没有点击停止当前任务，实际中断动作沿用此前指定验收会话的独立成功结果；没有新的发送、录音、草稿清空或固件操作。长期无线稳定性及设备同步提示不自动恢复仍单独保留，不以此次成功关闭全部未验项。

Physical results for reply-framing are now updated. The user's initial completed/no-new-content/no-Stop observation was not assumed to prove a regression. Native diagnostics subsequently confirmed three successful Wi-Fi reply reads, with measured zero HTTP queue and bridge processing of 302/313/576 ms; the independent status observation last took 303 ms. These are concurrent request-processing measurements, excluding transfer/device rendering and without collected task identifiers for a strict single-chat benchmark. They show clear improvement over earlier roughly 4.1–9.7-second and separate 24.3-second samples; the synthetic two-ms framing result is not presented as full real-read latency.

The user then independently confirmed running status, a visible Stop Task button and visible latest messages. Fresh content, runtime state and Stop visibility pass this physical take on the new candidate. This task was not interrupted; actual interruption remains supported by the earlier separate acceptance-chat test. No new send, recording, draft clear or firmware operation occurred. Long-term wireless stability and device syncing-hint auto-recovery remain separate pending items rather than being closed by this successful take.


用户继续非固件验收，已确认设备原连接方式为“自动选择”，临时改为“仅蓝牙”、USB 拔除，Mac 目标仍能连接。起初当前对话最后一条未显示，点“最新”一度提示连接或读取失败，随后用户主动确认又能看到。此项记录为纯 BLE 读取间歇失败后恢复，不能当作稳定性通过或已修复。同期 Mac 原生诊断收到多次 BLE 读取回复，均为 200、桥接处理约 250–795 ms；排队未测，这些值不含无线传输／设备收妥，不足以排除回复分片或显示等待问题。暂不修改 BLE 调度或固件。

接下来请求一次纯 BLE TAB5 麦克风短录音：日程新增的名称编辑中说合成短句后停止，检查完整性／次数及实际声源标签，不保存事项、不中断或发送 Codex 任务。归档调用方明确使用 `start_tab5` 和独立日程 owner，现有 Mac 接收端沿用同一认证语音路径；归档不冒充当前设备逐字实现。用户如遇错误保留提示、不重录，随后读取本机数值诊断。语音尚待结果，结束后须把连接方式恢复为用户原来的“自动选择”。

The user confirmed the original device mode was Automatic and temporarily selected Bluetooth-only with USB unplugged, retaining Mac selection. The last message initially did not display; Latest briefly returned a connection/read failure, after which the user independently confirmed content became visible again. This is an intermittent BLE-read failure followed by recovery, not a stability pass or claimed fix. Concurrent native diagnostics show successful BLE reply processing around 250–795 ms, with queue unmeasured; those figures exclude radio delivery/device acceptance and do not rule out fragment or display waits. BLE scheduling and firmware remain unchanged.

One BLE-only TAB5-microphone short take was requested in an unsaved new agenda-name editor, checking completeness/single copy and actual source label after explicit stop, without saving an item or sending/interruption in Codex. The archived caller explicitly uses start_tab5 and a separate agenda owner, while the Mac endpoint retains normal authenticated voice handling; the archive is not asserted identical to current firmware. On error the user should retain the prompt without rerecording for numeric diagnostics. Voice results remain pending. The device mode must be restored to the user's original Automatic afterward.


纯 BLE 语音结果：用户反馈点击后到可录音等待明显长，点击停止后“正在结束收音”停留几十秒，最终文字回显；进一步确认合成短句完整且只有一遍。Mac 原生状态独立确认声源 TAB5、BLE 开始 200／处理 83 ms、20 块／4.0 秒音频、最终 review；结束原因为“达到一分钟录音上限”，不是已证实的正常手动停止。没有识别异常记录。启动本机处理 84 ms、停止至最终识别 29 ms、最终结果至首次响应生成 3102 ms；首段结果 27265 ms 含用户等待说话，不把它当纯识别计算或精确启动耗时。此项仅证明该短句实际经 BLE 到达并正确回显，响应与正常结束仍未通过。

用户已恢复原“自动选择”，随后明确设备已连接 Wi-Fi；未保存测试事项。Mac 蓝牙调度与协议尚不调整：现有每两秒状态帧、交替邮箱轮询和 GATT 读写共享同一传输队列，桥接处理时间不能覆盖无线等待。新增仅本机有界诊断，按状态／资源／语音邮箱／控制邮箱分别记录最近流程的阶段、整段耗时、读回字节／次数、已提交写入字节／次数。写入提交和“流程完成”均不冒充设备已解密／显示；状态帧仍沿用原签名 ACK。只接受固定枚举和数字，不接收或存储正文、音频、任务／请求／主机 ID、设备地址、原始错误或密钥，不加网络字段。诊断不改变轮询、优先级、分片、重传、期限或权限；下一步先在恢复后的日常连接观察，不重复录音。

The BLE take returned the complete synthetic phrase once, but the user reported slow preparation and tens of seconds stuck ending capture. Native metadata confirms TAB5 microphone, successful BLE start processed in 83 ms, twenty blocks/four seconds of audio and final review. The end reason was the one-minute cap, not independently verified normal manual stopping. No recognizer failure was recorded. Local startup was 84 ms, stop-to-final 29 ms and final-to-first-response generation 3102 ms; first-result time of 27265 ms includes waiting for speech and is not pure compute or precise startup latency. This proves correct delivery/recognition of this short phrase, not acceptable responsiveness/stop behavior.

The user restored Automatic and explicitly confirmed Wi-Fi reconnection without saving the test agenda. BLE scheduling/protocol are not being tuned yet: status transfers and alternating mailboxes share one GATT queue, whose radio waits are absent from existing processing metrics. A bounded local-only diagnostic is being added for each status/resource/voice/control flow, retaining fixed stages, total duration and read/submitted byte/operation counts. Submission and flow completion do not claim device decryption/rendering; status still requires the original signed ACK. Its API takes only enums/numbers and retains no payload/audio/identifiers/addresses/raw errors/secrets, exports no network fields and changes no scheduling, fragmentation, replay, deadlines or permissions. Ordinary recovered connections will be observed before another recording request.


蓝牙计时源码验证完成：149 个实际测试体通过断言适配器。新增四项覆盖包括最终确认在内的流程计时、读写次数／字节统计、reset 与迟到结束不误确认新流程、空邮箱轮询不抹掉上次真实传输、每类只保留一条的有界存储，以及异常时钟／数字界限。与本轮修改前备份逐行比较，BLE 状态机只增加计时调用和诊断 token 传递；原读写、优先级、响应确认、超时与不重发策略保持。正常 App 设置页使用已有滚动动态布局展示固定数字摘要；设备状态帧不包含这些诊断。完整 `swift test` 仍缺 XCTest，Release 候选正在构建，旧 reply-framing 仍运行，尚未据此声称链路延迟已修复。

All 149 actual assertion-adapter bodies pass. Four new methods cover whole-flow timing through final confirmation, byte/operation counts, reset/late-result isolation, idle-mailbox exclusion, one retained result per fixed kind and bounded abnormal clocks/counts. Comparison against the focused pre-change backup confirms only timing calls/token propagation were added to BLE; reads/writes, priority, acknowledgement, deadlines and no-replay semantics remain unchanged. The existing dynamically sized settings scroll view displays fixed numeric metadata, never device status frames. Full local swift test still lacks XCTest. Release packaging is in progress, reply-framing still runs and latency is not claimed fixed.


ble-timing 候选 `artifacts/mac-tab5-20261022/ble-timing/AIBotBridge.app` 的 Release／严格签名、helper 字节一致和无凭据自检、刷机工具离线自检均已通过（无设备访问）。准备正常换包前，新原生状态显示用户另做了一轮 Wi-Fi TAB5 麦克风测试，已手动停止并返回 review：19 块／约 3.6 秒，开始处理 5 ms、停止处理不足 1 ms；本地启动 6 ms、停止至最终 20 ms、最终至首次响应 415 ms，首段 2373 ms 包含等待说话。此为后来的独立 Wi-Fi 录音，不覆盖前一轮 BLE 达到上限的失败；没有读取正文，用户尚未明确反馈其速度／准确性。

为保护这轮新预览，尚未退出 reply-framing 或启动 ble-timing；已请用户核对并保留需要的文字、退出未保存测试页，再正常切换。不因先前退出测试的反馈就假定后来录音也无需保留，未自动取消、清空或存储正文。

The ble-timing candidate passed Release/strict signing, byte-identical helper comparison, credential-free helper probing and offline-tool validation without device access. Before switching, new native state showed an additional user-initiated Wi-Fi TAB5-microphone take ending in review after manual stop: nineteen blocks/approximately 3.6 seconds, start processing 5 ms and stop below one ms. Local startup was 6 ms, stop-to-final 20 ms and final-to-first-response generation 415 ms; first-result 2373 ms includes waiting for speech. This later separate Wi-Fi take does not replace the prior capped BLE failure. No transcript was read, and perceived speed/accuracy has not been explicitly reported.

To preserve the new preview, reply-framing has not exited and ble-timing has not launched. The user was asked to check/retain needed text and leave the unsaved test editor before normal switching. Earlier editor-exit feedback is not assumed to cover a later recording; no automatic cancel, clear or transcript persistence occurred.


用户确认已退出后，正常菜单退出 reply-framing 并确认进程消失，再启动 ble-timing；唯一新桥接、七项开关保留、31 会话可读，BLE 数据确认。没有请求新录音。原生计时先后取得状态帧流程 3960 ms（提交 20820 字节／43 次）和 4648 ms（21874 字节／45 次），均读取一次约 500 字节的签名整帧 ACK。此为真实状态传输流程，不是语音识别耗时；确认了共享传输队列中有约四秒的状态占用，尚未量化每次音频请求等待或证明全部延迟根因。

对照现有协议、Windows `Tab5Telemetry.PackTelemetry` 与维护者归档的设备 `device_unpack_json`，BLE 已有按能力协商的标准 zlib 完整状态封装，Mac 之前始终发送未压缩 JSON。当前仅在 Mac 补用现有能力：同时确认配对、设备／nonce／ACK 签名、telemetryVersion 2／3、同 nonce 固件证明及安全的 `0.2.x-ui >= .42`，才压缩完整状态；不压缩资源、归属证明或 RPC，整封装不省字节则保留原文。展开上限仍为 32740 字节，不以压缩绕过限制；沿用 AES-GCM、原始序号与整帧签名 ACK，不改协议字段、调度、重传或固件。新六项合成测试准备验证能力／版本／证明边界、标准 zlib 精确展开／完整输入消费、无收益回退、展开上限和真实加密分片互通。尚未构建或启动压缩候选，不能将它写为实机提速通过。

After the user confirmed leaving the editor, reply-framing exited through its normal menu and its process was confirmed gone before ble-timing launched. The sole new bridge retains seven opt-ins and 31 chats, with acknowledged BLE. No new take was requested. Native observations measured status flows of 3960 ms (20820 submitted bytes/43 writes) and 4648 ms (21874 bytes/45 writes), each including one roughly 500-byte signed whole-frame ACK. This is real status-transfer occupancy, not recognition latency or a complete per-audio latency attribution.

The existing protocol, Windows PackTelemetry and archived device unpacker already support capability-negotiated standard-zlib full states; Mac had always sent plain JSON. The Mac-only change now requires confirmed pairing, matching device/nonce/ACK signature, known telemetry 2/3, same-nonce firmware proof and safe 0.2.x-ui >= .42 before packing. Resources, owner proofs and RPC remain plain, and non-beneficial envelopes fall back without expanding the original 32740-byte limit. Existing AES-GCM, sequence and signed ACK are retained without new fields, scheduling/replay changes or firmware edits. Six synthetic methods are prepared for negotiation/proof/version boundaries, exact zlib expansion/full input consumption, raw fallback, size limits and actual encrypted fragmentation. The compressed candidate is not yet built/launched or physically passed.


压缩源码最终 155 个实际测试体通过断言适配器；新增六项已执行，包含错误／过期证明、未确认配对、旧／未知能力版本的拒绝，标准 zlib 完整输入消费与逐字节恢复，外层字段／原序号一致，小数据／无收益／资源／归属证明不压缩，展开上限及正常 AES-GCM／实际分片互通。完整 `swift test` 仍仅因缺 XCTest 模块受阻。`artifacts/mac-tab5-20261022/ble-packed/AIBotBridge.app` 已完成 Release／严格签名、helper 字节一致比较、无凭据 helper 自检及刷机工具离线自检（无设备访问）；`git diff --check` 通过。

正常切换前原生工具再次报告 Mac 锁屏；尚未退出 ble-timing，也未正常启动 ble-packed。已请用户手动解锁，无需密码或重录，不使用其他界面途径绕过。后续须先观察压缩后的状态帧真实字节／签名 ACK 耗时，再决定语音复验；没有将构建或合成压缩率当作蓝牙延迟修复实机通过。

All 155 actual assertion-adapter bodies pass, including six new methods covering failed/stale proofs, unconfirmed pairing, old/unknown capability/version rejection, standard-zlib full consumption/byte-exact expansion, envelope/sequence preservation, raw fallback, expanded-size limits and actual AES-GCM/fragment compatibility. Full local swift test remains unavailable solely because XCTest is missing. The ble-packed candidate passed Release/strict signing, identical-helper comparison, credential-free helper probing and offline-tool validation without device access. Diff validation passes.

Before normal switching, the native tool again reported the Mac locked. Ble-timing has not exited and ble-packed has not normally launched. The user was asked to unlock manually without providing a password or recording again; no alternate UI bypass was attempted. Actual compressed status bytes/signed-ACK timing must be observed before another voice take, without presenting build/synthetic evidence as physical latency acceptance.


用户手动解锁后，ble-timing 已通过正常菜单退出，确认旧进程消失后启动 ble-packed。新候选唯一运行，七项开关保持开启、31 会话可读、Wi-Fi 收到认证请求且 BLE 数据已确认。切换前另一状态帧样本为 24912 ms／23772 提交字节／49 次写入，说明原链路也存在更长波动。压缩候选启动后的两个状态样本为 4626 ms／8341 字节／18 次写入，以及 6322 ms／11054 字节／23 次写入；均收到原签名整帧 ACK。状态内容与时点不同，不能当作受控同帧压缩率或稳定延迟比较；实际提交字节减少，但传输仍需数秒，语音启动／正常停止尚不能标记修复通过。资源帧仍未压缩。用户原“自动选择”保持，待一次短时纯 BLE 语音复验并恢复原模式。未发起录音、发送、停止任务、保存测试事项或固件操作。

After manual unlock, ble-timing exited through its normal menu and its process was confirmed gone before ble-packed launched as the sole bridge. Seven opt-ins and 31 chats remain available; authenticated Wi-Fi and acknowledged BLE recovered. A further pre-switch state sample took 24912 ms with 23772 submitted bytes/49 writes, showing existing longer variability. Two compressed-candidate status samples took 4626 ms with 8341 bytes/18 writes and 6322 ms with 11054 bytes/23 writes, each with the original signed whole-frame ACK. Payloads and times differ, so this is not a controlled same-frame ratio or stable latency comparison. Submitted data is smaller but transfers still take seconds; voice startup/normal stopping remain unverified. Resource frames remain unpacked. The original Automatic mode is retained pending one bounded BLE voice retest followed by restoration. No recording, send, task interruption, agenda save or firmware action was initiated.


ble-packed 纯 BLE 短录音复验未启动：用户反馈“语音未启动，请检查连接和电脑语音设置”。本机原生窗口确认语音开关仍为开启、语音状态仍等待发起，无本轮语音处理／语音邮箱结果；同时状态帧仍得到签名确认，最近样本 2152 ms／9762 提交字节／21 次写入，控制邮箱有完成记录。仅能确认没有观察到该启动请求到达语音处理器，不能把缺失诊断当作完整无线抓包或断定请求从未上链。

只读查看维护者 .234 归档：日程的同文提示来自 voice_remote_start_tab5 返回 false；其启动前检查包括参数、任务队列、busy 与传输可用性，不能据此归因于 Mac 语音权限。该归档不是设备当前 .236 的逐字证据，故设备状态／传输就绪根因仍待确定。未据此更改调度、权限、固件或宣称压缩已修复语音；已请求用户不重录、退出未保存编辑页、恢复原 Automatic 并说明提示是立即出现还是准备后出现。恢复尚待确认。

The ble-packed BLE short-take retest failed to start: the user reported the device's generic voice-not-started/check-connection-and-computer-settings message. Native Mac state retains voice opt-in and idle voice status without a corresponding voice-handler/mailbox result, while signed state ACKs continue (latest 2152 ms, 9762 submitted bytes, 21 writes) and a control-mailbox completion is present. This means the request was not observed at the handler, not a complete radio trace proving it was never transmitted.

Read-only maintainer .234 source places that agenda message at a false return from voice_remote_start_tab5; pre-start checks include arguments, job queue, busy state and available transport. The message does not establish a Mac speech-permission failure. The archive is not asserted identical to the installed .236, so the device-state/transport-readiness cause remains unresolved. No scheduler, permission or firmware change followed, and compression is not claimed to fix voice. The user was asked to avoid another take, leave the unsaved editor, restore Automatic and clarify immediate versus post-preparation failure; restoration is pending.


用户补充：点击麦克风后立即出现“语音未启动”，没有先等待准备。结合归档调用位置与 Mac 无对应启动处理记录，更符合设备启动前检查拒绝；具体失败条件仍未确定，不将其归因为固件缺陷或 Mac 权限错误。用户本次仅回答出现时机，尚未明确确认退出未保存编辑页及恢复原“自动选择”／Mac Wi-Fi；继续请求收尾确认，不要求重录。

The user clarified that the failure appears immediately after tapping the microphone, without waiting in preparation. Together with the archived call site and absence of corresponding Mac start handling, this supports a pre-start rejection, but the specific condition remains unknown and is not labeled a firmware defect or Mac permission error. This reply only clarified timing; leaving the unsaved editor and restoring Automatic/Mac Wi-Fi remain unconfirmed. A cleanup confirmation is requested without another recording.


用户确认已退出测试并恢复原 Automatic，Mac Wi-Fi 正常，纯 BLE 复验收尾完成。BLE 启动失败／长延迟仍未通过。下一项选取此前未独立通过的 Wi-Fi TAB5 麦克风较长含停顿录音；已通过的约三十秒测试声源为 Mac 默认麦克风，不能替代设备麦克风音频经 Wi-Fi 的持续传输验收。仅使用未保存日程编辑，失败保留现场，不保存事项或操作 Codex 任务。

The user confirmed cleanup and restoration of Automatic with normal Mac Wi-Fi. BLE retest cleanup is complete; BLE startup/latency remain failed. The next pending case is a longer paused Wi-Fi take using the TAB5 microphone. The earlier approximately thirty-second pass used the Mac default microphone and does not cover sustained device-microphone audio transport over Wi-Fi. This uses only an unsaved agenda editor, retaining failures without saving an item or acting on a Codex task.


用户随后主动反馈再次可录入，但结束收音仍等待很久，原拟 Wi-Fi 长录音测试未发出、未执行。原生诊断记录最近两次开始／停止均为 BLE；最近一次本地启动 6 ms、停止处理不足一 ms、停止到最终 58 ms、最终到首次响应生成 724 ms，接收九块／1.7 秒、手动停止、无识别文字。该最后一条不能冒充用户所说有文字那轮的精确对应；不读取正文或将其算作通过。

只读归档 ble_voice.inc 显示就绪要求最近五秒访问语音邮箱，状态输入另有五秒半帧期限。Mac 整帧发送独占和逐片 read/ACK 路径存在可解释就绪过期及积压的机制，但不宣称已经证明全部现场延迟。Mac 候选补用已有有界通知窗口，并在后台帧确认边界只读探测语音就绪，近期语音短时优先、控制轮换和状态刷新上界保留。未在半帧中插入完整语音交换，未改固件。旧 ble-packed 仍运行；源码验证与打包进行中，实机待验。

The user subsequently reported that recording worked again but ending capture still took a long time. The proposed longer Wi-Fi test was not issued or performed. Native metadata shows the two latest start/stop pairs used BLE. The most recent take recorded local startup 6 ms, stop processing below one ms, final recognition 58 ms and first-response generation 724 ms, accepting nine blocks/1.7 seconds before manual stop with no recognized text. This last entry is not assumed identical to the user's successful-text take, and no transcript is inspected or pass claimed.

Archived ble_voice.inc requires a voice mailbox access within five seconds for readiness; status input independently has a five-second partial-frame deadline. Mac whole-frame occupancy and per-fragment read/ACK provide a plausible readiness-expiry/backlog mechanism, not proof of all observed delays. A Mac-only candidate adds the existing bounded voice notification window, read-only readiness probes at acknowledged background boundaries, brief voice priority with control alternation and bounded status deferral. Whole voice exchanges are not inserted into partial status frames. Firmware is unchanged; ble-packed still runs while source validation and packaging proceed, with physical acceptance pending.


语音窗口候选最终 161 个实际测试体通过断言适配器，新增六项覆盖三种 ATT 容量下真实加密请求完整恢复、授权写确认前不得处理、末尾短窗、缺片／重复／错误 ID／错序／变长／越界、四秒窗口超时、能力协商与后台探测边界。完整 `swift test` 仍因缺 XCTest 模块受阻；首次 Release 尝试期间源码变动导致编译器拒绝该次构建，冻结源码后重新构建／打包成功。适配器初次缺 Foundation import，仅修正临时夹具导入后重跑通过，未弱化测试断言。Release／严格签名、helper 字节一致、无凭据 helper 自检及刷机工具离线自检通过（无设备访问），diff 检查通过。

用户确认保留需要的内容并退出编辑页后，正常退出 ble-packed、确认进程消失，再启动 `artifacts/mac-tab5-20261022/ble-voice-window/AIBotBridge.app`。唯一新进程、七项开关保持、31 会话可读，Wi-Fi 已认证且 BLE 数据确认。被动状态样本为 1779 ms／9725 提交字节／20 次写入，读取 508 字节／两次（包含就绪探测及签名 ACK），确认背景探测没有破坏该完整状态帧。不能据单次状态样本认定语音窗口实际使用或端到端改善；已请求一次五秒纯 BLE 语音复验，不保存事项，失败保留现场，之后恢复原 Automatic。固件未改。

All 161 actual assertion-adapter bodies pass, including six new methods covering encrypted request restoration at three ATT capacities, withholding dispatch until grant acknowledgement, short final windows, missing/duplicate/wrong-ID/out-of-order/changed-total/oversized fragments, four-second expiry, capability bounds and background probe boundaries. Full swift test still lacks XCTest. An initial Release attempt rejected source modifications during compilation; the frozen-source package rebuild succeeded. The first adapter compile lacked Foundation in its temporary fixture; adding that import and rerunning passed without weakening assertions. Release/strict signing, identical helper, credential-free helper and offline flash-tool checks passed without device access, as did diff validation.

After the user confirmed preserving needed content and leaving the editor, ble-packed exited normally and its process disappeared before the sole ble-voice-window candidate launched. Seven opt-ins, 31 chats, authenticated Wi-Fi and acknowledged BLE remain. A passive status sample completed in 1779 ms with 9725 submitted bytes/20 writes and 508 read bytes/two reads, including a readiness probe and signed ACK. This verifies that this background probe did not corrupt that full frame, not actual voice-window use or end-to-end improvement. One five-second BLE take was requested without saving an item; failures should remain in place and Automatic must be restored afterward. Firmware is unchanged.


语音窗口候选本轮实机：用户反馈启动较快，但“正在结束收音”约二十秒，停止响应未通过。Mac 记录 BLE 开始／停止均 200，启动处理 89 ms、停止处理不足一 ms、最终识别 51 ms、最终到首次响应生成 3159 ms；34 块／6.6 秒音频，手动停止，11 次结果／一段有效位置。用户进一步确认最终文字完整且只有一遍，并恢复原 Automatic。该成功不关闭长等待，也不把本机五十一 ms 当端到端结束时间；当前未单独确认 Wi-Fi，但此前认证状态可读。

进一步检查发现每个完成音频请求后的快速调度先穿插普通控制查询，控制或空邮箱完成后又等待 200 ms 定时器；慢背景帧结束还可能使近期语音租期过期而排入资源帧。Mac 调度候选改为近期实际语音期间优先收音、控制约每秒探测，所有邮箱完成后在原一秒短租期内继续调度（空／重复不续租），后台帧 ACK 后先检查语音。原四秒状态触发上限、完整状态确认、认证、窗口／请求期限和不重放保持。新增四项测试覆盖短租期、状态／控制不饥饿、异常时间／连接重置，以及合成持续音频调度；合成时长不是硬件测速。旧 ble-voice-window 仍运行，候选验证中，固件未改。

Physical voice-window feedback: startup felt fast, but ending capture took about twenty seconds and does not pass. Mac recorded successful BLE start/stop, start handling 89 ms, stop below one ms, final recognition 51 ms and final-to-first-response generation 3159 ms, with 34 blocks/6.6 seconds, manual stop and eleven results/one positioned segment. The user separately confirmed complete single-copy text and restored Automatic. This does not close the long wait or turn the 51 ms local measurement into end-to-end stopping time. Wi-Fi restoration has not separately been confirmed at this point.

Review found that each completed audio request's fast continuation interleaved a control query, whose completion or an empty poll then waited for the 200 ms timer. A slow background frame could also expire the recent-voice lease and queue resources next. The Mac pacing candidate prioritizes recent real voice traffic, probes control about once per second, continues all mailbox completions within the original one-second lease (idle/duplicates never renew it), and probes voice immediately after each background ACK. The four-second status trigger bound, signed frame confirmation, authentication, window/request deadlines and no-replay remain. Four tests cover the lease, control/status progress, abnormal time/connection reset and synthetic sustained-audio scheduling without claiming hardware timing. The old voice-window candidate still runs while validation proceeds; firmware is unchanged.


调度修正版最终 165 个实际测试体通过断言适配器，完整 `swift test` 仍缺 XCTest；Release／严格签名、与前版 helper 字节一致、无凭据 helper 及刷机工具离线自检（无设备访问）通过，diff 检查通过。用户已确认本轮文字完整单次并恢复 Automatic，原生窗口确认同一轮已结束、没有新收音后，正常退出 voice-window，确认进程消失，再启动唯一 voice-pacing 候选。七项开关与 31 会话保留，原生主窗口 Wi-Fi 已认证、BLE 数据确认；初始状态样本 2027 ms／9473 提交字节／20 写／509 读字节两次，包含原签名 ACK，不代表语音已提速。

已请求相同约五秒短句的纯 BLE 停止耗时比较，仍不保存事项、不操作 Codex 任务；异常保留，随后恢复原 Automatic。固件未改；当前剩余关键项仍是停止端到端等待，不能以 165 项合成／单元测试或成功文字关闭。

All 165 actual assertion-adapter bodies pass; full swift test still lacks XCTest. Release/strict signing, byte-identical helper, credential-free helper and offline flash-tool checks passed without device access, as did diff validation. After the user's complete/single-copy text and Automatic-restoration confirmation, native state showed the same completed take and no new recording. Voice-window exited normally and its process disappeared before the sole voice-pacing candidate launched. Seven opt-ins and 31 chats remain; native Wi-Fi is authenticated and BLE acknowledged. An initial status sample completed in 2027 ms with 9473 submitted bytes/20 writes and 509 read bytes/two reads including signed ACK, not a voice speed claim.

A matching approximately five-second BLE short take was requested to compare stopping latency, without saving an item or acting on Codex tasks. Errors remain in place before restoring Automatic. Firmware is unchanged; end-to-end stopping delay remains the key pending item and is not closed by 165 synthetic/unit bodies or correct text.


调度候选的纯 BLE 复验仍未通过：用户反馈停止后仍等待十几至二十秒。原生诊断显示本轮 28 块／5.6 秒音频，手动停止，本地启动 99 ms、停止至最终识别 53 ms、最终至首次响应生成 547 ms；首段结果 10266 ms 包含等待说话。最近一次语音邮箱流程 165 ms、控制 133 ms，均不是全部音频块的累计耗时，不能据此解释或排除前面的积压。用户已确认保留需要的文字、退出未保存编辑页并恢复原“自动选择”，未要求再次录音。

本轮只增加 Mac 进程内固定枚举和有界数字诊断：一次成功启动到终态响应写入确认之间，累计音频请求次数／总耗时／最长耗时／读写处理阶段耗时、窗口授权与有效通知片数，以及状态、资源、控制、空邮箱和间隔的占用。失败启动不会覆盖上一轮；连接重置标为未确认结束，终态完成后继续普通轮询不改写本轮结果。累计只计已结束流程；进行中尚未结束的流程暂计入其余间隔。终态响应已提交不代表设备已解密或显示。诊断不包含文字、音频、设备标识或地址，不加入设备状态、不持久化；未改变调度、传输参数或固件。

The pacing candidate still fails the BLE stopping-latency retest: the user reports ten to twenty seconds. Native metadata records 28 blocks/5.6 seconds, manual stop, local startup 99 ms, stop-to-final recognition 53 ms and final-to-first-response generation 547 ms. First-result 10266 ms includes waiting for speech. The latest voice-mailbox flow took 165 ms and control 133 ms; these are not cumulative audio costs and cannot explain or exclude preceding backlog. The user confirmed preserving needed text, leaving the unsaved editor and restoring Automatic; no further take was requested.

This round only adds process-local, fixed-enum and bounded-numeric capture diagnostics: from successful start through confirmed terminal-response write, accumulate audio count/total/max/read-write-processing phase costs, window grants/validated notification fragments and voice/background/control/idle/gap occupancy. Failed starts cannot replace the previous capture; reset closes it as unconfirmed, and later ordinary polls cannot change a finished capture. Totals include completed flows only, so a currently active flow temporarily falls in the remaining interval. A submitted terminal response does not prove device decryption/display. No text, audio, device IDs or addresses are retained, persisted or exported in device state. Scheduling, transport parameters and firmware are unchanged.


累计计时候选最终 168 个实际测试体通过断言适配器（新增三项累计分段、重置／新启动隔离与数字界限测试）；完整 `swift test` 仍缺 XCTest 模块。Release／严格签名、与运行前版 helper 字节一致、无凭据 helper 自检和刷机工具离线自检均通过，离线自检没有访问设备。最终复查补上成功响应门槛后重跑适配器与 Release；打包器拒绝覆盖初包，故使用新目录 `ble-capture-trace-final`，初包未运行。正常退出 pacing、确认旧进程消失后启动唯一新候选，七项开关与 31 会话保留。暂无新录音计时或延迟实机通过证据；未更改固件、未发送任务或保存测试事项。

All 168 actual assertion-adapter bodies pass, including three new capture-phase, reset/new-start isolation and bounded-count methods; full local swift test still lacks XCTest. Release/strict signing, byte-identical helper comparison, credential-free helper self-test and offline flash-tool self-test pass without device access. After adding the successful-response gate during final review, the adapter and Release were rerun. The packager correctly rejected overwriting the first bundle, so the final build uses the fresh ble-capture-trace-final directory; the first bundle never ran. Pacing exited normally and its process disappeared before the sole new candidate launched, retaining seven opt-ins and 31 chats. There is no new physical capture-timing or latency pass. Firmware, task sending and agenda saving were not touched.


换包后原生主窗口暂显示 Wi-Fi 等待设备、蓝牙寻找已配对设备，未确认无线恢复。已请求用户只核对供电／当前电脑 Mac／Automatic 及设备连接提示，不重新配对或录音；连接恢复和累计录音样本均待实机，不能以新进程运行替代设备连通性。

After switching, the native main window still reports waiting for Wi-Fi and scanning for the paired BLE device. Wireless recovery is not yet confirmed. The user was asked only to check power, current Mac selection, Automatic and the device connection label, without pairing again or recording. Connection recovery and a cumulative capture sample remain pending; a running process is not device-connectivity evidence.


用户随后确认已连接 Mac Wi-Fi，原生窗口也显示收到认证请求、BLE 数据确认；七项开关与 31 会话保留，语音尚无新采样。被动背景状态样本为 5992 ms／10769 提交字节／23 次写入，资源样本 4239 ms／6267 字节／13 次写入，仅为背景占用，非本轮语音耗时。已安排一次带累计诊断的约五秒纯 BLE 短录音：单次停止、不保存事项，失败或超过三十秒保留现场，正常回显后退出并恢复 Automatic。此样本目的是定位积压与结果返回耗时，未声称提速。

The user then confirmed Mac Wi-Fi; native state independently confirms authenticated Wi-Fi and acknowledged BLE, with seven opt-ins and 31 chats retained and no new voice capture yet. Passive background samples were 5992 ms/10769 submitted bytes/23 writes for status and 4239 ms/6267 bytes/13 writes for resources; these are background costs, not voice timing. One approximately five-second BLE capture with cumulative diagnostics was requested: one stop, no agenda save, preserve failures or a wait exceeding thirty seconds, and leave the editor/restore Automatic after normal completion. This sample attributes backlog versus result delivery and does not imply improved speed.


累计计时版首次试录提示“语音识别需连接电脑，离线可使用键盘输入”，Mac 未观察到对应启动处理。只读维护者 .234 日程调用方确认该提示对应状态 age 不在 0..6000 ms 或 pcInput.session 不可用，发生于启动请求之前；不能断言当前 .236 的具体失败分支。用户随后自行再次录音成功，但仍超过二十秒。新诊断取得本轮 24 块／4.8 秒、手动停止：本地启动 82 ms、首段结果 14345 ms（含说话等待）、停止至最终识别 33 ms、最终到首次响应生成 658 ms。整轮跨度 32607 ms，含说话等待；音频 24 次累计 16140 ms、最长 793 ms，读／写／处理阶段 9640／6400／21 ms，窗口授权／通知均 0。语音总占用 17218 ms、状态与资源 12949 ms、控制 1697 ms、其中空邮箱 922 ms、剩余间隔 743 ms；停止 297 ms、结果查询 222 ms。阶段有整数截断；整轮跨度不是点停止至回显的精确计时。足以确认本轮传输和后台队列占用远高于本机最终识别，但不证明全部来源或通知未用的原因。控制中另有保存草稿 404 元数据，未检查正文或自动补发。

用户确认保留需要文字、退出未保存日程并恢复 Automatic。当前 Mac 候选依照 Windows 既有增量格式与维护者归档的接收／树校验独立实现 BLE 状态增量；仅已 ACK 基线、当前连接、已知能力／版本启用，严格数组／null／删除／布尔语义与界限，OTA／显示操作保留完整帧。另加入固定数字／布尔的最近窗口协商状态，未改变通知授权或调度。源码验证／打包进行中，旧 capture-trace-final 仍运行；未刷固件、未将传输优化写为实机通过。

The first capture-trace attempt showed the device's voice-requires-computer/offline-keyboard message, with no corresponding Mac start handler observed. Read-only maintainer .234 agenda code associates it with state age outside 0..6000 ms or unavailable pcInput.session, before submitting start; this does not identify the exact current .236 branch. The user then independently completed another take but still waited over twenty seconds. Captured metadata: 24 blocks/4.8 seconds, manual stop, local startup 82 ms, first result 14345 ms including speech wait, final recognition 33 ms and first response generation another 658 ms. Capture span was 32607 ms including speech wait; 24 audio exchanges totaled 16140 ms, max 793 ms, with read/write/processing phases 9640/6400/21 ms and zero window grants/notifications. Voice occupancy totaled 17218 ms, background status/resources 12949 ms, control 1697 ms, idle mailboxes within those totals 922 ms and remaining intervals 743 ms. Stop exchange took 297 ms and result poll 222 ms. Phase integers are truncated, and capture span is not exact stop-tap-to-display latency. This establishes dominant transport/background occupancy for the take, not every cause or why notifications were unused. Control diagnostics also show draft-save 404 metadata; no draft content was inspected or operation replayed.

The user confirmed preserving needed text, leaving the unsaved agenda editor and restoring Automatic. A Mac-only candidate independently implements existing BLE state deltas after reading Windows format and archived receiver/tree validation: exact ACKed per-connection baseline, known authenticated capability/version, bounded array/null/remove/boolean semantics and full delivery for OTA/display operations. Fixed numeric/boolean window-negotiation metadata was also added without changing grants or scheduling. Validation/packaging is in progress while capture-trace-final still runs. No firmware operation or physical speed pass is claimed.


状态增量初版 175 个实际测试体通过，Release／签名、helper 一致／无凭据自检和离线工具自检通过；完整 XCTest 仍缺模块。用户恢复原模式后，原生确认同一录音已结束，正常退出 capture-trace-final，再启动唯一 ble-state-delta。Wi-Fi 认证与 BLE 数据确认恢复，七项开关与 31 会话保留。后台样本 1929 ms／5687 提交字节／12 次写入，下一帧 7477 字节；内容时点不同，仅表明新候选真实状态流程已获签名 ACK，不是受控压缩率或语音通过。最近窗口协商显示额度 8、通知能力有、订阅未确认，说明未使用通知不能归于缺少能力字段；具体回调错误尚未知。继续只补固定错误域枚举／有界数字码（不留原始错误消息），不改变订阅和重试行为，不请求新录音。

The first state-delta build passed 175 actual adapter bodies, Release/signing, identical/credential-free helper checks and offline tooling; full XCTest remains unavailable. After cleanup confirmation and native verification of the same completed take, capture-trace-final exited normally and the sole ble-state-delta candidate launched. Authenticated Wi-Fi, acknowledged BLE, seven opt-ins and 31 chats recovered. A background flow took 1929 ms/5687 submitted bytes/12 writes and the next submitted 7477 bytes; differing contents/times make this signed-ACK delivery evidence, not a controlled ratio or voice pass. Negotiation metadata shows eight credits and notification capability but unconfirmed subscription, so missing capability fields do not explain non-use; the callback error is not yet known. Only fixed-domain/bounded-code callback metadata is being added, without raw error messages, subscription/retry changes or another requested recording.


订阅错误码补充仍为 175 个实际测试体通过，已覆盖固定错误域与代码上限；Release／严格签名、helper 一致／无凭据自检、离线工具自检通过，diff 检查通过。正常退出 ble-state-delta 后启动唯一 `artifacts/mac-tab5-20261022/ble-subscription-check/AIBotBridge.app`，暂等待设备自动重连；未请求新录音、重新配对或改固件。初版另一个已确认状态样本为 1458 ms／5390 字节／12 次写入，非受控同帧比较。错误码仅来自 CoreBluetooth 订阅回调的固定域分类和有界整数，不保留原始错误说明、设备标识或载荷。

The subscription-error metadata update still passes all 175 actual adapter bodies, including fixed-domain/code-bound checks, plus Release/strict signing, identical/credential-free helper, offline tooling and diff checks. Ble-state-delta exited normally before the sole ble-subscription-check candidate launched; automatic device reconnection is pending. No new recording, pairing or firmware operation was requested. Another acknowledged initial-delta sample measured 1458 ms/5390 bytes/12 writes, not a controlled same-frame comparison. Error metadata is only a fixed CoreBluetooth callback domain category and bounded integer, never raw error descriptions, identifiers or payloads.


订阅错误码版已确认七项开关与 31 会话保持，暂未收到设备连接，尚无订阅回调，不能猜测错误码或归因为设备固件。已请用户只确认设备供电／Mac 选择和原 Automatic 下的连接提示；不要求录音、重配对或刷机。蓝牙端到端响应仍未通过。

The subscription-code candidate retains all seven opt-ins and 31 chats but has not yet received the device connection or a subscription callback. No code or firmware cause is guessed. The user was asked only to check power, Mac selection and the connection label under the original Automatic mode, without recording, re-pairing or flashing. BLE end-to-end responsiveness remains unpassed.


用户确认 Mac Wi-Fi 恢复后，原生诊断首次显示通知订阅失败为 ATT/8。对应本机公开 SDK `CBATTErrorInsufficientAuthorization`，不将其当作麦克风权限失败，也不推断需要重新配对。代码核对发现 tick 在首次完整状态之前订阅；旧设备无 bleProofRequired 时，仅验证过 info，尚未发送本机密钥保护的状态供设备确认。候选仅将一次订阅延后到 lastFrame 有限值：该值只在同一连接／配对代次中，完整状态签名 ACK 序号精确匹配后设置，reset 清为负无穷，资源 ACK 不设置。现代 owner 短证明、USB 优先、所有加密与权限检查保持不变，不重放用户操作，不在授权失败后循环重试。订阅时机是可验证嫌疑，尚待观察延后订阅是否通过，未断言当前固件具体实现。

After the user confirmed Mac Wi-Fi recovery, native metadata identified the notification failure as ATT/8. The local public SDK maps it to CBATTErrorInsufficientAuthorization, not microphone permission or a requirement to re-pair. Review found tick subscribes before the first full state. On the legacy path without bleProofRequired, the bridge has verified info but has not yet sent a state protected by its pairing key for device confirmation. The candidate only delays the single subscription until lastFrame is finite: that value is set exclusively after an exact signed full-state ACK in the same connection/pairing generation, cleared on reset and not advanced by resource ACKs. Modern owner proof, USB priority, encryption/authorization checks remain unchanged; user actions are not replayed and failed subscriptions are not retried in a loop. Timing is a testable hypothesis pending observed subscription success, not a claim about the exact current firmware implementation.


顺序修正版 175 个实际测试体通过断言适配器，完整 swift test 仍缺 XCTest；Release／严格签名、helper 字节一致与无凭据自检、刷机工具离线自检通过，diff 检查通过。本轮仅修改订阅门槛并新增说明，未用微小门槛测试替代现场订阅验证。原生确认没有新收音，正常退出 subscription-check 并确认进程结束后，启动唯一 `ble-subscription-order/AIBotBridge.app`，目前等待设备自动连接；尚未看到本候选通知订阅成功。切换前旧候选曾自行显示 BLE 断开、Wi-Fi 等待设备；不得把该断开归因于尚未启动的新候选。

All 175 actual adapter bodies pass for the ordering candidate; full swift test still lacks XCTest. Release/strict signing, identical and credential-free helper checks, offline flash-tool validation and diff checks pass. Only the subscription gate/comment changed this round; a tiny gate unit test is not substituted for physical subscription validation. Native state confirmed no new recording, subscription-check exited normally and its process ended before the sole subscription-order candidate launched. Device reconnection and successful notification subscription remain pending. The old candidate had already displayed BLE disconnection and waiting for Wi-Fi before switching; that disconnection cannot be attributed to the not-yet-running new candidate.


用户再次确认 Mac Wi-Fi 后，ordering 候选原生显示电脑归属已确认、后续完整状态签名 ACK 已确认，但通知仍为“额度 8／支持／未确认／ATT/8”。这推翻了“仅延后到首个状态 ACK 即可解决”的假设；不得将顺序候选称为订阅修复通过。首帧样本 13445 ms／10685 提交字节／22 写，后续主窗口数据确认正常；无新录音。只读资源面板没有选择任何固件，查看后正常关闭，未点击升级／核验／停止提供等按钮。

用户查看设备后明确版本为 247。Downloads 与当前 artifacts 未找到 .247 源码；GitHub 默认分支 versions/TAB5 返回 0.2.234-ui，对 247 的分支／提交搜索无结果。现有迁移包给的是 .236/.237 约定，不能代替 .247 授权回调实现。请求将 Windows 构建 .247 对应源码包复制到 Mac Downloads 后只读对照，重点为 BLE 特征／CCCD 授权与已选电脑验证；不要求升级、刷写、重配对、清空 bond，或再次录音。最终原生主窗口 Wi-Fi 收到已认证请求、蓝牙数据确认，用户原 Automatic 保持，固件边界不变。

After another Mac Wi-Fi confirmation, the ordering candidate showed acknowledged computer ownership and subsequent exact full-state ACK, yet subscription remained eight credits/supported/unconfirmed/ATT 8. This falsifies the hypothesis that delaying until the first state ACK alone fixes subscription; ordering is not a physical fix pass. The initial full frame took 13445 ms/10685 submitted bytes/22 writes, and the main window later reported acknowledged data; there was no new recording. The resource panel was viewed read-only with no firmware selected and then closed normally; no upgrade/boot-check/withdrawal action was invoked.

The user inspected the device and reported version 247. Downloads/artifacts lack matching .247 source. GitHub main versions/TAB5 returns 0.2.234-ui, and branch/commit searches for 247 returned no results. Existing migration guidance covers .236/.237 and cannot stand in for the .247 authorization callback. Matching source from the Windows .247 build is requested in Mac Downloads for read-only comparison of BLE characteristic/CCCD authorization and selected-computer validation. No upgrade/flash/re-pair/bond clearing or further recording is requested. Final native state confirms authenticated Wi-Fi and acknowledged BLE; the original Automatic selection and firmware boundary remain.


## 2026-10-22 `.234` 蓝牙实现未改与 Windows 对照 / Unchanged BLE and Windows comparison

用户说明 `.247` 的蓝牙适配代码未改、应与 `.234` 相同；随后在同一 TAB5、Windows 仅蓝牙模式下实测结束收音约两秒。该时间为用户估计，不是受控相同音频的计时，但足以把排查重点放回 Mac 接收差异；不再把获取 `.247` 源码作为继续工作的前提。Mac 先前音频窗口协商为额度 8／能力有／ATT 8／通知未确认，继续逐片读取。

只读核对 `.234` 主工程没有 `store_status_cb` 或修改 CCCD 权限的调用，sdkconfig 的 `MAX_CCCDS=8`，`keyboard_init` 开启 bond 并初始化持久存储。ESP-IDF v5.4.2 的官方子模块 SHA 为 `0225775e49669cd4f070400908f73718bddce83b`。该版本 [ble_gatts.c](https://github.com/espressif/esp-nimble/blob/0225775e49669cd4f070400908f73718bddce83b/nimble/host/src/ble_gatts.c) 先改当前连接的 CCCD 标志，再保存；[ble_store.c](https://github.com/espressif/esp-nimble/blob/0225775e49669cd4f070400908f73718bddce83b/nimble/host/src/ble_store.c) 容量回调未配置时返回 `BLE_HS_ENOTSUP=8`，且原样回到 ATT。由此出现“当前已开启，但保存失败报告 ATT/8”的可能路径；尚未读取实际设备存储，不能宣称其容量已满或根因已证实。无第三方源码被复制进产品。

Mac 候选仅在标准订阅返回 ATT/8 后，通过公开 `discoverDescriptors/readValue` 只读核对单一 CCCD。诊断只保留合法整数 0–3 或未知，不保存原始值／标识／错误文本。读回开启也不会冒充订阅成功；不直接写 CCCD、不重试订阅、不发窗口授权、不清空 bond。连接重置清除待回调对象。正常认证、签名 ACK 与回退逻辑不变。

The user reports unchanged BLE adaptation since `.234`, then approximately two-second finishing on the same device using Windows in Bluetooth-only mode. This is a user estimate, not an identical-audio controlled benchmark, but directs investigation toward the Mac receive path. Matching `.247` source is no longer a prerequisite. Prior Mac negotiation advertised eight credits/notification support but returned ATT 8 and retained fragment reads.

Read-only comparison finds no application store-status callback or CCCD permission override, eight stored CCCD slots and bonding/store initialization. ESP-IDF v5.4.2 pins official NimBLE commit `0225775e49669cd4f070400908f73718bddce83b`: the live subscription flags change before persistence, whose unconfigured capacity callback can return ENOTSUP (8) directly through ATT. This establishes a possible persistence-failure path, not proof of the device's actual storage contents/capacity. No third-party implementation is copied into the product.

The Mac candidate only discovers and reads the single CCCD after standard subscription returns ATT/8, using public APIs. Metadata retains an integer 0–3 or unknown, never raw values, identifiers or error text. An enabled readback is not subscription success; no direct CCCD write, subscription retry, window grant or bond deletion occurs. Reset clears pending objects; authentication, signed ACK and fallback behavior are preserved. Build and physical results follow below.


只读 CCCD 候选的 175 个真实测试方法通过断言适配器，已补合法数值／未知值不泄露原文与新协商清除旧读回的回归；适配器生成时遗漏 Foundation 导入导致首次编译失败，修正适配层后重跑通过，产品源码不因此改变。`swift test` 仍报 XCTest 缺失，Release／严格签名、helper 字节一致与无凭据自检、刷机工具离线自检、diff 检查通过。原生确认旧候选没有进行中收音，正常退出并确认进程消失后启动唯一 `ble-cccd/AIBotBridge.app`。当前等待 TAB5 从 Windows 选回 Mac／恢复原 Automatic；尚无 CCCD 现场结果或语音提速通过。已发一次连接恢复问题，不要求重录或重新配对。

The read-only CCCD candidate passes 175 actual test methods through the assertion adapter, including safe scalar/unknown handling and clearing old readback on new negotiation. The first adapter compile omitted Foundation imports; fixing that adapter-only generation issue made the rerun pass. Full swift test still lacks XCTest. Release/strict signing, identical/credential-free helper checks, offline flash-tool checks and diff checks pass. Native state confirmed no ongoing recording; the old app exited normally and its process disappeared before the sole ble-cccd candidate launched. Awaiting selection back from Windows to Mac with the original Automatic mode; no physical CCCD result or voice-speed pass yet. One restoration question is pending, without another recording or pairing request.


## 2026-10-22 CCCD 已开启后的 Mac 重新确认 / Reconcile an already enabled CCCD

用户确认已回 Mac。只读候选原生窗口确认 Wi-Fi 认证请求与 BLE 电脑归属；首轮状态读取曾等待三十秒以上，后续完整状态已获签名 ACK（样本 12326 ms／11026 字节／23 次写入）。语音通知仍返回 ATT/8，但随后的标准描述符读回为 CCCD 1。这证实设备当前通知开关已开启，与持久化失败路径吻合；没有读取 NVS，仍不宣称存储容量确已用尽。用户未进行新录音。

Mac 修正把此次证据限定为一次有条件的标准订阅确认：仅首次 ATT/8 才读取描述符，仅精确合法 NSNumber 整数 1 且原连接仍认证／配对代次有效时再次 setNotifyValue(true)，每条连接至多一次。0、2、3、布尔、文本、未知、读取失败及其他错误不重试；二次订阅失败保留逐片路径。无论读回什么，都只有无错误且 isNotifying 的系统回调才能启用原有音频窗口。未直接写 CCCD、清空可信记录、减弱权限或重放录音操作。新增三项回归覆盖已开启且保存失败、拒绝／格式／错误域分支、第二次失败与连接重置隔离。构建与现场结果待后续追加。

The user selected Mac again. Native state confirms authenticated Wi-Fi and BLE ownership. An initial state read waited over thirty seconds; a later complete state received its signed ACK (12326 ms/11026 bytes/23 writes). Subscription returned ATT/8, followed by a standard CCCD readback of 1. This proves the live notification flag is enabled and fits the persistence-failure path; NVS was not read, so capacity exhaustion is not claimed. No new recording occurred.

The Mac change allows one narrowly conditional standard subscription reconciliation: only the first ATT/8 initiates descriptor inspection; only exact NSNumber integer 1 on the same authenticated pairing generation permits another setNotifyValue(true), once per connection. Disabled/indication/both flags, booleans/text/unknown/read failure/other errors do not retry; a second failure keeps fragment reads. Only an error-free isNotifying callback enables audio notification windows. There is no direct CCCD write, trust-record deletion, permission weakening or voice-operation replay. Three regressions cover enabled-before-persistence-failure, denial/malformed/error-domain branches, bounded retry and independent connection reset. Build/physical results follow below.


重新确认候选 178 个实际测试方法通过断言适配器；swift test 仍在执行前因 XCTest 缺失而失败。Release 37.57 秒、严格签名、helper 字节一致及不读凭据的自检、刷机工具离线自检和 diff 检查通过。正常退出只读候选、确认进程结束后启动唯一 `artifacts/mac-tab5-20261022/ble-reconcile/AIBotBridge.app`。原生确认 Wi-Fi 认证、BLE 数据、七个开关和 31 会话保留，语音显示等待发起。窗口首次显示“额度 8／通知能力有／订阅已确认／错误无 0”，即标准系统回调成功。该条不单独证明第二次调用实际执行次数，也不等于音频通知已实测或端到端延迟达标。后台状态样本 1157 ms／4586 字节／10 写，因内容不同不能作受控提速比。已请用户进行唯一一次约五秒纯蓝牙日程名称录音，不保存，反馈结束延迟和文字结果后恢复原 Automatic。

All 178 actual test methods pass through the assertion adapter; full swift test still fails before execution due to missing XCTest. Release (37.57 seconds), strict signing, identical/credential-free helper checks, offline flash-tool checks and diff checks pass. The read-only candidate exited normally and its process ended before the sole ble-reconcile app launched. Native state confirms authenticated Wi-Fi, acknowledged BLE, all seven opt-ins and 31 chats, with speech idle. Negotiation now shows eight credits, notification support, confirmed subscription and no error: a successful standard system callback. This line alone does not count actual reconciliation calls, prove audio notifications in a real take, or establish end-to-end latency. The 1157 ms/4586-byte/10-write background sample has different contents and is not a controlled speed ratio. One approximately five-second Bluetooth-only agenda-name take is requested without saving, reporting finish latency/text correctness and restoring original Automatic afterward.


## 2026-10-22 通知实测与窗口授权应答差异 / Actual notifications and grant round trips

用户复验：停止到文字回显有所改善，约十秒以内；随后确认文字完整、只有一遍，已退出未保存编辑并恢复 Automatic。不记为达到 Windows 约两秒，也不把不同长度／说话等待的样本当作精确提速比例。Mac 原生显示 22 块／4.4 秒音频，本地启动 92 ms、首段结果 7030 ms（含说话等待）、停止至最终识别 31 ms、最终结果至响应生成 1440 ms。累计跨度 14476 ms（含说话等待），音频交换 22 次／8958 ms／最长 583 ms：读 2168、写 6711、处理 39 ms；22 次窗口授权／44 片通知，确认真实音频已使用通知。语音占用 10045 ms、状态资源 3454 ms、控制 386 ms、空邮箱 751 ms、其余间隔 591 ms；停止请求 205 ms、查询 230 ms。跨度／占用都不是设备点击停止到显示的精确计时，终态提交也不是渲染证明。

对照本仓库 Windows `Tab5BleMailboxWindow` 与 `.234` 维护者 receiver，Windows 在支持 WriteWithoutResponse 时对既有 type-3 grant 使用 command，精确 id/游标通知即回传确认；Mac 原来额外等待 grant 的 ATT 应答。新 Mac 候选只对已成功订阅且特征支持 command、单次长度足够的语音 grant 去掉此应答等待，遵守 `canSendWriteWithoutResponse` 及公开 ready 回调；pending 位在提交时清除，每窗口只发送一次，reset 清除。窗口仍必须收齐精确 id/位置/总长的受限通知才能处理完整认证密文；丢包或丢 grant 四秒超时，不重发。特征不支持时保留原应答路径；语音回复最后一片仍要求 ATT 应答，后台状态确认不变。新增实际命令授权计数，不记录载荷。回归新增已提交但无通知不得完成、丢 grant／重复提交被拒绝、错误标识不推进、完整通知可完成等边界。构建／现场结果待后续追加。

The user reports improved stop-to-display latency under roughly ten seconds, then confirms complete single-copy text and restored Automatic after leaving the unsaved editor. This does not meet the approximately two-second Windows observation, and differing speech lengths/waits prevent an exact speed ratio. Native metadata: 22 blocks/4.4 seconds, local startup 92 ms, first result 7030 ms including speech wait, stop-to-final 31 ms, final-to-response generation 1440 ms. The 14476 ms capture span includes speech wait. Audio exchanges total 8958 ms over 22 operations (maximum 583 ms): read 2168/write 6711/processing 39 ms, 22 grants/44 notifications confirming real notification audio. Voice occupied 10045 ms, background 3454, control 386, idle 751 and other intervals 591; stop 205 ms and poll 230 ms. These are not exact device stop-to-render timing.

Repository Windows behavior and the maintainer `.234` receiver confirm existing type-3 grants may use WriteWithoutResponse, with exact id/cursor notifications proving delivery. Mac unnecessarily awaited a separate grant ATT reply. The new candidate uses command grants only after successful subscription, explicit characteristic support and sufficient write capacity, obeying public CoreBluetooth flow control/ready callbacks. Each pending grant submits once and reset clears it. Complete bounded id/offset/length-checked notifications and subsequent ciphertext authentication remain mandatory; lost grant/packets time out after four seconds without replay. Unsupported characteristics keep acknowledged grants; final voice-response ATT confirmation and background signed ACK stay intact. A bounded command-grant count records no payload. Regressions cover no completion from submission alone, lost/duplicate grant, wrong identity and complete notification delivery. Build/physical results follow below.


命令窗口候选 179 个实际测试方法通过；首次 Release／签名通过但未启动。最终复核补充流控等待期间拒收尚未提交 grant 的通知这一门槛，避免 pending 请求被提前分片满足；原有 ACK 路径允许 ACK 前到达通知的语义保持。补充后同 179 方法再次通过，swift test 仍缺 XCTest；正在构建独立 `ble-grant-final`，此前 `ble-reconcile` 继续正常运行，没有未记录的候选切换或新录音。

The command-grant candidate passes 179 actual test methods; its initial Release/signing succeeded but was not launched. Final review added rejection of notifications while the grant is still pending local flow control, preventing early packets from satisfying an unsent request; notifications arriving before the fallback ATT reply remain supported. All 179 methods pass again; swift test still lacks XCTest. A separate ble-grant-final package is being built while ble-reconcile remains running, with no unrecorded switch or new take.


最终命令窗口候选的 179 个实际方法再次通过；完整 XCTest 仍缺模块。Release／严格签名、helper 字节一致及无凭据自检、刷机工具离线自检、diff 检查通过。用户已确认上轮结束并恢复 Automatic，原生诊断仍为同一轮已结束数据，没有新收音；正常退出 ble-reconcile 并确认其进程消失，唯一启动 `ble-grant-final/AIBotBridge.app`。七开关、31 会话、Wi-Fi 认证与 BLE 完整状态恢复，系统订阅已确认／无错误。新候选暂无真实命令授权计数或延迟结果，已请求一次同句约五秒纯蓝牙短录音；正常后退出未保存编辑并恢复 Automatic，异常则保留现场不重录。没有固件／Windows／版本源修改、提交或发布。

All 179 actual methods pass again for the final command-grant candidate; full XCTest remains unavailable. Release/strict signing, identical/credential-free helper checks, offline flash-tool checks and diff checks pass. The user confirmed the prior take ended and Automatic was restored; native diagnostics still showed that same ended take with no new recording. Ble-reconcile exited normally and its process disappeared before the sole ble-grant-final app launched. Seven opt-ins, 31 chats, authenticated Wi-Fi, acknowledged BLE and successful notification subscription recovered. Actual command-grant counts and latency for this candidate remain untested. One same-phrase approximately five-second Bluetooth-only take is requested, followed by leaving the unsaved editor/restoring Automatic on success, or preserving an error without rerecording. No firmware/Windows/version-source edits, commit or release occurred.


## 2026-10-22 命令授权实测与连接参数线索 / Command-grant result and connection-parameter lead

用户对 `ble-grant-final` 反馈仍约十秒，文字完整、只有一遍；随后明确已退出未保存日程编辑页并恢复原“自动选择”。原生诊断：23 块／4.5 秒音频，手动停止；本地启动 91 ms、首段结果 5935 ms（含说话等待）、停止至最终识别 37 ms、最终至首次响应生成 2091 ms。累计跨度 16911 ms（含说话等待），音频 23 次／10039 ms／最长 584 ms，读／写／处理 7269／2711／21 ms；23 次窗口授权全部使用免应答命令，45 片有效通知。语音总占用 11142 ms，状态与资源 4579 ms，控制 564 ms，其中空邮箱 828 ms，其余间隔 626 ms；停止请求 283 ms，查询 235 ms。时间从写阶段移到读阶段而总量未降，不将该候选登记为提速通过。不同录音长度和说话等待不能作严格 A/B 比值；终态响应提交仍不等于设备显示确认。

只读 macOS bluetoothd 的连接参数记录，未导出设备地址、名称、句柄、标识、原始载荷或完整日志：17:30:56 至 17:46:45 间八次出现拒绝 min=6、max=6、lat=0、mul=400；归档 `.234` 的 `ble_performance.inc` 实际同样请求固定 7.5 ms（代码值为 6..6，注释中的 6..12 不符）。[Apple 工程师 2026 年说明](https://developer.apple.com/forums/thread/822187)给出的常规请求下限为 15 ms，且最终是否接受由 central 决定。Windows 桥接另有吞吐优先连接参数请求；本机公开 CoreBluetooth central 头文件没有对应设置间隔接口。这里只形成参数兼容性线索：拒绝行没有直接指明 TAB5，同期其他连接／广播数值不得混作 TAB5 实测间隔；更不能把 181.25／270 ms 的广告间隔当成连接间隔。仍缺设备实际连接参数与受控时延对照，不宣称固件是全部原因，也不承诺修改参数即两秒。未调用私有接口、清除 bond、改系统蓝牙设置、重新配对、改固件或发送 USB benchmark。

本轮不新增调参候选或要求纯 BLE 重录；保留当前已签名运行包及完整文字结果，蓝牙停止延迟保持未通过。后续仍可独立验收 Wi-Fi 下 TAB5 麦克风长录音／停顿，不能拿它替代蓝牙通过。此轮仅更新验收证据，没有修改产品代码。

The user reports about ten seconds on ble-grant-final with complete single-copy text, then explicitly confirms leaving the unsaved agenda editor and restoring original Automatic. Native metadata: 23 blocks/4.5 seconds, manual stop, local startup 91 ms, first result 5935 ms including speech wait, stop-to-final recognition 37 ms and final-to-first-response generation 2091 ms. Capture span is 16911 ms including speech wait. Audio exchanges total 10039 ms over 23 operations, maximum 584 ms; read/write/processing costs are 7269/2711/21 ms. All 23 grants use commands, with 45 validated notifications. Voice occupancy is 11142 ms, status/resources 4579 ms, control 564 ms, included idle mailboxes 828 ms and remaining intervals 626 ms; stop takes 283 ms and poll 235 ms. Time shifted from the write phase to the read phase without a lower total, so this is not a speed pass. Differing lengths/waits prevent a controlled A/B ratio, and submitted terminal response does not prove device rendering.

Read-only bluetoothd connection metadata contains eight rejections between 17:30:56 and 17:46:45 for min=max=6, lat=0, mul=400, matching archived .234 ble_performance.inc’s fixed 7.5 ms request (actual 6..6 differs from its 6..12 comment). No addresses, names, handles, identifiers, payloads or complete logs were exported. Apple’s linked 2026 engineering response describes a normal minimum request of 15 ms and central control over acceptance. Windows additionally requests throughput-optimized parameters; public local CoreBluetooth central headers expose no equivalent interval setter. This is a compatibility lead only: rejection lines do not directly name TAB5; other links and 181.25/270 ms advertising intervals must not be mistaken for its effective connection interval. Device-side actual parameters and a controlled latency comparison remain absent. Firmware is not established as the complete cause, nor is two-second performance promised. No private API, bond deletion, system Bluetooth setting, re-pairing, firmware operation or USB benchmark occurred.

No new tuning candidate or repeated BLE recording is requested this round. Keep the signed running candidate and correct-text result; BLE stopping latency remains unpassed. A separate Wi-Fi TAB5-microphone long/paused take can still be accepted independently, without substituting for BLE success. Only acceptance evidence changed in this round, not product code.


已安排独立的 Wi-Fi TAB5 麦克风长录音验收：保持 Automatic／USB 拔除，先由用户确认 Mac Wi-Fi，再在未保存日程名称录约二十秒、中途停顿约三秒，只停止一次；核对中断、两段保留／重复和结束等待，正常退出编辑，不保存事项。若当前仍为 BLE／等待连接则不启动。该项此前没有实机通过记录，不能用已通过的 Mac 默认麦克风三十秒测试替代。当前问题待用户反馈；未启动新候选或更改开关。

A separate Wi-Fi TAB5-microphone long-take check is requested: retain Automatic with USB unplugged, confirm Mac Wi-Fi first, record approximately twenty seconds with a three-second pause in an unsaved agenda name, stop once, and report interruptions, both segments/single copies and ending delay. Leave the unsaved editor after success; do not start if still on BLE or waiting for connection. This has no prior physical pass and is distinct from the already passed thirty-second Mac-default-microphone take. Awaiting user feedback; no candidate switch or preference change occurred.


## 2026-10-22 长录音名称上限与继续修复蓝牙 / Agenda limit and continued BLE repair

用户 Wi-Fi 长录音触发“语音内容超过 32 个字，原名称已保留，请缩短重录”，并要求保留前 32 字。此前安排长录音在限长名称框验收不合适，本次不能据此判定两段文字端到端完整通过。原生诊断显示最新一轮 Wi-Fi 接收 97 块／19.4 秒、保留两段、手动停止，本地启动 2 ms、首结果 2386 ms、停止至最终 50 ms、首次响应生成再用 386 ms。没有读取正文，不以分段数量代替用户文字核验。用户未明确给出设备停止到显示耗时。

只读 `.234` 的 ui_agenda_voice.h 确认拒填发生在设备端：原名称加新文字必须同时不超过 32 个编码字符及 96 UTF-8 字节，成功插在原光标处。桥接请求只含独立 agenda owner，没有原名称、光标、剩余容量；简单返回 32 字不能正确处理已有名称，且成功分支会覆盖桥接提示。用户“前 32 字”的需求保留待处理，尚未更改任何产品行为；沿用先不动固件的边界，不以静默截断共享语音响应作为通用修复。

用户明确要求继续解决蓝牙慢，恢复该项最高优先级，没有关闭／放弃。发现 Mac 增量发送先拿未压缩增量与压缩完整帧比较，遗漏 Windows TelemetryFrame 已使用的压缩增量路径；归档接收器先解包再分派 delta，已有协议直接支持。Mac 修正仅补压缩候选比较，沿用已确认基线、树与展开大小限制、认证及精确序号 ACK，所有无收益／超限情况回退；不改字段、调度、固件或 Windows。新增两项回归：增量压缩必须显式启用且 baseSequence 合法；原始增量比压缩完整帧大但压缩后更小，解压逐字节一致并可独立重建完整状态，生成压缩包不能推进基线。

181 个实际测试方法通过断言适配器；完整 swift test 仍因本机缺 XCTest 而在执行前失败。Release／签名包正在准备，当前 ble-grant-final 仍运行。已请用户先退出未保存编辑再切包，暂不要求新录音。目标是先实际缩短后台状态占用，不能据代码检查或合成测试宣称十秒问题已解决。

The Wi-Fi long take hit the device’s 32-character agenda-name rejection; the user requests retaining the first 32 characters. A length-limited name field was unsuitable for accepting an unrestricted long transcript, and text integrity has not passed. Latest native Wi-Fi metadata records 97 blocks/19.4 seconds, two retained segments and manual stop: startup 2 ms, first result 2386 ms, stop-to-final 50 ms, first response generation another 386 ms. No transcript was inspected, and segment counts are not user text confirmation; device stop-to-display timing was not reported.

Read-only archived ui_agenda_voice.h locates rejection in the device: existing name plus inserted speech must fit both 32 encoded characters and 96 UTF-8 bytes, at the original cursor. The bridge receives the agenda owner but no existing name, cursor or remaining capacity; blindly returning 32 characters does not handle existing names, and the device’s success branch overrides bridge messages. Retain the requested behavior as pending within the no-firmware-change boundary; no shared speech response is silently truncated and no product behavior has changed for this issue.

The user explicitly asks to continue fixing BLE latency; it returns to highest priority and is not closed or abandoned. Mac compares an uncompressed delta against the compressed full frame, omitting the packed-delta path already used by Windows TelemetryFrame and supported by the archived receiver’s unpack-before-delta dispatch. The Mac change adds that candidate comparison while preserving authenticated/ACKed baselines, tree/expanded-size limits and exact sequence ACK, with full fallback for oversized or non-beneficial deltas. No field, pacing, firmware or Windows change. Two regressions verify explicit opt-in/valid baseline and a raw delta larger than packed full but smaller once compressed, exact decompression/independent state reconstruction and no baseline advancement from encoding.

All 181 actual test methods pass through the assertion adapter; full swift test still fails before execution because local XCTest is absent. Release/signing is being prepared while ble-grant-final stays running. User cleanup confirmation is requested before switching; no new take yet. First verify reduced actual background occupancy rather than calling code/synthetic checks a fix for the ten-second delay.


压缩增量候选已完成 Release 与严格签名：`artifacts/mac-tab5-20261022/ble-packed-delta/AIBotBridge.app`。181 个实际方法均通过（174 条逐项 PASS 日志另加 7 个显式执行的 delta 测试调用）；helper 与当前候选字节一致，无凭据 helper 自检通过，diff 检查通过。尚未正常切换运行：已发一次退出未保存日程编辑的确认，等待用户回复；旧 ble-grant-final 仍运行。新增代码只有 Mac 的增量压缩选择及相应测试，不把构建通过记作后台占用／语音延迟实机通过。

The packed-delta candidate completed Release and strict signing at artifacts/mac-tab5-20261022/ble-packed-delta/AIBotBridge.app. All 181 actual methods ran (174 individual PASS lines plus seven explicit delta-test calls); the helper is byte-identical to the running version, its credential-free self-test passes, and diff checks pass. It has not been switched into normal operation: one cleanup confirmation is pending for the unsaved agenda editor, while ble-grant-final continues running. Only Mac encoding selection and its regressions changed; building does not establish reduced background occupancy or physical speech latency.


用户确认退出未保存日程；原生核对仍为同一轮结束的 97 块／19.4 秒 Wi-Fi 数据，没有新收音。正常退出 ble-grant-final 并确认旧进程消失，启动唯一 ble-packed-delta。首次原生 getApp 超时但进程已启动，重取窗口成功，未重复启动第二实例。Wi-Fi 认证、BLE 数据、七项开关、31 会话与标准通知订阅均恢复；未重新配对或改变设置。离线刷机工具自检通过且未访问设备。首个已确认背景样本 646 ms／3685 提交字节／8 次写入；切换前样本 1323 ms／5406 字节／12 次写入，内容时点不同，仅作为不同候选的实际观察，不作受控速度比例。新候选语音尚未实测。

The user confirmed leaving the unsaved agenda editor; native diagnostics still showed the same ended 97-block/19.4-second Wi-Fi take with no new capture. Ble-grant-final exited normally and its process disappeared before the sole ble-packed-delta candidate launched. Initial native getApp timed out after launching the process; rebinding succeeded without a second instance. Authenticated Wi-Fi, acknowledged BLE, all seven opt-ins, 31 chats and successful standard notification subscription recovered without re-pairing/settings changes. Offline flash-tool self-test passed without device access. A first acknowledged background sample measured 646 ms/3685 submitted bytes/eight writes, versus a pre-switch sample of 1323 ms/5406 bytes/twelve writes. Different content/times make these observations, not a controlled speed ratio. Speech latency on this candidate is still untested.


随后第二个已确认背景样本为 769 ms／2969 字节／7 次写入，通知仍确认且没有新收音。安排唯一一次同句约五秒纯 BLE 短录音复验，新增日程保持空名称以排除原名称和 32 字上限影响；只停止一次、不保存。用户反馈端到端等待及完整单份文字，正常后退出并恢复 Automatic，异常保留现场。尚无本候选的语音实机延迟结果。

A second acknowledged background sample measured 769 ms/2969 bytes/seven writes; subscription remains confirmed with no new capture. One same-phrase approximately five-second BLE-only retest is requested in a new agenda’s empty name field, avoiding existing-name/32-character-limit interference: stop once, never save, report end-to-end wait and complete single-copy text, then leave/restore Automatic on success or preserve an error. This candidate still has no physical speech-latency result.


## 2026-10-22 压缩增量候选实机结果 / Packed-delta physical result

用户反馈停止到文字回显约 5–10 秒，随后确认文字完整、只有一遍，已退出未保存日程并恢复 Automatic。延迟仍未通过，不与 Windows 用户估计的两秒等同。原生计时为 17 块／3.4 秒音频，本地启动 127 ms、首结果 4829 ms（含说话等待）、停止至最终识别 65 ms、首次响应生成再用 622 ms。蓝牙累计跨度 11258 ms；音频 17 次共 7251 ms、最长 524 ms，读阶段 5697／写阶段 1481／本机处理 47 ms；17 次免应答窗口授权、34 片通知。语音占用 8669 ms、状态与资源 1515 ms、控制 462 ms，其中空邮箱 1061 ms，其余间隔 612 ms（含说话等待）；停止请求 1 次／254 ms，结果查询 1 次／258 ms。所有数值为有界元数据，未读取或保存正文。

背景占用已经缩短，但每 200 ms 音频块仍平均占用约 426 ms 收发，继续排查逐次链路等待。该样本较上一轮短，不能当作受控速度比例；桥接响应生成／提交时间也不等于设备实际显示时间。系统日志中匹配固件请求参数的 7.5 ms 连接间隔请求存在拒绝记录，但尚未与此 TAB5 连接可靠关联，不据此宣称根因或修改固件。暂不安排下一次录音。

The user reports approximately 5–10 seconds from stopping to display, then confirms complete single-copy text and restoration of Automatic after leaving the unsaved agenda editor. Latency remains unpassed, distinct from the user-estimated two seconds on Windows. Native metadata records 17 blocks/3.4 seconds: local startup 127 ms, first result 4829 ms including speech wait, stop-to-final 65 ms and another 622 ms to first response generation. BLE capture span is 11258 ms; 17 audio exchanges total 7251 ms, maximum 524 ms, with reading/writing/local processing 5697/1481/47 ms, 17 command grants and 34 notifications. Voice occupancy is 8669 ms, status/resources 1515 ms, control 462 ms, including 1061 ms of empty mailbox probes; remaining gaps 612 ms include speech wait. One stop takes 254 ms and one result poll 258 ms. Only bounded metadata is retained, without transcript inspection/storage.

Background occupancy is reduced, but exchanging each 200 ms audio block still averages approximately 426 ms. Investigation continues into individual link waits. This shorter sample is not a controlled speed ratio, and bridge response generation/submission does not measure device rendering. System logs contain rejected 7.5 ms requests matching firmware parameters, but no reliable association with this TAB5 connection yet; neither a proven root cause nor firmware modification follows. No further recording is requested yet.


## 2026-10-22 原录音的系统侧收发对照与范围边界 / Existing-take system timing and scope boundary

没有重录或切换候选。只读系统事件通过 TAB5 的公开服务 UUID 关联到唯一设备，并进一步关联连接对象；关联值只在分析进程内使用，不在本文保存设备标识、地址、连接句柄、原始日志或任何语音内容。运行进程的启动时间为 18:03:33，测试窗口约 18:09:27–18:09:38；不能拿较早 17:54 的 HID 省电参数当作本次录音参数。该连接先于候选启动的最后一次更新在 18:01:53 生效：interval=24（30 ms）、latency=0；本次查询范围没有后续成功更新。18:09:27 首次通知窗口附近存在 min=max=6、lat=0、mul=400 的拒绝；拒绝行无直接设备标识，只记录与既有实现及时间相符。同期 251 字节双向数据长度事件使用另一层链路句柄，未完成精确关联，不能据此把数据长度当成此设备的已证实参数。

系统 GATT 记录可直接还原本次 17 个音频窗口：首片读取提交至窗口授权提交累计 1876 ms，单次 75–196 ms；窗口授权提交至回复提交累计 3885 ms，单次 155–303 ms，后一段包括通知等待及很短的本机处理。加上桥接记录的回复等待等，与 7251 ms 音频总交换耗时相符；未把系统提交时刻当成无线到达、设备停止或显示时刻。Windows 对端代码也使用先读首片、窗口通知、最后一片 ATT 应答的流程，不能通过省略身份验证、跳过最终确认或重放音频来规避等待。

只读归档 `ble_performance.inc` 的注释称允许 6..12，实际 `itvl_min=6,itvl_max=6`，固定请求 7.5 ms。Apple 官方[参数指南](https://developer.apple.com/library/archive/qa/qa1931/)与[工程师说明](https://developer.apple.com/forums/thread/822187)列出通常至少 15 ms 的申请约束，并说明中央可拒绝或调整请求；这些是参数兼容性的依据，不是本机全链路性能保证。已核对本地 SDK：Mac 当前使用的 CBCentralManager／CBPeripheral 没有公开连接参数 setter；`setDesiredConnectionLatency:forCentral:` 属于 CBPeripheralManager 的反向角色，不能用于这里。没有调用私有 API、调整系统蓝牙设置或清除绑定。

后续可检验的设备端工作范围仅作记录，尚未修改、构建或生成固件补丁：在原 7.5 ms 申请被拒绝时，按 Apple 允许的间隔范围、零 peripheral latency 与合法 supervision timeout 协商回退，保留原 Windows 路径；必须读取真实生效参数，再对比相同短录音的音频交换及停止回显，验证 Windows 无退化。即使参数变更获接受，也不能预先承诺两秒；若仍为 30 ms，继续依据窗口收发计时决定是否需要协议侧减少往返。用户当前“涉及固件先不动”的边界下，此参数验证待后续授权阶段，蓝牙速度未解决／未通过，不能因文字正确而关闭。本轮仅更新验收记录，沿用现有已签名候选。

No new take or app switch occurred. Read-only system events associate the public TAB5 service with one device and its connection object. Association values remain only in the analysis process; this document stores no device IDs, addresses, handles, raw logs or speech. The app started at 18:03:33; the take spans approximately 18:09:27–18:09:38. Earlier 17:54 HID power-saving parameters must not be substituted for this take. The last successful connection update before app startup, at 18:01:53, is interval 24 (30 ms), latency zero, with no later successful update in the queried range. A min=max=6, lat=0, mul=400 rejection near the first notification window matches implementation/timing but lacks direct device identification. Concurrent 251-byte bidirectional data-length events use a different-layer handle that has not been reliably joined, so that size is not recorded as a confirmed target-device parameter.

The existing GATT trace reconstructs 17 windows: initial read submission to grant submission totals 1876 ms (75–196 ms each); grant submission to reply submission totals 3885 ms (155–303 ms each), including notifications and brief local processing. Together with bridge response waits this agrees with 7251 ms total audio exchange time. Submission does not mean on-air delivery, device stop or rendering. Windows also reads the initial fragment, grants notifications and acknowledges the final reply; dropping authentication/final confirmation or replaying audio is not a valid shortcut.

The archived ble_performance.inc comment says 6..12, but actual minimum/maximum are both 6: a fixed 7.5 ms request. Apple's linked guidance describes normally at least 15 ms and central acceptance/scaling rules, establishing a compatibility issue to validate rather than a performance guarantee. Local SDK inspection finds no public connection-parameter setter on the central/peripheral objects used here; setDesiredConnectionLatency belongs to the opposite CBPeripheralManager role. No private API, system Bluetooth setting or pairing deletion is used.

A later peripheral-side investigation would add a valid Apple-compatible fallback after rejection, preserving the existing Windows path, then read effective parameters and compare equivalent short recordings on both systems. It has not been implemented, built or emitted as a firmware patch. Acceptance cannot promise two seconds even if parameters change; if 30 ms persists, measured window timing must guide any protocol-side round-trip reduction. Under the current no-firmware-change scope, this remains a pending later validation, with BLE latency unresolved/unpassed. This turn updates acceptance documentation only and retains the existing signed candidate.


## 2026-10-22 继续优先修复 Mac 桥接 / Resume Mac-first repair

用户指出同一设备 Windows 约两秒，要求继续排查桥接。已明确更正：此前连接参数拒绝只是线索，不能据此断定余下问题属于固件、必须改固件才继续，或认为 Mac 已无可改进之处。保留系统计时事实，撤回以固件修改为下一步前提。

进一步对照发现 Windows PumpVoiceAsync 在 CompletedCount 增加后直接 continue；Mac 不论新语音请求还是空／重复邮箱都额外调度等待 15 ms。修正限定于最后回复 ATT 成功后的新语音请求，立即回到原调度器，保留空／重复轮询退让、短活动租期、每秒控制与四秒状态触发上限；失败不续传、不重放、不跳过认证和最后确认。新增回归覆盖下一可用传输时隙、空邮箱防忙循环、租期过期／无效时钟／新连接，以及连续积压下控制与状态公平性。不能用删除 15 ms 就声称解释全部五至十秒。

通知诊断增加授权提交至首片累计、后续片间隔累计／最长，均为固定标签和有界数字，包含系统回调等待；多窗口、新请求与新录音隔离由测试覆盖，不采集正文、音频或标识。正常运行的 packed-delta 原生窗口仍显示上一轮 17 块／3.4 秒已结束，没有新收音，用户已确认退出编辑并恢复 Automatic。当前在构建／检查 ble-immediate 候选，尚未切换或安排新录音。

The user points to approximately two seconds on the same device under Windows and requests continued bridge investigation. The previous parameter-rejection lead does not establish firmware as the remaining cause, make firmware modification a prerequisite, or prove no Mac improvements remain. Retain the measured facts and withdraw that prerequisite.

Further comparison finds Windows PumpVoiceAsync immediately continues when CompletedCount advances, while Mac adds 15 ms after both new requests and idle/duplicate probes. The change only removes this delay after successful final ATT confirmation of a new voice request. It resumes the existing scheduler, retaining idle backoff, the short activity lease, periodic control/status fairness, authentication/final confirmation and no replay on failure. Tests cover the next available transmission slot, idle busy-loop prevention, expired/invalid/new-connection clocks and fairness under continuous backlog. Removing 15 ms does not by itself explain the entire five-to-ten-second delay.

Notification metadata now separates grant-to-first-packet wait from subsequent packet gaps, with bounded numeric aggregates including system callback delay. Tests isolate windows, requests and takes without content/audio/identifiers. The current packed-delta native window still shows the previous ended 17-block/3.4-second take; no new capture, and the user confirmed editor cleanup/Automatic restoration. The ble-immediate candidate is being built/checked, not yet switched or physically retested.


立即续传候选已完成 183 个实际测试方法的断言适配执行（176 条 PASS 另加 7 个显式 delta 调用）、Release（37.88 秒）、严格签名、helper 字节一致及不访问凭据的自检、刷机工具不访问设备的离线自检、diff 检查。完整 swift test 仍在执行前因本机缺 XCTest 失败；不把适配器冒充完整测试。原生确认旧候选仍为上一轮已结束的录音，用户已有退出编辑确认；正常退出旧进程后启动唯一 `artifacts/mac-tab5-20261022/ble-immediate/AIBotBridge.app`。首次 getApp 超时后确认进程已启动，重新绑定窗口成功，未启动第二实例。七项开关及 31 会话保留，语音等待新收音；当前 USB 未接、Wi-Fi 等待设备、BLE 寻找设备。已询问设备选中 Mac／Automatic 后的连接提示，暂不安排录音。

对照还发现 Windows `Tab5VoiceSession.ForceTab5` 对独立 agenda owner 有特例，即使请求含 source=tab5，也允许优先使用已选择的电脑／大疆麦克风；Mac 日程请求则保持 TAB5 麦克风。电脑麦克风路径不会逐块经 BLE 发送 TAB5 音频，因此“仅蓝牙”模式本身不能证明同一音频路径。已向用户询问 Windows 两秒那轮的来源提示，只要求回忆、不重测；来源尚未确认，既不否定用户观察，也不据此关闭 Mac 延迟问题。固件与 Windows 源码均未修改。

The immediate-continuation candidate passes 183 actual test methods through the assertion adapter (176 PASS rows plus seven explicit delta calls), Release (37.88 seconds), strict signing, byte-identical/credential-free helper verification, offline/no-device flash-tool self-test and diff checks. Full swift test still fails before execution because local XCTest is missing; the adapter is not full XCTest. Native state confirmed the previous ended recording, with prior user editor-cleanup confirmation. The old process exited normally before the sole ble-immediate app started. Initial getApp timed out after process launch; rebinding succeeded without another instance. All seven opt-ins and 31 chats persist, with speech idle. USB is absent, Wi-Fi awaits the device and BLE is searching. One reconnection-status question is pending, without another recording request.

A further source comparison finds an agenda-owner exception in Windows Tab5VoiceSession.ForceTab5: even source=tab5 permits preferring the selected computer/DJI microphone, whereas Mac agenda requests retain the TAB5 microphone. That local-computer path does not send TAB5 audio blocks over BLE, so Bluetooth-only connection mode alone does not prove identical audio routing. The user is asked to recall the source label in the two-second Windows take, without retesting. The source remains unknown; this neither dismisses the observation nor closes Mac latency. Firmware and Windows sources are unchanged.

用户回复 Windows 那轮未注意来源；保留“约两秒、麦克风来源未确认”的观察，不推断实际路径。用户确认 Mac 已恢复，原生窗口核对 Wi-Fi 已认证、BLE 数据已确认，语音通知额度 8、订阅已确认、无错误，当前等待新收音。安排一次同句约五秒的纯 BLE 空日程名称测试，反馈停止至显示耗时、文字完整单份及来源提示；成功后退出未保存编辑并恢复 Automatic，异常保留现场、不重录。尚无 ble-immediate 的语音实机结果。

The user did not notice the Windows microphone source; retain the reported approximately two seconds with an unconfirmed source, without inferring its actual route. After the user confirms Mac reconnection, the native window verifies authenticated Wi-Fi, acknowledged BLE data, eight voice credits, confirmed notification subscription and no error, with speech idle. One same-phrase approximately five-second BLE-only take is requested in an empty unsaved agenda name, reporting stop-to-display delay, complete single-copy text and the source label. Leave the unsaved editor and restore Automatic after success, or preserve an error without rerecording. No physical speech result for ble-immediate is available yet.

## 2026-10-22 立即续传实机结果与图片混入状态修正 / Immediate-continuation result and inline-image correction

用户反馈 ble-immediate 停止到回显仍接近十秒；随后对文字完整单份、退出未保存日程并恢复 Automatic 的收尾问题回复“ok”。延迟未通过。原生计时确认 TAB5 音频 24 块／4.8 秒，全部走 BLE；本地启动 87 ms，首结果 7926 ms（含说话等待），停止至最终识别 35 ms，最终至响应生成 700 ms。全轮跨度 17920 ms；音频交换 24 次／9776 ms／最长 898 ms，读阶段 5482、写阶段 4213、本机处理 42 ms；24 次免应答窗口授权、48 片通知。新细分为授权提交至首通知累计 1545 ms、后续片间累计 891 ms／最长 73 ms，包含系统回调等待。语音占用 11639 ms、状态与资源 5486 ms、控制 412 ms，其中空邮箱 906 ms，其余间隔 383 ms。停止请求 147 ms，结果查询 477 ms。只保存有界计时和计数，不读取识别正文；这些阶段不等于设备按钮至渲染计时。

检查发现资源退让不完整：BLE 在新语音活动期间暂停独立 resourceFrame，但周期 service.frame() 仍通过 assets.projection() 夹带 1024 原始字节图片分片，并推进图片偏移。当前设备存在未完成资源流，旧候选最近资源帧耗时 7283 ms（四片）；本轮后台累计 5486 ms 仍明显。不能断定所有后台耗时或十秒均来自图片，但代码证实这一路绕过了资源退让。

Mac 修正将 BLE 状态改为仅投影完整 assetIds，不发送可选 data.resource、也不推进资源轮转／偏移；所有最新任务、时钟、控制、心跳与原 ACK 基线保持原路径。独立 BLE tab5_resources 每次从四片降为一片，仍仅在语音空闲时发送，收到资源证明才算完成；USB／Wi-Fi 默认资源行为不变。已先对照 Windows TelemetryFrame 的独立资源发送以及只读维护者接收器，原协议已接受这些格式，无字段／固件改动。新回归验证反复 metadata-only 发布不跳片，恢复后两槽字节完整一致，服务仍发布新的任务状态及递增序号，同会话单片资源、USB／Wi-Fi 默认内联和四片默认均保留。当前构建与测试中，未切换或安排新录音。

The user reports stopping-to-display still close to ten seconds on ble-immediate, then answers “ok” to confirming complete single-copy text and cleanup/restoration of Automatic. Latency remains unpassed. Native metadata confirms 24 TAB5 audio blocks/4.8 seconds over BLE: startup 87 ms, first result 7926 ms including speech wait, local stop-to-final 35 ms and another 700 ms to response generation. Capture span is 17920 ms; 24 audio exchanges total 9776 ms, maximum 898 ms, with reading/writing/processing 5482/4213/42 ms, 24 command grants and 48 notifications. Grant-to-first-notification totals 1545 ms and later notification gaps 891 ms, maximum 73 ms, including callback waiting. Voice occupies 11639 ms, status/resources 5486 ms and control 412 ms; idle probes account for 906 ms and remaining gaps for 383 ms. Stop/poll take 147/477 ms. Only bounded metadata is retained; the transcript is not inspected and these phases are not device-button-to-render timing.

Source inspection finds incomplete resource deferral: recent voice activity pauses standalone resourceFrame, but periodic service.frame() still embeds a 1024-byte raw image chunk through assets.projection(), advancing its cursor. Resources are pending on the current device; the previous latest four-chunk resource frame took 7283 ms and this take spends 5486 ms on background work. This does not attribute every background millisecond or the entire ten seconds to images, but confirms an image path bypassing voice deferral.

The Mac correction makes BLE status project complete assetIds without optional data.resource or advancing resource rotation/offsets. Current task/clock/control data, heartbeat cadence and ACK baseline handling remain intact. Standalone BLE tab5_resources carries one chunk per idle opportunity instead of four, retaining completion proofs. USB/Wi-Fi defaults stay unchanged. Windows TelemetryFrame and the read-only maintainer receiver already support this separation; no wire fields or firmware changes. New regressions verify repeated metadata-only projection does not skip bytes, both image slots reconstruct exactly, fresh task state/increasing sequences remain, single-chunk resources share the session, and default inline/four-chunk behavior is preserved. Builds/tests are in progress; no app switch or new take yet.


图片调度候选完成 185 个实际测试方法的断言适配执行（178 条 PASS 另加 7 个显式 delta 调用）、Release（37.50 秒）、严格签名、helper 字节一致／无凭据自检、刷机工具离线／无设备自检以及 diff 检查。完整 swift test 仍因缺少 XCTest 在执行前失败。用户已确认收尾，原生复核仍为相同已结束的 24 块录音；正常退出 ble-immediate 后启动唯一 ble-resource。首次 getApp 超时后复核进程并重新绑定成功；短暂自检进程已退出。新候选 Wi-Fi 已认证、BLE 数据和通知订阅已确认，无错误，七项开关及 31 会话保留。首个观察到的状态帧为 822 ms／2845 提交字节／6 次写入，不能以不同内容的单帧比较宣称提速。待同句纯 BLE 短录音复验，尚未通过延迟验收。

The image-scheduling candidate passes 185 actual test methods through the assertion adapter (178 PASS rows plus seven explicit delta calls), Release (37.50 seconds), strict signing, identical/credential-free helper checks, offline/no-device flash-tool checks and diff validation. Full swift test still fails before execution because XCTest is missing. After user cleanup confirmation, native diagnostics still show the same ended 24-block take. Ble-immediate exits normally before the sole ble-resource app starts. Initial getApp times out after launch; process verification/rebinding succeeds and the transient self-test process exits. Authenticated Wi-Fi, acknowledged BLE data and notification subscription recover without errors, preserving seven opt-ins and 31 chats. The first observed status frame takes 822 ms/2845 submitted bytes/six writes; differing frame contents do not establish a speed improvement. A same-phrase short BLE-only retest remains pending; latency is not accepted.


## 2026-10-22 图片分离候选实机结果 / Resource-separation physical result

用户反馈仍约十秒，文字完整、只有一遍，已退出未保存编辑并恢复 Automatic；延迟未通过。原生确认 TAB5 音频 23 块／4.5 秒、BLE 查询结束。本地启动 101 ms，首结果 4165 ms（含说话等待），停止至最终识别 74 ms，最终至响应生成 1215 ms。全轮跨度 14111 ms；23 次音频交换累计 7525 ms／最长 843 ms，读 4948／写 2489／处理 50 ms；23 次免应答窗口、45 片通知，授权至首片累计 1355 ms、后续片间累计 823 ms／最长 60 ms。语音占用 10154 ms、状态与资源 3295 ms、控制 408 ms，其中空邮箱 540 ms，其余间隔 254 ms。结束请求 1078 ms、结果查询 1094 ms。最近单片资源帧 828 ms／1695 提交字节／4 次写入。后台与音频累计较上一轮下降，但长度和链路时点不同，且用户端到端等待仍约十秒，不能标记提速达标或将差异归为单项精确收益。识别本身仍只占很小一段；继续分析已有系统收发记录，不要求立即再录或更改固件。

The user reports still approximately ten seconds, complete single-copy text, and leaving the unsaved editor/restoring Automatic. Latency remains unpassed. Native metadata confirms 23 TAB5 audio blocks/4.5 seconds over BLE: startup 101 ms, first result 4165 ms including speech wait, stop-to-final recognition 74 ms, then 1215 ms to response generation. Capture span is 14111 ms; 23 audio exchanges total 7525 ms, maximum 843 ms, with read/write/processing 4948/2489/50 ms. All 23 window grants use commands, with 45 notifications; first-packet waiting totals 1355 ms and subsequent gaps 823 ms, maximum 60 ms. Voice occupies 10154 ms, status/resources 3295 ms, control 408 ms; idle probes total 540 ms and remaining gaps 254 ms. Stop takes 1078 ms and result poll 1094 ms. The latest single-chunk resource frame takes 828 ms/1695 submitted bytes/four writes. Background/audio totals are lower than the prior sample but durations/link timing differ, and the user's end-to-end wait is still ten seconds; neither target acceptance nor an isolated effect size follows. Recognition itself remains a small portion. Continue examining existing system transfer records without another immediate recording or firmware change.


## 2026-10-22 同一轮系统收发与停止回复修正 / Same-take system trace and stop-reply correction

只读系统记录通过公开语音 characteristic 及同一设备的属性关联，恢复 18:56:28–18:56:42 这一轮 23 个窗口、45 通知与 26 次带确认语音回复；关联标识仅在分析内存中使用，不保存设备标识、属性句柄、原始日志或内容。18:56:36.686 开始周期状态写入，18:56:38.582 的语音只读就绪检查后，仍须完成最后状态分片及整帧 ACK，18:56:39.195 才开始下一块音频；因此长后台帧实际位于最后音频之前。停止回复提交在 18:56:40.552，最终查询回复提交在 18:56:41.840；这些为系统提交时点，不是设备显示或空中传输时间。

本地停止至最终识别只需 74 ms，而桥接立即返回 recognizing，设备需要再次查询，最终结果至响应生成延至 1215 ms。Mac 仅对已认证 BLE 首次 stop 增加至多 200 ms 的短等待：释放语音锁让实际最终回调完成，完成／错误／取消可唤醒；慢识别继续原查询，不确认部分文字。回复前再次检查许可、录音身份和后端代次，旧请求不能返回取消后新一轮文字。USB／Wi-Fi 不增加等待，不变更音频排空、认证、最终 ATT 或固件。新增回归覆盖异步最终／空语音／错误回调、锁释放、等待上限与部分文字、重复停止、取消后替换及撤销许可；189 个实际测试方法通过断言适配器，完整 XCTest 仍缺模块，Release 构建中。此改动针对末尾多余查询，不能宣称已解释或消除全部十秒延迟。

Read-only system events associate the public voice characteristic and the same device's attributes with the 18:56:28–18:56:42 take: 23 windows, 45 notifications and 26 acknowledged voice response submissions. Association identifiers stay in analysis memory; no device IDs, attribute handles, raw logs or content are retained. A status write starts at 18:56:36.686; after the 18:56:38.582 readiness-only voice probe, the remaining status fragment and whole-frame ACK precede the next audio read at 18:56:39.195. The long status frame thus occurs immediately before the final audio block. Stop response submission is 18:56:40.552 and final poll response submission 18:56:41.840; these are system submission timestamps, not on-air or device-render times.

Local stop-to-final recognition takes only 74 ms, but an immediate recognizing response forces another device poll and delays final-to-response generation by 1215 ms. Only the first authenticated BLE stop now allows a maximum 200 ms grace period. It releases the voice lock for the actual final callback; completion/error/cancellation signal it. Slower recognition keeps normal polling and never confirms partial text. Consent, take identity and backend generation are rechecked before replying so an old request cannot expose replacement-take text. USB/Wi-Fi add no wait, with no changes to draining, authentication, final ATT or firmware. New regressions cover asynchronous final/empty/error callbacks, lock release, bounded timeout/partial retention, duplicate stop, replacement after cancellation and revoked consent. All 189 actual test methods pass through the assertion adapter; full XCTest remains unavailable and Release is building. This targets the final redundant query and does not establish elimination of the entire ten-second delay.


停止回复候选已完成 189 个实际方法的断言适配执行（182 条 PASS 加 7 个显式 delta 调用）、Release（34.26 秒）、严格签名、helper 字节一致及无凭据自检、刷机工具离线无设备自检与 diff 检查。完整 XCTest 缺模块仍在执行前失败。用户已确认文字完整单份、退出编辑并恢复 Automatic；原生复核无新录音后正常退出 ble-resource，启动唯一 ble-final-reply。初次 getApp 超时但进程已启动，重新绑定成功；七项开关、31 会话保留，语音等待新请求。当前 Wi-Fi 等待设备，BLE 寻找已配对设备，尚无新候选实机录音；先等待连接恢复，不重复发送／配对，也不变更固件。

The stop-reply candidate passes 189 actual methods through the assertion adapter (182 PASS rows plus seven explicit delta calls), Release (34.26 seconds), strict signing, byte-identical/credential-free helper checks, offline/no-device flash-tool checks and diff validation. Full XCTest still fails before execution because its module is missing. The user confirmed complete single-copy text and editor cleanup/Automatic restoration; native diagnostics show no new take. Ble-resource exits normally before the sole ble-final-reply app launches. Initial getApp times out after process launch; rebinding succeeds. Seven opt-ins and 31 chats remain, with voice idle. Wi-Fi currently awaits the device and BLE is searching; no physical take on the new candidate. Await reconnection without repeated sends/pairing or firmware changes.
