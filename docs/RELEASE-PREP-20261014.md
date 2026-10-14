# 2026-10-14 发布准备 / Release preparation

本轮准备 Windows 桥接 **0.6.6**、TAB5 **0.2.194-ui** 及独立名画布局包 **2026.10.14.1**。版本沿用已核验候选，组件分别编号。此页为本地交付准备，不代表已推送、打标签、远端 CI 或公开发布；旧 .179 组合说明及下载目录保留为对应阶段记录。

## 源码与交付范围

| 对象 | 提交或身份 | 范围 |
| --- | --- | --- |
| 桥接修复 | `18220c3df135f325896f2c80508c68ce59a4414f`，分支 `codex/tab5-194-release-prep` | 以 `48afbb46` 为已部署组合基线，仅增加有界小屏心跳、BLE 精确响应恢复及名画布局工具；不取主目录其他未提交文件 |
| TAB5 源码 | `69cf5de8830059690f2ff336fdeb1323276fe43a`，独立工程同名分支 | 191–194 的 TF 解码／离线时钟、监控重绘、题注稳定性、总览启动和 BLE 暂停续期；同时修复构建入口短路径编译器参数冲突 |
| TAB5 应用 | SHA-256 `811c38d5506c0092bcf67a8c971fe03115f15c1901de0a588f211657a2096f29` | 已验收镜像，首刷和升级绑定同一应用；新入口复核构建后哈希保持一致 |
| 已验收 Windows DLL | SHA-256 `f6a99f8897d0996d487cb58a46104d074f52971dacd07e489eb363e32a8c7baf` | 发布候选复用这份已部署文件，安装器也由同一完整 stage 生成 |
| 名画独立包 | 2026.10.14.1；SHA-256 `a774f3717f83b685c8d9b9c1dc4f8ecd22b0a95a3abb80f882eb21726b29fc39` | 原 366 件作品／分页不变，413 张横屏显示图更新，413 张竖屏图原字节保留；书法不变 |

桥接 main 与候选分支并未合并：main 留有其他工作的修改，`Tab5Service.cs` 也与本轮已验收源码不同。后续推送／CI 必须选择上述候选分支的最终文档提交，不能把 main 的混合工作目录直接打包。

## 已完成检查

- Windows Release 构建 0 警告／0 错误；`--status-once` 返回有效状态；独立测试资料运行公开回归通过。
- TAB5 原构建入口完成生产构建，输出仍为上述应用哈希；无需重新刷机。JPEG SDK 补丁 8 项检查及相关原生 RPC、暂停续期、启动、RTC、解码、图库工作线程检查通过。
- 版本登记、中英文更新日志、发布说明提取、公开内容检查及许可材料检查通过。旧 186–190 日志已恢复，历史“未部署／待复测”不再冒充最终状态。
- TAB5 首刷 ZIP、应用升级 ZIP、源码／SDK 差异快照已生成，均绑定同一 194 应用。首刷包的 NVS 保持擦除态，不读取设备备份；源码快照不包含凭据，不声明独立环境逐字节重建。
- 本地安装、启动、六次 BLE 完整传输和自动展示恢复见 [194 验收收尾](development/TAB5-194-ACCEPTANCE-20261014.md)。

## 发布边界与后续验收

- 本轮没有修改 Mac 源码或构建 Mac 包，也不重建／重编号 ESP8266 固件和书法包。桥接正式发布前须获得最终候选提交的适用平台 CI 结果；Windows 结果不代表 macOS 已验证。
- BLE 速度改善有限；整包 BLE 升级、持续稳定性和提速目标尚未验收。多页作品翻页按用户要求后续验证；旧版通过记录仍保留。
- 首刷包已做本地结构、空白分区、应用身份及校验；本轮没有对现用设备执行首刷或重置配对。
- 后续发布的对象应是本轮明确列出的文件、最终提交和校验清单。候选标签格式为 `bridge-v0.6.6`、`tab5-v0.2.194-ui`，此处不表示标签已创建。公开附件下载和更新入口验证属于实际发布阶段。

本机交付目录：`artifacts/development/tab5-190-acceptance-20261014/release-prep-194/`。`release-payload.json` 列出最终文件 SHA-256、源码提交与准备检查；`SHA256SUMS.txt` 汇总待交付文件。原始状态、配对资料、用户配置及备份仅留本机，不打包。

## English

This local preparation targets Windows bridge 0.6.6, TAB5 0.2.194-ui and the independent painting layout pack 2026.10.14.1. The bridge candidate uses its isolated, committed branch rather than mixed main-directory edits. The TAB5 source commit is 69cf5de; its rechecked build retains the exact hardware-accepted application hash. Windows packages retain the installed f6 DLL, and TAB5 factory/upgrade/source packages bind the same 194 image with licenses and checksums.

Local Windows build, public regressions, version/content/material checks and TAB5 production/native checks passed. macOS/ESP8266/calligraphy are not rebuilt in this round. Applicable-platform CI, remote delivery and public-download verification remain release-stage checks. Whole-image BLE OTA, sustained performance/stability and physical multi-page navigation remain open; no new hardware write is performed for preparation. See the acceptance record and local payload manifest for the exact evidence and file identities.
