# AI-bot

**AI 状态，一屏掌握。** AI-bot 是一款搭配电脑使用的桌面 AI 助手，将 AI 任务状态、多平台账户额度和日常信息集中呈现在独立屏幕上，减少来回切换窗口。支持查看 Claude Code / Codex 的工作进度，以及 DeepSeek、智谱、Kimi、MiniMax、阿里百炼等国产模型服务的套餐用量或 API 余额。

工作之外，它也是桌边的时钟与信息看板，按需展示天气、行情、音乐和电脑运行状态。TAB5 还可搭配每日名画、每日书法图库，让屏幕在闲暇时展示艺术作品。

[完整下载中心](DOWNLOADS.md) · [下载与搭配](#下载与搭配) · [安装指南](docs/INSTALL.zh.md) · [本次更新明细](docs/RELEASE-0.6.11.md) · [English](README.en.md)

本次更新：电脑端 **0.6.11**、TAB5 **0.2.281-ui**、ESP8266 **0.5.7**。TAB5 现支持 Windows 与 Apple Silicon Mac；改善语音输入、长会话分页、设备预览和连接恢复，新增草稿长按清空，小屏重置信息更清晰。见[本轮说明](docs/RELEASE-0.6.11.md)。

## 选择适合你的桌面屏幕

AI-bot 由电脑端软件与桌面屏幕配合使用，两类设备按需选择。

<table>
<tr>
<td width="50%" valign="top">
<p align="right"><a href="docs/FLASH_BUILD.zh.md#1-核对硬件并留好回退材料">硬件介绍 ↗</a></p>
<h3>ESP8266 小屏</h3>
<p><strong>桌面状态看板</strong></p>
<p align="center"><img src="docs/assets/hardware/esp8266-product-composite.png" width="280" alt="ESP8266 SD2 小电视外形与 AI-bot 额度界面的产品合成示意"></p>
<p>采用 ESP8266 的小型桌面设备，搭配 240 × 240 彩屏。放在电脑旁，随时查看 AI 任务状态、模型额度、天气和音乐信息，主要通过电脑配置。</p>
<p><strong>线材提醒：</strong>小屏随附电源线只能供电；刷机或通过 USB 连接电脑时，请换用支持数据传输的 USB 数据线。</p>
<p><strong>连接方式：</strong>USB / Wi-Fi<br><strong>电脑支持：</strong>Windows / macOS<br><strong>适合：</strong>以信息展示为主，希望抬眼就能查看状态的用户。</p>
<p><a href="docs/INSTALL.zh.md#esp8266">安装与连接</a> · <a href="docs/FEATURES.zh.md#esp8266">查看界面</a></p>
</td>
<td width="50%" valign="top">
<p align="right"><a href="https://docs.m5stack.com/zh_CN/core/Tab5">硬件介绍 ↗</a></p>
<h3>M5Stack TAB5 大屏</h3>
<p><strong>触控桌面助手</strong></p>
<p align="center"><img src="docs/assets/hardware/tab5-product-composite.png" width="280" alt="M5Stack TAB5 官方机身与 AI-bot 原生额度界面的合成展示"></p>
<p><a href="https://shop.m5stack.com/pages/about-us">M5Stack</a> 是面向物联网开发的模块化硬件品牌，提供主控、传感器与扩展模块；TAB5 是其 5 英寸触控终端。</p>
<details>
<summary>查看 4 个侧面与背面实图（共 5 张）</summary>
<p align="center"><strong>USB 接口侧</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-official.png"><img src="docs/assets/hardware/tab5-side-official.png" width="360" alt="TAB5 官方侧面实图：USB-A、USB-C、耳机接口与 PORT.A"></a></p>
<p align="center"><strong>GPIO / microSD 侧</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-gpio-official.png"><img src="docs/assets/hardware/tab5-side-gpio-official.png" width="360" alt="TAB5 官方侧面实图：GPIO 扩展口、microSD 卡槽、支架螺孔与扬声器"></a></p>
<p align="center"><strong>M5 标识侧</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-logo-official.png"><img src="docs/assets/hardware/tab5-side-logo-official.png" width="360" alt="TAB5 官方 M5 标识侧实图"></a></p>
<p align="center"><strong>外置天线接口侧</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-side-antenna-official.png"><img src="docs/assets/hardware/tab5-side-antenna-official.png" width="360" alt="TAB5 官方侧面实图：外置天线接口与产品标识"></a></p>
<p align="center"><strong>背面</strong></p>
<p align="center"><a href="docs/assets/hardware/tab5-back-official.png"><img src="docs/assets/hardware/tab5-back-official.png" width="260" alt="TAB5 官方背面实图：电池位、M5-Bus 与接口引脚标注"></a></p>
<p><sub>5 张均为 M5Stack 官方实图，仅裁去留白。点击图片可放大。</sub></p>
</details>
<p>配备 5 英寸触控屏的桌面设备。除了查看状态和额度，还能继续 Codex 会话、语音输入、拍照添加附件和管理日程，也可展示时钟、名画与书法。</p>
<p><strong>连接方式：</strong>USB / Wi-Fi / 蓝牙<br><strong>电脑支持：</strong>Windows / macOS<br><strong>适合：</strong>需要触控操作、处理任务和更丰富展示的用户。</p>
<p><a href="docs/INSTALL.zh.md#tab5-first">安装与连接</a> · <a href="docs/FEATURES.zh.md#tab5">查看界面</a></p>
</td>
</tr>
</table>

<sub>两图均为硬件外观与 AI-bot 界面的合成展示，使用演示数据，非实机拍摄；不按实物比例排列。<a href="docs/HARDWARE-IMAGES.md">图片来源与说明</a></sub>

新版 116 秒视频展示电脑一键解锁、TightVNC 自动安装／接入与双向已读同步，使用实际 234 更新说明，并保留日程、时钟、额度和艺术浏览。画面来自原生界面的隔离演示数据，非硬件实拍。

https://github.com/user-attachments/assets/d745c087-9c9d-48c8-bd55-00a3879eff32

<sub>116 秒中文产品介绍，可在本页直接播放。[视频与来源](docs/PRODUCT_VIDEO.md) · [下载 MP4](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.10/AI-bot-product-intro-20261019-0610-234.mp4)</sub>

## 能做什么

[运行监控](docs/RUNTIME-MONITOR.md)提供连接与系统压力的状态、原因和建议；系统监控报警的恢复清除规则保持原样。

<table>
<thead><tr><th width="120" nowrap="nowrap">功能</th><th nowrap="nowrap">说明</th></tr></thead>
<tbody>
<tr><td nowrap="nowrap"><strong>任务状态</strong></td><td nowrap="nowrap">Claude / Codex 工作、空闲、待输入状态及完成提醒。</td></tr>
<tr><td nowrap="nowrap"><strong>账户额度</strong></td><td nowrap="nowrap">账户用量、API 余额、重置时间与 Codex 重置卡；电脑端提供额度历史。</td></tr>
<tr><td nowrap="nowrap"><strong>日常信息</strong></td><td nowrap="nowrap">天气、时钟、股票、电脑负载与媒体播放信息；网易云音乐与 QQ 音乐有专门适配，汽水音乐、Apple Music 等客户端通过 Windows 媒体会话接入，Chrome／Edge 可补充网页播放信息。实际兼容范围见<a href="docs/MEDIA-PLAYERS.md">媒体支持说明</a>。</td></tr>
<tr><td nowrap="nowrap"><strong>电脑控制</strong></td><td>在 TAB5 调节音量、静音、锁屏或一键解锁，查看锁定状态，点选最多四个带图标的程序或文件夹；Wi-Fi 解锁组件自动安装／接入。</td></tr>
<tr><td nowrap="nowrap"><strong>显示设置</strong></td><td nowrap="nowrap">固定页、轮播或智能跟随；可选页面、顺序、间隔与桌宠动画。</td></tr>
<tr><td nowrap="nowrap"><strong>设备管理</strong></td><td nowrap="nowrap">Windows 设备中心配置账号、数据与设备；统一查看更新并下载校验。<a href="docs/UPDATES.md">更新说明</a></td></tr>
</tbody>
</table>

<table>
<thead><tr><th width="33%" nowrap="nowrap">电脑端管理</th><th width="33%" nowrap="nowrap">ESP8266 小屏</th><th width="33%" nowrap="nowrap">TAB5 触控大屏</th></tr></thead>
<tbody>
<tr>
<td align="center" valign="middle"><img src="docs/assets/screens/device-center.png" width="240" alt="Windows 设备中心：我的设备与设备设置入口"></td>
<td align="center" valign="middle"><img src="docs/assets/screens/codex.png" width="180" alt="ESP8266 小屏：Codex 状态与额度"></td>
<td align="center" valign="middle"><img src="docs/assets/screens/tab5-overview.png" width="260" alt="TAB5 总览：任务、额度、时钟与天气"></td>
</tr>
<tr><td align="center" nowrap="nowrap">配置账号、设备与更新</td><td align="center" nowrap="nowrap">展示状态、额度与日常信息</td><td align="center" nowrap="nowrap">触控操作，集中查看信息</td></tr>
</tbody>
</table>

<sub>以上为当前原生界面与固定演示数据；小屏画面来自电脑端镜像。[完整功能图鉴](docs/FEATURES.zh.md)</sub>

<details>
<summary><strong>查看 TAB5 特色：Codex 直达、全年点阵与十二月历</strong></summary>

**日程待办与到期提醒**已包含在 TAB5 210：今日／全部分段筛选与列表对齐，空状态居中新建；本地保存、离线编辑与删除、重连同步，支持时间、重复和提前提醒。最多 16 项；到期事项保留，满额新增时才清理最早结束的过期单次事项。[使用与实机边界](docs/RELEASE-0.6.11.md)

**Codex 直达：** 从最近五个会话中选择目标，查看回复，语音输入草稿，确认后发送。支持取消、失败恢复和长按清空。[使用说明](docs/TAB5-QUICK-CONSOLE.md)

<table><tr>
<td align="center" width="33%"><strong>Codex 直达</strong><br><br><img src="docs/assets/screens/tab5-quick.png" width="280" alt="TAB5 Codex 直达：选择会话与语音草稿"></td>
<td align="center" width="33%"><strong>全年点阵</strong><br><br><img src="docs/assets/screens/tab5-annual.png" width="280" alt="年度点阵：365 或 366 天的年度进度"></td>
<td align="center" width="33%"><strong>十二月历</strong><br><br><img src="docs/assets/screens/tab5-floral.png" width="280" alt="十二月历：花卉、农历与节气"></td>
</tr></table>

全年点阵每天一个点，支持闰年、默认自动昼夜配色和暖灰两种选项；十二月历配合农历、节气和节假日。两者无需额外图库。名画与书法支持横竖屏，保留作品名、作者和博物馆；已核实的中文译名直接显示，其他作品保留馆藏原名。

[名画竖屏效果](docs/assets/screens/tab5-painting-portrait.png) · [书法竖屏效果](docs/assets/screens/tab5-calligraphy-portrait.png) · [完整屏保介绍](docs/FEATURES.zh.md#tab5)

</details>

<details>
<summary>查看 TAB5 电脑控制：音量、静音、锁屏与快捷启动</summary>

![TAB5 控制页，隔离演示数据](docs/assets/screens/tab5-computer-control.png)

在电脑“设备中心 → TAB5 → 电脑控制”设置最多四个快捷启动项。程序和文件夹显示图标；静音时改为扬声器加斜杠，锁屏和解锁后状态自动同步。Wi-Fi 最多保存十个网络，保留既有配置。[使用说明](docs/COMPUTER-CONTROL.md)

</details>

## 下载与搭配

<!-- downloads:start -->
| 用途 | 版本 | 下载 | SHA-256 |
| --- | --- | --- | --- |
| Windows 桥接 · Windows 10/11 x64 | 0.6.10 | [下载 · 27 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.10/AI-bot-bridge-v0.6.10-windows-x64-setup.exe) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.10/SHA256SUMS.txt) |
| Mac 桥接 · macOS 13+ / Apple Silicon | 0.6.7 | [下载 · 14 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.7/AIBotBridge-0.6.7-local-candidate-macos-arm64.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.7/SHA256SUMS.txt) |
| ESP8266 小屏 · 首刷／升级通用 | 0.5.3 | [下载 · 30 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.3/AI-bot-0.5.3-firmware-materials.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.3/SHA256SUMS.txt) |
| TAB5 · 出厂系统首次安装 | 0.2.234-ui | [下载 · 5 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/AI-bot-tab5-0.2.234-ui-first-install.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/SHA256SUMS.txt) |
| TAB5 · 已有 AI-bot 升级 | 0.2.234-ui | [下载 · 5 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/AI-bot-tab5-0.2.234-ui-upgrade.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.234-ui/SHA256SUMS.txt) |
| 每日书法 · 标准版 | 2026.1 | [下载 · 869 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.205-ui/AI-bot-DailyCalligraphy-Curated-2026.10.15.1.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.205-ui/SHA256SUMS.txt) |
| 每日书法 · 高清完整版 | 2026.1 | [下载 · 2.89 GiB](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#download) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#calligraphy) |
| 每日名画 · 标准版 | 2026.1 | [下载 · 191 MiB](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.194-ui/AI-bot-DailyPainting-Curated-2026.10.14.1.zip) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.194-ui/SHA256SUMS.txt) |
| 每日名画 · 高清完整版 | 2026.1 | [下载 · 1.86 GiB](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#download) | [SHA-256](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-HD.md#painting) |

先安装电脑桥接，再按设备选择固件；书法、名画按需下载。各组件独立更新，已是所列版本无需重装或重刷。

- **首次使用 TAB5**：选择首刷 ZIP；**已有 AI-bot**：选择升级 ZIP，解压后选择 `aibot_tab5.bin`，保留旁边的更新说明文件。两种硬件的固件不能混用。
- **图库**：Windows「软件与固件更新 → 屏保图库」直接导入 ZIP，无需解压；升级电脑软件会保留图库。图库要求 Windows 桥接 0.6.0+、TAB5 0.2.145-ui+。
- **图库版本选择**：同一类别选择标准版或高清完整版即可。高清版含对应标准图和导读，无需重复安装；电脑桥接 0.6.7+ 可浏览高清原作，TAB5 使用适配展示图。点击上表高清版的大小链接，按说明下载、合并后导入；标准名画沿用原包。
- Windows 支持 ESP8266 与 TAB5；Mac 当前支持 ESP8266，TAB5、统一更新与图库导入说明适用于 Windows。Windows 安装器未签名；Mac 为临时签名，未公证。

[安装与刷机步骤](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md) · [图库容量与许可](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/GALLERY-PACKS.md) · [历史发布](https://github.com/yaoyouzhong/AI-bot/releases)
<!-- downloads:end -->

## 安装与后续更新

1. **安装并打开电脑端。** Windows 用户从托盘右键进入「设备中心」。已有用户退出桥接后覆盖安装到原位置。
2. **添加自己的设备。** 准备 USB 数据线，按[图文指南](docs/INSTALL.zh.md)完成首次刷机、添加和配对；已经安装 AI-bot 的设备可直接验证并添加。
3. **选择想看的内容。** 配置账号、日常信息和轮播页面；TAB5 艺术屏保需先导入对应图库，图片同步时保持电脑与桥接运行。

以后从 Windows「设备中心 → 桥接设置 → 软件固件」打开「**软件与固件更新**」查看三个组件各自的更新明细、下载并校验，再按提示安装。启用「自动检查并提醒」后会通知新版本，安装仍由你确认。图库按需单独下载与导入。[完整更新步骤](docs/UPDATES.md)

## 更多资料

- **使用与更新：** [完整功能图鉴](docs/FEATURES.zh.md) · [本次更新明细](docs/RELEASE-0.6.11.md) · [更新日志](CHANGELOG.zh.md) · [TAB5 真机验收](docs/development/TAB5-CONTROL-215-ACCEPTANCE-20261017.md)
- **数据与隐私：** [数据来源](docs/DATA_SOURCES.md) · [额度趋势统计边界](docs/QUOTA_TRENDS.md)。活动统计提取本地状态与 Token 元数据，账户额度来自厂商接口，两者分开统计。
- **开发与源码：** [开发说明](docs/DEVELOPMENT.md) · [协议](docs/PROTOCOL.md) · [TAB5 源码与构建](docs/development/TAB5-SOURCE.md) · [组件独立版本](docs/COMPONENT-VERSIONS.md)
- **素材与许可：** [来源记录](PROVENANCE.md) · [第三方声明](THIRD_PARTY_NOTICES.md) · [截图与作品署名](docs/SCREENSHOTS.md) · [可选桌宠动画包](docs/assets/pet/README.md)

自有代码（含 TAB5）采用 [MIT 许可](LICENSE)，第三方组件与艺术作品保留原许可。项目独立开发，与提及的 AI 厂商无隶属关系。[反馈问题](https://github.com/yaoyouzhong/AI-bot/issues)时请附平台、版本、硬件与复现步骤，并移除凭据和私人日志。

Windows 0.6.10 / TAB5 0.2.234-ui 增加电脑一键解锁与 TightVNC 自动安装／接入，无需填写组件密码；音量拖动恢复、双向 Codex 已读同步、纯 BLE 启动核验及重启／USB 变化后的电量显示已完成真机验收。安装包、固件和对应源码已正式发布。[准备与验收记录](docs/development/RELEASE-0610-229-CLOSEOUT-20261019.md)。
