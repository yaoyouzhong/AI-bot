# Mac 后续修复 · 2026-10-24

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


## R12第一轮失败：后台Codex锁与终态交付

用户第一轮点击结束出现“连接中断，收音已停止”；第二轮不再做。R12仍运行。Mac记录USB手动stop200、最终识别75ms、至首次终态生成10982ms；即时三秒线程样本显示USB的MacTab5USBOperationResult在Codex.handle的gate上等待，后台Codex.refresh持该gate做目录／owner IPC。仅保存栈／固定原因／计时，未留转写或包内容，不能仅凭栈确定该后台请求具体op。

R13源码：语音的transport优先级贯穿识别完成至USB全部终态分片确认；比较当前voiceId与nonce，旧ACK、迟到recognizing结果不能放行，终态丢包继续保留优先级，30秒无有效peer请求后有界释放，禁用立即释放。原busy语义、8秒安全截止不改。服务对该期间read/read-ack快速503，保护串行邮箱；非语音read-ack使用try-lock，保留所有旧最新turn验证。增加固定“确认已读”本地诊断标签，避免丢失op线索。Debug1501检查通过，完整Release包正在构建；初步R13从未启动，已标作废弃，使用mac-voice-terminal-r13-complete-20261025。

设备独立源码还在发起只读回执前等待voice.busy结束，等待期间不持邮箱／不发布nonce，90秒加一次250ms上界；原身份／session和四轮幂等重试不变。实际worker UBSan与P4编译通过。为使快照与RPC/crypto不共用栈帧，等待helper明确noinline：528B主worker、2784B等待helper，避免抬高原网络路径栈。补丁为tab5-read-ack-voice-yield；与已验证按钮和预览内存补丁待后续统一。尚未生成固件／刷机。

## R12最终部署与USB复验待回

只运行`mac-usb-voice-priority-r12-complete-20261025`；两个初步R12包从未启动、父目录已标废弃。最终Debug／Release1478项、HTTP16、音乐9、签名／原helper及166文件源哈希均通过。R11正常退出、无旧PID／监听后启动最终包，唯一PID55328，三通道认证恢复；关闭检查窗口。状态包内嵌资源也按同一voice.busy门控，metadata-only不前移图片偏移。已请求两轮USB20秒、正中半秒停止、仅预览文字不发送；任一异常停下，不重复补点。该实测尚未返回，八秒安全截止仍未放宽。设备.270和固定latest哈希保持，按钮／ready及内存补丁不提前计为实机通过。

## 2026-10-25：仅Wi-Fi对比与USB录音优先

用户确认仅Wi-Fi10秒录音本次OK，Mac核对Wi-Fi200、主动stop原因、最终60ms／至响应452ms；USB数据随后确认。测试同时要求圆钮正中半秒接触，不据单次对比排除触摸因素或声称所有USB故障同因。R12将已有USB循环重排为录音先claim/RPC再普通status，必要状态及2秒full ownership保活仍执行；没有RPC能力的旧设备不发送RPC。录音期间不再强行发送第一张资源片，start在同一循环内改变busy时也暂停图片；结束后恢复原路径。异常沿用原关闭／重连，不重放命令。Debug1478项检查含冷USB认证、认证失败不下发、慢status优先级、start后资源再检查、idle恢复及原75秒host操作保活。R12 final包正在构建，未部署。

证据：`artifacts/development/mac-usb-voice-priority-20261025`；仅采用`mac-usb-voice-priority-r12-final-20261025`最终包，初步R12未启动、已标废弃。后续仍需USB两轮实机结果，当前没有新固件生成。

## 2026-10-25：任务页结束按钮与9.249秒语音等待

R10复验失败，用户明确任务页圆形按钮点后未切换状态。现场Mac无USB会话中断，最新语音仍由八秒无有效请求结束；读取完整线程采样未见旧NSHost阻塞，不据此关闭故障。R11只把设备已经携带的voiceCheck／voiceAgeMs／voiceStage／voiceHttp／voiceRequestMs／voiceSeq数值展示到本地诊断；不保留音频／转写／会话身份。Debug／Release1464、HTTP16、原helper和签名通过。正常退出R10、确认PID／监听消失后R11唯一PID54379，三通道认证恢复。实读上次语音请求9249ms、序号0、阶段1、HTTP0；正在请求一次仅Wi-Fi10秒对比并恢复原模式。

`tab5-voice-stop-20261025/candidate`在预览内存后续源码上修复圆形控制：8px外扩形成72px有效目标，停止接受PRESSED，起录仍CLICKED；同一次松开不能重复停止或重新起录。原中心9点完整按下／松开可通过，不宣称复现了全部实机失败；新25点实际pointer及快结果／新手势检查通过。停止后的ready判断移入设备状态同一互斥区，避免旧回包在停止后恢复recording；原更新块在可行交错下失败，新实现停止／取消两例UBSan通过并保留文字、取消优先。P4的ui.c／voice_remote.c对象及录音开始、相机原生回归通过。源码补丁在`docs/proposals/tab5-voice-stop-touch.*`；未生成或安装新镜像。

R11的v2内存回退已实际显示640×360 USB标准画质帧，高清内存问题仍未通过。后续对比期间关闭预览。

## 2026-10-25：.270录音提前返回与预览内存失败

两轮录音首轮正常，第二轮未经手动结束返回部分文字恢复提示，仍未通过。R9本地诊断显示“设备连接超过八秒无有效请求”，最终识别至响应13683ms；只记录计时／原因，未保存转写内容。只读线程采样捕获USB线程查询`lanConfiguration`时卡在`Host.current().addresses`的NSHost解析，并持有桥接共享锁。该现场证明实际阻塞路径，不把此前全部偶发断连归为同因。

Mac改为`getifaddrs`／`inet_ntop`数字地址枚举，选择运行中的私有IPv4，优先物理en接口；主机查询移出服务锁，随后复核监听状态。原生产getter在阻塞查询fixture下可复现USB心跳不能前进；修复后Debug／Release各1462项、实际窗口27项、HTTP16项、音乐9项及签名／原helper检查通过；完整XCTest仍缺模块。8秒语音安全截止不变。两端预览增加v2的640／320 RLE内存降级并明示画质；Windows生产模型47项和Release交叉构建通过，未声称Windows实体通过。

.270高清预览实机报“设备内存不足”，对应临时像素／编码分配失败。独立后续源码只回收图库压缩缓存和屏外快照，并在预览租约／worker期间暂停可选页面预热；未变化画面不再分配512KiB编码输出。显示／手势快照、照片、草稿和设置不被清除。实际worker＋既有JPEG的UBSan、准入／租约、缓存归属和两个P4对象检查已通过；不修改已安装.270或其冻结源码／交付包，暂未生成下一固件。证据在`artifacts/development/mac-lan-identity-20261025`及`artifacts/development/tab5-preview-memory-20261025`。R9已正常退出并确认无旧PID／监听；R10唯一PID53156，三通道认证恢复，检查窗口关闭，两轮录音复验等待用户反馈。


2026-10-25安装后检查点：用户已明确“现在安装 .270 并集中验收”。正常升级界面只选择并确认一次，设备重启后版本.270；旧升级请求显示未确认，未补发。首次USB启动核验通用未确认，未保存其原始失败样本、不唯一归因；随后第二次只读核验通过精确ELF `3faf0c24…2714c08`、VALID分区和三个实时界面样本。亮度79、音量70、非静音、提醒开启、轮播关闭、固定总览、15秒间隔、5分钟屏保，以及原选中页面和8页顺序回读一致；两次设置窗口均取消未保存。R9继续唯一PID47902后台运行，USB／Wi-Fi恢复。已请求两轮单次开始／结束录音（不发送、不同时打开预览）；录音和高清预览均尚未记录实体通过。

Installed270 with explicit authorization through the normal bridge UI, confirmed once. The old upgrade receipt remained unconfirmed after reboot and was not resent. A second read-only boot check verifies the exact ELF, VALID partition and three live UI samples; the first generic failure has no raw evidence for unique attribution. Display/sound preferences match and both dialogs were cancelled. R9 stays in the background; recording and preview acceptance are in progress.

## 当前状态（以下历史记录按时间保留）

- 已安装TAB5为 **0.2.270-ui**，用户已明确授权；USB精确ELF／VALID／三个实时界面样本通过，原展示／声音设置回读一致。当前Mac已正常切换到R9唯一PID47902，USB数据已确认；.270已获本候选授权，通过正常升级安装，精确ELF／VALID／三个实时样本及原设置保留已核验；录音与预览实体复验中。
- **长请求USB保活具体路径回归通过，长期稳定性继续观察**：R4单实例下仍有真实掉线，R5已部署修复；1357项隔离、16项回环、9项采样、helper／签名检查通过。实际最长21.26秒请求、41次等待保活，USB确认1／中断0，用户浏览约一分钟未掉线。只关闭这一步复验，不宣布全部偶发故障根因闭环。
- 蓝牙单独连接及恢复USB、单次音乐控制和封面本轮曾通过；不扩大为长期USB／BLE可靠性。早先USB枚举、Wi-Fi异常仍为观察项。
- 后续统一固件的源码已备：相机打开／满额等待、USB增量归属续期、后台读取撞音乐点击、预览缓存锁泄漏、诊断范围及文案精简、日程独立柔和双音。六份补丁在冻结.268基线上顺序dry-run／应用并逐一比较13个文件通过。相机／媒体真实LVGL专项与ESP32-P4对象编译已验证；新代码的相机性能和点击实机仍待未来镜像。
- .268设备预览会卡死，已停用复验并保留未来统一固件待验；现有.268提醒／删除窗口已通过并清理测试项，网络范围后续改动待新固件；用户明确此前草稿／照片均为可清理测试内容，但要求先解决相机打开慢再测附件；现按既有统一生成授权完成.269构建、校验和实际说明预览，固定latest已归档；本候选安装已获明确授权并完成，精确启动已核验；用户报告相机延时和满额提示通过，照片发送继续。ESP8266无硬件，暂缓实机回归。
- 现有可做的界面验收与六项源码修复已完成，照片按用户要求留至相机修复后；**.269已按明确授权安装并核验，当前继续其余实机验收；没有推送、标签或公开发布**。用户网络、配对、展示、照片和草稿保持。

Current status: explicitly authorized269 is installed. USB verifies its exact ELF, VALID partition and three live UI samples after one upgrade; original display/sound settings match. Mac remains sole R7 PID28727 with windows closed. The user passes the slow-camera fix and immediate three-photo-limit warning. Photo sending is requested next, followed by remaining269 physical checks. Six patches apply/compare across13 files; native/UI/P4/full-image evidence retains its separate scope. Overall long-duration connectivity and unavailable ESP8266 hardware remain observation/deferred. No push/tag/public release occurs.


用户接受原 r76 蓝牙停止后的约 4–5 秒等待，进一步提速延后。本轮继续快捷发送和日历年度更新；固件、Mac 本地识别选择及设备展示设置保持不变。

**当前执行顺序：**用户要求先完成全部验收与源码修复，再统一生成新固件。日历去重的固件构建已在工具链初始化阶段停止，没有生成新镜像；固定 latest 仍是 `0.2.267-ui`。保留已完成的源码补丁与本地界面验证，后续不按单项问题逐次生成固件。

**Current workflow:** finish the remaining acceptance and source fixes before generating one combined firmware candidate, as requested by the user. The calendar-label build was stopped during toolchain initialization, before any image was produced. Fixed latest remains `0.2.267-ui`. Preserve the source patch and native UI evidence; do not generate firmware for individual issues in this acceptance pass.

## .269录音与三照片发送复发：重新列为故障

相机延时和三照片满额提示由用户明确通过；随后三照片单次发送返回“Codex连接失败，文字已保留”，继续保留文字与附件，不自动补发。录音出现停止点击延迟、未点结束即恢复部分文字、开始按钮需点多次；后一次正常不关闭该故障。

本机只读诊断：Mac本地识别、默认Mac麦克风；公开回复读取曾为12–17秒，独立桌面快照约48ms。最近一段停止至最终结果73ms，最终结果至返回响应4237ms。“设备请求停止收音”也可能由设备故障恢复发出，不等于用户点击。设备当前仍.269，运行桥接R7，未以重启或清理现场替代定位。

源码发现和修正（独立于冻结.269源）：

- 开始入口在后台读取/同步时原来直接return；现在保留一次准备意图并显示可取消等待。任务、电脑会话、页面、快捷控制台状态改变或20秒超时均取消，不在后来自动启动；等待期间停止新后台读/存/预取，既有请求照常收尾。
- 停止/取消立即发布本地micStop、draining和零音量状态，保留文字；取消优先，迟到ready不能复活。界面不提前声称Mac麦克风已停止。
- USB/BLE voice原零等待邮箱准入会把普通争用当连接中断；仅在发布nonce前等待最多2秒。原总期限、身份/代次/断连保护和失败恢复不改，不重放语音或发送。
- Mac已建立公开历史边界后，普通最新回复采用新的桌面快照和最近公开文字，避免每3秒重读整段持久历史；显式历史导航、owner失效仍真实读取，控制始终用新owner状态。缓存仅公开assistant文字，最多1MiB，无持久化。
- 发送保留现有503协议；本地诊断增加固定预检阶段和codex/send结果，不含会话ID、照片、文字或异常原文。当前旧503具体分支尚未捕获，待新诊断版后一次人工复验。

证据位于`artifacts/development/tab5-voice-reliability-20261024/`：四文件补丁225行；真实LVGL单击/去重/取消/上下文/超时/快捷控制台和三照片草稿保留检查通过；生产停止命令/恢复函数UBSan通过；实际exchange的旧故障复现和USB/BLE两秒有界准入/身份/断连/无重放通过；ui.c、voice_remote.c、rpc_link.c实际P4对象通过。Mac Debug隔离1387项通过，Release构建中；swift test明确因无XCTest模块失败，不能记为完整套件通过。未生成新固件，不变更versions/TAB5和latest。实机开始/停止/连续录音与三照片送达继续待验。

Recording and three-photo sending are reopened failures on269. Source fixes and actual production-function/native/P4-object checks pass, but no new image or hardware pass is claimed. Mac adds bounded public latest-reply caching and content-free local preflight diagnostics, retaining wire errors and no-retry delivery semantics. Debug passes1387 isolated checks; Release deployment remains pending; full XCTest is unavailable.

## 2026-10-25预览停更／重绘与高清后续

用户补充：单张照片＋文字与三张照片＋文字均成功发送到指定测试会话，照片发送场景通过；旧503未捕获具体分支，保留新增预检诊断。录音开始需多次点击、偶发提前结束仍未通过。

.269预览首帧正常，重开／切页有时停更，随后又自行恢复；时钟和触摸始终正常。只读计数曾见12请求／11完整帧／1超时，无接收拒绝；相对状态快照超过30秒上界17次。源码发现设备单帧中止后保留请求去重，主机旧逻辑等待30秒；同时重复打开会清空当前会话，相同像素仍重建NSImage，分片进度反复改变提示。

桥接修复无进展等待（USB/Wi-Fi5秒、BLE10秒）、快照相对租约收窄、重复入口复用、common runloop续租、同像素保留图像对象及有完整画面时不抖动分片提示。v2仅对新固件协商高清JPEG；旧.269保持v1。设备使用实际LCD缓冲横竖屏采集及现有quality85逐行JPEG编码；未变化只发送编码摘要，主机没有旧帧时拒绝并触发下次完整帧，不缓存用户画面。Windows协议和显示同步适配；细节见PROTOCOL。

验证：Mac1415隔离（含实际固件软件编码器JPEG跨端解码）、真实窗口三次开关／计时／重复入口／同图对象27项；Windows跨编译0警告0错误，生产模型40项，JPEG光栅未在Mac伪作Windows通过。实际生产worker＋原vendor编码器按P4相同gnu++2b标准UBSan通过；最初用默认旧C++标准时报告的负数左移不适用于生产C++23，已恢复vendor字节完全不变后按相同标准严格重测。实际SDK与生产驱动64次高清旋转／128次旧尺寸、界限及锁检查通过；ASan在Darwin运行库初始化未进入main即自锁，已终止该测试，仅UBSan和显式红区作为通过证据。五个P4对象通过。统一.270完整构建和增量复核通过，镜像7205536字节、余量3424字节；初始7528源文件未变，随后仅同步两个非固件原生测试文件并重新冻结7529文件，复核构建镜像字节不变。生产校验器和303字节实际说明预览通过。R9已正常部署，唯一PID47902、USB确认；设备仍.269，.270安装未授权，实机修复未宣称通过。

Photo sending now passes for both one and three photos plus text. Recording and intermittent preview remain open. Native lifecycle, production model/worker/SDK/driver and cross-platform protocol checks pass; negotiated HD and unchanged-frame receipts await the combined270 image. No Windows native raster/hardware, full XCTest or ASan pass is claimed.

## 任务列表切换复测

2026-10-24，按现有 `.267` 打开任务页、切换两个项目或分组后返回的测试安排，用户明确反馈“列表正常，没有顶部大块留白”。本次列表显示验收通过、历史留白未复现；结果以用户实体观察为证，没有独立测量设备坐标。保留此前重启恢复和本地滚动状态调查，不把本次正常写成根因已修复，不追加未复现问题的固件修改。详细历史见[列表调查记录](MAC-TAB5-CODEX-LAYOUT-20261021.md)。

随后核对快捷控制台同步提示：在现有连接与固件上切换测试会话后按一次录音键，用户明确反馈“直接开始录音，已取消”。本次切换后的录音入口验收通过、持续同步提示未复现；未触发提示出现后的自动消失分支，不能将该分支写成已验收。取消结果以用户反馈为证，没有独立读取本轮后端取消回执。保留历史问题观察，不为本次未出现的提示反复试录。没有发送消息、生成或刷写固件、改变展示设置或运行实例。

On 2026-10-24, the user reported a normal task list with no large blank area after the requested project/group switching check on `.267`. This physical observation passes the current display check without establishing a permanent root-cause fix or independently measured coordinates. Preserve the historical restart recovery and scroll-state investigation; no speculative firmware change is added. In the subsequent Quick Console check, the user reports that recording started directly and was cancelled. The post-switch recording entry passes and the persistent synchronization hint was not reproduced. Automatic clearing after the hint appears was not exercised; cancellation is user-reported rather than independently confirmed by a backend receipt. Keep the historical observation without repeated takes. No message, firmware generation/write, preference or runtime change occurred.

## 日程语音名称上限复核

重新核对完整 `.264` 归档与已安装 `.267` 的冻结源码，发现 `ui_agenda_voice.h` 已包含用户要求的超长语音截取。两份文件与当前隔离副本字节一致（SHA-256 `d1fe61c9217cc8d143eee6e91aac9153877aa7f458f48a51af7fece82405c648`）。此前基于 `.234` 记录的“待实现”不再代表当前源码状态，本轮没有重复修改产品代码。

空名称保留最多 32 个编码字符；已有名称保留原前后文字，在原光标处插入剩余可容纳的语音前缀，同时遵守 96 UTF-8 字节上限、不截断编码字符。名称已满则保留原值并明确提示。识别结果仍须手动保存，不更改共享语音响应、Mac／Windows 协议或 Codex 草稿。用户已在现有 `.267` 空名称中完成超长录音，明确反馈“已回填前段并提示保留 32 字，已退出未保存”。据此，空名称超长回填及提示的实体验收通过，旧“待实现”项关闭；没有保存测试事项或生成新固件。用户未提供完整识别正文，不另宣称逐字符独立核验；已有名称／多字节边界的本轮证据仍为本地检查。

本地已执行现有日程原生 LVGL 检查，使用依照 Windows `AgendaSelfTest.cs` 公开示例构造的三条合成事项，不是实际 RPC 抓包或 Windows 测试输出。`AGENDA_VOICE_TRUNCATION_OK` 通过恰好 32 字、超过 32 字、已有名称中间光标插入并保留后缀、四字节字符触发 96 字节上限、已满名称五类场景；取消／错误／不同会话及不自动保存断言同时通过。已检查实际渲染截图的“已保留 32 字，请检查后保存”提示。证据在 `artifacts/development/tab5-agenda-acceptance-20261024/`；运行的是已有 Mac 原生预览可执行文件，没有编译设备固件或修改运行实例。

The complete archived `.264` and installed `.267` source already contain the requested bounded agenda-title insertion; their files match byte-for-byte with the isolated copy. The earlier `.234`-based implementation backlog is stale. Empty titles keep up to 32 encoded characters; existing text is preserved around the original cursor and only the fitting speech prefix is inserted, respecting the 96-byte UTF-8 bound. Full titles remain unchanged with a clear message. No automatic save or shared transcript/protocol change is made. The existing native LVGL agenda checks pass with three synthetic public examples modeled after AgendaSelfTest.cs, not live RPC data or Windows test output. Five truncation cases cover exact/over-32 input, cursor/suffix preservation, the UTF-8 byte bound and full titles; cancellation/error/owner isolation and no-auto-save checks also pass. The actual rendered truncation hint was inspected. Evidence is retained under the linked artifact directory. Only an existing native preview executable ran; no device firmware was built. The user now confirms prefix insertion, the 32-character notice and leaving without saving on `.267`. Empty-title overlength acceptance passes and the old implementation item is closed. No full transcript was collected for independent character comparison; existing-title and multibyte boundaries retain native-test evidence only.

## 相机启动内存不足：已复现，源码调整待实机复验

用户在现有 `.267` 打开拍照界面后反馈“显示相机内存不足，请稍后重试。（1/12）”。本轮相机预览／拍摄验收失败，照片上传／任务附件仍待后续独立验收。已通过桥接原生运行监控请求一次经过认证的 Wi-Fi 诊断；相机尚未产生帧。安全数值证据保留在忽略目录 `artifacts/development/tab5-camera-memory-20261024/`，没有采集照片、草稿、账户或配对内容。检查后关闭桥接窗口，运行实例和设备设置保持不变。

源码中 `1/12` 是启动阶段的 `ENOMEM`，可能来自帧池缺失、任务分配或首次启动内存门槛，单凭该码不能唯一定位。失败后的样本显示内部可用内存不足首次注册门槛、PSRAM 仍有余量；但采样本身额外占用 8192 字节内部任务栈及通信资源，不能将其当作失败瞬间的堆记录。当前证据支持内部内存余量不足这一排查方向，不宣称已排除其余启动分支。

检查 `.267` 的真实 ELF／对象符号后，将日程的四份 CPU 数据快照改为启动时明确分配在 PSRAM 的单个工作区，不回退占用内部内存。公开读取在分配失败时继续返回服务未启动，互斥锁／队列失败会释放已分配资源；worker 启动失败拒绝新命令并保留错误提示，重复启动不重复分配。原有 NVS 格式、日程内容、删除回执、同步协议和工作逻辑均保持；相机的 48 KiB 总量／16 KiB 连续块检查、帧所有权和驱动清理没有改动。

- [源码补丁](../proposals/tab5-camera-memory-headroom.patch)和[来源／验证摘要](../proposals/tab5-camera-memory-headroom-source.json)已保存；按摘要规定的 LF 归一约定，补丁在独立 `.267` 文件副本检查、应用及逐文件比较通过。既有日历去重修改保留在同一隔离源码树。
- 实际日程 worker 与存储测试在原生 UBSan 下通过，覆盖 PSRAM 分配、互斥锁／队列／任务失败及重复启动，原有离线新增／编辑／删除、重启迁移、失败保留、回执、满额清理和年度缓存检查同时通过；存储最大长度、中文名称、截断和坏数据检查通过。ASan 组合进程未在 30 秒内返回，已终止，不能记为通过。
- 使用保留的 `.267` ESP-IDF 设置只编译修改后的 `agenda.c`，目标编译通过。该对象 BSS 从 16,568 降到 360 字节，减少 **16,208 字节**；对象 text 增加 422 字节。这是单文件静态结果，不是实机剩余内存、完整链接大小或相机恢复成功的证据。

**相机问题继续保留待办**：等其余验收及源码修复结束后统一构建，再测首次打开、拍摄、关闭重开，以及录音／日程操作后的打开，并复核日程保存和恢复。此次没有完整固件链接、生成镜像、递增版本、运行准备脚本或刷写；fixed latest 仍是原 `.267`（SHA-256 `60ccffe6c2e1625a36b11ee3ad60eeec72b737b02b11619ad51fd538d08ff0e4`）。

The user reproduced camera startup failure on `.267`, reporting insufficient memory `(1/12)`. Preview/capture acceptance fails; uploads and task attachments remain separate. One authenticated Wi-Fi diagnostic confirms zero frames. Only bounded numeric evidence is retained outside the repository; no photo, draft, account or pairing content was collected. Inspection windows were closed afterwards.

Stage 1/ENOMEM covers missing reserved frames, task allocation and the cold-start heap gate, so it does not uniquely prove one branch. The post-failure sample supports internal-memory pressure with PSRAM available, but includes the diagnostic worker's 8192-byte internal stack and transport overhead. It is not a failure-time heap trace. Inspection of actual `.267` symbols identified four CPU-only agenda snapshots in internal BSS. These now share an explicitly allocated PSRAM workspace without internal fallback. Allocation/resource failures remain safe, failed worker creation refuses commands, and repeated startup retains ownership. NVS formats, contents, receipts, protocol and worker behavior are preserved, as are camera thresholds, frame ownership and driver cleanup.

The source patch applies and compares cleanly using the manifest's LF-normalization convention. Native UBSan runtime/storage suites pass, including resource failures, offline persistence, migration, acknowledgements, retention and malformed data. The ASan combination did not return within 30 seconds and was terminated, not counted as a pass. Only the changed translation unit was cross-compiled with preserved `.267` settings: BSS decreases by 16,208 bytes and object text grows by 422 bytes. This establishes neither runtime heap availability nor camera success/full-image fit. Camera acceptance stays open for the combined firmware retest, including cold start, capture/reopen and post-voice/agenda operation. No full firmware link, image, version bump, preparation, flash or release occurred; fixed latest remains the original `.267`.

## 整机睡眠后恢复：实际操作通过

在现有 `.267`、USB 断开及原无线设置的测试安排下，用户先反馈睡眠后锁定和解锁正常，随后明确补充：“唤醒后已进桌面；随后锁定、解锁并观察 30 秒未回锁”。据此，本次手动唤醒后的 TAB5 操作恢复、再次锁定／解锁及 30 秒未回锁验收通过，无需因最初措辞不清重复睡眠测试。

睡眠约 15 秒是测试安排，未独立计时或采集系统睡眠日志；后续原生检查确认桥接收到一次整机睡眠和一次唤醒通知。实际无线通道和恢复耗时本轮未独立核验。唤醒时已进入桌面，因此本轮未覆盖唤醒后仍停在锁屏的起点，也不覆盖 TAB5 主动唤醒睡眠中的 Mac。未修改自动锁定设置、运行实例或固件。

On unchanged `.267`, with USB disconnected and existing wireless settings as requested, the user clarifies that manual wake entered the desktop directly, then TAB5 lock/unlock succeeded with 30 seconds observed without relock. This passes the observed post-wake operation recovery and subsequent lock/unlock cycle; no repeat sleep test is needed to resolve the earlier ambiguous wording. Subsequent native inspection confirms one system sleep and one wake notification. The requested 15-second sleep was not independently timed or verified in system logs, and transport/recovery latency were not independently measured. Wake-to-locked-screen and device-initiated wake remain untested. No automatic-lock setting, runtime or firmware changed during this sleep test.

## 取消解锁后再次请求失败：已修复，实机复测通过

2026-10-24，在已部署 `mac-unlock-cancel-recovery-20261024` Mac 与 TAB5 `.267` 上，用户按“解锁弹窗中取消、等待约三秒、不手动清空密码框、再次明确解锁、桌面观察三十秒”的安排反馈：“取消后停止，第二次解锁成功，30 秒未回锁”。据此，取消停止、取消后的新请求恢复及本轮三十秒不回锁实机验收通过，本项从待办关闭。间隔、未手动清空及桌面观察按测试安排与用户反馈记录，没有读取密码框正文或独立计时，也未另行采集本轮通道回执；不能扩大为所有通道分别验收、断连或长期稳定性通过。以下保留旧包失败及修复过程。

The user confirms on the deployed cancellation-recovery Mac and unchanged `.267`: cancellation stopped input, the next explicit unlock succeeded, and no relock occurred for 30 seconds. The requested sequence included an approximately three-second wait and no manual field clearing. This passes the observed cancellation/new-request recovery and closes this item. Timing and observation are user-reported, with no secure-field reading or independently collected transport receipt. Disconnect behavior, every individual transport and long-term reliability are not inferred. Earlier failures and repair evidence remain below.

用户在解锁弹窗进行中取消，观察到密码框已有一个字符、输入停止并留在锁屏；随后再次点解锁出现失败。取消停止本身有用户观察，但取消后的再次请求恢复未通过。只读检查运行中的 ble-priority 桥接，设备记录显示 Wi-Fi 关闭／后续准备阶段失败，Mac 明确记录“密码框可能已有输入，未发送准备字符或密码；请清空后重新发起”。最近两次停止均停在密码页准备阶段，没有进入密码写入／提交阶段。没有读取密码框内容；那个字符是否为固定准备字符仅由当前实现和现场行为推断，不当作读取所得。

根因定位到 `MacVNCUnlockTest.prepareWithPublicCharacter`：每个请求都要求初始为空，再发送固定公开字符 `a` 唤出密码页；取消可能留下该字符，后续新的请求因此拒绝。Mac 源码已调整为 `prepareForDirectSubmission`：对于已启用登录按钮的同一核验密码页，连续核对至少 150 ms 的就绪状态及可用的字符计数，成功后只执行原有整值替换及单次按钮提交，不再追加准备字符；未就绪空框仍走原有一次字符／确认流程。目标绑定时的计数检查仅在整值替换入口接受已有输入，键盘输入及公开字符诊断仍要求为空；布尔、负值、小数、非有限值和越界计数均拒绝。取消、焦点／窗口变化、按钮或计数变化、读取错误和超时仍停止。取消不会自动清空或继续提交，恢复只由用户新请求触发；不读取安全字段正文、不改协议、不重录密码。

修复仅涉及 Mac，TAB5 `.267` 与固定镜像保留。桥接原生检查同时确认一次整机睡眠／唤醒事件，补充前项确有整机电源事件；时长和恢复延迟仍未计时。检查窗口均已关闭。Mac 构建、自检及新候选实机复验在后续结果中登记，不能将源码改动直接记为实体通过。

The user cancelled while a character was visible, observed input stop with the Mac still locked, then found a subsequent explicit unlock failed. The cancellation stop is user-observed, while recovery after cancellation fails. Read-only native diagnostics confirm Wi-Fi close/preparation failures and the explicit nonempty-field rejection before credential writing/submission. No secure-field text was read; attribution to the fixed preparation sample is an inference from code and behavior.

The root cause is the old preparation function's empty-field requirement on every request. Cancellation may retain its public `a`, blocking the next request. The Mac source now revalidates an already enabled login page for at least 150 ms, including stable counts when supported, then uses the retained whole-value replacement and one button submission without another sample. Only the full-replacement entry accepts existing input at target binding; keyboard/probe routes still require an empty field. Boolean, negative, fractional, nonfinite and out-of-range counts are rejected. Empty/unready fields retain the original sample/acknowledgement route. Cancellation, target/button/count changes, errors and timeouts still stop; only a new user request can recover, with no automatic cleanup/retry, text reads, protocol or credential changes. Firmware stays at `.267`. The inspection also confirms one system sleep/wake event but does not measure duration or recovery latency. Inspection windows are closed; build/self-test/deployment and hardware retest results must be recorded separately.

构建证据：最终 Debug／Release 各 690 项隔离检查通过；完整 `swift test` 因已安装 CLT 缺少 XCTest 未通过。新增验证将取消留下准备字符与新请求整值替换串联，覆盖支持／不支持计数、目标变化、取消、计数／按钮变化、超时和异常计数类型。为控制优化展开，新增验证拆为独立函数，计数与准备函数保留内联边界；中止的中间构建不计为通过。证据在 `artifacts/mac-unlock-cancel-recovery-20261024/`，Swift 源码冻结后未再修改；随后仅增加应用图标的 plist 声明，单独记录摘要。

Final Debug/Release builds each pass 690 isolated checks. Full XCTest fails because the installed CLT lacks its module. New fixtures join cancellation with a subsequent explicit full replacement, covering both counter modes and cancellation/target/button/count/error/timeout boundaries. Validation is split into separate functions with bounded inlining; stopped intermediate builds are not passes. Frozen Swift sources remain unchanged; the later icon-only plist change has a separate hash record.

USB 解锁在途断连实体测试已完成。按临时“仅 USB”、解锁弹窗出现时拔线且不点取消、观察约二十秒的安排，用户明确反馈：“断线后输入停止，Mac 保持锁屏，已恢复原模式”。据此，在当前 Mac 包和 `.267` 上，USB 在途断线停止及不自行继续解锁的场景实机通过；观察时长按测试安排与用户反馈记录，未独立计时或另行采集该次后端终态回执。此前拔线后切蓝牙的普通操作证据仍独立保留，不扩大为 Wi-Fi／BLE 在途断连全部通过。未要求改 Mac 网络、权限或设备展示设置。

The user completed the in-flight USB-disconnect check on the current Mac bundle and `.267`, confirming that input stopped, the Mac remained locked, and the original connection mode was restored. The requested sequence used temporary USB-only mode and approximately 20 seconds of observation, without pressing Cancel. This observed USB-stop/no-automatic-unlock boundary passes; duration is user-reported rather than independently timed, and no separate terminal receipt was collected. Earlier ordinary unplug-to-BLE handoff remains distinct; in-flight Wi-Fi/BLE disconnect behavior is not inferred. Mac network/permissions and device display settings are unchanged.

### USB 未识别：断连测试尚未开始

用户随后反馈插上 USB 两次仍显示未连接，未能进入 USB 解锁在途断连步骤。本项记为前置连接未建立，不计为断连停止失败。只读系统检查未发现 USB 串口设备节点，旧 IOUSB 视图也未见外接设备；该视图不作为全系统枚举的完整证据；用户随后确认线仍插着，沿用同一根线及相同插槽，设备显示正在充电；不能据此断定线缆、接口或设备硬件故障。当前单实例仍为取消恢复／图标修复包；USB 发现、串口、服务与通道协调源码均与此前实机通过的 ble-priority 冻结版本一致，不将其当作排除所有运行时故障的证明。

已请求保持现场，确认当前线是否仍连接、是否沿用先前成功的数据线和 TAB5 数据接口，以及 Mac 直连／扩展坞情况。未请求继续反复拔插、清除配对、重录密码、进入下载模式、重启或刷机；未更改源码、运行实例、系统设置或设备展示设置。

The user reports two USB insertions without recognition, so the in-flight disconnect test has not begun. A read-only system sample shows no USB serial node; the legacy IOUSB view also shows no external device but is not a complete system enumeration. The user subsequently confirms continued attachment, the same cable/slots and charging indication; this does not establish a defective cable, port or device. The deployed bundle remains unchanged, and USB discovery/serial/service/coordinator sources match the previously accepted frozen bundle; runtime faults are not excluded by source identity. Clarification requests only the current attachment, previously working cable/device port and direct/hub connection. No repeated reconnection, pairing reset, credential entry, download mode, reboot, firmware or settings change was requested or performed.

重启前补充检查：用户确认接线未变后，系统再次没有 USB 串口，旧 IOUSB 视图未见外接设备。近期系统 USB 日志存在枚举失败事件，但未可靠关联具体设备，不保存原始事件或身份。桥接原生设备中心确认 Wi-Fi／蓝牙在线；一次经过认证的 Wi-Fi 诊断取得新鲜 `.267` 运行样本，界面响应及显示错误计数正常。当前无线快照没有 `usbDiag`，不据此推断 TinyUSB 的具体运行状态，也不将充电显示等同于数据链路正常。有限数字证据保存在忽略目录 `artifacts/development/tab5-usb-enumeration-20261024/`。

已按 [M5Stack 开关机说明](https://docs.m5stack.com/zh_CN/core/Tab5#开关机) 请求一次单变量恢复检查：保持线和接口不动，双击电源键关机，等约五秒，再单击开机；不进入下载模式。启动后约二十秒只观察 USB，不做锁屏／解锁。重启结果待用户反馈；没有清除配对、调整系统 USB 权限或生成新固件。原生检查窗口已关闭，桥接留在后台。

After attachment was confirmed, a second system sample still shows no USB serial node or device in the legacy IOUSB view. Recent USB logs contain enumeration failures without verified device attribution; no raw events/identities are retained. Native UI confirms Wi-Fi/BLE online and one authenticated Wi-Fi probe obtains a fresh `.267` runtime sample with responsive UI and no display errors. The wireless snapshot omits `usbDiag`, so it cannot establish the exact TinyUSB state; charging indication is not data-link confirmation. Bounded numeric evidence is retained in the ignored artifact directory. One normal power cycle is requested using the linked official method, keeping the cable/ports unchanged and avoiding download mode. The result remains pending; no pairing reset, system USB permission change or firmware generation occurred. Inspection windows were closed.

重启恢复结果：用户明确反馈“重启后 USB 已连接”。独立只读检查确认 `IOService/IOUSBHostDevice` 中存在匹配 `.267` 描述符的 TAB5 数据设备、系统已出现 USB modem 串口；桥接原生显示“USB 数据已确认”。新的实时 USB ACK 显示运行时长已重新开始，固件仍为 `.267`，USB 诊断已可读。本次在保持线及接口不变、桥接未重启或替换的安排下恢复连接，记录为“重启可恢复，根因未闭环”，不关闭异常观察项，也不计作在途断连测试通过。

检查方法校正：重启后发现旧 IOUSB 视图仍为空，但 `IOService/IOUSBHostDevice` 和串口、USB ACK 均有效，因此旧视图的零设备数不能独立代表全系统未枚举。已明确标注原始数字证据的视图范围；重启前没有采集 IOService 设备列表，不补造该证据。可靠的前后比较为串口未出现→已出现、USB 未连接→实时回执确认。没有重新配对、修改显示设置或生成固件。原生检查窗口已关闭；下一步继续此前未开始的 USB 在途断连测试。

The user reports USB recovery after the normal power cycle. Independent IOService USB-host and serial-client reads now identify the expected TAB5 descriptor and USB modem, and the native bridge confirms live USB data/ACKs on unchanged `.267` with restarted uptime. The cable/port and Mac bundle were unchanged under the requested sequence. Record this as restart-recoverable with unresolved root cause, not closure or in-flight disconnect acceptance. The legacy IOUSB view remains empty even with valid IOService enumeration and ACKs; its earlier zero count was scoped incorrectly as a global count. Evidence now explicitly marks that limitation, without inventing a pre-restart IOService sample. The reliable before/after observations are the serial node and actual bridge USB receipts. Windows were closed; in-flight disconnect testing remains next. No pairing, display or firmware change occurred.

### USB 断连收尾与 ESP8266 硬件边界

用户确认 USB 在途断连停止且保持锁屏后，系统只读检查再次发现 TAB5 的 IOService USB 设备和 modem 串口；桥接原生确认 `.267` 的“USB 数据已确认”。因此当前连接也已恢复。没有为此要求重复拔插或正常锁／解回归，未独立测量本次回连时延或收集新的设备重启计数。检查窗口关闭，日常连接模式恢复来自用户确认。此前重启才能恢复的 USB 枚举异常继续保留，单次恢复不等于根因修复。

按顺序检查旧设备：原生 ESP8266 卡片为 USB 未连接、未发现设备地址，用户随后明确确认“当前没有可接入的小屏”。本轮硬件实机回归暂缓，不计为通过；未要求刷写、重置网络或改小屏显示策略。后续转入 TAB5 日程双向同步等全模块实际回归。

Post-test read-only IOService/serial and native bridge observations independently confirm restored USB data on `.267`. No repeat unplug or ordinary lock/unlock was requested; reconnect latency and fresh reboot counters were not collected. Original-mode restoration is user-confirmed, and the earlier restart-recoverable enumeration issue remains open. The ESP8266 card is offline, and the user explicitly confirms no connectable small-screen hardware is available. Its physical regression is deferred, not passed, with no flash/network/display changes. Remaining TAB5 module acceptance proceeds next.

## 日程双向同步：本轮新增／改名／删除及清理通过

ESP8266 因用户当前无可接入硬件而暂缓后，继续 TAB5 全模块实际回归。已读 Mac 日程调用方、存储规则及设备编辑器：设备新建默认有时间，编辑无时间事项会保留无时间；为避免产生提醒，本轮从 Mac 发起无时间测试。原生“全部事项”列表确认原为空，只有空白新建表单，没有需要保留的编辑草稿。

通过正式 Mac 窗口新增唯一合成事项“同步验收1024”，取消“设置时间”后保存一次，原生列表确认该名称及“无时间”。未更改已有事项，未设置提醒时刻，窗口随后关闭。已请用户在 TAB5 查看、改为“同步验收1024已改”并保存，先保留这一条供 Mac 回读；设备显示、回传及最终清理都尚未确认，不能将 Mac 本地保存等同为双向同步已通过。证据和清理待办保存在忽略目录 `artifacts/development/tab5-agenda-live-acceptance-20261024/`。

With ESP8266 hardware unavailable, live TAB5 module acceptance continues. Caller/store/device-editor review confirms that device creation defaults to timed items while editing an existing untimed item preserves that state. The native Mac all-items list was empty with no unsaved user draft, so one synthetic untimed item, `同步验收1024`, was created through the normal UI and confirmed as saved/untimed. No existing item was changed or alarm time set; windows were closed afterwards. The user is asked to view and rename it to `同步验收1024已改` on TAB5. Device visibility, return synchronization and eventual cleanup remain pending; local persistence alone is not a bidirectional-sync pass. The ignored artifact progress file explicitly retains the cleanup obligation. Firmware and display settings remain unchanged.

日程改名实机结果：用户明确反馈在 TAB5 已看到测试事项，并改名保存为“同步验收1024已改”。随后只读打开 Mac 原生日程“全部事项”，唯一行精确显示该新名称及“无时间”。据此，Mac 新增→设备显示、设备改名→Mac 回传及保持无时间的本轮场景通过；未测量传输时延，也不将其扩大为离线合并、并发冲突、重复日程和提醒全部通过。检查窗口已关闭。

已请用户仅在 TAB5 删除该测试事项并返回原日常页面，随后再核对 Mac 删除回传；删除结果和测试数据清理仍待确认。未由工具删除真实或测试日程，也未改动其他事项、显示设置、固件或运行包。

The user confirms viewing the item on TAB5 and saving its new name. A subsequent read-only native Mac all-items view independently shows exactly one row named `同步验收1024已改`, still untimed. Mac creation/device display and device rename/Mac return synchronization pass for this observed case. Latency, offline merging, conflict handling, recurring schedules and alarms are not inferred. Windows were closed. User-side deletion of this single synthetic item and return to the daily page have been requested; Mac deletion propagation and cleanup remain pending. No item was deleted by a tool, and no other item, display, firmware or running bundle changed.

日程删除收尾：用户确认已在 TAB5 删除唯一测试事项并返回日常页。Mac 原生“全部事项”随后确认无任何行，恢复测试前空列表。Mac 新增→TAB5 可见、TAB5 改名／删除→Mac 回传和测试清理的本轮场景通过；不扩大为所有方向／离线／冲突／提醒排列均通过。清理待办已关闭。

The user confirms deleting the sole synthetic item on TAB5 and returning to the daily page. Native Mac all-items inspection then confirms the empty pre-test state. The observed Mac-create/device-view and device-rename/delete/Mac-return paths pass, and cleanup is complete. Other permutations, offline/conflict and alarm cases are not inferred.

## 生日同步：显示与删除清理通过

原生生日列表原为空。已在 Mac 正式窗口新增唯一合成生日“生日验收1024”，公历 12 月 31 日、提前零天，添加列表并保存同步后重新读取，确认仅一条、日期和历法正确。该未来日期／当天提醒设置避免今天产生生日提醒。没有改动既有生日。已关闭检查窗口并请求用户在 TAB5 日历选择 2026 年 12 月 31 日核对显示及无重复；实体显示和最终测试数据清理尚未确认。证据及清理义务保存在 `artifacts/development/tab5-birthday-live-acceptance-20261024/`；不改显示策略或生成固件。

The native birthday list was empty. One synthetic Gregorian December 31 birthday, `生日验收1024`, was added and saved with zero-day lead, then reopened and confirmed as the sole stored row with correct date/calendar. Its future date/day-only reminder avoids a birthday alert today. Existing birthdays remain unchanged. Windows were closed; the user is asked to inspect December 31, 2026 on TAB5 for the expected single birthday. Device visibility and cleanup remain pending in the ignored progress artifact; no display-policy or firmware change occurs.

生日实体显示结果：用户明确确认 2026 年 12 月 31 日显示正确，只有一条测试生日。本轮公历生日保存→TAB5 指定日期显示及无重复验收通过，不推定农历／闰月、生日提醒或跨全部年份均已实测。

收尾已打开 Mac 正式“日历与生日”窗口，只选中唯一测试项“生日验收1024”，未执行移除或保存；已请求用户点击“移除→保存并同步”，再看 TAB5 同一日期确认测试生日消失并返回日常页面。此窗口为用户操作而有意保持打开，完成后再关闭；删除回传和恢复原空列表尚待确认。

The user confirms exactly one correct birthday on December 31, 2026. This observed Gregorian-save/device-display and no-duplicate case passes, without inferring lunar/leap-month, alarm or all-year hardware coverage. The normal Mac birthday window is now open with only the synthetic row selected for the user's Remove/Save cleanup and subsequent device disappearance check. No removal/save was performed by the tool. The window is intentionally left open for that interaction; deletion propagation and return to the empty baseline remain pending.

### 已发现的旧状态提示残留

日程删除后重开及生日保存后重开时，原生窗口仍保留上次“已保存／列表已更新，点击保存并同步后生效”提示，但实际列表已正确刷新。调用方仅保留旧 status，没有在窗口重新打开／重读成功后清空。已保存[最小源码补丁](../proposals/mac-profile-status-reset.patch)及[摘要](../proposals/mac-profile-status-reset-source.json)：生日重读成功时清空旧提示，日程仅在从关闭状态打开时清空再刷新，避免十秒定时刷新擦除正在查看的结果。当前仅通过补丁应用检查，未应用至工作源码、未替换运行包；留待 Mac 统一修复批次编译和实际复开核验，不记为已修复。

The reopened agenda/birthday windows retain stale saved/draft status text although their lists refresh correctly. A minimal source patch clears birthday status after successful reload and agenda status only on reopening from a closed window, preserving visible timed-refresh results. The linked patch/hash record is preserved for the final Mac repair batch. Application checks only have passed; working source, runtime and firmware remain unchanged, and final build/native-reopen verification is pending.

生日清理收尾：用户已移除测试生日并保存，明确确认 TAB5 同日测试生日消失且返回日常页。随后 Mac 正式生日列表重新打开为空，恢复测试前状态，检查窗口已关闭。该公历生日新增显示、无重复、Mac 删除→TAB5 消失及测试数据清理通过。

Birthday cleanup: the user removed and saved the synthetic birthday, confirmed its disappearance on TAB5 and returned to the daily page. The reopened native Mac list is empty, restoring its initial state. Inspection windows are closed. The observed Gregorian create/display, uniqueness and Mac-delete→TAB5 removal case is accepted and clean.

## 菜单栏与 Windows 右键菜单对齐

用户要求菜单栏仅保留机器人图标、两平台右键菜单一致并简洁。核对真实调用方后，Windows 当前使用 `DeviceCenterMenu`，旧 `TrayMenu` 不是托盘基准；Mac 原先仍把显示、网络、资源和诊断约三十项平铺。现按 Windows 结构重建 Mac 菜单：设备中心、可用的预览、按 ESP8266／TAB5 稳定排列的自定义设备名称二级菜单、退出。两种设备共十个快捷项的名称、顺序和路由标识对齐，停用设备置灰，删除或重新注册后的旧菜单目标不会执行；选项先关闭菜单，再进入现有正式设置。旧版资源导入收回 ESP8266 外观设置。图标入口改为 imageOnly，状态保留中文悬停提示，定时更新不再写菜单栏标题。

用户进一步要求将“小屏预览”统称“预览”，两平台菜单、Windows 预览标题及相关提示同步改名；现有预览能力和传输未改变。Windows 本轮只有展示文本变化，仍须实际 Windows 构建／运行验证。Mac 菜单的空列表、单设备、双设备、停用、长名称、快捷项目标及关闭后异步派发有隔离检查；最终构建／部署另行记录。

The user requests an icon-only menu bar and matching concise context menus. The actual Windows tray uses DeviceCenterMenu rather than legacy TrayMenu. Mac now follows its Device Center / available Preview / named device groups / Exit structure, with the same ten shortcut labels, order and routing keys. Disabled groups are greyed out, stale removed/re-registered targets are rejected, and menu tracking ends before existing settings open. Legacy resource imports move inside appearance settings. Activity labels remain in the tooltip rather than the menu-bar title. Both platforms rename Small-screen Preview to Preview without changing rendering/transport. Windows changes are presentation text only and still require Windows build/runtime evidence; Mac has isolated contract/routing cases, with final build/deployment recorded separately.

菜单精简部署结果：`artifacts/mac-tray-parity-20261024/AIBotBridge.app` 已签名并正常替换运行，只有一个桥接实例。最终 Debug／Release 各 796 项隔离检查通过，其中 106 项为新增菜单契约／目标和异步派发检查；Release 构建、严格签名及原钥匙串 helper 身份／私有管道检查通过。完整 swift test 已执行，仍因 CLT 缺少 XCTest 失败。Windows 两条必需命令均已尝试，本机无 dotnet，均退出 127，不记为 Windows 验收通过。两平台源代码中十个设备快捷项及“预览”名称比较通过，Windows 本轮仅五个文件共八处展示文字替换。原生 Mac 设备中心确认 `.267`、USB 数据确认、认证 Wi-Fi 与蓝牙 USB 优先待命；检查窗口已关闭。已请求用户只查看图标与右键菜单，实际精简显示反馈尚待确认。固定固件摘要保持原值，未生成／刷写固件，未发布。

The signed compact-menu Mac bundle has replaced the old app through normal quit/start and is the sole bridge instance. Final Debug/Release each pass 796 isolated checks, including 106 new menu contract/target/deferred-dispatch checks. Release compilation, strict signing and unchanged-helper private-pipe checks pass. Full XCTest still fails for missing CLT support. Both required Windows commands were attempted and exit 127 because dotnet is absent; Windows acceptance is not claimed. Source comparison matches all ten shortcuts and the Preview label; Windows has eight presentation-string replacements in five files. Native Mac UI confirms unchanged `.267`, USB ACK, authenticated Wi-Fi and authenticated BLE in USB standby. Inspection windows are closed; human visual acceptance of the new menu is requested and pending. Fixed firmware hash is unchanged, with no firmware build/flash or release.

菜单精简实体收尾：用户明确确认“显示正常，菜单已精简”。据测试安排，本轮仅查看顶部机器人图标及设备中心／预览／设备分组／退出的菜单结构，未点击设置或改变设备页面。纯图标和精简菜单显示验收通过，此项关闭；不扩大为全部快捷项的实体打开／保存、Windows 实机或实时镜像通过。

The user confirms normal display and the simplified menu after the requested visual-only inspection. Icon-only and compact-menu appearance pass and this item is closed. Settings/device pages were not changed; this does not establish every shortcut's physical open/save behavior, Windows runtime acceptance or live screen mirroring.

## 新增实时镜像需求：可行性已核对，尚未实现

用户希望“预览”按设备显示真实当前屏幕，而非现有桥接效果图。当前 Mac／Windows 均只有 ESP8266 数据效果绘制，TAB5 固件没有画面回传指令，不能把改名当作真实镜像已经实现。TAB5 维护者源码有实际 LCD 双缓冲和 LVGL flush 路径，可据此设计屏幕帧采集，但必须遵守其 DMA／相机缓冲所有权，不为预览复制额外全屏内存或暂停正常界面。ESP8266 当前 ST7789 配置只配置写通道、未配置 MISO；不能凭库存在 readRect 接口推定本机屏幕可读。若无法读回，需验证从实际绘制输出采集的低开销方案。240×240 RGB565 原始帧为 115,200 字节，在 460800、8N1 的 USB 串口上仅原始数据的理论传输下限约 2.5 秒，尚未计协议；不得承诺高帧率。官方 TFT_eSPI 文档也区分屏幕读回支持及 ESP8266 全屏彩色缓冲的内存限制。

目标是打开预览后才采集／传输、关闭即停止，通过已配对连接传送，先做只读镜像；刷新率、分辨率／色彩和正常业务占用须测后决定。ESP8266 当前无可用实体，实机验证仍受阻。此项需要独立的两端协议、固件及桥接实现和验收，当前没有协议补丁／工作实现，不将数据效果图冒充完成，也未生成固件。来源：[TFT_eSPI 官方说明](https://github.com/Bodmer/TFT_eSPI#sprites)，本机调用方为 firmware/src/main.cpp、firmware/platformio.ini 及归档 TAB5 esp_lvgl_port_disp.c。上游 master 资料仅作可行性参考，不当作已固定 2.5.43 依赖的编译／硬件结果。

The newly requested capability is each device's real current screen. Existing desktop preview is an ESP8266 data rendering; TAB5 has no screen-return command. Its actual panel buffers/flush path offer a capture design route, subject to DMA/camera ownership. ESP8266's current write-only configuration does not establish working screen readback; low-overhead output capture needs validation. A raw 240×240 RGB565 frame is 115,200 bytes, taking at least 2.5 seconds at 460800 8N1 before protocol overhead. Upstream TFT_eSPI documents hardware-dependent readback and constrained ESP8266 sprite memory; master documentation is feasibility reference, not pinned-library or hardware validation. The target is on-demand authenticated, read-only mirroring with measured performance and no background capture after close. No implementation/protocol patch, hardware pass or firmware build is claimed. ESP8266 remains physically unavailable.

## Mac 应用图标补齐

后续用户澄清：所指为顶部菜单栏标识。此前只补齐应用包 `AppIcon.icns` 与系统图标解析，菜单栏仍仅设置状态文字，不能记为用户所需 logo 已完成。已在启动时为现有状态按钮加载同一包内机器人图标，设为 18 点并显示在文字左侧；周期刷新仍仅更新状态文字，原左右键操作保持。该补充正在独立 Mac 构建与部署验收，不生成 TAB5 固件。

The user clarified that the requested logo is the top menu-bar identifier. Earlier bundle-icon packaging and NSWorkspace resolution did not fulfill that request: the status button remained text-only. Startup now attaches the same bundled robot icon at 18 points to the left of existing status text; periodic title refresh and click actions retain their behavior. Mac build/deployment verification is in progress, with no TAB5 firmware generation.

菜单栏补充部署：`artifacts/mac-menubar-logo-20261024/AIBotBridge.app` 已通过正常退出／启动替换旧包。对比前一已验收包的冻结源码，本轮只有 `AppDelegate.swift` 上述七行变化；图标字节、钥匙串 helper、签名身份及其他源码保持。Release 构建通过，690 项隔离检查通过，签名严格校验及 helper 身份／私有管道检查通过；完整 `swift test` 已执行，仍因本机 CLT 缺少 XCTest 失败。原生设备中心确认 `.267` 不变、USB 数据确认、Wi-Fi 已认证及蓝牙已认证 USB 优先待命；检查窗口已关闭。自动截图仅涵盖程序窗口，系统菜单栏检查接口超时，实际 logo 可见性已请求用户目视确认，尚不记为实机显示通过。未生成或刷入固件。

The menu-bar build has replaced the old bridge through normal quit/start. Its frozen-source delta is exactly the seven AppDelegate lines above; icon bytes, Keychain helper, signing identity and all other sources are unchanged. Release build, 690 isolated checks, strict signing and helper identity/private-pipe checks pass. Full swift test was attempted and still fails for missing XCTest in this CLT installation. Native device UI confirms unchanged `.267`, USB data ACK, authenticated Wi-Fi and authenticated BLE in USB-priority standby. Inspection windows are closed. App screenshots omit the system menu bar and its UI provider timed out; user visual confirmation is requested, so visible menu-bar acceptance remains pending. No firmware was generated or flashed.

菜单栏实体反馈：用户明确确认机器人图标“已出现，显示正常”，本轮 logo 可见性验收通过；实际证据为用户观察，自动截图能力限制仍保留。用户随后指出旁边状态文字多余，继续改为图标入口，将状态保留在悬停提示。

The user confirms the robot is visible and displays normally, accepting menu-bar visibility through human observation. The automatic screenshot limitation remains. The subsequent request removes adjacent status text from the menu bar and retains it in the hover tooltip.

菜单栏精简调整：启动时将已有机器人按钮设为 `imageOnly` 并清空标题；定时刷新改为更新悬停提示，使用“Codex／Claude：工作中、空闲、离线”的完整文字，避免下次刷新重新出现缩写。资源加载失败时保留原应用名称作为可点击入口。设备状态发布、工作状态判断及左右键操作未改。本轮仅此 Mac 显示调整，构建与部署结果另行登记。

Menu-bar simplification sets the robot button to image-only with an empty title, and periodic updates now refresh a tooltip with full Codex/Claude status labels. This prevents the old abbreviations from returning on the next timer tick. A missing icon retains the application name as a usable entry. Device publication, activity classification and click actions are unchanged. Only this Mac presentation change is being built and deployed.

旧 Mac 包缺少图标资源及 `CFBundleIconFile`。新增 `scripts/build_macos_app_icon.py`，核验 `licenses/materials.json` 中已登记的原机器人 ICO 摘要，沿用原有 PNG 帧，仅为更大尺寸缩放，并用系统 `iconutil` 打包。`scripts/build_macos_app.sh` 接入生成步骤，plist 声明 `AppIcon.icns`。不新增美术来源，不改变菜单栏已有状态文字。

实际 ICNS 含 16–1024 像素的原生表示；系统导出验证十种表示的尺寸与透明通道，直接检查五个匹配原尺寸的 PNG 表示，其 RGBA 与原素材完全一致。包内记录素材、生成脚本及图标摘要；原生图标显示随部署检查。图标构建及语法检查通过；无固件生成。

The old Mac bundle omitted both its icon and `CFBundleIconFile`. A new packaging helper checks the reviewed robot ICO against the provenance manifest, reuses its PNG frames and resizes only larger representations before native ICNS packaging. The standard Mac build script and plist now include it. No new artwork source or menu-bar status-text change is introduced. Native export verifies ten 16–1024 px representations and transparency; five matching-size PNG payloads preserve exact original RGBA pixels. The bundle records source/generator/icon hashes. Native display is checked during deployment; no firmware is generated.

## 取消恢复／图标候选部署

已正常退出 ble-priority，并启动 `artifacts/mac-unlock-cancel-recovery-20261024/AIBotBridge.app`。进程核对仅一个新包实例，包摘要与严格签名核验通过，原钥匙串 helper 字节一致且私有管道检查通过。原生设备中心确认 TAB5 仍为 `.267`、USB 未连接、Wi-Fi 已认证、蓝牙数据已确认；未重设权限、配对、密码或显示偏好。检查窗口已执行关闭，不为核验关闭结果再激活桥接。

系统 `NSWorkspace.icon(forFile:)` 从实际部署包取得并渲染机器人图标，已目视核验；渲染为 `workspace-app-icon.png`。Finder 原生窗口检查遇到 `cgWindowNotFound`，不记为 Finder 窗口实测通过，也不绕过该限制。应用图标的声明、资源、系统图标解析与签名均完成核验。用户随后确认取消后再次解锁及三十秒不回锁实机复验通过；之前普通三通道验收属于旧 ble-priority 包，不能扩大为新包全部边界通过。

The new cancellation/icon bundle replaced ble-priority through normal quit/start and is the sole running bridge. Bundle hashes, strict signing and unchanged-helper/private-pipe verification pass. Native UI confirms unchanged `.267`, disconnected USB, authenticated Wi-Fi and confirmed BLE data; no re-enrollment or preference changes occurred. Inspection windows were instructed to close, without reactivating the bridge to verify closure. NSWorkspace resolves and renders the deployed bundle's robot icon successfully. Finder-window inspection is unavailable (`cgWindowNotFound`) and is not claimed as a physical GUI pass. The user subsequently accepted cancellation followed by a new explicit unlock and 30 seconds without relock; earlier ordinary three-transport results belong to the previous bundle.

## 快捷发送与录音复测收尾

2026-10-24，当前 `mac-usb-ble-unlock-20261024-ble-priority` Mac 与 TAB5 `0.2.267-ui` 上继续复测。运行进程及语音／发送相关七个源文件与该候选冻结源码一致，包含 r78 的连接缓存复核修复。原生设置确认 Mac 本地识别已启用、USB 未连接、Wi-Fi 已认证及蓝牙数据已确认；这证明无线连接就绪，不将两条无线连接均在线推定为本次操作经过两条通道。检查窗口在测试前关闭，避免抢占 Codex 焦点。

本轮安排为快捷控制台录音约 20–30 秒，中间停顿约两秒后继续；手动停止并核对回填，等待 20 秒后只发送一次，以覆盖此前 15 秒连接缓存的过期条件。用户明确反馈“两项正常：录音完整，发送一次并收到回复”，本聊天也收到用户标注的测试消息；该消息要求不回复，已遵照。录音完整性、操作间隔及用户端结果以用户反馈为证，不把测试安排当作独立计时或逐字识别验证。

按用户要求，**快捷发送偶发失败与录音中断暂时移出当前待办**，不再为未复现问题追加修复或重复试录；后续再次出现时保留现场和既有有界诊断，再定位。历史失败与根因未明的记录保留，不宣称永久修复、所有通道分别通过或长期稳定性已验收。本轮没有修改产品代码、识别方式、设备展示设置或固件。下一项继续日历年度更新的联网核验。

On 2026-10-24, the running ble-priority Mac and TAB5 `0.2.267-ui` were retested. Seven relevant source files match the candidate's frozen sources, including r78's expired-connection revalidation. Native UI confirmed local Mac recognition, disconnected USB, authenticated Wi-Fi and acknowledged BLE; simultaneous wireless readiness does not prove which transport carried this test. Inspection windows were closed before testing.

The requested test used approximately 20–30 seconds of dictation with a short pause, manual stop and staging verification, followed by a 20-second wait and one submission. The user explicitly reported complete recording and a single successful send/reply. This chat also received the marked test message, whose no-reply request was respected. Timing and text completeness are user-reported, not independently measured. At the user's request, both intermittent issues are removed from the active backlog for now and will be investigated if they recur. Historical failures remain; this is not a permanent-fix, all-transport or long-term reliability claim. No product code, recognizer, device display settings or firmware changed. Annual-calendar network verification is next.

## 快捷发送

桌面 IPC 的最近验证状态只有 15 秒有效期。旧发送入口会先按缓存拒绝请求，不能利用后面的实时 owner 核对恢复健康连接。现在将操作授权和连接显示缓存分开：直接发送仍实时读取目标会话；快捷发送在缓存过期时先只读核对，成功后才允许清空草稿。明确只读、关闭发送授权、目标 owner 不存在或协议不兼容仍拒绝。最终派发前再次检查授权。

已保存的回执优先返回；相同请求不会因缓存过期再次派发。会话 ID、原草稿逐字匹配、正在运行／待确认状态、持久化不确定回执和不自动重试规则保持。测试使用合成 IPC 和编辑器，没有向真实 Codex 会话发送消息。

这是一个确定的缓存边界修复，不代表早先全部“未确认发送”或录音中断都已查明根因。实体快捷发送与录音中断仍需后续日常使用观察。

## 日历年度更新

新增独立的后台年度数据读取器，启动及运行期间检查；成功后每天检查，失败一小时后重试。先恢复全部缓存，再进行网络请求，状态帧生成不等待网络。

- 节气来自香港天文台年度公历／农历对照表，校验 24 个节气、年份、月份、顺序及重复；读取当前年前一年至后三年。内置经公开年表核对的 2026–2028 年日期作为离线回退。
- 假期沿用 Windows 的 MIT `NateScarlet/holiday-cn` 数据源。它是引用国务院公告的第三方数据集，不是政府 API；校验公告来源字段、年份、日期、布尔值、重复、七类假期覆盖与大小。读取前一年至后一年。
- 未公布的空表不替换缓存；异常下载、格式错误和写入失败保留最近成功数据。次年公告包含的上一年 12 月调整合并到对应年份。
- 首页今日／明日、月历年度表、法定工作日提醒使用同一数据。2026 内置表仍可离线使用；未知假期年份继续显示待更新并暂停该年的法定工作日提醒，不从农历推断放假。
- 现有 `terms`、`holidayYears`、`holidayUpdate`、`workdayYears` 字段已由 Windows 和 TAB5 使用，没有变更协议或要求刷机。对端核对包括 Windows `Tab5Calendar` / `Tab5HolidayStore` / `Tab5SolarTerms` 和留存 .234 源码 `ui_calendar.h`；.264 尚无完整本地源码，不能声称已做 .264 新固件验证。

公共数据源：[HKO 2027](https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2027c.txt)、[HKO 2028](https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2028c.txt)、[holiday-cn](https://github.com/NateScarlet/holiday-cn)。2026-10-24 核对时，该数据集 2027 文件仍是未公布空表。原数据许可见 `licenses/holiday-cn/LICENSE`；没有迁入第三方实现代码。

## 额度历史核对

“昨日额度仍为 4”来自上游 Windows 迁移记录，缺少该案例对应样本，不能把它直接认定为当前 Mac 的已复现缺陷。本轮只读检查本机保存样本，并用实际 Swift 计算器核对每日增量：没有复现该固定值。未更改历史文件或算法，未读取／输出凭据、账户标识或供应商原始响应。用户随后确认“目前已正常”，本轮将当前额度显示异常关闭；历史案例根因未确认，不补作算法修复结论。

## 年度日历联网复核（语音／发送复测后）

2026-10-24，使用与生产相同的临时 `URLSession`、15／20 秒超时、逐字节大小限制，并直接编译当前 `MacCalendarStore.swift` 的实际解析器，执行无凭据、无用户缓存写入的独立网络探针：

- 2026 假期 HTTP 200，4843 字节，实际解析器确认有效安排；2027 HTTP 200，236 字节，解析为尚未公布的空表，不将其保存为有效安排。
- 天文台 2027 节气文本在 `www.hko.gov.hk` 返回 `NSURLErrorDomain/-1200`，底层 `kCFErrorDomainCFNetwork/-1200`；本机 curl 同样在 TLS 建连失败，尚未取得 HTTP 正文，不能归为年表解析错误。
- 依据天文台[官方文字年份目录](https://www.weather.gov.hk/tc/gts/time/conversion1_text.htm)和官方站点搜索，另以同一原生接口检查 `www.weather.gov.hk`、`my.weather.gov.hk` 对应年表，两者也在 TLS 建连失败。网页检索可读到[2027 官方年表](https://www.weather.gov.hk/tc/gts/time/calendar/text/files/T2027c.txt)，不等于本机应用下载成功。没有关闭证书校验、降级 HTTP、修改系统网络或替换数据来源。
- 只读本机正式缓存仍仅有 2025／2026 假期文件，没有节气文件。现有 2026–2028 离线节气回退与失败保留策略保持。本次核对了 Windows 调用方及当前 `.267` 候选中的 `ui_calendar.h`，既有年度字段消费兼容，无协议或固件变更。

探针源码、可执行文件及只含公开来源／固定错误码的输出保存在忽略目录 `artifacts/development/mac-calendar-network-20261024/`。本次确认假期在线读取及未公布年份处理；**天文台本机联网下载仍未通过，保留待办**。设备跨年显示须另由实体观察确认，不把独立探针作为设备验收。本轮产品代码与运行实例未改动。

Annual-calendar network follow-up: a standalone native probe uses the production ephemeral URLSession settings, bounded byte reading and the actual compiled MacCalendarStore parser, without credentials or writes to user caches. The 2026 holiday response is HTTP 200/4843 bytes and parses as published; 2027 is HTTP 200/236 bytes and correctly remains unpublished. HKO's main host and two official alternative hosts all fail locally during TLS establishment with NSURLErrorDomain/-1200, before any body reaches the parser; curl also fails its handshake. The official 2027 table is accessible through web retrieval, which does not establish native-app connectivity. No certificate bypass, HTTP downgrade, system-network change or source replacement was made. Existing 2025/2026 holiday caches and verified 2026–2028 offline solar terms remain. Windows producers and the current `.267` calendar consumer were checked without protocol or firmware changes. Native HKO download acceptance remains open; device year-boundary display requires separate physical confirmation. Product code and the running app are unchanged.

## 日历实体反馈与补班文案去重

用户已确认 TAB5 `.267` 的 2027 年 1 月 5 日“小寒”和“放假安排待更新”显示正常，此两项跨年显示通过实体核对。2026 年 10 月 10 日为国庆调休补班日，日期属性与[国务院办公厅通知](https://www.beijing.gov.cn/cs/gncs/zcwj/202603/t20260327_4568275.html)一致，但用户指出点击后的详情重复显示两个“调休补班”。

已在 `.267` 的实际 LVGL 界面代码复现：`calendar_holiday` 将补班日返回为“调休补班”，详情拼接又追加同名工作状态，形成“调休补班 · 调休补班”。修复仅在组装详情时去掉补班的重复名称，保留工作状态及不同的节日名称；月份网格的“班”标记、放假、节气和生日不变，没有改动日期数据或协议。

[源码补丁](../proposals/tab5-calendar-makeup-label.patch)与[来源摘要](../proposals/tab5-calendar-makeup-label-source.json)已保存。补丁在原 `.267` 源码上 `git apply --check` 通过；隔离副本的实际原生 LVGL 渲染及断言通过，覆盖单次补班文案、“班”标记、国庆与生日、普通工作日／周末、寒露、2027 小寒／待更新，以及合成节日与补班重叠。修复前后截图与输出保存在 `artifacts/development/tab5-calendar-label-20261024/`，已检查补班和跨年截图。

这是源码与本地界面验证通过，**修复尚未安装，实体去重验收仍待统一固件更新后进行**。用户要求暂不生成候选后，已停止刚启动的完整构建；未产生新镜像、未运行准备脚本、未刷写或改变设备设置。天文台本机联网失败仍独立保留，不因跨年离线显示正常而关闭。

The user physically confirmed the January 5, 2027 solar term and unpublished-holiday notice on `.267`. October 10, 2026 correctly represents a makeup workday under the published schedule, but its detail label duplicates the status. Actual LVGL rendering reproduces the cause: the calendar holiday helper and schedule field both return the same text. The source fix suppresses only that redundant makeup name, retaining any distinct festival, the workday status and grid marker. Native assertions and rendered previews pass for makeup days, holiday/birthday labels, ordinary weekdays/weekends, solar terms, unknown-year notices and a synthetic festival/workday overlap. The patch applies cleanly to the preserved `.267` source. This is source/native UI verification only; hardware acceptance of the fix awaits the combined update. At the user's request, the newly started firmware build was stopped during toolchain initialization, before any image or preparation step. Running firmware, fixed latest and display settings remain unchanged. Native HKO connectivity remains a separate unresolved item.

## 验证

- Debug 构建通过；`git diff --check` 通过。
- 要求的 `swift test --package-path mac-app` 已执行，当前 Command Line Tools 缺少 `XCTest`，完整测试没有运行成功。
- 临时断言适配器链接 SwiftPM 实际 Debug 链接列表（排除入口），执行 94 项真实测试方法通过，覆盖发送、会话同步、授权、回执、额度与日历。这不是 XCTest；脚本与输出在忽略的 `artifacts/development/mac-remaining-20261024/`。
- 对公共数据的额外检查：真实 2026 假期 JSON 可解析；2027 空表保持待公布。香港天文台通过网页检索可读取，已逐项核对 2027／2028 的 48 个节气日期；本机 Python／curl 直连遇到 TLS 失败，不能记为联网下载验收通过。因此内置已核对的离线节气表，后台仍按失败保留与重试规则运行。
- 最终候选 `artifacts/mac-remaining-20261024-r78-offline/AIBotBridge.app` 的 Release 构建、严格签名及 501 项应用隔离自检通过；94 项定向断言检查通过。包旁 SHA256SUMS 覆盖 11 个文件，含 LICENSE／THIRD_PARTY_NOTICES；钥匙串 helper 与 r76 字节一致。旧 r78 为未启动的中间包，以带 `-offline` 的最终路径为准。
- 首次启动验证受阻：界面工具报告 Mac 已锁屏，未替换当时运行的 r76。用户回复后界面已可操作，后续部署结果见下节。没有发布、推送或创建标签。

## 部署复核（用户回复后）

用户确认额度显示“目前已正常”后，本机界面已可操作。已正常退出 r76 并启动最终 r78，确认只运行一个桥接实例。原生设备中心显示 USB 数据已确认、Wi-Fi 收到已认证请求、BLE 已认证并处于 USB 优先待命；设备仍为 0.2.264-ui。语音设置核对为 Mac 本地识别、Mac 默认麦克风和原有启用状态；检查后返回设备中心。

后台已成功下载公开 2025／2026 假期缓存，2027 未公布空表没有保存为有效安排。HKO 下载仍未取得成功缓存，离线节气回退继续可用。没有刷写固件或改动 TAB5 展示设置。快捷发送尚未向真实会话派发测试消息，实体发送验收仍需用户正常使用核对；旧录音中断根因仍未确认。

## English

The user accepted the original r76 BLE latency of about 4–5 seconds and deferred further tuning. This change separates explicit mutation authorization from the desktop IPC's 15-second availability hint. Expired hints trigger read-only revalidation before quick submission can clear the composer; direct operations still verify the live owner. Stored receipts remain authoritative and unknown delivery is never automatically retried. Synthetic tests send no real messages. This fixes a concrete cache edge, not every historical recording interruption or unconfirmed submission.

The new Mac annual-calendar reader restores caches before background requests, checks daily after success and retries after one hour on failure. It validates HKO solar-term tables and the existing Windows holiday-cn dataset, including year/date completeness, official-source fields and previous-December corrections. The third-party dataset is not an official API. Invalid, unpublished or failed updates preserve the last good cache. Solar terms have verified 2026–2028 offline fallbacks; the 2026 holiday fallback and unknown-year behavior remain. Home labels, monthly tables and official-workday reminders share the same schedules. Existing protocol fields are reused; no firmware update is required. Real holiday JSON and the unpublished 2027 placeholder were checked. Local direct HKO downloads failed during TLS connection, while the public web tables were accessible for verifying the 48 dates; local HKO download acceptance remains pending.

The historical fixed quota value came from a Windows migration note. Local Mac observations and the actual Swift calculator do not reproduce it. The user subsequently confirmed that the display is currently normal, closing the current symptom; the historical root cause remains unconfirmed. No history or quota algorithm was changed.

Debug and whitespace checks passed. Required `swift test` cannot run because this Command Line Tools installation lacks XCTest. An assertion adapter linked to actual Debug objects passed 94 test methods; it is not XCTest or physical acceptance. The final `mac-remaining-20261024-r78-offline` candidate passed Release, strict signing and 501 isolated application checks; its 11-file manifest includes license/notices and its Keychain helper is byte-identical to r76. The earlier r78 package was never launched. The initial launch check was blocked by a locked Mac. After the user replied, the UI became available: r76 quit normally and the final r78 candidate started as the only bridge instance. Native UI confirmed USB data, authenticated Wi-Fi requests and BLE authentication in USB-priority standby, with firmware 0.2.264-ui and Mac local recognition retained. Public 2025/2026 holiday caches were downloaded; direct HKO downloads remain unverified. No real message was sent, so physical quick-submit acceptance and historical recording interruption diagnosis remain pending. No push, tag or release was performed.


## 设备预览：保留小屏、增加 TAB5 镜像（2026-10-24 源码阶段）

用户后续明确收窄：ESP8266 原有预览已满足需求，保留绘制与控制，不再将其改为真实截图；共用入口改为“设备预览”，TAB5 单独增加真实画面镜像。前述“两块屏都需真实回传”的探索记录据此被本节替代。Mac／Windows 的单个启用设备直接打开预览；两个启用设备列出名称供选择，停用设备不进入选择。原菜单栏纯图标和精简设置分组保持。

两端桥接已新增独立只读 TAB5 窗口、租约接收器与严格 RGB565 解码，沿用已有配对、认证、session、时间和 nonce 去重。TAB5 在维护者源码的实际 LCD 双缓冲读取缩略图，串行相机切换／跳过 DMA 更新，不切页或修改展示设置。打开窗口后才请求；关闭、最小化／隐藏、停用、更换归属或超时会停止后续请求。帧只在内存中，损坏／未完整帧不显示，最近成功画面可以明确标记未更新。传输优先级和 PSRAM 限额见 PROTOCOL。

源码在 `artifacts/development/tab5-screen-preview-20261024/candidate/firmware`，这只是隔离源码目录；没有生成任何固件镜像。源码补丁 `docs/proposals/tab5-screen-preview.patch` 及摘要登记可逐文件核验，依赖此前日历重复标签与相机内存修复，原修复字节保持。补丁在临时干净目录完成 check、apply 及逐文件比较。C 编码／真实旋转采样的 UBSan 合成检查通过；5 个修改对象用归档 ESP-IDF 5.4.2／目标工具链独立编译通过，已有 UI 两处 unused 警告仍在。新增对象 text 总差约 5901 字节，不能用作链接后镜像尺寸结论；原 OTA 余量 17024 字节，正式合并构建仍需重新核对。

Mac Debug 隔离检查 901 项通过，包含完整认证二进制 RPC 回传、伪造证明／错误 session／过期时间拒绝、帧摘要、丢块／重试、断连、归属变化、租约、关闭和设备选择；最小／默认窗口深浅外观 4 张原生合成截图已检查。Windows 用临时校验官方摘要的 .NET 8 SDK 完成 Release 交叉编译（0 警告、0 错误）；真实生产接收器在本机可移植运行 29 项检查通过。已增加 Windows 认证二进制 RPC 回归用例并编译，完整用例仍须 Windows 执行；`--status-once` 已尝试，macOS 缺少 WindowsDesktop.App，退出 150，不能记为 Windows 运行通过。

当前实体仍为 `.267`，不会回传镜像帧；新窗口明确提示等待镜像版固件。真实显示、刷新速度、USB／Wi-Fi／BLE 链路及相机共用缓冲回归，全部保留为合并固件后的实体待验收。ESP8266 当前无硬件，本轮不虚记小屏实机回归。尚未生成／刷写固件，未改组件版本，未推送／发布。Mac Release、签名、部署记录随后追加。


设备预览部署收尾：`artifacts/mac-device-preview-20261024/AIBotBridge.app` 已通过 Release 901 项隔离检查、严格签名与原钥匙串 helper 身份／管道检查，正常退出旧实例后替换运行，只有一个桥接进程。完整 swift test 已按规则执行，仍因安装的 CLT 无 XCTest 失败；不记通过。原生设备中心确认 `.267`、USB 数据回执、认证 Wi-Fi、蓝牙 USB 优先待命均正常。检查窗口已关闭；未用实体画面验收镜像，也未伪造双硬件同时在线。小屏 Mac 渲染文件与上一已验收 bundle 冻结源码摘要完全一致，Windows MirrorForm 与仓库基线无差异。现有固件摘要未变，未生成／准备／刷写新固件，未修改版本或发布。下一次合并固件须验收 TAB5 实际镜像、关闭与断连、相机和语音并发、尺寸／方向与各通道占用。


## 继续现有 .267 验收：常用任务与窗口重开（2026-10-24）

用户要求现有固件可做的验收先完成，最后统一生成新固件。常用任务原生列表的原选项全为关闭；仅临时固定已指定的“TAB5 操作验收”，通过正式窗口保存，独立读取非密钥配置确认恰好一项（不记录真实任务 ID）。已请用户在 TAB5 检查对应项目／分组排首与任务可见，只浏览不发送；实体反馈及清除临时固定仍待完成。清理义务见 `artifacts/development/mac-pins-live-acceptance-20261024/PROGRESS.json`。

已将此前准备的生日／日程旧状态提示最小修复应用到工作源码。另在正常运行的 device-preview 包实测日程：原“全部事项”为空，仅编辑未保存的测试名称，点窗口关闭后确认“放弃修改”；重开时字段仍保留“1024”。据此新增“放弃草稿后重开仍带回”的可复现缺陷，不是已保存日程重现。测试后用“新建事项”清空草稿并关闭窗口，没有保存或新增事项。

调用方复用同一个 MacAgendaWindow，原 open 只刷新列表，未重置编辑器；Windows 对应窗口关闭后销毁。最小修复在 Mac 日程窗口从关闭状态重新打开时调用 beginNew 并清空旧提示；生日在成功重新读取时清空提示。窗口仍可见时重复打开及十秒刷新不清除正在编辑的草稿，存储、同步、提醒与协议保持。补丁／摘要 `docs/proposals/mac-profile-status-reset.*` 已更新，最终构建及原生复开验证进行中。

相机启动、日历补班文案去重和 TAB5 镜像保留合并固件后的实体复验；ESP8266、Windows、Intel 及未具备账户等场景按环境不足保留，不借此反复要求已通过的录音、发送与同步。仍未生成或刷写固件。


常用任务实体结果：用户明确确认“分组已排到最前，任务正常可见”。本轮 Mac 固定→TAB5 项目／分组排序与任务可见通过；没有发送消息。随后正式窗口只取消该唯一临时选择并保存，独立读取确认配置恢复空数组，清理义务关闭。不将单项排序扩大为跨全部项目、满 32 项或重启保持均已实测。

窗口修复已部署 `artifacts/mac-profile-reopen-20261024/AIBotBridge.app`，Release 及 901 项隔离检查、严格签名、原钥匙串 helper 身份／管道通过。完整 swift test 仍因 CLT 缺 XCTest 失败，未记通过。旧实例正常退出后只有一个新实例，原生确认 USB 数据回执、认证 Wi-Fi 与 BLE 数据正常。生日只将合成名称加入未保存列表，取消并重开后列表仍为空，旧“列表已更新”提示已清除；没有新增实际生日。日程空名称校验未写入，重开旧校验提示已清除；输入合成草稿后手动刷新保留原文，“继续编辑”也保留原文，再确认放弃后重开字段为空。两类旧提示与放弃草稿带回问题的本机实际复验通过，未变更日程／生日存储或设备协议，检查窗口已关闭。

下一项日程离线：原 Mac 全部事项为空，通过正式窗口新增唯一“离线验收1024”，明确关闭“设置时间”并保存，列表确认“无时间”；无提醒。已请用户确认 TAB5 可见后，临时仅 USB／拔线离线改名为“离线验收1024已改”／重连并恢复原模式，先不删除以供核对回传。离线保存、回传与最终测试数据清理仍待反馈，不能据本地保存写成通过。义务记录在 `artifacts/development/mac-agenda-offline-acceptance-20261024/PROGRESS.json`。现有 `.267` 摘要不变，没有生成、准备或刷写新固件。


日程离线验收收尾：用户确认离线保存成功，重连后只有一条改名事项。Mac 正式“全部事项”精确回读唯一“离线验收1024已改”及“无时间”；此前临时仅 USB／拔线离线改名／重连恢复原模式为测试安排与用户确认，不另宣称独立测量每段断线时刻。Mac 仅删除这条合成事项并恢复空列表，用户随后确认 TAB5 测试项消失且回到日常页。离线编辑保存、重连回传、无重复和删除清理的本轮场景通过，清理义务关闭；不扩大为同时双端冲突、所有提醒或设备离线重启均已验收。固件和展示设置未改。


## 农历生日与缺失闰月回退验收准备

日程离线事项已完全清理后，转入未覆盖的农历生日场景。香港天文台公开 2027 对照表核对 2027-02-06 为正月初一；设备当前数学头文件的原生 UBSan 合成检查同时确认普通农历 1/1 与所选闰正月不存在时均返回该日，没有编译或链接设备固件。来源：https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2027c.txt 。网页读取成功不代表本机 URLSession 的 HKO TLS 下载缺陷已恢复。

Mac 正式生日列表确认原为空。通过正式窗口保存两条合成生日“农历验收1024”（农历 1/1，非闰）及“闰月验收1024”（农历闰 1/1），均当天提醒；独立读取非密钥配置逐字段确认仅两条。只在未来日期查看，今天不会触发这两项生日提醒。界面自动化首次菜单索引点击未选中，后经实际截图坐标选项及保存字段核验纠正，不把未选中的尝试计为通过。已关闭准备窗口，实际 `.267` 指定日期显示、各项无重复及最终清理仍待用户反馈。清理义务见 `artifacts/development/mac-lunar-birthday-acceptance-20261024/PROGRESS.json`。


农历生日实体及清理收尾：用户确认 2027-02-06 农历日期正确，两条合成生日各出现一次。本轮普通农历日期／生日投影和所选闰月不存在时回退普通月的实体场景通过，不代表有真实闰月年份或生日提醒触发全部通过。Mac 正式窗口仅移除两条测试生日并保存，非密钥配置独立回读为空；用户随后确认两条都在 TAB5 消失且已回日常页，清理义务关闭。

同一阶段重跑既有 Swift URLSession 临时公开数据探针：HKO 2027 下载仍在约 519 ms 返回 NSURLErrorDomain -1200；2026 假期 HTTP 200／已公布并解析成功，2027 HTTP 200／占位表未公布。没有禁用证书验证、改变系统网络或向有效年度缓存写入占位空表；HKO 本机下载继续保留环境／联网待查，不用网页工具读取成功替代该结果。证据只保存公开地址、状态、固定错误码和大小。无产品代码、固件或展示设置变动。


## 日程到时提醒／延后实机验收准备

生日测试数据全部清理后，Mac 正式日程“全部事项”仍为空。通过原生日期控件准备并保存唯一“提醒验收1024”：2026-10-24 14:28（Asia/Shanghai）开始，准时提醒、不重复，结束时间晚于允许延后的十分钟窗口。独立读取非密钥日程文件验证唯一项、精确开始分钟和 lead/repeat，未记录 ID。请求用户保持连接，到时观察同名弹窗，仅点击一次“10 分钟后再提醒”；随后核对 Mac 的真实 snooze 回传并在十分钟内删除，避免留下延后提醒。此时到时／延后和清理尚未通过；保存状态与清理义务在 `artifacts/development/mac-agenda-reminder-acceptance-20261024/PROGRESS.json`。设备声音／显示设置不变。


## 配置迁移导出与预览（等待到时提醒期间）

通过正式窗口将当前非密钥配置导出到隔离验收目录，收到“备份已导出”。独立检查文件根结构仅 Version／CreatedAt／Settings／Birthdays，恰好七个允许设置和空生日列表，不含设备配对或凭据字段；不把具体偏好值写入本记录。正常“打开备份”完成真实文件重读，预览包含七个设置及生日列表共八个选项，默认均未勾选。直接点“恢复所选”后明确拒绝并提示“请至少选择一个有效恢复项目”，没有实际恢复或改动偏好。窗口已关闭，主窗口返回原“我的设备”。

本轮通过的是实际导出／文件校验／打开预览／未选项拒绝，不扩大为实际改值恢复、跨 Windows 往返或失败回滚均已验收。临时备份只保存在忽略目录 `artifacts/development/mac-configuration-live-acceptance-20261024/`，不加入仓库、日志或发布包；未改设备页面、轮播、数据选择或连接设置。


到时提醒／延后实体结果：用户明确确认“提醒按时弹出，延后操作正常”。Mac 非密钥存储独立核对唯一测试项，snoozeOccurrence 精确匹配本次 due，snoozeUntil 为未来且距离核对时刻不超过十分钟，done 未置位；据此提醒触发与延后状态回传的本轮场景通过。不以该结果推定音量、铃声或延后十分钟再次触发均已实测。随后正常 Mac 窗口只删除“提醒验收1024”，恢复空事项列表，早于下一次延后提醒；TAB5 消失确认仍待用户反馈。检查窗口已关闭。

用户在提醒测试后要求优化“提醒页面 UI”。该要求加入当前验收／源码修复批次，仍遵守最后统一生成固件的顺序。已询问指 TAB5 到时提醒弹窗、TAB5 日程页，还是 Mac 提醒管理；在明确目标前先完成测试数据清理和现有布局检查，不据模糊页面名改动其他模块。


## TAB5 日程提醒弹窗整理（源码及原生预览）

用户明确优化对象是 TAB5 到时弹出的“日程提醒”，并指出最初两个等宽按钮过宽。最终沿用现有配色与字体，增加背景遮罩拦截误触，使用居中卡片分别展示提醒标题、开始时间、最多两行的事项名称及状态。底部按钮靠右，“10 分钟后再提醒”为 280 像素，“知道了”为 168 像素，均保持 64 像素触控高度；临近结束时只隐藏延后按钮，确认按钮保留紧凑尺寸。保存中两按钮置为不可点，收到失败回执后恢复并突出原错误，继续沿用原持久化回执及延后条件。

修改位于保留相机内存、日历补班去重及设备镜像实现的隔离 TAB5 源码树；产品改动仅 `main/ui_agenda.h`，没有协议或桥接端改动。原生 LVGL 构建及既有日程完整场景检查通过，新增实际指针检查覆盖遮罩拦截后方新建、关闭不保存、延后、保存中重复点击抑制及失败后重试；32 字长标题、失败提示、即将结束、正常状态均已检查实际渲染和字形。单独交叉编译 `ui.c` 对象通过，不代表完整固件链接大小或实体界面通过。补丁及来源摘要为 `docs/proposals/tab5-reminder-popup-ui.*`，按 LF 约定在独立副本检查、应用及逐文件比较通过；其余产品文件与镜像源码基线保持一致。

用户另确认“提醒验收1024”已从 TAB5 消失，此次到时提醒／十分钟延后回传／双方删除清理收尾通过，清理义务关闭。不将本轮扩大为十分钟后再次触发、声音或所有重复规则均已实测。新弹窗仍待全部验收／源码修复结束后随统一固件安装复验；当前 `.267`、版本及设备展示设置保持，没有完整链接、生成、准备或刷写固件。

The user identifies the TAB5 due-time reminder popup and finds the initial equal-width buttons too wide. The final layout preserves the existing palette/fonts, adds a modal backdrop, separates the reminder heading/start time from a two-line item title and feedback, and right-aligns compact 280 px snooze / 168 px dismiss buttons with 64 px touch height. Near the end, snooze is hidden while dismiss remains compact. Pending persistence disables both actions; failure restores them and highlights the existing error, preserving acknowledgement and snooze semantics.

Only product file `main/ui_agenda.h` changes on top of the preserved camera-memory, calendar-label and screen-mirror source work. Native LVGL build and the existing agenda suite pass. Actual pointer checks cover backdrop interception, dismiss without a save, snooze, pending duplicate-input suppression and failure/retry; normal, 32-character title, storage failure and near-end renders/glyphs were inspected. The individual target `ui.c` object compiles, without establishing full linked-image fit or physical acceptance. The saved source patch applies and matches under its LF convention; all other product files match the mirror baseline. The user also confirms removal of the synthetic reminder on TAB5, closing this reminder/snooze/cleanup scenario. No second snoozed trigger, sound or all-recurrence pass is inferred. Popup physical verification waits for the final combined firmware; no firmware link, generation, preparation, flashing, version or display-preference change occurred.


## 配置迁移的实际恢复及自动备份恢复

继续现有 `.267` 验收时，通过正式配置迁移窗口打开仅含一条未来合成生日“迁移验收1024”、Settings 为空的本地备份。原生日列表为空，预览仅出现未勾选的生日项目。勾选并恢复后界面报告成功，独立读取确认精确单条生日；程序自动生成的 before-restore 文件保存了原空生日及七项配置。

随后通过同一正式窗口打开该自动备份，仅勾选生日列表恢复，独立读取确认生日再次为空。两次自动备份中的七项 Settings 完全一致，没有选择显示、轮播、自选股票等设置。原配置备份留在程序自身受限目录；含用户偏好的备份不加入仓库／日志／发布包。本轮通过实际选择恢复、自动备份和使用自动备份恢复原值；不将其写成跨 Windows 实机往返、故障注入或失败自动回滚通过。证据为忽略目录 `artifacts/development/mac-configuration-live-acceptance-20261024/RESTORE-PROGRESS.json`。桥接窗口在操作后返回我的设备并收起。

The normal configuration window restored one synthetic future birthday from a birthday-only archive with empty Settings. The original list was empty; the UI success and an independent file check confirm exactly the selected birthday. Its automatic before-restore backup contains the original empty list and seven allowed settings. Opening that actual backup and restoring only birthdays returns the list to empty; the settings in both automatic backups match exactly. User preference backups stay in the application's restricted local folder, outside source/log/release artifacts. This passes selected restoration, automatic backup and recovery through that backup, without claiming Windows hardware round trips or injected-failure rollback. No display/cycle/watchlist choice was selected.

## 日程并发冲突保留与离线重启验收

Mac 全部事项原为空，通过正式窗口新增无时间“冲突验收1024”。用户确认 TAB5 已离线改为“冲突验收1024设备改”且保持断线，Mac 窗口仍精确显示原名；之后只修改同一原项为“冲突验收1024电脑改”。用户重连恢复原模式后确认两条修改各保留一次，Mac 正式窗口与非密钥文件独立核对精确两条且均无时间。本轮实际并发定义修改的两份保留通过，没有把某一端修改静默覆盖另一端。随后正式窗口逐条确认删除这两项，Mac 列表恢复为空；设备消失确认并入下一项，不提前关闭清理义务。

接着仅新增无时间“重启验收1024”，请求用户先确认旧两条消失，再离线改名、正常关机开机、断线核对保留，最后重连恢复原模式核对单条。此项目前待反馈，不以之前单纯离线编辑通过替代 NVS／启动恢复实测。两项分别记录于 `mac-agenda-conflict-acceptance-20261024/PROGRESS.json` 与 `mac-agenda-restart-acceptance-20261024/PROGRESS.json`。无固件、识别来源、展示设置或原有数据变化。

For concurrency acceptance, one untimed synthetic item was edited offline on TAB5. The user confirmed isolation, and the Mac still showed its original name before independently editing the same original item. After reconnection, the user and the actual Mac UI/file verify exactly one version of each edit, both untimed. This passes the physical concurrent-definition fork scenario. The two synthetic versions were then explicitly deleted through the normal Mac UI; device cleanup confirmation remains pending with the next check. A new sole untimed restart fixture now awaits offline rename, normal power cycle, offline retention and reconnection feedback. The earlier offline-edit pass is not substituted for NVS/boot recovery. No firmware or display-setting changes occurred.


## 离线删除重启收尾与删除弹窗样式

用户确认“重启验收1024已改”在离线删除、正常关机开机后没有恢复，重连也未出现且已回日常页。Mac 非密钥日程文件独立核对为空。此前离线改名／重启回传与本轮离线删除／重启／重连场景均通过，清理义务关闭；不外推为突然断电、损坏存储或所有并发故障均已实测。

随后用户要求检查删除事项界面并与新提醒弹窗统一。隔离源码在已接受提醒布局之上，仅改 `ui_agenda.h` 产品文件：同样的 800×360 居中卡片、遮罩、边框、标题层级、两行事项名、分隔线；取消／删除各 168×64，靠右排列，删除标题与主按钮用现有红色。状态文字明确删除同步影响，失败时突出原保存错误，保存中沿用双按钮禁用与真实回执，不改删除／同步语义。

原生 LVGL 构建、日程完整场景及实际指针检查通过：取消不删、确认只删目标、背景不穿透、32 字名称完整两行、待回执不接受重复点击、存储失败保留事项并恢复按钮。正常／长名称／失败三种实际渲染已检查，字形覆盖通过。单独 `ui.c` 目标编译通过，相对提醒布局对象 text 增加 320 字节、data/BSS 不变；不是完整链接体积或实体通过。补丁及来源摘要为 `docs/proposals/tab5-delete-popup-ui.*`，LF 约定的独立检查／应用／比较通过；其余产品源码与提醒布局基线一致。未生成、准备或刷写固件。

The user confirms offline deletion survives a normal power cycle and reconnection without resurrection; Mac's actual non-secret agenda file is empty. Both restart scenarios and cleanup pass, without inferring sudden-power-loss or corrupt-storage acceptance. The requested TAB5 deletion popup now matches the accepted reminder layout in the isolated source tree: modal backdrop, centered card, two-line title and compact right-aligned cancel/delete controls. Existing persistence and synchronization semantics remain. Native LVGL/agenda and actual pointer checks pass, including background interception, cancellation, exact-target confirmation, long-title fit, pending duplicate suppression and failure recovery. Normal/long/failure renders were inspected. The individual target ui.c compiles (+320 text bytes, unchanged data/BSS relative to the reminder object); no full image fit or physical pass is claimed. The saved patch applies and compares cleanly. No firmware generation/preparation/flash occurred.


## 通信测速实测发现的 Mac 兼容／调度缺陷

现有 `.267` 上通过正式连接升级窗口选择“通信传输”，确认“不写入 TF 卡”后开始一次。旧 profile-reopen 包报告设备身份不匹配，停止未确认；没有重配或重复启动。两端核对发现设备 `tab5_benchmark` 回包精确带 deviceId/type/result，但未带 version，Windows 对同一回包不要求版本；Mac 共用 management 则把缺失版本错误归入 identity。最小修复仅为精确类型／设备／有界结果的这一个旧回包接受版本缺省，显式非法版本及其余管理回包仍拒绝。

第一个兼容修复 Mac 包完成 Release、914 项隔离检查、签名与未变更 helper 检查，正常替换运行后复测出现 unavailable／停止未确认。进一步核对发现测速开始将 benchmarkMode 置位，普通 USB 调度随后调用的 frameLocked 却拒绝该模式，导致周期 step 关闭串口，排队的开始／停止指令失去 wire。保留这次真实失败，不记为测速通过。

后续源码保留测速期间的普通状态心跳与认证 RPC，只禁止状态夹带图片和独立资源批次；配置／OTA／照片／语音互斥不放开。BLE 资源轮也遵守同一配置忙门控，资源构建碰到竞态 busy 时仅退让而不重置已认证连接，诊断在帧实际构建后才开始。新增实际服务测试覆盖两种测速模式的状态帧、资源暂停、认证测速 RPC、互斥及结束恢复。当前完整本机 XCTest 仍缺 CLT 模块；最终 Release／隔离检查／实体复测另外登记，不能以第一次修复的 914 项结果代替第二次补充后的验证。

The first real RAM-transport attempt fails on a legacy benchmark reply that omits version; Windows already accepts that established reply. Mac now allows the absent version only for exact type/device plus a bounded result, preserving host authentication and rejecting explicit malformed versions and all other unversioned management replies. The signed 914-check compatibility build then exposes a second real failure: enabling benchmark mode makes the ordinary status getter reject, causing the USB scheduler to close its wire before queued management work. Neither attempt is counted as a physical benchmark pass. The follow-up preserves normal heartbeat/authenticated RPC while pausing image resources, retaining existing exclusive-operation gates, and deferring BLE resource turns without disconnecting on temporary busy. New service checks cover both benchmark modes and resource recovery. Final validation/deployment/physical results remain separate from the earlier build evidence.


测速完整 USB 复测：922 项隔离检查的 status 修复包正常替换后，USB 实际三轮上传和三轮下载各 256 KiB 全部完成，六个错误状态均为 0；设备逐段 SHA 与数据模式核验属于其生产测速路径，不是桌面模拟。原生结果汇总上传 113.7 KiB/s、下载 103.6 KiB/s，只涵盖本轮实际测得的 USB，不外推 Wi-Fi／BLE。随后补测单次停止，操作区显示已停止，结果区仍为“测速中”并保留部分数据，确认 UI 终态不一致；不以停止按钮变为不可点来推定设备终态。

后续新增有界停止确认 helper：只发送一次 cancel（图库使用 gallery_cancel），随后只读 status 查询至设备返回非 running 结果；使用单调时钟 15 秒观察预算，单次 USB 调用仍受既有超时约束。清理由不继承调用方取消的任务完成，收到真实终态才更新结果区并释放测速互斥；过晚取消保留实际 complete，失败／坏包／超时明确未确认，不伪造 cancelled。实际生产 helper 在独立原生异步程序通过七项检查，包括取消后的清理继续执行、普通／图库动作、坏结果／超长结果、截止时仍运行和已经完成。新增 XCTest 方法随完整测试尝试保留，但本机仍缺 XCTest，不写为 XCTest 通过。最终包和实体取消复验待后续回执。

最终 `mac-benchmark-stop-20261024` 包已正常替换为唯一运行实例。实体取消时先显示“正在停止测速…”，设备结束后结果区与操作区均显示“测速已停止”；开始按钮恢复、停止按钮禁用。随后只启动一次完整复验，USB 三轮上传及三轮下载各 256 KiB 完成，错误状态均为 0，平均上传 116.1 KiB/s、下载 102.6 KiB/s。设备各轮耗时依次为上传／下载 2.15／2.32、2.28／2.40、2.18／2.76 秒。结果区“测速完成”与操作区“测速结束”一致。关闭工具窗口后原生界面确认 `.267`、USB 数据、认证 Wi-Fi 与蓝牙 USB 优先待命均恢复，再关闭设备中心；桥接留在后台。本轮只验证 USB RAM 传输及取消，不写 TF、不改变网络／配对／展示设置，不将结果外推 Wi-Fi／BLE 测速。

天文台联网补充：本机对主站、官方备用站和 `data.weather.gov.hk` 官方 CSV 地址均未完成 TLS 建连。只记录 DNS 结果属于虚拟地址段的分类，不保存具体地址或网络配置。用户确认正在使用代理／VPN；尚无停用后的对照结果，不能断言代理就是唯一原因。没有更改代理、VPN、DNS 或证书校验。天文台数据用于年度 24 节气日期；农历／生日由本地计算，放假／补班来自独立年度假期数据，已核对的 2026–2028 离线节气表继续可用。

The final signed Mac build is the sole running instance. Physical cancellation first reports stopping, then both the result and action areas confirm the actual stopped state. A subsequent single full USB run completes six 256 KiB samples with zero errors, averaging 116.1 KiB/s upload and 102.6 KiB/s download. Normal completion messages agree. Native UI then confirms unchanged `.267`, USB data, authenticated Wi-Fi and BLE USB-priority standby; both inspection windows are closed and the bridge remains in the background. This covers USB RAM transport and cancellation only, without TF writes, firmware generation or network/pairing/display changes. HKO native TLS still fails across official endpoints. The user confirms an active proxy/VPN; without a controlled comparison its causal role remains unproven. Verified offline solar terms remain available, with network settings and certificate validation unchanged.

## 国内节气数据源替换

用户要求替换无法访问的香港天文台源，并明确倾向国内可靠来源。最初评估的 NAOJ 实现已在打包／部署前撤回，只有忽略目录保留研究回执；当前产品不请求它，也不以其作联网回退。

两端现改为中国科学院紫金山天文台[官方历书查询索引](https://pmo.cas.cn/xwdt2019/kpdt2019/202203/t20220309_6386774.html)及其年度日历 PDF。只接受官方两种主机的 HTTPS 链接，限制索引 512 KiB、PDF 4 MiB／16 页和提取文字 128 KiB；按封面目标年份、编制机构、十二个月及各两项节气校验。翌年一二月附表不能冒充完整翌年。合成错误输入覆盖缺项、重复、错年、错月、坏日期／时间、错误机构、外站／明文链接、冲突索引及 HTML 伪装 PDF；失败不写入缓存。新公开日期转换为既有 `terms-YEAR.txt` 语法，旧缓存／旧版本读取兼容，设备字段和固件不改。

本机实际 URLSession 下载官方索引和 2025／2026 PDF，生产 Swift 解析器取得每年 24 项，缓存后重开相同。2026 的 24 个日期与原核对结果全部一致。Windows 通过 PdfPig 0.1.16 读取相同 PDF 的 48 个日期，与 Mac 系统 PDFKit 独立提取结果完全一致。Mac 生产校验新增 24 项；Windows 生产自检也覆盖目录、年月、24 节气完整性、翌年不完整附表及旧缓存保留。库来自官方 NuGet 包，原 Apache-2.0 及 PDFBox／FontBox、Adobe 组件声明完整保留并随 Windows 输出复制。只提取公开日期事实，不打包来源 PDF／网页、图片或对方源码；来源归属与依赖记录已更新。

官方索引本次最新列到 2026，2027–2029 未列出，不猜测文件名、不把缺失当作下载成功。原 2026–2028 离线表继续可用；未来索引新增年度后才按同一校验路径读取。这不改变未公布放假安排的处理。没有调整用户代理／VPN／DNS，也没有关闭证书验证。

必需检查：Windows Release 构建零警告、零错误；`--status-once` 已尝试，在 macOS 因缺 WindowsDesktop runtime 无法运行。Mac `swift test` 已尝试，当前 CLT 缺 XCTest，不能记为通过；Release 构建通过，签名包 946 项隔离检查通过。独立原生生产模块 24 项检查、真实两年下载／离线恢复和 C# 真实文档解析分别有回执。

`artifacts/mac-calendar-source-20261024/AIBotBridge.app` 已正常替换为唯一运行实例，严格签名及原 helper 字节不变核验通过。运行包实际产生带 PMO 来源标记的 2025／2026 缓存，逐字节等于已验证生产探针结果。原生完整窗口状态确认 `.267`、USB 数据、认证 Wi-Fi 与 BLE USB 优先待命均恢复；随后关闭设备中心，桥接留后台。未生成新固件、刷写、改版本、推送或发布。来源补丁／摘要为 `docs/proposals/calendar-domestic-source.patch` 和 `.json`，详细公共数据回执在忽略目录 `artifacts/development/calendar-source-20261024/`。

Solar-term updates now use the Chinese Academy of Sciences Purple Mountain Observatory official index and annual PDF calendars. The evaluated NAOJ implementation was withdrawn before packaging/deployment and is not a product fallback. Independent Swift/C# readers validate the publisher/year, twelve months and 24 complete ordered terms, reject partial following-year appendices and malformed/external sources, and preserve the existing cache grammar and last good data. Native URLSession downloads for 2025/2026 and offline reload pass; all 48 dates independently extracted by system PDFKit and PdfPig agree, including all 24 previously verified 2026 dates. The official index currently ends at 2026; absent later years retain verified offline data rather than being invented. Source documents are not distributed; public facts, attribution and the complete PDF-library license/component notices are separately recorded. Required Windows Release passes with zero warnings/errors, while status-once cannot run without WindowsDesktop on macOS. Mac Release and 946 isolated checks pass; full XCTest remains unavailable in the installed CLT. The signed Mac bundle is deployed as the sole instance, with actual PMO-marked runtime caches matching validated output and native USB/Wi-Fi/BLE connection restored. The inspection window is closed. No firmware generation, flash, version bump, publication or network-setting change occurred.

The signed 922-check heartbeat build completes three real USB uploads and three downloads of 256 KiB each, all six error statuses zero; average UI rates are 113.7/103.6 KiB/s. This covers only the actually measured USB channel. A subsequent single cancel exposes stale UI: the footer reports stopped while the result still says measuring. The follow-up issues one stop command, performs bounded read-only terminal-status polling and updates the result/releases exclusivity only after the device stops running. Cleanup continues despite caller cancellation; late cancellation preserves actual completion and unconfirmed/error/timeout states are not fabricated successes. Seven standalone native checks execute the actual production async helper; XCTest remains unavailable. Final packaging and physical cancellation verification are recorded separately.


## 现有 .267：图库写卡及重复提醒补验

源码核对图库测速使用独立 cache/gallery-speed 根目录，不替换原图库。当前正式 Mac 连接窗口第一轮 USB 8/8 样本完成，96.2 KiB/s，接收 18503 ms、写卡 1971 ms、错误 0。中途停止另测：已有样本后只点一次停止，期间两键禁用并显示正在停止；最终两处均显示已停止，保留 5/8、错误 0。再次只启动一次恢复测试，8/8 完成，76.2 KiB/s，接收 22833 ms、写卡 1949 ms、错误 0。两个速率不作为稳定性能或无线／纯 Flash 速度结论。退出后完整原生状态确认 .267、USB 数据、认证 Wi-Fi 和 BLE USB 优先待命；检查窗口关闭。证据为 artifacts/development/mac-gallery-speed-acceptance-20261024/PROGRESS.json。

正常 Mac 界面只新增“重复验收1024”，2026-10-24 15:39–15:41（Asia/Shanghai）、每天重复、准时提醒；字段已精确回读。“知道了”源码只关闭提示，不发送 complete，不能误称完成事项。用户提前回复 ok，先登记初步关闭正常；15:41 后正常 Mac 全部事项列表显示唯一 2026-10-25 15:39。TAB5 最终顺延确认与清理仍等明确回执，保留唯一测试项及清理义务，窗口已关闭。证据为 artifacts/development/mac-agenda-daily-acceptance-20261024/PROGRESS.json。

五项待合并修复的 15 个固件产品文件最终摘要均一致；只准备未分配版本的分类说明草稿。未启动完整固件构建、生成候选、改 latest、刷机或改展示设置。仅 Wi-Fi／BLE 通信测速仍需相应模式实测。

Current .267 passes the full eight-sample USB gallery/card test, in-progress stop with consistent terminal UI and a subsequent complete eight-sample recovery run, all with zero errors. The isolated benchmark root preserves normal gallery files; native connection status is restored and windows are closed. A single daily reminder fixture is retained pending explicit final TAB5 next-day confirmation and cleanup. The Mac independently shows October 25 15:39 after the current occurrence ends. Dismissal is not mislabeled as completion. All fifteen product-file hashes match the five pending source changes; only an unversioned notes draft is prepared. No full firmware build, image generation or flash occurs. Wi-Fi-only/BLE-only RAM transport acceptance remains open.


## 现有 .267：无线通信与 USB 管理通道共存

用户明确确认每天重复事项已顺延且只有一条，Mac 已精确核对并删除唯一测试项，用户后续确认设备清理正常。仅 Wi-Fi 内存测速六轮各 256 KiB、错误均 0，平均上传／下载 319.2／153.3 KiB/s；USB 只作控制，未写 TF。

用户选择仅蓝牙且保持 USB 后，首次真实测速返回“当前模式没有可测速的连接”。原生窗口同时显示 USB 数据已确认、BLE 错误处于 USB 优先待命。源码确认 Mac 仅凭新鲜 USB 回执就暂停蓝牙，忽略既有 active 字段；Windows 已按设备报告的 USB 活动状态判断。Mac 现统一这个行为，USB 管理能力仍由原 usbActive 判定，BLE 状态与语音后续轮询仅在设备明确选用 USB 且回执新鲜时退让。设备身份不匹配不覆盖，缺失／坏活动字段、过期／倒退时间、认证拒绝及断线均不能保留 USB 显示归属，无协议或固件变化。

Mac 必需 swift test 已尝试但 CLT 无 XCTest；Release、971 项隔离检查及实际生产归属函数 14 项边界检查通过。签名包 artifacts/mac-ble-active-transport-20261024/AIBotBridge.app 已正常替换唯一实例，原图标与钥匙串 helper 字节不变。原生状态确认 .267、USB 与 BLE 数据回执同时恢复，不再错误待命。当前真实 BLE 通信传输测速进行中，不能提前记通过；用户原连接模式尚待恢复。范围补丁为 docs/proposals/mac-ble-active-transport.patch 及摘要。失败、修复与后续实际结果分别保留，未生成固件。

The daily recurrence and fixture cleanup are explicitly confirmed. Wi-Fi-only RAM benchmarking completes all six 256 KiB samples with zero errors, averaging 319.2/153.3 KiB/s. The first BLE-only attempt reports unavailable because the Mac incorrectly suppresses BLE on any fresh USB control acknowledgement. Both BLE scheduling guards now defer only when the paired device actually reports USB as its active transport, matching Windows' existing contract. USB management remains usable. Wrong identities cannot change ownership; missing/invalid active information, stale/future acknowledgements, rejection and disconnect do not preserve ownership. Release, 971 isolated checks and 14 production-helper boundary checks pass; full XCTest is unavailable in the installed CLT. The signed replacement runs as the sole instance with USB and BLE data confirmed together. Physical BLE benchmarking is in progress and original-mode restoration remains outstanding. No firmware or wire-format change.

## 仅 BLE 通信测速：真实失败、Mac 修复与最终通过

仅 BLE 首次返回不可用的问题由前述 USB 回执归属误判引起。修复后实际传输恢复，但 Mac 通用 RPC 仍逐片读／游标确认：第一轮上传／下载 256 KiB 为 108.62／46.71 秒、错误 0，第二轮上传 256 KiB 为 109.03 秒、错误 0，随后下载 192 KiB 在 35.65 秒返回 -2，达到既有通道总计五分钟时限。这是未全部完成，不能算通过。

Mac 随后接入设备早已支持的命令 3 RPC 通知窗口，合法 rpcNotifyWindow 最多 32，缺失／无效只沿用有效的旧八片额度；仅成功系统订阅后才启用。每批精确核对 ID、游标、总长和片数，完整结束后再授权下一批。整个加密请求、配对代次、75 秒请求界限及四秒窗口界限保持；失败不重放操作，不扩大设备测速时限。语音仍用原八片窗口和既有调度。大样本检查最初误用 32 KiB 状态帧加密上限，修正为正式 bulk RPC 上限后，1145 项检查通过；失败回执单独保留。

首个通知包实测仍走旧路径，手动只停一次，设备终态为已停止，保留 240 KiB／118.33 秒，不算完整样本。原生诊断记录一个约 50 KB 请求仍有 103 读／103 写，语音订阅已确认；该包没有 RPC 订阅错误记录，不能猜测具体错误码。后续为 RPC 增加仅数字／枚举的订阅诊断，并沿用语音已有的受限恢复：仅 ATT 授权不足且只读 CCCD 精确为 1 时，允许一次标准 CoreBluetooth 协调，仍要求成功回调，其他错误／重复失败保持逐片读取。原生最终明确显示 RPC 32 片、语音 8 片均已成功订阅；没有证据说明首次 RPC 拒绝的具体原因，或最终连接是否实际执行过该恢复分支。

最终唯一运行包为 `artifacts/mac-ble-rpc-recovery-20261024/AIBotBridge.app`，TAB5 仍为 `.267`。完整 BLE 三轮上传／下载各 256 KiB：48.67／45.58、39.99／44.50、40.50／44.86 秒，六个错误状态均为 0；正式汇总上传 5.9、下载 5.7 KiB/s，结果区完成、操作区结束，开始按钮恢复、停止禁用。USB 仅作控制／读取结果，不写 TF，不修改配网、配对或展示设置。正常数据回执恢复后已关闭检查窗口；等待用户将临时仅蓝牙恢复为测试前原连接模式。无线完整证据与早先不可用、超时及取消均保留在 `artifacts/development/mac-transport-live-acceptance-20261024/WIRELESS-PROGRESS.json`。

Mac 必需 swift test 已尝试，CLT 缺 XCTest，不能记通过；最终 Release、1158 项隔离检查、严格签名及原钥匙串 helper／图标字节一致通过。三个范围补丁依次应用后，十个最终文件与当前源码摘要全部一致，`git diff --check` 无输出。补丁为 `docs/proposals/mac-ble-active-transport.patch`、`mac-ble-rpc-window.patch`、`mac-ble-rpc-recovery.patch` 及各自摘要。没有 Windows／固件线上字段变化，没有生成新固件、改版本、刷写或发布。

The first BLE-only unavailable result comes from Mac incorrectly suppressing BLE on fresh USB management ACKs. Correct device-reported USB ownership restores transmission, but the old per-fragment RPC path exceeds the unchanged 300-second channel deadline after three complete zero-error samples and a partial second download with -2. This remains a failed full run. Mac then adds existing bounded type-3 RPC notification grants, exact ID/cursor/total/credit checks, whole encrypted-request verification, pairing-generation checks and unchanged deadlines, without operation replay or voice-window/pacing changes. An initial test fixture incorrectly used the smaller state encryption limit; it was corrected to the real bulk RPC limit before the 1145-check package. The first notification package still uses legacy reads and is stopped once, preserving a partial result and native 103-read/103-write metadata rather than claiming success.

The final package adds bounded subscription/error metadata and the existing voice-style one-shot readback reconciliation only for exact ATT authorization failure plus enabled CCCD; successful CoreBluetooth subscription is still required. Other failures retain legacy reads. Native UI confirms RPC 32-fragment and voice eight-fragment subscriptions. The earlier RPC rejection was not instrumented, so neither its exact error nor actual use of reconciliation on the final connection is inferred. All six final BLE 256 KiB samples complete with zero errors at UI averages 5.9/5.7 KiB/s; completion and released controls agree. USB is management-only and TF is untouched. Normal connection resumes and inspection windows close. Original connection-mode restoration awaits the user. Release, 1158 isolated checks, strict signing, unchanged helper/icon bytes, exact application of the three scoped patches and diff checks pass; full XCTest remains unavailable. Firmware stays .267, with no image generation, version bump, flash or publication.


## 新增网络管理布局与日常 Wi-Fi 现场

用户已明确恢复测速前原连接模式且连接正常。随后启动 .268 合并构建准备，源文件 15 项一致；在生成应用镜像前，用户新增“网络与连接诊断”整体布局重设计并要求同步切换已配对电脑的功能说明。构建已按进程树中止，没有 .268 应用 BIN、sidecar、latest 替换或版本登记变更，.267 固定镜像仍保留。

新增第六项源码为 artifacts/development/tab5-network-redesign-20261024/candidate。主视图“电脑与网络”左右等宽分组，各自显示当前对象、选择框及紧凑操作，主按钮统一右对齐；诊断详情移入独立视图，说明其电脑桥接状态对应 Wi-Fi 通信。入口文案补充切换已配对电脑，配对／添加说明使用电脑桥接的通用名称。管理、诊断及备注窗口覆盖底部导航，网络删除确认使用与日程删除相同的 168×64 紧凑按钮和警示色，并阻挡背景操作。身份、归属、USB 切换保护、网络持久化及协议字段不变。

原生实际 LVGL 预览通过电脑切换／USB 接管与拒绝、离线保留目标、中文备注／取消／保存失败、Codex 草稿隔离、十个保存网络、删除取消／失败／当前／最后一项／忙碌保护；真实指针核对入口、诊断进出和删除弹窗拦截背景，控件对齐与父区域边界正常。ESP32-P4 目标 UI 对象编译通过，非完整固件链接或实体显示验收。补丁 docs/proposals/tab5-network-layout.patch 及摘要精确应用通过；合并 15 产品文件中其余 14 项保持原摘要。分类说明仍为未分配 sidecar 的草稿。

同时用户报告 .267“电脑桥接响应异常／暂不可达”。本机确认桥接单实例、USB 正常，Wi-Fi 已关联，目标地址与 Mac 局域网地址相同，回程使用 en0，防火墙关闭；本机同端口未认证请求正常返回 401。设备真实 USB 诊断中状态请求由 1913 增至 1920，打开错误及 socket errno 都为 0，HTTP 出现 -1，有效状态数停在 1719；正常退出重启同一签名包后，请求 1964、有效数仍 1719。Mac 显示收到认证请求不等于设备已收到／校验回复，因此未记通信恢复。不得据此让用户重配或断言代理为根因。

已从实际 Mac 生产发送函数提取原样原生慢读探针，9 个 128 B／32 KiB／128 KiB 用例通过；只说明未在本机慢读复现通用截断，不替代 ESP 实体链路。当前根因尚未建立，等待用户保持 USB、临时仅 Wi-Fi 的对照并恢复原模式。未改 Mac HTTP 发送、配网、配对、防火墙或代理。检查窗口已关闭，运行包仍 mac-ble-rpc-recovery-20261024，重启后单实例 PID 99772。

The user restored the original link mode successfully, then added the network/computer layout redesign while .268 build configuration was in progress. That build was stopped before generating an application image; no sidecar, latest replacement, registry bump or flash occurred. The sixth scoped patch provides aligned computer/Wi-Fi groups, right-aligned primary actions, a separate diagnostic sheet, updated trusted-computer selection copy, full body coverage and consistent compact deletion confirmation with background input blocking. Existing ownership, USB protection, persistence and protocol rules remain. Native LVGL workflows, pointer navigation/modal blocking, bounds and target UI object compilation pass; physical display acceptance remains pending. All other fourteen previously verified product files retain their hashes.

Separately, ordinary Wi-Fi state polling on .267 fails to update the device's valid-status counter despite association, correct target/local route, a running Mac bridge and healthy USB. ESP open/socket errors are zero and HTTP can be -1; valid count stays at 1719 as attempts increase, including after a normal restart of the same signed Mac bundle. Authenticated requests observed on Mac do not prove delivery/decryption on TAB5. No pairing or proxy cause is established. Nine slow-reader cases using unchanged production send functions pass locally but cannot replace ESP-path evidence. A user Wi-Fi-only comparison and mode restoration are pending. Inspection windows are closed. No speculative HTTP/network/security setting change is made; resume the combined firmware build after this new issue is resolved.

## 常规 Wi-Fi 回包异常：整机重启恢复，根因未定

现场 `.267` 关联正常但有效状态计数停在 1719；仅 Wi-Fi 同样异常。Mac 增加仅内存中的请求／响应／发送计数、字节和耗时，显示在深层设备诊断，不记录内容、地址或凭据。Debug 临时诊断确认五次 HTTP 200 均约 5 ms 生成、发送本机栈成功；之后正常退出并改运行同身份签名的 Release 包 `artifacts/mac-wifi-status-diagnostics-20261024/AIBotBridge.app`。Release 构建通过（511.27 s），1158 隔离检查、固定 helper 身份和严格签名通过；完整 XCTest 缺模块未通过。复用已验证资源/helper 打包，避免重复编译。

内核 nettop 脱敏统计 42 条相关连接，其中 41 条有重传、最大重传 34560 字节；少量 ICMP 样本的小包丢失 50%、1400 字节两包无回复，样本不足以确认 MTU 或射频原因。无权限读取的数据包抓取没有进行，arp/netstat 没有给出可用条目，未据此推断地址冲突。只在忽略的开发证据中保存无地址的标量。

同包重启 Mac、仅 Wi-Fi、TAB5 Wi-Fi 重连、完全退出代理／VPN均未恢复。用户已恢复原模式及网络工具。TAB5 正常关开机后用户反馈正常，USB 实读运行 41429 ms、请求 14、有效 14、HTTP 200。只能确认整机重启恢复，不能声称驱动或链路根因已修复。等待一次短 BLE 传输后恢复 Wi-Fi 的对照，以检查先前测速期间的暂停／恢复是否可触发。

另修正可确认的文案问题：原代码请求开始即把 HTTP 状态清零，容易把正在等待误显示为“暂不可达”。新增仅 UI 使用的原子等待标记，保留原数值诊断协议；已完成的 HTTP -1 显示为“未收到有效响应，正在重试”。生产函数原生状态检查和真实 ESP32-P4 wifi_link.c 对象编译通过，未生成镜像。补丁与摘要为 `docs/proposals/mac-wifi-status-diagnostics.*`、`docs/proposals/tab5-network-status-copy.*`。

English: ordinary Wi-Fi status replies stalled at valid-count 1719 despite association and authenticated Mac requests. Ephemeral scalar-only host diagnostics show prompt HTTP 200 generation and local-stack send success. A signed Release bundle passes 1158 isolated checks and identity/signature verification; full XCTest is unavailable. Kernel counters show substantial TCP retransmission; small ICMP samples are insufficient to establish MTU/RF cause. Wi-Fi-only, Mac restart, device Wi-Fi reconnect and exiting the network tool do not recover the fault. A normal device power cycle restores 14/14 valid replies, but root cause is not established. A short BLE-transfer/resume comparison is pending. The misleading in-flight unreachable wording is independently fixed and checked in firmware source, with no wire change, generated firmware or flashing.

相关上游线索仅供后续定位：[ESP-Hosted #197](https://github.com/espressif/esp-hosted-mcu/issues/197) 有“关联仍在、数据通路停滞、整机重启恢复”的报告，[#184](https://github.com/espressif/esp-hosted-mcu/issues/184) 则包含最终与 AP 互通相关的不同复现。其组件版本、硬件和负载与本次不同，不能据此把本次确认为同一缺陷；当前未据此升级协处理器、改变无线协议或加入自动整机重启。

Upstream research only: the linked Espressif issues describe superficially similar stalls, with differing hardware, versions, workloads and AP findings. They are investigation leads, not a diagnosis of this device. No speculative coprocessor update, protocol-mode restriction or automatic whole-device restart is introduced.

### 短 BLE／取消／Wi-Fi 恢复对照完成

用户临时选择仅蓝牙后，Mac 启动不写 TF 的通信测试。约一分钟点停止并取得终态：BLE 上传首轮 262144 字节、42400 ms、错误 0；下载部分 147456 字节、26570 ms、错误 0，最终“测速已停止”。这不是六轮完整测速通过。停止后即使仍为仅蓝牙，Wi-Fi 回包有效数从 304 增至 319，HTTP 200；用户随后明确恢复原模式、连接和提示正常，Mac 再读到 368／367、HTTP 200、运行 973539 ms。所有窗口已收起，未复现持续中断。

短测后的 Mac 计数显示请求／响应／本机栈发送均 389、发送错误 0、最近生成 38 ms，但历史最大生成为 4329 ms，已超过固件单次 3000 ms 头部等待时限。这是新观察，不应继续概括为所有响应均及时，也不能把它等同于先前重启前的持续丢包根因。当前未增加超时或修改关闭／TCP 参数；尚需分离服务锁等待与生成阶段才可进一步归因。

16 个产品文件、七项固件补丁已核对并合入冻结完整源；更新说明仍是 701 字节的无版本草稿，不是 sidecar。按用户“修复完后再生成”的约束，已询问是否将未定位的原故障列为观察项再构建，或继续专项排查；处置选择待回复，无镜像生成或刷写。

English: the short BLE test stops cleanly after one full256-KiB upload and a partial144-KiB download, both error0. Wi-Fi valid replies continue, the user restores the original mode and confirms normal prompts, and a subsequent device sample reads368 attempts/367 valid HTTP200. The persistent outage is not reproduced. A host-generation maximum of4329ms exceeds the device3000ms header deadline; its wait stage is not isolated and it is not declared the prior outage's cause. Seven patches/16 product files are frozen and checked, with a701-byte unversioned notes draft only. Await the user's disposition of the unlocated fault before image generation.

## 最新检查点：Wi-Fi 观察与音乐封面

用户已明确把未再复现的 Wi-Fi 持续中断列为后续观察，授权继续统一生成固件。随后新增 Apple Music 封面缺失、倒置及上一首封面残留，故先补音乐修复。Mac 已有曲目绑定诊断版、方向及后备读取源码和 1191 项隔离检查；Release 完整打包和实体封面匹配／方向验收仍进行中。TAB5 保持 `.267`，尚未生成统一镜像。最新状态以 [当前验收记录](MAC-CURRENT-ACCEPTANCE-20261024.md) 为准；旧历史待办不覆盖新结果。

English: the user explicitly moves the unreproduced Wi-Fi outage to follow-up observation and authorizes unified firmware generation. Newly reported Music artwork absence, inversion and previous-track retention are handled first. Track-bound Debug code and1191 isolated checks are ready; full Release packaging and physical artwork acceptance remain pending. TAB5 stays on.267 and no combined image has been generated.

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


## .268预览卡死定位与正常重启恢复

用户确认预览失败后TAB5时钟停止／触摸无响应。Mac已落盘的8份加密sealed文件共999972字节在仓库外逐字节校验备份；没有读取Keychain、明文、照片或密码。设备RAM中尚未同步内容无法备份，已在重启前说明。用户按正常电源键关开机后明确确认时钟、触摸和USB恢复；没有进入下载模式、恢复出厂、重新配对或改网。恢复后原生数字采样USB确认1／中断0、uptime95710ms、UI页0；resetReason7对应SDK的其他watchdog复位，保留运行记录无有效标记，不能据此宣称取得冻结堆栈或唯一复位触发点。预览请求／发布／接收均0，未再打开危险采集路径。

实际.268调用`esp_cache_msync`时同时传入M2C和UNALIGNED。构建所用ESP-IDF5.4.2已启用chunked cache mutex，其函数在获得全局缓存锁后才拒绝该组合，错误返回没有释放锁。后续采集会在持有LVGL显示锁时等待同一缓存锁，解释无画面并继而整机卡住。使用实际SDK函数、生产预览函数及像素采样函数的UBSan模型，旧路径确定复现首次错误后锁未释放／第二次阻塞；只保留合法M2C参数后128次旋转采集及相机front、对齐拒绝、DMA等待、忙碌门控和非法参数检查通过。HAL与地址／锁为原生模型替身，不能替代新固件实机镜像验收。实际P4显示驱动对象编译通过，不改SDK或协议、不链接新镜像。

单行补丁和来源摘要保存在`docs/proposals/tab5-preview-cache-lock.patch`及`-source.json`；证据在`artifacts/development/tab5-preview-cache-lock-20261024/`。初次编辑发现后续源码副本的components是指向冻结目录的符号链接；已在任何固件编译／链接前恢复冻结文件并核对原SHA，改用私有组件副本，再只修改副本。原.268 BIN始终未变，SHA仍为`d4d021daba4319019a661a6056f2bc81c40e9f91da2f2988b410f1b11139aa10`。相机、USBdelta、媒体点击和预览缓存锁四份补丁在独立冻结.268文件副本顺序dry-run／应用／8文件比较通过。

Mac加入兼容保护：已知.268及更旧／未知固件不生成镜像命令、不保留画面，并说明待统一固件更新；晚到帧同样拒绝。小屏原绘制预览保持。6项门控检查及真实加密dispatcher旧版本拒绝检查加入后Debug共1370项通过。第一轮测试包的旧固件fixture强行取空命令而失败，从未启动该包；fixture改为先断言旧版本无命令、再用仅隔离测试的合成新版本覆盖既有接收流程，不更改实际版本或放开.268。最终兼容保护Release仍在构建，部署／签名／实机连接结果后续补记。用户已收到现有.268网络布局只读验收步骤，不能因源码通过就标为实体通过。


English: the user confirms a frozen clock/touch after the268 preview attempt, then normal clock/touch/USB after a normal power cycle. Eight sealed encrypted files (999972 bytes) were byte-verified in a private backup outside the repository; no Keychain/plaintext/photo/password was read. Unsynced device RAM was unavailable and disclosed before restart. Post-recovery numeric sampling shows USB1 confirmed/0 interrupted, uptime95710ms/UIpage0 and resetReason7 (SDK other watchdog), without a valid retained runtime trace; no frozen stack or unique reset-trigger claim is made. Preview counters remain0 and capture is not reopened. The actual installed IDF5.4.2 chunked-cache implementation acquires its global mutex then rejects M2C|UNALIGNED without releasing it. Actual SDK/production-preview/pixel-sampler functions under UBSan reproduce the old leak and second-call blocking under the display lock. M2C-only passes128 rotated captures plus camera/alignment/DMA/busy/argument paths, and the real P4 driver object compiles. Native HAL/address/lock substitutes are explicit; hardware mirror acceptance awaits a future combined image. No SDK/protocol or linked-image change occurs. A shared components symlink was discovered on the first edit, restored byte-exact before any firmware compile/link, then replaced by a private candidate copy. The268 image hash remains unchanged. Four source-only patches apply sequentially and match8 files. Mac quarantine blocks capture for268/older/unknown firmware and rejects late frames, preserving the original small-screen preview. Debug passes1370 isolated checks, including authenticated dispatcher gating. An initial stale-version fixture force-unwrap failed in an undeployed test bundle; the corrected fixture asserts no old-version command and uses only a synthetic future-version test announcement for the existing receiver cases. Final Release packaging/deployment remains pending. Read-only installed268 network-layout acceptance is requested separately, not inferred from source tests.


## 兼容保护正式包已校验，切换等待手动解锁

`artifacts/mac-preview-quarantine-r2-20261024/AIBotBridge.app`已完成94.60秒Release构建，实际成品1370项隔离、16项HTTP回环、9项音乐采样、固定helper私有管道／身份及codesign检查全部通过，23份Swift源码摘要一致。完整swift test已执行，仍因CLT缺少XCTest失败，不计为通过。正常退出旧包前，原生操作工具明确返回Mac锁屏且不能自动解锁，未执行退出或强杀；已请求用户手动解锁。R6保持当前运行，防护包尚未启动；不要误写成防护已生效，也不要重开.268设备预览。网络布局只读反馈待补。当前.BIN仍为原.268，没有下一镜像、版本、刷写或公开发布。

English: the final quarantine bundle builds Release in94.60s. Its actual executable passes1370 isolated,16 HTTP-loopback,9 music-sampling and fixed-helper identity/private-pipe checks; codesign and23 source hashes match. Full swift test is attempted but fails for missing XCTest in CLT. Before normal old-app quit, native control reports the Mac locked and unable to auto-unlock; no quit/forced termination or new-app launch occurs. Manual unlock is requested; R6 remains current and quarantine is not yet active. Keep268 preview closed. Read-only network-layout feedback is also pending. The268 BIN remains unchanged; no next image/version/flash/public release occurs.


## 诊断范围与备注精简，兼容保护包已部署

用户在.268只读验收中询问“设备配对／Codex／语音”三项为何位于连接诊断，并指出入口旁“查看通道与电脑服务状态，排查连接问题”是否准确。核对生产函数：配对取本地cfg.paired；Codex取最近活动快照的state；语音取电脑voiceEnabled开关。后两项的fresh条件只检查当前活动通道8秒内收到状态，不是独立Wi-Fi、Codex任务API或语音链路健康检测。原三项与Wi-Fi信息混排会误导，不能把“已启用”称为识别正常或把保存配对称为在线。

只在后续源码副本移除三项标签／更新逻辑，上移重新连接按钮和反馈，保留Wi-Fi网络、连接、IP、桥接鉴权／等待／错误及返回流程。入口备注改为“查看 Wi-Fi 连接与电脑桥接通信状态”。`docs/proposals/tab5-network-diagnostic-scope.patch`及来源摘要已登记；原生真实指针打开／返回、电脑切换／备注／USB归属、十网络、删除取消／失败／活动／最后／忙碌原流程通过，新页面及备注PNG已检查，ESP32-P4 ui.c对象编译通过。五份后续补丁顺序dry-run／应用／8文件比较通过；冻结源与.268 BIN校验未变。不是设备界面已更新或新固件实机通过。

用户反馈手动解锁完成后，R6经正常界面退出，完整进程和8765监听均空才启动R7 `artifacts/mac-preview-quarantine-r2-20261024/AIBotBridge.app`。唯一PID28727，USB确认1／中断0、uptime1212244ms，预览请求／发布／接收均0；检查窗口已关闭。该有限恢复不代表全部连接长期可靠性通过；.268采集继续被兼容门控拒绝，真实镜像等待未来统一固件。

继续现有.268提醒／删除弹窗验收：Mac原事项表为空，新增唯一“提醒界面验收1024请检查较长名称显示以及按钮大小”，10月24日22:40开始、22:50结束、准时提醒、不重复；保存后选中回读时间和名称一致。已请求用户核对较长标题、按钮宽度／间距及单次“知道了”关闭，先保留供删除弹窗核对。结果与清理均待补，不预先标为通过。


English: installed268's diagnostic row mixes a local saved-pair flag, recent Codex activity state and the host voice-enabled toggle. The latter two require a fresh current-transport snapshot within8s, not independent Wi-Fi/task-API/voice-path health probes. The entry's generic channel/service description overstates that page's actual Wi-Fi scope. The source-only follow-up removes the three labels/update logic, compacts retry/feedback and changes the entry to describe Wi-Fi connectivity and host-bridge communication, preserving network/IP/bridge auth/wait/errors/navigation. Real native pointer and existing host/profile/deletion workflows, reviewed PNGs and P4 UI-object compilation pass. All five patches apply sequentially and match8 files; immutable268 source/BIN stay unchanged. Physical acceptance needs a future combined image. After user manual Mac unlock, normal R6 exit is verified against all bridge processes/listener before sole R7 PID28727 starts. USB1/0, uptime1212244ms and preview request/publication/reception0 are observed; windows close. This is bounded recovery, not all-path long-term reliability. The unsafe268 capture stays blocked. A single synthetic long-title reminder is saved in the previously empty agenda for2026-10-24 22:40–22:50 Asia/Shanghai, nonrepeating/on-time, then read back. User title/button/dismissal acceptance and subsequent test-only deletion/cleanup remain pending. No next firmware/version/flash or publication occurs.


## .268提醒窗口布局实体通过，继续同项删除确认验收

用户对22:40长名称提醒明确反馈“显示和按钮正常，知道了关闭”。据此，现有.268到时弹窗的较长标题、按钮宽度／间距与单次关闭通过；没有独立读取本轮关闭回执或精确计时，不能扩大为所有名称或延后分支都重新验证。测试事项暂留，已请求只对此项检查删除确认窗口、先取消并确认仍在、再删除并返回日常页。删除布局、删除回传与测试项清理仍待反馈；未生成新固件或改用户草稿／照片／设置。

English: the user confirms normal long-title/button display and one-tap Got It dismissal for the22:40 reminder on installed268. That physical window-layout/dismissal check passes, without an independently read receipt/timing or renewed coverage of all title/snooze cases. The unique synthetic item remains for deletion-dialog acceptance: inspect, cancel/retain, then delete only that item and return. Deletion layout, host-sync readback and cleanup are pending. No new firmware or user draft/photo/settings change occurs.


## 提醒／删除窗口及清理通过，日程提醒音第二版定稿

用户先确认现有.268提醒窗口“显示和按钮正常，知道了关闭”，随后确认删除窗口“布局正常，取消保留，再删除成功”。Mac进入“全部事项”刷新，独立回读0行且测试名称不存在，测试项双方清理完成，两个检查窗口已关闭。此项实体通过，不保留额外临时提醒。

用户新增日程提醒音优化，选择“轻柔、圆润的短双音”。第一版实际源码合成试听后要求更清晰醒目；第二版240ms D5／80ms间隔／320ms G5／40ms静音，总提示0.68秒，平滑起音与衰减、轻微第二泛音。用户明确“这版可以”。试听WAV与生产`agenda_chime.h`同源，不等于TAB5喇叭实际音量／音色已验收。

日程通知改用独立请求入口，仍由原语音worker处理，保留用户音量／静音、30秒请求时限及录音优先；倒计时入口不变。`docs/proposals/tab5-agenda-gentle-chime.patch`及来源摘要已登记。原生UBSan执行原／新真实播放函数、模拟codec／队列／通知PCM资产，核对回复、试听／倒计时、监控三种既有输出逐字节一致；检查双声道、峰值8435／相邻差2958、间隔／尾部零样本、静音／低内存／打开失败／录音排队／播放中静音和40个写入失败点的收尾均通过。实际LVGL日程通知路由及原有日程／倒计时工作流通过；ESP32-P4 voice_remote.c和ui.c对象编译通过。新音色为本项目原创合成，无外来音频资产；源码与试听证据在`artifacts/development/tab5-agenda-chime-20261024/`。

六份后续补丁在冻结.268独立文件副本上顺序dry-run／应用／13文件比较通过；冻结显示驱动与原.268 BIN再次核验一致，没有链接新镜像或递增版本。新声音、相机等待、媒体点击、USBdelta、预览及诊断精简的实机复验仍待未来统一固件。

用户明确原照片／草稿“都是测试内容，可以清理后验收”。已请求仅在原“TAB5操作验收”会话清理旧测试附件，拍一张普通物体，手动单次发送带图的合成测试指令，以照片中显眼物体的实际描述核对附件送达；不让模型盲回“收到”作为图像通过。相机超过约30秒／列表消失／发送或看图异常保留现场、不补点，结果待反馈。没有通过工具自动发送消息、改日常会话、网络或展示设置。


English: the user passes installed268's long-title reminder/buttons/dismissal and deletion-dialog layout/cancel-retain/confirm-delete. Independent refreshed Mac All Items has0 rows and no synthetic test title; cleanup is complete and windows close. The user adds agenda sound refinement, initially preferring gentle rounded dual notes, then requests greater clarity after the first actual-source WAV. The second original cue is240ms D5,80ms gap,320ms G5,40ms silence (0.68s) with smooth attack/decay and a quiet second partial, explicitly accepted by the user. WAV approval is not physical speaker-volume/timbre acceptance. Agenda gets its own request, still handled by the existing voice worker with user volume/mute,30s expiry and capture priority; countdown is preserved. Native UBSan runs both actual output functions with synthetic codec/queue/notice assets, byte-compares unchanged reply/preview-countdown/health PCM, and checks stereo, peak8435/step2958, silent gaps/endpoints, mute/memory/open/capture/40 write-failure cleanup paths. Actual LVGL agenda notification routing and existing agenda/countdown workflows plus P4 voice/UI object compilation pass. All six follow-up patches apply sequentially and match13 files; immutable268 source/BIN remain unchanged, with no new linked image/version/flash. User confirms prior photo/draft content is disposable test material, authorizing cleanup for a single fresh ordinary-photo test in the original acceptance conversation, sent manually by the user. A concrete object description tests actual image delivery, not blind acknowledgement. Camera>30s/list disappearance/send/image errors should preserve the scene without repeat taps. Feedback remains pending; no tool sends a message or changes daily conversations/settings.


## 相机等待原因复核，按先修相机的要求统一构建.269

用户要求先解决打开相机慢，再测试照片附件，并追问原因。复核.268实际photo_open：codex_reading／codex_syncing／codex_load_waiting任一为真时先显示准备并等待，其后的三照片上限检查因此也被延迟；后台会话读取／草稿请求的耗时被带入相机入口。camera_preview_start本身只启动设备相机任务，不同步执行这段远端请求。该源码分支确定存在，本机原生回归验证局部相机在普通读取／保存期间立即启动且仅一次，满额立即拒绝，原草稿附件保留；真实整段打开秒数仍不能用原生检查替代。首次尚未加载的实际草稿恢复仍受保护，照片上传会等前一邮箱结果被消费，避免覆盖请求。

用户已明确将照片验收留到相机修复后；现有可做界面项结束、六份源码补丁及目标对象检查完成。按此前“全部修复后统一生成”的既有授权，启动一份.269统一候选构建，不再要求在旧.268上重做长等相机测试。源从原.268完整目录私有复制，再覆盖已核对的13文件，7528个文件逐一登记SHA；没有共享源码符号链接。仅子进程macOS background taskpolicy／nice10，最多4编译任务，不修改系统全局资源策略。构建先比对生成SDK配置与.268逐项定义一致再进入编译。此时成品／latest尚未生成，未请求或执行.269刷写，具体候选安装仍需最终校验后按固件工作流取得明确授权。

English: the user defers photo sending until camera slowness is fixed, then asks for the cause. Actual268 photo_open waits for any background read/save/load before even checking the three-photo cap, coupling host latency to local camera entry. camera_preview_start launches a device task rather than synchronously performing that host work. Native checks prove immediate single local start during ordinary read/save, immediate full-cap rejection and retained attachments; they do not establish physical opening seconds. Actual initial unloaded-draft restoration remains protected; uploads consume prior mailbox results before proceeding. With existing UI checks complete and six source fixes/target objects validated, standing unified-generation authorization is used to build one269 candidate, without repeating the slow old-camera test. A full private268 copy receives13 reviewed follow-up files, with7528 source hashes and no shared source symlinks. Only child background taskpolicy/nice10/max4 workers applies; generated SDK definitions must match268 before compilation. Image/latest readiness and candidate-specific flash approval remain pending; no device write occurs.


## 统一候选.269已准备，等待本候选安装授权

完整ESP32-P4配置与编译通过，所有配置定义与.268一致；7528份冻结输入在构建后仍逐字节匹配。BIN为7202304字节，OTA分区7208960字节、余6656；生产包校验器确认内嵌版本0.2.269-ui、BIN SHA-256 dbfe782fcfe389896e3b5972c2e8ef394bd7c1438ca91b785c016f5e59d081a4，ELF身份b70c4f29bfcec6c46afe7603449a388106418262ec358657ad34d017f8691b49并与实际ELF文件一致。符号表确认显示预览与日程通知实现已链接。

正常准备脚本已将成品／601字节分类列表说明／清单及校验归档到固定`artifacts/firmware/tab5/latest/`及`versions/0.2.269-ui/dbfe782fcfe3-7257fc99/`，LICENSE和THIRD_PARTY_NOTICES齐全。原.268历史BIN再次校验一致。使用实际latest sidecar的原生预览及两张滚动截图已检查，版本／分类／列表／末行完整，图片不是设备截图。`versions/TAB5`登记待发布.269，中英文TAB5日志同步、组件版本检查与diff-check通过，公开下载及桥接／ESP8266版本不变。

构建仅调整自身子进程资源：起初background taskpolicy／nice10／4任务，随后取消本构建树的Darwin后台I/O限制以改善完整重建吞吐，nice10与4任务仍保留，无系统全局或桥接策略改动。额外Mac契约脚本仅出现缺少CLI输入的用法错误，未执行、不计通过；该阶段没有协议改动，其余已登记原生专项／UI／目标对象和生产镜像检查分别保留，不能宣称全套XCTest或Windows实机通过。

已按固件工作流“刷机仍需对应候选的用户授权”请求安装.269，先相机打开／关闭重开，再照片发送与其余后续实机复验。目前设备仍为.268，尚未授权或执行.269刷写；没有推送、标签或公开发布。

English: the full P4 configure/build passes with identical268 config definitions and7528 unchanged frozen inputs. The269 BIN is7202304 bytes in a7208960-byte partition (6656 free), passes the production image/version/SHA loader, and embeds ELF identity matching the real ELF. Preview/agenda symbols are linked. The standard preparation script archives the image, structured601-byte notes/checksum/manifest and licenses at fixed latest and the new269 history directory; original268 archive still matches. Native rendering uses the actual prepared sidecar; both scroll positions are visually inspected, not presented as device screenshots. Pending TAB5 metadata and both changelogs register269; component and diff checks pass, with public downloads/other component versions unchanged. Child-only Darwin background I/O is removed during the full rebuild while nice10/max4 remain; no global or bridge policy changes occur. An optional Mac contract helper only rejects missing CLI inputs and is not run or counted as pass; no wire change occurs, and existing focused/native/UI/object/package evidence retains its limits. Candidate-specific269 installation permission is requested as required by the firmware workflow, prioritizing camera startup/reopen before photo sending. Installed268 remains unchanged; no269 authorization/device write, push/tag/public release yet.


## .269安装及相机入口实体通过，继续照片发送

用户明确授权“现在安装 .269，先验相机”。Mac正常升级页重新选择固定latest BIN，逐项核对.269／SHA后只发起一次升级，设备确认开始。随后设备上报.269，但旧请求提示仍转为未确认；未重复刷写。首次USB启动核验返回通用未确认，没有保留原始失败样本，不能唯一归因；等待本机连续30秒健康窗口后第二次只读核验通过：精确ELF b70c4f29bfcec6c46afe7603449a388106418262ec358657ad34d017f8691b49、VALID分区与三个实时界面样本一致，同一配对设备，USB和无线状态恢复。

升级前后均经正常显示设置页回读并点取消：亮度79、音量70、静音关、提醒开、轮播关、固定总览、15秒间隔、5分钟屏保，参与轮播页面与完整顺序一致。未执行恢复自动模式／轮播，不改配对或网络。安装证据与前后基线保存在`artifacts/development/tab5-combined-269-20261024/`；两个检查窗口已关闭。

用户主动反馈“相机延时的问题解决了。超过三张也立马提醒了。”，据此相机入口等待与满额延迟两项实机通过；没有独立量秒，未额外要求重拍。用户刚添加的三张为已授权可清理测试照片，已请求在原验收会话单次发送，并以分别描述三张显眼物体核对端到端图片送达；异常保留现场、不补点。照片发送、镜像、新提醒音等其余.269实体结果仍待后续，不能把安装通过等同全部功能通过。

English: the user explicitly authorizes269 installation prioritizing camera testing. The normal Mac UI selects fixed latest, checks version/hash and starts exactly one upgrade. The device acknowledges and reports269; old request status later becomes unconfirmed, without a second flash. The first USB boot check is generic unconfirmed with no raw failed sample, so no unique cause is asserted. After the native30s health interval, a second read-only check passes exact ELF/VALID/three live UI samples on the paired device, with links recovered. Read-only before/after display settings match brightness79/volume70/mute off/alerts on/cycle off/fixed overview/15s/5min and all selected pages/order; Cancel is used, without mode/cycle restoration or network/pairing changes. Windows close. The user independently confirms camera slowness solved and immediate over-three feedback, physically passing those two fixes without measured seconds or repeat captures. Three newly added disposable test images are requested for one manual message in the original acceptance conversation, verified by separate concrete image descriptions. Attachment sending/mirroring/physical new sound and other269 checks remain pending; installation is not full feature acceptance. No publication occurs.
