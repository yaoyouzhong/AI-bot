# Windows → Mac 对齐交接（2026-11-09）

## 范围与取用方式

- Windows 交接分支：`origin/codex/windows-dynamic-microphone-20261105`。以包含本文件和 `tab5-318-r4-source/manifest.json` 的提交为交接点。Mac 从自己的当前分支核对差异，不直接合并 Windows 平台实现或覆盖未提交改动。
- 目标：原生 Claude Terminal 会话的未读、已读回执和重连，以及 Codex 桌面输入与语音涉及的共同行为。**Mac VS Code 暂不纳入。** 本轮没有修改、构建或部署 Mac；本地 Swift 源码核对只用于定位验收点。
- 固件实际显示 `0.2.318-ui · R4`，协议版本仍为 `0.2.318-ui`。沿用 `controls`、`repliesReady`、`repliesRead`、`repliesUnread`、`replyView.hasText` 和 `read-ack`；没有新增协议字段或要求 Mac 升级权限。
- 交接和远端推送仅包含工程源码与验证说明。Windows 已装候选和 TAB5 镜像按下方哈希识别；不视为 GitHub Release 或可公开分发的安装包。

## 需要在 Mac 核对的行为

| 优先级 | 用户行为与 Windows 实现 | Mac 侧核对及验收 |
| --- | --- | --- |
| P1 | **清旧未读、发新提醒。** `ClaudeWorkspaceNative.cs` 按同一会话实际接受的主支用户消息清除更早的未读；完成扫描独立保存上一状态。读取先看到新完成轮、短暂探测失败或读取旧历史均不能吞掉新提醒或复活旧提醒。 | **待验证。** 核对 `MacClaudeNativeBackend.swift`、`MacClaudeWorkspace.swift`、`MacTab5Codex.swift` 的观察与发布路径，测试「第一轮未读 → 电脑提交第二轮 → 第一轮清除 → 读取先于完成扫描 → 第二轮只出现一次未读」。保留另一会话未读，空白、取消或侧支不作为新公开完成。 |
| P1 | **准确已读回执。** Windows `ClaudeWorkspace.cs` 对最新有公开正文的完成轮次签发回执，即使之后有空白或运行中的轮；纯读取不发布新未读，回执绑定 device/task/turn，重复读复用，过时回执不清除新未读。 | **待对齐。** 核对 `MacTab5Codex.swift` 的 `page` / `read-ack`；本地检查的 `page` 仍由 `!history` 限制且读取时写入 `ready`。补「空白后继轮」「读历史不新增通知」「连续读取超过容量」「跨设备和旧回执拒绝」。若 Codex 与 Claude 分支共用代码，保留 Codex 自身的桌面已读同步。 |
| P2 | **同一会话重连。** Windows 优先刷新选中会话的输入能力，历史加载、其他会话的慢探测和短暂断联不会占用同一路径。实际派发仍校验会话、输入目标和草稿。 | **待验证。** 检查 `MacClaudeWorkspace.thread/list` / `MacTab5Codex.refresh`、原生 Terminal 适配器；只补造成选中会话延迟的缺口，复用现有的时效和工作区机制。真机实测关闭并恢复同一 Terminal 会话后，TAB5 从灰到可点的时间。Windows 的约 5–10 秒仅供参考。 |
| P2 | **历史与输入兼容。** Windows 接受经过身份验证的隐藏 `system/local_command` 连接节点；中文建议不能误当真实草稿。Codex 桌面识别新旧 Composer class token，保护附件、引用和已有草稿。 | **待验证。** 对照 `MacClaudeHistory.swift`、`MacClaudeWindowComposer`、`MacTab5Desktop.swift`；优先测试重命名后精确送达、中文建议和相同文字真实草稿、当前 Codex 桌面版本。已有 `stop_hook_summary` 支持无需重复。 |
| P2 | **语音权限与焦点。** Windows 已验证允许后同一次录音继续，必要时恢复原绑定，取消不会发送；设备端识别后是否送达仍以实际回执为准。 | **待验证。** 检查现有 Mac 授权恢复，使用原生权限机制及隔离会话验证「首次允许 → 继续录音 → 识别 → 确认送达」和取消。Windows 的 PickerHost、COM 线程和豆包窗口识别不移植。 |
| — | **TAB5 共享 UI。** 318‑R4 打开目标新回复时不闪旧正文，返回防止短时间重叠连点，自动定位不会固定为主动历史书签。 | **共用固件，Mac 不另做 UI。** 配对 R4 验证角标、正文和返回；同会话角标统计未读会话数，不累加同会话的回复条数。 |

Mac 已有 USB `--tab5-automation`、唤醒命令和本机活动时间上报，先复用；Windows 的受保护安装、最高权限登录任务、控制台租约和窗口激活都是平台专属，不在 Mac 引入 root 或另设管理员助手。

## 构建和验收

- Windows 候选主 DLL SHA-256：`113a7af79e227db958318ceb682ad72b101039d87d054989b1e50eff9e7e7c5c`。Release、正常桌面启动、Claude 工作区/发送/回执、TAB5 RPC 和状态容量、Codex 回归已通过。最新部署和剩余实机项见[验收记录](TAB5-REPLY-RETURN-R4-20261109.md)。
- TAB5 R4 镜像 SHA-256 `33231b1746f6a26baddf96f5c4425257246fcd10d727ee452a4b74e60858886e`，ELF `c07629d9118b2914818536400a4a786cf2cbb4e97a7e127df56b4af94bf5edc9`。使用[合并源码增量](tab5-318-r4-source/README.md)和[逐文件清单](tab5-318-r4-source/manifest.json)，基于分支已有的 `.318` 完整归档，不叠加旧补丁。声效输入由有权使用该素材的本地构建端提供，仓库不新增该二进制。独立重建须重新记录摘要，不假定与已装镜像逐字节一致。
- Mac 第一轮：记录实际分支和 SHA，在 `mac-app` 执行 `swift test`、`swift build -c release`；然后在实际 Mac 与 TAB5 上优先走两条 Terminal 会话的未读闭环及同一会话重连。只记录轮次和时间等必要证据，不保存正文、凭据、草稿或配对密钥。已有通过项仅在相关实现未变时复用。
- 暂无正式发布、标签或 Release 附件；Mac 自己确认对应候选的安装、授权及真机证据。详见[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)。
