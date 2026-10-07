# AI-bot 0.6.0 发布说明

本次重点是 TAB5 Codex 直达、屏保与艺术欣赏，以及电脑端统一更新入口。电脑端和两种设备独立编号；本地与公开版使用同一套功能，名画、书法只需另外下载喜欢的图库。

| 组件 | 本次版本 | 用户操作 |
| --- | --- | --- |
| 电脑端 AI-bot | 0.6.0 | Windows 安装器；Mac 提供 arm64 ZIP |
| ESP8266 小屏 | 0.5.0（未变） | 已安装此版无需刷机 |
| TAB5 | 0.2.145-ui | 已安装 AI-bot 选升级包；出厂系统选首刷包 |
| 可选图库 | 2026.10.07 | 名画和书法分别下载，在 Windows 电脑端导入 |

## 电脑端 AI-bot

- **统一的软件与固件更新入口。** 同时列出电脑端和已添加设备的当前/可用版本、更新明细及对应操作。自动检查和提醒可关闭；同一设备同一版本提醒去重，并遵守静默时段。
- **选对下载包并校验。** 按组件选择安装器或固件，核对 SHA-256、设备类型、内置版本及最低桥接版本。支持取消下载；设备移除、关闭或版本变化时阻止继续。后台检查不会自动安装或刷写。
- **Codex 直达配套。** 最近会话选择、同名标题辨别、窗口唤起及目标核对；草稿留在选定会话，语音文字填入与显式发送分开。改善输入框焦点恢复、追加文字、标点/换行和读回，避免重复填入或重复发送。
- **升级与通信优化。** Wi-Fi、USB、BLE 协商压缩升级，旧设备回退原协议；传输期间协调图片、状态同步等负载。USB 大块回复、BLE 窗口传输和音频缓冲处理改进；升级后核对运行镜像身份。
- **独立图库管理。** 在“软件与固件更新 → 屏保图库”下载、导入名画或书法 ZIP。导入先检查目录、逐文件哈希和图片，再切换到新图库；失败或取消时保留原图库。已有程序目录图库继续兼容。图库存在用户数据目录，程序升级会保留。
- **Windows 数据目录检查。** 防止异常启动环境把配对及用户数据写入错误账户目录。

上述统一更新、TAB5 Codex 和艺术图库服务为 Windows 功能。macOS 同步版本号，保留原有平台功能；不宣称实现这些 Windows 专属入口。

## ESP8266 小屏固件

本次没有固件改动，继续使用 0.5.0。电脑端仍兼容小屏；统一更新入口按其独立版本检查，不要求随 TAB5 一起刷机。

## TAB5 固件

- **Codex 直达是本次主要新增功能。** 页面右下角常驻入口、按压反馈、最近五个会话与前后切换；查看选中会话、语音草稿、停止录音、确认发送、取消及失败恢复。忙碌时限制冲突操作，清空采用长按，发送必须显式触发。
- **八种屏保。** 经典、年度点阵、暗夜、暖灰、十二月历、自动昼夜、每日名画、每日书法。年度点阵支持闰年和进度；自动昼夜按本地 07:00–19:00 切换配色。
- **十二月历花卉更新。** 十二种传统花卉与每月短句，公历、农历、节气、节假日及休班信息；预览可切月，自动屏保跟随实际月份。未知年份信息据实显示。
- **名画和书法欣赏。** 预览及自动屏保均支持正反横屏、左右竖屏，竖屏按作品重新排版，正常系统界面保留横屏。切换保持同一作品和分页，预取另一方向；断线时保留最近成功图片。
- **独立下载两类作品集。** 名画 402 件、804 张横竖屏图片；书法 411 件、3,000 张横竖屏图片。作品数不计重复分页。书法优先较少分页；屏保保留作品名、作者、博物馆与来源，已核实的通用中文名显示为备注，无通用译名保留馆藏原名。
- **显示与容量。** 保留已验收显示基线，修复 JPEG 解码缓存同步造成的尾部花屏及自动屏保状态干扰；完整中文字库无损压缩并缓存字形。没有改变 OTA 分区或删除字符。同时修复连续翻页后的竖屏图片缓冲分配失败。
- **额度和连接信息。** 本周额度、重置倒计时的描述、字重与亮度优化；USB/Wi-Fi/BLE 状态与等待提示更清楚。
- **固件升级体验。** Wi-Fi、USB、BLE 压缩传输及进度反馈优化，区分下载、写入和最终核验；自动/固定通道尊重用户选择，升级前准备数据并协调后台任务。

## 下载和搭配

先安装电脑端，再按设备选择固件。TAB5 升级 ZIP 与首刷 ZIP 含同一个应用版本，用途不同，不能混用。名画 ZIP 约 199 MiB，书法 ZIP 约 726 MiB，均为可选项，不下载也能使用其他功能。图库每件附来源、署名、许可及校验记录；程序许可证不替代图片许可。

后续通过电脑端“软件与固件更新”获取各组件更新和更新明细；图库包在“屏保图库”中另行导入，不随每次固件更新重复下载。

## 验证状态

Windows 隔离构建及完整回归、两包全部图片验证、366 天双方向服务、图库导入边界均通过；修复长路径下 GDI+ 文件接口误报内存不足，改用 .NET 文件流验证，不降低校验要求。

`.145` 已通过精确镜像启动核验和用户第二页及后续分页竖屏验收。设备记录确认第二、第三页竖屏图完成解码，持续复用两块输出缓冲；24 次分页与 96 次方向模拟检查也通过。已恢复原自动轮播。详见 [.145 验收与镜像身份](TAB5-ACCEPTANCE-145.md)。

首刷包和升级包共享应用镜像；不擦除已配对设备重做首刷，不宣称新一轮出厂首刷验收。macOS 构建由 CI 检查，图形安装与运行验收不由 Windows 测试推定。

## English

AI-bot 0.6.0 pairs with unchanged ESP8266 0.5.0 and TAB5 0.2.145-ui. Components keep independent versions. Local and public editions share functionality; artwork is supplied as two optional collection ZIPs.

Windows adds unified component update discovery, notes, notifications, matching downloads, SHA-256 and compatibility validation, and explicit installation handoff. Codex Direct improves recent-session selection, target validation, voice drafts, focus recovery and explicit sending. OTA compression and USB/BLE transfer scheduling improve communication. A collection manager validates and imports artwork into the user data directory, preserving existing collections and legacy app-folder galleries. macOS keeps its existing platform features; these new TAB5 services and update UI require Windows.

TAB5 adds a permanent Codex Direct entry, five recent conversations, voice draft review and explicit send/cancel/recovery controls. Eight screensaver choices include annual grids with automatic day/night colors, a twelve-flower monthly calendar, daily paintings and calligraphy. Art previews and automatic screensavers support four orientations with matching layouts and offline retention. Captions retain artist, work and museum information; only verified Chinese equivalents are added. Optional packs contain 402 paintings and 411 calligraphy works, with pages/layouts counted separately. Stable display/cache fixes, lossless font compression, quota/countdown styling, connection indicators and compressed OTA progress are included.

ESP8266 has no firmware change. Existing TAB5 devices use the upgrade ZIP; factory devices use the first-install ZIP. Artwork is optional and survives app upgrades. The .145 image passed exact-image boot verification and user acceptance for second and subsequent portrait pages; retired and failed-job buffers are retained for reuse. Import and annual-service tests pass; first-install hardware acceptance is not inferred from the shared upgrade image.
