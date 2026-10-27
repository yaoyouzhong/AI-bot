# AI-bot 0.6.10 / TAB5 234

Windows 0.6.10 与 TAB5 0.2.234-ui 已正式发布，完整下载中心已更新。音量拖动恢复、双向 Codex 已读同步、各通道控制、取消／断线、USB 黑屏唤醒已按记录验收。最后未通过的纯 BLE 启动核验已在 .234 和原版 Windows c608 下通过，用户也确认重启与 USB 插拔后的电量显示正常。已通过项目不重测。安装、真实结果及剩余限制见 [逐项收尾](development/RELEASE-0610-229-CLOSEOUT-20261019.md)。

## 电脑一键解锁

TAB5「应用 → 控制」可按当前连接使用 USB、蓝牙或 Wi-Fi 解锁 Windows。PIN 在 Windows 端独立加密保存；取消、断线或未完成时停止，不自动切换通道重发。页面在现有状态中显示实际解锁通道，最近八次记录保留阶段、结果、准备与输入确认耗时。

Windows「电脑控制 → 一键解锁设置 → Wi-Fi 解锁设置」提供按需安装 TightVNC Server。新安装自动配置组件认证；已有兼容安装点击「自动接入」，复用本机认证并保留原密码、配置和归属。首次使用也无需填写 TightVNC 密码。不兼容的安装明确停止接入，不覆盖现有设置。

TightVNC 仅作为独立的本机服务使用：安装 Server、仅允许回环连接、不创建防火墙例外，关闭 Web 访问；主机及安装器不记录组件密码。官方未修改 MSI、完整对应源码、GPL 与来源摘要随包提供。安装与卸载组件需要 Windows 管理员确认。只有 AI-bot 安装且未被修改的组件可从页面卸载；卸载 AI-bot 时可选择保留或同时移除。Windows PIN 和设备配对保留。

## 准备速度与无线核验

TAB5 延续 229 的连续新鲜、一致屏幕样本准备方式；保留熄屏唤醒等待、一次性授权、总超时及最终新鲜检查。桥接短 RPC 处理和回复获得有限的普通状态传输优先级，授权期限保持不变。

「核验启动」在没有 USB 时可使用已配对无线通道，通过三次新鲜挑战核对固件 ELF、运行分区 VALID 和持续 UI 心跳。无线性能历史暂不提供。旧设备继续使用 USB 核验。

## 安装与升级

- Windows：新用户运行安装器，缺少 .NET 8 桌面运行时或 WebView2 时补齐；已有兼容运行环境自动跳过。升级前正常退出托盘桥接，用户设置、PIN、配对和图库保留。
- TAB5：本轮目标 0.2.234-ui，包含音量拖动恢复、实际阅读后的 Codex 已读同步、BLE 报告有界发送与整数运行时间、界面心跳竞态及电量显示连续性修复。已有 AI-bot 使用升级 ZIP，出厂设备使用首刷 ZIP；两包不可混用。升级确认针对精确镜像，配对、PIN 和展示设置保留。
- Mac、ESP8266 与图库沿用各自实际已发布版本，不为同号重建。Windows 安装器维持既有未签名状态；独立 TightVNC MSI 保留厂商签名。

## 验证与限制

构建、公共回归、实际安装、主机部署与设备验收分别记录。原子替换的暂时占用恢复、持续失败保留和释放后恢复已用真实文件句柄测试。原版 c608 对精确 .234 的隔离 BLE 核验在 9.34 秒内确认三份实时样本、ota_1 VALID，无隔离违规；未放宽健康条件。新增电量验收为用户定性观察，冷启动充电校准条件保留，不承诺所有启动的固定显示秒数。跨管理员 UAC、真实 MSI 3010 重启分支和长期稳定性仍按收尾记录列为未完成边界，不把模拟通过写成真实系统验收。

## English

Windows 0.6.10 and TAB5 .234 are published and the complete download center is current. Volume-drag recovery, bidirectional Codex read sync, transport control, cancellation/link loss and USB black-screen wake have recorded hardware acceptance. The last failed isolated BLE boot check now passes on .234 with original c608; the user also accepted battery display after reboot and USB changes. Previously passed tests are retained. TAB5 can unlock Windows over its active USB, Bluetooth or Wi-Fi path. PIN storage remains separate and DPAPI protected. Cancellation/disconnect stops the operation without switching transports or replaying a PIN. The latest eight diagnostics retain phase, outcome and split timing.

Wi-Fi settings install TightVNC Server on demand, generate authentication automatically, or import compatible existing local authentication while retaining its password, settings and ownership. No manual component password is required, including first use. Unsupported configurations stop explicitly. The separate unmodified signed MSI, complete corresponding source, GPL and provenance are bundled. Server access is loopback only, without a firewall exception or Web access. Only unchanged AI-bot-owned installations can be removed; app uninstall offers a keep/remove choice while retaining PIN and pairing.

Firmware .229 replaces fixed preparation with consistent fresh samples while preserving wake settling, single-use grants, deadlines and the final fresh check. Short RPC handling/responses gain bounded priority over ordinary telemetry. Wireless boot verification uses three fresh challenges to validate the exact ELF, VALID partition and UI progress. Wireless OTA timing history is unavailable; older firmware retains USB verification.

The standard Windows installer prepares missing .NET 8 Desktop Runtime/WebView2 dependencies and skips compatible existing installations. Keep the existing user's settings and collections when upgrading. Factory and OTA firmware ZIPs remain separate. The .234 image retains unlock behavior and includes volume recovery, rendered-reply read receipts, bounded BLE report delivery with integer uptime, heartbeat ordering and battery-display continuity. The exact approved image passed the original c608 isolated BLE check in 9.34 seconds with three live samples and ota_1 VALID, without isolation violations or weakened validation. Battery display was accepted qualitatively; unchanged cold charging calibration prevents a universal display-time guarantee. Mac, ESP8266 and collection packages are not rebuilt. Source, builds, real installs and hardware acceptance have separate evidence; cross-account UAC, actual MSI 3010 reboot-required behavior and long-duration stability remain unverified limits in the closeout record.
