# Mac 对齐 Windows 开发交接（2026-11-01）

接手前读取[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)，后续交接沿用[模板](CROSS-PLATFORM-HANDOFF-TEMPLATE.md)。本轮完成后由一个发布负责人汇总双平台同一候选 SHA 的构建物；先验收最终草稿附件，再公开原包。

## 本次交接目的与基线

Windows 已完成本轮开发与主要真实操作验收，本提交将 Windows 实现、回归测试、TAB5 增量补丁及交接资料交给 Mac 端逐项核对。不要整段移植 Windows UIA、TSF、WinForms、音频设备 API；按 Mac 当前实现补齐同等用户行为，已有能力不重复实现。

核对时 origin/main 为 `f4a509d`（PR #20，Mac USB OTA）；Windows 开发基线为 `0b39860`（PR #19），PR #15 的合并提交 `572a7ee` 已在历史内。主干整合还需保留本地 main 的既有提交与未提交资料。最终合并 SHA 以 Git 历史为准。

用户目标是 Mac 补齐后争取今晚发布新版本；这不是忽略验收的承诺。尚未推送、打标签或发布。源码元数据 0.6.14 是候选版本，`release-manifest.json` 当前仍为 Windows 候选范围；Mac 就绪后必须按实际待发附件重新核对发布范围。旧 setup/ZIP/源码快照不可直接发布。

## 必须保持的产品约定

- 录音开始时绑定设备、会话、编辑框；题卡还绑定题目。切题/切会话后拒绝串题，保留识别预览与原草稿。
- 结束录音自动填入，不存在另一个“回填”按钮。Codex 直达蓝色箭头提交；任务页用发送/追加。
- 同一目标内允许用户在电脑校对，再用 TAB5 提交最新文字；提交前再次确认目标与冻结后的文字。目标换了、文字又变化了或已有附件/引用时不能误发。
- 消息被 Codex 可靠接收或持久入队即成功，不等待开始执行。结果未知只做只读核对，不能再次点击发送，也不能换 API 补发。
- 连续录音追加保留原文换行、标点和两段内容；成功填入后光标在句尾。
- 配对、用户草稿、麦克风恢复、重复提交保护、常用任务、设备展示配置继续保留。
- 性能优化必须先测量，并保留发送成功率与绑定保护。连接中断恢复不是本次已知 Bug，不新增专项断链验收。

## Mac 对齐清单

状态是基于当前源码的交接判断；Mac 执行人需逐项补充实际位置、测试结果及剩余问题。Windows 行为通过不代表 Mac 已通过。

| ID / 优先级 | 状态与要求 | Windows 证据 | Mac 核对入口与验收 |
| --- | --- | --- | --- |
| M01 / P0 | 待核对补齐：电脑校对后提交最新文字，仍严格绑定设备/会话/题目/输入框 | `Tab5CodexComposer.cs` 的 SubmissionSnapshot；`Tab5CodexComposerTarget.cs`；`Tab5QuickConsole.cs` 仅直达允许同目标编辑 | `MacCodexDirectValidation.swift`、`MacCodexQuestionValidation.swift`、`MacTab5Codex.swift` 及桌面提交调用方；测试同目标编辑成功、跨题/跨会话不发送、二次变化被拒绝 |
| M02 / P0 | 已有队列实现，需对齐核对并完成 Mac R35 真实排队；不要重写已有队列解析 | `Tab5CodexDeliveryReceipt.cs`、`Tab5QueueTransportSelfTest.cs`、`Tab5CodexNativeAppend.cs`、`Tab5CodexJournal.cs` | `MacCodexQueueReceipt.swift` 已有 server/local 队列证据与 alias 去重；`MacCodexSubmissionReceipt.swift`、`MacTab5Codex.swift` 核对完整动作前基线、旧消息排除、可靠入队即成功、未知不重发；Mac R35 必须 Mac 真机 |
| M03 / P0 | 待核对补齐：明确未发送与未知送达分开，按普通会话/题卡提示原因 | `Tab5QuickConsole.cs`、`Tab5CodexComposer.cs`、`docs/PROTOCOL.md`；类型化 submitted=false / attempted=false | Mac 在确实未动作时返回明确未发送；conversation_target_or_text_changed / question_target_or_text_changed 与固件补丁兼容；未知结果继续保守禁止重复 |
| M04 / P0 | 录音绑定、预览保留、多题下一步与提交、电脑校对后题卡提交：已有相关实现，按本轮行为逐项回归 | `Tab5VoiceSession.cs`、`Tab5WindowsFollowupSelfTest.cs`（134项）、`Tab5VoiceSelfTest.cs` | `MacTab5Voice.swift`、`MacCodexQuestionValidation.swift`；开始录音后切题，第二题必须保持空，第一题识别保留预览；不得因 UI 文案相近而认为验证完成 |
| M05 / P0 | 确认需补齐 GLM：新财务总览端点；保留旧端点和缓存 | `ZhipuBalance.cs`、`MigratedDomestic/DomesticQuotaService.cs`、`ZhipuSelfTest.cs` | 最新 `MacDomesticBrowser.swift` 的 Swift URL 过滤和注入 JS 仍仅接受旧 query-customer-account-report，两处均需补 finance-overview；具体字段及金额规则见下文 |
| M06 / P1 | 待同步 MiMo 主额度解析和 MiniMax 重置/扩容归一化；真实账户待验证 | `XiaomiQuota.cs`、`MiniMaxQuota.cs`、`DomesticQuotaService.cs`、`DomesticQuotaCycleSelfTest.cs` | `MacDomesticBrowser.swift`、`MacDomesticQuota.swift`；只移植接口契约和测试用例意图；无有效账户不能声明真实刷新通过 |
| M07 / P1 | 先测量再判断：录音准备、填入、提交确认等待 | `Tab5UiWorkTiming.cs`、`Tab5VoiceTiming.cs`、`Tab5StatusPublisher.cs`、`Tab5VoiceFocusPreparation.cs`、`Tab5AudioEndpointCache.cs` | Mac 原链路已经较快，不照搬 Windows UI 调度/焦点准备。测量主线程等待、设备读取、识别结束、填入、队列确认；只保留有收益且不放宽检查的优化 |
| M08 / P1 | Mac 平台核对：语音准备不干扰用户其他应用或默认输入法；未安装/未运行/服务异常有准确提示 | `Tab5DoubaoAvailability.cs`、`Tab5InputMethod.cs`（仅 Windows TSF）、`Tab5VoiceHost.cs` | `MacDoubaoSpeech.swift`、`MacDoubaoBackground.swift`、`MacDoubaoMicrophone.swift`；TSF 不适用 Mac。Windows 真实微软拼音→豆包已通过；Mac 必须验证自己的平台行为 |
| M09 / P1 | UI 对齐：语音默认可用，移除多余启用/排查入口，只保留一个直接语音设置入口；打开窗口不等待音频枚举 | `Tab5VoiceHost.cs`、`Tab5ConnectionForm.cs` | `MacBridgeSettingsLayout.swift`、`MacSettingsControls.swift` 及语音设置入口；单一入口、选择麦克风、当前状态；后台加载、保存前实时检查、失败不覆盖、取消不保存 |
| M10 / P1 | UI 对齐：取消能关闭非模态窗口，未保存修改需确认；保存/取消的视觉次序与主次样式在同平台一致 | `ComputerControlForm.cs`、`UnsavedChanges.cs`、`MigratedWeather/WeatherSettingsForm.cs` | Mac 按原生平台习惯统一，不必复制 Windows 左保存右取消；检查全部设置入口、关闭按钮、取消、保存失败和编辑保留 |
| M11 / P1 | UI 对齐：常用任务长标题可读、无横向滚动、已选数、保存/取消、列表外 pin 不丢失 | `Tab5TaskPins.cs`、`DeviceCenterStyleSelfTest.cs` 的 CheckTaskPinEditing | `MacTaskPins.swift`；可采用原生列表/表格，保留32项上限、隐藏任务已有选择；无需逐项让用户点击验证常规存储逻辑 |
| M12 / P1 | 连接页切页不跳高；升级命令和固件 offer 必须来自同一快照/包 | `Tab5ConnectionForm.cs`、`Tab5Service.cs`、`Tab5RemoteOta.cs` | `MacTab5ConnectionData.swift` 与 OTA 发布/状态组装；保留 PR #20 USB negotiated binary mailbox；不倒退其已完成优化 |
| M13 / P2 | 已有无需盲改：DeepSeek、Kimi等已核对契约；缺账户项记录待验证 | `docs/development/DOMESTIC-QUOTA-INTERFACE-AUDIT-20261101.md` | DeepSeek 未发现需更新的余额端点；Kimi、阿里等缺真实订阅验证，不因为 Windows 缓存存在就声称 Mac 账户可用 |

## 国产模型接口要点（不含账户资料）

1. GLM：新增官方 HTTPS 默认端口精确路径 `/api/biz/account/finance-overview`，读 `data.availableTotalBalance`。保留 `/api/biz/account/query-customer-account-report`。按捕获端点选解析器，不混用字段。新版两位小数 half-up，旧版向零截断。新版不提供累计消费则保持未知，不能复用旧 USED。非成功/缺字段/非法金额拒绝并保留最近成功缓存。Windows 有真实后台刷新成功证据，Mac 无。
2. MiMo：支持 `data.usage.items`，只接受唯一非 `compensation_total_token` 主额度行；补偿不混入主额度，0–1 比例转百分比并交叉核验已有 used/limit。多主额度/非法比例/矛盾数据保守拒绝；周期取所选行，保留旧格式。
3. MiniMax：`end_time/weekly_end_time` 是绝对毫秒时间戳；仅缺失才回退旧倒计时。`used_percent/total_percent` 归一化，例如120/200=60%。排除 video/hailuo 独立预算，两条解析入口使用相同规则；非法总量与时间不覆盖缓存。
4. DeepSeek 当前官方 `/user/balance` 无需迁移。Kimi、阿里、阶跃、千帆等真实未配置账户仍待验证；只读契约核对不算真实账户成功。

## TAB5 固件补丁与来源

完整 TAB5 工程不在本仓库根 firmware（那里是 ESP8266）。本次共享增量在 `docs/proposals/`，顺序为：

1. `tab5-direct-submit-feedback`：未发送/未知送达分类；
2. `tab5-button-press-feedback`：清空、回复、翻页等按压反馈；
3. `tab5-submit-context-feedback`：普通会话/题卡上下文提示；
4. `tab5-remote-ota-offer-race`：新 offer 到达之前等待，不提前消费错误安装意图。

每组有 patch 和 JSON 来源/哈希。JSON 中的“not installed”是生成时历史状态，不是最终部署结论；本机后来完成安装及核验，详见 WINDOWS-IDLE-SUBMIT-FIX-20261101.md。最终实机镜像仍登记 `.302-ui`，镜像 SHA-256 `475e0bf93790a6125555f7c863f2b25aa1fc959d7734681f32441849ad17f84e`；版本字符串相同不证明二进制相同。实际镜像仅在本地 artifacts/firmware/tab5/latest，不提交未知来源二进制。固件空间只余576字节，新增功能前必须重新核对分区与大小，不能直接假定还能扩展。

Mac PR #20 已合入上游；如需要最终 USB OTA 加速镜像，应按其能力协商保持旧固件兼容，并把本次反馈/offer竞态修复纳入正确源码基线。未授权新刷机，不为 UI 或桥接缺陷反复刷新固件。

## 已完成验收与仍待验证

Windows 用户实际确认：USB/Wi-Fi 直达识别和蓝色箭头发送；任务页运行中追加与可靠接收；保留换行、标点、连续录音和句尾光标；电脑草稿/附件保护；同目标电脑校对后提交；切题/切会话拒绝串题并保留预览；USB/Wi-Fi音乐标题/封面更新；豆包自动切换与识别；按压反馈；设置页布局、电脑控制取消与未保存保护、连接页不跳高、语音设置快速显示、常用任务计数/取消、语音状态与取消不保存。

剩余真实验证：长时间闲置首次录音（用户要求放后）；DJI Mini2 不在身边暂缓；MiMo/MiniMax 与其他未配置账户有账户后再验；Mac R35 排队仍待 Mac 真机。最新发布安装包尚未重建及执行正式 setup 升级路径。纯 Wi-Fi 封面链路此前蓝牙也在线的场景不能单独证明纯 Wi-Fi cover carrier。

普通保存/取消/配置读回应自动测试，不再让用户逐项重复点击。真实录音、设备显示、发送接收与实际速度才安排必要实测，每次一个简单步骤。

## Mac 完成标准与今晚发布前检查

- 对上表每项记录“已完成／已有无需修改／不适用／待验证”，写出实现位置、自动验证、真实验证及剩余风险。不要仅回复“已对齐”。
- 在 Mac 实际执行 `cd mac-app && swift test && swift build -c release`；按当前 main 的验证入口跑受影响自测。Windows 不能替代这些命令。
- 新旧余额样例、送达未知不补发、完整队列基线、目标绑定、电脑校对、取消不保存与 pin 保留均有自动回归。
- UI 先设计，再实际渲染检查；自动诊断不要抢用户前台窗口。
- Mac R35 及真实端到端语音/提交需独立证据；无账户与缺麦克风项继续明确待验证，由用户决定发布范围，不能改成已通过。
- 合并后重新生成最终 Windows/Mac 包，记录 source SHA 和文件哈希。此前候选安装成功不等于最终安装器已验收；现有配对、草稿和展示设置必须保留。
- 发布范围、版本元数据、release-manifest 与实际附件一致。正式发布前还需授权推送/标签/发布，按 RELEASE-CHECKLIST 完成许可证、SHA256、双语主页/下载/安装说明与公开链接核验。

## 证据入口

- `WINDOWS-IDLE-SUBMIT-FIX-20261101.md`：增量开发、真实反馈、具体候选/安装证据（按时间阅读，后条覆盖旧状态）。
- `WINDOWS-FOLLOWUP-FROM-MAC-20261029.md` / `WINDOWS-FOLLOWUP-FROM-MAC-20261031.md`：原始任务与代码对齐。
- `DOMESTIC-QUOTA-INTERFACE-AUDIT-20261101.md`：厂商接口、缓存保护、账户验收边界。
- `../PROTOCOL.md` / `../TAB5-QUICK-CONSOLE.md`：消息字段、提交与提示语义。
- `../RELEASE-0.6.14.md`：历史准备材料及最终包仍待重建的说明。
