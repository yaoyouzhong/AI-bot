# Windows ZIP 手动安装：下载、解压、启动

[下载选择指南](DOWNLOAD.zh.md) · [小屏刷机](FLASH.zh.md) · [Mac 安装](MAC_PACKAGE.md)

适用 Windows 10/11 x64，当前示例为 **v0.1.3 测试版**。这是免安装 ZIP：不用 Git、Python 或 .NET SDK。

> **普通用户推荐 [联网精简安装包](WINDOWS_INSTALLER.md)，约 8 MB，自动准备运行环境。** 下面仅供选择 ZIP 的用户手动安装。

![安装路线示意](assets/guides/install.svg)

## 1. 下载应用包

打开 [v0.1.3 发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.1.3)，下载：

**[AIBotBridge-0.1.3-local-candidate-win-x64.zip](https://github.com/yaoyouzhong/AI-bot/releases/download/v0.1.3/AIBotBridge-0.1.3-local-candidate-win-x64.zip)**

认准结尾 **win-x64.zip**。不要选 Mac、source 源码包，或把 `.sha256` 当程序。附件不用全部下载；`local-candidate` 是沿用的打包名称。

建议同时下载同名 `.sha256`，检查方法在本页末尾。

**完成标志：** 下载目录中有约 7.6 MB 的 Windows ZIP。

## 2. 首次使用：准备运行环境

**不是临时安装，也不是每次启动都要安装。** 它们是保留在 Windows 中的公共运行环境，AI-bot 使用时自动调用，不需要你单独打开。已具备对应组件的电脑可以跳过；以后更新 AI-bot 通常不用重装组件。

当前 ZIP 没有内置 .NET 桌面运行环境，所以“免安装 ZIP”仅指 AI-bot 本身直接解压运行。

| 组件 | 什么时候需要 |
| --- | --- |
| .NET 8 Desktop Runtime x64 | 启动当前 Windows 应用需要；已有兼容的 8.0.x 桌面运行时可跳过 |
| Microsoft Edge WebView2 Runtime | 国产模型网页登录、授权和相关额度功能使用；仅使用其他功能时不必先手动安装。Windows 11 及很多 Windows 10 电脑已具备它 |

### 2.1 安装 .NET：选择桌面运行时，再点安装

1. 打开 [微软 .NET 8 官方下载页](https://dotnet.microsoft.com/zh-cn/download/dotnet/8.0)。
2. 在页面找到 **.NET Desktop Runtime / .NET 桌面运行时** 区域，在 **Windows** 一行点 **x64**。选择 8.0 系列最新补丁版本即可；不要选 SDK、ASP.NET Core Runtime、x86 或 Arm64。
3. 下载完成后，打开浏览器的下载列表（可按 `Ctrl+J`），点击文件。文件名形如 **`windowsdesktop-runtime-8.0.xx-win-x64.exe`**，其中 `xx` 会随更新变化。
4. 安装窗口中点 **安装 / Install**。若 Windows 询问是否允许此应用更改设备，核对发布者为 **Microsoft Corporation** 后点 **是**。公司电脑若要求管理员凭据，请联系 IT 管理员。
5. 等待出现安装成功提示，点 **关闭 / Close**。如果提示需要重启，先保存工作，再按提示重启。

**怎样确认：** 打开 Windows **设置 → 应用 → 已安装的应用**（Windows 10 为“应用和功能”），搜索 `Desktop Runtime`，确认存在 **Microsoft Windows Desktop Runtime - 8.0.x (x64)**。安装器若直接进入修复/卸载页面，说明该版本已存在，关闭即可，无须卸载。

### 2.2 WebView2：网页授权需要时再补装

如果国产模型授权窗口已经能正常显示登录网页，**直接跳过本节**。装有 Edge 浏览器本身不能作为 WebView2 Runtime 已就绪的判断依据。

若窗口提示“WebView2 初始化失败，请安装或修复 WebView2 Runtime”，按以下步骤补装：

1. 打开 [微软 WebView2 官方下载页](https://developer.microsoft.com/microsoft-edge/webview2/)，找到 **Download / 下载** 区域。
2. 找到 **Evergreen Standalone Installer（常青版独立安装程序）**，点击 **x64**；若出现许可提示，阅读后选择接受并下载。不要选 Fixed Version 或 Arm64。
3. 下载完成后双击 **`MicrosoftEdgeWebView2RuntimeInstallerX64.exe`**。若出现 Windows 权限提示，核对发布者为 **Microsoft Corporation** 后继续。
4. 等待安装完成。提示“已安装”或“已安装最新版本”时不用重装；不要因此卸载现有组件。
5. 从托盘正常退出 AI-bot，再启动，打开 **模型额度 → 国产模型额度授权**。

**怎样确认：** 授权窗口可以显示厂商登录网页。若仍空白，记录具体错误并检查网络，不能仅凭空白就判断缺组件。

**装完以后：** 下载的安装 EXE 可以删除，系统中的运行组件要保留。不会要求你每次开机手动启动组件；WebView2 常青版会按微软更新机制维护。

安装方式参考微软的 [.NET Windows 安装说明](https://learn.microsoft.com/dotnet/core/install/windows)和 [WebView2 部署说明](https://learn.microsoft.com/microsoft-edge/webview2/concepts/distribution)。

## 3. 完整解压

1. 旧版正在运行时，右键 AI-bot 托盘图标 → **退出**。
2. 右键 ZIP → **全部解压**，放在固定目录，例如用户目录下的 `Apps\AI-bot-0.1.3`。
3. 进入解压后的文件夹，找到 **AIBotBridge.exe**。

不要在压缩包里运行，也不要只复制 EXE。旁边的 DLL、`runtimes`、`licenses` 等全部保留。

**完成标志：** 普通文件夹中能看到 EXE 和配套文件。

## 4. 双击启动，看右下角托盘

双击 `AIBotBridge.exe`。它常驻托盘，不会主动打开大窗口。看不到图标时，先点右下角 **∧ 隐藏图标**。

| 左键：打开镜像 | 右键：打开菜单 |
| --- | --- |
| <img src="assets/screens/mirror-window.png" width="330" alt="镜像真实离线示例截图"> | <img src="assets/screens/tray-menu.png" width="330" alt="托盘菜单真实示例截图"> |

图片为已有版本的真实离线示例，额度是样例，菜单文字可能随版本略有调整。

**完成标志：** 左键能打开镜像，右键能打开菜单。

## 5. 接上小屏

已刷 AI-bot 固件：直接接 USB **数据线**。尚未刷入：先完成[刷机图解](FLASH.zh.md)。

1. 右键托盘 → **设备连接 → USB 管理与诊断 → 设备信息…**。
2. 能读到设备信息后，选择 **显示模式 → 系统监控**。
3. 看实体屏是否出现并更新 CPU、内存等数据。
4. 最后恢复 **智能跟随**，启用循环展示，保留原页面顺序和间隔。

<img src="assets/screens/device-control.png" width="680" alt="设备控制真实离线示例，未连接状态不代表连接成功">

**完成标志：** 设备信息有响应、实体屏能切页。只有电脑镜像有画面不算连接成功。基础 USB 使用不必先配 Wi-Fi。

## 6. 按需要设置

| 需求 | 右键菜单入口 |
| --- | --- |
| 城市与天气 | 内容设置 → 设置天气 → 数据源与定位 |
| 自选股 | 内容设置 → 设置自选股 |
| Claude / Codex 额度 | 先在本机对应 CLI 登录，再选桥接服务 → 刷新状态 |
| 国产模型登录 | 模型额度 → 国产模型额度授权 |
| 开机自动运行 | 桥接服务 → 开机启动 |
| 页面轮播 | 循环展示 → 调整展示顺序 |

更多配图见[功能图鉴](FEATURES.zh.md)。默认已有桌宠，不需要另找图片。

## 升级与排错

升级：**退出旧版 → 完整解压新版 → 启动新版 EXE**。保留 `%APPDATA%\AI-bot` 和 `%LOCALAPPDATA%\AI-bot`；不要清空。移动程序目录后，关闭再开启一次“开机启动”，更新启动路径。

**v0.1.3 主要更新 Windows 程序；已有正常工作的 AI-bot 固件，不必为本次升级重新刷机。**

| 现象 | 先这样处理 |
| --- | --- |
| 双击没有大窗口 | 展开右下角隐藏托盘，找 AI-bot 图标 |
| 提示缺少 .NET | 安装第 2 步的 Desktop Runtime 8 x64 |
| 网页授权空白 | 检查 WebView2 和网络 |
| 串口占用 | 正常退出旧桥接、刷机工具和串口监视器 |
| 小屏不动 | 核对数据线、设备信息响应和固件硬件型号 |
| 趋势有空白日期 | 缺失历史不会补造，先确认当前额度能刷新 |
| 系统拦截运行 | 核对来源和哈希，不关闭系统防护 |

<details>
<summary>可选：检查下载文件的 SHA-256</summary>

在下载目录地址栏输入 `powershell` 后回车，逐行执行：

```powershell
Get-FileHash -Algorithm SHA256 .\AIBotBridge-0.1.3-local-candidate-win-x64.zip
Get-Content .\AIBotBridge-0.1.3-local-candidate-win-x64.zip.sha256
```

两串哈希应相同，忽略字母大小写。哈希检查完整性，不是数字签名。

</details>

开发者自行编译请看[源码构建进阶说明](BUILD_WINDOWS.zh.md)。
