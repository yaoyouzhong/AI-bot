# TAB5 电量数字连续性

当前发布结果：用户已明确授权提交和公开发布；Windows 0.6.10 与 TAB5 .234 的带注释标签、正式附件及服务器摘要均已确认，下载中心同步更新。已验收 Windows 安装器和 c608 原字节保留，后续 CI 仅修复滚动可达性测试，远端全平台 CI 已通过；旧失败和未测系统分支保留。媒体 417 文件独立重建一致，主观试听未验，公开播放器交互由单独记录说明。后文早期“未授权”“待完成”均为保留的阶段记录。

Published outcome: explicit user authorization covered scoped commits, normal pushes, annotated component tags and public release. Formal asset sets and server digests match; downloads are current. Accepted Windows bytes remain unchanged. A test-only scroll-reachability repair passed remote CI; old failures and untested system branches remain recorded. The 417-file media project independently reproduces identical HTML. Subjective listening and separately tracked public-player interaction retain their stated limits. Earlier pending/unapproved states below are historical.


最终新增验收：用户在 .234 本次升级后直接确认“升级重启后，电量很快就显示且插拔后显示也没有问题”。据此记录本次启动和 USB 变化的电量显示验收通过，未额外要求重启或重做旧功能。首次显示时间为用户定性观察，没有精确秒数；不能把它写成所有冷启动的固定时间保证。.234 保留 .233 的电量源码字节，仅另修复报告整数时间；纯 BLE 启动核验也已通过，电脑 WLAN 已恢复。此前 .233 的“没注意看”仍保留为当次未观察。证据为 `hardware-boot-fix-234/battery-display-user-acceptance.json`，BLE 见 [对应记录](TAB5-BLE-UPTIME-20261019.md)。

Final new acceptance: on the .234 OTA reboot the user directly observed quick battery-number appearance and normal display across USB plug/unplug. This accepts the newly reported battery behavior without an additional requested reboot or repetition of old tests. No precise display time was measured and the unchanged cold-calibration limits still apply. .234 preserves the .233 battery-source bytes; its separate isolated BLE boot check also passed, and PC WLAN was restored. The earlier unobserved .233 startup stays unobserved. Public release remains unapproved. The following .233 state is historical.

用户已批准并完成 0.2.233-ui OTA，重启后的真实 IPC 确认了当前版本和精确镜像；尚未公开发布。用户补充电量空白主要发生在重启或插拔 USB 后，但没有注意本次升级后数字首次出现的情况，因此启动显示记为未观察，电量尚未验收。Windows 0.6.10 / c608 原程序文件与 400 项编译输入保持不变。纯 BLE 启动核验仍未通过，不能由电量回归替代；其临时诊断切换另有明确授权。

用户报告电量数字间歇消失、随后恢复。生产采样器每两秒读取电压和电流；单次超出 5000–9000mV 范围的转换会立即清空整个电量估算，之后在充电状态下可能再次等待约一分钟的静置校准。原生测试执行真实 `battery_status.c` 与 BSP 充电控制：已有可信 50% 的情况下注入一次 0mV，旧实现立即丢失数字，返回预期失败。此结果证明了可复现的代码路径，尚未唯一确认用户现场异常读数的来源。

本地修复仅改变一个主目录输入 `battery_status.c`：

- 单次异常转换保留十秒有效期内的可信电量，随后快速补采；连续两次读到异常电压才清空旧估算，确认电池移除时不会长期展示旧数字。
- I2C 读取、初始化或恢复充电失败时，最多四次采用 250ms 延迟补采，之后恢复两秒间隔；健康读数到来即恢复正常采样频率，避免持续故障时长期高频轮询。
- 采样任务先读取、后等待，取消原来的启动前两秒等待。页面仍沿用既有每秒指示器刷新，不声称屏幕每 250ms 重绘。
- 短暂读取失败保留可信锚点，不把未观测时间计入充放电。连续十秒没有新读数仍显示未知；恢复后不沿用可能已经更换电池的旧锚点，也不把恢复充电后的零电流误当成冷启动静置电压。

充电电压校准时长、满电判断、90 秒充电暂停上限、失败后的充电恢复和冷却间隔保持原值。冷启动时若没有任何可信电量且设备正在充电，仍需等待真实校准，不能用虚构百分比填充。后台补采和显示刷新有不同的调度开销，250ms 是本地任务的补采延迟，不是实机空白时间保证。

本地生产采样器回归覆盖：单次异常无空白、恢复不重启一分钟校准、确认异常清除、短暂 I2C 丢读、十秒过期、替换电池不继承旧值、四次快速补采上限、恢复正常采样和启动立即读取。原有充放电、充电写入失败/回读失败、无法暂停、恢复重试、90 秒上限和缺失采样时间等断言均保留并通过。另一个电量估算测试通过，避免插 USB 电压上升就误报满电。实际固件编译通过；构建、模拟时间与原生测试不替代实机验收。

候选 BIN SHA-256：`542afd4cb6a705c1a37aa9cbaede6b35fa6688330fe80832d04a47bad95c2e75`；ELF SHA-256：`808ed8f9d60c613cfe3b954e223d6e9e51b99dd680ca3f64c6c26e3cb0475ff4`。223 项主目录输入与 .232 对照仅上述一个文件变化。冻结候选及隔离的固定路径校验位于 `artifacts/development/release-closeout-20261019/battery-display-fix-233-candidate/`；旧失败、修复和本地检查记录在 `battery-display-continuity/`。经用户授权的标准准备已推广全局 latest 为 .233，现有 GUI 选择后完整 BIN/ELF 核对成功，再由用户 OTA；`host-offer-battery-233/ota-info-after-upgrade.json` 确认 installedVersion=0.2.233-ui。固件升级期间未重启或替换 Windows，未改变配对、PIN、展示设置或电脑配置。仅新增电量显示验收，已通过的项目不重测；未观察的启动时间不要求重启补测，先推进蓝牙精确诊断。

English: The user reported an intermittent missing battery percentage, mainly after reboot or USB changes. The production sampler discards its complete estimate after one out-of-range voltage conversion, potentially causing another one-minute charging calibration. An actual-source native test reproduced this path; the unique hardware cause is not yet established. The .233 repair retains a recent trusted percentage across one invalid conversion and confirms absence before clearing it. Missed readings use at most four rapid 250ms retry delays, then return to the original two-second interval. Healthy recovery immediately resumes normal pacing, and startup samples before its first delay. The existing ten-second freshness boundary remains; stale recovery cannot inherit an unverified battery anchor or fabricate a rested zero-current estimate. Calibration duration, full-charge evidence and charger restoration protections are unchanged. Native continuity/recovery and charge-estimation tests passed, as did firmware compilation. The existing indicator still refreshes once per second; 250ms is a retry delay, not a hardware display guarantee. Only battery_status.c changed among 223 main inputs. The user approved the exact image, selected it through the existing GUI and completed OTA; fresh IPC confirms installedVersion=0.2.233-ui and the exact offered BIN/ELF hashes. Global latest is now .233. Windows and its 400 inputs stayed unchanged throughout this upgrade. The user did not observe startup battery display, so it remains unaccepted; no reboot is requested merely to recover that timestamp. Previously passed physical tests stay accepted. BLE boot remains failed and separate, and a temporary diagnostic bridge switch has separate explicit approval. Public release has not occurred.
