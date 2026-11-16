# AI-bot Mac 0.6.22

[正式发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.22) · [下载中心](../DOWNLOADS.md) · [安装指南](INSTALL.zh.md#mac)

本次只更新 Apple Silicon Mac 桥接。Windows 保持 0.6.21，TAB5 保持 0.2.321-ui，ESP8266 保持 0.5.8，无需刷新固件。

- 首次启动集中引导系统权限。未配对 TAB5 也能申请 Terminal 自动化；实际从设备控制 Terminal 仍需配对。
- 通知未收到确认时可以重试。辅助功能打开系统设置后，引导会继续检查当前 App 的权限。若开关已开但权限未生效，可查看当前 App 或稍后处理。
- 从公开 0.6.21 覆盖升级后，macOS 可能要求重新确认权限。已通过 Mac 原生构建、自动回归和原包校验；同份包的独立账户首装、重开及旧版覆盖保留权限和配对未完成完整实测。

## English

[Release](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.22) · [Downloads](../DOWNLOADS.en.md) · [Installation](INSTALL.zh.md#english-quick-start)

This release updates the Apple Silicon Mac bridge only. Windows remains at 0.6.21, TAB5 at 0.2.321-ui, and ESP8266 at 0.5.8. No firmware update is needed.

- First launch guides users through system permissions. Terminal Automation can be requested before pairing; controlling Terminal from TAB5 still requires a paired device.
- Retry an undecided notification request. The guide keeps checking Accessibility after opening System Settings. If its switch is on but access has not taken effect, reveal the current app or finish later.
- macOS may ask again for permissions after replacing the public 0.6.21 app. The Mac build, automated regression checks, and package verification passed. Full clean-account first launch and relaunch, and an upgrade proving permission and pairing retention, were not completed for this package.
