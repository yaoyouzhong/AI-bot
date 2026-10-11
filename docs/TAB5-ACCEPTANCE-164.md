# TAB5 .164 当天缓存与旋转优化 / Daily cache and rotation

2026-10-10：精确候选已获批准并安装，完整 ELF／ota_1 VALID 启动核验通过，未公开发布。当天缓存与离线交互真机验证通过；首次同步阶段曾出现操作迟缓，USB 认证／重连超时使通信稳定性验收未通过，不能据此宣布全部验收完成。

| 对象 | SHA-256 |
| --- | --- |
| 固件 `0.2.164-ui` | `17424307e4705e3a57b79be349430f27b15e63c5525dcf3ee40c9739554ed74c` |
| 内嵌 ELF | `069983a6a47763efe917bc7f54c2fdc2cb854b6dc0c196cacd18f6e1aa6a26b5` |
| 升级说明文本 | `6b96bde334b2b7c367241b9c350eff80966f849dfbc1cd323d3d0021a50a0c88` |
| 沿用桥接 DLL | `38ab55303bcaf6cd331619fbc8887753921c4ad455e7dede6b34dd9e482d9257` |

镜像 6,680,128 字节，安装时固定路径为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，精确归档为 `versions/0.2.164-ui/17424307e470-6b96bde3`。latest 此后已有未批准安装的 .165 草稿，不应将其视作 .164 或当前修复的交付。源文件 146 项与当时隔离 ESP-IDF 快照全量一致；相对 .163 只修改姿态采样／过滤、JPEG 缓存、书画 worker／调度／控件及升级标识共 9 项。分区、认证、USB、Wi-Fi、BLE 底层和日常 USB 优先策略不变。

## 当天缓存

- 当前设备没有 TF 卡。Flash 为 16 MiB，双 OTA 分区各 6.875 MiB、桌宠双份缓存 2 MiB，其余为设置及启动分区，不能挪用来保存完整当天书画。使用 PSRAM 中的 JPEG 压缩缓存，不修改分区、不格式化设备。
- 实际已安装图库中，今天两分类共 5 页、10 张横竖屏图片，2,225,576 字节（2.12 MiB），超过旧 2 MiB 压缩缓存上限。全年最大组合为 9,338,171 字节（8.91 MiB）、14 页；最多页面日为 15 页、8,244,075 字节。
- 新压缩缓存上限 10 MiB、64 项，按当天日期、分类、页码和方向区分。进入书画后，后台发现两分类页数并预取全部页面和方向，支持阅读期间继续同步；不生成后台 RGB 画面。仍只保留两个 RGB 输出缓冲。
- 新一天完整下载并校验首张 JPEG 后才清除旧日压缩缓存，失败保留原成功内容；旧日迟到结果不能恢复旧日缓存。初次下载、重启和语音／相机／照片大任务回收后需要重新补充。
- 后台预取开始前至少有 5 MiB PSRAM，下载后保留 4 MiB；低内存暂停。10 MiB 是上限，并非在所有运行组合下保证完全填满；尚未在真机最复杂日期证明全部缓存。旧图和当前页保留。
- 前台请求取消后台预取，后台只在已验证的 SHA 和 JPEG 尺寸下入缓存。手动翻页忙时保留最近一次点击，空闲时执行，防止预取吞掉操作。认证／内容错误不会在一次读取中自动重试；缺失回复仍受原有次数限制。

## 旋转与标识

手动翻页或退出阅读的 30 秒停留只暂停自动换页，同页的另一方向可立即加载并提前预热。姿态采样从 100 ms 改为 50 ms，稳定确认从 350 ms 改为 150 ms，仍拒绝平放、模糊斜角、加速度和短暂抖动；4 个连续样本确认。方向已准备时，生产 50 ms UI 计时器直接换图，不发网络请求。物理转动到面板完成刷新的耗时尚未测得，不能以 150 ms 确认时间代替。

升级箭头采用 26 × 26 深绿色圆底及白色箭头，已检查真实 .164 sidecar 的首屏和滚动末屏预览。

## 验证与功能漏纳审计

- ESP-IDF 独立快照构建、正式镜像与 notes 校验、146 项源码哈希一致性通过。
- 方向过滤／真实 BMI270 驱动恢复测试通过：四方向、浅角支架、短暂错轴、平放、斜角、晃动、过期样本。
- 30 张布局／9.6 MB 压缩缓存保留及跨日淘汰测试通过；生产 worker 自动预取两分类全部页和方向后，离线逐页重开零 RPC，后台不解码和不增加输出位图。低内存暂停、跨日失败保留、哈希错误、前台回收和三通道读取语义测试通过。
- 8 组手动翻页／阅读返回的缺失方向转换、8 组提前预热后计时器换图零请求、8 组忙时翻页点击恢复通过；始终仅两个输出缓冲。
- 把绘图可用空间压到 4 MiB，名画／书法 × 四方向 × 图片／详情共 16 组合，实际 512 行分块与完整绘图逐像素一致，峰值 3,250,800 字节；其余主页面／额度／语音／通知等 LVGL 回放通过。一次测试失败由夹具未开启 saver 导致生产计时器未进入书画分支，失败日志保留；修正夹具状态后同一断言通过，生产代码未为此改动。
- 正在运行桥接的 Portable PDB 316 项源码 SHA 与当前文件全部一致；内嵌 2,632 件馆藏导读资源 SHA 与实际 catalog 一致。近期已开发功能未发现其他实现漏纳。升级圆底先前只有方案，已在 .164 实现；名画修正是独立图库包，已真实导入并验证完整保留。
- 运行 DLL 内嵌历史更新日志不会随文档编辑自动更新；本次固件使用匹配镜像的真实 sidecar，未为更新日志重装桥接。

## 真机验收与收尾

- 17:29:53 首次回报 .164，17:30:40 已记录完整 ELF `069983a6a47763efe917bc7f54c2fdc2cb854b6dc0c196cacd18f6e1aa6a26b5`、`ota_1 VALID`，升级错误 0。Wi-Fi 压缩传输 4,180,559 字节，12,922 ms；安装阶段独立为 19,378 ms。
- 17:31:36 后当天 10 张 JPEG 共 2,225,576 字节补齐。17:32:47 到 17:36:52 期间缓存命中增加 16 次、未发新取图 RPC，设备最新缓存解码 19–20 ms；这只是解码时间，不代替物理转动到面板完成刷新的总时延。后续记录命中 74 次，输出分配仍为 2。
- 用户先反馈翻页／详情迟缓，随后确认正常切换，并补充首次预览显示“等待电脑”。时序与首次同步相符，但不能单凭时序判定所有迟缓原因。新增异步调度负例在 .164 源码复现：后台请求未结束时，已经缓存的前台图片也被拒绝；修复作为独立下一候选准备。
- 17:50:59 确认桥接进程不存在且 18765 端口关闭。TAB5 保持通电不重启，用户确认书法左右翻页、横竖屏、详情／返回“离线正常”。这是实际设备离线验证，不能由桌面回放替代；没有验证最复杂日期或断电后的持久化。
- 正常恢复固定 Release 桥接，`/status` 确认 `selectedMode=auto`、`cycleEnabled=true`，原六页顺序和 15 秒间隔保留。恢复后 Wi-Fi／BLE 通信正常，USB 曾持续 `status_write` 超时 `0x80070079`；18:04:50 自行恢复，末样本设备回报年龄为 2 秒，最近控制失败仍为 18:04:48。恢复不代表认证／重连根因已解决。原蓝牙专用设置在剩余实机验证结束时恢复。
- 持续观察从 17:30:42 至 18:30:44，3601.657 秒、1731 个样本；4.157 秒时记录 USB RPC stage 2／GCM `-18`、49,236 字节，UI 心跳年龄 44 ms，显示错误／欠载均 0；之后 USB 回复和控制超时。首个失败保留，整体稳定性判定为失败。47 次读取失败发生于主动退出／重启阶段，期间无桥接回报属于预期；这不是连续在线一小时通过，也没有对 .164 重跑三通道测速，不把 .163 的失败当作 .164 成功。
- 用户随后要求暂不构建新固件，并明确顺滑优先于转场。后续本地交互修复及验证记录见 `docs/development/TAB5-GALLERY-INTERACTION-FIXES.md`；设备尚未运行这些修复。

后续已在当前 .164 上完成 USB / Wi-Fi / BLE 各三轮上传和下载内存测速，共 18 项通过，既有错误计数未增加。原一小时观察仍为失败；后续 USB 源码修复及回归边界见 [USB 排查记录](development/TAB5-USB-RELIABILITY-2026-10-10.md)，未构建或安装新固件。

19:02:55 又记录一次 USB metrics ACK 超时（4015 ms、零接收字节），随后恢复。该失败发生于测速结束后；认证失败计数未增加。后续采样不构成稳定性验收通过。

## English

The exact .164 image is installed locally with authorization and full ELF/ota_1 VALID boot verification; it is unpublished. Hardware daily-cache/offline interaction passes, while first-fill interaction and USB transport stability remain unresolved. Today's installed painting/calligraphy collection contains 5 pages and 10 layouts totaling 2,225,576 bytes; the most complex curated day totals 9,338,171 bytes. With no SD card, use a single validated-day JPEG cache capped at 10 MiB/64 entries, retaining only two RGB outputs. Background synchronization downloads both categories, every page and both layouts without decoding, preserves 4 MiB for rendering and pauses when memory is insufficient. Reboot or foreground memory reclamation requires refilling. A cap does not guarantee full cache population under every concurrent workload; hardware verification of the largest day remains pending.

Manual-page/reader-return holds no longer block layout loading and now warm the reverse layout. Posture sampling is 50 ms with 150 ms stability confirmation, retaining flat/shake/ambiguous/stale rejection. Cached rotation through the production timer makes no request. Background work yields and busy page taps are retained. The upgrade badge has a dark-green circle and white arrow. None of these timings substitutes for measured physical-rotation-to-panel-ready latency.

Source/build/sidecar checks, sensor/driver tests, single-day background/offline tests, 30 layouts at 9.6 MB, bounded output ownership and 24 hold/prewarm/input scenarios pass. All 16 actual partial-render combinations pass at a 4 MiB drawing budget with pixel comparison. A preserved failed replay had the saver disabled in its added fixture; correcting the state passes the same assertion. Running bridge source hashes (316) and all embedded art-guide bytes match current sources. No additional completed recent feature is found omitted; historical embedded release notes are distinct from the new firmware's actual sidecar. The device cached all 10 current-day JPEGs (2,225,576 bytes); subsequent cache hits decoded in 19–20 ms without new image RPCs. With the bridge process absent and port 18765 closed, the user confirmed physical calligraphy paging, rotation and Details/Return work offline without reboot. The original six-page auto carousel at 15 seconds is restored. USB GCM -18 occurred 4.157 seconds into monitoring; status-write timeouts followed bridge restart, then USB recovered at 18:04:50 with fresh subsequent reports. This recovery does not resolve the initial failures. The observer completed at 18:30:44 with 1731 samples and a failed overall result; intentional offline/restart intervals are separately recorded, so this is not a continuous-online one-hour pass. New firmware builds remain paused; subsequent local interaction fixes and the desktop-only, device-disabled motion experiment are documented in docs/development/TAB5-GALLERY-INTERACTION-FIXES.md. Largest-day, full physical latency and reboot-persistent caching remain unverified; the Bluetooth-only preference will be restored after remaining hardware work.

Evidence: `artifacts/development/upgrade-164-20261010/source164.json`, `daily-cache-budget.json`, `upgrade164-firmware-build.json`, `native-day-build.log`, `native-day.log`, `art-orientation-test.log`, `art-orientation-driver-test.log`, `gallery-jpeg-cache-test.log`, `gallery-worker-test.log`, `gallery-dma-test.log`, `bridge-source-audit.json`, `bridge-resource-audit.json`, `native/ota-release-notes-actual.png`, `native/ota-release-notes-actual-scrolled.png`; failed fixture evidence: `native-day-fixture-failed.log`.

Hardware evidence: `after-boot-verification.txt`, `install-first164.json`, `interaction-failure164.txt`, `interaction-recovered164.txt`, `hardware-observation-state.json`, `before-offline164.txt`, `offline-phase.json`, `after-offline164.txt`, `restored-cycle164.json` under `artifacts/development/upgrade-164-20261010/`.
