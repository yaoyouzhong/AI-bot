# AI-bot v0.6.31 发布验收 / Release acceptance

2026-11-25。Windows x64 和 Apple Silicon Mac 桥接 0.6.31 与 TAB5 固件 0.2.323-ui 通过一个正式页面交付。[发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.31) · [附件 SHA-256](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.6.31/SHA256SUMS.txt)

| 组件 | 源码 | 冻结候选 |
| --- | --- | --- |
| Windows 和 Mac 桥接 | `bd2a4b8245bda21e7a9e2ad55615ec1ee04d924d` | `c577faecbf8435914e76489c67709465c06180b156ff164c3578dfb2b4408635` |
| TAB5 | `707c85348b237778d5f86bfe059ea0b4ad301c89` | `3f865b8bb0a23709a1444f46dc944602fa5001bbc0b1fe9394b1cc978b402b9f` |

- **Windows**。从正式 0.6.30 使用冻结安装器在 Windows Sandbox 升级。托盘在 14.647 秒后恢复，模拟资料、加密记录和 LAN 权限保留，安装后程序集与候选便携包一致。使用真实 Codex 会话验证只读状态，并以同一候选、真实 TAB5 USB 和隔离会话数据验证「工作中」到「待命」及萌宠。安装器未签名。[构建批次](https://github.com/yaoyouzhong/AI-bot/actions/runs/45558201068)
- **Mac**。使用冻结 ZIP 验证 0.6.28 升级、实际启动和资料保留。原配对 TAB5 通过 USB 验证项目归属、已保存回复、上一轮和总览状态切换，验收后恢复日用桥接。应用临时签名，签名完整性通过。Gatekeeper 分发评估拒绝，带下载隔离标记的首次打开未测。[升级交互与隔离验证](MAC-DESKTOP-UPGRADE-CONFIRMATION-HANDOFF-20261125.md)
- **TAB5**。首刷包和升级包的应用字节与已验证启动的 323-R2 一致。首刷包通过生产加载器离线校验。USB 夹具发出的同一类新回复在开启提醒的会话有声且有横幅，静音会话无声、无横幅且保留未读。用户完全关机再开机后，设备上仍显示原测试会话的划线铃铛，随后恢复测试设置。Mac 的 Codex 桌面降级通知未单独验证。[固件设计与前序验收](TAB5-SESSION-NOTIFICATIONS-20261124.md)

首次添加设备与 Mac 系统授权画面继续用于对应步骤。安装指南和更新说明新增 Windows 升级确认截图。中英文主页与产品视频同步更新 TAB5 总览及会话铃铛画面。[安装](../INSTALL.zh.md) · [更新](../UPDATES.md) · [视频](../PRODUCT_VIDEO.md)

## English

The exact Windows and Apple Silicon Mac installers were tested on their native platforms. Paired USB testing on TAB5 covered the desktop conversation changes. The 323-R2 firmware passed a new-reply sound comparison and retained its per-conversation mute setting after a physical power cycle. The factory ZIP passed the production loader offline. Windows Setup is unsigned. The Mac app is ad-hoc signed and not notarized, and first launch with a quarantine flag was not tested.
