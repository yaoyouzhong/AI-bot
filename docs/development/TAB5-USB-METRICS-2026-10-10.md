# USB 指标零字节超时取证 / USB metrics timeout

2026-10-10。设备保持 `.164`，未构建、打包或安装新固件。使用独立 Windows 桥接进行临时诊断，正常用户配对与数据目录核验通过。

## 真机证据

原桥接在 19:22:50 又记录一次 `tab5_metrics_ack` 零字节超时。新增诊断在持有原串口 gate 时，用现有 `tab5_ping` / `usbDiag` 读取指标发送前后计数；失败仍保留，后续探测不算原请求成功。普通运行不开启诊断，不增加额外探测或延长原 4 秒期限。

提高诊断请求频率后，同样的故障在 19:45:48、19:46:33、19:50:43 再现：

| 时刻 | 指标线长 | 设备接收字节增量 | 行数增量 | 原确认 |
| --- | ---: | ---: | ---: | --- |
| 19:45:48 | 1627 | 1667 | 2 | 4016 ms，零字节 |
| 19:46:33 | 2366 | 2406 | 2 | 4015 ms，零字节 |
| 19:50:43 | 2496 | 2536 | 2 | 4016 ms，零字节 |

每次增量恰好为指标线长加 40 字节的后续探测命令，行数为指标与探测各一行；前后 CDC epoch 相同，无换行溢出、二进制过期或发送不完整计数增加。探测立即收到 hello，不需要补换行。

19:50:43 的进一步验证：在同一串口 gate 内、没有插入另一条新指标的情况下，**原样重放上一条已确认的指标，设备返回该旧序号的成功确认**。实际 `device.c` 为各通道独立保存指标序号：相等则确认而不重复应用，较旧则拒绝。因此本次失败指标没有被应用；不是该指标已经应用、只有 ACK 丢失。重放仅用于显式诊断，不用于生产自动恢复，不重放控制命令，不改变凭据或设备配置。

## 根因判断与修复

真机证据将同类故障定位到设备收包后、指标应用之前。正常身份／会话／序号拒绝会发送拒绝确认，而本次完全没有返回；外层 JSON / type 分派失败会静默丢弃该行。

上一轮已发现的 TinyUSB 并发缺陷与这一故障路径吻合：栈先清除 OUT busy，再把 DMA 缓冲复制到 FIFO；另一核心的应用读取可能提前重新挂接同一 DMA 缓冲，让下一包覆盖前一包。接收字节总数可能保持不变，但 JSON 内容损坏，无法进入指标分支，随后电脑端等待 4 秒超时。

独立 TAB5 工程的修复仍为 `firmware/main/usb_task_policy.h` / `usb_link.c`：栈及 CDC 接收任务同在核心 1，优先级分别为 8 / 6。当前 native 回归新增真实协议形状的指标帧：真实 TinyUSB FIFO / 重挂函数经过覆盖后，保持字节数量却无法通过 cJSON 分派；按修复调度则逐字节相同且可分派。63 种 FIFO 占用、跨核心与同优先级负对照均保留。

没有抓到原 19:02:55 的逐字节线缆或 DMA 内容，因此不能声称重建了该历史事件。当前证据为三次同类真机故障、一次序号判别，以及源代码缺陷与本地因果复现；新调度修复尚未在硬件运行。不能据此写成“修复后真机稳定性通过”。

## 验证与收尾

- 独立 Windows Release 构建零警告、零错误；指标调度、传输集成回归通过。
- 六项原生 USB / RPC 回归通过；另运行实际固件 telemetry 接收逻辑，验证旧序号拒绝、相同序号确认及会话隔离。首次运行缺少 delta 夹具，保留失败日志，补入同次桥接生成的夹具后按原断言通过。
- 所有运行样本及故障均保留在 `artifacts/development/usb-metrics-20261010/`。重点文件为 `confirmed-metrics-failure.json`、`sampling-summary.json`、三份 `metrics-*.jsonl`、`usb-rx-race-test.log`、`telemetry-native.log`、`telemetry-test.log`、`transfer-test.log`。
- 用户通过 Esc 停止 Computer Use 后，没有继续桌面自动操作。结束诊断压力运行，使用既有正常退出、恢复循环和启动入口恢复固定 Release 桥接；实际读回另存 `restored-display.json` / `restored-runtime.json`。
- 诊断入口为 `--tab5-usb-metrics-diagnostic <本地日志路径> [25..250 毫秒]`。它只记录数字计数、长度、时刻及判别结果，不记录指标内容、密钥或会话值。仅显式开启时追加握手和失败取证；常规启动保持原 250 ms 指标调度。

## English

Three zero-byte metrics ACK timeouts reproduced on unchanged .164. Device byte and line counters account for each complete metrics line and the subsequent 40-byte ping, with no CDC generation change. On the final failure, an exact replay of the previous acknowledged metrics was accepted at its old sequence under the same serial gate. The production receiver rejects older sequences after applying a newer metric, so the failed metric was not applied; an ACK lost after successful application is excluded for this capture.

This corroborates the device receive/dispatch failure path reproduced with the vendored TinyUSB FIFO/rearm code: cross-core reuse overwrites a DMA packet without necessarily changing the total byte count, causing JSON dispatch to fail without an ACK. The same-core, lower-priority CDC receiver source fix preserves exact bytes in native tests. Original-event wire contents and repaired-firmware hardware behavior remain unobserved. No new firmware was built. Diagnostic pressure testing was stopped and the existing Release bridge restored after the user stopped Computer Use.
