# TAB5 蓝牙启动核验报告修复

最新结果：.234 已经用户批准、完成 OTA，并在原版 c608 下通过唯一剩余的纯 BLE 启动核验，9.34 秒、三份实时样本、精确 ELF、`ota_1 VALID`，无隔离违规。用户已恢复电脑 WLAN。真实 .233 诊断发现报告的 `uptimeMs=562752.83` 被整数解析器拒绝，根因是 .231 新增调用触发 cJSON 宏的转换优先级；.234 先求整数毫秒，未放宽校验。详细根因、九份实际 DLL 回放与实机证据见 [整数时间修复](TAB5-BLE-UPTIME-20261019.md)。电量也由用户在 .234 本次重启和 USB 插拔中观察通过。后续各“当前”“待执行”段落均为早期历史快照，不代表仍需重测。

Latest result: the approved .234 OTA passed one isolated BLE boot operation using the original c608 bridge: 9.34 seconds, three live samples, exact ELF and ota_1 VALID, with no isolation violations. The user restored PC WLAN. The actual .233 diagnostic exposed fractional uptime caused by the .231 cJSON macro call; .234 fixes integer evaluation without weakening validation. Battery display was also directly accepted after this reboot and USB changes. Earlier pending states below are historical; accepted tests must not be repeated. Publication remains unapproved.

当前状态：.230 纯 BLE 超时；.231 报告已收到但健康条件拒绝；.232 也已实际升级，隔离有效且报告已返回，1.08 秒后仍以相同健康错误失败。三次均不能写为通过，具体失败数值没有被 c608 保存。电脑 WLAN 已恢复。用户随后批准并安装了独立电量修复 .233，未因此重测 BLE；电量启动显示未注意观察，不能写为通过。当前原 Windows c608 文件保持不变；用户另已明确批准临时切到只补数值的诊断版，读取一次失败样本后正常切回。该诊断的 Release 构建、14 项数值测试及完整公共回归（含 63 项无线生命周期回归）通过，实际普通桌面切换仍需执行并核对。来源、缺陷及真实证据边界见 [界面心跳采样](TAB5-UI-HEARTBEAT-20261019.md) 和 [电量显示连续性](TAB5-BATTERY-DISPLAY-20261019.md)。后文 .231 脚本与恢复段落为历史记录，以本段和对应实际结果为准。

用户拔出 USB、选择“仅蓝牙”后，设备仍有 Wi-Fi 后台通信。用户随后暂时关闭电脑 WLAN、保留蓝牙和网线，脚本在 USB 未连接、蓝牙已确认且 Wi-Fi 等待设备回报时，通过已有同用户 IPC 启动只读核验。结果为 `wireless_timeout`；没有输入按键、修改网络配置或写入固件。用户已恢复电脑 Wi-Fi 及 TAB5“自动选择”。失败证据在 `artifacts/development/release-closeout-20261019/host-deployment-control-fix-230/ble-wireless-boot-verification.json`，不能被此前通过的 Wi-Fi 核验覆盖。

实际源码存在可独立复现的丢失路径：界面调度报告时即记住请求 ID；后台工作线程只调用一次 BLE RPC，而实际 mailbox 在其他状态请求仍占用时立即返回空结果。旧线程忽略失败，同一 ID 不会再次调度。使用实际生产线程和 cJSON、按真实 mailbox 约定模拟忙碌边界后，旧实现得到 `calls=1 successes=0`。这证明了源码缺陷；现场没有记录到该次报告的每个内部状态，因此不能宣称已排除命令递送等其他因素，最终仍需纯 BLE 真机复验。

修复只涉及两个主目录输入：`boot_verify_worker.inc` 和 `rpc_link.c`。BLE 只读报告在通道忙、没有响应或服务暂忙时，以 250ms 间隔有界重新尝试；最多 16 次、六秒后不再开始新的尝试，每次 exchange 最多两秒。每次刷新 uptime/UI age/flush 样本并沿用原有认证、会话、时限和随机 nonce 验证。服务拒绝即停止。Wi-Fi 单次 HTTP 路径及其他 RPC 的时限保持原值。没有为锁屏、解锁、音量、PIN 或其他用户操作增加重试，也没有改变 NVS、配对、PIN、展示设置或 OTA 提交规则。

实际线程原生回归覆盖空闲发送、一次瞬时占用、连续占用 500/2000ms 后恢复、持续超时、持续占用、请求拒绝、并发拒绝、任务创建失败和 Wi-Fi 单次路径。持续超时的模拟为三次调用、6,500ms 后停止，持续即时占用为 16 次、3,750ms 后停止；这些是确定性的模拟时钟结果，不是实机耗时。真实设备的调度和加解密开销需单独观察。

候选 BIN SHA-256：`919b0f2bcc8ffd33ac935d9416f3acb9e02db2d9a0913c09ddf40df2064dced4`；ELF SHA-256：`0db8dd176bb6415b00c9e91a16e1842ea17b22bcd3cc8cf0f220176767877f86`。冻结了 221 项主目录源码/配置及此前源码包已包含的两份生成音频，确认除了上述两项之外均与 .230 一致；两份音频字节未变。Windows 的 400 项编译输入和已部署 DLL 也未变。

候选冻结在 `artifacts/development/release-closeout-20261019/boot-fix-231-candidate/`。正式解析器检查镜像和结构化 sidecar，完整原生界面回归使用该实际 sidecar，已查看顶部与滚动后的说明和控制页。首刷、OTA 与源码包分别包含许可证和摘要，首刷私有区域保持擦除状态。初始隔离准备没有写入设备或替换全局 latest；后续经用户授权的桌面步骤已将全局 latest 登记为精确 .231，但仍未刷入设备。

用户已批准新增 .231 固件升级。`Offer-Boot-Fix-231.cmd` 为普通桌面入口：预检、备份、正常退出同一桥接，将已审查镜像通过标准准备脚本登记到固定 latest，正常启动并只提供 OTA。它不替换 Windows 程序、不自动刷写、不锁屏、不输入按键，失败时恢复旧候选与桥接。保留 AppProfileGuard，因此不能从 Codex 子进程重定向环境启动实际桌面部署。用户明确要求只验收此前未通过的项目：升级时核对精确镜像和启动状态，功能验收仅补纯 BLE 三份实时报告；已通过的音量、已读同步、锁屏／解锁、取消、断线和黑屏唤醒均不再重复。

第一次桌面运行在预检停止：脚本把正常桥接与其 `--runtime-health-guard` 子进程都计为主进程，误报数量不为一。现场 HTTP 服务正常，两个进程父子关系与生产守护实现吻合；尚未重启、修改候选或刷写。脚本已改为校验唯一主进程以及守护子进程的参数、父进程和会话关系，正常退出时仍等待两者结束。旧失败日志保留，下一次运行使用独立证据目录。产品 DLL 和固件字节均不变。

第二次桌面运行已备份、正常重启桥接并实际提供精确 .231，`ota-offer.json` 为 `ok=true`，目标设备、BIN 和 ELF 均匹配；但脚本误从响应顶层读取字段，忽略了已有 IPC 的 `body` 包装，因此误报失败。回退在等待进程退出阶段未完成，随后只读检查确认桥接已退出、全局 latest 仍是精确 .231，设备未刷入。这也是部署辅助脚本缺陷，不是固件验证结果。

恢复入口改为 `Recover-Boot-Fix-231.cmd`：在普通桌面核对现有已批准字节及第一次重启前的唯一私人备份，比较配对／PIN 和原展示策略，恢复同一 Windows 桥接并只提供既有 .231。解析函数已用真实保存的 `ok/body` 回包验证，并拒绝失败、无 body、扁平回包以及设备、版本、BIN、ELF 不匹配的七类错误。原备份目录通过时间窗口唯一定位，本地检查只读了目录元数据，没有读取私人文件。实际恢复仍需普通桌面运行；异常时不再自动退出已经启动的桥接，不刷写或回写配置。原两次失败和回退不完整均保留，不能记作成功。

English: The .231 firmware was approved, installed and confirmed by the user and the read-only bridge API. Its isolated BLE report arrived within the 2.33-second verification operation, but the existing UI-health predicate rejected it. The test sent one operation, recorded no isolation violations and performed no input or device writes; it remains a failure, not a pass. PC Wi-Fi was restored. USB was subsequently connected only to read existing diagnostic counters; current heartbeat ages were 3-12ms and display errors zero, which do not recover the missing failed sample. A separate race in the exact .231 heartbeat function was reproduced and confirmed in its ELF; the .232 local repair preserves all health thresholds. The 400 bridge inputs and deployed c608 DLL remain identical. Previously passed user acceptance must not be repeated; only the failed BLE boot check remains. Both desktop-helper failures and successful recovery are retained. Public release remains unapproved.
