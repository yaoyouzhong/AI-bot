# 桥接 0.6.19：Mac 验收与 Windows 发布交接

## 范围和源码身份

- Windows → Mac 对齐：`origin/codex/component-update-followup-20261110`，来源 `6a6c1af877878e017294ee3ad240a4094afbad51`。合入 `origin/main` 当时的 `7793366d38123c0b2fe4bf2a4c332a3a7e90335e`，保留两侧历史。
- 共同候选源码为 `89f113897ef21d6b1ee3b45c2d321ea2dec5f5b3`，已推送至 `codex/mac-component-update-0619-20261111`。[Candidate packages 42984952727](https://github.com/yaoyouzhong/AI-bot/actions/runs/42984952727) 在对应系统原生构建；后续交接文档提交不改变该候选的源码身份。
- 本次目标标签 `v0.6.19`，平台 `all`：Windows x64 和 Apple Silicon Mac。用户指定 Windows 统一发布；此 Mac 交接包含分支推送和候选准备。协议 v1、配对、草稿、固件版本与兼容下限不变；ESP8266 0.5.8、TAB5 0.2.318-ui、独立图库沿用已发布版本。

## 对齐与验证

| 用户行为或发布项 | 源码 / 对端 | 状态与证据 | 限制 |
| --- | --- | --- | --- |
| ESP8266 更新优先精简包 | Windows `ComponentUpdateCatalog.cs` / Mac `MacUpdateWindow.swift` | 已完成：两端优先选最新正式版本。精简包要有可信地址、合理大小及同一发布内的专用 SHA-256 文件；缺失时兼容完整材料包。Mac 离线回归覆盖精简包优先、缺 sidecar 回退及较新版本优先。 | 校验失败停在下载；仍走原有刷机确认流程。 |
| 正式附件下载 | [Windows 来源交接](ESP8266-COMPACT-UPDATE-HANDOFF-20261110.md) / Mac 原生 `MacReleaseCatalog`、`MacGalleryDownloader` | 已完成：Windows 来源提交的原生构建、状态、更新自测与公开下载已有记录；Mac 在隔离目录从正式 GitHub 发布选中并下载 `AI-bot-esp8266-0.5.8-flash.zip`，392,706 字节；SHA-256 `135727db235e2ef88fc852357598539f236811564da353c86046bd4f759eca06`，与 sidecar 和 GitHub 公布摘要一致。 | Mac 线上下载没有进入安装器，未写入任何设备。实体小屏刷机不在本次桥接变更范围。 |
| 软件固件界面 | 两端既有入口及控件 | 已完成：选包发生在后台；保持组件表、说明、状态及底部操作的顺序，沿用现有原生布局、字号、颜色和点击区域。Mac 在默认 800×620 和最小 600×440 离屏检查，查看真实发布列表加载后的 ESP8266 行；下载按钮可用，安装按钮保持禁用。另查看原生渲染的浅色、深色及窄窗布局。 | 最小高度可纵向滚动说明；不需要调整网页或设备 UI。未激活电脑窗口。 |
| 桥接源码归档 | `scripts/check_public_content.py`、Windows `verify_release_local.ps1`、Mac `package_desktop_source.py` | 已完成：主干的独立 TAB5 .318 源码与正式 TAB5 源码 ZIP 的 1,175 个文件逐一匹配，正式 ZIP SHA-256 `a4b96b24bc51810ade2b73096661fa6baac6effcad3173a3c452b8c24af8534f`。守卫只认可这份清单固定路径和摘要，新文件或变更内容仍会被拦截；桌面源码包排除独立 `tab5/` 快照。 | 原 TAB5 附件、归属声明、发布权利范围和正式下载不变。 |

Mac 原生：`swift test --package-path mac-app` 514 项，4 跳过、0 失败；`swift build -c release --package-path mac-app` 通过；`--self-test-tab5-ui` 离屏通过。全局公开内容检查、来源材料校验、组件版本、下载索引、双语最终说明提取、打包及晋级工具回归通过。Mac 单平台对应源码归档也已实际生成并检查，保留构建所需的 ESP8266 字形头文件和许可，未包含独立的 TAB5 树。实体设备展示与固件升级不适用；未重刷、未发送消息、未改配对与设备展示设置。

## Windows 发布接手

1. 拉取交接分支并保留候选源码提交的祖先关系。取回同一 Actions 批次的 `release-candidate-bridge`，核对 `CANDIDATE.json`；从原包验收，文档后续提交无需重建。Windows 补实际安装器安装、启动及资料保留，确认本次更新路径；Mac 的常规覆盖升级、权限和实际资料读回以实际候选验收结果为准。
2. 按[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)记录 `release-review`、`windows-install-startup`、`macos-install-startup`。关键项或任何失败项不得沿用为通过。通过后由 Windows 负责人晋级同一冻结原包及对应源码标签，详见[发布自动化](../RELEASE-AUTOMATION.md)。
3. 双语说明已在 [中文 CHANGELOG](../../CHANGELOG.zh.md#0619---2026-11-11)、[English changelog](../../CHANGELOG.md#0619---2026-11-11) 和 [0.6.19 发布说明](../RELEASE-0.6.19.md)；[更新指南](../UPDATES.md)与[安装指南](../INSTALL.zh.md)已补新版操作说明。正式附件上线后，按[收尾清单](../RELEASE-CHECKLIST.md)刷新 README 和下载中心的生成块，更新两份 README 的顶部说明与链接，以及 `docs/INSTALL.zh.md` 和 `docs/FIRST-INSTALL.zh.md` 的当前版本、安装器及校验入口。发布前保留现有公开链接。
4. 主页更新用语：**Windows／Mac 桥接 0.6.19 会优先下载经过校验的 ESP8266 精简刷机包。设备固件无需随桥接更新。** / **Windows and Mac bridge 0.6.19 prefers the compact ESP8266 flash package and verifies it before handoff. Device firmware does not need to be updated with the bridge.** 既有主页截图、宣传片、封面和工程仍准确，复用原素材及既有播放证据；只变后台下载包选择。0.6.18 对话与 TAB5 .318 的历史发布说明保留。

PowerShell 入口：

```powershell
git fetch origin
$candidateSource = '89f113897ef21d6b1ee3b45c2d321ea2dec5f5b3'
$candidateDir = 'artifacts/release-0619/bridge'
$acceptanceRecord = 'artifacts/release-0619/bridge-acceptance.json'
gh run download 42984952727 --repo yaoyouzhong/AI-bot --name release-candidate-bridge --dir $candidateDir
Get-Content "$candidateDir/CANDIDATE.json"
# 在共同源码上补 Windows 原生检查；包验收使用已冻结的安装器。
dotnet run -c Release --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once
dotnet run --no-build -c Release --project windows-app/AIBotBridge/AIBotBridge.csproj -- --self-test-unified-updates
# 完成 record 后先预检，再由 Windows 负责人按发布授权执行。
python scripts/release_pipeline.py status $candidateDir $acceptanceRecord --expected-source $candidateSource
python scripts/release_pipeline.py promote $candidateDir $acceptanceRecord --expected-source $candidateSource --repo yaoyouzhong/AI-bot
# 预检通过后在同一 promote 命令追加 --execute --publish。
```
