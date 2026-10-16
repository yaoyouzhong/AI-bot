# TAB5 语音音源恢复与连接窗口 / Voice recovery and connection layout

## 本机交付

- 本机桥接 0.6.7 热修复，未创建公开版本、标签或推送。
- 已更新固定 Release 目录中的 `AIBotBridge.dll` 与 PDB；DLL SHA-256：`1952c3c05a14a9ecdb24354759d3cf07b5a6df2fe29625c14add7428f1a9a05c`。依赖包版本、原有 win-x64 串口运行库及资源集合保留。
- 正常退出旧桥接，备份程序及本机配置后替换；运行 `--restore-cycle-default` 再正常启动。本机语音 `Enabled=true`，原设备选择与快捷键保留。
- 原 6 个循环页面、顺序及 15 秒间隔不变，`selectedMode=auto`、`cycleEnabled=true`；小屏保持 0.5.3、USB 在线且状态计数继续增加。
- TAB5 USB 当前拔掉，Wi-Fi 通信与蓝牙数据确认正常。设备仍运行 0.2.197-ui，键盘语音按钮布局修改没有刷入。

## 语音行为

- 大疆已连接且豆包选中同一端点时，TAB5 与电脑共用该物理输入。桥接只通过共享模式采样做电平检测，不播放到 VB-CABLE，不切换豆包音源，也不创建恢复租约。若电脑选择的是其他麦克风，明确提示选择同一个大疆，不替用户静默切换。
- 开始时大疆不可用，才使用 TAB5 内置麦克风：先原子保存原音源与临时音源，再切到 CABLE Output；结束、取消、失败或准备被取消后，在确认豆包空闲时恢复并读回核对。
- 恢复失败保留租约，按 1／3／10／30 秒间隔重试，随后每 30 秒继续尝试；桥接重启也会恢复待处理租约，语音关闭时同样处理。新录音等待在途恢复结束；用户手动选择其他音源时保留其选择。
- 在激活隐藏草稿框、切换音源之前检查豆包是否正在录音或识别，不接管其他会话。大疆录音中断开会结束该轮并保留文字；重新开始时可使用 TAB5，避免录音中切换共享音源。
- 按用户最终要求，移除固定版本／文件指纹白名单。从已运行豆包服务所在目录加载本机 RPC，按真实状态及启停返回值判定；升级后先沿用，发生实际调用错误再提示和修复。不分发或修改豆包二进制。
- 固件 197 的日程 owner 曾显式请求内置麦克风，桥接现对该 owner 使用与其他语音相同的大疆优先策略，不要求为此再次刷 TAB5。

## 验证与边界

证据：`artifacts/development/tab5-microphone-recovery-20261015/`。

- Release 构建：0 警告、0 错误；按独立输出目录运行项目 `--status-once` 成功。
- 核心自检：恢复失败与退避、读回不一致、启动恢复、取消／失败状态、忙碌拒绝、手动覆盖、切换已生效但确认丢失、并行恢复与新录音串行化、损坏租约保留。替身验证不算真实识别成功。
- 实际 WinForms 草稿前置检查测试：忙碌或 RPC 不可用时不创建窗口句柄、不发送快捷键、不改麦克风。
- 真实大疆共享输入：收到数据、`InputReady=true`，虚拟输出 `closed`，与豆包当前端点相同，音源前后未变；未启动豆包录音，未保存音频。
- 当前豆包 0.9.2.7 的运行时只读查询通过；更新桥接后仍选中 Wireless Mic Rx，恢复状态为 restored、失败计数 0。
- 真实无线大疆验收：用户保持 USB 拔掉，在日程编辑页录音并结束，明确反馈“回填正常，电脑仍用大疆”。同时桥接记录 `phase=review`、`source=dji`、`sound=True`、虚拟输出 `closed`、`trigger=native control (runtime checked)`、`restoreFailures=0`。原始诊断保存为 `tab5-live-latest.txt`。
- 内置麦克风临时切换及归还仍待真实触屏验收。已安装版没有日程录音中的手动切换入口；后续本地候选已复用 Codex 入口并补齐直连大疆切换逻辑，尚未部署，见 [音源切换记录](TAB5-VOICE-SOURCE-SWITCH-20261015.md)。大疆意外中断时仍结束本轮，重新开始后选择可用音源。

## 连接窗口

保留微软雅黑与原白／蓝配色，采用连接选择、简短解释、三行通道状态、屏保等待、固定底部操作区。内容不滚动；延后初始 DPI 布局，避免小窗口配大字号。正常／最小窗口及几何缩放预览通过，另检查屏保输入与底部按钮不重叠。预览在证据目录 `connection-preview/`，不把缩放模拟称为真实显示器 DPI 切换。

## English

The local 0.6.7 hotfix is installed, with the DLL hash above and the original win-x64 serial runtime/dependency packages retained. Voice is re-enabled. The original six-page, 15-second auto carousel and pairings are preserved; ESP8266 remains 0.5.3 with advancing USB traffic. TAB5 remains 0.2.197-ui with USB unplugged; its separate keyboard-button changes have not been flashed. No public release, tag or push was made.

DJI mode directly shares the endpoint already selected in desktop Doubao, using shared capture for metering with no virtual output, channel changes or recovery journal. Only built-in TAB5 mode borrows CABLE Output and restores the journaled previous selection after completion/cancel/failure. Failed restoration retries with backoff and resumes on startup; manual choices win. Busy recognizers are refused before focus changes. A mid-take DJI disconnect ends that take while preserving text; restarting can use the built-in input. Agenda owner requests from firmware 197 receive this same DJI-first policy.

Per the user's final instruction, version/hash allowlists were removed. The installed service RPC is used across upgrades and evaluated by actual runtime replies. Real incompatibility errors are surfaced rather than assuming a new version is broken. Synthetic fault/lifecycle tests, actual DJI metering without channel changes and current-build read-only RPC queries passed. With USB unplugged, the user confirmed successful DJI recording/final insertion while desktop Doubao retained DJI; matching diagnostics show review, DJI, detected sound, closed virtual output and zero restore failures. Built-in-channel borrowing/restoration still needs physical acceptance. The installed agenda UI lacks a manual switch; a subsequent local candidate reuses the Codex entry with direct-DJI handoff but remains undeployed, as recorded in the source-switch note.

The ESP connection dialog now exposes all settings without scrolling, with readable channel rows and a fixed footer. Default/minimum and geometry-scale captures plus footer-overlap checks passed; these do not establish actual multi-monitor DPI-transition acceptance.
