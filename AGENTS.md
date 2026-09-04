# AI-bot project rules

本仓库是独立实现的 AI 状态桌面时钟，不复制 `esp8266-ai` 或其他无许可证项目中由第三方创作的源码、图片、文档和 Git 历史。功能验收以现有个人项目的可观察行为为基线；只有经 Git 历史确认由本项目维护者独立创作的代码，才可以在完成依赖拆分和来源记录后迁入。

## Scope

- `windows-app/`：Windows 10/11 托盘桥接程序，负责 AI 状态和额度、天气、股票、系统监控、音乐、桌宠、屏保以及 USB 优先/Wi-Fi 回退。
- `mac-app/`：macOS 菜单栏桥接程序，提供与平台能力相符的状态、额度、系统监控、音乐、股票、桌宠、设备控制和 LAN 配对功能。
- `firmware/`：ESP8266/ESP-12S 固件，负责全部显示页面、USB/Wi-Fi 双通道、独立时钟、桌宠和设备管理。
- `docs/`：协议、架构、来源和发布说明。
- `.github/workflows/`：持续集成和基于标签的候选发布流水线。

## Engineering rules

- 默认中文沟通；代码、命令、协议字段使用英文。
- 修改前先读调用方和协议对端；协议变更必须同时更新 Windows、固件与文档。
- 使用最少代码解决当前需求，不复制旧项目实现，不引入来源不明的图片或二进制资源。
- 密钥、Cookie、OAuth token、密码和本地运行状态不得进入仓库、日志或发布包。
- Windows 与设备串口速率固定为 `460800`；小消息协议为 `@AIBOT ` 加单行 JSON，协议 `version=1`。
- USB 连续 8 秒无有效心跳后必须自动恢复 Wi-Fi HTTP 轮询；桥接恢复后自动回到用户配置页面。
- 网络和供应商请求失败时保留最近一次成功的可显示数据，不能用空结果覆盖缓存。
- `tokens_today` 只描述本机可见日志；供应商账户额度必须来自供应商接口或明确的本地授权状态，二者不得混算。
- 版本源为根目录 `VERSION`；正式发布使用带注释的 `vX.Y.Z` 标签。
- README、CHANGELOG 的中英文内容必须同步。

## Required validation

Windows 代码变更：

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
```

固件代码变更：

```powershell
python -m platformio run -d firmware
```

macOS 代码变更（只能在 macOS 上执行）：

```bash
cd mac-app && swift test && swift build -c release
```

发布前：

```powershell
git diff --check
git status --short
```

编译通过不等于实机通过。涉及串口、显示或设备行为时，必须另外记录真实 ESP8266 验证结果。

## Release boundary

- 未经明确授权，不执行 `git push`、创建标签或发布 GitHub Release。
- CI 只验证；Release 工作流只响应显式推送的 `v*` 标签。
- 发布包必须附带 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和 SHA-256 校验文件。
