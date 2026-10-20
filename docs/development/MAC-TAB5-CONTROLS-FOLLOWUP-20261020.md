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

## English

The user requested full Codex controls, direct navigation, lock acceptance and missing pet/diagnostic/calendar data. Preserve daily read-only authorization and all no-merge/deploy/release/flash boundaries. Real task mutations require the user's specifically designated test task; never approve its permission requests automatically.

A read-only probe of the existing, owner-checked desktop IPC socket succeeded at initialization, exact current-task owner discovery, snapshot subscription and unsubscribe. No mutation or service deployment occurred, and no task contents were retained. This is version-specific desktop IPC, not a stable public API: implementation must validate owner/version/identity/state and fail closed, retaining public read-only history rather than resuming desktop tasks in an independent process. The device diagnostics consumer expects the currently missing `runtimeHealth`; unavailable measurements must remain unknown. All four local selected/default pet files are absent, which does not establish a transport failure. Calendar annual detail was added in `29f1654`, with hardware verification tracked separately. Full controls, direct navigation, lock, pets and diagnostics remain pending.
