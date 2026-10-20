# Mac TAB5 操作与剩余数据接续

用户要求继续开发完整 Codex 操作、Codex 直达，验收一键锁屏，并报告桌宠、系统诊断及日历缺失。继续仅在测试分支开发／CI／临时候选验收，不合并 main、不部署服务、不发布、不刷机。日常 Codex 只读已获持续授权，应保留；真实发送／追加／中断仍仅针对用户明确指定的测试任务，不能自动批准任务中的权限请求。

## 已取得的证据

- 桌面公共代理 socket 仍缺失。但本机 Codex 桌面进程持有现有 `~/.codex/ipc/ipc.sock`；已验证当前用户所有权与目录／socket 权限，通过只读握手、当前任务持有者发现、订阅状态快照及退订关闭。协议为四字节小端长度和 JSON；初始化版本 0，发现／订阅版本 1，状态快照版本 11。只记录能力和字段名称，没有发送、追加、中断、改输入框或部署服务。
- 该 IPC 属于桌面版本相关接口，不能当作稳定公共 API。后续实现须限制精确本机持有者、版本、任务身份、运行状态与请求幂等，遇不兼容即拒绝写入；不得在独立 stdio 子进程中恢复桌面活跃任务来假冒同一会话控制。现有公开 App Server 只读历史路径继续保留。
- 实际设备源码 `ui_runtime_health.h` 读取 `runtimeHealth`；Mac 当前没有此对象，所以诊断页整体缺少数据。后续应输出实测可用项；未知的 VPN、队列、磁盘延迟不能填写正常或零。
- 本机 Claude／Codex 的 selected/default APET 四个文件均缺失；不是已证实的设备传输失败。图库选择和授权图片导入实现存在，但默认桌宠没有自动来源。不得导入来源不明的旧个人素材；已有项目可选素材的许可与用户选择应独立核对。
- 日历缺少 `calendar.detail` 已在 `29f1654` 补齐现有 2026 年事实表。新候选验证和实体结果见日常数据记录；不能将源码修复记成实机通过。

上述完整控制、直达、锁屏、桌宠和诊断仍未完成新一轮实机验收，不因既往只读、音量或音乐通过而标为通过。

## English

The user requested full Codex controls, direct navigation, lock acceptance and missing pet/diagnostic/calendar data. Preserve daily read-only authorization and all no-merge/deploy/release/flash boundaries. Real task mutations require the user's specifically designated test task; never approve its permission requests automatically.

A read-only probe of the existing, owner-checked desktop IPC socket succeeded at initialization, exact current-task owner discovery, snapshot subscription and unsubscribe. No mutation or service deployment occurred, and no task contents were retained. This is version-specific desktop IPC, not a stable public API: implementation must validate owner/version/identity/state and fail closed, retaining public read-only history rather than resuming desktop tasks in an independent process. The device diagnostics consumer expects the currently missing `runtimeHealth`; unavailable measurements must remain unknown. All four local selected/default pet files are absent, which does not establish a transport failure. Calendar annual detail was added in `29f1654`, with hardware verification tracked separately. Full controls, direct navigation, lock, pets and diagnostics remain pending.
