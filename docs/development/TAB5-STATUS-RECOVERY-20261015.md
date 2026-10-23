# TAB5 状态包超限与自动恢复 / Status size and recovery

## 问题与根因

现场桥接报告 `USB：状态数据超出协议上限`，最后成功帧为 32,739 字节。状态发布超限后提前返回，超过六秒不再提供共享状态；TAB5 超过八秒没有有效数据时，各物理连接仍在的图标变为黄色。报错虽然写在 USB 状态里，实际影响 USB、Wi-Fi、BLE 共用的状态发布。

原实现缩减 Codex 任务列表到最少 24 项，但 `repliesRead`、`repliesUnread` 仍发送列表外的任务。任务、回复、天气、额度、运行监控等内容累加后，缩减不足以保证包可发送。默认中文 JSON 转义还增加了传输字节数。

## 修改

- TAB5 状态采用保留 HTML 敏感字符转义的 UTF-8 JSON；不改变持久化和其他设备的序列化方式。
- 已读／未读状态只发送当前包里的任务，完整桌面同步记录仍保存在内存；恢复较大列表不会丢失原记录。
- 发送压力下优先保留概览任务及正在查看的任务、回复；必要时缩减到两项。正常状态预留 512 字节，目标上限 32,228 字节；协议最大明文仍为 32,740 字节，加 28 字节加密开销后不超过 32 KiB。
- 最小任务列表仍无法容纳时，自动生成有效的恢复状态：继续更新会话、序号、时间和可容纳的当前字段，保留可用缓存及其采样时间。支持 `stale` 的缓存标记过期，旧监控时间不会刷新成新采样；不重放缓存中的升级或显示命令。
- 每次发布都重新尝试完整状态。超限消失后自动恢复完整发布，既不要求重启设备，也不要求重启桥接。诊断报告 `degraded`、`fallbacks`、`recoveries` 及暂缓字段名称，不记录消息文本或凭据。

这是本机桥接热修复，TAB5 保持 `0.2.197-ui`，没有扩大协议、刷机或公开发布。

## 验证

- 修复前新增回归测试明确失败于 `repliesRead bounded to transmitted tasks`，修复后通过。
- 80 个完成任务、40 个已读／40 个未读、中文及特殊字符保真、当前回复优先、低于 24 项的预算路径、连续更新及加密往返通过。
- 正常 → 超限 → 连续恢复状态 → 自动恢复正常、启动即超限、旧采样时间保留、最大尺寸旧帧恢复、旧命令不重放通过。
- `--self-test-tab5-telemetry` 全部通过，包含压缩、增量确认、重连首帧、连续 RPC 时数据新鲜度、USB 指标调度及图片公平传输；`--self-test-tab5-rpc` 通过（模拟链路）。
- 要求的 Release 构建零警告、零错误；`dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once`、安装目录串口运行检查、`git diff --check` 通过。
- 完整 `--self-test-tab5` 在原有 USB 清理断言 `unrelated cleanup faults are not hidden` 处失败，修改前基线同样失败；本次没有改动该分支，不能报告该套件全通过。

最终安装 DLL SHA-256：`30C9821CDD0404108C69C982284A959FDA87F233F5A6E9669B36AFA2C36507CC`。

最终版本于 2026-10-15 16:27:22–16:28:10（北京时间）采集 25 份真实设备诊断：USB COM9 已连接，USB 数据年龄最大 111 ms；Wi-Fi 桥接通信正常，数据年龄最大 3,662 ms；蓝牙全程认证连接、USB 优先待命。没有 USB/Wi-Fi 超过八秒的样本。实际完整状态约 34.6 KB，自动限制任务后约 31.1 KB，设备持续接收，未进入恢复降级。蓝牙待命按当前固件显示浅蓝色，其较旧的数据年龄不等于无线断开。

极端超限恢复使用受控测试，未向真实设备注入异常业务数据。本次未获取屏幕照片，图标颜色依据真实接收年龄及固件颜色分支推导。

正常退出、完整平台依赖替换及启动后，`auto`、开启轮播、原六页与顺序、15 秒间隔均核对通过。配对、设备清单、用户设置、语音设置、事项等已有配置哈希保持一致；正常运行中的额度、天气和股票缓存有更新。旧程序和配置备份在本机 LocalAppData，位置记录于下列证据目录。

证据：`artifacts/tab5-status-budget-20261015/` 中的 `telemetry.log`、`rpc.log`、`baseline-tab5.log`、`build-required.log`、`installed.json`、`live-final-samples.json`、`live-final-stats.json`、`policy-verification.json`、`profile-comparison.json`、`backup-location.txt`。

## 是否扩大协议

32 KiB 是当前应用软件限制。固件 `device.h`、USB 行接收、Wi-Fi 加密回复、BLE 分片、解密、解压及增量树边界需一起审核。M5Stack 官方资料注明 TAB5 有 32 MB PSRAM，因此硬件容量具备扩展条件，但不能据此宣称更大状态包已通过三通道、内存峰值和 UI 响应验证。当前固件不能接收仅由桥接单方放大的状态包。扩大容量与详情按需传输属于后续协议工作；本次保持现有协议和固件。

硬件依据：[M5Stack TAB5 官方规格](https://docs.m5stack.com/zh_CN/core/Tab5)，本次通过 M5Stack 官方 MCP 查询。

## English

Shrinking the Codex catalog did not shrink desktop read/unread maps, and the original fallback stopped at 24 tasks. Combined status growth could therefore halt the shared publication used by all three transports. The fix sends compact UTF-8 JSON, filters task maps without discarding desktop read state, prioritizes the viewed reply, and budgets the catalog down to two tasks with 512 bytes of reserve.

If even that frame is oversized, publication emits a bounded recovery status with current ordering/clock fields and available current sections, preserving cached business values and original sample timestamps. Cached commands are never replayed. Each publication retries normal state and automatically exits degradation when it fits. Tests cover running and cold-start overload, repeated fresh fallback, automatic normal recovery, maximum-sized legacy baselines and encrypted bounds. Extreme failure cases are simulated.

The final locally installed bridge passed build, status capture, serial-runtime, telemetry and simulated RPC checks. Twenty-five real-device samples over approximately 48 seconds showed USB and Wi-Fi data ages below eight seconds throughout, with authenticated BLE standby. Display policy and existing preferences were preserved. No physical screenshot was captured. The broad TAB5 self-test has an independently reproduced pre-existing USB cleanup assertion failure, so it is not claimed as passing. Firmware remains 0.2.197-ui; no protocol expansion, firmware flash or public release occurred.
