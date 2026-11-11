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

Actions 批次 **42984952727** 的 Windows、Mac 与冻结作业全部通过。候选 ID：`c60a79ce821ae01679a5b3ef6c4c673a4837a85cfacbfdcae6465e39477d0668`。已下载 [冻结原包](https://github.com/yaoyouzhong/AI-bot/actions/runs/42984952727/artifacts/13838949568)，逐一核对冻结清单、许可、第三方声明、双语说明及 SHA-256。

| 文件 | 字节数 | SHA-256 |
| --- | ---: | --- |
| `AI-bot-bridge-v0.6.19-windows-x64-setup.exe` | 32,031,255 | `1378d04a5bab59891ee0f9e5e37421ce48ed0e15ef42e69247c6321f43eb0bc7` |
| `AI-bot-bridge-v0.6.19-macos-arm64.zip` | 19,881,856 | `5318e75f25710a853f8e2926965e2d8b10de17ccf1b995a926a038d63e6e7754` |
| `AI-bot-bridge-v0.6.19-windows-x64.zip` | 33,620,962 | `b1f8eb75c037e9e0a85d00f18a82e3cd7711ba344ad9a2ba2b65cfda685f57dc` |
| `AI-bot-bridge-v0.6.19-source.zip` | 161,411,549 | `8e224bce0e89f77450f3d89809da580d5def89629153d8af1a2e5dec0d447600` |

- **Windows**：原生 Release 构建和源码与解压便携包的独立桌面自测通过，111 个归档文件逐一校验；最终安装器未签名。最终源码归档 3,045 个文件（含清单），未纳入独立 TAB5 源码。实际安装器覆盖安装、启动及资料保留待验证。
- **macOS**：Actions 中 514 项 Swift 测试，5 跳过、0 失败；从同一 ZIP 执行 `--self-test-flash-tool`、`--self-test-tab5-ui`，离屏窗口版本读回正确。临时签名、未公证，与 0.6.18 公共包的签名类型相同。本机从开发签名 0.6.18 覆盖安装、正常启动和再次启动均通过，资料核验见下。
- [验收记录](WINDOWS-RELEASE-0619-ACCEPTANCE-20261111.json)已绑定该候选，`release-review` 和 `macos-install-startup` 已通过。剩余 `windows-install-startup` 仍阻止晋级；本交接未创建标签或 Release。

1. 拉取交接分支并保留候选源码提交的祖先关系。取回同一 Actions 批次的 `release-candidate-bridge`，核对 `CANDIDATE.json`；从原包验收，文档后续提交无需重建。Windows 补实际安装器安装、启动及资料保留，确认本次更新路径。
2. 按[统一开发发布流程](../DESKTOP-DEVELOPMENT-RELEASE.md)补记 `windows-install-startup`。若已有同一候选的本地验收文件，保留 Windows 记录，用 `record` 合入本交接的 Mac 通过证据。通过后由 Windows 负责人晋级同一冻结原包及对应源码标签，详见[发布自动化](../RELEASE-AUTOMATION.md)。
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
if (!(Test-Path -LiteralPath $acceptanceRecord)) {
    Copy-Item docs/development/WINDOWS-RELEASE-0619-ACCEPTANCE-20261111.json $acceptanceRecord
}
# 在共同源码上补 Windows 原生检查；包验收使用已冻结的安装器。
dotnet run -c Release --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once
dotnet run --no-build -c Release --project windows-app/AIBotBridge/AIBotBridge.csproj -- --self-test-unified-updates
# 完成 record 后先预检，再由 Windows 负责人按发布授权执行。
python scripts/release_pipeline.py status $candidateDir $acceptanceRecord --expected-source $candidateSource
python scripts/release_pipeline.py promote $candidateDir $acceptanceRecord --expected-source $candidateSource --repo yaoyouzhong/AI-bot
# 预检通过后在同一 promote 命令追加 --execute --publish。
```

## Mac 原包覆盖安装验收 · 2026-11-11

- **安装和启动通过。** Apple Silicon / macOS 27.3。原安装为本机开发签名的 0.6.18（源码 `dd5692653b372268eb60bc5c369abb132483b3a1`）。正常退出并留存旧 App 和资料备份后，将上表冻结 ZIP 的 App 放回 `/Applications/AIBotBridge.app`。已安装 App 的 17 个归档文件、0.6.19 版本、嵌入的共同源码 SHA、arm64 和严格签名均核对通过。常规后台启动和再启动均成功；验收后保留 0.6.19 运行。
- **设置保留。** 安装前、安装启动后、再启动后对照，41 项非缓存偏好完全一致，偏好无缺项；四项变化均为额度或行情缓存的运行时刷新。包含两台设备登记、议程、生日、常用任务、萌宠和私有会话资料在内的 43 个受检文件内容完全一致。
- **原配对可用。** 同一钥匙串资料项在三次检查中未被改写；常规启动及再启动后，用原 ESP8266 凭据请求实际新版进程的本地状态端点获 HTTP 200，用原 TAB5 配对密钥通过带挑战值的状态认证并解密正确响应。未重配对。
- **草稿可读。** 36 个私有存储文件逐一保持原字节。Codex 和 Claude 草稿日志经原钥匙串密钥认证解密，分别保留 15 条和 8 条记录；各有 1 条非空草稿，均由实际运行的 0.6.19 经加密 `draft-load` 读回，与安装前的文字及附件引用完全匹配。未发送、确认、清除或改写草稿。
- **验证边界。** 配对和草稿使用本机回环协议验收，未重测物理 USB／无线连线、设备显示、录音及其他 macOS 权限。没有重刷设备、改系统权限或发布。私有备份、草稿正文、配对凭据和本地探针输出均未提交。
