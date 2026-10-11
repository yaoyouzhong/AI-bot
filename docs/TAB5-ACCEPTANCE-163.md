# TAB5 .163 实机验证 / Hardware validation

2026-10-10：已按精确候选授权安装，USB 核对完整 ELF 与 ota_0 VALID 分区通过。用户确认横屏可以操作、蓝闪消失，但横竖屏切换明显迟缓，旋转体验验收失败。USB／BLE 三通道测速及 USB 持续观察已有失败，未宣布稳定性通过；未公开发布。配套桥接沿用已运行版本。

| 对象 | SHA-256 |
| --- | --- |
| 固件 `0.2.163-ui` | `bcc3c1bf5941bb6881c9d05d81e98c9e43053ad7ca8d3e4f9fd8362f8fd3cef6` |
| 内嵌 ELF | `e3b5041b3ccaca8d5799dd5856ee3b7d82366a1d40954e8211da7dc51d524604` |
| 实际升级说明 | `cecb12d72ffc382198ebf5248ae3c0423c968f4bfe51d84fb2d4be5d1e0890b8` |
| 名画修正包 `2026.10.10.1` | `886058efa89620a9535fc9105d6eb93d6c370660a14d3adf5500ed998b6e089c` |
| 沿用桥接 DLL | `38ab55303bcaf6cd331619fbc8887753921c4ad455e7dede6b34dd9e482d9257` |

固件为固定路径 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，6,677,568 字节，归档 `versions/0.2.163-ui/bcc3c1bf5941-cecb12d7/`。.162 的历史镜像及说明保留。未采用升级箭头外围圆圈方案。

## 屏保修改与验证

- 隐藏页面的主缓存和 native 缓存在艺术屏保期间无须保留。进入名画、书法屏保或预览时释放这些缓存，保留艺术位图和选择；退出后由原缓存计时器重建。普通屏保行为保持。
- 作品详情原先提前返回，使打开详情后的传感器方向变化无法应用。现在先调整阅读布局与触控坐标，再暂停作品／日期／页码轮换；返回同步更新画面方向。
- 与 .162 的 146 文件 main 快照对照，仅 `ui.c`、`ui_gallery_reader.h`、`ui_gallery_saver.h` 变化。当前源码逐文件与隔离 ESP-IDF 构建快照一致；USB 源码 SHA-256 不变。
- 原代码在受限绘图内存回放中失败，保留 `render-budget-baseline.log`：约 700 KiB 预算无法同时容纳已分配层及 2,693,520 字节的新旋转层。LVGL 的分配失败会等待重试，可导致绘图无法完成；这支持内存缺陷诊断，尚不能证明所有真机蓝闪均由此导致。
- 修复候选预热全部隐藏页面缓存后进入屏保，使用实际 512 行分块绘图，对名画／书法 × 正反横竖屏 × 画面／详情，共 16 组合逐像素对照完整绘图。全部通过，旋转层峰值为 3,250,800 字节；转换后的 Details／Return 触控、阅读中四方向切换、原页返回通过。
- 原生 LVGL 使用实际 .163 sidecar 校验分类、条目与滚动；其余主页面、额度、输入和轮播回放通过。ESP-IDF 构建和正式固件准备脚本通过。
- 补充两分类 × 四方向的缓存画面触控退出，共 8 项：真实指针输入触发退出，屏保层隐藏、亮度恢复、展示模式和轮播偏好保持。完整回放再次通过。最初补测重入屏保时，测试时钟日期与位图日期不一致，允许的在线换日刷新淘汰了后续姿态的测试位图；该失败日志保留，夹具按既有离线回放规则固定缓存场景，未修改生产代码或弱化断言。在线日期更新与下载用例继续保留。

软件内存预算回放未模拟真实堆碎片、任务调度及面板信号，不能代替硬件验收。

## 名画真实基线修正

批准的 .162 名画包已正确导入，但其本地基线与实际旧图库有两幅年度作品差异，记录见 [.162 验收](TAB5-ACCEPTANCE-162.md)。修正候选从实际旧名画备份和未改变的实际书法目录重新生成，保留用户原年度身份、顺序、页数和全部竖屏字节。已通过运行版本的生产导入器替换实际名画目录，原备份与新备份均保留。

- 文件：`artifacts/development/saver-163-20261010/painting-production-layout/packs/AI-bot-DailyPainting-2026.10.10.1.zip`，208,814,317 字节。实际旧图库的 366 件作品及全部 catalog 元数据逐项相同，恢复原序号 120、128 的作品。
- 430 张横屏保留完整作品名、作者和年代；430 张竖屏与实际旧备份逐文件 SHA-256 相同；1,782 张书法图片与实际安装目录逐文件相同。
- 原始来源校验、两分类年度完整性、366 件名画的标题／作者／年代字形检查通过；原生名画 fixture 与该候选的实际图片哈希一致。
- 使用正在运行版本的桥接 DLL，在隔离 profile 中通过生产导入器检查全部 JPEG、元数据与 SHA-256；每分类 366 天 × 两种方向的服务 manifest 返回成功，并保存 100%／150%／200% 图库界面截图。实际安装只更新名画；不安装测试过程中生成的书法包。

## 待真机验证

对应精确候选批准后，核对 ELF、VALID 分区和新鲜设备回报。检查名画、书法四方向的蓝闪、翻页、详情、阅读中旋转、返回及退出，重测进入预览的全程等待；随后执行三通道 RAM 完整性及至少 60 分钟 USB 观察，保留任何失败。结束正常退出桥接、执行 `--restore-cycle-default`、正常启动，核对原六页面、顺序及 15 秒间隔，恢复用户原仅蓝牙设置。

## English

The .163 firmware is installed with exact-candidate authorization and verified by full ELF hash and ota_0 VALID state, without public release. The user reports usable landscape interaction and disappearance of blue flashes, but rotation remains too slow. USB/BLE benchmark failures and a USB authentication failure during observation prevent a stability pass. Only three gallery UI files differ from the .162 snapshot; all 146 source hashes match the isolated build, and USB source is unchanged. Release hidden main/native page caches before art rendering. Details follows device orientation while retaining work/date/frame, and Return adopts the current orientation.

The original code fails a constrained-memory render replay; the fixed candidate passes 16 actual 512-row partial-render combinations with pixel comparison, transformed touch checks and live orientation changes. Actual .163 release notes and other UI regression checks pass. This does not model hardware fragmentation, scheduling or panel behavior; hardware blue flashes, exit and sustained USB stability remain unverified. The .162 failures are retained.

Eight cached-artwork touch exits additionally pass through the real pointer dispatcher, restoring brightness and retaining mode/cycle preferences. The first added test failed because re-entry used a clock date different from its image fixture, allowing an online daily refresh to evict a later pose's image. The failed log is preserved; the fixture now follows the existing offline-cache replay setup. Production code and assertions were not weakened, and online daily-refresh/download tests remain covered.

The painting correction candidate 2026.10.10.1 is ready, built from the actual old backup: all 366 identities, order and catalog metadata match, all 430 portrait images retain their bytes, and all 1,782 calligraphy images match the actual installed collection. Landscape captions, original source hashes, annual completeness, glyph coverage and the actual native fixture are validated. The running bridge version's production importer passes all JPEG/hash checks and 366 dates in both orientations per category in an isolated profile, with 100%/150%/200% UI captures. Only the painting pack is proposed for installation. The painting correction is now imported through the running production DLL into the real profile. Verification covers 866 painting files, all 366 original identities/order, 430 unchanged portraits, both complete backups and 1,788 unchanged calligraphy pack files. Hardware visual acceptance and restoration of the original carousel/Bluetooth-only preference remain separate.

Evidence: `artifacts/development/saver-163-20261010/validation-source163.json`, `saver163-firmware-build.json`, `native163.log`, `native163/`, `native163-exit.log`, `native163-exit-fixed.log`, `gallery-correction-validation.json`, `gallery-import163.log`, `gallery-import-ui/`; baseline failure: `artifacts/development/saver-162-20261010/render-budget-baseline.log`.

## 本轮真实安装与结果 / Current hardware evidence

- .163 于 16:50:26 回报安装完成；USB 启动核验精确 ELF `e3b5041b3ccaca8d5799dd5856ee3b7d82366a1d40954e8211da7dc51d524604`，`ota_0`、`VALID`，镜像 6,677,568 字节。Wi-Fi 压缩传输 4,179,657 字节、14,968 ms，不能当作完整安装时间；设备 installation phase 为 19,500 ms。
- 持续观察从 16:52:51 开始，16:55:38 已捕获 USB RPC stage 2、error -18、1,953 字节的认证失败；保留首次失败，后续发生串口写入超时／恢复。原定 60 分钟观察尚未完成，不能宣布通过。
- 三通道 RAM 测速只完成 10 项：USB 上传 256 KiB 用时 15,185 ms，下行 0 字节失败（串口响应写入超时 0x80070079）；Wi-Fi 三轮双向均通过；BLE 上传通过，下行只有 196,608/262,144 字节失败。未通过的通道没有补成成功项。
- 用户先后纠正方向反馈，最终明确“能够切换但时延太长”。传感器已识别横屏而图片仍维持竖屏；代码发现翻页／阅读返回的 30 秒暂停也阻塞缺失方向加载。.164 另行准备当天压缩缓存、方向预热与更快姿态确认，不能以软件回放替代真机速度。
- 名画修正已真实导入，全量 SHA、原年度目录及顺序、430 竖屏字节、两份备份、书法未改全部核对通过。记录 `installed-gallery-correction-verified.json`。

The exact .163 boot is verified. Compressed Wi-Fi transfer timing is separate from the 19,500 ms device installation phase. Observation records a USB RPC authentication failure after 167.656 seconds, then serial write timeout/recovery. The 10-case RAM benchmark fails USB download and BLE download; all six Wi-Fi cases pass. It is neither an 18-case pass nor a completed 60-minute stability pass. The user clarifies that orientation changes eventually occur but are too slow. A 30-second manual-page/reader-return hold blocks missing-layout loading; .164 is being prepared separately. Production painting correction and full preservation checks pass; physical visual acceptance remains distinct.

Evidence: `artifacts/development/saver-163-20261010/after-boot-verification.txt`, `hardware-observation.jsonl`, `hardware-observation-state.json`, `hardware-benchmark-1.json`, `installed-gallery-correction-verified.json`.
