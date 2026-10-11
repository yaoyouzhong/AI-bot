# 三通道优化候选与验证 / Transport candidate and validation

2026-10-09 准备候选，2026-10-10 部署已授权的桥接候选和 .157。**实际镜像启动核验已通过；三通道速率及长期稳定性仍待完成，不能宣布 USB 偶发超时已解决。**

## 改动及边界

| 项目 | 本轮行为 | 验证边界 |
| --- | --- | --- |
| USB 调度 | 250 ms 指标采样遇串口忙立即跳过；取得串口后再生成最新指标帧，避免旧帧排在 RPC 前；已有历史样本随下一帧补齐 | 不增加或放宽原 4 秒 ACK 等待；持续占用时仍可能暂时没有新指标 |
| USB 超时定位 | 增加已读字节、完整行、残余字节和读取用时；诊断增加 metricsDeferred | 不记录回复正文或凭据；这些计数帮助区分静默、错类型和不完整回复，不能单独证明故障根因 |
| 图库恢复 | 清单及 SHA 绑定的分块在缺少有效回复时最多额外尝试两次，保留同一次下载已经完成的分块；重试使用新 nonce | 不跨链路续传，不把半张图写入缓存；重试耗尽后仍由现有 UI 退避重新请求 |
| 图库等待 | USB/BLE 图库单次应答等待由通用的 75 秒缩至 8 秒；Wi-Fi I/O 超时由 15 秒缩至 8 秒 | Wi-Fi 为 SDK I/O 超时，并非整个图片下载的总上限；取消在请求／分块边界生效 |
| Wi-Fi 错误 | 保留 HTTP 401/403/404/503 等明确状态，避免当作无回复重复请求；保持 HTTP 连接复用及失败关闭 | 对端 HTTP 错误不代表成功认证；收到有效加密响应后仍执行原验证 |
| 图库间隔 | 去掉每块完成后的固定 10 ms 等待，保留调度让出 | 只消除软件固定等待；无线传输、解码与首次准备耗时仍需分别测量 |
| BLE 重连 | 仅记住本进程中已认证且数据 ACK 成功的设备；其广播出现后提前结束扫描并优先连接；断线后一次 250 ms 重试，失败恢复 3 秒退避与完整 4 秒扫描 | 首次连接保留完整扫描；地址提示不替代认证，配对变化清除提示；未调整已验证的传输窗口和原生队列 |
| 额度显示 | 标题为「近7日 · 周额度使用（百分比）」；去除当日柱图上方的部分记录文字 | 保留原数值及缺失／不完整数据语义；已查看原生预览，物理屏幕待确认 |

USB 超时原因为待验证项。本轮避免盲目延长超时，并增加下一轮现场判断所需信息。OTA 保留已有压缩、分块和写 Flash 策略，没有修改升级协议或再次提高 BLE 窗口。

## 已执行检查

| 检查 | 结果 |
| --- | --- |
| Windows Release，输出到独立 development 目录 | 通过，0 警告、0 错误 |
| USB telemetry / integrated transfer 自检 | 通过：繁忙让出、恢复最新样本、异常与取消释放、64 样本历史、会话恢复、ACK 超时计数与正文不泄漏 |
| BLE voice / transfer 自检 | 通过：发现提示、单次快速重试、配对变化、扫描取消、MTU 23/247/517、队列错误、最终 ACK、超过 10 秒的旧协议传输 |
| 三通道图库桥接测试 | 通过：726 次认证请求，36 个图片场景、两方向与多页、48 KiB / 8 KiB 分块、SHA-256、JPEG 解码；HTTP 为 loopback，USB/BLE 为模拟传输 |
| 固件生产图库 worker 的桌面测试 | 通过：123 次解码调用，72 次离线重开零 RPC；清单丢失、尾块丢失保留首块、重试上限、401 拒绝、前台／通道切换、损坏图片不缓存。SDK／解码器为测试替身 |
| 固件生产 RPC 桌面测试 | 通过：100 次完成竞态、100 次重连／新请求竞态、跨通道隔离、过期回复拒绝、超时退出、低内存及 completion 释放 |
| 固件 HTTP 桌面测试 | 通过：POST、连接复用、分片读写及失败关闭；HTTP 错误状态保留 |
| ESP-IDF 固件 .157 | 通过；应用 6,588,000 bytes，分区空间检查通过 |
| 实际 sidecar 的 LVGL 全套预览 | 通过；已查看更新说明首页、滚动后内容与额度趋势截图 |
| 两个仓库 git diff --check | 通过；存在换行转换提示，无空白错误 |
| 必需的 --status-once | **通过**：10 月 10 日 09:09:44，实际 Explorer 桌面启动环境退出 0，状态 JSON 解析成功；此前 Codex 启动环境退出 2 的记录保留，资料目录保护未改动 |
| 候选真机部署及启动 | **通过**：桥接 DLL 与批准的哈希一致；.157 精确 ELF 指纹匹配，运行分区 ota_1 / VALID，连续 UI 心跳通过 |
| 三通道 RAM RPC 真机测速 | **18 组通过**：每通道两方向各 3 次、每次 256 KiB，SHA 和下载字节校验通过；Wi-Fi 上传明显偏慢，不能判定速率已理想 |
| 重连、图库实测及长时间运行 | **未完成**；升级后曾出现一次 USB metrics 无回复超时及 BLE GATT 写入超时，原因尚未确认；持续运行记录进行中 |

证据目录：`artifacts/development/transport-20261009/`。包含 `telemetry-test.log`、`integrated-transfer-test.log`、`gallery-transport-test.log`、`ble-voice-test.log`、`ble-transfer-test.log`、`gallery-worker-test.log`、`rpc-exchange-test.log`、`rpc-http-test.log`、`firmware-build.log`、`preview.log`、`desktop-status-result.json`。

开始运行 BLE voice 自检时曾误把输出目录传给其可选的 ADPCM 输入文件参数，调度检查完成后因文件不存在退出；去掉该参数后完整重跑通过。最终日志为正确命令的结果。

## 候选身份与范围

| 对象 | 身份 |
| --- | --- |
| 已安装固件 | `0.2.157-ui`；下面哈希记录该已安装镜像，latest 随后续候选更新，不能用 latest 当前内容替代此身份 |
| 固件 SHA-256 | `74c3f0efdbaca865a176b53c78d4f91508cbc14a61fd4fe8b7634f77143ba26f` |
| 固件内嵌 ELF SHA-256 | `5cb4d023c6d721fc666e22d3d82ccc0d59547118b12fa3369ae873ec80ac1eb0` |
| 桥接候选 DLL | `artifacts/development/transport-20261009/bridge/AIBotBridge.dll` |
| 桥接 DLL SHA-256 | `7e9920edf5ef6ad4f8d17454da49960ee20bbdae462517d923490c0d430d0f2b` |

桥接仍为 0.6.3 的本地 development 候选，用上述哈希区分已运行程序。构建使用当前工作区，保留既有未提交的 UI／图库等改动；本报告只评估本轮传输与额度文字变化，不替其他改动补做验收。部署前应核对整个候选范围，不能把该目录当作仅包含本轮修改的独立发布包。

本轮桥接修改：`Tab5Telemetry.cs`、`Tab5UsbText.cs`、`Tab5Service.cs`、`Tab5BleClient.cs`、新增 `Tab5BleDiscovery.cs` 及对应自检。TAB5 修改：`gallery_remote.c`、`rpc_link.c`、相关桌面测试，以及本会话先前已授权的 `ui_dashboard.h` 文字调整。未提交、推送或公开发布。

最初只读审视期间观察到桥接于 23:37:46 启动新进程，当时设备仍为 .156；后续部署、测速及维护重启见下文。跨进程的累计中断计数不能直接比较，也不能把清零视为修复成功。

## 上机验收顺序

### 2026-10-10 已完成现场步骤

- 09:10:56 部署桥接候选，覆盖文件先备份至 `runtime-backup-20261010-091051/`；程序使用原配对和资料。`deployment-result.json` 核对了 DLL 哈希和原轮播设置。
- 用户在设备点击 Wi-Fi 升级。桥接压缩传输 4,124,415 bytes，发送 10,625 ms；这不是从点击到重启可用的总时间。
- 设备于 09:12 上报 .157。随后 USB 核验确认精确 ELF SHA-256、`ota_1 / VALID` 及连续界面心跳，见 `boot-verified-157.txt`。
- 固件保留的升级诊断：prepareMs=7644，writeMs=9474，verifyMs=1071，installMs=18808，previousError=0。传输与写入存在重叠，不能把上述分项简单相加作为总时间。
- 首个桥接进程在 09:12:53 记录 USB metrics ACK 超时：receivedBytes=0、completeLines=0、pendingBytes=0、readMs=4016；09:12:10 记录 BLE GATT write 超时。升级后恢复阶段和稳定运行阶段尚未完整区分，因此不判定故障已修复。原始证据见 `post-install-diagnostics-20261010-0914.txt`。
- 09:16:21 为重新打开核验窗口而正常重启桥接，见 `verification-maintenance-restart.txt`。这是测试维护操作；新进程中断计数清零不构成修复证据。
- 09:24:49–09:24:53 测速后正常退出、执行 Release `--restore-cycle-default` 并正常启动。`policy-after-benchmark.json` 确认 auto、cycleEnabled=true、六页原顺序及 15 秒均保留；新进程 44496。此次收尾重启是已记录的维护操作，不计作意外重启。
- 真机 RAM RPC 测速全部 18 组成功（每通道上传/下载各 3 次，每组 256 KiB），见 `hardware-benchmark-157.json`。中位耗时／有效载荷速率：USB 上传 454 ms／563.9 KiB/s、下载 775 ms／330.3 KiB/s；Wi-Fi 上传 18032 ms／14.2 KiB/s、下载 685 ms／373.7 KiB/s；BLE 上传 6576 ms／38.9 KiB/s、下载 4975 ms／51.5 KiB/s。这是 RAM RPC 测量，不含图片解码或 OTA Flash 写入；每方向 3 个样本不足以交付 P95，也没有同日旧候选对照，不能宣称提升比例。Wi-Fi 上传 17.7–18.9 秒中，编码约 77–81 ms、解码 4–9 ms，主要等待在 exchange；需要进一步定位请求写入、对端读入和网络等待。
- 09:21:01–10:21:02 完成 60 分钟只读链路观察，共 1,746 个样本；汇总为 `link-observation-final.json`。采样读取失败为 0，显示错误和欠载均为 0，但记录到 9 次独立 USB 控制中断（8 次指标 ACK 超时、1 次 status ACK 的 IOException），并伴随 RPC 邮箱断开。维护重启将记录分成 5 个桥接进程；最后一个进程只覆盖约 16.5 分钟，因此不能声称同一候选连续稳定运行 60 分钟。后台观察不替代图库、会话及物理重连完整验收。
- 用户审阅修订预览并明确采用后，生产源码中的固件升级提示改为 14×14 像素圆润箭头，Codex 改为未读任务数量角标，二者使用深绿色 `#21854A`。超过九个任务显示 `9+`；只有实际读取对应任务回复才清除该任务未读状态。原生 LVGL 通过 1、3、9+、读取一个保留其他任务及既有阅读流程检查，见 `notification-adopted-preview.log`。这些源码变化尚未进入设备当前 .157 镜像。最初 MSVC 预览的 C atomic 编译失败保留，最终使用现有 Clang 入口通过。

### 2026-10-10 Wi-Fi 上传复测与界面收尾

- Windows 对 TAB5 Wi-Fi RPC 接收连接启用受支持的逐包 TCP ACK；不支持时保留默认策略。诊断仅记录请求字节数、读入／处理／写回复耗时，不记录消息正文。Release、LAN 真 Socket、认证 RPC、726 次图库请求及 BLE 调度检查通过。
- 首轮及第二轮真机各 18 组均通过 SHA／字节校验，使用同一 .157、每方向每组 256 KiB。Wi-Fi 上传基线三次为 18.892、17.714、18.032 秒；修改后首轮为 3.107、6.535、14.238 秒，第二轮为 25.596、16.689、15.335 秒。首轮有加速，但第二轮没有稳定复现，**不能判定速率优化通过**。每轮只有三个样本，不能交付 P95。
- 大请求对端读入仍出现约 1.3–5.6 秒等待，处理／回复阶段通常低于 1 ms。这定位到请求到达过程，不能单凭这些计时区分设备发送、SDIO、射频或 TCP 重传。下一候选将发送缓冲从 4 MSS 调到 16 MSS（23,040 bytes），保持 SDIO 队列和无线缓冲上限；独立源码快照已完成 ESP-IDF 构建，尚未真机验证，不能宣称已解决根因。
- `Tab5ConnectionForm.cs` 去除两个写死的最低固件版本标签；执行时继续检查 `bleFirmwareProbe` 与 `bleOtaProbeIsolation` 能力，无法执行时保留兼容提示。Release、自检及正常桌面 `--status-once` 通过；实际桥接窗口已核对标签移除。
- 09:45:23 部署运行 DLL SHA-256 `7ac15dd560e941bb4762b95065a5859a478cb305783499c6cc228f27a7bcc81c`，替换文件已备份至 `wifi-upload-ui-runtime-backup-20261010-094517/`。源码工作区包含其他既有变更，此候选不是只含网络改动的独立发布包。
- 10:04:28–10:04:33 测速收尾：正常退出，执行 Release `--restore-cycle-default` 并正常启动。`policy-after-wifi-upload.json` 确认 auto、cycleEnabled=true、六页原顺序及 15 秒间隔保留；维护重启单独记录，不作为稳定性证据。
- 用户随后要求将额度时间标记纳入 .159。已在生产协议与固件接入精细表盘和竖线：Codex 保留实际 API 窗口，Claude 分别使用明确的 5 小时／7 天窗口，5 小时按分钟、周额度按小时比对，内部保留秒级精度，不增加文字或底部刻度。未知、无效、未开始或到期周期隐藏；旧缓存不推测周期。配套桥接已独立构建并通过 TAB5 数据／线路自检及正常桌面 status-once，原生测试覆盖小时和分钟移动；完整固件构建与设备安装分别记录。

### 剩余验收计划

1. 新的固件上传候选完成构建、镜像／说明校验及原生预览后，再根据对应候选授权安装；.157 安装、启动核验和桥接桌面验证已经完成，不重复执行。
2. 在新候选上复测 Wi-Fi 上传分布与内存、三通道完整性；对照已记录的 .157 数据，不以单次最快成绩作为稳定优化结论。
3. 每通道使用同一图片与方向，分别测至少 10 次冷加载和缓存命中，记录取图、解码、总时间及 P50/P95。重新采集当日基线；历史单次结果不能冒充当日 P95。
4. USB／BLE 各做至少 20 次断开恢复，记录系统发现、身份确认、首个数据 ACK 和图片可用；USB 重点对比新增诊断，BLE 区分首次完整扫描与已认证设备重连。
5. 对每通道验证清单与中间分块中断、重试耗尽、模式切换和前台业务抢占；要求无重复已完成首块、无半图缓存、失败后可再次成功读取。
6. 连续运行至少 60 分钟，覆盖轮播、图库、会话读取和后台指标；记录进程／设备重启、ACK 失败及恢复、链路数据年龄、显示错误与欠载。出现未解释失败时不判定稳定性通过。
7. 正常退出并恢复自动轮播，重新启动，核对原页面、顺序、15 秒间隔和 `cycleEnabled=true`。

固件审批依据：[本地固件流程](TAB5-FIRMWARE-WORKFLOW.md)明确要求“刷机仍需对应候选的用户授权”。以上候选准备不构成已安装或真机通过。

## English

This local candidate adds non-queuing USB metrics, content-free reply-timeout counters, bounded same-transport gallery retries retaining completed ranges, explicit HTTP error preservation, shorter gallery waits, and one authenticated BLE discovery fast path with full-scan fallback. Quota labels requested in this conversation are included. OTA transfer parameters remain unchanged.

Release and ESP-IDF builds, simulated USB/BLE and loopback HTTP gallery tests, firmware worker/RPC fault tests, BLE scheduling/framing tests and native LVGL previews passed. On October 10, the approved bridge and .157 were installed. The normal Explorer desktop `--status-once` passed without changing the profile guard; USB boot verification confirmed the exact embedded ELF hash, ota_1 / VALID and continuous UI heartbeats. Compressed Wi-Fi sending took 10.625 seconds for 4,124,415 bytes, excluding full user-visible restart time. Retained device phases report prepare 7.644 s, write 9.474 s, verify 1.071 s and install 18.808 s; phases overlap. All 18 device RAM RPC benchmark rounds passed integrity checks, with three 256-KiB samples per link/direction. Median upload/download times were USB 0.454/0.775 s, Wi-Fi 18.032/0.685 s and BLE 6.576/4.975 s. Wi-Fi upload remains a material bottleneck. These results exclude image decode and Flash writing, have no matched old-build baseline and do not establish P95. USB no-reply and BLE GATT timeouts were observed during post-upgrade operation, so gallery/recovery/sustained-run acceptance remains incomplete. The 60-minute observation completed with 1,746 samples, no sample-read failures and zero display errors/underruns. Nine distinct USB control interruptions remain (eight metrics ACK timeouts and one status ACK IOException), accompanied by RPC mailbox disconnects. Maintenance restarts divide the record across five bridge processes; the final process covers only about 16.5 minutes. This is not uninterrupted 60-minute acceptance for one candidate and does not establish full gallery/session/reconnect stability. The original auto carousel was restored through normal exit, Release restoration command and normal startup, with all six pages and the 15-second interval confirmed. The user has adopted the revised rounded 14-pixel upgrade arrow and unread-task count badge in deep green #21854A. Production-source native LVGL checks cover individual-task read semantics and the 9+ cap. These edits are not installed on .157.


Both follow-up ACK-policy suites passed all 18 integrity checks on the same installed .157, but Wi-Fi upload was not stable: 3.107/6.535/14.238 seconds in the first suite and 25.596/16.689/15.335 seconds in the second, against 18.892/17.714/18.032 seconds before the change. These are three samples per suite, not P95 or sustained-speed acceptance. Large bodies still spend about 1.3–5.6 seconds arriving, while handler/write work is usually below one millisecond; this does not distinguish radio, SDIO, retransmission or device send pressure. An isolated next candidate increases TCP send capacity from four to sixteen MSS (23,040 bytes), keeping existing SDIO and radio queue bounds; its ESP-IDF build passed; hardware acceptance is pending. The deployed bridge DLL hash is 7ac15dd560e941bb4762b95065a5859a478cb305783499c6cc228f27a7bcc81c. Static minimum-version UI labels were removed while runtime feature checks remain. After testing, normal exit, Release carousel restoration and normal startup preserved the six original pages and 15-second auto interval, recorded in policy-after-wifi-upload.json. The user subsequently requested the time marker in .159. Production bridge metadata and firmware now support an independent refined clock/line on each quota bar: actual Codex API durations and explicitly named Claude five-hour/seven-day windows, minute comparisons for five-hour limits, hour comparisons for weekly limits and second precision internally. No new labels or external ruler. Unknown/invalid/not-started/expired cycles hide the marker; old cache entries do not gain guessed periods. A separate bridge build passed TAB5 protocol/metadata tests and normal-desktop status-once; native tests cover hour/minute movement. Firmware builds and physical installation are recorded separately.


### .159 候选身份 / Candidate identity

- `0.2.159-ui`，应用 6,633,088 bytes；固定交付为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，历史 .158 及此前 .159 候选均保留归档。
- 镜像 SHA-256：`6d53d1f43c915612ace8ef8038e20f09067af7ea48839bb4b97efb0a77cecbea`；ELF SHA-256：`83e845dbfeba6a68c0b889b915ec1bcd12971d18b7aca2be974cc659b9a8748c`。
- 包含 .158 总览／官方 Logo／页面样式、已采用的通知箭头和数字角标、TCP 发送缓冲候选调整及额度时间标记。国产周期贯通采集、缓存重载与 TAB5 转换，短周期按分钟、周及套餐按小时比对；明确 1 至 7 整小时的周期每小时一份，标准五小时与套餐百分比分 5 等份，周额度分 7 等份。只保留条内分隔、表盘与竖线。时间标记需要配套新版桥接，未知套餐周期不推算；[各厂商规则](DOMESTIC_QUOTA_SETUP.md#额度周期与-tab5-159)与真实账号边界另列。
- 最终证据目录为 `artifacts/development/quota-cycles-20261010/`：`firmware-source.json`、`quota-time159-firmware-build.log`、`prepare.log`、`final-sidecar-preview.log`、`desktop-status-result.json`。配套桥接 DLL SHA-256 为 `ce2b96fdc16b96ae6b9a3d5ed96502fd1e8fd6faa8c381b92e931902311825bb`；Release 0 警告／0 错误，国产周期／缓存／协议自检与正常桌面 status-once 通过。status-once 使用已有缓存，不代表厂商在线刷新；当时 Codex／Claude 缓存周期为空。完整 TAB5 自检与 Release 日志为该目录同级的 `quota-cycles-20261010-tab5.log`、`quota-cycles-20261010-bridge-build.log`。固件 143 个源文件相对前一份快照仅额度头文件变化，最终生成配置仍为 SND=23040、MSS=1440、SDIO TX queue=20、remote TX buffers=32。
- 以上最终候选及配套桥接已按授权安装。精确 ELF、ota_0 / VALID 与界面心跳启动核验通过，用户确认 Codex 表盘、竖线和条内等分显示正常。四组 RAM RPC 复测中 Wi-Fi、BLE 各 24 次完整性校验全部通过；USB 下载两次失败，并有一次指标 ACK 超时，稳定性未通过。原六页顺序和 15 秒自动轮播已恢复。详见[.159 实机验收记录](TAB5-ACCEPTANCE-159.md)；国产现有套餐记录已过期，仍不能替代真实有效套餐验收。

The final .159 application is 6,633,088 bytes at the standard local firmware path. Image/ELF and matching bridge fingerprints are listed above; earlier candidates remain archived. It retains the .158 overview/logo/style baseline, adopted notifications and bounded TCP send-capacity candidate, and carries domestic periods through collection, cache and TAB5 conversion. Short windows use minute comparisons; weekly/package windows use hour comparisons. Explicit one-to-seven whole-hour limits use one part per hour; standard short/package percentages use five parts and weekly percentages seven. Unknown periods remain unknown. Final evidence is under `artifacts/development/quota-cycles-20261010/`, with build and TAB5 logs alongside that directory. Release, API/cache/protocol fixtures and normal-desktop status-once passed; the latter only reads existing cache and does not verify fresh provider access. The isolated 143-file snapshot retains the .158/Wi-Fi baseline except for the quota header. The authorized matching bridge and image were subsequently installed: exact-ELF boot verification passed on ota_0 / VALID and the user confirmed the Codex screen. Wi-Fi and BLE each passed 24 integrity checks across four RAM RPC suites. Two USB downloads and one metrics ACK failed, so sustained stability did not pass. The original six-page, 15-second auto carousel was restored. See the [.159 hardware record](TAB5-ACCEPTANCE-159.md); current domestic-plan account acceptance remains unavailable because retained plan records have expired.
