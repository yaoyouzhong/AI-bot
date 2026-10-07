# TAB5 Codex 直达 / Codex Direct

## 当前验收状态 / Current acceptance

2026-10-06：.121 与配套桥接已安装，精确镜像启动核验通过；用户反馈“效果还是不错的，一切正常”，本轮日常语音优化通过。此前会话操作、追加草稿、光标及年度屏保的用户验收保持有效，不重复列为待验。主动失焦、断线及逐通道覆盖不由日常反馈推定通过。单次录音仍为 60 秒；延长到 3 分钟暂未实现。

.122 倒计时数字/单位加粗已安装并通过精确启动核验；用户认为颜色偏暗，视觉确认仍待完成，桥接未更换。下面保留各次实现时的历史记录，其中“尚未部署/待验”不覆盖本节最新状态。当前安装证据见 [TAB5 .121](TAB5-ACCEPTANCE-121.md)。

The paired bridge and .121 are installed and exact-image boot verification passed. The user accepted everyday voice behavior on 2026-10-06. Prior accepted session, draft, caret and annual-saver checks remain accepted. Deliberate focus loss, disconnections and per-transport coverage are separate. Recording remains capped at 60 seconds; three-minute support is not implemented. The .122 countdown-weight firmware is installed and boot-verified; final visual acceptance remains pending after the user reported its color too dark. The implementation-stage notes below are historical and do not override this current status.

## 历史实现记录 / Implementation history

语音缓冲修复：Windows 桥接在接收当前会话的 `audio` 分块前检查播放余量，200 毫秒音频块只在待播放量不超过 200 毫秒时写入。等待使用异步 20 毫秒步进，界面、取消和识别线程仍可推进；连续 1500 毫秒无余量则明确失败，不丢块、不扩大缓冲、不将截断结果自动填入。取消、焦点变化及旧会话请求仍按原身份校验。`audioWaitMs` / `audioWaitPeakMs` 及 `playbackBufferedMs` / `playbackPeakMs` 用于区分播放积压和识别收尾，不记录声音或正文。现有 .120 可直接使用桥接修复；.121 另含停止进度反馈、自动模式 USB 优先及 Wi-Fi/USB 16 kHz ADPCM。NAudio 内部复现和六秒积压完整性回归通过，真机延时改善尚待确认。

The bridge admits current-session audio packets only while playback backlog is at most 200 ms, keeping 200 ms packets within a 400 ms queue. Asynchronous 20 ms waits preserve UI/IME/cancel progress; no room for 1500 ms fails explicitly without dropping audio, increasing the buffer or auto-staging a truncated take. Existing session identity and cancellation checks remain. Queue/wait diagnostics contain timing only. The fix works with .120; .121 separately adds stop progress feedback. Real NAudio burst and integrity tests pass; hardware latency improvement is unverified.

2026-10-05 用户确认光标问题已解决。同时确认停止时提示立即变化、后续仍等待几秒：现有诊断记录识别收尾 1406 毫秒、一次 Codex 回填 2641 毫秒，未覆盖点击到回填的完整同轮时间线，不能据此推断丢失停止命令或网络积压。.121 本地候选在本地停止点击时立即将停止图标换成动态进度环，持续覆盖识别及回填，完成、取消或失败后隐藏并停止动画。保留现有收尾等待和尾音传输，尚未刷入。

The user confirmed caret placement fixed and clarified that stop feedback updates immediately but completion takes a few seconds. Retained diagnostics show a 1406 ms recognition final wait and a 2641 ms staging operation, not a complete correlated end-to-end trace. The .121 local candidate replaces the stop icon immediately with a progress ring through recognition and staging, stopping the hidden animation on completion/cancel/error. Existing finalization and tail delivery remain; not flashed.

语音回填光标修复：正文追加且回读确认后，配套 Windows 桥接重新核对目标、草稿与前台，把主输入框 TextPattern 文档末尾折叠为空选区并选中，回读确认起止端点均在末尾。保留原正文及段落，不输入按键或发送。用户正在按键、草稿变化或提供器不可用时不强行定位；已确认的回填不会因此被当作失败重做。诊断 `caretEnd=True/False` 只记录定位结果。桥接端改动，不需要再刷 .120；真实 Codex 光标效果待用户验收。

After verified staging, the paired Windows bridge rechecks foreground, target and draft, selects a collapsed TextPattern range at the main composer's document end and verifies both selection endpoints. No text or keyboard input is generated. Held input, changed drafts or unavailable providers prevent forced positioning; successful staging is retained to avoid duplicate append. Diagnostics report caretEnd without draft content. Bridge-only change; .120 needs no reflash and actual Codex caret behavior awaits user acceptance.

最新安装状态：2026-10-05 配套桥接与 .120 已完成部署、Wi-Fi 升级及 USB 精确启动核验；下方“本地候选、尚未部署”为实现时的历史记录，新增交互仍待验收。见 [.120 安装核验](TAB5-ACCEPTANCE-120.md)。 Latest installation: paired bridge and .120 deployment, Wi-Fi OTA and USB exact-image boot verification passed; candidate-only wording below records the earlier implementation stage. New interaction acceptance remains pending.

.120 本地候选新增打开会话后的草稿只读同步：配套桥接在 `open-recent` 成功且目标主输入框可确认时返回可选 `draftEmpty`（布尔或 null）。仅 `true` 恢复豆包语音按钮并撤销该会话旧发送凭据；`false`、null 或缺字段保留同一会话原状态。检查不聚焦编辑器、不写入、不发送；会话切换仍清除旧目标的设备发送入口。模拟回归已通过，尚未部署或真机验收。

The .120 local candidate adds read-only draft synchronization after opening a conversation. A successful `open-recent` may report nullable `draftEmpty`; only explicit true resets the voice key and retires the target's old send tickets. False, null and missing fields preserve same-target state. Observation never focuses the editor, edits or submits. Switching still discards the previous target's device send entry. Simulated regressions pass; deployment and hardware acceptance remain pending.

当前设备 **0.2.107-ui** 已通过精确镜像启动核验，`ota_1 VALID`、升级错误为 0；会话联动完整实机验收仍待用户确认。已安装 **0.2.107-ui** 将清空图标改为扫帚，并让升级进度跟随实际写入、单独显示校验耗时，保留等宽“长按清空”及初始隐藏逻辑。用户确认扫帚和本轮升级反馈均正常。安装 .107 使用旧接收程序，新写入进度在下一次升级生效，尚未独立实测该路径。.106 Wi-Fi 电脑发送约 12 秒、接收与写入阶段 17.995 秒、纯写入 9.621 秒、最终校验 0.879 秒，设备安装 18.993 秒（不含重启）；进度反馈修正不等于硬件上限或实际提速。配套桥接文件哈希匹配，原自动轮播的六页顺序与 15 秒间隔保留；用户此前确认真实首次录入、重新录音和补录追加保持未发送。该实机流程未主动注入写入异常，异常分支由回归测试验证。

此前 **0.2.100-ui** 已完成精确镜像 ELF 匹配、`ota_1 VALID` 与升级错误为 0 的核验；用户已验收按钮动效、实际麦克风全称和连续补录追加到电脑 Codex 草稿且保持未发送。快捷语音与 TAB5 本地编辑器分离，采用豆包输入法官网人物图标、绿色录音状态和圆圈叉号取消图标。

## 操作

- 各常规页面右下角常驻 Codex 图标入口，触摸区域 60×60 像素；底部导航为它保留独立位置并相应放宽。全屏拍照、屏保和升级流程覆盖入口，结束后恢复。
- 0.2.106-ui 面板每次进入时按更新时间初始化最近五条，默认选中最新一条；“上一条 / 下一条”直接请求电脑切换到对应会话，回执成功且目标一致后才更新 TAB5 选中项。本次打开期间不因切换而重排列表；重新进入时再刷新。切换期间锁定重复操作，失败保留原选中项并要求重新确认目标后才录音。
- 点击“Codex”打开或唤起当前选中的那一条，不再重新选最新；电脑切到其他程序后也返回这条会话。同一已确认会话再次唤起保留未发送的回填记录与发送/清空入口；成功切到另一会话后重置旧的发送入口，但保留电脑和 TAB5 原草稿。实际项目名、标题和位置从电脑回执同步。桥接以本机实时更新时间校正目录缓存，只包含桌面已知会话并保留桌面显示标题；归档会话和内部辅助任务不进入列表。Codex 未运行时通过注册入口尝试启动并打开指定会话；失败或目标未就绪仍提示失败。
- 两个主按钮各自保留拉丝金属护圈、侧壁和按压回弹，去掉共用大底座及重复名称，腾出底部空间。背景在正常、按下和禁用状态均透明。回填确认后，右键变为参考图的蓝色圆面（sRGB `#3A83F7`，屏幕转换为 RGB565）和几何居中的 10 像素粗白箭头；按下仅移动键面。实际 LVGL 预览不代表设备帧率或视觉验收。
- “豆包”首次按下开始，第二次按下结束识别。沿用大疆麦克风优先、TAB5 收音回退。
- 底部识别面板把状态和辅助按钮放在同一工具栏，正文自动换行并可滚动。收音就绪时显示绿色提示、绿色话筒和录音指示点，结束收音后隐藏。快捷语音只更新此预览，任何部分结果、完成、失败或取消都不写入 TAB5 的 Codex 输入框。
- 完整识别结果追加到刚打开的电脑 Codex 会话草稿末尾，保持未提交。补录不自动换行：已有中英文句号、逗号、问号等分隔符时直接续写，缺少分隔符时补中文句号；识别句末闭引号/括号内的标点，保留手动换行及空白，不重复补标点。成功后清空 TAB5 预览并显示蓝色发送箭头；用户检查、修改电脑文字后独立点击才向原会话的当前输入框发送一次回车，不重选最新会话或恢复原识别正文。
- 0.2.101-ui 首次回填提示在 .120 候选中改为“检查草稿，可继续语音补充；确认后点蓝色箭头发送”；连续补录且桥接确认实际追加到非空草稿时才显示追加提示。电脑已清空后重新回填使用首次提示；取消和失败不计作成功回填。发送成功提示“已成功发送Codex执行。”。
- 底部工具栏在语音辅助键左侧放置扫帚图标和“长按清空”，使用低对比的描边按钮。.107 用原创木柄、金色刷毛的扫帚代替橡皮擦。两按钮同为 184×48，图标和文字起点对齐，间隔 24 像素。初次进入先隐藏清空，当前流程确认语音回填成功后显示；录音期间、发送或清空确认成功后隐藏。重新录音保留已有电脑草稿与已确认回填记录，不据此隐藏清空。显隐依据本流程的成功回填记录，并非实时读取所有电脑手动编辑。仅长按才请求清空已确认的电脑当前会话输入框，普通点击无操作，操作期间锁定。桥接核对实际前台、目标与输入框身份，不切换到另一会话。清空回读确认后恢复豆包按钮并提示“输入框已清空，可重新录音”，保留 TAB5 本地草稿；不确定时不自动重写。清空使原回填发送记录失效。
- 右下角辅助键在录音期间显示“取消录音”，结束后回到初始状态；成功回填后显示带绿色话筒的“继续语音”，尚未成功回填的错误状态显示“重试录音”；点击复位后提示“点豆包继续语音，文字将追加到电脑草稿”，下一次点豆包开始补录。复位只清理本次预览，保留两端草稿、编辑、图片和待处理请求。
- 发送仅在目标会话、唯一可读输入框、实际前台与编辑焦点确认且没有按键/鼠标按下时执行。重复点击锁定；发送结果未确认时提示检查电脑，不再次自动发送。发送成功回执证明回车已触发且输入框清空，不等同于模型已经处理完成。
- 配套发送确认修复在回车后重新定位输入框；界面刷新导致的临时节点失效只重试读取，在最多 40 次 / 5 秒的窗口内确认清空。持续非空、失去目标或无法读取仍提示未确认，整个流程只输入一次回车。`Codex 快捷发送` 诊断单独记录确认读取次数和短暂不可用次数，不记录草稿正文。

2026-10-04 发送确认修复已通过一轮实机验收：一次临时节点失效后，第 2 次读取确认输入框清空，耗时 1219 毫秒；用户确认电脑执行且 TAB5 正确确认发送。.106 已安装并通过启动核验；桥接记录了目标确认、语音追加及清空成功，但会话联动的完整人工验收仍待确认。.107 已通过精确启动核验，用户确认扫帚和本轮升级反馈正常；新接收程序的进度路径仍待下一次升级独立观测。
- 录音和填入期间锁定会话选择。取消、断线恢复和未确认完成的识别结果只保留草稿，不自动填入。
- 电脑草稿变化、主输入框未就绪、含无法保留的内联对象或写入结果未确认时，保留识别预览并提示原因。一次写入后不自动重写；同一请求的重放不会追加两次。原有 TAB5 会话编辑页保留独立语音与手动发送能力。
- 已部署桥接对写入接口的暂时不可用异常继续进行有界只读核对，重新定位同一目标的主输入框；只有完整追加内容回读一致才确认成功，最多读取 40 次，间隔 80 毫秒。未写入、用户后来修改、持续不可读或取消仍不确认，不重试写入或发送。异常可能发生在写入前或写入后，因此“未确认追加”不等于肯定未写入。

## 协议与实现

复用已配对的 USB / Wi-Fi / BLE RPC 信封及 session、issuedAt、nonce 验证，新增 `kind=desktop`：

| op | 参数 | 成功回执 |
| --- | --- | --- |
| `open-recent` | UUID `requestId`，可选 UUID `taskId`；.106 箭头及 Codex 按钮显式传入所选目标，旧端省略时仍解析最新 | `taskId`、`title`、`projectId`、`folder`、`updatedAt`、`foreground=true` |
| `stage-draft` | UUID `requestId`、明确 UUID `taskId`、不超过 2000 UTF-8 字节的 `text` | `taskId`、`staged=true`、可选 `appended=true/false` |
| `submit-draft` | UUID `requestId`、原 UUID `taskId`、成功回填的 UUID `draftRequestId` | `taskId`、`submitted=true`、`attempted=true` |
| `clear-draft` | UUID `requestId`、明确 UUID `taskId`，只接受当前电脑前台的同一会话 | `taskId`、`cleared=true`、`attempted=true/false` |

请求回执在有效重放窗口内按请求 ID 去重；同 ID 重放沿用首次解析出的目标，同 ID 改变操作、目标或正文返回冲突。每次新点击使用新请求 ID。语音绑定打开成功回执的明确会话 ID；`stage-draft` 的会话消失时拒绝，不改投另一会话。固件绑定请求开始时的通道，不自动换通道重试。

`submit-draft` 还要求同一桥接内 15 分钟内的成功回填记录。一次回车尝试即消费该记录，不因新的请求 ID 或不确定回执重复输入；桥接重启、记录失效、另一会话或未成功回填均拒绝。输入部分成功时尝试释放回车，不重发。设备回到初始状态不会自动重放发送。

语音回执新增 `canInsert`，仅明确停止、识别器确认完成且正文未截断时为真。此字段不授权自动发送。

`sourceName` 为可选的实际录音端点全称，取自成功打开的 Windows 设备，不根据 `dji` 猜测型号。回退时改为 `TAB5 内置麦克风`。直达状态栏显示全称，过长时横向滚动；旧桥接未返回名称时只显示原状态消息。

电脑端通过 Windows Accessibility 检查目标文档标题和会话底部主 ProseMirror 编辑器，排除提问卡片中的额外输入框；同名会话不自动填入。Chromium 可访问树按需生成，先展开树再定位主编辑器。连续读取稳定的原草稿，写入前再次比较编辑器身份、原文、目标和前台；用 `ValuePattern.SetValue` 同步追加合并文本，再回读确认，不发送回车。内联对象无法安全还原时拒绝改写；合并草稿上限为 32000 UTF-8 字节。兼容性取决于 Codex 的可访问结构，源码与模拟测试不替代真机验收。

识别输入框时按 CSS class token 匹配，兼容 `ProseMirror-focused`；只有可访问原始树确认属于 placeholder 的提示文字才不算草稿。已打开目标会话时仅唤起窗口；需要切换时等待目标会话实际就绪，不使用固定 700 毫秒等待。`/diagnostics/tab5` 的 `Codex 草稿` 记录失败原因与耗时，不记录正文。`Codex 直达` 分别记录实际前台、目标确认、Alt 回退、已有按键或鼠标按下及部分输入结果；普通唤起失败时，仅对明确点击执行一次完整 Alt 按下/抬起回退，已有按键或鼠标按下时跳过。Windows 拒绝或目标未就绪时仍返回失败。

## 验证范围

Windows 编译、状态单次读取、快捷控制 / RPC / 语音模拟测试，以及真实 LVGL 的五条会话与“不自动发送”事件测试分别验证。2026-10-04 使用生产代码在实际 Codex 输入框验证测试草稿回填与回读（1016 毫秒），已有草稿阻止覆盖（94 毫秒）；仅清理本次测试文字。用户随后确认真实语音文字进入所选 Codex 会话的输入框。该结论来自用户确认；本轮事后读取的桥接诊断未保留对应语音回填记录。设备镜像与启动健康核验通过，原自动轮播和页面顺序、15 秒间隔已恢复。真实 DJI 采集与各通道分别验收仍未完成。

## English

The desktop bridge now continues dictation inline at the existing draft end. Existing Chinese/ASCII punctuation and manual paragraph breaks are preserved; a Chinese full stop is inserted only when no separator exists. Closing quotes/brackets are inspected for preceding punctuation. Target identity, concurrent-edit checks, exact readback and one-write protection are unchanged. This is a bridge-only adjustment and needs no firmware update.

From candidate 0.2.106-ui, entering the panel defaults to the latest of five recent catalog entries. Previous/Next immediately requests desktop navigation to the corresponding explicit target; update panel selection only after matching foreground confirmation. Keep list order fixed for this visit, refreshing on reopening. The Codex key raises or opens the currently selected conversation, including after switching to another desktop app. Reopening the same confirmed target preserves staged send/clear controls; changing target resets the previous send entry without deleting either draft. Failed or wrong-target navigation retains panel selection and blocks dictation until explicit confirmation. Existing Windows open-recent taskId semantics are reused; omitted targets still resolve latest for older clients.

Installed 0.2.107-ui passed exact-image startup verification with ota_1 VALID and no upgrade error; full conversation-selection hardware acceptance still awaits user confirmation. Installed 0.2.107-ui adds the broom and write-based upgrade progress with a separate timed verification stage, retaining the conditional equal-width clear control. The user confirmed the broom and this upgrade's feedback. Installing .107 used the previous receiver; the new write-progress path applies on the next upgrade and has not yet been independently observed on hardware. Measured .106 Wi-Fi sender time was about 12s, receive/write 17.995s, pure writing 9.621s, final verification 0.879s and installation 18.993s excluding reboot. Correcting feedback neither establishes the hardware ceiling nor claims faster installation. Companion hashes and the original six-page automatic cycle and 15-second interval remain. The installed desktop reconciliation fix passed user first-dictation/Re-record/append acceptance with an unsubmitted draft and blue arrow. That flow did not deliberately inject a provider exception; the exception branch is covered by regression tests.

Installed 0.2.101-ui distinguishes first staging from repeated dictation appended to a nonempty desktop draft. The additive `appended` reply field comes from the original composer snapshot only after successful readback; missing fields default to the generic review hint. Cancellation and failed staging never count as successful takes. The .120 candidate uses the one-line review hint “检查草稿，可继续语音补充；确认后点蓝色箭头发送”, labels the confirmed-draft action “继续语音” and the pre-staging retry “重试录音”, retaining the reset-then-record behavior. The submission message remains “已成功发送Codex执行。” after confirmed submission.

The outlined 长按清空 control beside Re-record uses an original wooden-handle, golden-bristle broom in .107 and accepts only a long press. Both controls remain 184×48 with aligned icon/text offsets and a 24px gap. Clear starts hidden and appears after a confirmed voice take, hides during recording and after confirmed submission or clearing, and remains available after Re-record preserves an existing known draft. Visibility follows confirmed staging in this flow rather than live polling of all desktop edits. `clear-draft` verifies the foreground conversation and composer snapshot without navigation, clears once, confirms readback and invalidates prior send tickets. Ordinary taps and request replay do not clear again. Confirmed clearing restores the Doubao key and leaves the TAB5 local draft intact. The user has confirmed the new broom icon on hardware.

The installed desktop bridge continues bounded read-only reconciliation when the write provider raises a transient accessibility exception. It reacquires the same target composer and confirms only an exact readback of the complete appended text, for up to 40 reads spaced 80ms apart. Missing writes, subsequent edits, unavailable reads and cancellation remain unconfirmed; input is never repeated or submitted. An uncertain write can have taken effect before the exception, so the warning does not prove that no text was inserted. One real dictation/append flow has passed acceptance.

Candidate 0.2.100-ui keeps independent mechanical keys with transparent hit areas in pressed and disabled states, and gives the lower half a scrollable transcript panel with an integrated toolbar. Product-name captions and the shared deck are removed. Recording-ready feedback and microphone indicators are green. Confirmed staging switches to a reference-blue (#3A83F7) face with a centered 10px white arrow. Quick Console takes never write to the TAB5 composer. Desktop staging identifies the main composer rather than question-card editors, waits for a stable draft and appends through guarded UIA ValuePattern with readback. Existing plain text is retained; concurrent edits and unsupported inline objects are not overwritten. One application is never automatically repeated after uncertain readback, and submission still requires a separate explicit click. Two sequential draft insertions were verified against the actual Codex UI without sending; the test draft was removed. Device recording/layout acceptance is pending.

Candidate 0.2.99-ui groups two raised controls on a mechanical deck, with original procedural brushed-metal collars, graphite/red lacquer faces, side walls and contact shadows. Captions stay “Codex” and “豆包”, separated from the collars by at least 20 pixels. Centered Codex and original frameless official Doubao site icons identify the keys; recording/staging states use stop and send icons. Press/release motion depresses, shrinks and rebounds the face. The voice key explicitly stages an unsubmitted draft, and only a separate third click presses Enter in the staged conversation's current composer, including user edits. The secondary Cancel/Re-record key retains desktop drafts, user edits, attachments and pending requests. A same-conversation staging receipt expires after 15 minutes or bridge restart; uncertain submission never triggers a retry. Success confirms Enter and an empty composer, not model completion. Opening attempts to launch a stopped Codex through its registered URI, with a longer bounded cold-start wait. Hardware acceptance of the new controls and animation remains pending. The user confirmed .098 USB progress without blue flash and voice insertion; exact .098 boot identity and deployment closure remain pending.

Local candidate 0.2.96-ui adds a permanent bottom-right Codex icon with a 60×60 touch target in its own footer slot. Full-screen camera, screensaver and upgrade workflows cover it. Browse up to five recent conversations; each click on “回到 Codex” resolves the latest conversation anew and updates the panel from the actual desktop reply. Dictation stays bound to that opened ID. Recognition is previewed at the panel bottom, staged as an unsubmitted desktop draft, then cleared from TAB5 only on success. DJI remains preferred with TAB5 fallback. The user reported that the entry failed to bring Codex forward. The candidate bridge attempts at most one complete Alt down/up activation fallback for an explicit request, skips it while a key or mouse button is held, and verifies actual foreground/target state. Hardware confirmation remains pending. Diagnostics record status and duration without draft contents. Installed 0.2.90-ui and its desktop insertion fix were verified on October 4: production code staged and read back a real Codex test draft in 1016 ms and rejected overwriting it in 94 ms. The test text was removed. The user then confirmed voice text in the selected conversation's composer; post-test diagnostics did not retain that insertion record. Firmware identity and boot health passed, and the original display cycle was restored. DJI capture and each transport still require separate acceptance.


## 2026-10-05：焦点切换实测 / Focus-switch experiment

独立测试程序绕开桥接的失焦停止状态机，用 Windows SAPI 两段固定语音经 VB-CABLE 输入真实豆包输入法。保持焦点时原框收到完整 36 字；切到同进程另一窗口的可编辑框，两次均只收到切换前 18 字，另一框 0 字。切换后 700ms 原生查询已 idle，早于第二段播放及显式停止请求。当前输入法自身也会受焦点迁移影响，不能仅移除桥接保护来保证持续录音。证据位于 `artifacts/development/voice-focus-probe/RESULTS.md` 与 `results.jsonl`；三轮恢复麦克风配置，未替换正式桥接/固件，未操作 Codex 草稿。真实 TAB5/大疆、跨进程应用和 API 接入仍属于独立验收范围。

An isolated real-IME probe bypassed the bridge focus-loss state machine and fed two fixed SAPI speech clips over VB-CABLE. The baseline returned36 characters; two trials switching to another editable window in the same process returned only the first18 to the original field, zero to the second. Native idle was observed700ms after the switch, before the second clip and explicit stop. Removing the bridge guard alone cannot ensure continued capture for this tested IME. All three trials restored microphone selection; production bridge/firmware and Codex drafts were untouched. Hardware microphone/TAB5, cross-process applications and API integration require separate acceptance.

## .121 连续录音候选验证

桥接仅在本次拥有的豆包录音仍活动、未请求停止时尝试恢复原语音草稿焦点；恢复成功保留同一 voiceId、音频顺序和已有文字，不发送重新开始指令。`focusRestores` 记录成功次数。原生状态未知或已经停止时不自动重开录音，因此不能保证输入法自行结束后的无缝继续。

用户录音期间只运行 `--self-test-tab5-voice-core`（无窗口、模拟音频/输入法）；`--self-test-tab5-voice` 包含激活测试窗口的 UI 验证，不能在用户录音期间调用。内部通过不代表真机连续录音通过。
