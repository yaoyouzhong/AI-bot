# TAB5 语音断线恢复（0.2.89-ui）

当前结论：089 启动核验及 V6-R1/R2/R3 均已通过，断线结果回收缺口在本轮测试条件下关闭。用户确认取消恢复保留原草稿和光标，新录音未混入旧文字，也未自动发送。用户已确认测试清理和恢复设置，23:19:32 +08:00 桌面收尾回执及随后现场复核确认原配对、正式桥接和原自动六页/15 秒轮播保持，USB 在线、语音 idle，收尾完成。不承诺恢复未传出的音频。以下部署及验收过程记录保留历史状态，以本段结论为准。

## 问题及修复

V6 实机验收曾出现 USB 断线前约 5 秒的语音文字没有回到 TAB5，而重连后新一轮录音正常。088 在请求失败时结束 worker，原 voiceId 随局部变量释放；桥接结果在最后一次请求后 30 秒清理。未证明所有断线前音频都已到达电脑，不能承诺恢复未传出的声音。

089 在已有 voiceId 的请求失败后停止收音、关闭音频资源并丢弃未确认的发送队列，保留本轮任务、原草稿和光标锚点，等待原通道重连。只对原 voiceId 执行幂等 stop/poll，不重发不确定音频、不重新 start、不自动发送消息，也不跨 USB/Wi-Fi/BLE 自动切换来恢复。回收结果要求 voiceId 与 taskId 同时匹配，避免旧会话文字进入新会话。

恢复等待窗口为 120 秒；已发出的底层 RPC 仍受原 75 秒请求上限约束，取消在当前请求返回后生效。恢复期间可点取消还原原草稿和光标；设备不会重新收音。成功时提示“已取回识别文字，本段可能不完整，请检查后补录”，空结果、过期或超时明确提示未完整保存，已有草稿及已显示的部分文字不被空结果覆盖。没有取得 voiceId 的启动中断无法查询，直接提示重新录入。

Windows 桥接在失去设备心跳 4 秒后先处理已收到的播放队列，再结束识别；排队处理仍使用现有 3 秒上限。结果在最后一次有效请求后保留 5 分钟，以覆盖设备检测请求超时及等待重连。保留范围是当前桥接进程中的当前语音会话；新一轮开始、主动取消和桥接重启不承诺保留旧结果。没有修改电脑端失焦处理。

相邻 TAB5 仓库修改 `firmware/main/voice_remote.c`，增加可隔离测试的 `voice_recovery.c/.h`；现有 UI 原光标插入逻辑保持。桥接修改 `Tab5VoiceSession.cs`。协议继续使用已有 stop/poll 与 voiceId，不增加字段或端点。

## 已完成验证

- Windows Release 构建零警告/错误，status-once 成功；语音回归通过，包括失联时播放尾段处理、超过 75 秒后原结果取回、重复查询不重复启动/播放、旧 ID 不影响新录音及保留期限后清理。合成音频/IME 不替代真人录音。
- 原生 C 恢复模块回归通过：延迟重连、stop 回执丢失、离线取消、请求返回时取消、不同 voiceId/taskId 拒绝、空结果及超时，检查请求仅含 stop/poll。
- 实际 LVGL 麦克风点击和界面回归通过：恢复期间控件保护/可取消、切页后原光标恢复插入、重复结果不重复插入、取消恢复原草稿、不自动发送；088 原光标场景与完整 LVGL 预览均通过。已检查恢复中/恢复后截图，提示完整显示。
- ESP-IDF 构建成功（原 `weather_icon_create` 未使用警告仍在）；显示 IRAM 与 BLE 接收栈 ELF guard 通过。日志位于 `artifacts/firmware/tab5/build/voice-recovery-*.log`，guard 及预览证据位于 `artifacts/firmware/tab5/previews/0.2.89-ui/`。

## 本地候选及部署边界

配套桥接已于 23:03:42 +08:00 通过桌面脚本部署，用户确认且运行 DLL 与候选 SHA-256 `71CEA30DD4697281796831C77D60826B81981471AA0B0B8141D7D84383963E25` 一致；配对未变，USB/BLE 正常，原自动六页/15 秒轮播保持。用户随后确认 089 升级完成，23:07 现场版本为 0.2.89-ui、重启后约 38 秒、显示错误/欠载为 0；启动核验仍为“尚未核验”，ELF/分区核验及真机 V6 仍待完成。

- 桥接候选及安装脚本：`artifacts/development/voice-recovery-089/`。`install-from-desktop.cmd` 按状态端口识别运行桥接，验证桌面配对和候选哈希、正常退出、备份 DLL/PDB 后更新，恢复原自动轮播并启动。具体候选哈希见 deployment.json；须由桌面运行以保持原配对配置。
- 固定固件：`artifacts/firmware/tab5/latest/aibot_tab5.bin`，0.2.89-ui，6644480 字节，SHA-256 `32ed1549f91f71d7f294cb1a47d1c06e80de19994b262d02106fdca8168c358a`，ELF `6d2251a20fb8840a5c511eaa3bc9e382cd7951cf969e87a957521bcdbcd111d7`。
- 历史包：`artifacts/firmware/tab5/versions/0.2.89-ui/32ed1549f91f-6e3553fd/`。镜像及分类说明由统一准备脚本验证。刷机仍需本次候选的明确授权。

## 真机复验（更新桥接、刷入并核验启动后逐项进行）

089 启动核验已通过：用户确认且现场 verified、ELF 与候选一致、ota_1/VALID、previousError=0、完整 6644480 字节。USB COM9 在线；以下语音断线恢复步骤均已取得用户通过反馈。

V6-R1 通过：用户确认短时断线后“取回了录入的文字，提示也正确”，随后补充确认原光标插入位置正确、无重复且无自动发送。V6-R2 通过：用户明确确认断线 45 秒后仍取回文字、原光标位置正确、无重复且未自动发送，覆盖超过旧 30 秒保留窗口的恢复。V6-R3 通过：用户确认取消恢复保留原草稿和光标，重连后新录音不混入旧文字、未自动发送。

1. V6-R1：仅 USB、保持独立供电；原草稿“今天去公园”，光标放“去|公”处，录音说短句约 5 秒后拔 USB。确认停止收音、提示重连取回文字，草稿保留；等待约 10 秒后接回 USB，确认自动取回已识别内容并插在原位置、不重复、不发送。逐字记录回收结果，不因部分恢复就认定全部音频完整。
2. V6-R2：保持同条件，断线约 45 秒再接回，验证超过旧 30 秒结果保留期仍可回收。期间不操作电脑以排除电脑端焦点变化。
3. V6-R3：断线后点取消恢复，确认原草稿和光标恢复；接回后等待电脑结束旧识别，再录一段，确认无旧文字迟到混入。
4. 验收结束恢复自动连接及原循环展示。任何失败保留现场，不自动重发消息或把软件测试当作真机通过。

## English

Version 0.2.89 retains the current voice session and original insertion anchor after a transport failure, stops capture, and retrieves the existing result over the original transport using stop/poll. It never replays uncertain audio, starts a replacement recording, or sends a message automatically. The bridge drains received playback before recognition ends and retains the current result for five minutes without requests. Returned text is explicitly marked potentially incomplete. Software checks, deployment, boot verification and all three user-confirmed USB recovery scenarios passed. The user confirmed test cleanup and restored settings; the desktop finish receipt and live checks confirmed unchanged pairing, the accepted bridge, an idle voice session, and the original automatic six-page cycle at 15-second intervals. Closeout is complete within these tested scenarios.
