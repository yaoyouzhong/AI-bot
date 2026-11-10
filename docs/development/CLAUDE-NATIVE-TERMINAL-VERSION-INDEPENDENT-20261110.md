# Claude Code 原生终端：按实际会话识别

Windows 的「AI-bot-原生终端」已按原会话 ID 续接，Claude Code 2.1.322 报告空闲。旧桥接仅允许 2.1.318 和 2.1.319，返回不了发送能力，TAB5 正确显示灰色。Mac 已合入的 `8e2cba2` 对现有 Terminal/iTerm2 窗口和桥接注册终端也有相同的版本名单。

两端原生终端均取消版本名单。Windows 仍验证实际可执行文件、进程 ID、启动时间、唯一控制台、原始输入状态、当前权限和编辑区。Mac 现有窗口仍绑定实际进程、启动代次、可执行文件和前台 TTY，桥接注册的终端仍核对启动器、唯一 worker、终端组及提交前的编辑区。Mac 版本目录中的可执行文件名与记录一致用于确认文件身份，不判断该版本是否列在名单里。VS Code 和后台代理使用各自独立接口，未扩大此次适配范围。没有更改 TAB5 协议或固件。

Windows Release 构建、`--status-once`、原生工作区、发送保护、管理员回归均通过。现有 2.1.322 会话在测试候选中越过原来的版本筛选；实际进程由 Windows 报告为管理员权限，当前普通权限诊断进程不能附加其控制台。尚未替换常驻桥接，未向用户会话发送消息。

在 Mac 同步同一源码提交后运行：

```bash
swift test --package-path mac-app
swift build -c release --package-path mac-app
python3 tools/probe_mac_claude_terminal.py
```

再在实际 Terminal 或 iTerm2 重开同一隔离会话，核对 TAB5 按钮恢复、原终端收发和草稿保护。Windows 的静态检查不替代 Mac 原生构建或设备验收。
