# Windows 联网精简安装：双击，按提示完成

[下载选择](DOWNLOAD.zh.md) · [ZIP 手动安装](INSTALL.zh.md) · [小屏刷固件](FLASH.zh.md)

**[下载 v0.1.3 Windows 安装包（约 8 MB）](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.1.3/AIBotBridge-0.1.3-setup-win-x64.exe)**

适用 Windows 10/11 x64。本版为测试版，完整首次安装、升级及卸载仍需全新电脑验收。

![Windows 联网安装步骤示意，非安装器截图](assets/guides/setup.svg)

## 用户只需这样操作

1. 双击安装 EXE，阅读欢迎页，点“下一步”。
2. 阅读许可协议并接受；安装位置通常保留默认值。
3. 选择是否创建桌面快捷方式。“检查运行环境”会显示两个组件是已安装还是需要补装。
4. 点“安装”。已有组件会跳过；缺少组件时保持联网等待；.NET 下载页显示进度并支持取消，下载校验通过后再允许微软安装器的管理员请求。
5. 看到“AI-bot 安装完成”后点“完成”。默认启动 AI-bot，在右下角托盘找图标。

这是联网精简安装包：不内置约 59 MB 的 .NET 安装器，只在本机缺少 .NET 8 桌面运行时时下载。已有两个组件时会跳过组件下载。首次缺少组件的电脑仍需要下载运行环境，减少的是安装 EXE 的体积。

不需要自行寻找组件下载按钮，也不需要手动解压。系统中的组件保留供以后使用，不是每次启动都要安装。若单位电脑不允许安装运行环境，需要 IT 管理员协助。

若提示权限取消、下载失败或安装未完成，处理提示后重试；不会将失败当作安装成功。微软要求重启时，先保存工作再重启。安装 EXE 目前没有项目代码签名；不要求关闭系统防护。

升级前从托盘正常退出旧版，再运行新安装包。由旧 ZIP 迁移且启用了开机启动时，在新版托盘关闭、再开启一次“开机启动”以更新路径。安装向导不自动开启开机启动。

Windows 设置 → 应用 → AI-bot → 卸载，可以移除应用。用户的 `%APPDATA%\AI-bot`、`%LOCALAPPDATA%\AI-bot` 和公共运行环境会保留；卸载前请退出程序并在托盘关闭曾启用的开机启动。

## 构建与验证

`scripts/package_windows_local.ps1` 从独立源码快照生成应用、收集许可、构建安装向导并校验 ZIP。无需全局安装 Inno Setup；编译器作为固定哈希的 NuGet 构建材料解压到任务缓存。

安装向导版本读取根目录 `VERSION`。打包器校验 .NET 的微软发布 SHA-512 和两个组件安装器的 Authenticode 微软签名，将 .NET 的固定下载地址和 SHA-256 编入安装器，并写入 `INSTALLER_DEPENDENCIES.json`。用户端下载完成后再次核对 SHA-256，通过后才运行；失败或取消时不继续安装。随包保留 Inno Setup 原始许可及应用全部许可。

开发者可使用以下只读诊断，不安装组件、不启动 AI-bot：

```powershell
.\AIBotBridge-<版本>-setup-win-x64.exe /CHECKONLY="C:\临时目录\环境检查.txt" /VERYSILENT
```

`/SELFTEST=<报告路径>` 检查编译后的版本判定逻辑；二者都会在真正安装前退出。报告父目录须已存在。退出安装时返回非零码是 Inno 的正常行为，检查报告内容判断是否通过。

已具备环境的本机不能证明缺少组件的全新 Windows 已验收。新机验收需覆盖：两个组件均缺失、只缺一个、取消管理员许可、断网、重试、升级、卸载保留用户数据。不得在日常工作电脑上卸载公共组件来伪造新机测试环境。

## English

The v0.1.3 Windows online setup pre-release detects .NET 8 Desktop Runtime x64 and Evergreen
WebView2, skips installed prerequisites, and installs only missing components.
.NET is downloaded from a pinned Microsoft URL only if missing, with progress, cancellation and SHA-256 verification before execution. WebView2 uses Microsoft's bundled online bootstrapper. The per-user
application install creates shortcuts, supports uninstall and preserves AppData
and shared runtimes. It does not enable logon startup automatically. The candidate
is unsigned. Clean-machine prerequisite installation,
permission denial, offline retry and upgrade/uninstall need separate acceptance.

下载测试使用独立编译的本地 HTTP 测试夹具，覆盖正常响应、内容被修改、HTTP 404，以及环境检测不发起下载；不安装或运行测试响应。取消交互和全新电脑补装仍需实测。
