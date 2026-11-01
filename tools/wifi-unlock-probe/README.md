# 纯 Wi-Fi 解锁：安全桌面前置探针

此工具仅验证电脑端访问锁屏界面的前置条件，**不是解锁工具，不构成解锁成功证据**。
未接入 TAB5 PIN 输入，也不发送网络请求或按键。

## 目的与最小权限边界

普通桌面桥接无法直接操作 Winlogon 安全桌面。先确认独立 SYSTEM 进程能否在当前活动控制台会话中读取 LogonUI 的非内容属性，再决定临时 PIN 的提交实现。

- `--preflight`：普通用户只读检查会话、锁定状态、桌面名称、输入桌面可访问性。
- `--system-launch <session>`：只允许 SYSTEM、会话 0；最多等待 120 秒锁屏。会话改变、锁定状态未知均拒绝。复制自己的 SYSTEM token，把固定的同一程序以 `--inspect` 启动在指定会话 `WinSta0\Winlogon`。
- `--inspect <session>`：只允许 SYSTEM、指定的当前活动控制台且已锁定；仅枚举路径为系统目录 `LogonUI.exe` 的可见窗口，以及 Edit 的 IsPassword/HasKeyboardFocus 属性。未发现受保护输入框时，在 10 秒内每隔约 250 毫秒重试并复核会话，报告检查次数；12 秒独立看门狗，父进程 16 秒兜底终止自己的子进程。
- 不读取 UI Name、Value、Text、PIN、账户名、剪贴板，不截图、不注入按键、不创建登录认证组件、不监听网络、不更改桌面 ACL。
- `ProtectedEdits > 0` **不能判定该框是 Windows Hello PIN**，也不能证明可以提交输入。后续还需识别具体登录方式、验证一次性提交和 WTS 实际解锁状态。

## 本地运行

```powershell
dotnet build tools/wifi-unlock-probe/WifiUnlockProbe.csproj -c Release -o artifacts/development/wifi-unlock-20261017/probe
& artifacts/development/wifi-unlock-20261017/probe/WifiUnlockProbe.exe --self-test
& artifacts/development/wifi-unlock-20261017/probe/WifiUnlockProbe.exe --preflight
& tools/wifi-unlock-probe/Verify-WifiOnly.ps1
```

最后一项只取实时连接摘要，忽略历史固件报告、历史 HTTP 200。无法确认排除 USB/BLE 时退出 2。摘要检查通过也不替代测试过程中所有请求的 Wi-Fi 通道证据。

## 待用户明确批准后的一次性系统运行

批准对象：本次构建的探针和 SHA-256 清单；仅创建任务 `AIBot-WifiUnlockPreflight`，无定时触发器；SYSTEM 身份，最长 3 分钟；运行后自动注销任务。新建 `C:\Program Files\AI-bot-WifiUnlockProbe`，SYSTEM/管理员可写、普通用户只读。已存在的目录或任务拒绝覆盖。

从普通 Windows 桌面打开管理员 PowerShell，在审查过的运行包中执行 `Run-Preflight.ps1`。脚本先校验四个程序文件的哈希，再复制到受保护目录并复核哈希后启动任务。不是从 Codex 包重定向环境直接执行。

启动后，在 120 秒内手动锁屏并显示 Windows PIN 输入页面。**本前置探针不需要输入 PIN**。观察结束后用电脑原有方式解锁。仅 TAB5 的 USB/蓝牙要在最终纯 Wi-Fi 验证时排除；不要断开用于恢复操作的电脑键盘。

报告固定写入受保护目录的 `result.json`。脚本正常退出或报错都会尝试停止/注销自己创建的任务；若终端被强行关闭，任务运行时间仍受 3 分钟限制，但可能需要手动运行清理脚本注销残留任务。

回退：管理员执行 `Remove-Preflight.ps1`，检查固定绝对路径、归属凭据、任务动作、文件白名单、非重解析点后，停止/注销同名任务并删除本工具可执行文件，保留报告和归属凭据。不会删除原有用户数据或改变系统登录设置。

## 后续主链路验收（本探针不覆盖）

1. TAB5 USB 断开；BLE 无连接且不允许回退；Wi-Fi 心跳新鲜。
2. 明确识别目标用户会话的 Windows Hello PIN 输入方式，排除密码框、UAC 和普通应用。
3. TAB5 本次输入 PIN；仅短暂保留在内存；端到端鉴权加密；不进入日志、配置、崩溃转储或重试缓存。
4. 用户确认后仅提交一次；超时/连接切换不重发，取消清空。
5. 用 WTS 锁定状态和电脑实际桌面双重确认解锁，不以按键发送成功替代解锁。
6. 验证错误 PIN、断网、超时、取消、重复请求，不保存 PIN。

## 依据

- https://learn.microsoft.com/en-us/windows/win32/services/interactive-services
- https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createprocessasuserw
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openinputdesktop

以上系统 API 只说明权限和进程/桌面机制，不保证 PIN 远程提交可行；必须以本机锁屏实测确定。
