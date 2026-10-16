# 复用 Codex 手动切换音源 / Reusing the Codex source switch

## 行为与范围

Codex 已有“改用 TAB5”按钮，经 `voice_remote_command(3)` 发送既有 `op=tab5` 请求。日程编辑复用同一请求及桥接会话，不增加协议字段或切换版本白名单。日程仅在大疆实际录音时显示切换入口；切换过程中禁用主按钮，保留取消，切到内置后收起切换入口。空闲仍只显示麦克风图标，名称／时间布局不变。

开始录音时，没有可用的大疆输入就使用内置麦克风。接收器在线、但发射器关闭或静音时，Windows 仍可能报告可用端点；普通静音不会触发自动切换，此时可手动点“改用 TAB5”。识别仍需启用的电脑桥接及内置音频通路。

大疆直连模式不经过虚拟声卡，原来的即时切换不能只搬一个按钮。配套桥接现在先停止自己拥有的大疆录音，等豆包空闲并确认已提交文字稳定，再临时借用 CABLE Output，开启内置麦克风段。保留同一 voiceId、owner 和累计时限，不清除前段文字；再次明确结束后返回合并草稿。内置段结束、失败或取消后复用已有恢复租约和退避重试，归还原豆包音源。旧的虚拟通路模式继续使用原切换路径；大疆直连录音中意外断开仍结束本轮并保留草稿，不自动重启识别。

保留现有 wire state，切换准备时 ready=false，内置 PCM 到达前不提示可说话。不自动保存日程、不发送 Codex。旧会话请求不得停止或读取当前录音，重复切换不得重开录音或重复借用通道。

## 本地验证及交付边界

证据目录：`artifacts/development/agenda-source-switch-20261015/`。

- Windows Release 构建通过，0 警告、0 错误；独立输出的项目 `--status-once` 通过。
- `--self-test-tab5-voice-core` 通过，包括既有加密 HTTP／USB／BLE 参数测试、回填和音频生命周期回归。新测试对 Codex 和日程两个 owner 各覆盖成功、准备失败、重启失败、准备前取消、准备后取消、token 取消、等待超时及失焦；使用真实恢复租约类与明确的音频／IME／设置替身。
- 新用例确认先提交前段文字再借用音源、同一身份及完整前后段文字、收到内置 PCM 后就绪、重复／旧会话隔离，以及成功／失败／取消后的音源归还。替身验证不等于实际豆包切换成功。
- 日程原生 LVGL 指针测试验证切换按钮确实发送 command 3，等待时不停止／回填／保存草稿，内置录音就绪后切换入口收起，主按钮和键盘保持原位；字库覆盖检查通过。
- 固件编译和精确哈希另存同目录 `validation.json`。没有更新运行中的桥接，没有刷入 TAB5 或更新 canonical latest；实际内置录音、两段文字合并与豆包音源恢复仍待部署后的真机验收。

## English

Agenda reuses the existing Codex command 3 / op=tab5, with no wire-schema change or IME version allowlist. The control is visible only during actual DJI capture, hides after switching, and retains cancel while preparing. Idle UI remains icon-only. No available DJI input at start selects the built-in microphone. An online receiver can remain available when its transmitter is off/muted; silence alone does not switch sources.

Direct shared DJI capture bypasses the virtual cable. Its handoff now ends only the owned DJI segment, waits for confirmed idle recognition and stable committed text, then borrows CABLE Output and starts the built-in segment. It preserves voice identity, owner, first-segment text and the total time limit. Explicit final stop returns the combined draft; no agenda save or Codex send occurs. Readiness remains false until device PCM arrives. Completion/failure/cancel reuse the existing journal and restoration retries. Unexpected direct-DJI disconnection still ends the take instead of automatically restarting recognition.

Release build, project status-once, existing voice regression and native agenda pointer/glyph checks passed. New synthetic scenarios cover both owners, successful concatenation, failures before/after borrowing, cancellation, timeout, focus loss and duplicate/stale requests using the production recovery journal class. Build identities are recorded in validation.json. This candidate is neither deployed nor flashed; real built-in capture, combined transcription and microphone restoration still require physical acceptance.
