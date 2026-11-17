# Claude 会话与 Codex 已读状态：按能力和字段对齐

2026-11-17，Mac／Windows 共用开发分支 `codex/tab5-calendar-and-claude-idle-20261116`。本轮是本地开发与验收，不包含推送和发布。继续使用既有 TAB5 固件与 v1 协议。

| 路径 | 共同要求与平台实现 |
| --- | --- |
| Claude 后台会话 | 不比较 Claude Code 发行版本。后台 CLI 记录和 daemon 任务必须指向同一个会话和任务；同时核对可执行文件、当前用户和真实进程代次。真正写入前检查已核验的 daemon socket／命名管道对端、私有控制密钥、当前任务状态与授权；继续使用已知请求结构，拒绝不兼容响应，送达以原会话回执确认，未知不重发。平台原有的发送、追加、中断范围不变。 |
| Claude VS Code | Windows 既有入口已按进程、面板及控件判断。Mac 移除发行版本白名单，要求互动会话记录有有效 UUID、PID、启动时间和状态；绑定真实 VS Code 宿主，要求扩展数据库中有唯一的同会话记录。执行前继续核对选中面板、前台、系统权限、输入框和按钮可用性，并保留草稿冲突和来源冲突保护。 |
| Codex 已读状态 | 读取桌面当前已知的 `electron-thread-read-state-v1` 存储入口；不要求对象内部存在固定 `version` 字段。只接受当前有效账号和本地主机的 UUID 数组，最多 8192 条。存储入口变化或结构不认识时，继续保留未知；不会当成清空，也不会扫描其他账号、主机或未知命名空间。已有已读通知请求格式不变。 |

验收分开登记：Mac 自测、真实已安装 Claude CLI 接回环模型、实体 TAB5；Windows 交叉编译、Windows 原生自测和实体 TAB5。未运行的步骤不记作通过。旧历史记录中的 2.1.314／2.1.318／2.1.319 仍指当时测试过的版本，不是当前接入条件。

Windows 接收端在同一候选源码运行：

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --status-once
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-codex-read-sync
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-claude-workspace
```

实体 TAB5 用专用测试会话。创建或继续一个已有可辨识 UUID 的空闲 Claude 后台会话，再从设备发送一次不调用工具的测试文字；核对原会话收到一次、设备出现公开回复，另检查错误身份或能力不足时保留草稿。Windows VS Code 可验证同一活动面板、隔离草稿和一次回执；Mac 没有已安装的正常 VS Code/插件环境时，该项单独保留待实测。
