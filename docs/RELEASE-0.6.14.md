# Windows 0.6.14 发布准备 / Release preparation

> 当前提交交接（2026-11-01 晚）：主要 Windows 真机功能及本轮界面验收已由用户确认，细节见 [Windows 增量记录](development/WINDOWS-IDLE-SUBMIT-FIX-20261101.md) 和 [Mac 对齐交接](development/MAC-FOLLOWUP-FROM-WINDOWS-20261101.md)。下文旧日期、旧包哈希与“尚未验收”是历史阶段状态，不代表当前结论。长闲置、缺席 DJI/未配置账户、Mac R35 仍待验证；正式最终包/setup 需在 Mac 补齐并整合代码后重新生成和验收。今晚发布是目标，尚未执行推送、标签或发布。
>
> Current handoff (November 1 evening): the user confirmed core Windows hardware behavior and the latest UI fixes. Historical candidate packages and pending notes below are superseded by the linked acceptance log. Long-idle, unavailable microphone/accounts and Mac R35 remain pending; rebuild and validate final installers after Mac parity work. No push, tag or release has been performed.

> 2026-11-01 更新：下文 10-29 的候选与包摘要属于历史准备记录。已同步 PR #19，并继续修复 Windows 任务页回执和确认延迟；旧 setup／ZIP／源码快照不含最终改动，不能发布。当前验收结论以 [11-01 Windows 记录](development/WINDOWS-W31-VALIDATION-20261101.md) 为准：普通发送与短期回执恢复已通过，新确认流程的流畅性、首次加载、运行中持久排队等仍待验。源码尚未提交，Mac R35 未验项不由 Windows 关闭。
>
> November 1 update: October 29 packages and hashes are historical preparation records. PR #19 is included, with further Windows receipt and latency fixes. Previous setup/ZIP/source archives are superseded and must not be published. See the [current Windows acceptance record](development/WINDOWS-W31-VALIDATION-20261101.md); hardware latency and remaining scenarios are pending. No release authorization is implied.

**本地候选，尚未发布。** 2026-10-29 准备，Windows 实机验收按用户要求留到 2026-10-30。当前正式下载仍为 Windows/Mac 0.6.13，本文不能作为新版本已上线或实机通过的证明。

## 本次范围

- Windows：Codex 提问题卡、多题下一步与提交；录音开始绑定设备、会话与输入框，变化时保留预览并拒绝串题。
- 蓝色箭头提交已确认全文；Codex 可靠保存消息到队列即算成功，不等待执行；未知结果不补发。加密短期回执保留现有防重边界。
- 连续录音保留原文、换行和标点，成功填入后定位末尾；音源可选 TAB5、内置或 USB 麦克风，保留原有豆包恢复与 DJI 共享规则。
- Windows UIA 读取优化仅有本机局部测量依据：1813ms → 350–396ms。未修改历史缓存或语音等待常数，不承诺实体录音整体提速。
- 本次只发布 Windows 的准备材料；Mac、ESP8266、TAB5 和图库不重建、不随本次刷机。`desktopPlatform=windows`；共享的 Mac plist 版本按仓库校验约定同步，不表示有新 Mac 包。

## 使用步骤

在 TAB5 打开目标 Codex 会话后开始录音，结束录音会自动填入。核对文字，必要时继续录音追加；蓝色箭头才是提交。题卡上依当前题目进入下一步或提交；题目变化后若提示目标不一致，识别预览保留，先核对题目再重新录音。不要因未知结果重复发送。

入口区别：「Codex 直达」使用蓝色箭头；「Codex 任务」使用发送／追加按钮。Codex Direct uses the blue arrow; the Codex Tasks page uses Send/Append.

已有用户正常退出旧 Windows 桥接后覆盖安装，保留原配对、草稿、展示设置和图库。首次接入继续按[安装指南](INSTALL.zh.md)选择已有 AI-bot 或出厂设备；本次桥接更新不要求重新配对或刷机。

## 已有证据与待办

开发候选 `0.6.13-candidate.20261029.w29` 已安装并核对实际窗口。配对／设置／草稿的 6 个保护文件、7,151 个图库文件哈希一致；ESP8266 的六页轮播顺序和 15 秒间隔保留。新补充的故障用例只修改自测，未更换该常驻候选。

Release 构建及主要回归通过；W29 自测由 61 项增至 93 项，增加写盘失败、取消、并发重放、请求冲突、24 小时到期和 512 条容量边界。另通过冻结候选的 BLE 语音分片、设备登记、展示设置、用户偏好与配置恢复、目录保护和会话已读归属检查。全部使用隔离数据／模拟传输，不能代替真实录音和实体设备验收。

发布包与开发候选的版本元数据、自测和内嵌说明不同。明天必须对最终待发布包完成安装／状态检查及 Windows 实机验收，不能把旧开发候选的通过记录直接套用到新包。正式提交 SHA、CI、公开附件与公开页面校验只能在相应步骤实际完成后登记。Mac R35 排队实机仍待完成，不能代替 Windows 验收。

详见 [W29 验证记录](development/WINDOWS-W29-VALIDATION-20261029.md)、[发布准备交接](development/WINDOWS-0614-RELEASE-PREP-20261029.md) 和[发布收尾清单](RELEASE-CHECKLIST.md)。

后续授权更新：10 月 29 日 22:17 已使用本次冻结 ZIP 将原桥接目录更新为 0.6.14，真实版本窗口和安装后状态检查通过。107 个安装文件已校验，原程序有回滚副本；配对、草稿、设置及图库哈希保持。该结果补充上面的打包时记录；Windows 实机功能仍待 10 月 30 日验证，setup 安装向导的实际升级路径尚未执行。

## English

**Local candidate preparation; not published.** Windows hardware acceptance is deferred to October 30. Public Windows and Mac downloads remain 0.6.13.

Windows adds Codex question-card answers and Next/Submit, recording-time device/chat/input binding, exact confirmed-text submission, and encrypted short-lived receipts. Reliably saved queue entries count as delivered before execution; uncertain outcomes never resend. Consecutive dictation preserves text, punctuation and line breaks, with existing end-caret placement. Explicit TAB5/built-in/USB selection retains microphone recovery and DJI sharing. Cached UIA reads measured 1813 ms versus 350–396 ms locally; no end-to-end voice speedup is claimed.

Stopping recording automatically fills the target. Review the text, append another take if needed, and use the blue arrow to submit. A changed question retains the preview and blocks insertion; check the question before recording again. Existing installations retain pairing, drafts, display settings and galleries. This Windows-only preparation does not rebuild Mac or change device firmware. Mac source version metadata follows the shared bridge version convention; its published download stays unchanged.

The installed development candidate and local package have separate identities. Ninety-three W29 checks and the listed isolated regressions pass, including persistence failure, cancellation, concurrent replay, expiry and capacity boundaries. Final package installation, physical microphone/question/queue acceptance, release CI and public asset/page verification remain pending. Mac R35 hardware evidence is separate.

Subsequent authorized update, October 29 at 22:17: the verified 0.6.14 ZIP is now deployed to the existing bridge directory. The actual About window and installed status check pass; all 107 installed files are verified with rollback copies, and pairing, drafts, settings and gallery hashes are retained. This supersedes the earlier package-time installation status. Hardware acceptance remains scheduled for October 30; the setup wizard's actual upgrade path has not been exercised.
