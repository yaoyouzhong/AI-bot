# Mac Claude Code 后台会话接入

Windows 普通终端通过控制台输入 API 向原会话发送消息，Windows 后台适配器通过 Claude 自己的控制通道发送。Mac 接入 Claude 自带的后台服务。TAB5 可以向已启动并验证身份的空闲后台会话发送文字。普通 Terminal/iTerm2 会话可以查看历史。

在原项目目录下，等原会话空闲后退出 Claude，再运行 `claude --bg --resume <会话 UUID>`。使用完整 UUID 续接已有历史，并在 `claude agents --json` 确认后台项的 `sessionId` 与原会话一致。桥接自动发现已经运行的后台会话。该版本的适配器验证范围为 Claude Code 2.1.318 的本机安装和默认配置目录。工具审批继续由 Claude 处理。[Claude Code 后台会话文档](https://code.claude.com/docs/en/agent-view#dispatch-an-agent-from-your-shell)

## 界面

TAB5 沿用 Claude 页签、原标题和会话别名。后台会话使用 Windows 已有的「后台会话」来源徽标和琥珀色样式，保持 1280×720 横屏的 80px 会话行、字号、留白、截断和按钮位置。已验证的空闲后台会话启用文字发送。等待工具审批、身份冲突或连接不可用时，原回复仍可查看。送达未知时保留草稿并显示原回执状态。Mac 设置页沿用现有位置、字体和说明文字层级，介绍后台会话与普通终端的能力。继续使用 v1 状态和 RPC 字段。

## 派发

以用户 UID、Claude 可执行文件、进程代次、会话 UUID、后台 job 和 Unix socket 对端 PID 共同确定目标。派发前验证权限和轮次，写入现有的请求记录。Claude 的后台服务收到控制帧后，还要在同一会话的主分支找到唯一新增、内容匹配的真实用户记录，桥接才确认送达。适配器仅接受文字发送，运行中追加、图片和停止的能力暂未启用。

## 验证

```bash
swift test --package-path mac-app
python3 tools/probe_mac_claude_background.py
swift build -c release --package-path mac-app
```

隔离试验用已安装的 Claude Code 2.1.318 和本机模拟模型，验证原 UUID 续接、TAB5 设备接口重复提交只产生一条原生用户记录、完成回复可从 TAB5 读取接口取回。模拟模型在回环地址，Claude 进程的沙箱限制外连。实际 TAB5 从此后台路径发送真实模型消息的验收待进行。
