# Mac 语音与 TAB5 显示 / Mac voice and TAB5 display

2026-10-28，本地开发与实机验收记录；未公开发布。

## 当前验收进度 / Current acceptance (2026-10-29)

当前安装 **Mac R34／TAB5 .292**。回填、单次发送及正确成功提示、长按清空、句尾完整、取消重录已获用户确认。本轮两道题的回答分别收到，用户另明确反馈“都是正常的”，多题独立回填／下一步／提交按本次实测通过。下方旧版本中的“待验／当前”均是历史记录，不应覆盖后续明确反馈。

Mac 实体音源名称与完整文字、恢复 TAB5 收音后两段连续追加提交、Mac 独立豆包输入恢复均已通过；用户随后明确确认回填光标在文字最后。原 TAB5／Mac 默认输入偏好已恢复，桥接后台运行。R34 Release 及 28,742 项隔离检查通过，完整 XCTest 仍因本机缺模块失败。

录音期间更换题卡的保护已实测通过：旧题开始录音，电脑提交并换新题卡后才在 TAB5 结束录音；用户确认旧文字留在预览，提示“识别文字已保留在预览，可重新录音”，新题没有旧文字。该提示是预期保护，不是发送误报。

空闲发送已获用户明确确认。用户将 Mac Codex“跟进处理方式”从“排队”改为“引导”后，运行中 TAB5 单次发送实测通过：消息直接到达当前运行，用户明确确认无需电脑再次操作且 TAB5 显示发送成功。此前排队模式下的误报正在 R35 源码修复，实机验收按用户要求留到 2026-10-30；当前安装仍为 R34，并按用户选择使用引导模式。连接中断恢复没有已知故障，仅留作日常使用观察，不再安排专项验收；DJI 当时未连接。正常单次／两段发送、多题操作通过不扩大为这些范围全部通过。

English: installed R34/.292 retains accepted staging, actual submission with correct status, clear/final words/cancel-re-record and the two-question flow. Mac source naming/full text, restored TAB5 two-take append, ordinary Mac Doubao restoration and explicit end-caret placement pass. Recording across question-card replacement also passes: after stopping, the old transcript remains only in preview with a re-record hint and does not enter the new question. That rejection is expected protection. Original preferences are restored and the bridge stays in the background. R34 Release and 28,742 isolated checks pass; full XCTest remains unavailable. Idle submission is accepted. After the user changed Mac Codex follow-up behavior from Queue to Steer, active TAB5 submission passed: the message reached the current run and the user explicitly confirmed successful TAB5 status without a second desktop action. R35 source fixes the earlier Queue-mode false unconfirmed status; hardware acceptance is deferred to October 30, and installed R34 keeps the requested Steer preference. Connection interruption is not a known defect and is left to ordinary-use observation; DJI was not connected. Historical labels below do not override later feedback.

## 语音收音设备与无框输入

- Mac 设备中心 → TAB5 → 语音设置可保存“TAB5 内置麦克风”或“Mac 麦克风”，下一段录音生效，活动录音不切换。未保存选择的旧安装继续使用请求指定的音源。不可用设备明确报错，不静默换麦克风。
- Mac 模式可选择实体音频端点；名称来自实际开启的输入设备，区分 Mac 与 DJI。TAB5 模式通过设备音频块收音，豆包转送仍需 VB-CABLE Mac。沿用已有 `source`、`sourceName` 字段。
- 用户确认 TAB5 内置麦克风经豆包识别后文字完整返回；实际诊断确认 34 块、6.8 秒设备音频，输入 IO、Fn 释放和最终文字提交均已确认。
- 常规设备录音采用透明无边框的原生输入窗口，设置里的手动测试仍显示编辑器。保留首响应者、输入法、锁屏及焦点离开检查。用户确认无桥接窗口且文字完整返回 TAB5；Mac 不再另行显示隐藏编辑器中的文字。
- 用户发现豆包设置页遗留，R3 补充临时设置窗口清理、后台复用及原应用焦点恢复。用户原本打开的设置窗口保留。R4～R6 保留这些修复，用户随后仍报告“Mac页还是显示豆包输入法设置页”，设置页清理未通过。R7 改用 WindowServer 可见性区分已关闭的 AX 残留窗口，临时窗口通过 AX 关闭按钮关闭并回读；结束时不将豆包设置进程当作焦点恢复目标。保留真正预先可见的用户设置窗口，签名／来源验证不变。R7 最终 Release、3118 项隔离检查（新增 9 项可见性／焦点回归）、签名和 helper 校验通过，已正常安装至原路径，USB／Wi-Fi／BLE 恢复。R6 传输源文件逐字节一致；本次未重复刷机。用户确认 R7 设置页不再遗留、文字完整返回，但结束时仍闪现设置页后消失。原因是关闭窗口后恢复原麦克风需要重新打开。R8 改为先隐藏并确认、保留后台 AX 控件，仅在隐藏失败时关闭；恢复时已在语音页则不重复点击导航。R8 Release、3118 项隔离检查及签名通过并安装；用户确认文字完整返回，但开始与结束仍闪现设置页，实机闪现验收失败。用户明确要求每段录音后恢复 Mac 麦克风，避免影响 Mac 上独立使用豆包。

## Mac 验证与部署

R3 Release 构建、3083 项隔离检查（含 49 项收音设备检查）、原生界面检查、固定签名和未更改的 Keychain helper 校验通过。原生检查包括透明无框输入窗口的首响应者和文字输入、可见测试窗口、两种收音设备和控件启用状态。完整 XCTest 仍因本机 Command Line Tools 缺少 XCTest 模块无法执行，不记为通过。

隔离源码排除四个原有的刷机／设备窗口未提交改动。各次正常退出后保留完整旧包备份，并安装回原 `artifacts/mac-deepseek-separated-r36-complete-20261026/AIBotBridge.app`；安装二进制哈希、唯一实例、USB 数据及 Wi-Fi／BLE 认证已核验。当前收音选择 `tab5`、识别方式 `doubao` 保留。

## TAB5 .284 候选

用户提交的 .283 完整归档来自 `b4445622c073f0785299d27982bc8204d372d22b`，归档 SHA-256 `0750f5f1dd9d77165dc45f88b70070a434c44b582ecc42e4db9967186e092898`。1097 个归档文件、7542 个完整构建输入、9 个 SDK 修改文件校验通过。只在独立工作副本修改，冻结归档不变。[范围补丁](../proposals/tab5-voice-source-label.patch)及[来源摘要](../proposals/tab5-voice-source-label.json)记录完整差异。

- Codex 录音提示分行显示状态／计时与实际收音设备，包含 TAB5、Mac、DJI 和识别方式。
- 仅总览 Codex 卡片改为“周额度已用”，仅模型额度 Codex 页改为“周额度已使用”。其他模型原文案和计算方式不变。
- ESP-IDF 完整构建、板级配置一致、固件源构建后不变、镜像和说明校验通过。实际 LVGL 检查涵盖三个音源、名称缺失回退、静音／准备、文字更新、25 个停止按钮命中点及待启动行为；已检查 Codex 两页和 Claude 未改文案的渲染。
- 准备脚本生成固定 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，实际 sidecar 的分类、列表及滚动预览已检查。镜像 SHA-256 `5c6d3b2fe622a614394b84ee0182d9d88aaac260cac77a23f16d6d1d878f345d`；ELF `5eb64bc973fd21c55a797268b5f822d440a497a37bab2038f90582ae9133229d`。大小 7208928 字节，原 OTA 分区余 32 字节；未调整分区。

## 首次升级失败与传输修复

用户明确授权“刷入 .284 并验证”。第一次仅确认一次升级请求，设备开始后，用户报告前段等待及全程传输明显慢于 Windows，随后自动中断。没有自动重发旧请求；实时采样确认设备仍正常运行 .283，界面刷新及显示错误正常。刷机前读取并取消保存：亮度 79、音量 70、非静音、提醒开、轮播关、固定总览、15 秒、屏保 5 分钟及原页面顺序。

释放桥接 USB 后读取了设备保留的数值失败记录：目标 7208928 字节，写入到 835584 字节后失败；总计 210307 ms，准备分区 8257 ms、写入 1149 ms、等接收块 200434 ms、缓冲空位等待 0 ms。通道为 Wi-Fi，最终校验未开始。记录位于 `artifacts/mac-voice-source-20261028/ota-mac-slow/FAILED-284-OTA.json`。设备保持原版本不是成功验收。

对照同仓库 Windows 实现，Mac 缺少已有的 `tcp-v2`／zlib 协商、压缩范围恢复能力和 TCP no-delay 设置，且 HTTP 发送有 180 秒总截止。R4 已补齐现有能力与数值进度，缓存压缩内容以免首个请求临时压缩阻塞监听队列，保留旧接收端的限速兼容及鉴权、精确偏移和镜像验证。R4 Release、3109 项隔离检查、29 项真实 HTTP 回环及 16 项监听恢复检查通过，签名和原 helper 一致。完整 XCTest 仍因 CLT 缺模块不可执行。

解锁 Mac 后正常替换为 R4，用户已授权的同一 .284 镜像第二次升级完成，USB 核验精确 ELF／VALID 分区／三个实时界面样本通过，升级后显示设置逐项与升级前一致，取消保存未覆盖。真实保留记录：总计 130780 ms，准备 1767 ms、写入 11648 ms、等接收块 115710 ms、校验 937 ms，完整写入 7208928 字节，错误及崩溃为 0。用户指出前段仍慢、后段才提速；本轮只确认安装成功，速度问题仍未验收。

普通 Wi-Fi 状态回包也观察到大量 TCP 重传；无刷写通信测试仅测得 USB 上传 143.0／下载 146.2 KiB/s，不能当作 Wi-Fi 性能。R5 只改变 TCP 分段上限为 1200，Release 与同组隔离／HTTP 检查通过后部署对照。普通状态仍大量重传，方案未通过，不能据此确认 MTU 根因。R6 移除该上限，改为从首字节开始限制应用层连续提交突发（1024 字节／2 ms），保留原有旧 OTA 节奏。Release、3109 项隔离、29 项真实 HTTP 完整镜像／鉴权、16 项监听恢复检查和签名通过，已正常替换并后台运行。6 MiB 原始回环完整接收约 18.5 秒；这是有意控制突发后的测试结果，不是实机 OTA 用时。

同一设备普通状态采样：R4 共 7 条连接、258084 发送字节中 167220 重传（64.8%）；仅改 MSS 的 R5 为 60.8%；R6 共 15 条连接、176395 发送字节、0 重传。R6 真实 RAM 测速三轮每轮 256 KiB：Wi-Fi 上传 0.76／0.79／0.79 秒，下载 1.49／2.95／1.67 秒，全部内容校验通过且错误 0；USB 六项同时通过。第一轮已有正常吞吐，未复现此前开头长时间停滞。对照支持应用层突发发送是当前 Mac 链路重传的直接触发因素；不能把未抓包确认的底层丢包位置宣称为已定位。

完整 .284 已在 R4 安装且启动／展示设置核验通过，R6 未再刷写同一镜像；下一次固件升级仍需记录完整 OTA 的首块与总耗时。语音设置仍为 TAB5 收音＋豆包，设置页闪现状态见上文及 R9 记录。未修改系统网络设置或重新刷写固件。

证据目录：`artifacts/mac-voice-source-20261028/`；`frameless-r2/`、`frameless-r3/`、`ota-r4/` 分别保留各阶段源码摘要、日志、包与备份；`tab5-284/` 包含固件构建、原生预览、授权及安装状态。未推送、打标签或发布。

## English

The Mac voice settings persist a TAB5/Mac capture choice for the next take and report the confirmed physical input endpoint through existing protocol fields. The user accepted full TAB5-microphone transcription through Doubao; diagnostics independently confirmed 34 audio blocks and 6.8 seconds. Ordinary recording uses a transparent borderless native input host; the manual settings test stays visible. The user accepted the hidden bridge host and complete TAB5 text return, but reported a leftover Doubao settings page. R3 adds temporary-settings cleanup and restores the app that was active before routing; its physical cleanup acceptance failed when the user reported the settings page remains visible on R6. R7 checks real on-screen visibility, presses the verified temporary window’s AX close button and verifies disappearance; it also excludes the settings process from final focus restoration. Truly pre-existing visible settings windows are preserved. R7 final Release, 3118 isolated checks (nine new visibility/focus regressions), signing and unchanged-helper verification pass. It is normally installed at the original path with USB/Wi-Fi/BLE restored. R6 transport sources match byte for byte; no additional flash was performed. The user accepts R7 with no leftover settings page and complete text return, but reports a settings-page flash at stop. Closing after acquisition forces reopening for microphone restoration. R8 hides/verifies and retains reusable background controls, closing only on hide failure; it skips redundant voice-pane navigation during restore. R8 passes Release, 3118 isolated checks, signing and unchanged-helper verification; it is normally installed with USB/Wi-Fi/BLE restored. The user confirms complete text but settings flashes at both start and stop, so R8 physical flash acceptance failed. The user requires microphone restoration after every take to preserve standalone Mac Doubao use.

R3 passes Release build, 3083 isolated checks, native UI checks, fixed signature and unchanged helper verification. Full XCTest remains unavailable in the installed CLT. Isolated bundles exclude four unrelated existing window edits and are deployed at the original path with complete backups and verified binary identity.

The .284 candidate is based on the verified .283 archive at b444562. It displays actual capture devices and changes only the two requested Codex weekly-quota captions. Full firmware build, identical board configuration, scoped source provenance, production package validation, real LVGL recording/quota fixtures and the actual sidecar preview pass. The unchanged OTA partition has 32 bytes of headroom. Local latest is not proof of installation or a public release.

The user authorized .284 installation. The first request was acknowledged but the Wi-Fi transfer failed after 835584 of 7208928 bytes; the device continues running .283. Retained timings show 210307 ms total, 8257 ms partition preparation, 1149 ms writing, 200434 ms waiting for data and no final verification. No old request was automatically replayed. R4 implements the existing Windows-compatible compression/resume negotiation, TCP no-delay, bounded deadline and numeric progress. Release, 3109 isolated, 29 HTTP transfer and 16 listener recovery checks pass. The second authorized attempt installs the exact .284 image and passes USB exact-ELF/VALID/three-sample verification with unchanged display settings. Retained timing: 130780 ms total, 1767 ms preparation, 11648 ms writing, 115710 ms data wait and 937 ms verification, zero final error/crash. The user still reports a slow beginning followed by faster transfer; speed remains unaccepted. Normal Wi-Fi status traffic also shows high TCP retransmission. R5 caps only TCP segment size at 1200; build/checks pass but real normal-status retransmission remains high, so the candidate fails performance acceptance and does not establish an MTU cause. R6 removes that cap and bounds application-level writes from the first byte (1024 bytes per 2 ms), preserving legacy OTA pacing. R6 passes Release, 3109 isolated checks, 29 full-image/authenticated HTTP checks, 16 listener recovery checks and signing; it is installed in the original app path. Normal status observations change from 64.8% retransmitted bytes on R4 (60.8% on MSS-only R5) to zero across 15 R6 connections. Three real 256 KiB Wi-Fi uploads take 0.76/0.79/0.79 seconds and downloads 1.49/2.95/1.67 seconds, all integrity-checked with zero errors; six USB checks also pass. The first download already has normal throughput. This controlled comparison supports application-level send bursts as the direct trigger on the current link, without claiming a packet-level location for the underlying loss. The already-installed exact .284 image was not flashed again; complete R6 OTA timing awaits the next authorized firmware. System network settings remain unchanged. Voice source/provider remain TAB5/Doubao; settings-flash acceptance is tracked in the R9 record below. No push, tag or public release has occurred.

## R9：参考 Windows 后台切换 / Windows-inspired background routing

Windows 的现有实现使用 `settings-rpc` 读取、临时更新和恢复 `voice.selectedMicrophoneId`，不操作设置窗口。Mac R9 独立实现对应本机通知方式：录音前只读原选择、先保存恢复记录，再发送安装版设置页实际使用的麦克风通知；结束或取消时发送原选择，期间用户手动修改则保留新选择。系统默认输入和豆包持久化设置不写入；常规路径不打开设置窗口。旧版未完成恢复记录仍有 AX 恢复路径。

原选择读取仅解析 MMKV 设置映射中的 `d` 字段及 `selectedMicrophoneId`，有大小、版本、CRC、一致快照和 Foundation 安全解码限制。独立格式实现参考 [MMKV IO](https://github.com/Tencent/MMKV/blob/master/Core/MMKV_IO.cpp)、[MMKV 元数据](https://github.com/Tencent/MMKV/blob/master/Core/MMKVMetaInfo.hpp)；本机仅观测麦克风通知名称与字段，未复制或分发豆包代码。失败时不猜测原设备；启动后沿用实际 CoreAudio 输入确认。通知没有确认回包，不能把“已发送恢复请求”记为已实测恢复。

R9 Release、3139 项隔离检查（新增 21 项读取边界）、实时原设备只读校验、签名及原 helper 校验通过，已安装到固定路径，USB／Wi-Fi／BLE 恢复。用户实测临时路由、不闪现、文字回传及 Mac 独立麦克风恢复尚待反馈。完整 XCTest 仍不可用。

Windows uses settings RPC to read, temporarily change and restore the microphone without opening settings. The independent Mac candidate sends the microphone notification observed from the installed settings app, after saving a restoration journal and reading the original preference. Each take restores the current saved choice, including a newer manual selection. It writes neither the system default nor vendor preferences; legacy AX recovery remains only for an old journal.

The bounded read-only MMKV decoder follows the linked upstream format, validates version/CRC/stable metadata and uses secure Foundation decoding. No vendor code is copied or distributed. Existing CoreAudio input verification remains mandatory. Notification delivery has no acknowledgement, so a restoration request is not proof of restoration. R9 passes Release, 3139 isolated checks (21 new decoder checks), live preference reading, signing and unchanged-helper verification. It is installed at the fixed path with USB/Wi-Fi/BLE restored; actual routing, flash removal, complete transcription and standalone Mac recovery remain pending. XCTest remains unavailable.

## TAB5 .285：清空后先刷新 / Refresh before draft persistence

在 .284 独立源码上，仅在清空文字之后、同步 NVS 检查点之前提交 LVGL 刷新，避免保存等待期间旧像素仍显示。两个原有助手函数禁止重复内联以满足原分区容量，不改分区或全局编译优化。16 组长短文字、语音／手动来源、键盘开关及光标位置的真实长按、正常定时刷新与局部帧检查通过；存储入口之前即为空。旧版相同检查残留 2273 像素。普通最终帧在旧版也通过，因此尚不能宣称完全复现用户偶发的持续残影。另验短按、松手、忙碌／发送保护、拼音、照片、草稿修订、新输入及保存失败重试。

完整 ESP-IDF 构建和原板级配置、固定路径准备脚本、生产镜像验证、真实 sidecar 两页预览通过。镜像 `2ab8ba65650e69fa20742272f2aaf7158081f401e7360627ec2c5bcbb048f03e`，ELF `81c42b4851894a9c37e76e7bdc9db31c55bacd830b055879945e4c7872d1e820`，7208928 字节、原分区余 32 字节。用户随后回复“确认”，授权该精确 .285 镜像；已安装，精确 ELF、VALID 分区和三个实时样本通过。首次核验未确认，启动稳定后再次只读核验通过。设置前后逐项一致；用户不在设备旁，清空残影视觉验收延期。[补丁](../proposals/tab5-clear-render.patch)与[来源记录](../proposals/tab5-clear-render.json)已保存。

The .285 candidate refreshes the empty textarea before synchronous NVS persistence. Two existing helpers avoid repeated inlining to fit the unchanged partition. Sixteen real long-press/normal-timer/partial-flush cases show an empty frame at storage entry, where the baseline retains 2273 pixels. Baseline eventual frames also pass, so the persistent intermittent symptom is not claimed fully reproduced. Input guards, IME/photos, revisions, retyping and failed-save retry checks pass. Full firmware build, unchanged configuration, production package and actual notes previews pass. The exact hashes above identify the 7208928-byte image with 32-byte partition headroom. The user subsequently confirmed the exact .285 candidate. Installation, exact ELF/VALID/three-sample verification and unchanged settings pass. The first early boot check was unconfirmed; a later read-only stable check passed. Physical visual acceptance is deferred because the user is not near the device.

## .285 实机升级与 R10 传输修正 / Physical .285 upgrade and R10 routing

R9（保留 R6 节奏）的完整 Wi-Fi 长流性能验收失败：前段持续重传，360 秒服务器超时后，设备在同一次请求、同一镜像与写入状态内自动转入既有鉴权分块续传，最终安装成功。未重复发起升级、未重刷同版本。设备保留记录：总计 482285 ms，准备 8240 ms、写入 10368 ms、等待数据 461807 ms、校验 1339 ms，全部 7208928 字节写入，最终错误与崩溃均为 0。普通状态及 256 KiB RAM 测速的通过不能代替完整 OTA 验收，也不能证明底层丢包原因已经定位。

R10 仅针对身份对应、已确认 .257 及更新的 0.2.x 固件、已有压缩协商请求，在身份与镜像校验后直接使用接收端既有的初始 HTTP 503 → 鉴权分块路径，不先等待长连接超时。旧版、未知固件、其他设备记录及未协商请求仍走原路径；会话、offer、偏移、分块解压、完整 SHA 与启动校验保留。没有新增协议字段或修改 Windows／固件接收行为。R10 Release、3148 项隔离检查、47 项真实 HTTP 回环检查（含 6 MiB 完整鉴权分块解压与字节一致、身份／版本门槛、非法 offer／范围拒绝）、签名及未改 helper 校验通过，已安装回固定路径。回环数据为合成夹具，不能作为实机速度。下一次不同版本的真实升级才能核对新策略的完整用时。

The full R9/R6 Wi-Fi stream fails performance acceptance. After the server's 360-second timeout, the existing receiver resumes authenticated ranges within the same request/image/write state and successfully installs .285. No repeated request or same-version flash was made. Retained device timing: 482285 ms total, 8240 ms preparation, 10368 ms writing, 461807 ms waiting for data and 1339 ms verification; all 7208928 bytes are written with zero final error/crash. Normal status and RAM benchmark success do not validate full OTA or identify the packet-loss location.

R10 routes only an identity-matched, confirmed .257+ 0.2.x peer with existing compression negotiation directly into the receiver's existing initial-503/authenticated-range path, after authentication and exact-image validation. Unknown/older/unnegotiated peers retain streaming; foreign device metadata cannot enable routing. Session, offer, offsets, compression, full-image SHA and boot checks remain. No wire fields or Windows/firmware receiver behavior change. R10 passes Release, 3148 isolated checks, 47 real HTTP loopback checks (including full 6 MiB authenticated range reconstruction, identity/version gating and invalid offer/range rejection), signing and unchanged-helper verification; it is installed at the fixed path. Synthetic loopback timing is not physical performance. Complete hardware timing requires the next different-version update.

## R11–R13：同镜像重装、自动检测与启动核验

R11 已在 TAB5 .286 完成 Wi-Fi 首次安装与完全相同镜像重装，设备记录安装耗时分别为 85848 ms／68243 ms；后者分区由 ota_0 切至 ota_1，精确 ELF、VALID 与三次实时 UI 样本通过。安装耗时不含成功画面驻留、重启及设备自身连续 30 秒健康启动窗口。此前 .285 完整流再恢复分块的 482285 ms 不能继续作为直接分块的速度。USB 状态在线不表示固件通过 USB 传输：自动 OTA 当前优先可用 Wi-Fi。

R12 移除“检查豆包”按钮，打开语音页、展开识别选择和选择前自动检测，语音窗口可见时每 5 秒刷新。未安装项禁用并显示必要安装入口；安装成功后自动恢复可选。3161 项隔离检查与原生窗口检查通过，未伪装卸载用户实际输入法。

R13 接入升级后的自动启动核验，最后一块提供后等待设备健康启动，依可用 USB／Wi-Fi／BLE 请求原协议的精确 ELF、VALID 备用分区和三个独立实时 UI 样本。自动检查额外要求本次升级开始后发生重启，防止同版本升级前的旧心跳被当作完成。最多 6 次只读检查，不自动重发升级；超时不显示成功。完整构建与实机结果待补。开发用显式通道验收命令仅复用生产服务，串口与 HTTP 服务按测试通道单独启动；不修改设备网络／配对／展示设置。

TAB5 .287 候选只让成功的 Wi-Fi 分块保留 TCP 连接，失败时仍关闭并在原偏移按既有预算恢复；worker 退出后清理连接。320 个实际 OTA worker 故障场景、34 个续传场景及成功复用／失败关闭断言通过 UBSan；P4 构建、原板级配置和实际 sidecar 预览通过。性能提升仍待同设备实测，未预先记通过。

R11 completed both the initial .286 Wi-Fi install (85.848 s) and an identical-image reinstall (68.243 s). The latter switched ota_0 to ota_1 and passed exact ELF, VALID partition and three live samples. These device installation timings exclude reboot and the mandatory 30-second healthy-boot window. R12 removes manual Doubao detection and automatically disables unavailable providers. R13 adds bounded automatic boot verification using existing USB/Wi-Fi/BLE messages and rejects pre-upgrade uptime; it never retries installation automatically. The .287 candidate retains successful range connections and reconnects after errors. Native fault/recovery tests and the P4 build pass; live performance remains pending.

实机追记：仅启动 Wi-Fi 服务、未启动 USB transport 时，.286 启动核验 6.228 秒通过（`ota-r13/WIFI-BOOT.json`），设置读回一致。.287 通过 USB 完整安装，设备阶段记录 transport=0、安装 38793 ms、擦除 8261 ms、写入 9744 ms、等接收 19445 ms、校验 932 ms；自动核验第二次通过，当前 ota_0／VALID／精确 ELF，展示设置一致。该运行器在 USB 隔离时未提供 LAN endpoint，既有 USB host-claim 语义将保存地址清空，导致下一次 Wi-Fi 测试在写入前无法发现；已由正常桥接自动认证恢复地址并确认 Wi-Fi 认证请求。R14 验收程序改为保留正常 endpoint，并启动无线发现，避免该测试副作用；产品正常桥接流程未变更。

Live follow-up: Wi-Fi-only boot verification passes in 6.228 seconds without starting the USB transport. The .287 USB installation records 38.793 seconds and passes the second automatic boot check with unchanged display preferences. The initial isolated USB runner supplied an empty LAN endpoint, which the existing host-claim semantics persisted; the next Wi-Fi preflight failed before writing. Normal bridge authentication restored the endpoint and Wi-Fi authenticated traffic. R14 fixes the acceptance runner to retain the endpoint and include wireless discovery; normal application behavior is unchanged.


## .287／.288 实测与 Windows 对照 / Measurements and Windows comparison

R14 保留自动豆包检测、自动精确启动核验及后台语音，实现通过 Release、3207 项隔离、47 项 HTTP、原生界面和签名检查。实机 .287 同镜像 Wi-Fi 重装：安装 25578 ms，擦除 8237 ms、写入 11095 ms、等数据 4823 ms、镜像校验 944 ms；分区切换、自动精确启动核验与设置前后一致。相对 .286 同镜像 68243 ms，连接复用消除了多数重复握手等待。

按用户要求，.288 将首轮整分区擦除改为先擦 64 KiB，再逐段擦除并写入；独立接收任务同时继续接收。相同 Flash 上的擦除与写入仍按顺序执行，接收与这两项工作重叠。SDK 备用分区、回滚前提、范围、原始镜像 SHA、esp_ota_end 和启动分区设置均保留。840 项实际 worker 边界／故障检查（含 16 项首次写入早于完整接收与整区擦除）、34 项恢复检查、原板级配置 P4 构建和实际 sidecar 预览通过。

.288 已安装，镜像 SHA `7b6a2e30956fddde7f774de2f3d45f060d95a32d6b1b07eefeb82e63c65176d0`，精确 ELF `8fb43ec9296abb22845bf92477aa9c661f9068e9fde2632316a542ffe7b115a4`，7207488 字节。R14 同镜像 Wi-Fi 分块实测安装 25921 ms，累计分段擦除 9181 ms、写入 11137 ms、等数据 4166 ms、校验 934 ms；自动启动核验、VALID 分区和设置保留通过。首段等待被移入接收过程，但总时间尚未进一步下降，不能将并行改造本身宣称为总耗时提速。

Windows `Tab5OtaFlow` 对 tcp-v2 使用 16 KiB 连续发送；历史 .197 验收记录传输 13563 ms、设备安装 27724 ms，且镜像大小不同。Mac R15 按相同发送块大小进行 .288 真实连续流实验，TCP 数值采样约九成为重传，360 秒截止后才恢复分块，最终安装 415219 ms，精确自动启动核验通过。R16 将 TCP MSS 改为 1024 并缩短实验流截止后仍无改善，接收至 6144000 字节失败（153424 ms），原 ota_0／VALID／精确 ELF 保留，无崩溃。两实验均未部署为正常桥接；工作区撤回这些流／MSS改动。结果仅证明这些方案不适合当前链路，尚未定位底层丢包点。

R14 passes Release, 3207 isolated, 47 HTTP, native UI and signing checks. The .287 identical-image Wi-Fi install takes 25.578 s, versus .286's 68.243 s, with automatic exact boot and unchanged settings. Firmware .288 starts with a 64 KiB erase and interleaves further erases/writes with independent reception. Erase and write remain ordered on the same flash chip. All integrity, partition and rollback checks remain; 840 worker cases, including 16 proven overlaps, 34 recovery checks, full P4 build and actual sidecar preview pass. The exact .288 image above installs and reinstalls with automatic exact boot and preserved settings. Its R14 Wi-Fi range install takes 25.921 s; the initial full-erase pause is removed, but total time has not improved further.

Historical Windows .197 records 13.563 s transfer and 27.724 s device installation for a different-sized image. Mirroring its 16 KiB continuous sends in R15 produces heavy real-device retransmission and a 415.219 s recovered installation. R16's 1024-byte MSS experiment still fails, aborting after 6144000 bytes in 153.424 s while the original VALID partition stays healthy. Neither experiment is deployed; their stream/MSS source changes are withdrawn. The underlying packet-loss location is not claimed resolved. Evidence is retained under `tab5-287/`, `tab5-288/`, `ota-r14/`, `ota-r15/` and `ota-r16/`.


R17 只对通过鉴权、有效会话与范围检查且成功生成的固件分块，将发送节奏由 1024 字节／2 ms 调为 1024 字节／1 ms，其他 RPC 保持原节奏。Release、3207 项隔离、54 项 HTTP／准入检查和原生界面通过，但真实 .288 在 4521984 字节中止（27296 ms，未进入镜像校验，未改启动分区，无崩溃）。因此该改动也未部署，工作区恢复与正常安装的 R14 对应源码。缩短发送间隔和减小 MSS 均不能作为当前设备的安全提速手段；原分块连接复用与 .288 固件流水处理保留。

R17 changes pacing only for authenticated, session-valid, successful bounded firmware ranges from 1 KiB/2 ms to 1 KiB/1 ms. Release, 3207 isolated checks, 54 HTTP/admission checks and native UI pass, but the physical transfer aborts at 4521984 bytes in 27.296 s before image verification. The active boot partition stays unchanged with no crash. R17 is rejected and its workspace changes are withdrawn; stable installed R14 sources, range connection reuse and the .288 firmware pipeline remain.


最终回归：恢复 R14 原有节奏后，同一份 .288 Wi-Fi 完整重装再次通过。设备安装 28248 ms（擦除 9144、写入 11316、等数据 6367、镜像校验 940）；由 ota_0 切至 ota_1，精确 ELF／VALID／三个实时样本自动核验通过，设置前后完全一致。从发起至健康启动核验完成 90.6 秒，其中包含重启及设备自身健康观察，不计作传输速度。最后两轮稳定策略的完整安装为 25.921／28.248 秒，尚不能报告十几秒完整安装。

Final regression: after restoring R14 pacing, an identical .288 Wi-Fi reinstall passes again in 28.248 s (9.144 erase, 11.316 write, 6.367 data wait, 0.940 verification). The partition changes from ota_0 to ota_1; automatic exact ELF/VALID/three-live-sample checks and unchanged settings pass. Request-to-healthy-boot verification is 90.6 s, including reboot and the device's health window. Stable installation measurements are 25.921/28.248 s, not a claim of a ten-second complete installation.


最终校验：完整工作区 Release 构建通过，部署候选与工作区除四个预先隔离的用户窗口改动之外源码摘要一致；已安装 R14 签名、固定 .288 镜像摘要、版本登记及补丁来源摘要通过，`git diff --check` 无错误。再次执行 `swift test` 仍因本机 CLT 缺少 XCTest 模块失败，未记通过。Windows 原生运行和 ESP8266 实机不在本次 TAB5 验收证据内。

Final validation: the full workspace Release build passes. Candidate source hashes match the workspace except the four previously isolated user window edits. Installed R14 signing, the canonical .288 hash/version, firmware provenance and `git diff --check` pass. A repeated `swift test` attempt still fails because the installed CLT lacks XCTest; it is not reported as passed. Windows-native execution and ESP8266 hardware are separate from this TAB5 acceptance.


2026-10-29 收尾：用户解锁后，已恢复固定路径 R14 正常桥接，唯一实例与已验收二进制 SHA 一致。实际设备中心显示 TAB5 .288、USB 数据已确认、Wi-Fi 已认证请求、蓝牙已认证且 USB 优先待命。检查窗口已关闭，保留后台运行；未再次刷机或修改设置。

October 29 closeout: after the user unlocked the Mac, the fixed-path R14 bridge was restored as the sole running instance with the accepted binary hash. The live device center confirms TAB5 .288, USB data, authenticated Wi-Fi requests and authenticated BLE on USB-priority standby. The inspection window is closed and the bridge remains in the background. No additional flash or preference changes.


2026-10-29 后续：R18 通过系统 socket 修复 Mac 连续传输路径，相同 .288 Wi-Fi 重装安装 21.666 秒、等待数据 32 ms。已正常部署并恢复后台运行；详见 [TCP 根因对照及验收](MAC-TAB5-OTA-TCP-20261029.md)。此前 R14 为历史稳定检查点，当前部署以 R18 为准。

October 29 follow-up: native-socket R18 is deployed in the background. An identical .288 Wi-Fi reinstall takes 21.666 s with 32 ms data wait; the linked TCP comparison documents exact boot, settings and validation. R14 remains a historical checkpoint.


## R22：豆包实际麦克风不符 / Wrong actual microphone

2026-10-29，用户报告 TAB5 长时间显示准备，最后提示“豆包未确认实际收音”。R21 正常桥接收到每段 23 块／4.6 秒 TAB5 探测音频，但从未确认目标输入。独立只读 CoreAudio 观测在 05:39:07–05:39:15 UTC 及第二次 05:41:02–05:41:06 UTC 均显示豆包 `runningInput=1`，实际设备为 `BuiltInMicrophoneDevice`，而所选 TAB5 模式要求 VB-CABLE。Fn 与收音权限在这两次能启动输入，不能继续把问题笼统归给快捷键或权限。

第二次发送与官方设置页观测完全一致、没有 `targetBundlePath` 的通知仍未改变实际输入。官方设置页切换则使只读设置值在 VB-CABLE 与 BuiltInMicrophoneDevice 之间相应改变；检查结束已恢复后者。现有通知不能当作成功的选择命令，R9 的实际临时路由验收失败。观测只记录输入设备和运行状态，不采集音频、转写或账号设置。证据在本地 `artifacts/mac-voice-source-20261029/voice-r22/`。

R22 候选恢复官方设置控件切换，按 UID 读回确认，先持久化恢复记录；恢复时确认输入停止、原临时选择未被用户改动，并读回原 UID。输入不符单独报错，不再误归为 Fn／权限。准备探测静音不进入正式录音的静音提示计数。保留透明草稿窗口和 R21 BLE 修复；不刷固件，不变更 TAB5 展示设置。**官方设置页仍可能闪现，无弹窗切换尚未完成；构建和设备验证结果后续补记。**

The user reported preparation without a ready cue followed by the unconfirmed-input error. Two independent CoreAudio observations confirm that Doubao opens the built-in Mac microphone instead of the VB-CABLE route required for TAB5 audio. An exact copy of the observed selection notification also fails to change the active route; the official setting control changes the saved selection successfully and was restored afterward. R9's notification-only routing fails hardware acceptance. R22 uses the official control with UID readback and journaled restoration, reports wrong input explicitly, and excludes preparation silence from the ready cue. The bridge draft remains invisible; the vendor settings window may still appear. Build and hardware acceptance are not yet recorded as passed.

R22 候选和当前工作区 Release 构建分别完成；28465 项隔离检查通过，含准备静音不得覆盖首次 ready 提示。签名、包内未改变的 Keychain helper 和安装二进制哈希核验通过。R22 已安装到原固定路径，唯一正常桥接进程，USB／Wi-Fi／BLE 认证恢复，识别方式和 TAB5 麦克风选择保留。.291 固件未变。完整 XCTest 因当前 CLT 缺失模块失败，不记为通过；设备语音验收仍在进行。

Both the frozen candidate and workspace Release builds pass, as do 28465 isolated checks, signing and unchanged Keychain-helper validation. R22 is installed at the original app path; authenticated device connections and voice preferences are preserved. XCTest remains unavailable in this CLT installation. Real voice acceptance is pending.

R22 部分实机结果：CoreAudio 已在实际录音阶段确认 VB-CABLE 输入；设置读回和桥接诊断均确认结束后恢复 BuiltInMicrophoneDevice。准备到实际 IO 开启约 2.85–2.96 秒。最近四段诊断为三段无最终文字、一段取消，不能把完整识别回传或 TAB5 屏幕 ready 提示记为通过；已请用户反馈。设置页闪现仍未解决。详情见本地 R22 `ACCEPTANCE.json`，桥接已恢复后台，未更改固件及展示设置。

Partial hardware acceptance: CoreAudio confirms the VB-CABLE input during recording, and both settings readback and bridge diagnostics confirm restoration to the built-in microphone. Preparation to actual input takes approximately 2.85–2.96 seconds. Three observed takes have no final text and one was cancelled; complete transcription and the physical TAB5 ready cue remain unconfirmed. Vendor-settings flashing remains unresolved.


## R23：录音前的后台读取阻塞 / Background reads before voice

用户继续报告“等待当前读取结束”后又进入准备，且设置页出现。源码确认 .291 UI 把录音排在当前回复读取／草稿同步后；Mac USB 虽以独立线程计算 RPC，仍等待这个结果后才处理下一邮箱请求。`MacTab5Codex.handle` 会等待 catalog gate，再读取桌面和历史，慢读取占用邮箱。语音已开始后的优先级不能解决点击之前的占用。

R23 候选只对鉴权后的 `codex/read` 使用单个后台读取和单项内存缓存，首次／缺少匹配缓存沿用 429 稍后重试语义；以 device、session、task、page、turn、offset 隔离结果。近期成功页可在刷新时保留，最多 15 秒；错误不立即清空成功页，过期后传递失败。发送、追加、停止、草稿保存、已读回执保持原路径，不在后台重放。既有 .291 已按 429 重试，本改动不增加请求或响应字段，不改 Windows 请求格式。增加真实阻塞读仍立即返回的检查，及去重、缓存隔离、失效／时钟回退检查。

另增加麦克风准备各阶段的数字计时；未宣称恢复无弹窗。安装版 Mac 豆包 1.0.3 未发现可验证的等价后台设置入口，通知-only 已实测失效，不能称为接口切换完成。官方设置控件路径暂留，仍可能弹出；本轮先验证读取队列改善，不刷固件。

The device waits for an active read before starting voice; the Mac USB owner waits for a slow Codex handler even though computation runs off its serial queue. R23 moves only authenticated read operations into one bounded background job with a single result cache keyed to the exact device/session/task/page/turn/offset. Existing 429 retry handling remains compatible; mutations, draft saves and read receipts remain unchanged. Tests cover a truly blocked background read, coalescing, owner isolation, expiration and clock rollback. Microphone timing is split into stages. A verified headless Mac microphone-setting interface remains unavailable; vendor-window appearance is unresolved. Build and hardware results will be recorded separately.

R23 同时增加 TAB5 启动音频暂存：首块有效 PCM 被桥接接收后即能回复收音就绪，最多暂存 8 秒，实际豆包输入确认前不转送。与现有 Windows 的“实际 TAB5 PCM＋音频接收端可用”以及 .291 的本机音频 ACK 门槛相容。提前结束保留音频直到转送后才结束识别器；取消／失败／溢出／提前最终结果都禁止插入，提前停止后准备另有 10 秒上限。调试隔离检查 28486 项通过，发布构建和硬件验收另记。该改动减少用户开始说话前的等待，但不会消除豆包设置页，准备成本可能移到结束后的识别等待。

R23 also retains initial TAB5 PCM, announces capture after the first acknowledged real audio block and forwards only after verified Doubao IO. The bounded eight-second queue supports early stop, discards cancelled/failed/overflowed takes and rejects a final result before forwarding completes; stopped preparation has a ten-second deadline. The debug isolated suite passes 28486 checks. This improves when speech can begin; it does not remove the settings window and can move preparation latency to final recognition.

R23 实机已生成最终文字，原麦克风读回 BuiltInMicrophoneDevice。用户反馈结束后等待仍长。当前单段保留计时：收音就绪应答 503 ms；麦克风选择读回 1835 ms，准备完成 2592 ms，Fn 按下 3490 ms，实际 IO 开启 3692 ms；结束请求时待播 2140 ms，Fn 释放 6393 ms，IO 停止 6593 ms，提交文字 6843 ms，最终确认 7491 ms。停止到最终识别 5103 ms，最终结果到响应生成 393 ms。11 块／2.1 秒音频完整性仍待用户确认，不把本机最终提交等同于屏幕完整验收。

## R24：准备与最终回包延迟 / Preparation and final-response latency

候选合并 AX 属性读取、复用同次操作已定位的选择控件，并以 UID 读回代替重复完整控件遍历。隐藏成功后不再重复枚举窗口。输入法选择后按真实安全焦点和连续空闲状态启动，去掉固定 400 ms 等待，保持错误设备拒绝和取消检查。USB／BLE 在 recognizing 阶段的查询最多等 350 ms，最终回调可唤醒当前响应；不持有语音状态锁，不得把旧 voice／generation 结果交给新录音。保留播放速度、尾部排空及 600 ms 最终文本稳定条件。调试隔离检查 28489 项通过，含查询被最终结果／取消唤醒和过期 owner 拒绝。发布构建和实机尚待。设置页出现仍未解决。

R23 measured 503 ms to a capture-ready response but 5103 ms from stop to final recognition, including delayed microphone preparation and 2140 ms of queued audio; final-to-response generation added 393 ms. R24 batches accessibility reads, reuses controls within each operation, checks selected UIDs directly, removes redundant visibility enumeration and replaces a fixed 400 ms input-method delay with focus/idle gating. USB/BLE final polls wait at most 350 ms and are woken by completion/cancel, with owner/generation revalidation. Audio speed, draining and the 600 ms stable-final requirement remain unchanged. Debug isolated checks pass (28489); release and hardware results remain pending. Settings-window flashing is unresolved.

R24 冻结候选与工作区 Release 构建通过，Release 隔离检查 28489 项通过，签名及原 Keychain helper 核验通过；已安装至原固定路径。当前唯一正常桥接实例，USB／Wi-Fi／BLE 认证恢复，仍为 TAB5 麦克风＋豆包，.291 固件未改动。完整 XCTest 仍受本机缺少 XCTest 模块限制，未记通过。已请用户使用同一句短句对比，上述计时从桥接收到开始请求起算，不代表手指点击到界面的总延迟。

R24 Release builds and 28489 isolated checks pass; signing and unchanged-helper checks pass. It is installed at the original path with authenticated links restored and the existing .291 firmware/voice preferences preserved. XCTest is unavailable. Same-phrase hardware comparison is pending. Recorded capture-start timings begin at the bridge start request, not the physical tap.

R24 实机用户确认文字完整返回，体感仍需三秒以上。日志：收音就绪应答 504 ms；控件就绪 838 ms，选择读回 1021 ms，准备完成 1472 ms，Fn 按下 1834 ms，实际 IO 开启 2037 ms。结束请求 5060 ms 时待播 2000 ms；Fn 释放 7391 ms，IO 停止 7641 ms，提交文字 7818 ms，最终确认 8444 ms。停止至最终识别 3384 ms（R23 为 5103 ms，减少 1719 ms／33.7%），最终结果至响应生成 0 ms（此前 393 ms）。两次分别录到 4.8／2.1 秒音频，不应把该单次对比外推为所有录音保证。原麦克风恢复读回 BuiltInMicrophoneDevice。剩余主要为约 2 秒缓存播放、约 0.33 秒尾部排空、约 0.43 秒结束／提交和约 0.63 秒最终稳定确认。设置页弹出尚未解决；已关闭检查窗口并保留桥接后台运行。

R24 hardware: the user confirms complete returned text. Stop-to-final is 3384 ms versus the prior 5103 ms, a 1719 ms (33.7%) reduction; final-to-response generation is 0 ms versus 393 ms. Capture-ready response is 504 ms. These are individual takes of different recorded durations (4.8 versus 2.1 seconds), not a general performance guarantee. The remaining delay is mainly 2 seconds of queued playback plus draining and final confirmation. The original built-in microphone is restored. Settings-window flashing remains unresolved; inspection windows are closed.

### 后台切换入口复核 / Background switching endpoint audit

2026-10-29 再次对照 Windows 的 `DoubaoIme\\settings-rpc`、`settings.getMicrophoneList` 和 `settings.update`。本机 Mac 豆包 1.0.3 的主程序与设置程序未观察到命名 Unix 控制端点或 TCP 监听；安装元数据未声明 URL scheme、Services 或 AppleScript 支持，检查的协议标识未发现 Windows 对应方法。`VoiceStateIPC` 仅为已观察到的类型名称，不能作为麦克风设置接口存在或可调用的证据。这些负面结果也不能证明所有内部接口不存在。

现有桥接的 `settings()` 在无法复用控件时以 `activates=true` 启动设置应用，`select()` 操作官方麦克风选择器，故获取和恢复均可能显示窗口。此前通知-only 的两次实际输入验证已失败；不能再以通知发送成功代替选择成功。当前没有已验证的新后台切换机制，本轮未修改或部署生产代码，未让用户重复录音。只读选择与官方页面均确认 Mac 内置麦克风，CoreAudio 输入为空闲；临时检查窗口已关闭。端点观察及安装二进制摘要保存在忽略目录 `artifacts/mac-voice-source-20261029/background-routing-audit/RESULT.json`。**设置页弹出仍未解决，不记为验收通过。**

The installed Mac 1.0.3 main and settings processes expose no named Unix control endpoint or TCP listener observed in this audit; their metadata declares no URL scheme, Services or AppleScript support. The inspected protocol labels do not contain the Windows settings RPC methods. A `VoiceStateIPC` type label alone does not establish a callable microphone mutation API, and these negative observations do not prove that every internal API is absent. The current bridge explicitly activates settings when controls cannot be reused and operates the official picker, so acquisition and restoration can both display UI. Prior notification-only hardware tests failed. No new verified mechanism was found, no production code was deployed, and no repeat recording was requested. Both preference readback and the official page confirm the built-in microphone; CoreAudio is idle and the inspection window is closed. Settings-window flashing remains unresolved.

### 保存与通知链路实验 / Store-and-notify experiment

后续互操作排查发现，安装版主程序确有读取通知 `selectedMicrophoneId` 并调用内存设置逻辑的路径，因此不能把此前通知-only 失败直接解释为“完全没有切换处理器”。独立发送 `requestMicrophoneList` 收到了主程序回应，普通后台通知通道可用。实际设置存储使用 MMKV 多进程模式；单独通知仍不改变只读持久化选择。

在忽略目录中构建了隔离实验程序，使用 [Tencent/MMKV](https://github.com/Tencent/MMKV/tree/007a393cd5e255e9aa68d6648f7179159bdf0f9d) 的官方上游 Core（2.4.3、BSD-3-Clause，保留上游全部第三方许可），通过跨进程锁读取当前字典、比较旧麦克风、仅改 `selectedMicrophoneId`，再通知豆包。没有复制豆包实现或加载其私有框架，没有直接拼接修改 MMKV 文件和 CRC，也没有启动设置页。此方法是未正式验收的本机存储兼容实验，**不是已确认的豆包公开 RPC**，项目尚未引入该生产依赖。

实验 Release 构建、真实双进程锁竞争、合成字典往返、无关 MMKV 键保留及较新手动选择保留均通过。一次本机短暂写入／恢复后，字典与原值完整相等，原选择为 BuiltInMicrophoneDevice。豆包音量监测入口仍报告 BuiltInMicrophoneDevice；该入口与真实 ASR 的路由是否相同尚未证实，不能作为切换通过的证据。随后 180 秒 TAB5 验证窗口未收到录音，已自动恢复并清除实验恢复记录。尝试自动验证输入法录音时，CUA 明确报告 Mac 锁定，已请求手动解锁。正式桥接仍为 R24；无新 ASR 实机通过结论，无固件修改。证据与实验源码在 `artifacts/mac-voice-source-20261029/background-routing-probe/`。

Further interoperability inspection identifies a notification handler that reads the microphone ID and updates in-memory settings; prior notification-only failure does not prove the handler is absent. An independently posted microphone-list request receives a response. An isolated prototype uses the pinned upstream MMKV Core in multi-process mode to compare and update only the microphone field under its lock, then notify Doubao, without opening settings. It is an unaccepted storage-compatibility experiment, not a documented Doubao RPC or a production dependency. Release build, real inter-process contention, synthetic roundtrip, unrelated-key preservation and newer manual-selection preservation pass. The live settings roundtrip restores the original dictionary exactly. The meter still reports the built-in microphone, but its equivalence to actual ASR routing is unverified. No take arrived during the bounded TAB5 test window, and automatic restoration completed with no pending journal. macOS lock prevents automatic input-method testing; manual unlock is requested. R24 remains installed, and no ASR or firmware acceptance is claimed.

## R25：后台切换与桌面已读同步 / Background routing and desktop read synchronization

接续中断会话，候选将豆包麦克风选择改为已固定上游版本 MMKV Core 的多进程锁、比较后更新及通知。仅适配经签名验证的豆包 1.0.3；不再启动设置应用补救。转送音频仍必须通过 CoreAudio 实际输入设备检查，结束／取消／失败恢复原选择，用户后来手动选择不被覆盖。语音会话及恢复期间忽略桥接重新打开事件，避免自动带出设备中心。MMKV 完整上游许可随包保留；其上游 Package.swift 要求 Swift 6.2 或更新编译器，当前并未调整 CI 主机或发布工作流。

停止请求立即使 TAB5 会话退出收音状态，不再接收后续音频。已接收的有声数据仍完整排空后向豆包结束，以避免截断句尾。空结果不应等待静音来决定停止；只有确证全数字零的流可以缩短空结果确认，普通环境底噪不能冒充无语音。尚未有供应商正式结束空结果事件的可靠观测，不能把实际 ASR 完成简单等同于按键或 IO 停止；有声／未知空结果的兼容超时仍在。整体等待改善与无弹窗必须另做实机录音，不能由编译结果宣称通过。

另定位 Mac Codex 未读问题：旧版 `repliesRead` 仅收集设备回执，从未读取桌面已读。新增本地账户／主机桶的有界元数据读取，匹配已知回复的具体轮次，等待 5 秒桌面写入稳定后提供现有 `repliesRead`；失败保留最近成功状态，新轮次及账户不得继承旧确认。状态、令牌、账户字段和原始回复不记入日志或提交。无需修改设备协议，Windows 现有等价逻辑及 TAB5 的精确轮次回执已对照。

The resumed candidate uses pinned upstream MMKV Core with an inter-process lock, compare-before-write and notification, limited to the signature-verified installed Doubao 1.0.3. It never launches vendor settings as a fallback. Actual CoreAudio input verification remains mandatory before forwarding audio; restoration preserves a newer manual selection. Reopen events are suppressed during the voice lease and restoration. Complete upstream licensing ships in the bundle. This dependency requires a Swift 6.2+ compiler; CI hosts and release workflows have not been changed.

An explicit stop immediately leaves device capture and rejects further audio. Already received voiced audio must still drain before ending the IME to preserve the final words. Verified digital-zero audio allows a shorter empty-result check; ordinary background noise is not proof of silence. A reliable vendor empty-completion event has not been established, so compatibility waits for unknown/voiced empty results remain. Build success does not establish physical latency or popup acceptance.

Mac previously collected device-local read receipts only. The new bounded local desktop metadata reader binds its account/host bucket to each exact known completed turn, allows five seconds for desktop state to settle, retains successful state on transient failures, and never carries an acknowledgement into a new turn/account. Existing wire fields and receiver behavior suffice. Account claims, tokens and raw replies are never logged. Candidate build, packaging and hardware results follow separately.

R25 已在原固定路径部署，唯一正常桥接进程；候选及工作区 Release 构建通过，28525 项隔离检查通过（包括真实 MMKV 临时目录读写、比较冲突与无关字段保留、静音结果边界、已读解析／隔离／时序及桥接快照集成）。完整 `swift test` 再次因 CLT 缺少 XCTest 模块失败，没有记为通过。上游完整许可文件逐字节一致，包签名及未改动 Keychain helper 通过；R24 完整备份保留。二进制 SHA-256：`9ec0616371e82ca3be9494d7ccc4a937dc338429919883521ea2734e969fb1a9`。

正常界面确认 .291、USB 数据、Wi-Fi 已认证请求、BLE 认证待命。检查窗口已收起。已请求用户测试“不说话后点击结束”，并同时确认弹窗与未读通知；在得到真实反馈前，不将无弹窗、最终文字完整或结束耗时记为验收通过。本轮未刷固件、未改展示设置，未推送、打标签或发布。证据：忽略目录 `artifacts/mac-voice-source-20261029/voice-r25/` 的 `DEPLOYMENT.json`、`PACKAGE.json`、构建／检查日志和 SHA256SUMS。

R25 is installed at the existing fixed path as the sole normal bridge process. Candidate and workspace Release builds pass, with 28525 isolated checks including real temporary MMKV roundtrips/conflict preservation and integrated desktop-read snapshots. Full XCTest remains unavailable in the local CLT and is not passed. The upstream license matches byte-for-byte; bundle signing and the unchanged Keychain helper pass, with a complete R24 backup. Native UI confirms .291 and authenticated USB/Wi-Fi/BLE connectivity; inspection windows are closed. Physical empty-take latency, popup behavior, complete transcription and device badge clearing remain pending user feedback. No firmware flash, display-setting change, push, tag or release occurred.

R25 实机反馈：用户确认豆包设置页和设备中心都未弹出，点击结束后约 2 秒返回。只读 CoreAudio 观测确认实际录音阶段使用 VB-CABLE、随后停止，之后另一次普通输入使用 BuiltInMicrophoneDevice。此处“约 2 秒”为用户体感，没有伪装成精确计时；本轮未确认有声短句的逐字完整性。Codex 原会话的未读提示仍未清除，未读验收失败，继续修复。

R26 后续定位：对原会话进行一次只读桌面订阅，真实 v11 快照的 `threadRuntimeStatus.type=systemError`，最新 turn 为 `failed` 且有 6 个公开 assistant 项。旧解析器仅允许 idle/active，导致该会话无法建立精确已完成轮次，R25 的同步拿不到对象。补充 systemError 的只读解析，仍禁止 send/steer/interrupt；另修复状态帧减少任务数时不应过滤掉其他任务的精确已读回执，按现有设备处理规则限为 80 项，不增加线协议字段。构建和设备验收另记。

Hardware feedback for R25: no vendor-settings or device-center popup; explicit stop returns in approximately two seconds according to the user. CoreAudio observes VB-CABLE input during the take, followed by stopped input, then a separate ordinary use of BuiltInMicrophoneDevice. Exact spoken-text completeness is not established by this empty-take check. The old chat's unread badge still remains and fails acceptance.

R26 diagnosis: the affected chat's actual v11 owner snapshot reports runtime `systemError`, with a failed latest turn containing six public assistant items. The old idle/active-only parser rejects it, preventing exact-turn read synchronization. The follow-up accepts that runtime for reading only while keeping all mutation guards closed. Exact read receipts also survive a reduced transport task catalog, bounded to 80 and using existing receiver behavior. Build and hardware results follow separately.

R26 收尾：候选和工作区 Release、28533 项隔离检查、签名／未改动 helper 通过，原路径已部署，R25 完整备份保留。二进制 SHA-256 `295955040eb09981a2b1205177082112166e0b29f022daec6eb7294263adfb5b`。USB 数据、Wi-Fi 认证与 BLE 待命恢复，检查窗口关闭。用户明确反馈原会话未读提示“已经消失”，该项实机通过。R25 的语音源码逐字节保持；无固件改动或公开发布。完整 XCTest 仍受本机 CLT 缺模块限制。

R26 closeout: both Release builds, 28533 isolated checks, signing and unchanged helper pass. Deployment at the fixed path retains a complete R25 backup; normal authenticated connections recover and inspection windows close. The user explicitly confirms that the original chat's unread badge has disappeared. R25 voice sources remain byte-identical. No firmware change or public release; full XCTest remains blocked by the local CLT installation.


## 2026-10-29 R27：直达输入框焦点与 OpenClaw 来源

用户已经先点左侧会话定位，再用豆包录入并提交，仍看到“电脑主输入框未就绪”。运行中 R26 的只读界面诊断记录：定位会话返回 200，后续拒绝原因为“当前焦点不在主输入框”；豆包本次停止至最终文字约 1.623 秒，恢复麦克风成功。定位成功并不等于主编辑器获得键盘焦点。

R27 仅在明确的 `desktop.stage-draft`、正文非空且原读取失败时，有界扫描当前前台 Codex 窗口的可信主文档，寻找唯一且通过现有任务地址／全目录唯一完整标题与主编辑器结构验证的输入框。确认后设置该输入框焦点，再重新验证并执行原有追加、单次写入与读回流程。扫描不读取文字；不切换应用、不导航、不借用旧定位回执、不自动发送；弹窗、嵌入浏览器、其他会话、多个候选、超时或不完整扫描均不能写入。清空和发送继续要求现有精确焦点及草稿凭据。

OpenClaw 的本地 App Server 会话同样标记为 vscode，单靠 sourceKinds 无法区别。近期真实条目位于 `.openclaw/workspace`，会按活动时间排到前面。R27 按规范化 cwd 的 `.openclaw` 目录组件排除这类集成工作区，也覆盖其子工程和 workshop-skills；普通 Work/OpenClaw 项目和仅在标题讨论 OpenClaw 的 Codex 会话保留。路径规则不声称覆盖未知自定义目录。列表请求失败仍保留最近成功目录；线协议、任务身份、Windows 与固件不变。

工作区 Release 和 28,564 项隔离检查通过（新增 31 项，包括先定位但无焦点后的追加、重复请求、错误目标保留草稿、不自动发送、扫描边界与来源过滤）。已执行 swift test；本机 Command Line Tools 缺少 XCTest，不能记为通过。独立冻结候选 `artifacts/mac-voice-source-20261029/direct-r27/` 沿用 R26，并只替换这两项修复及其检查。部署及实机结果另行记录，不把隔离检查当作设备验收。

English: R26 runtime diagnostics confirm successful navigation (200) followed by a non-main-editor focus rejection. R27 reacquires a unique, currently verified main composer only for an explicit nonempty stage request after the normal read fails. It keeps fresh foreground/window/task/structure checks, a bounded complete scan, single-write readback, exact draft receipts and separate explicit sending. It never navigates, activates another app or uses an earlier open acknowledgment as write authority. OpenClaw's App Server sessions share the vscode source kind, so normalized `.openclaw` workspace paths are excluded before catalog ordering; ordinary Codex chats discussing OpenClaw remain. Unknown custom integration paths are not covered. Failed list reads preserve the last successful catalog. No wire change. Workspace Release and 28,564 isolated checks pass; XCTest is unavailable in local CLT. Frozen candidate deployment and physical interaction remain separate evidence.


R27 独立候选 Release、28,564 项隔离检查、签名与原 Keychain helper 字节一致性检查通过；已正常退出 R26 并部署到原固定路径，完整 R26 备份保留在 `direct-r27/previous-R26.app`。二进制 SHA-256：`b265d88289674eff3e8ae0643536e0525abd6d78ca73115abf9334f43e43b062`。用户另行授权安装 .292，当前由该固定路径运行原生产 Wi-Fi 升级与启动核验工具；正常后台重启和设备交互结果待后续记录。

English: the frozen R27 Release and 28,564 isolated checks pass. Signature and unchanged Keychain helper were verified; R26 exited normally and the fixed installation path now contains R27, with a complete R26 backup. Binary SHA-256 is recorded above. The separately user-authorized .292 firmware installation uses the existing production Wi-Fi acceptance runner; normal background restart and interaction acceptance are recorded separately.


R27 部署收尾：固定路径正常进程已启动，界面确认 TAB5 .292、USB 数据、Wi-Fi 已认证请求及蓝牙 USB 优先待命；检查窗口关闭，桥接继续后台运行。原生常用任务列表首项为当前“继续未完成任务”，OpenClaw workspace 项为 0；普通“更新 OpenClaw 到最新版本”会话保留。用户授权的 .292 安装已通过精确 ELF／VALID／三个实时样本及显示设置保留（71.405 秒含启动核验）。此处只确认来源过滤、部署和设备启动；无额外 Mac 点击的豆包直达回填、TAB5 额度触控行为仍待用户复测。

English: the fixed-path R27 process is running; UI confirms TAB5 .292 with USB data, authenticated Wi-Fi and authenticated BLE standby. Diagnostic windows were closed. The native task list now starts with the current Codex chat, contains no OpenClaw workspace rows, and preserves the ordinary OpenClaw-update discussion. User-authorized .292 installation passed exact ELF/VALID/three-live-sample/settings checks in 71.405 seconds through boot validation. Voice staging without an extra Mac editor click and the quota touch flow still require user interaction acceptance.


## 2026-10-29 R28：回填当前提问的回答框

用户确认 .292 的“总览其他模型 → 额度默认 Codex”正常，固件触控验收通过。R27 直达复测能返回 Codex 焦点，但提问卡片场景没有回填文字；桥接保留的诊断为定位 200、回填 503，原因“输入框内容结构尚不兼容”。用户明确选择回填“当前问题的回答框”。

本地已安装组件显示，中途提问使用独立 request-card 与 ProseMirror 回答框，操作为下一步、发送或发送更新。R27 的主输入框边界会拒绝此结构。R28 由独立编写的 AX 适配识别该结构，要求受信主文档、精确任务地址或完整目录唯一标题、一个回答框、可识别的问题标题及唯一下一步／发送按钮；不接受审批按钮、密码框、弹窗、嵌入浏览器或含不明引用的编辑结构。当前支持已核对的中文及英文标签。未复制供应商实现。

成功定位后，新的合法录音开始会在 IME 接管焦点之前捕获当前输入目标。捕获在录音状态锁外执行，返回时重新校验录音归属和取消状态。问题目标由本进程随机标识、当前卡片与编辑器身份及有界问题静态内容摘要绑定；回答文字和占位文字不参与摘要。问题、会话或目标改变时保留设备预览，不能退回主输入框。发现不能识别的请求卡片也不能借主输入框写入。

显式回填后，将精确文字与目标摘要保存在原加密回执中；问题路径不另外写普通聊天草稿。显式提交重新检查操作授权、同设备／同任务／同题／精确文字和期限，先消费回执，再执行同一卡片唯一的下一步／发送操作一次。不会发送普通新 turn，不清空回答，不发送全局回车，不选择选项或跳过，不重放未知结果。问题切换、卡片消失或发送更新状态变化只能确认本地 UI 已响应，不宣称模型执行完成。多题时逐题前进；下一段录音重新捕获当前题。

桌面操作工具拒绝直接访问 Codex 窗口（安全限制）；未换用其他 UI 自动化绕过。依据本地组件结构、既有桥接诊断与隔离回归实现；实际提问框回填／下一步／发送仍必须由用户操作验证。本轮不修改 .292 固件、展示设置或线协议；Windows 与固件共用的原请求和回执字段保持不变。构建、部署与实测结果另记。

English: the user accepts .292 quota navigation, but reports missing text in the question-panel case despite focus returning. R27 diagnostics show navigation 200 and staging 503 (incompatible editor structure). The user explicitly requests the current question answer field. R28 independently recognizes the installed request-card/ProseMirror structure in the verified primary task document, requiring one answer editor, a known Question header and a unique Next/Send/Send update action (checked Chinese/English labels). Approval controls, secure fields, dialogs, nested browsers and unsupported editor content do not qualify.

A fresh valid take captures the target before the IME changes focus, outside the voice lock, with ownership/cancellation rechecked afterwards. A process-local/card/editor identity and bounded static-question digest bind the answer; typed text and placeholders are excluded. Changed/closed/ambiguous questions preserve the preview instead of falling back to the main composer. An explicit submit retains opt-in, exact device/task/question/text/expiry checks, consumes its encrypted receipt before a single native question action, and never starts a normal turn, clears the answer, chooses options/skips, sends global Enter or retries an unknown result. UI advancement acknowledges only the local interaction, not model completion. Multi-question forms advance one question at a time, with a fresh target captured for the next take.

Computer Use denies direct access to Codex; no alternate UI automation was used. Static installed-component inspection and isolated tests are not hardware acceptance. No TAB5 firmware/display setting/wire-contract change is needed; real question staging/submission remains pending user verification.


R28 工作区最终验证：Release 通过，28,606 项隔离检查通过（相对 R27 新增 42 项）。覆盖问题与主框区分、同题追加、录音期间切题／关题不回退、原发送授权、单次本题提交、重复／未知回执不重发、题卡按钮白名单、嵌套文档／卡片拒绝，以及录音准备锁释放和取消后不启动后端。`swift test` 已执行，本机 CLT 仍缺少 XCTest，未记为通过。首次中途构建遇到源文件变动已丢弃，以上结果来自编辑完成后的重新构建。冻结包仅在已安装 R27 基础上更新五个相关源文件；不带入其他工作区窗口改动。

English: final workspace Release and 28,606 isolated checks pass (42 added over R27), covering target distinction/binding, append, changed/closed questions, send opt-in, one-shot native submission/unknown receipts, supported controls, nested-surface rejection, unlocked pre-capture preparation and cancellation. XCTest was attempted but remains unavailable in local CLT. An earlier build interrupted by source edits is not used; the final build runs after edits complete. The frozen candidate changes only five relevant source files from installed R27.


R28 已在原固定路径部署并正常启动：独立冻结包 Release、28,606 项隔离检查、签名及未改动 Keychain helper 核验通过；完整 R27 备份保留。二进制 SHA-256 `10b790c71afe44ae0909a070f9d5622cdd3c28054310b375f11b26eb0a96b9e3`。原生桥接界面确认 .292、USB 数据、Wi-Fi 已认证请求和蓝牙待命；检查窗口关闭并保持后台运行。已向用户发出本题回填及独立点击蓝色箭头的实测请求，当前不把两项记为实机通过。本轮没有刷机、公开发布或提交外部测试消息。

English: R28 is installed and running at the fixed path, with a full R27 backup. Frozen Release, 28,606 isolated checks, signing and unchanged helper checks pass; binary SHA-256 is recorded above. Native bridge UI confirms .292/USB/authenticated Wi-Fi/BLE standby; inspection window is closed. The user has been asked to test filling the current question and explicitly submitting it via TAB5. Both remain pending physical acceptance. No firmware flash, public release or external test message was performed.


R28 实机验收：用户按本轮问题要求使用 TAB5 定位、豆包录入并点击蓝色箭头，明确反馈“回填正常、提交正常。”当前提问回答框回填及其显式提交两项通过；.292 额度默认 Codex 已在前述反馈中通过。保留未测试范围：多题连续“下一步”、切题／关题期间录音拒绝、复杂引用或其他语言界面，仅有隔离检查或保守拒绝设计，不扩大为全部场景实机通过。桥接继续在后台运行；本轮无需再次刷机。

English: the user explicitly confirms “staging works, submission works” after the requested TAB5 navigation, Doubao input and blue-arrow submission test. Current-question answer staging and explicit submission both pass hardware acceptance; .292 quota navigation was separately accepted. Multi-question Next sequences, question changes during recording, complex references and other UI languages have not been physically validated; isolated checks/conservative rejection do not establish those cases. The bridge remains running in the background; no further firmware flash is needed.

## 2026-10-29 R29：首次回填偶发未就绪 / Intermittent first-stage readiness

用户随后反馈首次点击提交显示“电脑主输入框未就绪，识别文字保留在预览”，第二次成功。因此 R28 的单次提问流程通过仍成立，但不能将直达整体稳定性记为通过。继续会话时，原生桥接诊断中的最近八项已被后续成功请求覆盖，未拿到该失败请求的具体原因；不能断言已复现或唯一定位这一次故障。

源码复核确认：`AXFocused` 设置成功后立即读取焦点，桌面异步确认稍晚就会提前返回失败。R29 在一次焦点请求后增加最多 0.5 秒、11 次观察的确认预算；每次重新核对原窗口、前台进程、任务结构及原问题标识，焦点转到第三个控件或题目／会话变化立即停止。仅观察尚未写入的准备阶段，不重复设置焦点、不重复写字、不自动发送。现有精确文字比较、单次写入读回和加密幂等回执保持。目标扫描失败保留其实际固定诊断，不再一概归为“问题切换”；后续成功不会抹去本进程最近一次回填失败原因，不保存文字、窗口内容、账户或凭据。

隔离候选基于已安装 R28，只替换 `MacTab5Desktop.swift` 与 `MacCodexDirectValidation.swift`。新增检查覆盖异步焦点下首次请求成功、同请求不重复追加、目标失效停止、超时、时钟不前进、慢 AX 结果超出预算，以及成功后仍保留固定失败标签。本轮没有协议或固件变更，TAB5 保持 .292；Windows 对端与设备失败保留预览行为已对照。构建、安装及用户复测结果见下文，不将该时序修复作为原故障已经根治的证据。

English: after accepting the single R28 question flow, the user reported a first stage returning composer-not-ready and a successful second click. The latest eight native bridge diagnostics now contain subsequent successful operations, so the original failure has neither been reproduced nor uniquely attributed. Source review identifies immediate focus readback after an asynchronously acknowledged AX focus request. R29 observes the same verified editor/window/task/question for at most 0.5 seconds and 11 checks, stopping on invalid identity or a third focus target. It never repeats focus, text writes or submission. Existing exact text checks, single-write reconciliation and encrypted idempotency remain. Fixed in-memory failure labels survive subsequent success; no text, account or credential logging is added. The frozen candidate changes only the desktop implementation and its validation from installed R28. Protocol and TAB5 .292 remain unchanged. Build/deployment and physical acceptance are recorded separately below.

### 当前仍需现场验证 / Remaining physical checks

- **首次回填稳定性**：普通主输入框与当前提问回答框分别连续操作几轮；每轮先定位、录音、结束、只点一次回填，确认无需第二次点击。失败时保留预览和当前现场，读取桥接“最近回填失败”。
- **音源切换与恢复日常输入**：TAB5、Mac、现场接入的 DJI 反复切换，核对名称和真实音源；结束后在 Mac 直接使用豆包，确认正常收音。
- **提问边界**：多题“下一步”，以及录音时切题／关闭题卡后保留预览，仍只有隔离检查，不算实机通过。

已通过的无弹窗、空录音结束约 2 秒返回、旧会话未读提示清除、当前单题回填与提交、额度默认 Codex，以及后续 Wi-Fi／BLE 升级与精确启动核验不因旧文档的待办再次要求重复。BLE 曾有首块前失败，后续未复现，长期稳定性仍属观察项；完整 XCTest 缺模块属于本机测试环境限制，不是实机操作项目。

English: outstanding physical checks are first-click staging reliability in both composer types; actual TAB5/Mac/DJI source switching and ordinary Mac Doubao use after restoration; and multi-question/change/close boundaries. Final-word completeness and cancel/restart behavior were separately accepted by the user and need no repeat. Accepted no-popup/approximately-two-second empty takes, exact unread clearing, the single-question flow, quota navigation and later Wi-Fi/BLE boot-verified installs do not need repeating merely because an older record lists them as pending. The earlier pre-first-block BLE failure remains a long-term observation, not erased by later successes. Missing XCTest is a local test-toolchain limitation, not a physical acceptance task.

后续用户反馈：“长按清空，尝试不少次好像现在已经是OK的了。”据此将当前实际操作中的长按清空残字记为用户多次实测通过，并从上述待验清单移除，不再要求重复测试。没有提供逐次计数或分别覆盖手动／语音的记录，不将结果扩大为所有输入类型和边界场景均已通过。

English: the user subsequently reports that long-press clear appears correct after numerous attempts. The observed clear/residual-text flow is accepted on that repeated user feedback and removed from the pending list without another repeat test. No exact run count or separate manual/dictation coverage was supplied, so this does not claim every input type or boundary case was tested.

用户另确认“句尾完整这个功能也没问题。”句尾完整性按该反馈记为实机通过，不扩大为取消后重录、音源切换或首次回填稳定性已经通过。

English: the user separately confirms that final-word completeness also works. That behavior passes physical acceptance; cancel/restart isolation, source switching and first-click staging reliability remain separate.

用户随后明确确认“取消后重录功能也是正常的。”取消后重录也记为实机通过，并移出当前待验清单；不再按上述历史待验要求重复测试。

English: the subsequent explicit user confirmation that cancel/restart also works passes that behavior and removes it from the current pending list, superseding its earlier pending status.

R29 部署收尾：工作区及独立候选 Release、各 28,615 项隔离检查、签名与未更改的 Keychain helper 校验通过；完整 `swift test` 仍因本机 CLT 缺 XCTest 失败，未记通过。已正常退出 R28，保留完整备份并替换固定应用路径；安装后二进制 SHA-256 `a2d712d4b00bbe958ff9dbbb6831d99de5f130e1217a3b213ded8587165a6886`，唯一正常桥接进程。原生界面确认 TAB5 .292、USB 数据、Wi-Fi 已认证请求及 BLE 认证待命；检查窗口已关闭。首次启动的 UI 读取超时，随后确认进程及上述界面，不作为安装失败或重复启动证据。自检、源码摘要、备份、签名与部署记录位于忽略目录 `artifacts/mac-voice-source-20261029/focus-r29/`。本轮未刷机、改变展示设置、推送、打标签或发布。首次点击稳定性仍待用户验证。

English: workspace and frozen R29 Release builds each pass 28,615 isolated checks, signing and unchanged Keychain-helper verification. Full XCTest still fails because local CLT lacks that module. R28 exited normally and has a complete backup; R29 now runs at the fixed path with the recorded binary SHA-256 and one normal bridge process. Native UI confirms .292, USB data and authenticated Wi-Fi/BLE standby; the inspection window is closed. The initial UI lookup timed out, followed by successful process/UI verification. Evidence is in the ignored focus-r29 artifact directory. No firmware flash, display-setting change, push, tag or publication occurred. First-click reliability remains pending user acceptance.

## 2026-10-29 R30：同名诊断与运行中提交 / Identity and native submission

R29 后续实测失败：用户两三次录音回填均未进入题卡或主输入框，TAB5 提示 `composer_not_ready`。原生诊断记录两次回填 503（106／108 ms），定位均为 200，固定原因为“输入框内容结构尚不兼容”。这不支持将故障归因于焦点等待不足。后续普通主框也失败。

只读目录核对确认两个不同会话的完整标题都为“继续未完成任务”，精确重名计数为 2。桌面无法提供精确当前任务地址时，现有逻辑只能依赖全目录唯一完整标题；`findTarget` 却把身份校验失败覆盖为结构错误。将当前会话改名为“修复 TAB5 回填未就绪”，确认精确标题计数为 1 后，用户重新定位／重新录音，明确反馈“这次回填正常”。同一测试仍有选项，撤回先前将选项结构视为主要嫌疑的推测。此处通过的是回填，用户随后更正：最终发送通过电脑完成，不能记作 TAB5 提交通过。

设备始终以任务 UUID 请求；完整标题只用于确认桌面实际输入目标。相似标题不做模糊匹配；完全同名但有精确页面地址仍可使用。时间排序能区分目录条目，不能证明异步导航已完成或当前页面仍属该条目。已检查当前安装版的版本化 IPC：线程快照／owner 归属证明任务存在，不证明前台文档身份；没有找到可用于此核对的当前屏幕任务查询。R30 保持原边界，明确区分重名、题卡结构、扫描超限及多输入框，不把改名后的通过扩展为任意同名会话都已支持。

新增提交问题：用户报告任务运行中主输入框回填成功，但 TAB5 提交未确认，电脑提交可用；桥接保留“任务连接拒绝了发送”。旧主框路径先清空草稿，再调用新建 turn 的 `send`，运行中的任务会被 `task_busy_or_state_unknown` 拒绝，随后尝试恢复草稿。此源码路径确定存在；旧 UI 只保留通用拒绝说明，不能将未保留的底层响应伪装成已抓到的具体错误包。

R30 显式主框提交改用同一已验证输入框内唯一、启用且白名单匹配的原生 Send／Steer／Queue（发送／引导／排队）按钮。动作遵循当前桌面选择，不自动更改排队模式；不先清空、不发送全局回车、不代点停止／继续／审批，也不在未知结果后回退 IPC。范围限定同一 ComposerLayoutRoot，重新核对任务、焦点、问题状态、原文字、按钮身份与标签；加密回执先消费再单次执行。随后核对同一任务的原输入框被清空，只表示本地 UI 已响应，不证明模型已经执行或最终完成。网络后续失败及实际消息入列仍须实机观察。提问卡片继续走原同题按钮路径。

失败分为尚未执行（按钮未确认，409）与已尝试但结果未知（504）；固定的最近提交失败原因保留在本进程内存，后续回填成功不会覆盖。原授权、同设备／同任务／同目标／同文字／期限和重复请求回执仍在；后端结果未知不得自动重发。本轮不改线协议、TAB5 .292 或展示设置。中途“仅诊断”构建日志独立保留，不作为最终包验证；最终构建和实测另记。

English: R29 failed repeated physical staging with 503 while navigation returned 200; both composers were affected. The local catalog contained two identical full titles. When no exact live document address is available, that ambiguity correctly blocks editing, but the target scan incorrectly reported a structure failure. Renaming this chat to a unique title and starting a fresh take restored staging, including an options question. This supersedes the speculative options-structure diagnosis. The user explicitly clarifies that the computer performed submission, so TAB5 submission is not accepted.

Requests already carry UUIDs; sorting timestamps identify catalog rows but cannot establish the currently displayed document after asynchronous navigation. Current versioned thread IPC provides ownership/state, not verified foreground-document identity. Similar names are never fuzzy-matched. Exact live task URLs still take precedence even with duplicate titles. R30 preserves these checks and separates duplicate-title, question-structure, traversal-limit and multiple-editor diagnostics; arbitrary duplicate-title support remains limited when no exact live URL exists.

The old main-composer submission clears text and starts a new turn through IPC, which rejects active tasks. Runtime diagnostics confirm a generic send rejection but did not preserve its exact underlying error. R30 uses one verified native Send/Steer/Queue button in the same composer, respecting the desktop's displayed mode after encrypted receipt consumption. It never pre-clears, sends global Enter, clicks Stop/Resume/approval, changes queue settings, falls back to IPC or replays uncertain delivery. Same task/editor/text and button identity/label/enabled-state checks remain. Subsequent clearing of that original verified composer acknowledges local UI response only, not model execution or completion; actual delivery/queueing and later failures need physical acceptance. The question-specific path remains separate. Pre-action rejection (409) and uncertain attempted delivery (504) retain fixed in-memory diagnostics without text logging. Protocol, firmware and display settings remain unchanged; preliminary diagnostics-only builds are not final package evidence.

可靠性复核补充：原生提交确认后，在原 journal 锁内比较并清理同设备／任务中与本次文字精确一致、且无附件的本地草稿镜像；后来保存的新文字或附件不清理。清理失败单独显示，已经提交的操作不因此重发。用户要求按完整链路加强可靠性，验收不再以单次回填通过代替提交和恢复通过。

| 场景 / Scenario | 自动检查 / Automated evidence | 当前实机边界 / Physical status |
| --- | --- | --- |
| 相似／完全同名与精确地址 / Similar or duplicate names, exact URLs | 相似不模糊匹配；有精确地址可区分重名；错误地址拒绝 / Exact matching, URL precedence, wrong-route rejection | 唯一标题恢复回填已通过；无地址时完全同名仍需区分标题 / Unique-title staging passed; duplicate-title limitation remains |
| 当前题卡回填 / Current question staging | 同题、同框、精确文字，切题拒绝 / Bound target and exact text | R29 改名后带选项题卡回填通过；该次发送由电脑完成 / Options staging passed; computer performed submission |
| 主框发送／引导／排队 / Native main submit modes | 同输入框唯一启用按钮；停止、继续、审批不匹配 / Scoped unique action; Stop/Resume/approval rejected | R30 三种方式及实际入列分别待验 / Each native mode and actual delivery pending |
| 重复点击与重启 / Retry and restart | 消费回执；同 ID 回放结果，新 ID 不能复用已用草稿 / Consumed persistent receipts | 生产路径未主动注入重复发送 / No injected live duplicate sends |
| 目标或文字变化 / Target or text changes | 另一设备、切题、改字、按钮歧义均不执行 / Mismatch does not act | 对应异常现场仍待用户场景观察 / Physical edge cases remain unaccepted |
| 失败与草稿恢复 / Failure and draft lifecycle | 未执行与未知结果分开；无 IPC 回退；成功清理原镜像、保留新草稿 / Distinct outcomes, no fallback, conditional cleanup | 不能凭自检宣称长时稳定或断链恢复通过 / No long-term or disconnect acceptance claim |

English: native acknowledgment conditionally clears only the exact submitted device/task draft mirror under the journal lock, preserving newer text or attachments. Cleanup failure never replays an already attempted submission. The reliability matrix separates isolated checks from physical acceptance; neither single successful staging nor a large self-test count establishes stable end-to-end delivery.

R30 最终部署：工作区与冻结候选 Release、各 28,680 项隔离检查通过；最终冻结差异限于 `MacTab5Desktop.swift`、`MacCodexDirectValidation.swift`、`MacTab5Codex.swift` 三个文件。完整 `swift test` 再次因本机 CLT 缺少 XCTest 失败，未记通过。签名、原 Keychain helper 字节一致、许可证及校验文件核验通过。正常退出 R29 并保留完整备份，在原固定路径安装 R30，二进制 SHA-256 `67c1b6ab9cd0a8adcb22617fc15cf8c2794c25f59e4e9abdf729208598af93fd`；唯一正常桥接进程。原生界面确认 .292、USB 数据、Wi-Fi 已认证请求、BLE 待命，检查窗口关闭。证据位于忽略目录 `artifacts/mac-voice-source-20261029/identity-r30/`；早期构建单独标为 `diagnostics-only-*` 或 `before-draft-cleanup-*`，不算最终验证。下一步由用户在主输入框验证 TAB5 显式提交，避免新建题卡将该测试变成问题提交。本轮不刷机、不发布，原展示设置不变。

English: final workspace/frozen Release builds each pass 28,680 isolated checks. The frozen delta contains only the desktop adapter, its direct validation and conditional Codex draft cleanup. Full XCTest remains unavailable in local CLT. Signing, unchanged helper bytes, licenses and checksums pass. R29 exited normally with a complete backup; signed R30 runs at the fixed path as the sole bridge process with the recorded binary hash. Native UI confirms .292 and USB/authenticated Wi-Fi/BLE standby; the inspection window is closed. Final evidence is separate from earlier diagnostics-only/pre-cleanup builds. Main-composer TAB5 submission now awaits user testing without opening another question card. No firmware flash, display-setting change or publication occurred.


## 2026-10-29 R31：缩小提交容器与保留具体拒绝原因 / Scoped submission and precise rejection

用户实测 R30：回填成功，TAB5 提交仍提示“未确认发送”。本机桥接保留的诊断为“发送未执行：未确认当前输入框唯一且可用的发送、引导或排队按钮”。该路径在原生动作之前返回，故本次不能记作已点击后丢失确认；也不能凭这一通用错误定位到某个具体 guard。

对当前已安装桌面版本的静态界面结构进行只读核对：主输入框及其 AdaptiveFooter 内的主操作按钮均属于 ComposerLayoutBody；外层 ComposerLayoutRoot 标为 presentation，可能被辅助功能树省略。R30 回填验证已经要求 Body，而提交额外依赖外层 Root，形成不必要的兼容条件。R31 将提交查找限定到同一输入框最近的 Body，并在执行前复核同一 Body；不向 Root、页面或其他输入区扩大搜索。没有获取实际失败时的实时 AX 层级，所以“Root 被省略”仍是基于结构的解释，未伪称现场根因已证实。没有复制桌面应用实现源码。

R31 同时将预动作拒绝分成正文容器缺失、扫描上限／超时、其他编辑器或不兼容控件、原编辑器缺失、按钮缺失／多个／禁用、复核变化；保留原任务、前台、焦点和文字变化的具体原因，不再统一覆盖成按钮错误。诊断仅包含固定标签。唯一启用按钮白名单、授权、精确绑定、先消费回执、单次动作和未知结果不重试保持不变。TAB5 当前通用错误文案尚未细分，详细原因在 Mac 桥接本地诊断中查看。

回归检查覆盖外层 Root 存在或省略时均选中相同 Body、缺失 Body 不扩大范围、不能跨页面或题卡、祖先层数限制、嵌套正文拒绝、具体失败原因透传及已消费回执不重发。构建与实机结果另记；这些检查不能代替实际发送验收。

English: the user confirms R30 staging but reports another failed TAB5 submit. Native bridge diagnostics show rejection before any native action, although R30's generic label cannot identify the failing guard. Read-only inspection of the installed desktop's static layout places both input and primary footer action inside ComposerLayoutBody; the outer Root has presentation semantics and may be flattened out of the accessibility tree. R31 uses the nearest verified Body, already required by successful staging, and rechecks that same container before acting. It never broadens to Root or page. Missing Root is a structural hypothesis, not a captured live-tree root cause, and no vendor implementation was copied.

Pre-action diagnostics now preserve fixed specific reasons for missing container/editor/button, traversal bounds, incompatible scope, duplicate/disabled controls and revalidation changes, including existing identity/focus/text failures. TAB5's generic error wording is unchanged; detailed labels remain local to the bridge. Opt-ins, exact binding, single native action after receipt consumption and no replay of unknown outcomes remain. Regression checks cover flattened presentation structure, strict Body scope and exact failure propagation; physical submission remains pending separately from build validation.


R31 部署结果：工作区与冻结候选 Release 均通过，各 28,696 项隔离检查通过；完整 XCTest 仍因本机 CLT 缺模块失败。签名、未更改的 Keychain helper、许可证和校验文件通过。R30 正常退出并完整备份后，固定路径已安装 R31，二进制 SHA-256 `c65b7c2602cd76be29d53d40b3b8d8e02fe19e2381fcf9221e69f9f8e8521a28`。唯一桥接进程，原生界面确认 TAB5 .292、USB 数据、Wi-Fi 认证请求、BLE 认证待命；检查窗口已关闭。首次启动读取超时，随后进程与界面确认成功。证据与备份在忽略目录 `artifacts/mac-voice-source-20261029/body-r31/`。本轮未刷机、推送、打标签或发布；R31 实际提交仍待用户新录音、回填后单次显式提交验证。

English: workspace and frozen R31 Release builds each pass 28,696 isolated checks; full XCTest still fails for the missing local CLT module. Signing, unchanged helper, licenses and checksums pass. After normal R30 exit and a full backup, R31 is installed at the fixed path with the recorded hash as the sole bridge process. Native UI confirms .292, USB data, authenticated Wi-Fi and BLE standby; inspection is closed. Initial UI startup lookup timed out but subsequent process/UI checks succeeded. Evidence is in body-r31. No flash, push, tag or publication occurred. Physical R31 submission still requires a fresh user take followed by one explicit TAB5 submit.


R31 用户首次反馈：“这次好像一把头成功了。”按本轮新录音、回填后 TAB5 单次提交的测试上下文，记录为用户报告首次尝试成功，取代上文该单次流程的待验状态。保留用户“好像”的措辞；未额外抓取该次原生回执，也没有连续次数或分别覆盖发送／引导／排队的证据，不扩展为长期稳定性全部通过。保持当前安装版供日常使用观察，无需重测此前已经确认的长按清空、句尾完整、取消重录。

English: the user reports that this attempt appears to have succeeded on the first try. In the context of the requested fresh take, staging and one TAB5 submit, this records a user-reported first-attempt success and supersedes the pending status for that single flow. The tentative wording is retained; no additional native receipt was captured, and repeated reliability or separate Send/Steer/Queue coverage is not established. Keep the installed candidate for ordinary use; previously accepted clear, final-word and cancel/re-record behavior need no repeat.


## 2026-10-29 R32：已送达但确认误报 / Delivered action with uncertain acknowledgment

用户补充 R31 实测：“发送也成功了，但是电脑提示还是未确认发送，请检查电脑。”因此修正上一条验收边界：用户确认实际发送成功，但成功提示／确认链路未通过；不能把“一把头成功”记作完整闭环验收。桥接原生诊断的当前状态与最近提交失败均为“任务发送结果未确认”，两次回填分别 200（309／301 ms）。该次仍未保留更细的后动作分支，不能凭用户反馈断言唯一根因。

源码发现两条确认过早退出路径：AXPress 返回非 success（包括动作可能已发生的超时）即返回未知；一次发送后树结构或文字读取不完整也立即返回未知。R32 仅调用一次原生动作，无论 AXPress 是否成功响应均执行只读核对；以最多 2.5 秒／32 次观察等待同一任务、原编辑器、原 Body 与原窗口连续两次确认清空。临时快照不可读允许继续观察，窗口／目标变化或出现其他文字不确认成功。截止时间前后都检查，单次 AX 调用本身仍受系统接口超时控制，不宣称严格实时上限。不会补点、预清空、重发或回退 IPC；仅清空确认仍表示本地 UI 已消费草稿，不等于下游执行完成。

后动作诊断细分按钮响应与清空均未知、读回不可用、原文仍在、其他文字和上下文变化，不再被通用错误覆盖。新的成功提交清除旧“最近提交失败”，普通回填仍保留诊断以便排查。TAB5 线协议及错误文案不变。回归检查包含 AX 超时但草稿随后清空、瞬时不可读恢复、两次连续清空、停滞时钟／慢读期限、原文不变、用户改字、切窗口、后续成功清除旧提示，以及原回执防重放。当前候选构建与实机结果分开记录。

English: the user clarifies that R31 actually sent the message while the computer still reported unconfirmed delivery. This corrects the earlier apparent success: delivery is user-confirmed, but end-to-end acknowledgment is not accepted. Native bridge diagnostics retain the generic post-action unknown status; the exact branch was not captured.

Source review identifies two early exits: a non-success AXPress result despite possible delivery, and the first incomplete post-action tree/text snapshot. R32 presses once, then performs read-only reconciliation after either action result. At most 32 observations within a 2.5-second scheduling deadline require two consecutive empty observations of the same verified task/editor/body/window. Temporary missing snapshots can recover; changed context or other text is not success. Deadline checks surround observations, but individual AX calls have their own OS timeout and do not establish a hard real-time bound. No repeat press, pre-clear, IPC fallback or resend is added. This remains local composer-consumption acknowledgment, not downstream execution completion. Fixed post-action reasons survive propagation, and a later successful submit clears obsolete failure text while staging alone retains it. Protocol and TAB5 error wording are unchanged; regression tests and physical acceptance remain separate.


R32 部署结果：工作区及冻结候选 Release、各 28,708 项隔离检查通过；完整 XCTest 仍因本机 CLT 缺模块失败。签名、未改动的 Keychain helper、许可证和 SHA-256 文件通过。R31 正常退出并完整备份，固定路径安装 R32，二进制 SHA-256 `c1a849f68f12c7943736412520069aa4f62b4e104b9b247c2114a5910ff67931`。唯一桥接进程，原生界面确认 TAB5 .292、USB 数据、Wi-Fi 认证请求、BLE 认证待命，检查窗口关闭。首次启动 UI 读取超时后已核对进程及界面。冻结差异仅桌面适配与其验证两个文件；证据和备份位于忽略目录 `artifacts/mac-voice-source-20261029/ack-r32/`。未刷机、改展示、推送、打标签或发布。R32 实际提交后是否消除误报仍待用户验证，不能从自动检查推断已通过。

English: workspace and frozen R32 Release builds each pass 28,708 isolated checks; full XCTest still fails because local CLT lacks the module. Signing, unchanged helper, licenses and checksums pass. R31 exited normally with a complete backup; R32 is installed at the fixed path with the recorded hash as the sole bridge process. Native UI confirms .292, USB and authenticated Wi-Fi/BLE standby, and inspection is closed. The initial UI lookup timed out; subsequent process/UI verification succeeded. The two-file frozen delta, evidence and backup are in ack-r32. No firmware/display change, push, tag or publication occurred. Elimination of the false warning after actual submission remains pending physical acceptance.


## 2026-10-29 R33：以新增已接受消息补足提交确认 / Accepted-input reconciliation

R32 用户再次确认“发送成功，还是提示未确认发送”。原生桥接诊断为“已执行发送，但输入框状态暂时无法核验”，回填 200／350 ms；故延长原输入框读回观察未解决误报。R32 不能记作确认链路通过。该诊断仍不区分旧 AX 节点被重建或空内容快照不兼容，不能声称已抓到其中某个具体 UI 根因。

本次直接使用已有本机版本化会话 IPC 做只读结构核对，验证本会话存在 `userMessage`（`id`、`clientId`、`content`）及 `steeringUserMessage`（`id`、`input`、`status` 与消息别名）类型。只输出字段结构和固定布尔结果，不输出消息内容、原始会话、推理或工具载荷。独立编写的输入回执解析器沿用现有 owner 发现、local host、精确任务及 snapshot version 11 校验。用实际 IPC／解析器源码构建的窄诊断工具已在本会话验证：刚才这条消息精确匹配已接受输入；再次读取同一快照不能确认一次新发送。该工具没有发送操作，不算新的实机提交通过。

R33 在显式主框提交动作前取得短暂内存基线；只有原生动作已尝试但 UI 确认未知时，最多再读两次会话快照补足确认。要求同任务、同 owner、同预期文本摘要、当前或更新轮次、精确完整纯文本，以及发送前不存在的消息标识／别名；追加消息必须明确 `accepted`，`pending`、失败和未知状态都不算成功。旧同文消息、旧 pending 转 accepted、client/server 别名变化、后来补载的旧轮次、截断内容及混合附件均不冒充这次提交。仅容许编辑器段落末尾 CR/LF 差异，不模糊匹配内容。基线失败则不使用该补充证据，保留原 UI 路径；接口不可用时不伪报成功。

输入正文只在显式操作期间比较；回执对象只保存内存中的标识／正文摘要，不进入历史导出、日志或持久配置。后动作成功仍写回原加密操作回执并条件清理同设备／任务的本地草稿镜像。动作前拒绝不走此补充确认；没有第二次点击、IPC 发送或自动重发。提问卡路径不变。普通排队在服务端尚未接受前不会被此解析器当作已送达，仍按已有本地 UI 确认方式处理；模型执行完成与发送接受是两件事。

English: R32 again delivered the user's message but reported an unverifiable post-action composer, so its acknowledgment flow remains failed. The diagnostic does not establish whether the old AX node was replaced or the empty snapshot was incompatible. This iteration verifies the existing versioned local session interface rather than extending UI waits again. Read-only inspection identifies ordinary and steering input schemas. A narrow harness compiled from the actual IPC and independently authored receipt-parser sources verifies the exact latest input as accepted and rejects an unchanged snapshot as evidence of a new send; it performs no send and is not new physical-submit acceptance.

R33 captures a transient pre-action baseline and, only after an attempted native action with unknown UI acknowledgment, reads at most two further snapshots. Confirmation requires the same task/owner/expected text digest, current-or-newer turn, exact full text, and new message identities with no preexisting alias. Steering must be explicitly accepted. Old identical text, prior pending transitions, client/server alias transitions, newly loaded older turns, truncation and mixed attachments do not establish this operation. Only trailing paragraph CR/LF is normalized. Missing evidence preserves uncertainty; no second press, IPC send or automatic replay is added. Input bodies are compared only during this explicit operation; only ephemeral hashes remain in the receipt snapshot, never exported history or logs. Confirmed acceptance uses the existing encrypted result receipt and conditional local-draft cleanup. Questions are unchanged. Queued input not yet accepted by the server is not claimed delivered by this parser; existing UI acknowledgment remains available, and model completion is separate.


R33 部署结果：工作区及冻结候选 Release、各 28,731 项隔离检查通过；真实本机会话只读回执核验另行通过（确认本次文本已接受、相同快照不能当新发送），没有主动测试发送。完整 XCTest 仍因本机 CLT 缺模块失败。签名、原 Keychain helper 字节一致、许可证和 SHA-256 校验通过。R32 正常退出并完整备份，固定路径已安装 R33，二进制 SHA-256 `cd2b97835fbe7c9143a182db2737e33985f65f7217fe0a84ac16121e768e3dda`。唯一正常桥接进程，原生界面确认 .292、USB 数据及已认证 Wi-Fi／BLE 待命，检查窗口关闭。首次启动 UI 读取超时，随后进程与界面核验通过。冻结差异仅 6 个相关源文件，证据与备份在忽略目录 `artifacts/mac-voice-source-20261029/receipt-r33/`。未刷机、改变展示、推送、打标签或发布；安装后真实提交与提示一致性仍待用户反馈。

English: workspace/frozen Release builds each pass 28,731 isolated checks. The separate live read-only interface check confirms the exact accepted input and rejects unchanged-snapshot reuse; it did not initiate a send. Full XCTest remains unavailable in local CLT. Signing, unchanged helper bytes, licenses and SHA-256 checks pass. R32 exited normally with a complete backup; R33 is installed at the fixed path with the recorded binary hash as the sole bridge process. Native UI confirms .292 and USB/authenticated Wi-Fi/BLE standby, and the inspection window is closed. Initial UI lookup timed out, followed by successful process/UI verification. The six-file frozen delta and evidence are in receipt-r33. No flash, display-setting change, push, tag or publication occurred. Actual post-install submission/status consistency awaits user feedback.


R33 实机反馈：用户明确报告“这一次，显示已成功发送Codex执行。”按本轮 TAB5 提交与提示一致性验收上下文，记录本次提交成功提示通过，取代上文该单次流程的待验状态。没有另行统计连续成功次数、重放次数或分别覆盖发送／引导／排队，不扩大为全部模式及长期稳定性通过，也不将发送提示视为模型任务已经完成。保留当前 R33 安装供正常使用，不要求重复此前已通过的功能。

English: the user explicitly reports that this attempt displays successful sending to Codex for execution. In the current TAB5 submit/status-consistency test, the success indication passes for this single attempt and supersedes the pending status above. No repeated-run counts, duplicate-delivery counts or separate Send/Steer/Queue coverage were provided; this is not acceptance of every mode, long-term stability or completed model execution. Keep R33 installed for ordinary use without repeating previously accepted features.


## 2026-10-29 剩余验证与 R34 连续录音 / Remaining checks and sequential dictation

多题验证：工具收到两题各自回答，用户明确反馈“都是正常的”，本次多题切换／回填／提交通过。Mac 音源验证前记录原设置为 TAB5 内置麦克风、Mac 默认麦克风、豆包识别；仅临时改为 Mac／MacBook Pro 麦克风。用户确认 TAB5 显示 Mac 麦克风、文字完整，原生诊断亦显示实际开启 MacBook Pro 输入并已后台恢复豆包原选择。没有单独明确回答本次是否闪窗，不扩大该条反馈。测试后已通过原生设置恢复 TAB5 内置麦克风和 Mac 默认麦克风，保持豆包识别，并关闭所有检查窗口。系统和原生菜单均未发现 DJI；其实体收音仍未验。Mac 上脱离 TAB5 独立使用豆包仍待实际确认。

新故障：用户在同一道题分两次录音后，两段都已进入电脑回答框，但 TAB5 提示草稿变化／含引用，不能切到发送。原生诊断记录第二段回填 409／1588 ms，具体为“主输入框写入未确认”；此前两次回填 200／313、359 ms。该次写入已发生，不应再次写入重试。代码原本在每段之间主动加入 LF；当前题卡使用内联富文本编辑器，用户实际提交的回答中两段由空格衔接。未获取失败时 AX 原始字符串，换行被转换是有依据的兼容性解释而非唯一现场根因证据。

R34 将桥接自行加入的两段分隔符改为空格，继续当前段落，保留已有文字／用户段落和新识别文字，不放宽 `matchesReadback` 的内部空白比较、不补写。用户另要求回填后光标置于文字末尾；实现只在文字读回确认后，重新核对同任务／目标／精确文字，以 UTF-16 范围设置零长度末尾光标并读回验证。目标或文字变化时不挪动光标；不支持的光标接口单独提示，不撤销已经成功的文字回填或阻止提交。回归涵盖中文／代理对／组合字符位置、内联编辑器换行归一化模型下两次回填与一次提交、重复请求不追加、不重发，以及用户原有内部换行仍须精确匹配。该渲染模型不是实机 AX 抓取，视觉光标与连续追加仍待安装后反馈。

English: both question answers arrived and the user confirms both normal. A temporary Mac-source test is accepted for the displayed Mac name and complete text; native diagnostics confirm the MacBook Pro endpoint and Doubao selection restoration. The user did not separately answer the flash-window observation. Original TAB5 capture / default Mac input preferences have been restored through the native UI, with Doubao unchanged and windows closed. No DJI endpoint is present; standalone Mac Doubao use remains unverified.

The two-take question test exposed a new failure: both texts reached the answer editor, but staging returned 409 in 1588 ms with write-readback-unconfirmed, preventing TAB5 submission. The bridge injected LF between takes; the question uses an inline rich-text editor, and the submitted answer contains a space between segments. No failed AX raw value was captured, so normalization is a supported compatibility hypothesis, not a proven exclusive root cause. R34 uses a space for the bridge-owned separator while preserving authored text and strict internal whitespace checks; it never repeats the write. After verified staging, it rechecks task/target/text, sets a zero-length UTF-16 caret at the end, and reads the selection back. Changed targets/text are untouched; unavailable caret support is reported without invalidating already confirmed staging. Regression fixtures cover Unicode offsets, inline-normalization behavior, two takes/one submit and replay prevention; they do not replace visual/device acceptance.


R34 部署结果：工作区和冻结候选 Release、各 28,742 项隔离检查通过；完整 XCTest 仍因本机 CLT 缺模块失败。冻结差异为桌面适配、两组隔离验证和对应 XCTest 期望共 4 文件；当前已安装 R33 的其他源文件保持。签名、Keychain helper 字节一致、许可证和校验文件通过，R33 正常退出并完整备份后替换固定路径。R34 二进制 SHA-256 `8638b2e1e0a15246aad22a1d5d96de0f9597c57d27bc8af12fcfb1f2f7973f1f`，唯一桥接进程；原生界面确认 .292、USB／Wi-Fi／BLE 恢复，窗口关闭。首次启动 UI 超时后已独立确认进程和设备界面。证据在忽略目录 `artifacts/mac-voice-source-20261029/append-r34/`。未刷机、改变展示、推送、打标签或发布；两段追加／视觉光标和 Mac 独立豆包恢复正在通过题卡进行实机验证。

English: workspace/frozen R34 Release builds each pass 28,742 isolated checks. Full XCTest remains blocked by missing local CLT modules. The frozen four-file delta contains the adapter, two isolated validations and corresponding XCTest expectations; other installed R33 sources are preserved. Signing, unchanged helper, licenses and checksums pass. R33 exited normally with a full backup before replacement at the fixed path. The recorded R34 hash runs as the sole bridge, native UI confirms .292 and recovered links, and windows are closed; initial UI startup timeout was followed by successful verification. Evidence is in append-r34. No flash, display change or publication occurred. Two-take append/visual caret and standalone Mac Doubao restoration are undergoing physical question-card validation.


R34 第一轮实机结果：收到第一题答案“测试一 测试二”，按两次录音回填后单次提交的指定流程，连续追加和提交通过；用户第二题明确确认“豆包语音输入法正常的”，Mac 独立豆包使用恢复通过。第一题未直接说明光标位置，因此不把视觉光标定位自动记作通过。下一轮进行“录音结束保留预览→电脑切到下一题→旧预览回填应拒绝→新录音正常提交”的目标变化测试，同时补确认光标末尾。

English: the first R34 physical response contains both dictated segments, passing sequential append and submission in the requested flow. The second answer explicitly confirms ordinary Mac Doubao input works after restoration. Visual caret position was not explicitly reported and remains pending. The next test changes question after recording finalization but before staging, checks that the old preview cannot enter the new question, then tries a fresh take; it also asks for the caret observation.


切题场景补充：第二题反馈“没有旧文字”；第一题最终内容含测试句，用户明确更正该句为本人输入，不可据此推断桥接把旧录音写回第一题。原生诊断最近三次回填均 200（678／305／574 ms），未保留本次回填拒绝；所以“第二题无旧文字”与“实际触发旧预览拒绝”分开记录。仅补问实际拒绝提示及视觉光标观察，不因手动输入内容要求整轮重测。

English: the user confirms no old text in the second answer and clarifies that the test phrase in the first answer was manually entered; it is not evidence of a misplaced bridge write. The latest three visible stages are 200 and no stage rejection is retained. Absence of old text and an actually exercised rejection are recorded separately, with only observation clarification requested alongside caret placement.


用户明确说明此前复杂切题流程没有按要求操作、未注意观察，因此撤销对该流程的任何通过推断；“第二题没有旧文字”只保留为普通观察。随后单独光标题选择“在文字最后”，视觉末尾定位通过。旧预览保护改为两次独立题卡：先录音保留预览、电脑确认；新题卡出现后才尝试旧预览回填，减轻操作负担。

English: the user clarifies the complex boundary sequence was not followed/observed, so no boundary acceptance is inferred. The separate caret test explicitly confirms text-end placement. Old-preview protection is now tested with two separate question cards: retain a recording preview, acknowledge on the computer, then attempt staging only after the replacement card appears.


边界测试方法更正：用户指出“回填文字不是自动的吗”，核对快捷语音流程确认结束录音后自动发起填入电脑，蓝色箭头才是正式提交；没有前述假设的独立手动回填按钮。因此先前“结束录音留预览→换题→点回填”的设计不适用，不能归咎于用户未照做，也不能据此验收边界。已明确向用户更正。新步骤为：第一张题卡开始录音且不结束，用户用电脑确认“正在录音”；随后换新题卡，再结束录音，观察自动填入是否被阻止。第一步用户已确认正在录音，第二步观察待回。

English: the user correctly points out that finishing recording automatically stages text; the blue arrow submits, and there is no separate manual stage button in this flow. The earlier finish/retain/change/manual-stage procedure was invalid, not a user execution fault, and establishes no boundary acceptance. This was explicitly corrected. The replacement test starts recording on one question, receives a computer acknowledgment while recording continues, then replaces the question before the user stops recording and observes automatic staging. The first step is confirmed; the second observation is pending.


最终逐步边界反馈：用户明确报告“旧题录音测试”的文字保留在预览，提示“识别文字已保留在预览，可重新录音”，文字未进入新题。由此将本次录音期间题卡更换／阻止串题／保留预览记为实机通过。此结论基于正确的自动填入流程，不依据此前无独立回填按钮的错误测试。当前安装保持 R34/.292、原音源设置已恢复，无额外刷机或发布。

English: the final stepwise test explicitly confirms that the old-question transcript stays in preview with the re-record hint and is absent from the new question. This passes the exercised recording-during-card-replacement protection and preview preservation. It uses the actual automatic-stage flow, not the earlier invalid manual-stage procedure. R34/.292 and restored source preferences remain installed, with no additional flash or publication.


发送方式分项验证：用户在回复结束、任务空闲后通过 TAB5 发送“空闲发送测试”，随后明确反馈“测试通过”。空闲发送单项记为实机通过；运行中引导、排队和断链仍分开验收，不由该结果推断。

English: after the assistant finished and the task became idle, the user sent the requested idle-send phrase through TAB5 and explicitly confirmed the test passed. Idle submission passes; active steering, queueing and disconnect recovery remain separate.


2026-10-29 运行中追加复验：用户报告文字先进入引导／排队区域，手动点击“引导”后本会话收到补充，但 TAB5 显示“未确认发送，请检查电脑，不要重复发送，可重新录音”。桥接原生诊断保留“已执行发送，但输入框状态暂时无法核验”。这不是验收通过。R33/R34 的消息回执只接受新增且已接受的输入，尚未接受的排队消息不能满足该条件；未获得失败时的完整输入框快照，不断言输入框读回失败的唯一原因。

用户进一步说明 Mac 电脑直接输入也会排队，并明确选择“和 Windows 一样，直接补充到正在执行的会话”。当前安装包的设置定义确认 `followUpQueueMode` 支持 `queue`／`steer`，中文入口为“跟进处理方式”。[官方设置说明](https://learn.chatgpt.com/docs/reference/settings#general)也明确支持选择引导当前运行或等待下一次运行。仅查看了设置／接口定义，没有复制厂商实现。CUA 明确拒绝控制 `com.openai.codex`，因此请用户通过通用设置核对／选择“引导”；不绕过限制写内部配置。尚未修改桥接生产源码、重新安装或刷机，当前仍是 R34/.292。

English: the active-run test fails status confirmation. The input entered the desktop queue; after the user manually clicked Steer, it reached this conversation, while TAB5 still reported unknown delivery. Native bridge diagnostics retain unavailable composer readback. R33/R34 only reconcile newly accepted input, not pending queued input; no failed raw editor snapshot proves a unique readback cause. The user also observes queueing from direct Mac input and requests direct steering like their Windows installation. The installed setting schema and official General settings documentation expose queue/steer follow-up behavior. CUA refuses control of the Codex app, so the user is checking that setting; no internal-config bypass, production code change, redeployment or firmware flash occurred. R34/.292 remains installed.


用户已确认 Mac Codex 的“跟进处理方式”原为“排队”，并亲自改为“引导”。现在等待当前运行期间 TAB5 一次发送、无需电脑二次引导的实机复验；消息到达和设备成功提示需分别确认。此设置变更不等于 R34 发送确认缺陷已修复。

English: the user confirmed that Mac Codex previously used Queue and manually changed it to Steer. A single TAB5 submission during the active run, without a second desktop Steer action, now awaits physical verification of both arrival and device status. This preference change alone does not establish that the R34 confirmation defect is fixed.


引导模式复验通过：测试输入在助手保持运行时收到，用户明确确认“无需电脑操作，TAB5 显示发送成功”。本次仅修改 Codex 自身跟进偏好，未修改桥接代码；R34/.292 保持安装。结论限定为这一次直接引导与提示一致性通过，不宣称排队确认缺陷已修复或连续／断链稳定性已覆盖。

English: the Steer-mode retest passed. The input arrived while the assistant remained active, and the user explicitly confirmed successful TAB5 status without further desktop interaction. Only the Codex follow-up preference changed; bridge code and installed R34/.292 stayed unchanged. This establishes the exercised direct-steering/status flow, not a Queue-mode fix or repeated/disconnect reliability acceptance.


## 2026-10-29 R35 排队提交确认 / Queued submission acknowledgment

用户明确：进入 Codex 队列就算发送成功；排队实机验收留到 2026-10-30。连接中断恢复未发现已知问题，只在实际使用出现异常后再处理，不新增专项验收。本轮先完成源码与自动验证，提交 GitHub，并提供 [Windows 全项交接清单](WINDOWS-FOLLOWUP-FROM-MAC-20261029.md)，不安装 R35、不刷机、不打标签或发布正式版本。

修复确认两种排队保存位置：Codex 已提交到本地的 `queued-follow-ups[taskId]`，以及只读 `thread/queue/list` 的服务端队列。单次原生动作前保存短期基线，动作后仅在同任务／owner 下核对新增且完整纯文字一致的条目；关联本地、服务端及客户端别名，旧排队迁移／变 accepted 不能误作新发送。失败、未知、带附件的部分匹配、无法读取、未完整分页均不构成成功。返回仍是既有 `200 + submitted:true`，不增加设备字段；本机诊断区分“已保存排队消息”，不宣称任务完成。原生动作仍至多一次，回执先消费，结果未知不补发、不改队列。

本地读取有文件类型、所有者、写权限、大小与读取一致性检查；服务端查询使用独立只读 app-server 子进程、600ms 截止且不等待历史读取锁。synthetic wire 默认不碰真实队列／账户。解析只留内存标识和正文摘要，不导出正文、真实标识或原始快照。接口 schema 来自已安装版本的生成文档与只读查询，代码为本项目独立实现，没有复制厂商实现。

已用生产 IPC／回执／队列／RPC 源码的独立只读 harness 确认本机两种队列结构可读，连续不变快照不会被认成新发送。harness 未使用的历史／图片路径用窄桩隔离；这只是接口兼容性检查，不是排队发送实机验收。证据位于忽略目录 `artifacts/mac-voice-source-20261029/queue-r35/`。工作区与冻结候选 Release 构建均通过，两者分别通过 28,784 项隔离检查；工作区另通过 81 项 OTA／HTTP 回环检查和 16 项监听恢复检查。完整 `swift test` 已尝试，但本机 Command Line Tools 缺少 XCTest 模块，不能记为通过。此次 Windows 配套源码没有在 Windows 构建或实机验证。

English: the user defines reliable Codex queue retention as successful delivery and defers hardware queue acceptance to October 30. Connection interruption is not a known defect and remains ordinary-use observation. R35 adds pre-action baselines and post-action read-only reconciliation for committed local and server queues, bound to the same task/owner and full plain text. Alias transitions, old identical messages, unavailable/incomplete reads, failures and partial attachment matches cannot prove a new submission. The existing 200/submitted response remains wire-compatible; native diagnostics distinguish queue retention from model acceptance/completion. No second action or queue mutation occurs. Bounded local-file reads and a dedicated 600ms read-only app-server query retain only transient hashes. The production-source harness verifies both current local interfaces and unchanged-snapshot rejection without sending input. Hardware acceptance remains deferred, and R34/.292 stays installed.


Final R35 source validation: workspace and frozen-candidate Release builds both pass, each with 28,784 isolated checks. Workspace loopback validation passes 81 OTA/HTTP checks and 16 listener-recovery checks. Full `swift test` was attempted but cannot load XCTest from local Command Line Tools. No Windows build/runtime acceptance is claimed for this source delivery. R35 remains undeployed.
