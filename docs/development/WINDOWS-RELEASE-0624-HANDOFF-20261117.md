# Windows 0.6.24 发布交接（2026-11-17）

- **范围**：仅 Windows x64 桥接。分支 `codex/tab5-calendar-and-claude-idle-20261116`，包含 Windows 图库对齐 `5476a32e59b0e431351508da53e2fa871528a4fd`。组件标签预期为 `bridge-v0.6.24`，正式产品总页的 `vX.Y.Z` 由晋级工具按实际发布状态分配。本次只提交发布资料，后续由 Windows 端负责构建、验收和发布。
- **其他已发布组件**：Mac 0.6.23、TAB5 0.2.321-ui、ESP8266 0.5.8 保持。版本校验要求根 `VERSION`、Windows 项目和 Mac plist 源码声明一致；`desktopPlatform` 只选 `windows`。不因此重新构建或发布 Mac 包，不打固件包，不刷机。
- **发给用户的文字**：[双语说明](../RELEASE-0.6.24.md)、[中文日志](../../CHANGELOG.zh.md)、[英文日志](../../CHANGELOG.md)、[更新操作](../UPDATES.md#windows-0624)。发布页的双语正文从日志提取。公开下载、README 的最新版本提示和正式校验链接等附件上线后再换。

## 固定候选

先拉取分支，并核对工作区干净及 `5476a32` 已合入。在包含版本资料、此次最终 Windows 代码和双语日志的提交上记录完整 `git rev-parse HEAD`，供构建、验收和正式标签使用。此前 Mac 候选 `c2e9b99` 只证明当时已验证的路径，不能当作 Windows 安装包来源。未来若在 `5476a32` 之后又改 Windows，先核对更新说明和受影响的测试。

在 GitHub Actions `candidate.yml` 选择**开发分支或已合入且源码不变的提交**，参数 `component=bridge`、`desktop_platform=windows`；核对运行的源码 SHA，取得 `release-candidate-bridge` 原目录并记录批次。若在 Windows 本地采用仓库原生命令，用干净工作区执行（输出目录须为新目录）：

```powershell
$src = git rev-parse HEAD
$root = 'artifacts/windows-0624'
$raw = "$root/raw"
$candidate = "$root/bridge"
$acceptance = "$root/bridge-acceptance.json"
python scripts/check_version.py --tag bridge-v0.6.24
powershell -NoProfile -File scripts/package_windows_local.ps1 -OutputDirectory $raw
python scripts/release_pipeline.py stamp $raw --source $src --platform windows
python scripts/release_pipeline.py assemble $raw $candidate --source $src --component bridge --platform windows --require-stamps
```

Actions 已输出冻结候选时，直接用其目录替换 `$candidate`，不要重新 `assemble` 或打包。核对 `CANDIDATE.json` 的源码 SHA、平台、版本、候选 ID 和安装器 SHA-256。公开附件按现行规则使用一份 Windows 安装器 `AI-bot-bridge-v0.6.24-windows-x64-setup.exe` 和 `SHA256SUMS.txt`，许可证和第三方声明随安装包；其他材料放构建归档。实际附件名以候选封存结果为准。

## Windows 原生、安装与真机验收

| 路径 | 当前证据及接手动作 |
| --- | --- |
| Windows 原生回归 | `4d4051f` 时 Windows 已跑 Release、`--status-once` 及更新和设备中心回归。对最终 Windows 源码复跑[开发交接中的命令](UPDATE-INDICATORS-HANDOFF-20261117.md)，另跑 `--self-test-tab5-ble`、`--self-test-tab5-ble-voice`。核对两套图库只计一项、复用成功的目录查询、取消打开安装器、图库安装中退出后回到窗口。 |
| 原包覆盖升级 | 用冻结 Windows 安装器覆盖当前公开 0.6.21，验证安装、运行版本、重启、自启动和既有配对、偏好、图库及草稿保留。检查原包的安装器名称和校验值，再记录 `windows-install-startup`。Mac 测试或 Windows 交叉编译不代替此项。 |
| TAB5 真机 | 先在公开固件 0.2.321-ui 验证基础连接、USB 自动恢复及总览，核对实际固件及可用能力；R6（0.2.322-ui）若用于复测，单独记录其版本，不能代替公开固件验证，也不要求升级用户设备。再从 TAB5 向隔离的 Claude 后台会话发送一次，确认原会话只增加一条消息、设备能打开回复。核对 Codex 总览与已读同步。做蓝牙传输或录音时记录真实链路；确认设置和草稿保留。 |
| Windows 专用路径 | 用隔离测试会话测试管理员状态下 Codex 直达、焦点与一键解锁；不要把凭据写入日志。核对实际系统通知送达、点击并定位。过去的窗口回归或设备在 Mac 上通过不能替代 Windows 验收。 |
| 发布复核 | 检查双语日志、范围、限制、媒体和候选身份，结合上表及实际证据记录 `release-review`。没有取得最终 Windows 原包和本机／真机记录之前，不标记通过。 |

取得证据后再登记实际结果，并进行不写远端的晋级预检。验收文件与候选 ID 绑定；不要复制旧候选的通过记录。

```powershell
python scripts/release_pipeline.py record $candidate $acceptance --check windows-install-startup --result passed --evidence '替换为原包升级、启动及资料保留的真实记录'
python scripts/release_pipeline.py record $candidate $acceptance --check release-review --result passed --evidence '替换为双语、范围、复测及媒体复核的真实记录'
python scripts/release_pipeline.py status $candidate $acceptance --expected-source $src
python scripts/release_pipeline.py promote $candidate $acceptance --repo yaoyouzhong/AI-bot --expected-source $src --minimal-bridge-assets --scope-reason '本次仅发布 Windows 0.6.24 桥接；保留其他已发布组件'
```

## 发布后收尾

由 Windows 负责人按[发布自动化](../RELEASE-AUTOMATION.md)和[收尾清单](../RELEASE-CHECKLIST.md)晋级已验收原包。确认实际产品页、Windows 组件页和下载校验后，再刷新下载索引、中英文下载块及安装指南的 Windows 当前版本；Mac 下载继续列 0.6.23。核对 Windows 更新器能发现并校验实际安装器，Mac 不会把这次 Windows 发布当作 Mac 更新。

主页双语短句可直接使用：

- 中文：Windows 桥接 **0.6.24** 将软件、固件与屏保图库更新集中到「软件固件」，并改善 TAB5 连接与 Claude、Codex 会话兼容性。Mac 保持 **0.6.23**，设备无需刷机。
- English: Windows bridge **0.6.24** brings software, firmware, and artwork updates into one window and improves TAB5 connectivity and Claude and Codex compatibility. Mac stays at **0.6.23**. No firmware update is required.

已发布安装图文和视频上的历史素材继续明确标作示例。若旧 Windows 更新窗口或「连接设置」截图会把用户带到错误入口，只替换受影响的画面，使用实际 Windows 原生预览并登记来源、许可和 SHA-256。媒体未替换前不宣称已更新。
