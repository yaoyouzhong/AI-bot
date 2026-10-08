# 桥接与固件独立版本 / Independent component versions

桥接、ESP8266 和 TAB5 分别更新，不要求三个版本号相同。只修改一个组件时，不重编号、重发或要求用户重刷另外两个组件。历史 `v0.5.0` 标签与已发布文件保持不变。

## 版本来源

| 组件 | 当前版本 | 版本文件 | 后续组件标签 |
| --- | --- | --- | --- |
| Windows / macOS 桥接 | 0.6.1 | 根目录 `VERSION` | `bridge-v0.6.1` 等 |
| ESP8266 固件 | 0.5.0 | `firmware/VERSION` | `esp8266-v0.6.0` 等 |
| TAB5 固件 | 0.2.149-ui（已发布） | `versions/TAB5` | `tab5-v0.2.149-ui` 等 |

表中未来标签仅为格式示例，不代表已经发布或当前版本已经增加。`release-manifest.json` 登记每个组件的版本文件、标签前缀与中英文更新日志。桥接的 Windows 项目元数据与 Mac Info.plist 仍在同一桥接版本线内同步。

`versions/TAB5` 登记待发布镜像版本，不替代独立 TAB5 工程的镜像内版本；发布前核对其应用、sidecar、首刷包与公开源码身份。TAB5 本地候选仍由[固定固件工作流](TAB5-FIRMWARE-WORKFLOW.md)管理。

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
python scripts/check_version.py --tag bridge-v0.6.1
python scripts/check_version.py --tag esp8266-v0.5.0
python scripts/check_version.py --tag tab5-v0.2.149-ui
```

上述命令只检查格式与本地声明，不创建标签。历史 `vX.Y.Z` 保留为桥接主导的组合版本，包内各组件仍用独立版本。`extract_release_notes.py` 从对应组件的中英文日志选取说明；未完成的日志继续阻止正式打标签。

`prepare_release_assets.py --component bridge|esp8266|tab5` 分别验证对应包，不要求另一组件同时交付。`--publish-directory` 从验收目录生成最少用户附件和一份校验清单：桥接为 Windows 安装器、Mac ZIP、清单；ESP8266 为固件材料 ZIP、清单；TAB5 为首刷 ZIP、升级 ZIP、清单。源码和构建记录保留在验收资料或仓库开发入口；当前 TAB5 .149 正式发布另附对应源码 ZIP；可选图库继续使用 .145 发布页的原包。

TAB5 验证同时检查镜像内版本、说明 sidecar、首刷与升级应用字节及对应公开源码快照，不能只改文件名作为新固件。

## 更新判断与兼容性

Windows “检查更新”按正式发布中的组件标签及实际附件分别选择版本；忽略草稿、预发布、标签与包名不匹配的记录，也继续识别旧组合 Release。固件单独发布不会被当成电脑桥接更新，旧固件版本不从桥接版本号推断。

Windows 0.6.1 的[统一更新入口](UPDATES.md)还提供每日检查、同版本提醒去重、适用包下载及 SHA-256 校验。选择电脑端打开安装器，ESP8266 交给先备份再写入的 USB 工具，TAB5 交给既有设备确认及启动核验流程。下载不会自动刷写；本地与公开版功能统一，图库位于独立用户数据目录，升级保留图库。固件 ZIP 可声明 `minimumBridgeVersion` 和 `protocolVersion`（TAB5 在 `EDITION.json`，ESP8266 在 `COMPONENT.json`），不兼容时先升级电脑端。

协议 `version=1` 是协议标识，不是软件或固件版本。组件之间不按版本数字相等判断兼容；涉及协议或必要功能变化时，在对应更新说明中写明最低支持版本及是否需要组合升级。普通用户按相应更新说明升级即可，无需每次一起刷机。

## 文档与媒体同步

完整用户下载入口为根目录 [DOWNLOADS.md](../DOWNLOADS.md) / [DOWNLOADS.en.md](../DOWNLOADS.en.md)。`download-catalog.json` 只登记已公开稳定版本的七类下载（Windows、Mac、ESP8266 通用包、TAB5 首刷／升级、书法、名画），不把本地候选当成已发布。

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

`extract_release_notes.py` 自动在组件更新说明前添加完整下载表、固定入口和安装提示；本次组件使用目标标签的附件地址，其余沿用索引中的正式版本。草稿中的本次组件链接要到该 Release 公开且附件齐全后才可公开下载；原有打包检查继续验证本次组件的附件，下载索引检查不代替安装或真机验收。组件发布后重新 `refresh`、`sync` 并审查索引变动，使固定下载中心指向最新正式版本；旧 Release 内的固定入口仍可找到新版。上述命令只读取网络、修改本地文件，不推送或发布。

`verify_release_local.ps1` 检查下载中心/README 是否与索引一致并运行边界测试。现有 Release 工作流调用的说明提取器已接入完整下载表，无需改变工作流的权限、触发条件或草稿发布边界。网站上的内容仍须经明确授权提交/推送或更新 Release 说明后生效。

每次发布同时核对 README、中英文更新日志、安装/更新指南、功能图鉴、版本入口和下载链接，并区分当前说明与历史验收记录。截图仅在界面内容、显示版本或操作步骤变化时更新；未变化的截图和视频保留原捕获说明。替换截图后逐张复核并同步许可材料中的 SHA-256，避免文档、素材与发布附件不一致。

## 自动发布衔接

经用户授权应用的[工作流补丁](proposals/independent-release-workflows.patch)支持历史 `v*.*.*` 与三类组件标签。桥接和 ESP8266 按各自标签单独打包，只生成草稿，不自动公开发布；TAB5 标签检查版本与更新说明，保留独立工程本地构建及镜像核验，由本地已验证材料交付。候选工作流也可手动选择桥接、ESP8266 或组合打包。推送 `main` 只触发 CI 检查；显式推送对应版本标签才触发组件候选流程。

本地调用 GitHub CLI 使用 `python scripts/github_cli.py ...` 或其 `run_cli` 入口。Windows 直接解析真实控制台版 `gh.exe`，跳过 PATH 中的 GUI 包装器，并结合 `CREATE_NO_WINDOW`、隐藏窗口及标准输入/输出/错误重定向；失败保留退出码与错误，不打开交互终端。创建标签、push、刷机或公开新版本仍须对应操作的明确授权。

## English

The fixed entry points are DOWNLOADS.md and DOWNLOADS.en.md. download-catalog.json records seven published stable downloads, separately from local candidate version files. Run `python scripts/download_catalog.py refresh`, then `sync` to update both centers and README tables; `check` detects stale generated content, and `verify-online` checks public assets, URL availability, sizes and published checksums. Run `python scripts/test_download_catalog.py` for completeness and component-isolation regressions. The release-notes extractor prepends all downloads, using the target tag for the releasing component and retaining published companion packages. Draft asset links become public only after publication with all required assets. Refresh and sync after publication to advance the fixed center. These tools never push, upload or publish; packaging checks and installation/hardware acceptance remain separate. The local release checker includes index checks; workflow permissions, triggers and draft boundaries are unchanged.

Each release also reconciles README, bilingual changelogs, installation/update guides, feature references, version entries and download links. Preserve dated acceptance evidence. Refresh media only when visible content, versions or steps change; retain unchanged screenshots/videos with their original provenance. Visually review replacements and update their registered SHA-256 values.

Windows 0.6.1 retains daily update checks, deduplicated notifications, automatic selection/download and SHA-256 verification. Installation remains explicit through the existing Windows installer, ESP8266 backup/USB tool or TAB5 confirmation/boot-verification flow. Local/public functionality is unified; upgrades preserve separately installed artwork collections. Firmware metadata may declare minimumBridgeVersion and protocolVersion; see UPDATES.md.

The bridge, ESP8266 and TAB5 have independent version sources and changelogs. Only the changed component is renumbered and delivered. Windows/macOS share the bridge version line; TAB5 release declarations must match actual image, notes, first-install application and public source identity. Protocol version 1 is separate from release versions, and compatibility is based on supported protocol/features rather than equal numbers. Windows update checks select stable versions per component and retain legacy-bundle support. Component-specific packaging verifies only the requested payload and stages minimal user downloads plus checksums. Historical v0.5.0 tags/assets remain unchanged. The authorized CI patch routes bridge/ESP8266 component tags to draft packages only, while TAB5 tags validate metadata and retain independent local build/material validation. The candidate workflow also supports manual component selection. Pushing main triggers CI checks; explicitly pushing a version tag triggers its component candidate workflow. Local CLI calls use scripts/github_cli.py to resolve the real console executable, bypass GUI shims, hide its window and capture output/errors on Windows. Creating tags, pushing, flashing or publicly releasing still requires authorization for the corresponding action.
