# ESP8266 PC OFF：监控详情导致心跳超限

2026-10-14，本机 Windows 桥接仍运行、HTTP 状态可读，ESP8266 在 COM7 正常回复 ping，但无法持续接收状态。驻留进程的 `SerialPublisher.LastConnectionError` 明确记录状态帧超过 6144 字节；现场样本为 7413 字节，去掉 `runtimeHealth` 后为 3292 字节。

## 修复范围

`DeviceStatusFrame.Create` 从 ESP8266 的 USB 心跳中排除 `runtimeHealth`。固件不消费这部分监控详情；共享快照、桌面和 TAB5 继续保留完整数据。6144 字节上限和超限拒绝机制保持有效，未修改固件、组件版本或用户设置。

回归检查使用超过限额的监控快照，验证 ESP8266 心跳与无监控详情时一致、保留系统数据和循环策略，并确认 TAB5 仍收到完整详情。

## 验证与本机部署

- Release 候选构建通过，0 警告、0 错误。
- `ESP_HEALTH_HEARTBEAT_SELF_TEST_OK`、`RUNTIME_HEALTH_SELF_TEST_OK` 和天气协议检查通过。
- 已部署 Release 的 `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -c Release --no-build -- --status-once` 成功返回有效状态。
- 旧桥接通过 `--exit` 正常退出，部署已验证的 DLL/PDB，执行 `--restore-cycle-default` 后正常启动。候选与驻留路径 DLL 的 SHA-256 一致。
- 2026-10-14 11:27:01–11:28:49（北京时间），真实小屏 22 次回读全部 `usb_active=true`、`bridge_online=true`、`mode=auto`、`display_cached=false`，渲染页均非 offline。USB 状态计数从 36 增至 86，uptime 连续增长，未见重启；最大回读耗时 405 ms。
- 覆盖六页自动切换：Codex → DeepSeek → 智谱 → 天气 → 股票 → 系统，间隔 15 秒。桥接与设备均处于自动模式，循环启用；`settings.json` 和 `devices.json` 的前后 SHA-256 分别一致。
- 桌面状态仍含 8 项监控检查；TAB5 USB 和 Wi-Fi 回读正常。
- `git diff --check` 通过。未提交、推送、打包发布或刷写固件。

本次证据是实体设备的在线状态、绘制页和计数回读，未拍摄实体屏幕；约 109 秒观察不等于长期稳定性验证。完整采样、配置和旧程序备份位于本地忽略目录 `artifacts/development/pc-off-20261014/`。
