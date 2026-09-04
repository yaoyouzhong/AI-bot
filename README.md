# AI-bot

AI-bot 是一个本地优先的 AI 状态桌面时钟。目标产品包含 Claude / Codex 活动和账户额度、国产模型额度、天气、股票、系统监控、音乐、桌宠、屏保、USB 优先、Wi-Fi 回退、Windows 托盘以及 macOS 菜单栏桥接。

当前 `0.1.0` 是独立重实现的开发基线，只完成了本机会话活动 → Windows 桥接 → `@AIBOT` USB 协议 → ESP8266 屏幕这一最小闭环。完整功能是硬性范围，不是可选路线图；各模块的当前证据状态见 [功能对齐合同](docs/FUNCTIONAL_PARITY.md)。在矩阵达到对应验收级别前，不应把 `0.1.0` 当作旧产品的功能替代品。

## 功能

- 自动判断 Codex、Claude Code 的 `working`、`idle`、`offline` 状态。
- `127.0.0.1:8765/status` 本地只读状态接口。
- 460800 波特率 USB 串口通信。
- ESP8266 串口握手、状态显示和 8 秒离线判定。
- 不读取或上传 OAuth token、API key、Cookie 和对话正文。
- Windows、固件 CI 与标签驱动的候选发布流水线。

尚未完成的天气、股票、账户额度、国产额度、桌宠、屏保、Wi-Fi 回退和 macOS 功能已经纳入强制验收范围。README 只描述当前已实现能力，进度以功能矩阵为准。

## 目录

```text
windows-app/AIBotBridge/  Windows .NET 8 托盘桥接
mac-app/                  macOS 菜单栏桥接（待独立重实现）
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

本地状态接口默认使用 `127.0.0.1:8765`；测试或端口冲突时可通过 `AIBOT_HTTP_PORT` 调整。程序还会在选中的私有局域网地址上启动设备专用接口，但必须携带由 USB 配对下发的随机令牌；未认证请求返回 401。

## 固件

```powershell
python -m platformio run -d firmware
python -m platformio run -d firmware -t upload --upload-port COM7
```

上传固件前退出 Windows 桥接，避免串口被占用。刷写后应验证握手、状态刷新和拔掉 USB 后的离线页面。

## 隐私边界

桥接程序只查看会话日志文件的最后修改时间，不读取对话内容。本机调试接口只监听回环地址；Wi-Fi 回退接口只绑定选中的私有网卡地址并强制校验配对令牌。Windows 侧令牌使用当前用户 DPAPI 加密保存，设备侧通过已握手 USB 接收并保存，不进入源码、JSON 设置或日志。

## 许可证

AI-bot 自有源码使用 [MIT License](LICENSE)。第三方依赖保持各自许可证，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。项目来源说明见 [PROVENANCE.md](PROVENANCE.md)。
