# Windows W29 实现与验证记录

日期：2026-10-29。对应 [W29-01 至 W29-12 交接清单](WINDOWS-FOLLOWUP-FROM-MAC-20261029.md#windows-执行结果2026-10-29候选已安装)。候选 `0.6.13-candidate.20261029.w29` 已获授权安装；实际关于窗口确认版本。第一轮为本地开发结果，后续安装与资料保留证据见“保留本地 main 的候选安装”。未向用户 Codex 会话发送测试消息。

## 代码与现场

- 拉取方式：`git fetch --no-tags origin main`；远端 `main` 为 `572a7ee2bb5eda3ae7413110992f3a0de3aa8a2a`，确认是 PR #15 合并提交。
- 原工作目录：`C:\Users\yaououzhong\Work\AI-bot`，本地 `main` 仍为 `12e7732fa5c6020d7d830dcd41d448abd10b0643`。fetch 后显示本地领先 7／落后 28，不能 fast-forward；本次没有执行 merge、reset、rebase 或 stash。
- 原未提交清单为 `docs/development/TAB5-WIFI-PHOTO-20261023.md`、`software-photo-noise.jpg`、`software-photo-pose-0.jpg` 至 `software-photo-pose-3.jpg`、`startup-overview.ppm`；开发前后以 SHA-256 和 Git 状态复核保留。
- 开发目录：`C:\Users\yaououzhong\.codex\worktrees\windows-w29\AI-bot`，分支 `codex/windows-w29`。仅包含本次 Windows 源码、对应自测和说明修改。
- `git cherry` 显示本地仍有独立提交，不能只凭提交不等价断言功能缺失。第二轮按文件 blob 核对确认，账户与预览布局、首次设备引导已在远端基线内；414 个 Windows 代码文件与本地 main 完全一致，9 个差异仅为新版版本号和已核对的 OTA 修复。本地图库策展源资料仍保留在原 main；实际安装的图库包另行核对且不在覆盖清单中。
- 因本次禁止合并和标签，不能按工作树收尾规则“合并后删除”或“提交并打 archive tag 后删除”。该工作树与分支暂保留全部改动和验证产物，等待审查／后续授权，不能删除未存档工作。

## 实现边界

- 题卡输入只在唯一标题对应的前台 RootWebArea 内查找，题卡优先于主框。题卡含内部 composer 布局时仍由外层题卡拥有；可见题卡结构未知则拒绝，不回退主框。问题文本、节点与窗口构成身份，回答文字和 Skip→Submit 变化不改变身份。蓝色箭头只调用当前题卡的 Next／Submit／Send，永不调用 Skip、审批或停止。
- 录音 start 在语音窗口占用焦点前抓取目标；经过认证的配对设备摘要关联 taskId、voiceId、预览及一次填入。旧题／旧预览／已用片段拒绝且保留预览。现有 wire 没有 stage 的 voiceId 字段，因此由桥接关联同设备最新绑定录音及精确最终预览，不新增固件字段。进程重启丢失目标绑定时拒绝旧预览填入。
- successful stage 的完整读回文本及输入目标成为一次性提交凭据。用户在电脑修改草稿后，旧凭据失效；下一次明确录音追加并读回可确认新全文。提交不导航、不临时采纳新正文、不按 Enter。
- 原生调用前保存未知回执，复核设备／会话／目标／全文，调用一次。主框清空、题卡推进／消失或 Send→Send update 为原生确认；UI 不明确时只读队列／accepted 增量。未知和取消均不切换另一发送通路。
- 队列回执先要求本地和服务端基线都完整，再比较同任务／owner／账户／全文及所有旧别名。分页未完成、结构未知、旧消息迁移、旧 pending 变 accepted、附件部分匹配都不能证明本次提交成功。1800ms 的快照预算和独立只读 app-server 子进程不排在历史读取锁后；不修改 Codex 全局状态，不导出队列。
- 持久回执只含设备摘要、请求指纹、draft ID、时间和结果，DPAPI CurrentUser 加密、24 小时／512 条上限，写穿并原子替换；损坏拒绝提交。正文及原始队列 ID 不写入该文件，配对重建不会复用旧设备范围。
- 音源选项复用原设置字段，保留自动 DJI→TAB5 策略；显式选择离线设备不静默替换。物理端点沿用 WASAPI 共享采集及实际名称；DJI 继续要求与豆包同设备，其他显式物理端点沿用既有临时租约恢复。wire 的历史 `dji` 路由值继续表示电脑直采，真实名称由既有 `sourceName` 提供。
- 语音结果进入 review 时，先完成本次占用前台的恢复再返回，避免自动填入与 100ms UI 定时器竞争；用户主动换窗口时不抢焦点。原句尾、静音、取消和麦克风恢复常数保持不变。

## Windows 检查

参数均从 `Program.cs` 核对。下表保留首轮结果；后续 W29 已增至 93 项，必需的完整 `dotnet run ... -- --status-once` 已从正常 Explorer 桌面通过，见文末补充记录。自测采用 Release 构建和临时测试配置，未接管常驻桥接、实体串口或蓝牙设备。

| 命令／检查 | 结果 | 能证明的范围 |
| --- | --- | --- |
| `dotnet build windows-app\AIBotBridge\AIBotBridge.csproj -c Release` | 通过，0 警告／0 错误 | Windows 编译与既有打包目标 |
| `dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once` | 未通过，退出 2 | 工具启动环境触发数据目录重定向保护；未进入正常状态采集 |
| `--self-test-tab5-quick-console` | 通过，含 `WINDOWS_W29_SELF_TEST_OK checks=61` | 多题身份、精确追加、末尾光标回调、单次原生调用、异常／取消、队列别名、设备归属、持久回执与未知不重发 |
| `--self-test-tab5-rpc` | 通过 | 模拟 MTU23／247／USB、加密、防重、草稿与 OTA 路由 |
| `--self-test-tab5-voice-core` | 通过 | 音源选择、停止／排空、句尾、取消、恢复和加密 HTTP；音频／IME 为模拟 |
| `--self-test-tab5-voice` | 通过 | 另含真实 WinForms STA dispatch／编辑窗口生命周期；没有真实收音或输入法识别 |
| `--self-test-codex-tasks` | 通过 | 历史／已读／桌面 IPC／回执回归；25,192,341 字节历史夹具实际测试约 92ms，不能外推设备邮箱耗时 |
| `--self-test-tab5-remote-ota` | 通过 | 既有同版重装、停止 offer、HTTP／RPC 回环；不触发真实刷机 |
| `--self-test-upgrade` | 通过 | 芯片／镜像 SHA、两个 OTA slot、hash mismatch、pending boot 拒绝 |
| `--self-test-wireless-boot` | 通过，63 项 | Wi-Fi／BLE 认证三次采样、身份／challenge／重放／状态／取消，均为本地夹具 |
| `--preview-tab5-voice-settings <png>` | 通过且已检查图像 | 复用设置窗口、选项和说明无裁切；预览使用隔离配置，不保存用户设置、不打开麦克风 |
| `git diff --check`、`git status --short` | 已执行，diff check 通过 | 变更范围与格式；状态保留所有本次文件供审查 |

日志／设置预览保存在工作树的忽略目录 `artifacts/w29-windows-20261029/`。先前测试结果来自该会话命令输出；末轮构建、QuickConsole、RPC、Voice UI/Core、状态命令及只读现场复核另保存日志。没有凭据、识别正文或原始队列导出。

第一轮工具环境的 `status-once` 阻碍与仓库已有 [10-26 Windows 记录](WINDOWS-MAC-ALIGNMENT-20261026.md) 一致：Windows 已知目录表面路径正常，但打开的文件句柄被打包启动环境重定向。保留 `AppProfileGuard`；没有通过修改用户配置、关闭校验或将测试配置输出冒充正常运行来通过检查。第二轮从正常 Explorer 桌面启动冻结候选和已安装程序，两次 `--status-once` 均退出 0，详见下面安装记录。

## 只读测量与性能决定

本机 Codex 包为 `26.1023.6621.0`，从已安装资源核对 v11 desktop IPC 和 `thread/queue/list(threadId,cursor)`，没有复制第三方实现。

| 测量 | 本次观测 | 决定／限制 |
| --- | --- | --- |
| Windows UIA 身份 | 1 个 Codex 主窗口、1 个匹配标题的 RootWebArea、0 个精确 thread URL | 继续全目录唯一标题回退；没有证据支持 URL 优先定位 |
| UIA 输入框识别 | 逐节点跨进程属性读取 1813ms；缓存属性快照后 350ms，最终复核 396ms | 只改读取方式，写入／提交前仍实时复核。单机局部样本，不承诺全部场景同比提升 |
| 三次真实 IPC 快照 | 83/34/36ms；48/32/30ms；52/32/35ms；最终复核 68/41/39ms | 各组中位数约 32–41ms，没有据此添加后台历史缓存。慢历史与 USB/BLE 邮箱实际排队仍未测 |
| 本地／服务端队列只读快照 | 两者均完整可读，账户范围可用；654–924ms（含启动只读子进程） | 证明接口和结构，不证明真实提交已入队 |
| 真实 NAudio 缓冲夹具 | 六秒积压 192000 字节完整，峰值 400ms | 现有背压逻辑保留；不是六秒真实录音或设备传输结论 |
| 真实录音开始／停止到显示 | 本轮未采样 | 保留计时和当前阈值，不照搬 Mac 延时、不宣称句尾实机验收 |

最终复核同样取得完整本地／服务端队列（691ms），未发送消息；7 个原有未提交文件的 SHA-256 全部一致，原 `main` HEAD 和状态清单一致。

## 待验证与交付边界

第一轮结束时候选未安装；第二轮现已安装并核对实际窗口。无需实机的实现、构建、自测、只读 Codex 接口检查和设置图像检查已完成；没有将模拟确认写成 Windows 设备验收。

候选运行已经授权；用户随后要求所有实机验证留到 2026-10-30。今晚不操作实体设备或自行发送测试消息。明天按一次一个步骤验证连接、主框录音自动填入与蓝色箭头提交、引导、排队已保存即成功、两段录音及光标、题卡第一题／第二题、跨题拒绝、可用实体音源及用后豆包恢复。无需点击“回填”按钮。

Mac R35 排队实机仍待完成。连接中断恢复不是已知 Bug，不追加断链／长期压力专项门槛。前两轮只安装获授权的 Windows 本地候选，未改版本源；后续获准发布准备后才登记拟发布的 0.6.14，仍未推送、合并、打标签、发布或刷机。

## 保留本地 main 的候选安装

用户后续明确授权：先确认保留本地 main 已有功能，验证后安全安装 Windows 候选，保持配对、草稿与展示设置，再一次一个步骤实测；仍不允许推送、打标签、发布或刷机。

- 候选标识：`0.6.13-candidate.20261029.w29`，仅构建属性，不改变正式 `VERSION`。
- 对照范围：本地 main `12e7732` 与候选基线 `572a7ee` 的全部 Windows 代码文件。414 个完全相同；9 个差异为 `AIBotBridge.csproj`、`Tab5ConnectionForm`、`Tab5OtaCancellation`、`Tab5OtaStopSelfTest`、`Tab5Protocol`、`Tab5RemoteOta`、`Tab5RemoteOtaSelfTest`、`Tab5Rpc`、`Tab5UpgradeStatusView`，全部是远端新版或已回归的 OTA 对端改动。
- 首次添加设备、账户紧凑操作、TAB5 预览居中、图库安装／导读以及语音修复均已包含，无需再移植同一代码。`--self-test-onboarding` 在候选上通过，并检查最小窗口预览。
- 本地 main 与候选都默认 `LocalArt=false`；运行时图库来自现有安装包／用户目录。候选 payload 不含 `Assets/DailyArt`，不覆盖本地策展资源或用户图库。
- 最终候选 Release 构建零警告／零错误；隔离桌面的完整 `--self-test-public`、候选 QuickConsole（61 项 W29）和 onboarding 通过。既有语音／RPC 等路径仍采用上一轮已通过结果，候选识别号变化没有改动业务代码。
- 通过已有 Explorer 桌面进程调用正常隐藏启动，候选 `--status-once` 已实际退出 0，`AppProfileGuard.Validate` 通过；没有绕过保护。先前 Codex 工具环境退出 2 的记录仍保留。
- 安装与回滚证据目录：`C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w29-install-20261029`。其中 `source-parity.json` 保存代码对照，`candidate/` 是冻结安装内容，`rollback/` 与 `profile-backup/` 保留原二进制及受保护资料；均为本地忽略产物，不提交用户资料。
- 已正常退出原桥接与守护进程，仅覆盖原 Release 目录中的 43 个冻结程序文件，逐文件 SHA-256 校验通过；原 43 个文件均有可用回滚副本。安装位置为 `C:\Users\yaououzhong\Work\AI-bot\windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0`。
- `install-result.json` 确认安装通过、已安装 `--status-once` 退出 0、6 个配对／设置／草稿文件哈希一致。仅按既有规则恢复 ESP8266 自动轮播，实际 `/status` 的六页顺序及 15 秒间隔与操作前一致；未设置 TAB5 展示模式。
- 20:58 的 `post-result.json` 再次通过：受保护资料及 7,151 个当前图库文件的安装前后 SHA-256 全部一致。原 main 的 7 个未提交文件再次核对哈希一致，HEAD 和状态清单未变；末轮 `git diff --check` 通过，`git status --short` 已检查。
- 实际设备中心确认 ESP8266 固件 `0.5.7`、USB 已连接、Wi-Fi 待命；此为安装后真实连接回报，物理屏幕画面未由用户确认，不能替代显示实机验收。
- 已通过真实设备中心及“关于 AI-bot”窗口核对运行版本为 `0.6.13-candidate.20261029.w29`，原两台设备登记仍在。当前 TAB5 显示 USB 未连接／Wi-Fi 等待回报／蓝牙未连接；系统枚举到 CH340 和蓝牙串口，没有 TAB5 USB，尚不能验收录音和题卡。这不记为断链回归。

## 延后实机后的补充检查与发布准备

用户要求先完成无需实机的检查，并提前准备明天验收后的新版本发布。本轮新增 32 项异常回归，W29 合计 93 项通过；只修改 `Tab5WindowsFollowupSelfTest.cs`，运行逻辑未再次改动。

| 补充检查 | 结果与边界 |
| --- | --- |
| 回执写入失败／恢复 | 临时文件被独占时保持原回执字节；预留写入失败阻止原生提交；动作后写盘失败，当前进程保留已知结果，新进程回到持久未知，均不重发 |
| 24 小时／512 条 | 到期前保留、边界到期、满容量拒绝新操作但允许完成已预留回执；不驱逐未到期草稿；过期后回收容量，超大文件拒绝 |
| 提交取消／异常／并发 | 未知、异常、取消均只调用一次；同请求重放、换 requestId、变更 draft 和文字冲突均检查；并发 stage 返回 busy 后重放不追加两次 |
| `--self-test-tab5-ble-voice` | 冻结的已安装开发候选通过；模拟 MTU23/247/517、密文分片、流控、音频尾部、重复、取消及坏序号，无实体 BLE |
| `--self-test-device-registry` | 冻结候选通过；空目录、迁移、重复／禁用、原子保存失败、损坏／未来版本和备份均使用临时文件 |
| `--self-test-tab5-display-settings` | 冻结候选通过；真实本机 HTTP 回环与加密消息，模拟设备读回、认证、冲突、取消及页面合法性；不修改用户设备展示 |
| `--self-test-user-preferences` | 冻结候选在独立隐藏桌面通过；选择恢复不改未选字段、回滚备份、任务置顶、提醒去重、持久化及光标恢复；无实体设备 |
| `--self-test-profile-guard` | 冻结候选通过；确认工具环境会被保护阻止，未绕过目录保护 |
| `--self-test-codex-read-sync` | 冻结候选通过；账户／host／会话／turn 归属、过期与切换，均为夹具 |
| 必需 `dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once` | 从正常 Explorer 桌面执行源码版本 0.6.14，退出 0，目录保护通过，状态协议 version=1；未启动刷新服务或占用串口 |

日志与来源对照保存在工作树忽略目录 `artifacts/w29-preflight-20261029/`。首轮工具环境退出 2 与本轮正常桌面退出 0 分别保留，不掩盖最初失败。

发布准备将源码版本登记为 0.6.14，Windows csproj 和共享 Mac plist 元数据同步、`desktopPlatform=windows`；不构建 Mac 或固件。常驻开发候选保留原字节和版本，0.6.14 本地包另行构建和验证。明天最终包安装／实机确认之前不能宣称正式发布已完成，详见 [发布准备交接](WINDOWS-0614-RELEASE-PREP-20261029.md)。

22:06 发布准备包生成并完成独立复核：安装器自检／环境检测、14 项分发依赖许可、ZIP 的 106 文件逐字节验证、解压后完整 public 自测和离线工具检查均通过；2,557 个源码文件及三个包摘要一致。最终包程序的 QuickConsole（93 项）、RPC、VoiceCore 以及正常桌面 `--status-once` 也通过；后者的程序集哈希与 ZIP 内一致。安装器没有执行实际安装，离线工具检查没有访问设备。完整证据在 `artifacts/w29-preflight-20261029/PREPARATION.json`，包在 `artifacts/windows-0.6.14-prep-20261029/`；正式提交 SHA、Windows 实机、Mac R35、CI 和公开发布仍待相应步骤完成。

## 2026-10-30 Windows 实机验收（进行中）

- 环境：已安装 Windows 桥接 0.6.14，107 个安装文件校验一致；TAB5 实时报回固件 0.2.296-ui，USB COM9 已连接。本轮未刷机。
- Codex 直达主输入框首次录音自动填入：用户确认“可以填入”；11:52:41 桥接记录 `draft_append_verified`，尚无快捷发送请求。仅确认首次填入，不据此判定发送或题卡验收通过。
- 同次诊断：音源 `tab5`，状态 `review`，`microphone=restored`、`restoreFailures=0`；`caretEnd=False`，末尾光标尚未通过，需继续核对。麦克风恢复诊断不能代替用后独立豆包实测。
- 连续两段录音保留与顺序追加：用户确认“是的，都正常的”；11:54:04 再次记录 `draft_append_verified`，尚无快捷发送请求。第二段后仍为 `caretEnd=False`，不能把追加位置正确当成光标定位已通过。
- 首次蓝色箭头提交未通过：用户看到“未确认发送，请检查电脑，不要重复发送，可重新录音”；11:54:43 桥接记录 `composer_changed; attempted=False; confirm=not_started; elapsed=2313ms`。代码对应原生提交前 `Ready()` 复核失败，未调用原生发送；不能归因于队列回执延迟，也不能仅由该合并错误码判断是焦点、按键、输入目标或按钮节点变化。保留现场，继续核对，不重复发送。
- 用户确认失败后两段文字完整保留且未编辑。新增只读 `W29_SUBMIT_READINESS` 及提交日志细分 `ready` 原因，复用原有全部提交前条件，不放宽拦截或重新发送。第一次采样 `foreground_not_codex`；用户仅把 Codex 标题栏置于前台后采样 `ready`，输入目标、原生按钮与回执基线可用，队列读取完整。只读采样没有调用发送、聚焦或写入；当前结果不能反推 11:54:43 的确切失败原因。标题栏激活后的 `focused=False; caretAtEnd=False` 也不能单独证明填入时的失败原因。
- 本轮诊断源码 Release 构建 0 警告／0 错误，QuickConsole（W29 93 项）、RPC、VoiceCore 自测退出 0；证据目录 `artifacts/w29-acceptance-20261030/`。新增诊断仅在独立只读程序运行，常驻安装及昨晚发布包仍保留原字节，尚未纳入新发布包。下一次发送须通过新录音建立新凭据；旧凭据继续保持已消费状态。
- 11:59:28 必需 `dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once` 在正常 Explorer / PowerShell 7 上退出 0，目录保护通过；`git diff --check` 通过、`git status --short` 仍为原范围 31 个文件。首次误用旧 Windows PowerShell，辅助脚本加载 .NET 8 程序集失败；失败结果另存 `dotnet-status-legacy-powershell-failed.json`，没有进入程序或改变常驻桥接。
- 主输入框发送通过：新录音补充后用户点击一次蓝色箭头，本聊天实际收到完整三段测试文本，原有句号及段间空格保留；用户确认“这次成功了”。12:00:31 桥接记录 `native_action_confirmed; attempted=True; confirm=ui_reads=2,last=empty; elapsed=3469ms`。这是主框真实接收与设备成功反馈，不是排队路径或题卡通过；未重复执行先前失效请求。第三次填入仍记录 `caretEnd=False`。
- 两题题卡（后续澄清更正）：本聊天收到“大疆”与“usb和wifi都有”两题答案后，用户澄清这两题都是在电脑上回答，并非通过 TAB5。因此不计入 TAB5 题卡验收；12:02:49 桥接语音／草稿／发送摘要仍停留于主框测试，与实际操作相符，不另记为诊断日志未更新故障。此前对操作方式的理解作废。题卡录音填入、下一步和第二题提交仍全部待实测。
- 重新出两题后收到“大疆的Mini 2吧。”（用户对型号不确定）和“USB接收器。”；本轮确有新增真实桥接记录：12:06:48 `draft_append_verified`，12:06:53 `native_action_confirmed; attempted=True; confirm=ui_reads=2,last=empty`，音源 `dji`、`microphone=restored; restoreFailures=0`。与上一轮电脑作答区分记录；最后一次写入及提交有实机证据，用户已明确确认第一题录音结束后文字自动进入第一题回答框，该项实机通过；用户随后明确确认第一题点击 TAB5 蓝色箭头后电脑自动进入第二题；两题答案已收齐，末次提交有桥接成功回执，第一题→下一步→第二题提交的基本流程通过。`caretEnd=False` 仍保留为未解决项。
- 末尾光标实测补充：用户按光标测试步骤反馈“光标没问题的，在句尾”，用户可见的末尾位置通过。此前多次 `caretEnd=False` 与可见结果不一致，保留为即时 UIA 诊断准确性待核查，不能继续据此宣称光标功能失败；不删除原日志、不擅自将内部布尔值改为成功。
- 独立豆包恢复：按“无需 TAB5，在电脑上独立启动豆包语音”的步骤，本聊天收到“测试电脑麦克风恢复。”，桥接语音／填入摘要仍停留在上一轮 TAB5 操作。结合前序 `microphone=restored; restoreFailures=0`，本次用后电脑独立识别路径通过；不外推所有物理端点和异常恢复。
- 运行中发送：保持当前助手任务运行时，用户经 TAB5 录音提交，本任务在尚未结束的执行过程中实际收到“这是运行中发送测试。”；12:13:03 桥接记录 `native_action_confirmed; attempted=True; confirm=ui_reads=3,last=empty; elapsed=2594ms`，对应 12:12:55 的新录音填入。运行中接收与桥接确认通过；没有 `message_saved_in_queue` 回执，不能代替尚未执行的持久队列实机验收。用户随后确认 TAB5 显示发送成功，运行中发送完整链路通过。
- 主框原有换行／标点：用户先输入两行带逗号、句号和感叹号的草稿，再通过 TAB5 补录第三段；12:15:38 桥接 `draft_append_verified`。用户确认原换行和标点不变，补录接在第二行后面，中间仅一个空格，该主框场景通过，未提交此草稿。
- 持久排队、跨题拒绝及题卡连续补录仍待验证；Mac R35 仍单独待验。

## 2026-10-30 延时反馈与测量优化

用户反馈开始录音、识别文字、发送各步等待偏长；体验反馈题已提交，尚未执行录音中换题，跨题保护继续待验。实机填入摘要约 4.2–8 秒，发送确认约 2.6–3.5 秒；这不是识别本身的纯耗时。

对当前真实 Codex 窗口进行只读对照（`artifacts/w29-acceptance-20261030/read-performance-foreground.out`）：旧双扫描读取为 449/427/445ms，单扫描为 229/214/221ms，三次读取目标与文字均一致；中位数 445→221ms（仅这个局部读取约减半），会话标题解析 17ms，IPC 中位数约 96ms，完整队列只读约 639ms。首次非前台采样不可用并单独保留，没有把失败样本计为快速读取。

据此将 `StageBoundAsync.ReadDraft` 改为单次新鲜目标扫描，继续保留两次相同快照、写入前编辑器/题卡/正文实时复核、单次写入和全文读回；提交初检与最终 Ready 复用同一次扫描取得的目标和正文，保留动作前前台、唯一会话标题、输入状态、按钮身份及全文复核。没有更改任何等待常数、未知重发策略或豆包时序。整体端到端收益仍需安装后实测，不能套用局部 50% 的数字。

Release 构建 0 警告／0 错误；QuickConsole（93 项 W29）、RPC、VoiceCore 均退出 0，冻结待安装 payload 的 QuickConsole 也通过。本地候选证据目录 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w29-perf-install-20261030`；安装与最终核验结果另记。新增诊断和优化不属于昨晚冻结的发布 ZIP/安装器/源码包，正式发布前必须重新冻结并验证最终来源。

12:26:50 必需 `dotnet run --project windows-app\AIBotBridge\AIBotBridge.csproj -- --status-once` 在正常桌面退出 0。随后正常退出旧桥接并完成原目录更新，版本仍为本地 0.6.14 候选；107 个安装文件及 108 个回滚文件逐一 SHA-256 复核通过，实际只更换 `AIBotBridge.dll`、PDB 和文件校验清单。安装记录确认 7 个配对／设置／草稿相关文件与安装前一致，已安装状态命令通过，ESP8266 自动轮播六页和 15 秒间隔保持不变。新版进程 PID 61652，守护 PID 64484；本次未刷机，未清空电脑草稿。

辅助验证过程如实保留：首次在同一 PowerShell 内先后加载不同版本的辅助程序集导致源码状态辅助脚本报错，改为独立正常桌面进程后通过；安装后编码后台启动命令被自动审批拒绝，直接工具进程又被 `AppProfileGuard` 阻止。随后使用明确 `-File ... -Mode post` 的正常桌面只读命令获准启动，保留所有失败记录，没有绕过保护。图库完整安装后哈希复核及新候选端到端延时结果另记。新版只在本次进程启用既有耗时日志，记录阶段、状态和字节数，不记录正文、音频或凭据。

12:30:27 安装后复核通过：7 个受保护资料文件、7,151 个图库文件逐一与安装前哈希一致，轮播六页及 15 秒间隔保留，`post-result.json` 为成功。107 个安装文件和 108 个回滚文件也再次校验通过。当前 TAB5 不在线，系统仅枚举到蓝牙串口，没有 TAB5 USB；已请用户确认数据线，未把连接状态记成断链回归。新候选的实际录音／填入／发送延时尚待连接后测量，不能宣称体验问题已经解决。

12:40 重新连接 USB COM9 后进行了新版首次录音（DJI）。用户反馈文字填入正常、速度感觉差不多。日志分段：启动请求 3419ms，其中准备阶段（包括焦点与音源准备）2640ms、音源与触发 219ms；停止请求 12:40:46.942，首次非空识别轮询 47.892（约 950ms），进入 review 为 48.919（约 1977ms），内部 finalWaitMs=1422。12:40:52 填入检查 2500ms 结束但结果为 `composer_changed`，并非成功读回。对应最近 USB RPC 为 request=47ms、handler=2609ms、response=2094ms，合计约 4750ms；处理时长包含填入检查，不能重复相加，也不能把 RPC 回包称为 Codex 消息发送。本轮没有提交请求。

这轮不能作为成功填入提速样本：用户可见结果与写入确认不一致，需核对新文字实际出现位置并定位拦截条件；未自动重试或重复写入。整体等待偏长仍未解决。原始分段记录保存在新安装证据目录的 `voice-timing.log`，只包含时序／状态／字节数。此为重启后第一次录音，启动准备的冷启动影响尚未拆分，不能据单次采样直接缩短稳定等待或麦克风恢复保护。

用户随后明确确认新文字显示在电脑 Codex 输入框，而不是仅在 TAB5 预览；因此记录为可见填入正常但桥接未确认，不能简单标成填入失败。源码中 `composer_changed` 是写入前若干条件共用的拒绝原因，旧日志不能反推出具体条件或该次文字进入输入框的路径。之后的只读核对 `W29_DRAFT_WRITE_READINESS` 为 ready，此时输入框已空，不能代替当时的快照。

为下一次可复现采样补充诊断：`StageBoundAsync` 记录 writeReady 具体原因和 attempted，并写入本地耗时日志；`DraftWriteReadiness` 复用原有全部写入保护，不添加重试或放宽身份／正文条件。`Tab5VoiceHost.PrepareAsync` 分别记录恢复等待、豆包控制初始化、焦点稳定、音源准备、麦克风租约；没有改变等待阈值或音源选择。Release 构建零警告错误，QuickConsole（W29 93 项）、RPC、VoiceCore 通过，12:50:25 正常桌面的必需源码状态命令退出 0。新诊断候选及回滚证据目录为 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w29-diagnostics-20261030`；只更新程序 DLL/PDB/校验清单，7 个受保护配置／草稿文件另行前后核验。该次不重复遍历图库全量哈希，图库仍在写入范围外；全量校验沿用 12:30:27 的明确记录，不能把本次 GalleryFiles=0 写成图库全量复验通过。

12:52:09 诊断候选安装后复核通过：107 个安装文件、108 个回滚文件核对一致；7 个受保护资料文件保持哈希一致，ESP8266 自动六页轮播和 15 秒间隔保留，已安装 `--status-once` 通过。当前进程 PID 25728，尚无新录音。实时桥接未连接 TAB5；另用实际串口列表和 PnP 在场设备核对，只有 CH340 COM7 和蓝牙 COM3/4/5/6，没有原 TAB5 COM9，原因未确定，不标为专项断链验收或已修复故障。需要用户确认物理连接后继续一次录音采样；文字已出现但未获桥接确认、整体延时仍待定位，未宣称修复。

12:53:34 用户重新连接后 USB COM9 实时采样正常。诊断候选首次录音：用户确认文字进入 Codex 输入框且未点蓝色箭头；12:54:02 桥接 `draft_append_verified; writeReady=ready; attempted=True; elapsed=3094ms`，本次可见填入与桥接确认一致，主框填入通过。没有提交请求，不能用于新版发送耗时验收；此前 `composer_changed` 未复现，确切原因仍未知。

本次时序：启动请求 2940ms；准备恢复 0ms、豆包控制初始化 31ms、焦点稳定段 781ms、音源准备 1328ms、麦克风租约 110ms，整个准备 2328ms，音源／触发段 203ms。停止回执为 12:53:54.810，首次非空识别轮询 55.725（约 915ms），首次 review 回报 58.942（约 4132ms）；56.185 与 58.942 之间相邻轮询间隔 2757ms，桥接记录两次请求处理均约 0ms，但这段包含尚在 recognizing 的时间，不能直接断言全部是传输空等。填入确认 12:54:02.261，停止请求到桥接确认约 7451ms；填入处理 3094ms，与首次 review 回报间另有约 225ms 调度间隔。内部 finalWaitMs=1313 不等于用户停止到可见文字的完整等待。

以上是单次实机样本，文字、会话状态及冷启动条件与旧样本不同，不计算整体提速百分比。音源准备、轮询间隔和填入处理是后续定位点，未缩短稳定阈值或自动重试。实时摘要中的 12:54:05 USB RPC 已是另一请求，不能把其 16ms handler 当成本次填入耗时。样本另存诊断安装目录 `SUCCESSFUL-STAGE-LATENCY-SAMPLE.json`。

## 2026-10-30 参考 Mac 的传输衔接优化

- 基准仍是 12:53–12:54 的 Windows 实机样本：启动 2940ms，停止到填入确认 7451ms，其中填入 3094ms。相邻识别查询有 2757ms 间隔，但不能据此断言全部由资源传输造成。
- 核对 `origin/main=326b8bb`（PR #16），其新增合并没有修改语音运行实现。参考 `MacTab5Voice.needsTransportPriority/observeReply/acknowledgeUSBReply` 和最终结果短等待，不搬用 AX/CoreAudio API 或 Mac 实机结论。
- **已完成（代码与模拟验证）**：新增 `Tab5VoiceTransportPriority.cs`，覆盖识别完成到设备下一次查询之间的后台资源优先级空隙。只有匹配本次录音的 USB 最终回复全部分片 ACK 才释放；旧录音／旧 nonce 不可释放新录音，丢失回复保留优先级但最长 30 秒，状态心跳不受暂停规则影响。集成在 `Tab5Service`、`Tab5Rpc`、`Tab5UsbRpc`、`Tab5BleVoice`。
- **已完成（代码与模拟验证）**：USB/BLE 的 recognizing poll 最多等待 350ms，UI 线程继续处理识别；如期间已满足原稳定条件，当前查询直接返回最终文字。保留原 1200ms 收尾和 800ms 文字稳定阈值，取消返回取消，录音被替换则拒绝旧查询，Wi-Fi 保持原立即回复。位置：`Tab5VoiceSession.HandleAsync`、`Tab5VoiceHost.HandleAsync`。
- **已完成（代码）／实测待验证**：按此前 214–229ms 单次 UIA 扫描测量，光标定位的初次查找复用同一新快照，减少一次重复扫描；动作前后仍重新检查目标／文字。另增加音频端点枚举、物理采集启动、写入检查、SetValue 和光标阶段计时，均不记录正文。
- Release 构建 0 警告／0 错误；VoiceCore、VoiceResourcePriority、BLEVoice、RPC、QuickConsole（W29 93 项）全部退出 0。新增模拟检查覆盖当前 poll 取最终结果、修订中不提前完成、取消、录音替换、调用取消、等待上限、旧路径、旧 ACK、部分分片 ACK、失败不提前释放与租约到期。证据：工作树 `artifacts/w29-mac-reference-20261030/`。
- **待验证**：新候选的 Windows 实际端到端耗时和录音／题卡回归。尚无新实机录音，不宣称已经达到 Mac 速度，不给提速百分比。启动音源准备 1328ms 的细分仍待下一次实际录音，未为提速跳过麦克风租约或稳定条件。此前 `caretEnd=False` 与用户可见句尾位置一致的诊断差异仍保留。
- 发布准备：10 月 29 日 ZIP/setup/source 继续作为历史候选保存，不能用于发布新改动；最终验收后重做冻结与摘要。Mac R35 排队实机仍待完成，没有推送、合并、标签、发布或刷机。

13:04–13:05 本地候选安装完成，实际进程 PID 68980，安装目录保持原路径。正常桌面安装脚本确认 107 个候选文件和 108 个回滚文件、7 个配对／草稿／设置文件一致；必需源码 `dotnet run ... -- --status-once` 于 13:03:33 退出 0，安装后程序 `--status-once` 也通过。只读复核再次确认 107 个已安装文件与候选一致、进程路径正确、自动六页轮播及 15 秒间隔保持。图库不在写入范围，未重新遍历 7151 个图库文件。

独立的桌面安装后复核启动被自动审批以 `blocked by policy` 拒绝，未给具体原因，未重试该被拒动作。工具进程直接读取 AppData 时取得旧隔离视图（多项修改日期停在 9 月／10 月初），不能用其哈希冒充实际桌面配置变化或实际桌面重验。配置保留证据限定为已成功的正常桌面安装阶段；启动后的实际桥接只读诊断另确认 `test=False; pairMatchesDisk=True; modeOnDisk=auto`。启动后 7 文件全量哈希复验未完成，明确记为 `PostLaunchProfileHashRechecked=false`，未绕过 AppProfileGuard。

证据在 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w29-mac-reference-20261030/FINAL-VERIFICATION.json` 及同目录 preflight/install/status 记录。13:05 USB 实时未连接，语音 idle、麦克风 restored、无录音请求；尚不能进行新版延时实测。工作树 `C:\Users\yaououzhong\.codex\worktrees\windows-w29\AI-bot` 与 `codex/windows-w29` 保留供未完成实测及后续提交，不执行用户禁止的合并／存档标签／删除；原 main 的 7 个用户改动未变。最终 `git diff --check` 通过，`git status --short` 为 35 个本任务修改／新增路径。

## 2026-10-30 13:11 新候选真实录音与启动优化

用户重新连接 USB COM9 后，在 Codex 直达录音并确认自动填入，没有点击蓝色箭头。13:11:51 桥接 `draft_append_verified; writeReady=ready; attempted=True; elapsed=3015ms`。停止回复 13:11:46.991，review 回复 48.645，桥接填入确认 51.791：停止到 review 1654ms、停止到桥接填入确认 4801ms；旧样本分别 4132/7451ms。当前查询短等待生效（一次 355ms 仍 recognizing，一次 112ms 返回 review），原稳定条件保留。两次文字长度与负载不同，只记录样本差异，不宣称稳定提速比例。

SetValue 于 13:11:50.181 返回，之后光标步骤耗时 1110ms，故 4801ms 是桥接确认口径，不是精确可见文字出现时刻。对应 USB RPC 记录 handler=3047ms、response=1532ms，设备收到完整回复的耗时还长于桥接填入确认；这 1532ms 的原因仍待定位，不把它遗漏为零传输开销。用户可见填入通过，caretEnd 诊断仍 False，蓝色箭头发送本次未测试。

启动请求 3245ms（旧 2940ms），并未变快。新增细分首次明确：输入端点枚举 640ms、播放端点枚举 985ms、实际物理采集启动 110ms，输入准备共 1750ms。已据此修改 `Tab5VoiceAudio.StartCore`：物理麦克风成功启动时直接返回，只有 TAB5 路径或自动物理打开失败的回退才枚举播放端点；明确指定的麦克风缺失仍拒绝，`ValidateTab5`／切换预备仍验证虚拟通路，不使用过期设备缓存。此优化实际节省时间待下一次录音。

该最小补丁 Release 构建 0 警告／0 错误，VoiceCore、VoiceResourcePriority、QuickConsole（W29 93 项）退出 0。测量摘要保存在上一候选证据目录 `FIRST-RECORDING-SAMPLE.json`，构建／回归在工作树 `artifacts/w29-audio-lazy-render-20261030/`。其余待验项目及发布边界不变。

13:14:35 启动优化候选已安全更新并运行（PID 48700），USB COM9 当前连接，语音 idle、麦克风 restored。必需源码 `dotnet run ... -- --status-once` 于 13:13:37 退出 0，已安装程序 status-once 通过；107 个安装文件复核一致，108 个回滚文件保存，正常桌面安装阶段 7 个受保护文件一致；六页自动轮播／15 秒配置保留。没有再次执行此前被拒的桌面 post 启动，启动后配置全量哈希和图库全量哈希仍不标为已重验。证据：`C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w29-lazy-render-20261030/FINAL-VERIFICATION.json`。补丁尚无实际录音样本；`git diff --check` 通过，35 个未提交路径保留，未推送、合并、打标签、发布或刷机。

## 2026-10-30 13:15 启动优化实测

用户通过 Codex 直达再次录音，结束后自动填入并回复“好了”，没有点击提交。USB COM9 正常，桥接确认 `draft_append_verified; writeReady=ready; attempted=True`，麦克风 restored、恢复失败计数 0。

| 指标（毫秒） | 13:11 前一候选 | 13:15 延后播放枚举候选 |
| --- | ---: | ---: |
| 启动请求处理 | 3245 | 1896 |
| 输入端点枚举 | 640 | 563 |
| 播放端点枚举 | 985 | 未调用 |
| 物理采集启动 | 110 | 109 |
| 音源准备总计 | 1750 | 687 |
| 停止回复至 review 回复 | 1654 | 2382 |
| 停止回复至桥接填入确认 | 4801 | 5419 |
| 填入处理 | 3015 | 2859 |
| 写入前检查 | 593 | 688 |
| SetValue 调用 | 16 | 16 |
| 光标处理 | 1110 | 906 |
| 对应 USB 填入回复传输 | 1532 | 172 |

本次启动补丁实机生效：实际大疆路径未枚举播放设备，物理采集打开时间基本相同，启动减少 1349ms；其他阶段也有波动，不能把全部差值归因于移除枚举。停止到填入确认并未比紧邻样本更快，仍不能宣称整体延时已解决。USB 回复 172ms 表明前次 1532ms 不是当前可重复的固定耗时；本次没有 recognizing poll，首次查询已返回 review，停止至 review 的间隔不能全部算成识别本身。内部 finalWaitMs=1438，仅是识别收尾口径。输入框处理约 2.86 秒仍是明确的后续测量点；caretEnd 仍为 False，不推翻用户此前可见句尾位置正常的验收，也不将其写成已修复。

本次未修改／安装程序，仅保存 `windows-w29-lazy-render-20261030/FIRST-RECORDING-SAMPLE.json` 与验收文档，未测试蓝色箭头发送。W29-09/10 启动播放枚举优化记为“已完成（本次 Windows 实测）”，整体耗时稳定性与其余交互仍“待验证”；没有追加断链专项验收。最终 diff 检查通过，35 个任务路径保留。

## 2026-10-30 填入路径优化（只读对照通过，已安装待录音实测）

用户明确要求解决填入慢。依据 13:15 实机填入处理 2859ms、光标 906ms 的测量，检查发现 `FindTarget` 每次仍用 RawViewWalker 跨进程逐层读父节点；一次填入的多次安全复核会重复这些调用。候选改用 `GetUpdatedCache` 获取**每次观察全新的 raw subtree**，随后用 CachedParent/CachedChildren 在进程内遍历。不跨观察复用旧快照；仍扫描全部节点验证唯一输入框／题卡，仍现场读取文档标题、题卡身份和正文，并保留写入前后复核及单次写入。代码：`Tab5CodexComposerTarget.cs`；API 语义核对 [Microsoft CachedChildren 文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationelement.cachedchildren?view=windowsdesktop-10.0)。

`Tab5CodexComposer.PositionCaretAtEnd` 增加只读的“已在句尾”路径：先验证绑定与完整正文，若选区已是句尾则复核后返回，不再次 SetFocus。否则仍复核后聚焦／选择句尾；目标改变拒绝移动，不触发任何发送或重复写入。新增 10 项检查覆盖已在句尾、正常移动、动作前目标改变、焦点回调改变目标和选择后的变化；W29 自测现在 103 项。

构建 0 警告／0 错误，QuickConsole／RPC／VoiceCore 退出 0。两次显式只读测量均返回 `foreground_not_codex`，未取得有效的新旧耗时，不能宣称该候选已提速或完成 Windows 验收。已请求用户仅激活 Codex 标题栏；实际录音和提交不属于此次只读测量。证据在工作树 `artifacts/w29-stage-performance-20261030/`。当前运行仍为 PID 48700 的已验收启动优化版本，待新旧目标／文字一致性和耗时检查通过后再安装候选。

14:04 用户再次确认已激活 Codex 后，工具启动的诊断仍立即读到 `foreground_not_codex`；改用正常桌面的透明只读脚本检查启动环境。脚本先采到前台进程 WindowsTerminal，随后诊断读到正确 Codex 主输入框，说明这些前台失败不能归咎于用户没有点击。没有通过代码激活、聚焦或输入；AppProfileGuard 正常通过。

新旧定位方式在同一输入框三轮对照：旧单次 637/456/453ms，新单次 117/115/152ms，三轮目标身份与正文均相同。新旧都只有一次观察，不再沿用此前旧双扫描的对比口径。写入前只读检查 `ready`，当时主输入框空、0 换行；没有可用发送按钮符合空框状态。此结果证明定位阶段改善，不能据此预报完整填入提速比例，也不替代题卡及实际追加文字验收。正常桌面检查输出保存在 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-w29-stage-cache-20261030/desktop-inspection.out`，14:04:54 完成。

14:06:18 定位优化候选安装与只读复核完成，当前进程 PID 48668。107 个安装文件哈希复核一致，108 个回滚文件保留，正常桌面安装阶段确认 7 个配对／草稿／设置文件不变；安装前／后 status-once 通过，六页自动轮播与 15 秒间隔保留。实际桥接 `test=False; pairMatchesDisk=True; modeOnDisk=auto`，麦克风 restored、无恢复失败。启动后的配置全量哈希与图库全量哈希未重新执行，沿用明确的安装阶段证据范围。`FINAL-VERIFICATION.json` 保存上述结果及 `FullStagingHardwareTestPassed=false`。

此时 USB 未连接、语音 idle，需要恢复物理连接后才可做一次自动填入实测；不把只读定位速度提升当成整段填入验收，不增加断链专项验收。工作树及分支继续保留用于未完成验收，未提交／推送／合并／打标签／发布／刷机。最终 diff 检查通过，35 个任务修改／新增路径。

## 0.6.14 已安装（后续明确授权）

用户要求立即更新桥接后，22:17 完成原目录 ZIP 更新。包 SHA-256 为 `b35183a303016195faf96fe4a652f862033ab7a953ac873fe2273d5d9926ab35`，107 个安装文件、106 个回滚文件均逐一校验；正常退出旧桥接后启动新版，真实“关于”窗口显示 0.6.14。已安装 `--status-once` 通过，6 个配对／草稿／设置文件和 7,151 个图库文件哈希一致；ESP8266 自动模式、六页顺序与 15 秒轮播不变。证据在 `C:\Users\yaououzhong\Work\AI-bot\artifacts\windows-0614-install-20261029/FINAL-VERIFICATION.json`。未运行 setup 安装向导，未操作实体录音或发送测试消息，实机功能仍留到 10 月 30 日。
