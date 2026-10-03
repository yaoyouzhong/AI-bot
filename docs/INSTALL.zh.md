# AI-bot 安装与刷机指南：ESP8266 / M5Stack TAB5

更新于 2026-10-03，按 **v0.5.0 Windows 预发布界面**编写。[v0.5.0 下载页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.5.0)提供本页附件；稳定版仍为 v0.4.0，其菜单与本页不同。

**先确认硬件，再选对应固件。** Windows 桥接、ESP8266 固件与 TAB5 固件分别编号。本轮 Windows / ESP8266 候选为 0.5.0，TAB5 为 **0.2.89-ui**，不要求三个版本号相同。

| 设备与当前状态 | 所需文件 | 路线 |
| --- | --- | --- |
| ESP8266 / ESP-12S，240×240 ST7789，SD2 引脚方案 | `AI-bot-0.5.0-firmware-materials.zip`，或其内 `firmware.bin` | [ESP8266 刷机](#esp8266) |
| 出厂系统或尚未安装 AI-bot 的 M5Stack TAB5 | `TAB5-first-install-0.2.89-ui.zip` | [TAB5 首次安装](#tab5-first) |
| 已有 AI-bot、采用当前双 OTA 分区的 TAB5 | `aibot_tab5.bin`，同目录保留 `.bin.notes.json` | [TAB5 升级](#tab5-upgrade) |
| 只用电脑镜像 | Windows 应用包 | 跳过设备刷机 |

**TAB5 首刷也使用 0.2.89-ui，但要选择完整首次安装 ZIP。** ZIP 包含启动程序、分区表、OTA 初始化和同版本应用；单独的 BIN 只含应用，不能初始化出厂设备。两种硬件的 ZIP / BIN 不可互换。已有 AI-bot 的 TAB5 不重复首刷，旧分区不兼容时需单独迁移。

<a id="prepare"></a>
## 1. 准备设备

- Windows 10/11 x64；USB 数据线，不能是仅充电线。
- ESP8266 核对芯片、ST7789 及[引脚配置](../firmware/platformio.ini)，不能套用到其他 ESP32 小屏。
- TAB5 用 **USB-C 数据接口**连接电脑，USB-A 外设接口不用于首刷。安装器核对 ESP32-P4、芯片修订、16 MiB 容量及安全状态；启用安全启动或闪存加密的设备不受支持，检查不通过就停止。
- 留好原厂恢复资料。首次安装会替换 TAB5 P4 原固件与闪存设置，工具先完整备份；不改 C6 无线固件、SD 卡或 eFuse。

<a id="download"></a>
## 2. 应用和固件选哪个

从 [v0.5.0 发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.5.0)取得对应附件和 [SHA-256 校验清单](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/SHA256SUMS.txt)。这是预发布版，未完成的验收见[发布记录](RELEASE-0.5.0.md)。

| 文件 | 用途 | 处理 |
| --- | --- | --- |
| [Windows 安装器](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AIBotBridge-0.5.0-setup-win-x64.exe) | Windows 应用，含刷机工具 | 双击安装 |
| [Windows 便携 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AIBotBridge-0.5.0-local-candidate-win-x64.zip) | Windows 便携版 | 完整解压 |
| [ESP8266 固件材料 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AI-bot-0.5.0-firmware-materials.zip) | **仅 ESP8266**，含固件与重建材料 | 图形刷机直接选 ZIP |
| [TAB5 首刷 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/TAB5-first-install-0.2.89-ui.zip) | **仅 TAB5 首次安装**，含完整 16 MiB 安装镜像与清单 | 首次安装窗口直接选 ZIP |
| [TAB5 升级 ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/TAB5-upgrade-0.2.89-ui.zip) | **已有 AI-bot 的 TAB5 升级**，含 `aibot_tab5.bin` 与说明 sidecar | 完整解压，升级窗口选 BIN |
| [Mac Apple Silicon ZIP](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.5.0/AIBotBridge-0.5.0-local-candidate-macos-arm64.zip) | macOS 13+ 应用 | 完整解压 |

Windows 安装器与便携包二选一。GitHub 的 Source code ZIP 不是固件。校验值不符先重新取得文件，不继续刷写。

TAB5 源码与组件材料单独随 `TAB5-firmware-materials-0.2.89-ui.zip` 提供，其[许可范围](TAB5-LICENSE-SCOPE.md)与本仓库分开。

<a id="windows"></a>
## 3. 安装 Windows 应用并打开设备中心

1. 双击安装器，阅读许可，保留原安装位置，按需创建快捷方式。
2. 保持联网。已有 .NET 8 Desktop Runtime / WebView2 自动跳过，缺少时按提示补装。
3. 启动 AI-bot，在右下角托盘找图标，右键打开 **设备中心**。未启用 ESP8266 时左键也打开设备中心；启用后左键打开其小屏预览。
4. 设备中心分为 **我的设备 / 账号数据 / 桥接设置**。点 **我的设备 → 添加**，选择实际硬件。出厂设备先“首次安装”，刷完再“验证并添加”；已有 AI-bot 固件可直接验证并添加。
5. 当前支持每种型号一台，可同时添加 TAB5 和 ESP8266。

![当前原生设备中心，两种硬件均为隔离演示数据](assets/screens/device-center.png)

截图使用当前原生程序与固定演示数据，不读取真实账号或设备身份，不代表实体设备实拍。安装器未签名；系统拦截时核对来源及校验值，并遵守单位 IT 规定。

<a id="flash"></a>
<a id="esp8266"></a>
## 4A. ESP8266：首次刷机或更新

1. 连接 USB，关闭其他串口工具，保持桥接运行。
2. 新设备：**设备中心 → 我的设备 → 添加 → ESP8266 小屏 → ESP8266 首次安装…**。已有设备：选中该小屏 → **管理… → 固件升级**。
3. **浏览…**：选 ESP8266 的 `firmware-materials.zip`，无需解压；也可选包内的 `firmware.bin`。
4. 按提示识别目标设备，点 **开始刷机**。工具临时释放对应 USB，检查芯片、完整备份、写入并回读校验。备份不完整不写入。
5. 准备/备份阶段可取消，写入与校验期间保持供电和数据连接。
6. 显示“刷机完成”后关闭窗口；新设备在“添加”中刷新并**验证并添加**，已有设备检查 USB 恢复。
7. **管理 → 设备信息**确认响应；用**显示设置**和预览切到系统监控、天气等页面，确认实体屏更新。最后恢复自动显示、循环展示，保留原页面、顺序和间隔。

![ESP8266 原生刷机窗口，离线演示状态](assets/screens/firmware-flasher.png)

“更多选项”提供“只备份设备”和“查看备份”。备份可能含私人网络配置，不公开上传。无端口先换数据线/USB 口，确认 CH340 缺驱动后再装 [WCH 官方驱动](https://www.wch.cn/downloads/CH341SER_EXE.html)。进阶源码刷机见[ESP8266 构建说明](FLASH_BUILD.zh.md)；该页的 `0x0` 地址不适用于 TAB5 应用升级镜像。

<a id="tab5-first"></a>
## 4B. TAB5：第一次从出厂系统安装

1. **设备中心 → 我的设备 → 添加 → M5Stack TAB5 → TAB5 首次安装…**。已有入口也可从 **连接升级 → USB 配对 → 新设备首次安装…**打开；已有 AI-bot 的设备走后续升级。
2. 连接 USB-C 数据线。按 [M5Stack 官方说明](https://docs.m5stack.com/zh_CN/guide/tab5/restore_factory)，长按 RESET 约 2 秒，内部绿灯快速闪烁后松开，进入下载模式。
3. 点 **刷新设备**，主动选择这台 TAB5 下载端口，不选其他 ESP32 板。首刷需主动选端口，和 ESP8266 界面不同。
4. **选择安装包…**：选 `TAB5-first-install-0.2.89-ui.zip`。勾选“确认是 TAB5，允许替换原固件及设置”，点 **备份并安装**，核对目标设备与版本。
5. 工具核对包、芯片、容量和安全状态，读取并校验原设备完整备份，再写入和校验。准备可取消，写入后保持供电，等待结束。
6. 提示“写入校验通过”后，**短按 RESET**。等待屏幕启动，刷新并选择重启后的 USB 端口，点 **检查启动**。端口号可能变化。
7. 检查通过后点 **进入 USB 配对**。新设备回“添加”中刷新、**验证并添加**，再打开该设备的 **连接升级 → USB 配对**完成配对。
8. 需要无线时，在 **连接升级 → Wi-Fi**保存 2.4 GHz 网络，确认桥接有效数据；BLE 另行配对并确认数据。网络已关联不能代替桥接已连通。
9. 检查实体屏、触摸、数据更新。恢复自动连接、自动显示和循环展示；新设备在显示设置选择页面，已有设置保留原顺序和间隔。

![TAB5 原生首次安装窗口，隔离演示状态，未选择设备端口](assets/screens/tab5-first-install.png)

首刷 ZIP 包含相同的 **0.2.89-ui 应用**和首次启动所需内容，无需先刷旧版。089 已有升级后的真机启动记录，**不等于出厂设备首刷与原固件恢复整条链路已验收**。

### TAB5 备份与恢复

备份默认在 `%LOCALAPPDATA%\AI-bot\device-backups\`；完整 `.bin` 配套 `.bin.json` 记录设备身份、大小与哈希，两者一并保留，不上传 Release。

若写入或启动失败，重新进入下载模式，选中**原同一台 TAB5**，点 **恢复原固件…**，选择对应 `.bin.json`。工具检查 MAC 和备份哈希，先备份当前内容再恢复。校验通过后短按 RESET，确认原系统启动。不用其他设备备份，不恢复 C6、SD 卡或 eFuse。

<a id="tab5-upgrade"></a>
## 4C. 已安装 AI-bot 的 TAB5：后续升级

1. 设备中心选中 TAB5 → **连接升级 → 固件升级**。
2. 保持有效连接和供电；日常自动模式优先 USB > Wi-Fi > BLE，首次安装仅 USB。USB 偶发断连/升级中断仍是已知问题，失败先保留错误并确认现有固件可启动。
3. **选择固件…**：选 `aibot_tab5.bin`，同目录保留 `aibot_tab5.bin.notes.json`。核对 **0.2.89-ui**、校验值及更新说明，确认提供。
4. 在 TAB5 上按提示确认升级，等待传输、镜像校验和重启。“已提供固件”不等于安装完成。
5. 重启后连接 USB，点电脑升级页 **核验启动（USB）**。核对版本、ELF 指纹、运行分区和 `VALID` 状态，不能只看版本号。
6. 检查真实触摸、数据通道及本次更新功能；恢复原自动模式、轮播页面/顺序/间隔和连接选择。

![TAB5 原生连接与升级窗口，离线状态](assets/screens/tab5-upgrade.png)

首次安装 ZIP 不用于这里。旧分区不支持时停止，使用对应迁移/恢复方案；不能用首刷来保留现有设置。本地固定升级入口见[固件工作流](TAB5-FIRMWARE-WORKFLOW.md)。

<a id="connect"></a>
## 5. 怎样算刷完并连上

| 检查 | ESP8266 | TAB5 |
| --- | --- | --- |
| 身份与连接 | 已添加、设备信息有响应 | 已添加、USB 配对及有效数据确认 |
| 真实画面 | 系统监控更新，实体屏切页 | 屏幕/触摸正常，首刷或升级启动核验通过 |
| 无线（按需） | 实际 LAN 回退确认 | Wi-Fi、BLE 分别确认有效数据 |
| 收尾 | 自动显示、循环展示，保留原页面/顺序/间隔 | 同左，并恢复原连接模式 |

镜像有画面、串口存在、上传到 100% 都不能单独证明实机成功。不拔掉唯一电源线测试无线回退。

<a id="settings"></a>
## 6. 内容设置

公共账号、额度与数据源在**账号数据**；每台设备的内容开关在**数据设置**。亮度、页面和轮播在**显示设置**，TAB5 另有语音、日历生日和常用任务。电脑开机启动等在**桥接设置**。授权后检查真实数据，网页能打开不代表成功；密钥和密码不写入聊天、文档或发布包。

<a id="mac"></a>
## Mac 当前范围

Mac 面向 macOS 13+、Apple Silicon；v0.5.0 Mac 包由 macOS 发布流水线测试、构建和打包，交互与真机验收仍待完成。解压后将 App 放入“应用程序”，按 [Apple 标准方式](https://support.apple.com/zh-cn/102445)打开未公证应用。

Mac 菜单 **AI-bot → 小屏刷机…**面向 **ESP8266**：选 ESP8266 ZIP，等待备份、写入、回读校验和重新连接，实机行为仍需验证。本文 TAB5 首刷/配对/升级入口属于 Windows，不声明 Mac 已提供这些功能；TAB5 首刷使用 Windows 工具。

<a id="optional"></a>
## 可选网络

ESP8266 USB 正常后再按[进阶说明](FLASH_BUILD.zh.md)配置 Wi-Fi 回退；TAB5 用其 Wi-Fi 页面保存网络。电脑与设备须在可互通局域网。重置网络和关闭防火墙不是常规刷机步骤。

<a id="upgrade"></a>
## 电脑程序升级与卸载

升级：正常退出桥接，安装到原位置或替换完整便携目录，用原快捷方式启动，不清空 AppData。电脑和设备分别升级。改变路径时重新设置开机启动。

Windows 卸载前关闭开机启动并退出，从系统“应用”卸载；用户设置与公共运行环境保留。Mac 先关闭登录项、退出，再移除 App；Keychain 配对资料不自动删除。

<a id="troubleshooting"></a>
## 排错

| 现象 | 处理 |
| --- | --- |
| 没有 TAB5 首次安装入口 | 确认使用新版 Windows 设备中心，v0.4.0 菜单不同 |
| TAB5 ZIP 被升级窗口拒绝 | 首刷 ZIP 与升级 BIN 用错入口，按顶部表选择 |
| 没有端口 | 核对数据线；TAB5 进入下载模式后刷新，ESP8266 检查芯片驱动 |
| 端口被占用 | 关闭其他串口工具，内置刷机保留桥接，让工具释放对应连接 |
| 备份/写入/校验失败 | 保留错误，不判定成功；TAB5 必要时同设备备份恢复，不强制绕过检查 |
| TAB5 重启后检查找不到设备 | 刷新、选择重启后的端口，端口号可能变化 |
| ESP8266 黑屏/花屏 | 核对 ST7789、引脚和背光，不套用 TAB5 固件 |
| PC OFF / 等待数据 | 检查对应已添加设备和有效桥接数据 |

## English summary

Windows v0.5.0 is a pre-release, available from the release links above; v0.4.0 remains the latest stable release. ESP8266 uses its firmware-materials ZIP or firmware.bin. Factory TAB5 devices use the full TAB5-first-install-0.2.89-ui.zip; existing AI-bot TAB5 devices extract TAB5-upgrade-0.2.89-ui.zip and select aibot_tab5.bin with the notes sidecar beside it. Both carry application version 0.2.89-ui but are not interchangeable. First installation verifies and backs up the P4 flash before replacing it. Hold RESET about two seconds until the green LED flashes rapidly, select the target download port, install, briefly reset, select the new application port and verify boot before registration/pairing. Restore accepts the same device's verified backup only. Existing devices verify boot over USB after upgrading. Preserve automatic cycling, pages, order and interval. Mac flashing here covers ESP8266; Windows TAB5 support does not imply Mac TAB5 support. Native UI screenshots use isolated synthetic data, not hardware footage. Upgrade acceptance does not establish the factory-install/restore hardware chain. TAB5 source/component materials have a separate license scope; no new blanket MIT grant is made by this publication.
