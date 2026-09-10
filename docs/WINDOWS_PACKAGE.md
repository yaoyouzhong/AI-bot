# Windows 本地候选包 / Local Windows candidate

本包是未发布的 win-x64 候选，不是安装器，也不是全部功能已验收的正式版。

使用和再次分发前请阅读随包 `DISTRIBUTION_TERMS.md` 及 `licenses/` 中的第三方
原始条款。微软 SDK 两个 DLL 按官方 REDIST 清单原样随应用分发，不适用 AI-bot
的 MIT 授权。`DEPENDENCIES.json` 记录本次实际收集的包版本、许可和 SDK DLL 哈希。

## 使用

1. 在 Windows 10/11 x64 上安装 .NET 8 Desktop Runtime x64；国产额度网页授权还需要 Microsoft Edge WebView2 Runtime。本包为 framework-dependent，不包含这两个运行时。
2. 将 ZIP 完整解压到固定目录，不要只复制 EXE。保留 DLL、runtimes、许可证等配套文件。
3. 从资源管理器双击 `AIBotBridge.exe`，程序进入托盘，不创建终端窗口。不要同时运行旧桥接和新桥接，避免争用串口和 8765 端口。
4. 需要登录 Windows 后自动运行时，右键托盘 → 桥接服务 → 开机启动。程序移动目录后应重新设置启动项。
5. 天气/股票/账号需要各自的配置或授权。公开包不含维护者的 API Key、Cookie、账号缓存、Wi-Fi 配对或私有桌宠；默认显示与维护者导入私人素材后的显示可能不同。

升级前正常退出桥接，将旧程序目录保留为备份，再解压新包；不要删除 `%APPDATA%\AI-bot` 和 `%LOCALAPPDATA%\AI-bot`，其中有用户设置和运行缓存。候选包不能证明 Mac、所有账户或设备视觉均已验收。Wi-Fi 回退要求设备能实际访问电脑地址，不能穿透单位的客户端隔离。

## 验证

`FILES.sha256` 包含包内文件 SHA-256；ZIP 旁的 `.sha256` 校验 ZIP 本身。哈希用于检查完整性，不是数字签名或发行者身份凭证。

安装了 .NET 8 SDK 或提供 dotnet 宿主的对应运行时后，可在解压目录运行：

```powershell
dotnet AIBotBridge.dll --self-test-public
```

该测试不启动串口或真实账号刷新，输出保存在当前目录的 `artifacts/`。随包的 `TestFixtures/claude_sprite.h` 只有三个合成颜色像素，不是旧版桌宠。需要完整日志和退出码时使用 dotnet 命令，不直接异步调用 GUI EXE。

## English

Before use or redistribution, read `DISTRIBUTION_TERMS.md` and the original terms
under `licenses/`. The two Windows SDK DLLs accompany the application unmodified
under Microsoft's REDIST list and SDK terms, not AI-bot's MIT license.
`DEPENDENCIES.json` records the restored packages, notices and SDK binary hashes.

This is an unpublished, framework-dependent Windows 10/11 x64 candidate. Install
.NET 8 Desktop Runtime x64 and Microsoft Edge WebView2 Runtime separately. Extract
the entire ZIP to a stable directory and open AIBotBridge.exe in Explorer. Enable
login startup through the tray's bridge-service menu only if desired. Do not run
the old and new bridge simultaneously. Stop the app before upgrading, retain the
old program directory as a backup, and preserve the user's AppData directories.
No account credentials, pairing state or private pets are bundled. SHA-256 files
check integrity, not publisher identity. Packaged tests use synthetic fixtures and
do not establish real-account, hardware, macOS or network-isolation compatibility.
