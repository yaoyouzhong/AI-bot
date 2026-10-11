# USB 重连补漏 / USB reconnect follow-up

后续完整纳入检查及已构建的 `.166` 候选见 [验收记录](../TAB5-ACCEPTANCE-166.md)；本记录保留不构建阶段的证据。 / The subsequent authorized .166 build and inclusion audit are tracked in the linked acceptance record; this document preserves the earlier source-only stage.

2026-10-10。设备仍为 `0.2.164-ui`。本轮只修改源码、构建 Windows 桥接及电脑原生测试，没有构建、打包或安装设备固件。

## 新发现和修复

1. **旧 RPC 穿过重连边界。** Windows 在串口锁外执行请求处理；期间端口可以关闭、完成新握手，能力值又恢复为相同值。原 pump 只比较能力及分片大小，旧回复因此可能被发到新连接，造成拒绝或把健康新端口关闭。现在每次关闭推进连接代次，pump 绑定该代次，每一笔事务取得串口锁后再次核对。过期事务不发送、不关闭新端口；新代次重新建立 pump，避免复用旧请求编号缓存。
2. **文本 RPC 失败未丢弃半帧。** 原代码仅二进制路径在失败后关闭端口；文本超时或损坏回复也可能留下半帧。现在两条路径统一在持锁范围内结束失败连接，原异常继续上报；等待锁时取消不关闭他人连接。
3. **原生串口错误遗漏。** RPC 循环原先没有覆盖 `Win32Exception`，可能结束后台重连任务。现在复用状态通道的断线判定；清理端口时的已知原生断线异常也不会击穿工作循环。关闭同时清空最近 USB 确认时间，避免界面继续保留旧“已连接”状态。
4. **设备快速关闭／重开保留旧心跳。** DTR 关闭和重开可能发生在接收任务两次轮询之间；原代次同步只清空解析器和 mailbox，未使旧 USB 心跳失效。现在在接收任务看到代次变化时同步失效心跳，保留会话／序号防重放状态，允许 Wi-Fi 回退，并等待新完整状态建立可用 USB 会话。TinyUSB 回调中仍不获取应用锁。

这些是代码和受控回归确认的缺陷；不能据此将历史 GCM `-18` 唯一归因于重连。本轮没有削弱 HMAC、AES-GCM、会话、时效或序号检查，也没有扩大超时。此前 DMA 覆盖窗口的源码修复及证据见 [指标超时取证](TAB5-USB-METRICS-2026-10-10.md)。

## 验证

- Windows Release 构建：零警告、零错误。
- 真实 `Tab5BleVoice` mailbox pump 加生产串口事务守卫：处理器延迟返回时重连、排队等待时重连、相同请求编号的新连接、超时／格式／原生 I/O／取消失败均通过；旧任务不能写入或关闭新连接，新任务正常完成。
- 传输集成与 RPC 认证回归通过，包括防重放、请求不重复执行、分片顺序、字节完整性与取消。
- 六项电脑原生 USB / RPC 回归通过：包含 63 种接收 FIFO 占用、100 次完成竞争、100 次重连竞争。新增实际 CDC 代次同步逻辑配合实际 `link_state.c` 验证：旧 USB 心跳失效、Wi-Fi 回退、旧序号拒绝及新会话恢复。
- `--status-once`：从工具启动环境直接运行仍被既有目录保护拒绝（退出 2）；按程序指示经资源管理器正常启动，同一独立桥接退出 0，产生有效状态 JSON。未改环境、关闭保护或重新配对。
- 独立 Windows 桥接正常退出／重开三次，真实 COM9 握手与状态确认分别为 **156 / 156 / 203 ms**，三次采样均无 USB 控制或 RPC 中断，设备 RPC 累计失败计数保持 `4,1,0,0;0,0,0,0;0,0,0,0`。这是短时样本，不抵消旧 `.164` 的间歇性故障，也不是物理拔插计时。

证据目录：`artifacts/development/usb-reconnect-20261010/`。`build.log`、`transfer.log`、`rpc.log`、六项原生日志、`status-once-normal.exit` / `.json` 保存完整结果。实际 Windows 启动样本单独保存为 `live-cycle-*.txt`。

## 验收边界

Windows 正常启动／关闭测试仅覆盖 COM 已枚举情况下的握手和状态确认，不能代替物理拔插、睡眠恢复或 TAB5 屏幕可见全程。设备仍运行旧 `.164`；设备调度和心跳修复尚未上机，原 GCM 故障和长期稳定性仍需未来获准构建、安装后验收。不得将本轮本地通过写成“所有真机问题已解决”。

收尾使用既有正常退出／恢复循环／启动入口恢复日常固定 Release；独立诊断程序不留作常驻。Windows 修复保留在源码和独立验证产物中，固定 Release 未替换。展示读回证据为 `restored-display.json` / `restored-runtime.json`。此前临时自动连接模式的蓝牙专用偏好恢复仍按原记录待剩余设备验收收尾处理。

## English

The Windows RPC pump now owns a USB connection generation. Ownership is checked after taking the serial gate, so a delayed handler or queued transaction cannot send to or close a replacement connection. Text and binary transport failures both discard partial streams, native serial errors enter recovery, and closing clears stale USB availability. Device CDC generation changes invalidate the old heartbeat even when close/open occurs between readiness polls, preserving anti-replay state and wireless fallback.

The Windows build, production-pump reconnect regressions, authenticated RPC tests and six native USB/RPC suites pass. Status-once succeeds through normal Explorer startup without bypassing the existing profile guard. No device firmware was built or installed; physical reconnect, sleep/wake and repaired-firmware endurance remain unverified.

Three normal bridge restarts connected to the existing COM9 device in 156, 156 and 203 ms after enumeration, with no new USB interruption in those samples. These short observations do not clear the old firmware's intermittent failures. The existing daily Release is restored after testing; Windows changes remain in source and isolated build outputs.
