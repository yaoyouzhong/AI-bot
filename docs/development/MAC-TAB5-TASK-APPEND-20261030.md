# TAB5 Codex 任务页追加 / Task-page follow-up

## 用户目标

任务页追加与「Codex 直达」交付语义一致：选定精确会话，面向发送时的最新运行轮次，由 Codex 当前偏好选择排队或引导；可靠入队即发送成功。保留设备草稿及照片、电脑原有草稿，单次确认最多一次发送，未知结果不自动重发。

## 2026-10-30 实机观察

- R35／TAB5 .295，任务页点击“追加”显示“请求已处理，请先查看动态”，用户确认电脑既无排队项也无该消息。固件将大部分 409 映射成同一句话。
- R35 的桌面 IPC `supportsExpectedTurnSteer=false`；`MacTab5Codex.handle` 在发送前对 `steer` 直接返回 `desktop_steer_requires_expected_turn_support`。当前桌面 follower-steer v1 不接收调用者提供的预期轮次，选择发送时的当前轮次。不能从通用提示单独反推其他全部 409 的原因。
- 用户随后以「Codex 直达」发送，确认电脑队列中仍有消息、未点引导时 TAB5 显示成功；已将跟进处理方式恢复引导。两条路径不能混作同一个验收。

## 设计约束与未解决事项

- 不直接删除拦截：桌面 follower-steer 动作直接引导，不自动遵循用户的排队偏好。已有 `thread/queue/add`／`turn/steer` 协议定义，但本桥接的独立子进程是只读且不能替代桌面会话所有者；直接改成可写并不能证明正确交付。
- 「Codex 直达」使用原生输入框及只读回执：直接复用前需解决精确会话切换、电脑输入框已有内容及引用／附件的保留、设备图片输入、问题卡与普通输入框区分、异步收发、幂等以及未知结果不重发。不能将用户既有草稿一同误发送，也不能把图片丢弃后声称成功。
- 桌面内部 follower 的队列状态同步是整体状态操作，不能用未经并发验证的旧快照替换已有队列。需先建立可靠的、与当前桌面所有者兼容的单条添加及偏好获取机制；分别验证引导、排队、任务轮次变化、同名任务、图片、已有草稿、双击及超时。
- 如果恢复发送需要新增协议字段或改变设备状态提示，遵照仓库规则同时更新 Windows、TAB5 和协议文档；新增或修改 UI 必须先设计并进行渲染与真机检查。明确错误文案不能代替实际发送修复。

当前仅记录已证实事实与待实现约束；任务页追加尚未修复。

## English

Desired behavior is delivery to the selected exact conversation and its latest running turn, respecting Codex queue/steer preferences, retaining desktop/device drafts and images, and never automatically retrying an uncertain result. R35 task-page append is rejected by the current desktop IPC guard and TAB5 maps its 409 to a misleading generic message. Desktop-direct queue delivery was separately accepted in hardware. Follower steer alone bypasses queue preference; the public child is read-only and does not own the desktop session. Any integration must preserve existing queue entries, drafts and images and prove idempotency, timing races and both modes. This remains unresolved.

## R36 本地实现与交互设计（待真机验收）

- 保留任务页现有“追加”按钮、字号、尺寸、颜色和位置，继续复用既有 202“已追加，可查看会话动态”；成功表示桌面已接受发送或排队，不表示执行完成。没有新增设备字段或固件界面。
- Mac 将任务页的文字追加接到与直达相同的原生发送按钮，遵循 Codex 当前排队／引导设置。以准确 taskId 定位，发送时读取最新状态，不再因为设备显示的旧轮次拒绝文字追加；停止任务仍严格绑定 expectedTurn。
- 复用已加密的任务／设备／requestId 日志，在任何导航、写入、提交前保存未知回执。重复请求只读回执；未知结果不自动重发。只在确认原生提交或新消息／排队回执后清理设备草稿。
- 为保护独立草稿，电脑主输入框必须为空，题卡不得作为任务页的发送目标，电脑已有附件／引用不得被一同发出。设备带照片的追加保留草稿并拒绝，当前原生适配器仅支持文字；不将此场景计为完成。
- 锁顺序：任务日志已锁定时，桌面适配器只尝试取得自身锁，不阻塞等待；不调用任务 handle 或清理草稿接口，避免与直达路径反向嵌套。
- 视觉验收：使用现有 .296 固件在真实任务页检查运行中“追加”、提交反馈和草稿状态；不得用直达或问题回答框代替任务页验收。分别验收引导、排队且未点击电脑“引导”前的反馈，然后恢复引导设置。

English: R36 locally routes text-only task append through the native composer action, preserving Codex queue/steer preference and using the selected exact task's current state. Existing encrypted receipts prevent replay; unknown delivery retains the draft. Desktop drafts, question cards and attachments block submission. Device-photo append remains unsupported and must retain its draft. Existing wire fields and TAB5 layout are reused. Both modes still require task-page hardware acceptance; direct/question-card sends are not substitutes.

### 安装与验证记录

- R36 已于本地固定 App 路径安装并启动；签名核验通过，钥匙串 helper 与 R35 完全一致，旧包已备份。TAB5 .296，USB 数据确认、Wi-Fi 已认证请求、蓝牙已认证。
- Release 构建通过；专项 `--self-test-task-append` 通过（均为合成数据，未操作真实桌面）；既有 parity 自检 28,784 项通过。`swift test` 因本机 Command Line Tools 缺少 XCTest 无法运行，未宣称完整单测通过。
- 二进制 SHA-256：`ae21beb29aed7d7d0c784bf94f674a09097540a12239f8e5b67be896443134e9`。忽略目录 `artifacts/mac-voice-source-20261029/task-append-r36/` 保存源文件摘要、构建日志、签名与部署记录、R35 备份。
- 正在等待任务页引导／排队实机反馈；未提交或推送本候选。

### R36 真机未通过与下一轮设计

用户反馈任务页仍显示“请求已处理，请先查看动态”，会话未收到测试消息；明确记为未通过。桥接诊断显示已经识别目标任务和主输入框，但拒绝分支沿用了该成功诊断，无法区分已有草稿与附件检查。这是诊断缺陷，不能推断实际送达。

下一轮 UI 设计保持布局不变：Mac 连接诊断使用明确的未发送原因（已有草稿／附件或输入区结构无法确认），替换“暂不开放追加”的旧说明，字号和现有提示色不变；TAB5 任务页的通用 409 文案由“请求已处理”改为“请求未接受，请检查电脑”，不再将拒绝表达为成功，继续保留草稿。文字须在现有状态栏渲染检查。不会增加按钮或自动重试。附件检测须识别实际内容，空布局占位不应当作附件；不通过放宽已有草稿保护来实现发送。

### 用户纠正验收顺序／R37

用户确认 R36 失败后电脑输入框为空。原附件检测对空布局／工具栏过宽，R37 改为区分空附件布局与真实附件内容，并增加已有草稿／附件／结构未确认诊断；实际失败原因仍须由修订后的真机诊断闭环，不能仅凭合成测试宣称已解决。

用户要求先查清并验证桥接原因，再考虑固件。`.297` 仅构建及归档，从未刷入；固定 latest 已恢复 `.296`，版本登记也保持 `.296`。后续保持设备固件不变，先测试桥接。该顺序已写入 AGENTS.md。

R37 Release 构建与追加专项检查通过，提取实际附件布局函数的 8 项检查通过，现有 parity 28,784 项通过；完整 XCTest 仍因本机缺模块无法执行。Mac Codex 权限页新说明已检查实际浅色完整及深色最小窗口渲染。R37 签名包 SHA-256：`8039c1c4046cca9df3d73b1ab53c72dbe3d2d6b3cf3a060b85e334ffe6038d68`。等待 `.296 + R37` 实机结果。

### R37 实机结果与只读诊断设计

R37 + .296 实机仍未送达，诊断为附件／引用拦截；用户确认主输入框为空且没有照片、文件或引用卡片。此项明确为误拦截，不能把空布局修正当作已完成。

下一步先通过现有“定位会话”做只读结构诊断，不再让用户重复发送。既有 Mac“连接与诊断”区域附加固定类型统计（正文内外的图像、链接、附件容器、移除按钮数量、扫描边界），不记录标题、输入文字、文件名、URL 或截图。布局仍为现有可换行诊断文字，不添加按钮，不更改设备。定位后允许核对主输入框，只聚焦而不写入；题卡仍不得作为任务追加目标。查清具体误判边界后再修复发送路径。

### R38 只读现场证据

用户仅重新定位，没有录音或发送。结构计数为 `body.body-slot=1, body.body-slot.AXImage=1, body.body-slot.AXTextArea=1, nodes=13`，无附件容器或引用容器；用户明确确认空输入框且无附件。图像控件位于正文区域，但此前仅跳过 AXButton 工具控件，未覆盖 AXPopUpButton／AXMenuButton。R39 将原生工具选择控件也视为边界，避免检查其装饰图标；实际附件容器、引用及移除操作仍先检查。下一次仍先只读定位，ready=1 后才恢复发送验收；若仍不通过，统计控件祖先以进一步定位，不重发消息。

排序另有已证实的问题：桌面目录的本会话 `source_recency_at` 比首项旧约 16 分钟，虽然当前会话仍在运行并接收输入；现有桥接优先采用 recencyAt，导致“最新”列表滞后。会话 ID 定位正确。该排序问题单独待修，不用改变选择或重命名掩盖。

### R39 定位通过／真实原因

用户只定位后，诊断为 `body.AXPopUpButton.AXImage=1, body.body-slot=1, body.body-slot.AXTextArea=1, nodes=13, ready=1`。因此误拦截的图像已证实属于原生下拉按钮的装饰，并非用户附件。只读检查通过，已要求在任务页做一次明确追加以验证原生提交。真实 Mac 诊断窗口渲染已检查，文字在可滚动区域换行。

### 最近会话排序设计与 R40 实现

保留现有列表、字体、按钮、触控区及固定任务规则，不改变当前选中 taskId。排序采用桌面有效活动时间与明确本机使用时间中的较新者；成功定位和成功提交才登记本机使用时间，后台读取、心跳、轮询和记录维护不登记。即使同一轮持续运行导致桌面目录时间停滞，刚使用的会话仍会及时排到最近列表前面。记录仅存 UUID 与时间，限制 512 条，重启保留；目录更新时间较新时仍尊重目录。现有 updatedAt 秒字段保持，子秒操作只用于稳定次序。

本机记录不意味着另一设备或未经过桥接的所有操作都已实时同步；该范围须如实说明。新增验证包括同秒使用顺序、重启保留、时钟回退、只读不更新和新桌面活动优先。

### R40 交付状态

R40 已签名安装，SHA-256 `6d3a6b6a5b47aaa77cf543ffcdd9537c8fd30ef5b7be6e4e1a3b82e91a7933de`，保留 R39 备份。Release 构建、追加与排序专项检查及 28,784 项 parity 通过；完整 XCTest 仍因本机缺模块未运行。固件未升级，latest 与设备保持 .296。R39 只读现场 `ready=1` 已通过，但尚未收到用户后续任务页发送结果；引导、排队与 R40 列表排序仍待真机确认。设备带照片的追加仍为未完成项，不把文字路径视作完整附件支持。

R40 启动后等待超过两分钟，设备中心仍显示 USB 未连接、Wi-Fi 等待、蓝牙搜索；系统 `/dev/cu.usb*` 无设备。未发起验收，已询问用户 TAB5 是否能正常切页。上次确认设备版本为 .296，期间没有刷固件。此为现场待确认状态，不推断硬件故障或发送修复已经通过。

### R40 连接恢复与排序验收

用户确认 TAB5 可正常操作。随后设备中心确认 .296、USB 数据已确认、Wi-Fi 已收到认证请求、蓝牙已认证，连接恢复；未刷固件。用户按“定位当前会话，再重新打开列表”的步骤反馈“已排在第一位”，列表排序此项真机通过。任务页原生追加的引导与排队仍需分别验收，不由排序通过推断发送通过。

English: The device remained responsive and all three authenticated links recovered on firmware .296 without flashing. The user confirmed the current conversation appears first after locating it and reopening the list. Ordering passed this hardware check; task-page steer and queue delivery still require separate acceptance.

### R40 任务页引导验收通过

在无问题卡的运行中会话，用户从任务页追加“任务页引导验收”，消息实际进入当前会话。桥接诊断显示“最近发送预检：已提交”，并确认会话新增本次消息；结构检查 `ready=1`。用户进一步确认“无需电脑操作，显示成功”。因此文字追加的引导模式本次真机通过。用户随后已将跟进处理方式临时改为排队，排队验收尚待结果。

English: With no question card active, the task-page steer test reached the current running conversation. The bridge confirmed a new matching message and the user confirmed success on TAB5 without computer intervention. Text-only steer passed this hardware check; queue acceptance remains pending after the user switched the follow-up preference to queue.

### R40 任务页排队验收通过及收尾

用户切换排队偏好后，在无问题卡的运行中会话，从同一任务页单次追加测试文字，反馈“正常的，消息在排队”。尚未要求点击电脑引导时，桥接显示“已确认 Codex 保存本次排队消息，等待当前运行结束”，最近发送预检为已提交、结构检查 `ready=1`。据此任务页文字追加的排队接受确认通过，可靠入队即发送成功；此结论不表示任务已经执行完成。用户随后确认“已恢复引导”。

本轮 R40 + .296 真机已通过当前会话列表排序、文字引导追加、文字排队追加；未刷固件。照片追加仍不支持，保留为未完成项。完整 XCTest 环境限制及此前本地专项验证结果不变；本轮未提交、推送或发布。

English: After switching to Queue, the user reported the task-page follow-up was normally queued. Before any manual Steer action, the bridge independently confirmed Codex retained the queued message, with submitted preflight and `ready=1`. Queue acceptance passed, without implying execution completion. The user restored Steer. R40/.296 passed ordering and text follow-ups in both modes without flashing. Photo follow-up remains unsupported; XCTest limitations remain unchanged. No commit, push or release occurred in this acceptance run.

排队后续：当前轮次结束后，“任务页排队验收。”实际进入本会话，补充确认本次入队消息随后正常交付。用户要求继续大疆以外的可验项目；已有电脑草稿／真实附件保护及重复点击正在安排真机验证，未提前记为通过。

Queue follow-up: after the active turn ended, the queued test message reached this conversation, confirming subsequent delivery for this run. The user requested remaining feasible checks except DJI; desktop draft/attachment protection and repeated-click hardware checks are being arranged and are not yet marked passed.

### R40 已有电脑草稿保护真机通过

用户在电脑主输入框保留指定测试草稿，从 TAB5 任务页单次追加另一段测试文字。用户确认会话未收到消息；桥接诊断明确为“追加未发送：电脑已有草稿”，随后用户确认两边内容均完整保留。已有电脑草稿的拒绝发送与双端保留通过。设备 .296 仍将该拒绝显示为“请求已处理，请先查看动态”，误导文案再次复现；不能将正确拦截算作错误提示也已修复。Mac 诊断上方“最近发送预检：已提交”仍沿用此前成功状态，具体最新失败位于下方直达诊断；该显示一致性需另行整理。

English: A task-page append was rejected with a known pre-existing desktop draft. The user confirmed no message arrived and both desktop and TAB5 text remained intact; the bridge explicitly identified the desktop-draft guard. Protection passed. Firmware .296 still displays its misleading generic conflict text, so feedback wording is not accepted as fixed. The Mac top-level latest-preflight label retains an earlier submitted status while the detailed desktop diagnostic reports the new rejection; this diagnostic consistency remains to address separately.

### R40 真实附件保护失败／R41 修复

用户清掉电脑测试文字，仅添加一个测试图片附件，再从 TAB5 追加保留的测试文字。文字及图片一起进入本会话，用户明确确认电脑已发送，TAB5 显示结果不确定且保留文字。真实附件保护失败；未知回执没有自动重发，不能掩盖发送前漏检。不会保存测试图片或路径到仓库。

现场结构诊断 `body.AXButton.remove=1, body.AXPopUpButton.AXImage=2, nodes=17, ready=1`：诊断遍历到了移除按钮，但发送前检查把下拉控件作为整棵子树的跳过边界。R41 改为完整有界遍历，装饰图像可作为控件内容，但所有后代的附件槽、引用、移除操作及嵌套交互控件仍必须检查。抽出生产检查函数并加入真实结构形状的合成回归，覆盖装饰图标放行、下拉中的移除按钮拒绝、多层嵌套、引用槽、其他编辑器和扫描上限；任务日志回归同时验证该结构在写入／按发送前被拒绝，重放不发送。

保持 .296，不刷固件；R41 先构建与本地验证，再用用户明确定位触发只读附件检查。只读结果未通过前，不要求再次发送附件。当前仅设计检查逻辑，没有新增或改动 UI。

English: The real attachment guard failed on R40: task text and the pre-existing desktop test image were submitted together, while TAB5 retained text with unknown delivery. The metadata scan saw a removal button but the admission scan pruned popup descendants. R41 traverses every descendant within bounds, tolerating decorative icons while rejecting attachment slots, removal actions and nested controls. Synthetic tests exercise the observed tree shape and confirm rejection before write/submit with replay protection. Test-image content/path is not stored in the repository. Firmware remains .296; read-only explicit navigation must confirm the fix before further attachment submission tests. No UI change is introduced here.

R41 Release 构建、追加专项及既有 28,784 项隔离检查通过；`swift test` 仍因缺少 XCTest 无法执行。已签名安装至固定 App 路径，保留 R40 备份，钥匙串 helper 不变。二进制 SHA-256：`f89c59bf01607e115ab97450170c08b32e2ae7dceaf922fe2efa68730781b88c`。冻结源文件、哈希、签名及部署记录位于忽略目录 `artifacts/mac-voice-source-20261029/task-append-r41/`。真实附件只读检查及后续保护验收待确认，不能把合成结构通过当作实机通过。

R41 Release, append-specific checks and 28,784 isolated parity checks passed. Full XCTest remains unavailable. The signed candidate is installed at the fixed app path with R40 backup and unchanged keychain helper. The binary SHA-256 and ignored local evidence directory are recorded above. Hardware read-only attachment detection and protection acceptance remain pending.

R41 只读实机预检通过：连接恢复至 .296／USB 数据确认／Wi-Fi 与蓝牙认证；用户仅执行直达定位，桥接记录定位 200，结构 `body.AXButton.remove=1, body.AXPopUpButton.AXImage=2, nodes=18, ready=0`。真实附件已阻止放行。随后用户用电脑主输入框回复“已定位”时将测试附件一同发来，该回复不是任务页追加拦截失败的证据；这是测试反馈步骤设计不当，后续只用独立题卡反馈，保持测试主输入框不动。实际追加拒绝与双端保留尚待复验。

R41 live read-only preflight passed: explicit navigation returned 200 and the real removal-button/popup-image structure produced `ready=0`. The subsequent computer reply carried the staged test attachment; this is not evidence of a task-append guard failure. Feedback now uses a separate question card to avoid disturbing the test composer. Actual append rejection and retained-content checks remain pending.

### 已确认送达但待确认草稿锁定／R42 交互设计

用户报告主输入框和长按清空均不可用，发送按钮显示“查询送达”。源码确认旧 504 回执只原样查询，设备为防重发锁住该请求；没有人工确认已送达的恢复出口。此前要求点击“追加”不适用于这个状态，不能记作新附件保护测试。

新增 Mac 本地恢复入口，复用“Codex 会话”页原生次级按钮，置于 CLI 选择旁，标题“处理待确认发送…”。原生确认对话框标题“确认消息已送达”，正文先要求用户核对电脑会话，再说明只确认一条回执、不会重新发送。420 点宽的附件视图采用垂直信息层级：会话与请求短 ID 下拉选择、最多三行的原测试文字预览，12 点间距、系统 13 点字体和现有主／次文本色；长文省略不溢出。默认操作为“取消”，次操作“已在电脑确认送达”；只有匹配未变化的未知追加请求可处理，不批量清除、不移除照片、不操作 Codex。确认前重新核验请求、草稿指纹及启用权限；仍为同一条才原子登记本地人工确认的成功回执，保留其幂等记录。设备下次查询沿用现有 202 回执解除冻结，无新设备字段／固件改动。

新增 UI 必须先检查原生浅色／深色实际渲染；恢复操作本身随后真机验证。Windows 对齐清单记录此本地恢复能力，不把 Mac-only UI 当作协议变更。

English: An old unknown receipt permanently locks device editing while queries repeat 504. R42 adds a local Mac secondary action and native confirmation dialog for one unchanged pending follow-up the user has verified in the computer conversation. Cancel is default; an exact request/draft fingerprint and current permissions are rechecked before atomic manual acknowledgment. No resend or Codex action occurs. The existing 202 receipt unlocks the device on query; no protocol or firmware change. Native light/dark rendering and hardware recovery must be checked separately.

R42 界面实现采用请求短 ID 在前、会话标题在后的选择项，避免长标题遮住识别信息；最多三行文字预览，完整会话名／请求 ID 和正文可悬停查看。默认取消。实际原生离屏渲染已检查浅／深色、长标题／长文字及功能页默认／最小尺寸；未打开或置顶前台窗口。首次缓存渲染缺少背景与按钮文字，已按实际绘制外观合成语义背景，并修正长文换行后重新检查，不将首次异常图片计为通过。

恢复回归覆盖准确请求、查询不重发、人工确认后原请求重放仍只读成功、权限撤销拒绝、草稿变化拒绝及人工来源记录。首次测试对请求 ID 大小写的期望不符，已按生产 UUID 规范化修正测试；后台 Debug 专项通过。Release 最终安装包仍待构建与部署，当前实机仍为 R41。

English: The picker places the short request ID before the title; three-line text previews provide full-content tooltips. Native offscreen checks cover both appearances, long content and default/minimum feature-page sizes, without foreground windows. Initial cache/compositing and wrapping artifacts were corrected before visual acceptance. Recovery regressions cover identity, no retry, accepted replay, revoked permissions and changed drafts. A test UUID casing expectation was corrected to match production normalization. Debug checks pass; final Release packaging/deployment is still pending and R41 remains installed.

R42 最终 Release 构建、追加／恢复专项及 28,784 项隔离检查通过，原生功能页离屏回归通过；完整 XCTest 仍因缺少模块无法执行。用户正常退出后，后台替换并验证签名，保留 R41 备份，钥匙串 helper 未变化。二进制 SHA-256：`65df5c7550c3dc8bd3b90af3e94c62f32d65afe741037ecf1f0bebec4a784d03`。按用户后续“你启动桥接程序”指示启动 R42，后台进程／哈希已确认，未自动打开恢复对话框；用户自行操作恢复入口。冻结源／构建／渲染／部署证据位于忽略目录 `artifacts/mac-voice-source-20261029/task-append-r42/`。恢复解锁、真实附件拒绝和重复点击真机结果仍待确认。

R42 final Release, append/recovery regressions, 28,784 isolated checks and native feature-page offscreen checks passed; full XCTest remains unavailable. After the user's normal exit, the signed bundle was installed in place with R41 backup and unchanged helper. R42 was started on the user's subsequent explicit request and its process/hash verified. Recovery is user-operated and was not automatically opened. Hardware unlock, attachment rejection and repeated-click results remain pending.

R42 恢复真机通过：用户通过 Mac 人工确认旧测试消息已送达，在 TAB5 查询后明确反馈“已解除锁定，旧文字已清除”。没有重发测试消息。用户指出两端恢复步骤复杂，已记录体验改进：可核实的回执应自动恢复，剩余真正不确定的状态应在 TAB5 提供单次明确人工确认；当前 Mac 确认加设备查询是保持 .296 的兼容恢复路径，不将其记为最终交互方案。

R42 hardware recovery passed: after local manual acknowledgment and a TAB5 receipt query, the user confirmed the lock was released and old text cleared without resending. The user objected to the two-device complexity. Record future UX work: automatically reconcile provable delivery, and provide a single explicit device-side confirmation for genuinely uncertain cases. The Mac-plus-query path is the current .296-compatible recovery, not final UX acceptance.

R42 新请求附件保护复验通过：旧记录解锁后，用新文字“附件保护复验”从任务页提交，用户通过独立题卡确认“未发送，电脑图片和 TAB5 文字都保留”。随后进入移除电脑测试图片、改为新文字的重复点击场景，用户报告两次均未发送且显示通用 409 提示，因此重复点击验收未通过，不能仅因零发送而记作防重通过。正在获取本次拦截具体原因；遵照用户要求不自动打开或置顶电脑窗口，由用户读取现有诊断。

R42 fresh-request attachment protection passed: after unlock, the user confirmed the new test was not sent and both desktop image and TAB5 text remained intact. The following double-click test produced no delivery and the generic conflict hint, so duplicate-click acceptance remains failed/pending diagnosis; zero deliveries is not a pass. The user reads the existing diagnostic without automated foreground windows.

用户进一步要求诊断由助手自行读取，但不得抢占前台。只读读取当前已有桥接窗口只得到设备中心，未取得本次拦截分支；不再要求用户抄诊断。R43 新增本机用户目录下的后台元数据记录，最多保留桌面判定与请求结果两条当前记录，0600 权限、原子写入；只由实际 AppDelegate 注入，合成验证不写真实目录。记录固定状态、结构计数、固定拦截分支、进程与时间，不含消息、图片、文件名、会话／请求 ID 或凭据。补齐追加／查询回执的固定诊断标签；没有放宽发送检查或改动固件。Debug 专项已通过，Release 构建中。

The user requires agent-read diagnostics without foreground disruption. Reading the existing bridge surface exposed only Device Center, not the rejection branch. R43 adds a user-only, atomic local metadata file containing just the current desktop decision and request result. Only the real application injects this sink; fixtures use isolated storage. It contains fixed statuses/branch names, structure counts, PID and timestamp, never content, filenames, task/request IDs or credentials. Follow-up/receipt diagnostic labels are added without loosening submission checks or changing firmware. Debug validation passes; Release is building.

R43 Release／追加恢复与诊断专项／28,784 项隔离检查通过；完整 XCTest 仍缺本机模块。固定路径已安装并启动 R43，二进制 SHA-256 `9b192f36d3848e6e9fd02756de3d87eea3ba874a6f31027b1d4fead42ef337d4`，保留 R42 备份、签名核验通过、钥匙串 helper 未变化。为避免前台操作，对精确路径及 R42 哈希核验后的唯一桥接进程发出 SIGTERM，确认退出后替换；没有强制 SIGKILL 或操作其他进程。冷启动后只通过后台进程／哈希／串口存在性检查，未自动打开诊断窗口。保持 .296。等待用户从 TAB5 触发一次空主框只读定位；尚无新的诊断文件，不能推断本次重复点击拒绝原因或验收通过。

R43 Release, targeted regressions and 28,784 isolated checks passed; full XCTest remains unavailable. The signed candidate is installed/running with R42 backup and unchanged helper. A narrowly verified R42 process received SIGTERM for background replacement, without SIGKILL or other-process actions. Startup was checked through process/hash/port metadata without opening diagnostic windows. Firmware remains .296. A user-triggered read-only locate on the empty composer is pending; no new diagnostic file/event yet establishes the duplicate-click rejection cause.


### R43 定位失败现场／R44 补齐导航诊断（2026-10-31）

用户报告定位已切换会话，但 TAB5 提示未确认 Codex 前台。R43 后台元数据确认：定位 409／`desktop_foreground_unconfirmed`，桌面判定为“未取得 Codex 主页面的可访问导航区域”，不是“前台应用不是 Codex”。因此不能将该提示解释为用户没有切到 Codex，也不能凭可见切换直接放行输入或发送。当前安装桌面应用标识仍为 `com.openai.codex`，版本为 `26.1027.30824`；仅此信息不能确定是应用升级或 AX 时序造成。

R43 只输出追加区域结构，导航失败尚未进入追加检查，导致 detail 为空。R44 为导航单独记录固定节点角色桶、网页区域数、可信区域数、地址缺失／不匹配计数、子节点读取 AX 错误码及耗时。定位与任务追加的导航失败均记录本次导航元数据，不沿用先前追加结构。地址、标题、编辑器内容及会话标识不进入记录。没有新增界面、修改提示文案、放宽身份或附件判断，也不改变固件。本轮只补齐诊断，尚未宣称修复定位或重复点击问题；后续依据用户主动定位产生的新证据继续修复。

English: The live R43 metadata shows a navigation-shell failure, not a non-Codex foreground application, despite the device's generic foreground hint. Visible navigation alone does not authorize writing or sending. R44 records bounded fixed navigation counts and AX child-read errors separately from composer evidence, including task-append navigation failures. It logs no addresses, titles, contents or task identities. There is no UI, protocol, firmware or admission-policy change. This is a diagnostic correction; navigation and duplicate-click hardware acceptance remain unresolved.


R44 Release 构建、专项回归及 28,784 项隔离检查通过；完整 XCTest 仍缺模块。已签名安装并通过后台隐藏启动运行，保留 R43 备份，helper 未变；首次 LaunchServices 启动返回 -600，确认进程未运行后第二次后台启动成功。二进制 SHA-256 `f7a19b2059e5fee200c46e6143d7382f9617d3b3c4390878893276d8c76c74d5`。证据在忽略目录 `artifacts/mac-voice-source-20261029/task-append-r44/`。未刷机、未操作或置顶前台窗口；等待用户主动定位一次以获取本次导航统计。未提交、推送或发布。

R44 Release, targeted regression and 28,784 isolated checks passed; full XCTest remains unavailable. The signed candidate is running after a background hidden launch, with R43 backup and unchanged helper. The first LaunchServices attempt returned -600; a second attempt succeeded after verifying no process was running. No firmware or foreground UI operation occurred. New user-triggered navigation evidence remains pending. No commit, push or release.


R44 用户主动定位后，后台记录定位 200，主输入区 `body.AXPopUpButton.AXImage=1, body.body-slot=1, body.body-slot.AXTextArea=1, nodes=13, ready=1`，已确认目标任务和主输入框。此次定位与空主框检查通过；之前导航区域不可读没有复现。R44 未改变导航判断，不能据一次成功宣称该间歇问题已修复。继续安排无题卡、空电脑草稿的重复点击真机验收；尚未收到该测试消息。

English: User-triggered navigation on R44 returned 200 with the expected empty composer structure and ready=1. This locate/preflight passed, but the earlier unavailable navigation region did not reproduce; R44 did not change navigation admission, so one success is not evidence of a permanent fix. Repeated-click hardware acceptance is next, with no question card and an empty desktop draft, and remains pending.


R44 后续追加得到 409／composer_changed，后台明确判定为“电脑已有草稿”，发生在写入及发送之前；未收到测试消息。此阶段尚未进入本次附件检查，记录里的 ready=1 属于上次定位，不作为本次发送通过证据。正在确认电脑已有文字是否由本次语音自动回填产生，不能将这次拒绝计作重复点击验收通过，也不能直接认定为残留草稿。

English: The next append returned composer_changed/409 specifically for a pre-existing nonempty desktop draft, before writing or sending. No test message arrived. The retained ready=1 metadata belongs to the preceding locate, not a new attachment scan. Whether the desktop draft came from automatic voice staging remains to be established; duplicate-click acceptance is still pending.


### R44 按钮防连点与提交收尾真机通过（2026-10-31）

用户清除电脑已有文字后，从 TAB5 任务页发起追加。本会话只收到一条测试消息；后台为追加 202，并明确确认会话新增本次提交，主输入区域检查 ready=1。用户说明首次点击后按钮显示“追加中”且变灰，因此实际只点击成功一次，不能再次点击；随后确认 TAB5 显示成功、清除本次文字并恢复正常。按钮防连点、单条送达及成功后的草稿／按钮恢复通过。这里没有产生两个实机请求，不能称为“两次请求重放”硬件通过；同 requestId 重放不重发仍由本地合成回归覆盖。

本轮已通过照片保留与高清预览、电脑已有文字保护、真实电脑附件拒绝并保留双端内容、未知结果人工确认解锁、任务页文字引导／排队、会话排序及按钮防连点收尾。DJI 音源依用户要求暂不验；任务页设备照片追加仍未支持；.296 通用拒绝文案及两端人工恢复流程仍有已记录的体验改进。R43 出现的导航主页面不可读在 R44 后续定位成功，根因尚未确定，不能宣布修复。连接中断与长期运行仍按用户决定留待日常使用观察，不新增已知故障。

English: After the user cleared the desktop draft, task append produced one received message and a 202 accepted-message receipt. The user confirmed the first tap disabled the button with an in-progress label, preventing a second tap; TAB5 subsequently showed success, cleared the text and restored normal controls. Button-level repeated-tap prevention and successful completion passed. Two hardware requests were not generated, so request replay remains covered by synthetic regression rather than this physical check. Remaining limitations: DJI deferred, device-photo follow-up unsupported, misleading .296 conflict text and cumbersome manual recovery UX, plus the earlier unavailable navigation region whose cause remains unproven despite a subsequent successful locate. Connection interruption/long-run behavior stays ordinary-use observation, not a newly declared defect.

## 2026-10-31 R45/R46 与 .298 收尾

- Mac 拦截原因区分电脑草稿、附件／引用、未确认会话及其他编辑状态变化，后台只保留固定元数据。
- 发送前既有回执快照用于最多 60 秒的后续送达核对，不额外读取一次、不重发；超过期限仍未知则保持草稿。
- 新增设备端受限人工确认协议，原 Mac 菜单方式保留为兼容入口。Windows journal/服务同步受限实现和合成回归。
- .298 生产 LVGL 全套回归、恢复弹窗实际渲染及真实 sidecar 检查通过；构建及包校验通过。设备安装／提示验收结果接续记录。
- R46 两次用户发起定位正常，后台为 200、可信地址与标题唯一匹配。当前 Codex 对 AXManualAccessibility 返回属性不支持，不能声称这次成功证明历史根因已解决。
- 同期音乐封面保护及实测见 [封面重试记录](TAB5-MUSIC-COVER-RETRY-20261031.md)；导航原本可用，用户已纠正最初描述。

.298 草稿保护复验：用户反馈“未发送，电脑输入状态已变化”；后台 409 `composer_changed`，实际检查停在 `composerScanLimit`，并未提交。Mac 的主输入查找遍历整个历史消息树，长会话可超过 1,200 节点或 0.75 秒预算。本机已安装 Codex 26.1027.30824 的只读程序结构检查确认：历史正文容器 `[container-name:thread-content]`／`_transcriptContent_*` 与输入 footer 分离。R47 候选跳过这两个已知历史容器，保留可信 WebArea、完整输入区唯一性、精确会话、问答卡及附件检查；未知结构继续拒绝。合成回归覆盖 5,000 个历史节点仍能定位 footer、两个 footer 输入框仍拒绝及未知容器不跳过。

R47 真机复验：长会话中任务页追加成功、202 回执，用户确认该次此前已清空电脑草稿（不是保护失效）。随后重新输入电脑测试草稿，再从 TAB5 追加，后台 409 `desktop_draft_present`，用户确认提示“未发送：电脑已有草稿，请先处理”正确且两边文字完整保留。纯查询/设备确认送达恢复仍为离线回归已通过，尚无本轮真实待确认记录可进行设备确认实测，不能记为硬件验收通过。
