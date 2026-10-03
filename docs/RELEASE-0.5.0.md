# v0.5.0 正式发布记录 / Release record

2026-10-03，维护者明确确认已完成真机验收，并要求正式发布。目标为 `yaoyouzhong/AI-bot` 的 `v0.5.0`，标记为正式版及 Latest。[发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.5.0)提供当前下载与状态。

Windows / macOS / ESP8266 包的源码为 `bdec5df33e66ba0a1e2f6886d6612c9c7bdd77b2`。已发布的附注标签保持不变；首页、指南和源码入口更新在 `main`，安装包与固件字节不变。包内指南为打包时快照，后续补充以[当前在线指南](INSTALL.zh.md)为准。

The maintainer confirmed hardware acceptance on 2026-10-03 and authorized the stable Latest release. Published binaries and their annotated source tag retain their original identity. Current documentation is maintained on `main`.

## 范围 / Scope

Windows 设备中心与双硬件管理、TAB5 传输与语音恢复、额度历史累计修复；同步 macOS 包版本，更新两种硬件刷机指南、原生截图及 54 秒产品介绍。Windows / ESP8266 为 0.5.0，TAB5 独立版本为 0.2.89-ui。

Windows Device Center and dual-device management, TAB5 transport and voice recovery, and cumulative quota-history fixes. macOS package version is synchronized. Installation guides, native captures and the 54-second introduction cover both hardware models.

## 下载 / Downloads

保留 **6 个上传附件**：五个用户包与一份校验清单。GitHub 自动生成的 Source code ZIP / tar.gz 另行显示。许可与第三方声明已包含在包内；重复校验文件、构建记录、文档、原始 BIN 和宣传素材不再占用下载列表。

| 文件 | 用途 |
| --- | --- |
| `AIBotBridge-0.5.0-setup-win-x64.exe` | Windows 10/11 x64，内置刷机工具 |
| `AIBotBridge-0.5.0-local-candidate-macos-arm64.zip` | macOS 13+ Apple Silicon；保留原文件名与字节 |
| `AI-bot-0.5.0-firmware-materials.zip` | ESP8266 固件及对应源码、重建材料 |
| `TAB5-first-install-0.2.89-ui.zip` | 出厂 TAB5 首次安装，完整 P4 镜像及清单 |
| `TAB5-upgrade-0.2.89-ui.zip` | 已有 AI-bot 的 TAB5，解压选应用 BIN |
| `SHA256SUMS.txt` | 上述五个文件的校验值 |

TAB5 首刷也为 0.2.89-ui，使用完整首刷 ZIP；单独应用 BIN 不能初始化出厂设备。Mac 包的历史文件名不改变本次正式发布状态。ESP8266 保留对应源码材料，不单独发布裸固件。

The six uploaded files contain five user packages plus checksums. The legacy Mac filename does not change the stable release status. Both TAB5 packages use 0.2.89-ui; factory installation requires the full ZIP.

TAB5 应用基线 `dbef9dc0bddb1b28ad444ca22ad11f5c998e5b1a`，许可补充后源码 `2c45bbbc9e5aef1c4e57576096bd15fd4a304d92`；应用 SHA-256 为 `32ed1549f91f71d7f294cb1a47d1c06e80de19994b262d02106fdca8168c358a`。[开发者快照](development/TAB5-SOURCE.md)保留 608 个受 Git 管理的文件、实际组件许可、依赖锁定、sdkconfig 与 ESP-IDF 补丁；不含下载的组件树、工具缓存，不声明已独立复现相同二进制。

宣传视频与封面保留在仓库，由 README 提供入口，见[视频说明](PRODUCT_VIDEO.md)。自有代码含 TAB5 统一 MIT，第三方原条款保留，见[许可范围](TAB5-LICENSE-SCOPE.md)。

TAB5's public developer snapshot retains tracked source, licenses and locked build configuration, with managed dependencies fetched from official sources. No independently reproduced binary is claimed. The film is linked from README. Own code is MIT licensed; third-party terms remain intact.

## 验证 / Validation

- 维护者于 2026-10-03 确认已完成真机验收，作为正式发布依据；不补造逐项测试日志或新增测速数据。
- [CI 37091073366](https://github.com/yaoyouzhong/AI-bot/actions/runs/37091073366)与[发布流水线 37091169252](https://github.com/yaoyouzhong/AI-bot/actions/runs/37091169252)通过：Windows Release 构建及公开回归、macOS 36 项测试与 Release 构建、ESP8266 PlatformIO 构建。
- Windows 安装包与解压回归、许可和分发材料检查通过；TAB5 首刷包通过 126 项模拟校验，应用字节与固定升级 BIN 一致。模拟校验与用户确认的真机验收分别记录。
- 原 27 个远端文件已与本地 SHA-256 和大小比对；精简后五个包字节不变，校验清单仅列保留的五个包。
- 截图为原生 WinForms / LVGL 与隔离演示数据，非硬件实拍，见[来源](SCREENSHOTS.md)。视频帧与媒体结构已检查，没有新增主观听感确认。

Hardware acceptance is maintainer-confirmed. Automated checks and simulated installation checks are recorded separately. Asset cleanup preserves package bytes and narrows the checksum list to current downloads.

## 平台与历史边界 / Platform and historical scope

Windows 提供两种硬件的安装流程。Mac 为 Apple Silicon、临时签名且未公证；刷机面向 ESP8266，不声明 Windows TAB5 功能在 Mac 已实现，也未新增 Mac 交互或 Intel 验证结论。

历史 USB 偶发中断及升级中断未新增定位或修复证据；逐项开发结果保留在[2026-10-02 验收记录](ACCEPTANCE-2026-10-02.md)。正式发布不改写历史结果或新增长期稳定性、全部网络组合及其他 DPI 的测量结论。

Mac retains its signing and platform capability limits. Historical intermittent USB interruptions are not newly claimed fixed. Earlier individual results remain dated records.
