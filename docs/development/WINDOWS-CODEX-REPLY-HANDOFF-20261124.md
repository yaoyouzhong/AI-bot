# Windows TAB5 Codex 回复读取修复

## 范围

- 基线为 `main` 的 `6aaeafc`。改动针对 Windows 桥接的 Codex 回复读取，沿用现有 TAB5 协议和固件。
- macOS 已有相同的公开接口读取路径，可以沿用现有分页和 stdio 子进程。Mac 也会尝试桌面实时快照，但在 App Server 失败且无法使用实时快照时，当前 `page` 路径没有从本地会话记录读取指定轮次的兜底。若对齐 Windows 的读取恢复行为，Mac 需要补这一项并独立验收。见 [MacTab5Codex](../../mac-app/Sources/AIBotBridge/MacTab5Codex.swift)、[MacCodexPublicHistory](../../mac-app/Sources/AIBotBridge/MacCodexPublicHistory.swift) 和 [MacTab5CodexRPC](../../mac-app/Sources/AIBotBridge/MacTab5CodexRPC.swift)。

## 复现与修复

- TAB5 `0.2.322-ui` 经 USB 认证连接。实机打开 Codex 回复页出现「读取超时 · 可重新翻页」，同期桥接收到了 USB 请求。Windows 桌面会话列表正常，独立的桥接只读诊断在连接 `codex-ipc` 时超时。对同一真实会话，故障版桥接返回 `503 reply_unavailable`。
- Windows AppX 部署事件记录本机在 11 月 22 日 12:16 完成 `26.1117.4957.0` 更新。当前包仍在启动权限配置中保留 `codex-ipc` 的路径，但桌面会话走 App Server Host，当前打包代码中未见旧 `thread-owner-discovery` 或 `thread-stream-following-changed` 处理器，运行中的系统也没有该命名管道。更早的版本未做并机对照，首次移除旧接口的版本待确认。
- 桥接优先使用仍可用的旧桌面连接。该连接不可用时，启动已安装 Codex 包的只读 stdio App Server，用 `thread/read` 核对会话、用 `thread/turns/list` 确定目标轮次、用 `thread/items/list` 分批取公开答复。App Server 失败、缺少指定轮次或未返回公开文字时，回退到该会话的本地记录。读取不续聊、不同步桌面已读，也不向 App Server 发送审批或执行命令。
- 本机桌面的 App Server 由其子进程独占 stdio，未开放本地 TCP 端点。当前安装包的共享 daemon 分支明确排除 Windows，`daemon version` 也找不到本机控制 socket。独立 CLI `0.171.0-alpha.13` 返回当前桌面会话 `notLoaded`、`thread/loaded/list` 为空，能读到已保存的公开文字。独立进程无法读取桌面尚未持久化的文本和等待状态。依据见 [公开协议](https://developers.openai.com/codex/app-server)。

## 验证

- Windows Release 构建、`--status-once`、`--self-test-codex-tasks` 通过。专项覆盖跨目录页翻轮、实时状态隔离、App Server 优先级、管道超时后文件回退和读回执边界。
- 三个真实 Windows 会话由候选桥接读取最新公开回复并成功切到不同的上一轮。采样全部为 `source=app-server`，首次约 3.6 秒，复用连接约 0.16–0.24 秒。验证输出只记录状态、耗时、字节数和轮次是否变化。
- 本机已换装 `0.6.30-appserver.20261124`，运行安装目录中的 DLL SHA-256 与候选相同（`372A5E08C442AB78FB9A591F2178C6506CA3EB363E1EA66ED55A21746B04C5F7`）。TAB5 `0.2.322-ui` USB 认证恢复，11:52:39 的真机上一轮请求命中 `source=app-server; direction=-1`，加载 172 ms，返回批次 7327 字节，USB 记录包含读取应答。
- 用户在物理 TAB5 上确认「正文和上一轮都正常」。此次验收覆盖已保存回复首次打开与历史翻页。
- 当前桌面内存中的实时文本仍需桌面支持可共享的同一 App Server 连接。App Server 失败时的本地回退由专项回归验证。
