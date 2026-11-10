# 桥接与固件独立版本 / Independent component versions

Windows / Mac 协作、候选构建物与发布职责见[统一开发发布流程](DESKTOP-DEVELOPMENT-RELEASE.md)；本页维护版本规则，发布收尾继续使用既有清单。

桥接、ESP8266 和 TAB5 分别更新，不要求三个版本号相同。只修改一个组件时，不重编号、重发或要求用户重刷另外两个组件。历史 `v0.5.0` 标签与已发布文件保持不变。

2026-11-03 当前发布：Windows／Mac **0.6.16**、ESP8266 **0.5.8**、TAB5 **0.2.303-ui**；图库沿用原正式版本。四个交付项按三个组件 Release 登记，见[本轮说明及验收范围](RELEASE-0.6.16.md)。

本次 `desktopPlatform=all`，Windows 与 Mac 使用同一候选源码提交及共同桥接版本。Both desktop platforms use 0.6.16 from the same source candidate; ESP8266 0.5.8 and TAB5 0.2.303-ui remain independently versioned.

## 版本来源

| 组件 | 当前版本 | 版本文件 | 正式组件标签 |
| --- | --- | --- | --- |
| Windows 桥接 | 0.6.16 | 根目录 `VERSION` | `v0.6.16` |
| Mac 桥接 | 0.6.16 | 根目录 `VERSION` | `v0.6.16` |
| ESP8266 固件 | 0.5.8 | `esp8266/VERSION` | `esp8266-v0.5.8` |
| TAB5 固件 | 0.2.303-ui | `versions/TAB5` | `tab5-v0.2.303-ui` |

表中登记各平台实际已发布版本；后续版本仍分别编号。`release-manifest.json` 登记每个组件的版本文件、标签前缀与中英文更新日志。桥接源码元数据共用版本线，正式下载按平台实际存在的附件分别取版本；本轮 Windows 与 Mac 同时交付 0.6.16。

电脑端每次发布占用共用序列中的下一个版本号。例如 Mac 单独发布 0.6.15 时 Windows 仍为 0.6.14；下一次仅 Windows 补丁使用 0.6.16，Mac 仍为 0.6.15；若两端一起交付，则同时使用该次版本。Windows 项目及 Mac plist 的源码版本声明随根目录 `VERSION` 对齐，不表示未交付的平台已有新安装包。Each desktop release advances the shared sequence, but only shipped platforms adopt that release version. A Mac-only 0.6.15 can coexist with Windows 0.6.14; a later Windows-only patch would be 0.6.16 while Mac retains 0.6.15. Shared source metadata is not a claim that an unbuilt platform was released.

上一轮 2026-10-16 的范围为桥接 0.6.7、TAB5 0.2.205-ui、ESP8266 0.5.3、标准书法 2026.10.15.1 和两套高清完整图库 2026.10.16。标准名画沿用已有正式下载；不集成浏览器高清音乐封面扩展。构建、打包、公开附件和真机验收分别记录，公开下载索引仅在附件实际发布后刷新。The October 16 delivery covers bridge 0.6.7, TAB5 0.2.205-ui, ESP8266 0.5.3, standard calligraphy 2026.10.15.1 and both optional complete HD collections 2026.10.16. Existing standard painting remains unchanged; the browser music-artwork extension is not integrated. See [scope and acceptance boundaries](RELEASE-0.6.7.md).

2026-10-15 的 Windows 0.6.7／TAB5 0.2.196-ui 为日程与图库同步本地验收候选；后续 TAB5 0.2.197-ui 仅作字体索引、图标和词库的无损减包，继续配套同一 Windows 候选。构建、打包和真机验收分别记录；Mac 仅同步版本元数据，本次不提供新的 Mac 构建。公开下载索引保持正式版，不把本地包标作已发布。The October 15 Windows 0.6.7/TAB5 0.2.196-ui pair is a local agenda/gallery acceptance candidate; TAB5 0.2.197-ui adds lossless font-map, icon and dictionary compaction with the same Windows candidate. Mac metadata is aligned without a new Mac build, and published download entries remain unchanged.

`versions/TAB5` 登记待发布镜像版本，不替代独立 TAB5 工程的镜像内版本；发布前核对其应用、sidecar、首刷包与公开源码身份。TAB5 本地候选仍由[固定固件工作流](TAB5-FIRMWARE-WORKFLOW.md)管理。

此前桥接 `0.6.6` 与 TAB5 `0.2.194-ui` 的实际交付保留在[历史说明](RELEASE-0.6.6.md)。本轮范围及验收见 [0.6.16 说明](RELEASE-0.6.16.md)，此前结果见 [Mac 0.6.15](RELEASE-0.6.15.md) 与 [0.6.14 双平台桥接说明](RELEASE-0.6.14.md)，上轮见 [0.6.11／281 说明](RELEASE-0.6.11.md)，此前结果保留在 [0.6.7／205 历史说明](RELEASE-0.6.7.md)。字库随 TAB5 固件安装，年度精选名画与书法各 366 件以独立图文包提供，旧图库保持兼容。The three independently versioned components and optional collections retain separate verification and acceptance records.

桥接与历史组合版的发布展示标题统一使用 `AI-bot v版本号`，例如 `AI-bot v0.6.7`；功能说明放在正文。ESP8266 和 TAB5 保留各自命名方式，固件版本值和新包名不加 `v`。电脑端 Git 发布标签统一为 `vX.Y.Z`；固件保留组件前缀。后续构建物和旧客户端兼容规则见[命名规范](NAMING-CONVENTIONS.md)。

## 构建与检查

仅构建桥接：

```powershell
python scripts/check_version.py --component bridge
powershell -NoProfile -File scripts/package_windows_local.ps1 -OutputDirectory artifacts/bridge-package
```

Mac 桥接需在 macOS 上运行 `bash scripts/package_macos.sh artifacts/bridge-macos`，不构建设备固件。

仅构建 ESP8266，不构建 Windows 或 Mac：

```powershell
python scripts/check_version.py --component esp8266
python scripts/package_esp8266_local.py artifacts/esp8266-package
```

所有输出路径须为新目录。ESP8266 ZIP 的文件名、包内 `VERSION` 和 `COMPONENT.json` 均取 ESP8266 版本，包含对应源码和重建材料。桥接脚本的可选 `-Firmware` 仍可组合打包，但按各组件实际版本命名。

标签检查按组件路由：

```powershell
python scripts/check_version.py --tag v0.6.10
python scripts/check_version.py --tag esp8266-v0.5.1
python scripts/check_version.py --tag tab5-v0.2.151-ui
```

上述命令只检查格式与本地声明，不创建标签。0.5.0 及更早的 `vX.Y.Z` 保留为桥接主导的组合版本；0.5.1 起的 `vX.Y.Z` 只路由电脑端，不能触发固件打包。旧 `bridge-vX.Y.Z` 仍可读取为兼容别名。包内各组件仍用独立版本。`extract_release_notes.py` 从对应组件的中英文日志选取说明；未完成的日志继续阻止正式打标签。

`prepare_release_assets.py --component bridge|esp8266|tab5` 分别验证对应包，不要求另一组件同时交付。新流程用 `release_pipeline.py assemble/seal` 冻结后统一晋级：桥接为 Windows 安装器、Mac ZIP；ESP8266 为固件材料 ZIP；TAB5 为首刷 ZIP、升级 ZIP 和对应源码。每个 Release 附许可证、第三方声明、候选与验收记录及校验清单，并保留旧客户端需要的兼容别名。便携包和完整桥接源码保留在候选归档；可选名画与书法包分别按下载索引中的正式版本提供。三条组件版本线对应 Windows、Mac、ESP8266、TAB5 四项交付，不把两端桥接合计为一个安装包。

TAB5 验证同时检查镜像内版本、说明 sidecar、首刷与升级应用字节及对应公开源码快照，不能只改文件名作为新固件。

## 更新判断与兼容性

Windows “检查更新”按正式发布中的组件标签及实际附件分别选择版本；忽略草稿、预发布、标签与包名不匹配的记录，也继续识别旧组合 Release。固件单独发布不会被当成电脑桥接更新，旧固件版本不从桥接版本号推断。

Windows 0.6.2 的[统一更新入口](UPDATES.md)还提供每日检查、同版本提醒去重、适用包下载及 SHA-256 校验。选择电脑端打开安装器，ESP8266 交给先备份再写入的 USB 工具，TAB5 交给既有设备确认及启动核验流程。下载不会自动刷写；本地与公开版功能统一，图库位于独立用户数据目录，升级保留图库。固件 ZIP 可声明 `minimumBridgeVersion` 和 `protocolVersion`（TAB5 在 `EDITION.json`，ESP8266 在 `COMPONENT.json`），不兼容时先升级电脑端。

协议 `version=1` 是协议标识，不是软件或固件版本。组件之间不按版本数字相等判断兼容；涉及协议或必要功能变化时，在对应更新说明中写明最低支持版本及是否需要组合升级。普通用户按相应更新说明升级即可，无需每次一起刷机。

## 文档与媒体同步

用户下载入口为根目录 [DOWNLOADS.md](../DOWNLOADS.md) / [DOWNLOADS.en.md](../DOWNLOADS.en.md)。页面只列 Windows、Mac 安装包、ESP8266 精简刷机包及 TAB5 首刷／升级包；小屏新版发布齐备精简包和对应校验文件后才进入下载中心。[组件源码及构建材料](COMPONENTS.md)和[可选图库](GALLERY-PACKS.md)分别提供。内部索引 `download-catalog.json` 仍登记已公开的书法和名画包，用于下载核对。

维护流程：

```powershell
# 只读访问公开 Releases，验证下载 URL、附件大小、摘要与 SHA256SUMS.txt 后更新本地索引
python scripts/download_catalog.py refresh
# 生成中英文下载中心和 README 下载块
python scripts/download_catalog.py sync
# 离线检查生成内容是否过期；发布前另做在线核对
python scripts/download_catalog.py check
python scripts/download_catalog.py verify-online
python scripts/test_download_catalog.py
```

`extract_release_notes.py` 在组件更新说明前添加统一下载入口。组件发布后重新 `refresh`、`sync` 并审查索引变动，使下载中心指向最新的正式安装包；原有打包检查继续验证本次组件的附件，下载索引检查不代替安装或真机验收。上述命令只读取网络、修改本地文件，不推送或发布。

`verify_release_local.ps1` 检查下载中心/README 是否与索引一致并运行边界测试。生成文档默认留在本地，已有推送授权时可用 `--commit-closeout` 只提交指定生成文件；网站上的内容以实际推送及 Release 说明更新为准。

每次正式发布后核对并同步受影响的主页介绍、安装手册与说明、下载入口，逐项记录[发布收尾清单](RELEASE-CHECKLIST.md)的结果。小更新复用仍准确的截图和视频；只有会误导用户的部分才替换，登记新素材来源及 SHA-256。区分当前说明与历史验收记录。

## 自动发布衔接

当前入口见[发布自动化](RELEASE-AUTOMATION.md)：`candidate.yml` 显式构建所选桥接、ESP8266 或组合候选并冻结清单；`release.yml` 对历史与组件标签只检查版本及更新说明，不再构建。`promote.yml` 获取明确成功批次的已验收原包，默认只生成草稿，显式选择公开才发布。TAB5 保留独立工程本地构建与镜像核验，通过本地 `release_pipeline.py` 晋级。推送 `main` 只跑按路径选择的 CI，不自动公开版本；发布负责人已获本次发布授权时连续执行，无需逐阶段重复确认。旧提案补丁仅保留历史参考，不作为当前工作流安装入口。

本地调用 GitHub CLI 使用 `python scripts/github_cli.py ...` 或其 `run_cli` 入口。Windows 直接解析真实控制台版 `gh.exe`，跳过 PATH 中的 GUI 包装器，并结合 `CREATE_NO_WINDOW`、隐藏窗口及标准输入/输出/错误重定向；失败保留退出码与错误，不打开交互终端。创建标签、push、刷机或公开新版本仍须对应操作的明确授权。

## English

Bridge and legacy bundle release titles use `AI-bot v<version>`, for example `AI-bot v0.6.7`, with feature descriptions in release notes. ESP8266 and TAB5 retain their existing title conventions; firmware version values and canonical filenames omit `v`. Keep existing Git tag prefixes. See [artifact naming and legacy-client compatibility](NAMING-CONVENTIONS.md).

The fixed entry points are DOWNLOADS.md and DOWNLOADS.en.md. download-catalog.json records seven published stable downloads separately from candidate versions. `download_catalog.py refresh`, `sync`, `check` and `verify-online` verify published assets and generate bilingual download centers and README tables. These individual catalog commands never push or publish. The promotion wrapper runs download closeout after publication, updates the Release download table, and can explicitly commit only generated metadata within an existing push authorization. Draft links become downloadable only after publication. Packaging, installation and hardware acceptance remain separate evidence.

Every public release must complete the [release closeout checklist](RELEASE-CHECKLIST.md): synchronize affected descriptions, installation instructions and download links. Minor changes may reuse accurate screenshots and video; update only misleading portions, retain provenance and record replacement hashes. Preserve dated acceptance evidence.

Windows 0.6.2 retains daily update checks, deduplicated notifications, automatic selection/download and SHA-256 verification. Installation remains explicit through the existing Windows installer, ESP8266 backup/USB tool or TAB5 confirmation/boot-verification flow. Local/public functionality is unified; upgrades preserve separately installed artwork collections. Firmware metadata may declare minimumBridgeVersion and protocolVersion; see UPDATES.md.

The bridge, ESP8266 and TAB5 have independent version sources and changelogs. Four deliverables use three version lines: Windows and macOS share bridge source metadata but published downloads are selected per platform. Only changed components are renumbered and delivered. TAB5 declarations must match actual image, notes, first-install application and corresponding public source. Protocol version 1 is independent of release numbering; compatibility follows supported features. Legacy tags and assets remain unchanged. Candidate workflows build selected native packages and freeze their identities. Tag events only validate metadata; explicit promotion checks hash-bound acceptance and publishes the original bytes without rebuilding. TAB5 retains independent local build/material validation. Main pushes run scoped CI only. Local CLI calls use scripts/github_cli.py for the real hidden console executable. Publishing and pushing require applicable authorization; a user's explicit release instruction covers the necessary stages within that release scope without repeated approvals.

Windows 0.6.8 and TAB5 0.2.210-ui are published; Mac retains 0.6.7. See [release scope and acceptance](RELEASE-0.6.8.md).

上一轮已发布 Windows 0.6.10 与 TAB5 0.2.234-ui；版本文件、固定镜像及公开附件一致。Mac/ESP8266 沿用原包；最终验收见 [收尾记录](development/RELEASE-0610-229-CLOSEOUT-20261019.md)。
