# USB 与 Wi-Fi 回退验收 / USB and Wi-Fi fallback validation

新版本的真机结果尚未记录。旧版本验收是功能基线，不代替本版本回归。macOS 源码仍需在 Mac 上构建与运行。

No new hardware result is claimed. Previous-version acceptance is the behavior baseline; macOS still requires a Mac build and run.

## 保持供电 / Keep power connected

- USB 线同时供电和传数据时，整个测试保持连接。不要关闭 USB 供电或退出正在提供 LAN 状态的桥接程序。
- Keep a combined power/data USB cable connected throughout the test. Do not turn off power or stop the bridge's LAN service.
- 设备先正常配网，使用当前固件通过 USB 握手并接收 `lan_config`。电脑端点必须对设备可达；防火墙或单位网络隔离可能阻止回退。
- Provision Wi-Fi and let current firmware receive `lan_config` over USB. LAN reachability is required; firewall/client isolation can prevent fallback.

## 入口 / Entry points

Windows/macOS 托盘或菜单栏提供设备信息、单独确认的 Wi-Fi 重置和回退测试。管理功能均走 USB，断网也应可用。测试期间管理重置被阻止，不进行自动重试。

Both desktop menus provide USB information, separately confirmed Wi-Fi reset and fallback testing. USB administration must work without LAN access. Reset is blocked during the test and is never automatically retried.

Windows 命令行使用 README 中的 `--test-usb-management`、`--test-wifi-fallback`，先退出托盘程序，再运行一个测试进程。回退 CLI 启动自己的 LAN 服务和 USB worker；USB 管理 CLI 无需 LAN 服务，结束后释放资源；不会刷新个人在线账户。串口可用 `AIBOT_PORT` 指定，但先核对实际 CH340 端口。

For Windows CLI tests, exit the tray bridge and run one test process. The fallback CLI starts its own LAN listener and USB worker; the USB management CLI needs no LAN listener, releases them on exit and does not refresh live accounts. `AIBOT_PORT` can select the actual discovered serial port.

## 通过条件 / Passing criteria

1. 暂停常规 USB 发送后取基线，此时 `usb_active=true`。Pause ordinary USB traffic and sample the baseline while USB status is still fresh.
2. 12 秒后诊断应为 `usb_active=false`、`bridge_online=true`，USB 状态计数不变，LAN 成功计数增加，运行时间未倒退。After 12 seconds, USB is stale, bridge data remains fresh, USB count is unchanged and LAN count has increased without a reboot.
3. 恢复后 `usb_active=true` 且 USB 计数增加。Resume and require fresh USB status plus an increased USB count.
4. 失败、异常或取消均解除暂停；暂停本身另有 30 秒上限。Failure/cancellation clears the pause; a separate 30-second timeout also restores sending.

诊断信息请求不刷新 USB 心跳；该测试模拟状态通道停止，不能冒充拔线、驱动消失或供电中断测试。只有两阶段都通过才输出 `WIFI_FALLBACK_TEST_OK`。`USB_MANAGEMENT_TEST_OK` 仅证明 USB 管理小链路。`USB_MANAGEMENT_SELF_TEST_OK` 来自合成数据，不代表真机通过。

Diagnostics never renew USB status freshness. This is a status-traffic interruption test, not a cable/driver/power-loss test. Success markers distinguish fallback hardware testing, USB management hardware testing and synthetic self-testing.

手动重置 Wi-Fi 必须在准备重新配网时另行执行；应答超时意味着结果未知，先观察设备，不要盲目重试。端口 `8765` 被占用时先释放占用，或为独立 CLI 测试指定 `AIBOT_HTTP_PORT`，由 USB 自动下发。

Run Wi-Fi reset separately only when ready to reprovision. A missing reset ACK means an uncertain result: inspect the device before retrying. Release port 8765 or use `AIBOT_HTTP_PORT` for the standalone CLI test.
