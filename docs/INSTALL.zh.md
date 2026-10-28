# AI-bot 安装与刷机指南

适用于 **Windows 0.6.13、Mac 0.6.13、TAB5 0.2.282-ui、ESP8266 0.5.7**，说明更新于 2026-10-28。Windows 与 Apple Silicon Mac 均支持两类设备。

**先安装电脑应用，再按设备当前系统选择路线。** 第一次只需完成 USB 接入、添加设备和屏幕检查；模型账号、语音、Wi-Fi 和图库都可稍后设置。

| 设备现在是什么状态 | 下一步 |
| --- | --- |
| 已有 AI-bot 界面，只是换了电脑或首次添加到应用 | [直接验证并添加](#connect)，不首刷 |
| TAB5 仍是出厂系统 | [TAB5 首次安装](#tab5-first)，使用首刷 ZIP |
| ESP8266 小屏尚未安装 AI-bot | [ESP8266 首次安装](#esp8266)，使用小屏固件 ZIP |
| 已在使用 AI-bot，需要更新 TAB5 固件 | [TAB5 后续升级](#tab5-upgrade)，使用升级 ZIP 中的 BIN |

Mac 用户先看[应用安装与权限](#mac)，再按相同设备状态选择安装或升级。本文电脑窗口截图来自 Windows，不表示 Mac 控件名称完全相同。

## 1. 下载与安装

从[完整下载中心](https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md)一次找到电脑桥接、小屏固件、TAB5 首刷／升级包与书法／名画图库。已有图库无需重下。按用途选择：

| 你要做什么 | 下载什么 |
| --- | --- |
| Windows 安装应用 | [Windows 安装器](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.13/AI-bot-bridge-v0.6.13-windows-x64-setup.exe) |
| Mac 安装应用（macOS 13+、M 系列芯片） | [Mac 应用 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.13/AI-bot-bridge-v0.6.13-macos-arm64.zip) |
| ESP8266 首刷或更新 | [ESP8266 固件 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.7/AI-bot-esp8266-0.5.7-firmware.zip) |
| TAB5 从出厂系统首次安装 | [TAB5 首刷 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.282-ui/AI-bot-tab5-0.2.282-ui-first-install.zip) |
| TAB5 已安装 AI-bot，更新固件 | [TAB5 升级 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.282-ui/AI-bot-tab5-0.2.282-ui-upgrade.zip) |

**TAB5 首刷也是 0.2.282-ui，无需先刷旧版。** 首刷包直接选择 ZIP；升级包先解压，再选里面的 `aibot_tab5.bin`，同目录保留 `aibot_tab5.bin.notes.json`。两种硬件的固件不可互换；GitHub 的 Source code 不是刷机文件。

桥接、ESP8266 和 TAB5 分别更新，版本号可以不同；以后只升级更新说明要求的部分，无需每次一起刷机。

以后从“设备中心 → 桥接设置 → 软件固件”打开[软件与固件更新](UPDATES.md)，按组件下载、校验并安装。名画／书法是可选的[独立图库](GALLERY-PACKS.md)，不下载也可使用其他功能。

### Windows 安装

1. 运行 Windows 安装器，通常保留默认位置。安装器未签名，请先确认文件来自本项目下载入口。已有用户先正常退出桥接，再安装到原位置。
2. 按“检查运行环境”页继续。缺少 .NET 8 桌面运行时或 WebView2 时会联网补装，已有组件会跳过；下载慢时查看下载进度，失败可按提示重试。
3. 安装器按既定流程尝试配置电脑解锁组件，可能请求管理员许可。取消或配置失败时会显示“AI-bot 已安装，解锁配置尚未完成”；这不等于设备连接失败，解锁可稍后在 **TAB5 → 电脑控制 → 一键解锁设置** 中保存并重试。这里无需填写 Windows PIN 或密码。
4. 启动 AI-bot。没有设备时会自动打开设备中心；窗口关闭后，点右下角托盘 AI-bot 图标即可再次打开。已有小屏时单击可能打开预览，可右键选择 **设备中心**。

如果安装器下载运行环境很慢，可先从微软下载并安装 [Windows .NET 8 桌面运行时 x64](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe)，再重新运行 AI-bot 安装器；已检测到的组件会跳过。这是当前安装器使用的同一版本，不能用普通 .NET Runtime 或 ASP.NET Runtime 代替桌面运行时。

<a id="connect"></a>
### 第一次连接设备

![图 1：设备中心；添加新设备从“添加”开始，已有设备先在列表中选中](assets/screens/device-center.png)

1. 用支持数据传输的 USB 线连接设备；屏幕亮起只说明有供电。TAB5 使用 USB-C 数据接口。
2. 点 **我的设备 → 添加**（空列表也可点“添加第一台设备”），选择型号，点 **刷新设备**，核对端口和设备名称。多个端口不确定时，可拔插这台设备后再次刷新。
3. **已有 AI-bot 界面：** 点 **验证并添加**。TAB5 会同时完成 USB 配对；ESP8266 会验证小屏身份。**仍是出厂系统：** 使用下面对应的首次安装路线，完成后再返回添加。
4. 在设备中心确认这台设备已出现、状态在线，再检查实体屏幕有画面、时钟持续更新，切换显示页面能响应。做到这里才算首次接入完成；不要求先登录所有模型账号。

当前支持每种型号一台。验证超时、列表为空或只有供电时，先按[排错表](#troubleshooting)检查，不通过再次首刷修复普通连接问题。

![图 2：添加设备；选择型号，出厂设备点“首次安装…”，已有固件点“验证并添加”](assets/screens/add-device.png)

<sub>截图来自当前原生 Windows 程序，使用隔离演示数据；端口号仅为示例，操作时选择自己的设备。</sub>

## 2. 按硬件刷机（选一条）

<a id="esp8266"></a>
### A. ESP8266：首次安装或更新 · 4 步

准备 USB 数据线，设备须为支持的 ESP8266 / ESP-12S、240×240 ST7789 小屏；其他板卡先核对[引脚配置](../firmware/platformio.ini)。

**线材提醒：小屏随附电源线只能供电，不能用于刷机或 USB 桥接。请换用支持数据传输的 USB 数据线；屏幕亮起不代表电脑能识别设备。**

**Cable note:** The compact display's included power cable does not carry data. Use a USB data cable for flashing and USB bridge connections; a lit screen alone does not mean the computer can detect it.

1. **连接并打开刷机。** 插 USB；新设备在“添加”里选 **ESP8266 小屏 → ESP8266 首次安装…**，已有设备选中小屏 → **管理… → 固件升级**。关闭其他串口工具，保持 AI-bot 桥接运行。
2. **选择文件并刷入。** 点 **选择文件**，直接选下载的 ESP8266 固件 ZIP，无需解压；确认设备后点 **开始刷机**。工具自动先备份，再写入和回读校验；过程中保持供电与连接。

![图 3：ESP8266 刷机；先“选择文件”，再“开始刷机”](assets/screens/firmware-flasher.png)

3. **看到完成后添加。** 提示“刷机完成”后关闭窗口；新设备回“添加”刷新，点 **验证并添加**；已有设备等待 USB 恢复。失败不要当作成功，按[排错](#troubleshooting)处理。
4. **检查屏幕并收尾。** 在设备中心打开 **显示设置**，确认实体屏能更新、切页；恢复自动显示与循环展示，保留原页面、顺序和间隔。

<a id="tab5-first"></a>
### B. TAB5：从出厂系统首次安装 · 4 步

使用 **USB-C 数据接口**连接电脑。首次安装会替换 P4 原固件与设置，工具先完整备份；保留备份以便恢复。已有 AI-bot 的设备使用下方升级路线。

1. **进入安装窗口与下载模式。** “添加”里选 **M5Stack TAB5 → TAB5 首次安装…**。连接 USB-C，长按 RESET 约 2 秒，内部绿灯快速闪烁后松开；点 **刷新设备**，选择这台 TAB5 的端口。[进入下载模式的官方说明](https://docs.m5stack.com/zh_CN/guide/tab5/restore_factory)。
2. **选择首刷 ZIP 并安装。** 点 **选择安装包…**，选 `AI-bot-tab5-0.2.282-ui-first-install.zip`，勾选“确认是 TAB5，允许替换原固件及设置”，点 **备份并安装**。按提示确认设备；等待备份、写入、校验结束，不拔线。

![图 4：TAB5 首次安装；上方选端口，中间选 ZIP 并安装，下方检查启动](assets/screens/tab5-first-install.png)

3. **重启并检查。** 提示“写入校验通过”后短按 RESET，等待屏幕启动；点 **刷新设备**，重新选择启动后的端口，再点 **检查启动**。检查通过后点底部“返回添加设备”，再完成验证并添加。端口号变化属于正常情况。
4. **添加并确认数据。** 回“添加”刷新，选重启后的 TAB5，点 **验证并添加**，此操作会完成 USB 验证与配对。确认实体屏、触摸和数据更新；需要重新配对时使用 **连接升级 → USB 配对 → 刷新设备 → 配对**。保留 TAB5 当前展示设置，无需启用轮播；Wi-Fi 可稍后配置。

![图 5：TAB5 USB 配对；若尚未配对，选择本机端口后点“配对”](assets/screens/tab5-usb-pairing.png)

<a id="tab5-upgrade"></a>
### C. TAB5：已有 AI-bot，后续升级 · 4 步

1. **解压并打开升级页。** 完整解压 `AI-bot-tab5-0.2.282-ui-upgrade.zip`。设备中心选中 TAB5 → **连接升级 → 固件升级**，保持有效连接和供电。
2. **提供固件。** 点 **选择固件…**，选解压后的 `aibot_tab5.bin`，同目录保留更新说明文件；核对版本 **0.2.282-ui** 与更新说明，确认提供。

![图 6：TAB5 固件升级界面示例；升级完成后在状态旁点“核验启动”](assets/screens/tab5-upgrade.png)

3. **开始升级。** 支持的设备在电脑点 **开始升级**；旧固件先在 TAB5 上完成一次升级。等待传输、校验与重启。“已提供固件”只是准备完成，不代表已安装。
4. **核验并检查显示。** 重启后用 USB 连接电脑，在升级页的核验状态旁点 **核验启动**，看到“启动核验通过”后检查触摸与数据；保留原连接模式、亮度和展示设置，不要求 TAB5 恢复自动模式或启用轮播。

**传输测速：** “连接升级 → 传输测速”选择 **图库写卡**或 **通信传输**，点 **开始测速**。前者测独立样本的传输、校验和写卡速度，保留现有图库；后者只测通信。默认显示速度和完成状态，点 **查看详细数据**展开分段耗时。需连接 USB 读取结果；图库测试通道按 TAB5 的连接方式选择。

**Transfer tests:** Select **Gallery/Card** or **Communication** in the transfer test tab, then **Start Test**. Gallery tests include sample transfer, validation and card writes while retaining installed collections; communication tests do not write to the card. Expand details for phase timings. USB is required to read results; gallery transport follows the device connection setting.

![TAB5 传输测速界面示例：选择测试类型，开始／停止，结果与可展开的详细数据](assets/screens/tab5-transfer-test.png)

首刷 ZIP 不用于应用升级。旧分区不兼容时停止操作，按对应迁移方案处理，不用首刷来保留已有设置。

<a id="optional"></a>
## 3. 可选设置

设备中心分三页：**我的设备、账号数据、桥接设置**。账号数据提供模型账号、天气定位、自选股票和额度历史；我的设备中选择一台设备，再配置显示、数据与连接；桥接设置提供开机启动、服务状态、软件固件、配置迁移和提醒管理。TAB5 另有语音、日历生日与常用任务。

ESP8266 的“显示设置”选择自动轮播、智能跟随或固定页；自动屏保等待时间在“连接设置”。TAB5 的屏保类型与预览在设备“设置 → 屏幕与声音”；名画／书法先按[图库说明](GALLERY-PACKS.md)导入。

TAB5 的 **设置 → 存储与缓存** 独立显示 TF 卡状态。默认使用设备缓存；主动选择 TF 卡后，通过 Windows 桥接的 USB／Wi-Fi 保存电脑已安装的整套图库，包括全部作品、分页、横竖屏及文字。完成后可离线查看；未完成的作品仍需桥接。卡不可用时回退设备缓存，不格式化或删除其他文件。当前固件使用 FAT32 卡，128GB 卡需先准备为 FAT32。

电脑端 **屏保图库 → 浏览作品** 支持往期作品、搜索、完整阅读资料、双击全屏、滚轮缩放、拖动及 Esc 退出。

Windows **电脑控制 → 一键解锁设置** 在同一页设置开关、登录方式与凭据；保存时检查并补齐所需组件。明确选择 PIN 或密码，数字密码不等于 PIN；凭据仅在自己的电脑中填写。Wi-Fi 解锁使用随安装包提供的 TightVNC 组件，组件密码由程序生成并加密保存，不要求手动输入 TightVNC 密码。已有不兼容组件会停止接入并提示，不覆盖原配置。

Windows Computer Control → One-click Unlock Settings uses one page for the switch, explicit PIN/password type and credentials. Saving checks the required components; setup also attempts their configuration. The bundled Wi-Fi component generates and protects its own password. An incomplete unlock setup does not mean USB device registration failed. Incompatible existing components are preserved.

![TAB5 独立的存储与缓存设置，默认使用设备缓存](assets/screens/tab5-settings-storage.png)

TAB5 需要无线时，在 **连接升级 → Wi-Fi** 保存网络并确认有效数据。先把 USB 主流程完成，再按需设置无线；Wi-Fi 已关联不等于桥接数据已连通。ESP8266 无线设置见[进阶说明](FLASH_BUILD.zh.md)。

![图 7：TAB5 Wi-Fi 设置，按需配置；USB 已能正常使用时可以跳过](assets/screens/tab5-connection.png)

<a id="mac"></a>
## Mac：应用安装与两类设备接入

解压 Mac 应用 ZIP，将 App 放入“应用程序”。应用临时签名且未公证，首次打开按 [Apple 标准方法](https://support.apple.com/zh-cn/102445)操作；Intel Mac 未验证。Windows 安装器也未签名。

从 Mac 菜单栏打开 AI-bot 设备中心。新增 TAB5 后自动进入“USB 配对”页，连接数据线、选择目标设备后配对。已有 AI-bot 的设备先通过 USB 添加／配对；出厂 ESP8266 使用 **小屏刷机…** 和小屏固件 ZIP，出厂 TAB5 使用首次安装入口和首刷 ZIP。已有 TAB5 更新时选择应用 BIN 及同目录说明文件，不使用首刷包。

按实际使用的功能处理权限，未使用的功能无需先全部配置：USB 接入先验证设备；无线使用蓝牙／本地网络权限；电脑控制、语音分别按界面请求处理辅助功能、麦克风／语音识别等权限。权限被拒时回到对应功能查看提示，不要通过重新刷机处理电脑权限问题。Mac 实际权限与 Windows 不同，本文 Windows 截图不作为 Mac 窗口操作依据。

<a id="troubleshooting"></a>
## 出错时怎么办

| 问题 | 处理 |
| --- | --- |
| 找不到设备 | 换 USB 数据线或接口；TAB5 首刷先进入下载模式，再刷新。ESP8266 确认缺驱动时安装 [WCH 官方驱动](https://www.wch.cn/downloads/CH341SER_EXE.html) |
| 文件选不了 | 核对路线：ESP8266 选自己的 ZIP；TAB5 首刷选首刷 ZIP；TAB5 升级选解压后的 BIN |
| 端口占用 | 关闭其他串口工具；使用内置刷机时保持桥接运行，由工具释放 USB |
| 备份、写入或校验失败 | 保留错误与备份，确认设备仍能启动，不强行跳过检查 |
| TAB5 重启后找不到 | 刷新并选择重启后的端口，再检查启动；不要重新首刷 |
| TAB5 首刷后需恢复原系统 | 重新进入下载模式，在首次安装窗口点 **恢复原固件…**，选同一台设备的 `.bin.json` 备份记录；校验通过后短按 RESET 检查原系统 |
| 已连接但无数据 | 确认设备已添加、USB 配对成功与桥接正在运行 |
| 安装器下载运行环境失败 | 保留错误信息，检查能否访问微软下载服务；重试安装会跳过已安装组件。单位电脑的管理员许可需由管理员处理 |
| 安装完成但提示解锁配置未完成 | 可以先添加设备、检查显示；需要解锁时在“电脑控制 → 一键解锁设置”保存并处理组件提示 |
| 添加或配对超时 | 确认设备已经启动 AI-bot、所选端口属于它，关闭其他串口工具后重试；出厂 TAB5 才走首次安装 |
| 设备中心在线，某个模型没有额度 | 先在“账号数据 → 模型账号”配置对应来源；设备连接成功不代表模型账号已授权 |

备份保存在 `%LOCALAPPDATA%\AI-bot\device-backups\`，TAB5 的 `.bin` 和 `.bin.json` 一起保留；备份可能包含私人设置，不公开上传。USB 偶发中断仍有历史记录，失败先保留错误，不把重试成功理解为已修复。

需要核对下载完整性时，使用对应组件发布页的 SHA-256 清单：[Windows](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.13/SHA256SUMS.txt)、[Mac](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.13/SHA256SUMS.txt)、[TAB5](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.282-ui/SHA256SUMS.txt)、[名画](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.194-ui/SHA256SUMS.txt)、[书法](https://github.com/yaoyouzhong/AI-bot/releases/download/tab5-v0.2.205-ui/SHA256SUMS.txt)、[ESP8266](https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.5.7/SHA256SUMS.txt)。[发布与验收记录](RELEASE-0.6.13.md) · [TAB5 开发者源码](development/TAB5-SOURCE.md) · [许可范围](TAB5-LICENSE-SCOPE.md)

## 日常功能与更新说明

首次接入完成后，再按需要设置以下功能：

- **账号与数据：** 在“账号数据”配置模型账号、天气、自选股票；授权失败时检查对应来源，不重新配对设备。
- **语音：** 在 TAB5 的语音设置选择可用服务；Mac 本地 Speech 与豆包输入法使用各自权限。识别结束后先核对文字，再填入或发送。
- **电脑控制：** 在设备中心选择 TAB5 → 电脑控制，设置快捷启动、音量与一键解锁。Windows 的登录组件与 Mac 权限分别配置。
- **日程：** 在设备中心或 TAB5 应用页管理，最多 16 项；离线修改在重连后同步。
- **图库：** 名画与书法各 366 件，按需选择[标准图库](GALLERY-PACKS.md)或[高清完整版](GALLERY-HD.md)，同类无需重复安装。字库已在固件中，不必单独下载。

后续版本变化见[当前发布说明](RELEASE-0.6.13.md)与[更新日志](../CHANGELOG.zh.md)；界面示例见[截图说明](SCREENSHOTS.md)。安装步骤以本文当前路线为准。

## English quick start

Current downloads: Windows 0.6.13 / Apple Silicon Mac 0.6.13, TAB5 0.2.282-ui and ESP8266 0.5.7. Install the desktop app first. Model accounts, speech, wireless access and artwork packs can be configured after the USB connection works.

1. **Install the app.** On Windows, keep the default installation path unless needed. Setup downloads missing .NET 8 Desktop Runtime / WebView2 components and skips existing ones. It also attempts the previously documented unlock-component setup, which may require administrator approval. “App installed, unlock setup incomplete” is separate from device connectivity; retry unlock setup later in Computer Control. On Mac, extract the ZIP into Applications; the current Apple Silicon app is ad-hoc signed and not notarized. Follow [Apple's opening guidance](https://support.apple.com/102445) and grant only the permissions requested by the features you use. Intel is unverified.
2. **Connect with a USB data cable.** A powered screen alone does not prove USB connectivity. Open Device Center → My Devices → Add, choose the hardware and refresh ports. Select your own device; if several ports are unclear, unplug/reconnect that device and refresh.
3. **Choose by the device's current system.** A device already running AI-bot can use Verify and Add directly; TAB5 completes USB pairing in this step. Factory TAB5 devices use the first-install ZIP, enter download mode, back up/install, restart and check boot, then return to Add and verify. The completion button returns to Add Device. ESP8266 uses its own firmware ZIP. Do not use first installation to fix an ordinary connection failure.
4. **Check the complete result.** The registered device must appear online, show a live clock and respond to display changes. An installer exit code or successful write alone is insufficient. Preserve TAB5 display settings; restoring automatic cycling is an ESP8266-only deployment step.

For later TAB5 updates, extract the upgrade ZIP and select `aibot_tab5.bin` with its adjacent notes file. Start the upgrade, wait for restart and verify boot. Existing paired settings and artwork stay in place. Firmware ZIPs for the two hardware types are not interchangeable; GitHub Source code archives are not firmware packages.

If no USB device appears, check the data cable, port and appropriate driver. If pairing times out, confirm the device has booted AI-bot and close competing serial tools. For a slow prerequisite download, install the [same Microsoft .NET 8 Desktop Runtime x64](https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.31/windowsdesktop-runtime-8.0.31-win-x64.exe) separately, then rerun setup; detected components are skipped. The ordinary .NET and ASP.NET runtimes do not replace the desktop runtime. Missing model quota requires account authorization, not reflashing. Keep original firmware backups private in `%LOCALAPPDATA%\AI-bot\device-backups\`.

Optional features are configured from Account Data or the selected device: speech, computer controls, agenda and [artwork packs](GALLERY-PACKS.md). Standard and complete HD editions contain the corresponding previews and guides; app upgrades preserve installed packs. See [release notes](RELEASE-0.6.13.md), [updates](UPDATES.md) and [screenshots](SCREENSHOTS.md) for ongoing features.

Mac onboarding: adding TAB5 opens USB Pairing directly, including reused connection windows. Normal reopening preserves the selected tab, and firmware updates open the Firmware tab.
