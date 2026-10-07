# 0.5.1 / TAB5 0.2.133-ui 发布候选

2026-10-07。仅准备本地候选，尚未推送、打标签或公开发布。当前公开稳定版仍为 v0.5.0。

## 交付范围

- Windows 桥接 0.5.1：Codex 直达草稿恢复、语音与传输改进；公开版不编译 DailyArt 实现，不提供名画或书法下载接口，不附带图库。
- TAB5 0.2.133-ui：公开版保留六种屏保，包括十二月历花卉及年度昼夜配色；去除每日名画、每日书法入口与运行任务。
- 中文字库改为 LVGL 无损压缩，保留全部字形、字号、像素和既有缓存。分区表不变。
- ESP8266 0.5.0 不变。本轮 Windows 候选不代表 macOS 0.5.1 已构建或验收。

## 本地完整功能

已验收的 TAB5 .131、运行中的本地完整桥接、813 件作品及 3,804 张图片保留。名画和书法仅供本地使用，不发布独立图库 ZIP。公开候选不覆盖当前本地安装。

本地完整桥接构建显式传入 `-p:LocalArt=true`。TAB5 构建显式使用 `scripts/build.ps1 -LocalArt -LocalVersion 0.2.132-ui`；公开构建默认关闭艺术屏保。本地优化实验 .132 与公开候选 .133 使用不同版本，避免混淆。

## 验证边界

- 完整本地字体优化镜像剩余 682,912 字节；公开镜像剩余 696,096 字节，原 .131 仅剩 2,624 字节。OTA 槽位均为 7,208,960 字节。
- 16,132 个字形逐像素、存在性和度量核对通过；本地界面截图与原预览像素一致。
- 公开固件构建、LVGL 界面回归、旧艺术屏保偏好回退通过；公开桥接构建及 RPC 测试确认两类图库接口拒绝请求且实现类型不存在。
- Windows 分发材料按实际 SDK 10.0.22000.56 核验；保留 Microsoft 原始许可并检查 DLL 身份。
- Windows 安装包已生成，独立空白配置与 ZIP 解压回归通过；未覆盖安装到现用桥接。`--status-once` 通过 `dotnet AIBotBridge.dll` 控制台宿主运行成功；隐藏启动环境中的 `dotnet run` 因 WinExe 无有效控制台句柄失败，未修改程序绕过。
- 本地完整桥接另行构建，813 件作品的全部横竖屏 JPEG、日期映射与分块校验通过；本地艺术屏保两类各 12 种替换布局回归通过。
- TAB5 首刷、OTA 升级和开发者源码包分别生成；首刷应用与 OTA 的版本及 SHA-256 必须一致，源码快照记录应用身份、源码 commit、SDK commit 和补丁。源码含默认关闭的本地选项，不含作品图库。
- 实际包、日志和校验清单位于 `artifacts/development/tab5-release-132/`；目录名沿用本地容量实验编号，公开固件包内版本为 .133。公开候选保存在该目录，不替换供个人设备使用的固定 latest .131。
- .132/.133 尚未刷入真机，字库解压的实际刷新表现、公开版完整交互仍待实机验收；.131 的验收不能代替新镜像验收。
- macOS 构建与验收尚未执行。正式发布仍需针对具体 commit 和文件另行授权。

## English

Local candidates only: Windows bridge 0.5.1 and public TAB5 0.2.133-ui; no push, tags or public release. ESP8266 stays at 0.5.0. Daily painting/calligraphy, their service and artwork are local-only; public builds exclude them. The floral monthly calendar remains included. Existing full local installations and galleries are retained. Opt-in local builds use `LocalArt=true` for the bridge or `-LocalArt -LocalVersion 0.2.132-ui` for TAB5.

Lossless Chinese font compression preserves all glyphs and the existing cache. Pixel/metric checks cover 16,132 glyphs, with unchanged local preview images. Free application space increases from 2,624 to 682,912 bytes in the full local experiment and 696,096 bytes in the public build without changing partitions. Public build/UI/preference/RPC checks pass. The Windows installer and extracted ZIP pass isolated-profile tests; the running bridge is not replaced. Status-once succeeds via the dotnet DLL console host; hidden `dotnet run` lacks a valid WinExe console handle. Local full-gallery tests cover all 813 works and both orientations. TAB5 first-install, OTA and source snapshots retain matching application identity, licenses and hashes; optional local source contains no artwork. Candidate artifacts remain under `artifacts/development/tab5-release-132/`, separate from the personal .131 latest package. New firmware hardware acceptance and macOS build/acceptance remain pending; neither is implied by .131 acceptance or compilation. Publication requires separate authorization for the concrete payload.
