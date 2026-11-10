# Windows Claude 终端与 TAB5 对齐交接（2026-11-08）

## 范围和源码身份

- 来源 macOS / TAB5，接收 Windows；遵循[桌面统一流程](../DESKTOP-DEVELOPMENT-RELEASE.md)。交接分支 `codex/mac-native-claude-alignment-20261107`，本文件所在提交的完整 SHA 可用 `git rev-parse HEAD` 取得。构建已安装 Mac 候选时的基线是 `4b123377be61fa281dadc315604bd9a03e403448` 加当时未提交的改动；不能声称安装包由本交接提交的干净源码构建。
- 用户授权提交并推送交接分支，没有授权合并、标签或正式发布。此交接不指定发布负责人，也不构成发布候选晋级。Mac 桥接版本 0.6.18，TAB5 本地镜像版本 0.2.318-ui；本次不改协议版本，不要求其他组件重编号。
- Windows 先保存自己的未提交工作，再获取交接分支并按统一流程建立适当工作区，切勿覆盖已有修改：

  ```powershell
  git fetch origin +refs/heads/codex/mac-native-claude-alignment-20261107:refs/remotes/origin/codex/mac-native-claude-alignment-20261107
  git rev-parse origin/codex/mac-native-claude-alignment-20261107
  ```

  指定 ref 也可避开接收端可能存在的失效远端跟踪配置；接收端应核对该 SHA 和自身工作区后再决定如何整合。
- Mac AppleScript、TTY 和 `-1728` 错误处理是平台专用实现，不直接移植到 Windows。共同用户行为是自动定位指定的普通 Claude Code 终端；必要时自动激活，不要求用户持续将窗口置前；匹配不明确时保留草稿、拒绝发送，不重复提交。

## 对齐清单

| 优先级 / 用户行为 | 来源及接收端位置 | 状态 | 证据与后续 |
| --- | --- | --- | --- |
| P1：真实消息已送达但回执未确认 | Mac `MacClaudeHistory.swift`；Windows `ClaudeHistory.cs` | 待验证 | Mac 将 `system/stop_hook_summary` 作为只保留身份和父链的上下文节点；Windows 当前允许的 system 节点列表不含该类型。Windows 需按自身实现补齐并测试，不能据此断言已发生 Windows 实机故障。保留会话和作用域绑定、单条新输入、图片完整链与 digest 校验；不得暴露 hook 内容、放宽未知节点保护或重发待确认消息。 |
| P1：普通终端后台识别、读取、发送、追加及停止 | Mac `MacClaudeTerminalWindow.swift`；Windows 原生终端和 `ClaudeNativeReceipt` 相关实现 | 待验证 | Mac 隔离模型和真实 TAB5 USB 验收记录见[定位恢复](MAC-TERMINAL-LOCATE-RECOVERY-20261108.md)。Windows 应原生验证目标自动定位、必要时自动激活、歧义拒绝、输入框草稿保护、去重及停止；USB 可用于自动化，即使被测业务通道为无线也不要以手工点击作为前提。不要操作真实用户会话。 |
| P2：运行中最新回复的显示延迟 | [TAB5 补丁和验收](TAB5-CLAUDE-REPLY-REFRESH-20261108.md)；Windows `Tab5Service.cs`、`Tab5CodexTasks.cs` | 待验证 | TAB5 最新 Claude 回复运行中成功读取后 750 ms 再读；完成、隐藏或未知维持 3000 ms、历史页 15000 ms、429 重试 200 ms。Mac Claude 预读新鲜度 500 ms，Codex 仍为 1000 ms。Windows 没有该 Mac 缓存实现，应测新鲜度、单个在途请求与 USB/Wi-Fi/BLE 负载，不能把 Mac 测量当成 Windows 结果。 |
| 萌宠、Claude 名称与最近活动 | `MacClaudeSessionIndex.swift`、`ClaudeSessionIndex.cs`、两端 `Tab5Service` 与 `Tab5Protocol.cs`；[设备策略](TAB5-COMPANION-SELECTION-DESIGN-20261104.md) | 待验证 | 旧总目录的核心设计已进入交接分支并继续演进；Windows 源码包含可选会话名称、活动时间和 provider 投影，设备策略补丁也已跟踪。精确绑定和缺失数据仍须遵守现行协议，名称不能借用其他会话，未绑定的 Stop 不能推断成功。Windows 原生和受影响设备行为需分别验收。 |
| 蓝牙首次追加 409、iTerm2、VS Code | [历史记录](TAB5-REMAINING-ACCEPTANCE-20261108.md) | 待验证 | 用户决定蓝牙问题下次复现时再定位，iTerm2 / VS Code 继续暂缓。历史 409 缺少原始系统错误码，不能归因于蓝牙或宣布修复。音乐 Automation 播放/暂停在最近一次系统事件权限变化后未重新专项验证。 |

## 接收端回归与真机验收

Windows 先补 `ClaudeHistorySelfTest.cs`：覆盖该 system 节点处于合法父链、跨会话/错误父链、sidechain、未知节点、旧与新记录及文本和带图确认；校验不泄露 hook 内容、不绕过图片 digest，也不自动重发。执行下列项目规则要求的原生构建和状态检查，以及针对性测试：

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --self-test-claude-history
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --self-test-claude-session-index
```

使用隔离会话和本机模拟模型验证真实设备链路、图文送达、追加和停止，记录窗口匹配、单次提交、回执及最终实屏；覆盖草稿和配对保留。设备自动点按可通过 USB 执行，并分别核实实际业务使用的 USB、Wi-Fi、BLE 通道；不同通道结果不能互相代替。测试必要时可自动切换目标窗口，先保护用户草稿。按实际变更范围补充回归并明确记录未测项，编译或夹具通过不等于实机通过。

## 构建物与验收记录

| 平台 | 实际候选与身份 | 已验证 / 限制 |
| --- | --- | --- |
| macOS | 已安装 `/Applications/AIBotBridge.app` 0.6.18；主可执行文件 SHA-256 `a8bb700d24aa8369ddd9ce31086cdfb0e3c936fc4944c80bf518d564902107b6` | `swift test` 503 项、4 跳过、0 失败，Release 构建和签名检查通过；安装包基于上述脏工作区，非共同干净源码候选。 |
| TAB5 | 用户授权 USB 安装的 0.2.318-ui 本地候选；BIN 6,997,936 字节，SHA-256 `090d42a61077ea448f268a60e59ff6c0891d90c18a4a22211b33c9fff2d7505e`；ELF SHA-256 `e43f2e207b79c4a502bb0c9bd2c766b3716343a19125c9ff60bdc65edd669d19` | 启动分区、实屏和设置核对通过。两次隔离 USB 测量从模型响应完成到设备已读确认分别约 1839/1849 ms，包含 CLI、通信与采样；无同条件旧版基线，不计算提速倍数。Wi-Fi、BLE 性能及 Windows 未测。 |
| Windows | 本交接未构建 Windows 原生候选 | 待 Windows 原机构建、安装及对应实机验收；不要使用 Mac 的结果代签。 |

设备升级前后展示设置一致，升级器报告草稿已保存且检查到一份非空草稿；加密 journal 的摘要发生变化，升级前未记录解密内容摘要，不能宣称草稿内容逐字节一致。证据位于来源 Mac 的忽略目录 `artifacts/tab5-reply-refresh/`，不是 Git 附件；不得提交真实会话、凭据或本地运行数据。

TAB5 刷新补丁所用前置工程是本机忽略目录中的 `f52945c59b392070b33c72c113646cca33a1e21c31f6b68af6fa91a22b3120f6` 镜像对应源码，仓库已有 `.318` 源码归档对应更早的镜像。现已提供[完整源码增量和重建步骤](tab5-318-refresh-source/README.md)，从归档一次性还原最新版的 20 个源码和测试文件及系统语音素材并核对摘要，无须推测历史补丁顺序。Apple 系统语音许可限制及用户提交决定见该目录的来源说明；不能把新构建当作已验收原包。Windows 桥接和 TAB5 可独立发布。

## 发布交接

本次仅交付源码及文档，无共同候选 SHA、标签、草稿附件或正式发布。以后发布时由唯一负责人按统一流程，从同一候选源码分别构建、验收各平台原包并记录签名、许可与摘要；已有平台的正式版本和下载入口按实际发布结果保留。首项行动：Windows 补 `stop_hook_summary` 父链识别及有针对性的原生回归，之后完成真实设备验收。
