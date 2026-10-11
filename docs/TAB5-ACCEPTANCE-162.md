# TAB5 .162 与名画布局包候选 / Local candidates

2026-10-10：用户已批准该精确固件与名画包。设备经 Wi-Fi OTA 上报 .162，随后精确 ELF/VALID 核验通过；本次实机屏保验收失败，不能认定 .162 稳定。原 .161 与 .160 失败记录保留。此变更不包含升级箭头外围圆圈方案。

## 安装后故障现场

- Wi-Fi 压缩 OTA：镜像 6,677,440 字节，实际传输 4,179,736 字节，桥接记录传输 10,984 ms；此数值不包含完整重启和画面就绪时间。
- 用户报告：名画转为竖屏后，前后翻页、作品详情无法操作，不能切回横屏，也无法正常退出，并伴随蓝闪。
- 桥接最后设备回报停在 15:49:57；15:54:31 USB open 仍出现 `IOException / 0x80070079`。故障期间的回报已过期，不能用其中的零显示错误判断画面正常。
- 用户按请求重启后，15:57:25 的新回报显示 .162、USB/Wi-Fi/BLE 恢复、界面心跳年龄 44 ms。重启后的 crash 查询为空，无法从崩溃堆栈确认根因。
- 自动艺术屏保再次卡住，设备回报停在 16:06:30；该回报显示隐藏主页面缓存 5,036,544 字节、native 缓存 3,777,408 字节、剩余 PSRAM 477,000 字节。16:11 的 USB open 仍超时。用户随后报告横屏也蓝闪、无法操作和退出。
- 后续设备恢复的新回报中，启动核验为 `.162`、ELF `63c85dd48b89d4e8e702a60041fc929174ddd104f273d4fa85c8b6bdf8b20356`、`ota_1 / VALID`。16:17:31 回报界面心跳年龄 47 ms；心跳和启动核验不替代用户可见交互验收，也不冲销此前卡死。
- 三通道测速和一小时正式观察尚未开始；屏保故障排查优先。此记录不以重启恢复冲销故障。

现场证据：`artifacts/development/saver-162-20261010/portrait-failure-live.txt`、`after-power-restart.txt`、`crashes-after-restart.json`、`second-freeze-live.txt`、`landscape-failure-live.txt`、`after-boot-verification.txt`。

| 对象 | SHA-256 |
| --- | --- |
| 固件 `0.2.162-ui` | `df79c8feac9d8aba6e92d824a9060b0580c21dd02ce774a19fecc497810a7b49` |
| 内嵌 ELF | `63c85dd48b89d4e8e702a60041fc929174ddd104f273d4fa85c8b6bdf8b20356` |
| 实际升级说明 | `07c7a362c047a80622946e20b375b831f3ed250d92db4a37abad257ae645079a` |
| 名画 `2026.10.10` ZIP | `90203e3bbeea9a9aaf14f008ad55892bf5d1ae0e313fa98e9b5eb03b514d0291` |
| 沿用桥接 DLL | `38ab55303bcaf6cd331619fbc8887753921c4ad455e7dede6b34dd9e482d9257` |

固件安装时使用固定 `latest/aibot_tab5.bin`，6,677,440 字节，分区余量 531,520 字节；.162 历史包保存于 `artifacts/firmware/tab5/versions/0.2.162-ui/df79c8feac9d-07c7a362/`，后续候选会更新 latest。名画包为 `artifacts/development/saver-layout-20261010/painting-layout-candidate-v2/packs/AI-bot-DailyPainting-2026.10.10.zip`，208,484,185 字节。

## USB 修改及证据

相对已安装 .161 的完整 main 快照，146 个文件中仅生产文件 `usb_link.c` 改变，源码与隔离构建快照逐项 SHA-256 一致。

- 复现旧逻辑关闭／重开串口后，接收任务延迟清空缓冲导致新命令丢失。清理移到 DTR 降低回调，仅操作 TinyUSB 自带同步的读写 FIFO，不取得应用锁；接收任务恢复时只重置解析器和 mailbox。
- 回复使用命令被读取时的连接代次；关闭／重开后禁止旧命令回复进入新会话。接收缓冲逐字节检查代次变化。连接状态同时要求总线就绪和串口 DTR。
- 原代码故障测试失败日志保留；固定代码通过关闭清理、延迟恢复保留新命令、旧回复拒绝、传输中关闭、断连、堵塞时限和完整 65,535 字节发送检查。二进制边界与 mailbox 检查继续通过。
- ESP-IDF 隔离构建完成；生产正式准备脚本验证镜像、版本与实际 sidecar 并归档。原生 LVGL 预览使用该真实 sidecar，分类、条目及滚动显示正常。

这是已复现的生命周期缺陷修复，尚未证明它解释 .161 全部解密校验失败和 Windows 写入超时。没有放宽认证、吞掉错误或用第三轮成功冲销前两轮失败。

## 名画横屏包及证据

- 参考书法布局，在左侧完整显示作品名、作者和年代；完整画作等比置于右侧，馆藏、翻页与详情沿用底部。长标题换行，英文词按词边界换行，元数据无法在可读字号内完整显示时阻止生成。
- 元数据按 workId 从审定导读匹配，缺项或重复时停止。本地生成基线的原始来源 SHA-256、作品身份、366 件年度顺序及 430 张横屏页保持；该基线的 430 张名画竖屏与 1,782 张书法图片逐文件保持原字节。这一结论未覆盖安装前的实际用户图库。
- 全部名称、作者、年代的字体字形检查和年度完整性通过。用生产导入器在隔离 profile 导入最终名画包与原书法包，全部 JPEG 验证成功；每分类 366 日期 × 两种方向的服务 manifest 返回正常，保存 100%/150%/200% 图库界面截图。
- 原生界面检查两分类 × 正反横竖屏的底栏区域、转换后 Details/Return 点击通过，名画 fixture 与最终包图片 SHA-256 一致。浏览器实际 JPEG 预览核对长标题、宽卷、翻页、详情与返回。

横屏渲染是独立名画包，刷固件不会自动替换图库；安装时只更新名画，生产导入器保留旧图库备份。竖屏保持用户要求的精简显示，书法不替换。

### 实际导入核对与基线差异

用户确认导入完成。实际安装目录的 PACK.json 与批准 ZIP 完全一致，866 个声明文件 SHA-256 通过；旧包备份 `painting.backup-7817326fabdb4b8dbf7fd1f48c3f1d5e` 的 866 个文件通过。书法的 1,788 个文件与安装前记录一致。

实际旧名画与候选基线存在两件作品差异：零基序号 120 的 `cma-1918.544` 被 `cma-1957.139` 替代，序号 128 的 `cma-1962.161` 被 `cma-1957.141` 替代；其余 428 张竖屏字节一致。年度与全部竖屏保留检查因此失败，不能把本地基线通过写成用户图库完全不变。保留失败证据，并以实际旧包为基线准备修正候选；尚未再次替换实际图库。

证据：`artifacts/development/saver-162-20261010/installed-gallery-verification-complete.json`。

## 后续实机验收

新精确候选须获批准后安装。核对 ELF、VALID 分区、界面心跳与显示错误；进行三通道 RAM 完整性和 USB 连续观察，保留任何失败；实机检查横屏四项信息、详情／返回和正反方向，并重测预览等待时间。最后正常退出桥接、执行 `--restore-cycle-default`、正常启动，核对原六页面、顺序和 15 秒间隔。测试用网络自动模式仍需恢复用户原仅蓝牙选择。

## English

The exact .162 firmware and painting pack were approved. Wi-Fi OTA transferred 4,179,736 compressed bytes in 10,984 ms, followed by exact ELF verification and an ota_1 VALID partition. Hardware screensaver acceptance failed: portrait navigation/details/exit became unresponsive with blue flashes, and the user later reported landscape flashing and unresponsive exit. Telemetry stopped twice; the second event retained 5,036,544 bytes of main-page cache and 3,777,408 bytes of native cache with 477,000 bytes of free PSRAM. Restart recovery, fresh UI heartbeats and boot verification do not erase this failure. No retained crash stack was available. Benchmark and full-hour acceptance have not started.

Production import matches the approved ZIP and all 866 declared files; the complete old backup and unchanged calligraphy files are verified. However, two annual works differ from the actual pre-import painting collection. All 428 common portrait images match, but complete preservation fails. The previous preservation claim covered the local generation baseline only. This mismatch remains recorded while a candidate is rebuilt from the actual pre-import gallery. No public release occurred.

Evidence: `artifacts/development/saver-layout-20261010/validation-162.json`, `cdc-reconnect-baseline.log`, `cdc-reconnect-fixed-final.log`, `native-candidate162/`, `painting-layout-candidate-v2/`, `painting-pack-import-test.log`, `gallery-info-final.png`.
