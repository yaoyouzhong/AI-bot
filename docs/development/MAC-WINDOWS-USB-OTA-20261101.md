# Mac / Windows USB OTA 对齐 — 2026-11-01

## 定位与修改

先对照 Windows `Tab5UsbRpc.cs`、`Tab5UsbBinary.cs`、`Tab5UsbShortPacket.cs` 与实际 .302 构建源 `usb_link.c` / `usb_binary.h`。Windows 已协商 `rpcUsbBinary=1`、`rpcUsbBinaryChunk=65526`、`usbRxBatch=4096`、`rpcUsbWait=20`；Mac 原来固定 2048 字节 JSON/Base64 逐片往返。48 KiB 批量回复在 Windows 大块通道一次写入，Mac 原实现需二十多次。

Mac 现在复用已有 AIB2 二进制协议，按已识别设备在当前连接的能力选择 65526 / 16384 / 2048 字节。无二进制能力的设备保留 JSON；能力不成立时不能使用大块。64 字节对齐的二进制写入在声明正文外追加换行，和 Windows 一致。校验响应魔数、随机 tag、状态及读写长度；任何错误关闭连接，不在已发布请求后退回 JSON 或重放操作。串口只在原队列读写，等待仍有界。

实际大回复完成后使用 250 ms 的短轮询租约，避免每个范围额外等待 100 ms；空请求、重复 ID、小回复不能续期，语音优先和原状态心跳不变。新增显式 `--test-tab5-usb-link legacy|negotiated <report.json>` 仅进行现有 256 KiB 加密内存上下行，不提供固件、不写 Flash、不切换设备模式或持久设置。

未修改 UI、Windows 源码、设备固件、分区或协议字段；不需要设备重新刷机来启用本次传输优化。未新增 UI，所以本次不涉及布局设计或渲染验收。

## 测速口径

- 固件字节提供完成、设备安装（接收/擦写/校验并行）与发起至健康启动是不同阶段，不相加也不互相替代。
- Windows 历史 .119 USB 安装 19.207 s（6,940,368 字节）；Mac 优化前 .302 记录的是 39.8 s 提供完毕、97.3 s 至精确健康启动（7,207,632 字节）。版本、字节与计时阶段不同，不能据此计算平台速度倍数。
- 最新 Mac 蓝牙短测 .302：256 KiB 上行 22.004 s、下行 20.377 s，完整字节/错误码验证通过，不刷机。与先前 .291 约 20 s 的短测相近，不能作为本次 USB 改善证据。
- Mac 同镜像完整 USB 重装已按用户授权完成，结果见下文；同镜像 Windows 原生复测尚未完成，不能把实现对齐写成实测速率相同。

构建、回归、实际内存对照、部署与完整重装结果见下文。本地证据目录 `artifacts/ota-speed-20261101/`，不包含配对密钥、原始用户内容或认证负载。

## English

The comparison identifies a concrete Mac gap: Windows already negotiates the existing AIB2 binary USB mailbox, up to 65,526-byte payload fragments, 4 KiB receive batching and 20 ms bounded request waits. Mac used fixed 2 KiB JSON/Base64 exchanges. The Mac update follows the existing peer capabilities, retains legacy JSON, validates magic/tag/status/length, uses the same outside-body newline for 64-byte-aligned writes, and closes rather than falling back or replaying after a failed publication. A bounded 250 ms post-bulk polling lease removes per-range timer gaps while preserving voice priority and status heartbeats.

The explicit legacy/negotiated comparison runs authenticated 256 KiB RAM-only samples. It does not offer firmware, write Flash, change device mode, or persist preferences. UI, Windows, firmware, partitions and protocol fields are unchanged. No UI design/render acceptance applies.

Host byte provision, concurrent device installation, and request-through-healthy-boot time are separate metrics. Historical Windows .119 installation (19.207 s, 6,940,368 bytes) and current Mac .302 provision/healthy-boot observations (39.8/97.3 s, 7,207,632 bytes) cannot establish a speed ratio. The current .302 BLE control completes 256 KiB up/down in 22.004/20.377 s; it is separate from USB. The authorized full Mac USB reinstall is complete, as recorded below. Same-image native Windows acceptance remains pending. Build, regression, physical RAM comparison and deployment evidence is under the directory above.

## 已完成验证与部署

最终 Release 构建通过；28,922 项隔离回归、81 项 OTA/真实 socket 检查和实际固件 USB 二进制原生回归通过。`swift test` 实际执行但因本机 CLT 缺少 XCTest 失败，不能记为通过。签名候选和原 Keychain helper 均核验，保持相同 helper 字节。

同一 .302 设备、同一签名候选，通过显式 legacy 开关对照原 2 KiB/100 ms 路径；随后执行两轮协商路径。每轮核对完整 262,144 字节、错误码 0，再等待取消完成；已开始但未完成的下一样本不计入完整样本。这一阶段未刷写设备，后续授权重装单独记录。

| 256 KiB 方向 | Mac legacy | Mac 协商路径 1 | Mac 协商路径 2 | Windows 历史 .164 三样本 |
| --- | ---: | ---: | ---: | --- |
| 设备 → 电脑 | 1701 ms | 648 ms | 530 ms | 550 / 503 / 847 ms |
| 电脑 → 设备 | 1702 ms | 645 ms | 661 ms | 1111 / 956 / 965 ms |

OTA 相关的电脑 → 设备下行吞吐从 150.4 KiB/s 提高至 396.9 / 387.3 KiB/s（2.64 / 2.57 倍，耗时减少 62.1% / 61.2%）。该差异由同机、同固件的新旧路径对照支持。Windows 历史样本来自 [USB 验证记录](TAB5-USB-RELIABILITY-2026-10-10.md)，仅作量级参照；版本、主机和时刻不同，不能宣称当前两端同镜像性能相同，也不能将短时 RAM 倍数直接外推到完整升级。

候选 R49 二进制 SHA-256 `c0c379fd3c1affa84eac1bdd157e08ea252cb770181d91405b0ced2182ffb073` 已从固定路径后台运行，R48 完整包已备份，严格签名、原 helper、唯一进程和串口打开检查通过。当前 latest 镜像仍是 .302，SHA-256 `b913705e04ecdec7cf737f5fc33a8a6c2d65eb3f94c6bde3164bb294eb1b44ca`；后续已按用户明确授权完成一次同镜像 USB 重装，展示设置前后读回一致，未执行轮播恢复。ESP8266 未修改、未刷机，本轮无其真机验收。验收阶段未创建标签或公开发布。

完整 USB 重装验收使用同一 .302 镜像，不新增固件版本；原 RAM 测试、构建与完整重装为不同证据，见下表。

## Completed validation and deployment

The final Release build, 28,922 isolated checks, 81 OTA/socket checks, and the actual firmware's native USB binary tests pass. The attempted full `swift test` fails because this CLT installation lacks XCTest. The signed candidate retains the exact Keychain helper.

On the same .302 device, the explicit legacy control completes 256 KiB up/down in 1701/1702 ms. Two negotiated runs complete their first up/down samples in 648/645 and 530/661 ms. Completed samples verify exact bytes and zero error; subsequent partial samples are excluded. Downlink improves from 150.4 to 396.9/387.3 KiB/s (2.64/2.57 times; 62.1%/61.2% less elapsed time). The historical Windows .164 samples in the table are only a scale reference, not a controlled current-platform comparison or a full-installation claim.

R49, identified above, is installed at the existing fixed path with a complete R48 backup. Strict signing, unchanged helper, sole background process and open serial descriptor pass. The unchanged .302 firmware identity is also verified. The user subsequently authorizes one complete same-image USB reinstall, which passes with matching display settings. The acceptance does not change carousel settings, modify/test ESP8266, create tags or publish a release. Complete USB timing is recorded below, separately from RAM measurements.

## 用户授权后的完整 USB 验收

用户明确确认对象为 TAB5 USB 有线升级，并批准使用当前 .302 镜像完整重装。一次重装完成，无自动重刷。预检脚本最初误将清单的说明正文摘要与 sidecar 文件摘要比较，在退出桥接及设备操作前停止；按正式准备脚本改为核验说明正文后继续，镜像、说明和产品代码未因此改动。

| 阶段 | R48 优化前 | R49 本次 | 耗时减少 |
| --- | ---: | ---: | ---: |
| 发起至提供全部镜像字节 | 39.782 s | 14.802 s | 62.79% |
| 设备完整安装 | 38.587 s | 20.557 s | 46.73% |
| 发起至精确健康启动核验 | 97.321 s | 72.371 s | 25.64% |
| 等待数据 | 18.303 s | 0.174 s | — |
| 分段擦除 | 9.186 s | 9.206 s | — |
| Flash 写入 | 9.767 s | 9.854 s | — |
| 镜像校验 | 0.929 s | 0.934 s | — |

传输、擦写和准备并行，各阶段不相加。提供字节的桥接进度不是设备最终接收确认。两个完整样本提供完全相同的 7,207,632 字节镜像；旧样本运行 .301 安装器，新样本运行 .302。八项 OTA／RPC／USB 接收源文件逐字节相同，记录于 `OTA-RECEIVER-SOURCE-COMPARE.json`；仍不冒充接收端完整二进制完全相同。剩余安装时间以擦写为主，不声称硬件上限或消除健康观察。

本次 .302 精确 ELF、`ota_1`／`VALID`、三个递进实时 UI 样本及升级前后展示设置一致均通过。随后读取数值 RTC 阶段记录，确认 `previousStage=9`、`previousError=0`、`transport=0`、本次完整安装 20557 ms。已恢复固定路径的唯一签名后台桥接，二进制摘要、helper 和串口打开再次核验。`FULL-USB-COMPARISON.json`、`usb-reinstall.json`、`usb-before-install-numeric.json` 与 `usb-after-install-numeric.json` 分别保留比较、启动验收与两个数值阶段快照。

当前 20.557 s 的设备安装已接近 Windows 历史 .119 的 19.207 s 量级，但镜像大小、版本和主机不同，不能宣称当前同镜像 Windows/Mac 完全相同；本轮也不构成长期稳定性验收。UI 无修改，沿用原 .302 实际 sidecar 渲染，真机 USB 安装/启动/设置检查已完成；ESP8266 不在本次验收范围。

## Authorized complete USB acceptance

The user confirms TAB5 wired USB and authorizes one complete reinstall of the existing .302 image. Exactly one reinstall runs, with no automatic repeat. An initial diagnostic-script notes-hash comparison stops before the bridge or device is touched; it is corrected to hash the notes text, matching the official preparation script. Product code and payloads do not change.

For the identical 7,207,632-byte image, host provision falls from 39.782 to 14.802 s, device installation from 38.587 to **20.557 s**, and request-through-exact-healthy-boot verification from 97.321 to **72.371 s**: reductions of 62.79%, 46.73% and 25.64%. Data-ready wait falls from 18.303 to 0.174 s. Erase/write/verify remain approximately 9.2/9.8/0.93 s. Phases overlap and must not be added; host provision is not final receiver acknowledgement. The old installer runs .301 and this one .302; the eight inspected OTA/RPC/USB receiver files match byte-for-byte, while the complete receiver binaries differ.

Exact ELF, ota_1 VALID, three advancing live UI samples and matching before/after display settings pass. Retained numeric diagnostics independently report success, USB transport and 20557 ms installation. The sole signed background bridge at the fixed path, helper and serial descriptor are rechecked. Artifacts listed above preserve each evidence type separately. No UI changes require a new render; the existing exact .302 sidecar preview is retained. No ESP8266 acceptance or long-term reliability claim is made. The new install time is near the historical Windows .119 scale, but a current same-image native Windows comparison remains outstanding.

## 蓝牙实验后的稳定版复核 / Stable-version recheck after BLE experiments

用户选择保留稳定版本后，恢复相同 .302 镜像，固定路径 R49 二进制及全部源摘要保持原样；USB 安装再次测得 **20.516 s**，与此前20.557 s一致，精确 ELF、VALID、三个实时 UI 样本及展示设置保留通过。USB 优化没有撤回。证据见 `artifacts/ble-ota-speed-20261101/restore302.json`、`restore302-after-numeric.json` 和 `EXPERIMENT-RESTORE.json`。

After the user chose the stable version, restoring the identical .302 image with unchanged R49 again measures **20.516 s** installation. Exact ELF, VALID, three advancing UI samples and settings preservation pass. The USB optimization is retained.
