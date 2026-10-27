# AI-bot 0.6.11 / TAB5 .281 / ESP8266 .57

## 本次交付

Windows 与 Apple Silicon Mac 电脑桥接 0.6.11；TAB5 0.2.281-ui；ESP8266 0.5.7。图库保留现有已发布版本，无需重复下载。

TAB5 现支持 Mac 的 USB、Wi-Fi、BLE 接入、日常显示、设置、Codex 会话及草稿、语音确认、照片、设备预览和固件管理。Mac 使用原生菜单栏与系统授权；Windows 媒体会话、Windows PIN/TightVNC 与 Mac 的权限、解锁和语音实现分别维护，不能互套安装步骤。Apple Silicon 为当前交付架构；Intel 未验证，Mac 包临时签名且未经公证。

Windows 修复后台额度刷新抢占录音焦点，保留迟到识别文字；输入法提前停止时明确提示检查不完整文本，不自动重放或发送。长会话优先使用桌面 owner/本地历史，未实现的 Codex CLI 分页接口不作为功能可用性的依据。DeepSeek API 与网页来源独立记录，不混算不同授权数据。

TAB5 输入草稿新增长按清空：短按不清空，保留照片、忙碌保护和持久化；BLE 接收路径避免整份配置栈拷贝，保持原门槛、分区、协议和配对。小屏重置信息加粗，轮播页面、顺序、间隔和亮度保留；Windows LAN 状态裁剪恢复小屏 Wi-Fi 回退。

## 验收与限制

用户已确认会话、长会话分页、长按清空、两次长语音及其余实机测试通过，并确认小屏字体清晰度改善。TAB5 精确镜像 ELF/VALID 分区核验通过；小屏 0.5.7 写入后验证应用摘要，程序扇区以外字节完全不变，恢复原 6 页、原顺序、15 秒自动轮播。Mac R36 及 .279 的实机记录来自 Mac 工作阶段，.281 的新增交互由本次 Windows 现场验收，不宣称在 Windows 重新执行 Mac 硬件测试。

真实 NimBLE 任务最小剩余栈没有单独测量；176/320 字节 ELF 局部栈帧检查和实际 BLE 操作通过是两类证据。识别准确率仍取决于所选服务、声音及网络，发送前须核对文字。极端历史超过桌面 IPC 帧限制的情况不在验收范围内。

## 下载与更新

从[完整下载中心](../DOWNLOADS.md)选择组件。先更新电脑应用；已有 TAB5 使用升级 ZIP 内应用 BIN 和 sidecar，出厂设备才用首刷 ZIP。小屏使用 ESP8266 固件 ZIP。保留现有照片、配对、网络和展示设置；不要用全片首刷替代升级。

[安装说明](INSTALL.zh.md) · [TAB5 验收](development/TAB5-281-ACCEPTANCE-20261027.md) · [小屏字体](development/ESP8266-CREDIT-WEIGHT-20261027.md) · [Mac 最终来源隔离验收](development/MAC-MODEL-ACCOUNTS-20261026.md)

## English

Desktop 0.6.11 supports TAB5 on Windows and Apple Silicon macOS; TAB5 .281 adds hold-to-clear while retaining photos, and ESP8266 .57 strengthens reset-credit text. The release also improves Windows voice focus, delayed text handling, bounded history, previews, quota-source separation and small-screen Wi-Fi fallback. Mac permissions, speech and computer-control paths are platform-specific. Packages are unsigned/ad-hoc signed as documented, not notarized. Existing devices use upgrade packages and retain settings; factory ZIPs are only for first installation. User hardware acceptance, Mac phase records, static BLE frame checks and the unmeasured runtime stack watermark are recorded separately above. Review recognized text before sending.
