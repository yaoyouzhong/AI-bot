# TAB5 Wi-Fi 一键解锁候选（0.2.227-ui）

开发、布局调整、桥接/固件部署及 TightVNC 安装已完成；.227 精确启动与生产 RFB 认证通过。用户拔 USB 并关闭蓝牙后，纯 Wi-Fi 真实解锁成功。根据初次约 10 秒的反馈，已部署加速版：电脑端实测流程 3281ms，用户确认成功且明显变快。取消和断线边界仍只有合成测试，未全部宣称实机验收通过。详细证据见文末。

## 入口与界面

- TAB5：应用 → 控制 → 原“解锁”按钮，不增加独立应用。有效 USB / BLE 继续沿用既有 HID 路径；USB 和 BLE 均断开、Wi-Fi 已配对且状态新鲜时，使用新的 Wi-Fi 路径。面板打开后固定通道，不中途切换或重发。
- 电脑：设备中心 → TAB5 → 电脑控制 → 一键解锁设置 → Wi-Fi 连接设置。
- 新设置页只保留开关、访问密码、短状态提示，底部仅保留左侧辅助操作与右侧“保存”；关闭统一使用窗口 × 或 Esc。沿用现有白底、白底开关行、蓝色主按钮、微软雅黑和按住显示密码控件。未保存的输入不能触发旧配置连接检查；关闭清空输入、失焦恢复掩码。
- Wi-Fi 默认关闭，与已有 USB / BLE 一键解锁独立开启。旧 Wi-Fi 试验配置不自动启用。Windows PIN 沿用已绑定当前 TAB5 的电脑端 DPAPI 记录，不新增第二份 PIN，不在 TAB5 输入或取回 PIN。

## 实现与限制

普通权限桥接通过独立实现的 RFB 3.8 VNCAuth / KeyEvent 子集连接本机官方 TightVNC Server。检查配置仅允许 loopback、启用认证、禁用 Web 和日志、允许输入、断开时不锁屏或注销；核对已连接 TCP 完整对端、PID、Session 0，以及服务管理器登记的 tvnserver 独占进程、Running 状态、LocalSystem 账户和精确 Program Files 启动命令。能查询进程路径时另外核对实际路径；仅在 OpenProcess 返回 AccessDenied 且上述服务核验均通过时接受系统服务身份。未提供任意目标地址、任意键值、画面请求、鼠标、剪贴板或文件传输 API。厂商服务仍然是完整的远程桌面组件，不称为单纯键盘服务。

Windows PIN 留在电脑桥接的可清零字节缓冲中，准备完成后才读取，每个数字发送后擦除；不写请求、日志、重试队列或 TAB5 RAM。组件访问密码另由当前用户 DPAPI 加密保存，是独立 8 位且含字母的凭据。`unlock-pin/read` 继续拒绝 Wi-Fi，没有放宽旧的 USB / BLE 凭据导出路径，也不启动旧临时 SYSTEM 辅助程序。

沿用当前会话的锁屏状态及显示电源通知：最多 12 秒准备，已锁屏且屏幕亮起状态连续稳定至少 500ms 后继续；屏幕未知、熄屏或壁纸状态会重置稳定计时。新鲜 Off 状态最多两次 Shift，每次后等待 1500ms；只有明确壁纸状态允许一次 Space，再等待 350ms。会话变化或准备超时不读取 PIN。普通权限检测不能证明安全桌面的 PIN 框焦点，验收仍需用户确认已选择正常数字 PIN 登录方式。当前不覆盖账户密码、BitLocker、注销登录、RDP 切换或休眠唤醒。

每个有效 poll 最多发送一个数字的按下/松开，不能由网络请求指定键值。最后一位后等待 350ms；如 Windows 已解锁，不发 Enter；否则只发一次 Enter，最多观察 10 秒。新鲜普通电脑状态确认已解锁后 TAB5 显示成功，500ms 自动关闭。RFB 按下后的失败只尝试释放按键，不重发按下。

取消、会话/链路变化、过期、未知锁屏状态或 I/O 失败会清除状态；没有后续 poll 时 2500ms 租约清除，后台每 250ms 检查，正在执行的 I/O 限 2000ms。已经到达电脑并执行中的单个请求不能保证被取消撤回。设备与桥接各有 40 秒总预算；一次失败后必须重新由用户打开，旧 requestId、offer 或 next 不能重放。

## 候选来源与验证

基于已验收 .225 与独立 .226 无线启动核验候选继续开发，保留 .226 功能。源文件、配套桥接、构建脚本、截图和清单在 `artifacts/development/wifi-oneclick-20261018/`；`REVIEW.md` 与 `review.json` 记录精确 payload。既有主工作目录和维护工作树的大量未提交改动未覆盖。

- Windows Release 构建：0 警告 / 0 错误。权限兼容版 145 项专门自测覆盖 RFB 独立 DES 向量、分片、拒绝无认证、服务 PID/路径/账户/状态不匹配拒绝、DPAPI、默认关闭、单次请求、64 位重复数字、自动提交不发 Enter、取消、后台租约、禁用、会话变化、断连和加密 RPC。全部使用公开合成数据，没有真实 Windows 输入。该阶段证据为 `service-check-final-tests.log` 与 `tests/service-check-final/result.json`；一次误用已有夹具目录的重复运行失败，未作为产品通过依据，随后使用新目录完整通过。后续加速版 151 项与实机记录见文末。
- 125 项原 PIN 回归通过，含 100% / 150% / 200% 布局模拟；现有 RPC、升级和 63 项无线启动核验回归通过。
- TAB5 实际 Wi-Fi 工作线程的原生替身测试通过，覆盖单次 begin、序号、取消、认证失败、超时和链路/会话变化；没有 PIN 或 HID 路径。
- LVGL 界面回归通过，包含原 USB / BLE 解锁、Wi-Fi 同一入口、单次开始、取消、连接代次变化、不切换通道、成功需新状态及 500ms 关闭。两页设置窗口采用相同宽度、留白、开关行、密码输入控件、等高且同基线的页脚按钮，已做同一 DPI 环境的对照预览及 100% / 150% / 200% 检查。新电脑设置页及实际 sidecar 预览已目视核对，均为本地渲染，不能称为设备截图。
- 固件构建与分区检查通过，保留既有未使用变量/函数警告及开发环境 GDB 资源提示，没有以零警告描述。临时构建覆盖后，维护源码按哈希恢复，无冲突。
- 直接 Codex 子进程的 `--status-once` 被 AppProfileGuard 拒绝；按提示从正常 Explorer 桌面执行同一只读命令成功（exit 0、有效 JSON、18 字段），没有修改保护或保存原始状态。

## 现场验收

用户已明确授权并完成桥接备份/部署、刷入 .227 和安装官方 Server 组件。只安装 Server、仅本机连接、不加外部防火墙例外、不启用 Web 或镜像驱动、不改 Ctrl-Alt-Del 策略。独立组件密码在本机生成并用当前用户 DPAPI 保存，不进入聊天、安装命令或日志。

本机认证握手已通过且没有键盘输入。先由用户拔 USB、关闭电脑蓝牙，复核纯 Wi-Fi 新鲜桥接状态；此项通过后再锁屏，由用户点击 TAB5 原解锁按钮一次。后续逐项验收取消、断线不重放和错误后不自动重试。真实输入由用户控制，失败保留现场，不猜测或反复尝试 PIN。TAB5 保留当前显示设置，不运行恢复自动轮播命令。未提交、推送或公开发布。

来源：[RFB RFC 6143](https://www.rfc-editor.org/rfc/rfc6143)、[TightVNC 官方下载](https://www.tightvnc.com/download/)、[官方 Server / 安装配置说明](https://www.tightvnc.com/doc/win/TightVNC_2.7_for_Windows_Installing_from_MSI_Packages.pdf)。官方服务能力与本机 Windows Hello PIN 兼容性的真实验收分别记录。

## English

The authorized .227 firmware and companion bridge are installed, based on accepted .225 and the independent .226 candidate. Both settings pages share layout, white switch rows and aligned auxiliary/Save actions; window X or Esc closes each page. Official TightVNC Server 2.8.90 is installed with loopback-only authenticated access, no Viewer, external firewall exception, Web, mirror driver or SAS-policy change. Only the current user's read access was added to its configuration key. Ordinary bridge authentication through the production RFB adapter passed without keyboard input. SCM verifies the exact service PID, own-process type, running state, LocalSystem account and protected installation command; a process-path access denial is accepted only after these checks. The paired device sends one-use intent and progress; the existing DPAPI Windows PIN remains on the PC. All 151 final synthetic checks passed. Exact boot verification confirms .227, matching ELF and ota_0 / VALID. With USB disconnected and Bluetooth off, the user confirmed a successful pure-Wi-Fi unlock and a noticeably faster retest after replacing the fixed six-second pause. PC processing through unlock confirmation measured 3281ms; hardware cancellation/link-loss cases remain pending.

## 已授权部署与运行证据

用户随后明确要求调整布局后安装 TightVNC、构建并刷入新固件、准备 Wi-Fi 验收；此授权继续有效，无需重新询问业务批准。

- 已按用户反馈拉开标题/副标题间距，删除重复 PIN 说明，在标签括号注明 TightVNC 密码；两页统一白底开关与控件布局，移除底部关闭按钮，保留 × / Esc 和关闭清空行为。两个页面共用 `UnlockSettingsStyle`，避免按钮边框/自动尺寸造成视觉错位。
- 通过正常 Explorer 桌面启动部署脚本，先备份五个桥接文件及仓库外的真实用户配置，再正常退出/替换/启动。运行 DLL SHA-256：`bc62a4b85515ba2e296372fe7975d03c1c686f6d6cb368f8deaec53b45f1932c`。真实 Roaming profile、`test=False`、配对与磁盘一致，原配对时间保留。
- 模式、轮播开关、页面顺序与间隔一致。原始快照中的 `cycleStartedAt` 在正常重启后更新，因此初始全对象比较为 false；独立按实际设置字段复核一致，见 `deployment/display-settings-check.json`。没有为 TAB5 执行恢复自动模式命令。
- 设备已完成 Wi-Fi OTA：镜像 7,132,528 字节，压缩传输 4,880,766 字节 / 8203ms。传输耗时不代替 Flash 安装验收。镜像 SHA-256：`5c649efcba3141739ebc03e12f34572dfd717f54a13b8886f6b0a9dc7fad4923`。
- USB 精确启动核验成功：`0.2.227-ui`，ELF SHA-256 `4434c5425fea44dfa4a4d4c82d3574956ec77692ad3b700db5fc51e5212fcf36`，`ota_0 / VALID`，previousError=0；最近解锁失败为 none_since_boot。见 `deployment/ota-verify.json` 与 `boot-verified.txt`。USB COM9、Wi-Fi 和蓝牙仍保留，尚非纯 Wi-Fi 验收环境。
- 官方 TightVNC 2.8.90 x64 MSI 已下载核验并获用户 UAC 确认后安装：2,543,616 字节，SHA-256 `9693cb4cdc2e7e482e8e3f5890415b57ef87de3369fa5c337db516218a1170ce`，Authenticode Valid，OOO GlavSoft。服务以 LocalSystem 自动运行，仅安装 Server。安装脚本使用 Windows Installer API，组件密码在本机随机生成并标记 MSI 隐藏属性，不进入进程命令行/源码/输出；Windows PIN 不由安装脚本读取。
- 安装后发现厂商默认注册表 ACL 拒绝普通用户读取；通过正常 UAC 给当前 SID 增加单个 Server 配置键的 ReadKey，不继承、不增加写权限，保留前后 DACL。初次 PowerShell Provider 操作失败后，改用明确 Registry64 原生 API 完成；没有修改 Windows PIN、账户或服务进程 ACL。见 `deployment/vnc-config-read-permission.json`。
- 厂商 SYSTEM 进程另外拒绝 OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION)，实测错误 5。桥接增加上述 SCM 身份核验并保持普通权限，新增 8 项拒绝路径检查。最终 Release 构建及 145 项自测通过。
- 权限兼容版桥接通过正常退出/备份/替换/启动部署，DLL SHA-256：`9afa1bccdab6dac0108f88262612234b22e17874b621c2ea7068fe91cb7d4b81`。见 `review-service-check-fix.json`、`source-manifest-service-check-fix.json` 与 `deployment/service-check-fix/bridge-result.json`。重启后显示设置复核一致，重新运行精确启动核验通过，没有再次刷写同一固件。后续加速版见文末。
- 最终安装目录中的生产 DLL 在真实普通桌面 profile 完成 RFB 认证，`inputSent=false`、`unlocked=false`，见 `deployment/vnc-final-connection-check.json`。此证据只证明服务认证路径，不证明安全桌面按键和 Windows Hello 解锁。
- 当前配对设备的 PIN 和独立 Wi-Fi 访问均已启用，只记录布尔结果、不输出凭据。2026-10-19 07:59 的组件快照：6 个文件 / 2,134,129 字节，两个厂商进程合计约 24.28 MiB 工作集、4.29 MiB 专用内存；这是瞬时测量。TCP 本地端点为 `127.0.0.1:5900`，Server 签名 Valid；见 `deployment/credentials-readiness.json`、`deployment/vnc-installed-measurement.json`。
- 补充桌面 `--status-once` 启动调用被自动审批以 `blocked by policy` 拒绝，没有更具体原因；该命令未执行，没有更换入口重试。先前候选状态检查成功，最终运行桥接通过本机 HTTP 18 字段状态、真实 profile 和 OTA/USB 启动核验复核。拒绝并不撤销用户已给出的安装/部署授权。

实际安装、初次认证、资源测量和当前状态分别记录在 `vnc-install-result.json`、`deployment/vnc-final-connection-check.json`、`deployment/vnc-installed-measurement.json` 和 `deployment/current-state.json`。部署、握手、合成测试与后续真实解锁分别记录，不相互代替。

### 2026-10-19 08:15 现场验收第 1 项

用户确认已拔 USB 并关闭电脑蓝牙。08:15:00 与 08:15:38 两次实时诊断均为 USB 未连接、Windows 蓝牙关闭、Wi-Fi 已联网且桥接正常。该状态只接受最近 8 秒内通过配对认证的 Wi-Fi 状态请求，两次相隔 38 秒，因此不能由一次旧请求维持。见 `deployment/pure-wifi-readiness.json` 与两份采样。

本项通过，`current-state.json` 已更新 `pureWifiVerified=true`。未向 Windows 发送测试按键。下一项仅由用户按 Win+L 锁屏、确认数字 PIN 后，在 TAB5 原控制页点击一次解锁；失败不重复点击，先保留现场。

English acceptance step 1: The user unplugged USB and disabled PC Bluetooth. Two live observations 38 seconds apart confirmed USB disconnected, Bluetooth off, and authenticated Wi-Fi traffic within the eight-second freshness limit. Pure Wi-Fi readiness passed; subsequent unlock results are recorded below.

### 第 2 项：真实解锁成功及延迟反馈

用户随后确认在相同纯 Wi-Fi 环境中点击一次解锁后已进入 Windows 桌面，耗时接近 10 秒。见 `deployment/first-unlock-success.json`。这个结果属于桥接 `9afa1bcc...`，不转移为后续优化版本的验收结果。

现场固定开场等待 6000ms 是主要确定性耗时。优化版改为连续锁屏/亮屏状态稳定 500ms；未知/熄屏/壁纸状态重置计时，仍保留准备上限、唤醒间隔、逐位单次发送、自动提交后不发 Enter、取消和租约。只修改电脑桥接，不修改 `.227` 固件、协议或 TAB5 的 100ms 轮询。新增成功耗时诊断只记录总时间、准备时间及输入/确认时间，不记录 PIN、输入长度或请求标识。

优化版 Release 构建 0 警告 / 0 错误，151 项合成测试通过；新增覆盖短稳定期不读取 PIN、未知状态重置稳定计时、准备后取消无输入及成功耗时不含凭据。精确候选见 `review-fast-ready.json`、`source-manifest-fast-ready.json` 与 `changes-fast-ready.patch`。当前不将合成时钟下的加速结果称为真实 Windows 耗时。

English acceptance and latency update: The user confirmed a real pure-Wi-Fi unlock into the Windows desktop with bridge `9afa1bcc...`, taking approximately ten seconds. The follow-up bridge replaces the unconditional six-second observation pause with 500ms of continuous locked/display-on observations, resetting on unknown/off/cover states. Bounded wake-up, cancellation, leases and no-replay behavior remain. The firmware is unchanged. All 151 synthetic checks passed; real latency of the updated bridge requires a new user-triggered test.

### 加速版实机复测通过

最终桥接 DLL SHA-256 为 `aa71da62bd96bcf39620950e6645540de520f8455a125bc6d865b21b94125623`。已备份、正常退出并部署，配对和显示设置一致；生产 RFB 认证复核通过，没有重刷 `.227`。

用户保持 USB 断开、电脑蓝牙关闭，再次锁屏并点击一次解锁，反馈“成功，明显快了”。电脑端诊断确认 `unlocked; totalMs=3281; preparingMs=1906; inputAndConfirmationMs=1375`。计时从电脑开始处理请求（RFB 连接前）到后续 poll 确认 Windows 已解锁，不包含 TAB5 发请求前的界面时间或最终关闭面板时间，不将初次体感约 10 秒与本次电脑计时当作同一种精确端到端测量。

当前主链路与加速复测通过，证据为 `deployment/fast-ready/real-unlock-result.json`、`real-unlock-diagnostics.txt` 与 `deployment/current-state.json`。取消、断线不重放、错误后不自动重试的实机边界项尚未执行。

English final retest: Bridge `aa71da62...` was installed with pairing and display settings preserved. The user confirmed another successful pure-Wi-Fi unlock and noticeably faster behavior. The PC recorded 3281ms total, including 1906ms preparation and 1375ms input/confirmation. This excludes TAB5 UI time before the request and after confirmation. Primary flow and latency retest passed; remaining hardware boundary cases are not claimed as passed.

### 后续 USB 间歇失败排查（2026-10-19）

用户随后反馈一次成功、第二次失败、手动重试成功，并确认当时插着 USB，电脑已显示 PIN 输入框。现场 09:21 捕获的最近失败为 `00261803`：USB、自动模式、attempt 2、phase 0、缺失 `lockFresh,locked`、`submitted=false`，其余连接/认证/订阅/HID 就绪/连接代次标志均正常。厂商 Wi-Fi 适配器仍保留 08:21 的成功耗时；因此本次按 USB 路径排查。证据为 `deployment/intermittent-20261019-092113/diagnostics.txt` 与 `finding.json`。

在原 `.227` 的 `ui_unlock.h` 中，屏幕准备完成后 `unlock_preparing=0`，一键解锁授权请求等待期间继续依赖普通控制页状态：`computer_remote_lock_state` 只接受 `state->ready && lock_known` 且 6500ms 内的缓存。`lock-screen` 探测只更新显示/壁纸快照，未刷新普通锁屏缓存；普通 `status` 失败也会使 `state->ready=false`。授权还在等待时，该缓存一旦过期/失效，`ui_unlock.h:223` 就取消本次请求，尚未发送 HID。重新打开会刷新状态，所以能恢复。

诊断测试保留实际产品头文件不变，只给 LVGL 替身补齐真实 `computer_remote_lock_state` 的“无效时 locked=false”语义，分别模拟 6501ms 缓存过期、普通状态失效，均复现精确 `00261803`；重新打开并刷新状态后合成路径发送一次。见 `tests/intermittent-repro/run.log`、`computer_control_test.h`。原测试时钟每次前进都会刷新普通缓存，因此此前未覆盖授权等待时缓存单独失效的场景。

现场另有一次 USB RPC 无回复计数；现有记录没有请求操作与失败时刻，无法断定具体哪条请求触发，不能把合成触发方式写成现场完整时序。已确定的是缓存衔接/失效判定分支，尚未修复。运行桥接、`.227` 镜像与产品源文件均未改动，诊断构建覆盖按哈希恢复；没有重新刷机或发送真实按键。既有纯 Wi-Fi 成功记录保留，整体解锁稳定性重新标为未完成验收。

English follow-up diagnosis: With USB connected and the PIN entry already visible, the user observed failure followed by success on a new manual attempt. Device failure `00261803` records phase 0, missing lock freshness and no HID submission, with transport gates healthy. The unchanged firmware UI was reproduced cancelling a pending one-click grant when its ordinary control-status snapshot expired or became invalid; a new open with fresh state succeeded synthetically. The display/cover probe does not refresh that ordinary lock cache. One USB RPC no-response event is recorded, but its operation/timestamp is unavailable, so the exact upstream event remains uncertain. No production changes, hardware input or flash were performed. USB unlock stability is not accepted as fixed.

### 共同锁屏缓存源码修复（暂不构建固件）

用户要求修复，并明确“先不构建新固件”，随后指出不应只按 USB 问题处理。修复落在共享缓存：`computer_remote.c/.h` 为锁屏样本维护独立时间戳，有效 status 与 lock-screen 回复都能更新；普通控制请求失败不清掉仍有效的锁屏样本，也不延长它。未知锁定值立即失效，较旧回复不能覆盖新样本；6500ms 有效期从请求发起计算，包含 RPC 等待时间。

USB / BLE 的已发起一次性授权在缓存暂缺时可等待原请求结果，仍受原 6000ms 超时约束，不再次索取 PIN；取得结果后必须重新通过绑定本次请求的锁屏/显示检查才能 HID 输入。连接变化、明确未锁屏、取消、未知新显示回复或超时仍停止后续输入。Wi-Fi 使用同一缓存进行设备侧状态读取和成功确认；电脑端的实际 Windows 状态检查保持不变。未增加协议字段或调整界面布局。

本地验证通过：USB / BLE 各 7 个针对性 UI 场景、Wi-Fi 共同缓存确认场景、实际 computer_remote.c 的三通道路由替身测试，以及原有完整解锁回归。旧测试替身补齐独立锁屏样本和未来时间拒绝语义；一处原夹具原地写入未来时间戳，现改为先推进假时钟再交付新样本，保留原成功断言。所有构建只生成 Windows 桌面测试程序，覆盖源按哈希恢复。

证据位于 `artifacts/development/wifi-oneclick-20261018/usb-cache-fix-20261019/`：`result.json`、`source-manifest.json`、`changes.patch`、`focused-test.log` 与 `regression-final-test.log`。`before/` 保留此前已构建的源文件；当前 source-snapshot 已包含待构建修复，不应再宣称它与旧 `.227` 编译清单匹配。版本、交付 BIN 和运行桥接不变，未构建/打包新固件、未刷机、未进行修复后的硬件验收。

English source-only fix: The user authorized a common cache repair while explicitly deferring new firmware. Lock observations now have independent request-start timestamps updated by valid status and lock-screen replies. Ordinary controller failure cannot erase or renew a still-fresh lock sample; unknown values, exact 6500ms expiry, delayed replies and older samples are handled conservatively. USB/BLE pending one-use grants retain their original six-second budget without a new credential request, followed by new request-bound lock/display evidence before input. Wi-Fi uses the same device cache; PC Windows-state checks are unchanged. Fourteen targeted USB/BLE UI cases, the Wi-Fi cache path, actual-worker tests and existing unlock regressions passed. Only native desktop tests were built. No new firmware, version change, deployment or hardware acceptance was performed.

Implementation references for the compatibility fix: [QueryServiceStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-queryservicestatusex), [QueryServiceConfigW](https://learn.microsoft.com/en-us/windows/win32/api/winsvc/nf-winsvc-queryserviceconfigw). SCM configuration describes the registered launch command; it is not presented as a direct image-path query of the running SYSTEM process. Final hardware unlock acceptance remains pending.

## 2026-10-19 诊断与通道提示候选（0.2.228-ui）

用户已授权补齐记录、构建及实机验证。按用户布局反馈，去除顶部独立通道标题；自动面板将通道并入主提示，例如“正在准备 USB 解锁”与“正在通过 USB 解锁”，恢复原有图标/提示/按钮位置。打开后所选通道不会随实时连接变化改写。手动面板并入原标题，例如“USB 解锁电脑”；蓝牙配对保持原提示。

设备保存本次启动最近 8 条解锁结果：成功、失败、取消；包含通道、阶段、缺失状态位、锁屏样本年龄、总耗时、USB/BLE 输入前准备与输入后确认耗时、最近控制读取的操作/HTTP 状态/请求年龄。Wi-Fi 的设备侧阶段时间无法等同于电脑端输入时间，因此准备与输入确认两项记录 unknown，电脑端既有 Wi-Fi 诊断仍报告实际分段耗时。未记录 PIN、PIN 长度、按键、账号、屏幕内容或配对码。第一次失败不会因下一次重试成功被覆盖。

回传复用三通道已经配对鉴权、会话/时间/重放校验的 computer-control/status RPC。可选 unlockHistory 字段为 v1、最多 8 行、每行 9 个固定 8 位十六进制整数；总长度最多 585 字节。字段顺序为序号、标志位、设备运行时间、总耗时、锁屏样本年龄、输入前准备耗时、输入后确认耗时、最近控制操作与 HTTP 状态、控制请求年龄。UINT32_MAX 表示未知。标志位：0..1 通道（USB=0、BLE=1、Wi-Fi=2）；2..3 结果（成功=1、失败=2、取消=3）；4..8 原因；9..11 阶段；12 自动；13 HID 已提交（Wi-Fi 不设置）；14 缓存新鲜；15 锁定；16..24 缺失状态位，顺序与既有失败诊断相同。控制操作高 16 位（无=0、status=1、lock-screen=2），低 16 位 HTTP 状态。新旧候选需使用匹配桥接；不改变原有凭据或按键协议。

状态响应有效后才确认记录已回传。失败时保留待回传状态；关闭面板或切换页面后可补发只读状态请求，不触发解锁或按键重发。桥接严格拒绝非数字、超长、非法枚举、乱序与保留位；鉴权失败/旧会话不能写入记录。普通状态响应不清除最近记录。设备重启清空环形记录，桥接显示最后收到记录的时间，避免将旧记录误当实时状态。

本地验证：8 条环形边界、失败后成功保留、取消/重复刷新去重、三通道锁定标签、USB/BLE 各 7 个缓存场景、Wi-Fi 共同缓存确认，以及实际 worker 三通道回传/失败待发/响应确认均通过。Windows Release 构建通过；PIN 135 项、Wi-Fi 151 项、电脑控制和已认证诊断往返通过。并行窗体自测曾出现 PIN 修改断言失败；单独重跑原断言通过，产品窗体未改动。新固件构建和实机结果另补，不以本地测试代替现场验收。

English: Candidate 0.2.228-ui integrates the latched transport into the existing progress text (no separate top title) and an eight-entry numeric-only diagnostic ring shared by USB, BLE and Wi-Fi, including cache age, stages, outcome, durations and last controller-read status. Delivery uses the existing authenticated computer-control/status RPC, with strict bounded parsing, acknowledgement only on a valid response, and read-only delivery after modal close/page changes. Existing credential admission and single-shot input rules remain in place. Wi-Fi device-side split timings are unknown rather than inferred from PC input. Local native and Windows tests passed; firmware build, deployment and hardware acceptance are tracked separately below.

### 0.2.228-ui 最终构建与现场阶段（2026-10-19）

最终界面已按反馈移除独立顶部通道标题，并去掉通道后的冒号；准备、进行中和失败状态将通道并入原主提示。最终候选：BIN SHA-256 `05861a6a11cad8f5889eeac2f2352cc9b76ea899f3000244ccc85d34c414f2be`，ELF SHA-256 `b6a00343ede99c57b3668da073c0aad55eab2ab7e7baea6f5cdd8fc9640857ed`，7,134,896 字节；Windows DLL SHA-256 `a3990ae6850c080c826f7fe801a2fdcd49be930562568876441532a1475a7c0c`。最终产物在 `compiled-228-final/`，已通过固定 latest 校验和实际 sidecar 预览。此前两份界面中间稿未作为本轮最终交付，保留构建证据。

配套桥接已按普通桌面档案更新，`test=False`、`pairMatchesDisk=True`，显示设置保留；生产 TightVNC 适配器认证通过且连接检查没有发送输入。首次发起升级时忙状态拒绝，未创建升级请求；随后设备已启动/回报新版本，未继续触发额外刷写。真实升级记录为 Wi-Fi 通道，传输 18,406ms、写入 7,076ms、结束安装阶段 17,988ms。首次启动核验发生在设备约 13 秒运行时，未通过最终状态检查；等待固件内 30 秒连续健康启动确认后，三次新 USB 心跳、准确 ELF、ota_1、VALID 和 stateError=0 均通过。全部现场记录见 `diagnostics-channel-20261019/`。

USB 第 1、2 次真实锁屏解锁均由用户确认成功，且提示为 USB。设备记录分别为：总耗时 8,219/8,038ms；准备 7,104/7,336ms；输入确认 1,115/702ms；锁屏样本年龄 98/199ms。两条记录同时保留并回传；用户第 1 次体感接近 10 秒。暂未复现之前第二次失败，不将两次成功写成全部间歇性问题验收完成。准备阶段仍含原有 6 秒锁屏/显示观察保护，速度单独列为待优化项，不能直接删掉保护窗口。纯 Wi-Fi 的物理隔离及持续新鉴权已核对，后续解锁、重复、取消、断线和 BLE 结果按逐项现场确认补充。

English deployment: Final layout integrates transport into the existing status text without a separate top heading or colon. Exact .228 ELF, ota_1 VALID and three fresh USB UI heartbeats passed after the firmware's healthy-boot confirmation window. The matching ordinary-profile bridge and production RFB authentication passed; display/pairing settings were preserved. Two user-confirmed USB unlocks succeeded, taking 8219/8038ms, including 7104/7336ms preparation and 1115/702ms input/confirmation. Both records survive in the device history. USB preparation latency and remaining wireless/boundary acceptance are still open; two successes do not establish full intermittent-failure acceptance.

现场继续：纯 Wi-Fi 条件下用户确认解锁成功且通道显示为 Wi-Fi；设备实际保留两次成功（attempt 3/4），总耗时 3,386/3,579ms。电脑端最近一次为 3,344ms，准备 1,922ms、输入与确认 1,422ms。USB 未连接、Windows 蓝牙关闭且新 Wi-Fi 鉴权/诊断上传持续更新，未用旧 USB ACK 作为当前纯 Wi-Fi 运行证明。两次 USB 记录仍保留。取消和断线边界测试继续逐项确认。

English: Two pure-Wi-Fi success records were observed (3386/3579ms device total); the user confirmed success and the Wi-Fi label. PC split timing for the latest attempt was 3344ms = 1922ms preparation + 1422ms input/confirmation. Fresh authenticated Wi-Fi uploads continued with USB unplugged and Bluetooth disabled. Earlier USB records remained present. Boundary acceptance remains in progress.

取消边界：用户确认立即取消后 Windows 仍停在锁屏/PIN 页面，未进入桌面；设备 attempt 5 记录 cancelled，经过 977ms，电脑端同样为 cancelled。USB 1/2 与 Wi-Fi 3/4 成功记录仍在。English: Real cancellation passed: the user confirmed Windows stayed locked, while the device recorded attempt 5 cancelled at 977ms and the PC also reported cancelled; earlier success records remained present.

断线边界：用户确认 USB 解锁开始后拔线，保持锁屏且面板显示未完成，没有自动重试；attempt 7 为 USB/failure、peer_or_lock_state_changed、phase 4、1,123ms、hidSubmitted=False，最近 lock-screen 读取 HTTP 200、年龄 248ms。说明失败对应明确通道断开，未将新鲜锁屏数据误当成仍可发送键盘输入。此前取消后的显式新 Wi-Fi 解锁（attempt 6）正常成功，3,368ms；取消不会永久阻断下一次用户操作。English: Actual USB disconnect stopped at 1123ms without HID admission or automatic retry; the user confirmed Windows stayed locked and the panel reported incomplete. A separate explicit Wi-Fi attempt after cancellation succeeded (3368ms).

### 本轮实机验收收尾

蓝牙总开关原先仍关闭，用户重新打开后恢复鉴权和 HID 就绪；未改配对、未重启桥接。蓝牙两次解锁均由用户确认成功且提示为蓝牙：attempt 9/10，总耗时 8,215/8,114ms，准备 7,415/7,300ms，输入与确认 800/814ms。USB 保持断开；Wi-Fi 控制状态通道仍可用，因此本项证明蓝牙 HID 解锁，不宣称网络完全隔离的纯 BLE 测试。

最终实机环形记录恰好保留 attempt 3–10 共 8 条，取消 5、USB 断线失败 7 在后续成功 8/9/10 后仍保留；更早的 USB 1/2 已保存在单独证据中，不以环形覆盖视为丢失验收数据。本轮共 8 次成功（USB 2、Wi-Fi 4、BLE 2），取消 1 次、故意断线 1 次；用户逐项确认所要求的结果。准确镜像、分区 VALID、配套桥接、本地必要回归和实际 sidecar 预览均通过。未进行长时间压力测试、Wi-Fi 中途断网或网络隔离的纯 BLE 专项，不扩大本轮通过范围。

剩余优化：USB/BLE 总耗时约 8.0–8.2 秒，主要为 7.1–7.4 秒准备，其中含原有固定 6 秒屏幕状态观察窗口。Wi-Fi 约 3.4 秒。后续优化应以新的连续新鲜、稳定锁屏/显示证据替代固定等待，保留未知/断线/取消时不发送和不重放 PIN 的边界。本轮不再修改或构建新的延迟候选。最终状态：USB 拔出，Windows 蓝牙开启且已鉴权，Wi-Fi 正常；TAB5 保留当前控制页面和用户展示设置。

English closeout: Two user-confirmed BLE HID unlocks passed (8215/8114ms; preparation 7415/7300ms; input/confirmation 800/814ms). Wi-Fi controller status remained available, so this is not a pure BLE network-isolation test. The final ring contains exactly attempts 3–10, preserving cancellation 5 and intentional USB failure 7 after subsequent successes. Scoped hardware acceptance passed: eight real successes across USB/Wi-Fi/BLE, one cancellation and one intentional disconnect. Long soak and Wi-Fi mid-request network-loss tests were not performed. USB/BLE fixed preparation observation remains a separate latency optimization item; no additional latency build was made.

后续授权：用户继续要求优化 USB/蓝牙准备耗时，新的 .229 候选与测试记录见 [USB/BLE 准备阶段提速](TAB5-USB-BLE-FAST-PREPARATION-20261019.md)。本文件上述 .228 性能数字保留为基线，不与新候选结果混用。English: Subsequent user authorization starts a .229 USB/BLE preparation optimization; the linked record tracks its build and hardware results separately from the .228 baseline.
