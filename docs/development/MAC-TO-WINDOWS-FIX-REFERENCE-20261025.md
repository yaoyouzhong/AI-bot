# Mac 桥接修复回顾与 Windows 对照（2026-10-25）

最新集中交接请先读 [Windows 开发任务与验收清单（2026-10-26）](WINDOWS-FOLLOWUP-FROM-MAC-20261026.md)，包含 R29–R36 的小屏／模型账号、赠送余额与账户隔离补充；下文保留原始测量过程。

这份记录供 Windows 桥接后续修复使用。它依据当前仓库两端实现及本轮实际验收，不能把 Mac 通过直接记为 Windows 通过。此前详细过程保留在 `MAC-CURRENT-ACCEPTANCE-20261024.md`；本次自动测试证据在 `artifacts/development/tab5-preview-stability-20261025/`，当前已安装的统一设备源码在 `artifacts/development/tab5-combined-279-r2-20261026/source/`。本地交付不代表公开发布。

## 2026-10-26 最新交付与明确差异

当前设备为 .279，Mac 已正常切换为签名 R26，查看回复修复已由原长会话实机确认。用户已通过 .277 准实时高清、.278 持续预览下音乐单次控制、.279 高清保持／及时切页／相机拍照重开／录音单次响应及连续文字／前台操作后高清恢复／诊断说明准确；Mac 两次关闭重开均恢复高清。本轮用户确认无蓝闪，长期稳定性不外推。新发现的长会话“查看回复”超时见下文，R26 已部署，用户确认原会话回复显示且不再超时。

| 问题与本轮结论 | Windows现状（仅静态审查） | 后续入口与验证边界 |
| --- | --- | --- |
| 高清raw内存失败、共享mailbox未发布忙、栈溢出及预览调度等待 | .274–.277为同一设备修复，升级共同固件即可使用，主机v2兼容 | `screen_preview.c`、`photo_transfer_crypto.c`、实际LCD采样头；需Windows真实设备测试，不能用Mac通过替代 |
| 主机等待使切页偏慢 | `Tab5ScreenPreviewForm.cs`仍1000ms检查，`Tab5ScreenPreview.cs`完整帧仍1000ms、unchanged仍2000ms。Mac已按已确认.277+对非BLE使用100ms；BLE／旧固件保守周期保留 | 在Windows接收Context增加兼容版本门槛后调主机节奏，保留单请求、原期限、尺寸／SHA、前台优先及同像素不重绘 |
| Wi-Fi已连接、Mac200回包但设备有效包停止，重连／重启均无效 | Windows `Tab5Service.Respond`原本已验证同nonce固件证明并启用现有压缩；无需把Mac此次补齐重复套入Windows | Mac修复后约26KB→11KB，设备有效包0→14继续增长，用户确认恢复。原失败阶段未唯一定位；若Windows重现应分别记录正文接收／解密／帧接受，勿直接重配 |
| USB兼容文本邮箱每轮100ms造成分片等待 | Windows `Tab5UsbRpc.cs`已有独立循环、完成后立即继续及协商二进制／ready-wait；Mac保留R22的活动预览调度修复 | 不照搬Mac队列补丁。额外Mac二进制实验已退出活动源码，未部署／未计实机通过，现行R25不包含它 |
| 电脑开始升级未确认、旧提示覆盖实际下载 | MacR25修正实际帧时钟45s／15s通告上界，保留原30s／10s截止；真实提供／已开始不再被旧过期提示覆盖，观察版本不等于精确启动通过 | Windows `RemoteOtaCommand`／`BootVerifyCommand`仍直接用Expires，`Republish`重用`_lastSnapshot`；应复查相同旧帧边界。Windows已有固定拒绝原因映射，Mac此次补齐。本轮 .278 已实际单次确认、设备回执、正常更新及精确启动全部通过；原失败根因未唯一定位，Windows仍需自己的实体流程 |
| 录音文字停顿换行、停止触摸迟钝、长请求阻塞保活 | 固件UI变更共同受益，Mac识别合并／USB保活为平台实现 | 参考下文逐层问题，分别验证Windows音频后端、终态交付与恢复，保留手工换行、草稿及不确定操作不重放 |

MacR25通过Release、1728项隔离（含33项实际升级模型合成回归）、27项真实窗口合成生命周期和16项HTTP回环检查，签名及Keychain helper一致；本机XCTest仍缺失，未记通过。.277通过104项真实mbedTLS、36项像素一致、68项实际worker／JPEG／vendor故障场景、真实LVGL完整／前台及完整P4／配置／7531摘要／保留区／实际更新说明检查。上述离线检查与Mac验收均不是Windows原生证据；本轮未改Windows源码，也未重复创建固件或公开发布。R25 基线 Mac 包位于`artifacts/mac-firmware-command-r25-complete-20261026/AIBotBridge.app`，设备候选仍使用固定latest。

Latest installed delivery:279/R26; reply-history repair is deployed and accepted on the original long conversation without timeout. The user accepts quasi-real-time HD page updates, recording priority/recovery and camera/photo feedback/recovery. Windows already has authenticated Wi-Fi packing and independent binary USB RPC; do not duplicate Mac-specific fixes. Its viewer still checks at1s with1s/2s next-frame waits, and its OTA/boot commands use absolute deadlines while Republish reuses a snapshot, so review compatible277+ cadence and old-frame bounds separately. R25's desktop-start flow now passes the legitimate278 update with one confirmation and exact boot verification; its33 model cases remain separate evidence. Windows source/native execution is unchanged; Mac and device evidence cannot substitute for it. No public release.

## .278 前追加发现：预览与音乐电脑状态争用

用户在 .277/R25 持续预览时看到反复“正在读取播放状态”，播放点击没有电脑效果。桥接 media 回执为空、USB 不中断，设备 RPC 最后错误 -1011（未发布邮箱忙）。设备 `rpc_exchange_admission` 对 `computer-control` 原本为 0，和持续分片可碰撞；这是设备共用路径，Windows 主机已有独立线程也不能代替设备准入。候选 .278-r2 使用 1 秒发布前等待，并在 `screen_preview.c` 发送下一个未发布分片前检查 `computer_remote_busy()`，电脑状态／显式操作完成后在原预览期限内继续。已发布未知结果不重放，归属、断连与录音／照片／升级保护保留。

旧／新实际 policy/exchange/mailbox 各 16／18 场景及 73 个真实 worker／JPEG／vendor UBSan 场景通过；包含分片间让位、超时不延长、录音和电脑归属变化抢占、无重放和分配回收。首次测试发现忙提示被新预检查拦截，已保留原终结错误提示行为并重跑通过。完整 P4、真实 LVGL、板级配置、7531 源摘要、12 个保留符号、正式镜像与实际说明预览均通过。 .278 已实际单次确认升级及精确启动通过，用户确认预览打开时播放／暂停两次均单次生效、状态稳定。Windows 后续需在共同固件上同时开镜像、播放／暂停单次点击并核对状态恢复。

The shared device path also needs foreground admission: continuous preview can collide with a zero-wait computer status RPC before any media command reaches the host.278-r2 bounds admission to1s and pauses unpublished preview work while computer control is active. Native production-path tests pass; the installed278 physical music regression passes with preview open and both controls responding once. No new wire format or Windows-native acceptance is implied.

## .279 追加：首个高清帧仍要求新的连续大块

.278 的音乐／预览并发已由用户实机确认通过，但预览重开后三次在原始像素申请阶段失败，稍后总空闲PSRAM仍约9MB。.276缓存只解决“已有raw跨帧复用”，未消除首帧重新申请1843200字节连续空间的需求。候选.279优先借既有 detached gallery spare 或 camera pool 的 idle完整帧，备用general heap保持；归还用现有gallery_release_buffer，保留照片、可见图和活动CSI帧均有原归属保护。

预览CPU写入借帧后，相机在原gallery所有者排空后、初次USERPTR入队前，必须做完整C2M清理和失效；失败按原路径在STREAMON前退出。81个worker/codec/vendor/实际pool函数场景及真实相机task+LVGL/V4L2模拟通过，包含128次低内存重开、64借还循环、四向逐点、三处缓存同步失败后恢复。完整 P4、正式镜像和实际更新说明通过；.279 已正常安装并精确启动核验。用户确认高清保持无内存提示、切页及时、相机两次打开和照片完整、录音完整连续且单次结束、前台操作后高清恢复；Mac 两次实际关闭重开恢复高清。Windows共用设备固件即可取得此修复，仍需在自己的主机上验证预览重开、录音、相机拍照与回归。

The first HD frame still needs a contiguous allocation even after the276 cache fix.279 borrows existing idle full-frame storage and safely returns it, while the camera cleans CPU-written frames before CSI ownership. Native production-path and full P4 checks pass; installed279 HD hold, page updates, viewer reopens, camera capture/reopen and recording recovery now pass on Mac. The278 desktop upgrade and music/preview concurrency have now passed on Mac, without substituting for Windows-native verification.

## .279 后追加：长开发会话“查看回复”超时

Mac `thread/turns/list(itemsView:full, limit:32)` 在这条长会话返回约 23 MB，超过 `MacTab5CodexRPC.Lines` 的 8 MiB 接收边界，实际设备请求得到 503。摘要视图只有 32 条公开项而 full 有 556 条，不能以摘要冒充完整历史。R26 改为 `itemsView:notLoaded` 轮次目录，按所选与最新轮次用 `thread/items/list(turnId, limit:16, sortDirection:asc)` 分批读取，仅保留公开 assistant 文字；原接收上限、1 MiB 公开文字上限、轮次归属、游标与时间边界保留。两轮只读实测最大回包 130781 字节，接口总耗时 94–97 ms；这不是 TAB5 显示延迟。R26 Release、2829 项隔离、27 项真实窗口合成生命周期、16 项 HTTP 回环及签名通过，helper 与 R25 一致；已正常退出 R25 并启动唯一 R26，USB 数据确认。XCTest 因 CLT 缺模块不可用；用户关闭重开一次“查看回复”，确认原会话回复已显示且不再超时。最终包 `artifacts/mac-codex-reply-r26-complete-20261026/AIBotBridge.app`。

Windows `Tab5CodexTasks.cs` 主路径已有 desktop owner／stored replies，与 Mac 路径不同。仅 API 回退仍有 `thread/read(includeTurns:true)`，`ReadOutputAsync` 使用整行 `ReadLineAsync`，单次调用 25 秒；没有查到 Mac 同款 8 MiB 行边界。因此不能直接声称 Windows 已有相同 503。应在已有授权和公开回复边界内检查长会话回退的内存／等待，以及 owner／stored 路径的完整文本、前后轮次与旧结果归属；若采用接口分页，应检测本机接口能力，并保留失败缓存，不扩大原文读取范围。参考 `MacCodexPublicHistory.swift` 和对应 validation，勿迁入本机真实会话内容。

The Mac failure is a measured oversized full-history response, not a preview transport timeout. Bounded turn metadata and selected-turn item paging preserve public text and limits. Windows has different owner/stored primary paths and an unbounded-line full-thread API fallback; review that fallback with long-history tests rather than claiming the Mac-specific 503 is reproduced on Windows. No Windows source changes or native acceptance are implied.

## 已在两端同步的预览修复

| 问题 | 处理方式 | Windows 状态与复验 |
| --- | --- | --- |
| 首帧能出，之后超时，切页更新不稳定 | 原请求硬上限仍为 30 秒；JPEG 首片前允许 15 秒无进展，编码期间设备重复既有认证 `begin` 保活；传输开始后 USB/Wi-Fi 5 秒、BLE 10 秒无进展仍放弃。旧请求不能覆盖新请求 | `Tab5ScreenPreview.cs` 已同步；生产模型 90 项通过。实际 USB/Wi-Fi 编码与切页待 Windows 实机 |
| 一次内存不足后一直低画质 | 标准／小图至少成功 3 帧且冷却 30 秒后重试高清；再次失败冷却加倍，最多 300 秒；高清成功重置。重试时保留最近完整画面 | 已同步，模型覆盖降级、冷却、恢复、原帧保留及硬截止；需实体内存压力验证 |
| 相同画面不停重绘 | 相同像素且尺寸相同才复用图像；横竖尺寸变化必须更新。状态文字未变不重复赋值。普通完成帧约 1 秒，未变帧约 2 秒再请求 | `Tab5ScreenPreviewForm.cs` 与 Mac 窗口均已修复；Windows Release 交叉编译通过，原生 WinForms 重开／最小化／横竖屏仍待 Windows |
| 预览与照片／录音／升级争用 | 设备优先前台操作，仅回收未发布 staging 与既有可回收隐藏缓存；保持照片、草稿和可见快照。JPEG 输出从 128 KiB 按实际需求增至 512 KiB | 设备共用修复；不能仅升级 Windows 就宣称内存问题已修好，需要配套 .273 |

代码入口：Mac `MacTab5ScreenPreview.swift`、`MacTab5ScreenPreviewWindow.swift`、`MacTab5ScreenPreviewValidation.swift`；Windows `Tab5ScreenPreview.cs`、`Tab5ScreenPreviewForm.cs`、`Tab5ScreenPreviewSelfTest.cs`。协议语义见 `docs/PROTOCOL.md` 的预览部分。小屏原有数据绘制预览保持原实现。

## Windows 优先补查的差异

### 1. 录音、长请求和 USB 心跳相互阻塞

Mac 曾出现录音提前返回、停止后显示连接中断、浏览任务时 USB 重连。发现并修复了不同层面的具体问题，不能合并成一个根因：

- 慢桌面请求执行期间继续原有归属维护；已准备好的 RPC 结果先交付，避免再被状态包拖延。
- 语音优先持续到当前 voiceId／nonce 的终态全部分片确认，不能在识别完成但设备尚未收完时提早结束优先。
- 只对已检查、身份匹配且支持现有增量的 .269 及以后接收器，让状态增量承担原 2 秒归属续期；首次、基线失效、旧版和未知版仍用完整状态。精确 ACK 才推进基线。
- 周期性主机地址解析曾在服务锁内阻塞；改为枚举数字地址并移出锁，随后复核监听状态。保持原 8 秒安全截止，不能靠无限加大超时掩盖阻塞。

Mac 入口：`MacTab5USB.swift`、`MacTab5USBVoicePriority.swift`、`MacTab5Service.swift`；回归：`MacTab5USBVoicePriorityValidation.swift`、`MacVoiceTerminalPriorityValidation.swift`、`MacVoiceSchedulingValidation.swift`。

Windows 已有独立 `RunUsbStatusAsync`／`RunUsbRpcAsync`，RPC 执行在单次串口事务锁之外；`Tab5UsbRecovery.InSessionAsync` 隔离旧连接；`LanPairing.cs` 已用接口枚举。这些不能机械照搬 Mac 单线程调度补丁。

**尚需补查／补测：**`Tab5Service.cs` 的状态与资源批次仍共用 `_usbGate`；当前检查的路径没有找到与 Mac“终态全分片确认后才释放”一致的语音优先租约。应记录长 RPC、状态续期、资源批次、终态分片的排队时间，复现后按 Windows 调度结构修复，避免资源连续占锁。验收需覆盖 20 秒含停顿录音、单次停止、完整终态、同时浏览长历史、封面传输和实际断线恢复。

### 2. 停顿自动换行

用户要求识别返回文字保持一段，停顿也不要新增换行。Mac `MacTab5SpeechTranscript.swift` 在识别片段层合并分段与内部换行，保留英文词边界、纠错、去重；不改用户手工草稿中的换行。用户连续两轮通过。

Windows `Tab5VoiceSession.Snapshot()` 当前将 `editor.Text` 交给 `BoundText()`；后者只限制 UTF-8 长度，未做同等段落归一化。**这是待修的行为差异，但 Windows 识别后端为不同的桌面输入链路，不能直接把整个编辑框换行删除。**应在“本次识别新增内容”边界归一化，覆盖中文停顿、英文单词、CRLF／Unicode 行分隔、识别纠错、取消及原草稿保留。

### 3. 音乐封面缺失、串歌、方向、黑线及同步慢

Mac：Apple Music 自己显示封面不代表其自动化接口一定返回当前曲目封面。修复包括校验曲目身份、丢弃迟到旧封面、目录回退仅接受可确认匹配结果；保留显示缓存但不能把上一首冒充当前封面。清晰度升级后，黑线通过同图 JPEG／无损对照定位到设备 JPEG 路径，最终随设备修复并由用户通过。后续优化采样及传输节奏，两段等待均获用户确认改善；播放控制最终单次操作通过。暂停约 1 秒进度差用户接受。

Windows 使用 `NowPlayingService.cs` 的 SMTC／浏览器／播放器来源，已有来源与曲目组合键、浏览器唯一匹配和封面缓存；不要照搬 AppleScript 或 Apple Music 目录查询。**需在 Windows 验证：**异步取封面期间切歌、同名不同歌手／专辑、无封面、来源切换、方向与 stride、资源确认时间。设备 JPEG 黑线修复两端共用，Windows 封面源异步竞态尚不能凭 Mac 验收关闭。

### 4. 解锁取消和断线后的残留输入

Mac 已修复取消后再次解锁前的输入残留处理、取消中止和会话边界；用户通过“取消停止→再次成功→30 秒不回锁”，USB 中断停止输入也通过。涉及 `MacHostUnlock.swift` 等平台专属实现。

Windows 不使用 macOS 锁屏辅助功能链路。参考行为是：取消／通道变化后停止后续输入，迟到旧请求不能继续；重试从受控空字段开始；只有实际状态能确认解锁。**Windows 需按自己的 HID／登录实现复验**，不要移植 Mac 的 AX、Keychain 或锁屏探测代码。

## 属于 TAB5 固件的共用修复

- 相机打开慢、满 3 张仍准备很久：设备先做容量和忙状态判断，局部显示／相机资源有界交接；用户已通过打开速度及立即满额提醒。单图和三图发送也已通过。
- 结束录音多次点击：设备接受停止按下事件；识别回填避免 LVGL 逐字符重建与大量草稿事件，保留光标、取消恢复和输入上限。.272 用户两轮通过。Windows 不应额外给同一停止按钮发送重试命令掩盖 UI 问题。
- 添加照片缺少反馈：新代码先实际呈现“正在添加照片…”，确认添加成功后显示“已添加 N/3 张”；失败不冒报成功。
- 连接诊断：只说明 Wi-Fi 与电脑桥接通信，分组布局已获用户源码预览确认；不把设备配对、Codex、语音服务状态混在 Wi-Fi 诊断里。
- 蓝闪：用户报告退出桥接后仍整屏周期蓝闪，重启恢复。历史显示回归及当前代码发现周期最大块 heap walk 和无线任务饥饿风险；.273 去除诊断整堆遍历、增加阻塞让出及正文期限。**尚未唯一确定蓝闪根因，也不能把候选修复写成长期实机通过。**诊断零值代表未采样，不能当内存耗尽；不重复已失败的盲目显示优先级调高。

上述设备修复使用同一 TAB5 固件，与使用 Mac 或 Windows 无关；配套桥接仍需要各自的状态／协议验证。ESP8266 未具备本轮实机，不外推小屏通过。

## Windows 验证与交接方式

当前 Windows 预览生产模型 90 项通过；实际 Windows 项目在 Mac 上 `dotnet build ... -c Release` 成功，0 警告、0 错误。这不是 Windows UI、音频、串口和蓝牙实机结果。`--status-once` 需要 WindowsDesktop 运行时，Mac 不能完成该运行验证。

在 Windows 固定 Release 路径正常退出、备份、部署后执行项目规定 build 与 `--status-once`，再按以上差异逐项复验。不改防火墙、配对或用户展示设置来“通过”测试；超时保留现场与数值诊断，不记录识别内容、图片、凭据或密码。公开发布、推送和标签仍需单独授权。

没有向其他聊天或人员发送消息。本文件就是可直接交给 Windows 开发工作的参考；这里明确未完成的项目必须由对应平台实机关闭。

## 本轮远程实机追补（2026-10-25）

Mac标准“窗口”菜单现复用托盘的设备预览选择；设备列表不变时保留菜单对象，避免辅助操作失效。托盘菜单及原小屏预览保持。该入口已实际打开TAB5预览。随后.273真实设备出现初次超时、异常重启和640×360降级，不能记通过。保留的RTC记录明确为 `screen_preview`、`SP_SPILL_MIN_RAW=256`，PC对应同一.273 ELF的`_svfprintf_r`。两个大录音状态对象嵌套在8KiB任务栈中；P4静态栈分别3152与2768字节。新候选将状态放到任务PSRAM并使用16KiB栈，两个函数分别降至544与16字节，增加数值余量观察。此为两端共用设备修复；Windows不用照搬Mac菜单代码。后续实际安装／高清／切页／重开结果按当前验收记录更新，尚未完成时不得关闭该项。


2026-10-25 .274 实机追补：Mac 已正常安装并通过精确启动与设置保留核验；预览最小栈余量11860字节，未观察到新的panic，支持设备栈修复。首帧高清后曾临时降为640×360，再自动恢复1280×720，因此清晰度／长期内存恢复不能记全面通过。切页和关闭重开尚因Mac再次锁屏未验。Windows仍须对应平台实机验证；不能把Mac的栈余量证据外推为Windows预览验收完成。

274 physical follow-up: normal installation, exact boot and unchanged preferences pass on Mac. Preview retains11860bytes minimum stack without a new observed panic, supporting the device stack fix. HD temporarily falls to640×360 before recovering; clarity and sustained memory recovery remain open. Page-switch/reopen tests await manual Mac unlock. Native Windows acceptance remains separate.


## 离线追补及 Windows 剩余差异（2026-10-25）

同一组2169个合成事件直接运行两端当前生产接收器，两端各2152项检查通过。30／60／120／240／300／300秒高清重试退避、三帧恢复门槛、旧帧拒绝、旧窗口stop不影响新stream、USB→Wi-Fi请求切换和30秒硬上限一致。测试使用合成RLE像素；Windows JPEG解码入口在便携夹具中明确抛错，未将其算入通过。真实设备worker／codec／vendor另外54项UBSan分配故障与清理用例通过。日志、生产文件SHA和限制在 `artifacts/development/tab5-274-offline-20261025/RESULTS.json`。

仍需Windows针对性处理的差异优先级：

| 顺序 | 入口与当前事实 | 后续工作与边界 |
| --- | --- | --- |
| 1 | `Tab5Service.cs:561`状态循环、`:589`串口门及资源批次；Mac `MacTab5Voice.swift:47`的needsTransportPriority延续到终态分片已交付 | 记录Windows门等待、心跳间隔及终态ACK，实际复现后按现有独立RPC循环调整。当前是待核查调度差异，不宣称Windows已复现同一根因，也不机械复制Mac串行循环 |
| 2 | `Tab5VoiceSession.cs:106`Snapshot直接BoundText(editor.Text)，`:109`仅做UTF-8长度限制；Mac识别段层保留单段 | 在识别新增内容的边界核对自动换行；保留手工草稿换行、纠错和取消。不能对整个编辑器做全局换行删除 |
| 3 | `Tab5ScreenPreviewForm.cs`处理Visible/Minimized/Dispose；生产接收器策略本轮离线一致 | Windows本机做实际窗口重开／最小化恢复、JPEG、USB／Wi-Fi／BLE复验；Mac窗口行为与便携模型不替代WinForms验收 |

.274的内存阶段诊断不是保留至重启的记录：后续成功将其清0。必须在失败任务完成时关联数值样本，不能用稍后0判定没有内存失败。本轮不增加高频整堆扫描，不猜测改分区，也不为了诊断写出实际画面或识别内容。Windows音乐和解锁平台差异仍按前文复验。

The same2169 synthetic events pass2152 assertions on each production host receiver. Retry scheduling, late-stream/window isolation, transport changes and hard deadlines match. The portable Windows fixture deliberately excludes native JPEG/UI/transports.54 actual device-worker/codec/vendor allocation/cleanup cases also pass under UBSan. Remaining Windows work is evidence-driven scheduling/terminal-ACK investigation, recognition-segment paragraph handling and native window/codec/transport acceptance. A successful later task clears the device memory phase; it is not a sticky failure history. No Windows hardware result or new firmware is claimed.


## .275：预览与任务读取争用（2026-10-26）

.274真实现场：TAB5已在Codex页且界面／连接心跳健康，预览请求6121→6129、超时20→27，而完整帧仅572→573；中途确实收到一张Codex页，随后再次停更。不能把它归为单纯画面去重、整个设备卡死或持续错误采样。请求交换阶段失败计数同期增长。

独立复现找到一处确定问题：设备`rpc_exchange_admission("screen-preview")`为100ms；任务读取共享USB mailbox，预览begin之后的frame在发布前遇忙就退出，主机仍等待这张帧直到无进展超时。实际旧／新exchange各18个USB/BLE场景证明：busy返回前没有发布请求，新代码用本地固定-1011区分；已发送后超时、断线、generation和owner变化不标为可重试。修复仅对尚未发布的busy在原30秒请求内重试，并检查前台语音／照片／升级抢占；不扩大交换期限，不重放已发布请求，不切换通道。完整真实worker/codec/vendor的60个UBSan场景通过，包含4秒任务读取前后争用、原截止、语音抢占、归属变化与不确定分片不重放；原unlock准入回归通过。

这是两端共用的TAB5固件修复。Windows不用照搬Mac菜单或增加自动重发；原接收器和预览线协议均兼容。实际Mac／Windows生产接收器的既有2152项分别通过证据保持，不能把源码复现和.275完整构建当作实体切页通过。固定镜像、源码和后续实机结果见`artifacts/development/tab5-combined-275-20261026/`及`tab5-preview-admission-20261026/`。

Physical274 observations show healthy device/page heartbeat but only one new complete Codex frame across repeated timeouts. Actual shared-mailbox exchange reproduces an unpublished100ms admission collision. Firmware275 distinguishes that local result and retries only before publication, within the existing owner/deadline and foreground preemption. Published uncertainty is never replayed.18 baseline/new exchange cases,60 actual worker/codec/vendor UBSan cases and original unlock admission regression pass. This common firmware fix needs physical acceptance separately; Windows requires neither Mac menu code nor unconditional retries.


.275 Mac实机收尾：正常升级仅一次，精确ELF／VALID／三个实时UI核验通过。用户明确Codex页10秒内跟随；两次关闭重开都重新取得1280×720，基本场景通过。第二轮同一流从11/12继续到18完整帧/21请求，超时和拒绝始终0，设备最小栈11760、显示错误／欠载0；已记录12次-1011准入遇忙，重试后继续出图，支持本轮共享mailbox修复。曾反复标准降级／高清恢复，持续高清仍待确认，不外推长期全部稳定或Windows实体通过。非页面设置回读一致；用户在验收切页中把固定页由额度改为桌宠，未写回覆盖。检查窗口收起，用户预览保留。

275 Mac acceptance: exact boot passes, the user confirms Codex follows within10seconds, and two close/reopen cycles receive actual1280×720 frames. One stream progresses11/12→18/21 complete/request counts with zero timeout/rejection and11760bytes minimum stack;12 unpublished busy results coexist with continued successful frames. This supports the mailbox fix for the observed scenario, not unconditional HD/long-term/Windows acceptance. Non-page settings match; the concurrent navigation-page difference is retained rather than overwritten.


## .276：原始截图大块申请失败与租约缓冲（2026-10-26）

用户否决.275清晰度，R19在USB ACK保留到任务404／phase1的真实证据：1280×720×2原始像素申请失败；之后成功把设备字段清0，旧诊断曾因此错过。总空闲约8.46MB不能代表最大连续块，未凭总量断言无内存问题。Mac只新增数值观察保留／去重／归属与重启边界，不保存图像；Windows可参考同样的数值证据处理，不能复制Mac AX或依赖其菜单。

共同固件.276提前预留并租约复用raw scratch，成功后保留、5秒闲置回收；录音／相机／OTA回收闲置资源，原子准入保护活动worker。拒绝或不确定请求不保留，所有未来请求仍重新采样并鉴权。既有v2协议、主机高清降级／恢复、原ESP8266预览不变。Windows无需无条件重发，也无需为scratch改变接收器。

实际worker/codec/vendor的66项UBSan、完整及前台专项真实LVGL、完整P4／配置／7531摘要／保留区／实际说明校验通过。首次镜像超400字节，仅RPC封装函数用-Os后原分区剩496字节，未改分区及crypto/transport/display/codec编译选项。R19 Mac Release／1587隔离／27窗口合成回归通过，helper不变；完整XCTest因CLT缺模块仍失败。共同固件.276已正常安装并精确启动核验；用户确认Mac高清及自动切页正常。USB完整帧13→23，超时／拒绝／显示错误／欠载均0。该结论不外推Windows原生设备或前台录音／相机回收实机。正式证据`artifacts/development/tab5-combined-276-20261026/`；范围补丁`docs/proposals/tab5-preview-buffer-reuse.patch`。

276 addresses a live phase1 raw allocation failure using early, lease-scoped scratch reservation/reuse with safe idle/foreground reclamation and fresh authenticated captures. It is a common device fix; Windows receivers remain compatible.66 actual native cases and full/focused real-LVGL/P4/package checks pass, and276 is now installed with exact boot verification; the user accepts HD clarity and automatic page updates on Mac. R19's numeric latch is a diagnostic reference, not evidence of native Windows acceptance. Long-term operation, foreground reclamation and native Windows acceptance remain bounded pending checks.


## Mac预览延迟计时与调度参考（2026-10-26）

同一.276／1280×720、约86–87KB JPEG的三次Mac基线完整请求为5727／5780／5782ms，其中请求排队675–722ms，begin至首片2831–2918ms，首片至完整2167–2274ms。稳定画面的unchanged回执不形成新的完整传输，不能用两张完整图之间的时间推算纯带宽或切页延迟。R20仅增加有界数值分段计时，不保存像素、身份或文本。

Windows已有独立USB RPC循环：完成请求后立即继续，空闲5／20ms，并可协商既有binary ready-wait；Mac当前兼容CDC实现每100ms只pump一个完整RPC。两端调度结构不同，不应把Mac连续pump修复机械抄入Windows。Windows可以参考测量边界以及窗口250ms检查／完整帧非BLE间隔250ms的候选策略；两端继续只对变化像素重绘，unchanged冷却不缩短，保留录音优先、原截止、SHA及尺寸检查。Mac R21实测减少排队和呈现等待，但单次frame仍约2.3秒；R22进一步仅在有效RPC完成后250ms窗口将USB检查缩为10ms，空读不延长、录音／配置／关闭／重连回到100ms。R22已实测完整请求3.958–4.122秒（原5.727–5.782秒），分片接收0.804–0.973秒（原2.167–2.274秒），高清1280×720保持，用户确认更新明显更快且高清正常。一轮真实关闭重开及后续6帧／7请求无超时或拒绝；未据此宣称Windows已修复或实机通过。

For the same276 device and1280×720~86–87KB JPEG, three Mac baseline complete requests take5.727–5.782s:~0.7s admission,~2.9s preparation and~2.2s reception. Unchanged acknowledgements are not full-frame transfers and their interval is not bandwidth or switch latency. Windows already pumps USB RPC independently with immediate continuation and existing negotiated ready-wait; do not copy Mac's queue-specific fix blindly. Timing boundaries, bounded pacing and faster viewer checks are references only; native Windows/runtime acceptance is not established.


## .277：准实时预览（2026-10-26，本轮用户验收通过）

R22已获用户“更新明显更快、高清正常”确认，但用户继续要求准实时。实测剩余约2.8秒在设备begin到首片之间。共同设备代码每1KiB SHA更新都延迟1tick，1.84MB原始图有1799次主动暂停。新预览专用入口保持SHA、完整字节、内部1KiB DMA上下文／缓冲，将调度检查改为最多16KiB或约1ms，并通过回调保留前台抢占、原期限与归属；照片和普通RPC节奏不变。实际mbedTLS104项输出一致性／内部边界／失败清零／取消用例通过，模拟1kHz下全HD暂停112次；不能把模拟tick直接当设备端实测耗时。

全尺寸采样还移除比例为1时的冗余64位除法；36项旧／新实际像素函数在全HD、非整块尺寸、四向旋转及缩略尺寸逐点一致。68项实际worker/JPEG/vendor UBSan故障及清理场景通过，新增校验中语音和归属变化抢占。真实LVGL完整及录音开始／结束／回填、相机、照片流程回归通过。P4完整构建、7531冻结摘要、相同板级配置、12保留区符号、正式镜像与实际sidecar预览通过；不使用新JPEG硬件DMA，不改原分区或JPEG质量。

Mac R23按已确认277+启用非BLE完整／unchanged后100ms等待；旧固件和BLE保留原周期。窗口100ms检查仍按像素去重，不重放旧请求或并行采集。Windows既有v2接收器无需协议升级；如要一致准实时体验，应参考同一固件门槛缩短其主机冷却／窗口检查，保留独立USB RPC循环，不抄Mac串口队列实现。设备已从TAB5正常升级入口安装并通过精确启动；Mac完整请求约2.0–2.3秒，用户明确认为达到准实时预期且高清正常。该结果不能记Windows或全部稳定性通过。

Firmware277's preview-only scheduling preserves complete SHA and internal1KiB staging, but yields after at most16KiB/about1ms with foreground/owner/deadline checks; photos and ordinary RPC retain their pacing.104 real mbedTLS cases,36 old/new pixel-fidelity cases,68 actual worker/codec/vendor fault cases and full/focused LVGL regressions pass. Tick savings are simulated evidence, not measured hardware latency. Full P4/config/source/retained-layout/package/actual-note checks pass. Mac R23 gates shorter100ms frame/unchanged waits on confirmed277+ while retaining legacy/BLE cooldowns and image dedup. Windows remains compatible and should independently gate equivalent host pacing without copying Mac's USB queue. 277 is installed and exact-boot verified; actual Mac complete requests are~2.0–2.3s and the user accepts quasi-real-time HD updates. This is not Windows or indefinite stability acceptance.
