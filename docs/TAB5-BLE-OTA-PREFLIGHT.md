# 蓝牙 OTA 优化与短时预检 / BLE OTA optimization and preflight

## 目标与当前边界

用户确认约 6.86 MB 固件的蓝牙升级总耗时目标为 **120 秒以内**。先优化，再做短时 RAM 预检；估算超标、超时或任何一轮失败时，不安排整包蓝牙刷机验收。预检通过仍不等于整包升级通过，最终需蓝牙传输、写入、校验、重启及精确镜像启动核验。

0.2.111-ui 配套桥接增加 Windows 11 临时 `ThroughputOptimized` 连接参数申请，仅实际 BLE RPC 大传输期间生效，结束恢复 Balanced、断连释放请求；Windows 10 保持旧行为。SDK 引用更新至 22000.56，目标系统和输出目录仍为 Windows 10 19041，使用前运行时检查。申请状态与实际协商/速度分开记录，系统或控制器可以拒绝。首个大下载回复现在也进入大传输优先调度。保留既有 48 KiB 加密二进制范围请求、八包发送窗口、顺序发送、最终确认与重试边界，没有增加未验收的窗口大小。

依据：[Microsoft 的固件升级连接参数建议](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothledevice.requestpreferredconnectionparameters)。此申请只作用于当前连接，不修改全局蓝牙配置。实际提速幅度待真机预检。

## 预检

### .116 真机预检已通过，.117 待完整升级验收

.116 已安装并核验精确镜像。140 段完整 RAM 预检接收 6,867,152 字节，SHA-256 一致，耗时 61.275 秒；含加密回复 3,615,162 字节，Wi-Fi 恢复确认、USB 控制无中断。既定估算为 99 秒，不能当作 Flash 升级耗时。.117 三通道压缩、蓝牙入口与内部证据见 [候选验证记录](TAB5-OTA-COMPRESSION-117.md)。下文保留历次验证状态。

The installed .116 passes the exact full-image BLE RAM hash preflight in 61.275 seconds, with confirmed Wi-Fi restoration and no USB control interruption. Its 99-second estimate is not full OTA timing. See the .117 candidate record for all-transport compression and pending hardware installation.

### .116 后续：内部验证通过并准备完整固件预检

.116 本地候选与配套桥接已准备，尚未部署。新增完整镜像 RAM 接收、生产解压和 SHA-256 校验，精确 .116 镜像载荷减少 47.59%；末段损坏/取消/会话变化、超时与恢复失败测试通过。设备传输预算 110 秒，桥接仍按实测接收时间乘 1.2 加 25 秒筛选 120 秒目标；不能用旧三轮原始数据估算代替该结果。先 USB/Wi-Fi 安装，再一次完整固件 RAM 预检；详细证据见 [内部验证与候选](TAB5-BLE-COMPRESSION.md)。

Local .116 and paired bridge are prepared but not deployed. Full-image RAM preflight exercises the real decoder and SHA-256; the exact candidate saves 47.59% payload. Fault regressions pass, while hardware acceptance remains pending. Install over USB/Wi-Fi before the full-image preflight; retain the 120-second full-upgrade gate.

### 初轮：先完成内部整链路验证

2026-10-05 用户要求先完成内部测试，再统一真机验证；停止逐个参数候选反复试机。新增 BLE 固件独立分块压缩的电脑/设备实现，真实 .115 镜像认证传输与原生边界测试通过，载荷减少 47.57%。本轮未部署、未刷机，未替换 latest；原始 RAM 预检仍不能代表压缩固件升级。实现与完整证据见 [内部验证](TAB5-BLE-COMPRESSION.md)。

### 已撤回候选：空闲语音轮询在锁后让行

2026-10-05 后续：候选三轮完整下载耗时 4823/3581/3972ms，共 12376ms，无完整性错误；因未选择固件，预检不产生有效整包结论。录音结束异常后已回退到已核验的 `bridge-window64-default`，保留 64/7。回退后仍一度复现，用户随后确认恢复，不能确认候选为根因。以下保留实验设计与先前状态，当前不再部署该候选。停止后重复“可以说话了”已定位为固件被旧回复覆盖，.115 修正待真机验收。

64 片复测记录中，大 RPC 回复累计 11862ms、排队 1390ms，语音轮询 22 次、计时 4719ms；后者包含等待共享锁，不能全部当作可回收的开销。原代码只在轮询后根据大传输状态延长间隔，已经排队的空闲轮询没有锁后复核。

候选 `bridge-idle-poll` 仅更新电脑端：首次读取语音邮箱取得共享锁后重新检查大传输优先状态。没有最近一秒的语音活动、且距上次真实读取不足一秒时，释放锁并延后本次读取；首次探测、距上次真实读取达到一秒、或已有最近语音活动时仍读取。普通空闲轮询和活跃语音的原有延时间隔不变，正在处理的请求分片不走让行路径。这里的一秒是允许再次探测的门槛，不是保证一秒内响应，实际仍受轮询调度、锁排队和 GATT 完成影响。

保留窗口 64、原生并发 7、心跳刷新、认证/顺序/完整性和最终确认；延期不伪造设备空回复、不改变完成请求计数，也不允许重放。通知错误及未消费通知仍拒绝。诊断新增可选 `voiceDeferredCount`，仅计入真正延期的初始轮询；实际 `voicePollCount` 不包含这些延期。本地构建及锁后复核、定期探测、活跃语音、取消/异常释放、分片路径、重复请求和通知错误测试通过。固件仍为 .114，暂无新候选真机速度结果。

准备期间旧运行桥接曾于 14:48:22 出现普通状态传输超时（2440/7190 字节，尚无大 RPC）；14:48:59 已自动恢复蓝牙数据确认、USB/Wi-Fi 正常，显示错误 0。证据在 `idle-poll/baseline-readback.json`；不能归因于尚未部署的新候选，也不能将一次恢复称为长期稳定。部署后先确认连接正常，再保持 USB、仅蓝牙、勾选暂停 Wi-Fi，运行一次「蓝牙升级预检」，检查真实读取/延期次数、三轮完整性及速度；本轮不需要窗口对照或刷固件。120 秒门槛不变。

### .114 同版复测：采用 64 片窗口，仍未达到两分钟

2026-10-05 14:04 读回第二次同进程 32/64/32 对照，六个运行文件仍匹配 `bridge-window-comparison`，固件仍为已核验的 .114；本轮没有重新部署或刷机。

| 确认窗口 | 三轮耗时（ms） | 合计（s） | 整包保守估算（s） |
| --- | --- | --- | --- |
| 32（前） | 5839 / 4440 / 5700 | 15.979 | 208.46 |
| 64 | 3885 / 4415 / 4682 | 12.982 | 172.10 |
| 32（后） | 5165 / 4374 / 5326 | 14.865 | 192.34 |

九轮再次全部通过，64 比最后一组 32 缩短 **12.67%**，比两组 32 的平均耗时缩短 15.82%；两组 32 自身相差 6.97%。两次对照中，64 都快于前后两组 32，六轮 64 均完整，因此采用 64 作为大回复默认上限，保留原生并发 7。数据仍有波动，不能将两次短测称为长期稳定性、绝对硬件上限或完整 OTA 验收；也不再要求重复相同的 ABA 实验。

每组主机与设备均完整覆盖 18 RPC / 789381 字节 / 1650 片，零错误、拒收和诊断丢弃；实际窗口 32/64/32、并发 7、链路 15ms / 2M / DLE251 均确认。确认次数仍是 66/33/66，等待分别为 7401/8082/7347ms，64 的确认等待本次反而更长：减少确认次数不是等比例消除等待，计时也不可相加。Wi-Fi 三次暂停/恢复成功、未过期，读回后已联网；显示错误 0。桥接保留的 13:55:55 取消记录未更新，不能记为本轮故障。原自动轮播、六页顺序和 15 秒间隔保持。

仅电脑桥接改为 `min(peerOffer,64)`，旧设备依能力回退至 32/8，小回复与语音保留原限制。显式窗口对照仍为 32/64/32，原生排队对照仍固定窗口 32，所有退出恢复默认 64/并发 7。`bridge-window64-default` 已于 14:09 通过原资源管理器快捷方式部署，14:11 核对六个文件、18765 监听进程及启动时间匹配；蓝牙数据已确认、Wi-Fi 联网、显示错误 0，原轮播设置恢复。当前进程还没有大 RPC 或新的启动核验，沿用前述 .114 精确启动及两轮 RAM 证据，不将本次部署记作新的大传输验收。部署证据在 `window64-default/deployment-verified.json`，候选准备快照保留原样。无需新固件、无需再次窗口对照。原始与计算证据分别在 `window-comparison/hardware-repeat.json`、`hardware-repeat-analysis.json`，原记录保留。

64 的两轮保守整包估算分别为 165/172 秒，仍超过 120 秒目标，暂不安排完整蓝牙刷机。压缩保持为后续已确定工作，原始链路当前仍有耗时差距，不能宣布已达到理想状态。

### .114 窗口对照真机结果：确认减半，稳定提速尚未证实

2026-10-05 13:57 读回确认 .114 精确 ELF 为 `bc30895c5115f3d305389194bdf8eea72ca8b6c4c3c1386fe64566f1213c501e`，`ota_0` 为 VALID，完整 6863568 字节、阶段 9、错误 0；六个已部署文件匹配 `bridge-window-comparison`。本次安装走 Wi-Fi（transport=1），电脑发送 13625ms、设备安装 19299ms，不是 BLE OTA。

| 确认窗口 | 三轮耗时（ms） | 合计（s） | 整包保守估算（s） | ATT 确认次数 |
| --- | --- | --- | --- | --- |
| 32（前） | 5720 / 4510 / 4622 | 14.852 | 204.72 | 66 |
| 64 | 4417 / 4465 / 3781 | 12.663 | 165.29 | 33 |
| 32（后） | 4208 / 4458 / 4440 | 13.106 | 165.07 | 66 |

九轮完整性全部通过，每组主机和设备均为 18 RPC / 789381 字节 / 1650 片，零失败、拒收或诊断丢弃；实际 `gatt.WriteWindow` 为 32/64/32，原生并发始终 7，结束恢复 32。64 比前后平均值快 9.41%，但比最后一组 32 仅快 **3.38%**；前后原参数本身快了 11.76%。首组第一个 RPC 读取 484ms，另两组为 62/31ms，首组存在额外延迟，原因尚未确认。不能把全部前后差异归因于窗口变化。64 与最后 32 的最慢轮分别为 4465/4458ms，保守整包估算都约 165 秒，**120 秒目标未达到**。

确认数确实减半，但确认等待仅由最后 32 的 8214ms 降为 7484ms（首组为 8044ms），等待仍包含无线排空，不能按确认次数直接外推提速。实际链路均为 15ms / 2M / DLE251；三次 Wi-Fi 暂停和恢复均成功、未过期，之后已联网且数据新鲜。新启动后的设备连接次数 1、断开原因 0、显示错误 0；桥接保留 13:55:55 的取消记录，三组 trace 均无失败，不将该历史记录直接认定为本轮中断。原自动轮播、六页顺序及 15 秒间隔保留。

原始数据与计算保存在 `window-comparison/hardware-result.json` 和 `hardware-result-analysis.json`，候选准备快照保留原样。暂不改变默认参数；保持当前固件和桥接，不再刷机或重启，再运行一次同条件窗口对照，以检查这一小幅差异能否重复。未安排整包 BLE OTA，压缩仍是后续已确定工作。

### 7/31/7 真机结果：扩大排队无收益，保留七个

2026-10-05 13:39 读回：六个运行文件与 `bridge-queue-comparison` 一致，三组各三轮 256 KiB 全部通过，实际 trace 排队上限分别确认为 7/31/7，结束已恢复 7。

| 排队上限 | 三轮耗时（ms） | 合计（s） | 整包保守估算（s） |
| --- | --- | --- | --- |
| 7（前） | 4631 / 3868 / 4159 | 12.658 | 170.50 |
| 31 | 5091 / 4476 / 4980 | 14.547 | 184.95 |
| 7（后） | 5059 / 4140 / 4260 | 13.459 | 183.95 |

31 比前后原参数平均耗时慢 **11.40%**、吞吐低 10.23%，且慢于两组原参数，未观察到收益。两组 7 本身相差 6.33%，仍有波动；本结果不证明绝对硬件上限。三组估算均超过 120 秒，不安排完整 BLE OTA。每组设备和主机均完整覆盖 789381 字节 / 1650 片 / 18 RPC，零失败、拒收或诊断丢弃；各组 66 次确认，等待分别 8940 / 10416 / 9949ms，占回复 76% / 77% / 80%，含无线排空且不可与其他计时相加。链路均为 15ms / 2M / DLE251。Wi-Fi 三次暂停/恢复成功且无过期，之后联网及数据新鲜已确认。设备保留的计数 4/原因 531 无事件时间；主机本次无中断。原自动轮播、六页顺序和 15 秒间隔保持。当前桥接未重新启动核验，本次未刷机。原始及计算记录在 `queue-comparison/hardware-result.json` 和 `hardware-result-analysis.json`。

下一候选 .114 只增加可选 64 片回写能力，配套桥接默认仍为 32；「蓝牙窗口对照」自动跑 **32 → 64 → 32**，原生并发固定 7，退出恢复 32。这样改变确认频率而保持原生排队不变；约 48 KiB 回复的确认由四次降为两次，但等待包含真实传输，不能承诺节省一半时间。接收缓冲、通知授予、射频、Flash、认证和最终完整性检查均不变。先用 USB/Wi-Fi 安装并核验 .114，再保留 USB、选择仅蓝牙、勾选暂停 Wi-Fi，点新窗口对照按钮。实际收益和两分钟整包目标均待验收，压缩仍为后续已确定功能。

### 恢复版真机结果与一次点击的排队对照

2026-10-05 13:13 读回确认六个运行文件与原 `bridge-radio-isolation` 完全相同。恢复分组发送后，暂停 Wi-Fi 的三轮 262144 字节全部通过：6343 / 6156 / 5056ms，合计 **17555ms**，40.36 / 41.59 / 50.63 KiB/s，整包保守估算 **224 秒**。比滚动版 19213ms 缩短 8.63%，但同一原发送方式的首次结果是 13997ms，本次慢 25.42%。已有明显波动，不能把前后差异全部归因于发送代码，也不能宣称达到硬件上限。

两端完整覆盖 18 RPC / 789381 字节 / 1650 片，零失败、拒收或诊断丢弃。实际 15ms / 双向 2M / DLE251；接收处理 53.128ms，占接收跨度 0.31%。回复 16625ms，其中 ACK 等待 13642ms，含无线排空；块间间隙仅 62ms，各项计时不可相加。Wi-Fi 暂停 17582ms，stop/start 成功、restored=1、未过期，之后已联网且数据新鲜。设备保留的连接计数 3 / 原因 531 没有事件时间，不能认定发生于测速期间。当前桥接未重新做启动核验；原 .113 精确核验证据仍为历史证据，本轮未刷机。原自动轮播、六页顺序及 15 秒间隔保留。证据在 `cohort-recheck/hardware-result.json`、`hardware-result-analysis.json`、`hardware-comparison.json`。

下一候选 `bridge-queue-comparison` 只改电脑桥接，新增「蓝牙排队对照」：同一进程连续运行 **7 → 31 → 7** 原生待完成写入上限，每组仍是三轮 256 KiB RAM 下载，统一勾选暂停 Wi-Fi。保留按组等待的发送方式、32 片线上窗口及最终 ATT 确认；不重新启用已撤回的滚动补位。默认值始终为 7，31 只在第二组临时使用。每组先确认完整三轮和 Wi-Fi 驱动恢复再继续；失败或取消停止后续组，恢复 7 并保留已完成结果，不自动选择或持久化性能参数。

三组对照整体取消期限 120 秒，每组原有 40 秒主机期限、20 秒传输预算及最多 5 秒当前请求等待保持；退出清理最多另等 4 秒停止确认，失联不保证立即确认。诊断 `蓝牙排队对照` 在内存保存三组独立结果、射频/隔离计数、主机 trace 和整包估算；已提交的原生操作仍按原规则排空。`gatt.NativeLimit` 是当次 RPC 使用的最大配置上限，不是实际峰值并发或空中包数。首个 RPC 可能因较早开始轮询缺少 trace 票据，分析仍须核对双方字节覆盖；设备完整性和耗时不依赖主机 trace 全覆盖。

本地 Release、7/31 排队故障及最终确认、ABA 顺序/独立快照/失败与取消恢复/并发拒绝、BLE 传输、升级校验、status-once 和常规/最小窗口检查通过。新参数实际收益待真机对照。使用原 Explorer 快捷方式更新桥接即可，无需重刷 .113；在「传输测速」点新按钮运行三组。120 秒整包目标、未达标不安排整包 BLE OTA，以及后续压缩计划不变。

### 滚动队列真机无提速，恢复分组发送复查

2026-10-05 12:52 读回确认六个运行文件匹配 `bridge-rolling`，仍使用 .113、暂停 Wi-Fi。三轮 262144 字节全部完整且错误为零：6267 / 6219 / 6727ms（40.85 / 41.16 / 38.06 KiB/s），总计 **19213ms**。相比隔离模式原分组发送的 13997ms，耗时增加 37.27%、吞吐下降 27.15%；整包保守估算为 **236 秒**，超过 120 秒。本次没有观察到滚动队列收益，撤回该候选并恢复原分组发送；无线环境随时间变化，仍需回到原方式复测，不能只凭一次前后比较断言全部差异的因果。

两端记录均为 789381 字节、1650 接收片、18 个 RPC，零拒收/失败/诊断丢弃，完整覆盖。Wi-Fi stop/start 均成功，暂停 19267ms、恢复标志 1、未过期；之后设备 Wi-Fi 已连接且数据新鲜。双向 DLE251、15ms/2M 保持。接收处理仅 54.375ms（接收跨度的 0.29%），块间间隙从 187ms 减至 94ms，但 RPC 回复从 12810ms 增至 18204ms、ATT 等待从 9607ms 增至 15125ms。等待包含无线排空、计时重叠；不能把它当成可全部删除的开销。桥接本次没有中断记录；设备保留的连接计数 2/原因 531 没有发生时间，不能据此认定测试中断连。

新桥接进程的启动核验显示“尚未核验”；本轮没有刷固件，.113 的精确启动证据保留于上一轮，不冒充新核验。原自动轮播、六页顺序及 15 秒间隔均正常。原始记录 `rolling/hardware-result.json`、计算 `hardware-comparison.json` 均保存。只恢复上轮修改的发送函数及对应测试，强制非增量重建后 DLL 与原 `bridge-radio-isolation` 完全相同，验证部署载荷没有残留滚动实现。`bridge-cohort-recheck` 已通过 Release、BLE 队列/传输/故障、升级及 status-once 检查；用户通过原快捷方式更新桥接即可，无需再刷 .113。保持勾选暂停 Wi-Fi，复测期间不再调整其他性能参数，压缩计划不变。

### .113 同固件对照结果与电脑端滚动队列候选

2026-10-05 暂停 Wi-Fi 的三轮各 262144 字节全部通过：5626 / 4076 / 4295ms（45.50 / 62.81 / 59.60 KiB/s），总计 13997ms。取消勾选、保持 Wi-Fi 的同固件三轮也全部通过：8108 / 5949 / 4896ms（31.57 / 43.03 / 52.29 KiB/s），总计 18953ms。本次暂停 Wi-Fi 的总时间短 26.15%、平均吞吐高 35.41%；最大接收间隙从 541.543ms 降至 175.750ms。这是一组短时对照，支持优先继续此方向，尚不能证明长期增益或达到硬件上限。整包保守估算分别为 **202 秒 / 280 秒**，均未达到 120 秒。

隔离轮 Wi-Fi stop/start 均成功，暂停 14059ms、restored=1、未超时；后续健康状态确认已重新联网且收到新数据。两轮均为 1650 片、789381 字节、零拒收，双向 DLE=251，实际连接间隔 15ms、电脑捕获的链路均为双向 2M。接收处理分别仅 46.914/48.951ms，占接收跨度 0.35%/0.26%，这一处理段不是主要瓶颈。隔离轮完整捕获 18 个 RPC，回复 12810ms，其中 ATT 确认等待 9607ms（约 75%，含无线排空），块间间隙 187ms。常规轮电脑记录只有 17 个 RPC、740069 字节，少了首个 49312 字节回复，代码允许在预检开始前已进入轮询的请求没有采样票据；设备三轮及字节检查完整，仍可比较设备耗时，但不能直接把两轮电脑部分统计当成完整同口径数据。常规轮 `wifiIsolation` 保留前次记录，不代表本轮也停用了 Wi-Fi。

六个运行文件匹配 `bridge-radio-isolation`；精确 .113 ELF、ota_1 VALID、完整 6863568 字节和 previousStage=9/error=0 已核验。安装为 Wi-Fi（transport=1），电脑发送 15.656 秒、设备安装 19.353 秒，不能记为 BLE 整包验收。原自动轮播、六页顺序及 15 秒间隔保持。原始读回分别为 `radio-isolation/hardware-isolated.json`、`hardware-control.json`，计算见 `hardware-comparison.json`；原候选准备证据不覆盖。12:17:01 取消记录早于这两轮，设备 BLE 连接计数仍为 1、断开原因 0。

下一候选只更新电脑端 `bridge-rolling`，复用已安装 .113，不再刷固件。每个 32 片确认窗口内仍最多七个原生写入，但任一完成释放位置后就按游标顺序补入下一片，不再等待整组。继续提交前检查所有已完成任务；任何已观察到的异常、取消或同步提交失败都停止补入，等待全部已提交任务结束后原样抛出错误；窗口末尾确认只在全部成功后发送。现有语音/小消息/旧设备路径不变，完整性校验与无线参数不变。本地测试明确阻塞最早六个操作，验证其余单个位置可持续补入，同时覆盖乱序完成、迟发故障、立即失败、取消、最老操作排空及最终确认。实际提速仍待同样暂停 Wi-Fi 的短测，压缩保持为后续确定工作。

### .113 候选：临时暂停 Wi-Fi 的对照预检

仅蓝牙模式此前仍保持 Wi-Fi 关联和后台轮询；其对当前吞吐的影响尚未测量。.113 增加 `ble_ota_probe_isolated`，电脑「传输测速」默认勾选“预检时暂停 Wi-Fi，结束后恢复”；取消勾选仍走原 `ble_ota_probe`，便于在同一固件上对照。32 片确认窗口、最多七个原生写入和三轮各 256 KiB 均不变。压缩是用户已确定的后续功能，先完成原始蓝牙链路优化；本版没有实现压缩，也不宣称链路达到理想状态。

临时停用/重启 Wi-Fi 由原 Wi-Fi 任务执行，要求仅蓝牙模式且 USB 仍连接。准备和恢复各最多等待 6 秒，传输仍为 20 秒总预算加最多 5 秒当前请求等待；电脑总等待上限 40 秒。Wi-Fi 暂停请求自申请起最多 35 秒，完成、取消、USB/模式变化或期限到达后恢复，不清除已保存网络，不复位无线协处理器。重启失败每秒重试并阻止新预检，超过本轮恢复等待则报告失败；不能保证故障中的驱动调用一定按时返回。成功完成表示 Wi-Fi 驱动重启已确认，**不等于已经重新关联路由器**，需查看真实连接恢复。

新增 USB 数值诊断 `bleRadio` 和 `wifiIsolation`，不扩大 BLE 身份信息。连接参数为 USB 读取当时的值，可能在传输结束后变化；包数、字节、处理耗时和间隙只在预检采样期间累积。接收处理耗时包含首次参数申请和锁等待，并非纯 CPU 用时，且不包含前面的缓冲复制。零 DLE 字段表示尚未观察到实际事件，不能据此认定包长为零。阶段、字段顺序和单位见 [协议](PROTOCOL.md)。

本地验证覆盖能力兼容、数值边界、暂停/恢复、取消时序、USB/模式丢失、35 秒失效、驱动错误与重试、实际测速控制流程及接收计数溢出；Release/固件构建、界面和 ELF 防护检查通过。候选准备时尚未安装或真机验证 .113，后续结果见上节。通过 USB/Wi-Fi 安装并核验后，保留 USB、选择仅蓝牙，先测勾选暂停的预检；读回完整结果和恢复状态后，再以取消勾选的一轮对照。三轮失败、估算超过 120 秒均不安排完整 BLE OTA。

### .112 连续调度真机结果：三轮完整，仅小幅变化

2026-10-05 11:23 的真实预检为 `complete`，三轮各 262144 字节、错误码均为零：5816 / 4837 / 5977ms，对应 44.02 / 52.93 / 42.83 KiB/s。总计 16630ms，较上一轮 17024ms 缩短 2.31%，平均吞吐增加 2.37%。前两轮变快、第三轮变慢；一次短测不能证明稳定提速。按最慢轮次计算，整包保守估算为 212.69 秒，显示 **213 秒**，仍未达到 120 秒目标，不安排整包 BLE OTA。

六个已部署文件全部匹配 `bridge-continuous`；18 个实际 RPC 全部记录、无丢弃样本、无失败或蓝牙中断，链路始终为吞吐优先申请成功、实际 15ms 间隔及双向 2M PHY。请求处理合计 16ms；回复合计 15143ms，其中 66 次 ACK 累计等待 12062ms（约 79.7%，包含无线排空，不能视为可全部删除的开销）。块间空隙仅 172ms，占从首个 RPC 开始到末个结束约 1.04%；共享锁排队累计 1487ms。仍有两次状态刷新及 27 次语音轮询，其计时包含排队，不能与上述值相加或等同于全部可回收时间。结论是继续只压缩块间空隙的空间很小，不能据此宣称已达到无线硬件上限。

TAB5 仍为已核验的 .112，此前镜像安装为 Wi-Fi，未执行新的刷机。自动轮播、原六页顺序和 15 秒间隔均已确认。原始数值和派生计算保存在 `continuous/hardware-preflight.json`、`continuous/hardware-analysis.json`；候选准备快照保留原样。

### .112 电脑端连续调度实现

本轮只更新桥接 `bridge-continuous`，沿用已经安装并核验的 .112，无需刷机。成功的大 RPC 回复完成后保留一秒优先状态，跨越连续块之间的短空隙；空轮询、重复请求和普通小回复不会续期。下一次真实大回复可以续期；处理或回写失败、取消会释放，断连销毁连接。状态刷新在取得共享发送锁后再检查一次优先状态，避免排队期间开始的大传输仍被已排队的状态刷新插入。仍保留六秒刷新期限，正在录音时的轮询间隔保持 15ms；未改变 32 片回写窗口、七个原生操作上限、确认方式或协商参数。

仅显式启动「蓝牙升级预检」时，在内存记录整轮数值；诊断新增 `蓝牙预检分段` JSON。每轮保留最多 32 个真实完成的 RPC 样本，额外样本仍计入总数并标记 `droppedSamples`；最多在开始后 35 秒内接纳新的采样票据，结束后只接收已经开始的操作结果。每次运行有独立编号，旧运行的迟到结果不能混入下一轮。记录不含请求标识、正文、配对资料或语音文字，也不自动写磁盘。

`controlMs` 从电脑开始查询能力到读取结果或取消完成，包含 USB 控制等待，与设备的三轮数据耗时不同。样本 `startMs/endMs` 相对预检开始；其余耗时为毫秒，字节数为实际加密 RPC 请求/回复大小。块间空隙、读请求、处理、回写、共享锁排队、命令/ACK 等待和后台状态/语音轮询分别记录；这些数字有包含或重叠关系，**不能全部相加**。特别是排队已包含在请求/回复时间内，命令累计等待可并发重叠，ACK 还包含之前数据的无线排空。设备结束检查可能早于电脑最后一次 ATT 完成，最后样本可以晚于 `controlMs`。

Release 构建、连续调度/失效与取消/锁后复核/状态和语音刷新/有界诊断生命周期测试，以及既有 BLE 分片、窗口、故障与升级检查均通过。候选准备时以此前 40.89–50.20 KiB/s、保守估算 222 秒作为比较基线；实际短测结果见上节，120 秒目标和整包升级边界不变。

### .112 真机结果：三轮完整，耗时目标未达标

2026-10-05 用户反馈测试似乎成功，现场读回确认为 `complete`：三轮各 262144 字节，耗时 **6260 / 5100 / 5664 ms**，错误码均为 0，总有效数据 768 KiB、总计 17024 ms。对应速度 **40.89 / 50.20 / 45.20 KiB/s**。相比上一轮八片排队版的前两轮，本次对应吞吐分别提高约 39.39% / 37.75%；这是两次短测对比，不保证所有环境或持续整包传输都有相同收益。

已核对六个运行文件匹配 `bridge-window32`，实际 RPC 显示 `write8/bulk32`。精确 .112 ELF、ota_0 VALID、完整 6859936 字节、previousStage=9/error=0 均已确认；此次安装使用 Wi-Fi（transport=1），电脑发送 10.250 秒、设备安装 18.939 秒（不含完成停留与重启），不能记为蓝牙 OTA。

按原最慢轮次、20% 传输余量和 25 秒保留预算，整包估算 **221.58 秒，显示 222 秒**，仍超过用户的 120 秒目标。当前验收范围仅是这一次完整 BLE RAM 下载和观察到的提速，尚无完整蓝牙刷机结果；不放宽门槛或安排整包蓝牙刷机。

最后 RPC 为 16549 字节尾块，2 次 ACK 共等待 499ms、回复 593ms；不能直接与上轮 49317 字节、13 次 ACK 的快照相比。ACK 等待还包含前序排队数据的无线排空，不应把整个 ACK 耗时当成可删除的软件开销。保留的 10:54:18 中断记录早于 10:55:09 最后 RPC，仅凭这条旧记录不能判定完整预检中发生断连。证据 `artifacts/development/tab5-ble-ota/window32/hardware-112-complete.json`；原自动轮播、六页顺序及 15 秒间隔均保持。

### .111 排队发送复测与 .112 独立回写窗口

2026-10-05 已核对六个运行文件均匹配 `bridge-queued`，真实预检仍失败：两轮完整 262144 字节分别耗时 8726/7025 ms（29.34/36.44 KiB/s），第三轮 196608 字节、5298 ms、错误 -2，总计 21049 ms。没有蓝牙中断记录；这次没有有效提速，也未达到两分钟目标。

最后一段 RPC 的真实诊断为 `queuedBatches=13`、`commandCount=90`、`ackCount=13`、`ackWaitSumMs=907`、`batchWallMs=1062`、`response=1125ms`。吞吐优先申请成功，实际连接间隔 15000 us、latency=0、双向 PHY=2M。这里是单段快照而非全程平均；907 ms 约占该段回复时间 81%，说明应继续减少分批 ATT 确认等待，不能称无线硬件已经达到上限。

.112 新增可选身份字段 `rpcWriteWindow=32`，只协商电脑到设备的大回复确认间隔，不改变设备到电脑的 `rpcNotifyWindow=32` 节流或语音 `mailboxWindow=8`。新桥接遇到缺失、类型错误或超出 8..32 的字段时沿用八片窗口；旧桥接忽略新字段。仅超过 12288 字节的 RPC 回复使用新能力，小消息/语音/只支持有响应写入的设备仍走旧路。

每个 32 片窗口先按顺序提交最多七个原生无响应写入，全部完成才提交下一组；全部命令成功后发送窗口最后一片的 ATT 确认。最多七个原生操作同时挂起、窗口末尾确认后才释放共享门锁；任何批次错误/取消都先排空已提交任务且不继续窗口。49317 字节回复、MTU 517 时由 13 次确认降至 4 次，不取消整包认证和设备游标校验。Windows 原生完成不等于无线送达，因此真实接收完整性和内存余量仍须预检证明，不根据本地测试承诺提速。

固件接收回调直接复制到既有 65535 字节有界缓冲，未扩大内部内存池、改动 Flash 或分区，也不新增通知突发。历史 .073 的 32 片失败发生在设备向电脑发通知，与当前接收方向分开记录。SDK 的访问缓冲在回调后由协议栈释放，参见 [NimBLE GATT server reference](https://mynewt.apache.org/latest/network/ble_hs/ble_gatts.html)；这不构成实际吞吐保证。生产身份回调的最大字段组合为 **512 字节**，恰好达到 ATT 上限，后续不可再直接增加字段。

桥接扩展窗口与兼容/取消/中途提交失败测试、实际固件 RPC 核心的 MTU/丢片拒绝测试、身份边界及固件构建防护检查已通过。候选准备时尚无 .112 真机结果；后续完整短测见上节。20 秒预算、三轮完整数据及 120 秒估算门槛不变，完整 BLE OTA 仍未验收。

### 2026-10-05 结果与电脑端第二轮候选

.111 的真实短时预检完成两轮：256 KiB / 8.027 秒、256 KiB / 6.628 秒，即 31.89、38.62 KiB/s；第三轮在 196608 字节处达到 20 秒总预算，以 `-2` 退出。两轮数据检查通过，不是 Flash 写入失败。三轮未完成，不能报预检通过；按两轮速率参考外推，整包仅传输约 173–210 秒，120 秒目标未达到。

电脑端第二轮候选保持设备 .111 和线上八包窗口不变，只对 RPC 大回复启用有界提交：按游标顺序提交最多七个 WriteWithoutResponse，等待所有已提交操作结束，再发送最终 WriteWithResponse；任何提交、完成或取消错误均不发送成功确认，并先等待已提交操作结束再释放 GATT 门锁。语音、小回复及不支持无响应写入的设备保留原方式。不增加重试或自动重放用户动作。操作提交顺序不等于已证明所有适配器都按同样顺序送达，设备现有偏移检查和整包校验仍负责拒绝丢片/乱序；真实字节完整性与速度需短时预检确认。

新增每次 RPC 的原生读取耗时、命令/确认数及累计等待、批次墙钟时间、排队批次数，以及传输完成当时的实际连接间隔、延迟和双向 PHY。重读同一已完成请求不会覆盖这份快照。命令累计等待在并发时重叠，不能与批次墙钟相加。预检达到预算时直接提示已完成轮数及原因，不把未完成结果包装成有效估算。

本轮无需刷固件，更新配套桥接后复用 .111 的只下载预检。保持 20 秒预算、完整三轮要求和 120 秒估算目标不变。故障测试通过仅证明本地逻辑，不能声称已取得硬件提速。

实际连接参数单位与 PHY 读取依据：[Microsoft connection parameters](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothleconnectionparameters)、[connection PHY](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothleconnectionphy)。

- 先通过 USB 或 Wi-Fi 安装 0.2.111-ui 配套接收程序，再保持 USB 线连接，在 TAB5 选择「仅蓝牙」。电脑升级窗口「传输测速 → 蓝牙升级预检」。
- USB 只用于启动/停止与结果读取，测试数据固定走 BLE；自动模式、其他固定模式或蓝牙未就绪不开始。中途模式/认证会话变化则停止，不跨通道回退。
- 三轮各 256 KiB 下载，共 768 KiB，只在 RAM 接收；复用 OTA 的加密分块 RPC 与字节检查。预检不擦写 Flash，不改变启动分区，不创建/提交聊天内容。
- 总传输预算 20 秒；每个请求等待最多 5 秒，因此正常故障退出约 25 秒内。主机 30 秒后发停止请求；失联时不能保证电脑立即确认，但固件有独立边界。
- 三轮必须全部完整、错误码为零且通道为 BLE/down。使用最慢一轮，按已选择的真实固件大小估算：`秒数 = 文件字节 / 最慢轮字节每秒 × 1.2 + 25`。
- 20% 为传输余量，25 秒为写入、校验、完成提示和重启的保守预算，不是假称测到这些阶段；小样本不能保证长时稳定。约 6.86 MB 镜像要达到此预检目标，最慢轮需约 85 KiB/s。
- 无完整三轮、未选择固件、超过目标均不报告通过。不会自动发起 OTA。预检达标后仍需另一个镜像版本用于实测，并核对 `previousOtaPhases.transport=2`、总字节、哈希、分区 VALID 及用户观察的重启完成；原自动轮播及用户连接选择保留。

## 协议与兼容

USB `tab5_benchmark` 增加 action=`ble_ota_probe`，所有此类响应增加可选 `bleOtaProbe:true`。新主机先发 status 核对能力，不对旧固件误发测试。普通 start/status/cancel 及其结果格式不变。设备发出的已认证 `kind=benchmark` 请求增加 `otaProbe:true`，只用于本机选择 5 秒等待上限；回复内容和认证规则不变。旧桥接可按现有 benchmark 处理，但缺少新电脑端调度与估算界面。

部署需同步 Microsoft.Windows.SDK.NET.dll 及其运行依赖，不能只替换主程序五件套。真实设备结果与候选检查分别记在本地 `artifacts/development/tab5-ble-ota/`，没有读回前不能报告 120 秒目标通过。

## English

Follow-up: complete internal validation before a consolidated hardware pass. BLE OTA independent-range compression reduces the exact .115 image payload by 47.57%; production authenticated RPC and native decoder bounds/fault tests pass. No deployment/reflash/latest replacement. Existing raw RAM preflight is not compressed-image acceptance; see [internal evidence](TAB5-BLE-COMPRESSION.md).

Follow-up: this experiment is withdrawn; the verified window64/native7 bridge is restored. RAM rounds took4823/3581/3972ms with no integrity error, but no image was selected for a valid estimate. Voice stop trouble also reproduced after rollback and later recovered; causality remains unconfirmed. Firmware .115 fixes stale ready-to-speak presentation, pending device acceptance. The following describes the withdrawn experiment.

The bridge-idle-poll candidate rechecks bulk priority after acquiring the shared GATT gate for the first voice-mailbox read. The previous64 repeat had22 voice polls and4719ms of poll timing including gate waits; that total is not all reclaimable. During bulk work, an idle poll may yield if a real read occurred less than1000ms ago. The first probe, a due probe or recent voice activity bypasses deferral. Normal/active polling delays and in-progress fragment reads stay unchanged. The1000ms threshold permits a probe; it is not an end-to-end latency guarantee. Keep64/native7, heartbeat and all authentication/order/integrity/final-ACK checks. Deferred reads do not synthesize device replies or execute/replay requests. Optional voiceDeferredCount separates deferrals from real voicePollCount. Local build and scheduling/cancellation/failure/fragment/notification checks pass; hardware speed and voice responsiveness remain to be verified on unchanged .114.

The old runtime showed a normal-status timeout at14:48:22,2440/7190bytes with no bulk RPC; by14:48:59 BLE acknowledged data, USB/Wi-Fi and display health recovered. Preserve this in idle-poll/baseline-readback.json and do not attribute it to undeployed code. Explorer bridge-only deployment followed by one isolated BLE RAM preflight is pending; no new firmware, window ABA or full OTA. The120s gate remains unchanged.

The same-build .114 window ABA repeat at 14:04 passes all nine RAM rounds: 32-before 5839/4440/5700ms (15.979s), 64 at 3885/4415/4682ms (12.982s), and 32-after 5165/4374/5326ms (14.865s). Window64 is 12.67% shorter than the final baseline and 15.82% shorter than the baseline mean; baseline drift is 6.97%. Both ABA runs favor64 over both controls, with all six64 rounds intact. Adopt min(peerOffer,64) for large replies, retain native7, and require no further identical ABA repeat. This does not establish sustained performance or a hardware ceiling; conservative64 estimates of165/172s still exceed120s, so no full BLE OTA is scheduled. Compression remains agreed subsequent work.

All three repeat traces fully cover18RPC/789381bytes/1650fragments without failure/rejection/drop; actual windows32/64/32, native7, 15ms/2M/DLE251 are verified. ACK counts66/33/66 but waits7401/8082/7347ms show that halving barriers does not halve wait time. Timings overlap. Wi-Fi restoration succeeds all groups with subsequent association, no display errors, unchanged retained13:55:55 cancellation, and original display policy. Evidence is in window-comparison/hardware-repeat.json and hardware-repeat-analysis.json. Bridge-only candidate bridge-window64-default was deployed through Explorer at14:09 and verified at14:11 against six files, the port18765 process and its startup time. BLE data is acknowledged, Wi-Fi associated, display errors zero, and the original display policy restored. No new bulk RPC or exact-boot check has run in this process; retain the prior exact .114 and two RAM comparisons as historical evidence, without calling this a new bulk-transfer acceptance. Readback is preserved in window64-default/deployment-verified.json; candidate preparation snapshots remain unchanged. No firmware flashing was required. Older peers retain their offered32/8 bounds; small replies/voice are unchanged. Window ABA remains32/64/32, native-queue ABA retains wire32, and all exits restore production64/native7. The historical first-run result follows.

The .114 hardware window comparison matches all six bridge files and verifies exact ELF bc30895c5115f3d305389194bdf8eea72ca8b6c4c3c1386fe64566f1213c501e, ota_0 VALID, full 6863568 bytes, stage9/error0. Installation was Wi-Fi (transport1), sender13625ms/device19299ms, not BLE OTA. All nine RAM rounds pass: window32/64/32 totals14852/12663/13106ms, estimates204.72/165.29/165.07s. Each trace fully covers18RPC/789381bytes/1650fragments, no failure/rejection/drop; actual windows32/64/32, native7 throughout, default32 restored. ACK counts66/33/66 confirm the intended mechanism.

Window64 is9.41% shorter than the two baseline groups' mean but only3.38% shorter than the final baseline, while the baselines themselves improve11.76%. First-RPC reads are484/62/31ms; the cause of the first group's extra delay is unconfirmed. Do not attribute the entire change to window size. The slowest rounds for64/final32 are4465/4458ms, so both conservative OTA estimates remain about165s, above120s. ACK waits8044/7484/8214ms include radio drain and do not halve with count. Link15ms/2M/DLE251, all Wi-Fi cycles restored without expiry and later fresh data confirms association. Device boot counters show one connection/reason0/display errors0; the host retains a13:55:55 cancellation, not a failure in the three captured traces. Original display policy is preserved. Keep default32 and request one same-build repeat without reflashing/restarting to check reproducibility. Raw/derived evidence is retained in window-comparison; candidate preparation snapshots are unchanged. No full BLE OTA, compression remains subsequent work.

The 13:39 native-queue ABA hardware readback matches all six candidate files and confirms actual limits 7/31/7, restored to seven. All nine 256 KiB rounds pass: groups total 12658/14547/13459ms, conservative estimates 170.50/184.95/183.95s. Native31 is 11.40% slower than the two baseline groups' mean (10.23% lower throughput), and slower than both. Baselines themselves differ 6.33%; this does not establish an absolute ceiling. Every trace covers 18 RPCs/789381 bytes/1650 fragments without failure/rejection/drop. Each group has 66 ACKs with waits 8940/10416/9949ms, about 76/77/80% of reply time; radio drain and overlapping waits cannot be treated as removable overhead. Link remains 15ms/2M/DLE251; all three Wi-Fi cycles restore successfully and later fresh data confirms association. Retained device count4/reason531 lacks an event timestamp; no host interruption. Original display cycle remains. No new flash or startup verification; evidence is in queue-comparison/hardware-result and analysis JSON files.

The next .114/window-comparison candidate advertises an optional 64-fragment response window, with default32 and native7 unchanged. Explicit 32/64/32 isolated RAM testing changes ACK frequency only: a ~48 KiB reply needs two rather than four barriers. No receive-buffer, notification, radio, Flash or integrity change, and no promise of halving elapsed time. Install via USB/Wi-Fi, verify exact .114 boot, then run the new window-comparison button in BLE-only mode with USB retained and isolation checked. All exits restore32, no automatic parameter selection or OTA; the 120s goal and subsequent compression work remain.

The restored cohort sender exactly matches all six original radio-isolation files. The 13:13 readback passes three 262144-byte rounds at 6343/6156/5056ms, total 17555ms, estimate 224s. This is 8.63% shorter than rolling's 19213ms but 25.42% longer than the same original sender's first 13997ms. Radio variability prevents attributing the entire difference to scheduling. All 18 RPCs/789381 bytes/1650 fragments are covered without failures or rejections. Link remains 15ms/2M/DLE251; handler 53.128ms is 0.31% of span, reply 16625ms includes ACK waits 13642ms, and inter-RPC gaps are 62ms. Overlapping waits include radio drain. Wi-Fi stop/start succeeded, held 17582ms, restored without expiry, then fresh data confirmed association. Retained connection count 3/reason 531 has no event timestamp. Prior .113 boot verification is historical; no flash or new boot verification occurred. Original display cycle remains. Evidence: cohort-recheck hardware-result, analysis and comparison JSON files.

The bridge-only queue-comparison candidate adds an explicit 7 → 31 → 7 ABA run, each group using the unchanged three-round isolated RAM preflight. Cohorts still drain within the existing 32-fragment wire window before final ATT confirmation. Default capacity is seven; 31 is temporary in group two. Incomplete data, failed Wi-Fi restoration, cancellation or exceptions stop remaining groups and restore seven while preserving results. The comparison has a 120s cancellation budget; per-pass 40s host and 20s transfer plus one 5s request remain, with up to 4s additional stop cleanup. No automatic selection, persistence or OTA. In-memory diagnostics retain each group's result/radio/isolation/host trace and estimate. Optional gatt.NativeLimit reports configured capacity, not measured concurrency or radio packets. Verify trace byte coverage before comparing aggregates; the known first-ticket limitation remains. Release, queue/fault/drain, ABA lifecycle/concurrency, BLE, upgrade, status and normal/minimum UI checks pass; hardware benefit remains unverified. Deploy only the bridge via the existing Explorer shortcut, retain .113, and select the new comparison button. Full OTA still requires the 120s gate; compression remains subsequent work.

The12:52 rolling trial matched all six deployed files, retained .113 and Wi-Fi isolation, and completed three262144-byte rounds without errors in6267/6219/6727ms (40.85/41.16/38.06KiB/s). Total19213ms is37.27% longer and throughput27.15% lower than the cohort sender's13997ms; estimate236s remains above120s. No gain was observed, so withdraw rolling and restore cohorts for a same-condition recheck before attributing all differences to scheduling. Radio conditions may vary.

Both ends cover789381bytes/1650fragments/18RPCs with zero rejection/failure/dropped samples. Wi-Fi stopped/restored successfully, held19267ms with no expiry, and fresh health confirms recovered Wi-Fi. DLE251/15ms/2M remain. RX handler54.375ms is0.29% of span; inter-RPC gaps fell187 to94ms, but reply time rose12810 to18204ms and ACK waits9607 to15125ms. Waits include radio drain and overlap other metrics. No current host interruption; retained device count2/reason531 lacks event time. No firmware write occurred; the restarted bridge has no fresh boot verification, while prior exact .113 evidence remains valid historical evidence. Original display cycle is preserved. Keep rolling/hardware-result.json and hardware-comparison.json. Restore only the two sender/test files from the last trial; force a nonincremental rebuild and verify DLL identity with bridge-radio-isolation to exclude stale rolling output. Cohort-recheck Release/BLE/fault/upgrade/status checks pass; Explorer deploy, same isolated preflight, no firmware reflash or other parameter change. Compression remains subsequent work. The rolling-candidate description below is historical and superseded by this result.

The same-.113 comparison passed all three262144-byte rounds in both modes. Isolated5626/4076/4295ms (45.50/62.81/59.60KiB/s), total13997ms; Wi-Fi-retained8108/5949/4896ms (31.57/43.03/52.29KiB/s), total18953ms. Isolation shortened this sample26.15%, raising mean throughput35.41%; maximum RX gap fell541.543 to175.750ms. Conservative estimates202s/280s both exceed120s. One short pair is not sustained or absolute-limit evidence. Wi-Fi stop/start succeeded, held14059ms, restored without expiry, and subsequent fresh health confirmed association/data recovery. Both runs received1650 fragments/789381 bytes with zero rejection and actual251-octet DLE,15ms interval/2M. Handler time46.914/48.951ms is only0.35%/0.26% of RX span. Isolated trace covers18 RPCs, replies12810ms/ACK waits9607ms (75%, including drain). Control trace misses the first49312-byte RPC because a pre-existing poll may have no capture ticket: device totals remain complete, but host aggregates are not directly comparable. Control retains previous isolation counters. Six runtime files match; .113 exact ELF/ota_1 VALID/full bytes/error0 are verified. Its installation was Wi-Fi (sender15.656s, device19.353s), not BLE. Original display policy remains. Numeric evidence lives in radio-isolation/hardware-isolated.json, hardware-control.json and hardware-comparison.json.

The next bridge-only candidate, bridge-rolling, reuses .113. Within each32-fragment barrier, keep at most seven native writes pending and refill each available slot in cursor order instead of draining entire cohorts. Observe all completed operations before further submission. Faults/cancellation/synchronous submission failure stop refilling and drain all submitted work before propagating the original error; ACK only follows full success. Voice/small/legacy paths, integrity and radio parameters remain. Tests keep the oldest six commands blocked while a single slot progresses, and exercise out-of-order completion, late/immediate failure, cancellation, oldest-operation draining and final ACK. Hardware improvement remains pending; compression remains subsequent work.

The .113 candidate adds an opt-in wire action `ble_ota_probe_isolated`, selected by the desktop checkbox by default; unchecking uses the original probe for a same-firmware comparison. BLE-only previously retained Wi-Fi association/polling, but its throughput cost is unmeasured. Keep the 32-fragment window, seven pending native writes and three 256 KiB rounds unchanged. Compression is an agreed later feature after raw BLE optimization, not implemented here.

The existing Wi-Fi worker stops/restarts only the Wi-Fi driver, preserving saved networks and without resetting the wireless coprocessor. Require BLE-only and USB presence. Preparation/restoration waits are each six seconds, the transfer budget remains 20s plus one exchange up to five seconds, the stop lease expires after 35s and the host deadline is 40s. Completion, cancellation, mode/USB loss or expiry requests restoration. Failed restarts retry each second and prevent a new test; unconfirmed restoration fails the probe. Driver calls themselves are not guaranteed to return within those bounds. Completed restoration means driver restart, not renewed AP association; verify the latter on hardware.

USB-only numeric `bleRadio` and `wifiIsolation` diagnostics leave BLE identity size unchanged. Link parameters are current at USB readback and may change after transfer; receive metrics are retained from the capture interval. Handler elapsed time includes parameter requests/lock waits but excludes preceding buffer copying, so it is not pure CPU usage. DLE zero means no observed negotiation event. Capability/parser/lifecycle/error/retry/benchmark/native counter checks, Release/firmware builds, layout and ELF guards pass; installation/coexistence/restoration were unverified at preparation, with subsequent results recorded above. Install via USB/Wi-Fi, verify boot, keep USB and run BLE-only isolation preflight; read back results/recovery before the unchecked comparison. No full BLE OTA after failed/incomplete or over-120s estimates.

The 11:23 continuous-scheduling hardware screen completed three 262144-byte BLE/download rounds with zero errors in 5816/4837/5977ms, or 44.02/52.93/42.83 KiB/s. Total 16630ms is 2.31% shorter than the previous 17024ms; average throughput increased 2.37%, but round three slowed. One short comparison does not prove repeatable improvement. The slowest-round estimate is 212.69s, displayed as 213s, still above the 120s target; no full BLE OTA is scheduled or performed.

All six installed files match bridge-continuous. The trace contains all 18 actual RPCs, no dropped samples/failures or BLE interruption, and successful throughput preference with actual 15ms interval and bidirectional 2M PHY throughout. Handler time totals16ms; replies15143ms, including66 ACK waits totaling12062ms (79.7%, including radio drain). Inter-RPC gaps total only172ms, 1.04% of the first-to-last RPC span, and shared-gate queue waits1487ms. Two status updates and27 voice polls remain; their wall time includes queueing and cannot be added to the other metrics or treated as fully recoverable overhead. Further gap-only tuning has little measured room; this is not a hardware-limit finding. Firmware remains verified .112 with prior Wi-Fi installation, and original automatic six-page cycling/15s interval is confirmed. Numeric readback and calculations are saved in continuous/hardware-preflight.json and hardware-analysis.json; the original preparation snapshot is retained.

The bridge-only `bridge-continuous` candidate reuses installed and verified .112; no firmware flash is needed. Successful large RPC replies retain bulk priority for one second across short block gaps. Idle/duplicate polls and ordinary small replies do not renew it; real bulk work can renew it, handling/write failures and cancellation release it, and disconnect destroys the session. Status traffic rechecks priority after acquiring the shared send gate. The existing six-second refresh bound, 15ms active-voice polling, 32-fragment write window, seven-native-operation bound, ACK behavior and negotiated parameters are preserved.

Only an explicitly started BLE RAM preflight activates the in-memory numeric `蓝牙预检分段` trace. Retain up to 32 completed RPC samples, aggregate additional samples with an explicit dropped count, and stop issuing capture tickets after 35 seconds or preflight completion. Already-started work can report its final result after completion; generation tickets reject late work from older runs. No request IDs, payloads, pairing data or voice text are retained or automatically written to disk. `controlMs` includes host USB capability/control/readback/cancellation time and differs from device transfer duration. Relative sample start/end and all other durations use milliseconds; byte counts include encrypted RPC framing. Queue time is included in request/reply time, native command waits overlap, and ACK waits include radio drain: do not sum all metrics. The final Windows ATT completion can occur after the device result and `controlMs`.

Release build, scheduling/lease/fault/cancellation/post-gate-refresh/voice/trace lifecycle tests and existing BLE transport/upgrade checks pass. The preparation baseline was 40.89–50.20 KiB/s with a conservative 222-second estimate; subsequent hardware results are recorded above. The 120-second criterion and full-OTA acceptance boundary remain unchanged.

The .112 hardware screen completed all three 262144-byte BLE download rounds with error zero in 6260/5100/5664ms (40.89/50.20/45.20 KiB/s; 17024ms total). Corresponding first/second-round throughput improved 39.39%/37.75% against the previous queued-eight screen; this short comparison is not a sustained-performance guarantee. Six installed bridge hashes match and actual RPC diagnostics show write8/bulk32. Exact .112 ELF/ota_0 VALID/complete bytes/error0 startup is verified; its installation was Wi-Fi, not BLE. The unchanged conservative estimate is 221.58s (displayed 222s), above the 120s goal, so complete BLE OTA remains unaccepted and is not scheduled. The last 16549-byte tail's ACK waits include queued radio drain and are not directly comparable to the prior 49317-byte snapshot. Original automatic cycling, six-page order and 15s interval remain. Numeric evidence is retained in window32/hardware-112-complete.json.

The deployed queued-write .111 bridge still failed: two complete 256 KiB rounds took 8.726/7.025s (29.34/36.44 KiB/s), then the partial third round reached the budget. Six installed hashes matched; no BLE interruption was recorded. The final RPC spent 907ms in 13 ATT ACK waits out of a 1125ms reply, with 13 queued batches, a successful throughput preference request, actual 15ms interval and bidirectional 2M PHY. This is one RPC snapshot, not a whole-run average or a hardware-limit measurement.

.112 advertises a separate optional `rpcWriteWindow=32` only for large computer-to-device RPC replies. Missing/invalid capabilities retain eight; old bridges ignore the field, small replies/voice/acknowledged-only peers retain existing behavior and notification pacing is unchanged. Within a window, submit ordered cohorts of at most seven native commands, drain each cohort, then send the final acknowledged fragment. Errors/cancellation drain submitted work and stop. A 49317-byte reply at MTU 517 needs four barriers instead of thirteen. Existing receive buffers, cursor/authentication checks, memory pools, Flash and partitions are unchanged. The production maximum-width identity is exactly 512 bytes and must not grow. Local fault/core/boundary/build checks pass, but native completion is not proof of air delivery; .112 requires USB/Wi-Fi installation followed by the original RAM preflight. Actual speedup and complete 120-second BLE OTA acceptance remain pending.

The .111 hardware preflight completed two 256 KiB rounds in 8.027/6.628s (31.89/38.62 KiB/s), then hit the 20s aggregate budget after 196608 bytes of round three. No Flash write failed; the incomplete screen does not pass. Transfer-only extrapolation is approximately 173–210s, above the user's 120s whole-upgrade target.

A second, bridge-only candidate retains .111 and the eight-packet wire window. Large RPC responses submit at most seven WriteWithoutResponse operations synchronously in cursor order, drain every submitted operation, then send the final acknowledged write. Errors/cancellation suppress that barrier; the GATT gate is retained until submitted tasks complete. Voice/small replies and legacy acknowledged-only peers keep the previous path. No automatic replay is added. Native submission order is not proof of all adapters' air delivery order: device offset checks and authenticated complete-packet verification still reject corruption, and hardware preflight remains required. Per-RPC counters record read latency, command/ACK counts and wait sums, batch wall time and queued-batch count; actual interval/latency/PHY is sampled at completion and retained instead of overwritten by duplicate mailbox polls. Overlapping command waits must not be summed with wall time. Timeout text now identifies the budget and completed rounds. No firmware flash is needed for this candidate; the time budget and acceptance criteria remain unchanged.

The user-defined target is a complete BLE upgrade of roughly 6.86 MB within 120 seconds. Optimize first, then screen with a RAM-only preflight; do not schedule full BLE flashing after a slow, incomplete or failed screen. Windows 11 temporarily requests ThroughputOptimized only during actual BLE bulk RPC traffic, restores Balanced afterwards and releases the request on disconnect. Windows 10 retains the existing behavior; the compile-time SDK is 22000.56 while the OS target stays 19041. Request status is not proof of negotiated speed. Large first download responses now receive bulk priority; retain authenticated 48 KiB binary ranges, ordered eight-packet windows and final barriers.

Install .111 via USB/Wi-Fi, select BLE-only and use the desktop connection window's BLE upgrade preflight. USB supplies control/readback only. Three 256 KiB downloads must all pass byte/hash checks; no Flash or task mutation occurs. A 20-second transfer budget and five-second exchange bound permit roughly 25 seconds to finish; the host requests cancellation at 30 seconds. Wrong mode, stale links or changed session/mode stop without fallback. Estimate from the slowest complete BLE/download round: image bytes / bytes per second × 1.2 + 25 seconds. The 20% margin and 25-second Flash/verify/reboot reserve are assumptions, not measured OTA timing. Missing/invalid rounds or estimates over 120 seconds do not pass, and no upgrade starts automatically.

USB action `ble_ota_probe` and response capability `bleOtaProbe:true` are additive. Probe requests carry authenticated `kind=benchmark,otaProbe=true`; old benchmark payloads and start/status/cancel remain compatible. Deploy the updated Windows SDK runtime dependencies together with the bridge. Actual BLE throughput and the full 120-second target remain unverified until hardware readback and a later full-image installation; preserve the user's original display cycle and connection preference.
