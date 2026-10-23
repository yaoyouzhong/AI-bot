# TAB5 197 本机验收与音源隔离 / Local acceptance and audio isolation

## 已安装与核验

- 桥接 0.6.7 固定 Release 目录更新，82 个交付文件逐项校验；旧程序和用户配置备份在本机 LocalAppData 下。
- TAB5 0.2.197-ui：BIN SHA-256 `c6fd83ac63ab0f3252f7191694313a6f1f31be8556489808de79e9ce26686302`。
- 真机启动核验：ELF SHA-256 `0b719858790b6956d66aa83038a455ff8928e0140ffa73ac01f0d20d5d38e761`，`ota_0 / VALID`，连续三次 UI 心跳通过，显示错误、LCD 欠载均为 0。
- 本次 Wi-Fi 压缩传输 4,532,699 字节，桥接记录传输 13,563 ms；设备记录 installMs=27,724。这些是不同阶段计时，不作为全部升级体验耗时。
- settings、devices、两个配对文件、Codex 配置与备份哈希一致；自动轮播保留原 6 页、顺序和 15 秒。
- USB 后来显示未连接，用户确认是主动拔线；Wi-Fi 在线、蓝牙数据已确认。不能把 USB 未连接记为固件缺陷。

证据：`artifacts/development/tab5-197-device-20261015/`，尤其 `diagnostic-boot-verified.json`、`profile-preservation.json`。持续采样在用户拔线后未完成，不能报告为稳定性验收通过。

## 用户反馈与即时隔离

- 用户反馈：无线语音报不支持；电脑正在使用大疆时，TAB5 输入频繁改变豆包音源。
- 现场豆包为 0.9.2.7；桥接语音请求鉴权通过，但此构建不在 native 控制适配表，退回 USB HID。仅更新适配表不能解决音源共用。
- 现有 `Tab5DoubaoMicrophone.AcquireAsync` 临时选择 CABLE Output，结束后通过租约恢复；即使恢复成功，期间仍会影响电脑大疆输入。
- 正常退出桥接，确认恢复租约不存在、豆包选中可用的 `麦克风 (Wireless Mic Rx)`；备份本机语音配置后仅将 `Enabled` 设为 false，保留原麦克风和快捷键偏好，再从固定目录启动桥接并恢复轮播。
- 未修改系统默认音频设备、未结束用户的豆包录音、未修改豆包程序文件、未部署新兼容表、未启用外部识别服务。后续方案需确保 TAB5 收音识别不切换电脑豆包的麦克风选择。

隔离证据：`artifacts/development/voice-doubao-0927-20261015/audio-isolation.json`、`bridge-isolated-live.json`。PE/只读查询记录仅用于兼容诊断，不能算真实语音识别成功。

后续按用户确认的轮流共用方式安装了本机语音热修复并重新启用：大疆直连时不切通道，内置麦克风才借用／恢复音源；恢复失败自动重试，且按用户要求取消豆包版本白名单。以上“当前不支持”及“停用”描述保留为当时隔离记录；最新交付、测试及仍待验收范围见 [语音热修复记录](TAB5-VOICE-MICROPHONE-RECOVERY-20261015.md)。

## 键盘弹出布局

维持原色彩、字库与时间选择布局。语音按钮从名称上方移至名称框下方、拼音候选栏上方；触摸高度 40 → 72 px，宽度 180 → 252 px。录音、结束、取消保持同一行。录音期间保留键盘画面并禁用按键及候选，完成后恢复，避免覆盖语音草稿。

原生 LVGL 已验证按钮下沿实际指针点击、录音中坐标不变、键盘禁用及恢复、原有保存/时间选择/语音失败保留流程；全页预览回归通过。预览位于同一证据目录的 `ui-preview/`。此项是本地修改，设备仍为 197；没有刷入或更新 canonical latest。

本地以 `0.2.198-ui` 编译验证通过，镜像 6,685,440 字节，应用分区剩余 523,520 字节（511.25 KiB）。日志为同一证据目录的 `idf-build.log`；此版本仅为本地编译产物，未登记交付、未刷入，canonical latest 的 197 镜像哈希保持不变。编译及模拟界面验证不代表真实语音识别通过。

后续左侧优化另见 [日程与语音布局记录](TAB5-AGENDA-VOICE-UI-20261015.md)：应用名缩为“日程”，语音按钮改为共用 Codex 图标的 72 像素圆形按钮，显示计时、麦克风名称和电平。右侧时间区逐像素保持一致。该次本地 198 编译镜像为 6,686,592 字节，分区剩余 522,368 字节；仍未刷入，197 canonical 指纹不变。用户已完成 USB 拔掉时的大疆录音回填及电脑音源保持验收；内置麦克风恢复单独待验。

## English

Bridge 0.6.7 and firmware 0.2.197-ui were installed locally. Exact ELF identity, ota_0 VALID and three advancing UI heartbeats passed with zero display errors/underruns. Configuration hashes and the original six-page, 15-second auto carousel were preserved. The user confirmed physically unplugging USB; Wi-Fi and authenticated BLE remained available. Extended stability, actual touch, reminders and dictation remain separate acceptance items.

The installed Doubao 0.9.2.7 is outside the native-control compatibility list. More importantly, the current shared desktop IME path temporarily selects CABLE Output and can interfere with simultaneous DJI use. The bridge was exited normally, Wireless Mic Rx selection and completed lease restoration were confirmed, and only this user's TAB5 voice Enabled setting was disabled before restarting the other bridge functions. No OS audio default, third-party binary or independent recognition provider was changed.

A subsequent local hotfix re-enabled voice with direct shared DJI input, journaled restoration only for built-in TAB5 capture, automatic recovery retries and no version allowlist, as requested by the user. The paragraph above records the earlier isolation state; the linked voice-recovery record supersedes it for the current delivery and pending acceptance.

The keyboard-mode voice controls are now lower and larger in source/native preview, with stationary recording/stop controls and locked keyboard input during a take. They have not been flashed; 197 remains the canonical installed candidate.

Local compile validation as 0.2.198-ui passed: the image is 6,685,440 bytes with 523,520 bytes (511.25 KiB) free in the app partition. This is a local build only, without delivery registration or flashing; the canonical 197 image hash remains unchanged. Compilation and simulated UI validation do not establish actual speech-recognition success.

The subsequent left-column refinement is recorded separately in the linked agenda/voice UI note: the shorter “日程” label, shared Codex icons, 72-pixel circular controls, elapsed time, full microphone name and input level. The right time region remains pixel-identical. Its local 198 build is 6,686,592 bytes with 522,368 bytes free, still unflashed and without changing the canonical 197 identity. The user has since accepted wireless DJI dictation/final insertion with unchanged desktop microphone selection; built-in restoration remains a separate physical test.
