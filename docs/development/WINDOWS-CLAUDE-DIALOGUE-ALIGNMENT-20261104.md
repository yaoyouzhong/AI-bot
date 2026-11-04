# Windows 接收 Claude / Codex 对话对齐（2026-11-04）

遵循[统一流程](../DESKTOP-DEVELOPMENT-RELEASE.md)。本次只核对实际变更及受影响路径；未修改的功能沿用已有实现和验收，不重新打开历史清单。

## 源码与交接边界

- 来源平台为 macOS，接收平台为 Windows 10/11。分支 `codex/tab5-claude-dialogue`，基线 `0dc59134626e110f25e427bc6d7c4f61e64a8a85`；接收目标为包含本文的本地提交，可用 `git log -1 --format=%H -- docs/development/WINDOWS-CLAUDE-DIALOGUE-ALIGNMENT-20261104.md` 取得完整 SHA。
- Mac 交接已推送至上述分支，Windows 本次实际接收提交为 `9973f9b25083c0d35a696b947f55dd2327587820`，基于它完成原生验证和修正。历史记录中的“尚未推送”是 Mac 当时的本地阶段，不能作为当前远端状态。
- 本次 Windows 对端代码已经同时实现，接收端以原生验证和必要修正为主，不需要把 Swift 整套重写为 C#。不能把 Mac 实测或 Windows 交叉编译写成 Windows 原生通过。
- 桥接 VERSION 保持 0.6.17，新增能力仍是本地候选；TAB5 独立候选为 0.2.313-ui。ESP8266 未修改，不刷机、不增加小屏验收。
- 共同设计与完整实现记录见[对话设计](CLAUDE-WORKSPACE-PARITY-DESIGN-20261104.md)、[本轮结果](CLAUDE-DIALOGUE-HANDOFF-20261104.md)。

## 已对齐的共同用户行为

1. 底部入口及页面标题统一为“对话”，字体沿用共用标题样式。仅有一个客户端时显示单按钮；两个都有时显示两个。
2. 总览直接进入该客户端、该会话的明细，不先进入项目列表，也不替换为最近会话；常规入口显示当前轮次/最新回复，未读入口定位精确轮次。从总览进入后返回总览，从列表进入后返回列表。
3. 底部“对话”进入项目和历史列表。按客户端、设备、会话保留草稿、附件、回执与语音归属；忙时保护已有操作。
4. Claude 支持项目/历史、公开回复、草稿、文字/图片发送、排队追加和中断。未知送达只核对，不自动重发；取消排队保留草稿。外部仍运行的 Claude 会话只读，桥接只控制自己持有的 CLI 实例。
5. 完成状态只采信同一会话的精确 CLI 结果；旧结果不得覆盖新提示或其他会话的待确认状态。实际显示对应回复后才确认已读，回执固定携带客户端。
6. 现代桥接目录暂缺时显示“对话待同步”，不退入旧 Claude 只读页。旧桥接仍保留兼容路径。
7. Claude 待命采用原始无道具小人的轻微呼吸，已由共享固件完成；电脑端不重复绘制。

协议继续使用 version=1。新增可选 `claudeTasks`、`aiDialogueVersion=1`，相关 RPC 的 `client=codex|claude` 缺省仍为 Codex；参见[协议](../PROTOCOL.md)。Claude 原始 UUID、路径、用户提示、推理和工具内容留在电脑，设备只使用不透明别名和按需读取的助手公开回复。

## 本次 Windows 核对清单

以下“待验证”仅指 Windows 本次新增或修改的路径，不代表需要重测整个产品。

| ID / 优先级 | 行为及实现位置 | 当前状态 / 已有证据 | Windows 接收端工作 |
| --- | --- | --- | --- |
| W01 / P0 | `ClaudeSessionIndex.cs`、`ClaudeCatalog.cs`、`ClaudeHistory.cs`：项目/历史、公开回复、别名及失败保留 | 已完成（代码、C# 离线自测、交叉编译）；原生待验证 | 原生运行自检，核对安装版 CLI 路径、精确 resume 与目录解析；不保存真实 agents 原始输出 |
| W02 / P0 | `ClaudeStream.cs`、`ClaudeWorkspace.cs`：发送、追加、中断、精确回执与每设备 DPAPI 日志 | 已完成（对端代码及交叉编译）；Windows 原生待验证 | 用隔离合成会话核对文字/PNG、重复请求、未知送达只核对、排队取消保留草稿及中断；确认不同设备/客户端不串草稿、附件和回执 |
| W03 / P0 | `ClaudeIdentity.cs`、`ActivitySignals.cs`、`Tab5Service.cs`：同会话状态、精确总览别名及已读 | 已完成（C# 离线身份回归、Mac/共享固件真机）；Windows 链路待验证 | 对同一固件核对 Windows 发出的别名、状态与 client 回执；不重验动画、排版、字体、翻页控件和返回布局 |
| W04 / P1 | `Tab5Rpc.cs`、`Tab5Service.cs`、`Tab5VoiceSession.cs`：图片分块与语音目标增加 client 绑定 | 已完成（代码、交叉编译）；Windows 路由待验证 | 只验证 Claude/Codex 目标归属、切换保护和旧请求拒绝；复用原识别后端、相机及音频硬件已有结果，不重测识别准确性或拍摄质量 |
| W05 / P1 | `ClaudeApprovalForm.cs`、`TrayApplicationContext.cs`：Claude 手工工具确认 | 已完成（代码及交叉编译）；Windows 原生待验证 | 核对合成请求的原生渲染、拒绝默认、允许本次及过期拒绝；不自动打开/置顶用户窗口，不自动批准工具 |
| W06 / P1 | `Tab5UIAutomation.cs` 与服务命令/应答引擎 | 已完成（代码及 C# 自检）；Windows 真机调用待验证 | 复用共享设备命令协议，只补 Windows 对端调用；Mac JSONL 交互 CLI 不是已完成的 Windows CLI，不能把其参数直接用于 Windows exe |
| W07 / P1 | 原有 Codex、配对和资料格式 | 已有无需修改；共享工作区/路由修改需定向防回归 | 运行下面的现有 Codex/RPC 自检；不重新要求已验收的全部日常使用场景 |
| W08 | 共享 TAB5 直达明细、返回、标题、客户端按钮、待命形象及 read-ack client | 已完成（实际 LVGL 及最终镜像 USB 真机） | 复用同镜像结果，不重建、不重刷；只核对 W03/W04 的 Windows 数据链路 |

Windows 的音频、输入法和 DPAPI 保留现有平台实现，不移植 Mac Keychain/AppKit。工具请求参数仅供用户本机手工核对，不进入设备或诊断日志。

## 原生验证命令与记录

在 Windows、仓库根目录执行。先检查本地未提交修改并保护已有工作；不要覆盖用户资料。以下自检使用现有隔离入口，不能用跨平台编译结果代替执行结果。

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-claude-session-index
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-claude-history
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-tab5-automation
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-codex-tasks
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-codex-read-sync
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-tab5-rpc
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -c Release --no-build -- --self-test-tab5-voice-core
git diff --check
git status --short
```

回传 W01–W07 的实际结果、必要修正、源码 SHA 和实际包 SHA-256；代码、自测、安装包与具体设备链路分开记录。只补受影响验证，不把缺项或失败记成通过。截图/日志使用合成数据，不提交凭据、真实对话或运行状态。普通自动化不抢占电脑前台，手工界面由用户自行打开。

## 可复用结果与发布边界

- Mac 已通过原生 462 项测试（1 条环境门控跳过，0 失败）、29,068 条隔离 parity 检查、Release 构建及本地签名。门控的真实 CLI 路径另行通过 loopback 文字、PNG、resume、排队与中断验证。
- Windows 已交叉编译通过，0 warning / 0 error；身份、目录、历史、阅读与自动化 C# 离线回归通过。这些是接收基线，不是 Windows 原生安装/设备结果。
- 共享 TAB5 最终镜像为 `e0c0981beae8525e74fc1aeb3880904aeb95df909d63f5b9a39162ec72deb587`，6,923,744 字节；精确 ELF 为 `e5f52c3aff72315dec130342b4a16705b3205ba60da3cb41bc367ba4ffeb8ef7`。已验证 VALID 分区、实时 UI、两端入口、已读、目录暂缺、待命形象及设置保留。文件仍在固定 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，不把同版本旧镜像当成它。
- 已补齐[完整 .313 工程](tab5-313-source/README.md)：999 个项目文件、7,600 项重建输入摘要、依赖覆盖与 SDK 补丁，无需拼接旧包。25 项 Mac 增量逐项匹配[来源摘要](CLAUDE-WORKSPACE-SOURCE-MANIFEST-20261104.json)，Windows 不另行复制旧项目或用根 `firmware/` 构建 TAB5。
- 语音识别准确性、原相机功能、账户余额/消费、旧设备使用场景均未因本次交接重新成为待办。旧交接记录保留其历史含义，不作为本次新增发布门槛；本次只验上表实际变更。
- 此次只固定源码，不发布 Windows/Mac/TAB5 包。Mac 原包不因本地提交或文档变化重建。正式发布另按已有授权与统一流程选定源码、收齐受影响平台结果、核验并晋级实际原包。

Windows 接手的第一步：取得并核对包含本文的提交，在 Windows 执行原生构建及隔离自检，再按 W01–W07 补受影响链路。
