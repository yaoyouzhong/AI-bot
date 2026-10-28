# 功能与界面图鉴

当前 Windows **0.6.12**、Mac **0.6.11**、TAB5 **0.2.282-ui**、ESP8266 **0.5.7**。两类图库各 366 件；公开内容版次均为 **2026.1**，提供标准与高清完整版；[内部包编号保留](GALLERY-VERSIONS.md)。[本版范围与验收](RELEASE-0.6.12.md) · [高清图库安装](GALLERY-HD.md) · [截图来源](SCREENSHOTS.md)

AI-bot 把 AI 任务状态、账户额度和日常信息放到桌边。先看两类设备的整体用法，再看各自重点，完整页面可按场景展开。

[认识设备](#overview) · [TAB5 触控大屏](#tab5) · [ESP8266 小屏](#esp8266) · [电脑端设置与更新](#settings)

截图来自原生界面的隔离渲染，数值、设备名称、连接状态、会话和曲目为固定演示数据，不是实体屏照片。桌宠采用项目自选动画预览；首次使用有内置 BYTE SPROUT，无需下载素材。[截图来源](SCREENSHOTS.md)

<a id="overview"></a>

## 1. 先认识两类设备

**选择 Windows 或 Apple Silicon Mac 电脑端，再按设备分别设置。** 可以使用任意一类设备，也可以各连接一台；固件和电脑端独立编号，不必一起升级。

| 设备 | 适合怎样使用 | 操作方式 | 主要区别 |
| --- | --- | --- | --- |
| **TAB5 · 1280×720** | 查看任务、触控操作、阅读回复与欣赏艺术作品 | 屏幕上的导航、应用和设置；电脑端配置账号与内容 | 提供 Codex 直达、语音／相机草稿、日历与日期倒计时等屏保；艺术屏保支持横竖屏 |
| **ESP8266 · 240×240** | 随时看 AI 状态、额度和轮播信息 | 在电脑端选择页面、轮播与桌宠 | 聚焦简洁展示；没有 TAB5 的触控、Codex 直达或艺术图库 |

Windows 与 Apple Silicon Mac 均支持两类设备，具体范围见[平台说明](#validation)。

<a id="tab5"></a>

## 2. TAB5：从总览到触控操作

### 整体界面与入口

总览集中显示 AI 状态、常用任务、额度和日期天气；底部 **总览 / Codex / 额度 / 应用**贯穿主要页面。进入“应用”可打开天气、电脑状态、行情、音乐、时钟或桌宠；右上角齿轮进入设备设置，右下角角色图标打开 Codex 直达。

<table><tr><td width="50%"><img src="assets/screens/tab5-overview.png" alt="TAB5 总览：状态、任务、额度与日期天气"></td><td width="50%"><img src="assets/screens/tab5-apps.png" alt="TAB5 应用：天气、电脑状态、行情、音乐、时钟与桌宠"></td></tr></table>

| 想做什么 | 从哪里进入 |
| --- | --- |
| 看 AI 状态与常用任务 | **总览**；任务阅读和编辑进入 **Codex** |
| 看账户用量、重置或 API 余额 | **额度**，选择对应模型厂商 |
| 看天气、行情、负载与音乐 | **应用**，选择对应卡片 |
| 换时钟样式、查看桌宠 | **应用 → 时钟 / 桌宠** |
| 看月历与生日 | 点击总览日期或顶部生日提醒；电脑端管理 **日历生日** |
| 调节屏幕、声音、轮播或升级 | 右上角 **设置**；六个设置分区见下方完整图集 |

### 日程待办与提醒

**应用 → 日程**使用下移的“今日／全部”分段筛选，左侧显示列表、右侧突出所选事项的时间与详情；无事项时居中显示“暂无事项”和“新建”。新建、编辑并设置开始时间、持续时长、重复和提前提醒。到点显示提醒框，可选择“知道了”或在时长允许时延后 10 分钟；到期事项保留。设备本地持久化，离线修改或删除重连后与电脑同步。最多 16 项，只有满额新增才清理最早结束的过期单次事项；保存失败保留原记录与输入。电脑端入口为设备中心的“日程待办”。点击名称旁麦克风时需要电脑语音服务；离线可用键盘输入。

<table><tr><td width="50%"><img src="assets/screens/tab5-agenda.png" alt="210 日程分段筛选、列表与独立详情"></td><td width="50%"><img src="assets/screens/tab5-agenda-empty.png" alt="210 日程空状态与居中新建"></td></tr></table>

<table><tr><td width="50%"><img src="assets/screens/tab5-agenda-editor.png" alt="TAB5 新建日程，时间、重复和提前提醒"></td><td width="50%"><img src="assets/screens/tab5-agenda-reminder.png" alt="TAB5 居中日程提醒框"></td></tr></table>

### 重点一：直接操作 Codex，阅读任务与回复

从右下角 **Codex 直达**选择最近五个会话之一，语音输入进入目标会话草稿，检查后显式发送。支持继续语音、取消、失败恢复和长按清空。**Codex 页面**用于选择项目与任务、查看回复和历史；电脑端“常用任务”配置优先显示的任务。[操作说明](TAB5-QUICK-CONSOLE.md)

<table><tr><td width="50%"><img src="assets/screens/tab5-quick.png" alt="Codex 直达：最近会话与语音草稿"></td><td width="50%"><img src="assets/screens/tab5-tasks.png" alt="Codex 任务：项目、任务列表与编辑区"></td></tr></table>

<details>
<summary>展开任务动态、历史回复、语音输入与相机附件</summary>

**任务动态与历史回复：** 查看当前会话状态、历史轮次与分页内容，返回后继续原任务。

<table><tr><td width="50%"><img src="assets/screens/tab5-activity.png" alt="Codex 任务动态与本机用量"></td><td width="50%"><img src="assets/screens/tab5-reply.png" alt="Codex 历史回复与分页阅读"></td></tr></table>

**语音草稿：** 语音设置管理识别方式和麦克风；编辑页录音后回填文字，可继续编辑。识别结果需检查，识别完成不会自动发送。Windows 与 Mac 的语音来源和系统权限不同。

<table><tr><td width="50%"><img src="assets/screens/tab5-voice-recording.png" alt="Codex 编辑页录音与文字回填"></td><td width="50%"><img src="assets/screens/tab5-voice-review.png" alt="识别结果回填草稿，检查后再发送"></td></tr></table>

**相机附件：** 编辑页打开相机，拍照确认后作为附件加入草稿，可移除，再与文字一起发送。下图相机内容为项目角色模拟素材，附件缩略图使用测试色块。

<table><tr><td width="50%"><img src="assets/screens/tab5-camera.png" alt="原生相机页面，内容为模拟素材"></td><td width="50%"><img src="assets/screens/tab5-photo-draft.png" alt="确认照片后添加到 Codex 草稿"></td></tr></table>

</details>

<details>
<summary>查看长按清空草稿</summary>

长按清空仅清除文字，已添加的照片保留。短按不会清空；清空后仍可继续编辑。

<table><tr><td width="50%"><img src="assets/screens/tab5-draft-clear-before-281.png" alt="长按清空前的演示草稿"></td><td width="50%"><img src="assets/screens/tab5-draft-clear-empty-281.png" alt="长按清空后的输入框"></td></tr></table>

</details>

### 重点二：全年点阵与十二月花卉台历

**全年点阵**每天一颗点，显示已过、今天与剩余日期，支持 365／366 天；**十二月历**结合每月花卉、农历、节气和节假日。两者无需额外图库。

<table><tr><td width="50%"><img src="assets/screens/tab5-annual.png" alt="全年点阵：每天一颗点与年度进度"></td><td width="50%"><img src="assets/screens/tab5-floral.png" alt="十二月历：当月花卉、农历与节气"></td></tr></table>

在 TAB5 **设置 → 屏幕声音**选择并预览。屏保选项包括经典时钟、年度点阵、年度点阵暖灰、十二月历、每日名画、每日书法和日期倒计时。经典时钟和年度点阵按设备本地 07:00–19:00 使用日间配色，其余为夜间配色；不再单列暗夜与自动昼夜。经典时钟秒数变化时时分位置稳定，每分钟保留轻微移位。十二月历预览可切月，自动屏保跟随实际月份。

<details>
<summary>展开经典时钟与点阵配色</summary>

<table><tr><td width="50%"><img src="assets/screens/tab5-classic-day.png" alt="经典时钟白天配色"></td><td width="50%"><img src="assets/screens/tab5-classic-night.png" alt="经典时钟夜间配色"></td></tr></table>

<table><tr><td width="50%"><img src="assets/screens/tab5-annual-night.png" alt="点阵暗夜配色"></td><td width="50%"><img src="assets/screens/tab5-annual-warm.png" alt="点阵暖灰配色"></td></tr></table>

</details>

### 重点三：横竖欣赏每日名画与书法

预览和自动艺术屏保均支持正反横屏、左右竖屏，书法后续分页也能转向；普通系统页面保持横屏。作品名、作者、博物馆与来源保留在画面中；已核实的中文译名直接显示，没有通用译名则保留馆藏原名。

<p><img src="assets/screens/tab5-painting-portrait.png" height="400" alt="每日名画竖屏：林良作品与馆藏署名"> <img src="assets/screens/tab5-calligraphy-portrait.png" height="400" alt="每日书法竖屏：王羲之快雪时晴帖（传唐摹本）与馆藏署名"></p>

本次精选作品集含 **366 件名画、366 件书法**，分别提供可选下载包，分页和方向不重复计作作品。图片同步需要电脑及 AI-bot 运行；断线保留最近成功图片。公开内容版次均为 **2026.1**；标准包内部编号分别为 2026.10.14.1、2026.10.15.1。两套高清完整包已含对应标准图和资料。[版次说明](GALLERY-VERSIONS.md)[图库安装与许可](GALLERY-PACKS.md)

<details>
<summary>展开名画与书法的横屏排版</summary>

<table><tr><td width="50%"><img src="assets/screens/tab5-painting.png" alt="每日名画横屏排版"></td><td width="50%"><img src="assets/screens/tab5-calligraphy.png" alt="每日书法横屏排版"></td></tr></table>

</details>

### 常用页面完整图集

下面按使用场景展开，包含额度、天气三种视图、行情、电脑状态、音乐、九种时钟、月历、桌宠和设备设置。

<details>
<summary>账户额度：Claude / Codex 用量、重置卡与模型 API 余额</summary>

进入底部 **额度**，用左上角厂商选择器切换。Claude / Codex 可显示账户返回的用量、重置时间和套餐；Codex 重置卡及到期时间取决于账号实际返回内容。

![TAB5 Codex 额度页：用量、重置时间与重置卡](assets/screens/tab5-quota.png)

DeepSeek 显示 API 可用余额，智谱显示开放平台可用余额；余额与 Coding Plan 用量采用不同布局。数据源未提供的字段显示 `--`。

<table><tr><td width="50%"><img src="assets/screens/tab5-deepseek.png" alt="DeepSeek API 可用余额"></td><td width="50%"><img src="assets/screens/tab5-zhipu.png" alt="智谱开放平台可用余额"></td></tr></table>

</details>

<details>
<summary>日常信息：天气实况／24 小时／7 天、行情、电脑状态与音乐</summary>

**天气：应用 → 天气。** 页内切换实况、24 小时预报和未来 7 天；电脑端 **账号数据 → 天气定位**选择城市与来源。

![TAB5 天气实况](assets/screens/tab5-weather.png)

<table><tr><td width="50%"><img src="assets/screens/tab5-hourly.png" alt="天气 24 小时预报"></td><td width="50%"><img src="assets/screens/tab5-seven-days.png" alt="天气未来 7 天预报"></td></tr></table>

**行情：应用 → 行情。** 按美股、港股、A 股分组，列表可滚动；电脑端 **账号数据 → 自选股票**保存代码，最多 20 只。截图报价为演示值。

![TAB5 行情：分组自选股票](assets/screens/tab5-stocks.png)

**电脑状态：应用 → 电脑状态。** 查看 CPU、内存、上传下载速率与近期网络曲线；设备数据设置需启用系统信息。

**音乐：应用 → 音乐。** 显示播放器提供的曲名、进度与封面；未提供封面时显示占位。可用时在音乐页控制上一首、播放／暂停和下一首，按当前播放器报告的能力启用。Windows 和 Mac 的数据来源与权限不同；具体兼容范围见[媒体支持说明](MEDIA-PLAYERS.md)。

<table><tr><td width="50%"><img src="assets/screens/tab5-system.png" alt="电脑状态：CPU、内存与网络曲线"></td><td width="50%"><img src="assets/screens/tab5-music-controls-282.png" alt="音乐：示例曲目、无封面占位和播放控制"></td></tr></table>

没有曲目时音乐页显示空闲，清除旧曲名与封面。 / With no current track, the music page clears stale metadata and artwork.

![音乐空闲原生界面](assets/screens/tab5-music-idle.png)

</details>

<details>
<summary>时钟、日历与桌宠：九种表盘、农历节气与生日</summary>

**应用 → 时钟**打开时钟页，用左右按钮或九宫格选择经典圆盘、纯数字、轨道圆盘、奶油刻度、方形表盘、昼夜环、几何玩色、星轨和液晶时光。时钟／倒计时使用顶部的分段入口，计时中只显示大数字，轻触后显示操作；切回时钟仍继续计时。

![经典圆盘时钟](assets/screens/tab5-clock-0.png)

<table><tr><td width="50%"><img src="assets/screens/tab5-clock-1.png" alt="纯数字时钟"></td><td width="50%"><img src="assets/screens/tab5-clock-2.png" alt="轨道圆盘时钟"></td></tr></table>

<table><tr><td width="50%"><img src="assets/screens/tab5-clock-3.png" alt="奶油刻度时钟"></td><td width="50%"><img src="assets/screens/tab5-clock-4.png" alt="方形表盘时钟"></td></tr></table>

<table><tr><td width="50%"><img src="assets/screens/tab5-clock-solar.png" alt="当地日出日落昼夜环"></td><td width="50%"><img src="assets/screens/tab5-clock-lcd.png" alt="经典绿色液晶时光"></td></tr></table>

<table><tr><td width="50%"><img src="assets/screens/tab5-clock-geometric.png" alt="几何玩色"></td><td width="50%"><img src="assets/screens/tab5-clock-star.png" alt="星轨时钟"></td></tr></table>

![九宫格表盘选择](assets/screens/tab5-clock-picker.png)

**两类倒计时独立：** 应用时钟内设置时长、开始／暂停／重置；日期倒计时在屏保设置中指定名称和日期，按天显示，未选中此屏保时隐藏配置。

<table><tr><td width="50%"><img src="assets/screens/tab5-clock-countdown.png" alt="时钟独立倒计时"></td><td width="50%"><img src="assets/screens/tab5-date-countdown.png" alt="屏保按天日期倒计时"></td></tr></table>

![日期倒计时配置入口](assets/screens/tab5-countdown-settings.png)

**日历**从总览日期或顶部生日提醒进入，显示月历、农历、节气、节假日与生日；电脑端 **日历生日**管理生日信息。**应用 → 桌宠**同时展示 Claude / Codex 状态与各自角色；下图为自选动画。

<table><tr><td width="50%"><img src="assets/screens/tab5-calendar.png" alt="日历：日期、节假日与生日"></td><td width="50%"><img src="assets/screens/tab5-pet.png" alt="桌宠：Claude 工作中与 Codex 空闲"></td></tr></table>

</details>

<details>
<summary>设备设置：网络、屏幕声音、轮播、存储、升级与设备信息</summary>

右上角齿轮打开 **设置**，六个分区分别管理网络连接、屏幕声音、内容轮播、存储管理、固件升级和设备信息。存储管理可选择设备缓存或 TF 卡；完整图库保存到 TF 卡后可离线查看。

<table><tr><td width="50%"><img src="assets/screens/tab5-settings-connection.png" alt="设备设置：连接与网络"></td><td width="50%"><img src="assets/screens/tab5-settings-screen.png" alt="设备设置：屏幕与声音"></td></tr></table>

<table><tr><td width="50%"><img src="assets/screens/tab5-settings-content.png" alt="设备设置：内容轮播"></td><td width="50%"><img src="assets/screens/tab5-settings-storage.png" alt="设备设置：存储管理和缓存位置"></td></tr></table>

![设备设置：.282 更新说明与演示旧版本](assets/screens/tab5-firmware-notes-282.png)

![设备信息（演示元数据）](assets/screens/tab5-settings-info.png)

固件升级图使用正式 .282 更新说明；图中“当前版本 0.2.21-ui”为隔离夹具，并非实际设备版本，也不表示所有历史分区均可直接升级。

电脑端“我的设备”选 TAB5，可打开 **显示设置、语音设置、日历生日、常用任务、数据设置、连接升级**；托盘分组提供常用快捷入口。[安装与连接步骤](INSTALL.zh.md#tab5-first)

</details>

<a id="esp8266"></a>
<a id="pages"></a>

## 3. ESP8266 小屏：状态与额度，一眼可见

### 整体用法：电脑配置，小屏自动展示

小屏以 **Claude / Codex 状态与额度**为主，电脑端选择固定页、智能跟随或自动轮播。天气、行情、系统负载、音乐和桌宠作为可选轮播页；240×240 页面显示关键信息。

<p><img src="assets/screens/claude.png" width="240" alt="Claude 状态与账户用量"> <img src="assets/screens/codex.png" width="240" alt="Codex 状态、额度与重置卡"> <img src="assets/screens/dual.png" width="240" alt="Claude / Codex 双额度总览"> </p>

### 重点：看任务状态、额度和重置

角色状态反映工作、空闲及等待输入；可提示任务完成。账户页面显示套餐、已用比例、重置倒计时，以及账号提供的 Codex 重置卡与到期时间；双额度页便于同时查看两个账号。

小屏 0.5.7 已加粗重置卡文字；显示内容仍取决于账户实际返回。上方镜像图用于展示版式，字形以以下对照及实体屏为准。[查看字形对照](assets/screens/esp8266-reset-text-057.png)

完成对应 CLI 登录，在 **账号数据**配置账号，再在小屏 **数据设置**启用所需来源。**额度历史**位于账号数据，或小屏镜像底部“Codex 额度趋势”。

<details>
<summary>展开 AI 活动、本机 Token 与其他模型额度</summary>

**AI 活动**展示当前状态和本机今日 Token；阿里 Token Plan、Kimi、MiniMax 用对应套餐字段，DeepSeek / 智谱用余额布局。

<p><img src="assets/screens/activity.png" width="240" alt="AI 活动与本机今日 Token"> <img src="assets/screens/domestic_alibaba.png" width="240" alt="阿里 Token Plan 套餐用量"> <img src="assets/screens/domestic_kimi.png" width="240" alt="Kimi 套餐用量"> </p>

<p><img src="assets/screens/domestic_minimax.png" width="240" alt="MiniMax 套餐用量"> <img src="assets/screens/domestic_deepseek.png" width="240" alt="DeepSeek API 余额"> <img src="assets/screens/domestic_zhipu.png" width="240" alt="智谱开放平台余额"> </p>

支持程度取决于厂商接口和实际授权；“待验证／待接”入口不是已完成账号验收。[账号接入与边界](DOMESTIC_QUOTA_SETUP.md)

</details>

### 常用页面与操作

| 想做什么 | 从哪里进入 |
| --- | --- |
| 切到某个页面 | 小屏 **显示设置**选择固定页，或在电脑端 **设备预览 → ESP8266 小屏**切换 |
| 自动切换所选页面 | **显示设置 → 自动轮播**，选择页面、顺序与间隔 |
| 跟随 AI 事件切页 | **显示设置 → 智能跟随** |
| 更换角色与天气动画 | 该设备 **外观设置** |
| 配置来源与连接 | 该设备 **数据设置 / 连接设置** |
| 看版本、升级或诊断 | 该设备 **管理…**；统一更新见下文 |

<details>
<summary>展开天气、行情、电脑负载与音乐四类页面</summary>

<p><img src="assets/screens/weather.png" width="240" alt="天气：城市、温度与时钟"> <img src="assets/screens/stocks.png" width="240" alt="行情：每页四行自选股票"> </p>

<p><img src="assets/screens/system.png" width="240" alt="电脑负载与网络曲线"> <img src="assets/screens/music.png" width="240" alt="音乐曲名、进度与示例封面"> </p>

城市在 **账号数据 → 天气定位**设置，自选股票在 **账号数据 → 自选股票**保存；支持 A/H/美股，最多 20 只，小屏每页四行。系统显示 CPU、内存和网速；音乐使用支持系统媒体会话的播放器，封面取决于播放器是否提供。

</details>

<details>
<summary>展开桌宠与时钟屏保</summary>

<p><img src="assets/screens/pet.png" width="240" alt="桌宠页面，自选项目角色"> <img src="assets/screens/screensaver.png" width="240" alt="小屏时钟屏保与日期"> </p>

桌宠可随 AI 状态播放自选动画。小屏屏保以时钟和日期为主，等待时间在 **连接设置**调整，0 为关闭自动屏保；TAB5 的全年点阵、花卉台历与艺术图库不适用于小屏。

</details>

<details>
<summary>展开显示、轮播、外观、连接与管理设置</summary>

在“我的设备”选 **ESP8266 小屏**，或从托盘中的该设备分组进入：

| 入口 | 能做什么 |
| --- | --- |
| **显示设置** | 调节亮度；选择自动轮播、智能跟随或固定页面；勾选轮播页、调整顺序和 10/15/30/60 秒间隔。 |
| **外观设置** | 天气动画；通用、Claude、Codex 的“选择动画”；各角色“恢复默认”；素材图库。 |
| **数据设置** | 选择该设备所需的数据来源与模型厂商。 |
| **连接设置** | 自动、仅 USB、仅 Wi-Fi；屏保等待分钟数，0 为关闭自动屏保。 |
| **管理…** | 修改名称、停用／启用、固件升级、设备信息、连接诊断、重置网络和移除设备。 |

<a href="assets/screens/cycle-settings.png"><img src="assets/screens/cycle-settings.png" width="440" alt="当前显示设置：亮度、显示方式、轮播页面与顺序"></a>

选择“自动轮播”后保存，会按勾选页面和间隔循环；“智能跟随”按事件选择页面；固定页保持所选内容。

自动连接优先 USB，连续 8 秒无有效心跳后尝试已配对 Wi-Fi；仅 USB 模式不自动回退。设备仍有电但电脑数据失联时，可进入 `PC OFF` 独立时钟。镜像有画面不代表设备已经收到数据，需检查连接与实体屏。

换肤在 **外观设置 → 桌宠动画 → 选择动画**；“通用”用于两个角色，或分别选择 Claude / Codex。支持有许可说明的 PNG/JPG/BMP/GIF；导入后需同步到设备。恢复默认指已导入的本机角色默认资源，没有该资源时保留原选择并提示。公开包不附用户私人角色素材。[可选项目动画包](assets/pet/README.md)

</details>

<a id="settings"></a>

## 4. 电脑端：配置账号、管理设备与统一更新

### 设备中心是统一入口

截图按窗口类型控制显示大小；点击可查看原图。 / Screenshots are sized by window type; select one to view the full image.

<a href="assets/screens/tray-menu.png"><img src="assets/screens/tray-menu.png" width="160" alt="Windows 托盘菜单：设备中心、设备预览与按设备分组的设置"></a>

Windows 右键托盘打开菜单：**设备中心**是统一入口，**设备预览**按已启用设备提供小屏或 TAB5 画面，双设备时先选择目标。后面按设备分组设置；名称随你的命名变化，停用设备的快捷项不可用。左键托盘在启用小屏时打开／隐藏小屏预览，否则打开设备中心。预览不替代实体屏与连接状态检查。

<a href="assets/screens/device-center.png"><img src="assets/screens/device-center.png" width="720" alt="设备中心的我的设备页，演示 TAB5"></a>

| 页面 | 当前入口与用途 |
| --- | --- |
| **我的设备** | 添加 ESP8266 / TAB5，查看连接和固件版本；选择设备后进入该设备的设置。每种型号最多一台。 |
| **账号数据** | 模型账号、天气定位、自选股票、额度历史；公共数据源在电脑上配置，各设备按各自的数据设置使用。 |
| **桥接设置** | 开机启动、服务状态、软件固件、配置迁移、提醒管理、关于应用。 |

<details>
<summary>查看账号数据与桥接设置页面</summary>

<p><a href="assets/screens/device-center-accounts.png"><img src="assets/screens/device-center-accounts.png" width="720" alt="账号数据"></a></p>
<p><a href="assets/screens/device-center-bridge.png"><img src="assets/screens/device-center-bridge.png" width="720" alt="桥接设置"></a></p>

</details>

<a id="mac-native"></a>

### Mac 原生界面 / Native Mac interfaces

Mac **0.6.11 正式版**从菜单栏 AI-bot 图标进入：左键打开设备中心，右键展开下方菜单。**设备预览**在双设备时先选择 ESP8266 或 TAB5；后面的设备分组提供各自的设置入口。Windows 从托盘进入，以上 Windows 窗口保留各自平台标注。[Mac 安装与配对](INSTALL.zh.md#mac)

<a href="assets/screens/mac-menu.png"><img src="assets/screens/mac-menu.png" width="250" alt="Mac 原生 AI-bot 菜单：设备中心、设备预览与双设备分组，演示数据"></a>

**我的设备**列出两类硬件。选择 TAB5 后可进入显示、语音、日历生日、日程待办、电脑控制、屏保图库、常用任务、数据与连接升级；右上角 **管理 ▾**提供名称、启停与移除入口。

<a href="assets/screens/mac-device-center.png"><img src="assets/screens/mac-device-center.png" width="720" alt="Mac 原生设备中心：双设备列表与 TAB5 主要操作入口，未配对演示状态"></a>

<details>
<summary>查看 Mac 账号数据与桥接设置 / Account Data and Bridge Settings</summary>

**账号数据**配置模型账号、天气定位、自选股票与额度历史；**桥接设置**提供开机启动、服务状态、软件固件、配置迁移、提醒管理、运行监控和关于应用。

<p><a href="assets/screens/mac-accounts.png"><img src="assets/screens/mac-accounts.png" width="720" alt="Mac 原生账号数据页：模型账号、天气、自选股票和额度历史入口，隔离演示数据"></a></p>
<p><a href="assets/screens/mac-bridge-settings.png"><img src="assets/screens/mac-bridge-settings.png" width="720" alt="Mac 原生桥接设置页：服务、更新、配置迁移、提醒和运行监控入口，隔离演示状态"></a></p>

</details>

**TAB5 设备预览**保留 Mac 原生标题栏、窗口边界和“仅查看”提示。下图装载已登记的 TAB5 纯数字时钟演示画面，窗口内明确标为非实时连接；未连接设备不会因此获得画面。预览不替代实体屏与连接检查。

<a href="assets/screens/mac-tab5-preview.png"><img src="assets/screens/mac-tab5-preview.png" width="720" alt="Mac 原生 TAB5 设备预览窗口：沿用已登记的纯数字时钟演示图，非实时连接"></a>

以上五图在 macOS 上以 **0.6.11 同源 AppKit 控件和菜单工厂**隔离捕获，使用演示设备与数据，未读取私人账号、额度、地址或配对标识，未操作真实设备或保存桥接设置。已对照本机运行的 R36 界面及正式发布包；本机旧候选的 0.6.10 版本标签不作为正式版证据。TAB5 屏幕内容沿用原有来源，不代表新的固件或实机验收。[捕获版本、方法与摘要](SCREENSHOTS.md#mac-native-20261028)

The five Mac captures use the **same AppKit controls and menu factory as released Mac 0.6.11**, with isolated demo devices and data. Left-click the menu-bar icon for Device Center; right-click for the menu. My Devices provides device actions, Account Data manages shared sources, and Bridge Settings provides service, update, migration and notification tools. The native TAB5 preview loads an existing attributed clock fixture and explicitly says it is not a live connection. Captures were compared with the running R36 app and the published package; the older local candidate's 0.6.10 label is not release evidence. No private account data, device identifiers or real settings are published or changed.

Mac 的麦克风、语音识别、蓝牙和辅助功能等权限按实际使用功能授权；解锁与音乐接入使用 Mac 自身方案。 / Microphone, speech, Bluetooth and Accessibility permissions, music sources and unlock behavior follow macOS-specific paths.

### 账户数据的含义

- **额度字段：** `5H` / `WK` 是已用比例；重置时间和重置卡取决于账户返回内容。未知字段不补造。本机 Token 只覆盖本机可见日志，不等于所有设备的账户用量。
- **额度历史：** 设备中心 → **账号数据 → 额度历史**，或小屏镜像底部“Codex 额度趋势”。支持近 7/30 天与图表／明细，保留 90 天采样；没有记录的日期不估算。[统计口径](QUOTA_TRENDS.md)
- **其他模型账号：** 设备中心 → **账号数据 → 模型账号**。按厂商选择网页授权或支持的 API 配置；数据设置决定各设备要用哪些厂商。DeepSeek 为 API 余额，智谱为开放平台可用余额，不等于 Coding Plan。阶跃、百度、小米等入口的“待验证／待接”状态不代表已经完成真实账号验收。[接入与边界](DOMESTIC_QUOTA_SETUP.md)

网络或厂商请求失败时保留最近成功数据。**桥接设置 → 服务状态**查看设备连接、数据更新时间和错误；**提醒管理**设置勿扰和查看提醒记录。

### 安装、升级与配置保留

从 **设备中心 → 桥接设置 → 软件固件**打开“软件与固件更新”，查看各组件版本、说明和适用包。自动检查只提醒，不自动安装。首次安装硬件走“我的设备 → 添加”的对应首刷流程；后续升级按已安装设备办理。[完整安装指南](INSTALL.zh.md) · [更新步骤](UPDATES.md)

设备管理右上角使用 **管理 ▾** 展开操作。Wi-Fi 按“未联网”“已联网 · 桥接未通”“已联网 · 桥接正常”和“等待设备回报”区分；已连路由器但 Wi-Fi 桥接通道未通时显示黄色，不等于 USB 通道不可用。首页与连接页采用同一判断。

图库在更新窗口的“屏保图库”中导入 ZIP，软件升级保留图库。“浏览作品”支持往期查找、标题／作者／拼音搜索、键盘循环翻页、高清缩放与独立阅读；[高清完整包](GALLERY-HD.md)无需预装标准版。左侧列表切换作品，图片下方翻页；全屏支持滚轮缩放和拖动，右键或 Ctrl+0 还原。目录和安装说明在“更多”菜单。**桥接设置 → 配置迁移**提供备份、预览与选择恢复；配置、配对资料和设备原固件备份留在本地，不作为公开素材发布。

<details>
<summary>展开软件与固件更新窗口</summary>

<a href="assets/screens/update-center.png"><img src="assets/screens/update-center.png" width="720" alt="统一更新：分别查看电脑端和两类固件的版本及说明"></a>

</details>

<details>
<summary>展开年度图库与电脑浏览界面</summary>

<a href="assets/screens/gallery-packs.png"><img src="assets/screens/gallery-packs.png" width="680" alt="年度图库：主要操作排成一行"></a>

<a href="assets/screens/gallery-browser.png"><img src="assets/screens/gallery-browser.png" width="800" alt="浏览作品：紧凑翻页、阅读资料与全屏"></a>

<a href="assets/screens/gallery-fullscreen.png"><img src="assets/screens/gallery-fullscreen.png" width="800" alt="全屏放大：滚轮缩放与拖动"></a>

</details>

### 运行监控与离线书画

Windows 的运行监控显示 VPN／PAC、电脑负载、异常原因和处理建议；TAB5 系统页同步简洁状态。告警详情与声音按设置工作，同一异常避免重复提醒，恢复后清除提示。

<a href="assets/screens/windows-runtime-monitor-0612.png"><img src="assets/screens/windows-runtime-monitor-0612.png" width="560" alt="Windows 运行监控：模拟 PAC 异常、电脑状态和处理建议"></a>

![TAB5 运行监控](assets/screens/tab5-runtime-health.png)

![TAB5 检测详情：原因、数据与建议](assets/screens/tab5-runtime-health-detail.png)

主动选择 TF 卡并保存完整图库后，每日书法与名画可离线预览。横屏的作品名、作者和年代显示在图画旁，竖屏保留简化信息。启动首先显示总览，再恢复原有展示设置。[实机边界](development/TAB5-194-ACCEPTANCE-20261014.md)

Runtime monitoring reports connection and computer-health causes and advice. Completed TF galleries remain available offline; artwork captions use side placement in landscape and compact portrait information. Overview appears at startup, followed by the retained display settings. These native screenshots use isolated data.

## 电脑控制与网络 / Computer controls and network

**应用 → 控制**调节电脑音量、静音或锁屏；静音图标为扬声器加斜杠，数字／图标位于静音按钮上方居中。电脑锁定后显示“已锁屏”，解锁后恢复。最多四个快捷启动项在 Windows 设备中心配置，显示程序或文件夹图标。播放／暂停及上一首／下一首在音乐页，按实际媒体会话能力启用。[完整说明](COMPUTER-CONTROL.md)

![电脑控制与启动项，隔离演示](assets/screens/tab5-computer-control.png)

<table><tr><td width="50%"><img src="assets/screens/tab5-computer-control-muted.png" alt="静音状态"></td><td width="50%"><img src="assets/screens/tab5-computer-control-locked.png" alt="电脑已锁屏"></td></tr></table>

网络设置最多保存 **10 个 Wi-Fi**，迁移既有记录、备注与优先项。黄色图标表示已经连上路由器但电脑桥接尚未通过；“桥接正常／鉴权通过”才表示电脑通路已就绪。下图选中第十项，使用合成网络名，不是真机配置清单。

![十网络容量原生预览](assets/screens/tab5-network-ten.png)

Windows 0.6.9 + TAB5 217 provide volume/mute/lock, actual lock state and four local launchers with icons. Media controls follow the matching Windows media session. Save ten networks with migration; router connectivity and authenticated bridge readiness are separate states. Native fixture images do not establish hardware acceptance.

<a id="validation"></a>

## 5. 平台与验证范围

| 平台 | 当前范围 | 验证说明 |
| --- | --- | --- |
| Windows 10/11 x64 | 双硬件设备中心、上述设置、统一更新与 TAB5 服务 | 当前发布构建和隔离回归通过；截图不是所有账号、全新安装或长期运行的验收。 |
| ESP8266 0.5.7 | 小屏页面、时钟、桌宠、USB / Wi-Fi 与固定设备身份 | 真实 USB 换口与未配置 LAN 恢复已验证，具体记录见本版发布说明。 |
| TAB5 0.2.282-ui | 电脑控制、十网络、横屏翻转、时钟、图库、日程与存储 | 282 的系统告警、诊断、检测详情与返回操作经用户实机确认；217 精确镜像启动、215 控制／锁屏／无闪动及拔 USB 后 Wi-Fi 音量获确认；210 的日程、内存和重启验收沿用；满额腾位、BLE 整包 OTA 和长期运行不扩大验收结论。 |
| macOS 13+ Apple Silicon | 菜单栏、镜像、ESP8266 与对应平台功能 | CI 测试／构建通过；现支持 TAB5 连接、控制、预览、固件和图库管理；授权及语音来源按平台区分。Intel Mac 未验证。 |

[发布明细](RELEASE-0.6.12.md) · [截图来源与复现](SCREENSHOTS.md) · [开发参考](REFERENCE.zh.md)

## English summary

AI-bot puts AI activity, account quotas and everyday information on a desktop display. This guide covers Windows 0.6.12, Mac 0.6.11, ESP8266 0.5.7 and TAB5 0.2.282-ui, plus two independent 366-work annual collections. Browse past works on the computer; select TF storage explicitly for complete-library caching and read complete long originals on the supporting firmware. Start with the device overview, then follow the matching device tour.

**TAB5:** overview and the four main navigation tabs come first. Highlights cover Codex Direct and task reading, Annual Dots and the floral calendar, and landscape/portrait art. Expand the grouped galleries for activity/history, voice/camera drafts, quotas and API balances, current/hourly/seven-day weather, markets, computer metrics, music, nine clock faces, calendar, pets and all settings panels, including independent Storage Management. Voice results and camera attachments enter drafts; sending is explicit. Art screensavers support portrait rotation; normal system pages remain landscape and can automatically flip 180 degrees with touch following. Art collections are optional downloads.

**ESP8266:** AI activity, quotas, reset times/cards and configurable cycling are the primary use. Expand the galleries for other providers, weather, stocks, metrics, music, pets and the clock screensaver. Control pages and settings from the computer; TAB5 touch operations, Codex Direct and art collections do not apply to the small display.

Device Center groups My Devices, Account Data and Bridge Settings. Components update independently through the Windows unified update window. Screenshots are native renders with isolated synthetic data, not hardware photos or additional acceptance evidence. macOS supports both ESP8266 and TAB5 through its native menu-bar app, including device, firmware and artwork management; permissions and speech sources differ by platform. See the linked installation, provenance and hardware records for exact boundaries.

Windows and Mac have separate permission, speech, music and unlock paths. The dedicated native Mac gallery above adds five AppKit menu/window captures with isolated demo data, separate from Windows captures and shared TAB5 screen fixtures. The collection manager keeps primary actions on one row, with directory/help in More. Select works in the left list; compact image paging, reading and fullscreen remain below the image. Right-click or Ctrl+0 restores the image; wheel zoom and drag also work fullscreen.

Agenda/reminders in TAB5 210 support local persistence, offline edits/deletions and reconnect sync, with explicit save, repeat and advance reminders. Complete optional HD editions include device previews and guides; desktop browsing prefers verified HD originals with bounded decoding. The refined agenda, internal memory, USB writing and connected-USB reboot battery display passed local 210 hardware acceptance; native screenshots remain isolated fixtures. Offline name entry uses the keyboard; dictation requires the computer voice service.

## 电脑一键解锁

TAB5 可经 USB、蓝牙或 Wi-Fi 解锁；Windows 端保存 PIN，TightVNC 密码自动生成或从兼容安装自动接入，页面不要求填写。组件安装／卸载需管理员确认，已有外部安装保留原配置。最终验收与支持边界见 [本轮收尾](development/RELEASE-0610-229-CLOSEOUT-20261019.md)。
