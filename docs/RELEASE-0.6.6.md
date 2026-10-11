# 桥接 0.6.6 与 TAB5 0.2.177-ui

本次精简 Windows／Apple Silicon Mac 安装材料，保留离线刷机；TAB5 增加 TF 卡作品缓存及最新字库。新图库内容仍整理中，本次不发布；现有图库继续兼容。ESP8266 0.5.2 保持原下载。

## 精简与校验

从已核验的 Espressif 4.9.1 官方独立档案中保留 esptool、LICENSE、README，移除未使用的 espefuse、espsecure、esp_rfc2217_server。可执行文件字节与官方版本一致，完整匹配源码和现有依赖许可保留。ORIGIN.json 记录官方档案、精简档案、可执行文件与源码的校验值；运行时仍校验固定档案哈希后从新临时目录提取，不依赖网络、Python 或以前的工具缓存。

备份、写入、校验命令和串口选择保护保留。Windows 安装所需的 .NET／WebView2 条件与此前一致；保留离线刷机不意味着缺少运行时的新电脑可全程离线安装。

## TF 卡与字库

已从桥接校验获取的名画／书法图片及完整可选导读按类别、日期、页码和横竖屏保存到 `/sdcard/aibot/gallery`。再次查看优先使用内存或 TF；已保存日期可跨重启复用，尚未同步的日期需要桥接。本版不直接导入年度 ZIP，也不一次性复制全年图库。

图片与文字同一记录，各自校验 SHA-256。两份轮换记录保留完整提交，未完成或损坏记录不参与展示；已认证的内容版本排除旧记录。无卡、只读、容量不足或介质错误时沿用内存／桥接。照片与图库共用挂载和文件访问锁，不格式化、不删除其他用户文件。

冻结字库含 9,027 个映射字形，比正式 .176 增加 11 个；原有 9,016 个字形尺寸及位图逐项保持。冻结导读资料所需的 5,557 个字符全部覆盖。字形随固件安装，图库导入不会更新字库；整理后的新内容发布前再核查缺字。只有字库进入固件，整理中的图片、导读和原文未打包。

## 验证记录

发布准备中。测试、最终包大小、源码／镜像身份与硬件验收将在完成后记录；不把构建或模拟通过称为实机通过。此前汽水音乐、Codex 已读同步、Apple Music 和长期稳定性的验收边界沿用 0.6.5。

## English

Bridge 0.6.6 trims Windows/Apple Silicon Mac tool bundles while preserving offline flashing, matching source and notices. Official esptool executable bytes are unchanged; builds verify upstream inputs and runtimes verify a deterministic subset. Existing backup/write/verify protections and installer runtime requirements remain intact.

TAB5 0.2.177-ui stores synchronized JPEGs and complete optional guides on TF, keyed by category/date/page/layout. Memory/TF precede bridge requests; saved dates persist and unsynchronized dates still require the bridge. Alternating checksummed records exclude incomplete/corrupt/stale revisions. Card failures fall back; gallery/photo I/O shares a nonformatting mount/lock and retains unrelated files. Full annual-ZIP installation/initial copying is outside scope.

The frozen font maps 9,027 glyphs, adds 11 to public .176, and preserves all 9,016 prior metrics/bitmaps. Its frozen guide snapshot requires 5,557 characters, all covered. Font updates ship in firmware; future collection text requires another coverage check. New collection images/text remain excluded and ESP8266 0.5.2 remains unchanged. Final package/build/source and hardware evidence will be recorded before publication; previous 0.6.5 acceptance limits remain.
