# TAB5 传输修复与 macOS 交接

2026-11-17。来源为 Windows 桥接和 TAB5，接收端为 macOS。交接覆盖开发分支 `codex/tab5-calendar-and-claude-idle-20261116` 上的 `759a5f7`、`a2e38f3` 及本次文档补充。Windows 候选已在本机驻留，TAB5 已安装 322-R6。授权范围为本地提交和推送该开发分支。

## 故障与已实施的修复

- 在配对、无注入的 322-R5 上，真机复现过传输停在约 96 或 144 KiB，TAB5 收不到 RPC 回复并记为 `-1`。追踪发现状态分片写入或整帧确认超时，以及语音邮箱首次轮询读超时，都能终止共享 BLE 会话并取消正在回写的 RPC。偏移量随触发时间变化。
- Windows 的状态分片逐片等待 ATT 确认，分片和整帧 ACK 轮询之间允许 RPC 与语音使用 GATT。只有匹配的已认证整帧 ACK 才提交状态。连接仍有效时，状态超时清除未确认 delta 基线，在状态特征上留出 5.1 秒静默，让固件 5 秒残片超时生效，再发完整状态。连续允许两次恢复，成功 ACK 后重置计数。
- 空闲邮箱读在取得 GATT 后开始 3 秒本地计时。收到请求头之前超时，且连接仍有效时，最多连续重试两次。已开始的请求继续使用现有错误和取消路径。协商了重放能力的 RPC 回应只重放当前窗口的原有密文，逐片等待 ACK 后才提交下一片，并在分片间让出队列。
- ATT 特征、认证格式、协议版本 1、USB 速率 460800、原链路选择和兼容能力协商保持原有约定。322-R6 相对 R5 的产品变更是「总览」萌宠状态单行，补丁由 TAB5 固件工程应用。[固件补丁](TAB5-OVERVIEW-SINGLE-LINE-322-R6-20261117.patch)

## 对齐清单

| 优先级 | Windows 实现和证据 | Mac 接手位置 | 状态与验收要求 |
| --- | --- | --- | --- |
| P0 | 状态写入、整帧确认和恢复。`Tab5BleClient.SendStatusFrameAsync`、`Tab5BleStatusRecovery`。真机出现过 6 次状态恢复，期间无 BLE 会话中断。 | `MacTab5BLE.swift` 的 `tick`、`sendStatus`、`confirmFrame` 与 `reset`，以及 `MacTab5BLEStatus.swift`。 | 待验证。用 CoreBluetooth 回调区分单项写入、整帧确认和底层断开。已认证且仍连接时，验证状态失败可以独立恢复并释放调度，过期回调不能推进新发送或更新 delta 基线。 |
| P0 | 空闲读超时隔离。`Tab5BleGattMailbox`、`Tab5BleVoice.PumpAsync` 和 `Tab5BleClient.PumpVoiceAsync`。超时与重试边界由故障注入覆盖，真机覆盖压力下轮询。 | `MacTab5BLE.swift` 的 `read`、`didUpdateValueFor`、`pump`、`tick` 和 `finishMailbox`。 | 待验证。区分首次读、已收请求头、订阅/授权窗口及真正断线。核对迟到回调、取消和重新配对的代际边界。验证短暂空闲读超时不会取消另一邮箱的回复。 |
| P0 | RPC 分片有序重放。`Tab5BleMailboxWindow.WriteBatchRecoverableAsync` 与对应自测。 | `MacTab5BLEWriteSchedule.swift` 已支持确认当前窗口的 `0x0e` 重放。检查 `MacTab5BLE.swift` 的 `writeNext` 和 `didWriteValueFor` 在 `replaying` 状态下的调度。 | 待验证。只确认是否能在 ACK 之间服务语音。保留加密字节、ID、顺序和能力协商，确保业务请求只执行一次。 |
| P1 | 前一阶段的状态发布与图库原链路重试。状态生成移出 Windows 传输路径，R4/R5 固件已补同链路重试和 TF/HTTP 诊断。 | `MacTab5Service.swift` 的 `publish`，以及现有 USB、Wi-Fi、BLE 状态读取路径。设备侧图库行为由共用固件提供。 | 待验证。先确认 USB、Wi-Fi 和 BLE 读取已生成的快照，慢采集或资源发送不能阻塞邮箱。连同设备验收确认 HTTP 响应头前的无响应仍走原链路，认证及内容错误仍直接返回。Windows WinRT 的计时和取消参数仅供对照。 |
| 范围 | 322-R6 萌宠状态单行。离屏渲染通过，实际文案宽 147 px，分配宽度 176 px，真机精确镜像启动核验通过。 | 共用 TAB5 固件。 | 已有无需修改。Mac 连接 R6 时验证状态照常更新即可。 |

## `759a5f7` 的功能和修复

### TAB5 日历入口

- **Mac 是否需要改**。已有无需修改。应用页第三行新增「日历」卡片，整卡可点击，返回时回到应用页。总览日期保持原有入口。改动在共用固件，日期、节日和生日的数据接口沿用现有协议。
- **已有验证**。322-R1 通过 ESP-IDF 构建、应用卡片实际触控目标的离屏回归，以及日历节日、生日和返回路径的离屏检查。物理 TAB5 经 Wi-Fi OTA 安装 R1，启动镜像和展示设置已核对。九宫格点击回归由离屏预览完成。全量预览遇到的总览节气断言也在 321-R5 原预览器复现。[R1 构建和验证](TAB5-322-R1-APPS-CALENDAR-20261116.md)
- **待验收**。Mac 桥接连接 322-R6 后，从「应用 → 日历」打开并返回应用页，再从总览日期打开。用已配置的节日、生日数据核对内容与返回路径。

### Claude 空闲识别

- **Mac 是否需要改**。Mac 源码已随 `759a5f7` 更新，先做原生验证。`MacClaudeNativeClient.allLiveTerminalsIdle` 核对同用户 CLI 会话、进程身份、启动时间和可执行文件，并要求所有已验证的活终端都报告 `idle`。`ActivityMetadata.swift` 在历史日志兜底中保留空闲判定，精确任务状态和待输入信号仍优先。
- **已有验证**。Windows Release 构建、运行真实 Claude 终端时的 `--status-once`、活动与工作区回归已通过。Mac 已增加 `ActivitySamplingTests.testVerifiedIdleTerminalSurvivesAgedLogsUntilNextSample`，覆盖日志超过 15 分钟仍显示 `idle`，下一次采样失去活终端证据后恢复 `offline`。该用例待 macOS 运行。[两端实现与 Windows 验证](TAB5-322-R1-APPS-CALENDAR-20261116.md#windows-与-mac-桥接)
- **待验收**。在 macOS 跑 `swift test` 和 Release 构建，再用真实 Claude Code CLI 验证空闲、运行、等待输入、关闭的状态变化。加入多个终端的空闲与运行混合场景，确认工作或待输入不会被空闲提示覆盖。若原生验证发现差异，再修改对应识别逻辑。

### Windows USB 解锁和登录准备

- **Mac 是否需要改**。Windows 登录助手、系统任务优先级和 LogonUI 安全桌面焦点检查均为平台专用，Mac 无需移植。322-R2 中控制 RPC 优先与解锁后刷新锁态属于共用固件。Mac 保持 `MacHostUnlock.swift` 的原生授权路径，USB/BLE 使用本机 `host-unlock`，Wi-Fi 使用 `wifi-unlock`。普通 `lock-screen` 查询在 Mac 上返回未知锁态，Windows HID 凭据发放与确认逻辑不适用于该路径。
- **已有验证**。Windows 登录助手及 PIN 回归通过。322-R2 真机用 Windows WTS 与已认证 USB 回执交叉核对，锁屏和首次解锁均为 5/5，面板打开到确认的中位数为 5.120 秒，仍高于 3–4 秒的目标。该组覆盖 Windows USB 和短间隔锁屏，Mac 没有对应真机结果。[Windows R2 真机记录](TAB5-322-R2-USB-UNLOCK-20261116.md#usb-真机复测)
- **待验收**。用 Mac 原有授权与已配对的 322-R6，确认控制页锁屏、已启用的 Mac 解锁路径、取消及断线恢复。USB/BLE 本机解锁和 Wi-Fi 解锁分别取证，记录首次成功率与耗时，核对真实系统解锁事件和 TAB5 回执。3–4 秒仍是 Windows USB 的待验收目标。

### Codex 会话切换

- **Mac 是否需要改**。Windows 修复在提权桥接里经当前交互桌面的 Explorer 打开 `codex://threads/{GUID}`，Mac 无需移植。`MacTab5Desktop.swift` 的 `desktop/open-recent` 已走 `NSWorkspace` 与辅助功能校验，先复验现有实现。对标题无法唯一定位的会话，已有 open-only 路径可确认前台应用，精确会话定位和后续草稿操作仍需目标证据。
- **已有验证**。Windows Release、直达、协议和状态读取回归通过。Windows 实机曾复现只将 Codex 置前台、仍停在原会话。正式运行的修复候选通过 Wi-Fi 接收与「定位」按钮相同的已认证请求，在 3.085 秒返回 `200`，并核对前台与目标会话 ID。TAB5 物理按钮及面板变化尚未验收。Mac 有 `MacCodexDirectValidation.swift` 和 `MacCodexNavigationTitleTests` 相关覆盖，本轮未在 macOS 执行。[Windows 会话定位记录](TAB5-322-R1-APPS-CALENDAR-20261116.md#codex-直达会话切换)
- **待验收**。Mac 授予现有辅助功能权限后，让 Codex 先在前台停在另一条会话，再从 TAB5 物理按钮定位。用前台目标身份和面板回执确认实际切换，再覆盖后台启动、同名会话与权限缺失。定位失败时核对设备选择和草稿，只有确认复现 Mac 侧缺陷后才调整原生实现。

## 构建与验收记录

- Windows 本机驻留 `AIBotBridge.dll` 的 SHA-256 为 `75ff24354a9699ca51149ca24a02f9b4206055e0d660996f11c764689ade1eff`。真机包来自当时包含同期本地改动的工作区。`a2e38f3` 交付对应的 BLE 修复，候选包的摘要和真机结果只绑定上述 DLL。
- 前一阶段的 Windows 状态发布和 R3–R5 开发资料仍在独立的本地改动中。Mac 可从本提交开始核对 BLE 调度，按需向 Windows 侧取用那些资料。
- Windows Release、`--status-once`、`--self-test-tab5-ble`、`--self-test-tab5-ble-voice`、`--self-test-tab5-host-binding` 和带独立输出目录的 `--self-test-tab5-integrated-transfer` 通过。自测覆盖 MTU 23/247/517、片序、ACK、窗口重放、超时与取消。恢复计数和空闲读超时通过定向注入验证。
- 从 `a2e38f3` 的暂存源码独立重建 Windows Release，编译 0 警告、0 错误，上述五项检查再次通过。日志位于本机 `artifacts/ble-rpc-stall-20261117/commit-verification-checks/`。
- Windows + 无注入 322-R5 真机完成三轮，每轮上传和下载各 3 次 256 KiB。18/18 通过字节、摘要和偏移校验，平均上传 11.0 KiB/s、下载 8.0 KiB/s。语音邮箱成功读 528 次，最长 3.375 秒，最终这组三轮未自然触发空闲轮询超时。尚未在真机走完整录音。
- 322-R6 OTA 后由 `ota_1` 启动并确认为 `VALID`，USB、Wi-Fi 和 BLE 连接恢复。配对和展示设置前后相同。BIN SHA-256 为 `2eb5a9172512963b0ac1fffc1ebabc3af90edac9382181af0f1e97daec5d0a35`，ELF SHA-256 为 `a353981dbe063adff0de128fe41ccf8bd8473df3e97ddd830496b0b1bd6356b0`。
- R6 单行布局和实际升级说明分别通过离屏渲染。全量 UI 预览在已有的日历节气断言处结束。
- Windows 本地原始证据位于 `artifacts/ble-rpc-stall-20261117/`。`baseline/gatt.jsonl` 记录最初的 GATT 阻塞，`continuous/control.jsonl` 记录 144 KiB 因邮箱超时中断，`mailbox-final/throughput.jsonl`、`control.jsonl` 和 `deployment.jsonl` 记录最终验收。R6 启动核验和总览预览位于 `artifacts/tab5-overview-single-line-20261117/`。这些目录是本机资料，Git 不携带二进制和运行日志。

## Mac 接手顺序

Mac 接收分支的代码与验证状态另见 [macOS 对齐记录](MAC-TAB5-BLE-ALIGNMENT-20261117.md)。以下保留 Windows 交接时的建议顺序。

1. 先审查 `MacTab5BLE.swift` 的共享 `busy`/`deadline` 机制，用 `MacTab5BLEDiagnostics` 记录操作类型、等待时长、片偏移、期望/实际 ACK、连接代际和重连原因。分别复现丢失状态 ACK 与邮箱首次读超时，判断各自是否会取消进行中的 RPC。
2. 用 `MacTab5BLEStatusTests`、`MacTab5BLEVoicePacingTests`、`MacTab5BLERPCWindowTests` 和 `MacTab5BLEDiagnosticsTests` 覆盖修复，再在 macOS 执行 `cd mac-app && swift test && swift build -c release`。复测分片顺序、授权窗口、旧固件兼容性和真实断线恢复。
3. 在已配对的 322-R6 上记录三通道双向吞吐和半小时稳定性。BLE 每方向至少重复跨越 96 和 144 KiB，并校验内容与顺序。对大回复期间的语音同时做邮箱轮询和实际录音验收，记录等待时长与结果。验证 USB 单独断开后回退 Wi-Fi、恢复后自动切回，执行前后核对展示设置。

Mac 原生测试、构建与安装均待验证，322-R6 三通道完整吞吐和半小时稳定性也待验证。当前没有冻结跨平台发布候选、标签或发布草稿。后续登记实际构建的源码 SHA、包摘要和设备结果，再按[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)交接。
