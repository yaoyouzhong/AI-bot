# TAB5 USB 重连定位 / USB reconnect investigation

2026-10-07，实机 `0.2.147-ui`。当前仅定位到串口枚举前的等待，尚未确认底层根因或完成修复；桥接增加了阶段计时，未修改轮询间隔、协议超时或固件。

## 实机证据

- 用户观察：自动选择模式下拔插 USB 在 10 秒以内恢复；保持插线重启约 20 秒恢复。
- 拔插：21:27:28.042 首次看到 COM9，约 0.2 秒后连接；桥接阶段合计 203 毫秒（写入 94、确认 109）。插线动作本身没有独立时间戳，不能从电脑记录推断准确的插线至枚举耗时。
- 重启：21:27:40.635 COM9 消失，21:28:08.491 再出现，21:28:09.302 已连接。确认时设备 uptime=25,554 毫秒，结合确认时间推算 COM 出现时设备已运行约 24.9 秒；桥接阶段合计 172 毫秒（写入 63、确认 109）。推算误差包括通信和采样时间。
- 稳定运行后的描述符探测：设备/配置正确；字符串响应最长约 32 毫秒；不支持的 Microsoft OS 字符串、Device Qualifier、BOS 在 1 毫秒内返回失败。此结果只能排除稳定运行时持续卡住，不能排除启动时控制传输异常。
- 第二次重启：21:43:48.865 端口消失；49.445/49.630 为枚举中/已配置；52.969 再次消失；53.172/53.387 再次枚举并出现 COM9；55.068 已恢复通信，设备 uptime=2,382 毫秒。即从首次断开到通信约 6.2 秒，本轮未复现慢启动。中间两次配置对应的 VID/PID 未记录，不能断定其身份；也不能把这次较快恢复归因于修复，因为没有更换固件或传输策略。
- 用户确认第二轮明显更快、两轮按法相同；因此保留“间歇性枚举延迟”的判断，不能假定 20 秒是固定启动成本。
- Windows USBHUB3 ETW 启动返回权限不足，未创建采集会话。只读 USB hub IOCTL 捕获了第二次重启，但尚未捕获慢启动时的 USB 控制传输。

## 管理员跟踪结果

用户明确批准后，限时 USBHUB3 跟踪于 21:48:13 开始、21:52:14 自动停止，未修改驱动、注册表或系统配置，未开启业务报文数据采集。三次重启均先枚举 `303A:1001` / COM8，随后切换到应用 `303A:4005` / COM9；六次枚举全部成功，枚举重试/最终失败均为 0。

| 重启 | 断开至恢复通信 | 应用 USB 枚举 | 桥接握手 |
| --- | ---: | ---: | ---: |
| 21:51:22 | 7.145 秒 | 201.426 毫秒 | 172 毫秒 |
| 21:51:44 | 6.342 秒 | 201.468 毫秒 | 172 毫秒 |
| 21:52:06 | 6.224 秒 | 203.111 毫秒 | 172 毫秒 |

六次 `Device Qualifier` 返回 `USBD_STATUS_STALL_PID`（`0xC0000004`），随后均立即成功完成枚举；对应当前全速设备未提供高速限定描述符，不是本轮延迟证据。跟踪无丢失事件；`tracerpt` 的 schema 警告来自系统头部 `PartitionInfoExtensionV2`，USBHUB3 事件正常解码。上述耗时不包含按下重启键至首次 USB 断开的时间。

用户确认三次都非常快，基本界面启动后立即连上。尚未在系统跟踪中复现最初 20 秒慢连接，不能确认底层根因，也不能称固件修复成功；本轮没有改固件。持续 USB hub 查询是这几轮额外的诊断条件，尚无不带查询的等价对照。保留已定位的边界和采集工具，避免凭正常样本修改协议/供电参数。证据为 `capture-result.json`、`etw-summary.json`、原始 `.etl`、`hub-detailed.jsonl`。所有限时采集已停止。

证据目录：`artifacts/development/usb-reconnect-20261007/`，包含 `observations.jsonl`、`descriptor-results.json`、`hub-observations.jsonl` 和相应采集脚本。没有采集配对密钥、网络密码或业务报文正文。

## 本地改动及验证

- 桥接诊断保留最近一次首次连接、慢连接或失败的 enumerate/gate/discover/open/identity/host/write/ack 耗时；当前阶段及年龄单独展示。计时从电脑开始尝试连接算起，不包含 USB 插线到 Windows 创建串口之前的时间。
- 修复 `--self-test-tab5` 入口遗漏独立测试资料目录的问题；沿用已有 `BeginPublicSelfTest()`，正常运行的资料保护不变。
- Release 构建 0 警告、0 错误；综合 TAB5 自检与 RPC 自检通过。正常桌面环境 `--status-once` 成功，真实 USB 已重新收发确认，轮播维持 auto/true、原六页、15 秒。
- 21:47:25 标准 Release 路径已运行最终诊断构建；DLL SHA-256 为 `1fefd963952a756f5fa1fd187f9b1cede89f9ef559b53666533a397367ce8d41`。正常退出/构建/恢复轮播/启动均通过；`test=False`、`pairMatchesDisk=True`。这次桥接重启后的首个记录为 172 毫秒。未更换设备 `.147` 镜像。

## English

The delay is localized before Windows exposes COM9, not yet fixed. After a hardware restart, COM9 appeared at approximately 24.9 seconds of device uptime; the desktop handshake then took 172 ms and the sampled connected state followed within about 0.8 seconds. The user observed under 10 seconds for cable reconnection and about 20 seconds after restart. A second restart recovered communication about 6.2 seconds after disconnection, without changing firmware or transport behavior; it did not reproduce the slow case. Steady-state descriptor requests completed promptly, which does not exclude an early-boot control-transfer problem. USBHUB3 ETW was denied by Windows permissions; a read-only hub-state capture recorded the second restart, but the slow-case control transfers remain unobserved. Stage timing and isolated self-test initialization were added. Release build, TAB5/RPC self-tests and desktop status collection passed; original automatic cycling was retained.

The user subsequently authorized a bounded elevated USBHUB3 capture, which stopped automatically. Three more restarts recovered in 7.145/6.342/6.224 seconds from first USB disappearance, with application enumeration taking 201–203 ms and bridge handshakes taking 172 ms. All six ROM/application enumerations succeeded without retry or event loss. Expected qualifier STALLs did not delay successful enumeration. The user confirmed all three felt fast. The original slow case was not reproduced under ETW/hub polling, and there is no equivalent no-polling control, so the underlying cause and a firmware fix remain unverified. No firmware or system configuration was changed; all capture processes have stopped.
