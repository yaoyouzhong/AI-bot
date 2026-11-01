# Windows 0.6.14 发布准备交接

11-01 最新候选：闲置录音元数据刷新修复已安装，DLL `12b48a24a217e30afe104bb6b148bd3345c9d781d8de29aa44dec83b9238a362`；本页旧 ZIP/setup 不含当前代码，不可直接发布。直达提交提示是独立 TAB5 源码补丁，尚未刷入设备，不混入 Windows 安装说明宣称已生效。MiMo / MiniMax 有账户后再验收，Mac R35 保持待验证；详见 [专项记录](WINDOWS-IDLE-SUBMIT-FIX-20261101.md)。

状态：本地准备，未提交、合并、推送、打标签、公开发布或刷机。用户要求 2026-10-30 完成 Windows 实机验证后再发布；本文件不提前确认实机结论或最终发布 payload。

10 月 30 日更新：实机发现等待偏长，已在最新工作树增加细分诊断，并依据真实只读测量减少重复 UIA 扫描。**下面的 10 月 29 日包仅为保留的历史候选，不包含此次改动，不可作为最新源码的最终发布包。** 待最终实机验收后重新冻结、打包、核验摘要和来源；新本地安装候选及测量见 [Windows 验证记录](WINDOWS-W29-VALIDATION-20261029.md)。

补充：13 时段候选加入参考 Mac 的最终回复传输优先级、USB/BLE 最终结果短等待与一次光标初始重复扫描消除，稳定和防串题条件保留；新增代码／模拟回归通过，实际提速仍待 Windows 录音。发布文件清单必须包含 `Tab5VoiceTransportPriority.cs` 及对应传输集成／自测，旧 COMMIT-SCOPE 摘要不能代表现工作树。详见验证记录。

## 组件与源码

- 基线为 PR #15 合并提交 `572a7ee2bb5eda3ae7413110992f3a0de3aa8a2a`，工作树为 `codex/windows-w29`。当前未提交改动必须进入最终正式提交，不能将基线 SHA 冒充发布源码 SHA。
- 拟发布 Windows 0.6.14；`VERSION`、Windows csproj、共享 Mac plist 元数据一致，`release-manifest.json` 的 `desktopPlatform=windows`。不修改 workflow，不生成 Mac 新包，正式 Mac 下载保持 0.6.13；固件与图库版本保持原状。
- 2026-10-29 只读核对 [GitHub 已发布 0.6.13](https://github.com/yaoyouzhong/AI-bot/releases/tag/v0.6.13) 的 Windows/Mac 附件存在；历史 0.6.4／0.5.1 草稿不是最新正式版本，不修改这些历史草稿。
- 本地 main 的账户布局、预览定位、首次设备引导、图库与语音修复已核对保留。93 项 W29 自测包含新增异常边界；新增测试不改运行逻辑。

## 已准备的发布文案

- 中英文更新日志：根目录 `CHANGELOG.zh.md`、`CHANGELOG.md` 的 0.6.14 段落。日期为准备日期，明确标记尚未发布。
- 双语发布说明：[Windows 0.6.14](../RELEASE-0.6.14.md)。包含自动填入、蓝色箭头提交、题卡变化、排队成功与未知不重发，以及 Windows／Mac 实机边界。
- 发布后中文首页摘要：`本次更新 Windows 桥接 0.6.14：支持 Codex 题卡回答、下一步和提交，录音绑定当前题目，保留连续录音原文；消息可靠入队即算发送成功。Mac 保持 0.6.13，固件和图库独立更新。`
- 发布后英文首页摘要：`Windows bridge 0.6.14 adds Codex question answers and Next/Submit, binds recordings to the current question, preserves consecutive takes and confirms reliably saved queued messages. Mac remains at 0.6.13; firmware and galleries update independently.`
- 安装指南的 Windows 新增说明：`停止录音后自动填入目标输入框。核对文字后点蓝色箭头提交；需要补充时继续录音，原草稿保留。题卡变化后先核对问题，未知发送结果不要重复提交。`
- 英文对应：`Stopping recording automatically fills the target. Review the text before using the blue arrow to submit; another take appends to the existing draft. Check the question after a target change and do not repeat an uncertain submission.`

公开下载表仍使用实际已发布附件；不提前伪造 0.6.14 链接、大小或 SHA-256。发布成功后运行已有 `download_catalog.py refresh/sync/check`，由实际附件更新 README、DOWNLOADS 和双语入口，再把首页“本次更新”及安装指南的 Windows 版本改为 0.6.14；Mac 行仍保留 0.6.13。

## 本地包与验证

按 `scripts/package_windows_local.ps1` 生成新的独立目录：来源快照→Release→隔离 public 自测→win-x64 publish→依赖许可→安装器编译／SELFTEST／CHECKONLY→ZIP 完整解压哈希→解压包回归→源码包。脚本不停止常驻桥接，不连接实体设备，不执行实际安装。

2026-10-29 22:06 完成本地包，输出目录为工作树内 `artifacts/windows-0.6.14-prep-20261029/`。`package_windows_local.ps1` 退出 0；依赖下载耗时较长，按固定哈希／签名校验后完成，未改下载校验或系统配置。实际生成内容如下，三者各附 `.sha256`：

| 文件 | 字节 | SHA-256 |
| --- | ---: | --- |
| `AI-bot-bridge-v0.6.14-windows-x64-setup.exe` | 31,199,362 | `c7da6da3a9d14a8235ced4239b12f2058f7208367886250fe4aef004d6e214b6` |
| `AI-bot-bridge-v0.6.14-windows-x64.zip` | 32,537,650 | `b35183a303016195faf96fe4a652f862033ab7a953ac873fe2273d5d9926ab35` |
| `AI-bot-bridge-v0.6.14-source.zip` | 80,381,484 | `99ae5047d40678577ed04ad2df541d4d7426e4d013cfe42940c109b6fa33d174` |

- 源码快照 `artifacts/release-check-e5edb05fb70b48b6b5cf144b85f3430e/`：Release 编译、完整隔离 public 自测通过；源码清单 2,557 文件全部逐字节验证，包含未提交的 W29 实现与本轮测试。
- 最终 win-x64 程序：QuickConsole（93 项 W29）、RPC、VoiceCore 通过；从正常 Explorer 桌面运行 `--status-once` 退出 0，保留目录保护，程序集 SHA-256 与 ZIP 内完全一致。
- 安装器：固定 Inno 6.4.3 校验、有效／损坏／404／无需下载检测、实际编译产物 `/SELFTEST` 和 `/CHECKONLY` 通过；本机报告 .NET Desktop 与 WebView2 已安装。只检测，未执行安装或修改组件服务。
- 分发依赖：14 项实际依赖许可、官方 TightVNC MSI／对应源码／签名、离线 esptool 固定摘要验证通过，保留 `LICENSE`、`THIRD_PARTY_NOTICES.md` 和完整文件清单。
- ZIP：106 个分发文件解压哈希通过；解压后的完整 public 自测在独立隐藏桌面通过，离线工具的读／写／验证命令构造测试通过，无设备访问或刷机。
- 最终独立复核：三个包及侧车摘要、ZIP 无重复／额外成员、版本／无内置图库、源清单与冻结来源全部通过。证据为 `artifacts/w29-preflight-20261029/PREPARATION.json`、`SHA256SUMS.txt`、`package.log`、`package-status-result.json`。已安装开发候选没有被替换。

准备清单明确记录 `sourceCommit=null`、`uncommitted=true`、基线 SHA、源码清单摘要和实机未验收。不能将 `572a7ee` 冒充正式发布源码 SHA。此源码包冻结在打包开始时，随后只补写了交接文档；正式提交确定后刷新文档版源码包及最终 BUILD／上传清单，无需仅为文档或发布元数据重编已验收的安装器和 Windows ZIP。

发布资产沿用 `scripts/artifact_names.py`：主安装包为 `AI-bot-bridge-v0.6.14-windows-x64-setup.exe`，发布时由既有资产工具生成完全同字节的兼容名 `AIBotBridge-0.6.14-setup-win-x64.exe`，保留旧客户端更新发现能力。本地 ZIP 和源码 ZIP 是审计材料；最终公开附件范围由既有 `prepare_release_assets.py --component bridge --desktop-platform windows` 生成，源提交必须填实际正式 SHA，不使用基线占位。提取后的双语 Release 正文已保存为 `artifacts/w29-preflight-20261029/RELEASE-NOTES.md`，正式发布前再按实际验收结论和日期更新。

## 11-01 最新状态（覆盖下方历史候选结论）

任务分支已同步 `origin/main=0b39860`，保留本地 main 功能并补齐 W31。新 Windows 候选与本地 setup 位于 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w31-20261101`；10-29 的 ZIP/setup/source 不能发布为这次代码。新 setup 的 `/SELFTEST`、`/CHECKONLY` 通过，但仍不是安装向导实际升级验收。来源仍未提交，不能把基线 `0b39860` 当作最终源码 commit。

实现、验证、安装和最终实机剩余项见 [W31 记录](WINDOWS-W31-VALIDATION-20261101.md)。Windows 实测后再冻结正式源码与附件，仍需具体 payload 的推送／标签／发布授权；本轮没有执行这些动作。

## 明天的必要步骤（10-29 历史记录）

1. 用户在准备完成后授权立即更新；现已安装同一份 0.6.14 ZIP 并通过状态及资料保留检查，见下节。明天先复核运行版本与包摘要，再直接进行实机验证；若包字节未变，无需重复覆盖安装。此次采用现有目录 ZIP 更新，安装器 `/SELFTEST`／`/CHECKONLY` 通过，但 setup 安装向导的实际升级路径仍未执行，不能混称安装器实装已验收。
2. 每次只给用户一个简单步骤：先主框录音自动填入／蓝色箭头提交，随后逐项验证题卡、多题、跨题预览、连续录音与光标、排队和可用麦克风；不是批量口令清单。Mac R35 排队实机仍单独待验。
3. 用明确获准的最终提交及包摘要完成 CI、远端分支／标签与发布，不能因今日“准备”执行这些写操作。发布流程保留 Windows-only 范围，不带上旧候选／备份／用户配置。
4. 附件实际公开后同步完整下载中心、两种语言首页、安装说明、组件版本页和更新说明，记录服务器 digest／Range 与本地完整哈希的不同证据。
5. 刷新相关截图、项目宣传片及可复现工程，核验网页内播放、独立观看入口、MP4 下载、暂停、拖动和重播。现有视频继续有效，但未宣称已展示新题卡功能；不以 HTTP 200 代替播放验收。

## 媒体准备

沿用现有 121 秒项目全功能介绍的结构：共有功能仍按 TAB5／ESP8266 对照，TAB5 独有功能单列。新题卡片段替换原 Codex 语音段的部分镜头，避免把整片改成只介绍本轮更新。封面不放版本号。

编辑入口与现有时间轴见 [产品视频记录](../PRODUCT_VIDEO.md)：保留 0–49 秒两种硬件对照、49–56 秒 TAB5 总览、86–115 秒日程与艺术、115–121 秒收尾；仅在 56–86 秒 Codex／相机／电脑控制段中替换与本轮有关的语音镜头，保留相机和电脑控制的介绍。原生演示、硬件合成图与实拍必须按实际素材分别标注。

已备妥字幕：中文 `结束录音，自动填入；核对后，蓝色箭头提交。`／英文 `Stop to fill automatically. Review, then use the blue arrow to submit.`；题卡字幕 `当前问题独立回答，下一步继续。`／`Answer the current question, then continue with Next.`；排队字幕 `消息已保存到队列，即确认送达。`／`Saved to the queue means delivered.`

新题卡与排队画面需要明天获验收的实际界面或对应隔离原生控件；今晚不伪造硬件画面、不使用真实草稿录屏。已有语音设置的隔离预览可复用；实际镜头确定后登记来源、许可和媒体 SHA-256。公开上传与在线播放检查保留到发布阶段。

## 保留现场

用户随后授权立即更新。2026-10-29 22:17，现有桥接目录已更新为本页同摘要的 Windows 0.6.14 ZIP；已正常退出旧程序并启动新版，真实“关于 AI-bot”窗口显示 `0.6.14`。107 个安装文件（含 `FILES.sha256`）逐一验证，106 个原文件（含旧安装标记）有已校验回滚副本；6 个配对／设置／草稿文件与 7,151 个图库文件的前后 SHA-256 一致。ESP8266 自动轮播、六页顺序及 15 秒间隔保留，TAB5 展示设置未改。已安装 `--status-once` 退出 0，目录保护保留。预检最初因 ProductVersion 包含 `+572a7ee...` 构建后缀而停止，核对冻结包的完整版本及哈希后才继续，未改候选字节或绕过保护。

本次更新证据与回滚目录：`C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-0614-install-20261029`，以 `FINAL-VERIFICATION.json` 为安装完成记录。先前的 `windows-w29-install-20261029` 备份仍保留；`PREPARATION.json` 是打包时快照，其中“未安装”字段不是安装后的当前状态。实机功能和 Mac R35 仍待验收，没有推送、合并、标签、公开发布或刷机。

工作树 `C:\Users\yaououzhong\.codex\worktrees\windows-w29\AI-bot` 暂留，原因是明天实机和最终发布提交尚待完成，当前也没有合并或存档标签授权。用户询问提交方式后再次 fetch，`origin/main` 仍是 `572a7ee`，与本分支基线一致；本地 `main` 仍在 `12e7732`，保留其独立提交与未提交资料。
