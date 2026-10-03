# v0.5.0 预发布记录 / Pre-release record

日期：2026-10-03。目标：`yaoyouzhong/AI-bot`，分支 `main`，附注标签 `v0.5.0`，GitHub Pre-release；不替换 v0.4.0 latest 稳定版。[发布页](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.5.0)为附件和公开状态的依据。

功能基线源码：`d0cb8dc1cd1cf4a277687247c337b4ac4b5489d4`，另加本轮测试、版本与发布文档修改。最终源码以 `v0.5.0` 标签及附件 `BUILD.json` 的完整 `sourceCommit` 为准。Windows、Mac、ESP8266 和本仓库源码包由该标签的发布流水线生成；TAB5 使用下述独立基线。

## 范围 / Scope

Windows 设备中心、TAB5 集成与额度累计修复；同步 macOS 包版本。更新两种硬件的安装说明、原生截图和 54 秒完整产品介绍。TAB5 0.2.89-ui 为独立固件基线，标准固件材料包仅针对 ESP8266；TAB5 首刷完整 ZIP 和升级 BIN/说明另行准备并校验，不自动把相邻仓库纳入源码包。

Windows Device Center, TAB5 integration and quota accumulation fixes; synchronized macOS bundle version. Updated two-device installation instructions, native screenshots and a rebuilt 54-second product overview. TAB5 0.2.89-ui is packaged separately for factory installation and application upgrades; standard firmware materials cover ESP8266 only.

## 发布附件 / Release payload

本地更新候选目录：`artifacts/release-0.5.0-r2/`，用于发布前校验；公开的标准应用、ESP8266 及源码包使用标签流水线重新构建的附件，不混用早期包。独立 TAB5 包补入许可范围、当前安装指南及重新计算的校验清单，应用镜像保持 0.2.89-ui 的已核验字节。

| 附件 | 适用范围 |
| --- | --- |
| `AIBotBridge-0.5.0-setup-win-x64.exe` / `AIBotBridge-0.5.0-local-candidate-win-x64.zip` | Windows 安装器 / 便携候选，内含新安装说明和两种刷机截图 |
| `AIBotBridge-0.5.0-local-candidate-macos-arm64.zip` | macOS 13+ Apple Silicon，临时签名、未公证，交互验收待完成 |
| `AI-bot-0.5.0-firmware-materials.zip` | 仅 ESP8266，固件与对应重建材料 |
| `TAB5-first-install-0.2.89-ui.zip` | TAB5 出厂设备首刷：完整 16 MiB P4 镜像及清单 |
| `TAB5-upgrade-0.2.89-ui.zip` | 已有 AI-bot 的 TAB5：解压后选 `aibot_tab5.bin`，保留同目录 notes sidecar |
| `TAB5-firmware-materials-0.2.89-ui.zip` | 独立 TAB5 源码快照、实际组件许可与构建材料，不属于本仓库源码 ZIP |
| `AI-bot-0.5.0-source.zip` | 本仓库源码及许可，不含相邻 TAB5 工程 |
| `AI-bot-product-intro-0.5.0.mp4` | 中文 54 秒完整介绍，1920×1080；视频工程单独在本地交付 |
| `SHA256SUMS.txt` / `RELEASE-NOTES.md` | 校验清单与预发布说明 |

TAB5 应用基线为独立工程 commit `dbef9dc0bddb1b28ad444ca22ad11f5c998e5b1a`；应用 SHA-256 为 `32ed1549f91f71d7f294cb1a47d1c06e80de19994b262d02106fdca8168c358a`。源码快照、组件许可与构建配置随独立材料包提供；未声明已完成独立环境的可复现重建。合并项目自有代码（含该 TAB5 基线）统一按 MIT 开源，包内补入明确许可；第三方条款保留，详见[TAB5 许可范围](TAB5-LICENSE-SCOPE.md)。新版 MP4 作为 Release 附件提供；旧 36 秒附件不代表新版。

## 验证 / Validation

- Windows 隔离 Release 构建及完整公开自测通过，含额度历史与 DeepSeek 同账户恢复/换账户/换币种检查。
- 首次全量测试发现旧断言错误地要求同账户余额恢复清空网页消费；已改为验证保留数值和原时间戳，并接入已有账户/币种隔离回归，未改产品逻辑。
- 分发材料测试补齐安装说明和截图夹具并检查打包内容一致，11 项通过；打包边界 4 项通过。
- ESP8266 PlatformIO 构建通过，RAM 56.2%、Flash 51.2%；有既有 snprintf 范围及工具链转义警告，不声明零警告。
- 标准 `dotnet run ... --status-once` 仍触发 GUI 宿主控制台句柄异常；同一隔离 Release DLL 直接运行 `--status-once` 退出码 0，状态 JSON 保存在本地证据目录，不对外分发。
- TAB5 完整首刷包通过 126 项模拟校验；其应用与固定升级 BIN 字节一致。模拟校验不访问硬件，不替代出厂首刷及恢复验收。
- 当前原生窗口和镜像以隔离配置、固定演示数据生成；TAB5 图片来自同日 LVGL 预览，详见[截图来源](SCREENSHOTS.md)。
- 视频全片 2 fps 联系表、全部转场 10 fps 条带、密集镜头全尺寸帧已审阅；音轨、时长和响度已测量，未完成实际听感确认，详见[视频说明](PRODUCT_VIDEO.md)。
- 更新候选执行记录和 SHA-256 放在 `artifacts/release-0.5.0-r2/`；安装包生成、解压完整性和解压后公开自测以该目录的验收回执为准。

Windows build/public regressions and ESP8266 build passed. Distribution fixtures were completed; no product behavior was changed. The standard GUI-host status command still fails on its console handle; direct execution of the same Release DLL succeeds. Final package verification is recorded beside the local artifacts.

NAudio 2.2.1 NuGet 包仅声明 MIT，未携带正文；打包现按精确包版本从仓库已有官方许可复制正文，并验证登记的 SHA-256。未知缺失许可或正文被改仍阻止打包。官方来源：https://github.com/naudio/NAudio/blob/b5d5ff83fd378f046398891fe5cd99426ce44732/license.txt

## 发布限制 / Release limitations

- 最新额度修复尚未部署，需候选安装/升级和真实使用确认；本次不停止现有桥接、不刷机。
- USB 偶发断连/升级中断未修复；保留已知问题，不主动恢复已暂停的调查。
- Mac 构建、测试及打包由 macOS 发布 CI 执行，通过后才公开附件；Windows 验证不能代替 Mac 交互及真机验收。
- TAB5 0.2.89-ui 已有升级后的启动记录；出厂首刷与原固件恢复整条真机链路尚未验收。
- 实体键盘、部分双设备/网络/异常恢复、其他 DPI/多显示器仍待验收。
- Published as a pre-release after explicit maintainer authorization and successful tag packaging. macOS tests/build/package run on macOS CI before publication; interactive Mac acceptance, Windows installation acceptance and the latest quota fix runtime acceptance remain pending. Intermittent USB interruptions remain unresolved. The merged project's own code, including TAB5, is MIT licensed; third-party components and marks retain their original terms.
