# TAB5 USB / BLE 准备阶段提速（2026-10-19）

当前结论：本轮范围内验收通过。TAB5 已安装 0.2.229-ui，电脑端已部署文末的短 RPC 调度补修。USB、黑屏唤醒、补修后蓝牙连续两次及两次手动取消通过；无线启动核验实机使用 Wi-Fi 通过。早先蓝牙第 5 次授权超时及全部日志保留，没有用后续成功覆盖失败现场。

本轮用户要求继续优化 USB 与蓝牙，重点缩短准备等待。基线是实机通过的 0.2.228-ui：USB 两次总耗时 8,219/8,038ms，准备 7,104/7,336ms；BLE 8,215/8,114ms，准备 7,415/7,300ms。主要耗时来自打开面板后的固定 6 秒只读观察。

## 实现

0.2.229-ui 本地候选只修改 `ui_unlock.h` 的开场准备判断。连续绑定不同请求的新鲜锁屏/显示回复，至少两条跨越 500ms，显示与封面状态保持一致后继续。单次旧 On 结果或重复 UI 刷新不能放行。未知、熄屏、回复超时、键盘未就绪、状态变化或超过 1 秒的观测间隔会重置观察。

总准备预算仍为 12 秒；取得一次性 PIN 授权后仍做新的请求绑定屏幕检查，再允许一次 HID 输入。熄屏 Shift 的 1.5 秒等待、封面 Space 的 350ms 等待及有限次数保持原规则。取消、断线、会话变化、6 秒授权超时以及不自动重发 PIN 的边界保留。此判断仍依赖 Windows 已发布的显示状态，不能证明键盘焦点或用户所选登录方式；沿用用户正常的数字 PIN 方法。Wi-Fi 解锁、界面布局、诊断协议及 Windows 桥接二进制均未修改。

## 本地验证

完整既有解锁原生回归通过，包括 wake/settle、未知/延迟显示、手动确认、连接恢复、取消、超时、USB/BLE 单次 PIN、Wi-Fi、记录保留。新增 USB/BLE 各 8 个准备场景，验证快速路径小于 1.5 秒（模拟）、重复缓存读不放行、未知/长间隔/未就绪/读超时重置、取消、连接变化、Off 唤醒。原缓存修复 USB/BLE 各 7 项和 Wi-Fi 确认，以及实际 worker 的独立缓存与三通道诊断回传也通过。

只有准备判断变化对应的旧 6 秒断言改为稳定新读数的行为断言；原超时、安全与单次输入断言保留。实际准备耗时必须以新固件实测记录为准，不能把模拟小于 1.5 秒写成实机结果。

证据：`artifacts/development/wifi-oneclick-20261018/usb-ble-preparation-20261019/`。新镜像构建、准确启动核验与逐项实机结果在下方继续追加。旧 .228 及其验收证据保留，不推送或发布。

English: This local .229 candidate replaces the unconditional six-second USB/BLE opening observation with consistent, fresh, request-bound samples spanning 500ms. Unknown/off/changed states, missed reads, unready HID and long sample gaps reset observation. The original bounded wake settling, single-use PIN grant, new final display proof, cancellation, transport pinning and no-replay rules remain. Native regressions and 16 new transport-specific preparation cases passed, together with the previous common-cache cases and actual worker tests. Simulation timings are not hardware acceptance. No Windows binary, Wi-Fi unlock path, layout or protocol changed.

## 构建与部署

0.2.229-ui：BIN SHA-256 `aade492214f81e4d73105242f0d740d063936ac55ac840c87981599d5d3c49a3`，ELF SHA-256 `f5134ef1146793f770064225c9f71535b7f6b9dd37518d1d29f2290e49331f29`，7,135,152 字节。构建源逐文件与 manifest 比对，产品改动仅 `ui_unlock.h`；覆盖源已按哈希恢复。固定 latest 和实际 sidecar 预览通过。桥接 DLL 保持 `a3990ae6850c080c826f7fe801a2fdcd49be930562568876441532a1475a7c0c`，仅正常重启加载新镜像，配对和展示设置保留。启动核验确认准确 ELF、ota_0、VALID，读取到真实 UI 心跳；镜像已实际安装。初始本机启动命令遇到既有传输忙状态被拒绝，后续仅观察已开始的升级，不重复刷写。

USB 普通亮屏解锁 2 次均由用户确认成功且明显更快：总耗时 2,861/2,884ms，准备 1,660/1,766ms，输入确认 1,201/1,118ms。与 .228 两次基线的平均值对比，整体减少约 65%，准备减少约 76%。熄屏及 BLE 测试另行追加，不以普通亮屏通过代替。

English deployment: Exact .229 ELF and ota_0 VALID passed with fresh USB UI heartbeats; the existing bridge binary, pairing and display settings were retained. Two user-confirmed normal USB unlocks took 2861/2884ms (preparation 1660/1766ms), about 65% less total and 76% less preparation than the .228 baseline average. Black-screen and BLE acceptance remain separate.

USB 熄屏场景由用户确认“从黑屏成功唤醒并解锁”，未先触碰电脑键鼠。设备第 3 条记录为成功，总耗时 3,875ms，准备 2,870ms，输入确认 1,005ms。新准备判断的实际熄屏唤醒主链路通过；BLE 与无线启动核验待逐项继续。

English hardware update: The user confirmed wake and unlock from a fully black PC screen without first touching the PC keyboard or mouse. Attempt 3 succeeded in 3875ms, including 2870ms preparation and 1005ms input/confirmation. BLE and wireless boot verification remain pending.

## BLE 第二次失败现场（保留）

首次蓝牙解锁用户确认成功且明显更快，设备第 4 条记录总耗时 3,075ms，准备 1,973ms，输入确认 1,102ms。第二次用户报告失败且未重试，第 5 条记录为 `credential_timeout`：总耗时 7,792ms，`hidSubmitted=False`，当时缺失 `bridgeFresh,hidReady`，未提交 PIN。不能将新候选的 BLE 重复验收记为通过。

现场蓝牙 RPC 记录出现排队 1,422ms、回复阶段 1,453ms，桌面处理仅 47ms。代码检查发现短 RPC 未进入状态同步暂缓条件，只有 bulk RPC 获得优先；完整状态传输占用同一 ATT gate，可能挡在短请求和回复之间。当前日志不足以重建失败那一轮全部排队时序，因此保留为有现场延迟支持的调度根因假设，先做确定性复现与最小修复验证。原 6 秒授权超时、单次 PIN 与不自动重发要求不变。

English failure evidence: BLE attempt 4 succeeded in 3075ms (1973ms preparation). Attempt 5 failed with a six-second credential timeout before any HID submission; the user did not retry. The live RPC diagnostic showed 1422ms queue and 1453ms response time versus 47ms handler time. Only bulk RPCs currently defer status transfers on the shared ATT gate. This is a code-confirmed scheduling gap and a plausible cause of the hardware timeout, not a complete historical trace. BLE repeat acceptance remains open pending reproduction, correction and hardware retest.

## 短 RPC 调度补修

新增确定性排队场景在旧调度下复现 `Short authorization handler lost ATT to queued telemetry`。补修桥接 `Tab5BleVoice.cs` 与 `Tab5BleClient.cs`：新请求处理和回复期间暂缓状态同步，成功完成后最多再保留 500ms 间隔给后续交互；空轮询和重复 mailbox ID 不续期，失败/取消立即释放。排队中的状态帧在取得 gate 后重新检查；原最长 6 秒强制状态刷新、bulk 射频参数、USB 优先待命与语音轮询规则保持不变。没有延长一次性授权期限或允许自动重试 PIN。

Windows Release 构建零警告零错误；新排队回归由失败转为通过，并覆盖处理/回复/间隔、6 秒刷新、重复/空轮询、失败/取消。BLE voice/mailbox MTU 23/247/517、bulk 与重放、BLE transfer/standby、telemetry 连续读和 full/delta ACK、135 项合成 PIN、加密 RPC 回归通过。

桥接 DLL SHA-256 更新为 `fbf812720b9b243afe070a9cb2ac044040e601993b040b6822bd0ccd146f8ce2`，正常退出后部署，原运行文件与私有配置已备份，配对和展示设置保持。TAB5 镜像仍为上述 .229，无需重刷。新桥接再次经 Wi-Fi 实机核验准确 ELF、ota_0 VALID 与 3 次新鲜心跳；蓝牙解锁复测另行记录。

English correction: The deterministic queued-telemetry case failed with the old scheduler and passed after short RPC handlers/responses gained bounded telemetry priority plus a 500ms successful-completion gap. Idle/duplicate polls do not renew it; failure/cancellation releases it. The six-second mandatory status refresh, existing bulk radio parameters, USB standby and voice polling remain. Release build and focused BLE/telemetry/PIN/encrypted-RPC regressions passed. The local bridge was deployed with backups and pairing/display preservation; firmware .229 was not reflashed. Exact wireless Wi-Fi boot verification passed again; BLE hardware retests remain pending.

补修桥接后，用户两次确认蓝牙解锁成功且明显更快。第 6/7 条总耗时 3,090/2,683ms，准备 1,985/2,082ms，输入确认 1,105/601ms；对应授权 RPC 排队均为 0ms，回复 78/15ms。相比 .228 两次 BLE 基线均值，总耗时减少约 65%，准备减少约 72%。两次成功后原第 5 条失败仍保留。Wi-Fi 保持开启，因此这是 BLE 键盘解锁场景，不是仅 BLE 隔离联网测试；也不等于长期无间歇失败保证。快速取消另行验收。

English BLE retest: With the corrected bridge, user-confirmed attempts 6/7 took 3090/2683ms total, 1985/2082ms preparation and 1105/601ms input/confirmation. Both authorization RPCs had zero queue time; responses took 78/15ms. Compared with the .228 BLE baseline averages, total time fell about 65% and preparation about 72%. The earlier failure remained visible. Wi-Fi stayed enabled; this validates BLE HID unlock rather than an isolated BLE-only network path. No long-duration reliability claim is made. Cancellation acceptance remains separate.

## 最终验收结果

| 场景 | .228 准备耗时 | .229 准备耗时 | .229 总耗时 | 结果 |
| --- | --- | --- | --- | --- |
| USB 普通解锁，两次 | 7.10 / 7.34s | 1.66 / 1.77s | 2.86 / 2.88s | 用户确认均成功且明显更快 |
| BLE 补修后解锁，两次 | 7.42 / 7.30s | 1.99 / 2.08s | 3.09 / 2.68s | 用户确认均成功且明显更快 |
| USB 黑屏唤醒并解锁 | 未在本轮基线测量 | 2.87s | 3.88s | 用户确认成功 |

USB 与 BLE 普通解锁总耗时均减少约 65%；准备均值分别减少 76.3% 和 72.4%。USB 测速在桥接 BLE 调度补修前采集，固件及 USB 路由保持原实现；没有把它写成补修桥接后的新增 USB 实测。

第 8/9 条均为 BLE `cancelled`，分别 733/1,589ms，`hidSubmitted=False`。用户确认两次都是手动解锁后取消，等待后仍在锁屏/PIN 页面；没有自动重试。取消后的最近 8 条窗口仍保留第 5 条失败和第 6/7 条成功。构建与实际镜像哈希、运行桥接哈希、配对与展示设置、`git diff --check` 均已复核。

最终证据：同一证据目录的 `acceptance.json`、`timing-comparison.json`、`bridge-scheduling-review.json` 与 `ble-scheduling-cancel-pass.json`。初始 `review.json` 明确登记被调度补修取代；构建、模拟、USB 核验和无线核验分别记录。独立状态命令早先被现有 profile/自动审核保护拒绝后未重试；现场运行态通过正常桌面启动、既有 HTTP 诊断及真实解锁核对。未推送、未公开发布；未做长时间压力测试、Wi-Fi 解锁重复测速或仅 BLE 隔离联网的启动核验。

English final acceptance: The scoped suite passed. Normal USB and corrected-bridge BLE unlock totals each fell about 65%; mean preparation fell 76.3% and 72.4%, respectively. USB black-screen wake succeeded. Both manually initiated cancellations (733/1589ms) submitted no PIN and left Windows locked; the user confirmed both were manual, with no automatic retry. Exact Wi-Fi wireless boot passed on the final deployed bridge. USB speed samples predate the bridge-only BLE scheduling correction; the firmware and USB routing were unchanged. Evidence, payload hashes, pairing/display preservation and whitespace checks are recorded. No public release, long soak, repeated .229 Wi-Fi unlock timing or isolated BLE-only boot verification is claimed.
