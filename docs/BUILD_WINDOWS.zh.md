# Windows 源码构建（进阶）

适用于当前电脑端 **0.6.0** 的 Windows 10/11 x64 构建。普通用户直接使用[安装指南](INSTALL.zh.md)中的安装器；本页只讲构建与本地验证，不另维护一套菜单教程。

## 1. 准备环境

- Git、Python 和 PowerShell；确认 `git --version`、`python --version` 可用。
- .NET 8 SDK；仓库 `global.json` 固定 8.0.400，并允许该功能带的补丁更新。
- 运行依赖为 .NET 8 Desktop Runtime x64；网页授权使用 WebView2 Runtime。安装器可引导补齐，源码 ZIP 运行环境需自行准备。
- ESP8266 构建另需 PlatformIO；TAB5 使用独立 ESP-IDF 工程，不能用 ESP8266 上传命令。

## 2. 构建与检查

```powershell
git clone https://github.com/yaoyouzhong/AI-bot.git
cd AI-bot
dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release
dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once
```

`--status-once` 输出一次状态 JSON 后退出，不启动托盘。需要隔离用户配置的公开回归时：

```powershell
dotnet windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.dll --self-test-public
```

公开自测使用隔离数据，不替代真实账号、安装与设备显示验收。日常运行从资源管理器打开生成目录中的 `AIBotBridge.exe`；不要同时运行多个桥接实例。

## 3. 生成可分发候选

```powershell
powershell -NoProfile -File scripts/package_windows_local.ps1
```

脚本在隔离副本中构建并回归，生成 Windows 安装 EXE、ZIP、源码及许可和 SHA-256 校验文件，输出位置由成功日志给出。`-Firmware` 可额外构建 ESP8266 材料包，使用脚本所调用 Python 环境中的 PlatformIO。它不会替换正在运行的程序，也不会上传或公开发布。

本地 ZIP 命名为 `AIBotBridge-0.6.0-local-candidate-win-x64.zip`；正式下载优先安装器。完整解压后保留 DLL、`runtimes/`、`licenses/` 等配套文件，不能只复制 EXE。ZIP 旁 `.sha256` 可与以下结果比对：

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath 'C:\下载目录\AIBotBridge-0.6.0-local-candidate-win-x64.zip'
```

## 4. 启动后的实际入口

右键托盘 → **设备中心**。没有设备时程序会引导添加；已有 ESP8266 且启用时左键托盘打开小屏预览，否则打开设备中心。

- **我的设备：** 添加设备；分别配置显示、数据、连接与升级。
- **账号数据：** 模型账号、天气定位、自选股票、额度历史。
- **桥接设置：** 服务状态、开机启动、软件固件、配置迁移与提醒管理。

![当前设备中心](assets/screens/device-center.png)

首刷、USB 配对、Wi-Fi 设置、升级与恢复统一按[安装指南](INSTALL.zh.md)操作；所有当前菜单路径与功能见[界面图鉴](FEATURES.zh.md)。构建或电脑镜像正常不等于硬件已连通，需核实设备响应和实体屏。

## 5. 开发资料

[架构与协议](DEVELOPMENT.md) · [ESP8266 从源码刷写](FLASH_BUILD.zh.md) · [TAB5 源码](development/TAB5-SOURCE.md) · [素材来源](SCREENSHOTS.md)

## English

Build Windows 0.6.0 with the SDK pinned in global.json, then run the status-once command and isolated public tests as appropriate. The packaging script builds from a verified isolated copy and emits installer, ZIP, source and checksum materials without deploying or publishing them. Runtime use starts from Device Center; installation and current UI paths are maintained in the linked installation and feature guides. Windows verification does not establish physical-device or Mac acceptance.
