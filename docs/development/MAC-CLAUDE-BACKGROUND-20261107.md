# Mac Claude Code 后台会话接入

Windows 普通终端通过控制台输入 API 向原会话发送消息，Windows 后台适配器通过 Claude 自己的控制通道发送。Mac 接入 Claude 自带的后台服务。TAB5 可以向已启动并验证身份的空闲后台会话发送文字和图片。普通 Terminal/iTerm2 会话可以查看历史。

在原项目目录下，等原会话空闲后退出 Claude，再运行 `claude --bg --resume <会话 UUID>`。使用完整 UUID 续接已有历史，并在 `claude agents --json` 确认后台项的 `sessionId` 与原会话一致。桥接自动发现已经运行的后台会话。该版本的适配器验证范围为 Claude Code 2.1.318 的本机安装和默认配置目录。工具审批继续由 Claude 处理。[Claude Code 后台会话文档](https://code.claude.com/docs/en/agent-view#dispatch-an-agent-from-your-shell)

## 界面

TAB5 沿用 Claude 页签、原标题和会话别名。后台会话使用 Windows 已有的「后台会话」来源徽标和琥珀色样式，保持 1280×720 横屏的 80px 会话行、字号、留白、截断和按钮位置。已验证的空闲后台会话沿用现有拍照按钮、草稿图片区和发送按钮。支持只发图片，也可附加文字，同一条最多三张 PNG 或 JPEG，每张不超过 2 MiB。沿用现有触控区、字号、颜色及上传状态，照片没有独立提交入口。等待工具审批、身份冲突或连接不可用时，原回复仍可查看。送达未知时保留草稿并显示原回执状态。Mac 设置页沿用现有位置、11–12 pt 说明文字层级和系统次级文字色，补充后台会话支持图片。按默认和最小窗口、亮色和暗色检查说明，保持与现有按钮的间距。继续使用 v1 状态和 RPC 字段。

## 派发

以用户 UID、Claude 可执行文件、进程代次、会话 UUID、后台 job 和 Unix socket 对端 PID 共同确定目标。派发前验证权限和轮次，写入现有的请求记录。Claude 的后台服务收到控制帧后，还要在同一会话的主分支找到唯一新增、内容匹配的真实用户记录，桥接才确认送达。图片沿用配对设备和会话绑定的加密草稿，通过 Claude 的本地文件引用提交。桥接以原生历史里同一用户消息下的图片附件、引用顺序和实际内容摘要确认送达。图片只在派发时写入本机用户可访问的临时目录。送达确认或派发前拒绝后删除临时文件。送达未知时保留最多十分钟供 Claude 读取，原设备草稿和回执继续由加密存储管理。当前图片派发只用于空闲会话。

## 验证

```bash
swift test --package-path mac-app
python3 tools/probe_mac_claude_background.py
swift build -c release --package-path mac-app
```

隔离试验用已安装的 Claude Code 2.1.318 和回环模拟模型，验证原 UUID 续接、TAB5 加密图片上传与发送、带文字三图、纯图、同请求重放和完成回复读取。模拟模型逐张核对 PNG、JPEG 的原始字节摘要和顺序，并等到送达回执生成后再返回回复。缺图、错误图片和分叉附件无法确认送达。AppKit 和 1280×720 LVGL 离屏预览检查已通过。真实 TAB5 的图片往返待验。
