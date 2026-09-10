# macOS（Apple Silicon 测试版）/ Mac test build

适用 macOS 13 及以上、Apple Silicon（M 系列，arm64）。Intel 尚未纳入候选打包验收。

`f7c2d31` 已通过 31 项 Mac 测试、Release 编译、`.app` 打包、签名完整性及 ZIP 解压校验。仍需真实 Mac 首次启动、权限、设备连接和持续运行验收；不宣称与 Windows 功能完全一致。

## 获取与安装

仓库尚无正式 Release。可从[已通过的 Candidate packages 运行页](https://github.com/yaoyouzhong/AI-bot/actions/runs/34465456002)下载 `candidate-macos-arm64` 附件。解压 Actions 附件后，可见应用 ZIP 和同名 `.sha256`。

在下载目录打开终端，使用实际文件名校验：

```bash
shasum -a 256 -c AIBotBridge-0.1.0-local-candidate-macos-arm64.zip.sha256
```

输出 `OK` 后解压应用 ZIP，将 `AIBotBridge.app` 拖入“应用程序”，从 Finder 启动。程序是菜单栏应用，没有普通主窗口；从菜单打开镜像与设置。

候选仅使用 ad-hoc 临时签名，未使用 Developer ID，也未经 Apple 公证；签名完整性检查通过不代表 Gatekeeper 接受。若系统拦截，停止并记录提示，不关闭系统防护。分发与公证要求见 [Apple 官方说明](https://developer.apple.com/developer-id/)。

音乐功能启用后，按系统提示授权访问正在运行的 Music 或 Spotify；首次授权、拒绝后的恢复路径和实际音乐显示需要实机验证。配对信息保存在 Keychain；不要共享用户设置或授权数据。

## 小屏刷机与首次连接

按 [Mac 安装与 ESP8266 刷机指南](FLASH_MAC.zh.md)完成硬件核对、串口识别、编译上传和实体屏验证。刷写前退出菜单栏应用，上传成功后重新启动，使用“查看本机状态”和“查看设备信息…”确认连接，再恢复智能跟随与轮播。USB 基础使用无需先配 Wi-Fi；需要无线回退时再执行指南中的配网与设备计数验证。

## 从源码生成候选

在 Apple Silicon Mac 安装 Xcode Command Line Tools、Python 3 和 Git，获取源码后在仓库根目录运行：

```bash
bash scripts/package_macos.sh artifacts/my-mac-candidate
```

输出目录必须不存在。脚本执行测试、Release 编译、`.app` 组装、签名与架构检查、ZIP 解压和逐文件校验。输出保留在指定目录，不启动应用、不连接设备。

## 首次使用验收

- Finder 启动与正常退出、重新启动，菜单和镜像正常。
- 空白用户状态下显示原创默认桌宠，不要求先导入资源。
- 音乐权限、Music/Spotify 曲名、封面、进度及暂停状态正确。
- 实际串口识别、配对、USB/Wi-Fi 回退、资源同步和断线恢复。
- 睡眠唤醒、设置保留和持续运行；记录 Mac 型号、系统版本及候选 SHA-256。

## English

See the [Mac flashing and first-connection guide (Chinese)](FLASH_MAC.zh.md) for serial-port discovery, PlatformIO upload, physical-display checks and optional Wi-Fi fallback.

This test build targets macOS 13+ on Apple Silicon only; Intel is unverified. Commit `f7c2d31` passed 31 tests, Release compilation, app packaging, signature integrity and extracted-ZIP checks. It includes an ad-hoc signed app, notices and hashes; it is not Developer ID signed or notarized. Build checks do not prove Gatekeeper acceptance, first launch, Automation permissions or device behavior. Verify the ZIP's `.sha256`, extract it and copy the app to Applications. If blocked, record the system message without disabling protection. The menu-bar app requires separate real-Mac acceptance before publication.
