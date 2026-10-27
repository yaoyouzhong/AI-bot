# AI-bot TAB5 客户端

本地候选 **0.2.217-ui** 已刷入并通过精确启动核验；电脑控制、静音、启动图标、锁屏状态与稳定轮询已通过现场确认。已保存网络扩展到 10 个并保留旧记录。电脑 Wi-Fi 重连后，用户拔掉 USB 确认无线音量控制正常；尚未确定原网卡／接入点异常的根因，不宣称已永久修复。

Local candidate **0.2.217-ui** passed exact boot verification. User acceptance covers PC controls, mute, launcher icons, lock state and steady polling. Ten saved networks retain existing records. Following a PC Wi-Fi reconnect, volume control passed with USB unplugged; the underlying NIC/AP fault is unidentified and no permanent fix is claimed.

公开候选准备保留已验收应用字节；完整新功能需要 Windows 桥接 **0.6.9**。首刷、升级和源码快照分别校验，未公开发布；不改变 TAB5 展示设置。Public packaging preserves the accepted application bytes; full new functionality requires Windows **0.6.9**. First-install, upgrade and source snapshots are validated separately; publication remains separate. Preserve TAB5 display preferences.

下列候选段落为历史过程记录。The candidate sections below are historical records.

本地候选 **0.2.216-ui** 将已保存 Wi-Fi 网络上限扩展到 10 个，迁移保留已有网络、密码、备注和首选项；新增不含凭据的 HTTP 连接诊断。构建、迁移测试与设备安装结果分别记录。

Local candidate **0.2.216-ui** expands saved Wi-Fi networks to ten, preserving existing credentials, labels and preference during migration. Adds credential-free HTTP connection diagnostics. Build, migration tests and device installation are recorded separately.

本地候选 **0.2.215-ui** 新增“控制”应用：电脑音量、静音、锁屏与四个常用启动入口；播放、暂停和切歌放在“音乐”页。入口在 Windows 电脑端配置，未连接、旧桥接或播放器不支持时按钮禁用。保留 212 的时钟返回与音乐空闲页调整。图标缓存使用 PSRAM，修正 213 启动时内部内存不足的问题。该候选新增实际锁屏状态显示，并避免状态查询时控件闪动。已本地部署并通过用户现场基础验收；详情见 [电脑控制](docs/COMPUTER-CONTROL.md)。

Local candidate **0.2.215-ui** adds a Control app for PC volume, mute, screen lock and four configured launch shortcuts. Playback controls live on the Music page. Shortcuts are configured on Windows; unavailable connections, older bridges and unsupported players disable the relevant controls. Includes the 212 clock/navigation and music idle changes. Icon state and caches use PSRAM to avoid the internal-memory pressure observed during the 213 boot. This candidate also reports the actual lock state and avoids poll-driven flashing. Locally deployed and basic user acceptance passed. See [computer control](docs/COMPUTER-CONTROL.md).

本地候选 **0.2.212-ui** 统一倒计时与时钟的左上角返回入口，离开倒计时仍继续计时；顶部时间可点击进入时钟，并返回原页面。无歌曲时使用独立音乐空闲页，暂停曲目仍保留歌曲信息。原生点击、返回路径和音乐状态切换测试通过；本次候选未刷机或公开发布。

Local candidate **0.2.212-ui** gives countdown the same top-left back control as the clock, without stopping a running timer. Tapping the header time opens the clock and preserves the return page. Music uses a dedicated idle view when no track exists, while paused tracks retain their metadata. Native pointer, navigation and music-state tests pass; this candidate has not been installed or publicly released.

本地版本 **0.2.211-ui** 新增普通界面的横屏 180° 自动翻转和方向锁定，并整理屏幕声音设置布局。竖放时保持横屏，触控随画面转向；书画屏保保留现有四向阅读方式。相机预览期间保持当前方向。已刷入 TAB5 并通过精确镜像启动核验，用户确认设置页实际效果、翻转和触控正常。使用方式、验证与剩余验收范围见 [屏幕方向](docs/SCREEN-ORIENTATION.md)。未公开发布。

Local version **0.2.211-ui** adds automatic 180° landscape rotation, a persistent direction lock and a refined screen/sound settings layout. Portrait poses retain landscape, and touch follows the display. Art screensavers retain four-way reading; camera sessions retain their opening direction. Installed on TAB5 with exact-image boot verification; the user confirmed the physical settings layout, rotation and touch behavior. See [screen orientation](docs/SCREEN-ORIENTATION.md) for behavior, validation and remaining checks. Not publicly released.

2026-10-17 本地候选 **0.2.210-ui** 整理日程首页的分段筛选、工具栏和详情层次，保留五行列表与居中空状态。沿用已验收 209 的电量自动恢复、内部内存和 OTA 写入修复。原生预览及触摸回归通过；210 的构建与真机结果见公共桥接项目 `docs/development/TAB5-210-ACCEPTANCE-20261017.md`。未推送或公开发布。TAB5 保留用户展示设置，不执行 ESP8266 小屏的轮播恢复。下文版本段落均为历史记录。 Windows 长命令修复已纳入 [构建配置与回归说明](docs/WINDOWS-BUILD.md)，重新生成构建目录仍自动生效。

Local candidate **0.2.210-ui** refines agenda filters, toolbar and selected-item hierarchy while retaining five rows and centered empty states. It retains the accepted 209 battery recovery and internal-memory/OTA fixes. Native preview and touch regressions pass; version-specific build and hardware results are tracked in the bridge project's 210 acceptance record. No push or public release. Preserve TAB5 display preferences; carousel restoration applies only to ESP8266. The older version sections below are historical. See [Windows build policy and regression coverage](docs/WINDOWS-BUILD.md) for the permanent compiler-response-file fix.

2026-10-14 本地候选 **0.2.194-ui** 已安装，精确应用／ELF、`ota_1` 和 `VALID` 启动核验通过；首屏总览、监控断连恢复、TF 预览和六次 BLE 256 KiB 传输已验收。多页翻页延后，BLE 整包升级、持续稳定性和提速目标仍待验证。完整结果见相邻 [194 收尾记录](../AI-bot/docs/development/TAB5-194-ACCEPTANCE-20261014.md)。本地候选不代表公开发布。

Local candidate **0.2.194-ui** is installed and boot-verified with the exact application/ELF, ota_1/VALID. Overview startup, monitor recovery, TF previews and six BLE 256 KiB transfers passed. Multi-page navigation is deferred; whole-image BLE OTA, sustained stability and the speed objective remain open. This is local evidence, not a public release. The older version sections below are historical.

本次公开版本 **0.2.176-ui** 汇总时钟、独立倒计时、Codex 已读同步、导航、显示模式与新增字库。每日名画和书法使用独立可选图库包，固件保留图库支持，不内置新作品或整理中的文字。[下载和发布范围](https://github.com/yaoyouzhong/AI-bot/blob/main/docs/RELEASE-0.6.4.md)。原生回归与构建通过，实际验收按对应镜像记录。

Public version **0.2.176-ui** consolidates clocks, separate countdown functions, desktop read synchronization, navigation, display modes and extended fonts. Painting/calligraphy remain independent optional packs; no new collection images or in-progress guides are bundled. Native build/regressions pass; hardware evidence remains version-specific.

独立的 M5Stack TAB5 触屏客户端，与原 ESP8266 小屏共用 AI-bot 电脑桥接程序。

本工程自有代码采用 [MIT License](LICENSE)，版权属于姚有忠；与合并后的 AI-bot 项目统一许可。第三方组件、字体、词库及服务标识保留各自许可与权利，见 [第三方来源](THIRD_PARTY_NOTICES.md)。

Own code is MIT licensed, copyright 2026 姚有忠, matching the merged AI-bot project. Third-party components and marks retain their original terms.

2026-10-07 当前安装版本为 **0.2.131-ui**：艺术屏保四方向及书法花屏修复已获用户确认，精确镜像启动核验通过，原自动轮播已恢复。验收范围与余项见相邻 [AI-bot .131 记录](../AI-bot/docs/TAB5-ACCEPTANCE-131.md)。下方旧版本段落保留为历史，不代表当前待办。

Current installed firmware is **0.2.131-ui** (2026-10-07). Art orientation and the calligraphy corruption fix are user-accepted; exact-image boot verification and original cycle restoration pass. See the linked .131 record for scope and remaining engineering work. Older version paragraphs below are historical.

历史源码候选 **0.2.91-ui** 调整[Codex 直达](../AI-bot/docs/TAB5-QUICK-CONSOLE.md)：入口移到右下角，每次打开最新会话，豆包识别文字填入电脑未提交草稿，新行为待真机验收。0.2.90-ui 已部署并核验启动；用户确认语音文字进入电脑 Codex 输入框。Historical candidate 0.2.91-ui adds the permanent bottom-right Codex Direct entry and latest conversation on each click; dictation remains an unsubmitted desktop draft. New hardware acceptance is pending. Installed 0.2.90-ui passed boot verification, and the user confirmed desktop voice insertion.

历史已验收基线 **0.2.89-ui**，配套桥接在原目录运行，启动核验通过。该版本设置布局、相机循环、语音光标与断线文字回收、附件回执查询等已按项验收，原自动轮播已恢复；具体范围见相邻 [AI-bot 验收记录](../AI-bot/docs/ACCEPTANCE-2026-10-02.md)。USB 升级中断原因按用户要求暂缓定位，实体键盘、双设备隔离及其他环境仍待验收，不称为全量通过。历史测速保留在[性能记录](docs/TRANSPORT-PERFORMANCE-070.md)。

历史功能状态见 [0.2.52 收尾清单](docs/CLOSEOUT-052.md)。0.2.52 的固件更新仅支持 Wi-Fi / USB；0.2.65 重新加入绑定所选通道的 BLE OTA，生产代码故障测试通过，真实 BLE 升级和速率仍待验收。

后续工作台与分区迁移进展见 [工作台记录](docs/WORKBENCH-NEXT.md)，显示优化见 [滚动性能记录](docs/SCROLL-PERFORMANCE.md)。[验收记录](docs/ACCEPTANCE.md) 及以下版本段落是早期阶段记录；各功能最新验证边界以对应专题文档为准，代码存在不代表已完成真机验收。

历史：0.2.39 曾增加 USB / BLE 固件更新和双向 Codex 请求，合并屏幕与声音设置并修复轮播状态。首次安装与验证边界见 [三通道 RPC 记录](docs/TRANSPORT-RPC-039.md)。

## 早期开发记录

2026-09-23：首版 USB、Wi-Fi、BLE 的真实数据显示和手动模式已通过设备反馈确认。0.2.0-dev 继续迁入十个平台的额度/余额与重置时间、桌宠、音乐封面和进度、网速曲线、农历时钟、独立轮播及屏保；范围见 [功能迁移表](docs/FEATURE-MIGRATION.md)，验证程度见验收记录。语音已加入大疆优先、TAB5 回退的开发源码及模拟验证，尚未部署或进行真实识别验收，详见 [双音源语音输入](docs/VOICE-INPUT.md)。

2026-09-24：新增 [Codex 任务入口](docs/CODEX-REMOTE.md)、[摄像头本机预览](docs/CAMERA.md) 和 [OTA 无线升级源码](docs/OTA.md)。摄像头画面、照片送入任务、无线升级、真实豆包转写及 macOS 桥接仍需分别验收或实现；当前固件与电脑桥接的开发构建不等于已部署版本。

0.2.1-dev 已加入九页图形布局、等比居中桌宠、节气和节假日信息，以及三款可保存选择的表盘。网络配色沿用原小屏的黄色上传、绿色下载。设计与数据年份范围见 [布局说明](docs/UI-DESIGN.md)。网易云音乐当前未向 Windows 上报时间线，未知时长明确显示待提供。

0.2.2-dev 重排总览和模型额度页：Codex 按套餐显示限制，PRO 金色描边，独立重置卡到期列表及真实每日用量图。时钟直接点击表盘循环切换并保存。

0.2.3-dev 总览只显示 Codex，增加美股/港股/A股三列、网络纵轴、重置卡票券和 PAYG 余额/消费双卡；修复日额度漏记及心跳引起的走时跳动。采样口径和真机验证边界见验收记录。

0.2.4-dev 总览右侧可勾选 Codex/DeepSeek/智谱，默认三项；农历与节气单行合并。时钟提供四款指针表盘及一款纯数字，新增低背光移动屏保。天气卡片重排，并接通真实 AQI 和带类型的气压数值。

## 目录

- `firmware/`：ESP-IDF 5.4.2、LVGL 9.2.2、官方 TAB5 BSP（ST7121/ST7123 自动识别）。
- `scripts/`：项目内工具链安装、构建和本地测试。
- `tests/`：实际 C 通道状态机测试。
- `docs/PROTOCOL.md`：USB/Wi-Fi/BLE 协议和配对边界。
- 电脑端改动位于相邻 `AI-bot/windows-app/AIBotBridge/Tab5*.cs`，在原托盘 App 中集成。

## 构建

新设备 USB 首次安装、原固件备份与恢复见 [首次安装指南](docs/FIRST-INSTALL.md)。Windows 桥接入口为“TAB5 连接 → USB 配对 → 新设备首次安装”；目前为本地候选功能，尚待出厂设备完整验收。已有 AI-bot 固件继续使用 [OTA 升级](docs/OTA.md)。

需要 Windows、PowerShell 7、Git、Python 3.12；本地 C 测试还需要 MSVC Build Tools。首次在项目 `.tools/esp-idf` 克隆官方 ESP-IDF **v5.4.2**（包含 submodule），然后运行：

```powershell
git clone --branch v5.4.2 --recursive https://github.com/espressif/esp-idf.git .tools/esp-idf
pwsh -File scripts/setup-idf.ps1
pwsh -File scripts/build.ps1
pwsh -File scripts/test-link-state.ps1
```

首次构建会按 `firmware/dependencies.lock` 下载组件，再应用本项目补丁。后续构建复用本地依赖。默认构建不烧录设备；`scripts/start-bridge.ps1` 是本机候选桥接的启动辅助脚本，其 `.tools` 中的程序不随本仓库分发。

Windows 固件及本地预览构建默认使用最多 **4 个独立效率核心**，优先级为 **BelowNormal**，编译子进程继承限制；脚本退出或构建失败时恢复调用终端原来的 CPU 绑定和优先级。效率核心按 Windows 实际拓扑探测，不固定写死编号；本机目前选择逻辑 CPU 16–19。没有可识别效率核心时会警告并退回最多 4 个允许使用的核心；拓扑探测失败则最多使用 4 个允许的逻辑线程，不会自动放开全部 CPU。预览构建还限制为 `--parallel 4`，预览程序本身在构建限制恢复后运行。

复核资源策略及父子进程继承可运行 `pwsh -NoProfile -File tests/build-resources-test.ps1`；测试使用进程探针并替代实际 IDF 调用，不重编固件或修改 SDK。临时构建脚本也应调用 `scripts/build.ps1`，或复用 `scripts/BuildResources.ps1` 的 Enter/Exit 作用域；直接运行 `idf.py`、Ninja 或其他旧脚本不会自动获得该保护。

完整 LVGL 预览和显示/相机测试会编译使用 GNU C 扩展的 SDK 代码，需要 Visual Studio 的 LLVM/Clang 组件。使用 `pwsh -File scripts/preview-ui.ps1 -Clang` 构建并运行预览；单项测试程序生成在 `.tools/ui-preview-clang64/`。不带 `-Clang` 的 MSVC 路径目前不能编译完整测试集。

工具安装到本项目 `.tools/idf-tools`，不修改系统 PATH。Windows 使用现有 Python 3.12，C 状态机测试使用现有 MSVC Build Tools。
设备烧录前应核对芯片和 USB 身份、备份原闪存；该动作会替换 UIFlow2 应用。

## 首次连接

1. 安装本项目 TAB5 固件与包含 TAB5 支持的电脑桥接候选版本。
2. 使用 USB-C 数据线连接电脑，桥接托盘菜单选择“设备连接 → TAB5 连接”。
3. 选择设备并点击“通过 USB 配对”。
4. 同一窗口可将 Wi-Fi 名称和密码通过 USB 写入设备。密码不会写入电脑项目文件。
5. TAB5 的“设置”页选择自动、仅 USB、仅 Wi-Fi 或仅蓝牙。顶部显示实际有效数据通道。

配对密钥位于电脑用户配置的 DPAPI 文件和 TAB5 NVS。备份闪存可能包含设备配置，`.local/` 不得提交或发布。

## 发布

公共电脑桥接继续由 AI-bot 仓库发布；TAB5 固件使用独立版本与发布入口。每次 TAB5 发布记录最低桥接版本、协议版本、P4 固件与 C6 无线固件的验证组合、SHA-256 和真机验证范围。
源码上传、固件标签和 GitHub Release 分别处理。构建产物、工具链、设备备份及凭据不提交；源码构建与模拟测试结果不替代三通道及交互功能的真机验收。
