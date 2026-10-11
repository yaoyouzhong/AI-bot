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

冻结字库含 9,027 个映射字形，比正式 .176 增加 11 个；原有 9,016 个字形尺寸及位图逐项保持。冻结导读资料所需的 5,557 个字符全部覆盖。字形随固件安装，图库导入不会更新字库；整理后的新内容发布前再核查缺字。只有字库进入固件，整理中的图片、导读和原文未打包。

## 验证记录

发布准备中。测试、最终包大小、源码／镜像身份与硬件验收将在完成后记录；不把构建或模拟通过称为实机通过。此前汽水音乐、Codex 已读同步、Apple Music 和长期稳定性的验收边界沿用 0.6.5。

## English

Bridge 0.6.6 trims Windows/Apple Silicon Mac tool bundles while preserving offline flashing, matching source and notices. Official esptool executable bytes are unchanged; builds verify upstream inputs and runtimes verify a deterministic subset. Existing backup/write/verify protections and installer runtime requirements remain intact.

TAB5 0.2.177-ui automatically synchronizes both complete installed collections from a supporting Windows bridge over USB/Wi-Fi: every work, page, orientation and complete optional guide. Settings show per-category progress. Saved collections remain available offline across reboot/year changes; unsaved works require the bridge. Persistent cursors resume interrupted work and verified pages avoid duplicate transfers. Background jobs allocate no RGB frames and yield to foreground use; BLE retains individual-image transfers. Changed revisions refresh metadata and reuse identical JPEGs, so text-only changes require no image ranges. Alternating checked records exclude incomplete/corrupt/stale data. Card failures pause saving/fall back; shared gallery/photo locking does not format or delete unrelated files. ZIP import remains on the computer. Mac does not currently offer Windows artwork synchronization.

The frozen font maps 9,027 glyphs, adds 11 to public .176, and preserves all 9,016 prior metrics/bitmaps. Its frozen guide snapshot requires 5,557 characters, all covered. Font updates ship in firmware; future collection text requires another coverage check. New collection images/text remain excluded and ESP8266 0.5.2 remains unchanged. Final package/build/source and hardware evidence will be recorded before publication; previous 0.6.5 acceptance limits remain.
