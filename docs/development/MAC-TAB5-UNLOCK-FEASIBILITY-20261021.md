# Mac 密码保存与解锁链路核对

## 当前范围

用户已明确允许桥接客户端保存 Mac 登录密码，并要求参考 Windows 保存 PIN 和解锁的方式。此前交接中“Mac 一键解锁不在当前能力范围内”不再是拒绝开发的范围限制；但不部署服务、不修改或刷写固件、不发布、不合并 main 的要求仍有效。

密码尚未采集或保存，未添加不可用的密码输入入口，也未启用任何解锁能力。运行候选仍为 `3c9b90d`。本次是实现与兼容性调查，不是 Mac 解锁验收通过。

## 已核对的实现

- Windows `Tab5PinStore.cs` 以当前用户 DPAPI 保存配对身份绑定的数字 PIN，独立开关控制使用。USB／BLE 的一次性 `unlock-pin/read` 在现有配对加密内再封装凭据，由设备发出 HID 按键；保存本身不负责解锁。
- Windows `Tab5WifiUnlock.cs` 不把 PIN 发给 TAB5，而是通过已配置并认证的电脑本地 RFB 服务执行按键。当前用户要求不部署服务，不能据此自动在 Mac 开启新服务。
- 已核对实际 TAB5 `.234` 参考源码包 `source/firmware/main/ble_keyboard_flow.h`。共享键盘事务明确拒绝长度不在 4–64 或包含非数字的输入，按数字键码映射后追加一次 Enter。当前 Mac 密码中的字母、符号不能沿用这一字段直接发送。
- 将该原始头文件临时解压到 `/tmp`，以 clang `-Wall -Wextra -Werror` 编译独立测试，仅使用虚构样本：数字接受，字母、符号、UTF-8 样本拒绝，四项均符合源代码行为。没有读取真实密码，没有改动固件源文件、构建固件或刷机；这不是硬件解锁测试。
- Mac `MacTab5Computer.swift` 的常规状态和 `lock-screen` 回应目前都将锁定状态标为未知。锁屏按键执行成功不能证明实际锁定。一次当前用户会话只读探针中，CoreGraphics 会话和 IORegistry 当前用户记录都没有可用的 `CGSSessionScreenIsLocked` 布尔值；不能把字段缺失解释为未锁定。该字段也不是 SDK 公开定义的会话状态常量。不能据此开放自动凭据输入。

用户随后明确：先修锁屏显示和 Codex 左右切换，解锁后续应支持字母、数字及符号密码。

## 后续实现要求

1. 凭据持久化使用本机 Keychain，独立条目绑定当前账户和已配对设备；支持更新、删除和独立启用。密码不进入偏好、RPC 状态、日志、诊断或版本库。不要求用户将密码改成纯数字。
2. 先确定并验收实际输入路径、准确锁定状态和目标会话。保存密码不是这三项的替代品；普通用户进程持有密码不等于能可靠操作 macOS 登录窗口。
3. 现有 HID 路径若要支持一般 Mac 密码，需要扩展设备输入能力并重新验收，这超出本轮不改固件的边界。现有 Windows Wi-Fi 路径不能直接视为 Mac 可用。
4. macOS 内置屏幕共享是可另行评估的服务方案，不等于已经证明能从桥接解锁当前本地会话。Apple 文档说明需显式开启和配置访问权限。当前不启用、不部署、不声称可用。
5. 最终应验证错误密码、过期／重复请求、取消、断连、用户切换和真实解锁回执；不确定时不得自动重输。真实密码由用户在本机输入，不通过聊天提供。

参考：[Apple 屏幕共享设置](https://support.apple.com/en-gb/guide/mac-help/mh11848/mac)。

## English

The user explicitly authorized storing a Mac login password in the bridge. This expands the prior development scope but does not authorize service deployment, firmware changes/flashing, publication or merging main. No password has been collected or stored, no unlock capability enabled, and the running candidate remains `3c9b90d`.

Windows stores a pairing-bound numeric PIN with per-user DPAPI and a separate enable switch. USB/BLE exports it only in the existing one-use encrypted grant for device HID input; Wi-Fi uses an authenticated local RFB service. Neither is merely a storage function. The actual `.234` firmware keyboard transaction accepts only 4–64 digits. A temporary clang test against the unmodified archived header confirmed acceptance of synthetic digits and rejection of alphabetic, punctuation and UTF-8 samples. No real credential, firmware edit, firmware build or flash was involved.

Mac lock replies currently remain unknown. A read-only current-session probe found no usable `CGSSessionScreenIsLocked` Boolean in either inspected source. This is not a public SDK session constant, and absence must not become an unlocked assertion. Successful lock-key submission is not state confirmation.

Future storage should use a separate local Keychain item bound to the account and paired device, with update/delete and independent enable controls. A working input path, exact session and actual lock/unlock observations must precede credential collection and capability advertisement. General Mac passwords exceed the current device HID contract; extending it remains outside the current firmware boundary. Apple's built-in Screen Sharing requires explicit service/access configuration and has not been validated as a local-session unlock backend here. It has not been enabled. Never request a numeric-only account password, log credentials, or retry uncertain input.
