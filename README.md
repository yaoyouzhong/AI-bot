# AI-bot

AI-bot 是一个本地优先的 AI 状态桌面时钟。目标产品包含 Claude / Codex 活动和账户额度、国产模型额度、天气、股票、系统监控、音乐、桌宠、屏保、USB 优先、Wi-Fi 回退、Windows 托盘以及 macOS 菜单栏桥接。

当前 `0.1.0` 是独立重实现的开发基线。Windows 与固件侧已经重建主要功能链路，但真实账号、真实 ESP8266 和 macOS 仍有未完成的验收。完整功能是硬性范围，不是可选路线图；各模块的当前证据状态见 [功能对齐合同](docs/FUNCTIONAL_PARITY.md)。在矩阵达到对应验收级别前，不应把 `0.1.0` 当作旧产品的功能替代品。

## 功能

- 自动判断 Codex、Claude Code 的 `working`、`idle`、`offline` 状态。
- `127.0.0.1:8765/status` 本地只读状态接口。
- 460800 波特率 USB 串口通信。
- ESP8266 串口握手、状态显示和 8 秒离线判定。
- USB 配对令牌保护的 Wi-Fi 回退、NTP/保持时间和 `PC OFF` 独立时钟。
- Open-Meteo 天气与 A/H/美股行情数据层，网络失败时保留最近成功缓存。
- 设备天气/股票数值页、15 秒自动轮播、股票每屏 4 行和 5 秒翻页。
- Claude/Codex 账户额度解析、最近成功缓存和设备额度页；真实账号与设备待验收。
- 国产额度统一模型、四家响应解析、隔离 WebView2 登录捕获和设备总览页；MiniMax 另支持环境变量 Key 直连。
- Windows 整机 CPU、物理内存和活动网卡上下行速率每秒采样，并提供设备系统页。
- Windows 系统媒体会话标题、歌手、播放状态与进度；AUTO 播放进入音乐页，停止后恢复轮播。
- 原创建模像素桌宠 `BYTE SPROUT`，随 Claude/Codex 工作状态行走或待机，不携带旧精灵图。
- 手动屏保与空闲自动屏保；AI 开工或音乐开始时临时唤醒，键鼠恢复后回到原显示模式。
- COBS 大资源分块、逐块/整包 CRC32、ACK/重试和 LittleFS 校验后替换协议。
- 歌曲变化时预渲染 232×44 中文标题/歌手位图和 112×112 封面，经大资源协议发送并由设备逐行显示。
- 可导入带独立许可说明的 PNG/JPG/BMP/GIF 桌宠，转换为 112×112 RGB565 后通过 USB 发送；原图不进入仓库。
- 天气城市/状况和最多 20 个股票名称由 Windows 中文字体预渲染；设备按页读取，资源缺失时降级显示代码。
- Windows 托盘提供设备模式/亮度控制和九页面 240×240 镜像；镜像可用合成状态生成 PNG 验收。
- Windows 托盘显示模式控制和基于系统空闲时间的自动屏保。
- 不读取对话正文；账户额度访问令牌只从本机 CLI 登录文件读取并发往对应厂商官方域名，不进入缓存、状态、串口或日志。
- Windows、固件 CI 与标签驱动的候选发布流水线。

仍未完成的是：真实账号额度登录验收、真实设备上的 USB/大资源/页面/屏保/Wi-Fi 回退验收、Windows 设置持久化与启动项，以及 macOS 的其余功能和真机构建。README 只描述当前已实现能力，进度以功能矩阵为准。天气、股票和额度的外发边界详见[数据来源与隐私](docs/DATA_SOURCES.md)。

## 目录

```text
windows-app/AIBotBridge/  Windows .NET 8 托盘桥接
mac-app/                  macOS 菜单栏桥接（独立基础已建，平台未验证）
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

托盘菜单的“设置…”可保存天气城市/经纬度、最多 20 个股票代码、自动屏保等待时间和固定串口；配置写入 `%APPDATA%\AI-bot\settings.json`，重启桥接后生效。该文件只接受非敏感白名单字段。

## 固件

```powershell
python -m platformio run -d firmware
python -m platformio run -d firmware -t upload --upload-port COM7
```

上传固件前退出 Windows 桥接，避免串口被占用。刷写后应验证握手、状态刷新和拔掉 USB 后的离线页面。

## macOS

当前源码包含菜单栏、Claude/Codex 活动与账户额度、Open-Meteo 天气、A/H/美股、CPU/内存/网速、Apple Music/Spotify 音乐元数据与进度、中文音乐文本资源、最近成功缓存、UserDefaults 非敏感设置、Keychain 配对令牌、认证 LAN `/status`，以及 `/dev/cu.*` 串口探测、460800 握手、两秒状态帧、USB 下发 Wi-Fi 回退配置、页面/亮度菜单控制和按本机空闲时间进入/恢复屏保。音乐读取默认关闭；用户从菜单开启后，系统可能要求自动化权限，且桥接只查询已经运行的播放器。需在 macOS 13+ 验证：

```bash
swift test --package-path mac-app
swift build -c release --package-path mac-app
bash scripts/build_macos_app.sh
```

音乐自动化必须从脚本生成的 `artifacts/AIBotBridge.app` 启动；直接运行 SwiftPM 裸可执行文件不具备 Apple Events 用途说明和 Hardened Runtime entitlement。脚本使用本机临时签名，仅供开发验证，不是正式分发签名。

当前 Windows 主机没有 Swift 工具链；Mac 端 USB 小控制帧、菜单控制、带 CRC/ACK 重试的二进制传输、天气/股票/音乐中文资源、Apple Music/Spotify 封面解码，以及带许可文件门槛的桌宠导入已有源码，但尚未编译或连接设备。国产额度、镜像和设备信息/Wi-Fi 重置仍未完成；音乐 AUTO 联动、自动化权限、播放器脚本字段和封面必须在真实 Mac 上验证。已有源码没有经过真实账号或真实系统指标验收，因此状态保持 `platform-unverified`。

## 隐私边界

桥接程序只查看会话日志文件的最后修改时间，不读取对话内容。账户额度会读取 Claude/Codex CLI 已有登录文件，访问令牌只用于请求对应厂商官方接口，不写入 AI-bot 缓存、状态、串口或日志。国产额度授权使用 `%APPDATA%\AI-bot\quota-auth-profile` 隔离浏览器配置；Cookie 留在该 WebView2 配置中，程序仅从明确允许的官方域名响应中解析可显示额度，不记录响应正文、Cookie 或令牌。本机调试接口只监听回环地址；Wi-Fi 回退接口只绑定选中的私有网卡地址并强制校验配对令牌。Windows 侧令牌使用当前用户 DPAPI 加密保存，设备侧通过已握手 USB 接收并保存，不进入源码、JSON 设置或日志。

## 许可证

AI-bot 自有源码使用 [MIT License](LICENSE)。第三方依赖保持各自许可证，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。项目来源说明见 [PROVENANCE.md](PROVENANCE.md)，运行时桌宠导入规则见[资产策略](docs/ASSET_POLICY.md)。
