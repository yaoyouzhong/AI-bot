# Mac TAB5 Codex 链路后续 / Codex link follow-up

本轮沿用测试分支与交接边界：自动测试、最小修复，不合并 main、不部署、不发布、不刷机。真实发送／追加／中断仍只针对用户明确指定的测试任务；本轮没有指定，未执行这些操作。

## 公开 socket 根因与不部署服务的修正

下面保留较早的受阻记录；本节是后续查清的根因与新路径。

1. 本机桌面主 App Server 的实际启动参数没有 `--listen unix://`，使用默认 stdio。只读核对随包启动逻辑：本地 daemon 路径受显式环境开关、主机类型和 `daemon version` 成功探测等条件控制；失败就启动 stdio，桌面不会因此创建 daemon/socket。当前默认 socket 的错误是 ENOENT，不是 EACCES，未发现本机 TCP 监听。这是当前启动方式的结果，不是“桌面窗口没打开”。未修改桌面启动配置或这些环境开关。
2. 实际 `.163` CLI 的 `app-server proxy` 是原始字节转发，Unix 端点采用 WebSocket 握手。空临时 CODEX_HOME 中的对照实验确认：stdio 初始化成功且不生成控制 socket；显式短路径 Unix 监听会生成 socket，直接 JSONL 无法握手，经 proxy 转发的 HTTP Upgrade 返回 **101**。旧客户端将 proxy 当作 JSONL 适配器，是另一处独立不兼容。所有临时进程／目录均已清理；未执行 daemon bootstrap/start、安装服务或接入用户内部桌面 IPC。[公开传输说明](https://learn.chatgpt.com/docs/app-server)
3. 正常桥接现在只在 Codex 只读功能启用后，通过已安装 CLI 启动 `app-server --listen stdio://` 子进程，关闭功能／退出桥接时结束。无常驻服务、无新增监听端口，不修改登录、配置、模型或审批策略。生产 RPC 仅允许 `thread/list`、`thread/read`、`thread/turns/list`、`thread/items/list`，其他方法在写管道／启动进程前即被拒绝。
4. 临时进程不是桌面任务持有者，所有任务实时状态保持 unknown；保存记录里的 inProgress 也不当作已证实运行中。发送、追加、中断、resume 及快捷控制台提交在界面、业务层和 RPC 层受限。即使有其他可信连接，notLoaded 也不能当 idle 自动恢复后发送。设备绑定的本机加密草稿仍可使用，不触发 Codex 任务操作。
5. 只读回复改为元数据读取加 `thread/turns/list` 的 summary 分页，每页最多 32 轮、单次最多 8 页，且检测重复 cursor；只保留 agentMessage，公开正文仍受 1 MiB 上限约束。更深历史不能无界加载。没有使用 deprecated 的整段历史回复来读取当前长会话。

本机 Release 与测试源解析通过；XCTest 仍因工具链缺模块而在执行前受阻。实际 Debug 客户端＋已安装 CLI 的空临时目录检查通过：标准 stdio、唯一子进程复用、非只读方法拒绝与退出清理。另做实际本机只读集成：投影 16 个近期任务槽位，精确匹配当前仓库项目，读取公开回复的业务 RPC 结果 200、正文 679 字节、保存状态 completed，全部发送控制为 false。诊断未输出或持久化标题、UUID、正文、路径、推理、工具输出或认证信息。它不等于正常候选／TAB5 的实机验收；后者须在本轮 CI 通过后执行。

新增七项回归覆盖进程生命周期、方法白名单、拒绝不重试、读失败不重放、只读公开回复／草稿／控制、分页边界、notLoaded 禁止自动恢复及桌面提交不改输入框（部分项目合并在同一测试）。完整结果以后续 CI 及正常候选记录为准。

Windows 当前走 `codex-ipc` 命名管道与持有任务的桌面进程交流，不依赖这个公开 socket。其源码明确标注为依赖版本的未公开桌面 IPC；这解释了两端现象不同，不能把 Windows 的成功当作本 Mac 公开接口已连通。

## 正常候选与 TAB5 只读验收

程序 `604ac8c9dc1bb5abfb560e4179811c8def611c16` 的 [Mac CI 39384001820](https://github.com/yaoyouzhong/AI-bot/actions/runs/39384001820) 与完整附件核验通过：163 项测试，0 失败／跳过，Release、签名候选、工具、8 张原生 PNG、音乐 ASan/UBSan 及源码／包哈希均通过。本机 Release、正常包签名和随包工具检查通过；本机 XCTest 仍缺模块，未执行。

用户处理正常候选的钥匙串提示，并明确授权本轮临时只读访问、完成后关闭。助手自动开启 Codex 只读后，正常 App 显示 **29 个近期会话**；唯一桥接及其自有 CLI 子进程、8765 监听和 USB 串口所有者已核对。USB 数据确认、Wi-Fi 认证请求、蓝牙已认证且 USB 优先待命。用户确认实体首页显示 Codex 任务，进入 AI-bot 项目任务后公开回复正常显示；上一轮／分页观察也回复正常显示，但没有单独明确长时间保留位置的结果。因此任务投影和公开回复的实际链路通过，不能据此宣称所有历史阅读行为通过。未向设备投影或在诊断中保留推理／工具输出，也没有发送、追加、中断、恢复任务或操作桌面输入框。只读开关随后由助手关闭，界面恢复“尚未启用”。

同轮合成检查明确复现：选中历史回复第二页后，四秒定时刷新会跳到最新回复第一页。修正只在成功读取后保存任务／轮次／页位置，后台刷新保留该位置；明确回到最新回复后继续跟随新轮次；失败导航不覆盖已显示的选择。新增回归同时覆盖这三种行为；更新了快捷控制台提示中与只读限制不一致的一句文案。

修正后实际 Debug 对象的同一合成复现保持第二页，并通过新轮次、失败导航与回到最新的检查。本机 Release、签名候选、随包工具与 8 张原生界面夹具通过，本机 XCTest 仍因缺模块未执行。程序 `a3bd642270a5a78fe200fe427821041e1a727a76` 的 [Mac CI 39385101526](https://github.com/yaoyouzhong/AI-bot/actions/runs/39385101526) 和完整附件核验通过：**164 项测试，0 失败／跳过**，新增阅读位置回归确认只执行一次且通过；Release、候选包／工具、原生渲染、音乐 ASan/UBSan、源码及包哈希通过。

新候选正常运行并恢复 USB／Wi-Fi／BLE 后，助手沿用本轮临时只读授权重新启用，仍显示 29 个近期会话。用户针对“打开公开回复、上一轮／第二页、至少停留十二秒、确认不自动跳回”的明确检查反馈“都是正常的”，**新候选公开回复和阅读位置保持实机复验通过**。

随后助手关闭只读，界面恢复“尚未启用”，确认自有 CLI 子进程退出、全部交互开关关闭、入口为空；主窗口 USB 数据确认、Wi-Fi 已认证请求、BLE 已认证且 USB 优先待命。用户返回原日常总览页，未修改设置或保存测试内容。临时只读已按约定收尾，返回首页时任务卡回退电脑状态属于关闭功能后的预期行为。

## 本轮钥匙串提示核对

用户在更换候选后再次反馈两次相同服务名的授权。只读源码核对确认启动先读取 `pairing-token`（旧设备 LAN），再读取独立的 `tab5-pairing-v1`（TAB5 配对）；二者都用 `io.github.yaoyouzhong.AI-bot` 服务名，弹窗外观因此难以区分。没有读取钥匙串内容或逐张检查账户，不能把两次提示认定为同一项重复读取。正常程序对 LAN 成功／失败读取有进程内缓存；TAB5 配对启动时读一次。

`codesign -d -r-` 显示 `604ac8c` 与 `a3bd642` 的临时签名 designated requirement 分别绑定不同 cdhash，旧包的始终允许不保证识别新包。按 [Apple 说明](https://support.apple.com/zh-cn/guide/keychain-access/kyca1243/mac)，允许仅针对本次访问，始终允许记录后续许可。稳定证书签名是后续候选身份的改进方向，本轮未创建证书、更改信任要求、合并配对材料或放宽钥匙串 ACL。本机密码只用于系统授权；桥接请求的是自己的配对材料。

## 较早的受阻记录与只读证据

本机已安装 CLI `0.163.0-alpha.10.2`。其公开 `app-server proxy --help` 提供 `--sock`，但默认控制 socket 不存在；只读 `app-server daemon version` 也返回同一 `ENOENT`。当前桌面子进程使用 stdio。实际桥接 RPC 对象复核代理在初始化前退出，没有任务请求到达服务端。没有连接内部桌面 IPC、读原始 rollouts、修改用户配置，或启动／部署 daemon。

按官方 [App Server 文档](https://learn.chatgpt.com/docs/app-server)核对握手，并用已安装 CLI 导出本版本 JSON schema（只在忽略目录）。另外在空的临时 `CODEX_HOME` 中启动一次短暂 stdio **隔离探测**：初始化、空任务列表成功，不存在的合成 UUID 读取被拒绝；随后进程退出、临时目录清理。没有登录、模型请求、真实任务创建或访问用户会话。这证明已安装版本支持这些协议方法，不能冒充已接入当前桌面服务。

## 修正与自动回归

1. App Server 的 `active` + 空 `activeFlags` 映射为现有 TAB5 `working`；等待批准／用户输入映射 `waiting`，`idle` 保持 `idle`。`notLoaded`、`systemError`、未知状态／标志保持 `unknown`，不猜成空闲或运行。
2. 首页任务先选实际运行中的会话，再按更新时间选择；传输裁剪优先保留当前阅读任务与首页运行任务。只有一个槽位时保留阅读任务，并从实际传输列表计算首页，避免引用不在列表内的任务。公开回复与阅读选择互不覆盖。
3. CLI 缺失、代理握手前退出、超时、拒绝、不兼容数据与后续断线给出分别可辨认的本机提示。不显示原始异常、stderr、路径、提示词或 token；失败保留最近公开数据并清掉可操作控制。

四项 XCTest 回归覆盖状态兼容、首页／受限列表、断线缓存与隐私、CLI／代理失败区分。另用实际 Debug 对象在原创合成目录验证状态、首页与阅读任务保留、缓存和公开内容边界，并确认本机真实代理的安全失败分类；这不等于 XCTest 或实机验收。本机 Release 通过；XCTest 仍在执行前因缺少模块受阻，完整测试以本轮 Mac CI 为准。

程序 `ef773a0` 的 CI 已通过，报告确认四项新回归均已执行。[最新源码 `1788914` 的 CI](https://github.com/yaoyouzhong/AI-bot/actions/runs/39381792622) 与完整附件核验为 156 项、0 失败／跳过，以及 Release／App／工具、原生渲染和源码／包哈希通过。实际 RPC 客户端还与已安装 CLI 在空临时目录中完成上述只读隔离探测，用户实际桌面数据未接入。新候选经用户本机钥匙串授权后正常运行，USB 与 BLE 自动恢复；所有交互开关仍关闭。

## 首页为什么显示电脑状态

已核对运行 `.234` 精确源码 `ui.c` 的 `render_home_task`：缺少有效 `overviewTask`／任务列表时，同一首页卡片回退 CPU、内存及“电脑状态”；有有效任务才显示任务标题与继续 Codex 入口。这与普通后台 Codex 活跃统计独立。现场 Codex 功能开关已恢复关闭，而且现有代理未连接，所以没有可用任务。没有改动首页布局或用户展示设置。

## 剩余条件

任务列表、保存的公开回复与本机加密草稿已具备不部署服务的只读路径，正常候选与 TAB5 复验见后续记录。桌面实时状态、发送、追加、中断与送达仍需要经验证的共享任务持有者连接，以及用户指定的真实测试任务；单独部署 daemon 并不证明桌面也使用它。当前不部署服务、不接内部 IPC；所有发送、桌面输入框及语音开关保持关闭。

## English

Normal candidate `604ac8c9dc1bb5abfb560e4179811c8def611c16` passed [CI 39384001820](https://github.com/yaoyouzhong/AI-bot/actions/runs/39384001820) and complete artifact verification: 163 tests, zero failures/skips, Release/app/tools, eight native PNGs, music sanitizers and source/package hashes. Local Release/signature/tool checks passed; local XCTest did not execute. After user-handled Keychain authorization and explicit temporary read-only consent, the assistant enabled the feature. The normal app showed 29 recent conversations with one owned CLI child and the expected exclusive listener/serial ownership; USB/Wi-Fi/BLE recovered. The user confirmed a Codex home task and public replies on the physical TAB5. Their older-turn/page response confirmed display, without separately confirming sustained position retention. The assistant then disabled the feature; send/steer/interrupt/resume/desktop input stayed unavailable throughout.

A synthetic check reproduced history page two resetting to the latest turn/page zero on timer refresh. The subsequent repair preserves only successfully displayed selections, continues following new turns after an explicit latest read and leaves selections intact after failed navigation. One regression covers all three behaviors; an inconsistent console sentence now states the read-only restriction. Actual-Debug-object checks now retain page two across new turns and reject failed navigation without changing it, while explicit latest reads follow new turns. Local Release/signed app/tool/eight native fixture checks passed; local XCTest still did not execute. Program `a3bd642270a5a78fe200fe427821041e1a727a76` passed [CI 39385101526](https://github.com/yaoyouzhong/AI-bot/actions/runs/39385101526) and full artifact verification: 164 tests, zero failures/skips, with the new reader regression executed once; Release/app/tools/native rendering/music sanitizers and source/package hashes passed. After normal startup, the assistant reused the existing temporary read-only authorization; the app again showed 29 conversations with USB/Wi-Fi/BLE recovered. Asked specifically to view public replies, navigate to an older turn/page two and remain there for at least twelve seconds without a reset, the user confirmed both checks normal. Public reply display and selection retention therefore passed physically on this candidate. The assistant disabled the reader, confirmed its child exited and all interaction switches were off with an empty launcher list, then returned the Mac window to acknowledged USB/authenticated Wi-Fi/BLE USB standby. The user returned TAB5 to the original daily overview without changing settings or saving test content. The home card falling back to computer status after this opt-out is expected.

On replacing the candidate, the user again reported two Keychain prompts. Startup code reads two separate accounts (`pairing-token` for legacy LAN and `tab5-pairing-v1` for TAB5) under the same service name; individual prompt accounts/content were not inspected. LAN reads are cached per process and TAB5 pairing is loaded once at startup. Read-only code-signature inspection showed different cdhash-bound designated requirements for the two ad-hoc candidates. An old candidate's permission need not identify the new binary; Allow is one access and Always Allow records subsequent permission, per the linked Apple guidance. Stable certificate signing remains a future packaging improvement. No certificate, trust requirement, Keychain content or ACL was changed.

The subsequent investigation found two independent causes. The actual desktop child uses default stdio; the bundled startup selects an existing local daemon only under explicit opt-in and a successful daemon-version probe, otherwise starting stdio without creating a socket. No startup setting was changed. An empty-home transport comparison additionally proved that this CLI proxy relays raw WebSocket bytes: a short-path Unix listener existed, bare JSONL did not initialize, and HTTP Upgrade through the proxy returned 101. All diagnostic processes/directories were removed; no daemon was deployed. The old JSONL-over-proxy assumption was incompatible even with a socket.

The repaired production reader directly starts an app-owned `app-server --listen stdio://` child only while the feature is enabled, ending it on opt-out/exit. It installs no service, adds no listener and overrides no auth/model/approval configuration. RPC permits only the four documented read methods; mutations are rejected before process startup or writes. Desktop runtime is unknown, stored in-progress turns are not treated as live, and send/steer/interrupt/resume/desktop submit are unavailable. Unloaded threads also cannot be mistaken for idle by another backend. Local encrypted drafts remain separate from task operations. Stored public replies use summary pagination (32 turns/page, 8 pages/request, repeated-cursor rejection), public agent messages only and the existing 1 MiB reply bound.

Local Release/source parsing and actual-object installed-CLI empty-home lifecycle/denial checks passed; local XCTest is still unavailable. An actual local read-only integration projected 16 recent-task slots, matched the repository project and retrieved a completed 679-byte public reply with status 200 and every send control false. The diagnostics output or persisted no title/UUID/body/path/reasoning/tool/authentication data. Seven new regressions cover the lifecycle, denial/retry, public pagination, drafts/controls, unloaded-state and desktop-submit boundaries. Full CI and subsequent normal-app/TAB5 acceptance are recorded separately. Windows currently uses version-dependent private desktop named-pipe IPC, so its working behavior does not validate the Mac public proxy path. The earlier blocked records below remain historical.

The installed `0.163.0-alpha.10.2` public proxy still exits before initialization because its default control socket is absent; read-only daemon version probing confirms ENOENT. The desktop child uses stdio. No daemon, internal IPC, raw rollout access, user configuration change or real-task mutation was used. A short-lived CLI process under an empty temporary CODEX_HOME passed initialization/empty list and rejected a missing synthetic thread, then exited and was removed. This isolated probe is not attachment to the user's desktop service.

App Server active/empty flags now maps to existing TAB5 working; approval/input waits map to waiting. Idle stays idle; unloaded/error/future states remain unknown. The home card prefers a working task and bounded snapshots preserve it alongside the reader when capacity permits; one-slot snapshots retain the reader with an internally consistent overview. Safe connection errors distinguish missing CLI, pre-handshake exit, timeout, rejection, invalid data and disconnect, retain cached public data and suppress controls without exposing diagnostics or secrets. Four new regressions cover these paths. Actual Debug-object synthetic checks and local Release passed; local XCTest is still blocked before execution and full tests depend on Mac CI.

Both program ef773a0 and latest reference-only follow-up 1788914 passed CI. The latter's full artifacts verified 156 tests with zero failures/skips, Release/app/tools, native PNGs and source/package hashes. Actual RPC-client/installed-CLI isolated probing also passed without attaching user desktop data. After user-handled Keychain authorization the new candidate ran normally with automatic USB/BLE recovery; all interaction switches remained off.

The exact running .234 source intentionally falls back to CPU/memory on the home task card when no valid task exists. In that earlier checkpoint the feature was off and the proxy unavailable, so no task was projected; the layout was not changed. The later temporary reader above now provides saved catalog/reply access; a verified shared task owner is still required for desktop runtime and delivery acceptance. No real test task was designated and no send, steer or interrupt was performed. Feature switches and device display preferences are preserved.


## 日常只读启用与后续开发 / Daily read-only enablement and follow-up

天气验收完成后，用户明确授权“开启并保留日常只读使用”。已在当前 `06dd164` 正常权限页开启 Codex 只读，界面确认“只读已连接本机 Codex · 29 个近期会话”；发送／追加／中断、桌面输入框、音乐、系统控制、语音开关均保持关闭。正常关闭权限子窗回到 TAB5，三通道与和风正常。此新授权替代较早的“验收后关闭”要求，今后不应自动关闭日常只读。

用户随后明确要求继续开发完整 Codex 操作功能。现有只读链路不能被表述为完整控制；接下来独立验证桌面任务持有者、发送／追加／中断与输入框能力，真实写操作只面向明确指定的测试任务，不更改模型、权限或审批策略。仍不部署服务、不合并 main、不发布、不刷机。

After weather acceptance, the user explicitly authorized persistent daily read-only access. It was enabled in candidate `06dd164`; the normal UI confirmed 29 recent local sessions. Sending/steering/interruption, desktop composer, music, computer-control and voice opt-ins stayed off. The permissions window was closed normally, with all transports and QWeather healthy. This supersedes the earlier temporary-only instruction; daily read-only should not be automatically disabled.

The user then requested full Codex controls. The existing read-only connection is not full control. Further work must establish the actual desktop task owner and verify sending, steering, interruption and composer behavior; live mutations remain limited to an explicitly designated test task. Model, permission and approval policies stay unchanged. No service deployment, main merge, release or flashing.
