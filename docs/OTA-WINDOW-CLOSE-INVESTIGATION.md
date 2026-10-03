# TAB5 USB 升级中断与关窗排查（2026-10-02）

用户决定：本轮定位暂缓，后续再次复现再继续。不继续切换低开销诊断版，不主动安排升级复现。保留已有日志与测试产物；问题未修复、未验收关闭。诊断收尾已完成：用户确认后，23:50:37 +08:00 桌面恢复回执及现场复核确认正式 Release 进程、原 DLL 哈希、原配对、auto/true/六页顺序/15 秒轮播均保持；USB COM9 在线，语音 idle，临时诊断进程已停止。下方为暂停前的调查记录。

当前结论：尚未定位 088 首次 USB 升级中断根因，不能标记修复或验收通过。用户已明确关闭的是“TAB5 连接 / 固件升级”窗口右上角 ×。现有代码和独立测试未发现该窗口关闭直接取消 OTA 服务。随后只读现场捕获到没有 OTA、没有升级窗口时也发生 USB 中断，首次原因为 metrics 确认超时；当前优先诊断独立的 USB 超时，无需先刷固件。不能将这次中断直接归因为 088 的同一根因。

## 新发现：普通运行也出现 USB 超时

原桥接进程持续为 PID 48888、23:19:30 启动。23:22 诊断首次/最近中断均为“无”；随后首次中断为 `23:24:37 USB metrics; TimeoutException / 0x80131505`，后续最近中断更新为 23:27:28、23:29:53 的 `USB RPC disconnected`。`capture-20261002-232943-466.jsonl` 记录整个采集段升级窗口列表为空、固件传输为“尚无升级传输”、同一进程，期间错误时间仍推进；设备运行时间连续增加，没有由这些记录证明设备重启。

`RunUsbMetricsAsync` 等待 `tab5_metrics_ack`；失败后会记录 metrics 错误、关闭共享串口，再由 RPC 循环报告 disconnected。这提供了一条当前实机观察到的中断链路，但缺少超时期间实际字节和关闭原因序列，不能继续猜测为驱动、线材、闪存阻塞或窗口取消。

已准备独立的临时诊断桥接（`diagnostic-bridge/`），原源码及正式运行文件不改。只在独立编译的五份源副本中增加白名单事件：串口关闭调用来源、打开、metrics 字节数、文本响应接收字节/行数/解析错误/残留字节/耗时，以及二进制收取长度和错误；不记录内容、密钥或语音。8 MiB 上限，不改变读取超时、协议、重试或 OTA 逻辑。日志写入仍可能影响时序，观察结论需保留这一边界。

诊断候选构建零警告/错误；RPC、48 KiB 分片/串口文本与二进制超时及取消回归、status-once 均通过，并验证超时测试确实产生零字节/二进制超时记录。资源名称与原程序一致。`diagnostic-manifest.json` 固定原运行 DLL 和候选文件哈希。`start-diagnostic-from-desktop.cmd` 验证原桌面配对/当前空闲/无 OTA，按状态端口选进程并正常退出后，从独立目录启动诊断版；不覆盖原文件。`finish-diagnostic-from-desktop.cmd` 恢复原桥接并核验原配对、自动六页及 15 秒间隔。

诊断桥接已由用户从桌面启动：2026-10-02 23:39:03 +08:00，PID 53712，状态端口属于独立 `diagnostic-bridge/AIBotBridge.exe`，配对匹配、语音 idle、无 OTA，原 auto/true/六页顺序/15 秒保持。事件文件为 `runs/2406a50f218d4f8b9d564e3cdd2e4b56/usb-events.jsonl`；截至 23:48:01 的约 9 分钟内，2138 次 metrics 确认成功，记录最大读取耗时 93 ms，34645 条事件无 timeout/failure，文件未达到上限。摘要保存同目录 `observation-summary.json`。短时未复现不代表修复。

发现初版诊断每条事件都在串口线程上同步写磁盘，会改变时序。另准备 `diagnostic-light-bridge/`：事件只在内存排队，后台每 250 ms 批量写入，队列上限 8192、文件上限 16 MiB，溢出记录丢失标记；读取超时、协议和重试不变。独立反射探针对两版各验证 1000 条串行/并发事件完整无重复；同一工具宿主测量的单次入队/记录中位耗时分别为 0.9205 ms 和 0.0017 ms，P95 为 1.6306 ms 和 0.0054 ms，不扩大为设备性能指标或故障因果证明。低开销版构建零警告/错误，RPC、分片/文本/二进制超时与取消回归、status-once 通过。

下一步由用户从桌面执行 `start-light-diagnostic-from-desktop.cmd`，脚本支持从当前初版诊断桥接正常退出并切换到低开销版；保留原正式文件、配对和轮播。完成采集后运行 `finish-light-diagnostic-from-desktop.cmd` 恢复已验收的原桥接。当前低开销版尚未部署，初版诊断桥接仍运行；需真实现场复现再定位根因。必须从桌面启动以维持当前真实桌面配置，工具进程直接启动曾存在配置视图差异。

## 现场证据和代码路径

- 原现场文件 `artifacts/firmware/tab5/previews/0.2.88-ui/usb-upgrade-interrupted.json`：19:44:54 最近错误是 `read mailbox; IOException / 0x80131620; USB RPC disconnected; auth=verified`；已提供 2408448/6642112 字节。设备仍为 087，用户随后确认“固件下载中断，原版本保持可用”。文件没有保留首次错误、关窗精确时间或串口关闭调用来源。
- `Tab5UsbRpc.UsbRpcPacketAsync` 在 RPC 版本、配对或端口状态无效时主动抛出上述 `USB RPC disconnected`，此分支还没有开始本次串口读写。该错误不能单独证明 Windows USB 驱动出错，更不能证明关窗直接取消 OTA。
- `Tab5ConnectionForm.Dispose` 停止窗口计时器并取消窗口自己的令牌；`OfferOta` 不使用该令牌。后台 USB/RPC worker 由 `DeviceServiceManager` 的任务和设备生命周期令牌管理。窗口 `FormClosed` 只移除窗口登记；它不是应用主窗口，普通关闭不调用桥接退出。
- `RpcOta` 每个有效分片续期 90 秒保护，`OfferOta` 在保护期内拒绝替换候选，这解释了下载中断后重新选择固件时报“设备忙”。不能据此认定服务永久卡死，也不据此缩短保护时间。
- 状态心跳、遥测、二进制读写错误、设备重新枚举等路径都能关闭串口。`RecordUsbFailure` 与 RPC 循环共用“最近中断”字段，后续 `disconnected` 能覆盖最初错误；虽另有“首次中断”，原现场文件未保留。
- 窗口默认在 Shown 时读取 Wi-Fi 列表，即使当前选中升级页；关闭窗口可能取消尚未完成的查询。这是需要实机证据的候选路径，不能把未包含该查询的测试结果扩大为全部关闭场景均安全。
- 检索当日 19:43:30–19:46:30 Application/System 日志无匹配事件。无日志不等于没有设备/驱动问题。

## 已执行的隔离测试

`artifacts/acceptance/tab5/ota-window-close/Probe.csproj` 直接加载正在部署的桥接 DLL（SHA-256 `71CEA30DD4697281796831C77D60826B81981471AA0B0B8141D7D84383963E25`），使用隔离测试配置和虚拟配对，无 USB worker、无设备写入。实际创建 WinForms 升级窗口，调用 Close/Dispose，走正式 HMAC/AES-GCM、RPC OTA 和 T5R2 分片响应，并逐段核对实际 089 镜像字节。

四组结果均通过：不关窗、约 1%、36%、90% 时关窗；每组完整 6644480 字节、136 个分片。36% 关闭点为 2408448 字节，与原故障提供偏移一致。关窗未取消服务、未撤回镜像，后续分片完整。另验证忙状态拒绝替换、观察到匹配版本后解除租约、显式 CancelOta 后返回 404，避免测试仅验证成功路径。

结果保存在同目录 `result.json`。探针构建零警告/错误；没有修改、重启或替换正式桥接，也没有刷写 TAB5。测试刻意关闭 Wi-Fi 列表加载以避免连接硬件，因此不覆盖窗口查询取消、真实 USB 调度/驱动或设备 flash 行为，也未复现完整托盘上下文。

## 后续 OTA 实机复现（普通 USB 超时排查之后）

已准备 `artifacts/acceptance/tab5/ota-window-close/capture-from-desktop.cmd`，从桌面运行后只读采集 5 分钟：状态端口所属正式进程、升级窗口存在/消失、USB 首次和最近错误、固件提供偏移、版本及运行时间。只保存白名单诊断字段，不保存配对密钥、语音内容或任务正文。关闭窗口时刻为轮询观察边界，不是系统事件精确时间。

须在下一次已授权、确实会写入的新镜像 USB 升级中复现：先采集，确认进度推进后关闭指定窗口，保持供电/USB；若中断先保留现场，不重启桥接，不反复选择固件。对照窗口消失前后的首次错误、进程变化和传输偏移，再决定修复路径。当前 089 已运行，同镜像可能直接判定已是当前版本，不能用该流程冒充实际 OTA；本次未准备或刷入仅改版本号的固件。

## English

The original USB interruption remains unresolved. The user identified the TAB5 connection/firmware window. Four isolated tests against the deployed bridge assembly passed complete authenticated OTA range reads, including real WinForms closure at 1%, 36%, and 90%. They exclude physical USB, flash writes, pending Wi-Fi queries, and the full tray context. Later live capture found metrics acknowledgment timeouts during ordinary operation, with no OTA or upgrade window and no bridge restart. The first diagnostic build was deployed from the desktop and recorded 2138 successful metrics acknowledgments over approximately nine minutes with no timeout. Its synchronous file writes affect timing. A buffered diagnostic variant passed build, trace integrity, RPC/transfer regression and status checks and awaits desktop activation. The first diagnostic build remains active; restoration of the original accepted bridge remains required after capture. No production files or firmware were modified, and no root-cause fix is claimed.
