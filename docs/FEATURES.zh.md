# 功能与界面图鉴

适用于电脑端 **0.6.0**、ESP8266 **0.5.0**、TAB5 **0.2.145-ui**，2026-10-07 按当前实现复核。[返回首页](../README.md) · [安装指南](INSTALL.zh.md) · [更新说明](UPDATES.md)

本文以 Windows 为主。截图使用当前原生控件与隔离演示数据，不是实体屏照片；Mac 功能范围见文末。桌宠截图采用项目自选动画预览，首次使用仍有内置 BYTE SPROUT，无需下载素材。

## 1. 从设备中心开始

![当前托盘菜单：设备中心、小屏预览与按设备分组的设置](assets/screens/tray-menu.png)

右键托盘打开菜单：**设备中心**是统一入口，后面按已添加的设备分组；只有启用了 ESP8266，才出现“小屏预览”。左键托盘也按这个条件打开／隐藏小屏镜像，否则打开设备中心。设备名称随你的命名变化，停用设备的快捷项不可用。

![设备中心的我的设备页，演示 TAB5](assets/screens/device-center.png)

| 页面 | 当前入口与用途 |
| --- | --- |
| **我的设备** | 添加 ESP8266 / TAB5，查看连接和固件版本；选择设备后进入该设备的设置。每种型号最多一台。 |
| **账号数据** | 模型账号、天气定位、自选股票、额度历史；公共数据源在电脑上配置，各设备按各自的数据设置使用。 |
| **桥接设置** | 开机启动、服务状态、软件固件、配置迁移、提醒管理、关于应用。 |

<details>
<summary>查看账号数据与桥接设置页面</summary>

![账号数据](assets/screens/device-center-accounts.png)
![桥接设置](assets/screens/device-center-bridge.png)

</details>

## 2. ESP8266：显示、轮播与桌宠

在“我的设备”选 **ESP8266 小屏**，或从托盘中的该设备分组进入：

| 入口 | 能做什么 |
| --- | --- |
| **显示设置** | 调节亮度；选择自动轮播、智能跟随或固定页面；勾选轮播页、调整顺序和 10/15/30/60 秒间隔。 |
| **外观设置** | 天气动画；通用、Claude、Codex 的“选择动画”；各角色“恢复默认”；素材图库。 |
| **数据设置** | 选择该设备所需的数据来源与模型厂商。 |
| **连接设置** | 自动、仅 USB、仅 Wi-Fi；屏保等待分钟数，0 为关闭自动屏保。 |
| **管理…** | 修改名称、停用／启用、固件升级、设备信息、连接诊断、重置网络和移除设备。 |

![当前显示设置：亮度、显示方式、轮播页面与顺序](assets/screens/cycle-settings.png)

选择“自动轮播”后保存，会按勾选页面和间隔循环；“智能跟随”按事件选择页面；固定页保持所选内容。完成刷机或测试后恢复原来的自动轮播及页面、顺序、间隔。

自动连接优先 USB，连续 8 秒无有效心跳后尝试已配对 Wi-Fi；仅 USB 模式不自动回退。设备仍有电但电脑数据失联时，可进入 `PC OFF` 独立时钟。镜像有画面不代表设备已经收到数据，需检查连接与实体屏。

换肤在 **外观设置 → 桌宠动画 → 选择动画**；“通用”用于两个角色，或分别选择 Claude / Codex。支持有许可说明的 PNG/JPG/BMP/GIF；导入后需同步到设备。恢复默认指已导入的本机角色默认资源，没有该资源时保留原选择并提示。公开包不附用户私人角色素材。[可选项目动画包](assets/pet/README.md)

<a id="pages"></a>

## 3. AI 状态、额度与日常页面

<p><img src="assets/screens/codex.png" width="240" alt="Codex 额度与自选桌宠，演示数据"> <img src="assets/screens/dual.png" width="240" alt="双额度页，演示数据"> <img src="assets/screens/activity.png" width="240" alt="AI 活动与本机 Token，演示数据"></p>

- **Claude / Codex 状态与额度：** ESP8266 在显示设置或小屏预览中切页；TAB5 在设备的总览、Codex、额度页面查看。账户额度与本机活动分别采集。先完成对应 CLI 登录，并启用所需数据；无授权、无相应计划或接口失败时可能显示 `--` 或旧缓存。
- **额度字段：** `5H` / `WK` 是已用比例；重置时间和重置卡取决于账户返回内容。未知字段不补造。本机 Token 只覆盖本机可见日志，不等于所有设备的账户用量。
- **额度历史：** 设备中心 → **账号数据 → 额度历史**，或小屏镜像底部“Codex 额度趋势”。支持近 7/30 天与图表／明细，保留 90 天采样；没有记录的日期不估算。[统计口径](QUOTA_TRENDS.md)
- **其他模型账号：** 设备中心 → **账号数据 → 模型账号**。按厂商选择网页授权或支持的 API 配置；数据设置决定各设备要用哪些厂商。DeepSeek 为 API 余额，智谱为开放平台可用余额，不等于 Coding Plan。阶跃、百度、小米等入口的“待验证／待接”状态不代表已经完成真实账号验收。[接入与边界](DOMESTIC_QUOTA_SETUP.md)

<p><img src="assets/screens/weather.png" width="240" alt="天气演示"> <img src="assets/screens/system.png" width="240" alt="系统监控演示"> <img src="assets/screens/music.png" width="240" alt="音乐演示与项目角色封面"></p>

| 内容 | 设置或使用方法 |
| --- | --- |
| 天气 | **账号数据 → 天气定位**设置城市与来源；小屏天气动画在该设备的“外观设置”。 |
| 股票 | **账号数据 → 自选股票**保存代码。支持 A/H/美股，最多 20 只；小屏每页四行。示例报价不是实时行情。 |
| 系统 | 设备数据设置启用系统信息，再在对应设备选择系统页；显示 CPU、内存与网速。 |
| 音乐 | 使用支持 Windows 系统媒体会话的播放器；显示曲名、进度与可用封面。未提供封面时显示占位，不保证所有播放器都可采集。 |

网络或厂商请求失败时保留最近成功数据。**桥接设置 → 服务状态**查看设备连接、数据更新时间和错误；**提醒管理**设置勿扰和查看提醒记录。需要输入或任务完成时可提示；不要把静态截图当作提示音验收。

## 4. TAB5：触控、Codex 直达与日历

电脑端选择 TAB5 后提供 **显示设置、语音设置、日历生日、常用任务、数据设置、连接升级**。托盘分组保留其中常用快捷入口；全部设置从设备中心进入。

![Codex 直达，固定演示会话](assets/screens/tab5-quick.png)

- **Codex 直达：** 点击 TAB5 右下角入口，从最近五个会话选择目标；语音结果进入该会话草稿，检查后再显式发送。支持继续语音、取消、错误恢复和长按清空。[当前操作说明](TAB5-QUICK-CONSOLE.md)
- **任务与历史：** 在 Codex 页面查看会话回复与历史；电脑端“常用任务”配置优先显示的任务。
- **语音与相机：** 语音设置管理识别方式与麦克风；设备编辑页可使用语音与相机附件。录音、草稿准备与发送是不同操作，不会因识别完成自动发送。
- **时钟与日历：** 多种时钟样式、月历、农历、节气、节假日及生日；电脑端“日历生日”管理提醒。

## 5. TAB5：全年点阵、十二月历与艺术屏保

TAB5 **设置 → 屏幕与声音**选择并预览屏保。八个选项为经典、年度点阵、暗夜、暖灰、十二月历、自动昼夜、每日名画、每日书法。

| 屏保 | 当前功能 |
| --- | --- |
| 年度点阵／暗夜／暖灰 | 全年每天一颗点，显示已过、今天、剩余日期和进度，支持 365／366 天。 |
| 自动昼夜 | 按设备本地 07:00–19:00 使用日间配色，其余时间切为夜间配色。 |
| 十二月历 | 每月花卉、农历、节气与节假日；预览可切月，自动屏保跟随实际月份。 |
| 每日名画／每日书法 | 独立图库；预览和自动屏保均支持正反横屏、左右竖屏，含书法后续分页。普通系统界面仍保持横屏。 |

<table><tr><td width="50%"><img src="assets/screens/tab5-annual.png" alt="全年点阵"></td><td width="50%"><img src="assets/screens/tab5-floral.png" alt="十二月花卉台历"></td></tr></table>

艺术屏保保留作品名、作者、博物馆与来源，显示已核实的中文译名；没有通用译名则保留馆藏原名。402 件名画、411 件书法分别下载，页数与方向不重复计作作品。图片同步需电脑及桥接运行，断线保留最近成功图片。[图库安装](GALLERY-PACKS.md) · [名画竖屏](assets/screens/tab5-painting-portrait.png) · [书法竖屏](assets/screens/tab5-calligraphy-portrait.png)

## 6. 安装、升级与配置保留

从 **设备中心 → 桥接设置 → 软件固件**打开“软件与固件更新”，查看各组件版本、说明和适用包。自动检查只提醒，不自动安装。首次安装硬件走“我的设备 → 添加”的对应首刷流程；后续升级按已安装设备办理。[完整安装指南](INSTALL.zh.md) · [更新步骤](UPDATES.md)

图库在更新窗口的“屏保图库”中导入 ZIP，软件升级保留图库。**桥接设置 → 配置迁移**提供备份、预览与选择恢复；配置、配对资料和设备原固件备份留在本地，不作为公开素材发布。

## 7. 平台与验证范围

| 平台 | 当前范围 | 验证说明 |
| --- | --- | --- |
| Windows 10/11 x64 | 双硬件设备中心、上述设置、统一更新与 TAB5 服务 | 当前发布构建和隔离回归通过；截图不是所有账号、全新安装或长期运行的验收。 |
| ESP8266 0.5.0 | 小屏页面、时钟、桌宠、USB / Wi-Fi | 沿用已发布固件；本次文档更新没有重刷或重新完成全部硬件验收。 |
| TAB5 0.2.145-ui | 触控、Codex 直达、八种屏保与艺术四方向 | 当前镜像和分页转向有[真机验收记录](TAB5-ACCEPTANCE-145.md)；不据此推定新一轮出厂首刷通过。 |
| macOS 13+ Apple Silicon | 菜单栏、镜像、ESP8266 与对应平台功能 | CI 测试／构建通过；未实现本文 Windows TAB5、统一更新及艺术导入入口。Intel Mac 未验证。 |

[发布明细](RELEASE-0.6.0.md) · [截图来源与复现](SCREENSHOTS.md) · [开发参考](REFERENCE.zh.md)

## English summary

This guide describes Windows 0.6.0 with ESP8266 0.5.0 and TAB5 0.2.145-ui. The current tray groups actions by device. Its left click opens the ESP8266 mirror only when an ESP8266 is enabled; otherwise it opens Device Center. Device Center separates My Devices, Account Data and Bridge Settings. Configure shared providers/weather/stocks in Account Data, device-specific display/data/connection settings in My Devices, and updates, startup, notifications and configuration migration in Bridge Settings. Screenshots use the same native controls as the application with isolated synthetic data. macOS has its own menu-bar/ESP8266 implementation and does not provide the Windows TAB5, unified-update or artwork-import flows. Refer to the linked release and hardware records for exact acceptance boundaries.
