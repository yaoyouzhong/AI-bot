# 084 验收修复候选 / Acceptance fixes candidate

2026-10-02。桥接修复与 TAB5 0.2.84-ui 已部署，16:39 完成真实 USB 启动/镜像/分区核验及桌面收尾。用户确认 TAB5 语音误触、切页返回、停止后草稿以及取消后立即重试、亮度数字实时更新及保存均正常；独立 USB 拔插和 BLE 设置耗时仍待逐项功能复验，不代表全部验收通过。根版本仍为 0.4.0。

## 修改范围

- TAB5：输入框失焦不结束录音；移除离开 Codex 页就发送取消命令的逻辑。语音继续绑定原任务，任务、项目、通知入口维持录音期间不可改绑的保护。离页完成的文字仍进入原草稿，不自动发送。停止、取消、断线与 60 秒限制保留。用户已明确暂不处理电脑端失焦，因此电脑端 800 ms 宽限和恢复输入焦点的新增逻辑已撤回。
- 透明输入：正常准备、录音、重试恢复透明且不显示任务栏入口；故障恢复及手动设置仍可显示窗口。这只约束桥接自有输入窗口，不控制豆包自身浮窗。
- 重试：下一次准备已获得合法输入焦点后，先确认上一轮自有豆包识别结束，再更换控制器；保留占用判断，不能停止他人的识别。
- USB：串口读取消而桥接生命周期未取消时按断线重试，避免 USB 状态或遥测循环永久退出。真实关闭仍正常退出。用户保留的黄色现场中，COM9 存在，状态时间停滞，RPC 报 SerialStream 的 OperationCanceledException，线程快照无活跃 USB 读循环；自动恢复仍须用候选重启设备验证。
- 亮度：本机设置显示百分比并随滑块即时更新，松手持久化；远程设值也更新数字。音量已有显示，无须重复修复。
- 蓝牙设置等待：读写请求立即发布，待处理设置优先于纯指标帧，减少等待下一次周期发布的延迟；尚无真机耗时对比，不承诺秒级响应。

此前“输入焦点已变化”的提示由电脑桥接产生；本次 TAB5 页面取消修复并不能证明它就是那次现场提示的原因。复验须区分设备误触与电脑输入焦点变化。

## 验证与候选

本地证据目录：`artifacts/development/acceptance-fixes-084-20261002/`（忽略的开发产物，不作为公开发布包）。

- Windows Release 构建 0 警告、0 错误；语音测试含实际 WinForms 透明/故障可见/重试透明检查、合成语音及控制状态检查。设置、USB 重试循环、BLE、BLE 语音、RPC、遥测测试通过。
- `dotnet run ... --status-once` 在当前 GUI 宿主报控制台句柄错误，未记为通过；直接运行候选 Release DLL 的 `--status-once` 成功，输出为合法 JSON。
- TAB5 ESP-IDF 构建通过；完整 LVGL 预览通过，覆盖输入框失焦、空白点击、切页返回、任务保护、后台完成、显式停止/取消、亮度实时显示和松手保存。预览截图确认亮度 62% 显示完整。设备设置及语音控制原生测试通过。
- 候选 ELF 显示 ISR/Flash 安全检查、BLE 接收栈静态检查通过；这不能代替真实运行栈余量、蓝屏和长期稳定性检查。
- 两仓库 `git diff --check` 通过，保留原工作区其他未提交变更。没有提交、推送或公开发布。

冻结候选：

| 文件 | SHA-256 |
| --- | --- |
| `aibot_tab5-0.2.84-ui.bin` | `641FEE7F6A2F6A18DAF57B70B30F1B5D47CD847300BCAE7F4FB15DAFDBF6BAC5` |
| `bridge/AIBotBridge.dll` | `650B1F48F1B80DB873F5273469FA4BF91008ACAA052923D79EBBED6E962D9A88` |

桥接运行需要候选 `bridge/` 目录的配套文件，不能单独复制 DLL。固件仅针对已配对 TAB5，不能用于 ESP8266。部署前核对设备身份和镜像，完成后恢复原自动轮播、页面、顺序及间隔。

### 刷机准备记录（16:27，中国标准时间）

用户要求“准备刷机”，本轮未停止桥接、未替换程序、未写入设备。候选及配套更新说明通过真实 `Tab5OtaPackage.Load` 校验；镜像为 6,641,456 字节，OTA 槽位 7,208,960 字节，分区布局检查通过。系统枚举确认 COM9 / VID_303A&PID_4005 的 USB 设备存在，但桥接设备诊断仍停留在 14:47:32；正式写入前必须恢复桥接并取得新鲜的设备协议身份与状态，不能仅凭 COM 号或缓存的“已连接”继续。

已在仓库外 `C:\Users\yaououzhong\AppData\Local\AI-bot-upgrade-backups\084-20261002-162742` 保存并逐文件核对原桥接 49 个文件、8 个本机配置文件，以及已知 0.2.83-ui 回退镜像。配置备份含本机配对资料，只保留在用户本地，不进入源码或交付包。此备份不是设备 Flash 全量读取备份。

准备清单位于证据目录 `flash-preparation.json`。候选 26 个配套文件中仅 DLL/PDB 两个文件与当前运行目录不同。下一阶段顺序：正常退出桥接，按哈希部署差异文件，恢复原自动轮播并启动，核验 USB 实时身份/空闲状态，随后经现有应用 OTA 更新入口提供 084 镜像，核验新版本、ELF 指纹、有效启动分区及真实界面，最后逐项复验。原页面、顺序和 15 秒间隔已保存且复查未变。

Preparation only: the image and notes pass the production loader; the original runtime/configuration and known 083 rollback image have verified local backups outside the repository. No runtime replacement or device write occurred. COM9 is present, but cached USB health is stale; fresh protocol identity and readiness must be checked after bridge recovery before OTA. The backup is not a full device Flash dump.

### 继续部署（16:30，中国标准时间）

用户授权“继续”后已正常退出桥接，核对候选及备份，替换 DLL/PDB 两个变化文件，再恢复自动轮播并启动。26 个候选配套文件核对通过；原页面、顺序及 15 秒间隔保持。回执为 `bridge-deployment.json`。实时 USB 已恢复为 COM9，协议设备身份仍为 `e8f60ae2ec56`，设备确认时间及运行毫秒持续递增，语音状态为空闲；这只证明桥接重启后的恢复，尚未证明后续拔插/设备重启能自动恢复。

自动审批拒绝了随后“正常退出、重新启动桥接并通过 --tab5-offer-ota 提供镜像”的组合操作，只返回 `blocked by policy`，未提供具体原因；该命令没有执行。已请用户通过现有桌面升级入口选择已冻结镜像，并在 TAB5 上确认开始更新。16:31 的观测仍为 083，尚无升级传输。桥接启动后的 BLE 配对认证失败另行保留，不能宣称三通道全部健康；须在升级完成后核验。

Bridge deployment completed after authorization, with the original cycle preserved and fresh USB identity/telemetry restored. Automatic approval blocked the subsequent restart-and-offer command before execution without a specific reason. The user was asked to use the existing upgrade UI. At 16:31 firmware was still 083; BLE authentication also required follow-up verification.

### 升级无进度：启动环境的配对视图不一致

用户开始升级后反馈无进度。16:32 现场显示 USB RPC 返回 `401 / HMAC rejected`，固件传输仍为“尚无升级传输”，BLE 也认证失败。USB 心跳在线不能证明鉴权通道正常；此前将其作为升级就绪证据不充分。

已核对：部署前正常桌面桥接读取的配对文件时间为 `2026-09-30T04:37:24.9868075Z`；工具会话启动的新桥接及工具文件视图读到同显示路径下的旧时间 `2026-09-30T04:05:28.4326030Z`，且各自均报告匹配磁盘。未读取或输出配对密钥。`docs/CRASH-077.md` 已记录同类工具会话启动后鉴权失败、正常桌面重启后恢复的现象。因此 16:27 工具侧配置备份不能替代正常桌面配对环境备份。

已正常退出本次工具启动的进程，准备 `start-from-desktop.cmd/.ps1`，由用户在正常桌面执行。脚本先核对此前正确桌面配对时间与候选程序/镜像哈希，在桌面配置视图另存备份，再正常退出桌面旧实例、恢复原自动轮播并提供候选；核对配对文件未变。不重新配对、不修改密钥。重新提供后先验证 BLE/USB 鉴权，再重试设备升级。脚本仅做语法检查，没有再次从工具会话启动。

The stalled upgrade was rejected by HMAC authentication before firmware data transfer. The tool-started bridge read an older pairing-file view than the previously working desktop process. USB heartbeat alone was insufficient readiness evidence. That process was shut down normally; a user-run desktop launcher now guards the original pairing profile and hashes, creates a desktop-context backup, preserves pairing and offers the candidate. Authentication must recover before retrying OTA.

### 正常桌面恢复与 084 实际升级

用户运行桌面启动脚本，`desktop-start.json` 记录 16:35:17 原配对视图核验成功、配对文件未变，另存桌面环境备份 `084-desktop-20261002-163515`；原自动轮播保持。实时读取确认配对时间恢复为 04:37:24、BLE 鉴权与数据确认恢复、USB RPC 无新错误。

用户重新开始后，日志显示本次实际使用 TCP 流控传输，共 6,641,456 字节，发送阶段 36,656 ms。不得将此作为 USB OTA 成功记录。设备自动重启后，于 16:36:52 取得新的 0.2.84-ui USB 状态（运行约 12.7 秒、重启原因 3），USB 未经手动重连自行恢复；随后 BLE 数据确认也恢复。亮度 75、显示错误 0、欠载 0。此处只证明本次 OTA 重启后的恢复，不替代独立拔插和冷启动复验。

已准备由正常桌面运行的 `finish-from-desktop.cmd`：临时正常退出桥接，读取三次 USB hello 并核对同一设备、递增运行时间、UI 活跃、无显示错误，再核对候选 ELF 指纹及 VALID OTA 分区；无固件写入，最后恢复原自动轮播和正常桌面桥接。执行结果待 `boot-084-verified.json` 与 `desktop-finish.json` 确认。

The desktop launcher restored the original pairing view and authenticated BLE. The actual OTA used TCP flow control: 6,641,456 bytes sent in 36,656 ms. Firmware 084 rebooted and USB recovered automatically, followed by BLE data confirmation, with brightness 75 and zero display errors/underruns. This is one OTA-reboot recovery observation, not general USB or long-term acceptance. Final read-only USB boot/hash/partition verification and desktop closure are pending their receipts.

### 16:39 启动核验与收尾完成

`boot-084-verified.json`：设备 `e8f60ae2ec56`，版本 `0.2.84-ui`；三次 USB hello 运行时间 189235 / 190448 / 191661 ms，UI 心跳年龄 29 / 21 / 12 ms，刷新计数递增，显示错误和 LCD 欠载均为 0。ELF SHA-256 为 `617680e9ede2367debcf6562a3afeea69a1b6992ee465069a43e30fd097dd376`，与冻结镜像一致；运行分区 `ota_0` / `0x20000`，state=2（VALID）、stateError=0。

`desktop-finish.json`：16:39:52 完成核验后正常桌面重启，配对文件未变；`selectedMode=auto`、`cycleEnabled=true`，原六页及顺序、15 秒间隔保持。升级已完成；语音误触/连续重试、亮度数字、独立 USB 重启/拔插及 BLE 设置计时仍按清单逐项复验。

Final receipts confirm the exact device and 084 ELF fingerprint, three increasing uptime/flush samples, healthy UI heartbeat, zero display errors/underruns, and VALID ota_0 with no state error. Desktop closure preserved pairing and the original automatic six-page/15-second cycle. Deployment is complete; individual voice, brightness, reconnect and BLE latency acceptance remains pending.

## 真机复验顺序

每次只执行并确认一项；不自动发送任何测试文本。

### 084-V1：TAB5 误触与切页语音连续性，通过

用户按本轮步骤在电脑不切窗的情况下，于 TAB5 录音中点空白、切到总览再回原会话，继续说话，手动停止并检查原草稿，反馈“都是正常的”。本项记为通过；不把该答复扩大为取消后重试、透明窗口、纯无线录音或持续电脑失焦通过。

16:43:50 只读运行核对：固件 084；USB COM9 在线，BLE 已连接且数据确认；语音已回 idle，诊断保留有声输入及 verified native control，finalWaitMs=1609（单次内部诊断，不是端到端耗时承诺）；Codex 尚无发送请求。亮度 75、显示错误/欠载为 0；原自动六页轮播及 15 秒间隔保持。

### 084-V2：取消后立即再次录音，通过

步骤：保留当前草稿，录音约 5 秒后明确取消，应保留开始前草稿；紧接着再次录音约 10 秒并手动停止，应无“豆包忙”阻断且文字返回原草稿，不发送测试内容。用户反馈“一切ok”，本项记为通过。

16:46:46 只读核对：语音已回 idle，有声输入、verified native control，finalWaitMs=1625（单次内部诊断）；Codex 尚无发送请求。USB COM9 与 BLE 数据确认正常，084、亮度 75、显示错误/欠载为 0，原六页自动轮播与 15 秒间隔保持。此结果不代表持续电脑失焦、纯无线录音或长期重复录音已通过。

### 084-B1：亮度数字与保存，通过

用户按步骤核对 75%，拖动到 60% 时数字实时更新且屏幕变暗，松手离页再回保持 60%，最后恢复 75%，反馈“正常的”。16:55:02 只读核对仍为 084、亮度 75、显示错误和欠载为 0。用户另提出音量数字位置不一致、三个设置下拉框过长及整页布局需求，已另行制作 085 布局候选；这不否定 084-B1 的功能通过。布局候选尚未刷入，见 [085 设置布局](SETTINGS-LAYOUT-085.md)。

084-V1 and V2 passed by user confirmation: tapping outside the input and navigating away/back did not interrupt the take; stopping returned text to the original draft; cancelling preserved the prior draft and an immediate new take completed without the busy error. Live diagnostics showed idle voice state, no Codex send requests, healthy USB/BLE and preserved display policy. Other feature checks remain separate and pending.

1. 语音误触：在原测试会话输入已有前缀，开始 20–30 秒语音；仅操作 TAB5，点空白、输入框外侧，再切到总览后回 Codex。电脑不作切窗操作。预期录音不中断，仍绑定原会话；停止后内容与前缀完整保留，无自动发送。再次录音并取消，预期恢复原草稿且能够继续新一轮录音。另验离页期间完成后回来的文字。
2. USB 自动恢复：结束语音，保持桥接运行，重启 TAB5，确认 USB 自行恢复绿色；再独立验证拔插恢复。不得手动重连后记为自动恢复通过。
3. 亮度：本机从 75 调到 60，拖动时数字随动；松手、离页、电脑回读均为 60，再恢复 75。保留其余设置。
4. 纯蓝牙设置：独立计时读取与保存，并回读实际值；记录耗时，不以代码路径缩短替代速度验收。
5. 返回原验收清单，继续长句、无线语音及剩余项目；已通过项目的范围维持原记录。

## English

The bridge and TAB5 084 are deployed; real USB boot, image fingerprint and VALID partition checks plus desktop closure completed at 16:39. Individual feature acceptance remains pending. TAB5 focus loss and page navigation no longer cancel the task-bound take; explicit stop/cancel, disconnect handling and the existing time limit remain. Task selection stays protected and transcripts return to the original draft without automatic sending. Newly added PC focus grace/recovery behavior was removed following the user's clarification.

The candidate restores the transparent bridge editor, retains ownership when cleaning up a previous take on retry, retries unexpected USB read cancellation, shows live brightness percentages, and publishes pending display-setting commands promptly. The previously observed focus-error message originates in the PC bridge; the TAB5 navigation change alone does not establish its original cause.

Release build, voice/UI and transport tests, native LVGL preview, device-setting/control tests and static ELF checks passed. The GUI-hosted `dotnet run --status-once` failed with an invalid console handle; direct execution of the candidate DLL succeeded. The user accepted TAB5 touch/navigation voice continuity and cancellation followed by an immediate new take; brightness display/value persistence also passed by user confirmation; independent USB reconnect and BLE latency remain pending. Deployment preserved pairing and the original automatic display cycle; no commits, pushes or public releases were made.
