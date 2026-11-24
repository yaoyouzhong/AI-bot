# Windows TAB5 Codex 回复读取修复

## 范围

- 本次发起端：Windows
- 是否需要交接：需要。Mac 核对本端的本地记录兜底缺口，并按 QQ 审批结果跟进 TAB5 读取与翻轮。
- 接收平台：macOS
- 发起端对齐初判：需要。Windows 对照两端读取源码，发现 Mac 在桌面快照和 App Server 都取不到正文时缺少本地记录兜底。
- 基线为 `main` 的 `6aaeafc`，Windows 改动已随 `aee01675a078` 合入。沿用现有 TAB5 协议和固件。

## 对齐清单

### TAB5 在桌面快照和 App Server 无法提供回复时继续读取已保存的对话

- 适用范围：两端共同要求。桌面快照不可用且 App Server 失败、缺少指定轮次或未返回公开正文时，Windows 和 macOS 都应从同一会话的本地记录读取指定轮次已保存的助手公开正文，并支持翻阅上一轮。
- 发起端初判与结果：Windows 已补本地记录兜底并通过专项回归。Mac 已有桌面快照、App Server 和分页路径。
- 接收端核对结果：原先两种读取来源都取不到指定轮次正文时，Mac 缺少本地回退。对齐开发已获批准并在本地分支完成，验证依据见下方 macOS 回填。
- 验收方法：用含两个已保存公开回复轮次的测试会话，分别模拟 App Server 失败、找不到指定轮次和没有公开正文，同时让桌面快照不可用。在 TAB5 打开指定轮次并翻到上一轮，检查正文和轮次一致，读取历史不产生或清除其他轮次的未读。另一会话的内容以及用户消息、推理和工具记录均不可出现在回复页。测试会话没有可用的目标轮次时，保留读取不可用状态。
- 对齐状态：待验证。本地代码检查已通过，尚未合入主干，待配对 TAB5 实机验收。

待对齐：macOS｜TAB5 Codex 指定轮次公开回复的本地记录兜底
  - macOS 回填：待验证。验证依据：本地开发提交 b88af65941f3 已补上所选轮次的回复读取。桌面不可用时，服务失败、缺轮、缺正文三种回退测试均通过。Swift 测试无失败，Mac 正式版构建通过，仍待配对设备验证。

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
