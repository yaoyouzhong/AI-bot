# 名称、版本与构建物 / Names, versions and artifacts

产品品牌统一使用 **AI-bot**。

| 对象 / Component | 发布名称 / Release title | 版本值 / Version value |
| --- | --- | --- |
| Windows / macOS 桥接 / Bridge | `AI-bot v0.6.7` | `0.6.7` |
| ESP8266 固件 / Firmware | 保留原方式，例如 / Retain existing convention, e.g. `ESP8266 0.5.3` | `0.5.3` |
| TAB5 固件 / Firmware | 保留原方式，例如 / Retain existing convention, e.g. `TAB5 0.2.205-ui` | `0.2.205-ui` |

桥接标题使用 `v`；两种固件的标题、版本值和新构建物文件名均不加 `v`。组件独立递增版本，保留 TAB5 既有 `-ui`，不为命名重编号或重新刷机。图库按 `YYYY.MM.DD` 编号，同日修订追加 `.1`、`.2`。功能说明放在发布正文。

Bridge titles use `v`; firmware titles, version values and canonical package filenames omit it. Components advance independently. Preserve the existing TAB5 `-ui` identity. Collections use `YYYY.MM.DD`, with `.1`, `.2` for same-day revisions. Feature descriptions belong in release notes.

## 构建物 / Artifacts

固定顺序为 `AI-bot-组件-版本字段-平台-架构-用途.扩展名`，省略不适用的字段。组件、平台和用途使用小写；只有桥接版本字段使用 `v`。正式构建物使用产品名称与版本，构建阶段由版本及发布状态表达，去掉 `local-candidate`。

Use `AI-bot-component-version-platform-architecture-purpose.extension`, omitting inapplicable fields. Component, platform and purpose fields are lowercase. Only bridge filenames prefix the version with `v`. Remove `local-candidate` from artifact names; build/release status is recorded separately.

以下为当前版本的命名示例，新名称用于后续构建；下载中心继续登记实际已发布文件。

These examples use current version numbers. Future builds use these names; the download catalog continues to point to assets that actually exist.

| 用途 / Purpose | 文件名 / Filename |
| --- | --- |
| Windows 安装器 / Installer | `AI-bot-bridge-v0.6.7-windows-x64-setup.exe` |
| Windows 程序 ZIP / App archive | `AI-bot-bridge-v0.6.7-windows-x64.zip` |
| Mac 程序 ZIP / App archive | `AI-bot-bridge-v0.6.7-macos-arm64.zip` |
| 桥接源码 / Bridge source | `AI-bot-bridge-v0.6.7-source.zip` |
| ESP8266 固件与刷机材料 / Firmware materials | `AI-bot-esp8266-0.5.3-firmware.zip` |
| TAB5 首刷包 / First install | `AI-bot-tab5-0.2.205-ui-first-install.zip` |
| TAB5 升级包 / Upgrade | `AI-bot-tab5-0.2.205-ui-upgrade.zip` |
| TAB5 源码 / Source | `AI-bot-tab5-0.2.205-ui-source.zip` |
| 标准名画 / Standard painting | `AI-bot-gallery-painting-standard-2026.10.14.1.zip` |
| 标准书法 / Standard calligraphy | `AI-bot-gallery-calligraphy-standard-2026.10.15.1.zip` |
| 高清名画 / HD painting | `AI-bot-gallery-painting-hd-2026.10.16.zip` |
| 高清书法 / HD calligraphy | `AI-bot-gallery-calligraphy-hd-2026.10.16.zip` |

高清分段在完整 ZIP 名后追加 `.part001`、`.part002`。校验文件沿用 `SHA256SUMS.txt` 或完整文件名加 `.sha256`。下载表统一显示「下载 · 大小」。

HD parts append `.part001`, `.part002` to the complete ZIP name. Retain `SHA256SUMS.txt` or a `.sha256` sidecar. Download links display “Download · size”.

## 兼容与迁移 / Compatibility and migration

命名模板和历史别名由 [`scripts/artifact_names.py`](../scripts/artifact_names.py) 维护。构建、打包验证、公开附件整理、下载索引和 Windows 更新识别同时支持新旧名称。同版本存在两种名称时优先使用新名称。

已发布附件保留原名称与下载地址。后续发布的 Windows 安装器、ESP8266 固件包和 TAB5 升级包同时提供同字节的旧名兼容附件，其 SHA-256 一并登记，使旧桥接仍可发现新版。图库不为改名重复上传大包；当前旧版图库继续可用，识别新名称需要包含本次兼容更新的桥接。

[`scripts/artifact_names.py`](../scripts/artifact_names.py) defines canonical names and legacy aliases. Packaging validation, publication staging, download indexing and Windows update discovery accept both, preferring canonical names for equal versions. Existing public assets and URLs remain available. Future Windows installers, ESP8266 packages and TAB5 upgrades retain byte-identical legacy-name aliases with published checksums so installed older bridges can discover updates. Do not duplicate large collections for naming alone; existing collection assets remain usable, while new collection names require a compatible bridge.

Git 标签沿用 `bridge-vX.Y.Z`、`esp8266-vX.Y.Z`、`tab5-vX.Y.Z-ui`，其中固件标签里的 `v` 是历史路由结构；内部固件文件仍使用 `aibot_tab5.bin` 等既有名称。发布名称、外层包名和内部协议名称分别承担不同用途。

Keep component Git tag prefixes (`bridge-v`, `esp8266-v`, `tab5-v`) for routing and history, and retain internal firmware names such as `aibot_tab5.bin`. Firmware tag prefixes are separate from displayed version values and canonical package filenames.
