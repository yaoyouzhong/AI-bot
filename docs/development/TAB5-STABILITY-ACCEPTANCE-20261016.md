# TAB5 201 稳定性真机验收 / Physical stability acceptance

2026-10-16，按用户确认逐项验收已安装的 0.2.201-ui 与此前批准的配套桥接。没有构建、部署新程序、重新刷机、推送、创建标签或公开发布。证据位于 `artifacts/tab5-stability-20261016/`。

## 已通过

| 项目 | 用户确认与运行证据 |
| --- | --- |
| 书法图片页竖屏 | 停留约 20 秒后画面稳定，能够退出 |
| 书法图片页其他三方向 | 均稳定，能够退出 |
| 书法详情 | 四方向旋转、滚动、栏目切换、返回及退出均正常 |
| 名画图片页 | 四方向稳定，能够退出 |
| 名画详情 | 四方向旋转、滚动、栏目切换、返回及退出均正常 |
| USB 断开 | 用户确认设备保持供电、能正常操作；多次采样确认 USB 未连接时仍有持续的设备 Wi-Fi 鉴权请求，蓝牙也在线 |
| USB 接回 | 自动恢复 COM9，蓝牙回到 USB 优先待命；桥接主进程仍为 PID 21132，未依赖桥接重启恢复。设备运行时间继续推进，没有重启 |

各显示测试后，显示错误、显示欠载及崩溃记录均为 0。接回 USB 后三次新鲜采样的界面心跳年龄为 9／8／3 ms，设备运行时间持续推进，USB 数据年龄为 110／113／10 ms。

## 证据边界

- USB 断开时，`DiagnosticSummary` 的固件运行时间和界面心跳字段保留最后 USB ACK；本轮发现其不再推进后，核对调用方确认它们是缓存，没有将缓存数值记为断开期间的新鲜心跳。Wi-Fi 的连接状态来自 8 秒内真实鉴权请求；断线期间的实际操作由用户确认。没有关闭蓝牙，不构成隔离蓝牙后的纯 Wi-Fi 测试，也未精确测量切换延迟。
- 以上为本次实物路径通过，不能反推此前 .194 偶发卡死的已确认根因，也不承诺间歇故障已被完全排除。若再次发生，仍须重启前保留现场及诊断。
- 声音／日程功能体验按此前指令暂缓；BLE 整包 OTA、64 KiB 极端状态真实硬件边界未在本轮补测。

## 收尾

已确认 `/status` 为自动模式、循环开启，保留原六页顺序及 15 秒间隔。最后按项目约定尝试正常退出、恢复循环再启动时，工具启动环境触发桥接内置 `AppProfileGuard`，退出命令被拒绝；当前正常桥接没有退出或被替换。

用户已从桌面运行 `artifacts/tab5-stability-20261016/finish-from-desktop.cmd`。09:48:41 的 `desktop-finish.json` 确认完成：正常退出、`--restore-cycle-default`、原固定路径正常启动均成功，进程 PID 30844，201／COM9 与新鲜心跳通过（9 ms），原六页／15 秒循环保留，9 个资料文件哈希均未变化。随后通过 HTTP 再次核对连接及循环。没有绕过或修改保护；本轮稳定性验收及正常启动收尾完成。

## English

The user confirmed both calligraphy and painting artwork/reader paths across four orientations, including scrolling, available chapters, return and exit. All post-test display errors, underruns and crash records were zero. With USB absent, authenticated Wi-Fi polling continued and the user confirmed responsive operation; BLE was also active, so this was not an isolated Wi-Fi test. Reconnecting restored COM9 and USB-owned BLE standby without restarting the device or bridge. Three fresh post-reconnect UI heartbeat ages were 9, 8 and 3 ms. Cached USB telemetry was explicitly excluded from disconnected-state heartbeat claims. Physical stability checks passed for this run; the historical intermittent fault remains without a confirmed incident root cause. After AppProfileGuard rejected the tool-launched exit, the user executed the normal desktop helper. Normal exit, cycle restoration and startup succeeded at 09:48:41: PID 30844, 201/COM9, 9 ms UI heartbeat, original automatic six-page/15-second cycle and nine unchanged protected files. Scoped stability acceptance and normal-startup closeout are complete. Audio/agenda, whole-image BLE OTA and extreme 64 KiB hardware tests remain deferred. No public release occurred.
