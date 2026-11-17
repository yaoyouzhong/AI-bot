# AI-bot 0.6.24 与 TAB5 0.2.322-ui

[下载中心](../DOWNLOADS.md) · [Windows 安装指南](INSTALL.zh.md) · [软件固件操作](UPDATES.md#windows-0624)

本次发布 Windows x64、Apple Silicon Mac 桥接 0.6.24 和 TAB5 固件 0.2.322-ui，ESP8266 保持 0.5.8。桥接和 TAB5 固件可分别升级。

### Windows 与 Mac 桥接

- **统一查更新。** 到「设备中心 → 桥接设置 → 软件固件」查看软件、固件和图库更新及数量。Mac 菜单栏也会显示数量。点击通知可打开相应项目。
- **浏览和安装图库各有入口。** 设备卡片的「屏保图库」打开作品浏览器。到「软件固件 → 屏保图库 → 管理图库…」查看新版、下载或导入。管理窗口沿用上次查到的版本，两套图库都有新版时合计为一项。安装时点退出会回到图库窗口。
- **设备设置改名。** TAB5 通过「连接设置」管理 Wi-Fi、USB 配对、固件升级和测速。ESP8266 小屏通过「管理 → 手动刷机…」刷机，清除 Wi-Fi 则用「连接设置 → 重置网络…」。
- **Windows 安装和控制。** 取消打开安装器会提示「安装包已下载，尚未安装」。一键解锁改进了 PIN 或密码输入框的识别。管理员模式运行桥接时，Codex 直达会在当前桌面打开对应会话。
- **桌宠和连接。** Claude 终端等待输入时可显示「空闲」。改进 Claude 后台会话和 Codex 总览、已读同步的兼容性。TAB5 需要重新验证连接时，优先沿用原 USB 连接。控制页优先响应操作，蓝牙请求超时可单独恢复。

### TAB5 固件

- **日历更好找。** 应用九宫格重新排布，点右下角「日历」直接看月历和生日。
- **解锁和总览。** USB 一键解锁优先处理登录。总览的萌宠状态保持单行，Codex 离线提示完整显示。
- **图库同步可接着传。** 遇到可重试的中断时，优先用当前连接继续，保留已校验的进度。

TAB5 已有 AI-bot 请用升级包，首次安装请用首刷包。

完整改动见[桥接更新日志](../CHANGELOG.zh.md)和 [TAB5 更新日志](development/TAB5-CHANGELOG.zh.md)。

## English

[Downloads](../DOWNLOADS.en.md) · [Installation](INSTALL.zh.md#english-quick-start) · [Software and firmware updates](UPDATES.md#windows-0624)

This release ships bridge 0.6.24 for Windows x64 and Apple Silicon Mac and TAB5 firmware 0.2.322-ui. ESP8266 remains at 0.5.8. You can upgrade the bridge and TAB5 separately.

### Windows and Mac bridge

- **Check updates in one place.** Go to Device Center → Bridge Settings → Software/Firmware to see desktop, firmware, and artwork updates and their count. The Mac menu bar also shows the count. Click a notification to open the relevant item.
- **Browse or install artwork.** The device's Artwork Collections card opens the browser. To check versions, download, or import, go to Software/Firmware → Artwork Collections → Manage Collections. It uses the last successful check, and two new collections count as one update. Quitting during installation returns you to the collection window.
- **Device settings.** TAB5 Connection Settings includes Wi-Fi, USB pairing, firmware upgrades, and speed tests. On ESP8266, use Manage → Manual Flash for USB flashing and Connection Settings → Reset Network to clear saved Wi-Fi settings.
- **Windows installation and controls.** If you cancel opening the downloaded installer, the bridge tells you installation has yet to start. One-click unlock improves detection of the PIN or password field. When the bridge runs as administrator, Codex Direct opens the selected conversation on the current desktop.
- **Companion and connectivity.** The companion can show Idle while a Claude terminal waits for input. Compatibility improves for Claude background sessions and for Codex overview and read status. The bridge tries the existing USB connection first when it needs to authenticate again. Device controls take priority while the control page is open. Bluetooth requests can recover individually after timeouts.

### TAB5 firmware

- **Calendar is easier to find.** The app grid has a new layout. Tap Calendar in the bottom-right corner to see the month view and birthdays.
- **Unlock and overview.** One-click unlock over USB prioritizes signing in. The overview fits companion status on one line and shows the full Codex offline label.
- **Artwork sync can resume.** After an interruption that can be retried, artwork sync tries the same connection first and keeps verified progress.

Use the upgrade package on a TAB5 that already runs AI-bot. Use the first-install package for a first flash.

See the [bridge changelog](../CHANGELOG.md) and [TAB5 changelog](development/TAB5-CHANGELOG.md) for all changes.
