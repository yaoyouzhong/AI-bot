# Mac 交接：TAB5 318-R6 与语音授权（2026-11-10）

从 `origin/codex/windows-dynamic-microphone-20261105` 同步。本轮已验收的产品源码为 `c9e817c55c1b16d6984b5af93e1e88d8f2e991e0`，包含 Mac 上次交接的 `8e2cba21caf422b94e6f673e89db34b8577d654e`；本文件随后的提交只更新文档。桥接版本线仍为 `0.6.18`，TAB5 对外版本仍为 `0.2.318-ui`，`318-R6` 是内部镜像编号。TAB5 协议、配对、会话 ID 和草稿格式均未变。流程按[跨平台开发与发布规则](../DESKTOP-DEVELOPMENT-RELEASE.md)执行。

| 优先级 | 对齐项 | 已有实现与证据 | Mac 下一步 |
| --- | --- | --- | --- |
| P0 | 未读会话优先显示 | [R6 固件](TAB5-318-R6-UNREAD-PRIORITY.md)把当前项目的新未读排在最上方，手指操作结束后才定位。同一条回复的重复心跳不再拉动列表；厂商、项目、选中会话、已读状态和草稿不变。Windows 连接的真实 TAB5 已由用户确认。 | 固件已实现，桥接无需重做排序。用已核验 R6 接 Mac，在同一项目向下滚动后让隔离会话收到新回复，确认未读置顶、其他项目不抢切、重复同步不抢滚动。 |
| P0 | 首次允许语音后继续同一次录音 | [Windows 修复](WINDOWS-VOICE-CONSENT-20261110.md)处理系统权限窗关闭时的暂时焦点变化，用户确认点「允许」后无需再次点录音。Mac 已有 `MacTab5Speech.preparePermission` 与 `MacTab5Voice` 的录音 ID／授权票据检查；`MacTab5VoiceTests.testPermissionResumesOnlyTheOriginalUncancelledTake` 覆盖继续、取消和拒绝。 | 先在隔离用户或独立测试 App 身份下触发真实的首次授权，保留日常 App 的权限。测试实际使用的输入源与识别后端；Apple Speech 路径对电脑麦克风申请麦克风和语音识别，TAB5 麦克风只需适用的语音识别许可；豆包路径按实际后端另验。确认一次点击后可录音、识别回填且不自动发送；拒绝、取消、断连或切会话时不迟到启动，不丢原草稿。仅复现缺口时修改 Mac 的权限或音频回调。 |
| P0 | Claude 原生终端不绑定版本 | [两端源码已移除固定版本名单](CLAUDE-NATIVE-TERMINAL-VERSION-INDEPENDENT-20261110.md)。Mac 仍校验实际可执行文件、进程启动代次、前台 TTY、唯一归属和输入区。 | 用当前安装的 Claude Code 在 Terminal 或 iTerm2 重开同一隔离会话，验证发送按钮恢复、消息仅进入目标会话一次、电脑已有草稿时安全拒绝。Mac VS Code 本轮不纳入。 |
| P1 | 目录短暂为空、已读确认 | `MacTab5Codex.swift` 已处理短暂空目录，使用 `MacCodexProjectIdentity` 归并同仓库 worktree，并调用 `MacCodexReadState` 同步桌面已读。Windows [对齐记录](WINDOWS-MAC-RELEASE-ALIGNMENT-20261110.md)另外覆盖缓存与已读同步。 | 对照行为复测，无需照抄 Windows 超时：瞬时空目录不清空厂商或项目，持续有效空结果正确清空，缓存不能授予发送。桌面读到同一回复后记录 TAB5 角标消退耗时；下一条新回复、其他会话及账号切换不被误清。只补可复现差异。 |

Windows 的 PickerHost、焦点恢复、管理员启动任务和防火墙规则是平台专用机制，Mac 继续使用自己的权限与后台启动路径。

## 构建物与验证

| 对象 | 已核验身份 | 结果 |
| --- | --- | --- |
| Windows 安装候选 | `AIBotBridge.dll` SHA-256 `ead4787f2194ada2ebcbfc18da6bae3a7df3c0025829541e7c3c299842f5fd07` | Release、`--status-once`、语音准备 39 项及语音核心和 Claude 工作区回归通过。安装后核对 83 个文件，运行的是同一 DLL；草稿、配对和配置等五个电脑端文件前后摘要一致。首次授权后同一次录音继续，用户真机确认。 |
| TAB5 318-R6 | 7,027,136 字节；BIN SHA-256 `de7cfc8515fec52e8990af04c86ca8d767dbe9208dc5e5cdb08606bb28549303`；运行 ELF SHA-256 `40cc39cbe8ba92e2dd763d2fa58cc1409a238fdc28bf320556ef13d70c2af153` | 64 条会话的实际 LVGL 离屏回归通过；用户真机确认未读优先显示。电脑自动发起升级未传输，改由 TAB5 本机发起、常驻桥接经 Wi-Fi 完成。核验从 `ota_0` 切到 `ota_1`、精确 ELF 一致、分区 `VALID`，Wi-Fi 自动回连，USB 恢复。此结果不算电脑自动发起升级通过。 |
| macOS 本轮候选 | 待 Mac 从同步源码原生构建，登记实际 App/归档摘要 | 上一轮 [Mac 项目列表验收](TAB5-PROJECT-PICKER-20261110.md)对应 `b053ad2` 和 R4-LIST1，不能用来证明本轮原生终端、R6 或首次权限实测通过。 |

Mac 先检查工作区并保留已有修改，再同步上述分支。从仓库根目录运行：

```sh
swift test --package-path mac-app
swift build -c release --package-path mac-app
bash scripts/build_macos_app.sh
```

用实际 App 包记录 macOS 版本、架构、完整源码 SHA、包 SHA-256、签名实际状态和上表的真机结果。现有设备已经是核验过的 R6 时不必再刷；如需复现固件源码，按 [.318 归档与 R4 增量](tab5-318-r4-source/README.md) → [LIST1](TAB5-PROJECT-SCROLL-LIST1-20261110.patch) → [R5](TAB5-318-R5-RELEASE-NOTICE.patch) → [R6](TAB5-318-R6-UNREAD-PRIORITY.patch) 在独立目录重放。R6 沿用 R5 的两段提示音；素材来源与公开再分发边界按 [R5 记录](TAB5-318-R5-RELEASE-NOTICE.md)核对。复建镜像需要自己的指纹和验收记录。固件与桌面包不入 Git；回传证据只保留非敏感结果，不附私人草稿、日志正文或凭据。

## 交回 Windows

先回传 Mac 构建和真机验收结果；遇到复现的差异，提供最小改动及可复查证据。Windows 汇总两端候选，后续由一个发布负责人统一处理。当前交接只提交和推送源码与文档，不创建标签、不公开 Release，也不授权接收端自行刷机、推送或发布。
