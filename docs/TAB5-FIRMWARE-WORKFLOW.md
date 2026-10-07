# TAB5 本地固件目录 / Local firmware directory

首次安装与升级的使用说明见[双硬件安装指南](INSTALL.zh.md)。TAB5 首次安装需要包含启动程序、分区表及初始化内容的完整 ZIP；下面固定路径仅用于已安装 AI-bot 的 TAB5 升级。首刷与升级应用可以同为 0.2.89-ui，文件格式不同。

用户选择升级固件时始终使用 `artifacts/firmware/tab5/latest/aibot_tab5.bin`。同目录包含与镜像匹配的 `.bin.notes.json`、`manifest.json` 和 `SHA256SUMS.txt`。这些是本地 OTA 候选，不代表已经刷入或公开发布。

历史固件保存在 `artifacts/firmware/tab5/versions/<版本>/<镜像哈希前缀>-<说明哈希前缀>/`，修改说明也保留独立归档。已有 development 目录保留为构建、预览和验收证据，不再让用户从这些目录选择新固件。

在 AI-bot 根目录用 PowerShell 7 执行：

```powershell
./scripts/prepare_tab5_firmware.ps1 -ImagePath <已构建的固件.bin>
```

源镜像旁必须提供匹配版本与 SHA-256 的 `.bin.notes.json`。`notes` 必须包含 `## 分类` 和 `- 用户可见变化`；脚本复用 Release 桥接的正式镜像/说明校验器，校验成功才归档和更新 latest。桥接 DLL 默认来自本仓库 Release 构建，可用 `-BridgeDll` 明确指定。脚本不会启动桥接或操作设备。

随后在相邻 TAB5 仓库执行实际说明预览，并检查生成图片：

```powershell
./scripts/preview-ui.ps1 -Clang -NotesPath '../AI-bot/artifacts/firmware/tab5/latest/aibot_tab5.bin.notes.json'
```

修改说明后须在桥接重新选择固件才能加载新说明；同版本不重复刷机。刷机仍需对应候选的用户授权，启动核验与恢复原自动轮播沿用项目规则。

Windows 候选可在 development 目录构建和自检，实际运行沿用固定的 `windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/AIBotBridge.exe`。更新前正常退出并备份原运行文件，再部署已核验的二进制、核对哈希及用户资料目录、恢复轮播后从固定路径启动。每次从新候选目录运行可能再次触发 Windows 网络访问授权；不为测试改动防火墙规则。

## English

Build and test Windows candidates under development, but run validated binaries from the existing fixed Release path. Exit normally, back up runtime files, verify deployed hashes and the user profile, restore the carousel and restart there. New executable paths may trigger repeated Windows network-access prompts; do not modify firewall rules for testing.

For factory installation use a full first-install ZIP, not this application BIN. Both may carry version 0.2.89-ui; see INSTALL.zh.md. For an existing AI-bot installation, select `artifacts/firmware/tab5/latest/aibot_tab5.bin`. Its matching notes, manifest and checksum accompany it. `scripts/prepare_tab5_firmware.ps1` validates the image with the Release bridge loader and requires structured release notes before updating this fixed path. Previous image/note combinations are retained under `versions/<version>/<image-hash-prefix>-<notes-hash-prefix>/`; development artifacts remain available as evidence. Preview the actual notes with the adjacent TAB5 preview script. Preparing a candidate does not flash a device or publish a release.
