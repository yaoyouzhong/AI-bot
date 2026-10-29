# Mac TAB5 BLE OTA scheduling — 2026-10-29

## 调度对照 / Scheduling

本次仅修改 Mac 桥接与本地验收工具，不改变 .288 镜像或线协议。参考当前 Windows 生产实现 `Tab5BleClient`、`Tab5BleGattMailbox`、`Tab5BleMailboxWindow` 和固件 `rpc_mailbox.inc`，未启用 Windows 的实验性 queued comparison。

Only the Mac bridge and its explicit local acceptance runner change. The existing .288 image and wire protocol remain identical. The reference is the current Windows production path, not its optional queued-comparison probe.

| Path | Before (Mac R18) | Candidate (Mac R19) / Windows reference |
|---|---|---|
| RPC small response | Four commands, one ATT response | Negotiated regular window, maximum 8 |
| RPC bulk response (>12288 bytes) | Four commands, one ATT response | `rpcWriteWindow` 8–64, bounded by peer offer |
| Bulk peer with `rpcReplay:1` | Reset on rejected cursor | Ordered command window, final ATT response; only ATT 0x0e replays the identical window once using confirmed writes |
| Bulk peer without replay | Mixed commands | Every fragment confirmed |
| Successful RPC continuation | 20 ms gap | Immediate next poll; idle/duplicate polls within the one-second activity lease use 15 ms, without renewing the lease |
| Bulk status deferral | 4 s frame interval bound | 1 s after completed bulk traffic, at most 6 s since last status frame |
| Native backpressure | CoreBluetooth readiness | Retained `canSendWriteWithoutResponse` / readiness callback |
| Connection parameters | OS managed | OS managed; no public Mac equivalent of Windows ThroughputOptimized request |

补发只使用内存中已生成的同一组密文，不重新执行 RPC、不创建新的请求 nonce，不跨连接或配对世代重试。第二次窗口失败、认证拒绝、断连、取消仍退出。最终完整密文验证与镜像 SHA/ELF 校验不变。

Recovery sends only the already-generated immutable ciphertext. It does not re-execute an RPC, allocate a request nonce, or retry across connection/pairing generations. A second failure, authentication rejection, disconnect or cancellation still fails. Whole-response authentication and image SHA/ELF validation remain required.

## 验收 / Acceptance

固定镜像 / Exact image: `artifacts/firmware/tab5/latest/aibot_tab5.bin`, 7207488 bytes.

SHA-256: `7b6a2e30956fddde7f774de2f3d45f060d95a32d6b1b07eefeb82e63c65176d0`

ELF SHA-256: `8fb43ec9296abb22845bf92477aa9c661f9068e9fde2632316a542ffe7b115a4`

`--test-tab5-upgrade ble` starts only the production BLE transport using the existing pairing. USB and the HTTP listener/discovery remain stopped. Read-only USB diagnostics can observe the device without claiming the USB transport. Acceptance requires automatic BLE boot verification, exact ELF, VALID partition, three live UI samples and identical display settings.

本轮完成旧调度与候选各一次完整蓝牙升级，结果见下表；不据此宣称长期稳定性。

One complete baseline and one complete candidate BLE reinstall passed. This is not a long-term reliability claim.

The .288 peripheral already requests 7.5 ms, data length 251 and 2M PHY during bulk traffic. These are requests, not proof that macOS accepted them. The existing `bleRadio` diagnostic requires USB ownership; a read-only attempt without claiming USB is rejected. This BLE-only acceptance does not claim actual interval/PHY measurements.

.288 设备在批量传输时已请求 7.5 ms、251 字节数据长度和 2M PHY，但不能据此声称 Mac 已接受。现有 `bleRadio` 诊断需要 USB 归属认证；本轮未取得 USB 控制权，因此不记录实际 interval／PHY。


### R18 baseline, real .288 BLE reinstall

The unchanged R18 transport plus a BLE-only acceptance entry completed successfully. Device `transport=2`, install 959196 ms, prepare 9188 ms, write 9647 ms, data-ready wait 939044 ms, verify 926 ms. From request through healthy boot verification: 1022.945 s. The first healthy-boot check was not yet satisfied; the next read-only check passed. Exact ELF, ota_0/VALID, three live samples and unchanged display settings passed. No automatic reinstall retry occurred. An earlier acceptance preflight exited before an OTA began; that attempt has no install timing and its cause was not established.

旧调度基线完整蓝牙重装通过：设备安装 959.196 秒，其中等待数据 939.044 秒；从发起到健康启动核验 1022.945 秒。第一次健康核验尚未满足，随后只读复核通过，并未重新安装。最终 ota_0／VALID、精确 ELF、三个实时样本和设置保留均通过。更早一次验收准备检查在升级开始前退出，未计入安装耗时，其原因未确定。

Evidence: `artifacts/mac-voice-source-20261029/ble-scheduling/baseline/{connection-run.log,reinstall.json,diagnostic.json}`.


### Observed macOS radio request rejection

During the R19 BLE-only run, the system `bluetoothd` connection log reports `Rejecting following parameters: min=6, max=6` and `The min requested is lower that the desired min: 6 < 15.000000`. This matches the .288 peripheral's 7.5 ms request. The OS has therefore rejected that request; these lines do not expose the actual accepted interval or PHY and do not alone quantify its contribution to total time. They are retained in `candidate/radio-rejections.log`. No global Bluetooth settings or private APIs were used.

R19 测试期间，系统日志明确拒绝 min=6/max=6 的请求，与 .288 的 7.5 ms 请求对应；实际采用的 interval／PHY 未获测量，不能单凭拒绝日志量化其耗时占比。未修改系统蓝牙设置，也未调用私有接口。


## Final R19 acceptance / 最终结果

| Device phase | R18 baseline | R19 candidate |
|---|---:|---:|
| Installation / 完整安装 | 959.196 s | 836.485 s |
| Prepare / 分段擦除 | 9.188 s | 9.135 s |
| Write / 写入 | 9.647 s | 9.624 s |
| Wait for data / 等待数据 | 939.044 s | 816.389 s |
| Verify / 镜像校验 | 0.926 s | 0.936 s |
| Request through healthy boot / 发起至健康核验 | 1022.945 s | 902.337 s |

同一 SHA-256 的 .288 镜像，设备两次均记录 `transport=2`。完整安装减少 **122.711 秒（12.79%）**。候选最终为 ota_1／VALID，精确 ELF、三个实时 UI 样本、显示设置保留通过。候选在传输期间无记录到重连；最后一块提供完成后正常重启并重新认证。首次健康核验未满足，下一次只读核验通过，没有重刷。

Both device records report BLE (`transport=2`) using the identical image. Installation improves by **122.711 s / 12.79%**. Candidate ends on ota_1/VALID with exact ELF, three live UI samples and unchanged display settings. No reconnect was observed during transfer; the reconnect after the last block belongs to the expected reboot. A second read-only health check succeeds without another installation.

Candidate transfer counters (whole acceptance session): 11117 submitted fragments, 511 confirmed fragments, 143 large replies / 4894305 encrypted response bytes, attribute write maximum 488, negotiated bulk window 64, **0 replayed windows, 0 write failures**, and **10223 CoreBluetooth backpressure pauses**. These pauses count readiness waits; they do not measure their duration or radio/link-layer retransmissions. The remaining speed limitation is therefore not observed application-level response replay, but its precise controller/radio contribution remains unmeasured. The OS rejection described above is a further observed limit. The same .288 image cannot be used to test a changed peripheral connection-parameter policy.

候选完整会话统计：11117 次片段提交、511 次带确认写、143 个大回复（密文 4894305 字节），最大属性写入长度 488、批量窗口 64，**0 次窗口补发、0 次写入失败、10223 次 CoreBluetooth 背压等待**。背压统计不是耗时或空中链路重传计数。剩余慢速不能归因于本轮桥接回复补发，但控制器／射频的精确耗时占比尚未分离；系统拒绝低间隔请求是另一个已观察到的限制。下一步若验证设备端不同协商策略，就需要另一份固件，不能把它算作本轮相同 .288 的结果。

Validation: candidate and full-workspace Release builds, 28455 isolated checks (including lost-fragment/window recovery), 81 HTTP/socket checks and 16 HTTP listener recovery checks pass. `swift test` was attempted and remains unavailable because installed CLT omits XCTest. No Windows source or firmware changes in this turn; no native same-image Windows benchmark or ESP8266 hardware acceptance is claimed. UI sources are unchanged from the accepted R18 snapshot; unrelated workspace window edits are not packaged.

验证：候选和完整工作区 Release、28455 项隔离检查、81 项 HTTP／socket、16 项监听恢复通过；已执行 `swift test`，但本机 CLT 缺 XCTest，未记为通过。本次未改 Windows 或固件，也未记录 Windows 同镜像原生实测或 ESP8266 实机通过。候选保留已验收 R18 的界面源文件，不带入工作区无关窗口修改。

R19 binary SHA-256: `1e87b733c990d78f24bf88fc006c6de236ac7bc308bd761ecf3eda571ede2e83`.

Installed at the existing fixed application path; R18 backup retained under `ble-scheduling/previous-installed-R18.app`. Normal UI confirms .288 with USB data, authenticated Wi-Fi and authenticated BLE standby. The inspection window is closed, with the bridge running in the background. No push, tag or public release.

已安装到原应用路径，保留 R18 备份；正常界面确认 .288、USB 数据、Wi-Fi 认证请求与蓝牙认证待命，检查窗口已关闭，桥接继续后台运行。未推送、打标签或公开发布。

Evidence: `artifacts/mac-voice-source-20261029/ble-scheduling/FINAL-ACCEPTANCE.json`, `candidate/{reinstall-1.log,reinstall-1.json,diagnostic-1.json,PACKAGE.json}`.


## R20 root-cause measurement and .289 negotiation candidate

The R19 sender is instrumented with monotonic duration counters for native backpressure and confirmed-write waits; pause counts are not elapsed time. An authenticated, read-only USB control channel starts/stops the existing synthetic BLE benchmark without publishing USB heartbeats. The data channel remains BLE; this probe never opens an OTA writer. Cancellation after the first completed down sample is intentional and drains the worker.

On unchanged .288, 262,144-byte up/down samples took 42.173/41.257 seconds. Holding a public ProcessInfo activity produced 42.473/42.517 seconds, so App Nap is not supported as the cause in this comparison. Actual radio diagnostics showed 30 ms, latency zero, 251-byte TX/RX lengths. Both directions were slow without Flash writes. The normal probe accumulated 19.374 seconds of native backpressure waits and 21.266 seconds of confirmed-write waits (including control traffic); these are not separate over-the-air transfer measurements.

The peripheral requested only fixed 7.5 ms. Apple bluetoothd rejected that request with connection-parameter error 0x3b. Windows 11 separately requests ThroughputOptimized from WinRT; public CoreBluetooth central APIs provide no equivalent. Candidate .289 preserves the Windows-compatible first request, then tries fixed 15 and 30 ms after rejection. Procedure collisions (0x2a) retry the same profile after a delay; total attempts are bounded at six. A pending NimBLE procedure owns its 40-second timeout, preventing overlapping updates. Accepted parameters stop retries. Idle restores the parameters sampled at bulk entry, and disconnect disables retries.

候选 .289 已通过 IDF 构建和原实现的模拟测试，Wi-Fi 实机安装为 21.703 秒；精确 ELF、VALID、三个实时界面样本和显示设置保留通过。蓝牙首轮短测实际协商到 15 ms、零延迟，256 KiB 上行 23.249 秒、下行 20.511 秒，零写入失败／零重放。参数回退对提速的效果已实测，完整蓝牙重装和分片对照仍在进行，尚不宣称达到 Windows 的升级速度。

Evidence: `artifacts/mac-voice-source-20261029/ble-root-cause/`, `.289` source/build/tests/first-install under `artifacts/mac-voice-source-20261029/tab5-289/`; source provenance patch is `docs/proposals/tab5-ble-interval-fallback.patch`. This changes no wire format, pairing, display preferences, or Windows sender policy. No Windows hardware throughput comparison was performed on this Mac.


The 244-byte single-link-layer-packet comparison did not improve throughput: .289 up/down 21.102/21.575 seconds versus default 488-byte down 20.511 seconds. Keep the default 488-byte strategy. OS logs show acceptance of fixed 15 ms/latency zero and later acceptance of restoration to the original 15 ms/latency 22 idle policy. Concurrent procedure collisions resolved before successful negotiation; zero replay/write errors were observed in both short probes.

The first .289 BLE full-install attempt failed before its first image block: current device trace stage 10, offset 0, error -1, prepare/write 0 ms, install 8 ms. The active .289 ota_0 partition remained VALID with live UI. The underlying reason is **not established**: the firmware maps first-block failures to ESP_FAIL, so that trace cannot distinguish ownership withdrawal, allocation or transport failure. The CLI was terminated only after confirming this terminal state. A separate manually initiated attempt is in progress; it must not erase this failure from the stability assessment. USB console filtering returned no matching OTA printf lines and supplies no evidence about the underlying error.

244 字节分片没有提速，因此保留默认 488 字节。首轮完整蓝牙升级在首块接收前退出，未开始擦写，原分区正常；底层失败原因未确定，不能用随后一轮成功来宣称问题已经全部根治。


### .289 full BLE result and .290 follow-up

The second explicitly initiated .289 BLE reinstall passed: device install **413.770 seconds**, data-ready wait 393.609 seconds, prepare 9.152 seconds, write 9.677 seconds, verify 0.936 seconds. Including reboot and exact healthy-boot verification: **468.350 seconds**. There were 10,809 attribute submissions, 410 confirmations, zero response-window replays and zero write failures. Native backpressure wait accumulated 178.778 seconds, confirmed-write wait 234.146 seconds, including setup/control traffic. Settings were unchanged. The only recorded disconnect followed completion of all image ranges and the device reboot. This is approximately half the preceding .288/R19 install time (836.485 seconds), with a 400-byte image-size difference; it is not an identical-image comparison across versions.

第二轮 .289 完整蓝牙重装通过：安装 413.770 秒，含重启／健康核验 468.350 秒；零窗口重放、零写入失败，设置保留。与 .288 的 836.485 秒相比安装时间减少约 50.5%，但两版镜像不同（差 400 字节），且首轮无首块失败仍未明确根因，不能宣称全面稳定性已解决。

Final .290 follow-up corrects the unused third interval fallback to 30–45 ms and uses a six-second supervision timeout for Apple-compatible profiles, preserving the initial Windows request. Apple permits fixed 15 ms but otherwise requires a range of at least 15 ms; see [Apple QA1931](https://developer.apple.com/library/archive/qa/qa1931/index.html) and [Apple DTS clarification](https://developer.apple.com/forums/thread/822187). It also retains the first block's negative cause in the existing numeric diagnostic instead of overwriting it with generic ESP_FAIL. Three injected authentication/ownership/integrity failures prove the exact error is preserved and no Flash operation starts. Existing OTA fault/recovery tests pass. This follow-up is not yet hardware-accepted.


### Final deployed R20 / .290 acceptance

The final .290 image is `8622f23e26562946f699b2bea7ba0a0dad05dfc1bb072354600a5194bc5b66df`, 7,207,888 bytes, ELF `732956884fd32d92a28d111e677437bdc3d90c95784b22a4509eb46678d0da1c`. Wi-Fi install: 22.673 seconds. The subsequent normal short BLE probe completed equal 262,144-byte up/down samples in 20.436/20.406 seconds, actual 15 ms/latency zero/251-byte TX/RX, with the hardened result validator confirming completed, error-free samples rather than merely a stopped worker.

An immediately following, explicitly initiated reinstall of the **same .290 image** over BLE passed: install **418.138 seconds**, prepare 9.122, write 9.644, ready wait 398.037, verify 0.933 seconds. Through exact healthy boot: **474.011 seconds**. 10,818 submitted attributes, 412 confirmations, **zero write failures and zero immutable-window replays**. Accumulated native backpressure waits: 178.254 seconds; confirmed-write waits: 240.674 seconds, including setup/control traffic. Three fresh UI samples, exact ELF, VALID ota_1 and unchanged display settings passed. Compare against R19/.288: install time decreases **50.01%**, but the firmware bytes differ; the identical-image claim applies only to the Wi-Fi and BLE .290 runs.

最终已部署 Mac R20 与 TAB5 .290。相同 .290 镜像 Wi-Fi 安装 22.673 秒、BLE 重装 418.138 秒；蓝牙含健康启动核验 474.011 秒。精确镜像／VALID／三个实时样本／设置保留通过；零写入失败与零窗口重放。安装比此前 .288/R19 减少 50.01%，仍是分钟级，未达到十几秒。此前 .289 首轮首块前失败原因未确定；.290 重复“短测后立即完整重装”没有复现，不因此抹去失败记录或宣称全面根治。

R20 binary `f94880798d50d0e9952ea5168df3966b1bcc81a769632e14e576a2e994061bfb` replaced the fixed app after backup; strict signature and unchanged Keychain helper verified. Native UI confirms .290, USB data, authenticated Wi-Fi and BLE USB-priority standby; the inspection window was closed and PID 7311 remained running. Unrelated window edits were excluded from the frozen candidate. Candidate and workspace Release builds, **28,464 isolated checks**, P4 build with identical configuration, negotiation tests, OTA fault/recovery matrix and three first-block failure tests pass. `swift test` was attempted and remains unavailable because the local CLT lacks XCTest. No Windows or ESP8266 hardware test is claimed. No push, tag or public release.

Machine-readable summary: `artifacts/mac-voice-source-20261029/ble-root-cause/FINAL-ACCEPTANCE.json`. First failure: `artifacts/mac-voice-source-20261029/tab5-289/BLE-FIRST-FAILURE.json`. The terminal diagnostic now retains negative first-block causes; it cannot retrospectively recover the cause lost in the earlier generic ESP_FAIL record.


## R21 / TAB5 .291 accepted locally

The .290 short confirmed-write probe failed with `-1011` during its first upload. This status is `RPC_STATUS_UNPUBLISHED_BUSY`: the receiver's shared mailbox was occupied and no request was published. OTA reads and the independently scheduled `ota-control` started acknowledgement had zero mailbox admission wait; OTA recovery did not recognize this result. This establishes a concrete first-block exit path. The historical .289 failure only retained generic `-1`, so it cannot retrospectively identify the exact competing request.

The actual production exchange is exercised with a preoccupied mailbox: zero admission returns `-1011` without publication, while bounded admission sends exactly once after the owner drains. The actual OTA task tests cover a first-block busy response recovering, persistent busy exhausting its retry budget before erase/write, and authentication/ownership/integrity errors remaining terminal. .291 gives OTA/OTA-control two-second admission within their existing total deadlines, boot verification one second, and admits only immutable OTA reads to bounded recovery from `-1011`.

A separate throughput difference exists in radio policy. Windows' earlier isolated BLE measurements paused Wi-Fi; the Mac acceptance runs used automatic mode, where the device excluded the existing bulk radio lease. `rpcOtaBleRadioLease=1` now permits the same worker-owned pause for authenticated, immutable BLE OTA ranges in automatic mode, with timeout/idle/error/disconnect restoration and no saved mode changes. Windows and Mac advertise the same optional capability. The same .291 image will be measured with R20 (capability absent) and R21 (present).

The .290 delayed-started-response run completed installation in 407.975 seconds and booted the exact ELF in a VALID partition. Its BLE three-live-sample check exhausted retries; it is not recorded as full acceptance. No first-block exit was reproduced in that full run. The R21 diagnostic releases its read-only USB polling before selected-transport boot validation and records terminal automatic verification failure instead of waiting for the overall test deadline.

Build limitations: local `swift test` cannot load XCTest from Command Line Tools; isolated release parity validation passes 28,464 checks. The Windows compile reaches the packaging target, which fails because this macOS host produces no Windows helper EXE; `--status-once` cannot run WindowsDesktop on macOS. No Windows runtime/hardware acceptance is claimed.


API cross-check: the implementation waits for CoreBluetooth's [`canSendWriteWithoutResponse`](https://developer.apple.com/documentation/corebluetooth/cbperipheral/cansendwritewithoutresponse) readiness callback and never ignores native backpressure. Windows additionally requests [`ThroughputOptimized`](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.bluetoothlepreferredconnectionparameters), while the device negotiates intervals on both platforms. Neither these API descriptions nor the present measurements establish a hardware throughput ceiling. The latest checked local Windows recovery report (October 14, .194) measured 18.857 KiB/s aggregate BLE download with radio isolation, distinct from old faster preflight builds and Wi-Fi/USB timing.


### Same-image hardware results

All three full BLE runs use the **same 7,208,416-byte .291 image**, SHA-256 `c159109f154b56e9d9bed2917cc8e4f2a2786fcef54a700d0a6fbf3612b33234`. Exact ELF, VALID partition, three fresh UI samples and unchanged display settings pass. Firmware-data transport is BLE (device phase transport=2); diagnostic USB traffic has no status heartbeat or image data.

| Run | Device installation | Request through healthy boot | Ready-buffer wait | Erase / write / verify |
|---|---:|---:|---:|---:|
| R20, radio lease absent | 399.979 s | 452.618 s | 379.813 s | 9.182 / 9.660 / 0.932 s |
| R21, radio lease enabled | 338.938 s | 390.904 s | 318.814 s | 9.131 / 9.655 / 0.941 s |
| R21 repeat, delayed started acknowledgement | 340.052 s | 392.691 s | 319.906 s | 9.168 / 9.666 / 0.927 s |

Installation time falls **15.26% / 14.98%**. The first run saves 61.041 seconds, of which 60.999 seconds is ready-buffer wait; flash timings are essentially unchanged. Both R21 runs have zero response-window replays and zero write failures. Numeric isolation trace stays STOPPED during transfer and finishes restored without expiration: `1,0,0,0,338483,1,0` and `1,0,0,0,339601,1,0`. The R20 baseline explicitly reports BLE active, Wi-Fi connected and isolation idle.

Short synthetic 256 KiB download controls on .291 (ordinary AUTO radio policy retained): normal 20.856 s, yield after eight submissions 20.286 s, confirmed-every-fragment 30.742 s. One sample per mode does not establish a repeatable benefit for yielding; confirmed writes are materially slower. Production therefore retains CoreBluetooth readiness handling and the existing 64-fragment acknowledged windows. Diagnostic modes require explicit CLI invocation and do not persist preferences.

The first .290 confirmed probe's `-1011` and deterministic production-source tests establish a real mailbox collision exit path. They do **not** recover the missing error code from the historical .289 generic failure. .291 baseline and both R21 full reinstalls have no pre-first-block exit. Repeated hardware success is not proof that every future interruption is eliminated.

Remaining limitation: installation still takes about **5 minutes 40 seconds** over BLE. Native backpressure and acknowledged-window completion dominate the sender's waits; their counters include setup/control and must not be added to device phase timings. No HCI/actual PHY numeric trace is available from the existing public system log; PacketLogger is not installed. No hardware-speed ceiling is inferred, and no same-image Windows hardware run was possible on this Mac. The Windows source capability is aligned but Windows runtime validation remains pending.

R21 (`96b4832a23ebd943ba0d2acf2e3ffe91491dc8695b1ce0e4d3147fffc0da83e9`) is installed at the existing fixed app path with a complete R20 backup. Signature and unchanged Keychain helper verified. GUI confirms .291, USB data, authenticated Wi-Fi and authenticated BLE in standby; window closed and background process retained. Evidence: `artifacts/mac-voice-source-20261029/ble-remaining/FINAL-ACCEPTANCE.json`, `tab5-291/`, and `docs/proposals/tab5-ota-admission-radio-lease.{patch,json}`. No push, tag or public release.
