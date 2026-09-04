# AI-bot

AI-bot 是一个本地优先的 AI 编程活动桌面时钟。Windows 托盘桥接程序读取本机 Codex 与 Claude Code 会话目录的最近活动时间，通过 USB 串口把状态发送到 ESP8266 + 240x240 ST7789 屏幕。

当前 `0.1.0` 是独立实现的基础版本，重点验证最小闭环：本机会话活动 → Windows 桥接 → `@AIBOT` USB 协议 → ESP8266 屏幕。它不包含旧项目的图片、桌宠、股票、天气、额度抓取或 macOS 实现。

## 功能

- 自动判断 Codex、Claude Code 的 `working`、`idle`、`offline` 状态。
- `127.0.0.1:8765/status` 本地只读状态接口。
- 460800 波特率 USB 串口通信。
- ESP8266 串口握手、状态显示和 8 秒离线判定。
- 不读取或上传 OAuth token、API key、Cookie 和对话正文。
- Windows、固件 CI 与标签驱动的候选发布流水线。

## 目录

```text
windows-app/AIBotBridge/  Windows .NET 8 托盘桥接
firmware/                 PlatformIO + Arduino ESP8266 固件
docs/                     协议与开发说明
```

## Windows

```powershell
dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once
dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj
```

运行发布包需要安装 .NET 8 Desktop Runtime。

默认探测所有串口。可通过环境变量固定端口：

```powershell
$env:AIBOT_PORT = 'COM7'
```

本地状态接口默认使用 `127.0.0.1:8765`；测试或端口冲突时可通过 `AIBOT_HTTP_PORT` 调整。

## 固件

```powershell
python -m platformio run -d firmware
python -m platformio run -d firmware -t upload --upload-port COM7
```

上传固件前退出 Windows 桥接，避免串口被占用。刷写后应验证握手、状态刷新和拔掉 USB 后的离线页面。

## 隐私边界

桥接程序只查看会话日志文件的最后修改时间，不读取对话内容。HTTP 状态接口只监听本机回环地址。设备端第一版只支持 USB，不提供网络配置和云服务。

## 许可证

AI-bot 自有源码使用 [MIT License](LICENSE)。第三方依赖保持各自许可证，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。项目来源说明见 [PROVENANCE.md](PROVENANCE.md)。
