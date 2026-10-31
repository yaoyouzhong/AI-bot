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
- 桥接版本源为根目录 `VERSION`；ESP8266 使用 `firmware/VERSION`，TAB5 待发布镜像版本登记在 `versions/TAB5`。三者独立编号，不因另一组件更新而重编号或要求一起刷机。`release-manifest.json` 登记各自标签与更新日志，详见 `docs/COMPONENT-VERSIONS.md`。
- 组件正式发布使用带注释的 `vX.Y.Z`（电脑端）、`esp8266-vX.Y.Z`、`tab5-vX.Y.Z-ui` 标签；0.5.0 及更早的 `vX.Y.Z` 保留组合版语义，旧 `bridge-vX.Y.Z` 仅作历史兼容别名。自动发布衔接先应用并验证已授权的工作流更新。
- README、CHANGELOG 的中英文内容必须同步。

## UI design requirements

- 自动化验收和诊断不得打开、切换或置顶电脑前台窗口，以免影响用户观看和操作；优先使用后台日志、只读诊断及离屏渲染。必须由用户操作的界面，先说明入口，由用户自行打开，不通过 Computer Use 抢占前台。
- 所有 UI 新增和修改必须先经过 UI 设计，覆盖 Windows、macOS、ESP8266、TAB5 及相关网页；弹窗、按钮、提示文案和状态变化同样适用。
- 实现前明确使用场景、信息层级、布局尺寸、间距、字体、颜色、主次操作、触控或点击区域及相关交互状态，并与现有界面风格保持一致。设计记录的篇幅与改动规模相称。
- 实现后必须查看实际渲染预览，检查可读性、对齐、留白、截断、按钮层级及适用的屏幕方向和尺寸，发现问题后迭代；功能或编译通过不能代替 UI 设计与视觉检查。
- 涉及设备显示的变更继续进行对应真机验收。交付时分别说明设计、实际渲染检查和真机验收结果，不将未经检查的默认布局视为完成。

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

桥接或桌面发送链路问题，优先保持设备固件不变，先用日志、离线回归和桥接验证定位原因并验证修复；不得通过反复刷固件猜测原因。固件确需修改时，完成针对性验证后再部署验收。

编译通过不等于实机通过。涉及串口、显示或设备行为时，必须另外记录真实 ESP8266 验证结果。

## ESP8266 小屏默认展示设置

- 以下要求仅适用于 ESP8266 小屏。TAB5 刷机或部署验收不执行恢复自动模式、启用轮播或 `--restore-cycle-default`，保留用户当前展示设置。
- ESP8266 小屏每次刷机或部署验收结束，必须恢复自动模式并启用循环展示，不能把最后的测试页面留作默认。
- 保留用户选择的循环页面、顺序和间隔，不以测试列表覆盖。
- 正常退出桥接后运行 Release 程序 `--restore-cycle-default`，再正常启动；确认 `/status` 中 `displayPolicy.selectedMode=auto`、`cycleEnabled=true` 且页面/顺序/间隔与操作前一致。
- 此规则用于部署收尾，不改变用户日常手动切页的功能；若当次明确要求停在某页验收，按当次要求执行。

## Release boundary

- TAB5 本地固件交付统一使用 `artifacts/firmware/tab5/latest/aibot_tab5.bin`；通过 `scripts/prepare_tab5_firmware.ps1` 校验并生成，历史包保存在同级 `versions/`。更新说明必须包含分类标题和列表，并使用实际 sidecar 做预览。详见 `docs/TAB5-FIRMWARE-WORKFLOW.md`。

- 未经明确授权，不执行 `git push`、创建标签或发布 GitHub Release。
- CI 只验证；Release 工作流只响应显式推送的历史 `v*` 或已登记的组件版本标签，默认只生成候选草稿。TAB5 标签校验元数据，固件仍由独立工程本地构建与核验。
- 发布包必须附带 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和 SHA-256 校验文件。
- 每次正式发布后，必须同步更新主页全部介绍、中英文下载入口、安装手册与安装说明、相关截图及宣传视频，并核验公开页面和实际下载链接；完成来源与许可、媒体摘要登记后才算发布收尾完成。逐项执行 [发布收尾清单](docs/RELEASE-CHECKLIST.md)。Windows 与 Mac 按实际已发布附件分别展示版本，不为保持同号而重建未变更的平台。
