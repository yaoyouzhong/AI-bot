# Mac TAB5 开发候选 / Development candidate

2026-10-29 预览修复补充：现场 .292 大屏预览降至 640×360，并持续报告原始像素分配失败。新增 .293 候选安全复用未显示的图库缓存，本地构建与回归通过，尚未安装。该问题现为发布前待实机验收项，不能沿用此前“只剩排队”的结论。当前安装仍为 R34/.292，R35 排队验收仍按原安排。详见 [预览修复记录](development/TAB5-PREVIEW-MEMORY-20261029.md)。

October 29 preview follow-up: installed .292 falls back to 640×360 with repeated raw-pixel allocation failures. The .293 candidate safely reuses unused gallery caches and passes local build/regression checks, but is not installed. Preview hardware acceptance is now an additional release gate; the earlier queue-only conclusion is superseded. R34/.292 remains installed and the R35 queue acceptance schedule is unchanged.

2026-10-29 后续源码：R35 修复排队消息已可靠保存却误报“未确认发送”的确认路径，实机验收按用户要求留到 10 月 30 日，当前安装仍为 R34/.292。本轮新增功能、Bug 修复、性能优化及已具备能力的 Windows 开发入口集中在 [Windows 对齐交接清单](development/WINDOWS-FOLLOWUP-FROM-MAC-20261029.md)。连接中断恢复没有已知故障，只留作日常观察，不安排专项验收。此次提交源码与文档，不发布正式版本。

October 29 source follow-up: R35 reconciles reliably retained queued messages after uncertain native acknowledgment. Hardware acceptance is deferred to October 30; R34/.292 remains installed. The linked Windows handoff covers new features, fixes, performance work and existing capabilities. Connection interruption is not a known defect and remains ordinary-use observation. This is a source/documentation delivery, not a formal release.

当前安装（2026-10-29）：**Mac R34／TAB5 .292**。用户已确认回填、发送与正确成功提示、长按清空、句尾完整、取消重录，本轮两题切换／回填／提交及 Mac 音源名称与完整文字确认正常。R34 连续追加与末尾光标已实机通过，Mac 独立豆包恢复也已确认；Release 和 28,742 项隔离检查通过；完整 XCTest 仍受本机 CLT 缺模块限制。录音期间更换题卡会保留预览并阻止串题也已实测通过；空闲发送和运行中直接引导已通过；排队确认修复留到 10 月 30 日实机验收，连接中断只在日常使用中观察，不列已知故障；DJI 现场未连接，详见[当前语音验收记录](development/MAC-VOICE-SOURCE-20261028.md)。未公开发布。

Current installation (October 29): **Mac R34 / TAB5 .292**. The user accepts staging, actual send with correct success status, clear, final words and cancel/re-record, plus this two-question flow and Mac source naming/full text. R34 sequential append/end-caret changes and ordinary Mac Doubao restoration pass physical acceptance; Release and 28,742 isolated checks pass; full XCTest is unavailable in local CLT. Recording across question-card replacement now passes preview-preserving rejection; idle sending and direct active steering pass; the queue fix awaits October 30 hardware acceptance, connection interruption remains ordinary-use observation rather than a known defect, and DJI is not connected; see the linked acceptance record. No public release.

以下为历史检查点，不代表当前安装状态。 / Historical checkpoints follow; they are not current installation status.

当前本机桥接：R24 录音延迟优化版（2026-10-29），保留 R21 BLE 行为及已安装 TAB5 .291。R23 已将 Codex 读取移到后台，并有界保留 TAB5 启动音频；实测收到开始请求后 503 ms 回复收音就绪，但停止至最终识别仍需 5103 ms。R24 批量读取麦克风控件、减少固定准备等待，并让最终查询及时返回；Release／28489 项隔离检查／签名通过，已部署；本次实机停止至最终识别从 5103 ms 降至 3384 ms，最终结果至响应生成 0 ms，用户确认文字完整返回，原麦克风已恢复。豆包设置页弹出仍未解决，不能将原通知方式宣称为可用后台设置接口。详见 [语音记录](development/MAC-VOICE-SOURCE-20261028.md)。

2026-10-29 当前：**Mac R21 与 TAB5 .291 已部署，正常后台连接已恢复。** 修复升级读取／开始回执共用 RPC 通道时“尚未发送、通道忙”被当作终止错误的路径，并对齐 Windows 既有无线暂停机制：自动模式下实际 BLE 升级经能力协商可临时暂停 Wi-Fi，完成后恢复。完全相同 .291 镜像，R20 安装 399.979 秒，R21 两轮 338.938／340.052 秒，减少约 15%；含启动核验分别 452.618 → 390.904／392.691 秒。三轮精确 ELF／VALID／三个实时样本／显示设置保留通过，两轮 R21 零窗口重放／写入失败、无线恢复成功。逐包确认更慢，分批让出未证明收益，默认保持原生背压与 64 包窗口。剩余耗时仍集中在 CoreBluetooth 流控与确认等待，尚未拿到实际 PHY／控制器调度证据，不能声称速度已全面解决；历史 .289 的通用失败码无法反推唯一原因。Release、28464 项隔离、固件构建及故障矩阵通过；XCTest 缺模块，Windows 运行验收仍待 Windows 主机。详见[实测与限制](development/MAC-TAB5-BLE-OTA-20261029.md)。未公开发布。

Current, October 29: **R21 and TAB5 .291 are installed with normal background connections restored.** Bounded mailbox admission and immutable-range recovery fix a reproducible unpublished-busy OTA exit path. An authenticated optional capability applies the existing Windows radio-pause policy to actual BLE OTA in automatic mode, restoring Wi-Fi afterward. With the identical .291 image, installation improves from R20's 399.979 s to 338.938/340.052 s (about 15%); healthy-boot completion improves from 452.618 to 390.904/392.691 s. Exact ELF/VALID/three live samples/settings pass; both R21 runs have zero window replays/write failures and successful radio restoration. Confirmed-every-fragment writes are slower; yielding has no established benefit, so default native backpressure and 64-fragment windows remain. Significant CoreBluetooth waits remain without actual PHY/controller scheduling evidence. Historical .289's generic failure cannot prove its unique cause. Release, 28464 isolated checks, firmware build and fault tests pass; XCTest and Windows runtime validation are unavailable here. See the linked results. No public release.

以下继续保留历史检查点；当前安装以文首 R34／.292 为准。

2026-10-29 当前：**Mac R20 与 TAB5 .290 已部署，正常后台连接已恢复。** 蓝牙参数拒绝后的有限回退实测生效，实际间隔 30 → 15 ms；相同 .290 镜像 Wi-Fi 安装 22.673 秒、BLE 重装安装 418.138 秒，含健康启动核验 474.011 秒。相比 .288/R19 的 836.485 秒，安装时间减少 50.01%（跨版镜像差 400 字节）；零窗口重放／写入失败，精确 ELF／VALID／三个实时样本／设置保留通过。仍是分钟级；.289 首轮首块前退出未确定原因，.290 重复相同正常短测后升级顺序未复现，不能宣称全面根治。Release 与 28464 项隔离检查、固件构建及故障测试通过，XCTest 仍缺模块。详见[蓝牙实测与剩余问题](development/MAC-TAB5-BLE-OTA-20261029.md)。未公开发布。

Current, October 29: **R20 and TAB5 .290 are installed, with normal background connections restored.** Bounded interval fallback reduces the measured interval from 30 to 15 ms. The same .290 image installs over Wi-Fi in 22.673 s and reinstalls over BLE in 418.138 s, or 474.011 s through healthy boot. Installation is 50.01% shorter than .288/R19's 836.485 s; images differ by 400 bytes across versions. Zero replay/write failures and exact ELF/VALID/three live samples/preserved settings pass. BLE still takes minutes. An earlier .289 pre-first-block failure remains unexplained; the normal short-probe-then-upgrade sequence passed on .290, which does not establish a complete stability fix. Release, 28464 isolated checks, firmware build and fault tests pass; XCTest remains unavailable. See the linked evidence. No public release.

以下继续保留 R19 及更早的历史检查点。

2026-10-29 当前：**Mac R19 已安装并恢复后台运行，蓝牙 RPC 发送窗口、一次原密文恢复及批量调度已对齐 Windows 现有机制。** 完全相同 .288 的蓝牙完整安装由 959.196 秒降至 836.485 秒（减少 12.79%），0 次窗口补发、0 次写入失败，精确 ELF／VALID／三个实时样本与设置保留通过。仍有大量 CoreBluetooth 背压，且系统拒绝设备的 7.5 ms 请求；实际 interval／PHY 未测，不能宣称蓝牙速度问题已根治。候选及工作区 Release、28455 项隔离、81 项 HTTP／socket、16 项监听恢复通过，XCTest 仍缺模块。详见[蓝牙对照与验收](development/MAC-TAB5-BLE-OTA-20261029.md)。R18 的 Wi-Fi 21.666 秒结果保留，未重新测试；既有语音／清空视觉体验验收仍待完成，未公开发布。

Current, October 29: **R19 is installed in the background.** BLE response windows, one immutable-ciphertext recovery and bulk scheduling align with existing Windows mechanisms. The identical .288 BLE install improves from 959.196 to 836.485 s (12.79% less), with zero response-window replays/write failures and exact ELF/VALID/three-live-sample/settings acceptance. Significant CoreBluetooth backpressure and OS rejection of the peripheral 7.5 ms request remain; actual interval/PHY is unmeasured, so this is not a complete speed fix. Candidate/workspace Release, 28455 isolated, 81 HTTP/socket and 16 listener recovery checks pass; XCTest is unavailable. See the linked BLE report. R18 Wi-Fi 21.666 s evidence is retained, not remeasured; prior physical voice/composer checks remain pending. No public release.

以下保留 10 月 28 日检查点。

2026-10-28 当前：**Mac R14 保持稳定分块传输，TAB5 .288 已安装，边接收边分段擦除／写入已实现并核验。** .287 连接复用后同镜像 Wi-Fi 安装 25.578 秒；.288 同镜像分块安装两轮 25.921／28.248 秒，精确 ELF／VALID／三个实时样本和设置保留通过。USB .287 实测 38.793 秒。时间均为接收至镜像校验，启动后健康核验另计；Windows 历史 13.563 秒是传输阶段，设备安装为 27.724 秒，镜像不同。Mac 连续流、MSS 和更快分块节奏实验均未通过实机性能／完整性验收，未部署，稳定策略保留。

豆包安装状态已自动检测，未安装时不可选择，已移除“检查豆包”；升级后自动通过 USB／Wi-Fi／BLE 可用通道核验启动；同镜像重装已实测通过。R14 Release、3207 项隔离、47 项 HTTP、原生界面及签名检查通过；完整 XCTest 因本机 CLT 缺模块不可运行。后台语音 R9 的实际无闪现／每段后 Mac 麦克风恢复，以及 .285 清空残影的视觉复验仍待用户在设备旁确认。详见[本轮记录](development/MAC-VOICE-SOURCE-20261028.md)。本地候选未公开发布。

Current, October 28: stable Mac R14 range transport remains installed; TAB5 .288 implements and verifies concurrent reception with segmented erase/write. Exact-image Wi-Fi installation takes 25.578 s on .287 and 25.921–28.248 s on .288, with exact ELF/VALID/three live samples and unchanged settings. The .287 USB install takes 38.793 s. These exclude post-boot health verification. Historical Windows records 13.563 s transfer and 27.724 s installation for a different image. Continuous-stream, MSS and faster-range experiments fail physical acceptance and are not deployed. Automatic Doubao availability, unavailable-provider rejection, automatic boot verification and explicit same-image reinstall are retained. R14 passes Release/3207 isolated/47 HTTP/native UI/signing checks; XCTest is unavailable in the installed CLT. Physical background voice restoration/flash and composer-clear visual acceptance remain pending. No public release.

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

下一步：继续核对连续更新与反复标准画质降级；本轮Codex切页和两次基本重开不外推长期稳定。蓝闪的持续肉眼观察、实际Windows／ESP8266环境边界保留；Windows共用设备修复与平台差异见[Windows参考](development/MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。无新Mac桥接、无重配网络／配对、无测试消息，无推送、标签或公开发布。

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

R18 标准“窗口”菜单复用托盘的设备预览选择，仅设备列表变化时更新；入口已实际打开正确TAB5，原小屏预览保持。Release、1564项隔离及27项真实窗口合成生命周期通过，签名有效、Keychain helper与R16相同；`swift test`仍受本机缺XCTest限制。Windows沿用既有诊断字符串，可接受`;sp`后缀；设备修复两端共用，详见[Windows对照](development/MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。

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

**剩余用户实机复验：**持续使用是否再蓝闪；设备预览的高清、切页、关闭重开及与录音并发；新照片提示和连接诊断布局。蓝闪故障期没有有效诊断，本轮针对明确调度／内存风险修复，根因尚未唯一证明。此前.272录音两轮、单图／三图发送、提醒音等通过结果保留，长期三通道可靠性继续观察。Windows差异与待修入口见[修复参考](development/MAC-TO-WINDOWS-FIX-REFERENCE-20261025.md)。不把Mac验收外推到Windows；未向其他聊天发送消息，无推送、标签或公开发布。

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

2026-10-25最新：R12 USB首轮结束失败，Mac手动stop200、最终75ms，但终态响应晚约11秒；实时栈抓到后台Codex目录gate挡住串行邮箱。R13完整源码通过Debug1501项，保持USB语音优先至同一take终态分片ACK，后台读取在该期间快速让出；read-ack不阻塞目录gate，原最新turn核验保留。完整Release构建中，R12仍运行，R13未部署。固件.270不变，设备端回执避让、按钮和预览内存补丁均仅源码检查，未生成新固件。

Latest: the first R12 USB stop failed. Mac received a manual stop and finalized in75ms, while terminal response generation lagged about11seconds; a live sample captured the serial operation waiting behind the Codex catalog gate. The complete R13 source passes1501 Debug checks and retains USB priority through matching terminal fragment ACKs, yielding background reads and avoiding a blocked read-receipt gate while preserving latest-turn validation. Release is building; R12 still runs and R13 is not deployed. Firmware stays270; device receipt-yield/button/preview-memory changes remain source-only, with no new image.

2026-10-25最新部署：R12完整包`artifacts/mac-usb-voice-priority-r12-complete-20261025/AIBotBridge.app`已正常替换R11，唯一PID55328，USB／Wi-Fi／BLE认证恢复，Debug／Release各1478项、HTTP16、音乐9及签名／原helper检查通过。保持认证、状态及保活，USB录音控制优先，内嵌／独立图片片均让出录音；两轮USB复验已请求，尚未通过。TAB5仍.270，设备按钮与高清预览内存修复仅源码验证，未生成新固件。

Latest deployment: the complete R12 bundle replaces R11 as the sole process with authenticated transports. Debug/Release1478 checks each, HTTP16, music9, source hashes and signature/helper checks pass. Live USB voice is authenticated and pumped ahead of ordinary status, while ownership/status maintenance remains and both inline/standalone image chunks defer. Two physical USB takes are pending. Firmware stays270 with source-only button/HD-preview memory follow-ups; no new firmware is generated.

2026-10-25最新：仅Wi-Fi录音手动停止通过，Mac确认最终识别60ms；USB9.249秒请求空档仍待修复验收。R12保持认证与心跳，在USB循环优先录音RPC并暂停可选资源片，Debug1478项通过，最终Release包构建中，未部署。TAB5仍.270；按钮和预览内存补丁未刷入，不生成新固件。

Latest: one Wi-Fi-only manually stopped take passes, with60ms host finalization. USB acceptance remains open after a9249ms request. R12 prioritizes authenticated live voice RPC while retaining status/ownership maintenance and deferring optional image chunks;1478 Debug checks pass and the final Release build is pending. R12 is not deployed; firmware stays270 with source-only button/preview-memory follow-ups and no new image.

2026-10-25当前：R10录音停止复验失败，任务页圆形按钮按后计时／图标不变。R11（`artifacts/mac-voice-observation-r11-20261025/AIBotBridge.app`）仅增加设备既有语音请求计时显示，正常替换后唯一PID54379，三通道认证恢复，Debug／Release各1464项及签名通过。设备保留的上次语音请求9249ms，正在做一次仅Wi-Fi对比，录音未通过。圆按钮按下即停止／72px命中／旧ready竞争的源码回归与P4对象通过，尚未刷入；当前仍.270。预览降级观察到640×360完整帧，高清内存未通过；未生成下一固件。

Current: R10 recording-stop acceptance failed. The sole R11 diagnostic bridge exposes existing device voice timing without content and passes1464 isolated checks in each configuration plus signature/helper checks. The retained request took9249ms; one Wi-Fi-only comparison is pending. Source changes for press-time stop, a72px effective target and late-ready synchronization pass native/target-object regression checks but are not installed. Firmware remains270; a complete640×360 fallback preview was observed, while HD memory acceptance stays open. No new image was generated.

2026-10-25最新：.270第二轮录音仍提前返回，诊断显示八秒无有效请求；线程现场捕获Mac USB队列中的NSHost解析阻塞及共享锁占用。数字网卡枚举和锁外查询修复通过Debug／Release各1462项、窗口27项及签名检查；R10已正常切换为唯一PID53156并恢复三通道认证，等待两轮录音复验。设备预览另报内存不足；两端v2画质降级已准备，设备可选缓存回收／暂停预热源码通过原生及P4对象检查，尚无新固件。单图／三图消息验收仍通过。下方R9/.270发布前记录按历史保留。

Latest: the second270 recording take ended early with an eight-second request gap. A live stack sample captured blocking NSHost resolution on the USB queue under the shared service lock. Numeric interface enumeration and lock-free host lookup pass1462 isolated checks in each Debug/Release configuration plus27 native window checks; R10 is deployed as the sole process with authenticated transport recovery, awaiting recording retest. Preview also reports memory pressure. Both hosts have bounded v2 quality fallback; device cache-reclamation/preheating changes pass native/target-object checks, with no new firmware generated. One- and three-photo message acceptance remains passed.


2026-10-25当前检查点：已安装TAB5 .270（本候选获明确授权，精确启动及设置保留通过），Mac已正常切换到R9（`artifacts/mac-voice-preview-r9-20261025/AIBotBridge.app`），唯一PID47902、USB确认。用户确认单图及三图＋文字发送成功；录音开始／停止／提前结束及间歇预览停更、重复重绘保持未通过。录音排队与预览重开／租约／无进展恢复源码已修复，新增经新固件协商的高清JPEG和未变化回执，Mac／Windows协议同步。Mac1415项、窗口27项、Windows生产模型40项、实际SDK/worker及P4对象检查通过，统一.270已完成构建、硬件配置、镜像／ELF／SHA和实际更新说明校验；已按对应授权安装，录音与预览实机复验中，未公开发布。最新事实以[当前验收检查点](development/MAC-CURRENT-ACCEPTANCE-20261024.md)为准，以下旧阶段记录按时间保留。

Current checkpoint: TAB5 runs270 after explicit authorization, exact boot verification and unchanged preference readback; the validated R9 bridge runs with USB confirmed. One- and three-photo messages pass; recording and intermittent preview remain open. Source fixes and negotiated HD/unchanged preview support pass native/production-model checks, with270 built and validated, installed with recording/preview physical acceptance in progress. No installation or public release is claimed; older entries below are historical.

2026-10-24 最新排查包为 `artifacts/mac-wifi-status-diagnostics-20261024/AIBotBridge.app`（Release）：在设备诊断中增加不含内容／凭据的 HTTP 回包计数和耗时，保留前述 BLE 及菜单等修复。构建、1158 项隔离检查和签名/helper 校验通过；完整 XCTest 仍受环境限制。日常 Wi-Fi 回包异常在仅 Wi-Fi、设备 Wi-Fi 重连、退出代理/VPN后均未恢复，TAB5 整机正常重启后实读 14/14 有效回包恢复；短 BLE 传输／停止／恢复原模式后回包持续正常，原持续中断未复现，但根因仍未定位；是否转观察项后统一构建待用户选择。用户已确认先前六轮测速后恢复原模式。TAB5 仍为 `.267`，未生成新固件；当前状态以[当前验收检查点](development/MAC-CURRENT-ACCEPTANCE-20261024.md)为准，以下旧记录按历史保留。

The latest diagnostic bundle is the Release path above. It adds ephemeral scalar HTTP response timing/counters and retains earlier fixes, passing build, 1158 isolated checks and signature/helper identity verification; full XCTest remains unavailable. Wi-Fi-only mode, device Wi-Fi reconnect and exiting the network tool did not recover an ordinary status-reply failure; a normal TAB5 power cycle restored 14/14 valid responses. A short BLE/cancel/original-mode-return comparison now resumes Wi-Fi replies and normal prompts without reproducing the persistent outage; root cause remains open. The user is being asked to choose continued investigation or an observation disposition before image generation. Original-mode restoration after the earlier full benchmark is confirmed. Firmware stays `.267`; no new image is generated. The linked current checkpoint governs later results.

2026-10-24 继续验收的最新 Mac 包为 `artifacts/mac-ble-rpc-recovery-20261024/AIBotBridge.app`：修复 USB 管理回执错误压住设备所选蓝牙通道的问题（按既有 active 回执判定），并接入有界 RPC 批量通知及受限订阅恢复。Release 和 1158 项隔离检查通过；实际 32 片 RPC／8 片语音订阅已确认，完整 BLE 六轮各 256 KiB、错误全 0、平均上传／下载 5.9／5.7 KiB/s；原连接模式恢复待确认。保留前述设备预览、日程／生日重开及测速修复，节气更新改读中国科学院紫金山天文台官方索引与年度日历资料，停止香港天文台请求。2025／2026 原生下载、两端 48 日期解析及运行包实际缓存更新通过，未来未列出年份保留离线数据。日本源未部署、不作回退。本文其余旧运行路径按历史保留；当前构建和实体检查以[后续修复记录](development/MAC-REMAINING-FIXES-20261024.md)最新条目为准。TAB5 继续 `.267`，现有固件能完成的验收先做，最后统一生成新固件。

The latest Mac acceptance bundle is at the path above. It fixes BLE standby to follow the existing device-reported active transport while retaining USB management. It also adds bounded RPC notification windows and restricted subscription reconciliation. Release and 1158 isolated checks pass; native 32-fragment RPC and eight-fragment voice subscriptions are confirmed, and all six physical 256 KiB BLE samples pass with zero errors at 5.9/5.7 KiB/s. Original-mode restoration awaits confirmation. It retains Device Preview and the original ESP8266 renderer. It clears stale reopened birthday/agenda status and resets an agenda editor after confirmed discard and reopening. Older bundle paths below retain their historical context; current build and physical results are in the latest follow-up entries. TAB5 remains `.267`; finish available acceptance before generating one combined firmware image.

2026-10-24 已部署取消后恢复与图标修复候选：`artifacts/mac-unlock-cancel-recovery-20261024/AIBotBridge.app`。新请求重新核验已有密码页，再整值替换并提交一次，解决取消残留准备字符导致下次请求失败的问题；应用包新增已核验来源的机器人 `AppIcon.icns` 与图标声明。Debug／Release 构建及各 690 项隔离检查通过，完整 XCTest 仍因 CLT 缺少模块不可用。严格签名、原 helper 和单实例部署核验通过；原生连接恢复，系统图标解析正确。用户已确认取消后停止、第二次解锁成功及 30 秒未回锁；实机结论登记在[后续修复记录](development/MAC-REMAINING-FIXES-20261024.md)。TAB5 保持 `.267`，按用户要求待全部验收与修复后统一生成固件；以下 ble-priority 验收保留为历史依据。

The cancellation-recovery/icon candidate is at the path above. A new explicit request revalidates an existing password page before one whole-value replacement/submission, avoiding rejection of a retained preparation character. Packaging adds the provenance-verified robot icon and bundle declaration. Debug/Release builds and 690 isolated checks each pass; full XCTest remains unavailable in CLT. Strict signing, unchanged helper and single-instance deployment pass; native connections and system icon resolution are verified. The user accepted cancellation stopping input, a successful next explicit unlock and 30 seconds without relock; physical acceptance is recorded separately in the linked follow-up. TAB5 stays at `.267` until acceptance/fixes are complete; the following ble-priority evidence is historical.

USB 在途断连本轮通过：用户确认停止输入、保持锁屏及恢复原连接模式，测试后原生 USB 数据回执已恢复。此前 USB 未识别在正常开关机后恢复，根因仍保留待查。ESP8266 当前无可接入硬件，本轮暂缓实机验收。

In-flight USB-disconnect acceptance passes for the observed case, with user-confirmed stopped input/retained lock/restored mode and independently confirmed post-test USB data. The earlier restart-recoverable enumeration issue remains open. ESP8266 hardware is unavailable for this run, so physical acceptance is deferred.

2026-10-24 语音／发送复测：在 ble-priority Mac＋TAB5 `.267` 上，用户确认录音完整、快捷发送一次成功。按用户要求，快捷发送偶发失败和录音中断暂时移出当前待办，复现后再定位；保留历史记录，不宣称根因已全部修复。测试范围及证据见[后续修复记录](development/MAC-REMAINING-FIXES-20261024.md#快捷发送与录音复测收尾)。

Voice/submission retest: the user confirms complete recording and one successful Quick Console submission on the tested ble-priority Mac and TAB5 `.267`. Both intermittent issues are removed from the active backlog at the user's request and will be revisited if reproduced. Historical evidence remains; underlying causes are not declared fully resolved. See the linked follow-up record for scope and evidence.

2026-10-24 最终普通锁屏回归通过：ble-priority Mac 和 TAB5 `0.2.267-ui` 的仅 USB、仅蓝牙、仅 Wi-Fi，以及 USB 拔线后切蓝牙，用户均确认首次点击锁屏／解锁成功、设备提示正确，桌面观察 30 秒未回锁。已核对设备与 Mac 的实际通道及成功回执；拔线恢复状态的具体耗时未测量。

Final ordinary-lock regression passes with the ble-priority Mac and TAB5 `0.2.267-ui`: USB-only, BLE-only, Wi-Fi-only and USB-removal-to-BLE handoff. The user confirms first-tap lock/unlock, matching device results and 30 seconds without relock. Independent device/Mac receipts verify the actual successful transports; handoff recovery time was not measured.

此前 ble-priority Mac Debug／Release 各 635 项隔离检查、冻结源码、严格签名和原钥匙串 helper 核验通过；完整 `swift test` 仍因本机 CLT 缺少 XCTest 无法通过。`.267` 已经授权安装并通过精确 ELF／VALID 分区／实时 UI 启动核验，16 个显示设置控件及顺序保持。此前失败与修复过程保留在 [USB／蓝牙验收记录](development/MAC-USB-BLE-UNLOCK-20261024.md)。

The earlier ble-priority configurations pass 635 isolated checks, frozen-source verification, strict signing and unchanged-helper checks. Full XCTest remains unavailable in the installed CLT. Authorized `.267` installation passes exact ELF, VALID-partition and live-UI boot checks; all 16 display controls and order are preserved. The linked record retains earlier failures and repairs.

现有配对、密码及 Wi-Fi 权限保留，不下发 Mac 密码，不改系统全局设置。Computer Use 外层保护的停止／交接／解锁、唤醒即锁屏、设备主动唤醒及未实测场景仍单独验收；手动唤醒后再次锁定／解锁及 30 秒未回锁已由用户确认；不将普通锁屏结果扩大到这些场景。本机运行包为 `artifacts/mac-unlock-cancel-recovery-20261024/AIBotBridge.app`；TAB5 固定交付为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`。

Existing pairing, credentials and Wi-Fi permission remain; the Mac password is never sent to TAB5 and global settings are unchanged. Computer Use protection handoff, wake to a locked screen, device-initiated wake and other untested scenarios remain separate; the user accepted manual-wake recovery followed by lock/unlock and 30 seconds without relock. The local running bundle and fixed firmware delivery paths are listed above.

这是测试分支 `codex/mac-tab5-phase1-ci` 的未发布候选，适用于 Apple Silicon、macOS 13+。不替换现有公开下载，不代表 Mac/TAB5 全部真机验收通过。2026-10-23 一键解锁及豆包音频转送记录见[新增接入记录](development/MAC-UNLOCK-DOUBAO-INTEGRATION-20261023.md)；自动豆包录音状态核验需要 macOS 14.2+。本轮已按授权安装本地 `.267`，未推送、合并 main、建标签或发布 Release。Intel Mac 尚未验收。

This unreleased development candidate targets Apple Silicon on macOS 13+. It does not replace public downloads or establish acceptance of every Mac/TAB5 feature. Local `.267` installation was authorized; no push, main merge, tag or Release occurred. Intel hardware is untested. Automatic Doubao recording-state verification requires macOS 14.2+; earlier integration evidence is linked above.

## 使用入口与权限

菜单栏只显示机器人图标，悬停可看 Codex／Claude 状态。右键菜单与 Windows 的设备中心菜单对齐：设备中心、已启用设备的“设备预览”、按设备分组的常用设置、退出；详细账号和桥接设置在主窗口中。设备分组最多两级，停用设备置灰，不在菜单中展开刷机、令牌和网络重置操作。

The menu bar shows the robot icon; hover for Codex/Claude activity. Its context menu follows the Windows device-center menu: Device Center, Device Preview for enabled hardware, device-grouped shortcuts, and Exit. Account and bridge settings remain in the main window. Groups use at most two levels, disabled devices are greyed out, and flashing/token/network-reset actions stay inside their settings windows.

“设备预览”保留 ESP8266 原来的预览绘制与控制；TAB5 使用独立的只读镜像窗口。启用一个设备时直接打开，启用两个设备时先按设备名称选择。两端桥接及 TAB5 实际显示缓冲采集源码已实现，旧 `.267` 尚不支持画面回传，窗口会提示等待镜像版固件。本轮未生成或刷写固件；真实画面、刷新速度及相机共用显示缓冲的实机验收，留待其余修复合并后统一进行。

Device Preview preserves the existing ESP8266 renderer and controls and adds a separate read-only TAB5 mirror. One enabled device opens directly; two offer a device-name selector. Both desktop adapters and actual TAB5 display-buffer capture are implemented in source. Existing `.267` cannot return frames, so its viewer waits for mirror-capable firmware. No firmware image was generated or flashed; actual pixels, refresh speed and shared camera/display behavior await the combined firmware acceptance.


1. 点击 Mac 菜单栏状态打开 **设备中心**，也可从右键菜单进入。布局参照 Windows，分为 **我的设备／账号数据／桥接设置**；左侧选择 TAB5 或 ESP8266 小屏，右侧进入对应功能。TAB5 的 **连接升级**采用 Wi-Fi、USB 配对、固件升级、语音、传输测速五页结构；蓝牙开关位于 USB 配对页。已安装 AI-bot 固件的设备先通过 USB 数据线连接，刷新端口并明确选择目标设备，再配对。配对结果不确定时保留同一把钥匙，不替换其他设备身份。首次连接允许本地网络访问。
2. 同一窗口通过 USB 写入 Wi-Fi。电脑和设备位于同一局域网，桥接运行时可自动发现；发现受网络限制时，重新通过 USB 保存电脑地址。Wi-Fi 密码仅发往设备，不写入偏好或诊断。USB 数据失效八秒后由设备回退无线；设备固定连接模式仍保留。
3. **显示设置**先读取设备实际值，再编辑亮度、声音、固定页、轮播页面/顺序、间隔和屏保等待。只有匹配回执确认才显示保存成功；未确认时重新读取。不因测试或 TAB5 升级恢复 ESP8266 的默认轮播。
4. 蓝牙首次安装默认开启，系统首次使用时请求权限；已保存的关闭选择仍会保留。仅连接已通过 USB 配对的设备。通过系统设置管理本地网络、蓝牙、自动化、辅助功能、语音识别和麦克风权限。

首次系统蓝牙配对显示数字时，核对 Mac 与实体 TAB5 的数字一致，并在两端确认。固件同时提供 HID 键盘服务，macOS 可能打开“键盘设置助理”；可以退出该助理，TAB5 状态与设置传输无需配置键盘布局。不要将“重新配对／清除旧配对”当作普通数字确认。

以 AI-bot Bridge 名义访问 `io.github.yaoyouzhong.AI-bot` 的“登录”钥匙串弹窗，与蓝牙配对是两种授权。“允许”仅针对此次访问；“始终允许”记住该候选对相应项目的许可，详见 [Apple 说明](https://support.apple.com/zh-cn/guide/keychain-access/kyca1243/mac)。草稿／回执密钥每个进程只读取一次，拒绝结果也缓存，后续操作不会反复请求同一密钥。构建脚本优先使用 `AIBOT_MAC_SIGN_IDENTITY`，否则读取本机 `~/Library/Application Support/AI-bot/local-code-signing-identity.txt` 中的 40 位证书指纹；该文件不进入仓库或发布包。选择固定身份后，签名失败会停止构建，不会静默退回临时签名。两者都未配置时仍为临时签名。固定签名可保持不同构建的身份规则，本机已确认辅助功能与定位许可跨构建保留。但本机自签名仍被钥匙串 partition 列表按 `cdhash` 识别，构建内容变化后仍会询问，不能承诺换包免密码；首次访问其他钥匙串项目或新权限也可能需要确认；不需要开启所有 App 的访问权限。

## 资源与固件

固定证书候选新增包内 `AIBotKeychainHelper`：只经继承的匿名管道处理本桥接既有的配对／供应商／私有存储项目，请求完成即退出，不安装服务或监听端口。工具校验实际父程序的路径、bundle ID 和同一叶证书，主程序也验证工具签名；失败不改用其他访问路径。工具的已签名字节按源码、工具链、架构和证书保留在本机构建缓存，因此普通桥接更新不再改变钥匙串访问进程的 CDHash。首次改用工具或工具本身更新仍可能按项目询问，应由用户在本机处理；跨构建实测结果以联调记录为准。凭据仍只持久保存在钥匙串，未导出、合并或修改旧项目权限。

Codex 直达与 Windows 对齐：精确确认可使用页面地址、可信主页面的完整唯一标题，或有当前页面标记的会话行唯一名称。未命名长会话加载后会采用 60 UTF-16 单元的缩略显示；此名称必须在完整目录中重新检查碰撞，不靠目标标题前缀或近期／项目子集猜测唯一性，不读取剪贴板。对原名或缩略名明确重复的已加载会话，纯打开操作沿用 Windows 的应用前台回执：请求仍绑定所选 ID，但界面明确说明没有精确读回页面身份。该回执不能授权输入框读取、填入、清空或发送；目录读取失败、外部页面和普通窗口标题不作为精确确认。会话按 Mac 原生最近活动时间 `recencyAt` 从近到远排序，缺少有效活动时间时回退 `updatedAt`；同名显示该时间，未知时间排后，不替代任务 ID。既有设备 `updatedAt` 承载这一排序时间，避免设备再次按记录更新时间重排；列表期间不重排的固件行为保持。

接口未返回显式名称的会话，按同一任务 ID 读取本机桌面目录的预览标题，避免统一显示“Codex 会话”；显式名称优先，目录读取失败保留本进程最近成功的名称。此读取只补齐已加载任务的显示文字，不增加任务、读取会话正文或提供导航身份确认。

**屏保图库 → 安装图库包**中导入现有 AI-bot 图库 ZIP；保留来源、许可、导读和全部图片 SHA。标准/高清包按类别导入，旧图库保留备份。设备显示专用 1280×720 JPEG，支持多页与横竖屏。电脑桌宠选择和当前音乐封面自动转为设备资源，收到资源确认后停止发送。

在 **屏保图库** 浏览名画和书法，按作品名／作者搜索、按日期定位，查看分页、导读和来源。高清完整包逐个校验原图 SHA、索引裁切范围及内置 `standard.zip`，按类别整体安装并保留旧包备份。浏览器优先合成高清原图，校验失败时使用已验证的设备预览；渲染限制为 1600 万像素。点击“浏览作品”后，可双击图片或点击“全屏观看”进入独立全屏窗口：滚轮／双指缩放、拖动平移、左右键循环翻页、⌘0 适合窗口。I 开关图文对照，宽屏左右排列、窄屏上下排列；“简介／欣赏提示／原文”保留完整可选择文字。阅读时左右键切换栏目，Option+左右键可在已选择文字时切换；Esc 先关闭导读，再退出全屏。图库管理显示已安装版本与作品数，支持检查正式精选集、SHA-256 校验下载、断点续传及取消；完整校验通过才替换旧图库。

选择升级 BIN 时保留相邻 `.notes.json`；也可选择完整升级 ZIP。先预览版本、SHA 和说明，再显式开始升级。选择固件不等于已经写入。USB、Wi-Fi、BLE 共用验证与回执；原始数据传输完成也不等于新固件启动成功。

升级或首刷后，保持所选精确应用镜像，点 **核验所选固件的启动状态**。必须核对版本、ELF SHA、VALID 运行分区及三个递增的实时 UI 样本。旧设备版本不支持无线核验时使用 USB。屏幕、触摸和实际数据仍需要人眼确认。

## 电脑、音乐、Codex、照片和语音

自动定位天气通过系统反向地理编码显示城市／区县名，沿用现有大致坐标网格；同网格名称在内存缓存，解析失败保留已知名称，新位置无名称时显示“当前位置”，不阻断天气刷新。手动城市不变。

系统诊断提供 App 内网络探测，默认每 30 秒沿系统网络路径检查 ChatGPT／资源服务的 HTTP 响应与耗时，不携带账户凭据、不调用模型。PAC 未启用时明确显示“未配置 PAC”；启用时比较系统解析与实际请求的代理／直连类型，不据此判断 VPN 隧道或具体节点。默认网络样本超过 75 秒失效，最多保留十轮曲线；运行监控设置可修改间隔、阈值、代理预期和可选小流量测速。CPU 等待仍不支持等价队列计数，不用平均负载或线程总数代填。

设备中心按当前 Windows 模块顺序排列：TAB5 九项、ESP8266 四项、账号数据四项，桥接设置六项及开机启动。音乐权限归入电脑控制，Codex 权限归入常用任务。生日、日程、常用任务和按设备数据筛选持久保存并复用已有鉴权字段；股票支持搜索与排序。额度历史提供 7／30 天周额度增量和每小时五小时比例，缺失不补零。桥接工具包含登录项、校验下载、选择性配置迁移、提醒／勿扰／记录，以及可配置的运行监控。设置窗口统一 24 pt 外边距、滚动内容和固定操作区。实现与验收边界见[功能核对表](development/MAC-WINDOWS-FEATURE-PARITY-20261022.md)。

**交互与权限**记住已保存的开关，不在重启时重置。首次安装默认开启音乐采集、Codex 只读和蓝牙；天气未配置城市／坐标时默认自动定位。系统指标、账户额度、行情、日历和桌宠沿用已有自动采集；需要账号的来源复用已保存授权，未配置账号不产生虚构数据。电脑控制、音乐控制、任务发送、桌面输入框和语音输入单独启用，首次系统权限仍由用户确认：

| 功能 | 使用与边界 |
| --- | --- |
| 音量/静音/锁屏/常用入口 | 读取 CoreAudio 实际结果；最多四个本机程序或文件夹。设备不能传入任意路径或命令。锁屏需要辅助功能。Mac 本机临时解锁测试已实机通过；新增日常设置在验证密码及键盘后保存到当前账户／配对绑定的钥匙串记录，独立开启 Wi-Fi 解锁。r66 用户已报告连续两次 Wi-Fi 解锁成功，本机最近一次成功回执已核对；完整场景验收仍待完成。r59 防止 Computer Use 下的后台桌面焦点覆盖锁屏观察，也禁止用该冲突状态确认解锁成功；该干扰场景仍待实测。r60 保持普通 RPC 连接，修复本机“首包后连接关闭、后续请求失败”复现；r60 已有解锁成功实测，但两次随后被系统屏幕共享组件再次锁定。r61 增加断开后确认及单次显示器唤醒。用户明确拒绝修改全局策略；r62 候选改为公开 AX 登录框单次写入／提交，不建立屏幕共享会话，不支持接口则停止。先唤醒显示器再独立确认锁屏；整机睡眠仍需外部唤醒，当前固件未找到对应网络唤醒流程。r62 手动显示密码框后已填入但未解锁；r63 未找到默认按钮，已在填入前停止；r64 候选允许同一登录窗口内唯一且明确标注“登录／解锁”的可按按钮。r64 仍未定位提交按钮；r65 非空框只读采集已观察到明确的登录按钮；r66 已移除采集路径，改为绑定字段／窗口后只写入一次，再定位并按下一次登录按钮，失败不重填。r66 在密码页已显示时连续两次成功；用户确认无密码页、黑屏及 Computer Use 场景仍失败，后者还误报未锁屏。r67 增加限于签名登录进程的一次 Tab 准备，并保留锁定观察、以可选系统锁定标记否决桌面误报；证据不足返回未知；r67 无密码页实测仍失败。r68 已确认隐藏密码框也会报告已聚焦且所属窗口支持 AXRaise。r69 密码页已显示时成功，无密码页／黑屏后仍失败。当前 r70 取消置前动作，仅在独立核验锁屏目标后向公开系统输入队列投递一次无密码的 Tab，重新核验后再尝试原有单次填入／提交；r70 实测仍是密码框已显示时可解锁、未显示时失败；自动显示密码页未通过。已发现设备 BLE HID 能力，但 Wi-Fi／BLE 协同准备尚未实现，整机睡眠与 Computer Use 场景仍未通过。USB／BLE 不下发 Mac 密码，oneClickUnlock 保持 false。 |
| Apple Music / Spotify | 采集和播放控制分开。新安装默认只采集；升级保留原来已开启的组合音乐权限，后续可单独关闭控制。无需开启音量／锁屏等电脑控制。首次触发自动化授权。控制与已显示歌曲绑定，只发送一次，随后读取状态；未知结果不重复跳曲。 |
| Codex 回复和草稿 | 通过已安装 CLI 的标准 stdio 临时子进程，只读本机近期会话与保存的公开回复，关闭功能／退出桥接时结束，不安装后台服务。可选择已安装的 CLI。只投影会话标题、项目名与公开助手回复；后台活跃统计继续只读文件元数据。历史分页有界，未知桌面实时状态不猜测。目录最多读取 512 条本机未归档会话；设备帧优先携带最近 64 条，并在 32 KiB 限额内逐级收缩。每组超过 8 条时显示编号分组页，最多 24 个分组页；不代表无限历史或跨电脑同步。 |
| Codex 发送 | 只读历史连接不执行写入；另行启用操作后，经已验证的现有 Codex 桌面任务持有者连接发送／中断。保留原任务模型和审批设置，不在独立进程恢复真实任务。当前桌面追加接口不能绑定指定轮次，追加保持不可用。指定任务的实体发送／停止及停止后的空回复提示已通过实体复验。加密草稿保存在本机，审批／用户问题仍在电脑处理。 |
| 快捷控制台 | 另需辅助功能。写入和读回要求确认当前主输入框及目标会话。除固定输入框标识与精确任务地址外，支持可信主页面内的主 ProseMirror 容器：页面须提供精确任务地址，或与完整目录中唯一的完整标题匹配；缩略标题、侧栏行、重复标题和“已请求打开”回执不能授权输入框操作。排除提问卡片、嵌套网页及弹窗；不支持的结构或不可写的接口明确拒绝。全空白草稿直接填入；已有正文保留并追加。一次写入后有界回读，核对时仅容许末尾 CR/LF 的段落表示差异；发送仍绑定实际原始回读快照，不自动重复写入。此版本相关能力必须在目标 Mac 实测。普通 Codex 会话操作不依赖此输入框。 |
| 照片 | TAB5 摄像头上传后先验证完整 SHA、JPEG/PNG 解码和像素边界，最多三张绑定当前设备/任务。加密保存，只在确认发送时作为该任务附件。USB/BLE 分块与 Wi-Fi 整图均保留幂等校验。 |
| 语音 | 默认 Mac 原生本地识别，支持 TAB5 收音及所选 Mac 麦克风。豆包候选使用签名输入法、公共输入源／Fn、自有文字编辑器和实际输入 IO 状态核验；Mac／DJI 直接收音，TAB5 音频经用户单独安装的 VB-CABLE Mac 转送。停止后等待音频尾段及已提交的完整文字，不自动发送任务。Mac 实体麦克风已确认实际收音及最终草稿；TAB5 USB＋豆包已取得用户确认的成功结果；早先偶发中断未判定彻底修复。提速候选不再补播就绪提示前的准备音频，正式收音后的音频仍完整转送；请等“可以说话了”再说话。临时麦克风自动恢复失败会明确提示，并提供手动恢复入口。当前日常识别保留 Mac 本地识别。 |

## 首次安装、备份与恢复

从 **TAB5 → 首次安装与备份恢复**打开专用工具，不使用“小屏刷机”的 ESP8266 地址。

1. USB-C 数据线连接 TAB5；长按 RESET 约两秒，内部绿灯快速闪烁后松开，进入下载模式。[M5Stack 官方说明](https://docs.m5stack.com/zh_CN/guide/tab5/restore_factory)。刷新并手动选择下载口。
2. 首刷只接受完整 `AI-bot-tab5-…-first-install.zip`，不接受 OTA 应用 BIN。确认替换后，工具核对 ESP32-P4/16 MiB/安全状态/芯片修订、清单布局与全部 SHA，完整备份并与原机 Flash 比对，再核对硬件 MAC 后写入并验证。不自动重试写入，不绕过安全启动或加密。
3. 已安装 AI-bot 会在备份后停止首刷，转用正常升级。Flash 校验通过后短按 RESET，等实际画面启动，回 TAB5 窗口配对新串口；可在“资源与固件”选择同一首刷 ZIP，提取经过验证的精确应用来核验启动。
4. **只备份**生成 `.bin` 与 `.bin.json`；备份可能含私有网络设置，权限为当前用户读写，妥善保留。**恢复**选择同一设备的 `.bin.json`，验证硬件 MAC、16 MiB 长度和 SHA，先保存当前原机备份，再恢复并校验。Windows/Mac 备份元数据大小写兼容。不要仅凭文件名恢复。

串口与蓝牙在底层操作期间暂停，完成后恢复；保留 TAB5 当前展示设置。首次安装、恢复和实际 OTA 均未在本轮对硬件执行。

## CI 候选与现场验收

GitHub Actions 仅对测试分支运行：原生 `swift test`、Release、临时签名 App、ESP8266/P4 工具命令帮助检查，以及使用固定数据的 AppKit 界面 PNG。测试进程不创建正常 AppDelegate、不连接真实 Codex、不请求权限、不访问设备或 Keychain。PNG 是原生视图渲染，不是实机操作或屏幕截图验收。

CI 附件包括 xUnit、日志、工具链、源码 SHA、含 LICENSE/第三方声明的候选 App/可执行文件与 SHA-256 清单。候选为临时签名，未公证。`--self-test-flash-tool` 只执行工具版本/帮助，绝不刷机；`--self-test-tab5-ui <新目录>` 只渲染固定数据界面。正常使用需由用户将候选 App 放入应用程序目录并按 macOS 提示打开，本轮不执行安装。

集中真机验收顺序：USB 身份/拔插/恢复 → Wi-Fi/固定模式/八秒回退 → BLE 权限/独占/睡眠唤醒 → 展示与保存/资源/图库分页 → 音乐、音量和权限拒绝/恢复 → Codex 精确任务、草稿、真实发送/追加/中断/回执 → 照片与语音/事项/日期 → 经另行明确选择后验证 OTA、首刷/原机恢复。每项记录实际结果，不以构建通过代替。已确认现场现象逐项保留，问题修复后只补相关回归。

具体提交与 CI 证据见 [开发记录](development/MAC-TAB5-PORTING.md)。

2026-10-20 本机检查、实际固件包问题与待验收项目见 [现场验收记录](development/MAC-TAB5-ACCEPTANCE-20261020.md)。该记录区分工具链受阻、离线验证和正常 App／硬件结果。

行情代码按已规范化自选标识去除市场前缀显示，保留指数前导点、美股类别后缀和数字前导零；供应商的附加后缀或空代码不再抹掉显示代码。行情来源、价格、涨跌幅及自选顺序不变。

本地语音根据识别片段在音频中的时间范围保留停顿前的文字；同一时间范围的临时修正仍可替换，后续累计结果覆盖已有范围时避免重复，缺少时间信息时不猜测新分段。取消／切换麦克风后忽略旧回调。交互窗口保留最近识别异常与收音结束原因，不包含识别正文。长录音与偶发连接中断仍待本轮实机复验；不会自动重录或改用云端识别。

公开回复读取完整条目，保留同一轮前面的进展文字；已验证的桌面 owner 同时补充尚未保存的公开进展。仅导出 `commentary`／`final_answer` 文字，历史接口兼容无 phase 的公开助手消息，不导出思考、工具、用户消息或请求。自动更新跟随最新一页，手动历史／页码保持原位置；实体动态显示仍以最新验收记录为准。

本机 `0.163.0-alpha.10.2` 桌面默认 stdio 不创建公开控制 socket，且其 `app-server proxy` 转发原始 WebSocket 字节，不能直接交换 JSONL。当前候选改用标准 stdio 的临时只读子进程；只读连接状态及限制显示在“交互与权限”。正常 App／TAB5 验收以[最新链路记录](development/MAC-TAB5-CODEX-LINK-20261020.md)为准，不部署 daemon。

---

# English

This unpublished Apple Silicon/macOS 13+ candidate lives on `codex/mac-tab5-phase1-ci`. It does not replace public downloads or imply full hardware acceptance. The 2026-10-20 acceptance record is linked below; the 2026-10-22 module-parity candidate is run locally at the user's request for inspection. This work does not flash devices, merge main, create tags or publish a Release. Intel support remains unverified.

Click the menu-bar status to open **Device Center**, also available in its context menu. Following the Windows layout, **My devices / Account data / Bridge settings** separate device controls, shared data and bridge tools. Select TAB5 or ESP8266 in the sidebar; TAB5 **Connection and upgrades** groups USB pairing, Wi-Fi and Bluetooth. USB-pair the explicitly selected AI-bot device, then provision Wi-Fi over USB. Local discovery requires the same LAN; Bluetooth defaults on for fresh installs and retains any saved opt-out; first use still requests system permission. Read actual display settings before editing and require the matching device receipt before reporting success. Preserve TAB5 display preferences during upgrades and validation.

For a system Bluetooth numeric-comparison prompt, compare the Mac and physical TAB5 displays and confirm both ends. The firmware also exposes a HID keyboard service, so macOS may open Keyboard Setup Assistant. You can quit the assistant; TAB5 status/settings transfer requires no keyboard-layout setup. A request to re-pair or delete an old bond is a separate operation.

The login-Keychain prompt for AI-bot Bridge and service `io.github.yaoyouzhong.AI-bot` is separate from Bluetooth pairing. Allow grants one-time access; Always Allow remembers access for that candidate and item, as described by [Apple](https://support.apple.com/guide/keychain-access/kyca1243/mac). A newly built ad-hoc candidate may need renewed authorization. The private draft/receipt encryption key is loaded once per process, including caching denial, so later actions do not repeatedly request the same key. The bundle script uses `AIBOT_MAC_SIGN_IDENTITY` first, then a 40-digit certificate fingerprint in `~/Library/Application Support/AI-bot/local-code-signing-identity.txt`. This machine-local file is excluded from source and packages. A configured identity fails closed on signing errors; it never silently falls back to ad-hoc. Without either setting, signing remains ad-hoc. A fixed signer preserves the designated requirement, and this host retained Accessibility and Location grants across changed builds. However, its self-signed apps remain identified by per-build `cdhash` Keychain partitions, so changed builds still prompt; fixed local signing does not guarantee password-free upgrades. First access to another item or a new permission can also require approval. Do not enable access for all applications.

**Screen saver gallery → Install gallery package** imports standard/HD gallery ZIPs with provenance, licensing, guides, image decoding and complete hashes. Device pet/music resources stop after acknowledged transfer. Select a BIN plus matching notes or an upgrade ZIP, review it and explicitly start. Boot verification requires the exact version/ELF, VALID partition and three advancing live UI samples; transferred bytes alone are insufficient.

Certificate-signed candidates bundle `AIBotKeychainHelper`, an on-demand child handling only this bridge's existing pairing/provider/private-store accounts through inherited anonymous pipes. It exits after one request, with no service or listener. Parent path, bundle ID and matching leaf certificate are verified, as is the helper signature; failures never switch to another access path. Its signed bytes are retained in a private build cache keyed by source, toolchain, architecture and certificate, keeping its Keychain CDHash stable across ordinary bridge updates. First migration or helper changes can still require per-item approval on the Mac; see the development record for actual cross-build acceptance. Credentials remain persisted only in Keychain, without exporting, consolidating or changing existing item permissions.

Codex exact navigation confirmation uses an accessible route, trusted primary document's unique full title, or the current thread row's unique accessible name. Loaded unnamed chats can use a 60-UTF-16-unit compact label; that label is independently collision-checked against the complete catalog, never accepted by a target-only prefix or project/recent subset. Explicitly duplicated full or compact names use Windows' app-foreground open-only acknowledgment: the request stays bound to the chosen task ID, while the app states that exact page identity was not read back. This cannot authorize reading, staging, clearing or submitting a composer. Database failures, external pages and generic window titles cannot prove exact identity. The clipboard is not accessed. Tasks use native Mac recent activity (`recencyAt`, falling back to valid `updatedAt`) in descending order; duplicate display names include that time and unknown times sort last. The existing device `updatedAt` carries this sorting time so its own quick-console sort does not restore record-update order. Timestamps never replace task IDs, and the device still keeps order fixed during a visit.

When the API omits an explicit chat name, its exact task ID selects the local desktop catalog's preview title instead of a generic chat label. Explicit names take precedence; metadata failures retain the last successful names for the process. This only supplements display labels of already loaded tasks, without adding tasks, reading conversation bodies or establishing navigation identity.

The gallery browser searches painting/calligraphy by title or author, locates a date, and displays pages, guides and provenance. HD full-bundle imports verify every source hash, crop bounds and embedded standard ZIP, activate an entire category atomically and retain the previous package. Verified originals are composited up to 16 megapixels; unavailable originals fall back to a verified device preview. The separate fullscreen viewer supports wheel/pinch zoom, drag to pan, wrapping Left/Right page navigation, Command-0 fit, and I to toggle image/text comparison. Chapters (introduction, viewing notes, original text) remain selectable and complete, beside the image on wide screens and below on narrow screens. Reading uses Left/Right to switch chapters (Option-Left/Right also works with selected text); Escape first closes reading, then exits fullscreen. Gallery management displays installed versions/counts and supports official release checks, checksum-verified downloads, resume and cancellation. The old collection stays active until validation completes.

Device Center follows the current Windows module order: TAB5 has nine cards, ESP8266 four, Account data four and Bridge settings six plus login startup. Music authorization lives under Computer control; Codex permissions under Pinned tasks. Birthday, agenda, pins and per-device data selections persist locally and use existing authenticated device fields. Stocks have a separate search/reorder editor. History offers 7/30-day recorded weekly increments and hourly five-hour percentage samples, with unknown periods preserved. Bridge tools include login items, verified update downloads, selective configuration migration, notifications/DND/history and configurable runtime monitoring. Settings share 24 pt margins, scrolling content and fixed actions. See the [implementation and validation matrix](development/MAC-WINDOWS-FEATURE-PARITY-20261022.md) for verified scope and pending physical/account acceptance.

**Interaction and permissions** independently enables CoreAudio volume/mute, whitelisted local launchers, lock, Music/Spotify, Codex and voice. Physical lock and the one-use local unlock test have passed human acceptance. The saved Wi-Fi unlock candidate verifies account/keyboard input before storing a current-user/pairing-bound Keychain record, with a separate enable switch. The user reports two consecutive r66 Wi-Fi unlock successes, and the latest local success receipt is verified; broader acceptance remains pending. r59 prevents background desktop focus during Computer Use from overriding an observed lock or falsely confirming unlock success; that interference scenario remains unverified. r60 keeps ordinary RPC connections open after reproducing a failed second request on the old connection; r60 physically unlocked the Mac but two attempts were immediately relocked by the system screen-sharing agent. r61 adds confirmation after disconnect and request-scoped display wake. The user explicitly rejected global policy changes. r62 instead attempts one public AX secure-field write/submission without a screen-sharing session, stopping when unsupported. Display wake now precedes independent lock verification. Full-system sleep requires an external wake source; no corresponding network-wake flow was found in the current firmware reference. After manual field reveal, r62 populated the password but did not unlock. r63 found no default button and stopped before writing; r64 permits a unique explicitly labelled login/unlock button in the same window; r64 still found no login button. The r65 nonempty-field probe observed an explicit login button. r66 removes the probe and binds the field/window before one write, then resolves and presses the login button once without input replay on failure. Two r66 unlocks succeeded with the password page prepared; no-field, black-screen and Computer Use scenarios failed, with a false unlocked status in the latter. r67 adds one Tab preparation bound to the signed login process, retains lock observations and uses an optional system lock hint only to veto desktop claims. Insufficient evidence remains unknown; the r67 no-field test still failed. The r68 probe confirms that a hidden password field can report focus and its window supports AXRaise. r69 succeeds with the password page present but still fails without it or after a black screen. Current r70 replaces raising with one password-free Tab through the public system input stream after independent lock-target verification, then revalidates before the existing single write/submission. Physical r70 testing still succeeds only with the password field visible; automatic reveal failed. BLE HID exists, but Wi-Fi/BLE preparation is not implemented; full-system sleep and Computer Use remain unaccepted. Mac passwords are never exported to USB/BLE and oneClickUnlock remains false. The music opt-in independently authorizes playback controls without enabling volume, lock or launchers. Music commands bind to the displayed track, execute once and read back actual state. First use requires the relevant macOS Automation/Accessibility permissions. See the [new integration record](development/MAC-UNLOCK-DOUBAO-INTEGRATION-20261023.md).

System diagnostics now probe ChatGPT/resource HTTP response and latency along the system network path every 30 seconds inside the app, without account credentials or model calls. Absent PAC is explicitly not configured; configured PAC compares resolved and observed proxy/direct types, without inferring a VPN tunnel or exact node. Network samples expire after 75 seconds and retain at most ten actual rounds. An equivalent public CPU waiting-queue counter remains unavailable; load average and total threads are not substituted.

Automatic weather location resolves city/district names through system reverse geocoding using the existing approximate coordinate grid. Names are cached in memory for the same grid; lookup failure preserves a known name, while an unnamed new location displays the generic current-location label without blocking weather refresh. Manual city settings remain unchanged.

Collection preferences persist across restarts and explicit opt-outs are preserved. Fresh installs enable playback collection, read-only Codex and Bluetooth; weather defaults to automatic location only when no manual city/coordinates are configured. Existing automatic metrics, quotas, stocks, calendar and pets remain automatic. Account sources reuse stored authorization. Music control is separate from collection; upgrading retains an existing combined music grant. Computer control, task sending, desktop composer access and voice input remain separate opt-ins. First-use OS authorization still applies.

Codex uses the installed CLI's standard stdio endpoint in an app-owned temporary read-only child, ending on opt-out/exit with no service installation or listener. It projects recent titles/projects and saved public assistant messages through bounded pagination; background activity remains metadata-only and unavailable desktop runtime remains unknown. The catalog reads at most 512 unarchived local chats; device frames prefer 64 recent chats and shrink to fit 32 KiB. Folders larger than eight chats use numbered picker pages, with at most 24 entries. This is bounded local history, not unlimited or cross-computer synchronization. Device/task-bound drafts and images remain encrypted. The read-only child never mutates tasks. A separate opt-in routes send/interrupt through the verified existing desktop owner, preserving the task model and approval settings without independent resume. Installed steer cannot bind an expected turn and remains disabled. Physical send/stop on the designated task and the subsequent empty-reply display repair passed acceptance. Desktop composer staging verifies the focused main editor and target chat. In addition to a fixed editor identifier plus exact task URL, it supports the main ProseMirror container in a trusted primary document with an exact task route or a full title that is unique across the complete catalog. Compact labels, sidebar rows, duplicate titles and open-only acknowledgments never authorize editing. Question cards, nested web areas and dialogs are excluded; unsupported structures and non-writable Accessibility interfaces are rejected. Whitespace-only drafts are replaced directly; existing body text is preserved and appended. One write is followed by bounded readback, allowing only terminal CR/LF representation differences during reconciliation. Submission remains bound to the actual raw snapshot; writes are never automatically repeated. Handle approvals on the computer.

Photos use authenticated Wi-Fi or USB/BLE chunks, complete SHA/decode/dimension validation, and at most three attachments bound to the selected task. Native on-device Speech remains the default. The Doubao candidate uses the signed installed app, public input-source/Fn events, an app-owned editor and actual input-IO verification (macOS 14.2+). Physical Mac/DJI capture is direct; TAB5 audio uses independently installed VB-CABLE Mac, without speaker fallback or system-default changes. Stopped capture, drained audio/tail and committed stable text are required before final review; tasks are never automatically sent. Real Mac microphone capture and a final draft are confirmed, and the user has confirmed successful end-to-end TAB5 USB/Doubao attempts. Earlier intermittent interruptions are not declared fixed. The latency candidate acknowledges pre-ready probes without replaying them later, while retaining all audio after capture readiness; wait for the ready cue before speaking. Failed temporary microphone restoration is reported with an explicit recovery control. Daily recognition remains on native Mac Speech. Both modes discard late callbacks after cancellation.

Use **First install and backup/restore** for the 16 MiB ESP32-P4. Connect USB-C, hold RESET for about two seconds until the internal green LED flashes rapidly, then release and select the download port. Accept only a complete first-install ZIP, validate the reviewed layout/chip/revision/security/hashes, read and verify the entire current Flash, recheck hardware identity, then write once and verify. Existing AI-bot installations are directed to upgrade after preserving their backup. Reboot manually, pair the application port and select the same first-install ZIP in the firmware tools to verify its exact application boot. Restore accepts only a verified `.bin.json` belonging to the same hardware MAC and backs up the current Flash first. Keep private backups securely; Windows/Mac metadata are interoperable.

CI performs native tests, Release, an ad-hoc signed app, non-device tool-help checks and fixture-only AppKit PNG rendering. It does not connect to a real conversation, device or Keychain, or request permissions. Artifacts include source SHA, reports, licenses and SHA-256 checksums; the app is not notarized. Hardware/permission/audio/display/OTA acceptance remains separate, following the ordered checklist above and the linked development record.

The [2026-10-20 local acceptance record](development/MAC-TAB5-ACCEPTANCE-20261020.md) distinguishes the local toolchain blocker, actual package-loader repairs, offline checks and pending normal-app/hardware acceptance.

The installed desktop defaults to stdio without creating a public control socket. Its proxy relays raw WebSocket bytes and cannot directly exchange JSONL. This candidate instead uses standard stdio for temporary read-only access, displaying its status and restrictions in the interaction window. See the linked follow-up for current normal-app/TAB5 acceptance. No daemon is deployed.

Public replies retain earlier progress from complete persisted items and supplement unsaved public progress from the verified desktop owner. Only published commentary/final text is projected; persisted assistant messages without a phase remain compatible. Reasoning, tools, user messages and requests are excluded. Automatic following selects the latest page; explicit historical turns/pages stay pinned. Physical live-update acceptance remains tracked separately.

Local speech retains earlier utterances using their positions in the audio, allows corrections within the same range and avoids duplication when cumulative results cover previous ranges. Missing timing does not imply a new utterance. Cancelled/switched microphone callbacks are ignored. The interaction window retains the latest recognition failure and capture-end reason without transcript text. Long-recording and intermittent-disconnect acceptance remain pending; capture is not automatically restarted or sent to cloud recognition.

Stock display codes come from normalized watchlist symbols without their market prefix, preserving leading index dots, share-class suffixes and numeric leading zeros. Empty or suffixed provider codes cannot erase that identity. Quote source, prices/changes and watchlist order are unchanged.


## 可信电脑绑定兼容 / Trusted-computer compatibility

Mac 候选支持 0.2.236 的 USB 主机绑定 v1，以及 0.2.237 声明的蓝牙短归属证明。首次追加 Mac 必须明确点击“配对所选 TAB5”；保留原 Keychain 密钥和其他可信电脑，不自动重绑。已绑定 USB 经一次性挑战和签名回执校验后接管，端点改变重新认证。蓝牙公开归属提示仅用于待命判断，仍需密钥认证及能力声明要求的短证明／签名 ACK，之后才进入 USB 待命或处理请求。旧固件不声明能力时保持原路径。配对失败或结果不确定不显示成功。

The Mac candidate supports USB host-binding v1 from 0.2.236 and the optional short BLE ownership proof advertised by 0.2.237. First enrollment requires an explicit USB pairing action and retains the existing Keychain key and other computers. Trusted USB ownership requires a one-use challenge and verified signed ACK, refreshed when the endpoint changes. A public BLE owner hint never grants access: key authentication and any required short proof/signed ACK precede standby or requests. Legacy firmware keeps its existing capability-based path. Uncertain enrollment is not reported as successful. Cross-computer physical acceptance is recorded separately.


## 2026-10-22 Windows 开发分支增量

本地 Mac 候选参考 Windows `codex/windows-tab5-multihost-20261021` 的 `79ffe7f`：设备中心按用户反馈采用 920×620 紧凑窗口，模块随宽度排列为三列或两列，按钮宽度最多 240；额度历史标记跨日补记并显示采样范围；“桥接设置 → 运行监控 → 设备诊断”可读取 TAB5 实际运行数据。USB 自动采样，无线读取需要 0.2.250-ui 或更新固件，超时保留旧数据并标出年龄。此入口不改变当前电脑归属或设备展示设置。USB 电脑认证失败时阻止发起固件升级。

The local Mac candidate follows Windows development commit `79ffe7f`: a compact 920×620 device center with adaptive two/three-column cards capped at 240 points, explicitly partial cross-midnight quota carry-over with observation bounds, and a Device Diagnostics tab under Bridge Settings → Runtime Monitoring. USB samples update automatically; authenticated wireless diagnostics require firmware 0.2.250-ui or newer. Failures retain visibly aged samples without changing host ownership or display preferences. Failed USB host authentication prevents initiating an upgrade.

## 连接升级 / Connection and upgrade

Wi-Fi 页读取设备保存的网络（最多 10 项），支持新增、保存并连接、仅保存备注及确认后删除。密码只经过已认证 USB 发往设备；读取失败保留上次列表。固件页集中选择／停止提供、开始升级、实际启动核验与更新说明；新设备首次安装仍在 USB 配对页进入。传输测速提供“图库写卡”和“通信传输”，沿用 Windows 的已登记协议；开始请求不自动重发，结果读取可重试，停止或超时会请求设备结束。图库写卡需支持 `gallerySpeed` 的固件及本机图库，测速通过 USB 控制、统计设备实际报告，不使用模拟数值。

The connection window has Wi-Fi, USB pairing, firmware upgrade, voice and transfer benchmark tabs. Saved Wi-Fi management supports up to ten networks, notes-only edits, connect/save and confirmed deletion; passwords travel only over authenticated USB. Read failures retain the previous list. Upgrade selection, withdrawal, start, boot verification and release notes share one tab, while first installation remains under USB pairing. Gallery-to-card and RAM-only transport benchmarks use the existing Windows protocol. Start is never automatically resent; readback may retry, and cancellation/timeout requests device cleanup. Results come from actual device reports. Physical network/benchmark/upgrade acceptance is separate from desktop UI checks.


## r71 本地准备诊断 / Local preparation probe

用户要求只靠 Wi-Fi，BLE 协同方案已撤回。当前 r71 临时只检查“本机认证屏幕共享发送一次 Tab → 仍锁定时断开 → 三秒锁屏观察”，不会填入密码或提交登录，设备显示解锁未完成符合诊断设计。没有更改系统策略或固件；必须实测密码页能否在断开后保留，才能继续评估完整解锁。实体键盘输入能展开隐藏密码框不等于 AXValue 写入能展开。整机睡眠、Computer Use 状态识别仍未通过验收。

Wi-Fi is mandatory; the BLE proposal is withdrawn. r71 temporarily performs only one authenticated loopback Tab preparation, disconnects while locked and observes lock for three seconds. It neither fills nor submits the password, so the device reports an incomplete unlock by design. No global policy or firmware changes. The password page must physically survive disconnect before a complete unlock sequence can be evaluated. Physical keyboard reveal does not imply AXValue reveal. Full-system wake and Computer Use lock-state acceptance remain outstanding.


### r72 当前范围 / Current scope

r71 本机认证、Tab 发送及断开后观察均完成，但用户确认密码框未出现，准备方案未通过。r72 已移除该诊断和无效的系统 Tab 准备，恢复有既往实测成功记录的 AX 单次填入／按钮提交路径；需先在 Mac 显示空白密码框。仍只使用 Wi-Fi，请求与认证协议不变，设备不接收密码，不改全局系统设置。此恢复不代表自动密码页、整机睡眠唤醒或 Computer Use 状态误报已修复。当前不安排重复实测。

r71 authenticated, sent Tab and completed post-close observation, but the user saw no password field. r72 removes that probe and ineffective system-Tab preparation, restoring the previously successful single AX write/button submission path; a blank password field must first be shown on the Mac. Wi-Fi-only request/authentication semantics are unchanged, credentials stay local and global settings stay untouched. This restoration does not fix automatic page reveal, full-system wake or Computer Use state errors. No repetitive hardware trial is scheduled.


### r73 单次字符诊断 / One-use character probe

普通 Wi-Fi 解锁保留 r72 路径。仅本机“下一次仅测试字符触发…”按钮可准备两分钟有效的当前配对诊断；下一次已认证 begin 只发固定公开字符 a，不用密码、不按回车、不删除，用户自行清空。过期时该请求拒绝，不自动改为密码输入；重启不保留。没有新协议字段／设备能力，仍返回原有阶段和未完成结果。该实验检验可打印键与 Tab 的差异，不声称已解决自动密码页、Computer Use 或整机唤醒。没有驱动安装或全局设置改动。

Ordinary Wi-Fi unlock retains r72. Only the local one-use probe button arms a current-pair diagnostic for two minutes. One authenticated begin sends fixed public a without password, Return or deletion; the user clears it. Expired probes reject rather than entering credentials, and arms do not survive restart. No new protocol fields/capabilities; existing progress/incomplete responses remain. This tests printable input versus Tab and does not claim page reveal, Computer Use or full-system wake fixes. No driver installation or global settings change.


### r74 字符准备与完整解锁候选 / Printable preparation and complete-flow candidate

r73 实测公开字符使密码框出现，回执确认该诊断完成。r74 普通 Wi-Fi 请求先发一次 a，并等待同一密码框的登录按钮就绪；有计数接口时还核对零到一。准备未确认则停止，不发送密码。确认后用本机保存密码整体替换字段、按一次登录按钮，再确认桌面；不是在 a 后追加密码。不连接屏幕共享、不改全局设置、不用 USB／蓝牙或额外驱动，协议不变。完整流程实测待完成；黑屏、整机睡眠及 Computer Use 尚未验收。

The user saw r73 reveal the password field, consistent with its completed diagnostic receipt. r74 ordinary Wi-Fi requests send one a and await the same field's enabled login button, plus zero-to-one counts where supported. Unacknowledged preparation stops before credentials. Confirmed preparation is followed by full replacement with the locally saved password, one button press and desktop confirmation, never password append. No screen sharing, global settings, USB/BLE, additional driver or wire change. Complete-flow hardware acceptance is pending, as are display-off, full-system sleep and Computer Use cases.


r74 实机进展：用户确认普通纯 Wi-Fi 请求从未手动打开密码框的锁屏状态自动进入桌面，30 秒未回锁；本地回执也确认成功。当前运行未观察到整机睡眠，因此仅验收上述一次完整流程。明确黑屏起点、真正整机睡眠及 Computer Use 仍待分别验证；保留 r74，不修改全局设置或安装扩展。

r74 hardware update: the user confirms ordinary Wi-Fi unlock returned from the locked screen to the desktop without manually opening the password field and stayed unlocked for 30 seconds; the local receipt also confirms success. No system sleep was observed in this run, so acceptance is limited to that single complete flow. Explicit display-off start, actual system sleep and Computer Use remain separate pending cases. r74 is retained, with no global setting changes or extension installation.


r74 后续更正：用户明确确认黑屏后自动唤醒并解锁成功。中间“没有自动亮屏”的问卷答案已被用户更正，不计为失败；无需重复黑屏验收。真正整机睡眠与 Computer Use 仍未验证。继续保留当前 r74 和系统设置。

r74 follow-up correction: the user explicitly confirms wake and unlock from display-off succeeded. The intervening no-wake questionnaire answer was corrected and is not a failure; no duplicate display-off test is needed. Actual system sleep and Computer Use remain unverified. Retain r74 and existing system settings.


### r75 Computer Use 诊断 / Computer Use diagnostics

r74 在本助手实际运行 Computer Use 时已复现锁屏误报未锁屏。r75 不改变已成功的解锁输入或状态判定，仅在本机内存保留最多 12 次回包／通知／焦点／签名登录窗口显示元数据变化；不读窗口文字或截图，不传到设备、不写日志或磁盘。Computer Use 修复尚未完成，需对照真实复现；普通锁屏与黑屏成功记录仍保留。全局设置不变。

False unlocked status was reproduced while the assistant actually ran Computer Use. r75 preserves accepted unlock input and existing state decisions, adding at most 12 local memory transitions of replies, notifications, focus and signed login-window visibility metadata. No window text/screenshots, device export or file logging. The Computer Use fix remains incomplete pending evidence from reproduction; ordinary/display-off successes stand. Global settings are unchanged.

后续实测更正：助手曾将“ChatGPT 正在控制这台电脑”误解为已解锁，现撤回该结论。用户明确说明按任意键会返回要求密码的锁屏，无法直接进入桌面。即使通知、焦点和控制台采样显示会话未锁，也不能证明本机已解除保护；TAB5 误报仍未解决。需要独立识别活跃保护界面，避免覆盖成未锁屏；显示锁定不授权退出保护或输入密码。普通 Wi-Fi／熄屏解锁结果保留，全局设置不变，真正整机睡眠唤醒仍待验证。

Correction: the assistant's interpretation of the ChatGPT control cover as an unlocked machine is withdrawn. The user explicitly reports that any key returns to a password lock screen rather than an accessible desktop. Unlocked session notifications, focus and console samples cannot establish that local protection has ended; TAB5's false unlocked indication remains unresolved. Active protection needs an independent observation that prevents unlocked reporting without authorizing removal of that protection or password input. Ordinary Wi-Fi/display-off results stand, global settings remain unchanged, and actual system-sleep wake is still unverified.


### r76 外层保护候选 / Local-protection candidate

r76 补充已签名 Computer Use 保护程序的在屏窗口检查，避免会话“已解锁”通知覆盖仍受保护的本机屏幕。肯定的全屏高层保护窗口显示锁定，元数据不足显示未知，进程常驻不直接判锁。保护存在／未知时否决密码输入和解锁成功确认；不退出保护、不绕过认证，不改全局设置。公开 API 只读取限定元数据，记录固定状态和计数，不读窗口内容。普通 r74 流程保留；本候选仍需实机验收，尚未宣称修复完成。

r76 adds signed Computer Use protection-window observations so an unlocked-session notification cannot override an independently observed protected local screen. A verified full-display high-level cover reports locked, incomplete evidence reports unknown, and a resident process alone is insufficient. Protection or uncertainty vetoes credentials and successful unlock confirmation without dismissing protection, bypassing authentication or changing global settings. Public APIs read bounded metadata and record only fixed states/counts. Ordinary r74 checks remain. Physical acceptance is pending.

r76 本机验收：Release／签名及 Debug、签名应用各 458 项隔离检查通过；完整 XCTest 因缺模块未能执行。用户确认 Computer Use 外层保护期间 TAB5 已正确显示“已锁屏”，桥接保留记录确认保护窗口覆盖了会话未锁通知。普通纯 Wi-Fi 解锁随后复测成功，30 秒未回锁。外层保护期间的解锁请求在输入前主动停止，该场景仍不支持，不能记为全流程通过。没有全局设置、固件、密码或设备展示配置改动。

r76 local acceptance: Release/signature verification and 458 isolated checks in both Debug and the signed application passed; full XCTest remains unavailable. The user and retained bridge observations confirm locked status during the Computer Use protective outer screen despite unlocked-session notifications. A subsequent ordinary pure Wi-Fi unlock succeeded and stayed unlocked for 30 seconds. Unlock attempts while outer protection remains are stopped before input and are still unsupported; this is not complete acceptance of that unlock scenario. No global settings, firmware, credentials or device display preferences changed.

后续交接调查：在“仅点 TAB5、全程 Wi-Fi、不碰 Mac、不改全局设置”的约束下，尚未找到公开且可核验的 Computer Use 保护退出／交还锁屏接口。指定任务的取消回执不能代替交接完成。保留 r76，不将此场景记为已解决；调查依据与继续开发所需接口见[集成记录](development/MAC-UNLOCK-DOUBAO-INTEGRATION-20261023.md#外层保护交接接口调查--protected-screen-handoff-investigation)。

Handoff follow-up: no public, verifiable Computer Use protection-release interface was found that satisfies a TAB5-only, Wi-Fi-only action without Mac interaction or global changes. A task-cancellation receipt is not a completed screen handoff. Keep r76 and this scenario unresolved; see the linked integration record for evidence and the missing interface needed for further implementation.

### r77 蓝牙语音传输候选 / BLE voice transport candidate

用户更正当前停止至文字耗时为 BLE 4–5 秒、Wi-Fi 1–2 秒，使用 Mac 本地识别。r77 提高已协商状态／增量帧的压缩强度，并把大分片后台发送的确认窗口从 5 调至 8；保留内容、刷新频率、背压、末片与签名整帧确认以及语音就绪探测。补充后台分段诊断。Release 与 70 个临时断言适配器测试方法通过；完整 XCTest 因缺模块未运行。实机耗时待复测，尚不宣称延迟问题验收完成。识别选择、固件与 TAB5 展示设置保持不变，详见[验证记录](development/MAC-TAB5-BLE-LATENCY-20261024.md)。

The user clarified current post-stop latency as BLE 4–5 seconds versus Wi-Fi 1–2 seconds using Mac local recognition. r77 improves negotiated status/delta compression and changes large background packet barriers from five to eight fragments, preserving content, cadence, backpressure, final/signed whole-frame acknowledgments and voice-lease probes. Background stage diagnostics are added. Release and 70 assertion-adapter test methods passed; full XCTest remains unavailable. Physical latency improvement is pending and is not yet accepted. Recognition selection, firmware and TAB5 display preferences remain unchanged; see the linked validation record.

r77 后续：用户实测文字完整但超过 5 秒，提速未通过。传输实验已撤回，正常恢复 r76；只保留后台诊断细分和旧断言修正。用户明确接受原版 4–5 秒，将进一步提速留待后续，优先继续偶发中断／发送失败、额度异常与日历年度更新。固件、识别提供方和展示设置不变。


r77 follow-up: the user received complete text but waited over five seconds; the speedup failed acceptance. Transport experiments were reverted and r76 restored normally, retaining only bounded background diagnostics and a stale assertion correction. The user accepts the original 4–5 seconds and defers further tuning, prioritizing intermittent capture/send failures, quota history and annual calendar updates. Firmware, recognition provider and display settings are unchanged.

## r78 · 快捷发送连接复核与年度日历

候选路径：`artifacts/mac-remaining-20261024-r78-offline/AIBotBridge.app`。修复桌面连接验证缓存过期后直接拒绝发送的问题；快捷发送先只读复核，再处理草稿，明确只读与发送授权检查、会话身份和防重复回执保留。增加 Mac 后台节气／假期年度更新、完整年表校验、原子缓存和失败保留；继续复用现有 TAB5 字段。旧录音中断未认定彻底修复，“昨日额度为 4”的上游旧案例在当前 Mac 样本未复现。蓝牙传输沿用 r76。完整记录及验证边界见 [后续修复记录](development/MAC-REMAINING-FIXES-20261024.md)。

The r78 local candidate revalidates expired desktop availability before quick submission, preserving explicit authorization, task identity and at-most-once receipts. It adds background annual solar-term/holiday updates with validation, atomic caching and last-good retention using existing TAB5 fields. Historical recording interruptions are not declared resolved, and the old upstream quota case was not reproduced in current Mac samples. BLE transport remains the r76 implementation. See the linked record for validation and remaining physical acceptance.


r78 部署补记：用户确认额度显示目前正常；随后本机界面可操作，已正常退出 r76 并启动最终 r78。USB 数据、认证 Wi-Fi 与 BLE USB 优先待命均在原生界面确认；Mac 本地识别保留。真实 2025／2026 假期缓存更新成功，HKO 联网及实体快捷发送尚未完成验收，旧录音中断仍需观察。

r78 deployment follow-up: the user confirmed the quota display is currently normal. Once the UI became available, r76 quit normally and final r78 started. Native UI confirmed USB data, authenticated Wi-Fi and BLE USB-priority standby, retaining Mac local recognition. Public 2025/2026 holiday caches updated successfully; HKO download and physical quick-submit acceptance remain pending, as does diagnosis of historical recording interruptions.

## 最新检查点：Wi-Fi 观察与音乐封面

用户已明确把未再复现的 Wi-Fi 持续中断列为后续观察，授权继续统一生成固件。随后新增 Apple Music 封面缺失、倒置及上一首封面残留，故先补音乐修复。Mac 已有曲目绑定诊断版、方向及后备读取源码和 1191 项隔离检查；Release 完整打包和实体封面匹配／方向验收仍进行中。TAB5 保持 `.267`，尚未生成统一镜像。最新状态以 [当前验收记录](development/MAC-CURRENT-ACCEPTANCE-20261024.md) 为准；旧历史待办不覆盖新结果。

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
