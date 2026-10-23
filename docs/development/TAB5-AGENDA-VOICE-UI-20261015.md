# 日程编辑左侧与语音交互 / Agenda editor and voice UI

## 设计范围

用户认可右侧时间选择，本次只整理名称、语音和日程开关，并将 TAB5 应用入口及页面标题从“日程待办”缩为“日程”。设备当前仍运行 0.2.197-ui；此界面未刷入。

沿用现有工作台配色：背景 `#20272A`、输入面 `#2B3539`、文字 `#EFF4F2`、次级文字 `#B9C5C9`、操作蓝 `#B9DCEB`、录音绿 `#A8D6BB`。沿用现有 24／30 像素中文字库及时间数字字体，不增加内嵌字体或图片。名称左对齐，主录音按钮位于名称框下方并与其左边缘对齐，必要状态排在按钮右侧；左列从上到下为名称输入、语音操作、日程时间开关。时间列维持原布局。

语音复用 TAB5 Codex 编辑器的原生绘制麦克风和停止图标，两处共用绘制函数。圆形操作区为 72×72 像素：空闲显示麦克风，实际可收音后显示停止方块；忙碌时显示取消，识别阶段禁用主按钮。录音状态显示计时、完整麦克风名称与实际电平；未检测到声音时给出提示。键盘弹出后操作区位于名称框和拼音候选之间，主按钮位置在本轮录音中不变。

不增加手动完成或自动保存。后续按用户要求复用 Codex 的录音中手动切换入口，仅大疆实际录音时显示“改用 TAB5”，详情及候选验收见 [音源切换记录](TAB5-VOICE-SOURCE-SWITCH-20261015.md)。大疆共用及已安装版恢复的真实验收范围见 [语音记录](TAB5-VOICE-MICROPHONE-RECOVERY-20261015.md)。

## 验证

证据目录：`artifacts/development/agenda-voice-ui-20261015/`。

- 全页原生 LVGL 预览回归通过，包括 Codex 原有语音交互。
- 日程预览通过：真实指针点击启动／停止、72 像素触摸区域、录音目标位置保持、键盘锁定／恢复、图标切换、电平、完整麦克风名、识别状态、取消／错误／其他 owner／超长结果保留原名称，以及原有时间选择／保存流程。
- 可见界面检查发现键盘模式两行状态不足高，已增加至 64 像素，录音计时和麦克风名称均完整显示。现有字库覆盖检查通过。
- 新建事项右侧时间区域 `(748,136)–(1216,620)` 与修改前预览逐像素一致。
- 本地编译、固件指纹及交付状态记录于同目录 `validation.json`；编译和预览不代表新界面真实触摸验收。

关键预览：`agenda-preview/agenda-editor-new.png`、`agenda-editor-keyboard.png`、`agenda-editor-voice-recording.png`、`agenda-editor-voice-dji.png`、`agenda-editor-voice-recognizing.png`。

## 去除语音底框

用户指出语音输入无需单独框起，并要求删除“语音输入”文字、重排麦克风位置。该区域容器改为透明，无边框、无底色；空闲时只显示麦克风图标。麦克风移到名称框下方，与名称框左边缘对齐；录音时取消紧邻其右侧，计时、音源及电平显示在后方。正常／键盘模式沿用相同的左右顺序，保留 72 像素圆形触摸区。日程指针／语音状态／字库预览检查通过；右侧时间区、键盘按键和拼音候选与上一版逐像素一致。新预览及编译记录位于 `artifacts/development/agenda-voice-flat-20261015/`，此调整未刷入设备。以上旧预览保留用于前后对照。

## English

The user accepted the right time column. This change only reorganizes the left name/voice/schedule controls and shortens the TAB5 app/header label to “日程”. It preserves the existing palette, 24/30-pixel CJK fonts and time-digit face, adding no embedded fonts or images. The installed firmware remains 0.2.197-ui; this UI has not been flashed.

The left column reads name, voice, then schedule toggle. Voice shares the existing TAB5 Codex microphone/stop drawing helper. Primary and cancel hit targets are 72×72 pixels. Recording shows elapsed time, full input name and actual input level; recognizing disables the primary control while keeping cancel available. With the keyboard open, these controls stay above the IME candidates and retain a stationary stop target. The existing draft/owner/error protections remain in place, with no automatic save. A subsequent candidate adds the user-requested Codex source-switch entry only during DJI recording; see the linked source-switch record for validation and deployment status.

Full native UI regression and agenda pointer/state/glyph checks passed. Visible review found and fixed insufficient height for the keyboard-mode two-line status. The right time region is pixel-identical to the prior preview. Local compile and firmware identity are recorded in validation.json; previews/builds do not establish physical acceptance of the new layout.

At the user's request, the voice container has no visible frame/fill or idle text. The microphone sits below and aligns with the left edge of the name field. During recording, cancel appears beside it, followed by elapsed time, source and level. Normal and keyboard modes preserve this horizontal order and 72-pixel circular hit targets. Agenda pointer/state/glyph preview checks passed; the right time area, keyboard keys and IME candidates are pixel-identical to the previous version. Updated preview/build evidence is under agenda-voice-flat-20261015; this correction remains unflashed, and earlier previews are retained for comparison.
