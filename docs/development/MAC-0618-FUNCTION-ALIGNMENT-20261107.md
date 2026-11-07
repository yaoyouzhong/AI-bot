# Mac 0.6.18 功能对齐与原包验收交接

日期：2026-11-07。目标是让 Mac 桥接对齐当前 Windows/TAB5 的用户行为，补齐 Mac 原生验收，再准备三端共同发布。**本文件是执行交接，不是 Mac 已通过的声明；当前先不发布。**

## 1. 接收后先做什么

1. 读取接收端当前 `AGENTS.md` 和[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)，记录分支、完整 HEAD、工作区及运行包身份，保留已有 Mac 改动。
2. 核对下面差异是否仍存在。“Mac 现场”来自 Windows 工作区中的 Swift 源码静态检查，不代表 Mac 电脑上的最新代码、已安装程序或实测结果。
3. 先补原会话发送能力、历史解析，再补来源/Harness 和会话控制状态；复用已经正确的实现，不重写整套桥接。先完成只读、离线回归与本机构建，再按实际授权完成账户/设备验证。
4. 按第 7 节回传。两端改动汇总并冻结同一源码提交后，在 macOS 构建最终原包，不得把旧包改名当成新候选。

## 2. 范围、身份与交付边界

| 项目 | 当前事实 / 要求 |
| --- | --- |
| 来源 / 接收端 | Windows 开发工作区 → macOS 原生开发与验收 |
| 本地分支 | `codex/windows-dynamic-microphone-20261105` |
| 检查时 HEAD | `9894a66750c601df687c3a6898c05aa1de309c04`，**只是基线，不包含当前全部未提交功能** |
| 最终共同源码 SHA | 待两端改动汇总后冻结，不能填写上面的 HEAD 冒充完成候选 |
| 目标版本 | Windows / Apple Silicon Mac `0.6.18`；TAB5 `0.2.318-ui`；`desktopPlatform=all` |
| 用户选择 | Windows、Mac、TAB5 一起准备，等待 Mac 原包验收；ESP8266 和图库不在本次功能修改范围 |
| 发布协调 | 本 Windows 会话汇总材料；最终由唯一发布负责人操作。Mac 回传代码、原包和证据，避免两端独立创建版本 |
| 当前交付 | 本文与参考实现通过 GitHub 开发分支 `codex/windows-dynamic-microphone-20261105` 交接；接收端需取得包含本文的提交。此前 ZIP 保留为生成时的选定文件快照，不能替代完整分支 |
| 授权边界 | 本地实现、回归和发布准备；不自动授权推送、标签、公开发布、刷机或新增模型请求。已有相同对象和范围的授权继续有效，不重复确认 |

优先从上述开发分支取得本文及实现，先比较接收端改动，避免覆盖 Mac 工作；这次开发交接提交仍不是双平台最终发行候选。参考 ZIP 是生成时选定文件的**工作区快照**，不是完整可构建源码、Git bundle 或可发布候选。若使用 ZIP，先解压到独立参考目录并按 `REFERENCE-MANIFEST.json` 校验。不能用 ZIP 的基线 HEAD 证明其中当时未提交文件的提交身份。

## 3. 目标行为与已确认缺口

状态按统一模板填写“已完成／已有无需修改／不适用／待验证”。下表状态均为 **Mac 待验证**，不是对已有代码全部判失败；静态发现另外明确列出。P0 为当前功能承诺的发布阻断项，P1 必须给出结果或明确剩余限制。

| ID / 优先级 | 目标行为与 Windows 参考 | Mac 现场与实现入口 | Mac 状态 / 验收重点 |
| --- | --- | --- | --- |
| M01 / P0 原会话收发 | 操作正在使用的 Claude 原会话；普通 VS Code 扩展和原生终端分别识别，不另启 `--resume`。`ClaudeNativeSend.cs`、`IClaudeNativeSession.cs`、`ClaudeNativeCode*.cs`、`ClaudeNativeTerminal.cs` | `MacClaudeNativeClient.swift` 已识别进程、VS Code 面板、终端；但没有覆盖默认 `hasSessionAPI=false`。`MacClaudeNativeOperation.run`、`MacClaudeNativeBackend.call` 都要求它为 true，因此当前真实客户端发送会被拒绝，测试假客户端设 true 不能证明生产可用 | 待验证。按真实机制拆分 API/输入/追加/停止能力；**不能简单把 hasSessionAPI 改 true 或删防护**。逐承载端验证原会话收到、设备能读回复、身份变化拒绝 |
| M02 / P0 来源与扩展 | 列表、输入标题、回复标题区分 VS Code / 原生终端；第三方 Harness 有显式适配接口。`ClaudeSessionSource.cs`、`ClaudeHarnessAdapters.cs`、`ClaudeHarnessSelfTest.cs` | `MacClaudeNativeSession` 无来源描述符；`MacClaudeWorkspaceBackend.thread/list`、`MacTab5Codex.refresh` 未透传 source，未发现对应注册表 | 待验证。Swift 实现共同契约；来源缓存与实时能力分离，多端认领只读；无需接入真实第三方产品 |
| M03 / P0 发送按钮 | 已就绪会话无需先打开回复、额外等两秒才可发送。`ClaudeWorkspace.cs`、`ClaudeWorkspaceNative.cs` | `MacTab5Codex.snapshot` 使用单个 control，未提供按会话索引的 controls；源数据到传输裁剪都需处理 | 待验证。未读的两条会话同时有能力；读 A 不清 B；过期、断连、禁用、冲突、身份错误撤销，不能用常亮按钮代替派发检查 |
| M04 / P0 回复与切换 | 完成回复稳定显示正文与“已完成”，后台静默刷新，不跳连接/缓存警告和“稍后更新”；Codex↔Claude 不因后台草稿追踪反复阻塞 | `MacTab5Codex.swift` 已有投影；对照 `Tab5CodexReadState.cs`、`ClaudeWorkspaceNative.cs` 核对周期刷新、replyView/receipts/responses 一致性 | 待验证。两会话反复切换、查看/返回，延迟与失败下保留缓存；首次加载、真实断连、运行/中断仍正确，不统一伪装完成 |
| M05 / P0 历史与空白页 | 普通 VS Code 空白页可识别，重试后主链可读；`ClaudeHistory.cs`、`ClaudeHistorySelfTest.cs` | `MacClaudeHistory.swift` 已有 verifiedNewPanel、interruption notice；上下文节点只接受 attachment、turn_duration、away_summary，**没有 system/api_error** | 待验证。补同会话、非分支、合法 UUID/父节点的 api_error 祖先连接，不公开载荷或制造用户回执；缺损历史仍受限。覆盖重试→完成、真实中断、异会话/伪造节点、空白页 |
| M06 / P0 送达与草稿 | 未知不重发、不记成功，不清后来文字/照片；停止只针对已确认轮次。`ClaudeNativeReceipt*.cs`、`Tab5CodexJournal.cs` | `MacClaudeNative.swift`、`MacClaudeNativeBackend.swift`、`MacTab5Codex.swift` 已有摘要、写前记录、回执、取消恢复，新增输入能力须完整复用 | 待验证。假服务覆盖超时、部分提交、重复请求、重启、轮次变化、停止确认和新草稿；真实支持路径另验 |
| M07 / P1 项目与入口 | 项目按路径身份绑定，同名不同路径不混并，同路径不重复；保留会话名，不靠改名猜来源；短暂失败不让 Claude 页签消失 | `MacClaudeCatalog.swift` 已有 standardizedFileURL、失败保留；`MacTab5Codex.swift` 已有八条分页。核对 `MacClaudeSessionIndex.swift` 标题和客户端可用性投影 | 待验证。两会话、同名目录、重启、失败恢复、超过八条、空白面板。按 macOS 路径实际语义处理，不照搬 Windows 全部转小写 |
| M08 / P1 权限与焦点 | 审批留在原客户端，不恢复桥接代管审批常驻入口；读取不抢前台，发送只作用精确目标 | `MacTab5FeaturesWindow.swift` 已移除审批入口；原生端使用 AX、AppleScript/TTY、VS Code SQLite，核对权限和真实客户端版本 | 待验证。缺权限不能记送达；准确说明普通 VS Code 是否需焦点，不宣称未经证明的后台 API。只读和发送分别记录窗口变化 |
| M09 / P1 语音与通道 | 保留自动音源、录音停止回填、文字/照片隔离、配对与展示设置 | 复用 Mac 已有独立证据，检查本次实际影响；不移植 Windows 音频端点 API | 待验证。USB/Wi-Fi/BLE 记录实际通道；未测写未测。Mac 长闲置首录若无新证据，继续保留未验证 |
| M10 / P0 最终原包 | 同一冻结 SHA，各系统构建，验收哪个包发布哪个包 | 旧 `0.6.17-development-79670da`、旧 `778e433…` 准备物都不能作为新 Mac 最终包 | 待验证。Release、最终 ZIP/DMG 哈希、安装/升级启动、签名/公证实际状态、设置保留与真机证据绑定候选 |

`MacClaudeNativeClient.find/state` 当前只接受 `2.1.314`、`2.1.318` 的交互记录；普通终端限 Terminal/iTerm2，IDE 限 VS Code。这是源码识别边界，**不是 Mac 实测支持矩阵**。先核对实际版本和元数据再调整，不无条件接受未知版本、伪造 API 或凭标题/目录认领会话。

## 4. 协议与平台实现约束

权威定义见 [PROTOCOL.md](../PROTOCOL.md) 的 Optional session source 和 `claudeTasks.controls`，扩展设计见 [CLAUDE-HARNESS-ADAPTERS.md](CLAUDE-HARNESS-ADAPTERS.md)。不另造字段或客户端页签。

### 来源标签与 Harness

```json
{"source":{"id":"claude.vscode","label":"VS Code","kind":"ide"}}
```

- 放在 `claudeTasks.tasks[]` 内。内置 ID：`claude.vscode`、`claude.terminal`、`claude.background`；未知 `unknown`，多端确实同时认领 `multiple`，后两者用 unknown 样式。
- id 长 1–64，限 ASCII 字母/数字/点/下划线/连字符；label 长 1–48 **UTF-16 code units** 且最多 80 UTF-8 bytes，禁止控制/格式/行分隔/段落分隔字符。Swift 不用 `String.count` 代替这两个长度。kind 仅 `ide/terminal/harness/unknown`。
- 来源是承载端，不是模型、项目或标题。已验证来源可跨断连/重启保留；缓存只用于显示，绝不能路由、授权发送或证明存活。
- Windows 缓存上限 512 项、读取上限 256 KiB。Mac 复用 `MacTab5PrivateStore` 与现有 Keychain 加密方式，保持有界和损坏降级，不移植 DPAPI，不新增不必要的存储迁移。
- 元数据随目录裁剪保留。旧端缺字段显示未知；现有 TAB5 `fc6e8d3b…` 已支持，不需为 Mac 补字段再刷固件。
- Swift 适配器提供来源描述、发现、活跃身份集合、按精确 session 解析操作端；返回来源必须和注册来源一致。重复 ID 拒绝，多端同时确认拥有同一会话时禁止先到先发。输入/追加/停止默认关闭，只有已实现且可核验的能力才能打开。
- 当前接口面向 **Claude 兼容历史和身份**。不同历史/身份另写映射；不做任意插件下载执行、通用设置页，也不把合成适配器称为第三方实测。用可注入只读适配器经过真实 workspace 验证注册、来源不符、冲突、断连与身份变化。

### 每会话 controls

`claudeTasks.controls` 以当前目录的不透明 task alias 为键，值沿用 `taskId/turnId/running/waiting/canSend/canSteer/canInterrupt`，不可用或观察超过 30 秒为 null，不是最后打开回复会话的副本。

设备只用六秒内已认证帧的选中会话精确项。映射存在但缺项/null/身份错时禁用；仅整个映射不存在时走旧控制路径。Mac 派发前仍复核授权、存活身份、输入能力、草稿和预期轮次。退出、禁用、目录裁剪、宿主更换须撤销相应能力。

### 状态与焦点

replyView、回执、正文与 turn 必须一致。短暂失败不清成功缓存，不把历史 completed 改 interrupted；实际停止须有证据。后台草稿保存/追踪应允许安全切换视图，同时保护原草稿和未知结果；不能靠删除它们解除堵塞。

Windows console 输入、Win32/UIA、启动身份、DPAPI 都是平台实现，不直接翻译成 Swift。普通 VS Code 扩展与显式提供协议的 Harness 不是同一接口。发送如需焦点，准确说明并按当前授权操作；读取轮询保持只读，不误激活其他终端。此前用户报告 VS Code 发送时原生终端浮到前面，后续 Windows 焦点跟踪未复现，**不能写成已定位修复，更不能据此宣称 Mac 通过**。

## 5. 可复用证据与边界

见[来源验收](TAB5-SESSION-SOURCE-20261107.md)、[静默刷新/控制](TAB5-SILENT-CONTROLS-20261107.md)、[回复状态](TAB5-REPLY-STATE-20261107.md)、[原会话接入](CLAUDE-CODE-NATIVE-ALIGNMENT-20261105.md)。历史记录保留原包身份，最新候选以下列为准。

| 当前本地验收物 | SHA-256 / 身份 | 已有证据及边界 |
| --- | --- | --- |
| Windows 日常桥接 DLL | `4b6bac4f0df20a52e3650ea3ad7e23ae1017a1d578bd2a4b148ca3f7a69f8327` | Release、history、Harness 10、workspace 66、send 35、receipt 23、普通用户 status-once；这是功能候选，非双平台发行原包 |
| TAB5 BIN `0.2.318-ui` | `fc6e8d3b680398175db8adf63e1827b3927b0c480b7c659c6e1eededa8e77fb7`，6,933,920 bytes | 已本地 OTA、精确启动与实体界面验收；固定入口 `artifacts/firmware/tab5/latest/aibot_tab5.bin` |
| TAB5 ELF identity | `d8e7531e4b41bfb28ab7752728deb4ea9c65ba6e1a9d70d1a80b3dd4802f8c4d` | 启动 ota_1/VALID，核对实际运行镜像 |
| TAB5 sidecar | `e42c3772a5fe6cf7a0dbc3dece8a49086655129d6af63c6326231982f641fb01` | 与上述 BIN 配套，不可混用旧 .318 sidecar |
| Mac 最终 ZIP/DMG | **待构建、待 Mac 验收** | Windows 不能替代 macOS 原生验证 |

最新实体视觉验收十段 51 帧、五次会话/客户端切换、两条已有回复打开。来源正确，采集画面中回复稳定、发送可用；帧间隔 2,016–6,079ms，不能证明毫秒时延或排除帧间闪烁。七份设置/配对/草稿摘要、两条原会话与设备展示设置保留；标签验收没有新增模型发送、追加或停止。

此前文字/照片等验证按各自记录复用；夹具、独立旧候选、最新截图的结果不能互换，没有给全部 Harness 或所有操作/通道组合做通过承诺。Mac 逐项记录实际证据。

## 6. Mac 执行与验收顺序

### A. 无模型请求的工作先完成

1. 核对保护现有工作，保留失败记录。回归覆盖 M01–M08，重点为生产能力默认关闭、两会话 controls、来源冲突、api_error 主链、空白面板、完成缓存、未知送达和新草稿。
2. 在 macOS 运行 `cd mac-app && swift test && swift build -c release`；记录 OS/架构、Swift/客户端版本、命令与退出码。Windows 不能代跑并证明这些结果。
3. 只读发现两条明确命名的 VS Code、Terminal/iTerm2 测试会话；核验身份/来源/能力与关闭后撤销。空白页识别不等于发送通过，各宿主支持范围单列。
4. 配对 TAB5 只读列表/回复/状态验收，保留现有已批准镜像、草稿、配对、亮度、音量、页面、轮播设置；无设备时完成独立部分并标注真机待验证。
5. 反复 Codex↔Claude、两条 Claude 会话切换，打开/返回已完成回复，以时间线核对状态与按钮；不从远程截图耗时推算产品时延。

### B. 真实模型验证

核对 Mac 当次已有授权；**Windows 已用完额度不是 Mac 新额度**。没有覆盖时准备最小测试组和预计输入条数再请求一次，等待时继续 A 和打包准备。不自动重试未知结果。

至少区分电脑发送→TAB5 看到对应回复、TAB5 发送→同一电脑原会话收到且能查看回复；VS Code 和原生终端分别记录。对外宣称支持的照片、运行中追加、停止各有证据。用合成色卡、专用可中断只读任务，不动业务会话或私人照片。假服务覆盖异常分支，不替代真实支持路径。

### C. 最终原包

两端汇总后由发布负责人给出共同完整 SHA。在干净且包含该 SHA 的 macOS 工作区使用现有 `scripts/build_macos_app.sh`、`scripts/package_macos.sh`，先读参数和依赖，再生成最终包。冻结前可做开发构建，必须明确标为开发验收，不 seal 为共同候选。

登记包内版本/构建身份、字节数、SHA-256，附 LICENSE、THIRD_PARTY_NOTICES 和校验文件。从最终原包完成安装/升级、实际启动，核对桥接、配对、草稿、设置、TAB5 主链路及签名/公证真实状态。原包未变、功能未变的证据按影响范围复用，不因文档修改重复模型测试或刷机；包重建/替换须登记新身份并补受影响核验。

## 7. Mac 回传格式

生成 `docs/development/MAC-0618-ALIGNMENT-RESULT-<日期>.md`，附相应本地附件或已授权交付位置：

1. 分支、完整 SHA、工作区；哪些已有、哪些新改，未提交/未推送明确标记。
2. 回填 M01–M10 的状态、文件、命令/实际操作、结果、证据、限制及是否阻断发布。
3. OS/Swift/Claude/VS Code/终端版本、实际支持范围、输入机制、焦点需求、权限；不记录凭据、原始私人会话或个人路径到公开材料。
4. Mac 原包文件名、大小、SHA-256、版本、候选 ID/共同 SHA、构建方式、签名/公证、安装/升级启动结果；未冻结填写“开发包，最终候选待定”。
5. 真机固件 BIN/ELF、设备与通道脱敏身份、实际操作结果、设置/配对/草稿摘要、实际模型请求条数和回执；未测、失败、未知分别列明。
6. 阻碍及发布负责人要处理的最小事项。不自行建标签、上传或公开发布；已通过原包不为交接重新构建。

## 可直接转发的执行提示

> 请从 GitHub 分支 `codex/windows-dynamic-microphone-20261105` 取得 `docs/development/MAC-0618-FUNCTION-ALIGNMENT-20261107.md` 及参考实现，按它对齐 AI-bot Mac 0.6.18。先检查仓库和已有改动，只补差异，不覆盖现有代码；此前 ZIP 仅作生成时的快照参考。重点完成普通 VS Code/原生 Claude 原会话收发、来源标签与编译期 Harness 接口、每会话 controls、稳定回复和切换、api_error 历史及送达未知保护。先完成不消耗模型请求的实现、Swift 回归和只读核验，再按已有授权完成真实路径；没有真实测试授权时准备最小请求组，不继承 Windows 已耗尽额度。Windows 的输入/进程/加密只作行为参考。最终两桌面包从同一冻结 SHA 构建，冻结前开发包不能冒充最终候选。按第 7 节回传代码差异、Mac 原包哈希和实际验收证据。当前只做发布准备，Mac 后续推送按其已有具体授权执行，不自行打标签、公开发布或刷机，不记录凭据。

## English scope note

Align macOS behavior with the current Windows/TAB5 candidate, preserving native implementations and existing work. This is a static inspection of Mac sources in the Windows checkout, not Mac runtime acceptance. The production client inherits hasSessionAPI=false; implement real capabilities instead of bypassing the guard. Add source metadata, a compiled Claude-compatible Harness seam and per-session controls; verify history, stable replies, delivery recovery and settings preservation. Final desktop packages share one frozen source commit and require native acceptance. The reference ZIP is an uncommitted selected-file snapshot, not a release source archive. Publication and final Mac package acceptance remain pending.
