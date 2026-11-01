# TAB5 纯 Wi-Fi 解锁验证：前置检查与当前阻碍

## 目标

TAB5 不连接电脑 USB、无蓝牙连接，仅通过 Wi-Fi。每次在 TAB5 输入 Windows Hello PIN，用户确认后一次性提交；不保存 PIN。实际 Windows 会话解除锁定才算成功。

## 本次完成

本节记录最初准备阶段；22:17 实际安全桌面检查结果见文末。真实解锁仍未测试。

- 独立只读工具源码 `tools/wifi-unlock-probe/`，不改桥接主程序、TAB5 固件或登录设置。
- Release 编译通过，0 警告/错误。会话条件和报告格式自测通过；普通用户调用 SYSTEM 路径退出 77，非法会话退出 64；三个 PowerShell 脚本语法检查通过。
- 当前普通用户进程：会话 1、Default 桌面、未锁定、可访问当前输入桌面。这不是 Winlogon 安全桌面证据。
- 已准备一次性 SYSTEM 计划任务运行脚本、白名单清理脚本和程序 SHA-256 清单，**尚未安装或执行**。无 PIN 输入、无按键注入、无登录认证组件、无固件刷写。
- 运行包：`artifacts/development/wifi-unlock-20261017/probe/`。完整审查哈希：同级 `review-sha256.json`。源码说明列出运行和回退范围。

## 新增阻碍：现有 Wi-Fi 不稳定

用户报告 Wi-Fi 经常连不上，之前由 Codex 操作重连后恢复。已在聊天“继续未完成任务”（01a14900-294b-7322-a8da-0152b8475067）及 `artifacts/acceptance-wifi-217-20261017/` 原始记录确认：18:45:44 执行的是 Windows `netsh wlan disconnect/connect`，重连电脑当前保存的 kukuo WLAN；不是重启桥接。电脑 IP 前后均为 192.168.31.122/24，随后鉴权 HTTP 200、ping、无线音量控制恢复。此次复发不能称作永久修复。

2026-10-17 21:32:29–21:32:55（北京时间）的 6 次现场采样：

- USB 均未连接，蓝牙均显示“已连接 · 数据已确认”，蓝牙 RPC 时间持续更新。
- Wi-Fi 均显示“等待设备回报”。因此不满足纯 Wi-Fi 测试条件。
- 本机诊断接口响应 8–103 ms；桥接实时状态继续刷新。
- 电脑局域网地址 `192.168.31.122:18765/tab5/v1/status` 的无凭据请求约 4.9 ms 返回预期 401。只证明电脑本机可访问监听接口，不能证明 TAB5 到电脑可达或配对鉴权成功。
- 旧 USB 诊断确认时间为 20:23:40；其中 Wi-Fi/IP/设备故障计数均不能视为当前现场。最后记录的设备 IPv4 为 `192.168.31.238`，当前 ICMP 未获设备回复、邻居表 MAC 为全零；可能已离线、地址变化或链路不可达，尚不能定位为桥接逻辑故障。
- Windows WLAN 事件在 20:43:43 和 20:44:09 有无线安全停止/重新成功记录，与最后 Wi-Fi RPC 时间接近。这是时间相关性，不是因果结论。

## 源码核对与诊断限制

当前交付维护源为 `.codex/worktrees/release-067-205/AI-bot` 和 `artifacts/release-068-20261016/tab5-saver-source`，主工作目录有大量其他未提交改动，未触碰。

- `Tab5Service._wifiReportAt` 和完整设备 Wi-Fi 数值诊断由 USB ACK 更新。USB 断开后，BLE 当前仍在线不意味着旧 Wi-Fi 诊断得到刷新。
- `WifiConnectionStatus` 用最近 8 秒已通过认证的 Wi-Fi 状态请求判定“桥接正常”，没有这类请求且没有新 USB 回报则显示“等待设备回报”。这个提示不能独自区别 Wi-Fi 关联失败、DHCP、TCP、鉴权或设备任务阻塞。
- 固件 Wi-Fi 工作线程负责连接、HTTP 状态轮询、UDP 发现和失败恢复；USB/BLE RPC 是独立通道。
- 桥接“保存并连接”发送设备 Wi-Fi 配置；“服务状态/重新连接”重建设备工作对象；它们不能与 Windows 网卡断开/重连视为同一操作。用户已确认 TAB5 当前 IP 和提示，历史操作已核实为 Windows WLAN 重连。

## 尚未验证

纯 Wi-Fi 端到端稳定性、当前 Wi-Fi 故障根因、锁屏安全桌面探针、正确 PIN 输入框识别、TAB5 PIN 输入与一次性提交、错误/超时/取消路径、真实解锁，均未通过验收。不得把编译、普通用户探针、401 或历史 HTTP 200 写成解锁成功。

## 下一步

1. 用户已现场确认 TAB5 IP 仍是 192.168.31.238，提示“电脑桥接暂不可达，正在寻找电脑”。本轮 21:38 左右的 18 秒受限头部抓包有网关 TCP 握手阳性对照，没有 TAB5 ARP/SYN；只保存解析后的地址/标志，不保存原始包或应用负载。当前程序的 Private/Public 入站允许规则存在。倾向 PC WLAN/NIC/AP 到对端的运行态问题，但仍不能区分网卡、驱动、接入点或设备。
2. 本次独立 `reconnect-current-wlan.ps1` 语法通过。用户已批准，但工具调用受阻，脚本未执行；用户随后手动完成同一网络重连，前后对照见下文。
3. Wi-Fi 主链路稳定后，再请求一次具体系统运行授权：临时任务 `AIBot-WifiUnlockPreflight`、最长 3 分钟、自动注销；受保护目录保留无敏感内容的报告；不发送 PIN。
4. 只读安全桌面检查具备条件后，再实现和实测 TAB5 临时 PIN 输入主链路。最终必须关闭 USB/BLE 回退并验证实际会话解锁。

## 本次重连授权与执行阻碍

用户明确授权“重连当前 kukuo Wi-Fi，保持桥接程序不动，对比重连前后的结果，继续查清反复断连的原因”。已复核脚本 SHA-256 为 `84C46AAF71FDDEF6208C57512B100BE0D166D085D7B627CBD906AE87966A2E54`，当前 SSID 为 kukuo、电脑 WLAN IPv4 仍为 192.168.31.122。

从 Explorer 桌面上下文启动重连脚本的工具调用被自动审批拒绝，返回 `blocked by policy`，没有进一步原因。本次没有改用其他路径规避。只读复核确认尚无本次 `wlan-reconnect.result`；桥接 PID 1116（19:51:48 启动）和 14104（19:49:10 启动）保持不变，Wi-Fi 仍等待设备回报。

当前基线写入 `reconnect-before.json`，近五小时 WLAN 事件写入 `wlan-event-timeline.json`。20:43:43 和 20:44:09 出现无线安全停止后成功的事件，但不是完整断开/重关联事件，也不足以单独推断密钥重协商、驱动或 AP 根因。用户已通过 Windows Wi-Fi 面板手动完成重连。

## 手动重连后的实际结果（22:00–22:06）

用户确认“已断开重连 wifi，tab5 的 wifi 也恢复了”。Windows 事件记录 22:00:07 用户断开，22:00:11 自动连接，22:00:12 手动连接同一 kukuo 配置成功。

- 桥接 PID 1116、14104 和各自启动时间前后完全相同；电脑地址保持 192.168.31.122/24。没有重启或部署桥接。
- 22:01:50 初次复核仍是 USB 未连接，Wi-Fi 显示“已联网 · 桥接正常”；22:01:03 Wi-Fi RPC 返回 ack=1、HTTP 200。TAB5 192.168.31.238 ping 3/3 成功，邻居地址恢复为 14:C1:9F:E8:87:C0。见 `manual-reconnect-after.json`。
- 22:05 左右的 18 秒同条件受限抓包中，观察到 8 次 TAB5 到电脑 18765 的 SYN，以及 8 次电脑的 SYN-ACK，间隔约 2 秒；网关 TCP 阳性对照仍成功。故障时同样抓包未见 TAB5 流量。分别保存在 `wifi-path-headers.json` 和 `wifi-path-headers-after.json`，没有覆盖故障基线。
- 22:04:49–22:05:37 七次采样均为 Wi-Fi 桥接正常，见 `recovery-stability-samples.json`。22:03:32 USB 已接上 COM9，蓝牙进入 USB 优先待命；后续采样不能作为纯 Wi-Fi 用户操作验收，但独立 Wi-Fi TCP 流量仍然存在。
- 新 USB 回报的 HTTP 诊断显示 open_error=0、socket_error=0、HTTP 200；22:05:57 到 22:06:29，设备 valid 状态计数从 1534 增至 1548。路径诊断只在失败时探测，其中 host_error=116 是过去故障的缓存，不能解释为当前 TCP 仍失败。当前诊断节选见 `recovery-fresh-device-health.json`。

## 新线索：电脑曾在双频接入点之间漫游

Windows System 的 Netwtw14 事件在 20:43:43、20:44:10 各有一次 7003。直接读取本机正在运行的 Intel 驱动 `Netwtw14.sys` 消息资源字符串，确认 7003 对应 `Roam Complete`，7021 对应 `Connection telemetry fields and analysis usage`；没有用网上其他型号的故障描述代替本机证据。

两次同时间 7021 的原始二进制分别含 BSSID D4:53:2A:47:8F:61、D4:53:2A:47:8F:62；7003 也同时包含这两个地址。当前 `netsh wlan show interfaces` 确认 :61 是 colocated 2.4 GHz、信道 3，:62 是当前 5 GHz、信道 40。这支持电脑在双频之间发生切换的解释，且第二次切换与最后成功通信时间接近。原始事件保存在 `intel-event-details.json`，WLAN 时间线保存在 `wlan-events-after.json`。

**结论边界：** 无桥接重启、无 IP 变化的电脑 WLAN 重连再次恢复通信，配合故障时邻居解析和报文异常，优先排查电脑无线驱动与 AP 的对端转发/漫游状态。尚未证明是 Intel 驱动缺陷、路由器转发表问题、固件问题或特定卸载机制；不能将漫游的时间相关性写成已确认根因。

当前网卡“首选频带”为“无首选项”、漫游主动性为“中间”。可审查的下一项单变量对照是仅将电脑首选频带临时设为 5 GHz，保留原值以便恢复；它只是频带偏好，并不保证完全禁止漫游，短期不复发也不等于永久修复。这属于额外系统设置变更，**尚未执行**。本次没有改网卡设置、驱动或路由器，没有修改桥接业务代码或刷固件，真实解锁仍未测试。

## 用户设置首选 5 GHz 后的复核（22:11）

用户随后自行在设备管理器将首选频带改为 5 GHz，并确认 TAB5 Wi-Fi 仍正常。只读复核：`RoamingPreferredBandType=2`，显示“3. 首选 5GHz 频带”；电脑连接 kukuo 的 5 GHz、信道 40、BSSID D4:53:2A:47:8F:62。原值为 0（无首选项），可用于回退。

22:11:45、22:11:50、22:11:55 三次采样均显示 Wi-Fi 桥接正常，设备有效状态计数 1671→1673→1676，最后 HTTP 状态为 200，连接和套接字错误均为 0。桥接 PID 1116、14104 及启动时间未变。USB COM9 和蓝牙仍连接，所以这仅确认设置生效、Wi-Fi 状态轮询继续成功，不是纯 Wi-Fi 操作或解锁验收，也不证明反复断连已根治。后续观察应保留这一单变量，不同时调整漫游、驱动或路由器配置。

## 已授权执行的首次安全桌面检查（22:16–22:18）

用户明确允许临时 SYSTEM 检查。通过 Windows 正常 RunAs/UAC 启动已审查、哈希一致的 `probe/Run-Preflight.ps1`，新建受保护目录 `C:\Program Files\AI-bot-WifiUnlockProbe` 和任务 `AIBot-WifiUnlockPreflight`。22:16:13 创建运行凭据，SYSTEM 检查进程 PID 43464 在会话 0 等待；用户随后手动锁屏。

22:17:49 的真实结果：`Stage=structure-inspected-not-unlock`、`Session=1`、`System=true`、`Locked=true`、`Desktop=Winlogon`、`InputDesktopAccessible=true`、`LogonUiWindows=1`、`ProtectedEdits=0`、`FocusedProtectedEdits=0`、`ForegroundLogonUi=false`、`Error=0`、`UnlockTested=false`。

这证明本机受控 SYSTEM 辅助进程可以进入目标会话的锁屏安全桌面并枚举登录窗口；没有证明已识别 PIN 输入框或能够提交 PIN。首版在锁定后立即单次枚举；报告文件创建与完成相隔约 38 毫秒。可能早于用户关闭锁屏壁纸/输入框出现，也可能是控件未通过所查 UIA 属性暴露，需结合用户实际屏幕确认，不能选定某一解释。

报告与运行凭据已复制保存为 `secure-desktop-first-result.json`、`secure-desktop-first-receipt.json`。运行后未发现检查进程或启动脚本进程，`schtasks /Query /TN AIBot-WifiUnlockPreflight` 返回任务不存在。受保护目录暂保留工具及报告，尚未执行可执行文件清理。没有读取 PIN、窗口名称或文本，没有按键注入，没有实际解锁操作。

为排除过早采样，探针候选已增加最多 10 秒的控件等待与会话复核，保留 12 秒看门狗和 16 秒父进程兜底，报告增加 `InspectionAttempts`。候选在 `probe-wait-v2/`，编译 0 警告/错误，自测通过，非 SYSTEM 检查仍退出 77；**候选尚未替换受保护目录中的首版，尚未运行第二次安全桌面检查**。待用户确认刚才是否看到了 PIN 输入框后安排下一次同步检查，不以首次窗口可见代替整个解锁链路通过。

## 第二次真实安全桌面检查：检测到受保护输入框（22:21）

用户明确确认首次已看到 PIN 输入框。在同一已授权只读检查范围内复测：先验证并保留首轮报告及凭据的哈希，用本次工具自己的清理脚本移除旧临时文件，再安装 `probe-wait-v2` 到相同受保护目录；任务名称、SYSTEM 身份、最大时长、读取范围均不变。第二次 receipt 创建于 22:21:24，SYSTEM 等待进程 PID 4228。用户按提示锁屏并显示 PIN 页面。

真实报告：`Stage=structure-inspected-not-unlock`、`Session=1`、`System=true`、`Locked=true`、`Desktop=Winlogon`、`InputDesktopAccessible=true`、`ForegroundLogonUi=true`、`LogonUiWindows=1`、`ProtectedEdits=1`、`FocusedProtectedEdits=1`、`InspectionAttempts=3`、`Error=0`、`UnlockTested=false`。

结论：安全桌面可访问，登录窗口在前台，一个受保护编辑控件可通过所查 UIA 属性识别且具有输入焦点。重复检查消除了本次未发现控件的现象，支持首轮采样过早的解释。仍不能仅靠 `IsPassword=true` 程序化区分 PIN 与账户密码；没有读取或提交任何凭据，没有测试 SendInput，没有部署网络输入辅助组件，没有改 TAB5 固件。后续实际解锁必须完成登录方式识别、鉴权加密的临时 PIN 提交及实际解锁状态验证。

第二轮报告及凭据已保存为 `secure-desktop-second-result.json`、`secure-desktop-second-receipt.json`。检查进程已退出，任务查询返回不存在，用户已正常解锁。首版、第二版构建和报告均保留在本次开发产物目录，供复核，不作为正式交付。

随后通过正常 UAC 运行已审查的 `Remove-Preflight.ps1`。现场复核受保护目录仅剩 `receipt.json`、`result.json`，四个临时程序文件已移除，清理进程和检查进程均已退出，计划任务不存在。系统登录配置未修改。

## 单次 PIN 候选完成，等待真机部署批准

按用户要求，只复用应用控制页原锁屏按钮：锁定且桥接支持时变为“解锁”，打开数字 PIN 弹窗；没有新增解锁入口。使用原页面深色配色、同一按钮大小和位置。首次预览发现标题使用不含中文字形的字体产生四个方框，已改用 `tab5_font_30`，扩充中英文提示所需的字形断言，并实际查看修正图片 `ui-preview/computer-unlock-pin-fixed-title-v2.png`。

实现、协议、权限、超时及限制见 [单次解锁协议](WIFI-UNLOCK-TRIAL-PROTOCOL.md)。数字 PIN 在设备上掩码显示，提交后清空；内层 AES-GCM 密文经配对 Wi-Fi RPC 转发至临时 SYSTEM 辅助进程。单次、无自动重试，不添加常驻服务或 Windows 登录认证组件。仍需用户先在电脑选中 PIN 页面。

本次验证：

- 临时辅助程序 Release 构建 0 警告/错误；加密、到期、篡改、重放、取消、焦点变化、缓冲区清零和 x64 INPUT ABI 自测通过。非 SYSTEM `--serve` 退出 77，没有输入行为。安装/清理脚本仅做语法检查，没有运行。
- 维护工作树中的桥接 Release 构建 0 警告/错误；USB/BLE 拒绝、明文凭据字段拒绝、非法输入、冒充管道服务拒绝自测通过。`--status-once` 被 Codex 环境中的 AppProfileGuard 数据目录保护拒绝（退出 2），未绕过；这项运行检查未通过，部署后还需正常用户环境验证。
- 固件 `scripts/build.ps1 -LocalArt -LocalVersion 0.2.217-unlock-trial` 完成；标题修正后重新构建。镜像 7,095,184 字节，分区仍可容纳（余量约 2%）。编译存在原有警告，不能写成零警告。
- 共享 LVGL 预览全套通过；验证原按钮切换、掩码、取消/超时/链路变化清空、重复确认只提交一次。使用真实 sidecar 再运行预览通过。模拟成功画面只用于布局，不代表真实解锁。
- 生产 OTA 解析器校验镜像、芯片、版本、校验和与 sidecar 通过。候选隔离在 `artifacts/development/wifi-unlock-20261017/firmware-candidate/`，尚未切换正式本地交付 latest。
- 原桥接 PID 1116/14104 及启动时间保持不变。未部署桥接、未刷机、未启动输入辅助程序、未实际提交任何用户 PIN。

可审查 payload 全路径、大小和 SHA-256 见开发产物 `review-manifest.json`。固件 SHA-256 为 `bc0a1d6721b842f747ee5a2a31421f12320fc7a331a3b239517740b98fea7296`；桥接 DLL 为 `b7d6244b7efed381eb5e1965e21739bbbf92cb61a9770b9e9a348b2a91dc53b1`；辅助程序 DLL 为 `868e6cbff4e59420b4219615a657f13236769d33735645d117ce0e67b5ab7ff2`。

下一次批准对象：备份并部署上述桥接候选，按标准路径准备/刷入上述 TAB5 候选，通过正常 UAC 运行上述辅助程序的单次试验和归属校验清理；USB/蓝牙断开后由用户只在 TAB5 输入 PIN。先确认正常锁屏入口与 PIN 页面，再执行一次解锁，核对真实 Windows 未锁定和辅助程序报告。失败不自动重试或猜测 PIN。

回退：保留部署前桥接二进制及配置副本；停用候选并恢复该副本。固件已验证可用回退包 `artifacts/firmware/tab5/versions/0.2.217-ui/bb3a94f9e186-3044bf49/aibot_tab5.bin`，SHA-256 `bb3a94f9e1862c200a81d5176852966663b0d456559f8f59e8b1eed0714d3e96`。临时任务无周期触发器，执行上限 4 分钟；清理只处理本工具归属文件，保留状态报告，不改网卡、路由器或现有 Windows 登录设置。

## 用户批准后部署与启动核验（22:56–22:59）

用户明确批准部署桥接、刷入试验固件、继续纯 Wi-Fi 单次解锁，由用户在 TAB5 输入 PIN、不保存。逐项核对批准 payload 哈希一致后，通过标准脚本将候选准备至 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，保留原版本归档。

首次普通 COM Shell 启动仍继承了重定向环境，AppProfileGuard 正常阻止，文件未替换；改由现有 Explorer 桌面的 ShellExecute 启动正常用户进程，状态检查通过，没有修改或绕过保护。部署脚本曾因 System.IO.Ports 通用占位 DLL 与已安装 Windows 实现哈希不同而停止；核实候选 `runtimes/win/lib/net8.0/System.IO.Ports.dll` 与已安装 DLL 哈希完全一致后，按 Windows 实现校验继续，未替换该依赖。

桥接原五个运行文件保存在本次产物 `bridge-before-unlock/`；用户资料顶层文件备份位于仓库外 `C:\Users\yaououzhong\AppData\Local\AI-bot-backups\wifi-unlock-20261017`，不打印其内容。正常退出后部署候选并核对哈希，主程序 PID 11684、健康监控 PID 56700 于 22:56 启动；部署前后正常桌面 `--status-once` 均成功。现场 profile 为真实 Roaming、`test=False`、`pairMatchesDisk=True`，原配对时间不变。

升级提供后 TAB5 已开始 Wi-Fi 压缩传输；随后本机 start 调用被“当前操作处理中”拒绝，没有重复启动。传输 4,861,070 字节，约 7.985 秒。设备回报试验版本后首次核验发生在健康观察期内而未通过；核查固件必须连续健康 30 秒后标记 VALID 的实现，窗口结束后再次核验成功：`ota_1`、`VALID`、ELF SHA-256 `bf3cb71549d9c0962a75908b48111aefaaecca49a1bde6f17ce6e1ea0ce8fb9e`，与批准镜像一致。设备保留诊断 `prepareMs=8200, writeMs=9615, verifyMs=930, installMs=19443, previousError=0`。

证据：`deployment-verified.json`、`ota-verified-after-health-window.json`、桌面状态结果和部署备份。尚未启动临时输入辅助程序，等待用户拔 USB、关闭蓝牙并确认 Wi-Fi 正常后进行一次 PIN 解锁。未读取或提交用户 PIN。

## 纯 Wi-Fi 就绪；辅助程序启动被工具策略拒绝

用户确认 USB 已拔、电脑蓝牙已关闭、TAB5 Wi-Fi 桥接正常。尝试执行“只读核对通道及批准哈希，然后通过正常 RunAs/UAC 启动 `trial/Run-Trial.ps1`”的命令时，工具在创建进程前拒绝，返回 `rejected: blocked by policy`。没有提供更具体原因；不得解释为用户未授权，也未使用其他入口重试或绕过。

该命令没有执行，不能将其中的检查或启动记录为成功。随后独立只读核验实际通道和辅助程序/任务是否存在，结果写入 `pure-wifi-before-trial.json`、`trial-not-started.json`。本次仅完成桥接和试验固件部署、实际启动核验及纯 Wi-Fi 准备；没有输入用户 PIN，没有进行实际解锁，不能宣称整个链路通过。候选及回退备份保留。
