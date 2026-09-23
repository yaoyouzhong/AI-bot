# USB 与 Wi-Fi 回退验收 / USB and Wi-Fi fallback validation

2026-09-08 首轮失败，修复 RX 缓冲后两轮完整 USB 真机测试通过；用户确认大屏保时钟实体屏符合旧版。当时 Wi-Fi 回退未通过，详见 [验收记录](HARDWARE_ACCEPTANCE_2026-09-08.md)。2026-09-23 在设备与笔记本处于可互访的 `yingda-guest` 网络后，Windows 真机 `--test-wifi-fallback` 通过；有线网卡动态切换仍未做实机验收。其他页面视觉未完成，macOS 源码仍需在 Mac 上构建与运行。

The first run failed; two complete USB runs passed after the RX-buffer fix. The user confirmed the large screen-saver clock on hardware. Wi-Fi fallback passed on the reachable `yingda-guest` network on 2026-09-23; live switching to wired Ethernet remains unverified because the adapter has no link. Other pages still need visual alignment; macOS still requires a Mac build and run.

## 保持供电 / Keep power connected

- USB 线同时供电和传数据时，整个测试保持连接。不要关闭 USB 供电或退出正在提供 LAN 状态的桥接程序。
- Keep a combined power/data USB cable connected throughout the test. Do not turn off power or stop the bridge's LAN service.
- 设备先正常配网，使用当前固件通过 USB 握手并接收 `lan_config`。电脑端点必须对设备可达；防火墙或单位网络隔离可能阻止回退。
- Provision Wi-Fi and let current firmware receive `lan_config` over USB. LAN reachability is required; firewall/client isolation can prevent fallback.

## 入口 / Entry points

Windows/macOS 托盘或菜单栏提供设备信息、单独确认的 Wi-Fi 重置和回退测试。管理功能均走 USB，断网也应可用。测试期间管理重置被阻止，不进行自动重试。

Both desktop menus provide USB information, separately confirmed Wi-Fi reset and fallback testing. USB administration must work without LAN access. Reset is blocked during the test and is never automatically retried.

Windows 命令行使用 README 中的 `--test-usb-management`、`--test-wifi-fallback`，先退出托盘程序，再运行一个测试进程。回退 CLI 启动自己的 LAN 服务和 USB worker，并在新监听地址下发后才开始暂停 USB；USB 管理 CLI 无需 LAN 服务，结束后释放资源；不会刷新个人在线账户。串口可用 `AIBOT_PORT` 指定，但先核对实际 CH340 端口。

`--test-wifi-stability-5m` 连续暂停常规 USB 发送至少 312 秒，保持 USB 供电并用只读诊断每 10 秒检查 Wi-Fi 状态计数持续增长、USB 状态计数不变、设备在线且页面未进入缓存/离线显示；每次续期 30 秒安全暂停窗口，结束或失败后恢复 USB 并验证状态计数重新增长。运行前也必须退出托盘桥接。

2026-09-23 实测：312 秒内 LAN 成功计数增加 153，USB 状态计数不变，所有检查点均在线且 `display_cached=false`，恢复后 USB 状态计数重新增长。独立 CLI 不运行托盘内的国产额度在线捕获，因此测试期间 DeepSeek/GLM 额度页会标记 `CACHED - CHECK APP`；这不是 Wi-Fi 传输缓存。恢复托盘后两者的 `stale` 均为 `false`。此测试仍不验证 Wi-Fi 下所有页面的实体屏视觉或独立供电拔线。

双端 DHCP 地址变化的回退另需实测：先确保设备通过 USB 配对，再让设备独立供电并使笔记本和设备都获得新的同网段地址；保持桥接运行，观察设备自动发现后的 LAN 状态计数和页面刷新。旧地址失效时，若路由器隔离客户端或丢弃广播，发现机制无法跨越该网络限制。测试期间不重置 Wi-Fi 凭据；失败时重新接入 USB 可下发当前地址。

2026-09-23 新增双端地址变化模拟验收：Windows 桥接从 `127.0.0.1` 切换到 `127.0.0.2`、模拟设备源地址从 `127.0.0.3` 切换到 `127.0.0.4`，两轮签名 UDP 发现均返回当前桥接地址且 HTTP 监听完成重新绑定。真机 `--test-wifi-discovery` 在保持设备 USB 供电、暂停普通 USB 状态时，把旧的桥接端口设为无效，设备约 10 秒后经新端口收到 LAN 状态；恢复旧端口后的普通 Wi-Fi 回退也通过。这仍不是两端真实 DHCP 同时改址和拔除 USB 数据线的验收。

For Windows CLI tests, exit the tray bridge and run one test process. The fallback CLI starts its own LAN listener and USB worker, waiting for the chosen listener address to be sent before pausing USB; the USB management CLI needs no LAN listener, releases them on exit and does not refresh live accounts. `AIBOT_PORT` can select the actual discovered serial port.

`--test-wifi-stability-5m` keeps normal USB status paused for at least 312 seconds while USB still supplies power. It renews the 30-second safety pause, samples Wi-Fi status progress and display freshness every 10 seconds using read-only USB diagnostics, then resumes USB and checks recovery. Close the tray bridge first.

On 2026-09-23, the 312-second hardware run gained 153 LAN status frames with no USB status frames, remained online with `display_cached=false` at every sample, and recovered USB afterward. The standalone CLI does not refresh domestic quotas through the tray's live capture, so DeepSeek/GLM showed `CACHED - CHECK APP` during the run; both were non-stale after the tray resumed. This does not verify every page's physical appearance or an unplugged device on independent power.

Recovery after both DHCP addresses change needs a separate hardware check: pair once over USB, power the device independently, assign new reachable addresses on the same subnet to both endpoints, and observe LAN counter and page progress after discovery. Client isolation or filtered broadcast can block discovery. Do not reset Wi-Fi credentials for this test; reconnecting USB can restore the current address if recovery fails.

On 2026-09-23, a loopback test changed the simulated bridge from `127.0.0.1` to `127.0.0.2` and the simulated device source from `127.0.0.3` to `127.0.0.4`; signed discovery replies and HTTP rebinding passed. On the real ESP8266, `--test-wifi-discovery` made the saved bridge port unavailable while USB still supplied power and ordinary USB status was paused. LAN status arrived through the new port after about 10 seconds, and normal fallback passed after restoring the old port. This does not yet prove simultaneous real DHCP renumbering or unplugged USB operation.

## 通过条件 / Passing criteria

Windows 新增 `--test-device-pages` 真机链路验收入口（先退出托盘）。它使用合成状态，通过真实 USB 上传天气文字、股票名称、音乐文字与测试封面，轮流切换九种页面并读取设备模式/亮度，测试 USB 超时及恢复，最后恢复原模式与亮度。不重置 Wi-Fi，不替换桌宠资源；传输成功仅代表设备最终 ACK/CRC/落盘检查成功，不代表屏幕视觉或真实账户验证。测试文字/封面留在设备资源缓存中，实际数据更新时会覆盖。

Windows `--test-device-pages` uses synthetic values over real USB: four resource kinds, nine display modes, brightness readback and USB timeout/recovery, followed by mode/brightness restoration. Exit the tray first. It does not reset Wi-Fi or replace pet assets. ACK success is transport/storage validation, not optical or live-account acceptance. Test text/cover resources remain cached until replaced by normal data updates.

Windows `--test-bridge-grace` requires the tray to be closed and a connected device. It pauses status for 12 seconds to verify stale USB and `bridge_online=false` while `page_data.display_cached=true`, extends the test pause past 30 seconds to verify the offline page, then resumes status and checks USB/page recovery. Device information requests do not renew status freshness. This test does not change Wi-Fi credentials or display settings; `rendered_page` is a firmware readback, not an optical screen inspection.

1. 暂停常规 USB 发送后取基线，此时 `usb_active=true`。Pause ordinary USB traffic and sample the baseline while USB status is still fresh.
2. 12 秒后诊断应为 `usb_active=false`、`bridge_online=true`，USB 状态计数不变，LAN 成功计数增加，运行时间未倒退。After 12 seconds, USB is stale, bridge data remains fresh, USB count is unchanged and LAN count has increased without a reboot.
3. 恢复后 `usb_active=true` 且 USB 计数增加。Resume and require fresh USB status plus an increased USB count.
4. 失败、异常或取消均解除暂停；暂停本身另有 30 秒上限。Failure/cancellation clears the pause; a separate 30-second timeout also restores sending.

诊断信息请求不刷新 USB 心跳；该测试模拟状态通道停止，不能冒充拔线、驱动消失或供电中断测试。只有两阶段都通过才输出 `WIFI_FALLBACK_TEST_OK`。`USB_MANAGEMENT_TEST_OK` 仅证明 USB 管理小链路。`USB_MANAGEMENT_SELF_TEST_OK` 来自合成数据，不代表真机通过。

Diagnostics never renew USB status freshness. This is a status-traffic interruption test, not a cable/driver/power-loss test. Success markers distinguish fallback hardware testing, USB management hardware testing and synthetic self-testing.

手动重置 Wi-Fi 必须在准备重新配网时另行执行；应答超时意味着结果未知，先观察设备，不要盲目重试。端口 `8765` 被占用时先释放占用，或为独立 CLI 测试指定 `AIBOT_HTTP_PORT`，由 USB 自动下发。

Run Wi-Fi reset separately only when ready to reprovision. A missing reset ACK means an uncertain result: inspect the device before retrying. Release port 8765 or use `AIBOT_HTTP_PORT` for the standalone CLI test.
