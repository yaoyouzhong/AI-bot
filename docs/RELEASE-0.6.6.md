# AI-bot 0.6.6 / TAB5 0.2.194-ui

2026-10-14。本次正式交付 Windows／Apple Silicon Mac 桥接 0.6.6、TAB5 0.2.194-ui，以及独立名画布局包 2026.10.14.1。ESP8266 与书法下载沿用下载中心已有正式版本；无需重复刷写或重导入未变化的组件。候选已合回 main，用户其他未提交工作保留。

## 变化与使用

- Windows 提供运行监控、告警详情与去重、图库浏览和电脑发起 TAB5 升级；修复小屏 USB 心跳因包含不使用的监控详情而超限，保留完整桌面／TAB5 数据。
- TAB5 修复断连时运行监控左侧闪屏、离线 TF 预览调度及 JPEG 解码内存问题；重启首屏显示总览，再恢复原展示设置。
- 仅蓝牙连续大数据传输按成功鉴权请求续期临时 Wi-Fi 暂停，结束／空闲／失败自动恢复；精确确认重传修复合法分片窗口拒绝，保留鉴权和队列上限。
- 名画横屏在图画左侧显示作品名、作者和年代；竖屏保留简化信息，书法布局保持。独立名画包保持 366 件作品、826 张横竖屏图片；413 张竖屏图片原字节保留，不改书法类别。
- 已安装 194 无需重复升级。新用户选择首刷 ZIP，已有 AI-bot 选择升级 ZIP；两包绑定同一应用，升级保留配对、设置和已保存图库。

## 验证与边界

合并提交 `e2311f28da0d4d558f85d97c3957f537be45ffd7` 的[主分支 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/38530182218)通过 Windows、macOS 和固件检查；[桥接打包流水线](https://github.com/yaoyouzhong/AI-bot/actions/runs/38530632198)通过，两平台包均有许可和校验材料。Windows 正式安装器复用已验收 DLL `f6a99f8897d0996d487cb58a46104d074f52971dacd07e489eb363e32a8c7baf`，不将后续流水线重新生成的 Windows 文件替换为已验收交付。

TAB5 应用 SHA-256 为 `811c38d5506c0092bcf67a8c971fe03115f15c1901de0a588f211657a2096f29`；独立工程源码提交 `69cf5de8830059690f2ff336fdeb1323276fe43a`。实机启动 `ota_1`、`VALID`；首屏总览、无闪屏与自动恢复、书画布局、两类 TF 预览及原有轮播均通过。三轮 BLE 上传下载共六次完整 256 KiB、错误 0，汇总上传 19.50／下载 18.86 KiB/s；改善有限。

多页作品翻页按用户安排延后；BLE 整包升级、持续稳定性与提速目标仍未验收。Mac 包为 Apple Silicon、临时签名、未公证，Windows 与 Mac 功能范围不同；真实 Mac 交互／Apple Music 和长期运行验收不由构建替代。首刷包已验证空白 NVS 和同一应用身份，本轮没有重置现用设备。源码快照不声明独立环境逐字节重建。

[完整下载](../DOWNLOADS.md) · [安装指南](INSTALL.zh.md) · [源码说明](development/TAB5-SOURCE.md) · [实机验收](development/TAB5-194-ACCEPTANCE-20261014.md)

## English

This release delivers Windows/Apple Silicon bridge 0.6.6, TAB5 0.2.194-ui and optional painting layout pack 2026.10.14.1. Existing public ESP8266/calligraphy downloads remain unchanged. The accepted candidate was merged into main while other uncommitted work was preserved. CI passed on e2311f2 and the bridge package workflow passed. Windows setup retains the hardware-accepted f6 DLL; the macOS package is arm64, ad-hoc signed and not notarized.

TAB5 fixes monitor repainting, cold TF scheduling/decoder memory, Overview startup and continuous BLE radio-lease recovery. Landscape painting captions sit left of the complete artwork; compact portrait and accepted calligraphy layouts remain. Installed 194 booted ota_1/VALID. All six BLE 256 KiB transfers passed at aggregate 19.50/18.86 KiB/s; improvement is limited. Whole-image BLE OTA, longer stability/speed and physical multi-page navigation remain open. Existing pairing, galleries and automatic rotation are preserved. Factory and upgrade packages bind the exact accepted application; source snapshots do not claim independently reproducible binaries.

<details>
<summary>历史 2026-10-13 候选记录 / Historical candidate record</summary>

以下为当时准备阶段，版本、候选状态与待验收项不作为当前发布结果；以本页上方正式交付和最终验收记录为准。

# AI-bot 0.6.6／TAB5 0.2.190-ui／ESP8266 0.5.3

2026-10-13 统一候选范围包含电脑桥接、两种设备固件、最新字库及年度精选书画。名画与书法是分别可选的独立 ZIP，程序安装包和固件不嵌入作品图片或阅读数据。软件升级保留原图库；导入新版类别时，图片、简介、欣赏资料、完整正文／参考文本及来源一起校验和更新。

## 本次变化

- Windows「屏保图库 → 浏览作品」支持日期查找、作品名／作者搜索、分页、完整阅读、双击全屏、滚轮缩放、拖动、适合窗口和 Esc 返回。主界面以图片为主，阅读资料默认收起。
- Windows 与 Apple Silicon Mac 精简未使用的三个 esptool 工具，保留实际可执行文件、匹配源码及许可，保留离线备份、刷机和校验。Mac 的可用功能沿用平台范围，不提供 Windows 的 TAB5、图库导入／同步入口。
- 小屏换 USB 插口后，Windows 先匹配 ESP8266 0.5.3 的固定设备身份，再恢复数据及新路径；不凭 COM 号或同型号自动认领。设备中心 USB／Wi-Fi／蓝牙状态保持单行，窄空间可查看完整提示。
- TAB5 新增独立「设置 → 存储缓存」，位于「内容轮播」下方，后台检测 TF 卡。默认设备缓存，主动选择 TF 卡才整库保存；卡不可用时回退，不格式化或删除其他文件。项目数据统一放在 `aibot/` 各模块目录下。
- 修复 FAT 长文件名配置。USB／Wi-Fi 保存电脑已安装的两套图库，包括全部作品、分页、横竖屏及完整文字；中断续存、同图复用。保存完成后可跨重启／年份离线读取，未保存的作品仍需要桥接。BLE 保留单幅同步。
- Codex 直达首次进入隐藏底部空框；保留语音、草稿和明确发送流程。原有已读同步、音乐、九款表盘与两类独立倒计时继续保留。
- 长文以带逐段及全文校验的 Unicode 分段阅读，7 篇全文全部保留，共 26 段；打开阅读时取得独立快照，TF 保存完整段集。旧阅读端收到升级提示，不把节录当全文。
- Windows 支持直接发起配对 TAB5 的升级，并简化启动核验与传输测速布局。旧设备须先手动安装一次支持该功能的固件，此后可从电脑操作；具体镜像的启动有效状态单独核验。
- 纳入全部运行监控修改：持续电脑负载、VPN／PAC、实际 Codex 连接异常和独立桥接守护。TAB5 系统页提供监控与简洁异常详情，过期数据保持未知或中断。
- USB 批量接收与请求就绪唤醒、Wi-Fi 并行接收和有效连接复用，继续保留写卡并行、校验与断点续传；优化竖屏作品详情绘制和底部翻页区域。版本与真实速度结果分别记录，不用构建通过代替提速验收。

## 年度图库与字库

| 内容 | 版本 | 独立作品 | 横竖屏图片 |
| --- | --- | ---: | ---: |
| 名画年度精选 | 2026.10.11.23 | 366 | 826 |
| 书法年度精选 | 2026.10.12.32 | 366 | 3,802 |

名画以作品价值及同件权威欣赏资料逐件选择。书法包含 304 件既有作品和 62 件新增原作资料；整册、整卷仍只计一件，照片、分页与方向不重复计作品。7 篇超过旧容量的完整参考随包保留，原文范围和异文说明单独标注；参考文本不冒称本件逐字精校。

最终字库为 **10,535 个字形**，两包所需 **7,208 种显示字符缺字 0**；原有 .177 的 9,027 个字形指标与压缩位图保持一致。字库随 TAB5 固件升级，导入图库不会安装字体。建议先升级 Windows 0.6.6 和 TAB5 .190，再导入新包，获得完整长文分段与 TF 存储能力；既有旧图片包保持兼容。

## 包与使用

从[完整下载中心](../DOWNLOADS.md)下载所需组件。Windows／Mac ZIP、ESP8266 通用包、TAB5 首刷和升级包分别提供，不互换。Windows 保留直接解压运行和原位置更新，不要求新增安装器。TAB5 首刷含启动及分区材料，升级只安装应用；两包必须绑定同一个 .190 应用。图库 ZIP 在 Windows 导入，无需解压，不直接拷到 TF 根目录。

现有 Windows 安装运行时要求不变；新电脑缺少 .NET／WebView2 时仍需安装对应运行环境。Mac 包为 Apple Silicon、ad-hoc 签名，非 Developer ID 公证，Intel 不在本次交付范围。

## 验证记录与限制

两份图库 ZIP 已逐成员验证 CRC、SHA-256 与清单；字体保持及显示覆盖通过。正式消费者隔离导入验证全部图片、完整正文和原子更新；7 篇长文验证分段重组、过期请求拒绝、离线存储、失败保留、独立阅读快照和四朝向触摸。Windows 最终源码构建及公开回归通过，浏览界面覆盖 100%／150%／200% 正常和最小窗口。

ESP8266 .5.3 已实机安装并验证固定身份、真实 COM8→COM7 换口恢复和持续 USB 同步；另有无 LAN 配置的实际 UART 恢复测试。没有实际关闭设备 Wi-Fi，不将此称为“关 Wi-Fi 后物理换口”验收。Mac 对端字段解析需在实际 macOS CI 完成测试和构建。

本记录先随候选源码冻结；最终组件 CI、镜像／源码／首刷／升级一致性、实际 TAB5 启动与 TF 保存验收、公开附件摘要及视频完整审片，在发布交付记录中补齐后才公开候选。构建、模拟与截图不代替真实设备验收。Apple Music 实际播放、长期运行／睡眠恢复等此前尚未通过项继续保留，不因本次发布称为已验收。

## English

The combined release targets bridge 0.6.6, TAB5 0.2.190-ui and ESP8266 0.5.3, with separately optional annual packs of 366 paintings and 366 calligraphy works. Packages update images, reviewed introductions/appreciation, complete originals or explicitly scoped references and sources atomically. Old image-only collections remain compatible; desktop upgrades preserve installed data.

Windows adds paired desktop-initiated TAB5 upgrades, a compact startup check and transfer-test layout, and all runtime monitor changes: sustained system pressure, VPN/PAC routing, actual Codex connection failures and a session watchdog. TAB5 includes the monitor, bounded USB readiness waits, Wi-Fi connection reuse, parallel receive/card writing, resumable storage and the portrait reader rendering improvements. Older devices need one manually confirmed bootstrap upgrade before desktop starts are supported. Hardware performance and boot acceptance are tracked separately. Windows retains portable ZIP deployment without requiring an installer.

Windows adds past-work browsing, search, full reading, fullscreen, wheel zoom and pan. Compact Windows/Apple Silicon tool bundles retain offline flashing and matching licenses/source. ESP8266 reports a stable identity for verified Windows USB recovery after port changes. Actual port-change and no-LAN-config UART tests passed; device Wi-Fi was not physically disabled.

TAB5 adds independent Storage & Cache, checked FAT long filenames, opt-in complete-library TF storage under `aibot/`, resumable synchronization and image reuse. Device cache remains the default. Completed content is readable offline; unsaved works need the bridge. Seven complete long references remain intact as 26 checked reading segments and independent snapshots. The final 10,535-glyph font covers all 7,208 required display characters and preserves the previous .177 glyph metrics/bitmap data. Font installation requires firmware; collection imports do not install fonts.

Both packs and source-local tests are verified. Final build/CI, image/source/factory/upgrade consistency, actual TAB5 startup/storage and public download verification are recorded separately before publication. Native captures are fixtures, not hardware photography. Mac retains its existing platform scope and is arm64/ad-hoc signed, not notarized. Apple Music subscribed playback and long-term acceptance remain pending.


</details>
