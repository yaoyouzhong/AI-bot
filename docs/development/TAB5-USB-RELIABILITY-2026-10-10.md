# TAB5 USB 认证与重连排查 / USB reliability

2026-10-10。完成两项源码修复及本地回归；设备仍运行 `.164`。遵照用户要求，没有构建、打包、提供或安装新固件。原先的间歇性认证错误和重连超时尚未完成修复后真机验收。

## 修复与依据

1. **CDC 接收缓冲并发窗口。** 工程内 TinyUSB 的 `device/usbd.c` 先清除 OUT 端点忙标记，再由 `class/cdc/cdc_device.c` 将 DMA 缓冲复制到 FIFO。应用读取 FIFO 会在 `tusb.c` 尝试重新挂接 OUT。原来应用接收任务未固定核心，可能在另一个核心于复制前重新挂接同一 DMA 缓冲，后续包可覆盖尚未复制的数据。独立 TAB5 工程 `firmware/main/usb_link.c` 与 `usb_task_policy.h` 现在将栈和应用接收任务固定于核心 1，栈优先级 8、接收优先级 6，使非阻塞栈接收回调先完成；未修改第三方组件。此窗口已本地复现，但没有原 17:30 故障时的 DMA 轨迹，不能判为该次 GCM 错误的最终归因。
2. **重连首包未采用已声明的压缩能力。** 设备握手已声明 `usbPacked=1`，桥接此前仍等完整状态确认后才采用，重连先发送约 31 KB 原始数据。`Tab5Service.ReadIdentityAsync` 现在在合法握手后交出能力字段，`Tab5UsbStatus.SupportsPacked` 核对协议、消息类型、目标设备及数字能力值，首次发布即可压缩。旧固件缺少能力时保持原始帧。未改变线协议、增加超时或放宽认证。

现有单次压缩拒绝回退仍发送同一认证帧和序号；身份、序号、未知拒绝以及 I/O、超时、取消不因此重试。

## 验证

| 层级 | 结果及范围 |
| --- | --- |
| Windows 独立 Release 构建 | 通过，零警告、零错误；未替换当前运行桥接 |
| Windows 传输与 RPC 回归 | 通过；能力协商、拒绝回退、认证、重放、分片及取消 |
| 原生 USB 接收并发测试 | 63 种 FIFO 占用场景通过；真实 TinyUSB FIFO 和重挂函数，模拟 DMA/调度；跨核心和同核心同优先级负对照复现覆盖，同核心低优先级接收保持原字节 |
| 另外五项原生测试 | CDC、二进制帧、RPC mailbox、exchange、decode 通过；含旧回复隔离、中途断开、超时、100 次完成竞争、100 次重连竞争、认证失败不交付明文 |
| 当前 .164 实机内存测速 | USB / Wi-Fi / BLE 各三轮上传与下载，18 项全部通过；每项 256 KiB，不写固件闪存 |
| `--status-once` | 未通过：当前启动环境触发既有用户数据目录保护，退出码 2；未绕过保护 |

USB 上传为 550 / 503 / 847 ms，下载为 1111 / 956 / 965 ms。测速期间 RPC 失败计数 `4,1,0,0;0,0,0,0;0,0,0,0`、USB 二进制过期计数 2 及最近 USB 控制失败时间 18:04:48 未增加。这是旧 .164 的可用性采样，不是新修复实机验证，也不覆盖物理拔插或整机启动。

并发夹具最初错误地纳入 FIFO 已满 4096 字节却仍预留一包 OUT 的不可达情况，保留其失败日志；修正为可达的 64 至 4032 字节后，仍保留逐字节及负对照断言。硬件 DMA 与调度为模拟，不能替代硬件压力验证。

## 未关闭的验收项

后续补漏见 [重连补漏与验证](TAB5-USB-RECONNECT-2026-10-10.md)：补齐 Windows RPC 连接代次、文本半帧恢复、原生串口异常，以及设备快速重开时的旧心跳失效。该记录也更新了正常启动下 `--status-once` 和独立桥接实机连接结果；下方为本记录当时的历史验收边界。

后续三次真机复现及序号重放取证见 [指标超时取证](TAB5-USB-METRICS-2026-10-10.md)：最后一次确认了失败指标未被应用，排除该次“已应用但 ACK 丢失”。调度修复仍待未来固件实机验证。

- 收尾再次读取发现 **19:02:55 新增 USB metrics 确认超时**：`receivedBytes=0`、`completeLines=0`、`readMs=4015`。其后的 RPC mailbox 读取因端口关闭报断连，`auth=verified`，设备认证失败计数未增加；链路随后恢复。测速结果保存于 18:58:40，因此这次失败发生在测速结束后，不能说此后一直无错误。串口 gate 在指标等待期间占用，解释了 RPC 跟随断连；最初为何没有 ACK 仍待修复后硬件取证。记录见 `final-live.txt`，未扩大超时或吞掉异常。
- 原一小时观察的 GCM `-18` 及重连 `status_write` 超时仍为失败，不由短测抵消。
- 新固件调度修复尚未进行 ESP-IDF 构建、启动、物理拔插、休眠恢复及持续并发真机验证；遵守暂停新固件的边界。
- 新 Windows 构建尚未替换运行桥接，首包压缩修复尚无真实重连耗时。
- 自动循环展示已恢复。临时自动连接模式尚需在剩余设备验证结束后恢复蓝牙专用偏好。

证据目录：`artifacts/development/usb-reliability-20261010/`。包含 `benchmark-result.txt`、`bridge-build.log`、`bridge-transfer.log`、`bridge-rpc.log`、六项原生测试日志、`rx-fixture-capacity-failed.log`、`status-once.log`。原生测试入口为独立 TAB5 工程 `tests/ui-preview/usb_rx_race_test.c`；本轮 `test-native.ps1` 仅构建电脑原生测试。

## English

The final live read found another metrics ACK timeout at 19:02:55, after the benchmark completed: zero bytes received over 4015 ms. Closing that serial session caused the following mailbox-disconnected report with authentication still verified. Connectivity subsequently recovered, with device authentication failure counters unchanged. The missing ACK is not conclusively attributed; short benchmark success does not establish continued stability.

Two source fixes are locally validated: serialize CDC RX against the higher-priority TinyUSB task on core 1, and negotiate packed status from the validated hello before the first reconnect publication. Actual vendored FIFO/rearm code reproduces DMA-buffer overwrite under simulated interleaving; 63 reachable occupancy cases pass with the scheduling policy. This demonstrates a corruption window, not conclusive attribution of the original hardware GCM failure.

The isolated Windows build, transfer/RPC tests and six native suites pass. All 18 RAM-transfer measurements on unchanged .164 pass without increasing observed failure counters. The failed one-hour observation remains failed. No new firmware was built or installed; the running bridge was not replaced. Status-once was blocked by the existing redirected-profile guard and is not counted as passed. Physical reconnect, sleep/wake and sustained validation of the fixes remain pending.
