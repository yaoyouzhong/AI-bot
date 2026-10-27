# 当前验收检查点 · 2026-10-24

<!-- R26-279-CURRENT-START -->
2026-10-26 当前：**TAB5 .279 已正常安装，精确 ELF／VALID 分区／三个实时 UI 样本核验通过，升级前后全部展示设置一致。** 用户随后切页按当前选择保留，未写回覆盖。镜像 SHA-256 `f6e540c750beb9b0ca908a221d4c80fd1caf170c112beaef2d299621387f8165`；固定 latest 与归档一致，本地交付不等于公开发布。

.279 复用既有空闲完整帧，并在相机重新接管前完成 CPU 缓存交接。81 项实际 worker／codec／vendor／缓冲池检查、128 次低内存相机捕获重开、64 次借还循环、四向逐像素及三个缓存失败位置恢复、完整 P4／板级配置／源摘要／保留区／实际更新说明检查通过。用户已确认：20 秒高清保持无内存提示、音乐切 Codex 及时高清、相机两次打开及照片完整、录音单次开始结束且文字连续完整、相机和录音后恢复高清、诊断说明准确且网络主页面有 USB／蓝牙状态、本轮没有蓝闪。Mac 实际关闭重开两次均恢复 1280×720；.278 已通过持续预览中的音乐单次控制回归。

本轮前台操作后累计预览请求 67、完成 63、超时 2、拒绝 1，显示错误和欠载 0；两次超时及一次拒绝与相机／录音测试交叠，恢复已通过，不能记为全程零失败或长期蓝闪根因闭环。Windows／ESP8266 实机证据仍独立。

**这条开发会话的“查看回复”超时已修复并实机通过。** Mac 收到 `codex/read` 返回 503；只读接口实测原先 32 轮完整历史约 23 MB，超过桥接 8 MiB 上限。summary 视图仅 32 条公开项，完整视图 556 条，不能拿摘要替代完整回复。修复改为无正文轮次目录加所选／最新轮次的 16 项分批读取，保留 8 MiB 接收限制和 1 MiB 公开文字限制。实际只读分批采样最近两轮两次采样共 25／26 次调用、最大 130781 字节、总 94–97 ms；这是接口证据，不是设备端到端延迟。R26 已通过 Release、2829 项隔离、27 项窗口生命周期与 16 项 HTTP 回环检查，签名校验及 Keychain helper 一致；已正常切换为唯一运行桥接，USB 数据、Wi-Fi 认证请求、蓝牙认证均已确认。检查窗口已关闭；用户关闭并重新打开一次“查看回复”后，明确确认“回复已显示，不再超时”，本轮原问题通过。XCTest 因本机 CLT 缺模块仍未通过。证据 `artifacts/development/tab5-codex-reply-read-20261026/`。

Current:279 is installed with exact boot and unchanged settings. The user accepts sustained HD, page updates, camera reopen/capture, single-tap recording with continuous text, HD recovery, accurate diagnostics and no blue flash in this bounded round; two actual viewer reopens also pass. Foreground tests include two preview timeouts and one rejection followed by accepted recovery, not an all-zero claim. A separate View reply failure is reproduced: a 23 MB full history response exceeds the retained 8 MiB cap. Bounded directory/item paging preserves complete public text; live API sampling passes, R26 Release,2829 isolated,27 window and16 loopback checks plus signing pass; the sole signed bridge is deployed, with USB data and wireless authentication confirmed. The user reopened View reply once and confirms the reply displays without timeout, closing this observed failure. XCTest remains unavailable. No public release or Windows-native acceptance.
<!-- R26-279-CURRENT-END -->



<!-- R19-276-CURRENT-START -->
2026-10-26 当前：**TAB5 .276已安装并通过精确ELF、VALID分区和三个实时UI样本核验。用户确认高清及切页自动更新均正常；R22桥接提速后，用户确认更新明显更快且高清正常，本轮速度验收通过。** 正常升级仅确认一次，不重放重启后的旧状态。升级前后亮度79、音量70、非静音、提醒开、轮播关、15秒间隔、5分钟屏保及页面列表顺序一致；固定页由额度变总览，用户明确确认是自己点过总览，未写回覆盖。

.275原始截图1843200字节申请失败已取得阶段1真实记录；.276按预览租约复用raw缓冲，结束空闲5秒回收，录音／相机／OTA可回收闲置缓冲，活动worker不可抢占。新请求仍重新采样、校验并绑定当前归属。66个实际worker/JPEG/vendor UBSan场景、真实LVGL完整及前台流程、P4完整构建、7531冻结源摘要、板级配置、保留区及正式镜像／说明检查通过。镜像8e2d258c6e76…f889c，7208464字节，原分区余496字节；未改分区。

实机运行249676→330000ms，完整帧13→23，超时／拒绝均0，最小栈11888字节，显示错误／欠载0；本次启动未保留raw失败记录。USB发布前准入忙后仍继续取帧。用户随后明确确认高清及自动切页更新通过；不外推长期蓝闪、.276前台录音／相机回收或所有连接稳定性。实际记录见artifacts/development/tab5-combined-276-20261026/PREVIEW-ACCEPTANCE.json。

速度基线：R20计时版沿用原节奏，三次完整请求为5727／5780／5782ms；排队约675–722ms、设备准备约2831–2918ms、首片至完成2167–2274ms。稳定画面的unchanged回执使完整帧间隔更长，不能把该间隔当成切页响应或纯带宽。仅保留固定数值，不保存真实截图、文本或身份。R20通过Release、1598项隔离和27项真实窗口合成生命周期；swift test因本机CLT缺XCTest失败，未记通过。

R22提速桥接已签名并后台运行（PID23012，artifacts/mac-preview-speed-r22-complete-20261026/AIBotBridge.app）。新USB预览及时发布，最多连续4个RPC／60ms，让位状态、归属变化、配置和录音；仅在有效RPC完成后的250ms内以10ms检查下一请求，空读不续期，编码／空闲保持100ms。非BLE完整帧间隔和窗口检查缩为250ms，同像素不重绘、unchanged冷却及高清／SHA／全部原截止保留。Release、1632项隔离、27项真实窗口合成生命周期通过；swift test仍因CLT缺XCTest失败。初次R22编译的运算符空白错误已修正并重新完整构建，未部署失败产物。

真实对比：完整请求从5727／5780／5782ms降为4122／3958／4007ms；分片接收降至973／956／804ms。一次实际关闭重开恢复1280×720，后续同一stream完整帧增至6／请求7，超时／拒绝／显示错误／欠载均0。用户明确反馈“更新明显更快，高清正常”，本轮切页速度及高清验收通过。还剩设备准备画面约2.8秒；这不是实体切页到呈现的逐帧精确延迟保证。已关闭运行监控和设备中心，保留用户预览窗口。证据artifacts/development/tab5-preview-speed-20261026/RESULTS.json；Windows参考同步更新，未对外发消息。

Current:276 is installed with exact ELF/VALID/three-live-sample boot verification. The user accepts HD clarity and automatic page updates, and confirms tapping Overview; all non-page settings match without writing them back. Existing worker/native/UI/full-image evidence remains valid. Live complete frames grow13→23 with zero timeouts/rejections/display errors or underruns; sustained blue-flash and foreground regressions remain bounded pending acceptance. R20 adds numeric timing only, passes Release/1598 isolated/27 synthetic-window checks; XCTest remains unavailable. Three complete requests take5.727–5.782s, including~2.8–2.9s preparation and~2.2s reception. R22 is now the sole signed background bridge, passing Release/1632 isolated/27 synthetic-window checks; XCTest remains unavailable. Real complete requests drop from5.727–5.782s to3.958–4.122s, with reception0.804–0.973s; HD remains1280×720. One actual close/reopen restores HD and the subsequent stream reaches6 complete frames/7 requests with zero timeouts/rejections/display errors/underruns. The user explicitly accepts noticeably faster page updates with HD intact. Long-term/foreground/Windows boundaries remain open. No new firmware, wire changes or public release.
<!-- R19-276-CURRENT-END -->

> 以下.275／R18和更早检查点保留当时结果；当前交付与阻塞以顶部最新检查点为准。

<!-- R18-275-CURRENT-START -->
2026-10-26 当前：**TAB5已正常升级到0.2.275-ui，Mac R18保持原实例。** 用户报告.274切到Codex任务后镜像仍停额度页。实读设备page8、UI和三通道心跳健康，预览请求6121→6129、超时20→27、完整帧572→573；确实晚到一张Codex画面，随后仍停更。没有把偶尔恢复记为通过。数值现场不含画面、识别内容或凭据，见`artifacts/development/tab5-preview-admission-20261026/LIVE-FAILURE.json`。

源码／原生复现确认：预览与任务读取共用CDC/GATT mailbox，单次100ms准入遇忙会在发布前退出；worker结束而主机仍等下一帧。新代码仅对此“尚未发布”结果在原请求期限内重试，检查归属和前台语音／照片／升级优先。已发布超时或不确定回执不重放，通道与原截止不改变；接收线协议保持。旧／新实际exchange各18个场景、原unlock准入回归、实际worker/JPEG/vendor的60个UBSan故障与清理场景通过。证据见`tab5-preview-admission-20261026/VALIDATION.json`，范围补丁`docs/proposals/tab5-preview-unpublished-admission.patch`。

.275完整构建、原板级配置、7531冻结源摘要、12个保留区符号位置、正式镜像和实际sidecar视觉校验通过。镜像`4585be739be1ef0e9c8ec0b9e475e132d90cb955a87d096d4437ac09a77304f1`，ELF`837c5d267a53fcef3aaba89f44ae69d9c8f0ead633dea4120eda52598683ffdf`，7208848字节、原分区余112字节，未改分区或数据布局。固定latest已更新并归档。原设置在升级前回读为亮度79／音量70／非静音／提醒开／轮播关／固定额度／15秒／5分钟及原页面选择顺序，对话框取消未保存。

升级只确认一次、设备确认开始；重启后.275和三通道恢复，旧“升级请求未确认”未重发。精确USB启动核验已读到通过：精确ELF、VALID分区及三个实时UI样本一致。升级后非页面设置回读与之前一致；用户正在切页，固定页由额度变为桌宠，未把这项差异虚记全量不变，也未写回覆盖用户页面。用户明确确认Codex在10秒内跟随，本轮切页通过。两次关闭重开都重新取得1280×720真实画面，基本重开通过；首次观察距打开约21.1／20.5秒，并非精确首帧耗时。第二轮同一stream从11完整帧／12请求继续到18完整帧／21请求，始终超时0、拒绝0；设备预览最小栈11760、显示错误／欠载0、重启原因3。RPC计数12次-1011准入忙后仍继续出图，符合修复分支。先前仍观察到640×360降级及HD恢复，持续高清和全部稳定性尚未通过。

下一步：继续核对连续更新与反复标准画质降级；本轮Codex切页和两次基本重开不外推长期稳定。蓝闪的持续肉眼观察、实际Windows／ESP8266环境边界保留；Windows共用设备修复与平台差异见[Windows参考](MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。无新Mac桥接、无重配网络／配对、无测试消息，无推送、标签或公开发布。

Current:275 is running after one normal upgrade confirmation with the original R18 bridge; stale post-reboot upgrade state was not replayed. Exact ELF/VALID/three-live-sample boot verification passes. Non-page settings match; selected page changed during user navigation, so full unchanged settings are not claimed. Shared mailbox admission contention is reproduced with actual code; only locally unpublished busy results retry within the original deadline/owner/foreground priority.18 baseline/new exchange cases,60 actual worker/codec/vendor UBSan cases and the original unlock regression pass. Full image/config/7531 inputs/retained-layout/actual-notes checks pass. The user passes Codex switching within10seconds. Two close/reopen cycles produce actual1280×720 frames; the same stream grows from11/12 to18 complete frames/21 requests with zero timeout/rejection. Basic reopen passes, while earlier standard-quality fallback and sustained stability remain open. No new wire protocol, partition, Mac bridge, pairing or public release.

当前清理：运行监控和设备中心已关闭，保留用户设备预览窗口。已请求只看当前清晰度／是否再降标准，反馈待收；未再要求切页或重开。

Cleanup: monitor/Device Center close, while the user preview remains open. Current clarity/fallback feedback is pending; no repeated page/reopen task is requested.
<!-- R18-275-CURRENT-END -->

> 下列.274及更早记录保留当时结果；当前状态以上面的.275检查点为准。

<!-- R18-REMOTE-CURRENT-START -->
2026-10-25 当前：**TAB5 0.2.274-ui 已安装并通过精确启动核验，Mac R18 后台运行（PID87956）。** 本轮正常升级只确认一次，重启后的旧请求未重发。USB 核验确认精确 ELF、VALID 分区和三个实时 UI 样本一致。亮度79、音量70、非静音、提醒开启、轮播关闭、固定模型额度、15秒间隔、5分钟屏保、原页面选择与顺序均回读一致；两次设置对话框均取消未保存。未重配网络、配对，未发送消息或改动照片草稿。

**.274 预览实测仍在验收：**首帧1280×720，随后实际出现640×360标准画质，之后自动恢复1280×720；高清截图已直接检查，未将真实设备图片落盘。清晰度尚未记通过，用户观感待反馈。预览持续运行期间，完成任务39→79，最小栈余量11860字节；重启原因3，显示错误／LCD欠载均0，USB会话2／中断1保持（该次中断对应正常OTA）。约47秒数值采样的内存阶段均0，但成功任务会重置此字段，因此不能据此推断初次降级原因。本轮尚未进行切页和关闭重开；Mac再次自动锁屏，界面工具明确要求手工解锁，当前预览窗口仍开着，未改锁屏或安全设置。

此前 .273 真实预览异常重启已取得RTC证据：`screen_preview`、core0、SP_SPILL_MIN_RAW=256、PC0x481b1e10；精确 ELF 对应 `_svfprintf_r`，入口额外使用1152字节栈。原工作函数和编码回调各含2752字节录音状态，P4静态栈3152与2768字节，在原8KiB任务下越界。.274 将状态移到PSRAM任务对象，栈改16KiB，静态用量降为544与16字节。实机观察余量支持本次栈修复，但不等同于唯一证明此前周期蓝闪根因或长期稳定性。

镜像 `520bd67a6635e1ed2b2a9f652cc8bb4a0b7ec99637dab41fa674fdfbb9ff420a`，ELF `bc40a00aa9444b9c1463e63d8de23ef363335f489cd568cdcee4f8e9619619ef`，7208720字节、原分区余240字节。完整构建、同板级配置、7531源摘要、11个RTC符号地址、正式镜像验证、实际sidecar视觉检查、真实worker/JPEG的UBSan及PSRAM状态归属、P4栈用量、诊断前缀／1..1024字节边界通过。固定latest与版本归档保持一致。

R18 标准“窗口”菜单复用托盘的设备预览选择，仅设备列表变化时更新；入口已实际打开正确TAB5，原小屏预览保持。Release、1564项隔离及27项真实窗口合成生命周期通过，签名有效、Keychain helper与R16相同；`swift test`仍受本机缺XCTest限制。Windows沿用既有诊断字符串，可接受`;sp`后缀；设备修复两端共用，详见[Windows对照](MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。

下一步：Mac手工解锁后，先确认当前高清画面，再依次完成切页更新和关闭重开；临时测试页面最后恢复模型额度，其他设置保持。蓝闪仍需用户现场肉眼观察。用户对本轮安装及测试的授权继续有效，不重复索取许可。证据：`artifacts/development/tab5-274-acceptance-20261025/`、`tab5-preview-stack-20261025/`、`tab5-combined-274-20261025/`。无推送、标签或公开发布。

Current state:274 is installed with a single normal confirmation, exact ELF/VALID/three-live-sample boot verification and unchanged settings. R18 remains the sole background bridge. Actual preview started at1280×720, temporarily fell to640×360 and recovered HD. Human clarity acceptance is pending; page-switch and reopen tests have not started because the Mac locked again. Preview jobs39→79 retain11860bytes minimum stack, normal OTA reset reason3, zero display errors/underruns and no additional USB interruption. This supports the stack repair but does not prove sustained preview or blue-flash acceptance. Numeric allocation phase0 after successful jobs cannot explain the initial fallback. Build/config/package/actual-notes/production-function checks remain passed; no security changes, message sends or public release.


2026-10-25 离线追补：用户明确当前不在设备旁，清晰度继续待现场确认；本轮没有操作设备。对冻结.274的实际预览worker、JPEG封装、vendor编码器和cJSON做UBSan及可跟踪分配故障注入，54个用例通过：任务／原始像素／输出／codec存储与行缓冲／MCU初始化分配失败、普通malloc失败后的既有PSRAM回退、输出扩容、512KiB上限、32次横竖交替、慢编码和丢片恢复。每个任务完成后受跟踪分配均释放。合成153004字节JPEG触发262144字节输出缓冲；高复杂度超限正确报告encoding，不误判memory。它不能测量设备真实最大连续块或证明初次降级原因。

同一组2169个合成事件直接驱动Mac和Windows生产接收器，两端各2152项检查通过；明确验证30／60／120／240／300／300秒重试退避、标准帧保留、相同画面回执、模拟页面像素替换、关闭重开旧token、迟到部分帧、USB→Wi-Fi切换和30秒硬截止。仅用RLE合成像素，不包含Windows原生JPEG/UI/串口实机。冻结7531源摘要及当前镜像／说明摘要再次一致。诊断明确记录：成功任务将lastMemoryPhase清0，后续0不是“未发生过内存失败”。无产品源码修改、无新固件；证据`artifacts/development/tab5-274-offline-20261025/RESULTS.json`。

Offline follow-up: the user is away, so visual acceptance remains pending and no device is operated.54 actual-worker/codec/vendor/cJSON native UBSan fault cases pass with every tracked task allocation freed, including allocator/PSRAM fallback, output growth/limit and repeated rotated captures. Both production host receivers pass2152 checks over the same2169 synthetic events, including explicit retry backoff, late-frame/old-token rejection, transport change and absolute deadlines. This excludes physical fragmentation, actual visual/page/reopen acceptance and native Windows JPEG/UI/transport.7531 frozen inputs and image/notes hashes still match. Successful jobs reset the memory phase, so post-success0 cannot locate the earlier physical failure. No production source or firmware image changed.

<!-- R18-REMOTE-CURRENT-END -->


<!-- R16-CURRENT-START -->
2026-10-25 当前交付：**TAB5 0.2.273-ui 已安装，Mac R16 已后台运行（PID86160）**。用户已明确授权本轮统一构建和安装，正常升级只确认一次；重启后的旧升级请求未重发。USB 核验通过精确 ELF、VALID 分区及三个实时界面样本。亮度79、音量70、非静音、提醒开启、轮播关闭、固定模型额度、15秒间隔、5分钟屏保、原选中页面和8页顺序回读一致；两次设置对话框均取消未保存。未重配网络／配对，未发送消息或清理照片草稿。

最终镜像 `be12e33d79d9f8d9bafbb0a44295f1e8938ccce10af3965bcbc055c7fe75df26`，ELF `1ada71ce11313d53c62fce3a616ff267d38bc3ad8833c2d8981449f533e9d821`；7208448字节，原升级分区余512字节。初次构建超出32字节，仅对预览控制代码使用-Os，JPEG／显示热路径与分区不变。最终7531个源摘要、同板级配置、11个RTC保留符号地址、正式准备脚本、SHA-256和实际sidecar两页渲染通过。最新照片完成提示按用户要求仅显示“已添加 N/3 张”，删除发送说明，四向添加／失败保留／重试专项复验通过。

本轮设备修复：去除周期诊断整堆最大块扫描；无线任务和正文分片主动让出、限制正文总时长；预览释放未发布staging，JPEG输出从128KiB按需增长到原上限，编码期间重复既有认证begin且不延长30秒请求期限。Mac与Windows均增加首片准备预算、内存降级后高清自动恢复，以及同像素／尺寸／文字去重。合入照片添加中提示和已获用户确认的连接诊断分组布局。

验证：完整真实LVGL回归、录音停止／文本、相机与照片、网络布局及实际状态函数、实际JPEG worker UBSan、认证准入和HTTP正文期限、6个P4对象与完整镜像通过。Mac Release及实际应用包1564项隔离检查、27项真实窗口生命周期、HTTP16项、音乐9项通过；签名有效，Keychain helper与R15相同。Windows Release交叉构建0警告0错误，90项生产预览模型通过。`swift test`受本机缺XCTest限制，Windows `--status-once`受Mac缺WindowsDesktop限制，均未记通过；ESP8266未具备实机。

安装后13:20:29至13:21:13，实际运行时间116651→160585ms，显示刷新1410→2150、显示错误0、欠载0、看门狗0，设备Wi-Fi有效回包45→65；USB确认2／中断1保持，唯一中断对应正常OTA重启。预览请求为0，本轮没有重新打开实体预览；这些是空闲运行与启动证据，不能当作高清预览／蓝闪完整验收。

**剩余用户实机复验：**持续使用是否再蓝闪；设备预览的高清、切页、关闭重开及与录音并发；新照片提示和连接诊断布局。蓝闪故障期没有有效诊断，本轮针对明确调度／内存风险修复，根因尚未唯一证明。此前.272录音两轮、单图／三图发送、提醒音等通过结果保留，长期三通道可靠性继续观察。Windows差异与待修入口见[修复参考](MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。不把Mac验收外推到Windows；未向其他聊天发送消息，无推送、标签或公开发布。

Current delivery:273 is installed with the explicitly authorized normal update, exact ELF/VALID/three-live-sample boot verification and unchanged settings. R16 is the sole bridge process. The final image is7208448bytes with512bytes partition headroom; source/config/RTC/package/actual-note checks pass. The photo receipt now only says “已添加 N/3 张”. Periodic diagnostic heap walks, wireless scheduling/body deadlines and preview memory/progress/recovery/redraw policies are addressed. Real-LVGL/production-function/P4/full-image checks, Mac1564 isolated/27 window/16 HTTP/9 music checks, and Windows90 preview-model/cross-build checks pass. XCTest and native Windows runtime/hardware checks remain unavailable. Post-install numeric samples show no display errors, underruns, watchdogs or additional USB interruptions, but no physical preview was opened. Blue-flash causation and sustained interactive preview acceptance remain explicitly pending user verification. Existing acceptance is retained; no public release or external messages.

2026-10-25 验收暂停：用户要求“后续再验收”。本轮只读取 .273／R16 数值诊断，运行3589655ms，显示错误／欠载／看门狗均0，预览请求0；蓝闪观察尚未收到结果，未标记通过。下次先完成蓝闪，再依次验预览清晰度、切页更新、关闭重开。

<!-- R16-CURRENT-END -->


<!-- R15-CURRENT-START -->
2026-10-25 当前已按用户明确授权安装 TAB5 .272，配套 R15 继续后台运行。正常升级只确认一次，重启后的旧请求未确认未重发；已核验精确 ELF、VALID 分区和三个实时界面样本，亮度、声音、轮播、页面顺序与屏保设置回读一致。用户连续两轮确认单次开始／结束、结束响应及返回很快，识别文字完整、不因停顿换行，无提前结束或连接中断。本次录音场景实机通过，移出当前阻塞项；长期偶发连接继续观察，不外推所有通道和并发场景。

.272 镜像 61594461…d587533（7207216 字节、分区余 1744 字节），7531 个冻结源摘要、完整构建、硬件配置、正式镜像／校验和及实际说明预览均通过。录音文本更新修复仅在内容变化时一次回填，保留草稿、光标、取消恢复和手动输入限制；保留旧失败与原生回归证据。第一轮用户反馈“这一次OK了，速度也非常的快”，连续第二轮“这轮也正常，一次结束且返回很快”。未额外发送测试消息。设备预览按用户要求继续暂缓；单图／三图发送通过保留。照片添加提示源码已完成：先呈现“正在添加照片…”再进行处理，成功后显示“已添加 N/3 张，发送后对方才会收到”。四向真实 LVGL、失败保留／显式重试、1–3 张数量与满额保护、相机打开回归及 P4 对象通过；尚未生成或安装后续固件，实机待统一候选。用户另再次报告预览经常提示读取超时／连接变化，仍未通过；无推送、标签或公开发布。

TAB5 .272 is now installed with explicit candidate-specific authorization and paired with the background R15 bridge. One normal upgrade confirmation was sent without replay after reboot. Exact ELF, VALID partition and three live UI samples pass; display/sound/carousel/saver preferences match. The user confirms two consecutive takes with single-tap start/stop, fast response/return, complete single-paragraph recognition and no early termination or interruption. This observed recording scenario passes; long-term connection reliability remains observation, without generalizing to all transports or concurrency.

The272 image61594461…d587533,7207216bytes with1744bytes partition headroom, passes the full build,7531 frozen source hashes, identical board configuration, production image/checksum validation and actual notes inspection. Changed transcripts update once while drafts, cursor anchors, cancellation and manual length limits remain. Earlier failures/native evidence are retained. No extra test messages were sent. Preview remains user-deferred and photo sending stays passed. Photo feedback now renders before processing and reports confirmed attachment count plus unsent state. Four-orientation real-LVGL transition/error/retry/count/full-limit and camera-readiness checks plus a P4 object pass; physical acceptance awaits a future unified image. The user again reports recurring preview read timeouts/connection-change notices; preview remains unaccepted. No subsequent image or public release was created.

2026-10-25 补充验收：用户确认 .272 连接诊断说明与内容正常，但要求继续优化布局，内容验收通过与布局待改分别记录。日程提醒音实机“声音清楚柔和，关闭正常”，扬声器音色及单次关闭通过；唯一临时事项“提醒音验收1025”已从 Mac 删除，全部事项恢复为空，用户确认 TAB5 消失并返回日常页。初存12:39，清理前回读12:37，未唯一确认改动来源，不据此另记准点计时通过。检查窗口已收起，原连接及声音设置未改。

Additional acceptance: the user passes272 diagnostic wording/content while requesting layout refinement separately. The actual speaker cue is clear and gentle with normal dismissal. The sole synthetic agenda item is removed, Mac All Items is empty and the user confirms device cleanup. Initial saved12:39 changed to12:37 before cleanup without independently established attribution; this is sound/dismissal acceptance, not a new punctuality measurement. Inspection windows close and connection/sound settings remain unchanged.

连接诊断布局后续源码完成：分为 Wi-Fi 网络与电脑桥接两块，突出状态、限制长名称边界、显示 IP 并将重连操作放到底部。结构化只读显示快照沿用原鉴权新鲜度和错误判定，不改变通道、请求或协议；保留原诊断文本入口。正常／等待／失败／未配置／长名称原生预览与已有真实指针工作流、实际状态函数 UBSan、两个 P4 对象及补丁 dry-run 通过。用户已确认实际渲染布局可以，实机待统一固件；冻结 .272 和 R15 未改。

Diagnostic layout follow-up source is ready: separate Wi-Fi/bridge cards, prominent state, bounded names, IP and a bottom reconnect action. A read-only structured display snapshot preserves existing authentication freshness/error rules and the original text API; transport behavior and protocol stay unchanged. Actual-LVGL workflow/state renders, production-status UBSan, two P4 objects and patch dry-run pass. User visual review passes; physical acceptance remains pending; frozen272/R15 are unchanged.
2026-10-25 蓝闪现场：用户报告任意页面整屏约每 5–6 秒蓝闪，时钟／触摸正常；正常退出桥接后仍闪，期间未取得有效设备诊断。保持接线和设置，正常关机再开机后蓝闪消失；恢复同一 R15（未打开预览）后，用户又观察约 30 秒未再蓝闪。恢复后的实时诊断显示刷新错误 0、欠载 0，USB／Wi-Fi／BLE 已认证，预览请求 0。只记恢复，不记根因已修复；无新固件。连接诊断新布局源码预览已获用户“这版布局可以”确认，实机仍待后续统一候选。

Blue-flash incident: the user observed full-screen flashes on every page at roughly 5–6-second intervals, with clock/touch still responsive. Flashing persisted after normal bridge exit; live device diagnostics were unavailable during the fault. A normal power cycle with wiring/settings unchanged cleared it. After restoring the same R15 without opening preview, the user observed another approximately 30 seconds without flashing. Fresh diagnostics report zero flush errors/underruns, authenticated USB/Wi-Fi/BLE and zero preview requests. Recovery only; root cause and permanent fix remain unverified. No new firmware. The user accepted the diagnostic-layout source preview; physical acceptance awaits a future combined candidate.
<!-- R15-CURRENT-END -->

以下保留此前阶段的检查点与证据；当前状态以上段为准。 / Earlier checkpoints and evidence follow.

2026-10-25 当前实机检查点：用户已分别授权统一生成及安装 .271；正常升级只确认一次，设备重启后旧请求未确认，未重发。第二次只读核验通过精确 ELF `d384d691…fece3f9`、VALID 分区及三个实时界面样本，首个泛化失败保留。匹配 R14 已正常替换 R13，旧 PID／监听先退出，当前唯一 PID65305及完整路径已核对，USB数据、Wi-Fi认证及BLE待命恢复；源167／Debug与最终Release1505／HTTP16／音乐9／原helper及签名通过，XCTest仍缺模块。亮度79、音量70、非静音、提醒开启、轮播关闭、8页选择／顺序、15秒及5分钟均保留；固定页面由升级前模型额度变为当前总览，期间用户明确切过页面，不归因升级、不自动改回，两个设置窗口均取消未保存。

最新录音仍未通过：用户在预览并发期间报告文字返回但显示连接中断／恢复取回，等待数秒；Mac最终识别64ms，至首次响应生成43945ms，USB多次约12KiB状态／保活请求读0字节超时，设备最近语音请求6660ms。仅R14持串口，实时栈在保活回执等待／重连，未捕获Codex目录gate。关闭预览后近5分钟没有新增USB超时，不能单凭对比唯一归因；已请求一轮不发送的纯录音隔离验收，异常即停。设备预览在R14基本可延时更新，但仍为640×360标准画质，用户明确要求先暂缓、继续其他验收；保留内存／清晰度／延时及并发故障，不继续修改或生成预览固件。预览与检查窗口已关闭，桥接后台运行。单图／三图发送原通过保留；无推送、标签或公开发布。

Current physical checkpoint (2026-10-25): explicitly authorized271 was installed with one normal GUI confirmation, no resend. A second read-only check proves exact ELF d384d691…fece3f9, VALID partition and three live UI samples; the first generic failure remains recorded. R13 exited before matchedR14, sole verifiedPID65305, with authenticated transports restored. Source167, Debug/finalRelease1505, HTTP16, music9, unchanged helper/signature pass; XCTest is unavailable. Sound/cycle/saver settings match; quota-to-overview page difference occurred amid reported user navigation and was not automatically restored.

Recording still fails during concurrent preview: final recognition64ms, first response43945ms later, repeated ~12KiB USB status/heartbeat timeouts with no bytes read; latest device voice request6660ms. R14 is sole serial owner; the stack waits on heartbeat replies/reconnects without a captured Codex gate. A five-minute interval after closing preview adds no USB timeouts, without proving a unique cause. One pure-voice, no-send isolation round is pending. Preview now mostly updates with delay but remains640×360 standard quality; the user explicitly defers its quality/memory/latency/concurrency work. No further preview source/image is being generated. Inspection/preview windows are closed; prior photo acceptance stands. No push/tag/public release.

2026-10-25最新失败／修复中：R12的第一轮USB录音在点结束后显示连接中断，第二轮已停止。Mac实际收到手动stop200，最终识别75ms；首次终态响应生成晚10982ms。即时线程采样捕获USB异步操作在MacTab5Codex.handle等待目录gate，后台目录刷新持有gate；没有保存请求内容。R13源码阻止语音期间的后台read/read-ack占用邮箱，非语音时read-ack也不等待gate；保留原最新turn验证。识别完成后的USB优先级持续到同一voiceId／nonce的全部终态分片ACK，旧take／旧ACK不能释放新take，丢包仍保留，离线30秒有界释放；8秒录音截止不变。Debug1501项（含实际加密dispatcher、最终结果未交付／迟到回包、实际Codex锁争用）通过，R13完整Release构建中，当前仍运行R12，未部署新包。设备补丁另让后台已读回执在voice.busy结束后再申请邮箱，90秒加一次250ms有界等待，原生worker和P4对象通过；主worker栈保持528B，等待快照单独2784B。无新固件，当前.270未关闭录音故障。

2026-10-25当前运行：R12完整包`artifacts/mac-usb-voice-priority-r12-complete-20261025/AIBotBridge.app`，R11正常退出并确认旧PID／监听消失，唯一PID55328，原生USB数据、Wi-Fi认证及BLE认证待命恢复。Debug／Release各1478项、HTTP16、音乐9、原helper／深签名和166源文件哈希通过；完整XCTest仍缺模块。USB录音先认证和RPC，必要状态／原保活不删；普通状态包内嵌和独立图片片均在录音期间暂停。已请求保持现有USB／模式的两轮20秒复验，与Wi-Fi对比一样正中半秒结束；结果待回，不算录音通过。固件保持.270；设备按钮／ready竞争及预览内存源码均未刷入，未生成下一镜像。以下R11/R12构建中条目按历史保留。

2026-10-25仅Wi-Fi对比通过：用户反馈本轮OK；Mac核对Wi-Fi查询200、结束原因为“设备请求停止收音”，最终识别60ms、至响应452ms，随后原生USB数据确认。该次使用圆按钮正中按住半秒，不能把单次对比当作USB根因的唯一证明。R12新增USB现场录音先认证／处理RPC、再发布普通状态；录音期间完全让出可选图片片，首次start后重查busy，空闲顺序和必要心跳保留，认证／通道失败不重放。Debug1478项通过，Release最终包构建中；尚未部署／实测。固件仍.270，圆按钮／ready竞争及预览内存补丁仍仅源码检查通过。

2026-10-25最新追加：R10＋.270复验仍失败。用户确认任务页圆形按钮第一次按后仍显示可以说话／录音计时、按钮不变。Mac最新为八秒无有效语音请求结束，最终识别139ms、最终结果至响应319ms；当前只读栈未再捕获NSHost阻塞，不能把后续故障算已修复。R11仅增加显示设备既有USB语音耗时字段，Debug／Release各1464项及签名／helper通过，正常替换R10后唯一PID54379运行；设备实读上次请求9249ms、序号0、阶段1、HTTP0（Mac收音的USB请求不使用HTTP，0不代表HTTP失败）。已请求一次10秒仅Wi-Fi对比，要求测试后恢复原模式；结果未回，录音保持未通过。

独立设备源码已增加72px有效命中范围和按下即停止、松开不重复／不重启；原56px按钮中心9次完整点击原生可通过，未把用户实机故障声称已复现。新25点真实pointer＋周期刷新、快回包／再次开始检查通过；另以原生产回包更新块复现停止／取消后被旧ready覆盖，新互斥区检查修复通过UBSan，两个实际P4对象及开始／相机回归通过。这些仍未刷入。预览内存源码保持待统一候选；R11原生观察到640×360 USB标准画质完整帧，说明降级可用，不能代替高清／长期稳定验收。预览已关闭以免干扰语音对比，无新固件生成。

2026-10-25追加实机失败：.270两轮录音首轮单次开始／结束正常，第二轮未点结束即返回“已取回识别文字，本段可能不完整，请检查后补录”；R9诊断记录八秒无有效设备请求。现场线程采样抓到USB队列在`Host.current().addresses`阻塞，且`lanConfiguration`持共享锁。修复改用数字网卡枚举、锁外查询并复核监听状态；旧生产getter的并发心跳检查先失败，新实现Debug／Release各1462项、窗口27项、HTTP16项、音乐9项及原helper／签名通过。已正常退出R9并确认旧PID及监听消失，R10从`artifacts/mac-lan-voice-r10-20261025/AIBotBridge.app`唯一PID53156运行，USB／Wi-Fi／BLE认证恢复；检查窗口已关闭，两轮录音复验已请求。设备预览另报内存不足，.270预览保持未通过；桥接已加入v2内存降级，设备侧仅回收可重建缓存／暂停预热的后续源码的实际worker/JPEG、缓存归属、准入与P4对象检查通过，尚未生成或刷入新固件。照片单图和三图＋文字的明确通过不受影响。


本文件汇总当前状态；各项证据和当时失败保留在 [后续修复记录](MAC-REMAINING-FIXES-20261024.md)。旧阶段清单中的“待验”不能覆盖后来的明确通过反馈。当前 Mac 为 `artifacts/mac-lan-voice-r10-20261025/AIBotBridge.app`（R10；正常切换、唯一PID53156，三通道认证恢复）；TAB5 为 `0.2.270-ui`（已获本候选明确授权并通过正常桥接安装，精确ELF／VALID分区／三个实时界面样本已核验，展示／声音设置回读一致）。

当前为已安装.276／Mac R22；高清、自动切页和本轮提速均获用户确认，长期及前台回收边界保留。下表汇总已确认结果；各行注明原实测版本，不要求重复已通过项目。用户已在本轮反馈Codex切页10秒内跟随，两次基本重开也取回新高清帧；本轮高清和切页提速通过，蓝闪及长期稳定仍保留现场边界，自动测试不代替这些结论。

| 本轮项目 | 当前结果 |
| --- | --- |
| 快捷发送、录音中断 | .272＋R15 连续两轮单次开始／结束、快速返回、完整单段文字且无提前结束／连接中断，通过本轮场景。历史失败保留，长期连接继续观察；单图／三图发送保持通过 |
| 任务列表留白、同步提示入口 | 切换列表正常、录音直接开始；未出现同步提示后的清除分支不算实测 |
| 常用任务固定 | 指定分组排首及任务显示通过，临时固定已清理 |
| 日程语音标题 | 空名称超长前缀及 32 字提示实体通过；多字节／光标边界为原生检查证据 |
| 日程基本同步、离线编辑 | 新增、改名、删除、离线回传无重复及清理通过 |
| 日程并发定义修改 | 两份改名各保留一条，双方已核对；两条测试项已从 Mac 删除，用户后续确认 TAB5 也消失 |
| 日程离线编辑后正常重启 | 用户确认离线改名仍在、重连单条；Mac 精确回读同名无时间事项通过 |
| 日程离线删除后正常重启 | 用户确认离线删除后重启／重连均未复现；Mac 精确回读为空，清理完成 |
| 日程到时提醒、延后 | 按时弹窗及十分钟延后回传通过，测试项双方已清理 |
| 提醒／删除窗口布局（.268实测） | 用户确认长名称、按钮、单次“知道了”关闭及删除窗口布局正常；取消保留／再确认删除通过，Mac“全部事项”刷新为空，测试项双方清理完成。提醒声音结果见下一行 |
| 日程提醒音优化 | 用户选择轻柔双音，第一版要求更清晰；第二版0.68秒试听明确接受。独立日程声音源码、实际输出函数UBSan与原有音色字节比较、日程／倒计时UI回归及P4对象通过；现有 .272 实机声音清楚柔和、关闭正常，通过；唯一测试事项双方清理完成 |
| 每天重复提醒 | 用户确认“知道了”关闭正常，15:41 后 TAB5 唯一事项已顺延 10-25 15:39，Mac 独立显示相同；双方测试项已清理 |
| 公历／农历生日 | 单条公历显示、农历新年、缺失所选闰月时回退及清理通过；不是全部历法年份遍历 |
| 跨年日历 | 2027节气、未知放假安排显示通过；用户在.268确认2026-10-10只一处“调休补班”，去重实体通过 |
| 配置迁移 | 实际导出／预览／空选择拒绝／仅生日选择恢复／自动备份恢复原值通过，七项设置一致；不替代 Windows 实机往返或故障注入回滚 |
| Mac 菜单栏、设备预览入口 | .279 用户确认高清保持、及时切页、相机／录音后恢复高清；Mac 两次实际关闭重开恢复高清。.278 已通过持续预览中的音乐单次控制。本轮项目通过，长期稳定性不外推 |
| 周期整屏蓝闪 | 用户确认 .279 本轮高清预览、切页、拍照和录音期间没有蓝闪；长期观察继续，未证明唯一根因 |
| Mac 生日／日程重开 | 旧提示清理、确认放弃不再带回草稿，以及刷新／继续编辑保留草稿，本机正常窗口通过 |
| 解锁 | 普通 USB／Wi-Fi／BLE、取消再试、USB 中断停止、睡眠后手动醒来再锁解及三十秒不回锁场景通过；受保护唤醒起点及长期稳定性未外推 |
| 通信传输测速 | USB 六轮与取消／重启通过；仅 Wi-Fi 六轮上传／下载 319.2／153.3 KiB/s；修复 Mac USB 归属误判并接入 RPC 批量通知后，仅 BLE 六轮 5.9／5.7 KiB/s。每轮各 256 KiB、错误均 0，不写 TF；用户已明确恢复原连接模式并确认连接正常 |
| 图库写卡测速 | 当前 Mac／`.267` 首轮 USB 完成 8/8，96.2 KiB/s、错误 0；中途停止后两处终态一致，再次完整 8/8、76.2 KiB/s、错误 0，连接恢复且窗口关闭。独立测试目录保留原图库，不外推 Wi-Fi 或纯 Flash 测速 |
| 日常 Wi-Fi 状态轮询 | 网络已关联、目标地址匹配、Mac／USB 正常，但有效 Wi-Fi 状态计数停止增长，HTTP 回包阶段异常；同包正常重启未恢复，用户确认仅 Wi-Fi 也异常并已恢复原模式。Mac 生成／发送及时；设备 Wi-Fi 重连及退出网络工具均无改善，整机重启后 14/14 有效回包恢复。短蓝牙传输／停止／恢复原模式后持续回包正常，持续中断未复现；根因未定；用户已明确列为后续观察并授权继续统一固件  .277 前新黄色现场经 Mac R24 同请求证明压缩恢复，用户确认；重连和重启未恢复的失败样本保留，原接收阶段未唯一定位。 |
| TAB5 电脑与网络布局 | .279 用户确认备注为“USB、蓝牙状态见‘网络连接’”，且主页面确有相应状态；分组、布局和导航说明通过本轮 |
| 国内节气源联网 | 已停止请求香港天文台，改读中国科学院紫金山天文台官方索引与年度 PDF；2025／2026 原生下载、48 日期两端解析及 Mac 实际缓存更新通过。索引未列出的未来年份保留原离线数据；日本源未部署，不作回退 |
| USB长请求及连接回归 | R4单实例仍掉线，R5修复同步等待阻塞保活；实机最长21.26秒请求、41次等待保活、USB1／中断0，用户浏览约一分钟未掉线；该路径回归通过，整体长期稳定性继续观察 |
| 蓝牙／USB切回 | 用户确认仅蓝牙30秒持续正常、恢复原模式后USB正常；有限场景通过，不外推全部业务与长期稳定 |
| Apple Music封面与控制 | .278 已安装，用户确认预览同时打开时两次控制均单次生效、状态稳定；本轮争用修复通过。既往封面高清、方向、黑线与曲目匹配验收保留 |
| 照片附件发送 | .269首次三图发送返回503；用户随后分别确认单图＋文字、三图＋文字均成功发到“TAB5 操作验收”，附件发送场景通过。早先503具体原因未定，保留失败记录及新增预检诊断，不要求继续重复发送 |
| 照片添加提示 | .277 实机添加中与完成提示均正常，已无“发送后对方才会收到”备注；测试照片已移除，预览恢复高清 |
| Codex 查看回复（长开发会话） | 已修复：32 轮 full 回包约 23 MB 超过 8 MiB 接收限制。R26 分批读取、Release／2829 项隔离检查通过，用户重新打开一次后确认回复显示、不再超时 |
| 相机后续等待 | .269用户明确“相机延时的问题解决了。超过三张也立马提醒了。”，打开与满额提示实体通过；未独立量秒。单图／三图发送已通过，见上行 |

## 历史检查点（以下保留当时的版本和待办）

历史英文状态：R5's actual21.26s host work completes41 waiting heartbeats with USB1/0; the user's one-minute browse has no drop. This specific regression passes; extended reliability remains observation. Installed268 calendar de-duplication and music artwork/control pass. TAB5 mirror freezes installed268; normal power cycling restores clock/touch/USB. Actual SDK/production-function checks reproduce a cache-mutex leak; the one-line aligned-M2C fix passes128 rotated captures and failure paths plus P4 driver-object compilation. Mac quarantine passes1370 Debug/Release checks and is deployed as sole PID28727, with USB1/0 and no preview commands. The diagnostic layout follow-up removes unrelated pairing/activity/voice toggles and corrects the Wi-Fi-only description; native workflow/visual/P4-object checks pass. Camera-entry, USB-delta lease, media-tap and preview-cache follow-ups remain source-only for a future combined image; UI/layout checks continue. Unified269 is installed with explicit authorization and exact ELF/VALID/three live UI samples verified; settings match. User passes camera latency and immediate three-photo-limit feedback. Photo sending remains pending; no public release occurs.

2026-10-24 录音可靠性重新排查：Mac实读出现12–17秒回复读取；最近一次本地识别停止至最终结果73ms、最终结果至响应生成4237ms，不能据此归咎识别引擎。源码确认普通任务页在后台读取/同步时直接忽略开始点击，USB/BLE单邮箱语音请求遇忙零等待会立即进入恢复路径。新增独立四文件补丁：可取消的单次录音准备（绑定任务/会话/页面、20秒上限、阻止新后台读取）、立即显示停止意图、语音请求发布前最多2秒准入；不重放已发布请求、不改总请求期限。真实LVGL专项、生产命令/恢复函数UBSan、USB/BLE实际exchange及三个P4对象通过。未生成新固件，设备仍.269。Mac最新公开回复缓存和发送预检固定阶段诊断通过1387项Debug隔离检查，正式包构建中；完整XCTest因CLT无模块不可运行。

Recording reliability is reopened after .269 photo-send/recording/start/stop failures. Native checks cover one queued click, cancellation/context/timeout, retained text/photos, immediate stop intent and bounded pre-publication USB/BLE admission without replay. Three actual P4 objects compile. No new firmware image is generated and hardware acceptance remains open. Mac Debug passes1387 isolated checks; Release deployment is pending. Full XCTest is unavailable in the installed Command Line Tools.

2026-10-25最新：用户确认单图／三图加文字发送均成功，照片场景通过；再次报告录音开始不稳和提前结束，保持未通过。预览首帧能显示，但打开／切页间歇停更后自行恢复，相同画面不断重绘且640×360清晰度不足；用户确认整机时钟、触摸正常。Mac实读11完整帧、1超时、部分新租约相对旧状态快照越过30秒上界。旧设备帧传输中止后不重试相同请求，主机旧逻辑等待30秒；新桥接缩短无进展等待、按快照收窄租约、重复打开保留会话、同像素不重绘。新增v2高清JPEG及未变化回执，Windows同步适配。Mac隔离1415项（含实际固件编码的四象限JPEG）、真实窗口三轮27项、Windows生产模型40项和跨编译通过；Windows桌面运行及完整XCTest不可用。实际SDK／驱动64次全分辨率旋转采集＋128次旧尺寸检查、生产worker与JPEG编码器UBSan、五个P4对象通过；ASan在本机初始化阻塞，未作为通过证据。TAB5仍.269，Mac已切换R9唯一PID47902，USB确认；统一.270完整构建、硬件配置、7529源文件、镜像／ELF／SHA、实际303字节说明预览均通过；未安装／未发布，待对应授权。

Latest: single- and three-photo messages pass on269; recording reliability and intermittent/stale/redrawing/low-resolution preview remain open. Bridge lifecycle/lease/progress changes and negotiated HD/unchanged-frame support pass native/production-model checks.270 is built and validated, including actual notes preview; R9 is deployed as the sole bridge with USB confirmed. Firmware remains269 pending candidate-specific installation authorization. Full XCTest and Windows desktop execution are unavailable; ASan initialization is not counted as a pass.

2026-10-25安装后检查点：用户已明确“现在安装 .270 并集中验收”。正常升级界面只选择并确认一次，设备重启后版本.270；旧升级请求显示未确认，未补发。首次USB启动核验通用未确认，未保存其原始失败样本、不唯一归因；随后第二次只读核验通过精确ELF `3faf0c24…2714c08`、VALID分区和三个实时界面样本。亮度79、音量70、非静音、提醒开启、轮播关闭、固定总览、15秒间隔、5分钟屏保，以及原选中页面和8页顺序回读一致；两次设置窗口均取消未保存。R9继续唯一PID47902后台运行，USB／Wi-Fi恢复。已请求两轮单次开始／结束录音（不发送、不同时打开预览）；录音和高清预览均尚未记录实体通过。

Installed270 with explicit authorization through the normal bridge UI, confirmed once. The old upgrade receipt remained unconfirmed after reboot and was not resent. A second read-only boot check verifies the exact ELF, VALID partition and three live UI samples; the first generic failure has no raw evidence for unique attribution. Display/sound preferences match and both dialogs were cancelled. R9 stays in the background; recording and preview acceptance are in progress.

> 以下保留.267／构建中各阶段的历史检查点；版本、待验和当时失败均以本表及后续明确结果覆盖。


## 现有固件继续项

- 每天重复提示／关闭／本次结束后顺延及双方清理已通过。按钮只关闭提示，不将其误称为完成事项。
- 仅 Wi-Fi 模式实际六轮各 256 KiB、错误全 0，平均上传／下载 319.2／153.3 KiB/s；USB 控制保留。仅 BLE 首测不可用和旧逐片路径超时分别保留，Mac 已修复实际 USB 归属判断并接入批量通知；最终六轮 5.9／5.7 KiB/s、错误全 0，语音八片窗口保持。原连接模式已由用户明确恢复，连接正常；随后新增的日常状态轮询异常另列。
- 图库 USB 完整写卡、中途停止及停止后恢复均已通过；后续仅在确有新问题时复测。

原五项修复的 15 个产品文件已核对一致；网络布局仍只修改其中 ui.c，其他 14 个产品摘要保持一致。另修正 wifi_link.c 把请求等待误显示为暂不可达的问题，当前共 16 个产品文件、七项固件源码变更。网络布局原生流程／指针检查、状态文案生产函数检查及两个目标对象编译已通过；统一构建仍中止，未生成应用镜像。常规 Wi-Fi 故障目前仅经整机重启恢复，短测及恢复原模式后未复现，不能写成根因已修复；用户已明确转为后续观察并授权继续统一构建；随后新增音乐封面问题，当前先修复音乐。环境缺失、首次权限及会改变既有配网／配对的分支沿用下方边界，不把这些项目虚记通过。

用户已手动解锁 Mac，并明确确认临时“仅 Wi-Fi”同样异常、测试后恢复原连接模式。随后 USB 实际诊断为请求 2387、有效回包仍为 1719、HTTP -1、连接错误及 socket 错误均为 0，RSSI -37。只能说明问题并非仅多通道切换时发生，尚未据此确定根因；继续以不含内容／凭据的 Mac 响应计数与耗时缩小范围。

## 统一固件安装后的实体复验

- 相机内部内存调整：首次打开、拍摄、关闭重开、日程／语音后再开；照片显示与附件／上传另外核对。
- 日历 2026-10-10 补班标签去重。
- TAB5 实际屏幕镜像：画面内容、方向、尺寸、关闭／断连及相机／语音并发；不以原生模拟截图替代。
- 新日程提醒与删除弹窗：真实触控、长名称、正常延后／关闭／删除。
- 电脑与网络新布局：电脑切换／备注、Wi-Fi 选择／删除、独立诊断页及返回。

源码补丁已保留相机、日历、镜像、提醒和删除布局五项独立来源／验证摘要。当前固定 latest SHA-256 为 `60ccffe6c2e1625a36b11ee3ad60eeec72b737b02b11619ad51fd538d08ff0e4`，没有因单项修复逐次生成镜像。

## 环境边界与用户已延后项

- 用户确认当前无 ESP8266 小屏；不虚记实机回归，也不改其默认展示设置。
- 本机无 Spotify、Windows／Intel 实体环境和完整供应商账户集合；相应场景不能写成全覆盖通过。
- 完整 XCTest 受当前 CLT 缺模块限制；已有独立检查、Release 和原生实体证据分别登记，不能互相替代。
- BLE 约 4–5 秒停止回显为用户接受的当前水平，进一步提速延后。
- 首次权限拒绝、重新配网、破坏现有配对等未覆盖分支保留边界，不为了补数重置用户日常环境。

## English

This is the current checkpoint; older chronological pending entries do not override later physical passes. Continue acceptance and source fixes on installed `.267` before generating one combined image. Offline edit and deletion survive normal power cycles and reconnect correctly, with cleanup complete. USB RAM transport cancellation and a subsequent full six-sample run pass with zero errors; no TF writes. Wi-Fi-only and BLE-only benchmarking also pass all six 256 KiB samples with zero errors, averaging 319.2/153.3 and 5.9/5.7 KiB/s respectively. BLE initially fails unavailable and later times out on the old per-fragment path; the final Mac build fixes USB ownership and uses bounded RPC notification grants. The user explicitly confirmed restoration of the original mode and a normal connection. Configuration selected restore and backup recovery pass without changing seven settings. Concurrent agenda definitions preserve both versions. Compact reminder and matching deletion layouts await combined firmware installation with camera-memory, calendar-label and screen-mirror changes. Solar-term updates now use the Chinese Academy of Sciences Purple Mountain Observatory official calendar index/PDF documents, replacing HKO network requests. Native 2025/2026 downloads, 48 matching Swift/C# dates and actual Mac runtime-cache updates pass. Future years absent from the index preserve verified offline dates. An evaluated NAOJ source was withdrawn before deployment and is not a fallback. The running signed Mac build passes 1158 isolated checks; full XCTest and Windows status-once remain unavailable in this Mac environment. Hardware/account limitations and deferred BLE speed work remain separate. No firmware generation, flashing, version change or publication occurred.

A later ordinary Wi-Fi status-polling issue is open: Wi-Fi is associated, the target matches this Mac, USB is healthy, and authenticated requests reach the Mac, while the device valid-status counter stalls. A normal restart of the same signed Mac bundle does not recover it. This is separate from the earlier successful RAM benchmark. The user confirmed the same failure in Wi-Fi-only mode, restored the original mode and manually unlocked the Mac. A later USB sample records 2387 attempts, 1719 valid statuses, HTTP -1, zero open/socket errors and RSSI -37. The failure is not limited to transport switching; root cause remains open. Bounded Mac response timing/send counters are being added without contents or credentials. The user added a TAB5 computer/network layout redesign, now implemented and checked with native workflows, real pointer navigation/modal blocking and target UI object compilation. The preliminary .268 build was interrupted before generating an application image; latest remains .267. Resume the single combined build after this new issue is resolved.

## 常规 Wi-Fi 最新检查点

- Mac Release 诊断版已正常替换临时 Debug 版，签名及固定 helper 身份校验通过，1158 项隔离检查通过；完整 XCTest 仍缺模块。所有检查窗口已关闭。
- 故障期 Mac 现场响应约 5 ms、发送本机栈成功，设备有效计数仍停在 1719，内核 TCP 大量重传。重启后短测另观察到一次最大生成 4329 ms，超过设备 3000 ms 头部等待时限，具体等待点尚未拆分；后续回包能自行继续，不是持续中断已复现。发送本机栈计数不能替代设备回执。
- 仅 Wi-Fi、同包重启 Mac、设备 Wi-Fi 重连、临时完全退出代理／VPN均未恢复；用户已恢复原连接模式和网络工具。随后 TAB5 正常关开机恢复，实读 14 次请求、14 次有效回包、HTTP 200，运行 41429 ms。根因及触发步骤尚未确立。
- 短 BLE 传输已停止确认：首个上传 262144 字节、42400 ms、错误 0；下载在 147456 字节／26570 ms／错误 0 时停止。不是完整六轮重测。停止后 Wi-Fi 有效数 304→319；用户确认恢复原模式和提示正常，Mac 随后读到 368 次请求／367 次有效、HTTP 200，运行 973539 ms。所有检查窗口已关闭。
- 七项固件源码、16 个产品文件已合入冻结源 `artifacts/development/tab5-combined-268-20261024/source` 并核对摘要；没有继续构建镜像。用户已明确选择“列为后续观察，继续统一生成固件”，不再等待 Wi-Fi 处置。随后新增 Apple Music 封面缺失、方向颠倒及串歌问题，当前先修复音乐，镜像仍未生成。
- 固件源码只把“请求正在等待”与“已完成但没有有效响应”分开，保留有效心跳优先、鉴权失败及断网状态；不改协议、不声称修复数据链路。未生成或刷写新固件。

The signed Release diagnostic bundle replaces the temporary Debug build and passes 1158 isolated checks, signature/helper identity checks and nine slow-reader cases; full XCTest remains unavailable. Live HTTP responses are generated promptly, while local stack acceptance is explicitly distinguished from device receipt. Wi-Fi-only mode, a Mac restart, device Wi-Fi reconnection and fully exiting the network tool did not recover the fault. A normal TAB5 power cycle did: the post-boot device sample has 14 attempts and 14 valid responses, HTTP 200. Root cause is still open. The short BLE comparison now stops cleanly after a 256 KiB upload and a partial 144 KiB download, both with error zero. Wi-Fi valid counts resume from 304 to319; the user confirms original-mode restoration and normal prompts. The later sample is368 attempts/367 valid, HTTP200. No persistent outage is reproduced. One host-generation maximum of4329ms exceeds the device3000ms header deadline and remains a separate observation; the exact wait point is not isolated. The frozen sixteen-file source is ready, but image building waits for the user to choose continued root investigation or an observation/deferred disposition. The seventh firmware source change separates in-flight waiting from completed failure without changing the wire protocol; native production-function checks and the target object compile pass. Sixteen product files are now in scope, with no image generation or flash.

## Apple Music 封面新增问题（进行中）

用户确认 Music 自身有封面，而 TAB5 曾提示无封面；随后报告封面倒置，以及新曲目仍显示上一首封面。原生四角测试复现 Mac RGB565 音乐图像行序翻转；方向归一化与 8 种 EXIF 方向检查通过，其他桌宠调用保留原坐标约定。现场只读诊断确认当前曲目 artwork 数量曾为 0、raw/picture 均返回 missing value；这不能证明所有流媒体封面已修复。

Mac 源码已增加有效图片后备读取、同 persistent ID 的资料库只读后备、曲目／歌手／专辑一致性核对、切歌清除旧图、两秒稳定观察、完成后再次核对曲目及同曲目定期重取。失败保留同曲目的已成功封面。Debug 构建及 1191 项隔离检查通过，其中 33 项为封面方向、无效返回、串歌拒绝和资源切换检查；完整 XCTest 因 CLT 缺 XCTest 仍不可运行。曲目绑定诊断版已启动，用户实体匹配／方向确认待回复。Release 完整打包脚本正在运行，尚不记为构建完成或实体通过。没有新增外部曲目搜索、私有框架或系统权限。

English: the user confirms artwork exists in Music but is missing or inverted on TAB5, then reports a previous-track cover. Synthetic asymmetric pixels reproduce the Mac row flip; all eight EXIF directions pass normalization checks while pet rendering retains its original convention. Scalar live diagnostics observe artwork count zero/missing values. Source changes validate candidates, try the same persistent-ID library item, bind title/artist/album, clear old artwork on change, wait for a stable sample, recheck after completion and periodically refresh. Debug plus1191 isolated checks pass, including33 artwork cases; XCTest remains unavailable. Track-bound live acceptance and full Release packaging remain pending; no firmware image is generated.

## 音乐高清通道最新检查点

本节覆盖上方旧音乐检查点。Apple Music current-track 的 picture/raw 均可能在新元数据下返回上一首有效图片；当前按同一 database ID 查当前播放列表／资料库，无法确认时以 Apple 公开目录严格核对歌曲／歌手／专辑／时长，不匹配不猜图。此目录并不覆盖全部订阅曲目，仍存在 Music 窗口有图但桥接暂无法确认的场景。用户已确认一次固定曲目、Music 窗口、Mac 内存图与 TAB5 均匹配；后续单步切歌又观察到接口封面为空，不能记为全曲目修复。

新增独立 TAB5 JPEG 通道：从原编码图片归一化方向后按最高560×336等比缩小，方图336×336，不放大低分辨率原图；Apple 目录优先下载600×600源图。小屏仍为112×112 RGB565，旧 TAB5 固件回退原尺寸。协议沿用既有 .080+ JPEG 资源，没有新增协议字段或固件依赖；当前 .267 即可验收。运行监控实际采样图优先显示高清内存数据，封面诊断增加编码与像素尺寸。

Debug 构建与1256项隔离检查通过，其中98项音乐检查涵盖目录精确匹配、8种 EXIF、高清尺寸／比例／不放大、JPEG CRC及分块、旧固件回退和曲目切换清理。公开 Apple CDN 实取600×600、HTTP200通过，图片只留内存。完整 XCTest 已重新尝试，CLT仍缺 XCTest 模块。高清实体确认待用户反馈，最终 Release 打包和桥接播放控制验收待完成；此前 Release 尝试已取消，不能记为成功。运行中为签名 Debug 诊断包；latest仍为 .267，没有生成、刷写或发布新固件。

English: this checkpoint supersedes the earlier artwork notes. Current-track Apple Events can return a previous picture despite new metadata. Exact same-ID lookup and a strictly matched public Apple catalog fallback avoid guessing; catalog coverage is incomplete. One fixed-track host/device comparison passes, while a subsequent transition has no artwork from the interface. TAB5 now has a separate aspect-preserving JPEG channel up to560×336, square336×336, with a600×600 catalog source where supported; small-screen RGB565 and old-firmware fallback remain unchanged. Debug plus1256 isolated checks pass, including98 artwork cases. An actual public CDN600×600 response passes without saving artwork. XCTest is unavailable in the CLT environment. Physical HD acceptance and final signed Release/control validation remain pending; the prior Release attempt was cancelled. Installed firmware remains .267 and no image has been generated or flashed.

### 高清包构建结果／待部署

最终 Release 打包脚本已完成（699.52秒），签名与固定 helper 身份／私有管道检查通过，实际 Release 可执行文件1256项隔离检查通过（其中98项音乐检查）。可执行 SHA-256：`f0225e6e27a8ea7073ade04ddda5ee139ee219db8dc3e1b86be0dd19b7c8afb4`；包位于 `artifacts/mac-music-artwork-20261024/AIBotBridge.app`。当前Mac锁屏，CUA无法正常退出测试包并启动新包，已请求用户手动解锁，不自动输入凭据。后台仍为高清 Debug 诊断版；正式包首次启动／现有 Automation 授权及 TAB5 播放暂停复验、高清实体确认仍待完成。新固件仍未生成。先前“最终 Release 正在构建”以本条为准；完整 XCTest 仍因环境缺模块不可用。

English: final Release packaging completes in699.52s. Signature, fixed helper identity/private-pipe probe and1256 isolated checks pass, including98 artwork cases. The bundle is prepared but has not been launched because the Mac is locked; manual unlock is requested without entering credentials. The HD Debug bundle remains active. Final-bundle launch, existing Automation-grant/play-pause checks and physical HD confirmation remain pending. Full XCTest is unavailable and no new firmware image is generated.

## 高清与控制已通过／进度最新检查点

用户确认：固定曲目高清封面明显更清楚、方向正常，随后补充“除了个别歌没有封面以外，清晰度和方向现在都可以了”。最终签名 Release 包已正常启动，进程路径核对为 `artifacts/mac-music-artwork-20261024/AIBotBridge.app`；原 Debug 包已正常退出。Mac 与 TAB5 实读资源均为336×336 JPEG、70598字节，目标及设备确认ID一致为`524de723`。用户在此正式包上确认 TAB5 暂停／恢复均正常，没有重复切换，封面正常。现有 Automation 授权可用，没有出现新授权提示；首次授权／拒绝分支未重测。所有检查窗口已关闭，桥接后台运行。

用户随后报告播放中约慢2秒；按提示暂停3秒后反馈“差1秒左右，还好”，此当前差异按用户意见记为可接受，不为对齐显示而固定加秒。另在设备源码确认进度与接收后时间分别取整，可能额外少显示1秒。独立修正为先用 double 累加，再统一取整；暂停、陈旧／不可用时间线不递增，未知时长零进度不伪造。生产头文件16项原生检查及 esp32p4 UI 目标对象编译通过，完整链接和修正后实体复验仍待统一候选。补丁 `docs/proposals/tab5-music-progress.patch` 不改协议、无第三方实现来源。

冻结源最新为 SOURCE-V4-CHECK：17个产品文件、9项固件变更（新增进度小数修正），两份候选对应摘要一致。固定 latest 仍为 .267，未生成／刷写新镜像。少数歌曲缺封面仍开放：曾观察到接口空结果或新元数据配旧图，当前仅接受同ID只读定位／公开目录精确匹配。公开 Music 网页在当前 Mac 网络下又被重定向到通用首页，未作为后备实装。已请用户决定将缺图列为后续观察并继续统一生成，还是继续排查并暂缓；在实际答复前不改变该项状态。

English: the user accepts HD clarity and orientation, except for individual missing covers. The final signed Release bundle is now running; Debug exited normally. Host/device agree on a336×336 JPEG,70598 bytes and CRC524de723. Device pause/resume passes without duplicate switching and artwork remains normal. Existing Automation consent works; first-consent/rejection paths were not reset or retested. Inspection windows are closed.

The user reports roughly2s lag while playing, then roughly1s after a3s pause and says it is acceptable. Separately, device code has a reproducible extra-second loss from truncating source position and elapsed receiver time separately. The independent fix adds fractions before truncation, preserving pause/stale/unavailable behavior. Sixteen native production-header checks and the esp32p4 UI object compile pass; full linking and post-install physical acceptance remain pending. V4 contains17 product files and9 firmware source changes. No new image or flash occurred. Individual unavailable covers remain open, and their observation-versus-investigation disposition awaits the user's answer before combined generation resumes.

## 缺封面继续排查：地区标题写法已修复

用户明确选择“继续排查缺封面，暂缓生成固件”，仍按此执行。固定暂停的缺图现场确认：当前播放器标题与 Apple 香港目录标题有“你／妳”写法差异，歌手、专辑和时长相符；原 ICU 繁简转换不合并这两个字，导致有效候选被严格标题判断排除。当前只为歌曲标题增加该地区写法兼容，歌手、专辑、两秒内时长及唯一封面条件不放宽，也不删除版本限定。

Debug 构建及1262项隔离检查通过，其中104项音乐检查，新增6项覆盖地区标题、歌手／专辑／时长仍须匹配、歧义拒绝和只限标题。签名诊断包替换旧正式包后，Mac 实际采样图与 Music 窗口一致；TAB5 资源为336×336 JPEG、45893字节，目标与确认ID均为`cdc46ad9`。用户实机确认原来缺失的封面现已出现、匹配、清楚且方向正常。窗口已关闭，歌曲仍按用户测试要求暂停；正在请用户核对另一首缺图。上一1256检查正式包的构建／控制证据保留，但新标题修复尚未最终 Release 打包，不能将旧包误认为已包含本修复。不生成新固件。

English: the user chooses continued missing-artwork investigation and defers firmware generation. A fixed paused case has a title-only 你/妳 storefront spelling difference; artist, album and duration match. ICU simplification does not merge these characters. A title-only compatibility rule fixes this without weakening other identity, duration, uniqueness or edition checks. Debug plus1262 isolated checks pass, including104 artwork cases and6 new localization/rejection checks. The signed diagnostic build delivers the matching336×336 JPEG,45893 bytes, CRCcdc46ad9 acknowledged by the device. The user confirms the formerly missing picture is now correct, clear and upright. Another missing case is being checked; final Release repackaging remains necessary. No firmware image is generated.

## 公开 Music 页面后备与第二个缺图验收

用户继续提供的第二个缺图现场确认：Music 自己显示图片，但 Apple Events 的当前 track 与同 ID 资料库读取均无可用图；旧 iTunes 购买目录也没有对应录音。Apple Music 对应地区公开搜索页能定位唯一专辑，专辑 JSON-LD 提供录音标题与时长。新增只读公开页面后备，严格核对标题／歌手／专辑／两秒内时长与唯一专辑；拒绝首页重定向、现场版和其他专辑，不执行页面脚本，不复用网页令牌、Cookie 或账户。对首页地区重定向只在该地区重试一次公开搜索，同曲目网络尝试最多每分钟一次。页面结构改变或资料不足时保留同曲目最近成功图。移除未部署且无实效证据的标识查询原型。

同时调整采样顺序：封面网络请求进行中仍读取并发布播放状态、进度和新曲目；切歌立即清旧图，完成时再次核对当前观察身份。Debug 构建、1287项隔离检查（其中129项音乐封面检查）及9项真实采样路径的无网络检查通过。完整 swift test 已尝试，CLT缺 XCTest，不能记为通过。签名 Debug 实机生成336×336 JPEG、50936字节，目标和设备确认ID均为 `1ec7aaad`；用户确认第二首原来缺失的图已出现、匹配、清楚且方向正常。前一地区标题案例也已通过。所有桥接检查窗口已关闭，用户保留的歌曲仍暂停。最终 Release 打包／启动与控制检查继续进行；当前固件仍 .267，没有新镜像。两例通过不代表全部订阅曲目均能取图。

English: a second fixed missing-artwork case has an image in Music but no usable public Apple Event artwork or matching iTunes purchase-catalog recording. A public regional Music search/album-document fallback now requires a unique album plus exact normalized title, artist, album and recording duration within two seconds. It rejects home-page redirects and other editions, runs no page scripts, and uses no tokens, cookies or account. A regional redirect is retried once and network attempts for the current track are bounded to once per minute. The undeployed identifier-query prototype is removed. Playback sampling continues while artwork awaits the network, and late artwork is checked against the latest observed identity. Debug plus1287 isolated checks (129 artwork) and9 no-network sampling-path checks pass. XCTest remains unavailable. The signed Debug bundle delivers a336×336 JPEG,50936 bytes, CRC1ec7aaad acknowledged by TAB5; the user confirms matching, clear and upright artwork. Both reported missing cases pass, without claiming universal coverage. Final Release packaging/control checks remain pending. Inspection windows are closed, the fixture stays paused, and no firmware is generated.

## 新增封面黑线现场：Mac 行宽兼容复验中

用户补充多个原缺图曲目均已能取图，但另一暂停曲目的设备封面有多条黑线。Music 原图及桥接内存预览未见黑线；Mac 发出的337×336 JPEG、42256字节，设备确认ID与目标一致为 `7f82c111`。因此取得错误封面／桥接图像本身带线／编码字节传输不一致不符合本次观测，但尚未直接读回设备解码像素。设备源码按16像素对齐的行距处理 JPEG。当前在 Mac 输出端将宽度向下取16的倍数，再按原比例选择高度；不裁切、不放大，小于16像素宽的源图保留既有 RGB565 后备。此为现有 .267 的兼容修正，不新增协议或固件依赖。

Debug及1303项隔离检查（其中145项音乐封面检查）与9项采样路径检查通过，新增近方图、奇数宽、横竖图及极小图覆盖。修正版已签名并后台运行，同曲目新图336×335 JPEG、41119字节，目标与设备确认均为 `a633b40d`。已请用户仅看原暂停现场，确认黑线是否消失；尚不记为实体修复通过，也不将最低层根因唯一归为硬件解码或 LVGL。之前启动的最终 Release 构建在此新增问题出现后正常终止（exit143），不计为成功。最终打包与统一固件继续暂缓，检查窗口已关闭，未改连接／展示设置。

English: the user confirms several previously missing covers now work, but reports black lines in another paused track. Music and the host RAM preview are clean; the337×336 JPEG,42256 bytes, has matching host/device CRC7f82c111. No device-decoded pixel readback is available. The device code handles JPEG rows with16-pixel alignment. A host compatibility change rounds width down to a multiple of16 and chooses height from the source ratio without cropping/upscaling; very small artwork retains RGB565 fallback. Debug plus1303 isolated checks (145 artwork) and9 sampling checks pass. The signed build delivers336×335 JPEG,41119 bytes, CRCa633b40d acknowledged by the device. Physical black-line acceptance remains pending; the exact lowest-level decoder/drawing cause is not claimed. The in-progress final Release attempt was stopped (exit143), not counted as success. Final packaging and firmware generation remain deferred, with inspection windows closed and settings preserved.

## 黑线链路定位与统一修复最新状态

用户明确反馈行宽调整后仍有两根横线，继续切换三首也均出现黑线，故该调整未通过实体验收，已从最终 Mac 源码撤回。随后在同一暂停曲目、相同320×324尺寸上，将 JPEG 在 Mac 解码后以无损 RGB565/RLE565 传输，设备确认ID `9ba41b0e`；用户确认黑线完全消失。该对比把编码数据从38431字节增加到305268字节，用户反馈同步过慢；这是临时定位传输，不是最终产品方案。最终源码仍采用高清 JPEG，不降低分辨率。当前后台诊断包暂保留已通过的无损图，检查窗口均关闭。

进一步检查本地 ESP-IDF 5.4.2：输出分配器使用 CPU 清零的 PSRAM，驱动只在 DMA 后失效缓存，缺少 DMA 前缓存交接。乐鑫官方提交 `446e2fc` 明确修复随机 JPEG 黑线，内容正是补充解码前失效。项目图库路径已有同类交接，音乐资源与本地 JPG 预览遗漏；现对这两处补齐已有项目的 C2M＋INVALIDATE 处理，失败时拒绝解码且不替换最近成功图。不修改共享 SDK、全局配置或协议。

实际生产 decode_jpeg 函数的8项原生模型检查通过，包括四组尺寸全部像素、缓存交接先于 DMA、缓存／引擎／解码失败清理；移除交接的负对照产生黑行并按预期失败。assets.c 与 photo_attachment.c 的 esp32p4 对象编译通过。模型与 RLE 对照不等于修复后 JPEG 已通过实体检查；后者需统一新固件。新增源码补丁 `tab5-jpeg-cache-handoff`，V5冻结源19个产品文件、10项变更，更新说明草稿951字节。全部既有修复将统一构建为 .268，尚未刷机或公开发布。Mac 最终源码保留公开页面／地区标题修复和持续采样，撤去行宽猜测及临时无损开关；最终 Release 构建与包验证继续进行。

English: width adjustment fails physical acceptance: internal horizontal lines remain across three tracks, so it is withdrawn from final Mac source. On the same paused image and320×324 dimensions, Mac-decoded RLE565 is acknowledged as9ba41b0e and the user confirms no black lines. The temporary comparison increases38431-byte JPEG to305268 bytes and is reported slow; it is not the final format. Final source retains HD JPEG. Local IDF5.4.2 allocates CPU-zeroed PSRAM but lacks the pre-DMA cache handoff addressed by Espressif commit446e2fc. The project's gallery path already handles this; music resources and local JPG previews now use the same C2M/invalidate handoff with safe failure retention. Shared SDK and wire contracts stay unchanged. Eight production-function model cases plus an old-function negative control pass; both affected esp32p4 objects compile. Fixed-JPEG hardware acceptance requires the combined image and is not inferred from model/RLE evidence. V5 contains19 product files and10 changes; draft notes are951 bytes. One combined .268 build proceeds, without flashing or public release. Final Mac source removes the width hypothesis/temporary RLE hook and retains the two accepted missing-cover fixes and uninterrupted metadata sampling.

## 统一 .268 候选已准备，等待安装授权

完整 ESP32-P4 构建和正式包校验器通过，固定入口已更新为 .268：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，7201296字节，SHA-256 `d4d021daba4319019a661a6056f2bc81c40e9f91da2f2988b410f1b11139aa10`，ELF SHA-256 `aa1b8968b25fc38e571e6222873f4933832a902a5e132f059f23989176ccf8bf`。现有0x6e0000分区校验通过，余量7664字节；.267归档保留。实际准备后的 sidecar含分类及列表，950 UTF-8字节；已运行并目视检查原生更新说明预览，包括滚动后的音乐／JPEG缓存条目。附带许可及校验文件。V5共19产品文件、10补丁，逐文件摘要通过。

临时构建脚本最初将环境版本目录写为完整5.4.2，造成 Wi-Fi Remote Kconfig未加载；失败与中间配置尝试已单独保存，不计为通过。当前只对该构建进程改为5.4目录，实际SDK仍5.4.2，板级 sdkconfig按已安装 .267 对应源码恢复，234项通道设置及全部配置值比对一致；未改全局环境或厂商源码。

最终 Mac 包 `artifacts/mac-music-artwork-final-20261024/AIBotBridge.app` 完成1091.87秒Release构建，签名／helper及实际成品1287项隔离检查、9项采样检查通过；可执行 SHA-256 `908b50fc84b83b64c88d21eca5743ea34da9b91c22ec1238d35ad2d9ece9909d`。完整XCTest仍因CLT缺模块不可用。最终成品不含临时无损开关与行宽猜测。当前仍为已确认无黑线的临时无损诊断版后台运行，正式成品启动／播放控制将与新固件JPEG复验衔接。

新镜像尚未刷入，缓存修复后的JPEG、相机、镜像与界面等实体项目仍待安装后验证。只完成本地候选；未创建标签、推送或公开发布，公开下载与版本声明仍保持原已发布版本。根据固定固件工作流，下一步安装需这份候选的对应授权；既有配对、网络及展示设置保留。

English: combined268 is built and validated at the fixed local firmware path, SHAd4d021daba4319019a661a6056f2bc81c40e9f91da2f2988b410f1b11139aa10, 7201296 bytes with7664 bytes of partition headroom. The actual prepared structured sidecar is950 UTF-8 bytes; native first/scrolled previews are visually inspected. V5 has19 product files and10 patches, with licenses/checksum and267 archive retained. An initial process-local SDK version-directory error and stale intermediate configuration are preserved as failed attempts. The repaired build uses the installed267 source board configuration; all values and234 transport settings match. SDK/vendor/global configuration is unchanged. The final signed Mac Release bundle builds in1091.87s and passes1287 isolated checks plus9 sampling checks and helper validation. Its launch/control test remains pending, while the accepted temporary RLE diagnostic stays active. XCTest is unavailable. No device flash or public release occurs; fixed-JPEG and other new-firmware physical acceptance await explicit authorization for this candidate.

最终 Mac 包同步候选验收文档后已重新签名并严格核验，实际可执行文件 SHA-256 为 `0b56a21280e1d744bec085cba69a8abe0313289b2687cf980bab743e994e9db3`；1287项成品隔离检查、9项采样检查及 helper 检查再次通过。上条908b摘要为文档同步前的签名，最终校验以包旁 `SHA256SUMS.txt` 为准。尚未启动此正式成品或刷入 .268。

English: after synchronizing the bundled acceptance document and re-signing, final executable SHA-256 is `0b56a21280e1d744bec085cba69a8abe0313289b2687cf980bab743e994e9db3`;1287 isolated checks,9 sampling checks and helper verification pass again. The preceding908b hash was the earlier signature; use the adjacent package checksums. Final-bundle launch and268 flash remain pending.

## .268 已安装并启动核验，最终桥接已部署

用户已明确授权“现在安装并继续验收”。已通过桥接常规升级流程选择固定 latest .268，设备确认开始，随后回报 .268；没有第二次刷写或改配对／网络／展示设置。USB“核验启动”确认对应精确 ELF、VALID 分区与三个独立实时界面样本全部一致。中途旧请求摘要仍显示未确认，未据此重刷，启动核验后已显示通过；该摘要观察保留在安装记录。

临时无损 Debug 已正常退出，最终签名 Release 包已启动，进程路径与最终包一致，既有 Automation 授权可用，未出现新授权提示。Mac 及 TAB5资源确认当前339×336 JPEG、36880字节，目标与设备确认ID均为 `46724182`；RLE临时对照已撤下。检查窗口已全部关闭，最终桥接后台运行。用户的黑线、同步速度及最终包暂停／恢复操作反馈仍待回传，不能以成功传输代替实体显示验收。后续相机、设备镜像、提醒／删除弹窗、日历去重与网络布局继续按新版逐项验收；未公开发布。

English: the user explicitly authorizes installing268 and continuing acceptance. Normal bridge OTA installs the fixed latest image; USB boot verification confirms the exact ELF, VALID partition and three independent fresh UI samples. No second flash or pairing/network/display reset occurs. An old unconfirmed-request summary is not treated as failure or retried; explicit boot verification replaces it with success. The temporary RLE Debug bundle exits normally and final signed Release runs with the existing Automation grant. The device acknowledges339×336 JPEG,36880 bytes, CRC46724182. Inspection windows are closed. Physical black-line/speed and final-bundle pause/resume feedback remain pending; transfer success is not physical display acceptance. Camera, mirror, reminder/delete UI, calendar de-duplication and network-layout acceptance continue on268. No public release.


## .268 音乐显示通过，封面传输节奏优化

用户已确认 .268 与最终桥接“无黑线，同步变快，播放控制正常”；随后指出进入“封面同步中”后进度仍慢，因此速度继续开放。保留 JPEG、原尺寸与质量，只修改 Mac USB 发送调度：原每100毫秒一片改为每轮最多4片、累计60毫秒后让出队列；每片仍独立校验 ACK。状态与 RPC 先处理；每片前检查停用／配置操作／状态期限，录音或识别中每轮最多一片。串口460800、协议和固件不变。

RAM 内数值计时实测，同一50936字节 JPEG 从就绪到设备确认由6253毫秒降至2236毫秒，缩短64.2%；两次USB收发累计分别405／578毫秒，分片数受其他已鉴权通道带片影响，不能将其当作独占USB链路吞吐。另一43103字节 JPEG 为2716毫秒；这些是当前接线与配置下的现场样本，不是全部连接模式的保证值。未保存歌曲信息或图片。Debug构建与1294项隔离检查通过，新增7项生产调度检查覆盖时间／数量上限、优先工作、资源耗尽和失败回到重连；完整swift test已尝试，当前CLT缺XCTest，不能记为通过。优化后实体观感与控制反馈、最终Release打包／启动仍待完成。原黑线与控制通过结论保留，其他 .268 项目继续待验收。无新固件或公开发布。

English: the user accepts268 artwork without black lines and normal playback controls, but reports slow progress after artwork synchronization begins. Mac USB now uses the existing1KiB+ACK exchange in bursts bounded to4 chunks/60ms. Status/RPC precede resources; enablement, management and status deadlines are rechecked per chunk, and active recording/recognition retains at most one resource chunk per turn. Baud, protocol, JPEG quality/dimensions and firmware stay unchanged. The same50936-byte JPEG improves from6253ms to2236ms ready-to-device-ACK (64.2%); another43103-byte sample takes2716ms. Authenticated channels may carry other fragments, so these are live end-to-end samples rather than exclusive USB throughput guarantees. No media/metadata is persisted. Debug and1294 isolated checks pass, including7 scheduling/error cases. Full XCTest is attempted but unavailable in the CLT environment. Optimized physical speed/control feedback and final Release packaging/launch remain pending. Other268 acceptance continues; no new firmware or public release.


## 封面提速后续与新增相机等待问题（最新）

初版四片调度虽有6253→2236毫秒同图样本，后续另一轮仍5372毫秒，用户明确反馈同步前后均要等数秒，不能记为速度通过。进一步 Mac USB 沿用设备明确公布的 usbPacked／usbDelta 能力与既有完整／增量状态格式；与BLE使用独立基线，仅在精确设备及序号ACK后推进，断连／重新配对清空，OTA／展示命令保留完整状态，未知能力回退原始完整帧。表示层拒收0／1／2仅以同一帧重试一次原文，身份／序号／超时不重放。没有新增协议或修改固件／Windows。

最后固定 Debug 调度为最多8片、累计60毫秒；录音中仍最多一片，状态期限与控制先行。曲目稳定等待由2秒减至0.5秒，保留同ID或公开目录严格匹配、完成后再读当前曲目的校验。相同50936字节 JPEG 最新就绪到确认1637毫秒；首片53毫秒，USB46片／46840字节／累计559毫秒；9轮，观察最大批6；状态生成认证824毫秒、状态总1191毫秒，102592字节原始状态经压缩／增量为9340字节，邮箱44毫秒。多通道可能带片，非独占USB吞吐保证。Debug、1319项隔离检查、9项采样检查通过，新增总32项调度／能力／有界回退与基线检查。固定版用户观感、最终Release打包／部署仍待确认。首轮Release在新增问题后正常停止，未计成功。

相机最新实体结果：能打开并添加照片，但一张以内也常在“正在准备相机”停十秒以上；三张后第四次点击同样久等，最后才提示上限；用户另见任务列表消失后重载。源码该提示位于任务阅读／草稿同步等待，早于摄像头初始化，三张上限又在等待分支之后。正在检查并修复入口；不扩大为硬件驱动根因。测速期间桥接曾多次正常切换进程，也可能造成任务列表重载，此干扰已明确告知用户，不能直接归因相机或设备重启。现保留用户照片和草稿，不发送、删除或重启TAB5。相机仍未通过；不生成下一份固件。

English: the initial4-chunk sender has a6253→2236ms same-image sample but later reaches5372ms, matching the user's continued delay report. USB now negotiates existing usbPacked/usbDelta capabilities, with a baseline independent of BLE, exact device/sequence ACKs, reset on reconnect/pairing, full frames for OTA/display commands and raw legacy fallback. Representation errors0/1/2 allow one exact raw-frame retry; identity/sequence/timeouts never replay. The final fixed Debug uses at most8 chunks/60ms and retains voice/control priority. Settling decreases from2s to0.5s without weakening artwork identity checks. The same50936-byte JPEG completes in1637ms:46 USB fragments/46840 bytes/559ms,9 turns/max6, status generation/authentication824ms and total1191ms,102592 raw status bytes represented in9340 bytes, mailbox44ms. These are live mixed-channel samples. Debug plus1319 isolated and9 sampling checks pass; fixed-version perception and final Release packaging/deployment are pending. The earlier Release attempt is stopped and not counted as success. Camera now opens/adds, but even the first attachment waits10+ seconds in the pre-camera synchronization gate, and a full draft waits before reporting its three-photo limit. Task-list reload may also coincide with diagnostic bridge restarts, an explicitly disclosed confounder. Photos/drafts remain intact; no send/delete/device reboot or next firmware generation occurs. Camera acceptance stays open.


## 音乐固定测试版与日历去重的实体反馈

用户明确确认固定提速版“两段都变快，封面正常”；据此，该USB现场下同步开始等待、传输观感及封面正常通过，保留此前 .268 无黑线及播放控制通过结果。Mac正式Release打包完成99.65秒，实际成品1319项隔离检查、9项采样检查及固定helper／私有管道检查通过；完整XCTest因CLT缺模块仍不可用。成品启动、既有Automation授权与最终包播放控制仍待一次正常切换后确认，不提前计为通过。

用户另在已装 .268 确认2026-10-10详情只显示一处“调休补班”，该指定日期的重复标签问题实体通过。相机入口后续修复的真实LVGL专项及ESP32-P4 UI对象编译通过；完整预览在稍后的拍照编码重试断言失败，仍在核对，未计为全套通过。没有生成下一份镜像或修改已安装 .268，现有照片和草稿保留。

English: the user confirms both music delay stages are faster and artwork remains normal in the fixed Debug/USB setup. Prior268 black-line and control acceptance is retained. Final Release packaging completes in99.65s, and the actual bundle passes1319 isolated,9 sampling and fixed-helper/private-pipe checks. Full XCTest remains unavailable in CLT. Final-bundle launch, existing Automation consent and playback controls await one normal switch. The user also confirms a single make-up-workday label on2026-10-10, passing that specific268 calendar case. Camera-entry focused LVGL checks and ESP32-P4 UI-object compilation pass; a later encoder-retry assertion in full preview fails and remains under investigation, not counted as a full-suite pass. No next firmware image is generated; installed268, photos and drafts remain intact.


## 相机入口长等待：源码修正就绪，下一份统一固件再验

用户再次明确第一张照片也会在“正在准备相机”停十秒以上。当前 .268 的 photo_open 把任意后台会话阅读、草稿保存／恢复都作为打开本地摄像头的前置条件，且数量上限在等待之后。新的独立源码补丁先检查三张上限，本地打开不占远程邮箱；只在当前未加载且未编辑的草稿确有在途恢复时等待，使用准确的恢复草稿提示。已有发送／录音／未确认请求仍阻止打开，离页取消排队；照片上传等此前邮箱结果收尾后再开始，避免覆盖回执。已进行中的草稿结果可在相机显示时收尾，但不启动新的后台草稿作业。

补丁及LF摘要为 docs/proposals/tab5-camera-open-readiness.patch 与对应-source.json；只在独立 follow-up 源副本修改 ui.c、ui_photos.h 和真实LVGL预览用例，未改已冻结／已安装 .268。最终专项 CAMERA_READY_UI_OK、补丁 dry-run 及 ESP32-P4 UI对象编译通过，保留已知未使用图标警告。专项覆盖满额即时提示、后台阅读／保存中只打开一次、上传不覆盖在途结果、相机期间完成草稿回执、附件／文本保留、首次恢复等待、离页取消和未确认发送保护。

较广照片过渡检查在原始 .268 与新副本均于同一断言失败：桌面 ui_preview_stubs.h 的 camera_viewport_still_begin 固定返回0（不支持真实显示占用），旧照片用例却期待已取得占用并开始编码；新副本完整预览已走过新增入口及图库返回检查，随后也到此失败。不伪造显示占用通过，不将专项／对象编译等同于全套照片和硬件验收。该既有模拟限制保留在验证记录。当前相机性能／第四张实机复验仍开放；无新 BIN、版本号、latest更新、刷写或公开发布。用户照片、草稿及设置保留。

English: the user confirms10+ seconds before even the first camera opening. The268 entry waits for any background read/save/load before opening local hardware and checks the attachment cap only afterward. The independent follow-up checks the three-photo cap first and permits local opening during background read/save; only an actual initial restore of this unedited current draft waits, with an accurate restore message. Sending, voice and uncertain-request guards, navigation cancellation, pending-result preservation and draft/photo ownership remain intact. Existing save results can finish while the camera is visible, but no new background draft job starts. Upload waits until preceding mailbox results are consumed. The review patch/LF hashes are recorded; immutable installed268 stays unchanged. Final focused real-LVGL checks, patch dry-run and ESP32-P4 UI-object compilation pass. Broader photo-transition checks fail identically in original268 and follow-up because the desktop scanout stub always returns unsupported0 while older tests require a held scanout. The follow-up full preview reaches/passes camera-entry/gallery-return checks before that same limitation. This is not claimed as a full-suite or hardware pass. Performance/fourth-photo physical acceptance requires the future combined firmware. No image/version/latest/flash/public release or user photo/draft/settings change occurs.


## 连接稳定性优先：正式包控制失败与 USB 续期根因

用户在正式提速包上报告播放／暂停有时需多次点击，Wi-Fi持续等待／暂不可达，随后明确三通道会间歇掉线重连。因此此前固定Debug速度／封面通过不等于正式包控制或连接稳定性通过；暂停其他验收及下一镜像生成。保留现场，没有重启TAB5、切模式、重配网络或关闭安全保护。

同一桥接进程运行期间，USB CDC代际由58增到74（约273秒），证实并非仅界面误报。实际 .268 receiver 的增量状态分支提前返回，漏掉完整状态路径末尾的 device_host_usb_touch；实际认证8秒到期。刚启用的 Mac USB 增量发送触发这个兼容问题，无用户RPC续期时会拒收、重开并让蓝牙退出USB待命。已以真实生产 receive_frame、认证租期函数、delta应用及 link_state 做原生UBSan检查：原版纯增量在8000毫秒拒收；原版每2秒完整状态保持120秒；补齐增量续期的源码纯增量保持120秒；错误基线、断线、旧归属、坏JSON及无线帧不续USB租期。Mac先兼容现有固件，每2秒发送已协商压缩的完整状态，间隔仍可增量；只有精确ACK后记录完整状态，重连重置，不依赖用户动作。Debug与1327项隔离检查通过，正式包构建／现场连续观察待完成。固件只准备补丁 tab5-usb-delta-host-lease.patch，不生成或刷写镜像。

Wi-Fi另有独立现场：设备531次尝试／0有效状态，openError28674，Mac状态请求为0；目标地址与当前Mac本地接口一致，路由走物理网卡，监听及本机LAN请求正常，现有应用获准且系统应用防火墙未启用。设备能连网关80端口，连Mac8765超时；Mac对设备3次ping无回包且邻居未解析。该组合指向局域网设备互达路径，尚不能定为路由器隔离、设备驱动或代理根因。普通账户无法打开抓包接口，未取得握手包；0捕获不是没有流量的证据。无提权或设置修改。音乐按钮源码另有后台状态读取期间忽略点击的路径，后续以专项验证区分，不与Wi-Fi混为一项。

English: final Release control/stability acceptance fails: multiple taps are sometimes needed, Wi-Fi remains unreachable, and the user reports recurring three-link reconnections. The same Mac process sees CDC generation58→74 in roughly273s. Actual268 delta reception returns before its USB host-lease renewal; ownership expires after8s. Newly enabled Mac USB deltas expose this compatibility defect. UBSan tests of production receiver/lease/delta/link-state code reproduce expiry at8000ms; full status every2s keeps the old receiver alive for120s, and the corrected receiver keeps delta-only ownership for120s while rejecting invalid/old-host/disconnected inputs without renewal. Mac adds the compatible2s packed-full refresh, retaining interleaved deltas, exact-ACK advancement and reset on reconnect. Debug and1327 isolated checks pass; final packaging/live observation remain pending. Firmware source only is patched. Wi-Fi is separately531 attempts/0 valid versus0 host requests, with correct local endpoint/listener, permitted application and disabled application firewall. Device-to-gateway TCP works but device-to-Mac times out; Mac cannot resolve/reach the device neighbor. LAN-path cause remains unproven. Packet capture is unavailable to the current account and no packets were obtained; zero counts are not evidence of absent traffic. No reboot, mode/network/pairing/security change, privilege escalation, new image or flash occurs. A separate media-button background-read click gate is under investigation.


## USB 保活现场与 LAN 启动恢复补充

第一份保活 Release（97.84秒构建，1327项隔离／9项采样／helper校验通过）已正常替换前包。原生三次采样设备uptime3280466→3738474毫秒，跨度458008毫秒（7分38秒）；USB CDC代际始终110，Mac确认1／中断0；蓝牙连接次数始终1、最近断开原因0。启动前有一次串口不可用探测，之后没有确认会话中断。不能由此宣称三通道或长期稳定性全通过。

该次热切换另出现LAN未监听。只读系统日志固定错误匹配确认Address-in-use，旧端口短暂占用后NWListener失败，旧实现无自动重试。该期间lanReady为false，USB认证未发布有效LAN地址，设备Wi-Fi尝试计数不增长，不能将此时旧HTTP计数当作继续发包。新实现保持端口独占，仅对地址占用在30秒窗口内退避重试；停止／换代取消旧重试，真正ready才标LAN可用，并显示固定类别／错误码。没有启用多进程端口共享，也不重试权限等其他错误。

真实本机回环端口冲突专项16项通过：旧监听释放后恢复、恢复后无令牌仍401、start幂等、stop清除可用状态、停止后排队重试不复活；所有测试仅随机回环端口／合成令牌，不读用户配对或改系统设置。纯策略6项纳入隔离检查。增加最多8条无内容音乐控制回执，只记录已知动作、通道、状态码和耗时，过滤无效动作，不保留请求ID、歌曲或图片；新增2项隐私／计时检查。合并Debug1335项隔离、16项回环及9项采样检查通过；最终合并Release与再次现场核验待完成。完整XCTest环境仍缺模块。

用户确认两端为同一普通Wi-Fi，SSID为2.4／5GHz合并。公开CoreWLAN只读确认Mac为5GHz信道40，设备已有诊断为2.4GHz信道3；没有改频段／SSID／代理／路由器设置。双频合一不是已证实根因，恢复Mac监听后继续核对互达。源码媒体按钮仍有后台状态读取时忽略点击路径，保留为待修／复验。TAB5保持.268，没有新镜像或刷写。

English: the first lease-compatible Release passes packaging/signature and1327 isolated/9 sampling/helper checks. Three native samples over458008ms retain CDC generation110, one confirmed USB session/no interruptions, and one BLE connection/reason0; an initial serial probe failure precedes confirmation. This does not establish all-link/long-term stability. Its hot launch separately loses the LAN listener after a transient address-in-use error, confirmed by fixed OS-log hints. LAN readiness remains false and Wi-Fi attempt counters stop, so old HTTP counts are not new traffic. Recovery now retains exclusive port ownership and retries only address-in-use with bounded backoff for30s; stop/generation changes cancel retries, and readiness reflects actual listening with content-free error codes. Sixteen real ephemeral-loopback checks pass, including release/rebind,401 authentication, idempotence and no resurrection after stop. Six policy cases join isolated checks; two bounded media-receipt/privacy cases retain only action/channel/code/timing. Combined Debug passes1335 isolated,16 loopback and9 sampling checks; final combined Release/live verification remain pending. XCTest is unavailable. User reports one ordinary combined-band SSID; Mac read-only channel is5GHz/40 versus TAB5's2.4GHz/3. No network/proxy/band/router/device configuration is changed. Combined-band networking is not proven causal. Media click gating and Wi-Fi peer reachability remain open. No new TAB5 image/flash occurs.


## 合并 R2 仍有超时，继续保留故障现场

合并R2构建、1335项隔离／16项回环／9项采样／helper及签名校验通过，旧进程退出与端口释放后启动，实际新PID24305监听8765。用户保持USB／原模式，仅点一次设备“重新连接Wi-Fi”后确认恢复正常；设备随后HTTP200、8个有效状态，未重启或重配，尚不计持续稳定通过。

用户随后再次指出USB／蓝牙仍不稳；对应原生回执也显示R2确认会话4／中断3，随后11／中断11，状态回执、请求通道、电脑认证读回均出现超时。CDC代际114→119，蓝牙物理连接次数仍1、最近断开原因0；因此先区分USB读回失败导致蓝牙活动／待命切换与真正BLE物理断连，不能把两者全说成已修复。前述7分38秒USB-only保护观察来自LAN未就绪的前一包，不扩大到恢复LAN后的三通道稳定。

当前只在Mac串口读取处补计数：发送字节、收到字节、完整行、坏JSON、其他消息类型和残余字节；不改变命令、超时或重放策略，不记录消息正文。Debug构建及1335项隔离检查通过；准备同配置Release采样，继续区分无回包／未完整回包／解析失败。未生成新固件、未重启设备或修改配对／展示；原照片草稿保留。

English: combined R2 passes1335 isolated,16 loopback,9 sampling, helper and signing checks. The previous process and listener are confirmed gone before PID24305 starts and listens. One existing-Wi-Fi reconnect restores operation per user; HTTP200/eight valid states are observed without device reboot/re-pairing, but sustained freshness is unproven. User reports continued USB/BLE instability and R2 counters confirm4/3 then11/11 confirmed/interrupted USB sessions with state/RPC/claim read timeouts, CDC114→119. BLE physical connection count remains1/reason0, so distinguish activity/standby transitions from actual BLE disconnect. The earlier458s quiet USB interval with LAN unavailable is not three-link acceptance. Mac-only reply tracing now records numeric write/read/line/parse/other/pending counts without body contents, changing no command/timeouts/replay. Debug and1335 checks pass; equivalent Release sampling follows. No image/flash/device reboot or pairing/display/photo/draft change occurs.


## 单实例检查纠正与连接复核（进行中）

后续完整进程清点发现旧 R1 与新桥接曾同时运行；原生操作工具使用旧句柄时可重新唤起已经退出的旧副本。此前只核对刚退出的 PID 和 LAN 监听，未逐一清点所有桥接进程，部署检查不充分。因此 R2 阶段“确认／中断4/3、11/11”以及 R3 首轮51/50等回执不能可靠归属所标包，也不作为单进程固件／USB根因证据；CDC变化仍是真实采样，但存在重复桥接干扰。先前单实例时间段的续期修复专项与源码缺陷证据独立保留。

正常退出旧副本后，核对唯一 R3 PID24820及8765监听；重新建立当前句柄、不复用旧句柄。两次真实采样uptime4730440→4815468（85028毫秒）USB CDC代际150、确认1／中断0；BLE连接次数1／最近断开原因0；Wi-Fi有效回包115→152，主机58次请求／58次响应发送完成／0失败。早先重叠期的认证失败数值显示写311B、读0B或1146B坏JSON，但不能归因设备本身。当前只是短窗口恢复，USB／BLE持续稳定性仍开放。

Mac原本没有Windows已有的全桥接单实例保护，现新增每用户、跨安装目录的非阻塞文件锁；在配置、屏幕锁观察、菜单及通信服务启动之前取得，并保持到程序退出，第二副本直接退出。锁不含PID／设备或用户内容，不删除锁inode，close-on-exec防止helper延长占用；拒绝符号链接或非本用户普通文件。临时目录的6项隔离检查及真实独立进程／两份可执行文件的6项检查通过，覆盖重复拒绝、正常退出／被杀后释放、同inode及空私有文件；Debug共1341项通过。当前旧包不具备此保护，切换时仍须逐一确认全部旧桥接退出。最终R4打包和实际运行复核待完成。

English: a complete process inventory finds overlapping old R1/new bridge copies; a stale native-control handle can relaunch an exited old copy. Checking only the just-quit PID and LAN listener was insufficient. R2-labelled4/3 and11/11 and initial R3-labelled51/50 session counters cannot be reliably attributed to those builds or used as isolated device-failure evidence. CDC changes are real but confounded by duplicate bridges. The independently reproduced268 lease defect remains valid. After normal old-copy exit and full inventory, sole R3 PID24820/listener is sampled twice over85028ms: CDC150, USB1 confirmed/0 interruptions, BLE1/reason0, Wi-Fi valid115→152,58/58 host responses completed/no failures. Numeric claim failures while overlapping show311B written and0B read or1146B/malformed JSON, without establishing a device cause. Sustained USB/BLE acceptance remains open. Mac gains a per-user cross-install-directory nonblocking file lock before any preferences/UI/link services; duplicates exit, the lock lives until exit, remains on the same inode, has no contents and is close-on-exec. Symlinks/non-owned non-regular files are rejected. Six isolated and six real separate-process/two-executable-copy tests pass, including normal/crash release; Debug totals1341. Old ungated builds still require explicit full exit verification. Final R4 packaging/live validation is pending. No firmware, device reboot, settings, photos or drafts changed.


## 单实例保护正式包已运行，蓝牙主动通道复验待反馈

R4正式包为 artifacts/mac-link-single-instance-20261024/AIBotBridge.app，98.82秒构建；实际成品1341项隔离、16项真实回环、9项采样、固定helper和签名校验通过，冻结源码摘要匹配。swift test已执行，CLT仍缺XCTest，不计全套通过。正常退出R3后完整进程清点为空、8765无监听，再启动唯一PID25205；实际再次执行同包入口返回BRIDGE_ALREADY_RUNNING/0，未启动第二套服务。

R3清除重叠后的四次采样跨度228958毫秒（3分49秒），CDC150、USB确认1／中断0，BLE1／原因0；Wi-Fi有效115→210，主机发送完成116／失败0。R4前两次采样跨度50466毫秒，CDC152（正常包切换重开）、USB1／0、BLE1／原因0，Wi-Fi220→235，主机22／22发送完成、失败0。只有启动确认前的一次串口探测失败；没有新的已确认会话中断。两包观察窗口分别记录，不拼接成同一进程稳定性结论。蓝牙在USB优先待命，尚不能用其连接计数代替主动蓝牙通道验收；已请求临时仅蓝牙观察30秒、恢复原模式观察20秒，等待实体反馈，整体连接稳定性继续开放。

English: R4's actual signed bundle builds in98.82s and passes1341 isolated,16 real-loopback,9 sampling and helper checks; frozen source hashes match. swift test is attempted but CLT lacks XCTest. R3 exits normally; every bridge PID and the listener are absent before sole R4 PID25205 starts. A real second invocation exits0/BRIDGE_ALREADY_RUNNING before starting duplicate services. Four clean R3 samples over228958ms retain CDC150/USB1 confirmed,0 interrupted/BLE1 reason0, with Wi-Fi valid115→210 and116 completed host responses/no send failures. R4's first50466ms retain CDC152 after the normal replacement, USB1/0/BLE1/0, Wi-Fi220→235 and22 completed responses/no failures. An initial pre-confirmation serial probe failure is distinct from an interrupted confirmed session. Windows are closed after reads; no old handle is reused. These two process windows are not combined into a long-duration pass. BLE is in USB-priority standby, so active BLE operation and return-to-USB acceptance await the requested30s/20s physical observation. Overall stability remains open; installed268, network/pairing/display, photos and drafts are unchanged, with no next firmware generation or flash.


## 当前连接与音乐控制回归通过（范围有限）

用户明确确认：仅蓝牙观察约30秒期间持续正常，恢复原模式后USB正常；随后播放／暂停及恢复原状态各一次即生效，封面正常。唯一Mac进程仍为R4 PID25205、8765由该进程监听。恢复后原生样本uptime5238529、CDC152、USB确认1／中断0、BLE连接1／原因0，Wi-Fi有效299、主机86次响应全部发送完成／0失败；与R4首样本间238895毫秒（3分59秒）没有新增USB中断。BLE状态年龄约44秒，也支持此前实际离开USB待命、收过BLE状态，仍不以此替代用户实体观察或声称所有蓝牙业务均已验收。

本轮主动蓝牙／切回USB和正式包单次播放控制回归通过，关闭这一步等待；长期偶发掉线、此前需重启的USB枚举异常及Wi-Fi互达根因仍按观察边界保留，不能写成所有故障均已彻底定位。另在原生LVGL中已确定复现旧固件后台状态读取期间忽略一次音乐点击，准备独立源码修正及验证；它不是本轮用户再次报告的失败，不抵消当前实体通过结果。没有改运行桥接／设备模式、网络、配对、照片或草稿，也没有新固件。

English: the user confirms continuous BLE-only operation during the requested30s, normal USB after original-mode restoration, and single-tap play/pause/restoration with normal artwork. Sole R4 PID25205 still owns the listener. Fresh numeric sampling retains CDC152, USB1 confirmed/0 interrupted, BLE1/reason0,299 valid Wi-Fi states and86 completed responses/no send failures. The238895ms R4 sample span has no new USB interruption. BLE state age44s supports recent active BLE status but is not broad business acceptance. This bounded active-BLE/USB-return and formal-bundle control regression passes. Long-duration intermittent faults, earlier restart-recoverable USB enumeration and Wi-Fi path cause remain observation items. A separate old-firmware background-read click-drop is reproduced with real LVGL and is being fixed in source; it is not a new user failure and does not negate this physical pass. No running bundle/firmware, network, pairing, photos or drafts changed.


## 单实例下 USB 再次中断：稳定性重新开放，定位长请求保活断档

此前有限时长蓝牙／USB切回和一次播放控制通过后，用户再次报告USB掉线重连。即时完整进程清点仍只有R4 PID25205且独占8765；新鲜样本USB确认2／中断1、CDC152→153，BLE仍1／原因0，Wi-Fi有效364、主机155次发送全部完成／0失败。不能用重复副本解释这次故障，也不再把前述短测通过当作稳定性关闭。

本次请求通道超时前写273B、读49B、完整行1／坏JSON0／其他类型1／残留0B。49B与固件固定tab5_host_required消息长度相同；R4追踪没有记录该消息的具体type，故仅作为线索，不伪称已直接读到类型。独立原生“最近会话请求”诊断显示多次USB读取回复耗时14–15秒，最长59802ms，且503失败；最新一次仍处理中约12秒。真实代码在唯一USB队列内同步调用service.rpc，期间普通2秒完整状态保活无法运行，远超设备8秒归属期限。该可确定的同步等待断档与本次重新认证线索一致，仍需正式包现场验证修复效果。

Mac已改为仅在后台运行一次请求处理，串口队列用最多100ms短等待检查完成，并按已有2秒节奏发送已协商压缩的完整状态、精确ACK后续期和更新基线；等待期间不携带图片分片。原75秒请求总期限、4秒串口期限、设备8秒认证期限不延长；禁用／配对变化或保活错误立即停止本次等待，不重放请求或自动重试副作用。增加纯数值等待／保活统计，并明确识别既有tab5_host_required为失败而非忽略等超时。模拟12秒慢请求、租期保持、失败不重试、截止／取消、真实后台结果与固定类型识别16项通过；Debug共1357项通过。R5正式打包及真实慢请求观察进行中，未部署前不宣称修好。相机／音乐点击源码补丁工作暂停在独立副本，不生成固件或修改用户数据。

English: following bounded BLE/USB-return and music passes, the user reports another USB reconnect. Sole R4 PID25205 still owns8765; USB2 confirmed/1 interrupted and CDC152→153 establish a single-instance recurrence. BLE remains1/reason0 and Wi-Fi364 valid/155 completed responses/no failures. Duplicate copies do not explain it, and overall stability reopens. The timed-out RPC writes273B/reads49B with one valid unrelated line. Its size matches the fixed host-required reply, but R4 does not record the actual type, so this remains an inference. Separate native content-free interaction timing shows14–15s USB reply reads and a59802ms request returning503; another is still pending at12s. Actual code synchronously occupies the sole USB queue in service.rpc, preventing2s full-state keepalives for longer than the8s ownership lease. R5 starts host work once in the background while the serial owner polls in at most100ms waits and sends existing packed full states on the2s cadence, advancing only after exact ACKs and updating delta baselines, without image fragments. The75s overall/4s serial/8s ownership bounds remain; cancellation, pairing change or heartbeat error stops without operation replay. Numeric wait/heartbeat counters and exact existing host-required classification are added. Sixteen simulated/real-thread checks pass, bringing Debug to1357. Release packaging and slow-request physical observation remain pending; no firmware/image/flash or user photo/draft change occurs.


## R5长请求保活包部署，继续等待真实慢请求覆盖

正式包 artifacts/mac-usb-rpc-keepalive-20261024/AIBotBridge.app 构建95.02秒，实际成品1357项隔离、16项回环、9项采样及helper／签名校验通过，冻结源码摘要匹配；swift test仍因CLT无XCTest失败，不计通过。R4正常退出后所有桥接进程及8765监听均为空，才启动唯一R5 PID26259。首次采样USB确认1／中断0、CDC163、BLE1／原因0、Wi-Fi577有效；53秒后仍USB1／0、CDC163、BLE1／0、Wi-Fi600有效。请求处理最大仅8ms、等待保活0次，尚未覆盖真正慢请求，所以不宣称现场已验证保活修复。已请求用户仅浏览原Codex会话一分钟，保持连接及草稿照片不变，不发送或录音；等待反馈及后端慢请求计数。所有检查窗已关闭。

R4收尾的单实例样本达到USB10／中断9、CDC161，最近8次超时均写273B／读49B／其他类型1；这份修复前证据独立保留。新版不再忽略已知的“需要认证”回复，但当前尚未在R5见到该错误。镜像与用户设置保持不变。

English: the actual R5 bundle builds in95.02s and passes1357 isolated,16 loopback,9 sampling, helper/signature and frozen-source checks; XCTest remains unavailable in CLT. Every old bridge process and8765 listener is gone before sole PID26259 starts. Initial and53s-later samples retain USB1/0, CDC163 and BLE1/reason0, while Wi-Fi valid577→600. Maximum host handling is only8ms and waiting heartbeats0, so genuine slow-request coverage is still pending rather than claimed. The user is asked to browse the existing Codex conversation for a minute without sending/recording or changing photos/drafts/settings. R4 ends with USB10/9/CDC161 and eight identical273B-write/49B-read/one-unrelated-line timeouts, preserved as distinct pre-fix evidence. R5 recognizes the known auth-required reply but has not yet observed it. Windows are closed; no firmware generation/flash occurs.

## 音乐点击源码补丁验证完成，仍不生成镜像

在独立后续源码副本中，真实LVGL先复现“点击播放／暂停撞上后台status读取，读取结束仍未发命令”。修正将至多一次明确点击绑定到当前电脑、通道、模式、连接代际和会话，等当前读取完成后在3秒窗口内执行一次；页面、期限、状态或曲目元数据变化就取消，不重试副作用。待执行期间暂停新status轮询，非读取操作繁忙时不排队；UI给出等待／取消提示并抑制重复点击，读取期间不闪烁禁用按钮。没有改变媒体协议或Mac／Windows行为，未覆盖用户草稿或照片。

独立补丁与归一SHA摘要为 docs/proposals/tab5-music-first-tap.patch 和 -source.json，3个文件以冻结.268为基线，补丁dry-run、应用及逐文件比较通过。190个媒体交互场景、原有90个锁定／解锁点击场景、相机入口专项均通过；真实ESP32-P4 ui.c对象编译通过，只有既有未使用变量／函数警告。广义照片预览的既有scanout桩限制不作通过声明。没有修改冻结.268源树／BIN、没有链接新镜像或递增版本；实机仍需未来统一固件复验。本次USB问题保持最高优先级。

English: source-only media work is now validated independently. Real LVGL first reproduces a tap lost during a background status read. At most one explicit tap waits up to3s, bound to the current host/link/mode/connection/session and displayed music state/metadata; it executes once after a successful read or cancels on changes. New polls cannot starve it, non-read operations do not queue it, and duplicate taps are suppressed with visible wait/cancel feedback. No media wire format/platform operation changes occur. The3-file patch applies and matches the immutable268 baseline.190 media and90 existing lock/unlock cases plus the focused camera-entry regression pass; actual ESP32-P4 UI-object compilation passes with existing unused warnings only. The known broader photo-preview scanout-stub limit remains unclaimed. No frozen source/BIN, new linked image/version/flash or user data changed. Future combined-firmware physical acceptance remains required, with USB recurrence still the current priority.


## R5真实慢请求保活实体复核通过，继续现有.268验收

用户按原连接、只浏览既有Codex任务约一分钟后确认USB未掉线。独立原生样本显示唯一R5 PID26259，USB确认1／中断0、CDC163，BLE连接1／原因0；Wi-Fi有效577→694、主机126次发送全部完成／0失败。请求处理最大21260ms、等待保活41次，已经覆盖超过设备8秒归属期限的真实慢请求；首末设备样本跨度270576ms（4分30秒）。因此“同步处理长请求阻塞保活”的具体修复路径本轮实机回归通过；不扩大为从此永不掉线，长时稳定及旧枚举异常仍观察。此前R4的短测通过后复发记录独立保留。

桥接检查窗口已关闭，无切换运行包、刷机或设置／用户数据改动。按剩余清单转入.268设备预览：已请求用户总览页打开Mac“设备预览→M5Stack TAB5”、核对画面与方向、切音乐页看更新，正常后关闭；目前待反馈，尚未记为通过。

English: the user confirms no USB drop while browsing the existing Codex task for about a minute on the unchanged connection. Sole R5 PID26259 retains USB1 confirmed/0 interrupted, CDC163 and BLE1/reason0; Wi-Fi valid577→694 and126 completed host responses/no failures. Actual maximum host handling21260ms and41 waiting heartbeats exercise the real >8s slow-work path over a270576ms sample window. This specific keepalive regression passes, without claiming perpetual/all-path stability; long-duration and earlier enumeration issues remain observation. R4's prior bounded pass and subsequent failure remain distinct. Windows are closed, no package/device/settings/data change occurs, and installed268 device-preview overview/music physical acceptance is requested next, still pending.


## .268设备预览未通过：保留现场并增加分阶段数字诊断

用户在.268总览打开“设备预览→M5Stack TAB5”等待后反馈一直无画面，保留窗口。原生窗口状态确认为“读取超时或连接已变化，等待下一帧。”；没有图像显示，不计为镜像通过。保留预览期间临时打开运行监控后关闭监控／设备中心，原预览仍保持。设备UI页0、数据年龄287ms、USB年龄7ms、CDC163、USB确认1／中断0；请求处理最长29755ms、等待保活80次，USB长请求保活仍正常。RPC诊断37,1,0,0;2,1,0,0;0,0,0,0为累计记录，不能据此全部归因预览。Wi-Fi／BLE状态年龄已较旧，另保留通道观察，未重连或改网络。

现有窗口信息不能区分设备命令未启动、begin未到达、采集或分片失败。因此Mac只增加内容无关的请求／发布／超时／接收／begin／分片／完整帧／拒绝计数，以及命令期限相对于状态采样时钟的数字差；数字只在已有运行监控展示，不保留ID、名称、图片或未知操作文本。命令及超时行为保持逐字节兼容，6项数字诊断／隐私检查通过，Debug共1363项通过。R6诊断版正式打包中，尚未部署或定位根因；不据此改设备协议、重启、重配或生成新固件。相机、USBdelta续期和音乐点击三份后续补丁另以冻结268为基线顺序dry-run／应用／7文件比较通过，仍只是源码审查，不是下一镜像。

English: the user reports no image after opening installed268's device preview. Native status confirms timeout/connection-change waiting; no mirror pass is claimed. The preview is retained while a temporary monitor is opened/closed. UIpage0/data-age287ms/USB-age7ms, CDC163 and USB1/0 confirm the live screen/link; maximum host work29755ms/80 waiting heartbeats retain the USB keepalive pass. RPC counts are cumulative, not all attributed to preview. Wi-Fi/BLE state ages are stale and remain separate observations without network changes. Existing status cannot locate command admission versus begin/capture/frame failure, so Mac adds bounded numeric phase counters and snapshot-relative TTL observations in the existing monitor, with no IDs/names/images/unknown-operation contents. Wire commands/timeouts remain byte-identical; six privacy/observation checks bring Debug to1363. Diagnostic Release R6 is building, not deployed or a cause claim. No device protocol/settings/reboot/re-pairing/firmware change occurs. The three source-only follow-up patches also apply sequentially to frozen268 and match all seven files; this is not a next firmware image.


## R6诊断包部署后连接未恢复：先确认设备响应，预览暂停

R6纯数字诊断包构建100.42秒，实际Release1363项隔离、16项回环、9项采样、helper／签名及冻结源摘要校验通过。R5正常退出后所有桥接进程、8765监听均为空，才启动唯一R6 PID27102。原预览随正常退出关闭；重置操作工具句柄，避免旧副本复活。没有修改命令内容或超时策略，也没有新固件。

切换后连接未恢复，不能记为部署实体通过：USB modem节点仍存在、lsof仅R6持有端口，但电脑认证写311B／读0、随后发现写40B／读0超时；Mac USB确认0／中断0，HTTP请求0，无设备新采样。0中断只是从未确认，绝非稳定证明。约一分钟后及后续相同失败记录仍在；该历史会合并连续相同文本，不能仅由记录年龄推断线程卡死。1秒进程采样仅保留固定函数符号计数，见串口reply／init／短等待路径，未保存原样本；这不是CPU归因或设备固件状态证明。预览请求／发布／接收仍均0，尚未重开，所以还未取得新分阶段预览回执。

先暂停预览复验，已请求用户只观察TAB5总览时钟并切页一次、反馈触摸及USB状态；不拔线、重启、重新配对或改网，等待反馈后再决定恢复步骤。此前R5实际21.26秒／41次保活及用户一分钟不掉线证据保留，但不能覆盖这次新进程重新建立连接的失败。其余新布局／弹窗验收仍未做，下一镜像继续暂停。

English: diagnostic-only R6 builds in100.42s and its actual Release passes1363 isolated/16 loopback/9 sampling/helper/signature/source checks. Every R5 process/listener is gone before sole PID27102 starts; the preview closes with normal exit and native-control bindings are reset. No command/timeout/device image changes occur. Connection does not recover: the modem exists and only R6 owns it, yet claim311B/read0 and discovery40B/read0 time out. USB0 confirmed/0 interrupted is never-confirmed, not a stability pass; HTTP requests and new device samples are absent. Identical consecutive failure notes coalesce, so old note ages cannot prove a frozen worker. One-second sampling retains only fixed serial reply/init/wait symbol counts, no raw sample or CPU/cause claim. Preview issued/published/received counters remain0 because it has not reopened. User clock/touch/USB-indicator observation is pending; no unplug/reboot/re-pair/network change is requested. R5's actual21.26s/41-heartbeat and user browse pass remain valid for that bounded path, not this new connection establishment. Preview/layout/dialog acceptance and next firmware stay open.
