# Claude 会话与 Codex 已读状态：按能力和字段对齐

2026-11-17，Mac／Windows 共用开发分支 `codex/tab5-calendar-and-claude-idle-20261116`。本轮是本地开发与验收，不包含推送和发布。继续使用既有 TAB5 固件与 v1 协议。

| 路径 | 共同要求与平台实现 |
| --- | --- |
| Claude 后台会话 | 不比较 Claude Code 发行版本。后台 CLI 记录和 daemon 任务必须指向同一个会话和任务；同时核对可执行文件、当前用户和真实进程代次。真正写入前检查已核验的 daemon socket／命名管道对端、私有控制密钥、当前任务状态与授权；继续使用已知请求结构，拒绝不兼容响应，送达以原会话回执确认，未知不重发。平台原有的发送、追加、中断范围不变。 |
| Claude VS Code | Windows 既有入口已按进程、面板及控件判断。Mac 移除发行版本白名单，要求互动会话记录有有效 UUID、PID、启动时间和状态；绑定真实 VS Code 宿主，要求扩展数据库中有唯一的同会话记录。执行前继续核对选中面板、前台、系统权限、输入框和按钮可用性，并保留草稿冲突和来源冲突保护。 |
| Codex 已读状态 | 读取桌面当前已知的 `electron-thread-read-state-v1` 存储入口；不要求对象内部存在固定 `version` 字段。只接受当前有效账号和本地主机的 UUID 数组，最多 8192 条。存储入口变化或结构不认识时，继续保留未知；不会当成清空，也不会扫描其他账号、主机或未知命名空间。已有已读通知请求格式不变。 |

验收分开登记：Mac 自测、真实已安装 Claude CLI 接回环模型、实体 TAB5；Windows 交叉编译、Windows 原生自测和实体 TAB5。未运行的步骤不记作通过。旧历史记录中的 2.1.314／2.1.318／2.1.319 仍指当时测试过的版本，不是当前接入条件。

候选源码为 `31ed9cbf73e702db67037945d5a0a1d62c8a72dd`。这份记录和验收探针的后续修改没有改变待测的桌面程序。当前只做开发交接，没有正式发布负责人、标签或上传。

| 环境 | 结果与证据 | 待验证 |
| --- | --- | --- |
| Mac 自测与本机后台 | `swift test` 执行 541 项，4 项跳过，0 失败；桥接自测 29,178 项通过。隔离运行已安装的 Claude Code 2.1.325 和本地模型，核对同一会话、原图字节、回执和防重复。 | 未安装 VS Code 和 Claude 扩展的机器只完成结构回归；真实插件面板待验。Codex 已读解绑完成自动测试，未用个人会话改写已读状态。 |
| Mac + 实体 TAB5 | arm64 本地签名 Release 应用（组件号 0.6.23），`AIBotBridge` 可执行文件 14,388,240 字节，SHA-256 `88026d4335638c745f1c9df40848e754f158f6446a8cb1d74621925ade9b3067`。TAB5 R6 `0.2.322-ui` 经 USB 自动操作：看到后台会话，在设备上发送隔离草稿，出现新回复并能打开。原会话记录只新增一条用户消息。自动触摸有一次回执未确认，凭会话和设备结果确认，未重发。既有设备设置保留，恢复原桥接进程。真机日志和截图：本机 `artifacts/claude-capability-alignment-20261117/device-confirmed*`。 | 此项是后台会话链路验收，语音草稿与模型回复由测试程序提供；未覆盖线上模型、麦克风质量或安装升级。 |
| Windows | 交叉编译成功，零错误、零警告。既有 VS Code 定位按能力判断，无需另加一套规则。 | 同一候选的 Windows 原生自测、真实后台会话、VS Code 面板和实体 TAB5 待验。交叉编译不满足发布门槛。 |

Windows 接收端先核对 `git rev-parse HEAD` 为上面的候选 SHA，在同一候选源码运行：

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --status-once
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-codex-read-sync
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-claude-workspace
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-claude-native-workspace
```

实体 TAB5 用专用测试会话。创建或继续一个已有可辨识 UUID 的空闲 Claude 后台会话，再从设备发送一次不调用工具的测试文字；核对原会话收到一次、设备出现公开回复，另检查错误身份或能力不足时保留草稿。Windows VS Code 可验证同一活动面板、隔离草稿和一次回执；Mac 没有已安装的正常 VS Code/插件环境时，该项单独保留待实测。
