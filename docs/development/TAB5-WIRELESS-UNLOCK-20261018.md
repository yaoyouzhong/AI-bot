# TAB5 蓝牙与纯 Wi-Fi 解锁候选交付（2026-10-18）

## 当前状态与授权边界

用户要求先完成开发和本地验证，刷机前再确认；待用户回来一起做真机验收。本次未刷机、未部署桥接、未安装 TightVNC、未注册新服务或驱动、未更改 Windows 登录配置、未提交或推送。当前在用固件与桥接保持原状态。

蓝牙候选 `0.2.217-ble-unlock` 已单独封存，详见 [蓝牙开发报告](TAB5-BLE-UNLOCK-20261017.md)。组合候选 `0.2.217-wireless-unlock` 保留相同 BLE HID 实现并加入可选纯 Wi-Fi 路径。两者均未完成实际配对和真实 Windows PIN 解锁验收。

## 体积与方案选择

- Wi-Fi 复用现有加密 RPC、Wi-Fi 驱动、数字键盘及锁屏按钮，没有新增固件组件或 NuGet 依赖。
- 对比蓝牙候选，固件从 7,111,856 增至 7,115,744 字节，增加 3,888 字节（约 3.8 KiB）。OTA 槽仍剩 93,216 字节，构建分区校验通过。
- 对比改动前同源码工作树 Release 构建，桥接主程序集增加 55,808 字节（54.5 KiB），包含本次本地回归测试代码。没有把外部服务塞进桥接发布目录。
- 选择标准 RFB 键盘适配：电脑上可选安装 TightVNC 2.8.90 的 Server 部分。官方完整 64 位 MSI 为 2,543,616 字节（约 2.43 MiB）；这是下载大小，不是安装后占用或运行内存。后两项尚未实测。
- 不安装 Viewer、镜像显示驱动、Web 客户端或其他远程桌面依赖。适配器没有画面请求、鼠标、剪贴板、文件传输、远程地址配置。厂商服务本身仍是具备远程桌面能力的组件，不称为“只有键盘的服务”。

来源：[官方大小与下载](https://www.tightvnc.com/download/)、[官方选择 Server 组件与 MSI 配置说明](https://www.tightvnc.com/doc/win/TightVNC_2.7_for_Windows_Installing_from_MSI_Packages.pdf)、[官方服务模式说明](https://www.tightvnc.com/doc/win/TightVNC_for_Windows-Installation_and_Getting_Started.pdf)、[RFC 6143](https://www.rfc-editor.org/rfc/rfc6143)。厂商文档说明服务模式可在未登录时访问；本机 Windows Hello PIN 兼容性仍需真实验收。

## 已实现的链路

1. 仍点原锁屏按钮。锁屏后显示“解锁”。开启面板时固定选择通道：仅当 USB/BLE 都未连接、Wi-Fi 心跳新鲜且桥接通告 `wifiKeyboardVersion=1` 时选 Wi-Fi，否则保留 BLE。途中不会自动换通道或重发。
2. 用户先在电脑显示 PIN 登录框，在 TAB5 输入现有数字 PIN。LVGL 只接收星号，PIN 放在可清零缓冲；提交即清除 UI 缓冲。
3. `wifi-keyboard/begin` 经既有设备配对 AES-GCM、HMAC、session、时间及 nonce 校验后处理。普通用户桥接只连接 `127.0.0.1` 上已有的厂商服务。读取本机服务配置，要求只允许 loopback、启用 VNC 认证、关闭 HTTP 与日志、允许输入；要求断开时不锁屏/注销；核对已连接 TCP 的完整地址/端口、对端 PID、Session 0 及受保护 Program Files 下的 `TightVNC/tvnserver.exe` 路径。无法核对则拒绝。
4. 完成 RFB 3.8 VNCAuth 握手后生成一次性 offer/key，绑定已认证设备和桥接 session，有效期 15 秒。PIN 另外封装为 AES-GCM 密文，不进入 JSON 明文字段或日志。服务握手只使用独立 VNC 访问密码，绝不把 Windows PIN 当成 VNC 密码。
5. `submit` 仅暂存解密后的可清零字节，不发送按键。TAB5 每次 `step(next=N)` 才授权一个数字的按下/松开，递增序号不可重复。每个请求使用新 nonce。每步检查本机当前控制台会话仍锁屏；没有后续请求时按 2 秒租约清除，清理定时器每 250ms 检查，正在执行的 I/O 另有 2 秒超时。
6. 最后一位后等待 350ms，再检查锁屏状态。若 Hello 已自动提交并解锁，不发送 Enter；否则只发送一次 Enter。只有后续新鲜电脑状态确认已解锁，界面才显示成功。
7. 取消、页面离开、失效、Wi-Fi/会话变化、非法包或发送错误均停止并清除。任何已发送部分都不自动补发；不确定时要求用户检查电脑。服务端不保存 PIN、不写请求日志、不保存重试队列。

本方案不调用此前的 `WifiUnlockTrial` 临时 SYSTEM 辅助程序，未修改其信任校验，也没有换入口重试曾被策略阻止的启动动作。新候选仅实现对合法安装的现成厂商服务的普通权限协议调用；实际安装仍待独立授权。

## 本地配置入口（代码已完成，本次未执行）

`AIBotBridge.exe --configure-wifi-keyboard` 显示本机掩码输入框，明确要求 TightVNC 的独立 8 位字母/数字访问密码，拒绝纯数字，避免误填现有数字 PIN。使用当前用户 DPAPI 加密写入 `AI-bot/wifi-keyboard-access.dat`。这保存的是组件访问凭据，不是 Windows PIN，也不改变 Windows 登录方式。

`AIBotBridge.exe --wifi-keyboard-check` 可在普通桌面只检查本机服务配置、对端身份与 RFB 认证，完成即关闭，不发送任何键盘输入。此入口供批准安装后的初次检查。

候选目前没有这项本地配置，默认不通告 Wi-Fi 键盘可用。配置存在只表示已配置；真正 begin 时仍会检查服务设置、对端身份、认证和锁屏状态。没有服务或检查失败会停止，不降级无认证或账户密码。

## 本地验证

- Windows Release 构建通过，0 警告/0 错误。普通桌面权限 `--status-once` 返回有效 JSON、退出码 0；只存通过结果，未保存原始状态信息。Codex 子进程第一次执行触发原有 profile guard，后用普通 Explorer 桌面上下文做同一只读检查通过，未绕过 guard。
- RFB 测试：独立 PyCryptodome 已知 DES 向量、分片读取、拒绝 None 认证/错误认证/畸形握手/过长服务名、只有数字及 Enter 按下松开。
- 状态机测试：传输与身份限制、非法明文字段、锁屏门控、4–64 位、64 位重复数字、取消、后台租约过期清除、篡改、重放、断连、半途解锁以及自动提交后不再发送 Enter。只用公开合成数据。
- 现有真实 RPC 实现接入模拟 RFB 输出流，验证加密 begin/submit/4 个 step/finish，记录恰好 80 字节键盘事件。每次请求额外重放、错误 HMAC 均拒绝，不重复按键。这是进程内模拟传输，不是实机纯 Wi-Fi 验收。
- TAB5 固件构建通过，现有 BLE 事务/后端/广播/mailbox/语音原生测试与完整 LVGL 预览通过。实际 sidecar 更新说明也用于候选预览。
- 未连接任何实际 VNC 服务，未触发真实 Windows 键盘输入，未使用真实 PIN。

证据目录：`artifacts/development/wifi-keyboard-20261018/`，包含构建日志、测试结果、大小报告、候选、源文件快照和预览。

## 候选与明天验收

组合固件：`artifacts/development/wifi-keyboard-20261018/firmware/latest/aibot_tab5.bin`。
固件 SHA-256：`1758745dd737d44718dc726638321b56b85d892fbd87173f04de4c94bdebf229`。
桥接目录：`artifacts/development/wifi-keyboard-20261018/bridge-candidate/`。
桥接及源文件哈希见同目录清单。正式 `artifacts/firmware/tab5/latest` 未切换，仍是原已验收的 Wi-Fi 合成测试固件；部署批准后才能通过准备脚本切换固定交付入口。

验收一次推进一项，用户确认当前现象后再继续：

1. 先确认要刷入的具体固件和桥接候选；蓝牙无需新增 Windows 服务。刷机后核对启动、现有页面和桥接状态。
2. 完成 BLE 配对、Windows HID 识别及桥接身份确认，再由用户在 TAB5 输入 PIN 验证锁屏。当前生产入口强制要求锁屏，不提供普通文本框输入模式；不得用错误数字尝试登录充当键盘测试。拔掉 USB、断开 Wi-Fi 后确认纯蓝牙路径。
3. 若继续 Wi-Fi，单独批准安装官方 TightVNC Server。核对官网 MSI 的签名/版本/大小/哈希，选择仅 Server。不开防火墙外部访问例外，不启用 Web、镜像驱动、Ctrl-Alt-Del 策略修改。使用独立访问密码，配置只允许本机连接、禁用日志及断开时锁屏/注销动作，再完成桥接的本机访问配置。适配器不提供或执行自动安装脚本。
4. USB 拔出、电脑蓝牙关闭，确认 TAB5 Wi-Fi 桥接正常；先用 `--wifi-keyboard-check` 验证认证握手（不发送按键），再显示 Windows PIN 登录框，由用户在 TAB5 输入现有 PIN，只提交一次。实际原桌面恢复后核对 TAB5 状态。
5. 检查取消、断连、错误 PIN 后没有自动重试，重连不重放；记录服务运行内存和磁盘占用。失败保留现场，不自动再输 PIN，也不改成账户密码。

限制：候选依赖人工先显示正确 PIN 输入框，不自动判断焦点；不覆盖开机 BitLocker、注销后登录、RDP 会话切换、休眠唤醒。未部署前不能承诺本机 Hello 或服务对端识别实测通过。
