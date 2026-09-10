# AI-bot

AI-bot 是一个本地优先的 AI 状态桌面时钟。目标产品包含 Claude / Codex 活动和账户额度、国产模型额度、天气、股票、系统监控、音乐、桌宠、屏保、USB 优先、Wi-Fi 回退、Windows 托盘以及 macOS 菜单栏桥接。

当前 `0.1.0` 是独立重实现的开发基线。Windows 与固件侧已经重建主要功能链路，但真实账号、真实 ESP8266 和 macOS 仍有未完成的验收。完整功能是硬性范围，不是可选路线图；各模块的当前证据状态见 [功能对齐合同](docs/FUNCTIONAL_PARITY.md)。在矩阵达到对应验收级别前，不应把 `0.1.0` 当作旧产品的功能替代品。

## 功能

- Windows 左键镜像底部新增“额度趋势”：近 7/30 天周额度每日用量及五小时使用率快照，历史仅在本机保留 90 天。每日用量仅在北京时间零点起止边界、账号和重置均可核实时显示；今天为“统计中”，不完整日期为 `--`，日均只计完整日。两分钟轮询通常无法取得精确边界，因此可能没有完整日数据，不承诺准确每日用量已稳定交付。详见 [额度趋势](docs/QUOTA_TRENDS.md)，不补造过去记录。

当前发布准备状态见[发布前检查](docs/RELEASE_READINESS.md)。Windows 与固件已在维护者设备上部署并做过分项验证，但不能据此认定全部功能验收通过。带日期的迁移文档是历史证据，不代表当前部署或发布状态。

- 已接入代码：等待输入/完成确认、本机 Token 统计、自动优先级/屏保唤醒、Wi-Fi 资源同步，以及 macOS 图库/镜像/桌宠缓存。macOS 尚未完成真机构建与验收。

- Windows petdex 图库提供搜索、九种动作预览和 Claude/Codex 角色选择，支持 8×9/8×11 精灵图；保存后复用独立资源槽同步。图库 UI 按旧版布局，设备显示仍待真机验收。

- Windows 桌宠支持 Claude/Codex 分别导入和恢复本机默认动画，保留上一次选择备份，重启后继续使用；旧默认桌宠按原尺寸保存为私有运行缓存，不随公开包分发。等待输入、完成绿环和确认清除已接入，待设备验收。

- 自动判断 Codex、Claude Code 的 `working`、`idle`、`offline` 状态。
- `127.0.0.1:8765/status` 本地只读状态接口。
- 460800 波特率 USB 串口通信。
- ESP8266 串口握手、状态显示和 8 秒离线判定。
- USB 配对令牌保护的 Wi-Fi 回退、NTP/保持时间和 `PC OFF` 独立时钟。
- Windows 和风天气优先、Open-Meteo 回退，保留原定位/设置窗口和右下天气动画；A/H/美股行情失败时保留最近成功缓存。
- Claude/Codex 单页、双额度页、五家国产额度/余额独立页；可设置轮播页面、顺序及 10/15/30/60 秒间隔。股票每屏 4 行和 5 秒翻页。
- Claude/Codex 账户额度解析、最近成功缓存和设备额度页；真实账号与设备待验收。
- Codex 重置额度逐笔到期日期在额度页和桌宠页展示，每屏两笔、每 4 秒翻页；双状态页显示总数。桌宠绘制区与额度区分离。
- 国产模型余额采用大数字、小币种并按基线对齐；Windows 网络采样缓存网卡列表 30 秒，避免重复统计常见虚拟网卡。
- 国产额度统一模型、五家响应解析（新增智谱 GLM 可用余额，见 [授权与边界](docs/GLM_BALANCE.md)）、隔离 WebView2 登录捕获和设备总览页；MiniMax 另支持环境变量 Key 直连。
- Windows 网络曲线每 250 毫秒采样，保留 224 点；上下行数字每 2 秒更新为最近 8 个样本的平均值，CPU/内存数字也每 2 秒更新。USB 小指标帧不替代心跳，LAN 按原轮询节奏更新。
- Windows 系统媒体会话标题、歌手、播放状态与进度；AUTO 播放进入音乐页，停止后恢复轮播。
- 原创建模像素桌宠 `BYTE SPROUT`，随 Claude/Codex 工作状态行走或待机，不携带旧精灵图。
- 手动屏保与空闲自动屏保；AI 开工或音乐开始时临时唤醒，键鼠恢复后回到原显示模式。
- 屏保按旧版大时钟规格显示：204×76 青色数码时钟、黄色冒号、日期/星期与缓慢移动；其他页面仍需按旧版逐页视觉对齐。
- COBS 大资源分块、逐块/整包 CRC32、ACK/重试和 LittleFS 校验后替换协议。
- 歌曲变化时预渲染 232×44 中文标题/歌手位图和 112×112 封面，经大资源协议发送并由设备逐行显示。
- 可导入带独立许可说明的 PNG/JPG/BMP/GIF 桌宠；GIF 最多采样 8 帧并保留时长。Windows/macOS 提供分角色持久化和默认恢复；原默认素材仅显式导入本机缓存，不进入仓库。
- 天气城市/状况和最多 20 个股票名称由 Windows 中文字体预渲染；设备按页读取，资源缺失时降级显示代码。
- Windows 托盘恢复原机器人图标、七组菜单和左键镜像，提供 14 个页面的离线渲染检查。
- 国产额度保留原授权窗口、后台刷新、限流退避及 WebView2 异常恢复；使用新的隔离 profile，旧额度缓存仅按可显示字段只读迁入。
- Codex 完成提示依据明确的主任务完成事件，启动不重放历史，子任务和重复事件不触发提示音；原合成提示音已迁入。
- Windows 托盘显示模式控制和基于系统空闲时间的自动屏保。
- 会扫描本机会话 JSONL，仅提取生命周期、模型、时间和 Token 元数据，不展示、保存或上传对话正文；账户额度访问令牌只从本机 CLI 登录文件读取并发往对应厂商官方域名，不进入缓存、状态、串口或日志。
- Windows、固件 CI 与标签驱动的候选发布流水线。

发布前仍需完成受控网络下 Wi-Fi 回退端到端验收、macOS 构建和实机验证，以及最终候选的长期稳定性、视觉与安装验收。维护者机器上的分项账号/USB 验证不等于所有厂商和全新安装均已通过。私有导入的旧桌宠、页面标志不随公开包分发；新用户默认使用 BYTE SPROUT。天气、股票和额度的外发边界详见[数据来源与隐私](docs/DATA_SOURCES.md)。

## 目录

```text
windows-app/AIBotBridge/  Windows .NET 8 托盘桥接
mac-app/                  macOS 菜单栏桥接（独立基础已建，平台未验证）
firmware/                 PlatformIO + Arduino ESP8266 固件
docs/                     协议与开发说明
```

## Windows

本地候选包的依赖、解压、启动、升级和校验说明见 [Windows 候选包](docs/WINDOWS_PACKAGE.md)。开发者可运行 `powershell -NoProfile -File scripts/package_windows_local.ps1`，隔离构建并验证 ZIP；不会覆盖运行实例或上传。

增加 `-Firmware` 可同时编译固件并生成[固件材料包](docs/FIRMWARE_PACKAGE.md)，含对应源码、第三方许可及重建配置。两种方式都会生成经过内容检查、附 SHA-256 清单的公开源码 ZIP。分发边界见[分发条款](docs/DISTRIBUTION_TERMS.md)，正式 Release 工作流仍需另行接入并验证。

正常使用请从资源管理器打开 `AIBotBridge.exe`。它是无控制台的 GUI 程序，不会在启动时先弹出再关闭终端。自动化诊断请使用 `dotnet AIBotBridge.dll --status-once` 或 `dotnet AIBotBridge.dll --self-test-public`，以保留标准输出并等待退出码；不要把 PowerShell 对 GUI EXE 的立即返回当作测试成功。部署工具启动的进程可能继承工具的退出管理；日常常驻应由资源管理器或用户自行启用的登录启动项启动。

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

上传固件前退出 Windows 桥接，避免串口被占用。USB 同时供电时不能拔线测试回退。保持线缆连接，从 Windows/macOS 菜单选择“测试 Wi-Fi 回退（保持 USB 供电）…”，程序暂停常规串口发送约 12 秒，保留 LAN 服务，以设备计数验证回退并自动恢复 USB。单位网络隔离时，回退不通过并不代表 USB 功能故障。

Windows/macOS 的设备信息和 Wi-Fi 重置现通过 USB 完成，不依赖局域网地址、配对令牌或 Wi-Fi 连通。重置需要单独确认，不包含在自动测试中。Windows 也提供以下入口（前两项需新版固件和真实设备，运行前退出托盘桥接以释放串口和端口）：

```powershell
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --test-usb-management
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --test-wifi-fallback
windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\AIBotBridge.exe --self-test-usb-management
```

`--test-usb-management` 只检查握手后的 USB 状态和设备信息，不代替逐页/图片验收。`--test-wifi-fallback` 还要求设备已配网且能够访问电脑 LAN 服务；合成自测只验证解析和测试状态流程，不访问设备。详细操作见 [USB 和回退验收](docs/USB_VALIDATION.md)。

## macOS

当前源码包含菜单栏、Claude/Codex 活动与账户额度、Open-Meteo 天气、A/H/美股、CPU/内存/网速、Apple Music/Spotify 音乐元数据与进度、中文音乐文本资源、最近成功缓存、UserDefaults 非敏感设置、Keychain 配对令牌、认证 LAN `/status`，以及 `/dev/cu.*` 串口探测、460800 握手、两秒状态帧、USB 下发 Wi-Fi 回退配置、页面/亮度菜单控制、严格私网地址发现、USB 设备信息、二次确认 USB Wi-Fi 重置、按本机空闲时间进入/恢复屏保和 AI/音乐事件 12 秒临时唤醒。音乐读取默认关闭；用户从菜单开启后，系统可能要求自动化权限，且桥接只查询已经运行的播放器。需在 macOS 13+ 验证：

```bash
swift test --package-path mac-app
swift build -c release --package-path mac-app
bash scripts/build_macos_app.sh
```

音乐自动化必须从脚本生成的 `artifacts/AIBotBridge.app` 启动；直接运行 SwiftPM 裸可执行文件不具备 Apple Events 用途说明和 Hardened Runtime entitlement。脚本使用本机临时签名，仅供开发验证，不是正式分发签名。

当前 Windows 主机没有 Swift 工具链。Mac 源码包含 USB/LAN、资源同步、天气/股票/音乐、系统监控、额度、屏保、设备管理、状态事件、本机 Token、图库、镜像和桌宠持久化；另有 XCTest 回归用例。尚未编译或连接设备，自动化权限、播放器字段、窗口布局、缓存重启、屏保和串口必须在真实 Mac 验证，状态保持 `platform-unverified`。Windows 专属 WebView2 国产厂商授权不宣称为 Mac 已有能力。

## 隐私边界

桥接程序会扫描本机会话 JSONL，仅提取生命周期事件、模型名、时间戳和 Token 用量；不展示、保存或上传对话正文。账户额度会读取 Claude/Codex CLI 已有登录文件，访问令牌只用于请求对应厂商官方接口，不写入 AI-bot 缓存、状态、串口或日志。国产额度授权使用 `%APPDATA%\AI-bot\quota-auth-profile` 隔离浏览器配置；Cookie 留在该 WebView2 配置中，程序仅从明确允许的官方域名响应中解析可显示额度，不记录响应正文、Cookie 或令牌。本机调试接口只监听回环地址；Wi-Fi 回退接口只绑定选中的私有网卡地址并强制校验配对令牌。Windows 侧令牌使用当前用户 DPAPI 加密保存，设备侧通过已握手 USB 接收并保存，不进入源码、JSON 设置或日志。

## 许可证

AI-bot 自有源码使用 [MIT License](LICENSE)。第三方依赖保持各自许可证，详见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。项目来源说明见 [PROVENANCE.md](PROVENANCE.md)，运行时桌宠导入规则见[资产策略](docs/ASSET_POLICY.md)。
# 对齐验收状态

最新[全功能对齐审计](docs/FULL_PARITY_AUDIT_2026-09-09.md)包含 25 个 Windows 新旧镜像逐像素对照及明确未完成项。镜像一致不代表实体屏、Wi-Fi 回退或 macOS 已验收；当前仍不是可替换稳定旧版的完成声明。
