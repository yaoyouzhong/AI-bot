# Claude Code 原会话接入修正

来源标签后续：日常桥接 `4b6bac4f…` 与 TAB5 `fc6e8d3b…` 已按用户要求完成本地部署，设备精确启动指纹核验通过。会话来源与发送权限分离，并预留编译期 Harness 接口；真机显示复核及当前完成状态见 [会话来源标签记录](TAB5-SESSION-SOURCE-20261107.md)。下方保留此前各轮证据。

最新进展：日常桥接已更新为 `695ac2e8…`，新增独立于查看回复的会话发送能力；`3140a12c…` 固件移除已完成回复底部的刷新提示，已获批安装并完成五次会话切换、四次查看回复的真机检查。最新状态见 [回复静默刷新与会话发送就绪](TAB5-SILENT-CONTROLS-20261107.md)。

最新后续：用户反馈回复页依次显示连接/缓存/完成状态，已按 Codex 对齐周期状态字段，并安装日常桥接 `2342238d…`；设备端 `da8ed059…` 已获批安装且精确启动核验通过。两条 Claude 原会话六次打开的真机采集画面均保持完成状态和正确正文。原因、设计、专项结果、采样限制及完整预览套件的既有失败见 [回复页状态对齐记录](TAB5-REPLY-STATE-20261107.md)。下文保留前一阶段的原始包身份和验证记录。

## 11 月 7 日后续日常修正与新会话验收（未发布）

本节更新此前各阶段状态，原始失败与验收记录保留在下方。日常安装路径的 Windows DLL 最终替换为 `7d23a00e096a0a288847bc107df54c42fad544f50871f8a810eeff406160a587`，在此前 `37be15bb…` 目录修复上补充下述原生历史重试节点修复。仅替换 DLL/PDB，保留已验收的登录辅助程序。设置、设备登记、配对、解锁 PIN 和键盘访问文件的五个摘要均保持不变；旧 DLL/PDB 已备份。`--status-once`、安装后 HTTP 状态和 TAB5 USB/Wi-Fi 连接正常。原有其它 Windows/macOS 工作区改动未清理，未推送或发布。

### 日常会话与发送能力

Windows 修正包括：同一项目路径忽略大小写和末尾分隔符，避免同一 AI-bot 目录被分为两组；保留原生会话的正式名称元数据；普通 VS Code 新标签补入目录发现；为可验证的原生 VS Code 输入框报告可写能力，并允许已确认空白会话读取为空历史。只有合法、完整且属于同一会话的纯名称记录，加上真实活动空白面板证明，才按空历史处理；损坏、截断或没有原生面板证明的记录仍拒绝。没有通过伪造消息或弱化送达回执判断来启用发送。

用户创建的两条新会话在 TAB5 上分别显示为 **VS Code终端**、**Claude Code原生终端**。原生终端从电脑发送的一条消息得到 `PC_TERMINAL_READY_1107`，真机回复页已查看；TAB5 向 VS Code 发送的一条消息得到 `TAB5_CODE_READY_1107`，原会话真实用户记录、桥接 202 回执、原生送达确认和真机“已完成”画面一致。没有另起模型会话，也没有重发旧未知请求。这两条验证覆盖电脑端发起和设备端发起，不宣称每条新会话都分别完成了双向全套测试。

前四次 VS Code 验收准备未发出 send：分别涉及目录未发现、输入能力缺失以及诊断脚本等待新草稿加载事件/误将缓存草稿视为空白。真实发送发生在 `daily-vscode-device-5/`，只有一个新请求。最后一次使用设备原本保留的测试草稿，没有重新输入覆盖；先查看名称、完整草稿和亮起的发送按钮，再点击发送。相关本地证据位于 `artifacts/tab5-switch-polish-20261107/` 的 `terminal-message.json`、`daily-case-result-5.json`、`daily-vscode-device-5/traffic.json` 和实际截图。

26 个归属明确、已结束的测试会话记录已可恢复归档到用户本地 `AI-bot/recovery/test-conversations-20261107/`，包括 18 个明确测试记录和 8 个独立 workspace 测试记录。没有删除普通历史、现有活动会话或加密草稿/回执。原项目列表中大量同名 `workspace` 来自不同测试目录的末级名称；路径归一化修复只合并同一路径，不把不同目录因为同名而合并。

Release 隔离构建通过，history、native-workspace（56 项）、native-receipt、native-send（35 项）回归及 `--status-once` 通过。原生发送回归明确使用本地 fixture，不计入真实模型请求。此前目录修复记录为 `bridge-ready-validation.json`、`daily-install-ready/result.json`；最终历史修复对应 `bridge-continuation-validation.json`、`daily-install-continuation/result.json` 和 `daily-continuation-verification.json`。后者确认日常唯一常驻、五个配置摘要、USB/Wi-Fi 及安装目录的 `dotnet run --no-build ... --status-once` 均通过。

### TAB5 切换等待与提示优化

已安装 BIN SHA-256 `937471655cfb7d8b6cd74127db8bc553290741fa0b3fc3f4b82288e6292914cc`，ELF SHA-256 `7b384c21ab27774bf813e930c90235b16374a178987800afce47278ff361c0b5`，版本保持 `0.2.318-ui`。固定交付路径仍为 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，最终 sidecar 归档为 `versions/0.2.318-ui/937471655cfb-a0076a78/`。启动核验明确通过，不能仅用相同版本号代替原包身份。OTA 曾先被忙状态明确拒绝；实际传输只有一次。监控脚本未正常完成其收尾，因此另做精确只读启动核验，未重发升级。

交互设计保持 1280×720 布局、166×48 客户端按钮及原字体、颜色、间距。仅后台读回复或加载草稿时立即响应切换，完成结果绑定原客户端、会话和修订号；旧客户端结果不能覆盖当前界面。脏草稿保存仍需完成，短于 350ms 的等待不显示同步提示。发送、录入、图片和停止保护保持。设计记录、实际 LVGL 预览和实际 sidecar 预览均已查看；对应源码/回归补丁为 [切换优化补丁](TAB5-318-SWITCH-POLISH-20261107.patch)。正式资源受限构建及栈预算/绘图后端检查通过，切换、工作区一致性和取消草稿刷新测试通过。

新原包真机完成 20 次客户端点击，2 次单次双向切换及 6 组三次往返都匹配最后选择，稳定截图没有遗留同步提示，可见草稿区域摘要保持一致。每个诊断触控命令耗时 3.54～5.70 秒，不把它写成亚秒级连点或 UI 响应耗时测量；即时切换与延迟提示边界由本地状态测试覆盖。两条新名称及电脑端原生终端回复均在正常全量目录中再次核对。展示设置、配对和日常常驻恢复通过，导航诊断禁止模型发送/追加/停止。证据为 `physical-switch-acceptance.json`、`device-nav-acceptance-937471/`。

日常桥接恢复后的只读状态采样获得 7 份完整目录，跨度 43.2 秒，Claude 均为 8 条、Codex 均为 69 条，两条新名称始终存在。真机连续切换也未出现客户端入口丢失。此前多次桥接更换/诊断重启会经历目录初始化，但用户所见每次入口消失的具体原因未被逐次记录，不能据此宣称全部均由重启引起。

### 窗口前台问题仍单独核查

用户确认 VS Code 发送成功，同时报告发送时 Claude 原生终端覆盖了已在前台的 VS Code。这不是预期行为，不能因为送达成功而记为窗口行为通过。消息记录确认进入原 VS Code 会话；三次普通背景终端读取，以及 VS Code 位于前台时的另三次终端读取，均未改变前台或窗口相对顺序。原两条真实消息额度已用完，用户另批准一条 `FOCUS_READY_1107` 固定回复消息用于窗口核查。

第一次额外验证在输入与 Submit 前被明确返回 409 `native_history_unconfirmed`（`deliveryFinal=true`），没有产生模型请求。真实会话中三条 `system/api_error` 重试节点连接原始输入和最终回复，旧解析器跳过这些节点后错误报告历史不完整。修复只将同会话、非旁支且标识/父节点合法的 `api_error` 作为隐藏图连接，不产生公开文本、用户回执或新叶节点；未知 subtype 仍不视为已验证连接。原始文件只读对比从 `Limited=true / Accepted=0` 变为 `Limited=false / Accepted=1`。新增连续重试与元数据隔离回归通过，未修改原始会话文件来规避问题。

修复安装后，使用正常日常桥接的已配对设备 HTTP 入口发送这条尚未消耗额度的消息，未在发送观察期间重启桥接。此轮是主机侧协议入口验证，不称为新增一次 TAB5 实际点按。一次请求返回 202，原 VS Code 会话新增唯一用户记录并回复 `FOCUS_READY_1107`；完整历史变为两轮已完成、两个真实用户回执。发送期间及 HTTP 返回后 25 秒以 25ms 轮询前台及可见窗口相对顺序，只观察到 ChatGPT→VS Code 的预期激活，Claude 原生终端没有置前。额外授权实际消耗一条模型消息，原明确拒绝的记录保留，没有重发未知请求。证据为 `focus-send-result.json`、`focus-send-continuation-result.json`、`focus-continuation-acceptance.json` 和三份只读历史对比记录。此次续发和目标激活通过；用户此前报告的终端置前异常仍未复现、根因未确认，不能声称已修复。

用户明确要求删除项目禁止验收自动切换前台窗口的规则，已删除 `AGENTS.md` 对应条目及统一流程中对该约束的引用。普通 VS Code 原生输入通道仍需焦点，桥接会激活核验过的目标窗口，用户不必先手动置前；规则删除不等于获得了后台发送 API。它与当前 Codex 客户端普通文字发送的原生 UI 通道相似；原生 Claude 终端则通过控制台输入通道工作。

English: The local daily Windows bridge is now the exact DLL above, with five configuration hashes preserved and no publication. Catalog grouping, native blank-panel discovery/naming and input capabilities are corrected. Twenty-six identified completed test transcripts were reversibly archived. One real computer-originated terminal message and one real TAB5-originated VS Code message were confirmed in their original sessions and on the device. The exact TAB5 polish image was boot-verified; actual renders and twenty physical client selections passed, with diagnostic pacing and offline-only timing coverage stated separately. Stable full-status samples retained both clients and both new names. A subsequent focus probe was explicitly rejected before submission because API-retry metadata broke native-history ancestry; the narrow parser fix passes regressions and comparison against the original transcript. After local installation, the one separately authorized additional model message succeeded in the same VS Code session through the normal bridge's authenticated HTTP endpoint. Automatic target activation passed, and the terminal did not move forward during sending or the following 25 seconds. This probe did not involve a new physical TAB5 tap. The earlier reported terminal-focus issue remains unconfirmed and is not claimed fixed. The user-requested foreground-rule removal also removes its stale release-workflow reference. No macOS or ESP8266 physical validation is claimed for this follow-up.

## 11 月 7 日第一阶段本地安装与验收（未发布）

日常 Windows 桥接保持原验收 DLL `c8a3d3c4892c0cc8b08726d5f98e8f56fdc5205b0ffa12dfe31256ae466d6bd3`。用户批准的 TAB5 切换修复候选已实际安装：BIN SHA-256 `c03b68f1dac63cacf969b4eccbce8764db98a59b560801a9d3792314ddae6d70`，ELF SHA-256 `1d8db399ec754ec4a638f218650fc01be49383b2aea5ce210a00d17f9f092619`；启动核验确认 `ota_1`、`state=VALID`，与原包精确匹配。此前一次启动请求被明确拒绝且未传输；收尾旧追踪后执行的是同一原包的实际安装，没有重发结果不明的升级请求。

用户单独批准结束旧 `q` / `w` 两条测试追踪后，先备份完整加密日志，再只修改这两条记录的追踪终态，仍保留 `tracking_ended_delivery_unknown`。原始备份位于用户本地 `AppData/Local/AI-bot/recovery/tab5-client-switch-20261107/1900fd1f6551c21e056b9e93a2cf876fddc9e3222b6ec1b15f279cdd081504b8/drafts.dat`；没有将解密内容写入仓库。真 TAB5 随后针对原 `8a053d1a…` 请求查询，收到 409 收尾回执，屏幕继续保留 `w`，没有将未知送达标成发送成功。全部 Claude 草稿和其它记录完整保留，Codex 的草稿与回执摘要也保持原值。

修复后的真机通过 20 次客户端点击：2 次单次双向切换、6 组三次往返点选。每组最终客户端均匹配最后选择，稳定画面已查看，没有遗留同步提示；可见输入区域与对应客户端基准摘要一致。诊断动作间实际间隔为 4.07～6.52 秒，中位数 4.98 秒，故不将此记录宣称为亚秒级连点的真机验证；即时连点、取消待切换与脏草稿等边界由此前离线状态测试覆盖。首次验收脚本误把总览页的颜色差识别为 Codex 选择态，未真正测试切换，单独记录为无效设置；加入已核对的对话页标题像素摘要后重跑通过，没有弱化断言。运行证据在 `artifacts/tab5-client-switch-20261107/physical-switch-acceptance.json`、`device-nav-acceptance-c03b68-v2/`。

最后的 `final-verification.json` 通过：五个设置/配对/访问文件摘要不变，Claude 日志与“原备份加两条已授权追踪变更”逐项完全一致，Codex 日志内容摘要不变，日常原包唯一常驻恢复，`/status` 正常，USB 和 Wi-Fi 已连接。测试接口仅允许读取、草稿同步及回执查询；本轮切换验收记录 78 次 read、2 次 draft-load，没有发送、追加、停止或确认送达操作。没有新增模型请求、推送、标签或公开发布。

### 安装后的使用反馈与边界

用户随后询问 Claude 灰色发送按钮。正常日常桥接的 3 份完整状态样本确认当前 control 对应 `TAB5-local-fixture`，`running=false`、`waiting=false`、`canSend=false`、`canSteer=false`、`canInterrupt=false`，没有 pendingRequest。这些是已关闭原测试终端的历史会话；固件按同一会话的发送能力禁用按钮，并非仅因输入为空。要继续输入需具备可写的活动 Claude 原生会话；不能通过开启按钮来绕过原会话绑定。

用户也确认切换时仍有提示闪现和轻微停顿。当前修复解决丢弃点击及重复草稿加载抢占，仍会等待原后台请求完成。现场最近一次 `read/ack` 记录 handler=656ms、response=1922ms，无 USB RPC/控制中断，说明仍有可感知的同步等待。不能把本轮可靠切换验收等同于无等待或动画流畅度优化；“正在同步，完成后切换”的短暂提示仍属于当前实现。本次没有修改原包来掩盖该等待。

English: The authorized TAB5 image is installed and verified by its exact ELF hash on ota_1 in VALID state. The two explicitly authorized old test trackers were closed with the unknown-delivery conclusion and all drafts preserved; the original encrypted journal is backed up in the user's local recovery directory. Twenty real client-selection taps pass, including one-click switches and repeated alternation, with actual action spacing documented rather than claiming subsecond interaction. The first invalid page-classification attempt is retained separately; corrected page identity checks pass. Final settings, pairing, journals, daily bridge identity and USB/Wi-Fi health checks pass. No model request or public release occurred. Historical fixture conversations correctly report no send capability. A brief synchronization hint and noticeable read-response wait remain; reliable switching is not claimed to be instantaneous.

以下为候选准备、授权及排查过程记录；最终状态以上述安装与验收为准。

## 11 月 7 日日常桥接本地替换（未发布）

用户明确要求将验收候选替换到日常桥接，先不发布。已正常退出原常驻程序及其守护，将 `foreground-1107-complete/` 的原包复制到 `windows-app/AIBotBridge/bin/Release/net8.0-windows10.0.19041.0/`，没有重新构建。当前 DLL SHA-256 为 `c8a3d3c4892c0cc8b08726d5f98e8f56fdc5205b0ffa12dfe31256ae466d6bd3`；45 个候选文件逐一匹配。实际替换旧 DLL 并补入原目录缺少的 PDB，其余文件保持。旧 DLL 已校验并备份于本地 `artifacts/claude-code-native-alignment-20261105/daily-install-20261107-2/rollback/`，其 SHA-256 为 `21cae7288226f592da8f678e534353569a3ee23605230bf79996a6acedacd012`。

安装路径的 `--status-once` 退出码为 0。重启后确认唯一常驻程序及其守护、连续更新的 `/status`、TAB5 `0.2.318-ui` 的 COM9 USB 连接与 Wi-Fi 桥接正常；USB RPC 有成功交换，本轮没有 RPC/控制中断。日常环境原有 `AIBOT_HTTP_PORT=18765`；初次验收误用了默认端口 8765，该次接口检查失败不能推断桥接故障。改用实际端口后通过，未修改端口或系统设置。完整文件清单、初次检查与更正后的结果分别保留为上述目录内的 `manifest.json`、`result.json`、`verification.json`。

`settings.json`、`devices.json`、`tab5-pairing.dat`、`tab5-unlock-pin.dat` 和 `wifi-keyboard-access.dat` 的前后 SHA-256 均相同；只记录摘要，没有把资料内容复制到仓库。没有修改 TAB5 固件或展示设置，没有新增 DeepSeek/其他模型请求，没有重发未知请求，没有推送、打标签或发布。

按 ESP8266 部署收尾规则，又正常退出桥接，执行安装路径的 `--restore-cycle-default` 后重启。确认自动模式、轮播开启、原页面/顺序/15 秒间隔不变，设置文件字节摘要也未变化，TAB5 USB/Wi-Fi 再次连接成功。记录为 `cycle-closeout.json`。当前 ESP8266 没有 USB 连接，也没有新的授权 Wi-Fi 请求，故只确认其桥接展示策略，未宣称本次 ESP8266 真机验收通过。

用户报告的“连接暂不可用 · 自动重试”来自 TAB5 对话读取前的 `rpc_read_link_ready()` 检查，表示当时没有满足新鲜度与授权条件的读取通道，不能据此判断 Claude 供应商故障或余额不足。当前连接检查正常；对话页面提示是否消失尚待用户目视确认，连接指标不替代该项结果。既有同包功能验收按下文记录复用，不把本次后台检查写成重新执行了真实模型全套测试。

### 日常列表与切换跟进

用户随后确认前两条 `TAB5-local-fixture` 的回复页显示“已中断”，第三条显示“已完成”。前两条为本次专用停止验收留下的会话；本轮排查没有发起新的停止或发送操作。对话内容现已能读取。

只读、已配对认证的状态采样保存在本地 `daily-catalog-probe.json`：18 份完整状态包中的 Claude 目录均为 32 条、15 个项目分页，数量没有变化。采样只保存数量、匿名标识及同步元数据，密钥、正文和草稿内容没有写入报告。固件列表可视区域高 328 像素、每行间距 88 像素，一屏约显示 4 条，同一项目超过 8 条另分页。用户所见从 1 条到 4 条可能与项目分页或滚动位置有关，此处保留为推断，未将采样数量等同于当时的屏幕布局。

双向切换提示“正在同步或录入，请稍后切换”的现有条件包括读取回复、保存/载入草稿、录入及发送等状态；这条分支拒绝本次点击，不会排队自动切换。用户进一步确认两个方向均反复出现。离线基线测试复现了同步中点击丢失，故不能将其当作等待即可消除的偶发现象。没有清空草稿、删除测试历史或重发未知请求。

English: The accepted Windows candidate was installed locally at the existing daily executable path without rebuilding or publishing. All 45 candidate files match, the original DLL is retained for rollback, installed-path startup and live TAB5 connections pass, and the five settings/pairing/access files retain their hashes. The initial verifier used port 8765 incorrectly; the existing daily environment uses 18765, and verification passes there without changing configuration. Required ESP8266 auto-cycle closeout preserved settings bytes, pages, order and interval, and TAB5 reconnected after the final restart. ESP8266 itself was not connected, so its physical-device acceptance is not claimed. No model request, firmware change, resend, push, tag or release was performed. The device's temporary connection message concerns read-channel readiness; disappearance of that specific UI message remains awaiting the user's observation.

Follow-up: The user can now read interrupted results from the two dedicated local test sessions and a completed result from another session. Eighteen authenticated read-only status samples consistently contained 32 Claude entries across 15 project pages. The viewport shows about four rows; pagination/scroll position is a possible explanation for the perceived count change, not a confirmed screen observation. The user confirmed recurring blocking in both directions. The offline baseline reproduced discarded client-switch clicks during synchronization. No draft clearing, history deletion or unknown-request resend was performed.

### TAB5 双向切换修复候选（尚未部署）

日常 Windows 桥接保持已验收的 `c8a3d3c…` 原包。修复位于现有 TAB5 `.318` 工作源码 `artifacts/tab5-project-dropdown-close-20261105/source/firmware/main/ui.c`：后台读取或草稿同步期间记录最后一次客户端选择，原操作和当前客户端的草稿保存完成后执行切换；等待切换时不再发起新的草稿读取抢占时序。点回当前客户端可取消待切换。发送、录音、照片及停止等录入/控制忙状态保留原来的拦截。

先记录最小交互设计，再实施修改；不改变按钮尺寸、布局、配色或触控范围。同步提示为“正在同步，完成后切换”，切换完成后清空。实际 1280×720 LVGL 渲染已检查，同步提示可读，完成态没有残留提示，原客户端和目标客户端草稿分别保留。离线预览先前捕获到未结束的动画缓存，推进动画后再次检查完成态；未据此修改产品渲染代码。

本地证据保存在 `artifacts/tab5-client-switch-20261107/`。旧源码在相同测试中失败；修复通过 12 次双向读取中切换、取消待切换、8 类输入忙保护、草稿加载/保存/超时、文字/图片/未知请求保留与零发送断言。原有工作区一致性及取消草稿刷新回归通过。这里的测试是本地模拟，不代表真机验收，不覆盖先前原包的验收结果。

官方 `scripts/build.ps1 -LocalArt -LocalVersion 0.2.318-ui` 构建成功，图库栈预算与绘图后端检查通过；保留两处未修改代码的 unused 警告，没有将构建写成零警告。构建只调整本任务进程的核心范围，最终使用原预算内两个 P 核、2 个 worker 和 BelowNormal，未修改系统设置。新包为 6,925,824 字节，比基线增加 32 字节；BIN SHA-256 为 `c03b68f1dac63cacf969b4eccbce8764db98a59b560801a9d3792314ddae6d70`，ELF SHA-256 为 `1d8db399ec754ec4a638f218650fc01be49383b2aea5ce210a00d17f9f092619`。准备脚本已用日常桥接的正式校验器检查并更新固定 `artifacts/firmware/tab5/latest/aibot_tab5.bin`，归档为 `versions/0.2.318-ui/c03b68f1dac6-9751c986/`。配套中英文说明的实际首屏与滚动后渲染已查看。源代码与测试差异保存为 [切换修复补丁](TAB5-318-CLIENT-SWITCH-SYNC-20261107.patch)。目前只准备本地候选，尚未向设备提供或安装，等待针对这份固件的部署授权；未发布。

English: The daily Windows bridge remains the accepted original package. The TAB5 candidate queues the latest client choice during background reads or draft synchronization, saves outgoing drafts before switching, and prevents a new draft load from starving the queued choice. Clicking the active client cancels the pending switch. Existing guards for active input, sends, photos and stopping remain. The baseline reproduces the defect; focused regressions and existing parity/cancelled-draft tests pass without sending a model request. Actual LVGL renders were checked after animations settled. The official firmware build, stack budget, drawing backend and production image validator pass; two warnings in unchanged code remain. The 6,925,824-byte candidate above is frozen and archived, including inspected bilingual sidecar previews. Firmware deployment awaits authorization for this exact image; physical-device acceptance and public release have not occurred.

### 获准安装后的阻塞排查（11 月 7 日）

用户随后明确批准安装 BIN `c03b68f1…` 并完成真机验证。已通过日常桥接提供该原包并发起一次启动；设备明确返回“设备还有未确认消息或正在同步的草稿，请处理后重试”，没有开始固件传输。没有重新发送启动命令。最初的只读启动核验循环看到同版本号，核验新 ELF 不匹配；这是旧镜像尚未更换，不能记为新固件启动失败或已安装。获知明确拒绝后终止了核验循环，记录为 `artifacts/tab5-client-switch-20261107/installation.json`。

只读解密检查发现：Codex 有 2 份草稿、18 条请求，无 dispatching 记录；Claude 有 3 份草稿、18 条请求，其中旧测试 `q`（`de2e16f2-d0ce-444f-9249-3111beef6637`）和 `w`（`8a053d1a-6f82-4b2b-aaf6-016c76fc1a5c`）仍是 dispatching，分别保留 1 字符草稿。前者有先前 `history-proof.json` 的未确认记录，后者有 `native_console_turn_unconfirmed` 原始证据，均不能据此宣称送达或未送达。此检查不打印草稿正文、不保存解密数据；前后内容摘要完全一致。

代码确认长期阻塞的条件：`ota_prepare()` 对任一内存草稿的非空 request 都返回 `draft_pending`；`codex_recovery_tick()` 自动查询最多三次后停止；桥接与设备把“送达仍未知”保留为待追踪状态。升级反馈又把它与草稿同步混在同一提示中。该设计没有把已停止查询的未知请求与真正进行中的操作区分开。设备仅有一个本地草稿 checkpoint，不能直接去掉 OTA 保护或忽略其它未保存内容。现有完整历史不能确定到底哪条内存请求触发了本次保护，未把两条主机记录直接等同于两个真机阻塞项。

已准备两条旧测试的明确追踪收尾方案，并完成只读预检：先将原始加密日志备份到用户本地恢复目录，只将这两条记录改为 `tracking_ended_delivery_unknown` / `test_tracking_closed_unknown`，不改原消息、草稿、请求身份或其余记录，不将其标为发送成功，不重发。此操作等待用户对旧请求追踪状态修改的单独答复，尚未执行。准备记录为 `tracking-closure.json`；对应 apply 入口还要求独立批准文件。

实际设备截图确认 USB、Wi-Fi 与配对正常；查看升级页和返回对话页后，设备展示设置、电脑设置、设备登记和配对文件摘要均保持，日常原包恢复后 `/status` 返回 200。只读导航使用专用 USB 诊断，拒绝 send、steer、interrupt 和 receipt-confirm；日志只出现三次 Codex read。普通 stdin 导航入口两次未在期限内就绪，没有执行触控，均收尾恢复；换用明确仅 USB 的诊断后正常，未把诊断工具异常归为产品切换验证失败。以上均在原固件上进行，修复候选的真机安装与双向切换验证仍未完成，没有新增模型请求或发布。

English: Installation of the exact candidate was authorized, but the device explicitly rejected the single start request before transfer because of pending delivery/draft state. No installation retry occurred. Read-only inspection preserved every draft and journal record, identifying two old unconfirmed test trackers. The code combines unresolved delivery with an OTA busy condition even after bounded automatic receipt polling stops; removing the guard would be unsafe because only one local checkpoint exists. A narrowly scoped, backed-up tracking closure preserving the unknown-delivery conclusion and all drafts has been prepared but awaits explicit approval. USB-only device navigation, configuration preservation and restoration of the accepted daily bridge pass; no new model request, firmware installation or public release is claimed.

## 11 月 7 日 Windows 普通交互终端

这里分别记录普通前台 CLI、Claude supervisor 后台任务和 VS Code 插件。三者按会话 UUID 绑定，设备回执只对发起操作的那条会话和请求生效。

- 前一轮在普通交互终端 `a6d24414-eabc-4bec-82b1-e208ba094b44`（PID 31844，CLI `2.1.318`，DeepSeek Flash）实测。配对的 TAB5 发出一条只读任务，原终端调用 Bash 并返回文件中的随机词 `TAB5_80D8E88CD6E0`。首次返回 504，随后同一请求的回执返回 202，设备显示相同的词。原请求只提交一次。
- 同一进程收到桥接测试入口提交的红色色卡。原生图片与测试 PNG 逐字节一致，TAB5 收到“红色”。两项实测的输入使用验收专用适配器；产品 DLL `b2860c43520e10cff8db8a81c246076add4b4fc48ebbce52567934d462e58bb3` 单独通过普通桌面启动和同一会话的只读探测。详见本地 `artifacts/claude-code-native-alignment-20261105/ordinary-foreground-02/` 下的 `native-results.json`、`production-inspection.json` 及两个批次的设备回执。
- 产品普通终端路径用 Windows 控制台 API 定向到原 CLI，检查进程代次、用户权限、输入队列归属、编辑框和原生历史。在现有 PowerShell 窗口直接键入 `claude` 通常会共用输入队列，此时桥接拒绝写入，以免 Claude 退出后输入落入 shell。要为 TAB5 开一条普通交互会话，可以由用户从目标项目的 PowerShell 运行以下命令，让 Claude 独占新控制台。保留原生交互界面和当前模型配置。

```powershell
Start-Process -FilePath (Join-Path $env:USERPROFILE '.local\bin\claude.exe') -WorkingDirectory (Get-Location).Path
```

适用范围为 Windows CLI `2.1.318` 和 `2.1.319` 的独占控制台。共享控制台、多个 Claude 持有者及不同权限级别的窗口均应保留草稿。下文“只读”及后台托管验收描述的是各自日期和宿主。

### 配对 TAB5 通过产品代码发送

USB 实机为 TAB5 `0.2.318-ui`，终端为 Claude Code `2.1.319`。文字、图片和追加使用 DeepSeek Flash，停止使用本机可控 SSE 服务；全部走普通终端产品适配器。真实模型记录位于本地 `artifacts/claude-code-native-alignment-20261105/foreground-1107-device/`，停止记录见下表。测试期间没有操作既有用户会话。

| 操作 | 设备、终端和回执 |
| --- | --- |
| 发送合成照片、读取回复 | `check-1794014453717214800` 的 `003-action-send.png` 显示选中的终端会话和照片。配对设备发送请求 `94509f0b-5681-4f5e-b8cf-567e61e2a1bf` 得到 202。原生用户消息 `bf2fea58-87de-4fd0-95e9-4b1a2a30844e` 的 PNG 与色卡 SHA-256 一致，设备同一轮 `005-photo-reply.png` 显示“蓝色”“已完成”。此次验证从已载入色卡的设备发送按钮开始。 |
| 发送文字并执行 | 通过普通 CLI 恢复同一会话 UUID `29401ff1-0eea-4899-b9a0-e94c7065e8a3` 后，重新绑定新的进程代次。`check-1794015001520693300` 的设备请求 `b1d19fae-4daa-45a4-a4ba-5bb7202a7b25` 得到 202，原生用户消息 `16fcdbda-5b54-4714-a0a8-9488bbb99c8c` 确认为 `q`。DeepSeek 按前文约定从 1 列到 500，用时 5.9 秒。 |
| 运行中追加 | `check-1794024964736044400` 在同一专用会话的等待任务运行时从 TAB5 追加 `w`。请求 `776e08b1-5e06-4a28-9723-608b84de845a` 返回 202，绑定设备轮次 `8ef90f92-004f-fb7c-11d3-d203b94ac4c3`；原生用户消息 `ca9ff4c3-4eff-4c8b-8c6a-ed9c69619d1c` 和桥接送达记录吻合，未重复提交。 |
| DeepSeek 停止 | CLI 拒绝单独的前台 sleep 后，模型改为后台等待，追加回复在设备切入回复页前结束。未发出停止，不计通过；两个等待均完成，原生记录分别出现 WAIT_DONE、APPEND_DONE。 |
| 真 TAB5 停止本地流式响应 | 最终候选批次 `foreground-1107-local-stop-1794026956228236900` 返回 202。设备请求 `e02d4e4e-865c-4d55-ab4c-ab627a618170` 的运行、停止、设备终态均绑定 `37c13d88-e7df-ba3e-1fdb-08b7ce2c54e2`。原生用户消息 `1cc18054-d2b6-4e97-8109-2908634fce74` 与中断标记沿用同一 `promptId`。`005-stopped.png` 已人工查看：显示“已中断”，保留 `Local partial reply`。同一原进程恢复 idle 并保持运行，显示设置保留。 |

照片候选 DLL 为 `be2f0d7e2cccd1f6d00a46369ad1ce454d2926713ef72e97b4d58be2c850f70c`，文字候选 DLL 为 `9b24e96d0f21164cb592251a96526f3b08f1e9e049b19ef5141bfc7bd00b6d67`。照片的首次试验因输入框换行隐藏了文件名后的分隔空格而在 Enter 前被拒绝。修复后，仅在两行显示内容逐字匹配、唯一差异为行间一个空格时，才使用原终端光标核对实际编辑长度，回到原位置后提交。未知回执不重发。最终候选的普通终端追加、停止均已开放；验收入口只限制目标 scope，不再另行启用能力。

当前交付候选为本地 `artifacts/claude-code-native-alignment-20261105/foreground-1107-complete/`，DLL SHA-256 `c8a3d3c4892c0cc8b08726d5f98e8f56fdc5205b0ffa12dfe31256ae466d6bd3`。该原包通过正常入口的停止真机复核，报告 SHA-256 `95ce193e8557c7a2821054308fec66192ca7cc7b795308b24147a7e8b598983d`。与前述图片、文字和追加候选相比，最终改动为开放已验收的控制能力及修正验收定位，发送、图片和送达核对逻辑未变；对应未变路径复用前述记录，不声称同一原包重跑了 DeepSeek 全套。Release 构建零警告零错误，35 项发送、23 项回执、56 项工作区及历史解析回归通过。正常用户启动和收尾记录见 `foreground-1107-complete-check.json`。未替换日常托盘，未推送或发布。

停止补验只连接本机 SSE 服务，不再消耗 DeepSeek 输入。期间遇到假服务分类错误、桌面锁定以及 OCR 漏识别；仅在用户解锁后继续，未修改锁屏设置。追加按钮的 OCR 回退复用已独立核对的完整按钮像素摘要，未改产品界面。`foreground-1107-local-active-1794026396173049900` 因假服务过早结束首轮，追加请求 `8a053d1a-6f82-4b2b-aaf6-016c76fc1a5c` 返回 504，阶段为 `native_console_turn_unconfirmed`；原生输入区留下 w，该未知记录和草稿保留，没有重发或标为成功。随后另开专用本地会话，仅验停止。`foreground-1107-local-stop-1794026782916217800` 首次通过，移除测试能力限制后又用上述最终原包复核通过。两次均确认同轮中断、原进程存活、设备画面及日常托盘恢复。

### 本地流式追加、中断及继续验收

11 月 7 日 12:08 的计时补测 `check-1794024456624270600` 未通过：等待消息进入原会话后，DeepSeek 返回明确的 `422`，拒绝消息中的 `tool_definition`；原生 `isApiErrorMessage=true`，没有 Bash 工具调用。TAB5 随后发起的追加请求 `4e8ee798-f06e-4d83-822e-7fb09dbd51bc` 在调用原生适配器前返回 `409 turn_changed`。该次没有追加输入或停止操作，不能计为已验收。设备显示设置保留，原托盘已恢复。该批次 DLL 为 `eaca10e4b87527201ee922575368cbd6bbea2ed9f4347423b4259b3e8fa845c6`，报告 SHA-256 为 `800534ad5e14bfe508e7d55db2d5778c86c86896dd13108bceff07637aadafca`。此前一次未发请求的预检停在单字母 OCR，后以已核对的完整编辑区像素摘要确认 `w`，未覆盖设备草稿。

兼容预检 `foreground-1107-compat-1794024721910723900` 只连接本机 SSE 假服务。专用 CLI 进程使用 `--strict-mcp-config`，并在该进程设置 `ENABLE_TOOL_SEARCH=false`、`CLAUDE_CODE_DISABLE_EXPERIMENTAL_BETAS=1`。已核验请求中没有 `tool_definition`，工具声明保留 Bash、没有 MCP；同一进程的运行中追加、目标轮次中断和中断后存活均通过。这些开关见 [Claude Code 环境变量](https://code.claude.com/docs/en/env-vars)，没有写入日常设置，也没有把假服务结果算作 DeepSeek 真机通过。

用户批准将输入上限增加到五条后，已执行一次兼容重试。候选为 `foreground-1107-retry/`，DLL SHA-256 `f6096c5df57b0d94589461e7b5f7ca244155c9eefa8258f4b66d9c9dd02b55e9`。它保留先前等待请求的预留记录，只接受上述明确 422 及未进入原生适配器的 409 证据，另用独立预留记录防止重复提交。批次 `check-1794024964736044400` 的追加通过，报告 SHA-256 `ee4ff62c738c547ec26a02c8c846ba188be4b15d1a9fe3b8d555da00e78c9c7f`。五条输入为照片、q、被拒绝的等待、兼容重试等待、w；没有再次发出输入，真实停止仍未执行。原进程在验收收尾前保持可用，随后只关闭本次恢复的专用终端，日常托盘和设备显示设置已恢复。

此前候选位于本地 `artifacts/claude-code-native-alignment-20261105/foreground-1107-final/`，DLL SHA-256 为 `2536a5495c850fd2e783894b38ac880cb1b19e0cb17c560abe7ad1c0c3ce0baf`。Release 构建零警告零错误，35 项原生发送、23 项回执、56 项工作区和历史解析回归通过。后续两个候选只修改真机验收识别及明确错误后的验收续接，产品发送逻辑未变，Release 构建均为零警告零错误。日常安装与正式发布待后续交付。

本机 SSE 假服务配合原厂 CLI `2.1.319` 验证了运行中追加、追加轮次中断及同进程存活，记录为 `foreground-1107-offline-1794016316963603300/state.json`。对应会话为 `19033216-0053-4717-bc5a-9137802580b1`。这组早期测试只覆盖产品控制台工作进程及原生日志；后续真实追加和设备停止的证据见上表。DeepSeek 本身的停止仍未计为通过。

另补了两类历史记录。CLI 的 `informational` 系统记录仅连接原分支，内部文本不进入设备回复。工具执行中断通知 `[Request interrupted by user for tool use]` 只有匹配原始 `promptId` 时才归入该轮，另一提示下的同字文本仍视作真实输入。

真实模型的五条输入额度已用完，不再运行旧的计时或兼容重试入口。其预留标记和原始回执继续保留。后续停止验证仅使用新的本地假服务会话，不消耗真实模型额度，也不续发先前未知的追加。

## 11 月 6 日 Windows 真机验收进展

### 终端原会话的后台托管

直接运行的 Claude Code `2.1.318` 在本机登记为 `interactive`。两条现有终端会话都只声明了 peer 消息管道，能力为 `notify_idle` 和 `artifact_yield`，没有后台 job ID。此入口继续只读。

在 `2.1.319` 的独立终端里，首次用 `/bg` 得到了不同的后台任务 UUID，该结果没有计为同一会话验收。已验证的路径为先结束空闲的专用终端，再用官方 `claude --bg --resume <原会话 UUID>` 接续，核对后台 job、实时进程记录和历史里的 UUID 一致后才开启 TAB5 写入。一旦发现新 UUID、两个活跃持有者、进程代次或后台服务身份变化，桥接会拒绝操作。已验收的接口版本为 Windows `2.1.319`。

使用配对 USB 的 TAB5 `0.2.318-ui` 和真实 DeepSeek Flash，在同一终端会话 `12919a95-e62d-4e3e-9795-ffe46327b39d` 核对了三项：

| 操作 | 真机证据 |
| --- | --- |
| 发送文字 | 设备返回 202。原生用户消息 `cff03c13-7390-4223-a782-77d9c0036aae` 进入原会话。 |
| 发送照片 | 原生用户消息 `c33d1c02-d47b-46b0-9140-fc82bc315c5f` 引用了设备的 PNG。Claude 实际载入的文件内容与设备上传的 SHA-256 相同。TAB5 显示“蓝色”和“已完成”。 |
| 运行中追加 | 设备在目标轮次运行中追加 `w`，返回 202，原生用户消息为 `c5e374f6-1f30-4c0a-be40-99193ddb830f`。首次计数在键入结束前已经完成，因此补测预置了设备草稿并在新任务运行时点击追加。 |

文字和图片走当前用户的 Claude supervisor 回复接口。桥接会核对命名管道服务进程、任务和会话身份，只有原生主分支新增消息和内容匹配后才确认送达。照片还须匹配有序的图片内容摘要。只写入管道或只看到助手回复均不清除草稿。两份验收候选 DLL 摘要分别为 `23ea8fce53927084677bbca556006075f08da67634de8a82b11f3182a27575f5`（文字、照片）及 `7407195abcf4f49871532a3379ccc6534b1abc5ce0c04f36271b6cdd606c662c`（追加专项，产品接口相同）。原始记录在本地 `artifacts/claude-code-native-alignment-20261105/terminal-bg-03/`。

11 月 6 日另行验证了终端中断。TAB5 的停止请求 `3f84bba7-fbf5-41fc-b634-2226364cc4ea` 绑定运行轮次 `468879b8-70a0-4de7-7ed4-a30fa986293f`。候选 `8b8a4606560120b41851010523624c949cca4ea97fcd02fe0005f1558bb796ca` 经原 job 的终端数据通道尝试写入一次 Ctrl+C。设备收到了 504，后续为只读回执查询。原轮次完整输出并自然结束，未得到原轮次的中断标记。随后同一会话、同一进程 64260 正常回复了 `TAB5_AFTER_INTERRUPT_OK`。证据位于 `terminal-bg-03/device-stop-v8/{stop,continuity}.json`。原进程可继续使用，终端中断未通过。日常终端入口因此只开放已验证的文字、照片和追加，停止试验限定于具体验收会话。VS Code 的中断使用另一接口，见下节。

### TAB5 的 Codex 追加按钮和未读提示

同版候选的 BIN SHA-256 为 `bfab89ed5dc4881c56adf0a69d45c5c0152d65e8f06a94b6dda79ccac307aff3`。从禁用发送的 Claude 会话切回有草稿的 Codex 会话，原固件的界面回归失败，修复后的回归通过。第一次安装被设备的 `draft_pending` 保护拒绝。配对 USB 随后收到旧终端照片测试请求 `c13f9211-5ebf-48b2-a621-6df7a4b85c92` 的回执查询。按用户授权，核对设备、会话、请求、文字和附件指纹后，仅结束该请求的追踪，保留“送达未知”。两条未命中的 `q` 原记录未改动。原始加密日志、76 个字符和一张图片均保留，TAB5 对同一份草稿的正常同步返回 200。设备随后接受 OTA，重启后核验运行镜像 ELF SHA-256 为 `429ee9d5dba5b55385be71678c785804a0673f08821751fd229028e1543527c8`。

重启后，设备从本机保存的旧测试会话恢复了同一份带图草稿，再次同步。用户点 Codex 时看到“正在保存草稿，完成后切换”。普通桥接找不到原测试目录中的会话标识，临时验收桥接核对设备、会话、文字及图片后，收到原样的保存请求并返回 200。设备随后切回 Codex，“测试”草稿仍在，运行中的“追加”为浅蓝色。两块区域与另一张已确认正常的真机参考图逐像素一致。最初的 Windows OCR 未识别按钮文字，单独记录该误判。真实路径保留在本地 `codex-switch-firmware/device-verify-1793970407782331900/`。没有向实际 Codex 会话发送验收消息。

新启动的 Windows 原生接口验收使用普通桥接为该专用测试会话分配的标识和加密存储。若已有草稿、附件或未确认请求，验收拒绝预填。此变更避免新一轮验收留下普通托盘无法识别的临时会话标识。“对话”角标按有未读回复的会话累计。现场截图中 `TAB5-original-id-test` 显示绿色的“查看新回复”，确认测试会话贡献了未读数。设备内已有角标的来源无法逐项反查，未批量标记已读。

Windows 与 Mac 的历史解析已将 `away_summary` 作为连接前后轮次的记录，摘要内容不进入 TAB5 回复。Windows 已跑历史回归。Mac 已补相同回归用例，本次尚未在 macOS 编译或实测。

### TAB5 → VS Code 原会话 → DeepSeek

设备为配对 USB 的 TAB5 `0.2.318-ui`，VS Code 扩展为 Claude Code `2.1.318`。验收在独立 Windows 桌面和 VS Code profile 内运行。每次写操作来自设备页面，按设备请求 UUID 对照同一插件会话的 CLI 回执。测试结束后恢复原托盘。

**最终候选已通过两组 Windows 真机验收。** 两组都使用 DLL SHA-256 `90512d8b475d6af8c53274b973b20ffeee22e886e6688f2816fa3143bc53b2bc`，在正常用户环境通过启动检查。结果分别为：

| 批次 | 实际模型 | 设备、原生回执与屏幕结果 | hardware.json SHA-256 |
| --- | --- | --- | --- |
| `deepseek-live-09` | `deepseek-v4-pro` | 15:50:46 发送、15:51:05 运行中追加 `w`、15:51:33 中断追加轮次，均返回 202。回复页保留已输出的算式，显示“已中断”。 | `8d491521fbd0d0f51e3ef07d5e7467aa1e87b1882d8b3ec8b9e1c92febf38a6e` |
| `deepseek-live-10` | `deepseek-flash` | 15:53:11 从 TAB5 发送蓝色测试图和文字。原会话记录同时包含文字块与图片块，设备显示“蓝色”和“已完成”。 | `cd6e196d4224d06f0d3f26570bd3cefb4ec879e257fa70558e237a42e34f684d` |

两组都记录了显示设置和原托盘恢复。Windows Release 构建为零警告、零错误。历史解析、52 项工作区、31 项原生发送和 23 项回执检查通过。用最终候选重新读取批次 02、06 的真实会话，各得到两条用户轮次，部分回复保留，第二轮状态为 `interrupted`。

#### 问题定位与前序批次

- **发送、追加、中断。** `deepseek-live-02` 在 DeepSeek Pro 上依次取得三个 202。设备发送首条任务，运行中追加 `w`，待追加轮次实际开始，再停止该轮。中断同时收到原生控制应答和目标轮次的中断终态。该批次的最后一帧仍显示“状态未知”。
- **追加回执。** 此前的批次已经入队，但桥接等到追加内容出现在历史中才确认，超过设备的等待时间。当前桥接识别 CLI 对同一会话和请求 UUID 发出的 `command_lifecycle: queued`，并另行检查追加轮次何时真正开始。旧会话里结果未知的 `q` 保留原样。
- **照片。** 候选 DLL `1aaedff42581aa5620207342d147e8d55677d9c27a3d5f234f663bc40672d06d` 在同一 TAB5 上核验了两个模型。Pro 批次 `deepseek-live-03` 有原生文字和图片回执，模型报告图片内容不可见。Flash 批次 `deepseek-live-05` 收到同一张合成蓝图，在对应设备回复页显示“蓝色”和“已完成”。Flash 只指定在这次隔离的验收配置里。[DeepSeek 图片支持说明](https://api-docs.deepseek.com/zh-cn/guides/anthropic_api/)
- **中断后显示“状态未知”。** `deepseek-live-02` 和 `deepseek-live-06` 的原生会话都把 `[Request interrupted by user]` 写为用户类型的记录，沿用被停止消息的 `promptId`。旧解析把它算成第三轮，设备因此显示空白回复。现按主分支和同一提示标识把它归到原轮次，保留已有的部分回复，也保留另起一轮手动输入同字样的消息。已确认的停止回执可用于再次读取原轮次。
- **复核位置。** 每个批次的本地记录位于 `artifacts/claude-code-native-alignment-20261105/deepseek-live-NN/`，`hardware.json` 对应设备请求和原生操作，`hardware/*.png` 是直接从设备读取的画面。

Windows 这条写入路径要求 VS Code 的 `claudeCode.claudeProcessWrapper` 指向当前桥接程序。首次启用需由该配置启动一个插件会话，使扩展与桥接共享原厂 CLI 的收发通道。日常 VS Code 配置、正式托盘安装和固件均未替换。独立终端和 macOS 的原生接口仍须分别交付和验收。

**Mac 对齐边界。** 历史解析按主分支、原始 `promptId` 和中断标记识别停止，保留已有的部分回复。接入层须为每次追加校验原会话 ID 和 CLI 的队列回执，为每次停止校验私有控制应答和同一目标轮次的终态，再将确切结果写入私有记录。当前 Mac 窗口适配器只提供只读能力。Mac 原生验收应从扩展自带的 CLI 建立双向会话流，并用相同模型分工复测发送、追加、停止和照片。macOS 目标上的构建和这些实测结果独立登记。

**独立终端。** 本机 CLI `2.1.319` 的 `claude agents --json` 可枚举会话，`claude attach` 用于交互式接入，`claude stop` 会停止整个后台会话。三者不能直接替代当前轮次的追加与中断。使用自定义 DeepSeek 地址时，官方 Remote Control 要求的 API 端点也不匹配。产品目前把这些终端保留为只读会话。[Claude Code 会话管理](https://code.claude.com/docs/en/agent-view) · [Remote Control 使用条件](https://code.claude.com/docs/en/remote-control)

### 当前接入要求：通过会话协议后台调用

Windows 的 VS Code 接口候选现在把 `AIBotBridge.exe` 用作 `claudeCode.claudeProcessWrapper`。扩展仍启动原厂 Claude CLI；包装进程保留插件传入的启动参数、工作目录、环境及原权限通道。TAB5 的操作通过仅限当前用户的命名管道到达这个进程，双方校验管道另一端的 PID、程序路径和进程代次。设备请求 UUID 对应 CLI 用户回执。停止还要求私有控制请求被应答且目标轮次实际结束。未通过包装入口启动的普通终端与插件会话继续使用只读能力。

正式验收的 Windows 目标为真实 Claude 用户配置中的 DeepSeek；验收窗口使用隔离的 VS Code profile，不复制用户密钥或修改日常 VS Code 的设置。隔离假模型的 `fixture-ide-first` 测试曾停在登录页，实测结果是测试服务没有观察到输入，不能记为插件验收通过。后续测试脚本在新 profile 中使用扩展提供的 `claudeCode.disableLoginPrompt`。Mac 需要在 macOS 上完成同等接口连接和原生验收。

TAB5 的写操作需绑定原客户端会话和运行轮次，在用户切换窗口时仍能工作。工作区中的 Windows 和 Mac 分发代码现在要求适配器提供会话 API；普通终端或插件只有窗口输入通道时只呈现可读取的会话状态。旧候选的界面操作结果保留为历史记录。未部署这些源码改动到日常托盘。

新会话实际发出列数请求并返回 500 后，另一次运行中的 TAB5 追加已到达电脑。桥接在输入前返回 409，记录为 `desktop_foreground_unconfirmed`，没有键入或提交 w，停止未执行。已结束通过焦点、按键或模拟点击完成验收的方案。

Windows 上的 VS Code 插件 2.1.318 使用双向 `stream-json` 并支持机器级 `claudeCode.claudeProcessWrapper` 设置。此前的隔离测试通过这一启动约定调用插件自带的 CLI 和本机模拟模型。测试包装程序从 CLI 的输出绑定会话 ID，在第一个请求仍运行时接受独立来源的追加输入，并将中断响应交回对应调用方。同一会话的两个用户 UUID 都得到回执，追加轮次的中断终态为 `aborted_streaming`。这一测试仅覆盖包装程序的协议行为，真实插件与 TAB5 的后续结果见本节开头。

验证源码见 `tools/claude-vscode-api-probe/`、`tools/probe_claude_vscode_session_api.py` 和 `tools/probe_claude_vscode_extension_api.py`。包装程序是只能用于本地模拟模型及固定测试消息的探针，不能安装为生产连接器。[VS Code 扩展说明](https://code.claude.com/docs/en/vs-code)；[流式输入能力](https://code.claude.com/docs/en/agent-sdk/streaming-vs-single-mode)。

### TAB5 回复与纯文字发送

在新开的 VS Code Claude 面板上，插件保存了与当前选中会话 UUID 相同的 `sessionWithNoTranscript`。Windows 候选用这个字段和活动客户端身份补充会话目录；仅在记录目录可完整枚举、对应文件不存在、标记匹配时，才允许空的首次发送基线。已有但损坏的文件仍会阻止发送。Mac 同步了这条判断，待原生验证。

候选 `7ef297b85ff48ccd6c3f32c640a7c28655a1ced04cfe4f3f6cd7ae20b8a1c968` 通过配对 USB 从 TAB5 新会话发出纯文字，原 VS Code 用户轮次得到 202 回执，设备回复页展示到 500。随后设备输入 w 时，目标轮次已经完成，设备按钮已恢复为“发送”。该批次只计首次发送通过，追加和中断没有发出。原始记录保存在本地 `fresh-count-physical/hardware.json`。

已验收的照片在 .318 上返回了原 VS Code 会话的回复“蓝色。”。这份证据绑定桥接 DLL `0ff6a4e5525112fe7faa143d87eabf6786d6355432f078e9dbce4a058e6305ff`。随后一次自动输入 q 在调用原生提交之前停在输入区检查，桥接却返回 504 并锁住设备草稿。另一次手动 q 有独立请求号，已核实绑定原会话、宿主及测试草稿。该次旧托盘只保存了发送意图，缺少提交阶段记录。原会话历史未见 q，保留该请求的未知结果。

Windows 候选先等原输入框取得键盘焦点。原生适配器明确返回且没有调用提交时，桥接保存最终拒绝回执并保留两端草稿。已经调用提交、随后丢失回执的请求继续等待确认。新增检查覆盖重启后查询、重复请求、保留新草稿及下一次主动发送。候选 DLL `0db1fa9901ccc664da63b59cced7a6d0ba009e649fead872977d96870fe76b2d` 已通过 Release 构建、31 项发送、23 项回执、46 项工作区检查，以及正常桌面的启动检查。运行中追加和中断待新候选实测。Mac 端拒绝回执已同步到源码，仍待原生测试。

### 自动选图与文字回填已通过

现场失败时，文件框的可访问树缺少“打开”按钮（19 个按钮、精确 ID1 查询为零），但原生 `GetDlgItem(hwnd,1)` 存在，按钮可见可用，类型 `Button`，标题 `打开(&O)`。增加受限原生按钮分支，保留原文件框、前台、路径、图片摘要和会话检查，点击一次后等待界面确认附件。独立合成蓝图检查实际完成点击并关闭文件框。

另一处失败来自附件标签。实际节点是名称为 `blue-probe.png 96×96 Remove attachment` 的按钮，文件名没有独立文字子节点。已按这个结构解析文件名，并增加实际标签、含空格文件名、占位提示及不完整元数据回归。Mac 源码和对应解析测试同步，Mac 原生验证仍待完成。

候选 `92817e106d5da9c2d1e36195754265b9797c495b727bb504317f4fa048a3007e` 的连续实机桌面检查返回 `passed=true`、`imageCount=1`、`textMatched=true`、`submitted=false`、`openType=Win32.Button`。程序自动选择合成蓝图并填入“蓝色测试图片，检查附件和文字。”。这次没有模型发送，原始 TAB5 照片请求和后续追加、中断仍保留未通过状态。后续源码补充在共享键入方法内保持原有空闲状态检查，需要随最终候选继续验证。

后续在普通桌面启用新版托盘成功，Codex 已通过同一用户管道查询原测试会话。首次空白输入框误计 4 个界面图形为附件，用户确认没有附件后，将识别改为附件项的移除按钮和文件名。修正候选 `66f675f87c4116edb7354a36789a2c9cb60c2aa25cfe4cfc9bf59c8f565cba9f` 在现场读到空白、附件为零。

用户确认手动选图后照片缩略图已出现，文字为空。旧候选对当前附件读取返回 `native_attachments_unconfirmed`。进一步候选沿原始可访问树查找附件标签，并给文件选择器各步骤增加独立错误码，补充文件名输入框的中英文语义匹配。DLL `55c3b3760a333f1ebbe615e6356b08b5781448ce328a6a3349f95b5ae9c559ba` 已通过构建及 27/23/40 项原生检查，等待在用户保留的附件上只读核验。尚未部署或重新发送。

从 Codex 启动的固定 VS Code 真机批次已运行。设备加载测试图片和文字后发出一次请求，原生观察为 `StagingAttempted=true`、`SubmitAttempted=false`、`native_attachment_picker_unconfirmed`。发送和回执返回 504，照片未确认，后续追加与中断未执行。托盘恢复，显示设置保留。用户随后报告文件选择对话框曾打开但停住，并手动确认选图。原请求记录保留，禁止自动重发。下一步需核实文件选择器和实际附件节点，尚未通过的用例继续保留未验收状态。

TAB5 .318 在终端用例发出一次照片请求，发送和两次回执查询均返回 504。尚未取得同一轮的图片回复，已保留原请求且禁止重发。追加、中断尚未执行。随后从正常 Windows 桌面运行的 VS Code 批次在原会话输入框预检停止，设备请求数为零。记录只能定位到预检，无法区分输入框不可见和存在草稿。

候选 Windows 桥接让常驻托盘处理同一用户的本机查询。普通桌面用 `--arm-resident-native-suite` 绑定指定的测试会话和候选程序，调用方通过 `--resident-native-inspect` 查询输入框、`--resident-native-run` 启动已绑定的固定用例、`--resident-native-status` 查询结果。只有匹配的原会话、空闲空输入框以及已认证的 USB 连接可以启动。会话或程序变化、计划过期、设备忙碌时停止。实际测试期间托盘短暂让出 USB，再由验收进程恢复。

普通桌面的首次启用结果为 `original_vscode_binding_failed`。现场进程链为 Claude → VS Code 扩展宿主 → VS Code 主进程，启动主进程的上一级 PID 已退出。用同一现场进程作对照，旧代码抛出 `ArgumentException`，修正后能返回正确的 VS Code 主进程。发送前仍需独立核对原会话和当前选中的编辑器。

修正候选 DLL 的 SHA-256 为 `705e21e7d1589b59648088cea08ffe3dceea042de3b14b19231284a9751a52be`。Windows Release 构建、27 项原生发送检查、23 项回执检查、40 项会话检查及隔离管道测试已通过。现有托盘已实测返回“不支持的本机命令”，修正候选的完整实机链路仍待验证。Mac 的原生编译及对应的真实会话验收单独记录。

## 用户确定的目标

Claude Code 使用与 Codex 相同的产品逻辑：TAB5 定位、读取及操作原客户端中的明确会话，权限确认由该会话原有界面承接。移除桥接顶层常驻“Claude 操作确认…”入口，不另起一条由桥接代管审批的对话。终端 Claude Code 和 VS Code Claude Code 插件均在支持范围内，不能靠窗口标题或同一项目猜测会话。

此前 .617／.618 候选的 `ClaudeStream` 使用 `--print --permission-prompt-tool stdio --resume=<id>`，是桥接自建的非交互执行进程；这不满足本次确定的原会话接入。当前开始替换该发送后端，不能仅删除菜单后让旧后台进程失去审批路径。旧原包、源码和验收保留为原范围证据，不能套用到新实现；`778e433` 桥接候选暂不推进发布。TAB5 .318 原包及其独立界面验收继续保留。

## 会话、交互与验证约束

### Windows 托盘菜单调整设计

场景为用户从托盘打开设备管理，Claude 工具授权回到已选中的原生客户端。保持当前系统菜单字体、系统前景／背景色、行高、左右内边距和子菜单点击区域，不增加颜色或图标。根菜单继续按“设备中心／设备预览 → 设备分组 → 分隔线 → 退出”排列，移除常驻独立审批项及其入口处理；原生等待授权状态仍由会话展示。生产分发已禁止旧 print 后端回退后再移除入口。修改后以真实 ContextMenuStrip 离屏渲染检查文本、分隔线、退出位置和设备子菜单，不调用 Show、Activate 或 SetFocus；实际客户端授权与 TAB5 集成仍需分别验收。

Mac 同步设计：保留设置中的 Claude 分节、系统字体、次级说明色与既有垂直间距；原生默认后端接通后，移除独立审批按钮及处理函数，把说明改为“操作原会话、保留电脑草稿、权限在原客户端确认”。不新增弹窗或改变 Codex 开关；说明自动换行，Mac 端须用现有离屏设置预览检查最小窗口与常用缩放。未取得 Mac 实际渲染前不得登记 UI 验收通过。

- 对齐 Codex 的精确会话归属、原模型／权限设置、已有草稿保护、显式一次发送和送达核对。新输入、附件、当前轮次或宿主变化时终止原操作；送达不明不自动重发、不自动切换另一执行后端。
- 终端版与 VS Code 插件按同一 Claude 会话 UUID 识别具体宿主，各自使用可验证的原生交互通道；原生会话不存在、身份有歧义或通道不兼容时保留草稿并给出明确状态。不得自动创建 stream-json 会话作为回退。
- 录音回填、照片、追加与中断的实际支持必须按新链路验证，不能沿用旧自建 CLI 的成功结论。公开回复读取和不受影响的加密草稿可复用，但仍要保持设备／客户端／会话绑定。
- 权限请求保留在原 Claude Code 会话中，TAB5 显示已有的等待电脑确认状态；不在桥接生成独立工具审批窗口，不自动批准，不修改用户权限模式、模型、Claude 配置或全局设置。
- 桌面诊断保持后台，只读识别会话和输入区，不切换／置顶窗口。需要打开客户端时由用户自行操作；只有明确设备操作才可执行已绑定的原生发送。
- 用户进一步确认：正式使用时，在 TAB5 主动点发送可以唤起对应的原客户端。此偏好不授权后台诊断抢占前台，也不等于全局开启 Remote Control 或修改 Claude 配置。
- CLI 本地会话消息接口是跨会话消息，不可冒充用户授权或把消息送达当成工具授权；没有核验其语义前，不以它替代原生用户输入。

## 已核对的现场证据

Codex 默认桌面路径通过原客户端 IPC／原生输入框发送，`Tab5CodexDesktop` 明确禁止为桌面会话另起独立 CLI；任务审批回到桌面。Claude 旧实现存在差异，已确认需修正。

本机终端 CLI 为 2.1.318，已安装 VS Code 插件为 2.1.314。用户已打开终端 Claude Code 并确认也使用 VS Code 插件。只读探测验证了原生会话记录；首次终端读取因原 CLI 为管理员完整性级别 12288、普通桥接辅助进程为 8192，Windows 返回错误 5。未写入输入、未改变前台、会话身份保持。已请求用户保留原管理员会话并另开普通权限终端；不提升权限或绕过系统保护。

当前已完成 VS Code 原会话单次文字发送的隔离实测及两端原生操作候选源码；权限衔接、终端／插件双宿主完整操作和设备集成尚未完成，不能标记为全面修复或可发布。以下各阶段记录保留当时范围，最新状态以“终端、照片、追加、中断与 Mac 对齐候选”一节为准。探测证据在 `artifacts/claude-code-native-alignment-20261105/`，私人会话数据不进入 Git 或发布附件。

### 普通权限终端实测：消息管道不能充当用户发送后端

用户另开普通权限终端后，已只读核对原进程启动身份和本机会话管道，并通过操作系统返回的管道服务端 PID 绑定原进程。用户明确批准一条仅要求固定回复、不读写文件或运行命令的测试消息后，探测程序只写入一次；原会话保存了相同请求 UUID，并返回预期文本，没有工具调用。没有启动第二个 Claude 执行进程，没有切换或置顶窗口。

但原会话将该输入记录为 `origin.kind=peer`、`isMeta=true`、`promptSource=system`、`userType=external`，并设置 `queueSkipAttachments=true`。因此这仅证明跨会话消息接收和回复，不证明原生用户输入、照片发送、停止或权限流程已通过。不能仅凭回复或相同 UUID 将设备草稿清空，也不能伪造来源字段规避该语义。该候选通道不接入生产发送路径，探测源码和私人结果均留在被忽略的验收目录，未加入产品或发布包。

下一步核对 VS Code 插件原生输入框及动作的可用性。官方 URI 的 `prompt` 只预填、不自动发送；会话未找到时还可能新建对话，所以不能未经精确宿主和会话核对就用 URI 当发送确认。终端原会话完整操作通道仍未解决，不以隐藏 print 进程、跨会话消息或盲目向 shell 注入按键替代。

### VS Code 原会话：用户发送已证实，自动化链路未通过

已只读核对唯一 Code 窗口、Claude 原生进程的父进程链、插件保存的面板会话以及当前编辑器保存的 `sessionID`。这些身份均匹配同一会话，输入框及原生发送按钮具备可访问接口。读取时前台未变；Chromium 将非编辑提示文字返回为输入框值，不能仅凭字符串非空断言已有草稿。

用户明确批准固定测试句后，`ValuePattern.SetValue` 把句子放入输入框，但没有触发插件输入状态更新，发送按钮仍禁用。探测在发送前停止，没有重复填入。用户按要求输入一个空格并退格、保持原句不变后，继续同一次测试，仅调用一次原生发送按钮。原会话记录了唯一一条内容完全相同、`origin.kind=human` 的非 meta 用户消息，并返回预期文本；没有工具调用或新的 Claude 执行进程。

这不等于无人配合的端到端验收通过：原生输入事件仍需用户触发，发送调用前后观测到前台窗口变化，尚未独立隔离其原因。原生权限请求、照片、追加和停止没有在这条链路验收。探测仅保存在私人验收目录，未接入生产后端，未删除正式程序原审批菜单，未安装新桥接、推送或发布。

下一步由用户自行打开固定会话的官方 URI 预填验证入口，后台只读核对相同会话、完整文本与发送按钮状态，不发送第二条模型请求。该步骤用于替代 accessibility 直接改 DOM 的不完整输入方式；主动设备发送允许唤起客户端，后台核对继续不置顶窗口。

该入口实测未通过，不能作为当前原会话的替代发送通道。首次 `.url` 文件链接在 Codex 中被当作文本预览；改由用户通过系统运行打开 URI 后，没有应用预填文字。只读检查确认 Windows `vscode` URL 协议已注册，原插件进程和会话仍在。进一步核对本机 2.1.314 插件的 URI 命令处理：会话已在面板打开或恢复中的标签页中时，仅显示原面板并忽略传入 prompt，同时提供手动输入提示。这是正在使用的原会话场景的实际限制，不能用“支持 prompt 参数”的一般说明推断可用。原生预填候选停止采用，不继续让用户重复测试链接，不关闭／重开其会话来绕开限制，也不发送第二条测试请求。证据为私人目录 `vscode-prefill-check-result.json`。

后续候选输入探测只等待用户自行聚焦同一条原会话的空输入框；没有有效身份、完整空草稿状态、准确编辑器焦点以及键盘／鼠标释放状态时不输入。稳定满足条件后，一次性通过原生 Unicode 键盘事件键入固定 ASCII 验证标识，不使用 clipboard、不按 Enter、不调用 Send、不改变前台。写入前创建一次性日志，部分输入或观察不明时不重试；等待 90 秒仍未满足条件则不执行输入。该探测仍属于私有验证，结果与正式原会话发送、权限和设备验收分别登记。

第一次等待未执行输入：收到回复后，插件将 document 的可访问名称从固定产品名改为会话标题，探测错误地把产品名称作为 document 筛选条件，返回 `Unique native frame unavailable`。已移除这个名称依赖，仍核对同一进程、原会话 UUID、保存的编辑器状态以及固定控件标识；只读绑定重新通过。确认第一次没有创建输入日志后才重新等待，没有重试一次已执行的键入。

用户随后确认固定标识自动出现且发送按钮亮起。后台同样确认：全部键事件写入、精确文本匹配、原生发送启用、前台及焦点保持、只输入一次、未触碰剪贴板、未调用 Send、未发送模型消息。结果在 `native-input-event-result.json`；第一轮未输入结果另存，仍不将这次预填通过当作正式后端或完整设备发送验收通过。当前标识仅是原客户端中的未发送测试草稿。

### 原生文字送达证据组件

Windows 已新增独立的 `ClaudeNativeReceipt`，历史读取器只为主分支真实用户的纯文字输入保留摘要，不保存原始提示词。基线与后续观察必须属于同一会话／客户端进程代次、同一文字摘要、完整且延续的主分支；只接受唯一新增且精确匹配的用户 UUID。旧相同文本、助手回复、peer 输入、附件缺少验证、多个新输入、换会话／进程或截断／改支均不确认送达。该组件不发送消息，也没有单独接通生产发送；后端仍须在写入前持久登记请求，在已核对原生输入后调用一次原生发送，并用真实 UUID 关联回执。

Windows 新候选完整构建、历史回归与 15 项原生回执回归通过，现有实际原会话的输入摘要匹配、实际 peer 消息仍被排除。候选 DLL SHA-256 为 `4b957a522bc213bf262c0fa3be9f885d6998300a24bae8ce9c0e5fa1d575b4ce`，与先前仅历史修复候选分开保存。此组件的事实不代替原生完整发送、照片、中断、权限或 Mac 原生验收；旧 SDK 回执路径暂未替换。

### 原生发送组件与一次完整实测

`ClaudeNativeSend` 现已实现独立的单次输入／提交流程：先验证授权、空草稿、原宿主和完整历史基线，持久登记后只键入一次；只重读观察，不修补输入；精确文字与身份仍匹配才调用一次原生发送，再通过历史新增用户回执确认送达。控件失效或调用异常后仍只读核对，不重复点击。15 项定向回归覆盖电脑已有／后来输入的草稿、换宿主、撤销授权、部分键入、未知送达、原生控件失效及提交后输入框清空。

用户另行明确批准固定中文测试句后，该组件通过隔离的既有会话适配器完成了“原生键入 → 原生发送 → 真实用户回执 → 原会话公开回复”。实际会话只有一条精确匹配的人类来源用户消息，收到预期回复，没有工具调用、peer 通道、新 Claude 执行进程或剪贴板改动。实测辅助程序 SHA-256 为 `bb32b9eee47ab6aa09fe4e546a48eb6b272c65cbea95992cfaee63a45215e229`，使用的产品发送源码候选 DLL 为 `999a351e063c2ee7ffe39955373fba34e2ac82145d8335025f4bca3f5480800b`；此版本当时为 14 项状态机回归，随后新增的原生控件失效回归另行通过，不把新字节套用为旧原包。

`ClaudeNativeCode` 正在将该已证实的适配器改为动态识别：按原 session UUID 查找实际进程与启动代次，验证 Code 父进程、用户工作区数据库、当前编辑器 sessionID 和固定可访问控件。读取不激活窗口；只有显式授权发送才能唤起原窗口并聚焦原输入框。已移除固定 PID／测试会话依赖；在本机实际原会话的只读探测通过，确认原生输入后客户端已正常清空草稿。动态适配器候选完整构建及 15 项发送状态机回归通过，候选 DLL 为 `ce9a1c41ac200bef3f0928a2abd31fddea4517ba44c2832903f1f978b539dab6`。

证据：`NATIVE-E2E-PLAN.json`、`NATIVE-E2E-ACCEPTANCE.json`、`NATIVE-SEND-STARTUP.json`、`dynamic-native-code-inspect.json`。这份实测的范围仅为隔离组件；其后新增的生产分发单独验证，见下一节。当前动态 Code 适配器只接纳已核对的 2.1.314 版本及纯文字单行输入；不向未知宿主或旧 print 后端回退。

### 生产分发候选：源码接通，未安装或发布

`ClaudeWorkspace` 的正式默认路径已调用原生客户端适配器；旧 `ClaudeStream` 仅供显式注入的回归夹具使用，不再通过默认构造另起 print/resume 进程。TAB5 服务传入实时设备配对、会话代次和功能开关授权检查。文字输入前将会话、宿主代次、原始历史 UUID 集合及文字摘要保存到现有 DPAPI 日志；重启后仅以原生新用户记录确认送达，不重发请求。真实用户 UUID 与设备请求 UUID 分开，向设备只返回不透明轮次标识；后来输入的新草稿保持不变。

送达未知时使用 .318 现有的用户确认且不重发流程，仍校验设备、会话、请求及原草稿，不凭用户确认编造执行轮次。Windows 顶层常驻“Claude 操作确认…”及其处理入口已从候选源码移除。真实 ContextMenuStrip 的主菜单和设备子菜单已离屏渲染检查，覆盖默认字体及 1.5／2 倍字体，无 Show 或前台激活，文字、分隔线、设备入口和退出项正常。这是离屏字体放大检查，不冒充各 DPI 真机点击验收。

本阶段候选 DLL SHA-256：`a75f12577ef08852124c3b66d9ef1b51a6b696cd35535f32d5bfbf66504fd99c`。完整 Release 构建通过（0 警告、0 错误）；15 项发送、15 项回执、33 项工作区定向回归及既有 Claude 工作区夹具回归通过。历史回归新增“仅思考 end_turn 不完成、公开文字 end_turn 才完成”。Mac 同步了历史用例，仍需在 macOS 实测，不把 Windows 结果替代 Swift 构建。

正常桌面环境 `--status-once` 通过，配置和常驻桥接文件哈希不变。初次自动启动因资料目录被重定向而被 AppProfileGuard 正确拒绝；未改保护逻辑。资源管理器自动启动又被审批拒绝，随后由用户通过 Win+R 手动运行隐藏的只读检查，才取得成功结果。失败及成功记录分别保留，未停止或替换现有桥接。

证据为 `NATIVE-WORKSPACE-STARTUP.json`、`NATIVE-WORKSPACE-STARTUP-redirected-context.json` 和 `menu-preview/renders/menu-preview.json`。新的动态只读会话检查被同一目录保护拒绝，未取得本阶段的新检查结果；旧动态候选的成功记录不套用于这份新二进制。

本阶段当时剩余范围为终端原生操作适配器、照片、追加、中断、空白新会话枚举及 Mac 原生发送；后续进展见下节。此阶段验收只绑定上面的 a75 候选，不套用到后续字节。已验收 .318 BIN 不变，不刷机。

### 终端、照片、追加、中断与 Mac 对齐候选

本轮按用户要求新增以下生产路径，均属于未安装的工作区候选：

- Windows：通过同一桥接的短命隐藏辅助进程绑定既有 Claude CLI 控制台，核对 session、PID、进程启动身份、完整性等级和 raw input 模式。只向原进程控制台输入，不另起 Claude、不执行 shell 命令、不清输入缓冲、不改变终端模式。管理员终端不提升权限接管。
- VS Code：核对原会话、宿主和编辑器，通过原生 Add → Upload from computer 选择本应用已验证的照片，读回输入和附件后只提交一次；追加使用原输入提交，停止只使用原 Stop 按钮。
- 终端追加使用原生 `Ctrl+X Enter` 排队；空闲发送使用 `Ctrl+X Ctrl+S`；中断只对原任务输入一次 `Ctrl+C`，观察同一轮次变空闲。不能把 sendNow 当成运行中追加，否则会中断任务。自定义 keybindings 未核验时保留草稿并拒绝操作，不重写用户设置。
- 照片回执同时验证原文和按顺序排列的解码图像摘要，允许无损重新编码，拒绝漏图、错图、顺序变化、纯文字冒充照片。终端剪贴板仅在显式照片操作期间临时使用，读到附件后、且剪贴板没有被用户或其他应用更改时恢复。
- 追加和停止绑定当前不透明轮次及原生宿主代次；操作中身份变化立即停止。停止不会清除电脑或 TAB5 的未发送草稿，不编造新的用户消息或伪造 interrupted 回复。
- Mac：默认 `MacClaudeWorkspaceBackend` 同步切到原生后端，沿用既有加密草稿、照片、请求去重与回执恢复。终端按原生 PID/UTC 启动时间、宿主与当前 TTY 绑定；VS Code 按工作区和活动会话、Accessibility 输入框绑定。仅用户主动操作可激活宿主；审批留在原客户端，设置中的独立审批按钮和处理函数已移除。

当前限制和验收边界：仅适配明确列出的客户端版本（Windows CLI 2.1.318；Code 与 Mac 候选 2.1.314/2.1.318）。Mac 支持范围拟为 Terminal.app、iTerm2、VS Code 插件；VS Code 内嵌终端未纳入。原生终端空输入和单行边界必须可核对；不能仅凭 `Try ...` 文案认定为空，也不能忽略隐藏的多行草稿。无完整历史的新空白会话枚举、终端建议文字的可靠识别，以及不同客户端的照片编码行为仍须实机核验，不用猜测通过。

Windows 首轮完整构建为 0 警告、0 错误；23 项回执、15 项发送、40 项工作区检查和既有历史／旧 CLI 夹具回归通过。上述为离线合成检查，覆盖照片指纹、旧轮次拒绝、追加只提交一次、中断重试不重复以及双方草稿保留；不能替代原生控件／终端按键验收。Mac 已新增 `MacClaudeNativeTests`（原生操作状态机、照片指纹、生产工作区日志与重复请求），尚未执行 `swift test` 或 Release 构建。

`f46fd56fea22314b73bfc73e443b3951d6d0dd21137dbddf03006b78153f0053` 是本轮只读检查候选 DLL；其检查入口只运行 `--status-once` 和 `--inspect-claude-native-clients`，不发送消息、不操作控件、不替换常驻桥接。后续源码加强了终端完整输入区边界检查，最终构建须另记哈希，不能复用 f46 的完整包验收。正常桌面只读检查、两端原生完整操作、Mac 离屏设置渲染和 TAB5 集成尚待完成，发布继续暂停。

复核构建 DLL 为 `d73db314c286e0bf8258b4b17af3d432906f918e02e42b78e0e494772c214df8`，目录 `native-operations-reviewed/`。构建及全部定向回归再次通过：回执 23、发送 17、工作区 40，另有历史和旧工作区夹具；新增发送用例覆盖键入后原轮次改变及附件未确认。证据 `NATIVE-OPERATIONS-VALIDATION.json` 记录逐项退出码、源码文件摘要、基线提交和镜像身份。正常桌面检查尚待用户运行，Mac 源码／测试尚未在 macOS 编译执行，不能将它们标为通过。

供原生文件选择器读取的照片会从加密存储导出到应用自己的 `native-images` 私有目录（Mac 目录 0700、文件 0600），导出文件重新核对内容与链接属性，保留以供原客户端异步读取；这些文件不是加密文件，不进入仓库或发布包。回执日志只保留摘要，不能把文件存在、附件图标或本地提交动作当作送达证明。

#### 2026-11-06 正常桌面只读检查

用户手动运行 f46 检查入口后，`NATIVE-OPERATIONS-STARTUP.json` 确认正常桌面资料预检和 `--status-once` 均通过；配置及常驻桥接文件保持，未请求前台激活、安装或停止桥接。`native-operations-clients.json` 中一个终端为 idle、可读取、输入区非空、无照片；另一个终端未通过控制台检查；VS Code 抛出 IOException。该结果不能解释非空内容是实际草稿还是建议文字，也不能据通用 IOException 推断 VS Code 具体失败步骤。

因此仅补充诊断信息：控制台按身份、完整性等级、连接、raw mode、输入区及边界区分失败阶段；Code 仅输出既有固定 `native_*` 错误码，不输出会话正文或路径。终端诊断返回建议样式和光标位置的布尔／数值观测，仍不会凭该信息清空或覆盖输入。新诊断候选 DLL `6f62c2957101f62e33e2cc460d9ab416a9c174d02e2b56541f41fbcc374e37ad` 构建为 0 警告、0 错误，23／17／40 项定向检查及历史、旧工作区夹具通过。`NATIVE-DIAGNOSTIC-VALIDATION.json` 绑定候选哈希；此前构建与测试重叠导致文件复制重试，已停止重叠、完成构建后重新运行检查，旧摘要另存 superseded 记录，不套用结果。

下一项用户配合为显示原 VS Code 测试标签、保留终端现有输入，并运行 `readonly-native-diagnostic-check.lnk`。该入口依然只读、不激活窗口，等待新的具体识别结果；没有发送新增模型请求，没有改动 Mac 源码或部署任一候选。

用户随后完成该入口：6f62 的正常桌面启动及配置／常驻文件保持均通过。新的 `native-diagnostic-clients.json` 确认原普通权限终端和 VS Code 均能准确识别，状态均 idle；VS Code 原生提交控件可定位。两者输入区都被判为非空。终端无照片，光标列为 2，且不是当前诊断定义的 `Try ` 建议样式；这些观测仍不证明是实际草稿或某种其他提示。第二终端明确失败于 `native_console_integrity_unconfirmed`，保持权限边界，不尝试提权。当前等待用户区分旧测试文字、自有草稿和灰色提示；只允许用户自行清理旧测试标识，不自动删除输入、不新增发送、不将识别通过写成操作验收通过。

#### 照片验收结果与自动执行边界

用户清空先前的测试文字后，批准向原终端和 VS Code 测试会话各发送同一张合成蓝图一次。6f62 两项均停在输入区预检，没有提交。随后修复终端裁剪空提示符导致误读上方历史的问题，并对齐两端已核对的插件提示文字。1bb9 候选的终端预检从非空变为 idle／空／无照片；工作区在调用原生键入或提交前返回 409 `native_history_unconfirmed`。测试工具已保留该次 attempt 文件，不重跑该请求。同期 VS Code 原宿主不在，记录 `original_scope_unavailable`，不向替代进程投递。这两项均未取得照片送达回执或同轮颜色回复。

无需再操作客户端即可复核：终端实际记录的选中分支在一个合法 `system/turn_duration` 节点中断；不是 16 MiB 尾部限制。只读结构复核表明纳入该节点后可从叶子追溯 22 个节点至根。Windows 和 Mac 只把明确属于同会话主链、具有 UUID 和父节点字段的耗时记录作为连接，不公开其内容，不计作用户输入、回复或新分支。未知子类型仍不确认历史。Windows 修复候选 DLL SHA-256 为 `892251e4bb8068f3aba5cfbd5455ec7de051f167a58152f1c2fa87032467397f`。构建无警告和错误；历史回归、23 项回执、22 项发送、40 项工作区检查和旧工作区夹具通过。Mac 源码及对应测试已修改，未在 macOS 编译验收。

日常使用由常驻桥接处理 TAB5 的主动操作，不应要求用户运行快捷方式。开发验收中的 Win+R 用于在正常桌面资料目录运行隔离候选：Codex 环境直接运行的只读进程仍被目录保护拒绝。自动构建、日志分析、离线回归均由开发端执行。真实桌面验收按已授权的测试清单集中运行；不设置可执行随意命令的常驻代理，不为自动化关闭目录保护。当前无新桥接安装、真实照片送达或公开发布。

#### Windows TAB5 合并真机验收

`tools/run_windows_tab5_native_suite.py` 读取冻结的候选 SHA-256、两个原宿主的会话范围和结果目录，先独立检查两端。进入实机阶段时使用 `--tab5-native-suite`，通过认证 USB 的自动触控和屏幕截图操作已验收的横屏 .318。测试使用独立目录保存草稿、合成照片和请求记录。电脑预置蓝色图片与草稿，TAB5 的发送、追加和中断必须通过设备的认证 RPC 到达桥接。摄像头拍摄和照片上传属于其他验收项。

| 步骤 | 通过依据 |
| --- | --- |
| 照片发送 | TAB5 加载测试草稿并发送。桥接得到包含匹配图像的原生用户轮次，设备从同一轮读到“蓝色”回复。 |
| 运行中追加 | 屏幕键盘输入 q 启动较长回复，再输入 w 追加。设备的追加请求绑定 q 的运行轮次，回执给出新的原生用户轮次。 |
| 中断 | 设备在回复页对追加轮次点击“停止任务”。确认回执仍指向该轮次，原客户端随后处于空闲。 |

每个宿主首次实机执行前独占保留其运行记录，重复启动时读取已有报告。程序保存关键截图、请求与轮次的对应关系、显示设置核对结果。一个宿主不可用时仍可检查另一宿主。使用已登记、可核验的常驻桥接时，脚本在启动诊断前正常退出该桥接，在批次结束后恢复。最终批次的候选 SHA-256 为 `9b5576f93e6702aa105b8a1ec854ae0d7a686344c2d30f1add1e2e0a888438a0`。已验证 Windows 构建、中文动作标签的离屏识别、USB 自动化协议回归、原生发送／回执／工作区回归和重复启动处理。Codex 直接启动该批次的报告停在 `desktop_profile_redirected`，实机结果应以正常桌面启动后的独立报告为准。

正常桌面批次 `batch-1793931124787772900.json` 的终端预检就绪，VS Code 原宿主未找到。USB 交接脚本把托盘与同路径健康守护进程当成了两个桥接，因此照片、追加、中断均为 `not_run`。现场核验了两者的文件摘要、父子 PID，以及守护进程与父 PID 对应的映射名。脚本已按 `RuntimeHealthGuard.Start` 的实际启动参数识别该组合，仅向托盘走正常退出路径。两个独立托盘、其他可执行路径和不匹配的守护参数仍会阻止交接。此次改动在脚本层，候选 DLL 摘要同上。修正后已通过脚本回归及现场只读进程识别，实际设备回执仍须由后续桌面批次取得。

后续批次 `batch-1793931453438074600.json` 已完成。终端侧确认配对 USB、1280×720、0.2.318-ui。TAB5 发出一次照片发送请求，桥接返回 504，随后同一请求的两次只读回执查询也返回 504。原测试会话在该次请求后的只读记录中未发现新增用户消息或颜色回复。照片送达状态为未确认，追加和中断未执行。VS Code 原宿主仍不可用。显示设置核对通过，原常驻桥接已恢复。保留 `terminal/photo.attempt` 与原请求记录，勿重发。当前证据还不能区分终端粘贴、输入回读和原生提交中的具体失败位置。

Mac 交接必须从最终冻结的同一源码提交开始，保留现有改动，不使用旧 .617/.618 或旧 .314/.318 的桥接链路验收替代：

1. 执行 `swift test`、`swift build -c release`；若失败只修本轮原生接入相关问题，回传具体提交和构建输出。
2. 对 Terminal.app／iTerm2 和 VS Code 分别核对原生身份、空输入、已有草稿、多行／建议文字、辅助功能权限缺失；只读检查不得激活窗口。
3. 用户批准实际测试请求后验证：文字＋照片从 TAB5 到原会话、公开回复回到同一轮；运行中追加不停止原任务；中断只停止选中任务，双方未发送草稿不变；权限请求留在客户端，不自动批准。
4. 验证窗口／标签切换、客户端退出或升级、后来的用户输入、送达未知与桥接重启：保留草稿，不自动重发，不另起 print/resume；已有请求回执可只读恢复。
5. 保存 macOS 设置页实际离屏预览，检查删除审批入口后的说明换行和控件间距；记录包 SHA、源码 SHA、验收范围与剩余项。不得自行发布、升级桥接版本或刷 TAB5。

### 本次独立修复：原生上下文节点截断历史

上述原生用户消息和回复之间存在 `type=attachment` 的上下文节点。Windows 和 Mac 原读取器只建 user／assistant 节点图，导致沿父 UUID 回溯时中断，漏掉真正的用户送达记录。两端现已保留同会话、有效 UUID、明确父节点的 attachment 作为透明连接；不输出其内容、不把它当用户回执，也不让晚到的旧分支 attachment 改变选中的消息分支。额外排除 `origin.kind=peer` 的用户形状记录，避免仅凭 UUID 或缺失 meta 标志接受跨会话消息。

Windows 完整 Release 构建、定向历史回归及普通桌面上下文 `--status-once` 通过，配置及原常驻程序文件哈希保持。修复候选 DLL SHA-256 为 `0e5c14fb0de5ced1a3165e44dacda40e545c7c3f55c794b05cc73de50714bdc8`。同一份候选读取实际 VS Code 原记录时，保留正确用户回执、回复仍归同一轮且不再标为截断；终端 peer 测试仍不会成为用户回执。Mac 已对齐实现及回归用例，尚未在 macOS 执行 `swift test`／Release 构建，不将 Windows 结果替代 Mac 验收。

证据：私人目录中的 `vscode-approved-test-receipt.json`、`HISTORY-FIX-VALIDATION.json`、`HISTORY-FIX-STARTUP.json`。其中旧常驻程序读取结果为回执未找到，修复候选的结果单独登记；不覆盖旧证据。TAB5 .318 原包继续保持，不因本次读取器修改重刷。

## 公开依据

- [Claude Code CLI](https://code.claude.com/docs/en/cli-reference)：交互模式、print 模式和会话恢复的区别。
- [原生会话枚举](https://code.claude.com/docs/en/agent-view#list-sessions-as-json)：`agents --json` 的身份、状态和待输入字段。
- [会话消息语义](https://code.claude.com/docs/en/cross-session-messaging#how-a-session-treats-an-incoming-message)：消息与原会话权限的边界。
- [原生快捷键](https://code.claude.com/docs/en/keybindings)：sendNow、queueSubmit、interrupt 和图像粘贴的区别。
- [Windows 控制台输入](https://learn.microsoft.com/windows/console/writeconsoleinput)：原控制台输入记录；不是生成 shell 命令或进程组信号。

## English

Claude Code must follow the existing Codex product model: operate on the explicitly selected native conversation and keep permissions in its original interface. Cover terminal and VS Code hosts, preserve exact identity, drafts, attachments, request receipts and unknown-delivery protection, and remove the bridge-owned approval workflow only when its replacement is functional. The earlier print/stdio candidate does not establish acceptance for this correction. Initial read-only probing identified an elevated terminal that Windows correctly refused to expose to a medium-integrity helper; no input or foreground change occurred. Native sending and both host paths remain unaccepted at this stage.

A user-approved one-shot probe reached the original medium-integrity terminal session and received the expected reply without tool calls. Its transcript nevertheless classified the input as a meta peer message and explicitly skipped attachments. This is not native user-send acceptance. The peer transport remains private diagnostic code, is not included in the product, and must not clear a device draft or replace the native client workflow.

The VS Code probe bound the existing editor, persisted panel identity and native process ancestry to one session. A single native button invocation produced an exact human-origin user message and the expected reply without tool calls. However, the user had to trigger a native input event after accessibility staging, and foreground identity changed during submission. Unattended/background acceptance, permissions, images, follow-ups and interruption remain incomplete; this probe is not a production backend.

The user permits an explicit TAB5 send to bring the corresponding native client forward; background diagnostics must still preserve foreground focus. The next isolated check uses the official native URI prefill entry opened by the user, followed only by read-only session/text/button verification, without another model submission or global configuration changes.

That prefill candidate failed. The initial local URL artifact opened as a text preview in Codex. After a user-initiated system URL open, Windows protocol registration and the original native process remained valid, but the installed 2.1.314 extension explicitly ignores the prompt when that session already has an open panel or a restoring tab. General URI prefill support therefore does not establish support for an already open conversation. Do not adopt this route, repeat the link exercise, close/reopen the user's session to evade the restriction, or submit another test message.

A subsequent private probe waits up to 90 seconds for the user to focus the original empty composer. It requires stable identity/focus and released modifiers, types only a fixed ASCII marker through one native Unicode-input batch, journals before input, never retries mutations, never presses Enter or Send, never touches the clipboard, and never activates a window. This remains separate from production send and physical-device acceptance.

The initial wait performed no input because the extension renamed its accessible document after the first reply. Removing the product-name filter while retaining exact session/process/editor binding fixed discovery. The user and private evidence then confirmed exact one-shot native input, an enabled send button and unchanged foreground/focus, without clipboard access or model submission. Keep the marker as an unsent test draft and do not equate prefill proof with integrated send acceptance.

Windows now also has an independent read-only native text receipt component. It retains input digests and requires the same session, host generation, exact text and unbroken main branch, with exactly one newly accepted matching user UUID. Fifteen focused receipt checks, history regression, the Windows build and actual native/peer transcript digest checks passed. It is not yet wired into the production sender; full sending, images, interruption, original-client permission handling and native Mac acceptance remain pending.

The native send state machine also passed 15 targeted authorization/draft/identity/single-action/unknown-delivery checks. A separately approved isolated live test then typed and submitted one exact Chinese prompt through the original VS Code interface, matched a real human-origin user receipt, and received the expected public reply without tool calls, peer messaging, a new Claude execution process or clipboard changes. Its accepted helper/source identities are recorded separately from later candidates. Dynamic session/process/workspace/editor discovery passed read-only inspection of that earlier candidate. The current dynamic Code adapter only admits the verified 2.1.314 client and single-line text.

The latest Windows source now wires native dispatch into the production workspace and removes the permanent bridge approval menu and handler. It persists an encrypted pre-input baseline, separates the native user UUID from the device request ID, recovers exact receipts across restart without resending, and preserves newer drafts. Fifteen send checks, fifteen receipt checks, thirty-three workspace checks, the legacy workspace fixture and the extended history regression passed. The Release build had no errors or warnings. Real menu controls were rendered offscreen at default and enlarged fonts without showing or activating windows; this is not physical DPI/click acceptance. The candidate DLL is `a75f12577ef08852124c3b66d9ef1b51a6b696cd35535f32d5bfbf66504fd99c`.

The redirected startup environment was correctly rejected, with no guard changes. Automatic Explorer dispatch was then rejected by approval review because of possible foreground effects. The user manually launched the hidden read-only startup check, which passed in the normal desktop profile and preserved configuration and resident file hashes. The latest dynamic inspection did not pass its profile preflight; the earlier inspection is not relabeled as this binary's evidence. Terminal native operations, photos, queued follow-ups, interruption, empty-session discovery and native Mac sending remain unimplemented; permissions and the integrated physical-device path remain unaccepted. No new bridge installation, firmware replacement, native-change push or release occurred.

Both history readers now preserve native attachment ancestry without exposing context contents or selecting a late attachment's old branch. Peer-origin records do not become user receipts. Windows Release build, focused regression, actual native-transcript reads and desktop-context read-only startup passed; existing configuration and resident files were preserved. Mac implementation and test cases are aligned but require native macOS tests and build. No bridge installation, firmware replacement, push or release was performed for this correction.

The subsequent working candidate adds native terminal operations, original-client photo attachment, queued follow-ups and interruption on Windows and macOS. It binds every operation to the original session/process/turn, fingerprints ordered decoded photos, preserves drafts, and never launches a fallback Claude worker. Terminal submission uses sendNow only while idle and queueSubmit while running. Mac production dispatch now uses the native backend and its standalone approval control is removed; the prior stream backend is explicit fixture-only. Windows offline checks passed (23 receipt, 15 send, 40 workspace checks, plus history and legacy fixtures). Native terminal/composer behavior, photo encoding, both hosts' real operations, Mac compilation/rendering and TAB5 integration are still pending. A `Try` prefix is never treated as proof of an empty draft. The read-only f46 candidate and subsequent stricter composer-boundary source must retain separate identities. The accepted TAB5 .318 firmware and resident bridge remain unchanged; this candidate is not releasable yet.

The reviewed Windows build is `d73db314c286e0bf8258b4b17af3d432906f918e02e42b78e0e494772c214df8`; all offline checks passed again, with send checks expanded to 17. `NATIVE-OPERATIONS-VALIDATION.json` binds source digests and check exit codes to this binary. Normal-desktop inspection and macOS compilation/native acceptance remain pending. Native photo selection needs validated plaintext exports under the application's private directory; these exports are not represented as encrypted and are never included in source or release assets.

On November 6 the user-launched f46 check passed desktop-profile startup and preserved configuration/resident files. One idle terminal was readable with a nonempty composer; another terminal and VS Code were unavailable, with insufficient failure detail to identify the cause. The diagnostic-only 6f62 candidate adds fixed stage/error codes and numeric/boolean composer observations without exposing drafts or weakening input guards. Its build and all targeted checks passed after serializing build/test execution. A new user-launched read-only inspection is pending; no new native model request or deployment occurred.

The user then ran the 6f62 check. Both the ordinary-integrity terminal and the visible VS Code native test session were identified successfully and idle, with nonempty composers. The second terminal was explicitly rejected by integrity checks; no elevation was attempted. User clarification of existing drafts versus suggestions is pending before any input. Startup and configuration/resident preservation passed; discovery is not send/photo/append/interrupt acceptance.
