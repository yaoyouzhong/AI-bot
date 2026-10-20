# Mac TAB5 Codex 链路后续 / Codex link follow-up

本轮沿用测试分支与交接边界：自动测试、最小修复，不合并 main、不部署、不发布、不刷机。真实发送／追加／中断仍只针对用户明确指定的测试任务；本轮没有指定，未执行这些操作。

## 当前阻塞与只读证据

本机已安装 CLI `0.163.0-alpha.10.2`。其公开 `app-server proxy --help` 提供 `--sock`，但默认控制 socket 不存在；只读 `app-server daemon version` 也返回同一 `ENOENT`。当前桌面子进程使用 stdio。实际桥接 RPC 对象复核代理在初始化前退出，没有任务请求到达服务端。没有连接内部桌面 IPC、读原始 rollouts、修改用户配置，或启动／部署 daemon。

按官方 [App Server 文档](https://learn.chatgpt.com/docs/app-server)核对握手，并用已安装 CLI 导出本版本 JSON schema（只在忽略目录）。另外在空的临时 `CODEX_HOME` 中启动一次短暂 stdio **隔离探测**：初始化、空任务列表成功，不存在的合成 UUID 读取被拒绝；随后进程退出、临时目录清理。没有登录、模型请求、真实任务创建或访问用户会话。这证明已安装版本支持这些协议方法，不能冒充已接入当前桌面服务。

## 修正与自动回归

1. App Server 的 `active` + 空 `activeFlags` 映射为现有 TAB5 `working`；等待批准／用户输入映射 `waiting`，`idle` 保持 `idle`。`notLoaded`、`systemError`、未知状态／标志保持 `unknown`，不猜成空闲或运行。
2. 首页任务先选实际运行中的会话，再按更新时间选择；传输裁剪优先保留当前阅读任务与首页运行任务。只有一个槽位时保留阅读任务，并从实际传输列表计算首页，避免引用不在列表内的任务。公开回复与阅读选择互不覆盖。
3. CLI 缺失、代理握手前退出、超时、拒绝、不兼容数据与后续断线给出分别可辨认的本机提示。不显示原始异常、stderr、路径、提示词或 token；失败保留最近公开数据并清掉可操作控制。

四项 XCTest 回归覆盖状态兼容、首页／受限列表、断线缓存与隐私、CLI／代理失败区分。另用实际 Debug 对象在原创合成目录验证状态、首页与阅读任务保留、缓存和公开内容边界，并确认本机真实代理的安全失败分类；这不等于 XCTest 或实机验收。本机 Release 通过；XCTest 仍在执行前因缺少模块受阻，完整测试以本轮 Mac CI 为准。

## 首页为什么显示电脑状态

已核对运行 `.234` 精确源码 `ui.c` 的 `render_home_task`：缺少有效 `overviewTask`／任务列表时，同一首页卡片回退 CPU、内存及“电脑状态”；有有效任务才显示任务标题与继续 Codex 入口。这与普通后台 Codex 活跃统计独立。现场 Codex 功能开关已恢复关闭，而且现有代理未连接，所以没有可用任务。没有改动首页布局或用户展示设置。

## 剩余条件

需要已有桌面服务提供可附接的公开 App Server 端点，才能继续真实任务列表、精确选择、公开回复、草稿与送达验收。当前不通过另建 daemon、部署服务或改用内部 IPC 绕过该条件。真实任务发送仍需要用户指定任务；所有发送、桌面输入框及语音开关保持关闭。

## English

The installed `0.163.0-alpha.10.2` public proxy still exits before initialization because its default control socket is absent; read-only daemon version probing confirms ENOENT. The desktop child uses stdio. No daemon, internal IPC, raw rollout access, user configuration change or real-task mutation was used. A short-lived CLI process under an empty temporary CODEX_HOME passed initialization/empty list and rejected a missing synthetic thread, then exited and was removed. This isolated probe is not attachment to the user's desktop service.

App Server active/empty flags now maps to existing TAB5 working; approval/input waits map to waiting. Idle stays idle; unloaded/error/future states remain unknown. The home card prefers a working task and bounded snapshots preserve it alongside the reader when capacity permits; one-slot snapshots retain the reader with an internally consistent overview. Safe connection errors distinguish missing CLI, pre-handshake exit, timeout, rejection, invalid data and disconnect, retain cached public data and suppress controls without exposing diagnostics or secrets. Four new regressions cover these paths. Actual Debug-object synthetic checks and local Release passed; local XCTest is still blocked before execution and full tests depend on Mac CI.

The exact running .234 source intentionally falls back to CPU/memory on the home task card when no valid task exists. The feature is currently off and the proxy unavailable, so no task is projected; the layout was not changed. A public attachable desktop endpoint is still required for real catalog/reply/draft/delivery acceptance. No real test task was designated and no send, steer or interrupt was performed. Feature switches and device display preferences are preserved.
