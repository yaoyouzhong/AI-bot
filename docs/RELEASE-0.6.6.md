# 桥接 0.6.6 与 TAB5 0.2.177-ui

本次精简 Windows／Apple Silicon Mac 安装材料，保留离线刷机；TAB5 增加 TF 卡整库同步及最新字库。新图库内容仍整理中，本次不发布；现有图库继续兼容。ESP8266 0.5.2 保持原下载。

## 精简与校验

从已核验的 Espressif 4.9.1 官方独立档案中保留 esptool、LICENSE、README，移除未使用的 espefuse、espsecure、esp_rfc2217_server。可执行文件字节与官方版本一致，完整匹配源码和现有依赖许可保留。ORIGIN.json 记录官方档案、精简档案、可执行文件与源码的校验值；运行时仍校验固定档案哈希后从新临时目录提取，不依赖网络、Python 或以前的工具缓存。

备份、写入、校验命令和串口选择保护保留。Windows 安装所需的 .NET／WebView2 条件与此前一致；保留离线刷机不意味着缺少运行时的新电脑可全程离线安装。

## TF 卡与字库

TF 卡采用统一的 `aibot/` 项目目录：图库、照片、其他持久数据及可重建缓存各有固定位置，按需创建，不散放卡根目录。目录和后续扩展约定见 [TF 卡目录约定](TF-STORAGE.md)。

首次连接支持此功能的 Windows 桥接后，TAB5 通过 USB／Wi-Fi 自动把电脑已安装的整套名画、书法同步到 `/sdcard/aibot/gallery`，包括每件作品的所有分页、横竖屏图片、简介、欣赏提示与原文。不是每天取一幅缓存。进度在选择名画／书法的屏保设置中显示；完整保存后，日常选取直接读卡，可跨重启和年份离线使用。尚未保存的作品仍需桥接。

全库后台任务不分配 RGB 显示帧，浏览、语音、照片及 OTA 优先；中断后从已完成作品继续，未完成作品中已校验的页面也会复用。完整复制只走 USB／Wi-Fi，BLE 保留单幅获取能力。图库版本更新时重读新元数据，SHA-256 相同的图片直接复用卡上字节，纯文字更新不重传图片；两个类别独立更新。无卡、卡满或只读时暂停保存并保留现有显示路径，不把部分同步显示为已完成。图库包仍在电脑端导入，不直接把 ZIP 当作 TF 卡文件读取；Mac 当前不提供 Windows 的图库同步入口。

图片与文字同一记录，各自校验 SHA-256。两份轮换记录保留完整提交，未完成或损坏记录不参与展示；已认证的内容版本排除旧记录。无卡、只读、容量不足或介质错误时沿用内存／桥接。照片与图库共用挂载和文件访问锁，不格式化、不删除其他用户文件。

冻结字库含 9,027 个映射字形，比正式 .176 增加 11 个；原有 9,016 个字形尺寸及位图逐项保持。最新导读快照所需的 5,561 个字符全部覆盖。字形随固件安装，图库导入不会更新字库；整理后的新内容发布前再核查缺字。只有字库进入固件，整理中的图片、导读和原文未打包。

## 验证记录

候选已完成构建、打包和本机部署，公开下载暂仍为 0.6.5／.176。最终包与对应 SHA-256 已校验：

| 材料 | 0.6.5／.176 字节数 | 本次字节数 | 变化 |
| --- | ---: | ---: | --- |
| Windows 安装包 | 59,406,746 | 24,649,549 | 减少 58.5% |
| Apple Silicon Mac 包 | 47,942,147 | 14,258,130 | 减少 70.3% |
| TAB5 首次安装 ZIP | 4,599,024 | 4,609,229 | 应用、启动与分区材料 |
| TAB5 升级 ZIP | 4,568,451 | 4,579,423 | 仅供已有 AI-bot 设备升级 |

- 桥接源提交 `fa4175284ae18efa5e213a1ec0440c04ab8fa8d4`；[Windows／Mac CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/38140957461) 通过。Windows Release 构建零警告／错误；750 项认证图库目录、清单与范围传输检查通过。四项真实官方工具输入测试通过，解压后的 Windows EXE 和 Mac 应用完成离线 esptool 版本及读取／写入／校验命令检查；没有访问或刷写设备。Mac Swift 测试、Release 构建、文件与严格签名校验通过；Mac 交互验收未新增。
- TAB5 源提交 `b742cb1b3333fb6c4357c0a6d120d05a6799dd03`；ESP-IDF 构建、实际 ELF 显示／执行安全检查通过。原生测试覆盖整库分页和横竖屏、冷启动续存、跨年份离线读取、纯文字更新零图片传输，以及损坏／卡满／只读／提交失败。原生设置布局及实际 sidecar 首屏／滚动说明已验证。这些测试不代替真实卡验收。
- .177 应用为 6,915,232 字节，SHA-256 `0f6a93a05d11a82cf2a583ebb13eaff5ae0901f90d6faf76b1a8cab5cc426da5`；首次安装、升级和公共源码清单绑定同一镜像。源码记录保留完整依赖许可和构建输入，不宣称已复现逐字节二进制。
- 本机沿原 Release 路径直接部署桥接 0.6.6，保留旧程序备份和既有图库。通过正常桌面环境启动，读回自动轮播、原六页顺序及 15 秒间隔。工具启动环境被数据目录保护拒绝时，没有绕过保护或迁移配对数据。
- 用户完成 .177 升级后，USB 只读核验确认三个递增运行心跳、正常显示心跳、零显示写入错误、精确 ELF 指纹和 `ota_0 / VALID`。设备回报 TF 状态为未挂载，两个类别已保存数量均为 0；真实整库保存和断开桥接后读卡尚未验收，正在核对卡格式和介质状态，不格式化卡。

本次原始证据位于 `artifacts/development/compact-tf-release-20261011/`。此前汽水音乐、Codex 已读同步、Apple Music 和长期稳定性的验收边界沿用 0.6.5。

## English

Bridge 0.6.6 trims Windows/Apple Silicon Mac tool bundles while preserving offline flashing, matching source and notices. Official esptool executable bytes are unchanged; builds verify upstream inputs and runtimes verify a deterministic subset. Existing backup/write/verify protections and installer runtime requirements remain intact.

TAB5 0.2.177-ui automatically synchronizes both complete installed collections from a supporting Windows bridge over USB/Wi-Fi: every work, page, orientation and complete optional guide. Settings show per-category progress. Saved collections remain available offline across reboot/year changes; unsaved works require the bridge. Persistent cursors resume interrupted work and verified pages avoid duplicate transfers. Background jobs allocate no RGB frames and yield to foreground use; BLE retains individual-image transfers. Changed revisions refresh metadata and reuse identical JPEGs, so text-only changes require no image ranges. Alternating checked records exclude incomplete/corrupt/stale data. Card failures pause saving/fall back; shared gallery/photo locking does not format or delete unrelated files. ZIP import remains on the computer. Mac does not currently offer Windows artwork synchronization.

The frozen font maps 9,027 glyphs, adds 11 to public .176, and preserves all 9,016 prior metrics/bitmaps. The latest guide snapshot requires 5,561 characters, all covered. Font updates ship in firmware; future collection text requires another coverage check. New collection images/text remain excluded and ESP8266 0.5.2 remains unchanged.

Validated candidates are 24,649,549 bytes for Windows (58.5% smaller), 14,258,130 bytes for Mac (70.3% smaller), 4,609,229 bytes for TAB5 first installation and 4,579,423 bytes for upgrade. Bridge binaries/source identify `fa4175284ae18efa5e213a1ec0440c04ab8fa8d4`; the linked CI passed Windows/macOS builds, Swift tests, package/signature verification and actual unpacked offline tool checks. Four real-input tool tests and 750 authenticated artwork transport checks passed. TAB5 source identifies `b742cb1b3333fb6c4357c0a6d120d05a6799dd03`; native full-library/resume/revision/error/UI tests, ESP-IDF build, real ELF guards and actual sidecar previews passed. First-install/upgrade/source materials bind the same 6,915,232-byte image and SHA-256 recorded above; byte-for-byte rebuild reproducibility is not claimed.

Bridge 0.6.6 was directly deployed at the original local path with backup, existing collections and the original six-page/15-second automatic carousel preserved. Normal desktop startup passed the profile guard; redirected tool startup was rejected and no guard bypass or pairing-data migration occurred. Following the user's .177 upgrade, three read-only USB heartbeats verified the exact ELF, healthy display and `ota_0 / VALID`. The card currently reports unmounted with zero persisted works: physical complete-library saving and offline card playback remain pending card/filesystem diagnosis. No card formatting occurred. Public downloads still identify .6.5/.176 until publication; original evidence is under the development directory above. Previous .6.5 acceptance limits remain.
