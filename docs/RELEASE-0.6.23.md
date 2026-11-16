# AI-bot Mac 0.6.23

[正式发布](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.23) · [下载中心](../DOWNLOADS.md) · [Mac 安装指南](INSTALL.zh.md#mac)

本次仅更新 Apple Silicon Mac 桥接。Windows 保持 0.6.21，TAB5 保持 0.2.321-ui，ESP8266 保持 0.5.8，无需刷新固件。

- 覆盖升级后按 macOS 返回的实际状态提示缺失权限，跳过仍然有效的授权。
- 启用电脑控制时会检查「系统事件自动化」。需要辅助功能时直接打开系统设置，不额外弹出一个打开设置的提示框。
- 从公开 0.6.22 使用原包覆盖实测：重新确认缺失权限后，退出重开未再弹窗，原配对和设置保留。TAB5 不在线，未测实际重连；独立账户首次安装待验证。

## English

[Release](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.23) · [Downloads](../DOWNLOADS.en.md) · [Installation](INSTALL.zh.md#english-quick-start)

This update is for the Apple Silicon Mac bridge only. Windows remains at 0.6.21, TAB5 at 0.2.321-ui and ESP8266 at 0.5.8. No device needs a firmware update.

- After upgrading, the bridge checks current system permissions and prompts only for access that is missing.
- With computer control enabled, the bridge checks System Events Automation. Missing Accessibility access opens System Settings directly, without a separate dialog inviting you to open it.
- An upgrade from public 0.6.22 to the exact release package preserved pairing and settings. After the missing permissions were confirmed, reopening did not prompt again. TAB5 was offline, so reconnection was not tested. A fresh install in an independent macOS account remains unverified.
