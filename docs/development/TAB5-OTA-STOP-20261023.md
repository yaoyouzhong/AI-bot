# Windows 桥接停止 TAB5 升级候选

## 行为

- 未开始时显示“取消选择”；准备/传输中改为“停止升级”。原来的“停止提供”无法中止正在进行的传输。
- 取消与 ready/commit、USB/BLE 分段以及 Wi-Fi 最后一段写入共用状态锁。尚未发出 commit 的请求可立即停止，旧 ready/started 回报失效；已发出 commit 或数据则显示“正在停止”。
- 取消后撤回该 offer，拒绝后续 USB/BLE 范围，取消当前普通/压缩 Wi-Fi 流，且不会通过晚到 HTTP 请求重新开始。沿用固件 `ota_update.c` 的下载失败清理：停止接收任务、`esp_ota_abort`，不改当前启动分区。
- 最后一段数据释放前就锁定不能取消，避免在设备已经校验或切换启动分区时误报停止。页面明确显示原因。
- 已开始的传输必须收到取消之后、同设备/同 host、原运行版本且 `otaDiag` busy=0 的新诊断，才显示“已停止，继续使用原固件”。过期采样、错误主机、版本变化、busy 状态均不能确认；超时保留未确认，禁止重新选择固件。断开/退出不算成功；未确认时以有界请求持续检查，30 秒后降低频率，重连后继续确认。
- 取消完成后重新选择会生成新 offer，旧请求不能继续；保留配对、Wi-Fi、蓝牙和用户展示设置。

## 范围和验证

仅 Windows 源码候选，无新线协议或固件变更；沿用现有已认证的传输和诊断，不改 Mac。没有替换正在运行的 .253 桥接，也没有重刷已核验的 .254。

Release 构建、加密 USB/BLE 分段与 ready 竞态、普通/压缩 HTTP 中途取消、最后一段取消拒绝、旧请求与错误/陈旧确认回归通过。现有 OTA/RPC、完整 .254 镜像压缩往返和 57 项无线诊断回归通过。升级界面按 100/150/175/200% 默认与最小尺寸渲染并检查。`--status-once` 已执行，工具环境被既有资料目录重定向保护拒绝（exit 2）；没有绕过保护。正常桌面驻留桥接和 .254 的现场诊断另有保存。

候选尚未安装；真实设备的中途取消、恢复连接和重新升级尚待验收，模拟传输不等于硬件通过。不得为了测试自动启动额外升级。

## English

The Windows candidate replaces the idle withdrawal caption with “Stop upgrade” during preparation/transmission. Stop, commit acknowledgements and release of the final USB/BLE/HTTP range share one lock. An uncommitted intention stops immediately. After commit or data, withdraw the offer and cancel raw/compressed Wi-Fi streams; subsequent ranges and stale prechecked HTTP responses cannot restart it. The existing firmware download-error cleanup joins reception, aborts the inactive OTA partition and leaves the running boot partition unchanged. No new wire fields or Mac changes are introduced.

Stopping is refused before releasing the final range, because the device may already be verifying/selecting its next boot partition. Started transfers only become confirmed stopped after a fresh post-stop report from the same paired device/host with the original firmware and otaDiag busy=0. Stale, busy, wrong-host and changed-version reports are rejected. Missing confirmation remains visible, retries bounded probes with backoff after 30 seconds, and prevents another offer; a later selection uses a new offer identity.

Release build, authenticated range/commit races, raw/compressed HTTP cancellation, last-range exclusion, stale-request/confirmation checks, existing upgrade/RPC, whole-image compression roundtrips and 57 wireless diagnostic checks passed. Actual WinForms layouts are rendered at four scales and two sizes. The required status-once command was rejected by the unchanged redirected-profile guard (exit 2); it was not bypassed. This is an uninstalled Windows candidate. Hardware cancellation/reconnect/retry is pending, and no additional physical upgrade was started for testing.
