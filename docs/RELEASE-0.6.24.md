# AI-bot 0.6.24 与 TAB5 0.2.322-ui

[下载中心](../DOWNLOADS.md) · [Windows 安装指南](INSTALL.zh.md) · [软件固件操作](UPDATES.md#windows-0624)

本次发布 Windows x64、Apple Silicon Mac 桥接 0.6.24 和 TAB5 固件 0.2.322-ui，ESP8266 保持 0.5.8。桥接和 TAB5 固件可分别升级。

- **软件、固件和屏保图库合在一处。** 从「设备中心 → 桥接设置 → 软件固件」检查更新。「管理图库…」可查看和安装名画或书法。图库正在安装时，退出操作会带你回到该窗口；安装完成或取消后即可退出。
- **更容易找到设备操作。** 选中 TAB5 后打开「连接设置」，可以管理 Wi-Fi、USB 配对、固件升级和测速。更新通知打开对应项目；取消启动已经下载的 Windows 安装器时，会明确提示尚未安装。
- **改善 TAB5 上的 Claude、Codex 和连接。** Claude 终端等待输入时，桌宠可以显示「空闲」；继续后台会话时按实际功能判断。Codex 总览和已读同步按可识别的数据字段工作。桥接也会优先在原 USB 连接重新认证，并尽量让蓝牙的单项超时不影响其他传输。

- **TAB5 应用与总览。** 新增日历入口，重排应用九宫格，萌宠状态固定单行显示，图库传输可保留已校验的数据块。已有安装请选择升级包，首次刷写请选择首刷包。

完整改动见[桥接更新日志](../CHANGELOG.zh.md)和 [TAB5 更新日志](development/TAB5-CHANGELOG.zh.md)。

## English

[Downloads](../DOWNLOADS.en.md) · [Installation](INSTALL.zh.md#english-quick-start) · [Software and firmware updates](UPDATES.md#windows-0624)

This release ships bridge 0.6.24 for Windows x64 and Apple Silicon Mac and TAB5 firmware 0.2.322-ui. ESP8266 remains at 0.5.8. You can upgrade the bridge and TAB5 separately.

- **Software, firmware, and screensaver artwork in one place.** Check for updates under Device Center → Bridge Settings → Software/Firmware. Manage Collections opens the painting and calligraphy options. If you try to quit while a collection is being installed, the bridge returns you to that window until the operation finishes or is cancelled.
- **Easier to find device controls.** TAB5 Connection Settings contains Wi-Fi, USB pairing, firmware upgrades, and speed tests. Update notifications select the relevant item. If you cancel opening a downloaded Windows installer, the bridge makes clear that it was not installed.
- **Improved Claude, Codex, and connections for TAB5.** The companion can show Idle when a Claude terminal is waiting for input; resuming a background session depends on its available controls. The Codex overview and read status use recognized data fields. The bridge also tries to reauthenticate on the existing USB connection and isolates individual Bluetooth timeouts where possible.

- **TAB5 apps and overview.** Open Calendar from the reorganized app grid, read the companion status in one line, and keep verified artwork blocks when transfers resume. Use the upgrade package on an existing AI-bot installation and the first-install package for a first flash.

See the [bridge changelog](../CHANGELOG.md) and [TAB5 changelog](development/TAB5-CHANGELOG.md) for all changes.
