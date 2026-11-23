# Windows 桥接后台负载优化交接

## 范围和源码身份

本轮在 Windows 分支 `codex/windows-bridge-background-load` 开发，基线为 `0906c443c1d1d1e3330e45dcceaa2d474dd536ae`。同一提交包含本文件和源代码。TAB5 的状态字段、操作请求和最低固件版本沿用现有定义。

## Claude 会话与控制

- 全部活动会话继续观察运行、等待和完成状态。草稿加载和回复读取能优先检查目标终端。控制台可用时，后台对最近一次加载草稿的终端约每 20 秒复查一次，对其他终端约每 60 秒复查一次。同一终端的并发检查共享同一个助手。其他终端仍有低频复查，以支持设备切回已加载草稿时复用控制状态。
- 后台观察从会话索引定位日志文件，只为有变化的文件重建简要轮次信息。索引拒绝重复会话路径，发送、回执和显式历史读取继续实时校验日志和目标。原生操作继续核对进程身份、控制台归属、轮次和草稿。
- Windows 自动化验收覆盖并发探测、失败退避、切回低频会话、重复日志、会话重连、未读确认、发送去重和草稿保留。安装候选后，以多个现有 Claude 终端复测 TAB5 切换、继续发送和中断，并在相同会话规模下比较助手启动次数和整机 CPU。
- macOS 无需对齐 Windows 进程调度。Mac 使用自己的终端会话和中继实现，仍按现有方式报告控制能力。参见 [MacClaudeNativeBackend.swift](../../mac-app/Sources/AIBotBridge/MacClaudeNativeBackend.swift) 和 [MacClaudeTerminalRelay.swift](../../mac-app/Sources/AIBotBridge/MacClaudeTerminalRelay.swift)。

## Codex 额度历史

- Windows 直接读取旧版快照。新样本和重置卡证据立即追加，累计 256 KiB 后归并进原有 JSON 快照。快照发布后清理追加文件，重复回放按采样时间合并。统计口径、保留时间和分钟采样不变。备份和离线重算需带上快照及同目录下仍存在的追加文件。
- 已用合成数据验证旧快照读取、小增量写入、尾行中断恢复、重置卡去重、归并及归并后重复回放。样例单行 186 字节。实际 0.6.29 机器上的旧格式快照约 19.5 MiB。
- macOS 继续使用自身的 [MacQuotaHistory.swift](../../mac-app/Sources/AIBotBridge/MacQuotaHistory.swift)。两端共享的七日摘要行为无需调整。Mac 的本地写盘开销需在 Mac 上单独测量。

## 验收记录

- Windows Release 构建、`--status-once` 及 Claude 会话索引、历史、原生回执、原生发送、原生工作台、工作台协议和额度历史七项定向自测通过。
- 装有 Claude CLI 的额外回环脚本在候选与已安装的 0.6.29 上均停在首次文本发送断言。该脚本使用旧的流式测试入口，结果单独记录。
- 待验收。候选安装、TAB5 终端控制和同场景 CPU 对照。当前运行的桥接及其配置、使用中的额度文件均未替换。
