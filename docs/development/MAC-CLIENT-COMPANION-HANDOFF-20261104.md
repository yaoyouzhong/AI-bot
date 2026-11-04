# Mac 客户端萌宠适配交接

本页保留发出时的交接要求。2026-11-04 后续实现与本轮结果见[Claude / Codex 对话交接](CLAUDE-DIALOGUE-HANDOFF-20261104.md)，Windows 接收端按[实际变更核对清单](WINDOWS-CLAUDE-DIALOGUE-ALIGNMENT-20261104.md)继续；不要把下面的历史“待实施”状态重新当作全部待办，也不重验本轮未修改场景。

状态：**待 Mac 端实施和原生验证，阻止正式发布。源码先提交，供 Mac 取得工程后继续适配。** 2026-11-04 用户明确要求统一提交上次发布后的全部项目修改，并指出 Mac 正等待 .312 工程；本轮先交付完整源码，后收齐适配及原包验收。本文件是交接要求，不是已送达、已实施或验收通过的证明。

## 范围和源码身份

- 来源：Windows 发布负责会话；接收：AI-bot macOS 开发会话。
- 远端：`https://github.com/yaoyouzhong/AI-bot.git`，本轮整理前基线：`2aa7acca07fc171c6cf37d811b3463d32adfa2f7`。萌宠功能提交为 `a25ea60a222bd6e940897ab37ada782788c1fb75`；接收方应取得包含本交接与[完整 .312 工程](tab5-312-source/README.md)的最新 `main`。
- 拟发布版本：Windows／Mac 桥接 0.6.17、TAB5 0.2.312-ui；目前未创建这两个正式标签或公开 Release。ESP8266 保持 0.5.8。
- 已冻结的桌面候选来源是上述 `2aa7acc`。适配产生运行代码变化后，旧候选不能代表新实现；最终两平台从同一源码 SHA 构建，记录实际包哈希及受影响验收。
- TAB5 0.2.312-ui 应用 SHA-256：`21d78bcce33bdbdb531588b225b59a9902e53ecff620c9b614863f56cdb34d8c`。不为电脑端适配反复刷机，动画、布局、背景及时间字体均由固件提供。
- 实施前读取项目规则及 [统一流程](../DESKTOP-DEVELOPMENT-RELEASE.md)，保护已有改动。不要自行打标签或公开发布；最终合并、推送与发布由原发布负责人统一处理。

## 共同用户行为

总览每次只显示当前受支持客户端的一只萌宠：Codex 或 Clawd。Codex／Claude Code 使用其他模型时，仍显示对应客户端的萌宠；不增加国产模型萌宠，不接入 OpenCode、DeepSeek Harness 或其他客户端。用户选中的萌宠屏保独立保留其选择，不随总览的活跃客户端切换。

工作、等待、完成、受阻与未知根据可信活动信号映射；断连和读状态失败不等于未运行。官方原始动画沿用固件，本项目定义状态对应关系及播放方式。

## 对齐清单

| 项目 | 当前证据与接收端要求 | 状态 | 是否阻止定稿 |
| --- | --- | --- | --- |
| Codex 会话投影 | `MacTab5Codex.swift`、`MacCodexDesktopIPC.swift` 已包含 `companionState`、`waitingKind`、`outcome`、`turnId`；核对完整调用链及已有回归。等待答题和授权必须区分，混合回退，状态绑定同一 task／turn；失败、过期保留缓存并显示 unknown。 | 已完成（源码与原生候选回归；不等于全部实机通过） | 后续修改需防回归 |
| Claude 完成信号 | `MacActivitySignals` 当前仅在 `agent == codex` 时生成 `completionActive`。Windows `ActivitySignals.cs` 也有同样限制，不能声称 Windows 已完成。请先核对真实 Claude Code 主会话完成事件，在 Mac 补齐可信的完成态生成、清除和输出；给 Windows 端明确相同事件及语义以便同步。 | 待验证（待实施） | 是 |
| 活动时间与客户端归属 | Mac `MacDomesticActivity` 当前只有 `activeProvider/state/needsInput/tokensToday`；Windows `DomesticActivitySnapshot.Providers` 含 `lastActivity`。固件的客户端选择读取 `domesticActivity.providers[activeProvider].lastActivity`。补齐 Mac 可信的已有时间数据及完整 TAB5 传输路径，不用快照生成时间代替实际活动时间。 | 待验证（待实施） | 是 |
| 两客户端同时工作 | 对齐固件当前优先等待、再工作、再比较活动时间的行为；相同时间沿用当前选择，空闲回退保留用户原模型选择。检查本家模型／自定义模型、多客户端同时工作与过期数据，避免旧国产模型记录吞掉当前 Claude 的真实信号。 | 待验证（待实施） | 是 |
| 固件绘制及素材 | 两种动画、单萌宠显示、循环规则、位置、背景、底部居中时间由 TAB5 实现；不用在 Mac 再绘制一套，也不改变 Mac 的 ESP8266 镜像页面。 | 已有无需修改 | 否 |

完成信号必须来自经核对的主会话完成事件，不能从文件静默、进程退出或普通空闲推算。子 Agent 结束、会话关闭、取消和失败不能误报成功；等待输入／授权不能被完成态覆盖。新一轮明确工作应清除上一轮完成状态，事件重复或乱序不得重复触发完成动画。若当前 hook 元数据不足以实现可靠绑定，先说明缺失信息和最小兼容方案，不导出对话正文、命令、问题、工具输出或凭据。

优先复用协议 v1 的可选字段及现有 `ToolState.completionActive/completionSequence/completionAt`。国产模型活动属于受支持客户端，不代表新增客户端接入。若必须新增或改变字段，同步协议文档与 Windows 对端；不要仅改 Swift 结构体而漏掉 USB／Wi-Fi／BLE 的 TAB5 编码、筛选或裁剪。

## 验证和回传

在 macOS 执行 `swift test --package-path mac-app`、`swift build -c release --package-path mac-app` 及正式打包脚本。覆盖：主任务工作→等待→继续→完成，新任务清除完成，重复／乱序／子任务事件，失败／取消，未知／断连及恢复，两客户端同时活跃，自定义模型活动时间和过期记录，以及旧协议字段缺失。测试业务行为，不只验证字段存在。

使用实际候选包检查受影响的启动、设置／配对／草稿保留及状态输出。自动化不得打开或置顶前台窗口。真实 Claude Code 活跃切换、等待／完成以及 TAB5 对应显示需与自动化结果分开登记；做不到的明确列为待验收，不能将 CI 或合成夹具记为实机通过。

回传：修改文件、完整源码 SHA／补丁、共同字段与事件定义、原生测试命令和结果、Mac 包文件名／大小／SHA-256、安装及真实设备结果、剩余事项。Windows 同步补齐后，再从共同源码生成两平台最终候选，按 [跨平台交接模板](CROSS-PLATFORM-HANDOFF-TEMPLATE.md) 收齐证据，之后统一提交及发布。

## 原候选保留记录

原桌面候选 ID：`668ba4be493e705dc4835c67da0c894d5368d85fd19df600a784ad55928e6d88`。原生候选构建：[41779153926](https://github.com/yaoyouzhong/AI-bot/actions/runs/41779153926)。这只能证明旧候选的原生构建与回归，不证明待适配功能已经完成。

现有 TAB5 原镜像及其已通过的显示／退出／位置／背景／字体验收可以保留。Mac 适配后仅补受影响的数据与实际状态路径，不自动使未变化的固件外观验收失效。

## English status

Release is on hold until Mac adaptation and native validation are complete. Codex task metadata already exists; Claude completion signals and activity timestamps for simultaneous-client selection require implementation and cross-platform agreement. Windows shares the Claude completion gap and must align too. Preserve accepted TAB5 firmware bytes, rebuild affected desktop candidates from one final source SHA, and distinguish tests, package installation and physical-device evidence. This handoff is prepared locally; delivery to a Mac chat has not been confirmed.
