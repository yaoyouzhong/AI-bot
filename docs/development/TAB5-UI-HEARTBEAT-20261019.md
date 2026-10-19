# TAB5 界面心跳采样竞态

后续结论：.233 的实际失败数值明确为小数 `uptimeMs`，不是本页竞态的现场证明；.234 修复该报告格式后，原 c608 在纯 BLE 下通过三份实时样本、精确 ELF 与 VALID 核验，电脑 WLAN 已恢复。详见 [报告整数时间修复](TAB5-BLE-UPTIME-20261019.md)。本页 .231/.232 结果保持当时的证据边界。

Later outcome: the actual .233 rejection was fractional uptime, not observed proof of this heartbeat race. After fixing that report format, .234 passed the isolated BLE exact-image/VALID/three-live-sample check on unchanged c608. PC WLAN was restored. Earlier .231/.232 records below retain their original evidence limits.

状态：用户已批准并完成 0.2.232-ui OTA，重启后实际 IPC 确认设备运行该版本；随后唯一未通过的纯 BLE 启动核验再次失败。该版本已完成本地构建、完整原生界面、实际函数回归和三类交付包校验，尚未公开发布。Windows 0.6.10 / c608 DLL 及其 400 项编译输入不变，提供新镜像及升级期间未重启桥接。

| 版本 | 真实结果 | 完成判断 |
| --- | --- | --- |
| .230 | 纯 BLE 报告超时 | 未通过 |
| .231 | 纯 BLE 报告已收到，2.33 秒后界面健康校验拒绝 | 仍未通过 |
| .232 | 修复已确认的心跳读取竞态；已 OTA，但纯 BLE 报告在 1.08 秒后仍被相同健康条件拒绝 | 仍未通过 |

.232 实机操作只发出一次只读核验请求，USB 未连接、Wi-Fi 桥接无新通信、BLE 已连接且数据确认，隔离违规列表为空。失败报告的具体数值仍未被现有桥接保存，因此不能继续把该笼统错误归因于已修复的心跳竞态。用户已立即恢复电脑 WLAN；没有自动重试、重启、再次刷写或重复已通过的项目。下一步先补精确失败数值诊断，不凭相同错误继续迭代固件。用户另报的电量问题独立记录在 [电量数字连续性](TAB5-BATTERY-DISPLAY-20261019.md)。

.231 核验的 USB 为未连接，Wi-Fi 桥接无新通信，BLE 已连接且数据已确认；整个操作没有隔离违规。脚本只发出一次已有 IPC 的只读核验请求，没有锁屏、解锁、键盘输入、网络修改或固件写入。返回错误为“设备界面尚未稳定运行”。身份和版本校验已经通过，接下来的健康条件包括递增 uptime、0–2000ms 界面心跳年龄、无刷新错误以及已有刷新次数。

该次回包的具体数值没有被桥接保存，因此不能把某个失败字段写成已观察事实。恢复电脑 Wi-Fi 后，用户接回 USB 只读诊断：当时界面心跳年龄 12ms、显示错误 0；随后十二份被动状态样本为 3–9ms、显示错误仍为 0。这些是当前运行证据，不能替代之前失败时的数值，也不能据此把 BLE 判为通过。

实际 .231 `ui.c` 的年龄函数在单一表达式中先读取时钟、再读取原子心跳。UI 在另一核更新心跳时，后台线程可能用较早的时钟减去较新的心跳，无符号减法溢出。冻结 .231 ELF 的反汇编确认了该顺序。用冻结源码中提取的实际函数进行受控交错：时钟捕获 1000ms 后，UI 发布 1001ms 心跳，旧函数得到 `4294967295ms`，原生回归按预期失败。这证明了源码和已刷字节中的缺陷，但仍不等于恢复了该次现场失败样本。

.232 只把原子心跳读取放在时钟采样之前，保留无符号时钟回绕语义，没有把错误值钳为零，也没有改动桥接的 2000ms、刷新错误或镜像校验条件。相同交错得到正常值；真实 3000ms 的旧心跳仍返回 3000ms，既有未来心跳异常仍保留无效大值，32 位回绕测试得到正确的 21ms。新 ELF 也确认先读取心跳、再调用时钟。

相对 .231，223 项主目录输入中只有 `ui.c` 变化，内容为这一个函数和解释注释。之前的 BLE 报告有界重发、RPC 时限、音量恢复、已读回写、解锁、取消和断线行为均保留。新增原生测试从实际 `ui.c` 提取函数，覆盖并发、真实停滞、异常未来值、回绕和启动零值；测试与对应源码一起交付。失败日志、旧/新函数、反汇编、实际回包及只读状态证据在 `artifacts/development/release-closeout-20261019/` 保留。

新候选 BIN SHA-256 为 `dcb1fcd3518d22696b57e60c759c7f9791cc93b5ae06c0a84ac82d11e4eeb077`，ELF SHA-256 为 `1d1056b350d66c943b01489b742fef2e1b0e0a6707785f69350c4af8fbbfa4e9`。用户已针对这些字节授权升级；现有桥接的固件选择器提供后，完整 BIN 与 ELF 哈希均匹配，OTA 后再次读取确认 installedVersion=0.2.232-ui。随后只补未通过的纯 BLE 核验。用户已经通过的音量、已读同步、各通道锁屏／解锁、取消、断线和黑屏唤醒不再要求重复。纯 BLE 核验须断开 USB，暂时关闭电脑 WLAN，保留蓝牙和网线，TAB5 保持自动选择；无论结果如何均立即恢复电脑 WLAN。公开推送、标签和 Release 仍未获授权。

English: The isolated .231 BLE report arrived, but its UI-health check failed. The failed report's numeric fields were not cached, so the exact failing field is unknown. Later passive USB samples were healthy and cannot replace that failed sample. A separate race is proven in the frozen .231 source and ELF: a newer heartbeat read after an older timer sample wraps the unsigned age to 4294967295ms. The .232 repair loads the heartbeat first. Controlled native tests and the new ELF confirm the correction while preserving genuinely stale, invalid-future and wrapping-clock cases. Only one helper and its comments changed within the 223 main inputs; bridge inputs and c608 remain identical. Firmware build, full native UI, actual-function tests, real sidecar preview and licensed factory/OTA/source archive checks have passed; all 958 source entries were hash-verified. The user approved these exact bytes and completed OTA. Fresh IPC confirms installedVersion=0.2.232-ui after reboot, with the exact offered BIN and ELF hashes. Its isolated BLE check then failed after 1.08 seconds with the same generic UI-health error; it sent exactly one operation and recorded no isolation violations. The actual failed numeric fields remain unavailable, so the repaired race cannot be claimed as the observed cause. PC WLAN was immediately restored and confirmed by the user. Previously passed physical acceptance is retained. No automatic retry, restart or additional flash followed. Exact failure diagnostics are needed before another BLE-driven firmware iteration. The separately reported battery-display issue has its own local candidate. Public push, tags and publication remain unapproved.
