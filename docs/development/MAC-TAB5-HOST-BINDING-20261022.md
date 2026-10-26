# Mac 可信电脑绑定迁移 / Mac trusted-computer migration

以当前 `codex/mac-tab5-phase1-ci` 未提交开发状态为基线，读取并校验 Downloads 中 `mac-binding-migration-20261022.zip` 的全部 SHA-256。指导文件及参考补丁解压到仓库外。先备份 122 个源码、测试、配置及相关文档到 `/Users/yaoyouzhong/Documents/Codex/2026-10-22/mac-binding-source-backup-120441`，逐文件验证复制后的 SHA-256，保存 MANIFEST、分支／HEAD／status／源码 diff；未包含凭据、运行态或构建缓存。没有应用参考补丁、回退旧代码、清理现有修改或切换基线。

The migration uses the current uncommitted development branch, with every supplied package checksum verified. Guidance/reference patches stay outside the checkout. Before editing, 122 source/test/config/document files were copied to the external backup above and verified individually; the manifest and Git state/diff support restoration. No credentials/runtime/build cache, blind patch application, history reset or cleanup of existing changes is involved.

## 实现 / Implementation

- 从原 Keychain 配对密钥派生公开 hostId；不变更存储格式、账户名或密钥。USB 连接只尝试已有记录的签名 claim，不自动发送配对／追加记录。首次绑定仍为明确 USB 配对动作，包含 hostId／名称；确认后重新取得挑战、验证签名 ACK，才将本机配对记录标记确认。
- 每个打开的串口独立维护挑战和已认证端点。挑战在发送前消耗，包括发送／回执不确定的情况；端点改变重新取得挑战。身份、名称、私网端点、能力字段、HMAC、旧 nonce、外部 host、忙碌返回均严格校验。未知电脑或忙碌只等待，不替换密钥／记录，不回退无签名接管。
- Wi-Fi 保持原本机配对密钥和鉴权；不借广播／发现更换设备所选电脑。状态中的 connectionHealth 增加兼容的 hostId／hostName／hostBindingVersion。
- 蓝牙先读取公开 owner 提示，非本机目标断开并退避；匹配后仍验证原 nonce／HMAC。声明 bleProofRequired 的固件必须先接收精确五字段的 AES-GCM 短证明并通过签名 ACK／精确 sequence 确认，之后才允许 USB 待命或 RPC／语音。证明不标记显示数据送达，不更新 USB 租约。未声明能力的旧固件保留兼容路径。
- 服务增加同一锁下的共享安全整数序列分配器，短证明无需生成完整状态即可分配；状态／资源复用同一序列。BLE admission 绑定连接代次和完整配对记录，超时／取消／换连接／换配对清除授权；读写和延迟回调验证代次、当前记录及期限。保留现有串行队列、分片和长度前缀，不升级工具链或添加 unchecked Sendable。
- 保留当前设置界面，仅替换“原电脑需要重新配对”的过时提示。协议文档按迁移包现有 Windows／固件约定同步，无新增协议版本、Windows 分支改动或固件变更。

The implementation derives host identity from the existing Keychain key, performs explicit enrollment followed by verified one-use USB claims, refreshes endpoint challenges and rejects untrusted/busy/uncertain outcomes without re-enrollment or unsigned takeover. Wi-Fi keeps its own key. BLE owner hints do not authorize access: key authentication and any advertised short proof/signed exact-sequence ACK precede standby or RPC/voice. Proofs do not renew USB/display freshness. A shared locked sequence allocator, connection/pairing generation gates and callback deadlines preserve cancellation isolation, existing serial queues, encryption and low-MTU framing. The current UI is retained, and documentation records the already-defined counterpart protocol. No firmware, Windows branch, credential binding or toolchain is changed.

## 验证 / Validation

- Release 构建、App 严格签名、无凭据 helper 身份及离线随包工具检查通过；helper 与前一正常候选逐字节一致。候选为 `artifacts/mac-tab5-20261022/host-binding/AIBotBridge.app`，正常退出旧实例后启动，确认唯一进程、七项开关仍开启、31 个 Codex 会话可读。未安装覆盖 Applications、提交、推送、建标签或发布。
- 本机 `swift test --package-path mac-app` 仍因 `no such module 'XCTest'` 在执行前失败。断言适配器对实际生产源码执行 114 个实际测试体通过，包括 11 个新增绑定测试和 5 个已有 BLE 测试；保留此前语音／Codex／天气／行情回归。此结果不等于完整 XCTest、CI 或两电脑实机验收。
- 新测试包含独立 Python HMAC 向量、签名字段及旧挑战篡改、外部密钥、名称／私网端点边界、未知 USB 不追加、同端点只认证一次、端点改变／断连重新挑战、超时不复用挑战、能力降级拒绝、外部 BLE owner 待命、短证明精确字段／安全整数、20 字节 MTU 的加密分片往返、签名 ACK 前不开放 admission、旧代次／换密钥拒绝、旧固件兼容与证明不更新 USB 租约。

Release/signature/helper/offline-tool checks pass. The normal candidate is running alone with seven retained opt-ins and 31 readable chats; there is no Applications replacement, commit, push, tag or release. Local XCTest still cannot execute because its module is missing. An assertion adapter executes 114 actual test bodies against production sources, including 11 new binding tests and five existing BLE tests plus earlier regressions. Coverage includes independent HMAC vectors, tampering/stale challenges, foreign keys, endpoint/name bounds, no unknown-host enrollment, reconnect/endpoint refresh, uncertainty, downgrade rejection, foreign-owner standby, exact short proofs/safe integers, encrypted 20-byte-MTU round trips, signed ACK admission and stale-generation/pairing isolation. This is not full XCTest/CI or two-computer hardware acceptance.

## 实机边界 / Hardware acceptance

| 场景 / Scenario | 结果 / Result |
| --- | --- |
| 已绑定 Mac 接回 USB、自动签名认证 | 通过：用户只接线，未点配对；正常 UI 的 USB 数据已确认 |
| 当前任务实时回复 | 通过：用户确认能看到本轮“连接已恢复”新文字，排除仅旧缓存 |
| Mac Wi-Fi 认证请求 | 已观察到已认证请求；USB 优先时可回到等待，不推定无线单独持续连接 |
| 蓝牙认证／USB 优先待命 | 已观察到；尚未独立确认本次硬件声明了 .237 短证明能力 |
| 拔线保持 Mac、Windows 同时在线 | 通过：用户确认无线仍连 Mac、未切 Windows；Mac UI 同时观察到蓝牙数据已确认 |
| Windows／Mac 交替 USB 自动接管、记录均保留 | 双向通过：均只接线，无重新配对；两份既有记录可用，不推定完整可信列表审计 |
| 未绑定 USB 不接管、首次明确追加 Mac | 未验证；本次已有 Mac 记录，无需重新绑定 |
| 当前电脑通道均不可用时不切他机 | 通过：确认 Mac 进程退出、串口释放后约 20 秒，Windows 仍运行，用户确认始终等待 Mac |
| 无 USB 手动选电脑、旧数据／输入隔离 | 手动 Windows → Mac 与选回后的新回复通过；迟到输入隔离未做真实输入验收 |
| .237 短证明、低 MTU、系统 bond／隐私地址实机行为 | 未验证；仅自动测试及正常认证待命观察 |
| 重启两端后的列表／当前目标／设置保持 | Mac 桥接及 TAB5 分别正常重启通过：当前 Mac、两条可信记录、无线恢复均保留；完整显示设置逐字段对照未做 |

No firmware was built/flashed/rolled back, and no NVS/bond/display/network settings were cleared. Windows task execution/input and sending real Codex messages are outside these connection checks. Remaining physical rows are explicitly unverified; current USB/live-reply success does not imply all multi-computer cases pass.

用户已确认在 Windows 桥接同时运行、Mac 拔掉 USB 后，无线仍保持 Mac，没有切到 Windows；Mac 同时显示 USB 串口不在、蓝牙数据已确认。此轮拔线保持与 Mac 蓝牙无线接续通过，不据此将 Wi-Fi 单独回退或特定 .237 能力标记通过。随后请用户仅把 USB 接到已绑定 Windows，不重新配对，核对自动接管并暂留现场；结果待反馈。

With both bridges running, the user confirmed that unplugging Mac USB retained Mac wirelessly without switching to Windows. The Mac independently showed absent USB and acknowledged BLE data. Unplug retention/BLE continuation pass this take, without inferring Wi-Fi-only fallback or a particular .237 capability. A separate trusted-Windows USB takeover check is pending, without re-pairing.

用户随后确认只将 USB 接至 Windows 即自动切到 Windows，未重新配对。Mac 只读 UI 同时显示 USB 不在、Wi-Fi 等待、蓝牙断开，不再报告有效 Mac 数据；没有向 Windows 任务发送消息／操作输入。Windows 单向接管通过，已请用户仅接回 Mac 并核对最新回复；反向结果待反馈。

The user confirmed automatic Windows takeover by USB reconnection alone, without pairing. Mac UI independently showed absent USB, waiting Wi-Fi and disconnected BLE rather than valid Mac data. No Windows task message/input was sent. This one-way takeover passes; return-to-Mac/live-reply feedback is pending.

用户确认接回 Mac 后自动回到 Mac、新回复正常；UI 同时再次确认 USB 数据、Wi-Fi 认证及 BLE USB 待命。双向接管与两台已绑定电脑的记录继续可用本轮通过，未重新配对。下一项拟正常退出 Mac 桥接约 20 秒、Windows 保持运行，核对失联后不改选。首次菜单退出及快捷键后原进程仍运行、串口仍有正常数据，故没有开始离线计时或将该观察计为通过；源码中正在处理设备请求时正常退出会被保护性拒绝，已请用户先离开自动查询的回复页再测试。没有强杀进程、切断服务或关闭功能开关。

The user confirmed automatic return to Mac and fresh replies; native UI again showed acknowledged USB, Wi-Fi authentication and BLE standby. Bidirectional takeover and continued usability of both existing records pass without re-enrollment. A selected-host-offline check is planned, but initial normal menu/shortcut quit attempts left the original process and live serial data running, so offline timing/acceptance did not start. Normal termination is guarded while device operations are active; the user was asked to leave the auto-refreshing reply page before retrying. No process was force-killed or feature opt-in disabled.

用户离开回复页后正常退出成功，进程和串口占用均确认消失；保持 Windows 桥接运行，约 20 秒离线观察后用户明确确认仍等待 Mac、没有切 Windows。随后启动同一 host-binding 候选，确认唯一新进程，USB 数据、Wi-Fi 已认证请求及 BLE USB 待命自动恢复。当前目标失联不改选和 Mac 桥接重启恢复通过。此前进程未退出时的较早反馈没有用于此项判定。下一项请求无 USB 的可信电脑手动 Windows → Mac 无线切换，待反馈。

After the user left the reply page, normal quit succeeded and both process/serial ownership were confirmed absent. With Windows still running, the user confirmed that TAB5 waited for Mac throughout the approximately 20-second outage without switching to Windows. Normal restart of the same candidate restored exclusive process ownership, acknowledged USB, authenticated Wi-Fi requests and BLE standby automatically. Selected-target outage retention and Mac-bridge restart recovery pass. Earlier feedback while the process was still alive was not used for this acceptance. Manual trusted Windows-to-Mac wireless selection without USB is now pending.

用户确认 USB 拔除、两台桥接同时运行时，设备可信电脑选择 Windows → Mac 两次无线切换均成功，最终已回到 Mac、新回复正常。此项手动目标切换实机通过，不扩大为旧输入／照片／语音真实操作隔离通过。随后请求保持无 USB、两台桥接运行，仅重启 TAB5，核对当前目标 Mac、自动无线恢复及两条可信记录；待反馈，不构建／更新固件。

The user confirmed both manual wireless switches, Windows then Mac, with USB absent and both bridges running; Mac remains selected and fresh replies work. Manual target switching passes without inferring real stale-input/photo/voice-operation isolation. A normal TAB5-only reboot is now requested to check retained Mac selection, wireless recovery and both trusted records, without firmware changes.

用户进一步确认 TAB5 正常重启后仍选 Mac、无线恢复、两条记录都在。Mac 只读 UI 同时取得 Wi-Fi 已认证请求及蓝牙数据已确认，USB 按本次验收要求保持未接回。再次核对七项功能开关全部开启、31 个会话可读，正常候选保持运行，未重新配对、刷固件或改变用户展示／网络设置。当前主要绑定流程通过；仍未实测新电脑首次追加／未知电脑拒绝、满 8 台／损坏记录、升级忙碌拒绝接管、真实迟到输入，以及特定 .237 低 MTU／隐私地址故障注入。自动测试不替代这些结果。

The user confirmed that a normal TAB5 reboot retains Mac selection and both trusted records, with wireless recovery. Native Mac UI independently confirms authenticated Wi-Fi requests and acknowledged BLE data; USB remains unplugged as requested for this test. All seven opt-ins and 31 chats remain, and the normal candidate stays running without re-pairing, firmware changes or display/network preference edits. Core binding flows pass. First enrollment/unknown-computer rejection, eight-host/corrupt-store cases, upgrade-busy takeover rejection, real late-input isolation and specific .237 low-MTU/privacy-address fault injection remain physically unverified; automated checks do not replace them.
