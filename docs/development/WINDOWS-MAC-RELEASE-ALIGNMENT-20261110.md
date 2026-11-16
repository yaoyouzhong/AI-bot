# Windows / Mac 发布前对齐（2026-11-10）

## 2026-11-10 发布前记录

- Windows 来源：`6456c169c3f92222c1a233489da36af347b8ed28`；GitHub Mac 来源：`8e2cba21caf422b94e6f673e89db34b8577d654e`。本次合并两端历史，Mac 产品源码沿用 Mac 提交。Windows 旧本地覆盖文件单独存档。
- Windows Codex、Claude 目录短暂为空时保留显示，持续有效空目录则清空；读失败最多暂留最近 30 秒的目录。缓存不授权发送。Codex 仅按本地 Git 元数据合并已确认的 worktree，分开同名不同仓库；同项目最多 64 条、整个目录最多 80 条，旧固件继续分页，LIST1 固件可合并经校验的分页。请求仍使用原会话 ID。
- Windows 已观察到同一 Codex 回复的未读标记后，标记消失即可在下一次有效状态同步中确认；新轮次和账户切换仍保留保护。未改设备协议和 ESP8266。
- Windows Release 构建、`--status-once` 及 Codex 已读、目录、发送，Claude 原生收发和工作区、管理员、OTA、显示设置、RPC、帧预算及传输回归通过。没有操作用户会话或草稿。LIST1 已在 Windows 重放并通过 LVGL 项目去重、64 条滚动和电脑输入唤醒回归。屏保现场后来可退出，之前的未唤醒没有捕获足够事件定位。
- 已安装的 Windows A 轮曾确认一次 UAC 且防火墙许可接续。本轮只读看到该原安装的登录任务仍在运行，桥接诊断同时报告认证 Wi-Fi 与 USB 已连接；这不替代第二轮升级和 OTA 后自动回连的验收。
- Mac 原生构建及设备上运行的历史身份见[项目列表验收](TAB5-PROJECT-PICKER-20261110.md)。两端正式桌面包仍需从最终相同提交分别原生构建、安装和验收；Windows 用最终包补测单次授权升级 B、常驻桥接 OTA、Wi-Fi 自动回连及 TAB5 实际已读耗时。
- 历史 Mac Linfei 音频和包含它的旧 ZIP 留作本地证据，不随新公开包。用户希望保留 Codex 原音频，仅重做 Claude 并保持音色一致、英文名称清晰。先试听并核实所选素材的再分发依据，再冻结新应用、首刷、OTA、完整源码和说明的身份；新镜像需重新做受影响的真机验收。
- 待发布版本源仍为桥接 `0.6.18`、TAB5 `0.2.318-ui`；核对 GitHub 暂无两者标签。此记录不是发布批准；无推送、标签、固件写入或公开发布。

## 正式发布与 Windows／TAB5 验收证据

桥接 `0.6.18` 和 TAB5 `0.2.318-ui` 均于 2026-11-10 正式发布。Windows x64 安装器随桥接发布，两个版本的标签均指向 `5d40ec48`。该源码包含 Windows Claude、Codex 和后台自动化修正 `6456c16`，以及语音权限恢复和未读优先修正 `c9e817c`。[桥接发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.18) · [TAB5 固件发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.318-ui)

| 项目 | 交付内容与对应验证 |
| --- | --- |
| Windows Claude 原会话与 Codex 输入保护 | Claude 历史支持识别同会话的隐藏 `local_command` 连接节点。Claude 与 Codex 的输入保护和送达回执经过专项回归。电脑输入框已有草稿时暂停发送，送达不明时保留草稿供核对。Claude 原终端和 VS Code 会话重连的设备结果见[专项记录](WINDOWS-CLAUDE-RECONNECT-20261109.md)。 |
| Windows Claude 已读 | 11 月 9 日的隔离回归覆盖历史读取、桥接重启、同会话后续输入及其他会话隔离。对旧版的复现表明，只读历史曾可重新点亮旧未读。电脑端发送下一条消息后由 TAB5 自动清除旧未读的补测停在测试 CLI 的目录信任提示，仍缺真机闭环。本地证据为 `artifacts/windows-voice-consent-20261109/unread-reproduction.json` 和同目录的 `unread-next-final-03-validation.json`。 |
| Windows 后台自动化 | 正式源码包含运行 Windows 消息循环的后台自动化入口。11 月 9 日在当时的本地候选上核对了受保护安装、登录任务运行、USB 和 Wi-Fi 连接，以及设置和草稿摘要。登录任务的实际 Windows 重启仍需独立验证。本地证据为 `artifacts/windows-mac-alignment-20261108/automation-message-loop-install/final-readback.json`。 |
| Windows 麦克风授权 | 识别本次权限窗口后，点击「允许」继续同一次录音。用户在当时已安装的候选上确认无需再点一次录音。拒绝、取消、超时和目标变化的保护见[语音验收记录](WINDOWS-VOICE-CONSENT-20261110.md)。 |
| TAB5 未读列表与默认会话 | 正式固件包含当前项目的未读优先和默认最近会话定位。未读优先曾在 [318-R6 真机](TAB5-318-R6-UNREAD-PRIORITY.md)确认。正式 R13 的安装、启动与总览采样另见[真机补验](TAB5-318-R13-DEFAULT-SESSION-20261110.md#正式发布与-2026-11-11-真机补验)。 |
