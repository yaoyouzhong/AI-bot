# Mac TAB5 操作与剩余数据接续

用户要求继续开发完整 Codex 操作、Codex 直达，验收一键锁屏，并报告桌宠、系统诊断及日历缺失。继续仅在测试分支开发／CI／临时候选验收，不合并 main、不部署服务、不发布、不刷机。日常 Codex 只读已获持续授权，应保留；真实发送／追加／中断仍仅针对用户明确指定的测试任务，不能自动批准任务中的权限请求。

## 已取得的证据

- 桌面公共代理 socket 仍缺失。但本机 Codex 桌面进程持有现有 `~/.codex/ipc/ipc.sock`；已验证当前用户所有权与目录／socket 权限，通过只读握手、当前任务持有者发现、订阅状态快照及退订关闭。协议为四字节小端长度和 JSON；初始化版本 0，发现／订阅版本 1，状态快照版本 11。只记录能力和字段名称，没有发送、追加、中断、改输入框或部署服务。
- 该 IPC 属于桌面版本相关接口，不能当作稳定公共 API。后续实现须限制精确本机持有者、版本、任务身份、运行状态与请求幂等，遇不兼容即拒绝写入；不得在独立 stdio 子进程中恢复桌面活跃任务来假冒同一会话控制。现有公开 App Server 只读历史路径继续保留。
- 实际设备源码 `ui_runtime_health.h` 读取 `runtimeHealth`；Mac 当前没有此对象，所以诊断页整体缺少数据。后续应输出实测可用项；未知的 VPN、队列、磁盘延迟不能填写正常或零。
- 本机 Claude／Codex 的 selected/default APET 四个文件均缺失；不是已证实的设备传输失败。图库选择和授权图片导入实现存在，但默认桌宠没有自动来源。不得导入来源不明的旧个人素材；已有项目可选素材的许可与用户选择应独立核对。
- 日历缺少 `calendar.detail` 已在 `29f1654` 补齐现有 2026 年事实表。新候选验证和实体结果见日常数据记录；不能将源码修复记成实机通过。

上述完整控制、直达、锁屏、桌宠和诊断仍未完成新一轮实机验收，不因既往只读、音量或音乐通过而标为通过。

## 桌面连接实现与实际限制

新增独立 Swift 桌面 IPC 连接层：仅连接用户目录现有 socket，检查目录／socket 所有权和权限，通过内核对端 UID／PID 与运行中的 Codex bundle 身份核对；不创建 socket，不部署服务。协议帧有长度、数量及单调时钟期限限制。握手、任务持有者与版本 11 状态必须匹配，短连接退出前退订。桌面快照只抽取状态和当前轮次，公开历史仍走只读 App Server；不将原始桌面快照、思考或工具输出传给设备。

发送与中断经过第二开关、近期目录、当前状态和持久回执校验；生产路径不再通过独立子进程 `thread/resume`。发送继承现有任务设置并携带固定请求身份，中断绑定 `expectedTurnId`。写入后的不确定错误保持未知，不自动重发。只读方式核对实际桌面回执发现 `method` 位于外层，模拟夹具已按真实层级校正。实际 Swift 对象连接当前桌面，读取真实 active/inProgress 状态成功；只读模型设置请求也返回精确持有者回执。两者均没有调用模型或改变任务。

**追加尚不能开放：** 当前桌面 `thread-follower-steer-turn` v1 不接收调用者指定的预期轮次，内部选择运行中的轮次。单次预检查无法消除切换轮次的竞争，因此本实现拒绝该操作且设备 `canSteer=false`。不能将支持发送／中断误称为完整操作已通过。今后须有支持原子预期轮次校验的入口，才可开放追加；本轮未部署共享服务或修改 Codex 安装。

回归覆盖精确持有者、版本不兼容、订阅退出、策略继承、等待审批时精确中断、旧追加接口拒绝、未知回执不重发、原始内容隔离和桥接第二开关。本机 XCTest 仍无法加载，完整 CI 另行记录。直达输入框的真实身份核对、用户指定任务的真实写入和一键锁屏仍待验收。

The native Swift transport connects only to the existing owner-checked Unix socket, verifies kernel peer UID/PID against the running Codex app, and bounds frames, messages and deadlines. It subscribes to an exact owner/version and closes cleanly; public history remains on the read-only App Server. Production start/interrupt use the desktop owner with policy inheritance, explicit opt-in and durable uncertain receipts. Native read-only probes verified actual runtime state and acknowledgement structure without changing a task. Installed steer v1 lacks a caller-bound expected turn, so it fails closed and is advertised unavailable; full controls are not claimed complete. Tests cover routing, incompatibility, privacy, opt-in and delivery uncertainty; actual designated-task mutations, composer identity and lock acceptance remain pending.

### 自动验证与现场停点

程序 `0e07a26e227fd89999dde495478613ec01319057` 的 [Mac CI 39441248398](https://github.com/yaoyouzhong/AI-bot/actions/runs/39441248398) 与完整下载附件核验通过：200 项测试、0 失败／跳过，Release、App、工具、8 张原生界面 PNG 和源码／包哈希一致。本机 Release 与临时候选签名通过，路径 `artifacts/mac-tab5-20261020/0e07a26/AIBotBridge.app`。尚未启动此候选、开启写入开关或执行真实任务写入。

目前运行的仍是日历修复候选 `29f1654`。用户完成钥匙串授权后，自动确认 USB 数据已确认、Wi-Fi 收到已认证请求、BLE 已认证／USB 优先待命，随后已询问 2026 年 10 月日历“休／班”标记及待更新提示消失情况；截至本记录尚无实体反馈。后续界面核对被 CUA 报告的 Mac 锁屏阻挡，已请求用户手动解锁，没有执行自动解锁，也没有把此次锁屏当成锁屏功能测试。解锁后先完成日历与原有开关／天气状态核对，再按新候选授权流程和用户指定测试任务推进。

Candidate `0e07a26` passed CI 39441248398 and full artifact verification (200 tests, zero failures/skips; Release/app/tools/native UI/hashes), plus local Release/signing. It has not been launched or enabled for mutations. Running calendar candidate `29f1654` recovered USB/Wi-Fi/BLE after user Keychain authorization; physical October calendar checks remain unanswered. CUA subsequently reported a locked Mac, so user unlock was requested. No automatic unlock or lock-feature acceptance occurred. Resume existing-state/calendar verification before the next candidate and specifically authorized test-task mutations.

## English

The user requested full Codex controls, direct navigation, lock acceptance and missing pet/diagnostic/calendar data. Preserve daily read-only authorization and all no-merge/deploy/release/flash boundaries. Real task mutations require the user's specifically designated test task; never approve its permission requests automatically.

A read-only probe of the existing, owner-checked desktop IPC socket succeeded at initialization, exact current-task owner discovery, snapshot subscription and unsubscribe. No mutation or service deployment occurred, and no task contents were retained. This is version-specific desktop IPC, not a stable public API: implementation must validate owner/version/identity/state and fail closed, retaining public read-only history rather than resuming desktop tasks in an independent process. The device diagnostics consumer expects the currently missing `runtimeHealth`; unavailable measurements must remain unknown. All four local selected/default pet files are absent, which does not establish a transport failure. Calendar annual detail was added in `29f1654`, with hardware verification tracked separately. Full controls, direct navigation, lock, pets and diagnostics remain pending.

## 系统诊断数据补齐

在等待用户手动解锁期间，新增 Mac `runtimeHealth` 生产端，沿用实际 `.234` 消费字段。先提供已采集 CPU／内存和桥接采样检查。CPU ≥90%、内存占用 ≥92% 持续有效采样 30 秒才确认警告，低于阈值持续 10 秒才解除；重复帧不累计观察时间，采样断档重新观察。告警会话／序号仅在新警告确认时更新。内存占用不是 Apple 的系统内存压力等级，文字明确区分。

CPU／VM 独立采样时间只保留在内存模型，不加入既有 `systemMetrics` JSON；较新的网络发布不能刷新旧 CPU／内存的观察时间，设备依据真实 `epochUtc` 判断监控是否过期。没有可用压力采样时不伪造监控。实际未采集的 VPN／PAC 路由、处理器队列、磁盘延迟保持 `unknown`；没有延迟历史点，不填零、不发探测请求、不读取代理 URL 或凭证。本机 Codex 持有者只证明本机连接，不标记模型服务出口正常。上述未采集项仍是功能缺口，不算完整诊断验收完成。

回归包含既有 JSON 契约、未知项、重复帧、持续确认／恢复、断档和网络续期边界。完整 CI 与候选实体诊断另行记录；此前日历候选仍是现场运行版本。

While UI access is blocked by the locked Mac, the Mac producer now supplies the existing runtimeHealth envelope with measured CPU/memory and sampling checks. High occupancy requires 30 seconds of actual observations; recovery requires 10 seconds, and gaps reset observation. Repeated frames do not manufacture samples or new alarms. CPU/VM freshness is internal-only and cannot be renewed by network publication. Unsupported VPN/PAC routing, queue and disk counters remain unknown, with no fabricated latency points or extra probe requests. Desktop ownership is not model connectivity. Those capabilities and physical diagnostic acceptance remain incomplete.

## 桌宠默认资源补齐

进一步对照 Windows 调用方确认：没有用户动画时，Windows TAB5 资源层会生成项目自有的 BYTE SPROUT 默认动画；Mac 仅在本机镜像视图绘制同一自有几何图形，资源层把空选择变为无资源，导致设备端空白。现将 Mac 已有绘图提取为共享函数，在内存生成两个 112×112、240 ms 动画帧，仍通过既有 RLE565 分块与 CRC 回执同步。已有自选／导入动画优先；临时读不到选择时保留上次成功资源，不删除或覆盖用户文件，不导入旧个人素材，不添加图片二进制到发布包。

实际编译对象已生成原生预览并执行资源确认模拟。首次预览发现重复翻转导致倒置，已修正并加入天线、眼睛、脚步坐标断言以及两帧差异回归；两帧最终预览已逐张核对为正向、脚步不同；原生资源回执模拟通过。完整 CI 待验证。新增测试覆盖空配置默认同步、不落地选择文件、用户选择优先和缺失／损坏读取保留。以上均不等于设备桌宠实际显示与动画验收。

Windows already sends the project's geometric BYTE SPROUT fallback when no user animation is selected. Mac drew it only in its local preview and omitted device assets. The shared original Mac drawing now produces two in-memory 112×112 frames with 240 ms delays through the existing RLE565/CRC transport. User artwork takes priority and last-good assets survive unavailable selections; no private files or external artwork are imported or bundled. Native preview caught and corrected a duplicate row inversion, with pixel/orientation, frame, acknowledgement and selection regressions added. Both final decoded RGB565 previews were inspected upright with different footsteps; native resource acknowledgement passed. Full CI and physical animation acceptance remain pending.

### 诊断／桌宠自动验证结果与待恢复现场

`c9bc5e2` 首轮 CI 中，新诊断／桌宠测试通过，但旧资源重组用例仍假设只有一个槽位，错误拼接了默认桌宠和自选桌宠的交替分块。`c8a254fcfed916efaac0c977b6c2fae635275383` 将该用例改为按槽位独立重组、逐个确认，并核验无效回执不撤销有效确认；未通过删掉断言或停用默认资源回避失败。

[Mac CI 39445293598](https://github.com/yaoyouzhong/AI-bot/actions/runs/39445293598) 通过，完整附件下载核验通过：**209 项测试，0 失败、0 跳过**，Release／签名 App／随包工具／8 张原生界面图／源码 SHA 与包哈希一致。本机 `swift test` 仍因缺少 XCTest 无法执行；本机 Release、App 构建和签名通过，候选为 `artifacts/mac-tab5-20261020/c8a254f/AIBotBridge.app`，尚未启动。原生 CPU／VM 新鲜度检查和最终 RGB565 两帧预览／回执模拟通过，均不替代实体验收。

现场仍为 `29f1654` 日历候选；日历实体反馈与用户手动解锁尚待回复。新候选包含已实现的 Codex 桌面发送／中断连接、日历、诊断子集和默认桌宠。解锁后先核对当前日历、原有只读开关与和风自动定位；再切换通过 CI 的新候选，依次验收桌宠和诊断，随后在用户明确指定的测试任务上验收 Codex 操作／直达，以及单独的一键锁屏。追加接口限制与未采集的诊断项仍按上文列为缺口。未合并 main、未部署服务、未发布、未刷机。

CI 39445293598 for `c8a254fcfed916efaac0c977b6c2fae635275383` and the downloaded full artifacts passed: **209 tests, zero failures/skips**, matching source/package hashes, Release, signed app, tools and eight native UI fixtures. The first run exposed an old single-slot reassembly assumption; the test now reconstructs and acknowledges both pet slots independently. Local XCTest remains unavailable; local Release/bundle/signing and native observation/decoded-preview checks passed. Candidate `artifacts/mac-tab5-20261020/c8a254f/AIBotBridge.app` is not launched. The running calendar candidate remains `29f1654`; physical calendar feedback and manual unlock remain pending. No task mutation, lock acceptance, deployment, release or flashing is claimed. Resume preserved settings, physical pet/diagnostic checks and specifically designated-task controls only after the user unlocks.

## 2026-10-21 接续验收

用户手动解锁后，旧候选已不在运行，启动通过 CI 的 `c8a254f`。用户报告该构建仍需连续 4 次钥匙串密码授权；代码中 LAN、TAB5 配对、私有草稿、DeepSeek 和按 Host 保存的和风凭据是独立钥匙串项目，临时签名随构建改变，但没有足够证据将每个弹窗精确对应到某项。不能把重复授权问题写成已解决；没有读取密码、调整 ACL 或允许所有应用访问。

- 自动确认 USB 数据已确认、Wi-Fi 收到已认证请求、日常 Codex 只读保持开启。音乐／语音／电脑控制初始均关闭。蓝牙持续寻找，系统侧设备未连接，本次恢复尚未通过。
- **日历通过：** 用户确认 2026 年 10 月 1–7 日“休”、10 日“班”及选中日期不再待更新均正常。
- **桌宠部分通过：** 用户已看到图案，但与 Windows 原来的两只不一致。当前为 Mac 原创默认动画，本机无 Windows 自选 APET；Windows 优先选择用户缓存／迁入动画。仅通过资源显示，外观一致性和原选择迁移待处理，不能用默认图案代替验收。
- **诊断不完整：** 用户确认 CPU／内存／上下载速率正常，诊断 VPN、PAC、CPU 等待、磁盘响应仍待确认／无数据，与当前未采集的实现一致。只读检查确认本机 IOBlockStorageDriver 提供 I/O 次数与累计时间字段，尚未实现或验证其单位与增量延迟，不能据此填入数值。
- 系统定位总开关开、AI-bot Bridge 开关关；恢复此前用户明确授权的同范围定位访问后，按原值保存触发刷新，Mac 显示“和风天气 已更新 · 24 小时 / 7 天”。未改变来源、密钥或城市。

用户明确授权新建“TAB5 操作验收”任务和临时启用发送／中断、桌面输入框，结束后关闭。任务已新建并回复就绪；这只是工具创建任务成功，**不算桥接发送通过**。两个临时开关曾正常开启；发现任务列表问题后已关闭，日常只读保持开启。真实桥接发送／中断／直达和一键锁屏均未执行。

### 会话来源筛选问题

用户反馈 AI-bot 分组只显示当前会话，另有 Workspace／Work。自动只读检查证实本机接口与桌面项目清单中的 AI-bot 均只有该一条，其余本机会话分属其他目录，远端任务不属于该本机接口。包含 16 个任务、缓存天气、64 个网络样本、日历和诊断的实际对象帧约 16.2 KiB，未触发 32 KiB 裁剪；不将项目筛选误判为数据帧容量故障。

同时复现一个实际遗漏：默认 `thread/list` 返回 29 条，未包含刚创建的验收任务；显式包含 `appServer` 来源后返回 30 条且包含该任务。本机已生成的官方协议 schema 说明 omitted/empty sourceKinds 默认仅交互来源；现明确请求 `cli/vscode/appServer`，并以空 modelProviders 请求当前用户各供应商历史。仍排除归档／子代理来源，仍只读取公开历史，未创建服务、修改模型或审批策略。新增回归覆盖来源、供应商筛选、旧历史保留与项目分组；构建与新候选实机结果另行记录。

On October 21 the user unlocked the Mac; the prior app was no longer running. Candidate c8a254f started after four reported Keychain prompts, which remain unresolved across temporary signatures/items. USB/Wi-Fi and daily read-only Codex recovered. Physical October holiday/workday labels passed. Pets now appear but differ from the user's Windows selections, so appearance parity remains pending. CPU/memory/network metrics work; VPN/PAC/queue/disk diagnostics remain unimplemented. Restoring the previously authorized location switch and refreshing unchanged settings recovered QWeather current/hourly/daily data. Bluetooth remains searching.

The user authorized a separate temporary acceptance chat and two temporary control switches; the chat returned ready. This is not bridge-send acceptance. Both switches were closed again while investigating the catalog, preserving daily read-only access. Live bridge mutations/direct navigation/lock are not yet tested. Read-only comparison found one local AI-bot-group chat, other cwd groups and remote chats outside the local interface. A 16-task status frame fits the byte budget. A separate real omission was reproduced: thread/list's default source filter excluded the desktop-created appServer chat. Explicit cli/vscode/appServer sources restore it, with all-provider history and existing opt-in/privacy controls retained. Regression and candidate verification follow separately.

### 会话筛选修复验证

`c3d89bfee62743bf90e060692048cf72efc62aee` 的 [Mac CI 39528862242](https://github.com/yaoyouzhong/AI-bot/actions/runs/39528862242) 与下载后的完整包核验通过：210 项测试，0 失败／跳过，Release、签名 App、工具及原生界面固定样本一致。本机 Release、候选包和严格签名检查通过；本机 XCTest 仍不可用。实际只读目录探针确认新筛选返回 30 条并包含新建验收任务，16 任务状态帧为 16,668 字节，未超上限。

候选位于 `artifacts/mac-tab5-20261021/c3d89bf/AIBotBridge.app`。切换时界面工具发送快捷键及菜单退出后，旧 `c8a254f` 进程仍在运行；原因尚未确认，已请求用户正常退出，没有强杀或同时运行第二个桥接。新候选启动与实体任务列表、发送／中断／直达验收仍待完成。Computer Use 明确拒绝操作 Codex 应用本身，未改用其他界面自动化绕过；任务状态仍可通过专用任务接口只读核对，桌面直达效果需要实体观察。

CI 39528862242 and full downloaded artifact verification passed for c3d89bf: 210 tests, zero failures/skips, matching Release/app/tools/native UI fixtures. Local Release, bundle and strict signature checks passed; local XCTest remains unavailable. The real read-only catalog probe returned 30 chats including the desktop-created acceptance chat; the 16-task status frame was 16,668 bytes. The candidate is ready but not yet launched: UI quit attempts left c8a254f running, so normal user exit was requested rather than force-killing it or starting a competing bridge. Hardware catalog/control acceptance remains pending. Computer Use explicitly rejected operating the Codex app; no alternative UI automation bypass was attempted. Dedicated task APIs can verify state, while desktop navigation needs human observation.

### 修复候选启动与目录实体结果

接续确认旧进程仍在运行。系统原生调用栈采样显示 USB 的 Codex 读取等待 `MacTab5Codex` 门锁，后台刷新正等待桌面 IPC；这与进行中操作阻止退出的保护一致，但未证明每次退出失败均为同一原因。暂时关闭只读后，菜单正常退出成功，确认旧进程已消失，未强杀。启动 `c3d89bf`，用户完成钥匙串授权；自动确认 USB 数据与 Wi-Fi 认证、30 个近期会话。日常只读恢复开启，本次已授权的发送／中断及桌面控制开关临时开启。

用户确认新分组为“Tab5 Operation Acceptance”，其中“TAB5 操作验收”与就绪回复均可见，桌面新建会话目录修复实体通过。此前预期的 outputs 是错误的界面名称，已纠正。自动定位开关再次为关，恢复同范围既有授权并按原值刷新后，和风当前／24 小时／7 天均已更新；蓝牙仍寻找设备，未通过恢复。

第一次尝试输入测试消息时，用户使用语音遇到“连接中断”。Mac 诊断确认开始请求为 USB 403，而语音开关仍为上一轮收尾后的关闭状态；测试任务没有新增轮次。恢复此前已授权的本地语音开关后，显示已启用、等待收音，继续单次录入和发送验收。不能将这次拒绝计为发送失败或把恢复开关计为完整语音通过。

After the user reported exit, the old process still ran. Native stack sampling found USB Codex reads waiting on the refresh gate while desktop IPC was pending, consistent with the active-operation exit guard but not proof of every failed quit. Temporarily disabling read-only allowed normal menu exit; the old process disappeared without force termination. Candidate c3d89bf started after user Keychain authorization, recovering USB/Wi-Fi and 30 chats. Daily read-only and the authorized temporary control switches were restored. The user physically found the acceptance group/chat and ready reply, passing the catalog fix. Restoring existing location access and refreshing unchanged settings recovered QWeather; Bluetooth still searches. A first voice-input attempt returned USB 403 because the prior cleanup had left voice disabled; no task turn was sent. Local voice was re-enabled for the next single attempt; sending and recording remain separately unverified.

### 发送／停止通过与空回复、目录容量修复

恢复语音后，用户从实体 TAB5 向指定任务发送一次验收文字。专用只读任务接口核对仅新增一轮，约 4.8 秒完成并返回“发送验收通过”；语音查询为 USB 200。随后助手仅向同一授权任务发送短暂等待指令，以供设备停止验收。首轮因入口说明错误而自然结束，不计通过；确认实际按钮为“查看回复”右上角“停止任务”后，第二轮用户明确点过该按钮，Mac 任务状态为 `interrupted`，约 4.9 秒结束。实体发送与停止通过；没有操作其他真实任务。发送／桌面输入框／语音临时开关随后关闭，日常只读保持开启。

停止后用户报告“会话动态 --／正在读取会话动态…”，退出重进仍一样；后续“能看到”不代表恢复，用户再次明确正文为空。实际生产读取探针返回 200、interrupted，但公开文字长度为零。对照现有 `.234` 消费者，空 `text` 会显示加载提示，内联回复缺少 `receipt` 会覆盖状态为 `--`。Mac 修复在无公开文字时提供明确状态提示，并保留 `hasText=false`、无阅读回执／未读通知；RPC 与状态帧使用同一状态文字。没有修改固件，也没有把占位提示伪装成助手回复。

用户同时澄清缺少的项目来自 Windows，Mac 实际只有 AI-bot／Work 两个登记目录；无需跨电脑同步。但 Mac 之前最多投影 16 条，而设备每组仅显示 8 行，确实会隐藏较早会话。目录现在遍历有界游标（最多 512 条），状态帧优先 64 条并保持既有 32 KiB 收缩保护，按每组 8 条生成编号分组页，遵守最多 24 项选择器限制并保留正在阅读／工作的任务。当前 30 条会话自动核对为 7 个可见分组页，完整数据帧约 19.3 KiB，均可被设备既有行数容纳；不宣称无限历史。新增回归覆盖空终态、延迟公开文字、内联状态、无伪造未读、游标去重／失败保留、8 行可见性和选择器保留任务。新候选 CI 和实体复验待完成。

After voice was enabled, the device sent one acceptance message; the task API verified one new completed turn and the expected reply in about 4.8 seconds. A separate tool-started wait was used only to test device interruption. The first wait expired naturally and does not count. The user then clicked the actual “停止任务” button in the reply view; the second turn became interrupted in about 4.9 seconds. Device send/stop passed. Temporary send/composer/voice switches were closed, preserving daily read-only.

The reply view remained blank/loading afterward, including after reopening. A production read probe returned 200/interrupted with zero public-text bytes. Existing firmware treats empty text as loading and a missing inline receipt as “--”. The Mac repair supplies explicit presentation text and matching receipt, retaining hasText=false and no read/unread acknowledgement for placeholders. No firmware change occurs. The user clarified missing projects were on Windows, but local history was genuinely truncated by the 16-task frame and eight-row picker. Bounded catalog cursors, up to 64 tasks per frame with existing byte-budget fallback, and eight-task numbered picker pages now expose all 30 current local chats across seven pages in roughly 19.3 KiB. The 512-catalog/24-picker limits remain explicit, not unlimited history. Regressions cover empty states, later real text, inline receipts, privacy, cursor duplication/failure retention and visible task/pin limits. New CI and physical display acceptance remain pending.

### 05f09b9 自动验证与实体复验

首轮 CI 在新增测试夹具的异构字典推断处编译失败；`05f09b936a649e3bdbca8b8540ce9bd48ac14321` 补充显式类型，未删除断言或修改运行逻辑。[Mac CI 39532411346](https://github.com/yaoyouzhong/AI-bot/actions/runs/39532411346) 及下载后的完整附件核验通过：213 项测试、0 失败／跳过，Release、App、随包工具、原生固定界面和源码／包哈希一致。本机 Release、候选包与严格签名核验通过；本机 XCTest 仍不可用。

旧候选正常退出后启动 `artifacts/mac-tab5-20261021/05f09b9/AIBotBridge.app`，用户完成钥匙串授权。自动确认 USB 数据已确认、Wi-Fi 收到已认证请求、蓝牙已认证／USB 优先待命；本次蓝牙恢复已观察到。日常只读恢复开启，显示 30 个近期会话，发送／输入框／语音／音乐／电脑控制保持关闭。用户重新进入已停止验收任务的回复页，确认出现明确的已中断／无公开文字提示，不再一直加载；空回复修复实体通过。历史分页、直达和锁屏仍分别跟进，不由此推定通过。

### 范围调整与后续界面工作

相机首次报“相机内存不足，请稍后重试（1/12）”；用户保留 USB 重启 TAB5 后仅进入一次拍照，改报“相机缓冲区不足，请重新打开（6/12）”。只读参考源码表明失败发生在设备相机初始化／视频缓冲申请阶段，尚未开始照片上传；不能将其计为桥接上传验收，也未最终确定分配失败根因。用户明确要求涉及固件的问题暂不修复、先完成桥接调试，因此暂停相机实机排查并保留待办，不修改或刷写固件，不重复要求用户重试。

用户要求下一步将 Mac 桥接页面布局风格与 Windows 适度统一：以 Windows 页面分组、设置顺序、状态提示和主要操作位置为参考，整理现有分散弹窗，同时保留 macOS 菜单栏及原生权限交互。先完成当前桥接功能调试；此条为下一阶段范围，尚未实施或验收。全部不合并 main、不部署、不发布、不刷机的边界继续有效。

The first CI failed to compile a heterogeneous test fixture; 05f09b9 adds explicit types without removing assertions or changing runtime behavior. CI 39532411346 and complete downloaded-artifact verification passed with 213 tests and no failures/skips, matching Release/app/tools/UI fixtures/source and hashes. Local Release/bundle/signing passed; local XCTest remains unavailable. After normal replacement and user Keychain authorization, USB/Wi-Fi and authenticated Bluetooth standby recovered. Daily read-only was restored for 30 chats; all temporary control/voice switches remain off. The user confirmed the stopped chat now shows an explicit interrupted/no-public-text state instead of endless loading. History paging, direct navigation and lock acceptance remain separate.

Camera initialization failed first at 1/12, then at 6/12 after one user-performed reboot/retry. Read-only source inspection places the failure before photo upload, but does not establish the final allocation root cause. At the user's direction, firmware-related investigation/repairs are deferred while bridge debugging proceeds; no firmware edits or flashing occur. The user also requested a subsequent Mac UI alignment with Windows grouping, setting order, status and action placement while preserving native macOS behavior. This is recorded future work, not implemented or accepted. Existing no-main-merge/deployment/release/flash boundaries remain in force.

### 历史分页通过、直达权限与磁盘采样

用户确认编号分组后续页能显示较早会话，历史分页实体通过。临时恢复已授权的桌面输入框开关后，用户从“Codex 直达”打开指定验收任务，Mac 会话已切换，但设备提示未确认前台；打开路由已观察到，精确输入框确认尚未通过。检查系统发现 AIBotBridge 辅助功能关闭；经用户当次明确授权、本人完成系统认证后，开关已确认开启，重新验收结果待记录。没有通过其他自动化工具绕过此前对 Codex 界面的操作限制。换候选后定位权限再次关闭，已恢复同范围既有授权并按原值刷新和风设置，实际刷新结果另核对。

磁盘响应新增桥接端采样，沿用既有 `runtimeHealth.pressure.diskLatencyMs` 字段；只读对照 Windows 调用方和设备消费者，没有改变协议或固件。本机 SDK 的公开 `IOBlockStorageDriver.h` 明确定义 Total Time 为累计纳秒、Operations 为累计已完成次数；使用按注册表身份匹配的相邻差值计算加权平均，首样本／新设备／计数回退／休眠长间隔不产生数值。空闲或读取失败只短暂保留上次有效测量，八秒过期；缺少计时的驱动不填零。未采集磁盘队列，不构造队列值或把平均负载当作 CPU 等待数。详情说明是磁盘 I/O 平均耗时，不是应用端到端延迟。独立生产采样探针取得三次真实有效值（约 0.194、0.048、0.071 ms），本机 Release 通过；本机 XCTest 缺失，完整 CI 与实体数据仍待完成。

The user confirmed older chats are visible in numbered group pages. Direct navigation opened the designated Mac chat, but exact foreground/composer verification failed; it is only partial acceptance. System Accessibility was off. After explicit action-time authorization and user authentication, the switch was verified on; the retry remains pending. No alternate automation bypass was used for the restricted Codex UI. Existing location access was also restored and unchanged QWeather settings refreshed, with completion separately checked.

Disk response sampling uses the existing pressure.diskLatencyMs field. The public SDK IOBlockStorageDriver header defines cumulative nanoseconds and completed-operation counts. Per-registry-entry deltas yield a weighted mean; initial/new/reset/suspended samples do not invent measurements. Idle/failure retention expires after eight seconds; drivers without timing data do not report zero. Disk queue and CPU queue remain unmeasured, and the detail distinguishes I/O mean duration from application latency. A production sampler probe returned three real values around 0.194/0.048/0.071 ms; local Release passed. XCTest still requires CI, and physical disk display acceptance remains pending.

辅助功能开关开启后，用户再次直达，以及手动点击主输入框后的对照仍未确认。桥接增加有限枚举的本机诊断（权限、前台应用、焦点、输入框标识／结构、目标地址或文字不可用），显示在交互权限页；不采集或输出实际窗口标题、输入内容、AX 树或 URL。原 409 与禁止误写逻辑不变。代码核对还发现 `file:` 页面身份写死 `/Applications/Codex.app`，现改为已确认 bundle ID 的实际前台应用资源目录，仍要求精确任务地址，拒绝其他目录、路径越界及远程 file host；不以通用标题或部分 UUID 判断成功。新增身份边界和不确定结果不重试回归。此改动并不证明实际安装版本公开了所需 AX 地址；完整直达仍待验证。

锁屏首次实体按钮灰色，未触发操作；正常重启同一候选后重新验收，结果待记。电脑控制临时开关开启、桌面输入框开关关闭、日常只读恢复开启。和风天气当前／24 小时／7 天已再次确认刷新成功。

Direct navigation remained unconfirmed after Accessibility was enabled and after a manual composer-focus comparison. The bridge now exposes only a bounded local reason enum, never actual titles, text, AX trees or URLs. Existing 409/no-typing protections remain. File-page identity previously hardcoded Codex.app; it now requires the verified foreground bundle's actual Resources directory plus exact task route, rejecting sibling/traversal/remote-host paths. Tests cover identity and no repeated effect on uncertain replay. This does not establish that the installed app publishes a usable accessibility route. Lock was initially disabled on the device; the same candidate was normally restarted for a separate permission-refresh check, still pending. Computer control is temporarily on, desktop input off, daily read-only restored; QWeather current/hourly/daily refresh succeeded.

### 9415cee 验证与辅助功能授权错绑定位

`9415cee0b625b6cef0bfe537eb78114ad5f179e5` 的 [Mac CI 39534484773](https://github.com/yaoyouzhong/AI-bot/actions/runs/39534484773) 与完整附件核验通过：219 项测试、0 失败／跳过，Release、签名 App、随包工具、原生界面和源码／包哈希均一致。本机 Release、App 构建及严格签名检查通过，新增直达状态的完整原生设置页预览已检查，无重叠或裁切。候选为 `artifacts/mac-tab5-20261021/9415cee/AIBotBridge.app`。

05f09b9 重启后锁屏仍灰色，添加该准确路径也未解决。系统权限条目的“在访达中显示”实际打开 `06dd164`，证实原授权指向旧候选；同名添加没有替换。系统移除按钮禁用，未强制修改隐私数据库。用户另行明确授权用系统 `tccutil` 仅重置桥接辅助功能，已执行 `tccutil reset Accessibility io.github.yaoyouzhong.aibot.bridge`，工具确认成功；没有重置其他权限或其他应用。

正常退出旧桥接、启动 9415cee 并由用户完成同类钥匙串授权后，通过系统“添加”选入新候选。授权开关为开，随后“在访达中显示”已核实打开 `9415cee`，与实际运行候选一致。USB／Wi-Fi／BLE 已自动恢复；日常只读与临时电脑控制恢复开启，发送／输入框／语音／音乐关闭。新授权下的真实锁屏与直达、磁盘显示仍待分别记录。上述旧路径是本次已验证的权限错绑，不将其泛化成所有钥匙串或定位问题的已证实根因。

CI 39534484773 and full artifact verification passed for 9415cee with 219 tests and no failures/skips, matching Release/app/tools/UI/source/hashes. Local Release/bundle/signing passed; the full native settings preview with the new direct-navigation status was inspected without clipping or overlap. Lock remained disabled after restarting 05f09b9 and adding its exact path. The system entry's Reveal in Finder opened 06dd164, proving it still referenced an older candidate. With the remove button disabled, the user explicitly authorized a bundle-scoped Accessibility reset via tccutil; it succeeded without resetting other apps/services. Following normal replacement, user Keychain authorization and adding 9415cee in System Settings, Reveal in Finder now resolves to the running 9415cee and its switch is on. USB/Wi-Fi/BLE recovered, daily read-only and temporary computer control are on, other temporary switches off. Real lock/direct/disk acceptance remains separately pending. This confirmed path mismatch is not claimed to explain every Keychain/location prompt.

### 9415cee 锁屏实机结果与剩余边界

用户确认锁屏按钮已经可用，点击后 Mac 实际锁屏；**锁屏操作实体通过**。用户同时反馈 TAB5 没有显示“已锁屏”，也不能解锁。代码核对确认 Mac 的 `locked`／`displayState` 当前返回未知，`oneClickUnlock=false`，所以不将这两项记为通过。交接文档已将 Mac 一键解锁排除在当前能力范围外，解锁仍由用户在 Mac 完成；没有保存密码、输入解锁凭据或修改系统认证策略。结束锁屏验收后，临时电脑控制开关关闭，日常 Codex 只读保留，桌面输入框开关仅为指定验收任务再次临时开启。

本机公开 SDK 的 `CGSession.h` 只声明用户／控制台／登录完成属性，没有锁屏属性；登录完成、控制台归属、屏幕休眠均不能冒充锁定状态。Apple DTS 在[公开接口讨论](https://developer.apple.com/forums/thread/850327)中区分受保护数据可用性与屏幕锁定，并指出常见锁定通知属于实现细节。按本项目只使用公开框架的规则，尚未找到足以实现准确锁屏状态的受支持信号，保留未知，不通过未公开字典键或命令成功回执制造“已锁屏”。这属于桥接能力缺口，不修改固件。

The user confirmed the enabled device lock button actually locked the Mac: physical lock acceptance passed. The device still cannot display a confirmed locked state or unlock; the Mac implementation deliberately returns unknown lock/display state and oneClickUnlock=false. Those capabilities are not accepted. Mac one-click unlock is outside the handoff's current scope; authentication remains on the Mac, with no password capture or authentication-policy changes. Temporary computer control was closed after this test, daily Codex read-only retained, and desktop verification temporarily re-enabled only for the designated acceptance chat.

The public SDK's CGSession properties describe user, console and login completion, not graphical lock state. Those properties and display sleep cannot substitute for lock evidence. Apple DTS distinguishes protected-data availability from graphical locking and describes common lock notifications as implementation details. No sufficiently documented signal has been established under this project's public-framework-only rule, so lock state remains unknown; neither undocumented keys nor successful command dispatch are used to claim a locked state. This is a bridge limitation, with firmware unchanged.

### 直达列表短暂不可见与位置反馈

用户最初报告验收会话及 AI-bot 会话不可见，随后在未重启或改动桥接设置的情况下又看到了会话，并明确异常位置在 TAB5 实体的直达／任务列表左下方。Mac 界面持续显示 30 个近期会话、USB 数据已确认和已认证无线连接；独立生产只读目录探针取得 30 条、7 个分组页，包含指定验收任务及 AI-bot 的一条本机会话，固定系统样本结合当前缓存生成的完整帧约 19.3 KiB。该探针不等于捕获了异常当时的实际发送帧，尚不能断言暂时不可见的根因。当前直达诊断仍是等待设备发起，没有把找到列表计为直达通过。位置反馈留待后续界面复核，按用户范围不修改固件。

The user initially could not find the acceptance or AI-bot chat, then found it again without a bridge restart/settings change and clarified that the unusual lower-left location was on the physical TAB5 task/direct-navigation UI. The Mac continued showing 30 chats and healthy transports. A production read-only probe returned 30 chats across seven picker pages, including the designated test chat and the one local AI-bot chat; the complete frame with fixed system samples and current cached data was about 19.3 KiB. This did not capture the actual transmitted frame at the time of the disappearance, so the cause remains unproven. Direct verification still awaited a device request; finding the chat is not direct-navigation acceptance. The layout observation is recorded without firmware changes.

### 9415cee 直达失败定位与磁盘实体通过

用户在正确辅助功能授权下再次点击指定验收任务的 Codex 按钮，设备仍显示“未确认 Codex 前台”。桥接有限枚举诊断为“主输入框标识尚不兼容”：权限、前台 bundle 和 AXTextArea 焦点检查已通过，失败在预期的 prompt-textarea／composer 标识匹配，尚未执行后续精确路由与文字校验。只读检查安装包的静态编辑器定义发现使用富文本 textbox，不能据此认定实际 AX 树具备桥接所需身份属性。没有读取 Codex 实时 AX 树、绕过界面工具限制、放宽精确任务校验或尝试写入。当前直达依然未通过，临时桌面输入框开关已关闭，日常只读保持开启。

用户补充验收任务与 AI-bot 项目的单条会话均位于 TAB5 左侧列表偏下，上方大片空白。只读对照消费者确认其先按 projectId 筛选，再从 visible_tasks[0] 紧凑填入行；Mac 没有传坐标或空白行，因此未发现可通过桥接排序修复的依据。设备布局／滚动状态根因仍待后续专门复核，按用户指示不修改固件，不将数据存在当成布局通过。

用户确认诊断页磁盘响应显示 0 毫秒，CPU 等待仍无数据。独立生产采样再次取得约 0.362、0.065、0.267 ms；参考消费者 health_number 使用 %.0f 显示，亚毫秒样本取整为 0，与用户观察一致。**磁盘数据采集及实体显示通过**，但当前整数显示不保留小数；未改固件或将数值人为抬到 1 ms。CPU 等待、VPN、PAC 仍未采集，不能据此宣称诊断全部通过。

With the correct Accessibility grant, the user retried direct navigation and still received an unconfirmed result. The bounded bridge diagnostic identifies the composer identifier check: permission, foreground bundle and AXTextArea focus passed, but the expected identifier did not. Exact route/text verification has not yet run. Static installed-editor inspection is not evidence that its live accessibility tree exposes the required identity. No live Codex AX probing, UI-tool bypass, relaxed task matching or typing occurred. Direct navigation remains unaccepted; the temporary composer opt-in was closed while daily read-only remains on.

Both single-chat groups appear low in the physical device list with blank space above. The consumer filters by projectId before densely filling rows from index zero; the Mac supplies neither coordinates nor empty spacer rows. There is no established bridge ordering repair for this observation. Device layout/scroll-state cause remains pending and firmware is unchanged.

Physical disk response now shows 0 ms. Fresh native readings were approximately 0.362/0.065/0.267 ms and the existing consumer formats integer milliseconds, explaining the observed rounding. Disk collection and physical display passed, with fractional display precision still absent. No fake minimum was introduced. CPU queue, VPN and PAC remain unmeasured.

9415cee 的系统定位开关起初为关，恢复既有同范围授权后两次原值刷新仍提示暂时无法定位，旧天气缓存保留。暂时关闭 Codex 只读以正常退出，再启动同一候选，没有出现可见的 SecurityAgent 进程；USB／Wi-Fi／BLE 自动恢复，Mac 明确显示“和风天气 已更新 · 24 小时 / 7 天”。同一候选正常重启解决了此次恢复，不将其泛化为定位失败根因。日常只读随后恢复，全部临时电脑／音乐／发送／桌面输入框／语音开关保持关闭。

The candidate's location permission was initially off. Restoring the existing scope followed by two unchanged-setting refreshes still left location unavailable, retaining old weather. After temporarily disabling the read-only child for normal exit and restarting the same candidate, no visible SecurityAgent process appeared; all transports recovered and the UI confirmed fresh QWeather current/hourly/daily data. This restart resolved the observed recovery but does not establish a universal location-failure cause. Daily read-only was re-enabled; temporary computer/music/send/composer/voice switches remain off.
