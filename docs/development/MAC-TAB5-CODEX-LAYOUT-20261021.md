# Codex 列表留白与语音回填续查 / List blank space and voice staging

## 2026-10-24 复测结果 / Retest result

当前 TAB5 `.267` 的任务页项目／分组切换复测，用户明确确认“列表正常，没有顶部大块留白”。本次实体显示验收通过，历史问题未复现；没有独立测量设备坐标或证明历史根因已消除。本次未修改列表代码、生成固件或刷写，下文保留此前失败、恢复与调查过程。继续核对快捷控制台同步提示的自动恢复，不能用列表正常代替该项结果。

The user confirms no large top gap after the requested project/group switching check on TAB5 `.267`. This physical display check passes; the historical symptom was not reproduced. Device coordinates were not independently measured and the historical root cause is not declared resolved. No list code, firmware build or device write was performed. Earlier failures and investigation remain below; automatic clearing of Quick Console's synchronization hint is checked separately.

## 历史调查 / Historical investigation

用户确认：进入任务页就出现前三四行空白，会话约从第五行开始；与下述负偏移测试图的位置基本一致。此反馈确认的是外观相似，尚未证明真机产生偏移的路径。保留已经通过的最近活动排序，以及七项日常开启偏好。

用户进一步纠正：Windows 没有该问题，早期 Mac 也正确，具体从哪个 Mac 构建开始不明。按这一对照优先排查 Mac 桥接回归，不将相似的负偏移图当成固件根因。固件继续不修改、不构建、不刷写。

## Mac 历史对照

- `0e07a26`：已连接桌面任务所有者，状态帧任务预算仍为 16／8／4／1。
- `c3d89bf`：明确请求 cli／vscode／appServer 来源及全部供应商，补入桌面创建的会话；没有坐标字段。
- `2da737e`：扩展目录游标查询与每项目八条分页；发送预算改为 64／48／32／16／8／4／1。这是待核对的行数／分页变化，尚非认定的首个故障版本。
- `09a15ca`、`441dc65`、`b204f0d`：分别修正排序／同名时间、未命名显示名、原生活动时间。后两项名称与顺序已有独立实机结果，不盲目回滚。

Windows 目录按项目挑选最多八条，再在预算内选取；Mac 按全局目录和预算生成分页。两端都未发送列表坐标。源归档 `ui.c` 的 SHA-256 为 `235c7facf41a291933c50a64aaacf2c4f20a4e810a77d014061038b7a4a4d8b9`，与归档编译输入清单匹配；没有据此推定当前异常时的每一帧已被捕获。

本地临时 A/B 候选 `artifacts/mac-tab5-20261021/codex-list-limit16/` 只在当前诊断候选的隔离源码副本内，将 `MacTab5Service` 发送预算恢复为 16／8／4／1；其余名称、活动时间、权限与协议路径保留。该限制未改入工作区，不作为正式修复。它可能暂时裁剪较旧任务，且设备已有滚动状态可能跨桥接重启保留；因此一次观察不能直接确定或排除该历史变更。构建、启动和实际反馈分开记录。

对照版 Release／严格签名／工具身份 probe 通过，全部 Swift 源文件与工作区逐一比较，仅上述预算一行不同；打包工具与 b204f0d 字节相同。正常退出 b204f0d 后，按精确路径启动该对照 App。首次界面访问超时，系统 SecurityAgent 进程曾出现；随后正常窗口确认 USB 数据、已认证 Wi-Fi、USB 优先 BLE 待命和天气恢复，不据此宣称免弹窗。七项日常功能均读回开启，桌面目录 31 项、操作已启用。已请求只看原来留白的项目，不要求重新录音或重启设备；实体对照结果等待反馈。18 个桌面测试体属于当前诊断源的独立执行，未将此临时预算变体宣称为通过完整 XCTest 或 CI。

用户随后明确反馈：16 项对照仍然在前面空三四行，任务仍从第五行附近开始。该对照没有改善，不能作为修复交付，也不能仅据此排除设备旧滚动状态跨桥接重启保留。已正常退出对照版并启动完整预算的 `codex-composer-diagnostic`，USB／Wi-Fi／BLE 与天气恢复。预算实验没有改入工作区。

继续准备隔离的 `codex-wire-diagnostic`：完整预算与既有协议数据保持原样，只在内存保留最近 16 个生成帧的数字摘要；匹配 USB 实际 ACK 序号后，显示帧字节数、任务数、空标题／非法 ID／多行标题数量、按 P1/P2 别名表示的项目任务全局索引，以及 ACK 的 uiPage 和既有固件版本。摘要不含任务 ID、项目名、标题、回复或凭据，不保存原始帧，不添加网络路由或协议字段。诊断服务源码单独保留在该本地候选目录；Release／严格签名／工具身份 probe 通过，工具字节不变。该步骤用于取得真实已确认帧证据，不是新的修复猜测。

该诊断版已正常启动。实际 USB ACK 匹配序号 17 的 21012 字节帧：31 个任务，空标题／非法 ID／多行标题各 0；七个项目页的任务数为 2／8／1／8／8／2／2，回报 `uiPage=8`、固件 `0.2.234-ui`。未导出任务名称、ID 或正文。按这份真实分组／顺序形状生成匿名夹具，使用冻结 `.234` 的筛选、紧凑填行及隐藏逻辑在原生 LVGL 回放，七页首行均为相对顶部 0，行间距 88；这不等于读取到设备实际坐标，也未重新核验设备运行镜像的精确 ELF。证据在 `artifacts/development/codex-list-layout-20261021/ack-frame-replay/`。

用户最终明确确认：**重启 TAB5 后位置恢复**，以此为准。桥接已恢复完整预算，七项偏好保留开启；没有刷机或修改固件。记录为本次显示恢复，不写成永久修复。原生指针交错另能重现：八行可滚动列表下拉时收缩至一行并隐藏，再返回后留下约 -95 像素负偏移；相同四行起点的控制夹具保持 0。这只是已证实的可触发路径，尚未证明是用户本次操作或某个 Mac 提交触发；四行分页未改入工作区，也没有据此再次要求修改固件。

列表恢复后继续处理原有语音回填待办。已请求仅针对既有“TAB5 操作验收”录一句测试并停止、不发送；识别期间不操作 Mac 界面，以免改变真实前台／焦点。停止后的具体输入框诊断待反馈，不能把旧导航成功状态当成回填成功。

## 列表验证与候选补丁

使用维护者 `.234` 对应源码归档（SHA-256 `c107e5f929e757a6342bea659f84e726a40d5f16b692441865f348fa26290890`）与官方 [LVGL 9.2.2](https://github.com/lvgl/lvgl/tree/v9.2.2)。下载的官方标签 tar.gz SHA-256 为 `129b4e00e06639fa79d7e8a6cab3c1ecce2445b1a246652ccd34f22e7b17ad6f`。依赖、提取的原始代码和生成图均留在忽略的 artifacts 中，不迁入桥接源码或发布包。

本地证据目录中的 `review-only/test_tab5_codex_list_layout.py` 从冻结 `ui.c` 提取实际列表、行、标题创建代码及 card/button/label 构造函数，使用原生 LVGL 执行几何断言。项目选择器、空提示和测试字体为简化夹具，不是完整界面模拟，也不验证设备缓存渲染、触摸、方向或刷新驱动。

- 新建单行原生列表首行相对容器顶部为 0，没有自然出现用户的下移。
- 八行列表滚动到 308 像素，再隐藏至一行，旧代码保留 `scroll_y=308`，首行在顶部以上 308 像素。这复现了独立的内容收缩后越界问题，不能据此解释用户的下移。
- 人为注入 `scroll_y=-260` 后，首行在容器下方 260 像素，上方留下大片空白。测试图与用户观察的位置基本一致，但注入状态不等于复现真实触发过程。
- 原创参考 helper 在项目变化时回到顶部，在行数变化／普通刷新时约束偏移至有效范围；有效滚动和同项目正常刷新不被清零。恢复负偏移、收缩、项目切换、空项目和同项目二十次刷新断言通过。

执行方法（LVGL 依赖应已解压）：

```bash
python3 artifacts/development/codex-list-layout-20261021/review-only/test_tab5_codex_list_layout.py \
  --firmware-source artifacts/mac-tab5-20261020/reference/AI-bot-tab5-0.2.234-ui-source.zip \
  --lvgl artifacts/development/codex-list-layout-20261021/lvgl-9.2.2 \
  --output artifacts/development/codex-list-layout-20261021/verified
```

证据位于上述 output：`verification.json`、`negative-offset-before.png`／`negative-offset-after.png`（由原生 PPM 转换，已查看），以及 `tab5-0.2.234-codex-list-scroll-review.patch`。脚本只在一次性目录核对补丁可应用性和应用后的精确字节，不修改归档或现有固件工程。没有构建镜像、改版本、更新 latest、刷机或更改展示设置。用户随后补充：Windows 正常、早期 Mac 也正常，要求先按 Mac 版本回归检查。这没有授权固件改动；候选 helper／脚本已移入本地忽略的 review-only 目录，仅留为调查证据，不进入工程改动或正式交付。不能将该补丁标为实体修复通过。

## 语音识别后主输入框未就绪

用户报告 Codex 直达的“豆包”语音识别结束后显示“电脑主输入框未就绪，识别文字保留在预览”。正常 b204f0d 设置页显示识别已结束、USB 查询 200，七项开关全部开启。失败处是后续 `stage-draft`，不是因开关关闭；仍需区分前台、焦点、编辑器标识／结构或精确地址，未确认本次具体失败项。

代码中发现可验证的诊断遗漏：`stage-draft` 读取输入框失败后返回 503，却没有用本次检查替换界面的旧导航成功状态。修正失败分支更新诊断，并把导航证据和对应状态一同保存，避免之后输入框失败显示旧的导航节点／页面匹配信息。协议、输入框身份限制和草稿／发送策略保持原样；这项诊断修复不表示输入框回填已可用。没有读取或发送真实预览文字，没有自动重试，也没有开启录音。

新增回归验证导航先成功、回填后失败时准确显示本次原因；重复同一请求不再次读／写／发送；后续精确地址失败也不附带旧导航证据。Release 构建通过。规定的 `swift test --package-path mac-app` 仍因本机缺少 XCTest 失败；另使用临时 precondition 断言适配器，直接编译全部生产源文件（排除 App 入口）并运行既有桌面测试文件中的 18 个测试体，全部通过。该独立执行使用注入的假后端，不是 XCTest、完整 Mac 测试、CI 或真机通过。证据位于 `artifacts/development/codex-list-layout-20261021/desktop-handler/`。

## English

The user reports three or four blank rows immediately upon entering the task page, with the task near the fifth row, and confirms visual similarity to the native negative-offset fixture. Native LVGL 9.2.2 executes constructors and list/row creation extracted from the exact maintained .234 archive. Picker/empty-label/font fixtures are simplified; device cache, orientation, touch and display-driver behavior are not covered. A fresh single row starts at zero. Shrinking eight scrolled rows to one retains positive 308px scroll and moves that row above the viewport, reproducing a separate real defect. An explicitly injected -260px scroll moves it down, matching the reported shape without proving the physical trigger. The original helper resets on project changes, bounds stale offsets, and preserves valid same-project scrolling. Native checks and before/after images pass. The review patch is verified only in a disposable copy and remains unapplied; no firmware image/version/latest change, flash or display-setting change occurred. The user then points out that Windows and early Mac versions work, directing investigation toward a Mac regression. No firmware authorization was given. The experimental helper/script now live only in ignored local review-only evidence; neither enters production changes.

History identifies `2da737e` as the task budget expansion from 16/8/4/1 to 64/48/32/16/8/4/1, together with catalog pagination; this is a comparison candidate, not a proven first failing commit. `c3d89bf` added desktop-source visibility, and later commits corrected names/recency with their own acceptance. Windows selects up to eight per saved project; Mac generates pages from the globally selected catalog. Neither transmits coordinates. The archived ui.c hash matches its compiled-input manifest, but no actual failing frame has been captured. A temporary isolated current-source Mac candidate restores only the old frame-budget ladder, retaining later naming/recency/permission paths. It may temporarily omit older tasks and cannot by itself reset retained device scroll state. The workspace budget and firmware remain unchanged; build, launch and physical comparison are tracked separately.

The temporary candidate passes Release, strict signing and helper identity probing; a file-by-file comparison finds only that one budget-line difference from the working sources. Its bundled helper is byte-identical to running b204f0d. After normal exit of b204f0d, the exact candidate started. Initial UI access timed out and SecurityAgent was briefly observed; the later normal window confirms recovered USB/Wi-Fi/BLE/weather, all seven preferences on, 31 catalog chats and mutation enabled. This does not establish a prompt-free startup. One physical list comparison is requested without recording or device reboot; feedback is pending. The 18 desktop test bodies validate the diagnostic sources separately, not full XCTest/CI for the temporary budget variant.

The user subsequently confirms that the 16-item comparison still leaves three or four blank rows. It did not improve the issue and is not a fix; retained device scroll state remains a limitation of the comparison. The experiment was normally exited and full-budget diagnostics restored with recovered transports/weather. A separate isolated wire-diagnostic candidate retains only numeric in-memory summaries for up to 16 generated frames. It displays counts/project-index aliases only after matching the exact real USB ACK sequence, together with uiPage and the existing firmware version. It stores no raw frames, task IDs, labels, replies or credentials and adds no route/protocol field. Release, strict signing and helper identity probing pass with unchanged helper bytes. This obtains acknowledged-frame evidence rather than proposing another speculative repair.

The wire diagnostic is running normally. Real ACK sequence 17 confirms a 21012-byte frame containing 31 tasks, zero blank/malformed/multiline entries, seven page sizes of 2/8/1/8/8/2/2, uiPage 8 and reported firmware 0.2.234-ui. An anonymized replay of that membership/order shape through the frozen native compact/filter/row logic starts all seven pages at relative y=0 with 88px row spacing. Actual device coordinates and current ELF identity are not measured. The user's latest explicit correction confirms that restarting TAB5 restored the position; that supersedes the earlier ambiguous restart response. Full budget and seven preferences remain enabled without firmware changes. This is recovery, not a permanent fix. A separate native eight-row pointer/shrink/hide interleaving retains approximately -95px scroll; its four-row control does not. Neither proves this user's trigger or a specific Mac regression; no four-row workaround entered production. The original voice issue continues with one designated acceptance-chat recording/stop-only request, avoiding Mac UI interaction during recognition; precise composer evidence remains pending.

The voice report reaches recognition completion and then fails desktop staging. The normal app reports completed recognition/USB query 200 and all seven feature grants enabled. This does not establish whether focus, foreground, editor identity/structure or exact-route checking failed. A concrete diagnostic omission retained the prior navigation success after staging returned 503. The Mac repair publishes the current failed read's reason and stores navigation evidence with its corresponding status. It preserves identity checks, receipts, text retention and explicit sending; it does not make the unsupported composer usable. Release passes; mandatory XCTest remains unavailable locally. All 18 existing desktop test bodies pass against actual production sources using a temporary precondition adapter and synthetic injected backends; this is separate from XCTest/full-suite/CI/hardware acceptance. No real preview was read/sent and no recording or automatic retry occurred.

## 本次识别结束后的精确诊断 / Current post-recognition diagnosis

用户再次实测：文字能识别并显示在 TAB5，但仍提示主输入框未就绪。停止后只读当前桥接设置页，本次原因明确为“未取得主输入框标识及精确会话地址”；识别成功和回填失败分开记录，不再要求重复同一诊断。

对照本项目 Windows 主编辑器检查与本机安装包的结构标记（仅检查静态资源，不导出界面内容）：当前包包含 `ProseMirror` 和 `_ComposerLayoutBody_5est5_2`。Mac 原实现只接受固定 AXIdentifier 与祖先精确 URL，尚未支持该主容器结构。新增候选路径要求：焦点是文本区，类 token 精确包含 ProseMirror；其祖先在同一个可信主 WebArea 内具有主容器，不能经过提问卡片、弹窗、其他输入框或嵌套 WebArea；该文档必须有精确任务地址，或匹配完整目录中唯一的完整标题（不用缩略名或侧栏行）；冲突的精确地址优先拒绝；最终复核前台、焦点和窗口。目录身份在检查后再次核对。写入仍只执行一次、保留原文、比较当前值并回读，发送仍需要独立明确操作。新增不可写／写入未确认诊断；没有键盘粘贴或通用文本框回退。此候选尚未实机验收。

The user's fresh physical test recognizes and previews text but still fails staging. Reading only the bridge settings after recognition now reveals missing editor identifier and exact route. Static installed assets contain ProseMirror and the main ComposerLayoutBody marker, matching the independently implemented Windows boundary. The new Mac candidate accepts this structure only for the focused text area inside its own trusted primary document, with an exact task route or a globally unique full catalog title. Question cards, dialogs, other input ancestors and nested documents are rejected; a conflicting live route cannot be rescued by a title. Compact/sidebar/open-only evidence is excluded. Catalog identity, foreground app, focused window and focused element are rechecked. Guarded single-write/readback and separate submission remain in place, with explicit read-only/unconfirmed-write diagnostics and no keyboard-paste fallback. Hardware acceptance is pending.

最终本地候选为 `artifacts/mac-tab5-20261021/codex-composer-structure-checked/AIBotBridge.app`。Release／严格签名／工具身份 probe／离线刷机工具自检通过，工具与 b204f0d 字节相同。`swift test` 仍因本机缺少 XCTest 失败；临时断言适配器编译全部生产源并执行桌面测试文件中的 20 个测试体通过（含结构身份、写入失败后状态更新、回执不重试、成功后清除旧失败状态、长度超限不写入），不是完整 XCTest／CI 或真机通过。旧诊断 App 已正常退出，新候选已按精确路径启动；CUA 首次选取新 App 超时，第二次成功，不声称免系统弹窗。USB 与已认证 Wi-Fi 正常、天气已更新、31 个近期会话已连接且操作启用；七项日常开关逐一读回开启。未执行真实录音、回填或发送，已请求用户仅录音／停止验证新候选；识别期间保持 Mac 界面不动。未修改固件或协议字段，无版本、标签、推送、Release 或部署设置变更。

The final local candidate is codex-composer-structure-checked. Release, strict signing, helper identity probe and offline flash-tool self-test pass; helper bytes match b204f0d. XCTest remains unavailable. All 20 desktop test bodies execute successfully against production sources using the temporary assertion adapter, including identity rejection, write-status recovery, no replayed writes and length rejection; this is not full XCTest/CI/hardware acceptance. The old diagnostic app exited normally and the exact new bundle is running. Initial CUA app selection timed out, then succeeded; prompt-free startup is not claimed. USB/authenticated Wi-Fi, updated weather, the 31-chat desktop connection and all seven opt-ins are confirmed. The agent did not record, stage or send real text; the user is asked for one stop-only test of this materially changed candidate, with no Mac UI interaction during recognition. Firmware/protocol/version/publishing/display settings remain unchanged.

## 实体已写入、首部空行及回读未确认 / Inserted text with leading blanks and unconfirmed readback

用户实测新结构候选：文字已经进入 Mac 主输入框，前面有两行空白；TAB5 提示电脑草稿正在变化／含引用并保留识别预览。只读桥接页确认本次为“主输入框写入未确认”，因此主编辑器识别与一次实际写入已有用户证据，不能写成完整回填成功。保留现有测试草稿，不自动重试或清除。

代码对照发现两个确定缺口：原实现将仅含换行的空框当非空，再添加分隔换行；写入后只立即回读一次且要求原始值完全相等，而现有 Windows 实现有有界只读核对与末尾 CR/LF 表示差异处理。本轮修正：只有全空白草稿作为空框，不添加前缀；非空草稿的全部内容保留。写入前仍比较精确原值，只调用一次系统写入，随后最多 12 次重新确认同一任务与主编辑器并读取，间隔 80ms，不重复写入。仅回读比较忽略末尾 CR/LF 的段落表示差异，不忽略首部／内部换行、尾部空格或正文差异；返回并加密保存实际读回的原文，发送时仍与该原始快照精确比较，不能仅凭归一化文本授权发送。清空失败后的恢复也使用实际确认的空框快照。真实回读的具体字节差异没有采集，不将时序或末尾换行中的某一项认定为唯一物理根因。

The user physically confirms insertion by the structural candidate, with two leading blank lines and the device's changed-draft/reference warning. The bridge reports unconfirmed write readback. This establishes editor identification and an actual insertion, not successful staging. Existing test text is preserved without retry or deletion. Code exposes two concrete gaps: whitespace-only values are treated as nonempty and receive another separator; readback is immediate and compares raw strings exactly. The repair replaces whitespace-only drafts directly, preserves all nonempty draft content, attempts one guarded write and performs at most twelve target-verified reads spaced 80ms apart. Only terminal CR/LF representation differs during reconciliation; leading/internal whitespace, trailing spaces and body differences remain significant. The actual observed raw value is returned and stored encrypted; explicit submission still requires an exact raw snapshot match. Rollback after a failed explicit send also uses the observed cleared snapshot. No actual value bytes were captured, so timing versus paragraph serialization is not claimed as the sole physical cause.

回读候选 `artifacts/mac-tab5-20261021/codex-composer-readback/AIBotBridge.app` 已正常启动，USB／已认证 Wi-Fi、天气、31 项桌面目录恢复，七项日常开关全为开启。Release／严格签名／工具身份 probe／离线工具自检通过，工具与基线字节相同。24 个桌面测试体独立执行通过；本次 `swift test` 仍因缺少 XCTest 失败，未宣称完整测试。新增四组验证全空白首填与正文保留、延迟／暂不可读后的末尾 CRLF 确认、首部／内部／尾空格／正文不同及持续不可读拒绝、实际原始快照发送绑定与回执不重放。已请求用户先手动清空上轮测试文字和空行，再只录音／停止，不发送；尚待反馈。没有自动访问或清除真实电脑草稿，没有固件、版本、发布或展示设置变更。

The readback candidate is now running with recovered USB/authenticated Wi-Fi/weather, 31 chats and all seven opt-ins. Release, strict signing, helper probing and offline tool self-test pass; helper bytes are unchanged. All 24 desktop test bodies pass via the independent adapter; the mandatory XCTest attempt still fails because XCTest is unavailable. Four new groups cover blank initial drafts versus preserved body text, delayed/unavailable reads and terminal CRLF, rejection of meaningful whitespace/body differences or persistent unavailability, and raw-snapshot submission/replay protection. The user is asked to manually remove only the previous test draft and repeat recording/stop without sending; hardware feedback is pending. No real draft was autonomously read/cleared, and firmware/version/publishing/display settings remain unchanged.

## 本轮用户实机验收通过 / Physical acceptance confirmed

用户按要求清空上一轮测试文字和空行，在“TAB5 操作验收”重新确认目标并录音／停止后回复“这一次全部OK了”。据此确认本轮 `codex-composer-readback` 候选的识别文字回填、首部空行修正及回填提示通过一次实机验收；不外推为真实发送／中断、长时间稳定性或所有通道分别通过。当前候选继续运行，七项日常开关保留开启。任务列表的重启恢复仍只记为恢复，尚无永久修复结论。没有修改／刷写固件，没有提交、推送、标签或发布。

The user repeats the designated chat's recording/stop-only check after manually clearing the prior test text and reports that everything is now OK. This confirms one physical acceptance of recognized-text staging, removal of leading blank lines and correct staging feedback on the codex-composer-readback candidate. It does not establish real submission/interruption, long-term stability or separate coverage of every transport. The current candidate stays running with all seven opt-ins preserved. The task-list position remains recorded as recovered after reboot, not permanently fixed. No firmware change/flash, commit, push, tag or release occurred.

## 连续语音与清空通过 / Continued dictation and clearing accepted

用户随后明确确认：快捷控制台连续语音、清空均已测试通过，发送尚未操作。更新这两项为用户实机通过，保留快捷控制台蓝色按钮发送待验；不把普通任务页此前发送／停止通过当作快捷控制台发送通过。当前候选及七项日常开关保持。

The user explicitly confirms successful physical tests of continued Quick Console dictation and clearing, and states that submission has not been attempted. Both are now accepted; the blue-button Quick Console submission remains pending, separate from previously accepted task-page send/stop. The current candidate and all seven opt-ins are preserved.

## 快捷发送失败与分支诊断 / Quick submission failure and diagnostics

用户点击一次蓝色按钮后报“未确认发送，请检查电脑，不要重复发送，可重新录音”，进一步确认电脑测试文字仍在、没有新回复。专用只读接口核对指定验收任务没有新增轮次，当前没有成功送达证据，不自动重发。桥接 UI 仍是上次主编辑器确认，暴露 submit-draft 未发布失败分支的诊断遗漏。新增有限枚举区分回填记录缺失／绑定不同、已消费、过期、编辑器不可读、末尾 CR/LF 表示变化、其他正文变化、桌面只读、任务拒绝或结果未知；没有输出 ID／正文／原始回执，原有精确快照、先消费、发送去重和协议字段不变。新增回归对所有前置拒绝断言零额外写入／零发送，正常提交状态单独确认。此为诊断补齐，尚未确定本次具体发送根因，不将其宣称发送修复。期间用户新增天气地名需求，已并行完成相关源码与模拟验证，准备合并一次本地候选切换。

The single blue-button attempt returned unconfirmed submission; the user confirms the text remains with no reply. The dedicated read-only test-thread inspection finds no new turn, so no delivery success or automatic resend is assumed. The bridge still showed the older composer confirmation: submit failures were not publishing their guard reason. New finite diagnostics distinguish missing/mismatched, consumed/expired staging records, unreadable editor, terminal-paragraph representation changes versus body edits, read-only connection, rejection and unknown outcome. No IDs/text/raw receipts are exposed; exact snapshot comparison, consume-before-effects, deduplication and wire fields are unchanged. Regression coverage asserts zero additional writes/sends for guard failures and separately verifies successful submission status. This diagnoses rather than fixes the unproven physical cause. The additional weather-name request is implemented/tested in parallel for one combined local candidate transition.

最终候选准备于 `artifacts/mac-tab5-20261021/codex-submit-place-final/AIBotBridge.app`，额外同步了天气设置中“系统地名服务和所选天气服务”的实际用途说明。Release 与严格签名通过，27 个联合测试体的独立执行通过；本机完整 XCTest 仍缺模块。尚未启动：检查旧候选时发现用户打开天气／股票设置的模态编辑窗口，为保留可能未保存输入，已请用户自行保存或取消后告知，未操作其中字段或丢弃设置。天气具体地名和新的发送分支诊断都尚无该候选实体结果；旧 readback 候选仍运行，七项开关沿用开启，草稿未改动，固件不变。

The final candidate is prepared at codex-submit-place-final, including an accurate settings notice for system place-name and weather services. Release/strict signing and 27 independent combined test bodies pass; full local XCTest remains unavailable. It has not launched: the old app has the user's modal weather/stock editor open, so the user is asked to save/cancel their edits first. No fields were changed or edits discarded. Neither concrete place-name display nor new submission-guard diagnostics has physical results on this candidate; the previous readback app remains active with seven opt-ins and drafts preserved, without firmware changes.

用户确认已自行关闭设置窗口后，旧 readback 候选已正常退出，精确 `codex-submit-place-final` 已启动。首次 CUA 选取超时，第二次成功；正常 App 显示 USB 数据已确认、已认证 Wi-Fi 请求、和风当前／24 小时／7 天天气已更新，以及 31 个已连接桌面会话和操作启用。七项日常开关逐一读回开启。没有更改城市／供应商／股票字段或设备展示设置，没有刷机。已请求只观察天气首页的地名是否正确，尚待实际反馈；不记录具体地名。快捷发送根因和修复仍待新的分支诊断。

After the user closed their settings editor, the old readback app exited normally and the exact codex-submit-place-final bundle started. Initial CUA selection timed out, then succeeded. Its normal UI confirms USB data, authenticated Wi-Fi, updated QWeather current/24-hour/seven-day data, 31 connected chats and enabled operations, with all seven opt-ins verified on. City/provider/stock/display settings and firmware were not changed. A physical weather-label check is requested without collecting the actual place name; feedback remains pending. Quick submission still awaits the new guard diagnostic and a proven repair.

## 快捷发送通过，切换会话后的失败待复核 / Submission accepted, switching-related failure pending

用户确认本次快捷发送成功，并补充只要中间切换过 Session，好像就发送不成功。专用只读接口在指定验收任务核对到一轮新增、已完成的用户消息及公开回复（约 5.8 秒），此前基线为旧中断轮次；本轮单次快捷发送与实际到达通过。识别内容与预设口令不是逐字一致，不据此验收识别准确率或固定回复文本。切换后的失败独立保留待定位，已询问是在 Mac 侧切走／返回，还是 TAB5 箭头切换后重新录音或发送旧草稿；尚不判定是快照表示、焦点／目标检查或回填票据失效。没有自动重发或触发其他任务。

The user confirms successful Quick Console submission on this run but reports apparent failures whenever a Session was switched in between. The dedicated read-only test-thread inspection confirms one new completed user turn and public response, approximately 5.8 seconds, versus the previous interrupted baseline. Single submission/delivery is accepted; transcription did not exactly match the proposed phrase, so exact recognition/reply wording is not accepted by inference. The switching-related failure remains a separate open issue. Clarification is requested for Mac switch-away/return versus device arrows with a fresh recording or an old draft; snapshot serialization, focus/target checks and ticket invalidation are not yet attributed. No automatic resend or other-task action occurred.

用户随后再次实测并回复：“切换过会话之后，它又成功了。”据用户结果，切换后发送也通过一次；“切换必然失败”没有得到稳定复现。当前语音回填、空行与提示、连续语音、清空、单次快捷发送和切换后发送均已有成功反馈。早先未确认发送及草稿保留的失败记录继续保留为偶发待观察，根因尚未确定，不将诊断补齐说成已修复该偶发路径；没有据此放宽任务／草稿校验或要求继续反复发送。当前候选与七项偏好保持。

The user repeats the test and reports success after switching chats as well. This accepts one switching/submission run; switching does not reproducibly cause failure. Staging, leading-line/feedback repair, continued dictation, clearing, single submission and submission after switching now have successful physical feedback. The earlier unconfirmed submission with retained draft remains an intermittent observation of unproven cause; diagnostic coverage is not claimed to fix that path. No identity/snapshot restriction was relaxed and no further repeated sending was requested. The current candidate and seven preferences remain enabled.

专用只读接口同时确认指定验收会话又出现了比前一次成功更新的已完成轮次，为本次发送提供独立送达证据；未向其他任务发送或检查其正文。 / The dedicated read-only interface also confirms another newer completed turn in the designated acceptance chat, independently corroborating delivery. No other chat was messaged or its contents inspected.
