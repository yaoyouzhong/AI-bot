# 日程待办候选 / Agenda candidate

此功能对应相邻 AI-bot 工程的 `docs/AGENDA.md` 和协议中的 Agenda v1。2026-10-15 已安装 0.2.197-ui；后续入口短名称“日程”及编辑页左侧优化尚未刷入。设备实现为 `agenda_core.c`（确定性规则）、`agenda.c`（NVS／RTC／加密 RPC worker）、`ui_agenda.h`（原生 LVGL 时间轴）。最多 16 项，不要求 TF 卡。电脑和 TAB5 均可新建／编辑名称、开始／结束时间、重复及提醒；列表仅显示开始时间，结束时间在编辑页设置并参与自动结束，到结束时间自动结束本次；重复事项转到下一次。无手工完成入口；延后 10 分钟不得达到或越过结束时间。跨午夜有效，过期提醒不补响。NVS `agenda_v3` 记录 4272 字节，包含待同步定义及原始编辑版本；结束状态按时钟计算，不额外写入 Flash。RTC 复用原有受校准保护的本地时钟。

独立原生测试目标为 `agenda-test` 和 `agenda-runtime-test`。前者读取 Windows `--self-test-agenda` 生成的 `scheduler-fixtures.json`；后者执行实际 worker 的保存、加载、去重和同步代码，使用明确的硬件替身。`TAB5_AGENDA_FIXTURE` 指向同一桥接测试生成的 `device-sync.json` 时，`ui-preview` 渲染并验证日程页面。替身及预览不代表实机验收。

Items automatically end at their configured end time; repeats advance, overnight events remain active until their end, and expired notices are suppressed. Snooze must remain before end; manual completion is absent from both UIs. The local candidate uses deterministic scheduler rules, a persistence/RPC worker and a native LVGL timeline. It reuses the calibrated RTC, stores up to 16 items in a separate NVS key and needs no SD card. The paired AI-bot repository owns the feature/protocol documentation. Native tests and hardware substitutes do not establish physical-device acceptance.

编辑页复用独立中文拼音输入法，创建顺序确保 Codex 默认输入法不被占用。名称语音识别由已启用的 Windows 桥接完成，优先与电脑共用豆包已选择的大疆，无大疆时使用 TAB5 内置麦克风；无电脑时可触屏键盘离线编辑。录音／识别期间禁止保存名称，结束录音得到完整文字后只回填草稿。取消、失败、超长及其他 owner 的返回均保留原名称，不自动保存或发送 Codex。用户已在 USB 拔掉时确认大疆回填正常、电脑保留大疆；内置麦克风切换／恢复仍待真机验收。

编辑页将名称／语音与时间设置分列；开始／结束两行点击后才展开月历与时分滚轮，提供今天／明天及 30 分钟／1 小时／2 小时时长快捷入口。修改开始自动平移结束并保留时长，取消选择不改草稿，错误结束时间留在选择器修正。`agenda_ui_test.h` 使用实际 LVGL 指针事件验证日期点击和滚轮拖动，并检查标签字库覆盖。

后续左列按名称、语音区、日程开关排列，右侧时间布局不变。语音使用与 Codex 编辑器共用的麦克风／停止图标和 72 像素圆形按钮，录音显示计时、完整麦克风名称及电平；取消不回填，识别时禁用主按钮。键盘弹出后仍可在名称框下方操作，录音期间按钮位置固定。全页／日程原生预览与本地编译通过，不代表这版新界面已经刷入或实机验收。

日程现复用 Codex 的“改用 TAB5”按钮和 command 3，仅大疆实际录音时显示，切换后收起。配套桥接先取回前段文字再启动内置段，最终一起回填；内置通道用完恢复原豆包音源。该候选尚未部署，原生测试覆盖真实按钮点击、切换等待及入口收起，不代表实际音源切换已验收。

Local definitions persist before acknowledgment and sync bidirectionally using base editions. Concurrent host/device edits are retained as two copies; unsupported, full or unconfirmed responses preserve pending device definitions. Device name dictation uses an independent owner through the enabled Windows bridge, preferring the same DJI endpoint as desktop Doubao and falling back at start to the built-in microphone. With USB unplugged, the user confirmed name insertion and an unchanged desktop DJI selection; built-in microphone restoration still awaits physical acceptance. Firmware 197 is installed; the shortened app label and subsequent left-column UI remain unflashed. Offline touch-keyboard editing and RTC reminders remain available. No automatic Codex submission or agenda save occurs after dictation.

The editor shows separate name and time columns, with a calendar/roller picker opened only on demand. Today/Tomorrow and common durations reduce repeated input. Moving the start preserves duration; cancel leaves the range intact, and invalid ends remain open for correction. Native pointer tests exercise date taps and roller dragging, and check displayed label glyphs.

The subsequent left-column refinement orders name, voice, then schedule toggle, preserving the right time column. It shares the Codex microphone/stop drawing helper, uses 72-pixel circular controls, and displays elapsed time, full source name and input level. Cancel preserves the original name and recognizing disables the primary button. Keyboard-mode controls stay below the name with a stationary stop target. Full native regression, agenda previews and local compilation passed; the new layout remains unflashed.

Agenda also reuses the Codex source-switch button and command 3 during actual DJI recording, hiding it after handoff. The paired bridge commits the first segment before starting built-in capture and returns the combined draft after explicit stop, then restores the original input. This pair remains undeployed; native button/wait/hide checks do not establish real source-switch acceptance.

## 204 retention clarification (2026-10-16)

当前待部署 204：过期事项保留，只在已有 16 条且新增时清理最早结束的一条；“全部”展示已结束记录。新建和删除回执原子落盘，失败保留全部记录，连接后补同步；协议要求 retentionSupport=2。沿用 203 的可变长度紧凑 NVS 格式，最大 3,574 字节；本页上文 4,272 字节与未部署状态属于早期历史描述。203 已通过实际启动、设置保存、离线重启保留及离线删除；204 容量新规则尚待部署验收。

204 retains expired entries until a full-list insertion needs one slot, and All displays them. Atomic replacement includes a durable eviction receipt; sync requires retentionSupport=2. The 203 compact format (3,574-byte maximum) is unchanged. Earlier fixed-size and uninstalled descriptions above are historical. 203 physical storage checks passed; 204 remains a local candidate.
