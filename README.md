# AI-bot

本次更新 **AI-bot 0.6.0 + TAB5 0.2.145-ui**，ESP8266 保持 0.5.0。重点包括 Codex 直达、统一更新、十二月历与八种屏保。名画和书法公开保留，分别提供可选图库 ZIP；本地与公开版功能统一。见[完整更新明细与验收状态](docs/RELEASE-0.6.0.md)和[图库安装说明](docs/GALLERY-PACKS.md)。新固件已通过本次真机验收。

**AI 状态，一眼便知。** 桌面 AI 状态助手，将 Claude Code / Codex 任务状态、账户额度与日常信息，集中显示在 **ESP8266 小屏或 M5Stack TAB5** 上。

[**下载 AI-bot 0.6.0**](https://github.com/yaoyouzhong/AI-bot/releases/tag/bridge-v0.6.0) · [TAB5 固件与可选图库](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.145-ui) · [安装与刷机指南](docs/INSTALL.zh.md) · [English](README.en.md)

电脑端 **0.6.0**、ESP8266 **0.5.0**、TAB5 **0.2.145-ui**。先安装电脑端，再按设备选择固件；已有 0.5.0 小屏无需刷机。完整更新内容按三部分见[发布记录](docs/RELEASE-0.6.0.md)。

桥接和两种固件分别更新，版本号无需相同；只按对应更新说明升级，无需每次一起刷机。[独立版本说明](docs/COMPONENT-VERSIONS.md)。

Windows 0.6.0 新增统一的「软件与固件更新」入口：自动提醒各组件的新版本，选择设备后下载并校验适用包，再进入安装工具。已安装 AI-bot 的 TAB5 自动选升级包；首次安装仍通过设备中心完成。[后续更新用法](docs/UPDATES.md)。

本次重点 [TAB5 Codex 直达](docs/TAB5-QUICK-CONSOLE.md)：右下角常驻入口、最近五个会话及前后切换，语音文字填入所选会话草稿，确认后显式发送；支持取消、失败恢复与长按清空。

![CI](https://github.com/yaoyouzhong/AI-bot/actions/workflows/ci.yml/badge.svg)
[![MIT](https://img.shields.io/badge/own_source-MIT-81dce6)](LICENSE)
![版本](https://img.shields.io/badge/bridge-0.6.0-blue)
![硬件](https://img.shields.io/badge/hardware-ESP8266_%7C_TAB5-a6b5ff)

[![AI-bot 双硬件产品介绍，126 秒](docs/assets/product-intro/AI-bot-cover.png)](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · 126 秒新版产品介绍

[下载新版视频（126 秒）](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [视频与截图来源](docs/PRODUCT_VIDEO.md)

[下载桌宠动画包](docs/assets/pet/AI-bot-pet.zip) · [预览 GIF](docs/assets/pet/AI-bot-mascot.gif) · [导入说明](docs/assets/pet/README.md)

视频使用原生界面与固定演示数据，包含 Codex 直达、统一更新、十二月历、名画与书法横竖屏欣赏。桌宠为可导入的设计预览；不代表默认形象已更换。

## 这次更新，先看这几项

### Codex 直达：选择会话，说完再发送

TAB5 右下角常驻入口可查看最近五个会话，前后切换目标；语音文字进入所选会话的草稿，检查后再发送。支持取消、失败恢复和长按清空。[操作说明](docs/TAB5-QUICK-CONSOLE.md)

![Codex 直达原生界面，固定演示会话](docs/assets/screens/tab5-quick.png)

### 一个入口，找到适合自己的更新

电脑端打开「软件与固件更新」，分别查看电脑软件、ESP8266 与 TAB5 的版本和更新明细。选择设备后下载并校验对应包；三者独立编号，不必每次一起升级。[更新步骤](docs/UPDATES.md)

![软件与固件更新，演示版本状态](docs/assets/screens/update-center.png)

### 全年点阵：每天一个点，一眼看清今年进度

「年度点阵」把全年 365／366 天放在同一屏，区分已过、今天与剩余日期，同时显示进度和天数。支持默认、暗夜、暖灰配色；自动昼夜模式按设备本地时间在 07:00–19:00 使用日间配色，其余时间切换夜间配色，无需下载图库。

![年度点阵全年进度，原生固定日期演示](docs/assets/screens/tab5-annual.png)

配色预览：[暗夜](docs/assets/screens/tab5-annual-night.png) · [暖灰](docs/assets/screens/tab5-annual-warm.png)。视频 94–100 秒单独介绍全年点阵。

### 十二月历，以及可以竖着欣赏的名画与书法

十二月历按月展示花卉，配合农历、节气与节假日信息。艺术屏保支持正反横屏和左右竖屏；预览与自动屏保均可转向，书法后续分页也可切换。画面保留作品名、作者和博物馆；已核实的中文译名用于显示，没有通用译名的保留馆藏原名。

![十二月历十月花卉，原生预览](docs/assets/screens/tab5-floral.png)

<table><tr>
<td align="center" width="50%"><strong>每日名画 · 竖屏</strong><br><br><img src="docs/assets/screens/tab5-painting-portrait.png" width="320" alt="林良孔雀竹石图，竖屏原生预览"></td>
<td align="center" width="50%"><strong>每日书法 · 竖屏</strong><br><br><img src="docs/assets/screens/tab5-calligraphy-portrait.png" width="320" alt="王嗣奭行书七言律诗轴，竖屏原生预览"></td>
</tr></table>

横屏效果：[名画](docs/assets/screens/tab5-painting.png) · [书法](docs/assets/screens/tab5-calligraphy.png)。图片及视频均为原生界面渲染，不是硬件实拍；作品出处、许可和截图方法见[素材说明](docs/SCREENSHOTS.md)。

名画、书法分别下载，均不内置于固件；不下载也能使用其他屏保。电脑端「软件与固件更新 → 屏保图库」导入 ZIP，无需手动解压，软件升级保留图库。

| 可选图库 | 独立作品 | 内容分页 | 横竖屏图片 | ZIP 大小 |
| --- | ---: | ---: | ---: | ---: |
| [每日名画](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/AI-bot-DailyPainting-2026.10.07.zip) | 402 | 402 | 804 | 199 MiB |
| [每日书法](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/AI-bot-DailyCalligraphy-2026.10.07.zip) | 411 | 1,500 | 3,000 | 726 MiB |

书法包含更多册页和长卷，同一作品可能有多页；每页另备横、竖两种排版，因此包约为名画的 3.6 倍，单张图片大小相近。分页与横竖图不会重复算作作品。[图库安装与许可说明](docs/GALLERY-PACKS.md)

![屏保图库管理，演示已安装状态](docs/assets/screens/gallery-packs.png)

## 桌面上的实时窗口

电脑端桥接读取本地 AI 活动与厂商额度，再送到桌面设备。Windows 常驻托盘，macOS 提供菜单栏应用和屏幕镜像；设备功能按平台能力提供。

| 功能 | 显示与操作 |
| --- | --- |
| AI 工作状态 | Claude / Codex 工作、空闲、离线与等待输入，任务完成提醒 |
| 额度与余额 | 账户用量、重置时间、支持的厂商余额与已采集额度趋势 |
| 日常信息 | 天气、时钟、股票、系统监控与正在播放的音乐 |
| 桌宠与显示 | 原创 BYTE SPROUT、亮度、指定页面与自动轮播 |
| TAB5 交互 | 触摸界面、任务与历史、语音输入、日历生日与常用任务 |

<table>
<tr>
<td align="center" width="33%"><strong>AI 额度与桌宠</strong><br><br><img src="docs/assets/screens/codex.png" width="240" height="240" alt="AI 额度与新桌宠设计预览：原生布局，演示数据"><br>额度、重置时间与动画角色</td>
<td align="center" width="33%"><strong>天气时钟</strong><br><br><img src="docs/assets/screens/weather.png" width="240" height="240" alt="天气时钟：实际程序界面，演示数据"><br>城市天气、温度与湿度</td>
<td align="center" width="33%"><strong>股票行情</strong><br><br><img src="docs/assets/screens/stocks.png" width="240" height="240" alt="股票行情：实际程序界面，演示数据"><br>自选股票、报价与涨跌幅</td>
</tr>
<tr>
<td align="center" width="33%"><strong>系统监控</strong><br><br><img src="docs/assets/screens/system.png" width="240" height="240" alt="系统监控：实际程序界面，演示数据"><br>处理器、内存与实时网速</td>
<td align="center" width="33%"><strong>音乐播放</strong><br><br><img src="docs/assets/screens/music.png" width="240" height="240" alt="音乐播放：实际程序界面，演示数据"><br>歌曲信息与播放进度</td>
<td align="center" width="33%"><strong>农历屏保</strong><br><br><img src="docs/assets/screens/screensaver.png" width="240" height="240" alt="农历屏保：实际程序界面，演示数据"><br>时间、星期与农历日期</td>
</tr>
</table>

<sub>以上采用原生 Windows 镜像布局与固定演示数据；新桌宠为设计预览，程序与固件默认形象尚未替换，并非实体屏照片。</sub>

### 两种硬件，一个 Windows 设备中心

**我的设备 / 账号数据 / 桥接设置**分别管理设备、共享账号和电脑服务。可同时添加一台 ESP8266 与一台 TAB5，连接独立管理，数据源共享。

- **ESP8266 小屏**：240×240 ST7789、SD2 引脚方案；自动模式优先 USB，失联后回退至已配对 Wi-Fi，也可指定仅 USB 或仅 Wi-Fi。
- **TAB5**：触摸屏，支持 USB / Wi-Fi / BLE；自动连接优先 USB > Wi-Fi > BLE，首次安装使用 USB。
  固件升级支持 Wi-Fi / USB / 蓝牙及传输压缩；自动择优为 Wi-Fi > USB > BLE，固定模式保持所选通道。固件可单独更新，见 [更新指南](docs/UPDATES.md)和 [TAB5 更新日志](docs/development/TAB5-CHANGELOG.zh.md)。
- 托盘左键打开已启用 ESP8266 的预览，否则打开设备中心；右键提供设备设置。开机启动位于 **设备中心 → 桥接设置**。

![Windows 设备中心](docs/assets/screens/device-center.png)

![TAB5 原生额度页，固定演示数据与固件预览](docs/assets/screens/tab5-quota.png)

<details>
<summary><strong>额度统计的边界</strong></summary>

账户额度来自厂商接口，本机 Token 数只覆盖本机可见日志，两者不会混算。
Windows 额度趋势保留近 90 天记录；按北京时间累计可核实的采样增量，
不完整日期也显示已记录时段用量，今天显示“统计中”；没有可比采样时显示 `--`。
仅完整历史日期计入日均，缺失时段不估算，额度变化无法核实不代表用户执行了重置。见[额度趋势说明](docs/QUOTA_TRENDS.md)。

</details>

### 本地数据与额度边界

活动采集只从本地日志提取状态、模型、时间与 Token 元数据，不上传对话正文。账户额度来自厂商接口，本机 Token 总数只覆盖本机可见日志，两者分开统计。请求失败保留最近成功的显示数据。

Windows 额度历史保留近 90 天采样；缺失时段不估算，无法核实的变化保留不确定标记。厂商接入需要对应的有效账号与授权。详见[额度趋势](docs/QUOTA_TRENDS.md)、[数据来源与隐私](docs/DATA_SOURCES.md)及[资源导入规则](docs/ASSET_POLICY.md)。

## 开始使用

**安装应用 → 确认硬件 → 选择首刷或升级 → 备份、写入与核验 → 添加并配对。** 操作步骤见[完整图文指南](docs/INSTALL.zh.md)。

| 下载 | 平台或用途 |
| --- | --- |
| [Windows 安装器](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.0/AIBotBridge-0.6.0-setup-win-x64.exe) | Windows 10/11 x64，内置刷机工具 |
| [Mac 应用](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.0/AIBotBridge-0.6.0-local-candidate-macos-arm64.zip) | macOS 13+，Apple Silicon |
| [ESP8266 固件包](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AI-bot-0.5.0-firmware-materials.zip) | 固件及对应源码、重建材料 |
| [TAB5 首刷 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/TAB5-first-install-0.2.145-ui.zip) | 出厂设备首次安装，含完整安装镜像 |
| [TAB5 升级 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/TAB5-upgrade-0.2.145-ui.zip) | 已有 AI-bot 的设备，解压后选择 `aibot_tab5.bin` |

校验清单：[电脑端](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.0/SHA256SUMS.txt) · [TAB5 与图库](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/SHA256SUMS.txt) · [ESP8266](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/SHA256SUMS.txt)。许可与第三方声明包含在包内；历史版本见 [Releases](https://github.com/yaoyouzhong/AI-bot/releases)。

**第一次刷 TAB5 也是 0.2.145-ui。** 选择完整首刷 ZIP，其中包含启动程序、分区与应用；单独应用 BIN 不能初始化出厂设备。后续升级保留 BIN 同目录的更新说明 sidecar。

准备 USB 数据线；ESP8266 核对 ST7789 / SD2 引脚，TAB5 使用 USB-C 数据接口。完成后核验真实屏幕与连接，恢复自动展示，保留轮播页面、顺序与间隔。

已安装 Windows 的用户先退出桥接，再安装到原位置，继续使用原快捷方式。Windows 安装器未签名；Mac 应用为临时签名且未公证，Intel Mac 未验证。Mac 刷机窗口当前面向 ESP8266，本文 TAB5 首刷、配对与升级流程属于 Windows 功能。

## 当前进度

| 组件 | 当前版本与范围 |
| --- | --- |
| Windows 0.6.0 | 统一更新、可选图库导入、Codex 直达桥接与双硬件设备中心 |
| ESP8266 0.5.0 | 显示页面、时钟、桌宠、USB / Wi-Fi 与轮播 |
| TAB5 0.2.145-ui | Codex 直达、八种屏保、艺术四方向显示、字体压缩与图片接收/分页修复 |
| macOS 0.6.0 | Apple Silicon 菜单栏桥接、镜像与 ESP8266 刷机 |

2026-10-07，TAB5 `.145` 已确认图片、第二页及后续分页竖屏正常；此前的普通界面、输入、预览及自动转向回归结果见[真机验收记录](docs/TAB5-ACCEPTANCE-145.md)。Windows 回归、macOS 测试与构建、ESP8266 构建通过[本次 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/37581182915)。macOS 的构建成功不代表新增 Windows 功能已移植或完成 Mac 真机验收。完整新增、优化和修复按三个组件列于[发布明细](docs/RELEASE-0.6.0.md)。

## 深入了解

| 想了解什么 | 从这里开始 |
| --- | --- |
| 安装与两种硬件刷机 | [图文指南](docs/INSTALL.zh.md) |
| 功能与平台差异 | [功能参考](docs/REFERENCE.zh.md) |
| 架构、数据与协议 | [开发说明](docs/DEVELOPMENT.md) · [数据来源](docs/DATA_SOURCES.md) · [协议](docs/PROTOCOL.md) |
| TAB5 源码与构建快照 | [开发者源码说明](docs/development/TAB5-SOURCE.md) · [固件工作流](docs/TAB5-FIRMWARE-WORKFLOW.md) |
| 来源与许可 | [来源记录](PROVENANCE.md) · [第三方声明](THIRD_PARTY_NOTICES.md) · [TAB5 许可范围](docs/TAB5-LICENSE-SCOPE.md) |
| 最近改动 | [更新日志](CHANGELOG.zh.md) |

自有代码（含 TAB5）统一 **MIT 开源**，第三方组件、字体和标识保留原条款。项目独立开发，与提及的 AI 厂商无隶属或背书关系。

欢迎通过 [Issues](https://github.com/yaoyouzhong/AI-bot/issues)反馈，附平台、版本、硬件与复现步骤，并移除凭据与私人日志。
