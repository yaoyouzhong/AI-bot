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
