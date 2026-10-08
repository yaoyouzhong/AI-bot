# 软件与固件更新 / Software and firmware updates

本页对应 Windows 0.6.2；旧版 v0.5.0 用户需先安装新电脑端才能使用完整下载流程。macOS 尚未实现此入口。

[完整下载中心](../DOWNLOADS.md)集中提供 Windows / Mac 桥接、ESP8266 固件、TAB5 首刷与升级包、书法和名画图库。桥接 0.6.2 的「完整下载与安装说明」链接也指向此处。

## 日常使用

1. 在 Windows 托盘右键进入「设备中心 → 桥接设置 → 软件固件」，打开「软件与固件更新」。列表同时显示电脑软件和设备中心已添加的设备，分别列出当前版本、可用版本及操作状态。
2. 点「检查更新」，选择要更新的一行，查看「所选更新说明」。设备版本来自已连接设备，不能根据电脑端版本猜测。支持版本上报的小屏按真实固件版本判断更新。
3. 点「下载并安装电脑端」或「下载并准备升级」。程序自动选对应包并核对 SHA-256。下载可以取消，校验失败不会进入安装。
4. 电脑端确认后打开安装程序；ESP8266 进入已有的小屏刷机窗口，连接 USB 后先备份再写入；TAB5 在设备「设置 → 固件升级」确认安装，重启后在升级工具核验启动。电脑端、两种硬件的版本号无需一致，也无需一起升级。

勾选「自动检查并提醒」后，程序启动约 30 秒开始检查，成功后每 24 小时再检查；失败约 1 小时后重试。每个设备的同一版本只提醒一次，遵守静默时段。点击提醒进入更新窗口。关闭自动提醒不影响手动检查。只有已添加、启用且已读取当前版本的设备参与可用升级判断。

0.6.2 修复：已连接的旧版 ESP8266 如果未上报固件版本，显示「版本未知（旧固件未上报）」，可以点「手动准备小屏升级」。这不表示发现了更新，不触发自动更新提醒。下载并校验最新正式固件包后，程序会说明无法排除同版本或降级，由用户确认进入 USB 刷机工具；仍需连接该小屏并点击开始刷机，先完整备份，再写入和校验。离线、版本上报无效、下载期间设备或版本改变都会阻止继续。新版固件通过 USB 和已认证的 Wi-Fi 请求上报自身版本。0.6.1 及更早的安装包不包含此修复。

TAB5 日常更新只下载升级 ZIP；新购设备首次安装仍使用设备中心的首刷流程。下载包与首次安装说明见[安装指南](INSTALL.zh.md)。本地与公开版功能统一，名画和书法图库独立下载；在本窗口“屏保图库”中导入 ZIP，程序升级保留图库。详见[图库说明](GALLERY-PACKS.md)。

## 验证与范围

查询来自本项目 GitHub 正式 Release，忽略草稿和预发布。下载校验清单与安装包必须属于对应发布记录。下载后校验文件、固件类型、内置版本和必要元数据，再调用原安装工具。下载期间设备移除、关闭或版本改变，会阻止继续升级。网络失败保留本次运行中最近一次成功结果并显示错误，不宣称更新成功。

此入口负责发现、下载和交接。安装后的真实运行版本、TAB5 精确镜像启动结果和实体屏效果仍需分别核验；下载成功不等于升级完成。自动后台检查不会下载安装器或刷写设备。

## English

The [complete download center](../DOWNLOADS.en.md) lists both desktop apps, ESP8266 firmware, TAB5 first-install/upgrade packages and both artwork collections. Bridge 0.6.2's manual-download link opens this center.

This describes Windows 0.6.2; v0.5.0 users need to install the newer computer app first. macOS does not yet implement this entry point. Open Device Center → Bridge Settings → Software/Firmware to reach Software and Firmware Updates, check releases, select the computer or an added device, read its notes and download the matching package. SHA-256, package identity and compatibility are validated before handoff. Confirm the Windows installer, use the existing ESP8266 USB backup/flash tool, or confirm installation on TAB5 and verify boot afterward. First-install TAB5 packages remain in the device-center setup flow. Component versions are independent.

Optional automatic checks start about 30 seconds after launch and repeat daily after success, retrying failures after an hour. Notifications respect quiet hours and are deduplicated per device/version. Background checks never download or install. Failed queries retain the last successful in-memory catalog with an error. Unknown or changed device versions block automatic upgrade selection. Local and public builds share the same features. Import optional artwork ZIPs through Artwork Collections; app upgrades preserve the user-data collection directory. Manual checking remains available when reminders are disabled. An accepted download does not prove installation, verified boot or physical display behavior.

0.6.2 fix: connected legacy ESP8266 devices that omit their firmware version offer a clearly labeled manual preparation action. This does not assert that a newer version exists and never generates an automatic update notification. After downloading and verifying the latest stable package, the user must acknowledge that the version cannot be compared and may be equal or older. The existing USB tool still requires an explicit start, complete backup, write and verification, bound to the selected screen's USB identity when available. Offline devices, malformed versions and target/version changes block continuation. New firmware reports its component version over USB and authenticated Wi-Fi requests. The 0.6.1 and earlier installers do not include this fix.
