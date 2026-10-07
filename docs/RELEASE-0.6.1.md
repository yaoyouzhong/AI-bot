# AI-bot 0.6.1 / TAB5 0.2.149-ui

本次修复 TAB5 拔掉 USB 后的图库同步，并改善首次蓝牙下载与重复查看。电脑端升级到 0.6.1，TAB5 升级到 0.2.149-ui；ESP8266 仍为 0.5.0。已安装的名画、书法包继续使用，无需重新下载。

## 改动

- 无线图库统一使用认证 POST，修复 Wi-Fi 下图片不更新的问题；USB、Wi-Fi、BLE 均保留支持。
- TAB5 保存最多 2 MiB JPEG，预取同页另一方向；命中缓存时跳过下载。重启、回收或淘汰后仍需重新同步；相机、语音、附件和升级优先回收缓存。
- 仅蓝牙模式下载图片时临时暂停 Wi-Fi。连续横竖图复用暂停，空闲 600 毫秒后恢复，继承最初的 35 秒上限；错误、模式变化或超时均走恢复路径。自动选择模式保留原有回退通路。
- Windows 复用图片快照及哈希，连续 BLE 图库请求保持吞吐优先；增加 USB 枚举/握手计时与 Wi-Fi 恢复诊断。
- 不改变图像质量、配对、网络配置或固件分区。macOS 仅同步版本元数据，保留原有平台功能。

## 实测与边界

`.149` 已安装并核验精确 ELF、有效 OTA 分区和显示错误 0。同一幅名画竖屏冷下载两次为 **7.26 / 8.54 秒**，此前 `.147` 同图记录为 9.48 秒；这是少量跨运行比较，不保证固定提速比例或每次低于 8 秒。两轮下载均未观察到蓝牙掉线，已记录的中断处于升级或主动重启附近。原自动轮播、六页顺序和 15 秒间隔保持。

图库缓存、无线恢复及相关传输回归通过。USB 间歇性慢枚举仍保留诊断记录，本次不宣称已彻底修复。首刷包与升级包包含同一已验收应用；没有擦除现有设备重做出厂首刷，macOS 图形安装与交互也不由 Windows 测试推定。

详见 [BLE 下载验收](TAB5-BLE-GALLERY-148.md)、[缓存记录](TAB5-GALLERY-CACHE-147.md)、[无线同步修复](TAB5-GALLERY-TRANSPORTS-146.md)和 [USB 排障记录](TAB5-USB-RECONNECT.md)。首次安装用完整首刷 ZIP，已有 AI-bot 设备用升级 ZIP；两者不能混用。可选图库继续使用 [.145 发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.145-ui)的原包。

## English

Bridge 0.6.1 and TAB5 0.2.149-ui repair wireless gallery synchronization and improve cold BLE downloads and cached revisits. ESP8266 remains 0.5.0; installed optional artwork collections remain compatible and need no replacement.

Authenticated gallery HTTP requests explicitly use POST. TAB5 retains up to 2 MiB of JPEGs, prefetches the alternate layout and releases the cache for camera, voice, attachments and OTA. In explicit BLE-only mode, downloads temporarily pause Wi-Fi; consecutive layouts share the pause across a 600 ms idle window without extending the original 35-second deadline. Errors, mode changes and timeout restore networking. Automatic-mode fallback remains available. Windows reuses validated image snapshots and hashes, retains throughput preference across gallery requests, and adds USB startup and Wi-Fi restoration diagnostics. Image quality, pairing, saved networks and partitions are unchanged. macOS retains existing functionality with synchronized version metadata.

The installed .149 passed exact-image and valid-partition checks with no display errors. Two uncached BLE fetches of the same portrait took 7.26 / 8.54 seconds versus a prior .147 record of 9.48 seconds; this limited comparison is not a fixed performance guarantee. Neither transfer showed a disconnect; recorded interruptions coincided with upgrades or deliberate restarts. Original carousel settings were preserved. Related regression checks passed; intermittent slow USB enumeration remains under diagnosis. First-install and upgrade packages share the verified application, but a fresh factory flash and macOS interactive acceptance are not claimed. Existing optional art packs remain available from the .145 release.
