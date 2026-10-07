# AI-bot

**AI 状态，一眼便知。** 把 Claude Code / Codex 的任务状态、账户额度与日常信息，放到桌边的 ESP8266 小屏或 M5Stack TAB5 上。

[下载与搭配](#下载与搭配) · [安装指南](docs/INSTALL.zh.md) · [本次更新明细](docs/RELEASE-0.6.0.md) · [English](README.en.md)

https://github.com/user-attachments/assets/ea8a8241-625f-4afe-8d24-1e4dc3c043db

<sub>108 秒产品介绍，点击直接播放。画面来自原生界面与固定演示数据。[下载 MP4](https://github.com/yaoyouzhong/AI-bot/raw/refs/heads/main/docs/assets/product-intro/AI-bot-product-intro.mp4) · [素材与作品来源](docs/PRODUCT_VIDEO.md)</sub>

## 能做什么

| 使用场景 | 功能 |
| --- | --- |
| **工作时，抬眼看进度** | Claude / Codex 工作、等待输入与完成提醒；账户额度、余额和重置时间。 |
| **在 TAB5 上直达 Codex** | 从最近五个会话中选择目标，查看回复，语音输入草稿，确认后发送。支持取消、失败恢复和长按清空。[使用说明](docs/TAB5-QUICK-CONSOLE.md) |
| **日常信息，随时看一眼** | 天气、时钟、股票、电脑负载、正在播放的音乐，以及桌宠与自选页面轮播。 |
| **用屏保装点桌面** | TAB5 提供八种屏保：包括全年点阵、十二月花卉台历、每日名画与每日书法。艺术作品可随设备横竖转向。 |
| **更新时，不用找错包** | Windows 的「软件与固件更新」统一查看版本与更新明细，下载并校验所选设备的适用包。[更新说明](docs/UPDATES.md) |

<table><tr>
<td align="center" width="33%"><strong>Codex 直达</strong><br><br><img src="docs/assets/screens/tab5-quick.png" width="280" alt="TAB5 Codex 直达：选择会话与语音草稿"></td>
<td align="center" width="33%"><strong>全年点阵</strong><br><br><img src="docs/assets/screens/tab5-annual.png" width="280" alt="年度点阵：365 或 366 天的年度进度"></td>
<td align="center" width="33%"><strong>十二月历</strong><br><br><img src="docs/assets/screens/tab5-floral.png" width="280" alt="十二月历：花卉、农历与节气"></td>
</tr></table>

全年点阵每天一个点，支持闰年、三种配色与自动昼夜切换；十二月历配合农历、节气和节假日。两者无需额外图库。名画与书法保留作品名、作者和博物馆；已核实的中文译名直接显示，其他作品保留馆藏原名。

[名画竖屏效果](docs/assets/screens/tab5-painting-portrait.png) · [书法竖屏效果](docs/assets/screens/tab5-calligraphy-portrait.png) · [更多功能与截图](docs/FEATURES.zh.md)

## 下载与搭配

**一份电脑软件 + 你所用设备的固件。** 两种硬件选其一即可，也可各连接一台；三者独立编号，不要求版本号相同，也不必一起升级。

### 1. 先安装电脑软件 · 0.6.0

| 你的电脑 | 下载 | 支持的设备 |
| --- | --- | --- |
| Windows 10/11 x64 | [Windows 安装器](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.0/AIBotBridge-0.6.0-setup-win-x64.exe) | ESP8266、TAB5 |
| macOS 13+ · Apple Silicon | [Mac 应用 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.0/AIBotBridge-0.6.0-local-candidate-macos-arm64.zip) | ESP8266；菜单栏与屏幕镜像 |

TAB5、统一更新和艺术图库导入的操作说明以 Windows 为准。Windows 安装器未签名；Mac 应用临时签名、未公证，Intel Mac 未验证。

### 2. 再按设备选择固件

| 你的设备 | 固件版本 | 下载与选择 |
| --- | --- | --- |
| **ESP8266 小屏** · 240×240 ST7789 | 0.5.0 | [首刷／升级通用包](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AI-bot-0.5.0-firmware-materials.zip)；已是此版本无需重刷。 |
| **M5Stack TAB5** · 触摸大屏 | 0.2.145-ui | 出厂设备选[首刷 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/TAB5-first-install-0.2.145-ui.zip)；已有 AI-bot 选[升级 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/TAB5-upgrade-0.2.145-ui.zip)。 |

TAB5 首刷包直接选择 ZIP；升级包先解压，再选择 `aibot_tab5.bin`，保留同目录的更新说明文件。两种硬件的固件不可互换。详细接线、备份与刷写步骤见[安装图解](docs/INSTALL.zh.md)。

<details>
<summary><strong>可选：为 TAB5 下载名画／书法图库</strong></summary>

| 图库 | 独立作品 | 下载 |
| --- | ---: | --- |
| 每日名画 | 402 件 | [名画 ZIP · 199 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/AI-bot-DailyPainting-2026.10.07.zip) |
| 每日书法 | 411 件 | [书法 ZIP · 726 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/AI-bot-DailyCalligraphy-2026.10.07.zip) |

下载后在 Windows「软件与固件更新 → 屏保图库」导入 ZIP，无需手动解压；软件升级保留图库。不下载也能使用其他功能。书法包含多页册页和长卷，因此包更大；分页不重复计作作品。[安装、容量与许可说明](docs/GALLERY-PACKS.md)

</details>

<details>
<summary>校验文件与历史版本</summary>

SHA-256：[电脑软件](https://github.com/yaoyouzhong/AI-bot/releases/download/bridge-v0.6.0/SHA256SUMS.txt) · [TAB5 与图库](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.145-ui/SHA256SUMS.txt) · [ESP8266](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/SHA256SUMS.txt)。[历史版本](https://github.com/yaoyouzhong/AI-bot/releases)

</details>

## 安装与后续更新

1. **安装并打开电脑端。** Windows 用户从托盘右键进入「设备中心」。已有用户退出桥接后覆盖安装到原位置。
2. **添加自己的设备。** 准备 USB 数据线，按[图文指南](docs/INSTALL.zh.md)完成首次刷机、添加和配对；已经安装 AI-bot 的设备可直接验证并添加。
3. **选择想看的内容。** 配置账号、日常信息和轮播页面；TAB5 艺术屏保需先导入对应图库，图片同步时保持电脑与桥接运行。

以后从 Windows「设备中心 → 桥接设置 → 软件固件」打开「**软件与固件更新**」查看三个组件各自的更新明细、下载并校验，再按提示安装。启用「自动检查并提醒」后会通知新版本，安装仍由你确认。图库按需单独下载与导入。[完整更新步骤](docs/UPDATES.md)

## 更多资料

- **使用与更新：** [完整功能图鉴](docs/FEATURES.zh.md) · [本次三组件更新明细](docs/RELEASE-0.6.0.md) · [更新日志](CHANGELOG.zh.md) · [TAB5 真机验收](docs/TAB5-ACCEPTANCE-145.md)
- **数据与隐私：** [数据来源](docs/DATA_SOURCES.md) · [额度趋势统计边界](docs/QUOTA_TRENDS.md)。活动统计提取本地状态与 Token 元数据，账户额度来自厂商接口，两者分开统计。
- **开发与源码：** [开发说明](docs/DEVELOPMENT.md) · [协议](docs/PROTOCOL.md) · [TAB5 源码与构建](docs/development/TAB5-SOURCE.md) · [组件独立版本](docs/COMPONENT-VERSIONS.md)
- **素材与许可：** [来源记录](PROVENANCE.md) · [第三方声明](THIRD_PARTY_NOTICES.md) · [截图与作品署名](docs/SCREENSHOTS.md) · [可选桌宠动画包](docs/assets/pet/README.md)

自有代码（含 TAB5）采用 [MIT 许可](LICENSE)，第三方组件与艺术作品保留原许可。项目独立开发，与提及的 AI 厂商无隶属关系。[反馈问题](https://github.com/yaoyouzhong/AI-bot/issues)时请附平台、版本、硬件与复现步骤，并移除凭据和私人日志。
