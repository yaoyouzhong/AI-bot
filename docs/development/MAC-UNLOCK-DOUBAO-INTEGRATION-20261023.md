# Mac 一键解锁与豆包正式入口候选 / Saved unlock and Doubao candidate

本记录接续 [临时测试记录](MAC-UNLOCK-DOUBAO-20261023.md)。r34 本机单次解锁由用户确认成功；这不能代替下面新增的设备触发流程验收。r35 是未发布候选，不改组件版本，不推送、建标签或发布，不改写设备固件。

## 一键解锁

- 电脑控制的一键解锁入口改为日常设置窗口：独立启用开关、两次 Mac 密码输入、验证并保存、删除密码、屏幕共享组件说明、配置检查及独立临时测试。主窗口和固定页脚沿用现有 Windows 分组与 Mac 共用间距。
- 保存操作先执行已验收的 Mac 账户认证及完整公开样本 `Ab9!@_-+`。用户明确点击“验证并保存密码”，并在认证后继续键盘验证；通过后才保存到 `tab5-mac-unlock-v1` 钥匙串项目。记录绑定当前 UID、账户和精确配对密钥派生身份。保存不自动启用；关闭开关保留密码，删除会关闭并删除该项目。配置迁移及诊断不导出此记录。
- 普通启动仅在开关已开启时读取一次记录到进程内；设备请求和轮询不触发钥匙串弹窗。读失败／未知版本／账户或配对变化不通告就绪，设置中的显式配置检查可重新读取。辅助进程仅为这个新项目增加删除操作，不能删除其他配对或供应商记录。
- 复用 Windows 和已核对维护者固件的 `wifi-unlock/begin,poll,cancel`。既有 AES-GCM、HMAC、nonce、session、issuedAt、配对检查保持不变。请求只含意图／offer／序号，不带密码、账户或键值。请求 ID 单次消费、120 秒保留、256 容量、40 秒总预算、2.5 秒轮询租约；失联／错误顺序／取消／停用／睡眠／控制台让出后停止，不重放。
- 正式入口每次以保存的同一 Mac 账户连接固定回环 Apple 屏幕共享，核验系统服务及 Apple 登录窗口、当前控制台、唯一安全字段和焦点，等待本地输入静止三秒后提交一次。临时测试仍要求同一连接的公开样本；正式入口使用显式保存时已验证的 ASCII 输入配置，再独立确认每次新连接的登录目标。受保护字段不提供计数时不假定为零，不能读取受保护文字。提交后必须连续观察到当前控制台已离开签名登录窗口，才返回 `unlocked=true`；提交本身不算成功。
- 当前已核对固件只在**纯 Wi-Fi**连接时选择该流程。Mac 继续保持 `oneClickUnlock=false`，拒绝向 USB／BLE 下发登录密码，不把 Windows 数字 PIN 路径冒充 Mac 密码输入。USB／BLE 的主机端解锁需要设备端另行适配，尚未完成。

## 豆包语音

- 默认仍为 macOS Speech 本地识别，要求 `requiresOnDeviceRecognition`；豆包为独立选项，不自动切换识别服务。
- Mac／DJI 实体输入优先使用明确选择的麦克风；未指定时优先唯一 DJI，否则用 Mac 默认麦克风。TAB5 的已鉴权 PCM／ADPCM 经既有解码后只输出到签名有效、唯一的 VB-CABLE Mac 设备。CoreAudio 输出设备设置后回读核对，不改系统默认输入／输出，不回退扬声器。
- 通过公共 AX 操作签名有效的豆包设置 App，仅匹配麦克风选择按钮。临时变更前保存不含秘密的恢复记录，结束／取消后恢复；用户期间主动改选优先。自动检测模式与固定设备模式分别保留。Mac 选择列表在点选后不自动关闭，流程额外关闭自己打开的列表，再核验实际选择。恢复失败保留记录供下一次恢复，不伪称已恢复。
- 通过公共输入源和长按 Fn 调用豆包，文字只进入桥接自有编辑器。公共 CoreAudio 的进程对象只查询签名输入法的输入 IO 布尔状态及输入设备，不建立音频 tap、不读取其他应用文字；快捷键发送不算收音就绪。自动状态核验需要 macOS 14.2+，不支持时明确停止。
- 停止时排空有界音频队列，并等待公开设备延迟／缓冲尾段后释放 Fn。输入法实际停止收音、编辑器已收到提交而非临时组合文字、无 marked text、文字稳定至少 0.6 秒，才返回最终预览；取消、失焦、输入源改变、结果超时不自动插入或发送任务。结束后恢复先前输入源及窗口展示模式。

## 本机状态与验证

- 用户自行完成 VB-CABLE Mac 1.0.8 的许可确认和管理员安装。系统及豆包均识别 `VB-Cable`，双声道输入／输出、48 kHz；Mac 内置输入和扬声器仍为系统默认。官方 ZIP SHA-256 与 Homebrew cask 登记一致：`e46b41c6876995403cb1da37d7c0d566f59edd83aa9fcbcdc3a205eb0b6e05c7`。PKG 签名为 Vincent Burel（`6K8JQXLBSY`），Apple 公证检查通过；没有将第三方安装器或驱动纳入桥接发布包。
- 已在真实豆包设置中查看 VB-CABLE，并核对自动选择的显示方式和列表关闭行为；随后恢复原 MacBook Pro 麦克风。只读 CoreAudio 查询成功返回豆包当前输入 IO 为 0，没有启动录音或读取音频。
- Debug 独立检查 335 项通过，覆盖保存与启用分离、配对绑定、重启读取、未知记录版本、禁用／删除、请求与序号重放、撤权／取消／租约、未确认提交不得报成功，以及豆包真实就绪标记、组合文本、最终提交、音频尾段和取消后迟到结果。没有真实凭据、网络、设备或录音操作。
- `swift test` 已执行，但本机 Command Line Tools 缺少 XCTest 模块，未执行完整 XCTest。Release 包及原生布局验证结果另行补记。正式保存密码、TAB5 Wi-Fi 触发解锁、实体麦克风和 TAB5 音频经豆包的实际识别仍待本轮实测，不以编译或组件识别代替。

参考：[VB-CABLE 官方说明](https://vb-audio.com/Cable/)、[Homebrew 官方 cask 元数据](https://github.com/Homebrew/homebrew-cask/blob/master/Casks/v/vb-cable.rb)、[豆包官方输入法](https://srf.doubao.com/)。第三方组件由用户独立安装，许可不并入本项目；不复制第三方实现。

## English

This unpublished r35 candidate follows the user-accepted one-use r34 unlock test. No version bump, push, release or firmware write occurs. The computer-control entry now has a Windows-style saved-unlock form with a separate enable switch, repeated password entry, account/keyboard verification before Keychain storage, deletion, component information, configuration check and separate temporary test. Records bind the current user/account and exact pairing identity; saving alone never enables access. Disabled settings retain credentials; deleting disables and removes only this new Keychain item. Startup reads once only when enabled, and device RPC never opens Keychain prompts. Migration/diagnostics exclude credentials.

The existing authenticated Wi-Fi unlock contract is reused without wire changes. Only intent, offer, sequence and progress cross the device link. Replays, expired leases, wrong order, cancellation, permission/pairing changes, sleep and console resignation stop input without retry. Enrollment verifies the full public ASCII sample; each saved attempt independently authenticates to signed loopback Apple Screen Sharing and verifies the current signed loginwindow secure target, followed by input quiet time and a single submission. Protected text is never read or treated as an empty count. Actual desktop-focus return must be observed before unlocked=true. Current firmware selects this route only in Wi-Fi-only mode. Mac oneClickUnlock remains false and USB/BLE never receive a Mac password; their host-side route requires separate device adaptation.

macOS on-device Speech stays the default. The separate Doubao mode uses the selected physical microphone, or unique DJI then the default input when none is selected. TAB5 audio goes exclusively through the verified VB-CABLE Mac device, without changing system defaults or speaker fallback. Public AX selects only the signed settings application's microphone controls and preserves automatic versus fixed selection, with a non-secret restoration journal and manual choices taking precedence. The actual picker remains open after selection; it is explicitly closed before verification.

Public input-source/Fn events target an app-owned editor. Public CoreAudio process metadata confirms the signed input method's actual input IO on the chosen device, rather than assuming a shortcut started capture. This automatic check needs macOS 14.2+. Queued audio and the output-latency tail drain before Fn release. Only stopped capture, committed unmarked text and 0.6 seconds of stability produce final review; cancellation/focus/source changes and timeout never auto-insert or send. Previous microphone, input source and app presentation are restored.

The user independently accepted the official VB-CABLE Mac 1.0.8 license and administrator installation. macOS and Doubao now enumerate its 48-kHz stereo input/output; built-in system defaults are unchanged. The official ZIP matches the Homebrew checksum, and the PKG's Vincent Burel signature and Apple notarization verify. Vendor installers/drivers are not bundled or relicensed. Actual settings checks preserved the original microphone, and read-only CoreAudio confirmed idle input IO without recording. Debug passes 335 isolated checks. Full XCTest remains unavailable because the host lacks its module. Release/layout results will be appended; saved-password device unlock and physical/TAB5 Doubao recognition remain for real acceptance.


## 接续验证 r36 / Resumed validation r36

- 接续上一会话的最后一条界面要求：设备中心 USB 故障摘要精简为“未连接，等待重连”，三个连接状态统一单行；完整原因保留在悬浮提示与连接诊断。默认浅色及最小深色原生图、运行窗口均已检查。
- 本地候选：`artifacts/mac-integration-resume-20261023-r36/AIBotBridge.app`。Release 构建、固定身份签名与深度校验通过；335 项独立检查通过，236 张原生布局图生成成功；11 个包内文件的 SHA-256 记录在同目录 `SHA256SUMS.txt`。
- `swift test --package-path mac-app` 已执行，仍因本机缺少 `XCTest` 模块无法运行完整测试。检查日志为 `artifacts/mac-integration-resume-{swift-test,build,selftest,ui}-r36.log`。
- 已正常退出 r35 并启动 r36。豆包安装／输入源／既有调用权限及 VB-CABLE 端点检查通过；识别方式仍保留 Mac 本地识别默认值。设备端解锁与语音识别的实际结果将在取得回执后单独补记。

The resumed r36 candidate shortens the device-center USB failure summary to a single line, retaining full diagnostics in tooltips and connection details. All three transport labels stay on one line. Default/light, minimum/dark and live layouts were inspected. Release compilation, fixed-identity signing and deep verification pass, as do 335 isolated checks and 236 native layout renders. The adjacent SHA256SUMS.txt covers 11 bundle files. Full XCTest remains unavailable because the host lacks the module. r35 exited normally and r36 is running. Installed Doubao/input-source permissions and VB-CABLE endpoints pass readiness checks; the user's default local recognizer is unchanged. Actual device unlock and recognition receipts remain to be recorded separately.


### 实机接入进度 / Live integration progress

- r37 运行窗口已确认 TAB5 固件 `0.2.261-ui`，Wi-Fi 已收到认证请求，蓝牙数据已确认；不再把此前离线状态作为当前状态。
- 用户自行输入 Mac 密码并完成账户认证／完整公开键盘样本，真实窗口回执为“密码已保存到本机钥匙串”。随后用户自行勾选 Wi-Fi 解锁并保存；代理从真实窗口核验开关为 1、状态为“Wi-Fi 一键解锁已开启”。这是正式保存和启用验收，尚不代替设备触发解锁成功。代理没有读取或输入密码。
- 豆包实体麦克风测试在准备阶段停止，未开始收音。r37 明确定位到读取豆包设置的“语音输入”入口失败；应用签名、进程位置及运行身份核验已通过。不能把环境检查通过写作识别通过。r38 增加有界 AX 状态／控件数量诊断，不记录其他应用文字或音频；候选编译、签名和 335 项隔离检查通过，等待本轮设备解锁结束后再替换运行程序。

The live r37 window confirms TAB5 firmware 0.2.261-ui, authenticated Wi-Fi requests and confirmed BLE data. The user personally completed account/public-keyboard verification and Keychain enrollment, then enabled and saved Wi-Fi unlocking. The agent verified the success receipt and enabled UI state without reading or entering credentials. This accepts enrollment and enablement, not device-triggered unlock. Physical-microphone Doubao attempts stop before capture because the voice-input settings entry cannot be read, after signature/process verification. r38 adds bounded AX status/control-count diagnostics without logging external text or audio; compilation, signing and 335 isolated checks pass. Its deployment waits for the user's active unlock test to finish.

### 实体麦克风通过，设备链路继续验收 / Physical microphone accepted

- 用户首次正式 Wi-Fi 解锁反馈为“解锁失败，现在解锁按钮变成灰了”。当时桥接没有新的成功回执，随后设备中心显示 Wi-Fi／蓝牙离线。未确认请求到达或失败阶段，不能把掉线直接认定为解锁失败根因；正式设备解锁仍未通过。
- 随后真实设备中心显示固件 `0.2.262-ui`，USB 数据已确认、Wi-Fi 请求已认证、蓝牙已认证并处于 USB 待命。本轮代理没有刷写固件；该观察不能代替纯 Wi-Fi 解锁条件。
- r39 将豆包故障缩小到公共 AX 返回成功但窗口列表为空。r40 让已核验签名的豆包设置应用以前台方式打开，随即可读取语音设置并继续收音；未绕过签名或焦点检查。
- r40 真实窗口显示“已确认豆包从所选麦克风收音，请开始说话”。用户对 Mac 麦克风说话并停止后，桥接回执为“豆包草稿已确认；本次测试未发送文字”。这证明实体麦克风实际 IO、停止及非空最终提交链路通过。没有持久化识别原文，因此不宣称指定短句逐字准确。
- r40 Debug 固定身份签名校验及 335 项隔离检查通过。正在进行 TAB5 麦克风 → VB-CABLE → 豆包的独立实测，尚未记为通过；识别方式临时切为豆包，结束后恢复原 Mac 本地识别选项。

The first saved Wi-Fi unlock attempt failed according to the user and left the device button grey. No success receipt was observed; the subsequent device center showed Wi-Fi/BLE offline. Request arrival and failure stage remain unconfirmed, so disconnection is not asserted as the root cause. A later live view reports firmware 0.2.262-ui with confirmed USB, authenticated Wi-Fi and BLE in USB standby. The agent did not flash firmware, and this mixed connection does not satisfy Wi-Fi-only acceptance.

r39 isolated Doubao's preparation failure to a successful public AX call returning no windows. r40 opens the verified settings app in the foreground, preserving signature and focus checks. Real input IO was then confirmed; after the user spoke into the Mac microphone and stopped, the bridge confirmed a final draft without sending text. This accepts physical capture and nonempty final commitment, not exact transcription accuracy because the draft text was not retained. r40 Debug signing and 335 isolated checks pass. TAB5 audio through VB-CABLE is a separate pending physical test; the temporarily selected Doubao provider will be restored to the original local recognizer afterward.

### r42：修复设备发起豆包录音时的崩溃 / Device-start crash repair

- 用户在 TAB5 日程新增界面点击录音后，先后看到“语音识别需连接电脑”和“语音连接中断，本段未完整保存”。本机系统崩溃报告定位到 `AI-bot.tab5-usb` 队列的 `MacDoubaoSpeech.source()`：后台调用 `TISCreateInputSourceList` 触发 HIToolbox 的主队列断言。Mac 窗口测试从主线程启动，因此没有覆盖设备入口。仅记录故障位置，不复制原始系统报告或运行身份数据进仓库。
- 输入源可用性检查移动到既有主线程 `begin` 阶段；USB／LAN／BLE 发起流程保持异步，不在设备锁内同步等待主线程。输入源查询、选择与编辑器操作均在主线程执行，失败仍以异步错误返回设备。
- 解锁诊断增加固定的开始／取消动作摘要及本地固定错误码，便于核对请求是否到达和前置检查失败。不会记录请求 ID、会话、凭据、密码或设备载荷，未改变协议字段和解锁约束。
- r42 Release 候选为 `artifacts/mac-integration-resume-20261023-r42/AIBotBridge.app`，固定身份签名及深度校验通过，335 项隔离检查通过，11 个包内文件有相邻 SHA-256 清单。`swift test` 再次执行仍因主机缺少 `XCTest` 模块失败；`git diff --check` 通过。已正常退出旧版、启动 r42，真实设备中心确认 USB 数据及 Wi-Fi 鉴权恢复，正在复测。

The TAB5 agenda recording action produced offline/interrupted messages. The local crash report identifies a HIToolbox main-queue assertion in `TISCreateInputSourceList`, called by `MacDoubaoSpeech.source()` on the USB worker. The earlier Mac-window test started on the main thread and did not cover this entry. Only the fault location is recorded here, not the raw system report. r42 moves input-source availability checking into the existing asynchronous main-thread begin stage, avoiding synchronous main-thread waits under device locks. Source enumeration/selection and editor work now stay on the main thread; failures still return asynchronously.

Unlock diagnostics add fixed begin/cancel labels and local error codes without request IDs, sessions, credentials or payloads; wire fields and unlock safeguards are unchanged. The r42 Release bundle passes fixed-identity signing, deep verification and 335 isolated checks, with an adjacent SHA-256 list for 11 files. Full XCTest again fails because the host lacks its module. Diff whitespace checks pass. The previous app exited normally; r42 is running with confirmed USB and authenticated Wi-Fi while physical retesting continues.

### r43：兼容设备准备阶段音频 / Preparation audio buffering

- r42 实测不再崩溃，但用户在尚未说话时收到“本段未完整保存，未取回识别文字”。真实桥接诊断为 USB 音频 400、已接收 0 块。调用方会在提示可以说话之前发送音频探测，只有收到音频确认且主机 `ready=true` 才提示就绪；此前桥接在豆包准备期间拒绝首包，导致设备转入失败恢复。
- Mac 在既有语音会话内暂存最多 8 秒音频，继续保持 `ready=false`；确认豆包实际输入 IO 后按原序转送，重复包不重复转送。缓冲或输出队列超限明确停止，取消丢弃准备音频，未完整转送时的停止／失联不能报告完成。无协议字段、固件或 Windows 行为变更。
- r43 Debug 编译、固定身份签名及 341 项隔离检查通过。新增 6 项覆盖首包准备、顺序和去重、取消、提前停止以及 8 秒上限。已部署后请用户重测，未将诊断准备成功记为真实识别通过。

r42 no longer crashes, but the device fails before the user speaks. Live diagnostics show a rejected USB audio request and zero accepted chunks. The caller sends probe audio before announcing readiness, requiring both audio acknowledgment and host readiness. Rejecting that first packet while Doubao prepares sent the device into failure recovery. The Mac now buffers at most eight seconds within the existing session, keeps ready=false until actual input IO, and forwards buffered samples in order without replaying duplicate packets. Overflow stops explicitly; cancellation discards preparation audio; stopping or losing the connection before complete forwarding cannot claim completion. No wire fields, firmware or Windows behavior change. r43 Debug compilation/signing and 341 isolated checks pass, including six preparation-buffer regressions. Physical recognition is still being retested separately.

### 回传未通过及恢复日常输入 / Failed delivery and daily-input recovery

- r43 用户反馈：首次仍在准备时失败；第二次开始录音，但说话未结束即中断，设备未取回文字。桥接随后保留的本机诊断为已接收 28 块／5.6 秒音频，首段结果约 8.1 秒、停止至最终识别约 2.6 秒，最终状态为有文字的预览。但设备端结果与此不一致，因此不能据此宣称 TAB5 端到端通过。旧“用户停止收音”标签只说明收到 `stop` 请求，也可能来自设备失败恢复，现已改为“设备请求停止收音”。每次新会话清除旧识别异常，避免将上一轮错误归到当前轮次。
- 用户同时反馈日常豆包语音失效。真实豆包设置仍选 `VB-Cable`，待恢复记录保留原 MacBook Pro 麦克风。代理从公开设置界面手动恢复 MacBook Pro 麦克风并关闭选择列表，桥接选项改回原 Mac 本地识别；用户明确确认“日常豆包语音已恢复”。未继续请求 TAB5 豆包录音。
- 修复切换途中取消／失败时可能遗留麦克风列表的问题：本轮打开的列表总在退出时清理，恢复旧记录前也清理遗留列表，选择后等待读回确认。恢复失败最多再尝试一次，保留恢复记录并显示明确诊断，不再静默忽略。语音设置新增“恢复豆包麦克风”，仅在没有活动语音时执行，保留用户随后手动改选的设备。此修复的自动切换／恢复完整实机循环仍须单独验收。

The user reports that r43 first failed during preparation, then began a second take but interrupted before speech finished and failed to return text to TAB5. Host diagnostics show 28 chunks/5.6 seconds, a first result after about 8.1 seconds and final recognition about 2.6 seconds after stop, ending in a nonempty review. This conflicts with device delivery, so end-to-end acceptance is not claimed. A stop RPC can come from device recovery as well as a person; its diagnostic label now says “device requested stop,” and new sessions clear prior recognition errors.

Doubao remained on VB-Cable and the restoration journal retained the original MacBook Pro microphone. The agent restored that input through the public settings UI and returned the bridge to native Mac recognition. The user explicitly confirmed daily Doubao voice input works again; no further TAB5 Doubao recording was requested. The cleanup repair closes the owned picker on cancellation/failure and before recovering an interrupted journal, waits for selection readback, retries restoration at most once, and reports failure while retaining the journal. An explicit restore button is available only while voice is idle and preserves later manual choices. The complete automatic switch/restore hardware cycle still needs separate acceptance.

### 当前候选 r46 / Current candidate r46

- 已部署 `artifacts/mac-integration-resume-20261023-r46/AIBotBridge.app`。Release 构建、固定身份签名／深度校验、341 项隔离检查和 236 张原生布局渲染通过；语音页全图及最小尺寸图已检查，`git diff --check` 通过。`swift test` 仍因本机缺少 XCTest 模块无法执行完整测试。构建／检查日志使用 `artifacts/mac-integration-resume-*-r46.log`，相邻 `SHA256SUMS.txt` 覆盖 11 个包内文件。
- r46 真实语音设置显示原 Mac 本地识别。点击“恢复豆包麦克风”后，窗口确认“豆包麦克风恢复已完成，保留当前手动选择”；只读检查确认旧恢复记录已清除。这验证了手动改选优先及旧记录清理，没有启动收音，也不等同于自动切换恢复的完整实机验收。
- 为下一轮一键解锁验收临时关闭桥接 TAB5 蓝牙（原值开启），已请用户拔掉 Mac USB 数据线并保持设备 Wi-Fi。尚未再次请求锁屏；测试收尾需恢复原蓝牙开关。

r46 is deployed from the path above. Release compilation, fixed-identity/deep signature verification, 341 isolated checks and 236 native UI renders pass; the full voice page and minimum-size layout were inspected, and diff whitespace checks pass. Full XCTest remains unavailable on this host. Adjacent checksums cover 11 bundle files. The live restore button preserved the user's manual microphone choice and cleared the pending journal without recording; this accepts manual-choice preservation and journal cleanup, not the complete automatic microphone cycle. For the next saved-unlock check, bridge TAB5 BLE was temporarily switched off from its original enabled state; USB removal and continued device Wi-Fi were requested. No new screen-lock request has been issued yet, and BLE must be restored afterward.

### r47–r49：电脑状态间歇性失败 / Intermittent computer status failure

- 用户拔掉 USB 后按提示手动锁定 Mac、在 TAB5 发起一次解锁，并反馈回到桌面；随后设备提示无法读取电脑状态，用户确认 Mac 本身处于可操作桌面。桥接没有取得足够的正式解锁成功回执，因此暂不把这次桌面恢复记作设备端完整验收通过。无需重新录入或重新启用密码。
- 一键解锁设置原来只在创建窗口时读取诊断，复用窗口可能显示旧值。新增打开时刷新及“刷新解锁诊断”，无需重新读取钥匙串。电脑状态记录仅保留最近 4 条已鉴权请求的固定操作、通道、返回码、队列／处理时间及采样年龄，涵盖分发器的会话检查拒绝；不保存请求 ID、载荷或凭据。
- 蓝牙已恢复原开启设置；真实桥接显示归属／数据确认，用户确认 TAB5 蓝牙图标恢复。USB 仍未连接。用户观察电脑控制页在“电脑已连接”与“无法读取电脑状态”之间反复切换。两次无线采样间 Wi-Fi RPC 无响应失败累计由 229 增至 241，蓝牙失败累计仍为 0；这一字段解释参照已存 `.234` 源码，当前实机为 `.262`，没有将旧源码冒充当前构建源码。
- r49 已到达 Mac 的电脑状态请求均为 Wi-Fi／200，队列耗时 0 ms，处理 2–56 ms。设备仍丢失部分响应，根因尚未确认。只读本机监听检查 20 次均按预期拒绝无凭据请求，最长 12.8 ms；这不能代替设备链路验收。
- 用户更正最近“豆包正常”的报告实际上是本地输入，因此这次只记录本地识别正常，不新增 TAB5 豆包通过结论。此前明确确认的日常豆包恢复记录保留。
- r48 Release 构建及签名、341 项隔离检查通过，但未部署；完整 XCTest 仍因主机缺少模块失败。当前诊断部署为 r49 Debug，签名与 341 项隔离检查通过。没有修改设备固件、展示设置或版本号。

After USB removal, the user locked the Mac, requested one TAB5 unlock and reported desktop return, then reported device state-read failures while the Mac remained usable. A complete saved-unlock receipt was not established, so device acceptance remains open without repeating credential enrollment. The settings window now refreshes diagnostics on opening and offers a refresh button without reloading Keychain. Four bounded authenticated request metadata entries include operation, transport, status, queue/processing time and age, including dispatcher rejections; no IDs, payloads or credentials are retained.

The original BLE-enabled setting is restored and the user confirms its icon recovered; USB remains disconnected. Computer status still oscillates. Two device samples show Wi-Fi RPC no-response failures rising from 229 to 241 while BLE remains at zero, interpreted using the archived .234 field layout rather than claiming it is current .262 source. Arriving Mac requests return Wi-Fi/200 with zero queue time and 2–56 ms processing. Twenty unauthenticated loopback checks correctly return rejection, at most 12.8 ms; neither result proves the device link is reliable. The user corrects the latest successful speech report to native recognition, so it adds no TAB5 Doubao acceptance. r48 Release/signing and 341 isolated checks pass but were not deployed; r49 Debug is deployed with signing and the same checks. Full XCTest remains unavailable. No firmware, display settings or version numbers changed.

### r50 连接假设未通过 / Connection hypothesis rejected

- r50 短暂测试通用 RPC 的 HTTP/1.1 连接复用。基于实际编译对象和合成配对／后端的本机 socket 检查通过：同一连接连续读取、每次独立鉴权、错误凭据关闭、显式关闭及 HTTP/1.0。341 项隔离检查也通过。这只是验证候选的实现，不是确认现场故障根因。
- 用户观察后明确回复仍反复断开，因此撤回该连接复用改动及其专用测试，保留原有语音端点的连接行为和已验证修复。两次 r50 采样期间设备 RPC 失败累计曾保持 317，但电脑读取仍失败，不能以总计数不增长替代页面通过。
- Mac 到设备的 10 次 ICMP 探测均收到回复（平均约 53 ms，最大约 97 ms），不能排除应用链路或瞬时故障。已向用户询问当前 `.262` 构建源码位置；本机 Work／Downloads 及公开主分支没有对应源码，公开版本登记仍为 `.234`。不再用旧版源码假定当前连接管理细节。

r50 briefly enabled HTTP/1.1 reuse for generic RPC. An actual-object synthetic socket check passed sequential reads, per-request authentication, bad-proof closure, explicit close and HTTP/1.0; 341 isolated checks also passed. The user still observed repeated disconnections, so that speculative change and its dedicated test were withdrawn, retaining the existing voice endpoint behavior and validated fixes. The device's cumulative RPC failure count stayed at 317 across two r50 samples, but the page still failed; a flat aggregate is not acceptance. Ten ICMP probes received replies (about 53 ms average, 97 ms maximum), which does not rule out application or intermittent failures. Matching .262 source location is requested: local Work/Downloads and public main do not contain it, and the public version registry remains .234. Older source is not treated as the current connection implementation.

### 收尾候选 r51 / Handoff candidate r51

- 已正常退出 r50 并部署 `artifacts/mac-integration-resume-20261023-r51/AIBotBridge.app`。Release 构建、固定身份签名／深度校验、341 项隔离检查、236 张原生 UI 渲染及 `git diff --check` 通过；解锁完整页面与实际多行诊断排版已查看。`swift test` 仍在 XCTest 模块导入处失败，没有执行完整套件。
- 相邻 `SHA256SUMS.txt` 覆盖 11 个包内文件，包含 LICENSE 与第三方声明；包内候选说明与当前仓库文件一致。日志为 `artifacts/mac-integration-resume-*-r51.log`，UI 图为 `artifacts/mac-integration-resume-ui-r51/`。
- 真实窗口确认 Wi-Fi 已认证、蓝牙数据已确认、USB 未连接；语音设置为原 Mac 本地识别、默认麦克风、等待设备发起。没有再启动收音、锁屏或解锁，没有刷写固件或修改 TAB5 展示设置。电脑状态间歇失败、TAB5 豆包完整回传与自动麦克风恢复循环、正式设备解锁回执仍未验收通过；等待当前 `.262` 源码位置以继续定位。

r50 exited normally and r51 Release is deployed at the path above. Build, fixed-identity/deep signature verification, 341 isolated checks, 236 native UI renders and whitespace checks pass; the full unlock page and live multiline diagnostic layout were inspected. Full XCTest still stops at module import. Adjacent checksums cover 11 bundle files, including license/notices, and the bundled candidate document matches the repository. The live app confirms authenticated Wi-Fi, acknowledged BLE, disconnected USB, native Mac recognition, default microphone and idle voice. No further recording, locking, unlocking, firmware write or TAB5 display change occurred. Intermittent computer-status failure, complete TAB5 Doubao delivery/automatic microphone restoration, and the saved device-unlock receipt remain unaccepted pending current .262 source access for diagnosis.

### Windows 开发分支对照 / Windows development reference

用户指出 GitHub Windows 开发分支后，已读取并抓取 `codex/windows-tab5-multihost-20261021`，固定到 `a9a70d256ea573af99959bc1c514642008725141`。该提交的 `versions/TAB5` 为 `.257`，全部分支列表及仓库 `.262` 提交搜索未找到对应实机版本。不能再称仓库只有 `.234`：完整公开源码基线仍为 `.234`，开发分支另有至 `.257` 的补丁及较新 Windows 桥接代码。

对照确认 Windows 普通电脑状态仍使用 `version=1`；通用 RPC 已支持 HTTP/1.1 复用，但此前 Mac r50 单独加入该行为未通过实机。`.236` 增加主机代次核验，`.253` 的有界等待用于 USB／BLE 邮箱内的解锁凭据请求，`.257` 新增 Wi-Fi OTA 范围读取；这些差异尚不能证明当前电脑状态失败根因。另发现 Mac `lock-screen` 未带 Windows 返回的 `version=1`，但该探测与普通 `status` 不同，不能把它直接认定为本次桌面状态间歇失败原因，未据此部署猜测性修复。

8 个只读参考文件及其 SHA-256 清单保存在忽略目录 `artifacts/mac-windows-reference-20261023-a9a70d2/`。本轮未改变产品代码或运行候选；需继续取得 `.257` 到实机 `.262` 的源码差异。

Following the user's GitHub reference, the Windows development branch was fetched and pinned to the commit above. It registers .257, not the device's .262; branch enumeration and a .262 commit search found no matching source. The earlier statement that only .234 is available is corrected: .234 is the full published source baseline, while this branch contains newer Windows code and patches through .257. Ordinary computer status retains version=1. Windows supports generic RPC keep-alive, but the Mac r50 trial alone failed physical acceptance. Host-generation checks, USB/BLE credential-mailbox admission and Wi-Fi OTA changes do not establish this fault's cause. The Mac lock-screen probe also lacks the Windows version field, but it is distinct from ordinary status and was not treated as proof of the intermittent desktop failure. Eight read-only reference files and hashes are retained locally; product code and r51 deployment are unchanged. The .257-to-.262 source delta is still needed.

### 已取得 .262 源码覆盖包 / .262 source overlay obtained

用户更新 GitHub 后，已抓取同一 Windows 开发分支的 `2fb1b14c09550bce27173eb349d78187d1661ce7`（前一提交 `2e77bc2` 添加源码，后一提交保留归档字节）。`versions/TAB5` 现为 `0.2.262-ui`；`docs/development/tab5-262-source/SOURCE.json` 所列 39 个文件的字节长度和 SHA-256 均已用 Git 原始 blob 核对通过，保存于忽略目录 `artifacts/mac-tab5-262-reference-20261023/`。登记的应用 SHA-256 为 `4d5479a1332cdcb02afd68d668f5249a097c1ef116ef744c3fb376ef4ce42fa5`。这是 .257 基线上的完整修改文件覆盖包，不冒称独立完整 SDK 工程或已重建镜像；本轮未构建或刷写设备。

此前“缺少 .257 到 .262 源码差异”的阻塞已解除。实际 `.262` 的 `rpc_link.c` 确认 `rpcDiag` 的 Wi-Fi 槽位仍为第 2 组，阶段 1 表示未取得完整响应，后续阶段才是解密、解包和响应体处理。`http_exchange` 与 `.234` 基线在规范化换行后逐字一致；新增照片加密只走 `image-chunk` 分支，普通电脑状态仍使用原加密路径。此前阶段 1／状态 -1 的记录因此指向 HTTP 响应获取失败，而不能归为解密或 JSON 字段不兼容；这尚不能区分设备发起失败、链路问题和主机响应交付失败。继续保留 r51 与未通过的实机结论。

After the user updated GitHub, the same branch was fetched at the new commit above. The version now registers .262. All 39 manifest entries were verified against raw Git blob byte counts and SHA-256 values and retained in an ignored local reference directory. This is a full changed-file overlay on .257, not a standalone SDK project or a rebuilt binary; no firmware build or flash occurred. The source-delta blocker is resolved. The actual .262 RPC implementation confirms that the second diagnostic group is Wi-Fi and stage 1 precedes complete-response acquisition, decryption and unpacking. Its HTTP exchange function matches .234 after newline normalization, while new photo encryption is restricted to image-chunk. Prior stage-1/status-minus-1 failures therefore concern HTTP response acquisition, not evidence of a decryption or JSON-field mismatch. Device initiation, network transport and host delivery remain to be distinguished; r51 and the unaccepted hardware results remain in place.


### 转向 USB 豆包复测 / USB Doubao follow-up

- 用户说明 Windows 端也有电脑控制反复断连并已在那里定位，要求 Mac 停止重复排查。停止 LAN 诊断工作，撤回本轮临时请求计数与事件追踪；保留此前已验证的代码，不改变协议或设备固件。纯 Wi-Fi 解锁最终验收等链路修复后继续。
- USB 重新连接后，r52 的实际语音选择明确为豆包。用户报告第一段说到一半失败、第二段成功；主机第二段显示 USB 查询 200、32 块／6.4 秒音频、设备请求停止、停止至最终识别 2660 ms、最终结果至响应生成 499 ms，麦克风自动恢复原选择。随后界面还观察到另一段 34 块／6.8 秒的最终结果。它们证明 USB＋豆包有成功路径，但不能证明首次启动稳定，也不能把 Mac 生成响应等同设备成功取回。
- 新增最近三次语音结束原因与豆包输入 IO／Fn 过程的本机内存诊断；不记录音频、识别文字、会话标识或鉴权材料。下一次录音不再覆盖此前失败摘要。r54 是诊断候选，不宣称已修复中断。
- r54 位于 `artifacts/mac-doubao-20261023-r54/AIBotBridge.app`，Release 构建、固定身份签名及深度验证、342 项隔离检查通过。`swift test` 仍因主机缺少 XCTest 模块失败，未运行完整套件。已正常退出旧候选并部署 r54，真实 USB 数据确认，开始单次复测。

The user assigned the shared computer-control disconnect investigation to Windows; Mac stopped duplicate LAN diagnosis and removed this round's temporary network counters. Final pure-Wi-Fi unlock acceptance awaits that fix. With USB reconnected and Doubao explicitly selected, the user reported one interrupted attempt followed by success. The host confirmed 32 audio chunks / 6.4 seconds, a device stop, a final result 2660 ms later, a USB 200 poll and automatic microphone restoration. A subsequent 34-chunk / 6.8-second final result was also visible. These demonstrate a working USB/Doubao path, not reliable first-attempt behavior or proof that every generated response reached the device. r54 adds bounded in-memory session outcomes and capture/Fn transitions without transcripts, audio or owner/authentication data. It is diagnostic, not a claimed fix. Release/signature verification and 342 isolated checks pass; full XCTest remains unavailable. r54 was deployed and USB confirmed for one controlled follow-up attempt.


### r54 单次实机结果 / r54 physical result

用户明确确认本次测试成功。界面识别方式为豆包，真实 USB 查询 200，收到 34 块／6.8 秒 TAB5 音频；结束原因为设备请求停止。豆包过程为准备麦克风、Fn 按下、输入 IO 从停止到开启、请求结束、Fn 释放、输入 IO 停止、最终文字确认，没有提前结束事件。首段结果 9873 ms（含说话等待），停止至最终识别 3326 ms，最终结果至响应生成 374 ms。主机确认麦克风已恢复原选择；随后通过设置界面切回 Mac 本地识别并核对成功提示。

结论限定为本次 USB＋豆包端到端成功，未再复现先前偶发中断。r54 没有声称修复该偶发故障；其最近三次元数据诊断保留在内存中，便于下次复现时核对。电脑控制由 Windows 侧继续定位；Wi-Fi 一键解锁最终验收继续等待该修复。未锁屏、刷机、修改 TAB5 展示模式或发布。

The user confirmed the r54 attempt succeeded. Doubao was explicitly selected; the host received 34 chunks / 6.8 seconds from TAB5 and answered the USB poll with 200. Capture proceeded from Fn down through confirmed input IO, explicit device stop, Fn release, stopped input IO and confirmed final text, without an early-stop event. Timing was 9873 ms to first text (including speaking), 3326 ms from stop to final and 374 ms from final to response generation. Microphone restoration was confirmed, then the daily provider was restored to native Mac recognition through the UI. This is one successful end-to-end USB/Doubao acceptance attempt, not proof that the prior intermittent fault was fixed. Windows owns the computer-control investigation; final Wi-Fi unlock acceptance waits for that fix. No screen lock, firmware/display change or publication occurred.


### 识别延迟定位与候选 / Recognition latency diagnosis and candidate

r55 用户确认成功，分段时序为：0 ms 准备、1778 ms Fn 按下、2087 ms 确认输入 IO、7307 ms 收到停止（待播音频 2220 ms）、9680 ms Fn 释放、9882 ms 输入 IO 停止、10218 ms 输入法提交、10897 ms 最终确认。主机停止至最终识别 3591 ms、最终至响应生成 6 ms；本次主要延迟为准备音频积压，并非请求查询。

r56/r57 候选在 Mac 内部区分首次就绪前的链路探测与就绪后的正式音频：首次实际输入 IO 确认前的音频仅校验／确认序号，不进入播放队列；界面明确等待“可以说话了”。首次就绪后出现暂时未知，继续有界缓存并按顺序转送，不丢正式音频。重复块不重播，准备阶段停止不声称成功。r57 还在异步主线程启动前同步清除旧会话 ready，避免快速取消再启动继承旧状态。固件与 Windows 调用方的 ready／音频确认流程已阅读；这是 Mac 的准备策略，不增加字段、版本或设备命令，不修改 Windows 实现或刷机。r56 调试独立检查 345 项通过；r57 构建及提速实测待补。

The user confirmed r55 succeeded. Its measured stop-to-final delay was 3591 ms, including a 2220 ms pending playback backlog at stop; Fn release occurred 2373 ms after stop, IME commit 538 ms after release, and final confirmation 679 ms later. Response generation followed final confirmation by 6 ms. r56/r57 treat pre-ready device packets as validated transport probes instead of accumulating playback delay. All post-ready recording audio remains preserved, including bounded buffering across temporary readiness loss. Duplicate chunks are not replayed and stopping during preparation cannot claim success. r57 also clears prior readiness synchronously before main-queue startup. Caller and counterpart ready/ack behavior was checked; no wire fields, versions, Windows changes or firmware deployment are involved. r56 passed 345 debug isolated checks; final r57 build and physical timing remain to be recorded.


r57 Release 构建、固定身份签名／深度校验、345 项隔离检查及 `git diff --check` 通过。相邻 SHA-256 清单包含 11 个文件，包内候选说明与仓库一致。完整 XCTest 仍因本机工具链缺少模块未执行。尝试切换候选时界面工具报告 Mac 锁定并暂停自动操作，尚未退出 r55／部署 r57；已请用户回到可操作桌面。提速效果尚无 r57 实机结果，不能按理论积压时长宣称实际改善。

r57 Release/signature/deep verification, 345 isolated checks and whitespace validation pass. Adjacent hashes cover 11 files and the bundled guide matches the repository. Full XCTest remains unavailable. The UI tool reported a locked Mac and paused automation before the candidate switch, so r55 has not yet been replaced by r57. The user was asked to return to the desktop. No r57 hardware speedup is claimed from the theoretical removed backlog.


用户手动恢复桌面后，已正常退出 r55 并启动 r57，语音设置核对为豆包，等待接回 USB 进行提速复测。启动后真实设备版本已变为 0.2.263-ui（非本轮刷写）。用户新增观察：USB 接入时电脑状态正确，拔 USB 后持续“正在读取电脑状态”；仅记录该对比，继续由 Windows 侧排查。后续延迟对比需注明固件 .262 → .263 环境变化，不能视为严格同版本 A/B。

After the user unlocked the desktop, r55 exited normally and r57 launched with Doubao selected. The device now reports .263 (not flashed by this task). The user observed correct computer status over USB and continuous reading after unplugging; this evidence is recorded for the Windows-owned investigation only. Speed measurements across the .262-to-.263 firmware change are not a controlled same-firmware A/B.


### r57 提速实测完成 / r57 latency acceptance

用户确认“这一次成功，并且返回速度也挺快”。实际诊断：收到 25 块／4.8 秒音频，其中首次就绪前探测为 1.8 秒；停止时仅待播 240 ms。时序为准备 0 ms、Fn 按下 1701 ms、输入 IO 开启 2008 ms、停止请求 5125 ms、Fn 释放 5607 ms、IO 停止 5805 ms、输入法提交 6126 ms、最终确认 6812 ms。停止至最终结果 1687 ms，最终至响应生成 100 ms，最近请求为 **Wi-Fi 查询 200**，不是 USB；不能将本次标作 USB-only 验收。与 r55 的 3591 ms 相比，本次实测更快，但固件 .262→.263、通道及说话时长均不同，不据此保证固定百分比改善。准备积压从 2220 ms 降到 240 ms，支持针对准备回放积压的修复有效。

麦克风自动恢复原选择已核对；本轮结束通过设置界面恢复 Mac 本地识别，显示保存成功。r57 保持运行。Release、签名和 345 项隔离检查通过，完整 XCTest 缺模块的限制不变。原来的偶发中断尚不宣称完全消除，保留不含文字的最近三次诊断。用户另确认电脑页长时间等待后变为“电脑已连接／未锁屏”；这表明最终有恢复，但不证明重连及时或持续稳定，仍由 Windows 侧负责定位。未继续锁屏／解锁验收，没有新发布或固件写入。

The user confirmed successful recognition with noticeably faster return. r57 received 25 chunks / 4.8 seconds, including 1.8 seconds of pre-ready probes. Pending playback at stop was 240 ms. Stop-to-final measured 1687 ms (versus r55's 3591 ms); final-to-response generation was 100 ms. The latest successful poll was **Wi-Fi 200**, so this attempt is not classified as USB-only. Firmware, transport and utterance duration differed, preventing a controlled percentage claim. The reduction from 2220 to 240 ms pending playback supports the preparation-backlog fix. Automatic microphone restoration was confirmed and the daily provider restored to native Mac recognition through the UI; r57 remains running. Build/signature/345 isolated checks passed; XCTest remains unavailable. Earlier intermittent interruptions are not declared eliminated. The user also observed delayed eventual computer-status recovery, which remains assigned to Windows and does not establish stable reconnection. No further unlock test, firmware write or publication occurred.


### r58/r59 解锁诊断与焦点冲突 / Unlock diagnostics and conflicting focus

用户最新报告 Windows 状态与锁屏／解锁正常，Mac 状态与锁屏已恢复；USB 解锁弹出 PIN 界面，拔 USB 后 Wi-Fi 解锁失败。用户确认 .264 相关解锁源码没有变化，以已核对的 .262 为准。该固件 USB 就绪时使用 Windows PIN 路径；Mac 不实现该凭据通道，不能在此输入 Mac 密码。Wi-Fi 路径允许蓝牙保持开启，沿用已保存的本机钥匙串配置。

r58 已构建并启动，347 项隔离检查通过。固件结束时无论成败都会 best-effort cancel；Mac 原先覆盖真实结果为“已取消”。现保留终态及本机连接／认证／键盘静止／密码框／输入／确认阶段，诊断不含密码或请求标识。协议阶段仍只发送原有值。尚未进行 r58 的新解锁实测。

随后用户补充：ChatGPT Computer Use 执行期间锁屏会被误报为未锁屏。代码核对发现原逻辑优先采信 AX 与 NSWorkspace 前台一致，覆盖已观察到的锁屏通知；这能解释该现象，但尚未采集同一次真实锁屏的信号对照。r59 在显示路径让锁定信号优先；凭据／成功判定路径遇到“桌面焦点＋锁屏观察”时返回未知，防止提前宣称成功。通知只能否决，不能授权输入或单独确认成功；签名 loginwindow、当前控制台和密码框检查保留。读取焦点前后均检查锁定观察。通知缺失、启动时未知及远程会话差异仍需实机验证，不宣称已穷尽所有锁屏识别问题。新增隔离检查覆盖冲突、缺失信号、解锁后恢复及通知不能授权输入。r59 构建及实机结果待补。

The user now reports functioning Windows controls and Mac status/locking, but a USB PIN prompt and failed Wi-Fi unlock. They confirm the relevant .264 unlock source is unchanged from the verified .262 reference. USB selects the firmware's Windows PIN path, which does not support Mac credentials. Wi-Fi can coexist with Bluetooth and uses the existing local Keychain enrollment. r58 was built and launched with 347 isolated checks passing. It retains terminal outcomes when the firmware sends its unconditional cleanup cancel, with bounded local progress/failure diagnostics and unchanged wire stages; no new r58 unlock attempt has occurred.

The user then reported false unlocked status while Computer Use is active. Source inspection found foreground AX/NSWorkspace agreement previously overrode an observed lock, consistent with the report but not yet a simultaneous physical signal capture. r59 gives lock evidence precedence for display and returns unknown for contradictory desktop focus in credential/success decisions. Notifications may only veto, never authorize typing or independently prove success; signed loginwindow, console and secure-field gates remain intact. Observations are checked before and after focus inspection. Missing notifications, startup uncertainty and remote-session behavior still need physical verification. Isolated checks cover conflict, missing evidence, post-unlock recovery and non-authorizing notifications; r59 build and hardware results are pending.


r59 已完成 Release 构建、固定身份签名／深度校验、357 项隔离检查及 `git diff --check`。`swift test` 仍因主机 `no such module 'XCTest'` 未运行完整套件。已通过应用菜单正常退出 r58 并启动 r59；界面确认 .264、USB 未连接、Wi-Fi 收到已认证请求，一键解锁保持启用且加载原钥匙串配置。已请用户先观察真实锁屏状态，只有状态正确时才尝试一次 Wi-Fi 解锁；实机结果待回报。未改密码、配对、蓝牙开关、日常语音提供方或设备固件。

r59 Release build, fixed-identity/deep signature verification, 357 isolated checks and whitespace validation passed. Full XCTest remains unavailable because the host lacks its module. r58 exited normally through its menu and r59 launched. UI verification confirmed .264, USB disconnected, authenticated Wi-Fi requests, and unchanged enabled unlock/Keychain enrollment. The user was asked to verify physical lock status first and try one Wi-Fi unlock only if that status is correct; results remain pending. No credentials, pairing, Bluetooth setting, daily speech provider or firmware were changed.


### r59 失败回执与 r60 HTTP 候选 / r59 failure and r60 HTTP candidate

用户反馈本次 Computer Use 没有执行，因此未复现误报，不能认定 r59 已通过该场景；Wi-Fi 解锁仍失败。刷新本机诊断得到“设备已结束解锁请求；阶段：认证本机账户；已收到 0 次续期”。这证明 begin 已接受、设备取得 offer 并随后 cancel，但并未进入密码输入，也未收到第一次 poll；不能据此认定密码不正确。

核对 .262 对端后发现 `wifi_unlock_run` 的 begin/poll/cancel 复用一个 ESP HTTP client，`http_exchange` 成功读取后不关闭连接，而 Mac 普通 RPC 原先总是 `Connection: close`。Windows 已核对分支的 `LanStatusServer.HandleVoiceAsync(rpc:true)` 保持普通 RPC 连接。r60 让 Mac `/tab5/v1/rpc` 采用现有 voice 端点的有界顺序连接复用；每条仍独立鉴权、解密、校验会话与 nonce，原有序号、2.5 秒租约、40 秒总期限、取消和不重试规则不变。HTTP/1.0、显式关闭、鉴权失败及其他路由仍关闭连接。没有修改固件或 Windows 协议。

新增持久连接回归测试使用固定合成配对与无副作用的未知操作，通过真实服务的鉴权和加密响应路径。因缺少 XCTest，使用本机 Debug 编译对象链接现有测试与断言适配器（忽略目录 `artifacts/mac-http-r60-harness/`），客户端不自动重连。修复前首个 RPC 返回有效加密响应，下一次同连接请求失败；修复后 5 个场景通过，包含连续 RPC、重放 409、后续坏 proof 401 并关闭、显式关闭／HTTP1.0、既有语音连接和其他路由。这是可复现的连接缺陷及本机修复证据，尚不是实机解锁通过。此前 r50 同类候选未解决电脑状态断连的历史结论保留；这次针对新取得的“begin 后零续期”解锁证据，不把旧状态页问题重新判定为同一根因。

The user reports Computer Use was inactive during this attempt, so r59's interference scenario remains unverified; Wi-Fi unlock still failed. The retained diagnostic shows device cancellation during local account authentication with zero polls. Begin was accepted and its offer reached the device, but password input and the first renewal did not occur; this is not evidence of an incorrect password.

The verified .262 worker reuses one ESP HTTP client for begin/poll/cancel and does not close successful exchanges. Mac previously closed ordinary RPC responses; the verified Windows branch keeps them open. r60 extends the existing bounded, sequential voice connection handling to `/tab5/v1/rpc`. Every request retains independent authentication, decryption, session/nonce validation; sequence, 2.5-second lease, 40-second deadline, cancellation and no-retry behavior are unchanged. HTTP/1.0, explicit close, failed authentication and other routes still close. No firmware or Windows protocol changes are involved.

A no-reconnect local socket fixture linked actual Debug objects, synthetic pairing and effect-free requests; a temporary assertion adapter ran the test source because XCTest is unavailable. Before the change, the first encrypted RPC succeeded and the next same-socket exchange failed. Afterward five scenarios passed, covering sequential RPC, replay rejection, bad proof closure, explicit close/HTTP1.0, voice regression and other routes. This proves the local connection defect and fix, not physical unlock success. The earlier r50 candidate's failed computer-status acceptance remains recorded; the new evidence concerns zero unlock renewals and does not retroactively establish the old status-page root cause. r60 Release/deployment and physical acceptance are pending.


r60 Release 构建、固定身份／深度签名验证、357 项独立检查及 `git diff --check` 通过；5 个真实编译对象 socket 场景另行通过。完整 XCTest 仍因缺少模块未执行。11 个包内文件 SHA-256 已写入相邻清单。已通过菜单正常退出 r59 并启动 r60，界面确认已加载钥匙串配置且 Wi-Fi 解锁开关保持开启。此时用户已接回 USB，设备 .264、USB 数据确认、Wi-Fi 已认证、蓝牙已认证待命；已请用户拔 USB 后进行一次 Wi-Fi 解锁，结果待回报。Computer Use 活动时锁屏误报的实机验证仍未完成。

r60 passed Release build, fixed-identity/deep signature verification, 357 isolated checks and whitespace validation, plus five separate socket scenarios using actual compiled objects. Full XCTest remains unavailable. An adjacent SHA-256 manifest covers 11 bundle files. r59 exited normally and r60 launched with the saved Keychain configuration and Wi-Fi unlock still enabled. USB had been reconnected; UI showed .264, confirmed USB data, authenticated Wi-Fi and authenticated standby Bluetooth. The user was asked to unplug USB and make one Wi-Fi unlock attempt; results and the active-Computer-Use lock-state scenario remain pending.


### r60 实机解锁与系统回锁 / Physical unlock and system relock

用户报告期间一次解锁成功，另两次回到桌面后很快重新出现密码／Touch ID 锁屏，另有接电自动休眠后无法唤醒／解锁。本机 r60 回执为“本次解锁已确认成功”；与用户看到短暂桌面一致，但不等于保持解锁。读取限定时间的系统事件（不保存原始日志）确认两次登录成功后约 0.6–0.7 秒，`ScreensharingAgent` 向 loginwindow 请求 `SACLockScreenImmediate`，触发真实回锁。根因定位到共享会话结束后的系统恢复行为，不是 TAB5 状态文案。`RestoreMachineState` 当前没有显式设置，本机 Apple ScreensharingAgent 二进制包含该键；尚未修改此系统策略。

r61 在 RFB 连接及短期显示活动结束后，要求桌面连续两秒可确认，最多等三秒；再次锁屏返回明确本机失败说明，不重发密码或按键。新增已通过原有配对、开关、租约和锁屏校验的请求范围内显示活动，使用公开 IOKit `IOPMAssertionDeclareUserActivity`，四十秒上限并在结束／失败时释放，不改全局睡眠策略。它只能在 Mac 仍能处理请求时唤醒显示器，不能把整机深睡眠的网络唤醒宣称已实现。用户说接电自动休眠；所读电源设置在 AC 下允许网络唤醒、自动整机睡眠为 0，相关最近测试时段未记录新的整机 Sleep，显示器休眠与早前整机睡眠仍需按场景验收。

新增隔离检查覆盖断开后两秒确认、0.6 秒回锁、未知／取消／慢查询、活动正常与异常释放。准备了仅修改 `RestoreMachineState` 及删除该测试覆盖项的本地脚本，尚未执行；该策略影响本机其他屏幕共享会话的结束行为，需要用户明确确认后才可调整。不在桥接中偷偷设置，也不长期保持 VNC 连接来掩盖回锁。r61 构建与部署待补。

The user reports one successful unlock and two brief desktop returns followed by actual password/Touch ID lock screens, plus a wake/unlock failure after automatic sleep on AC. r60's success receipt matches the brief desktop return but not persistent unlock. Scoped system events (raw logs not saved) show ScreensharingAgent requesting immediate loginwindow lock about 0.6–0.7 seconds after both successful logins. This identifies the system session-ending restore behavior. The RestoreMachineState preference is absent and the installed Apple agent contains that key; no system policy has been changed.

r61 closes the RFB connection and scoped display activity before requiring two seconds of confirmed desktop within a three-second window. Relock produces a specific local failure, never repeated password input. A bounded public IOKit user-activity assertion wakes the display only inside an already-authorized unlock attempt and is released on completion/failure. It does not change global sleep policy or implement network wake from full system sleep. AC settings allow network wake with automatic system sleep disabled; no new full Sleep event appeared in the inspected recent test interval, so display-off and earlier system-sleep cases still need distinct physical acceptance. New isolated checks cover post-close stability, delayed relock, unknown/cancelled/late reads and activity cleanup. Local preference-change and rollback scripts are prepared but not executed: changing the system-wide screen-sharing disconnect policy requires explicit user confirmation. r61 build/deployment remain pending.


r61 Release 构建、固定身份／深度签名、364 项隔离检查和差异格式校验通过；完整 XCTest 仍因缺少模块未执行。相邻 SHA-256 清单含 11 个包内文件。已正常退出 r60 并启动 r61；真实设备 .264，USB 未连接、Wi-Fi 已认证。系统 RestoreMachineState 未修改，调整请求等待用户确认；没有开始新一次锁屏／解锁，也没有改动全局睡眠设置。回锁策略、显示器熄屏唤醒及整机睡眠唤醒的实机结果均仍待确认。

r61 passed Release build, fixed-identity/deep signing, 364 isolated checks and whitespace validation; full XCTest remains unavailable. Adjacent hashes cover 11 bundle files. r60 exited normally and r61 launched against .264 with USB disconnected and authenticated Wi-Fi. RestoreMachineState and global sleep preferences remain unchanged; the system-policy change awaits explicit confirmation. No new lock/unlock test has begun. Persistent unlock, display wake and full-system sleep wake still require physical acceptance.


## r62：仅本次请求的 AX 候选与唤醒边界 / Request-scoped AX candidate and wake boundary

用户明确要求不修改系统全局设置。此前 RestoreMachineState 请求已撤销，不再等待批准；策略及睡眠设置保持原样，先前准备的两个全局策略脚本已改为 `.disabled` 且去除可执行权限。本轮没有使用私有 API、持久防休眠断言或常驻 VNC 会话。

设备入口改为公开辅助功能接口候选，不建立屏幕共享连接：确认当前控制台及 Apple 签名的 loginwindow、安全密码框、同一焦点后，先检查 AXValue 可写及字段 AXConfirm 或同窗口的 AXDefaultButton/AXPress。仅全部支持时单次替换并单次提交，不读取 AXValue，不猜按钮或坐标，不使用 CGEvent 或 RFB 兜底。接口拒绝、取消、失焦、超时或不确定写入均停止；迟到结果不重试。提交后等待并连续两秒独立确认桌面，回锁不报成功。系统是否对真实锁屏提供这些接口尚未知，构建与隔离检查不能证明可用。

已认证、已启用且绑定当前设备的 begin 在焦点未知时可进入有界显示器唤醒准备，明确桌面状态仍拒绝。40 秒上限的本次活动断言先于最多三秒锁屏确认，只有新鲜的真实登录窗口证据才允许密码输入；结束或异常释放。睡眠与会话退出仍取消在途请求，唤醒不恢复旧输入。新增只保存在内存的睡眠／唤醒计数及最近请求年龄，显示在本机诊断，便于区分无请求、熄屏及整机睡眠。

整机睡眠时本机桥接不能自己接收请求或自行执行唤醒。Apple 的网络唤醒依赖现有系统设置与网络设备支持；不替用户改设置。所核对 .262 开发分支没有发现远端魔术包／网络唤醒步骤，`tab5_wifi_wake` 只是唤醒设备自己的轮询任务；用户已明确说明相关 .264 代码与 .262 相同。因此这里只修复可运行桥接的显示器唤醒前置顺序，不能声称已修好真正的整机睡眠唤醒。后续实际验收要分别记录，不能通过永不休眠掩盖问题。

The user rejected all global policy changes; the prior approval request is withdrawn. RestoreMachineState and sleep preferences remain untouched; the prepared policy scripts are disabled. The saved-device candidate uses public AX only: current-console/signed-loginwindow/secure-field/focus checks, writable-value and explicit confirm/default-button capabilities before a single replacement and submission. Unsupported or uncertain operations stop without RFB/CGEvent fallback or retry. No secure value is read. Success requires two seconds of continuously verified desktop; physical AX availability is unproven.

An authenticated enabled request with unknown focus may enter bounded display-wake preparation, never credential input. The existing 40-second activity bound and three-second fresh-lock wait precede input; cancellation/sleep invalidates the attempt. Memory-only power-event counters and last-request age support diagnosis. A sleeping process cannot wake itself; full-system network wake depends on the existing system/network support and an external wake source. The .262 reference has no network-wake sender; its tab5_wifi_wake wakes only the device polling task. This candidate does not claim full-system wake support.

References: [Apple AXUIElementPerformAction](https://developer.apple.com/documentation/applicationservices/1462091-axuielementperformaction), [Apple display activity](https://developer.apple.com/documentation/iokit/1557127-iopmassertiondeclareuseractivity), [Apple network wake](https://support.apple.com/en-gb/guide/mac-help/mh27905/mac).

Validation pending: Release build/signature and deployment. Debug executable passed 380 isolated checks; full `swift test` remains blocked by missing XCTest. New checks cover unknown-to-locked readiness, desktop/unknown/late rejection, one-write/one-submit failure boundaries, delayed login, stable desktop, relock and cancellation. No real lock/credential operation was run by these checks.

只读电源核对补记：本机接电配置 `sleep=0, displaysleep=10, womp=1`，电池配置 `sleep=1, displaysleep=2, womp=0`，本轮未修改。10 月 23 日最后一组已记录整机 Sleep/Wake 为 19:03–19:04，早于 19:31–19:32 回锁测试；尚无对应故障时刻的整机 Sleep 证据。因此优先验收显示器熄屏路径，不能将用户口述的自动休眠直接等同于已经确认的整机睡眠。原始电源日志未保存。

Read-only power verification: AC sleep=0/display=10/womp=1; battery sleep=1/display=2/womp=0, unchanged. The last recorded Oct 23 system sleep/wake cluster is 19:03–19:04, before the 19:31–19:32 relock tests. Full-system sleep at the reported failure is not established; display-off acceptance comes first. Raw power logs were not persisted.

r62 Release 构建、固定身份／深度签名验证和 380 项隔离检查通过，`git diff --check` 无错误；完整 XCTest 仍缺模块。11 个包内文件已生成 SHA-256 清单。已通过菜单正常退出 r61、启动 r62，界面确认新 AX 路径说明、钥匙串配置已加载及允许开关仍开启；TAB5 .264、USB 未连接、Wi-Fi 已认证、蓝牙数据已确认。请求用户进行一次普通锁屏验收，结果待回报；未由工具锁定或解锁机器。熄屏与真实整机睡眠验收尚未进行，不标记完成。

r62 Release build, fixed-identity/deep signature, 380 isolated checks and whitespace validation passed. Full XCTest remains unavailable. Eleven bundle files have adjacent SHA-256 hashes. r61 exited normally through its menu and r62 launched; UI confirms the new AX path, loaded Keychain configuration, existing enabled switch, .264 firmware, USB absent, authenticated Wi-Fi and confirmed BLE. One human-initiated lock/unlock test is pending. No tool locked/unlocked the machine; display-off and full-system sleep acceptance remain outstanding.

### r62 首次实测失败 / First physical attempt failed

用户回报未成功。手动解锁后通过桥接“刷新解锁诊断”取得：阶段为确认登录密码框，系统尚未公开可聚焦的密码框，未输入密码、未建立屏幕共享；已收到 19 次续期。当前运行整机睡眠／唤醒计数均为 0，普通 Wi-Fi 状态请求正常。故本次失败定位于密码框准备，尚不能判定 AXValue 或提交动作是否受支持。已请用户进行一次手动 Tab 显示空白密码框后再触发的诊断试验；不把这一步作为最终一键解锁操作要求，也不修改系统设置。

The user reports failure. After manual unlock, the bridge's refreshed diagnostic identifies missing public password-field exposure during preparation, before any credential input or screen-sharing session, with 19 successful lease renewals. Sleep/wake counts for this run are both zero; ordinary Wi-Fi status requests are healthy. This does not yet establish whether AXValue/confirmation is supported. A single user-assisted Tab-to-reveal diagnostic is pending; manual preparation is not the intended final one-click workflow. No global settings changed.

### r62 手动显示密码框后的结果与 r63 提交候选

用户手动显示密码框后再次尝试，看到密码已填入但未解锁。刷新本机诊断：进入“确认桌面恢复”，72 次续期，八秒内未确认桌面，整机睡眠／唤醒仍各为 0。这说明写入和选中提交动作均返回成功，但不证明实际触发了身份验证，也未取得密码错误的证据。r62 没有记录选择了字段 AXConfirm 还是默认按钮，不能事后声称必然走过某一个分支。

r63 只使用同一签名登录窗口的 AXDefaultButton + AXPress，不再选字段 AXConfirm。写入前确认按钮存在、所属进程、角色、动作及与密码框窗口的关联；AXWindow 缺失时只允许有界父链确认，不猜前台窗口。写入后再次确认同一字段／窗口／默认按钮及 enabled，最多按一次，异常不退回另一动作。增加“按下登录窗口默认按钮”阶段，仍映射既有 wire sending。密码框自动显示尚未解决，继续使用一次手动显示的诊断试验来隔离提交问题，不声称已完成一键解锁。全局设置不变。

After manually revealing the field, the user observed populated password bullets but no unlock. The bridge reached desktop confirmation with 72 renewals and no sleep/wake events; both write and selected action returned success, but actual authentication is unproven. r62 did not retain which submission branch was chosen; neither AXConfirm nor default-button use can be asserted retrospectively. r63 exclusively binds the same signed login window's explicit default button and presses it once, with checks before write and before press, bounded parent-chain membership when AXWindow is absent, no alternative-action retry, and a distinct local stage mapped to the existing sending stage. Automatic field reveal remains unresolved. Build and physical acceptance pending; global settings unchanged.

r63 Release 构建、固定身份／深度签名、380 项隔离检查及 `git diff --check` 通过；完整 XCTest 仍因缺少模块未执行。11 个包内文件的 SHA-256 清单已生成。正常退出 r62 并启动 r63，界面确认钥匙串已加载、允许开关仍开启、.264 设备 Wi-Fi 已认证且蓝牙数据确认，USB 未连接。已请用户一次手动显示密码框后验证默认按钮提交；结果待回报。没有改系统策略或睡眠设置，也未执行工具发起的锁屏或解锁。

r63 passed Release build, fixed-identity/deep signing, 380 isolated checks and whitespace validation; full XCTest remains unavailable. Eleven bundle files are hashed. r62 exited normally and r63 launched with existing enrollment/enablement and authenticated Wi-Fi/confirmed BLE against .264, USB absent. A human-initiated default-button submission test after manual field reveal is pending. No global policy/sleep changes or tool-initiated lock/unlock occurred.

### r63 实测与 r64 明确标注按钮候选

用户补充 r62 填入后一直停留原处，没有反应。r63 则没有填入；桥接回执为“登录窗口未公开默认提交按钮”，20 次续期、整机睡眠／唤醒计数仍均为 0。故 r63 停止点明确，不把它说成写入失败。

r64 在同一密码框所属、Apple 签名登录窗口内有界查找控件：仍优先显式 AXDefaultButton；缺失时只接受唯一、支持 AXPress、公开按钮标题或无标题按钮描述精确等于 Login/Log In/Sign In/Unlock/登录/登入/解锁的后代。最多 64 个节点、8 层、1 秒；不读 AXValue、文本框标题，不记录原始控件标题／描述／标识或账户文本，只保留按钮总数、可按数量。多个候选、结构超界、未知接口、会话或焦点变化均停止。写入前及按下前重查唯一身份；仍只按一次，不在动作失败后换目标。自动显示密码框未完成，本候选不能宣称完整一键解锁或睡眠唤醒已修好。

The user clarified that r62 remained still after populating the field. r63 stopped before writing because AXDefaultButton was absent, with 20 renewals and no sleep/wake events. r64 permits a unique explicitly labelled AXPress button within the same signed login window when AXDefaultButton is absent. It uses exact localized login/unlock captions, rejects ambiguity and bounds inspection to 64 nodes/eight levels/one second. No secure values or text-field titles are read; raw captions/identifiers/account text are not recorded. Only button/pressable counts enter diagnostic failures. The chosen identity is rechecked before write and press; actions never fall back or retry after uncertainty. Automatic field reveal and full sleep wake remain unresolved.

图标问题另行核对：当前 Info.plist 未指定 CFBundleIconFile，打包脚本未生成／放入 icns；菜单栏只设 title，LSUIElement=true 隐藏 Dock 常驻图标。本轮只解释原因，未混入图标设计或改变 Dock 行为。

The separate icon question was traced to absent CFBundleIconFile/icon packaging, text-only status-item titles and LSUIElement=true. No icon redesign or Dock-behavior change is included in this unlock candidate.

r64 Release 构建、固定身份／深度签名及 391 项隔离检查通过；完整 XCTest 仍缺模块。11 个包内文件的 SHA-256 清单已生成，差异格式校验通过。通过菜单正常退出 r63 并启动 r64，界面确认钥匙串配置已加载、开关保持开启、.264 USB 未连接、Wi-Fi 请求正常；蓝牙启动时曾显示归属确认失败等待重连，未改蓝牙设置，不把该瞬时状态写成已恢复。已请用户进行一次手动显示空白密码框后的验证，结果待回报。

r64 passed Release build, fixed-identity/deep signing and 391 isolated checks; full XCTest remains unavailable. Eleven files are hashed and whitespace validation passed. Normal menu quit/relaunch completed; enrollment and enablement remain, .264 USB is absent and Wi-Fi requests are healthy. BLE initially showed ownership confirmation pending retry; no BLE setting was changed and recovery is not claimed. One human-initiated empty-field test is pending.

### r64 失败与 r65 只读控件诊断

用户报告 r64 不成功；刷新回执显示同一登录窗口中按钮 3 个、可按 3 个，但无默认或明确标注的登录按钮，尚未输入密码，20 次续期，整机睡眠／唤醒各 0。源码核对发现 r64 跳过文本框子控件，可能遗漏内嵌提交按钮；尚无实际结构证据，不认定这就是根因。

r65 临时诊断候选在验证签名登录窗口并聚焦安全框后只读取控件结构，**无论结果如何都在密码写入／提交之前停止**。包含文本框 AXChildren，但不读 AXValue、字段文字、提示或窗口标题。最多 64 节点、8 层、2 秒及前 5 个按钮，采集按钮短标题／描述／标识、深度、是否属于输入框后代及是否支持 AXPress。当前账户名／全名会替换为占位符，超长或控制字符标签省略。仅本机内存中的界面诊断保留，退出即丢弃；不写日志、仓库、状态文件、网络回包或发布资源。保留既有配对与开关，本候选不能解锁。

The r64 receipt reports three pressable buttons, no explicit/default login match, no password input, 20 renewals and zero sleep/wake events. Its traversal skips text-field children, a possible missed embedded control, not yet an established cause. r65 is temporarily diagnostic-only: after signed-window/focus checks it inspects bounded control structure including AXChildren of text fields, then always stops before password write/submission. It never reads AXValue, field text, hints or window titles. At most five button captions/descriptions/identifiers plus structural metadata are shown in local memory, with account-name redaction and unsafe/oversized labels omitted. No raw snapshot is logged, persisted, exported, copied to source documentation or packaged. Existing opt-in/pairing stay unchanged; this candidate cannot unlock.

r65 Release 构建、固定身份／深度签名和 394 项隔离检查通过；完整 XCTest 仍缺模块，11 文件 SHA-256 清单及格式校验完成。正常退出 r64 并启动 r65，界面确认只读说明、已加载配置与原开关；Wi-Fi 状态请求正常，蓝牙启动中未作成功断言。已请用户锁屏后输入一个公开英文字母且不回车，使可能仅在非空框出现的按钮可见，随后只读采集，清空并手动解锁。真实密码不是采集前提；结果待回报。

r65 passed Release build, fixed-identity/deep signing and 394 isolated checks; full XCTest is unavailable. Eleven bundle files are hashed and whitespace validation passed. Normal r64-to-r65 restart completed with visible diagnostic-only notice and retained enrollment/enablement; Wi-Fi is healthy, BLE startup recovery is not asserted. The pending human test uses one public letter without Return to expose nonempty-field controls, then captures metadata, clears that letter and manually unlocks. No real password is requested for this probe.


### r65 采集结果与 r66 延后查找提交按钮

用户反馈“没有反应”，符合只读候选不写入、不提交的预期。刷新桥接诊断确认请求实际到达、采集完成；非空安全框所在窗口出现了唯一明确登录描述的可按按钮，且它不是文本框后代。相比 r64 空白框中未发现该按钮，这支持“写入前要求登录按钮存在会过早拒绝”的判断，尚不能保证 AXValue 写入也会触发相同显现行为。原始控件快照未复制到此文档或日志。

r66 移除临时采集函数及界面说明。先绑定已确认的安全字段／所属窗口，只写入一次，再有界查找同一窗口的默认或唯一明确标注的登录按钮，复查目标、enabled、取消及时限后按一次。写入后不支持提交则停止，不重填、不回退 AXConfirm／RFB／事件模拟。错误文字明确区分“未输入”和“已写入但未提交”，并保留连续两秒桌面确认。补充非空后才出现按钮、写入后无按钮不重试的隔离检查。未更改系统全局策略、固件或协议字段；自动显示密码框和整机休眠唤醒仍未解决。

The user's “no reaction” is expected for a read-only candidate. Refreshed diagnostics confirm request receipt and completed inspection. A uniquely labelled pressable login button was observed with a nonempty secure field, outside the text-field descendants. Compared with its absence in the earlier empty-field probe, this supports deferring button discovery until after writing, but does not prove that AXValue triggers the same appearance. No raw control snapshot is copied into this document or logs.

r66 removes the temporary probe and binds the verified field/window before a single write, then resolves the same window's explicit/default login button. It rechecks target identity, enabled state, cancellation and deadline before one press. Missing submission stops without password replay, AXConfirm, RFB or simulated-event fallback. Errors distinguish no input from input without submission; continuous two-second desktop confirmation remains. Isolated checks cover a button available only after writing and absence without replay. Global policy, firmware and wire fields are unchanged; automatic field reveal and full-system wake remain unresolved. Build and human acceptance pending.


r66 Release 构建、固定身份／深度签名及 393 项隔离检查通过；完整 XCTest 仍缺少模块。11 文件 SHA-256 清单已生成，格式校验通过。通过菜单正常退出 r65、启动 r66，界面确认新候选说明、已加载配置和原启用开关；.264 USB 未连接，Wi-Fi 状态请求正常。启动时蓝牙归属确认等待重连，未改蓝牙设置，不声称已恢复。已请用户一次手动显示空白密码框后的实测，结果待回报；没有由工具锁屏或解锁。

r66 passed Release build, fixed-identity/deep signing and 393 isolated checks; full XCTest remains unavailable. Eleven bundle files are hashed and whitespace validation passed. Normal r65 quit/r66 launch completed, with the new notice and existing enrollment/enablement confirmed. .264 USB is absent and authenticated Wi-Fi requests are healthy. Startup BLE ownership is pending retry; no setting change or recovery claim. One human-initiated empty-field test is pending; no tool initiated lock/unlock.


### r66 连续两次解锁成功 / Two consecutive successful unlocks

用户回报连续两次解锁均成功。打开本机解锁设置，最近一次回执明确为“本次解锁已确认成功”，Wi-Fi 状态请求正常；整机睡眠／唤醒计数均为 0。回执只保留最近一次，不声称工具独立核对了两次完整记录。该结果确认 AX 单次写入后定位登录按钮的路径可以实际解锁，无需改变全局屏幕共享策略。此前测试要求手动 Tab 显示密码框；已请用户确认前两次是否执行了该步骤，并进行一次不按 Tab／不点击密码框的测试，成功后观察 30 秒是否回锁。自动准备、较长桌面保持、熄屏与整机睡眠场景仍待分别验收。

The user reports two consecutive successful unlocks. The latest local receipt explicitly confirms success, Wi-Fi status requests remain healthy, and sleep/wake counts are both zero. Only the latest receipt is retained; both attempts were not independently audited by tools. This confirms that one AX write followed by login-button discovery can unlock without global screen-sharing policy changes. The preceding instructions requested manual Tab preparation; clarification and one no-Tab/no-field-click attempt with 30 seconds of desktop observation are pending. Automatic preparation, longer desktop persistence, display-off and full-system sleep remain separate outstanding acceptance cases.


### r66 扩展场景失败与 r67 密码页准备／状态候选

用户确认：不在密码输入框时失败，黑屏后失败，Computer Use 运行时失败且设备错误显示未锁屏。最近一次本机回执已到登录按钮阶段，窗口中仅两个可按按钮、没有可确认的登录按钮，因此填入后没有提交；19 次续期、整机睡眠／唤醒均为 0。回执仅保留最后一次，不能将此原因断言为上述三个试验的共同根因。r66 只记录为密码页已准备时两次成功，不标为完整一键解锁通过。

r67 在已签名、当前控制台且保持 AX 焦点的登录进程上，通过公开 CGEvent.postToPid 发送一次无修饰键 Tab 按下／释放；没有密码、Return、全局事件投递、其他进程或 RFB 回退。动作前后复查目标与取消，按下后即使取消仍释放同一进程的按键，不重发；事件投递不作为成功证据。再有界等候并核验安全字段，保留一次 AXValue 写入、唯一按钮提交及持续桌面确认。现有请求内 IOPM 显示器活动不改系统设置；实际是否能显示密码页及点亮屏幕尚待验收，整机休眠仍需外部唤醒。

状态方面修正一个可导致证据丢失的分支：会话切换／睡眠通知与暂时离开控制台不再清除已观察到的锁定；只使旧的未锁定证据失效。桌面焦点必须同时具备当前未锁定观察才返回 false，否则为未知。通过公开 CGSessionCopyCurrentDictionary 读取可选的未文档化锁定标记，只接受布尔值；缺失或异型为未知。正值只用于显示锁定和否决桌面成功，负值／缺失不授权输入或证明解锁。当前 UID／控制台条件仍必须满足；若 Computer Use 导致无法验证真正登录焦点，仍停止输入而不猜测目标。该兼容标记与原有分布式通知均不是稳定公开锁屏契约，须保留未知状态。

增加本机内存中的通知／兼容标记摘要及显示器熄灭／亮起计数；不读取窗口内容、账户文本、密码，不把原始会话字典写日志或传给 TAB5。既有 locked 布尔／null 协议不变，未修改 Windows／固件。增加会话失效、锁定保持、未知状态、类型过滤和取消时按键配对检查。

The user reports failures without the password page, after a black screen, and during Computer Use, which also falsely displayed unlocked. The latest receipt reached button selection but found only two pressable non-login buttons; input was not submitted, with 19 renewals and no system sleep/wake. It cannot identify all three attempts independently. r66 is accepted only for two prepared-password-page unlocks.

r67 sends one unmodified Tab pair via public CGEvent.postToPid exclusively to the currently focused, signed loginwindow in the current user's console. It sends no password/Return/global event, never switches targets or falls back to RFB, rechecks identity/cancellation and balances the pair even after cancellation. Delivery does not prove readiness: bounded AX secure-field checks, a single write/button press and continuous desktop confirmation still follow. Request-scoped IOPM activity leaves system settings unchanged; password-page reveal and display wake remain unverified, and full-system sleep still requires external wake.

Session invalidation/off-console reads now retain positive lock observations while invalidating stale unlocked evidence. Desktop focus requires an accompanying unlocked observation; otherwise the result is unknown. A boolean-only optional undocumented lock marker returned by public CGSessionCopyCurrentDictionary may only display lock or veto desktop success. Missing/malformed/negative hints cannot authorize input or prove unlock. UID/console and independently verified signed focus remain mandatory; Computer Use focus conflicts still stop input. Both this compatibility marker and the existing distributed notifications lack a stable documented lock-state contract. Local memory diagnostics add only boolean summaries and display sleep/wake counts, never contents or raw session dictionaries. Existing bool/null wire semantics are unchanged. New isolated cases cover lock retention, unknown evidence, strict types and balanced cancellation.

API references: [CGEvent](https://developer.apple.com/documentation/coregraphics/cgevent), [session dictionary](https://developer.apple.com/documentation/coregraphics/cgsessioncopycurrentdictionary()), [display activity](https://developer.apple.com/documentation/iokit/1557127-iopmassertiondeclareuseractivity). These references establish API availability, not successful lock-screen delivery.


r67 Release 构建、固定身份／深度签名、404 项隔离检查及格式校验通过；完整 XCTest 仍缺模块。11 文件校验清单已生成。菜单正常退出 r66 并启动 r67，界面已确认新说明、保留配置／开关，.264 USB 未接、Wi-Fi 已认证、蓝牙数据已确认。启动桌面时通知与可选标记均为未知，按设计不凭此断言未锁屏。已请用户先进行一次不按 Tab 的普通锁屏测试，结果待回报；黑屏与 Computer Use 分开后续验收。没有工具发起锁屏／解锁或修改全局策略。

r67 passed Release build, fixed-identity/deep signing, 404 isolated checks and whitespace validation; full XCTest remains unavailable. Eleven bundle files are hashed. Normal quit/relaunch confirmed the new notice and retained enrollment/enablement, with .264 USB absent, authenticated Wi-Fi and confirmed BLE. Both optional observations are unknown at desktop startup, which deliberately cannot prove unlocked. One no-Tab human lock test is pending; black-screen and Computer Use cases will be evaluated separately. No tool initiated locking/unlocking or global policy changes.


### r67 无反应与 r68 未准备锁屏页只读采集

用户报告 r67 无反应。刷新最近一次回执：仍在登录按钮阶段停止，两个可按控件但没有默认／明确登录按钮，22 次续期；整机睡眠／唤醒 0／0，显示器累计熄灭／亮起 4／4。累计计数不能全部归于本次尝试。说明定向 Tab 准备并未在此次产生可用的提交界面，不能称其修复有效，也不能仅凭投递返回认定按键已生效。手动解锁后的通知为未锁定，可选兼容标记未知；这不是 Computer Use 锁屏状态修复已验证的证据。

r68 临时停用设备请求中的写入／提交，改为在确认签名登录进程及静止条件后，仅检查尚未移动焦点／发送 Tab 的控件结构。最多 64 节点、8 层、2 秒；只保留窗口动作、字段安全角色／焦点／公开动作以及前四个可操作控件的短标签和标识。字段内容／标题、静态文本、窗口标题不读，账户名脱敏，超长／控制字符标签省略，标签最多 32 字符；不调用控件动作，不填密码，不提交。本地内存诊断之外不记录原始快照、不导出给设备。现有请求内显示器活动仍保留，不改全局设置；这是取证候选，暂不能解锁。下一步依据实际公开入口判断是否能安全显示密码页，不再调整按键延迟盲测。

The user reports no reaction with r67. The latest receipt again stopped at login-button selection, with two pressable controls but no default/explicit login button and 22 renewals. System sleep/wake counts are 0/0; cumulative display sleep/wake counts are 4/4 and cannot all be attributed to this attempt. Targeted Tab did not yield a usable submission page; posting alone did not establish delivery. After manual unlock the notification says unlocked and the compatibility hint remains unknown, which does not validate the Computer Use fix.

r68 temporarily replaces device-triggered input/submission with a bounded read-only probe of the untouched signed login page, before any focus change or Tab. At most 64 nodes, eight levels and two seconds are inspected. It retains window action types, field secure-role/focus/action metadata and short labels/identifiers for at most four actionable controls. Field contents/titles, static text and window titles are excluded. Account labels are redacted, unsafe/oversized labels omitted and output labels capped at 32 characters. It invokes no control action, password write or submission, retains raw metadata only in local memory and exports no snapshot. Existing request-scoped display activity remains, without global setting changes. This diagnostic candidate cannot unlock; the next action depends on an observed public entry control instead of guessed input timing.


r68 Debug／Release 构建、固定身份／深度签名及 407 项隔离检查通过；完整 XCTest 仍缺模块。11 文件 SHA-256 清单及格式校验完成。通过菜单正常退出 r67 并启动 r68，界面明确只读、原配置／开关保留，.264 USB 未接、Wi-Fi 已认证且蓝牙数据已确认。已请求一次保持锁屏原样的人工采集：不按 Tab／不点击字段／不输入字母，触发后等约十秒自行解锁。无可见变化与设备失败提示均符合诊断版设计；结果待回报。未操作系统全局设置，未由工具锁定或解锁机器。

r68 passed Debug/Release build, fixed-identity/deep signing and 407 isolated checks; full XCTest remains unavailable. Eleven bundle files are hashed and whitespace validation passed. Normal r67 quit/r68 launch confirmed the read-only notice and retained configuration/enablement, .264 USB absent, authenticated Wi-Fi and confirmed BLE. A human capture of the untouched lock page is pending, without Tab, field clicks or sample input, followed by manual unlock after about ten seconds. No visible change and a device failure indication are expected for this probe. No global settings or tool-initiated locking/unlocking occurred.


### r68 已采集与 r69 登录窗口置前候选

用户完成未操作锁屏页的采集。本机回执确认存在一个公开窗口及一个被系统报告为已聚焦的安全字段，但没有明确的密码页入口或登录提交按钮；该窗口公开 AXRaise 动作。此证据表明 AX 字段焦点不等于物理密码页已显示，原始按钮标签／标识未写入本文。采集期间整机睡眠／唤醒与熄屏／亮屏计数均为 0；不能推导黑屏或 Computer Use 场景的结论。

r69 删除临时控件采集及 r67 定向 Tab 准备代码。保留 r66 单次 AXValue 写入及明确登录按钮 AXPress 路径，在写入前对安全字段所属、同一已签名登录进程的窗口执行一次已公开的 AXRaise；操作前后在两秒总预算内复查同一字段／窗口／会话及取消。未知、超时、动作失败、目标变化均在密码写入前停止，不重试，不转发键盘事件，不连接屏幕共享。置前后仍须原有一次写入、按钮定位／身份复核与持续两秒桌面确认；API 返回成功不代表密码页实际已显示。本轮同步修正既有 XCTest 对“离开控制台会清空已锁定观察”的旧预期，使其匹配 r67 保守保留策略；本机缺 XCTest 模块，不能声称运行过这些 XCTest。

The untouched-page probe completed. It exposes one public window and a secure field reported as focused, but no explicit password-page entry or login submission button; the window offers AXRaise. This shows that AX field focus does not establish that the physical password page is shown. Raw button labels/identifiers are not copied here. No sleep/wake or display power events occurred during this run, so black-screen and Computer Use behavior cannot be inferred.

r69 removes the temporary probe and failed process-targeted Tab preparation. Before the retained r66 single-write/explicit-button submission, it performs one advertised AXRaise on the secure field's own signed-loginwindow window, with the same field/window/session/cancellation revalidated within two seconds. Unknown, late, failed or changed targets stop before password input without retry, keyboard-event fallback or screen sharing. Button identity and continuous desktop confirmation remain necessary; an AX acknowledgement does not prove physical visibility. Existing XCTest expectations about losing positive lock evidence off-console were updated to match r67; missing XCTest modules still prevent running that suite.

[Apple AXRaise documentation](https://developer.apple.com/documentation/applicationservices/kaxraiseaction) limits raising to what the containing application permits and notes that floating windows may remain above it. This is an observed supported action worth testing, not proof of a working lock-page reveal.


r69 Release 构建、固定身份／深度签名、406 项隔离检查及格式校验通过；完整 XCTest 仍缺模块。11 文件 SHA-256 清单已生成。正常退出 r68 并启动 r69，界面显示实际解锁候选、原配置与启用开关保留；.264 USB 未接、Wi-Fi 收到认证请求、蓝牙数据已确认，打开诊断时尚无新电脑状态请求。已请用户回电脑控制页后进行一次无 Tab／不点击字段的实测并观察 30 秒；结果待回报。系统全局设置未变，工具未锁屏或解锁。

r69 passed Release build, fixed-identity/deep signing, 406 isolated checks and whitespace validation; full XCTest remains unavailable. Eleven files are hashed. Normal r68 quit/r69 launch confirmed the functional candidate notice and preserved enrollment/enablement; .264 USB is absent, Wi-Fi authentication was received and BLE data confirmed, with no new computer-status requests yet at diagnostic opening. One human no-Tab/no-field-click test after returning to device computer controls, including 30-second desktop observation, is pending. No global settings or tool-initiated locking/unlocking changed.


### r69 密码页仍须人工准备；r70 系统输入队列候选

用户确认 r69 在明确显示密码框时成功，黑屏或唤醒后未显示密码框时失败。最近一次本机回执仍在登录按钮定位阶段停止，只有两个可按控件而没有明确登录按钮，21 次续期；整机睡眠／唤醒 0／0、熄屏／亮屏 1／1。AXRaise 没有解决该场景；本次不是已证实的整机睡眠唤醒失败。

r70 删除无效的 AXRaise 准备，保留 r66 已成功的 AX 单次写入／明确按钮提交。与 r67 的 postToPid 不同，准备动作使用公开 CGEvent.post(tap:.cghidEventTap)，向 WindowServer 系统输入队列发送一次无修饰 Tab（40 ms 按下后释放、200 ms 等待）。仅在独立核验当前 UID／控制台、签名登录进程和实际 AX 焦点、已有事件权限后允许；用户按住修饰键或 Tab 则拒绝。取消或目标变化发生在按下后也必须释放，不重发，动作前后各检查并受两秒总预算限制。之后重新核验安全字段／所属窗口；最多等候既有四秒字段显现，空回调不再发送任何键。密码和 Return 均不进入该事件流，仍只通过单次安全字段写入和按钮动作提交；不建立屏幕共享或改变系统设置。

这是新的有界准备候选，公开 API 的事件投递不保证锁屏封面会接受，不能提前称为修复。Computer Use 焦点冲突仍停止输入；整机睡眠需要外部唤醒，另待解决。现有隔离检查改为覆盖未就绪／取消／目标变化／超时后的按键释放和禁止重发。

The user confirms that r69 unlocks with the password field visibly present, but fails from black-screen or post-wake states without that field. The latest receipt again lacks an explicit login button despite two pressable controls and 21 renewals; system sleep/wake is 0/0 and display sleep/wake 1/1. AXRaise did not solve this case; full-system sleep is not established by this attempt.

r70 removes ineffective AXRaise preparation and retains the successful r66 one-write/explicit-button submission. Unlike r67 postToPid, one unmodified Tab pair enters WindowServer's system input stream through public CGEvent.post(tap:.cghidEventTap), with a 40 ms hold and 200 ms settling interval. Independent current UID/console/signed login-process focus checks and existing event permission are mandatory; held modifiers or Tab reject preparation. Cancellation, changed targets or lateness after key-down still release the key without replay, with checks before/after and a two-second bound. Fresh secure-field/window validation follows, allowing the existing four-second AX exposure wait without sending another key. No password or Return enters the event stream; submission remains a single secure-field write and explicit button action. No screen sharing or global setting changes occur.

This is an unverified preparation candidate: public posting does not guarantee lock-cover acceptance. Computer Use focus conflicts still stop input; full-system sleep needs an external wake source and remains unresolved. Isolated cases cover unavailable targets, cancellation, changes and late completion with balanced release/no replay.

API references: [CGEvent.post](https://developer.apple.com/documentation/coregraphics/cgevent/post(tap:)), [HID event entry location](https://developer.apple.com/documentation/coregraphics/cgeventtaplocation/cghideventtap). These distinguish stream insertion from process-targeted delivery, not physical lock-screen success.


r70 Release 构建、固定身份／深度签名、406 项隔离检查及格式校验通过；完整 XCTest 仍因缺少模块失败。11 文件 SHA-256 清单已生成。正常退出 r69、启动 r70，界面确认新说明及已有配置／开关；.264 USB 未接，Wi-Fi 状态请求正常。蓝牙启动时处于连接中，未声称已确认或修改设置。已请求一次不显示密码框的普通锁屏实测，成功后观察 30 秒；黑屏和 Computer Use 场景仍分开待验收。未由工具锁屏／解锁，未改全局系统策略。

r70 passed Release build, fixed-identity/deep signing, 406 isolated checks and whitespace validation; full XCTest still fails due to the missing module. Eleven files are hashed. Normal r69 quit/r70 launch confirmed the new notice and preserved enrollment/enablement, .264 USB absent and healthy Wi-Fi status requests. BLE was connecting at startup, without a recovery claim or setting change. One human test from the locked non-password-page state with 30-second post-success observation is pending; black-screen and Computer Use cases remain separate. No tool initiated lock/unlock or global policy changes.


### r70 实测结果与设备键盘路径调查 / Physical result and device-keyboard investigation

用户回报 r70：黑屏唤醒后已有密码输入框时可以解锁，没有密码输入框时仍失败。最近一次本机回执仍在登录按钮定位阶段停止：两个可按控件但无默认／明确登录按钮，20 次续期。累计整机睡眠／唤醒 0／0、屏幕熄灭／亮起 3／3；累计事件不能全部归于此次尝试。系统输入队列 Tab 没有解决密码页显现，不能把事件发送或字段焦点视为密码页已准备。r70 不标记为完整验收通过，也不安排相同候选的重复试验。

只读检查发现本机已枚举 TAB5 的 Bluetooth Low Energy HID 键盘；这只证明设备存在，不证明当前配对认证、订阅、发送准备或睡眠唤醒能力。已核对维护者 .234 发布源码中的 `ble_keyboard.inc`／`ble_keyboard_flow.h`，以及 GitHub 开发分支 `2fb1b14c09550bce27173eb349d78187d1661ce7` 对应 .262 参考的 `unlock_remote.c`／`ui_unlock.h`。现有 Wi-Fi 解锁要求 USB 与桥接 BLE 均未连接；BLE 准备走另一套 Windows 锁屏封面／显示状态协议。Mac 未实现这些状态时返回 null，不能伪造值来诱导按键。现有封面准备键是可打印的 Space，不能直接复用为 Mac 的 Tab；显示准备键为 Shift。当前固件没有已完成的“Wi-Fi 请求＋BLE 只发准备键＋Mac 本地密码”流程。

候选方向是显式协商的设备非秘密准备动作，绑定同一电脑、当前认证会话和单次用户请求；最多发送一次 Tab 并可靠释放，结果不确定不重发，密码仍只在 Mac 端。必须先解决当前通道互斥及双方身份绑定，不能只放宽 `wifi_only` 判断。整机休眠时桥接不能处理请求，而设备现有键盘检查要求八秒内认证及非挂起状态，因此这条在线准备路径不构成整机睡眠唤醒方案，也不应为唤醒而去掉认证检查。当前只完成调查，未增加协议、修改固件或部署新候选；正在确认用户是否接受无线流程依赖 BLE。

The user reports that r70 unlocks after a black screen when the password field is already visible, but still fails without it. The latest local receipt stops at login-button discovery: two pressable controls, no default/explicit login button and 20 renewals. Cumulative system sleep/wake is 0/0 and display sleep/wake 3/3; these events cannot all be assigned to this attempt. System-stream Tab has not solved page reveal. Event posting or field focus does not establish readiness; r70 is not fully accepted and the same test is not being repeated.

Read-only inspection enumerated a TAB5 Bluetooth Low Energy HID keyboard. Enumeration does not prove authenticated readiness, subscription, delivery or sleep wake. The maintainer's .234 keyboard sources and .262 unlock/UI reference at development commit `2fb1b14c09550bce27173eb349d78187d1661ce7` show separate paths: Wi-Fi unlock excludes USB/bridge BLE, while keyboard preparation uses Windows cover/display evidence. Unsupported Mac observations remain null and must not be fabricated. Existing cover preparation sends printable Space, not Tab; display preparation sends Shift. No integrated Wi-Fi request/BLE preparation/Mac-local-secret flow currently exists.

A possible extension would negotiate one non-secret device preparation action bound to the same host, authenticated session and explicit user request, sending Tab once with balanced release and no uncertain retry. Passwords would remain local to the Mac. It requires explicit cross-channel identity and lifecycle handling, not simply relaxing `wifi_only`. Full-system sleep remains separate: the bridge cannot answer while asleep and current HID admission requires fresh eight-second authentication and a non-suspended connection. These checks must not be removed to claim wake support. Investigation only: no protocol or firmware implementation and no new deployment; the user's acceptance of a BLE dependency is pending.


### 纯 Wi-Fi 约束与 r71 连接结束前准备诊断 / Wi-Fi-only constraint and preparation-before-disconnect probe

用户明确要求必须只靠 Wi-Fi；上一节 BLE 协同提议已撤回，不等待批准，也不修改固件。用户进一步观察到：未显示密码框时，直接用实体键盘输入密码会自动展开并填入密码框。这是用户操作观察，不能推导 AXValue 或任意模拟按键同样会触发页面展开。

r71 暂时将设备解锁入口改为单次准备诊断。使用既有账户／当前配对 Keychain 配置，只向已核验 Apple 服务身份的 127.0.0.1:5900 连接认证；不启用或配置服务，不改系统全局设置。先用现有请求内显示活动和独立登录窗口核验、键鼠静止检查，再认证一个共享 RFB 连接。复核当前签名登录窗口后仅发送一次非打印 Tab，40 ms 后释放；不发送密码键序列、Return、剪贴板或画面请求，不写 AXValue。取消发生在按下后，仅为单次释放给出最多 200 ms 的 IO 清理窗口；不重放按下，失败关闭连接。连接内观察 0.4 秒仍锁定，关闭 socket 后再观察三秒；这只是限定时长的锁屏观察，不声称系统异步恢复动作全部结束。诊断总流程仍受现有设备租约与 40 秒预算限制。

该候选不会进入旧 AX 提交路径，也不会报告解锁成功；本机显示诊断完成说明，设备“解锁未完成”为预期。需要用户观察密码页在连接内是否出现、断开后是否保留，再决定是否存在可实现的准备／断开／本机 AX 提交顺序。绝不通过延长连接、常驻会话或修改 RestoreMachineState 掩盖回锁。现有 r70 直接路径保留在独立函数中但不由 r71 设备入口调用。构建、部署及实机结果待分别记录。

整机睡眠仍独立未解决。Apple 的 IOPMAssertionDeclareUserActivity 文档只承诺点亮显示器和推迟显示器睡眠；其调用需要进程能运行。Apple 的 Wake for network access 依赖已有系统／网络能力，不能据此承诺任意睡眠 Mac 都能被当前 TAB5 HTTP 轮询唤醒。本轮不改变睡眠设置或增加常驻防睡眠。

The user requires Wi-Fi only. The BLE proposal is withdrawn, with no pending approval or firmware change. The user's physical-keyboard observation shows that typing while the password field is hidden reveals and populates it; this does not establish that AXValue or synthetic input does the same.

r71 temporarily routes device attempts to a preparation-only probe. Existing pair-bound credentials authenticate one identity-verified Apple loopback service at 127.0.0.1:5900, without enabling/configuring services or changing global settings. Request-scoped display activity, independent signed-loginwindow checks and input-idle checks precede authentication. A fresh target check permits one nonprinting Tab with a 40 ms hold and release; no password key sequence, Return, clipboard, framebuffer request or AXValue write occurs. Cancellation permits at most 200 ms of IO cleanup solely to balance that key; no down is replayed and failures close the connection. The probe observes lock for 0.4 seconds inside the connection, closes the socket and observes for three more seconds. That interval is not proof that all asynchronous OS restoration has finished. Existing lease and 40-second attempt limits remain.

No AX submission follows and this candidate never reports unlock success. Device failure is expected; the local diagnostic explains completion. Physical observation must establish whether the page appears and survives disconnect before considering a prepare/disconnect/local-AX sequence. No persistent session or RestoreMachineState modification masks relocking. The r70 direct backend remains separate and is not called by the r71 entry. Build, deployment and hardware outcomes are recorded separately.

Full-system wake is still unresolved. IOPMAssertionDeclareUserActivity documents display wake and postponement of display sleep, and requires a running process. Wake for network access depends on existing OS/network support, not an assurance that TAB5 HTTP polling wakes every sleeping Mac. No sleep policy or persistent prevention assertion changes.

References: [Apple display activity API](https://developer.apple.com/documentation/iokit/1557127-iopmassertiondeclareuseractivity), [Apple network wake](https://support.apple.com/en-gb/guide/mac-help/mh27905/mac).


r71 Release 构建、固定身份／深度签名及 416 项隔离检查通过；完整 XCTest 因缺少模块失败。11 文件 SHA-256 清单与差异格式检查完成。通过菜单正常退出 r70、启动 r71，界面确认准备诊断说明、已有 Keychain 配置及启用开关保留；.264 USB 未连接，Wi-Fi 状态请求成功，蓝牙数据确认但本候选解锁不使用该通道。已请求一次无密码框锁屏页的人工准备观察：约 15 秒后自行解锁，回报密码框是否出现／又消失。没有工具锁屏、输入真实密码或发起解锁。实机结果待回报，未宣称修复。

r71 passed Release build, fixed-identity/deep signing and 416 isolated checks; full XCTest failed because the module is missing. Eleven bundle files are hashed and whitespace checks pass. Normal r70 quit/r71 launch confirmed the probe notice, loaded Keychain configuration and preserved enablement. .264 USB is absent and Wi-Fi status succeeds; BLE data is confirmed but unused for this unlock probe. One human observation from the untouched lock page is pending, with manual unlock after about 15 seconds and reporting whether the field appeared/disappeared. Tools did not lock, enter real passwords or initiate unlocking. No hardware success or fix is claimed.


### r71 未显现密码页；r72 撤下诊断并恢复已准备密码页路径 / Probe failed; restore prepared-field path

用户回报密码框没有出现。刷新本机回执确认该请求完成 Apple 本机服务认证、一次 Tab 发送、连接关闭及其后三秒锁屏观察，47 次续期；本次运行整机睡眠／唤醒和屏幕熄灭／亮起均为 0。该结果排除本次请求未送达或认证未完成，不能证明 RFB 键盘事件已被物理控制台接收。短连接准备没有达成目标，不能据此接上密码输入，也不重复相同测试。

r72 删除 r71 临时诊断入口及其专用检查、r70 系统 Tab 准备及专用检查；恢复先前有人工成功证据的 AX 单次写入／明确登录按钮单次提交／持续桌面确认路径，保留 r67 的保守状态观察。设备入口不建立屏幕共享、不发送准备按键，不修改系统全局设置。界面明确要求先在 Mac 显示空白密码框；这是暂时可用的部分能力，不是完整一键解锁修复。取消、身份变化、未知状态、时限及禁止重输的检查保留；测试数量减少来自删除已撤下的准备实验，未放宽存续断言。旧的手动接入／键盘验证窗口不变，密码配置不需重录。

目前没有已验证的、符合“只靠 Wi-Fi、不改全局设置”的自动密码页显现方案。实体键盘能展开界面不能证明软件事件能做到；也不能声称 macOS 上绝对不可能实现。整机睡眠唤醒、Computer Use 状态误报仍未解决。r72 只恢复可用基线，本轮不再要求用户重复锁屏试验；后续若继续完整功能开发，需要新的可核验机制或证据，而不是将按键投递当成成功或盲目改延迟。构建与部署结果另记。

The user reports no password field. The refreshed receipt confirms Apple loopback authentication, one Tab send, connection closure and three further seconds of confirmed lock, with 47 lease renewals. All system/display sleep/wake counters are zero for this run. Request delivery and authentication succeeded; physical-console input delivery is not established. Short-connection preparation failed its objective and does not justify proceeding to password input or repeating the same trial.

r72 removes the temporary r71 entry and its dedicated checks, and removes r70 system-Tab preparation and its checks. It restores the previously human-accepted single AX write/explicit login-button press/stable-desktop path, retaining conservative r67 state observations. Device attempts open no screen-sharing connection and send no preparation keys; global settings remain unchanged. The UI explicitly requires a blank password field to be displayed on the Mac first. This is partial functionality, not a complete one-click fix. Cancellation, identity, unknown-state, timing and no-replay checks remain. Fewer checks reflect removed experiments, not weakened retained assertions. Manual enrollment/keyboard verification and saved credentials are unchanged.

No automatic password-page reveal has been verified within the Wi-Fi-only/no-global-change constraints. Physical keyboard behavior does not establish synthetic-event behavior, nor does this investigation prove impossibility on macOS. Full-system wake and Computer Use misclassification remain unresolved. r72 restores the usable baseline without another repetitive hardware trial. Further complete implementation requires a new verifiable mechanism/evidence rather than treating event posting as acceptance or guessing delays. Build/deployment results follow separately.


r72 Release 构建、固定身份／深度签名及 400 项隔离检查通过；完整 XCTest 仍缺模块，未声称通过。11 文件 SHA-256 清单及差异格式检查完成。正常退出 r71 并启动 r72，界面确认当前说明、已有钥匙串配置及启用状态；.264 USB 未连接、Wi-Fi 已认证且状态请求正常。保留蓝牙原设置，但解锁不依赖蓝牙。未执行新的锁屏／解锁测试，未改全局设置，自动准备及其他未解决项目不标为通过。

r72 passed Release build, fixed-identity/deep signing and 400 isolated checks; full XCTest still lacks its module. Eleven bundle files are hashed and whitespace checks pass. Normal r71 quit/r72 launch confirmed the current notice, loaded Keychain configuration and enablement, .264 USB absent and authenticated healthy Wi-Fi status. Existing BLE settings are preserved without an unlock dependency. No new physical lock/unlock test or global setting change occurred; automatic preparation and other outstanding cases remain unaccepted.


### r73 可打印输入假设与替代架构调查 / Printable-input hypothesis and alternatives

用户要求继续找纯 Wi-Fi 方案，不改系统全局设置，必要时换架构。核对 [RFC 6143 ClientInit](https://datatracker.ietf.org/doc/html/rfc6143#section-7.3.1)：shared flag 只涉及是否让其他客户端继续共享，不是 macOS 物理控制台选择或解锁授权。现有 Apple type 30 认证和 ServerInit 不足以证明按键送到了物理锁屏。[Screens 开发商说明](https://help.edovia.com/en/screens-5/troubleshooting/log-in-twice-mac/)也记录断开时恢复连接前状态；未找到可引用的公开逐连接关闭恢复策略接口。没有据此宣称不存在任何实现。

本机 SDK 的 AXUIElementPostKeyboardEvent 已自 10.9 废弃，文档仅说明向目标应用合成低层键盘事件；没有锁屏可用性承诺。因此不把它当作新的可靠通道。未调用私有 API、未更改屏幕共享或认证设置。

新假设来自用户观察：未显示密码框时，实体可打印输入会展开密码页。此前准备只尝试非打印 Tab／AXRaise，不能据此排除字符触发。r73 保留正常 AX 解锁路径，新增本地按钮“下一次仅测试字符触发…”，将下一次经认证的 Wi-Fi begin 替换为单次公开字符诊断。绑定当前配对，内存有效期两分钟，过期请求拒绝而不悄然转成真实密码输入；USB／BLE、不合法请求不能触发，已消费 requestId 不能重放。锁屏时的会话通知可取消正在运行的尝试，但不会把已经准备的诊断悄然变为密码输入。接入后只执行一次诊断，下一次明确请求恢复普通行为；用户须自行清空样本且不要连续重试。

诊断接口没有密码参数、不复制密码到诊断函数；独立确认当前控制台、签名 loginwindow、安全字段、已有事件权限、无按住的修饰键／a，并等键鼠静止。通过公开 CGEvent 系统队列发送一个固定字符 a，40 ms 后释放；取消、失焦或超时也释放，不重发。不按 Return、不删除、不写 AXValue、不连接 RFB、不读取字段内容或剪贴板；样本是否真正送达、是否打开密码页由用户观察，函数返回不代表成功。若字符未触发，仍不能断言所有软件方案不可能，但下一步应评估不同输入架构，不再盲目改 Tab 延迟。

替代方案：Mac 专用虚拟 HID 输入组件可保留 TAB5 到 Mac 的 Wi-Fi 传输，凭据仍在本机。Apple [DriverKit virtual HID entitlement](https://developer.apple.com/documentation/bundleresources/entitlements/com.apple.developer.driverkit.family.hid.virtual.device)需要申请；[Karabiner 虚拟设备项目](https://github.com/pqrs-org/Karabiner-DriverKit-VirtualHIDDevice)说明了已有驱动架构。该方案需安装／批准扩展并设计受限本机 IPC，与“不改变全局锁定策略”可以区分，但并非零系统组件、也不是本次请求结束即完全不存在的改变。这里只评估，不下载、安装、授权或复制第三方实现。若用户严格禁止任何系统组件变化，则不能将其作为满足全部约束的交付。输入驱动是否解决此机器锁屏仍需另行实测。

整机睡眠是独立问题：[Apple 网络唤醒文档](https://support.apple.com/en-gb/guide/mac-help/mh27905/mac)要求适用的系统／网络条件。运行中的 Mac 软件不能保证唤醒已停止执行的自己；新驱动也不能替代网络唤醒源。现有 TAB5 没有已实现／验收的此类唤醒流程，本轮不更改系统电源设置。

The user requests continued Wi-Fi-only investigation with global settings unchanged. RFC 6143's shared flag concerns other clients, not selection or proof of the physical macOS console. Type-30 authentication/ServerInit cannot establish that recipient. Screens' vendor documents restoration on disconnect; no publicly supported per-connection override was found. The deprecated AXUIElementPostKeyboardEvent SDK contract supplies no lock-screen guarantee and is not adopted as a reliable new route.

r73 tests the user's specific printable-input observation while preserving ordinary AX unlock. A local, pair-bound, two-minute, memory-only arm substitutes a fixed public-character diagnostic for one authenticated Wi-Fi begin. Expired arms reject rather than falling into real credential input. Invalid/USB/BLE requests cannot trigger it, consumed IDs cannot replay, and lock notifications do not silently turn a prepared diagnostic into credential entry. A subsequent explicit request returns to ordinary behavior; the user must clear the sample and avoid repeated attempts.

The diagnostic receives no password. Current console, signed loginwindow, secure target, existing posting permission, idle input and absence of held modifiers/a are checked before one fixed Unicode a through public CGEvent, followed by release after 40 ms. Cancellation/change/timeout balances release without replay. No Return, deletion, AX value write, RFB, field-value read or clipboard occurs. Posting does not prove delivery or physical page reveal. A negative observation would motivate a different input architecture, not an impossibility claim or another timing guess.

A dedicated Mac virtual-HID component could retain Wi-Fi transport and local credentials, but requires approved installation, entitlements and a constrained local IPC design. It need not change global locking policy, yet remains an added system component requiring separate acceptance; nothing is installed or copied here. With an absolute prohibition on system components, it does not meet the constraints. Hardware behavior is unverified. Full-system wake remains separate and dependent on applicable network wake support; neither the stopped bridge nor a new input driver guarantees it. No power settings change.


替代架构补充：上述 Karabiner 项目明确只接受 root 客户端的虚拟设备命令，因此若进一步验证其驱动，需单独的受限特权助手，不应让整个桥接程序以 root 运行。助手只允许已验证本机桥接发起受限准备动作，设备配对、用户凭据和正常 UI 仍留在用户进程；具体授权和安装方案尚未实施。

Alternative architecture clarification: the referenced Karabiner project accepts virtual-device commands only from root clients. Any evaluation would therefore need a separately constrained privileged helper, not a root bridge process. A narrowly authenticated helper would handle preparation while pairing, credentials and UI remain in the user process. Installation/authorization design is not implemented.


r73 Release 构建、固定身份／深度签名及 Debug／签名应用各 413 项隔离检查通过。完整 swift test 因本机缺少 XCTest 模块失败，不能记作测试通过。已生成 11 文件 SHA-256 清单并通过差异格式检查。正常退出 r72 并启动 r73；界面确认 r73 说明、已读取钥匙串配置及原有启用状态，.264 USB 未连接、Wi-Fi 已认证且连续状态请求成功。一次性诊断按钮已展示，尚未准备或执行，等待用户从本地按钮开始后单次实测；两分钟计时以用户点击为起点。未改全局设置、未安装驱动、未触碰密码字段，自动准备／整机唤醒／Computer Use 仍不记为通过。

r73 passed Release build, fixed-identity/deep signature verification and 413 isolated checks in both Debug and the signed app. Full swift test fails because this host lacks XCTest; it is not recorded as passed. Eleven bundle files have SHA-256 checksums; whitespace checks pass. Normal r72 quit/r73 launch confirmed the r73 notice, loaded Keychain configuration, existing enablement, .264 USB absent and successive authenticated Wi-Fi status success. The local one-use probe button is displayed but not armed or executed; the physical test starts with the user clicking it, so its two-minute deadline does not expire while awaiting a reply. No global setting, driver installation or password-field interaction occurred. Automatic preparation, full-system wake and Computer Use remain unaccepted.


### r73 实测与 r74 完整流程候选 / Physical probe and integrated candidate

用户确认 r73 公开字符诊断使原本未显示的密码框出现。刷新本地回执确认已走完公开字符投递、20 次设备续期，未输入密码、Return 或删除；本次系统／显示器睡眠与唤醒计数均为零。这为该机器上可打印字符的效果提供了实机证据，但尚不是完整解锁成功，也不覆盖黑屏、整机睡眠或 Computer Use。

r74 将同一公开字符投递接入正常 Wi-Fi 解锁请求：显示唤醒、独立核验签名 loginwindow／当前控制台、安全字段及窗口、三秒静止，然后单次 a。投递成功本身不能继续密码输入：六秒内等待同一窗口的登录按钮从不可用／不存在变为启用并稳定至少 150 ms；若字符计数受支持，还要求从零变为恰好一。计数接口不可用时只用按钮变化，始终不读 AXValue。初始已可提交且没有零计数证明、计数异常／能力变化、确认超时、焦点／窗口变化、取消均停止，不发送真实密码、不重发样本。准备成功才执行已验证的单次 AXValue 整体替换，再重新确认同一窗口的默认／唯一明确登录按钮并按下一次，最后连续两秒确认桌面。整体替换用于避免把公开字符追加进密码；任何不确定结果不换路径重试。失败可能留有公开样本，用户需自行清空。

沿用本机钥匙串、当前用户／配对、40 秒总时限、2.5 秒租约和原协议；新本地阶段仍映射 preparing。r73 单次诊断按钮保留但不是完整流程入口；普通测试无需点击它。没有 RFB、蓝牙、USB、驱动或全局策略改动。Computer Use 状态误报和真正整机休眠后的唤醒仍独立待解决。r74 编译／隔离验证与实机结果分开记录，不以构建通过代替实测。

The user confirms r73's public character revealed the previously hidden password field. Refreshed local receipt confirms the public-character stage and 20 lease renewals, without password, Return or deletion; system/display sleep and wake counters are zero. This is physical evidence for printable reveal on this Mac, not complete unlock acceptance or evidence for display-off, full sleep or Computer Use.

r74 integrates that single a into ordinary Wi-Fi unlock after display wake, independent signed-loginwindow/current-console/secure-field/window checks and three seconds of idle input. Posting alone cannot authorize the next step: within six seconds, the same window's login button must change from absent/disabled to enabled and remain ready for at least 150 ms. Supported character counts additionally require zero to exactly one; unsupported counts use the button transition without reading AXValue. Initially enabled submission without zero-count proof, count/interface changes, timeouts, target changes and cancellation stop before credentials, without sample replay. Only acknowledged preparation proceeds to one full AXValue replacement, one revalidated same-window explicit/default button press and two seconds of confirmed desktop. Full replacement avoids retaining the sample in the password. Uncertain operations never retry or switch backends; a failed attempt may leave the public sample for manual clearing.

Keychain/current-user/pair binding, 40-second budget, 2.5-second lease and wire contract remain; the added local stage maps to preparing. The r73 one-use diagnostic button remains separate and is unnecessary for ordinary testing. No RFB, Bluetooth, USB, driver or global-policy change. Computer Use classification and actual full-system wake remain separate. Build/isolated verification and physical acceptance are recorded independently below.


r74 Debug 与签名 Release 应用各 431 项隔离检查通过，Release 构建及固定身份／深度签名核验通过，11 文件 SHA-256 清单完成。完整 swift test 仍因本机缺少 XCTest 模块失败，未计作通过。正常退出 r73 后启动 r74，界面确认新说明、已读取的钥匙串配置及既有启用状态；USB 未连接，Wi-Fi 已认证且状态请求连续成功，蓝牙原状态保留但不参与解锁。本次没有准备诊断按钮、没有输入密码或改系统设置；等待用户以普通 TAB5 解锁请求验收从无密码框到桌面的完整流程。

r74 passed 431 isolated checks in both Debug and the signed Release app, Release build and fixed-identity/deep signature verification. Eleven bundle files are hashed. Full swift test still fails because XCTest is unavailable on this host; it is not recorded as passing. Normal r73 quit/r74 launch confirmed the new notice, loaded Keychain configuration and existing enablement; USB is absent and authenticated Wi-Fi status requests succeed. Existing Bluetooth state is preserved without an unlock role. The diagnostic is not armed, no password is entered and no system setting is changed. Ordinary TAB5 unlock now awaits physical acceptance from a hidden password page through stable desktop.


### r74 无手动准备的完整 Wi-Fi 解锁通过一次 / One complete Wi-Fi unlock accepted

用户按 r74 普通请求验收说明，在 USB 拔出、暂不运行 Computer Use、锁屏后不按 Tab／不点密码框／不输入文字的流程下，回复“自动进入桌面，30 秒未回锁”。随后正常打开本机解锁设置，回执显示“本次解锁已确认成功”；Wi-Fi 状态请求持续成功。当前运行整机睡眠／唤醒均为零，屏幕熄灭／亮起累计为 4／4，不能由累计计数倒推本次请求一定从黑屏发起。该记录接受一次未手动显现密码框的完整解锁及 30 秒保持桌面，不能扩展为真正整机睡眠、独立黑屏起点或 Computer Use 场景已通过。

保留正在运行的 r74，不为本次成功再改实现、构建或全局设置。下一项使用同一候选验收明确从黑屏发起的单次 Wi-Fi 解锁；之后再分别处理真正整机睡眠和 Computer Use 状态识别。没有安装虚拟键盘或其他系统组件。

Following the ordinary r74 test instructions—USB disconnected, Computer Use inactive, no Tab/click/text to reveal the password field after locking—the user reports automatic desktop return with no relock for 30 seconds. The local settings receipt independently says the unlock was confirmed successful, and Wi-Fi status requests continue succeeding. Current-run system sleep/wake counters are both zero; cumulative display-off/on counts are 4/4 and do not prove this particular request began with the screen off. This accepts one unprepared-field complete unlock and a 30-second stable desktop, not actual system-sleep wake, a separately controlled display-off start, or Computer Use.

The running r74 implementation is retained without another code/build/global-setting change. Next, the same candidate will undergo one explicit display-off-start Wi-Fi unlock test, with actual system sleep and Computer Use handled separately afterward. No virtual keyboard or other system component is installed.


### r74 黑屏起点成功，按用户更正记录 / Display-off success confirmed by user correction

用户补充此前有一次“黑屏 → 唤醒后无密码框 → 自动解锁成功”。随后黑屏验收问卷一度回复“没有自动亮屏”，但最新明确更正为“进入黑屏状态之后，然后唤醒并且解锁成功的，是 OK 的”。以最新说明为准，将黑屏后自动唤醒并解锁成功纳入 r74 人工验收；不将被更正的问卷答案记为产品失败，也不要求重复同类测试。前一条完整流程的“30 秒未回锁”保持原记录；不擅自给每次黑屏尝试添加新的次数或保持时间。

工具曾在读取失败回执时报告本机锁定并暂停自动操作，因而未继续 UI 控制；该工具反馈不是桥接故障或睡眠原因的判定。用户已澄清实际成功，因此无需据此改动已成功的输入流程。

只读电源核对发现当前接电配置没有自动整机睡眠定时，网络唤醒选项已启用，且存在其他应用持有的防自动睡眠断言。未改动或清除这些配置／断言。当前桥接运行此前观察到的整机睡眠计数为零，系统累计睡眠计数在本轮两次只读检查间也没有增加；这些是当前环境证据，不倒推所有历史场景。真正整机睡眠后的唤醒仍未经验证，不能与已通过的熄屏唤醒混为一谈。Apple 的网络唤醒说明见 https://support.apple.com/en-gb/guide/mac-help/mh27905/mac 。

下一项保留 r74 核对 Computer Use 活动期间的电脑状态显示，仅要求复现原操作并观察 TAB5；该场景的状态识别与解锁尚未通过。没有新代码、驱动、固件或全局设置改动。

The user first added a successful sequence from display-off through a revealed non-password cover to automatic unlock. A later questionnaire response briefly said the screen did not wake, but the latest explicit correction states that wake and unlock from black screen succeeded. The latest correction governs: record r74 display-off wake/unlock as human-confirmed, do not count the corrected answer as a product failure, and do not request the same trial again. The earlier complete-flow 30-second no-relock report stands; no additional count or duration is invented for individual display-off attempts.

A UI tool reported a locked host and paused automatic operation while checking the apparent failure; UI control was not continued. That report does not establish a bridge failure or sleep cause. The corrected success supplies no reason to change the accepted input path.

Read-only power inspection shows no current AC idle-system-sleep timer, network wake enabled and another application's idle-sleep prevention assertion. These settings/assertions were not changed or removed. The running bridge had observed zero system sleeps, and system-wide cumulative sleeps did not increase between this turn's two read-only checks. These describe the current environment, not every historical event. Actual system-sleep wake is unverified and distinct from accepted display-off wake. Apple's network-wake reference is linked above.

Next, retain r74 and observe TAB5 lock-state reporting while Computer Use is active using the original reproduction; its state classification and unlocking remain unaccepted. No code, driver, firmware or global-setting changes in this turn.


### r75 Computer Use 误报复现与有界诊断 / Reproduction and bounded observations

“暂时无法复现”回复随后被用户撤回。按用户要求由本助手实际触发 Computer Use，在桥接设置中持续刷新诊断；约 14 秒内执行 25 次刷新后工具报窗口不可用并停止。用户按约手动锁屏，确认 TAB5 错误显示“未锁屏”，随后手动解锁。误报已复现，r74 在该场景不能记为通过。最后可读的内存诊断是未锁定通知，手动解锁后的新读数也为未锁定，但旧诊断没有保存锁屏中间状态，不能据此认定具体丢失／覆盖原因。

r75 保留 r74 输入路径和状态决策，只增本机诊断。普通状态读取在决定返回值后记录最多 12 次变化：实际回包锁状态、核验焦点、通知／兼容标记、是否当前控制台、固定通知事件计数，以及 Apple 签名 loginwindow 的在屏窗口数量与数值层级。公开 CGWindowListCopyWindowInfo 只读取当前会话元数据，最多 512 行、八个登录窗口并受半秒预算约束；未知／失败为问号，不当作未锁定。先核验 PID 对应 Apple 签名再读取其显示／透明度／层级，不读标题、账户、字段、位置或像素。不请求新权限，无磁盘日志、网络字段或永久记录；窗口元数据只供诊断，不影响状态、不授权输入。手动解锁后仍可查看状态变化。诊断移入可滚动正文，避免撑大底部区域。r75 不是误报修复完成声明，需要一次相同 Computer Use 复现来对照信号。

The user withdrew the earlier unable-to-reproduce answer and requested that the assistant actually trigger Computer Use. Twenty-five diagnostic refreshes ran for about 14 seconds before the tool reported an unavailable window and stopped. During that activity the user manually locked the Mac, observed false unlocked status on TAB5, then unlocked manually. The r74 Computer Use case is reproduced and unaccepted. The last readable sample and post-unlock state both show an unlocked notification, but the old diagnostics did not retain intermediate states and cannot establish exactly where evidence was lost or overwritten.

r75 retains r74 input and decision paths and adds local diagnostics only. After deciding each ordinary status result it retains at most 12 state changes: returned lock state, verified focus, notification/compatibility flag, current-console boolean, fixed notification counters and signed Apple loginwindow on-screen counts/numeric layers. Public CGWindowListCopyWindowInfo metadata is bounded to 512 rows, eight login windows and a half-second budget. Unknown/failure remains unknown. After process signature verification, only visibility/alpha/layer metadata is read; no titles, account/field text, positions or pixels. No new permissions, disk logs, protocol fields or persistent records. Window observations do not influence state or authorize input. History survives manual unlock in memory, and scrollable UI holds diagnostics. This is not a completed misclassification fix; a matching Computer Use trial is needed to correlate signals.


r75 Release 构建、固定身份／深度签名、Debug 与签名应用各 431 项既有隔离检查通过，11 文件 SHA-256 清单与差异格式检查完成。完整 XCTest 仍因本机缺模块未能执行。正常退出 r74 并启动 r75；设置页确认新说明、既有启用及钥匙串配置，USB 未接、Wi-Fi 状态请求成功。启动时通知与核验焦点为未知，实际回包也为未知，登录窗口在屏计数为零；这些仅是启动读数，不代表锁屏行为。没有准备字符诊断按钮或修改密码。准备继续由本助手触发 Computer Use，用户手动锁屏保持至少十秒后解锁，查看保留的变化。

r75 passed Release build, fixed-identity/deep signing and the 431 existing isolated checks in Debug and the signed app. Eleven bundle files are hashed and whitespace checks pass; full XCTest remains unavailable. Normal r74 quit/r75 launch confirmed the new notice, existing enablement/Keychain configuration, USB absent and successful Wi-Fi status requests. Startup notification/verified focus and returned state are unknown, with zero on-screen login windows; these are startup observations, not lock-screen acceptance. No character probe is armed and credentials are unchanged. The next assistant-triggered Computer Use trial asks the user to remain locked for at least ten seconds before manual unlock so retained transitions can be inspected.

### r75 Computer Use 遮罩与系统锁屏的区别 / Control cover versus system lock

用户反馈未看到 Computer Use 后，助手实际显示桥接窗口、点击刷新并截图，确认窗口有共享标志，然后再次连续刷新。第一轮六次刷新后工具报告锁定并暂停；用户仍观察到 TAB5“未锁屏”。手动恢复后，r75 保留记录显示锁定通知、锁标记、核验焦点及在屏登录窗口曾一致为锁定，约 2–4 秒后一起切换到未锁定。这不是仅一个旧焦点压过持续锁定通知的情形；没有据此更改现有判断或宣称修复。

第二轮同时做了 60 秒独立只读控制台采样，仅输出相对时间、布尔状态和当前用户匹配结果，不保存原始 IORegistry 字典或用户标识。Computer Use 执行 24 次刷新后报告无法自动解锁并停止。采样在约 37.6 秒观察到 IOConsoleLocked=true 和当前控制台会话锁标记为真，40.2 秒变为 IOConsoleLocked=false 且会话锁标记缺失，59.8 秒又观察到锁定。独立状态源也随之变化，因此未将它盲目添加为新的物理锁屏判据。

用户最初说明看到“ChatGPT 正在控制这台电脑”，助手错误地将其解释为本机已经解锁。用户随后明确纠正：这是一层受保护的外部界面，按任意键会返回要求密码的锁屏，不能直接进入桌面。因此撤回“不是锁屏误判／locked=false 已正确”的结论。当前会话通知、焦点及 IORegistry 的未锁读数，不能证明本机已解除保护；TAB5 仍显示未锁屏的问题继续作为未解决项。

这一轮采样未修改桥接代码或全局设置。r74 已通过的普通纯 Wi-Fi 解锁及熄屏起点验收保留。后续需独立识别活跃的受保护界面，对其显示锁定或未知，不能让会话未锁通知覆盖它。进程运行本身不证明保护界面活跃；显示锁定也不授权向保护界面输入密码或退出保护。真正整机睡眠后的唤醒仍未验收。

After the user reported not seeing Computer Use, the assistant raised the bridge window, clicked refresh and captured its sharing indicator before running the loop. Six refreshes completed before the tool reported a locked host and paused. The user still saw unlocked on TAB5. Retained r75 observations after manual recovery showed lock notifications, the compatibility flag, verified focus and visible login windows agreeing on locked, followed roughly 2–4 seconds later by unlocked observations. This is not merely stale desktop focus overriding a continuously observed lock. No decision change or successful fix was claimed.

A second trial paired Computer Use with a 60-second read-only console observation. Only relative times, booleans and current-user matches were emitted; raw registry dictionaries and identities were not persisted. Twenty-four refreshes completed before the tool reported unsuccessful automatic unlock and stopped. At approximately 37.6 seconds the root console and current session indicated locked; at 40.2 seconds the root indicated unlocked and the session lock hint disappeared; at 59.8 seconds locked was observed again. This additional source also changed, so it was not adopted as an independently reliable physical-lock test.

The assistant incorrectly interpreted the initial control-cover report as an unlocked local machine. The user then explicitly corrected this: pressing any key returns to the password lock screen, not an accessible desktop. The conclusion that false lock reporting was correct is withdrawn. Session notifications, foreground focus and registry readings do not establish that local protection has ended. TAB5's false unlocked indication remains unresolved.

This observation round changed no bridge code or global setting. Accepted r74 ordinary Wi-Fi unlock and display-off results stand. Follow-up must independently identify active protection and report locked or unknown instead of letting an unlocked-session notification override it. A running helper alone does not establish an active cover, and displaying locked must not authorize credential entry into, or removal of, the protection. Actual full-system-sleep wake remains unaccepted.


### r76 外层保护检查候选 / Protected local screen candidate

按用户纠正恢复问题定义：Computer Use 外层界面仍要求本机回到密码锁屏，不能将自动化会话未锁读数作为本机已解锁。本候选通过公开 NSRunningApplication、Security 与 CoreGraphics API，检查当前会话中已验证开发者签名的 Computer Use 保护程序窗口。程序标识和开发者团队来自本机已安装包的公开签名元数据；不读取其配置、内部状态、认证数据或实现源码，不关闭或操作该程序。进程常驻且没有在屏窗口时不判锁定。只对签名匹配的进程读取窗口可见性、透明度、层级和几何范围；屏保层级以上且覆盖活动显示器的窗口才是肯定保护证据。可见但无法确证的窗口、读取失败或超限为未知。没有标题、截图、AX 内容、精确位置记录或网络导出。

正向保护证据优先于会话“未锁”通知，电脑控制状态返回既有 locked=true；不确定时不返回 false。它只补充状态显示及否决凭据输入／桌面成功确认，绝不授权输入。请求开始、原登录目标的 isCurrent 及解锁成功确认均保留保护检查；保护未退出或未知时停止，不尝试用准备字符、密码或额外按键退出外层界面。保护退出后仍须沿用 r74 当前控制台、Apple 签名 loginwindow、同一安全字段／窗口、准备回执、单次写入／提交与稳定桌面等独立验证。

观察最多四个匹配进程、512 个窗口条目、16 个可见保护窗口及少于 32 个活动显示器；窗口遍历限制为 0.25 秒，未知不能退化为未锁定。诊断仅保留固定状态、窗口计数及既有最多 12 次内存变化。未改全局设置、系统权限、固件、Windows 或协议字段；USB／BLE 的 Mac PIN 能力继续关闭。针对保护覆盖会话读数、输入否决、未知、常驻无窗口、多显示器及非完整覆盖新增隔离检查。候选构建和实机结果另行记录；不能将签名或几何检查通过本身算作受保护场景已修复。

Following the user's correction, unlocked automation-session readings no longer establish an accessible local desktop. The candidate uses public NSRunningApplication, Security and CoreGraphics APIs to observe windows of the installed Computer Use protection application after developer-signature verification. Identity/team values come from public local signing metadata. It neither reads the application's configuration/internal state/authentication data nor controls or dismisses it. A resident helper without visible windows is not a lock. Only verified owners' visibility, alpha, layer and geometry are examined; a screen-saver-or-higher window covering an active display is positive protection evidence. Partial visible UI, unavailable metadata or exceeded bounds remain unknown. No titles, screenshots, AX content, coordinate logging or device export.

Positive protection overrides unlocked-session reports using the existing locked=true status; uncertainty cannot return false. This observation only affects display and vetoes credential input/success confirmation. It never authorizes input. Request admission, the original login target's isCurrent guard and final desktop confirmation all retain protection checks. No character, password or extra key is used to remove the protection. Once clear, all independent r74 console/signed-loginwindow/secure-field/window/preparation/single-write-and-submit/stable-desktop checks still apply.

Observation is bounded to four matching applications, 512 window rows, 16 visible guard windows, fewer than 32 active displays and a 0.25-second window traversal. Diagnostics retain only fixed states/counts and the existing 12 in-memory transitions. No global settings, permissions, firmware, Windows or wire fields change; Mac USB/BLE PIN remains unavailable. Isolated checks cover conflicting session signals, credential veto, uncertainty, resident helpers, multiple displays and partial covers. Build and physical acceptance are recorded separately; signature/geometry checks alone do not establish a hardware fix.

r76 验证与部署：Debug 和签名 Release 应用各 458 项隔离检查通过；Release 构建、固定身份／深度签名及 11 文件 SHA-256 清单完成，git diff --check 通过。完整 swift test 仍因缺少 XCTest 模块失败，未记作通过。正常退出 r75 并启动 r76，既有钥匙串配置与启用状态保留；TAB5 .264 USB 未连接，Wi-Fi 状态请求连续成功，蓝牙原设置保留。

r76 实机结果：按用户就绪回复实际操作 Computer Use，连续刷新 24 次后窗口切换使诊断按钮不可用，立即停止。用户在“ChatGPT 正在控制这台电脑”的外层界面观察到 TAB5 正确显示“已锁屏”。保留的桥接变化独立确认：会话通知已变为未锁、兼容标记消失、登录窗口计数降为零时，已签名保护程序仍有一个已确认的保护窗口，实际电脑控制回包保持 locked=true。退出外层保护的过渡窗口为未知时没有回报未锁，后续系统登录界面仍为锁定；用户手动认证回到桌面后回包恢复未锁。由此接受本机本次外层保护状态识别，不推及其他系统版本或所有自动化产品。

用户另在外层保护期间尝试了解锁并报告失败。回执确认请求已接收后在输入前被保护检查主动停止，续期为零，没有提交密码。不能将此次显示修复记为外层保护期间解锁成功，也不将其误称为密码错误或网络失败。当前版本不解除／操作另一应用的保护界面；该界面存在或证据不确定时解锁仍停止。

随后明确停止 Computer Use 操作，请用户从可操作桌面手动锁屏，保持 USB 拔出，不按 Tab、不点击密码框，直接发起一次普通 TAB5 Wi-Fi 解锁。用户确认“成功进入桌面，30 秒未回锁”。这接受 r76 普通纯 Wi-Fi 解锁回归，保留原 r74 熄屏起点结果；没有重复熄屏测试，也不将本次扩展为真正整机睡眠验收。全局设置、配对、密码、蓝牙、语音及设备展示配置均未改动。尚未实现外层保护期间的一键解锁。

r76 validation/deployment: Debug and the signed Release application each passed 458 isolated checks. Release build, fixed-identity/deep signature verification, eleven-file SHA-256 manifest and whitespace checks passed. Full swift test still fails because XCTest is unavailable and is not recorded as passing. Normal r75 quit/r76 launch preserved enrollment and enablement; TAB5 .264 had USB disconnected and successful Wi-Fi status requests. Existing Bluetooth settings remained.

Physical result: after user readiness, the assistant ran 24 Computer Use refreshes until a window change removed the diagnostic button, then stopped. The user observed locked on TAB5 while the protected ChatGPT outer screen was present. Retained bridge transitions independently show that although the session emitted unlocked, its compatibility flag disappeared and login-window visibility fell to zero, one verified protection window remained and actual status stayed locked=true. An uncertain transitional guard window did not produce an unlocked claim; the subsequent system login screen remained locked. After manual authentication, desktop reporting recovered to unlocked. This accepts the observed protection classification on this machine, not all OS versions or automation products.

The user also attempted unlock while protection was present. The receipt confirms rejection at admission before password input, with zero renewals. This is neither accepted outer-protection unlocking nor evidence of a wrong password/network failure. The application does not dismiss or operate another application's protection; active/unknown protection continues to stop unlocking.

Computer Use was then explicitly stopped. From an accessible desktop the user manually locked, kept USB disconnected, did not prepare the password field, and initiated one ordinary TAB5 Wi-Fi unlock. The user confirms desktop return with no relock for 30 seconds. This accepts the r76 ordinary Wi-Fi regression check and retains earlier r74 display-off evidence; no duplicate display-off or actual system-sleep acceptance is inferred. Global settings, pairing, password, Bluetooth, speech and device display preferences are unchanged. Unlocking while the outer protection remains present is still unimplemented.

### 外层保护交接接口调查 / Protected-screen handoff investigation

用户进一步要求只点 TAB5、全程 Wi-Fi、不触碰 Mac 且不改全局设置。2026-10-23 核对本机可用工具、桥接调用方及 OpenAI 公开文档后，仍未找到满足该约束的受支持交接入口；本次没有新增解锁实现、构建或实机成功记录，继续保留 r76。

[Computer Use 的 Locked use 文档](https://learn.chatgpt.com/docs/computer-use#locked-use)明确说明其临时解锁仅服务于受信任的活动控制任务，并保留本地屏幕保护，不是供其他应用使用的通用解锁能力。这与本轮“会话未锁，但本地仍受保护”的观察一致。

[App Server 的 turn/interrupt](https://learn.chatgpt.com/docs/app-server#interrupt-a-turn)可以取消指定任务轮次，但其文档没有定义保护窗口的拥有者发现、屏幕交还回执或恢复系统登录界面的保证。仓库现有 `MacCodexDesktopIPC.execute` 也要求明确的任务和预期轮次；保护窗口元数据不提供该关联。不能把任意任务取消成功、保护进程常驻或一个会话未锁读数当作交接完成，也不自动停止所有任务来猜测拥有者。

当前缺口是受支持的控制权交接：需要可绑定本次用户请求及实际控制任务的接口，由控制方正常结束保护并给出可核验结果，再重新执行已有登录目标验证。若该接口以后可用，才有依据继续开发并验收。此结论仅说明当前已核对的接口不足，不宣称所有未来版本都无法实现。未使用私有保护协议、进程终止、模拟物理输入或削弱 r76 输入否决来替代交接。无需用户重复锁屏测试这个尚未实现的流程。

The user now requires a TAB5-only, Wi-Fi-only action with no Mac interaction or global changes. On 2026-10-23, the available tools, bridge callers and public documentation were checked. No supported handoff satisfying these constraints was found. No new unlock implementation, build or successful physical trial resulted; r76 remains in place.

The linked Locked use documentation confines temporary desktop access to trusted active control tasks while preserving local protection. This matches the observed discrepancy between session state and local accessibility. The linked App Server interrupt method cancels a specified turn but does not document protection-owner discovery or a completed screen handoff. Existing `MacCodexDesktopIPC.execute` also needs an explicit task and expected turn; protection-window metadata supplies neither. Cancelling an arbitrary task is not handoff evidence, and cancelling every task is not adopted.

Further implementation needs a supported request-bound handoff from the actual controller, a verifiable result and fresh validation of the ordinary login target. This is a limitation of the interfaces examined, not a claim about every future version. No private protection protocol, process termination, synthetic physical input or weakened r76 credential veto substitutes for that handoff. No additional user lock test is requested for this unimplemented flow.
