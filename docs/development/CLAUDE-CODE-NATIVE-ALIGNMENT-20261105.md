# Claude Code 原会话接入修正

## 用户确定的目标

Claude Code 使用与 Codex 相同的产品逻辑：TAB5 定位、读取及操作原客户端中的明确会话，权限确认由该会话原有界面承接。移除桥接顶层常驻“Claude 操作确认…”入口，不另起一条由桥接代管审批的对话。终端 Claude Code 和 VS Code Claude Code 插件均在支持范围内，不能靠窗口标题或同一项目猜测会话。

此前 .617／.618 候选的 `ClaudeStream` 使用 `--print --permission-prompt-tool stdio --resume=<id>`，是桥接自建的非交互执行进程；这不满足本次确定的原会话接入。当前开始替换该发送后端，不能仅删除菜单后让旧后台进程失去审批路径。旧原包、源码和验收保留为原范围证据，不能套用到新实现；`778e433` 桥接候选暂不推进发布。TAB5 .318 原包及其独立界面验收继续保留。

## 会话、交互与验证约束

- 对齐 Codex 的精确会话归属、原模型／权限设置、已有草稿保护、显式一次发送和送达核对。新输入、附件、当前轮次或宿主变化时终止原操作；送达不明不自动重发、不自动切换另一执行后端。
- 终端版与 VS Code 插件按同一 Claude 会话 UUID 识别具体宿主，各自使用可验证的原生交互通道；原生会话不存在、身份有歧义或通道不兼容时保留草稿并给出明确状态。不得自动创建 stream-json 会话作为回退。
- 录音回填、照片、追加与中断的实际支持必须按新链路验证，不能沿用旧自建 CLI 的成功结论。公开回复读取和不受影响的加密草稿可复用，但仍要保持设备／客户端／会话绑定。
- 权限请求保留在原 Claude Code 会话中，TAB5 显示已有的等待电脑确认状态；不在桥接生成独立工具审批窗口，不自动批准，不修改用户权限模式、模型、Claude 配置或全局设置。
- 桌面诊断保持后台，只读识别会话和输入区，不切换／置顶窗口。需要打开客户端时由用户自行操作；只有明确设备操作才可执行已绑定的原生发送。
- CLI 本地会话消息接口是跨会话消息，不可冒充用户授权或把消息送达当成工具授权；没有核验其语义前，不以它替代原生用户输入。

## 已核对的现场证据

Codex 默认桌面路径通过原客户端 IPC／原生输入框发送，`Tab5CodexDesktop` 明确禁止为桌面会话另起独立 CLI；任务审批回到桌面。Claude 旧实现存在差异，已确认需修正。

本机终端 CLI 为 2.1.318，已安装 VS Code 插件为 2.1.314。用户已打开终端 Claude Code 并确认也使用 VS Code 插件。只读探测验证了原生会话记录；首次终端读取因原 CLI 为管理员完整性级别 12288、普通桥接辅助进程为 8192，Windows 返回错误 5。未写入输入、未改变前台、会话身份保持。已请求用户保留原管理员会话并另开普通权限终端；不提升权限或绕过系统保护。

当前为接入探测与实现阶段，尚未完成新的原生发送、权限衔接、终端／插件双宿主验收，不能标记为已修复或可发布。探测证据在 `artifacts/claude-code-native-alignment-20261105/`，私人会话数据不进入 Git 或发布附件。

## 公开依据

- [Claude Code CLI](https://code.claude.com/docs/en/cli-reference)：交互模式、print 模式和会话恢复的区别。
- [原生会话枚举](https://code.claude.com/docs/en/agent-view#list-sessions-as-json)：`agents --json` 的身份、状态和待输入字段。
- [会话消息语义](https://code.claude.com/docs/en/cross-session-messaging#how-a-session-treats-an-incoming-message)：消息与原会话权限的边界。

## English

Claude Code must follow the existing Codex product model: operate on the explicitly selected native conversation and keep permissions in its original interface. Cover terminal and VS Code hosts, preserve exact identity, drafts, attachments, request receipts and unknown-delivery protection, and remove the bridge-owned approval workflow only when its replacement is functional. The earlier print/stdio candidate does not establish acceptance for this correction. Initial read-only probing identified an elevated terminal that Windows correctly refused to expose to a medium-integrity helper; no input or foreground change occurred. Native sending and both host paths remain unaccepted at this stage.
