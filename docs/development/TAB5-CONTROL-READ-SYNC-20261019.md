# 控制页拖动恢复与已读反向同步候选

状态：本地实现、构建和下述回归已通过；Windows 0.6.10 / TAB5 0.2.230-ui 已部署并完成本页两项真机验收，未公开发布。

用户确认拖动音量后控制页全部不可用，返回和其他页面正常；接回 USB 仍无效，顶部仅显示“电脑已连接”。普通桌面运行的只读探针经实际 LAN 鉴权请求三次电脑控制状态，均为 200，音量和锁屏能力有效。旧代码在拖动时暂停轮询；状态过期后禁用滑块，LVGL 不发送禁用控件的 RELEASED，因此拖动标志无法清除，轮询持续暂停。原生指针输入复现记录为 `adjusting=1 disabled=1 fresh=0`。

修复保持只读状态轮询，不覆盖正在拖动的滑块值；释放、控件失效或离页后取消遗留拖动，不补发音量。过期状态显示正在读取，避免沿用“电脑已连接”。同一复现已得到 `adjusting=0 disabled=0 fresh=1`，并检查没有重放命令。候选基于与已验收 229 镜像对应的 219 项编译主目录摘要重建；旧工作目录中缺少/较旧的覆盖文件已从已校验源码包恢复并保留原件，防止回退已验收的 USB/BLE 解锁改进。

已读同步使用实际渲染后的加密回执。读取和邻页预取不修改桌面状态；设备、账户、主机、最新结束轮次和五分钟期限均需匹配。通过当前 Codex 26.1013.4894.0 的版本化 IPC v3 同步，由 Codex 自身验证并保存，桥接读回确认后才成功。没有直接改写 Codex 全局 JSON，也没有发送会话消息。详情见 [协议](../PROTOCOL.md)。

已通过桥接 Release 编译、完整公共回归及合成 IPC 回归：无预取写入、设备隔离、账号切换、旧轮次拒绝、IPC 字段、持久状态确认与幂等重试。设备回执工作线程的原生测试覆盖 USB/Wi-Fi/BLE、去重、并发、四次有界重试、拒绝和任务分配失败。故障复现、错误构建和旧版本证据均保留在 `artifacts/development/release-closeout-20261019/`，不会把这些测试替代实际设备和 Codex 侧栏验收。

正确 229 基线上的完整固件构建、完整界面与解锁专项回归、实际 sidecar 预览均已通过。正式格式的 Windows 候选与其实际运行文件集已完成完整公共回归及 Codex 专项回归。一次公共回归因工作目录复用旧的合成测试凭据，触发“Wi-Fi 默认关闭”断言；失败日志保留，在全新工作目录完整重跑通过，未修改断言。

候选 `control-fix-230-final/`：Windows DLL `c608df148dce97745cdc19b3f9d51b13bbb08cba8344d812f876a890f4a0939a`，固件 BIN `4c11f7ccc7d7bcb04fedcaab6043b1524599a85903945711cf71dbbf3cfef71a`，ELF `e60cbe4be5df991203a3969a9f0c860b6998ee8a4ae90f3fc685fc66f707ab0a`。87 项运行文件、400 项桥接输入和 221 项固件主目录输入已冻结。`Deploy-Control-Fix.cmd` 保留现有目录保护，预检、备份、正常退出／启动并在失败时回退，只登记 OTA 候选，不自动刷写、锁屏或输入 PIN。

用户批准部署与升级后，从普通桌面启动脚本，88 项文件校验、正常退出／启动、PIN、配对和展示设置保留通过。设备已实际升级 .230，经 USB 启动核验确认精确 ELF `e60cbe4…7ab0a`、`ota_1` 分区 `VALID`、完整应用偏移和错误码 0；没有由脚本锁屏或输入凭据。

用户确认连续拖动音量三次、返回并重新进入控制页后，电脑实际音量跟随变化，界面持续可操作。随后对已完成的真实会话临时标为未读；用户从 TAB5 显示正文后确认电脑未读标记消失，Codex 应用 API 也读回 `isUnread=false`。测试没有发送消息或由 Agent 打开电脑会话，原有已读状态得到恢复。用户要求重新设置后进行的验收另有记录，未把第一次未确认观察当作用户验收。

新的 Windows 安装器、运行 ZIP／源码包和 TAB5 首刷／升级／源码三包已按已部署字节生成，旧包保留。用户第一次纯 Wi-Fi 验收已成功：USB 拔出、电脑蓝牙关闭，从 TAB5 锁屏后解锁回到桌面；设备记录 `attempt=1 transport=Wi-Fi success`，总耗时 3,585ms，桥接准备 1,875ms、输入与确认 1,187ms。第二次锁屏／解锁用户同样确认成功，实际记录仍走 Wi-Fi，总耗时 4,278ms，桥接准备 2,453ms、输入与确认 1,485ms；但该次诊断已显示蓝牙重新连接，因此只证明 Wi-Fi 路径成功，不追加蓝牙隔离结论。

取消验收的实际通道为 BLE：`attempt=3 result=cancelled`，1,267ms，`hidSubmitted=False`。用户确认取消后等待十秒仍锁屏，随后手动解锁；没有继续输入或自动重试。USB 拔线的 `attempt=4` 在 964ms 因连接变化终止，未提交 HID；随后 `attempt=5` 的 BLE 成功记录由用户明确确认是再次主动点击 TAB5 解锁，不能记作自动重试或电脑键盘手动解锁。USB 黑屏唤醒的 `attempt=6` 成功，3,478ms，用户确认屏幕被唤醒并回到桌面。

USB 拔出后的 Wi-Fi 启动核验已通过三份实时报告，精确 .230 ELF 与 `ota_1 VALID` 匹配。纯 BLE 启动核验则实际失败为 `wireless_timeout`，失败证据保留。用户恢复电脑 Wi-Fi 和 TAB5“自动选择”后，后续工作转为本地诊断；“仅蓝牙”是连接偏好，不代表 Wi-Fi 后台通信已关闭。新发现的报告丢失缺陷与未部署的 .231 候选见 [蓝牙启动核验](TAB5-BLE-BOOT-VERIFY-20261019.md)。新版 .230 视频及其独立可重建源码已完成本地媒体检查；最终源码和另行批准的公开推送／标签／发布仍待收尾。

English: On the deployed .230 candidate, the user accepted two TAB5-triggered Wi-Fi lock/unlock cycles (device totals 3585/4278ms; bridge preparation 1875/2453ms and input/confirmation 1187/1485ms). USB was absent in both. Bluetooth was off for the first but appeared connected in the second diagnostic, so the second proves the actual Wi-Fi route rather than Bluetooth isolation. BLE cancellation completed in 1267ms without HID input; the user waited ten seconds with Windows still locked, then unlocked manually. USB removal terminated attempt 4 in 964ms without HID submission. The following successful BLE attempt 5 was explicitly initiated by another user click on TAB5, not an automatic retry or PC-keyboard recovery. USB black-screen wake/unlock succeeded in 3478ms. Wi-Fi boot verification passed three live samples with the exact .230 ELF and VALID partition. Isolated BLE verification failed with wireless_timeout; the failure remains recorded. PC Wi-Fi and automatic device selection were restored. The separate .231 boot-report repair remains a local candidate awaiting approval and real-device verification.
