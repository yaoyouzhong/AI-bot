> 已被 0.6.0 标准版与独立图库方案替代。当前范围及状态见 [发布说明](RELEASE-0.6.0.md)。
> Superseded by the unified standard edition and optional collection packs; historical details below.

# Windows 0.5.1 / TAB5 0.2.143-ui 发布候选

2026-10-07。仅准备本地候选；未推送、打标签或公开发布。当前公开稳定版仍为 v0.5.0。旧 `.133` 及 `artifacts/development/tab5-release-132/` 中的 Windows 包已被本轮候选替代，不能用于本次发布。

## 用户如何选择

| 用户场景 | 下载和使用方式 |
| --- | --- |
| Windows 电脑 | 安装 AI-bot 0.5.1；同时支持两种设备，不需要分别安装桥接程序 |
| ESP8266 小屏 | 固件仍为 0.5.0，无需因电脑软件更新而重刷 |
| 新 TAB5 | 使用 TAB5 0.2.143-ui 首刷 ZIP |
| 已有 TAB5 | 从「软件与固件更新」下载对应升级 ZIP，在设备端确认安装 |

三个组件独立编号。日常更新统一从 Windows「软件与固件更新」进入：查看当前/可用版本、说明和匹配的下载，校验后交给已有安装工具。可开启自动检查和提醒，不会自动安装。详见 [更新指南](UPDATES.md)。正式发布前保留 v0.5.0 稳定下载链接。

## 交付内容

- Windows 0.5.1：统一更新入口、Codex 草稿与语音恢复、OTA 和精确镜像核验。公开版不编译 DailyArt 实现、不提供图库接口、不附带作品集。
- TAB5 0.2.143-ui：保留十二月历花卉、年度昼夜配色等六种公开屏保；不包含每日名画/书法或其下载、方向任务。首刷与升级包具有相同应用镜像，但用途不同。
- 字库无损压缩保留全部字符、尺寸和像素。公开镜像 6,512,864 字节，现有 7,208,960 字节 OTA 槽位剩余 696,096 字节；不改分区或用户配置。
- 813 件本地作品及 3,804 张图片保留，不发布图库 ZIP。本地桥接仍在固定路径运行，统一更新禁止公开包覆盖艺术完整版。本地 `.142` 为独立压缩验收候选，固定 OTA latest 保持本地版本；详见 [验收记录](TAB5-ACCEPTANCE-142.md)。

## 验证与剩余条件

- `.141` 精确镜像启动和用户实屏通过：普通界面、输入、两类艺术屏保及自动四方向稳定。它保留原显示/图片/缓存逻辑，仅修复自动屏保期间的后台翻页事件；撤回后续诊断和缓存实验。字体压缩不能据此前失败版本认定为蓝闪根因。
- `.142` 只恢复两套压缩字体和配套缓存/生成选项：16,132 个字形存在性、度量和像素一致；23,990 项快速绘制核对通过；界面预览逐像素保持 `.141`；空间剩余 682,912 字节。精确镜像启动和用户实屏均通过：没有蓝闪，文字、图片和转向正常，原自动轮播已恢复。
- Windows Release 构建、同一 Release DLL 的 `--status-once`、公开完整自测、分发材料及打包边界检查通过。隐藏宿主中直接 `dotnet run` 受 WinExe 控制台限制，未削弱启动保护；改用用户桌面环境运行同一 DLL 验证。
- 更新服务故障、下载/交接窗口、下载期间设备移除，以及真实公开 `.89` OTA 下载/哈希/镜像/sidecar 校验通过；没有安装旧镜像。自动模式、原六页顺序及 15 秒间隔保留。
- 新包集中在 `artifacts/development/release-closeout-143/`；原生预览、字体和镜像证据在 `artifacts/development/gallery-preview-sync/`。每个包保留许可证、SHA-256 和源码身份；最终包清单与状态见交付目录 `CLOSEOUT.json`。
- **公开 `.143` 仍需实机验收。** 本地 `.142` 不能代替公开镜像六屏保、旧配置回退和首次安装验证；不为候选打包而覆盖现用艺术完整版。
- **macOS 0.5.1 尚未构建和验收，统一更新入口目前仅实现于 Windows。** 当前 Windows 主机没有可用 Mac 构建环境；完整 bridge 发布材料验证仍要求真实 macOS 包，保留此门槛。ESP8266 没有源码变更，不宣称新一轮硬件验收。
- 发布前需上述实机和 macOS 条件满足，再针对具体 commit、目标标签和候选文件获得公开发布授权。

## English

Local candidates: Windows bridge 0.5.1 and public TAB5 0.2.143-ui; ESP8266 stays at 0.5.0. No push, tags or public release. Published v0.5.0 links remain valid until a new release exists. Old .133 and Windows packages under tab5-release-132 are superseded.

Install one Windows bridge for either device. Component versions are independent; use Software and Firmware Updates for discovery, optional notifications, verified downloads and installation handoff. TAB5 first-install and OTA packages serve different purposes but contain the same application. Public builds retain six screensavers including the floral calendar and exclude daily art and its collections. The 813-work local collection remains private; local editions reject replacement by public updates.

Public .143 is 6,512,864 bytes with 696,096 bytes free in the unchanged OTA partition. Stable .141 passed exact-image and physical acceptance. Local .142 independently restores lossless fonts and their cache, with 16,132 glyph comparisons, 23,990 drawing cases and unchanged preview pixels; exact-image boot and user physical acceptance pass with no blue flashes and normal text, pictures and orientation. Original cycling is restored. Previous failures do not establish font compression as the cause of blue flashing.

Windows Release build, status-once using the same DLL in the desktop console host, full public regression, packaging boundaries and distribution materials pass. Existing update/download/device-removal and real public .89 package validation evidence remains applicable. Deliverables are under release-closeout-143 with hashes, licenses and source identity; CLOSEOUT.json records final outcomes. Public .143 physical acceptance and real macOS build/acceptance remain outstanding; the complete bridge release gate still requires the macOS package. Publication requires authorization for the concrete payload after these conditions are met.
