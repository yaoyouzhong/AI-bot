# TAB5 纯 Wi-Fi 单次解锁试验

状态：本地候选，尚未进行真实 PIN 提交或解锁验收。仅验证本机 Windows Hello 数字 PIN 链路，不是常驻解锁服务。

## 界面与操作

- 复用应用控制页原有按钮：未锁定显示“锁屏”；锁定且桥接声明 `connectionHealth.unlockTrialVersion=1` 时显示“解锁”。旧桥接仍显示禁用的“已锁屏”。不增加第二个入口。
- 点击“解锁”打开数字键盘；PIN 仅显示掩码。用户每次输入 4–64 位数字，点击“确认解锁”。取消、离页、空闲 60 秒、失去纯 Wi-Fi 条件或提交后清空输入。
- 试验必须断开 USB 和蓝牙，并由用户先在电脑上选中 PIN 登录页。只凭 UIA `IsPassword` 不能区别 PIN 和账户密码，本试验不宣称自动识别登录方式。
- 每次启动临时辅助程序最多提交一次；结果不明不自动重试。只有实际 Windows 会话恢复未锁定才返回成功。

## 传输与权限

沿用配对设备的加密、鉴权、会话和防重放 RPC，新增 `kind=unlock-trial`，桥接仅接受 `Wi-Fi`。USB/BLE 拒绝；固件也主动检查两者均未连接及 Wi-Fi 最近状态有效。

1. `begin {confirmPinPage:true}`：辅助程序验证锁定会话及前台 LogonUI 中的受保护焦点控件，返回随机 `offer`（32 个十六进制字符）、`key`（32 字节 AES 密钥的 Base64）和 `expiresMs=15000`。该响应位于既有配对加密通道内。
2. `submit {offer,sealed}`：`sealed` 为 Base64(`nonce[12] || tag[16] || ciphertext`)；AES-256-GCM，AAD 为 offer 的 ASCII 字节。PIN 只在设备可清除缓冲区与辅助程序解密缓冲区中出现；桥接转发密文。密钥不构成独立于外层配对通道的端到端身份认证。
3. 辅助程序在解密前消耗本次提交资格；校验期限、认证标签、数字格式、原会话和原控件身份。任何失败都不复用提交资格。`cancel` 撤销未使用密钥；设备本地取消也清除待发送 PIN，已发送请求不能撤回。
4. 辅助程序一次发送数字，必要时仅一次 Enter；存在按下的修饰键时拒绝输入；复核锁定状态最多约 8 秒。响应仅含状态，不返回凭据或 UI 文本。

辅助程序不监听网络。桥接通过当前会话命名管道转发；管道 ACL 限 SYSTEM 和该登录用户，校验客户端会话；桥接核对服务进程位于受保护的 `C:\Program Files\AI-bot-WifiUnlockTrial\WifiUnlockTrial.exe`。临时 SYSTEM 计划任务无周期触发器、执行上限 4 分钟；等待锁屏最多 90 秒，安全桌面工作进程服务期 120 秒，并有看门狗。退出后任务移除，清理脚本仅删除带本工具归属凭据的明确程序文件，保留状态报告。

不记录 PIN、UI 文本、按键内容或明文请求，不写凭据到配置或文件；固件关闭 core dump。内存清零不等于能够保证操作系统永不分页或转储，试验不作该绝对承诺。

## 源码与验收边界

- 辅助程序：本仓库 `tools/wifi-unlock-trial/`。
- 桥接候选：维护工作树 `release-067-205/AI-bot/windows-app/AIBotBridge/Tab5Unlock.cs`、RPC 分发和 capability 字段。
- 固件候选：`artifacts/release-068-20261016/tab5-saver-source/firmware/main/unlock_remote.c`、`ui_unlock.h`、`ui_computer.h`。
- 自测验证加密、到期、重放、取消、焦点变化、缓冲区清零、管道冒充拒绝、纯 Wi-Fi 限制、同一按钮入口和重复点击抑制。所有自测使用合成数据，不输入用户 PIN。
- 构建、模拟界面和安全桌面只读检查不等于解锁通过。尚需授权部署候选、临时 SYSTEM 输入程序，并实际断开 USB/蓝牙，由用户在 TAB5 输入 PIN，观察真实解锁及清理结果。

Windows API 依据：[SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)、[WTSQueryUserToken](https://learn.microsoft.com/en-us/windows/win32/api/wtsapi32/nf-wtsapi32-wtsqueryusertoken)、[GetNamedPipeServerProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid)。这些 API 的存在不保证本机 PIN 提交成功。
