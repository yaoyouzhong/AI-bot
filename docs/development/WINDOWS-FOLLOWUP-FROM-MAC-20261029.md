# Windows 开发交接：Mac TAB5 本轮功能、修复与性能优化

日期：2026-10-29。给 Windows 开发会话的入口文档；按本文件继续开发，不要把 Mac 验收结果当成 Windows 验收结果。

## 范围和当前决定

- 对照当前仓库的 `mac-app/`、`windows-app/` 与 TAB5 .292。覆盖本轮语音、Codex 回填与提交、历史读取、OTA 和传输优化；较早交接见 [10-26 清单](WINDOWS-FOLLOWUP-FROM-MAC-20261026.md) 和 [Windows 已完成记录](WINDOWS-MAC-ALIGNMENT-20261026.md)。不要重复开发其中已完成的项目。
- Mac 当前实机安装 R34，TAB5 .292。R35 排队确认修复见本次提交；**排队模式实机验收按用户要求留到 2026-10-30，今天不测试、不刷机**。当前电脑偏好是“引导”。源码修复与自动检查不等于新候选已安装或实机已通过。
- **消息被 Codex 可靠接收并保存到队列，即算发送成功。** 不要求模型开始执行或任务完成；不能因队列尚未执行就提示“未确认发送”。未能证实收到时保留未知结果，不自动重发。
- 用户在 Mac Codex 将“通用 → 跟进处理方式”从“排队”改为“引导”后，TAB5 运行中一次发送直接进入当前会话，成功提示正常。这是应用偏好与已验证行为，不是 Windows/Mac 固有差异；桥接不要偷偷修改用户的应用偏好。
- 连接中断恢复**不是已知故障**，只是未专项验证。用户决定日常使用发现异常再修复；不为本次交接新增断链或长期压力验收门槛。DJI 当时未连接，不能写成收音失败。
- 下面的“缺口”来自 Windows 源码对照；除明确记载外，没有在 Windows 实机复现错误。不能复制 macOS AX/CoreAudio/MMKV 路径到 Windows，应在 UIA、TSF、WASAPI 和现有 IPC 架构内实现相同行为。

## 需要 Windows 继续开发或核实的清单

| 编号 | 优先级／类型 | Windows 现状与差异 | 目标与开发入口 |
| --- | --- | --- | --- |
| W29-01 | P1 新功能 | `Tab5CodexComposer.IsMainComposer` 明确排除 `@container/request-card`，`FindEditor` 只返回主框 | 支持当前提问题卡的回答框、独立多题与“下一步／提交”。参考 Mac `MacTab5Desktop.questionInfo/targetKey/submitQuestion`、`MacCodexQuestionValidation`；Windows 从 `Tab5CodexComposer`、`Tab5QuickConsole` 接入 |
| W29-02 | P1 正确性边界 | Windows 录音会话绑定 task/voiceId，但没有 Mac `beforeStart → prepareVoice` 的当前题卡目标快照；主框导航也不能证明录音属于新出现的题卡 | 录音开始记录设备、任务、题卡／主框身份；自动填入时复核。题卡变化必须保留预览、拒绝写进新题，不静默回退主框。参考 `MacTab5Service`、`MacTab5Desktop.prepareVoice` |
| W29-03 | P1 提交行为 | Windows `SubmitAsync` 用受焦点保护的 `SendInput(Enter)`；Mac 单次调用已核验输入框内原生发送／引导／排队按钮 | 对齐“当前可见提交动作”的语义，兼容用户 Enter 换行／组合键提交设置。若 Windows 保留键盘路径，需证明这些设置下也正确；不能补点第二次、停止任务、自动审批或未知结果后回退另一发送接口 |
| W29-04 | P1 已知 Mac Bug 的跨端补齐 | Windows `ConfirmClearAsync` 重找输入框并判断清空，没有新增已接受消息／已保存队列的确认；未证明 Windows 同样误报 | 加入只读消息与排队回执确认，解决“已交付但输入框读回失效”的潜在同类情况。Mac 参考 `MacCodexSubmissionReceipt`、`MacCodexQueueReceipt`、`MacCodexDesktopIPC`、`MacTab5CodexRPC.queueReceipt`、`MacCodexSubmissionValidation` |
| W29-05 | P1 提交绑定差异 | Windows `_drafts` 当前只保存时间、task 和 consumed；`SubmitAsync` 再读取提交当时的文字，没有与该次成功回填原文比较 | 明确保存并复核成功回填的目标与精确文字，避免旧 TAB5 操作提交后来换入的草稿。题卡变更也须拒绝。复用现有请求指纹与一次性消费；不要破坏用户明确编辑后的重新确认流程 |
| W29-06 | P2 定位与诊断 | Windows `UniqueTitle` 要求全局唯一标题；Mac 在实际暴露精确任务 URL 时优先用 ID，标题只作严格回退，并区分重名／题卡结构／扫描上限等原因 | 核对当前 Windows UIA 是否真实暴露稳定 task ID／URL，有证据才用；没有时继续安全拒绝重名。时间排序只能帮助选条目，不能证明当前前台输入框属于哪个同名会话。错误提示应指出原因，不只写“未就绪” |
| W29-07 | P2 新功能／平台对齐 | Windows `Tab5VoiceAudio.StartCore` 主要是已选 DJI 或 TAB5，现有源名与 DJI 直采通路已具备；Mac 增加 TAB5／电脑物理输入端点持久选择 | 按产品需求补电脑内置／USB 麦克风选择与真实端点名称。用户明确选择的不可用设备应报错，不冒充原音源。保留当前 DJI 共享端点和 TAB5 虚拟输入实现；实体音源需 Windows 实机验证 |
| W29-08 | P2 追加体验差异 | Windows `AppendText` 按标点插入句号，Mac R34 用空格连接本次两段，均不应把已有草稿整段重排 | 统一连续录音的体验：保留原文、用户换行及标点，避免内联回答框把自动 LF 归一化后出现“已写入却不能发送”。Windows 已有末尾光标与读回，不要重复实现；用实际题卡验证最终分隔策略 |
| W29-09 | P2 性能评估 | Mac `MacTab5ReadAhead` 将慢历史／IPC 读取移出设备邮箱；Windows 路径虽是 async，RPC 仍等待 `Tasks.HandleAsync` 的结果 | 先测慢 Codex 读取是否占用 USB/BLE 邮箱，若存在再增加单个后台读与有界缓存；缓存绑定设备／会话／页码，成功旧页短时保留，不能混页、无限并发或后台重放修改。参考 `MacTab5Codex`、`MacTab5ReadAheadValidation` |
| W29-10 | P2 性能评估 | Mac 准备期有界接收 PCM、最终结果可唤醒当前 poll；Windows 已有准备／播放排空、状态轮询及句尾稳定判断 | 按 Windows 实际时序优化开始就绪和停止到文字显示，不照抄 Mac 延时常数。参考 `MacTab5Voice`、`MacVoiceFinalPollValidation`；Windows 对应 `Tab5VoiceSession`、`Tab5VoiceHost`、`Tab5VoiceAudio`。先确认不丢句尾／提前停止／取消结果 |
| W29-11 | P2 回执体验 | Windows QuickConsole 回执在内存；重启后缺少旧 draft 会拒绝继续提交，因此已有防重保护，但无法返回之前的成功回执 | 评估加密、设备绑定、短期持久回执，让重启／丢回复后能查询结果；不得把“重启后拒绝”误写成“现在会重复发送”。只存必要状态与摘要，沿用 Windows 凭据保护机制 |
| W29-12 | P1 已同步源码、需 Windows 验证 | 本轮 Windows 已有同版本重装与 `rpcOtaBleRadioLease` 对端小改动，不是空白功能 | 核对 `Tab5RemoteOta`、`Tab5OtaCancellation`、`Tab5UpgradeStatusView`、`Tab5ConnectionForm`、`Tab5Protocol`、`Tab5Rpc` 及对应自测。验证同版确认、能力门槛、停止、精确镜像启动核验，不能看到版本相同就提前认定重装完成 |

优先级是开发排序，不是未经验证的安全漏洞评级。W29-09/10 属于有证据才实施的性能评估，不应为追求完全相同的内部实现而改动正常 Windows 通路。

## 排队确认的实现要求（W29-04）

1. 在用户明确提交、授权／设备／任务／目标／文字全部通过后，先取得动作前标识基线；先消费回执，再执行一次原生提交。
2. 首先保留原生 UI 确认。只有 UI 结果不明确，才读取同一任务与 owner 的接受／排队状态；绝不再发送。
3. 本地队列是 `.codex-global-state.json` 的 `queued-follow-ups[taskId]`；仅在明确操作期间有界读取，不写它，不把它导出。文件不可读、结构不兼容不等于队列为空。
4. 服务端队列用当前安装版提供的只读 `thread/queue/list`，参数 `threadId` 和 `cursor`。当前返回 `data[]` 条目含 `id`、`clientUserMessageId`、`input`；它与本地队列不是同一来源。Mac 当前适配对未完成分页不作确认，单次读时限 600ms、不会排在历史查询锁后等候；Windows 根据自身超时预算实现有界完整快照。
5. 成功必须是完整纯文字相符的新条目。历史同文、已有队列迁移到服务端／accepted、旧 pending 变 accepted、其他任务／owner、正文不同、带附件的部分匹配、失败／未知状态不能充当本次回执。
6. 动作前必须同时取得完整可用的本地和服务端队列基线；否则不能用动作后的 accepted／queue 增量兜底确认，避免未观察到的旧队列迁移造成误报。别名关联必须覆盖本地 id、服务端 id、clientUserMessageId 等；只保留短期内存摘要，不输出正文、真实条目 ID、原始事件或工具内容。
7. 入队就是交付成功，但不是模型完成。沿用 `desktop.submit-draft` 的 `200 + submitted:true`，不新增协议字段；现有 .292 显示通用成功提示，桥接诊断可明确“已保存排队消息”。若将来为 TAB5 新增专门“已排队”文案／字段，须同步固件、Windows 与协议文档，另行验收。
8. 原样重放请求返回已有回执，换 requestId 但复用已消费 draft 也不能重发。清理本地草稿时，仅清理同设备／任务且文字仍等于已提交内容的镜像。

## Windows 已有能力：保留并做必要回归

| 能力 | 已有代码 | 对齐结论 |
| --- | --- | --- |
| 回填后末尾光标 | `Tab5CodexComposer.MoveCaretToEnd`、`TextPattern` 零长度末尾选择、读回确认 | 已有；光标失败不撤销已成功回填，不导致再追加一次。题卡支持扩展后回归即可 |
| 单次写入＋只读确认 | `AppendAsync`、`ConfirmClearAsync` | 已有；Windows 会重新定位失效 UIA 节点，不能宣称完全缺少读回恢复 |
| 请求防重／draft 先消费 | `Tab5QuickConsole.HandleAsync` | 已有；加强目标／原文与持久结果能力，不推翻既有消费保护 |
| 句尾等待、静音结束、取消重录 | `Tab5VoiceSession`、`Tab5DoubaoVoice` 和自测 | 已有；Mac 完整句尾通过不能代替 Windows 实机，不能把平台阈值直接统一 |
| 豆包麦克风恢复 | `Tab5DoubaoMicrophone` 的租约日志、恢复重试；DJI 共享设备检查 | 已有；Mac 后台切换实现是平台适配，不要求迁移 MMKV／macOS 通知到 Windows |
| 会话已读同步 | `Tab5CodexReadState`、`Tab5CodexDesktop.MarkReadAsync` | 已有，部分主动同步比 Mac 更完整；保持按账户／host／turn 归属的边界 |
| BLE 批量窗口与一次密文恢复 | `Tab5BleMailboxWindow` 的 bulk 64、协议确认和 replay 处理 | Windows 是本轮 Mac 对齐参考；不重复“优化”成另一套机制 |
| 系统 TCP／TCP_NODELAY | `LanStatusServer`、`Tab5OtaFlow` | Windows 已有；Mac 从 Network.framework 换 BSD socket 的修复不适用于 Windows |
| 10-26 余额来源隔离、缩放、预览、资源调度等 | [Windows 10-26 对齐记录](WINDOWS-MAC-ALIGNMENT-20261026.md) | 使用后续实际代码与记录，旧交接文档中的未完成项不是本轮新增缺口 |

## 性能事实与不可直接移植的内容

- Mac R24 单次停止到最终文字 5103 → 3384ms，最终文字到响应生成 393 → 0ms；两次录音时长不同，这是局部现场测量，不是保证每段提升 33.7%。Windows 需要自己的分段计时。详见 [语音验收记录](MAC-VOICE-SOURCE-20261028.md)。
- Mac R18 的同镜像 Wi-Fi 对照把设备等待数据降至 0.020s，安装 21.602s；这是 Mac TCP 发送路径修复，不能把主机提交时间当设备安装完成。详见 [TCP 对照](MAC-TAB5-OTA-TCP-20261029.md)。
- Mac BLE R19 向 Windows 的窗口与不可变密文恢复对齐；R21 在同 .291 镜像上安装 399.979 → 338.938／340.052s。不能把跨版或不同镜像时间当同条件加速。详见 [BLE 对照](MAC-TAB5-BLE-OTA-20261029.md)。
- 只有同一 firmware capability 已协商时启用快速分片、无线暂停租约等；不放宽认证、镜像 SHA／ELF、启动核验或取消边界。
- TAB5 .284–.292 的音源名称、清空视觉、配额默认页、OTA／BLE 固件侧修复是共享固件成果，不在 Windows 另写一份；对应 `docs/proposals/` 仅是独立工程补丁与来源记录，不是完整可直接编译的固件仓库。

## 本次源码交付的检查结果

工作区与冻结 R35 候选均通过 Release 构建，各通过 28,784 项隔离检查；另通过 81 项 OTA／HTTP 回环检查和 16 项监听恢复检查。生产源码只读 harness 已验证当前本地／服务端队列结构可读及不变快照拒绝，未实际提交消息。完整 `swift test` 因本机 Command Line Tools 缺少 XCTest 模块未通过；随后 [GitHub CI 40864671059](https://github.com/yaoyouzhong/AI-bot/actions/runs/40864671059) 已在 Windows 主机通过 Release 构建、`--status-once` 和完整 `--self-test-public`；这不是 Windows 设备实机验收。R35 未安装，实机排队验收仍为 10 月 30 日。

CI 修复补充：固定 MMKV 依赖需要 Swift 6.2+，三个 Mac 构建工作流统一选择 macOS 15／Xcode 26.3（[GitHub 镜像工具清单](https://github.com/actions/runner-images/blob/main/images/macos/macos-15-arm64-Readme.md)），应用最低系统版本仍为 macOS 13。Windows OTA 停止用例改为跟踪开始操作实际生成的新 offer，并验证停止后旧 offer 在重新选择固件后仍返回 404；Mac XCTest 断言同步当前诊断保留最近失败的行为。合并前须以 [PR #15 当前 CI](https://github.com/yaoyouzhong/AI-bot/pull/15/checks) 全通过为准；不能把前次失败运行当成最终结果。

## Windows 开发顺序与验证

先拉取包含本文件的最新提交并检查本地未提交改动，再核实本机 Codex 版本／队列接口，不覆盖 Windows 现场已有修复。建议顺序：W29-01/02/05 → W29-03/04 → W29-06/07/08 → 有测量依据的 W29-09/10 → W29-11；W29-12 对已同步代码先构建和回归。

必需检查：

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
git diff --check
git status --short
```

对受影响路径运行现有 Composer、QuickConsole、CodexDesktop、Voice、DoubaoMicrophone、RemoteOta 等自测；从 `Program.cs` 核对实际参数，不臆造命令。

实际验收保持每次一个小步骤：主框空闲发送；运行中引导；排队入列即显示成功；两段录音保留且光标在末尾；题卡下一步与第二题独立提交；旧题录音跨新题时拒绝串题；结束后电脑独立豆包正常。**结束录音会自动填入，没有单独的“回填”按钮；蓝色箭头才是提交。** 不要再要求用户点击不存在的入口。

Mac 已确认的功能只记作参考基线；R35 排队实机在 10-30 验收。连接中断与长期运行按照用户决定留给日常观察，不列已知故障，也不阻塞本次交付。没有 DJI 就记未验证，不强行代测。涉及固件变更遵循根 AGENTS 的对端／版本／实机规则；没有明确授权不要刷机、推送、打标签或发布。

## 可直接交给 Windows 会话的任务

请读取本文件及仓库 AGENTS.md，先核对当前 Windows 实现，按 W29-01 至 W29-12 的证据、优先级与已有能力逐项补齐。不要重复开发“Windows 已有能力”表中的实现，不要把 Mac 专用平台修复照搬。排队已可靠保存即发送成功；未知结果不重发。保留现有配对、音源、设备展示和所有用户草稿。先实现并运行 Windows 构建、自测，再按简单单步流程安排实际验收；按完成／不适用／待验证记录每项结果。连接中断恢复不是本次已知 Bug，不新增相关专项验收。Mac R35 的实机结论以 2026-10-30 后更新的验收记录为准。

## English handoff summary

This is a source-based Windows follow-up list for the October 29 Mac/TAB5 work, not Windows hardware acceptance. Priority gaps are question-card targeting and Next/Submit, recording-time target binding, exact staged-text submission checks, native action semantics and read-only accepted/queued delivery receipts. A reliably retained queue entry counts as successful delivery; execution completion is separate. Preserve existing Windows end-caret handling, one-write readback, idempotency, microphone recovery, read-state sync, TCP sockets and BLE window recovery. Review source selection and append semantics; measure mailbox/history and voice-final latency before porting performance changes. The existing same-image reinstall and BLE radio-lease peer changes require Windows checks. R35 queue hardware acceptance is deferred to October 30 at the user's request. Connection interruption is untested, not a known defect, and is left to ordinary use. No firmware flash or formal release is part of this handoff.

Validation: both Mac Release builds pass 28,784 isolated checks each, plus 81 OTA/HTTP and 16 listener-recovery loopback checks. The production-source read-only queue harness passes. Full XCTest is unavailable in the local Command Line Tools; the subsequent Windows CI run linked above passes Release, status-once and public fixture regression checks. Windows hardware acceptance remains separate. CI now selects Xcode 26.3 for the pinned dependency; final merge eligibility follows the current PR checks, not earlier failed runs.


## 追加：TAB5 高清预览内存修复（.293 候选）

共享固件新增 [缓存借用补丁](../proposals/tab5-preview-gallery-cache.patch) 与 [来源及哈希](../proposals/tab5-preview-gallery-cache.json)，基于已安装 .292。Windows 无需重复实现或修改协议：预览可借用未显示的图库缓存，图库需要解码时归还预览空闲缓冲区。核对父补丁链后应用到独立 TAB5 工程；不要在 Windows 桥接中降低默认清晰度掩盖此问题。

本地固件构建、82 项工作器检查及真实 LVGL／完整 UI 回归通过；候选 .293 尚未安装，跨平台实机高清预览仍待验证。现场证据与边界见 [修复记录](TAB5-PREVIEW-MEMORY-20261029.md)。合并源码不代表正式固件发布。

Shared TAB5 .293 firmware safely transfers unused gallery caches to HD preview and returns idle scratch to foreground decoding. Windows needs no protocol or bridge change; apply the linked patch after checking its parent provenance. Local build and regressions pass, but .293 is not installed and cross-platform physical preview acceptance remains pending. Source merge is not a firmware release.
