# Windows 开发交接：Mac TAB5 本轮功能、修复与性能优化

> 最新开发入口已更新为 [Mac R47 / TAB5 .298 Windows 对齐清单](WINDOWS-FOLLOWUP-FROM-MAC-20261031.md)。下文保留 10-29 历史检查点；当前验收状态以新清单为准。

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

本地固件构建、82 项工作器检查及真实 LVGL／完整 UI 回归通过；候选 .293 已授权安装到 Mac 配对设备，精确启动核验和 1280×720 首帧通过；图库切换、持续高清及 Windows 实机预览仍待验证。现场证据与边界见 [修复记录](TAB5-PREVIEW-MEMORY-20261029.md)。合并源码不代表正式固件发布。

Shared TAB5 .293 firmware safely transfers unused gallery caches to HD preview and returns idle scratch to foreground decoding. Windows needs no protocol or bridge change; apply the linked patch after checking its parent provenance. Local build and regressions pass, and .293 is installed on the Mac-paired device with exact boot verification and a 1280×720 first frame. Gallery switching, sustained HD and Windows physical preview acceptance remain pending. Source merge is not a firmware release.


## 2026-10-30 实机补充

Mac R35／TAB5 .296 已安装。“Codex 直达”在排队模式下，用户确认未点引导时消息已入队且 TAB5 显示成功；之后已恢复引导。TAB5「Codex 任务」的“追加”走独立接口：Mac 当前因无法绑定预期轮次而拒绝，设备通用 409 提示误导；Windows 也需检查桌面 follower-steer 是否遵循排队偏好以及当前轮次的约束，不能仅凭 IPC 返回就宣称与原生提交一致。详见 [任务页追加调查](MAC-TAB5-TASK-APPEND-20261030.md)。该路径尚未修复、尚无跨平台实机验收。

On October 30, R35/.296 desktop-direct queue delivery and success indication passed on Mac, with Steer restored afterward. TAB5 task-page append is a separate rejected path on Mac with a misleading generic conflict message. Windows must independently check whether follower-steer honors queue preference and current-turn constraints; neither platform's task-page parity has been accepted.

## 2026-10-30：任务页追加候选 / Task-page append candidate

Mac R36 本地候选把无照片的任务页追加接入 Codex 原生提交按钮，继承当前排队／引导方式；准确定位所选会话并面向最新状态，电脑已有草稿、问题卡或附件时拒绝混合提交。仍使用原请求及加密幂等回执，未知结果不自动重发，可靠入队算成功。Windows 现有 follower-steer v1 不能据此视为已对齐：需验证排队偏好、最新轮次、已有草稿／附件、重复点击及未知结果保护。Mac 的照片追加仍未支持，须保留设备图片与草稿。R36 正在真机验收，未经确认的结果不得标为通过。

The local Mac R36 candidate routes text-only task follow-ups through native submission and preserves the current queue/steer preference. It verifies the selected conversation, protects pre-existing desktop drafts/cards/attachments and uses durable receipts without uncertain retries. Windows follower-steer v1 does not establish equivalent queue behavior. Verify both modes, latest-turn dispatch, draft/image preservation and replay protection independently. Photo append remains unsupported on this Mac adapter. Hardware acceptance is pending.

R40 补充：原生模型／推理下拉按钮的装饰图像会暴露为 AXImage，不能当成草稿附件；Mac 真机只读已确认 AXPopUpButton 子图像及 ready=1。目录 recencyAt 在长轮次期间可能滞后，Mac 通过明确定位／成功提交的本机使用时间补充排序，后台轮询不更新，最多保存 512 个 UUID 与时间。Windows 应按实际控件及本地目录行为验证，不照抄 Mac AX 类型。文字发送、排队／引导和排序完整实机验收仍未完成。

R40 follow-up: native popup toolbar decorations must not be mistaken for attachments. The Mac read-only hardware check identified an AXPopUpButton image and passed preflight. Explicit navigation/submission timestamps now supplement stale long-turn recency; polling never promotes tasks, and local storage is bounded to 512 IDs/timestamps. Windows should validate its own native controls/catalog behavior. Full submission, queue/steer and order acceptance remains pending.

### R40 最新真机结论 / Latest hardware result

R40 + TAB5 .296 已通过本轮文字任务页引导、排队及当前会话排序验收：引导无需电脑操作且显示成功；排队时用户确认消息仍在队列，桥接确认 Codex 已保存本次排队消息；重新定位后当前会话排第一。用户已恢复引导，未刷固件。上述结论更新前文“待验收”状态，不能替代 Windows 实机测试或照片追加验收；Mac 照片追加仍未支持。

R40/.296 passed task-page text Steer, Queue and current-conversation ordering in this hardware run. Steer required no computer intervention and reported success; Queue was confirmed by the user and the bridge's retained-message receipt while still queued. The current conversation appeared first after explicit navigation. Steer was restored and firmware was unchanged. This supersedes earlier pending Mac results, not Windows hardware or photo-follow-up acceptance; Mac photo append remains unsupported.

R41 保护检查补充：R40 真机发现真实附件也可呈现为下拉控件，直接跳过该控件的全部子节点会漏掉内部“移除附件”按钮并误带附件发送。R41 保留装饰图标兼容，但完整检查所有后代及嵌套交互控件；本地回归通过，真实附件复验待完成。Windows 按本平台实际控件树做同类验证，不将“图标不应误报”实现为整棵交互子树免检。已有电脑文字草稿及预览照片保留已在 Mac 真机通过。

R41 guard note: R40 hardware revealed that a real attachment can itself be a popup control. Pruning all descendants missed its removal action and submitted the attachment unintentionally. R41 retains decorative-icon compatibility while checking every descendant and nested interactive control; local regressions pass, with hardware retest pending. Windows should test its actual control tree and never exempt whole interactive subtrees merely to avoid icon false positives. Mac desktop text-draft protection and preview photo retention passed hardware checks.

R42 恢复能力：旧 504 待确认回执会让 TAB5 永久停在“查询送达”，禁止编辑和清空。Mac 新增本地“处理待确认发送…”入口，仅供用户已在电脑确认送达的、草稿指纹未变化的文字追加请求；默认取消，二次核对具体请求／权限后登记人工确认，保留幂等记录，不重发、不修改电脑草稿。设备查询沿用既有 202 解锁，不增加协议字段。Windows 应提供同类受控人工确认出口，不能直接删除所有未知回执或盲目重发。Mac 本地恢复回归／离屏 UI 已通过，真机恢复待验。

R42 recovery: an old 504 receipt leaves TAB5 stuck on delivery queries with editing/clearing disabled. Mac adds a local user-confirmed-delivery action for an exact unchanged text follow-up, defaulting to Cancel and rechecking permissions/fingerprint before atomic acknowledgment. It preserves replay protection, never resends and does not alter the desktop draft. The existing 202 receipt unlocks the device, with no new protocol fields. Windows should provide a comparable controlled recovery path rather than deleting unknown receipts or retrying blindly. Mac local recovery/UI checks pass; hardware unlock remains pending.


## 2026-10-31 最新验收与诊断对齐 / Latest acceptance and diagnostics

以下更新前文待验收状态：Mac R42 的真实电脑附件拦截与双端保留已真机通过；人工确认旧未知回执后，TAB5 查询解锁并清空旧文字也已通过。Mac R44／TAB5 .296 按钮防连点通过：首次追加显示“追加中”并禁用按钮，无法再点，电脑仅收到一条消息，设备最终成功、清空草稿并恢复按钮。该测试只产生一次有效点击，不冒称两个实际请求的重放验证；请求幂等另有本地回归。照片保留／高清预览和电脑文字草稿保护也已通过。

Windows 对齐时需核对这些具体状态转换与内容保留，不能仅核对接口成功码。已有电脑文字时应明确拒绝，勿覆盖或混发；装饰图标不能误判，但真实附件及其移除控件仍须完整检查。未知结果恢复功能已可用，但两端操作繁琐是后续体验改进：可证实送达应自动收敛，仍不确定时给出明确的单次人工确认，不能盲目重发。

诊断应可后台读取，不自动打开、切换或置顶窗口，也不要求用户抄日志。Mac R43/R44 使用本机用户专属、限量的固定状态与结构计数，区分导航失败、已有草稿、附件拒绝和实际送达；不保存内容、文件名、会话／请求标识或凭据。Windows 按自身可访问接口实现，不照搬 AX 类型。Mac 曾出现前台已切换但导航区域不可读，后续定位通过，尚无根因，不将其当成已修复的通用 Windows Bug。设备照片追加仍未支持，.296 通用拒绝文案仍待改；DJI 暂缓。

English: This supersedes earlier pending results: real desktop-attachment rejection/content preservation and manual recovery/unlock passed on R42. R44/.296 passed button-level double-tap prevention and completion: the first tap disabled Append, one message arrived, and the device reported success, cleared text and restored controls. This does not claim two physical requests were replayed; request idempotency has separate synthetic coverage. Validate actual control states and preserved content on Windows, including desktop drafts and nested attachment actions. Provide bounded, user-only background diagnostics without foreground windows or logged content/identifiers/credentials. Automatic reconciliation and simpler unknown-result confirmation remain UX work. An intermittent Mac navigation-region failure has no established root cause despite a later successful locate. Device-photo follow-up, .296 rejection wording and deferred DJI acceptance retain their stated limitations.

### 2026-10-31：封面有限重试（R46）

Mac 已补同一资源跨通道共享的三轮上限和轮次间两秒确认等待，避免设备不确认时永远从头重发。Windows `Tab5Assets.NextChunk` 存在同类无限循环，应同步预算与只含数值的后台诊断；保持现有随机分块、正常状态/控制、最后一次成功缓存及后续歌曲独立重试。需覆盖未确认、迟到确认、持续状态刷新、预算耗尽、新曲目与各通道切换，不能只验证第一轮正常传输。

详见 [现场及验证记录](TAB5-MUSIC-COVER-RETRY-20261031.md)。设备没有“无法离开页面”的已确认故障：用户已更正导航一直可用。R46 是有限重试保护，不能宣称具体旧曲目失败原因已查明。

### 2026-10-31：送达恢复协议与 .298

- 本次已同步 Windows 服务端 `receipt-confirm`、`canConfirmDelivered` 和 journal 的设备绑定／精确草稿／权限／幂等校验，以及合成回归。确认只结束原待确认发送，不能再次调用桌面提交；重放不得清掉新草稿。
- Mac 复用发送前已有回执基线，在内存最多保留 64 项、60 秒，后续纯查询可核实已接受／已排队消息；不增加重复提交，不把原文写入诊断。Windows 可按本端可靠回执来源补齐同等自动核实能力。
- .298 在设备端自动查询最多三次，仍不确定时提供“取消／继续查询／确认已送达”。Windows 必须保持旧客户端、未获许可及内容变化时的安全拒绝；返回具体拒绝代码，避免再统一显示“请求已处理”。
- macOS 已完成 Release 与独立回归；Windows `win-x64` 交叉构建 0 错误／0 警告。Windows 原生 `dotnet build ... -c Release`、`--status-once`、journal 自检、真实追加与设备恢复验收仍需 Windows 端执行，不能把交叉编译算作原生运行通过。
- Mac 的 `AXManualAccessibility` 属于平台兼容处理，不要求复制到 Windows。现场两次定位均成功，但该 Mac Codex 版本返回属性不支持；历史间歇问题未能据此确定唯一原因。

### R47 补充（2026-10-31）

- Mac 长会话扫描跳过已识别的历史正文容器，避免历史节点耗尽输入定位预算，仍拒绝含糊目标。正常追加及“电脑已有草稿”具体拦截已真机通过。Windows 检查是否也扫描历史全文，不照搬 Mac AX 类名。
- R46 单纯限制封面重试后仍有 336×336 JPEG 无法确认；R47 增加同尺寸 RGB565/rle565 有界兼容路径（仅协议可表示的宽度 ≤336，不缩小宽图），并保留纯数值设备/USB诊断。实现可参照 `MacTab5Assets.advanceCoverLocked`；单纯停止循环不能宣称所有封面显示已修复。
