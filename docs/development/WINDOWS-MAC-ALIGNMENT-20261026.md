# Windows 与 Mac 桥接对齐记录（2026-10-26）

## 基线与范围

依据 GitHub main `aa8fc2d801f193dc43000f94f4a54998d7bf78f5` 的 [Windows 交接文档](WINDOWS-FOLLOWUP-FROM-MAC-20261026.md) 和两端实现。
本地分支 `codex/windows-mac-alignment-20261026`。原工作目录的既有修改未纳入本次工作。
本次交付是 Windows 候选；已按用户后续授权安装到现有桌面启动目标并启动。未刷机、未提交或推送、未创建标签或发布。
共用 ESP8266/TAB5 固件已在 Mac 分支实现的功能不重复移植；没有改变设备协议字段。

## 对齐结果

| 项目 | Windows 改动 | 验证与限制 |
| --- | --- | --- |
| W01 总余额 | 十进制合计同币种充值、赠送钱包；权威总额优先；相同重复值去重、冲突及不完整分组拒绝，保留成功缓存 | 合成金额、币种、零值、重复与缺失通过；真实网页待验 |
| W02 来源隔离 | API/网页余额、消费、时间分开保存；换 Key 不展示前一个 Key 的 API 数据；网页失败、退避不污染 API；设备配置 API 时消费留空；纯网页使用同响应余额和消费 | 两个虚构账户、事件反序、序列化重启及换 Key 回归通过；删除无法确认钱包语义的 DOM 余额兜底 |
| W03 预览 | 仅确认 `.277+ -ui` 的 USB/Wi-Fi 使用 100ms；旧版、未知版本和 BLE 保留保守节奏；原单请求、绝对截止、校验与高清恢复不变 | 106 项合成预览检查，含 99ms 不续发、100ms 续发；真实帧率与准备/传输/UI 分段计时待验 |
| W04 升级命令 | 通告不超过实际状态帧后 45s/15s，原请求 30s/10s 不延长；下载开始后旧拒绝回执不能覆盖传输进度 | 旧快照先复现失败后修复；现有加密 HTTP/RPC、取消、精确启动核验合成测试通过；未实际升级 |
| W05 语音调度 | USB 回复交付结束前暂停后台资源；清除缓存状态内夹带资源；每次资源分片后释放串口门锁，保留状态心跳 | 终态三分片、断线释放先复现后通过；真实录音、封面并发延迟及资源吞吐待测 |
| W06 段落 | 只合并本次识别后缀，保留原草稿；中英文与 Unicode 分隔符分别处理，继续使用 2000 UTF-8 字节界限 | 合成纠错、手动编辑、换行及字节限制通过；实际输入法按键与取消/来源切换待验 |
| W07 长会话 | 保留 owner/stored 路径；先利用元数据中的本地历史路径，再尝试有界分页；单回包 8MiB、公开文本 1MiB、分页及游标限制、8s 分页截止；异常不覆盖成功缓存 | 合成新旧接口、公开文本隔离、归属、游标、超量、取消通过；本机 CLI 条目分页尚未实现，见下文 |
| W08 账号窗口 | 活动 WebView2 窗口加入每厂商 60/75/90/100/125% 缩放保存；API/网页来源与失败分别显示；配置 API 时可独立手动刷新网页 | 原生合成窗口 100/125/175% 尺寸缩放可见检查通过；这是布局缩放模拟，不等于真实桌面 DPI 或已登录网页弹窗验收 |

## 验证入口

从独立临时目录执行自测，避免固定 fixture 目录被上次运行污染。使用 `dotnet <Release/AIBotBridge.dll>` 等待完整进程退出。

- `dotnet build windows-app/AIBotBridge/AIBotBridge.csproj -c Release`：0 警告、0 错误。
- `--self-test-public`：通过；包含官方额度、ESP 心跳、显示策略、加密 RPC/OTA、启动核验等既有回归。
- `--self-test-media-cost`、`--self-test-codex-tasks`、`--self-test-tab5-voice-core`、`--self-test-voice-resource-priority`：通过。
- 新独立入口 `--self-test-screen-preview`：106 项检查通过，不依赖设备中心窗口测试的后续断言。
- `--self-test-model-account-ui <输出目录>`：虚构账号、每厂商缩放保存/恢复与布局截图；没有初始化真实网页登录。
- `git diff --check`：交付前执行。

本地证据位于 `artifacts/windows-mac-alignment/`，含构建、public 与专项自测日志、`ui/` 截图、基线失败和修复前失败日志。此目录被忽略，不纳入源码提交。没有保存真实额度、凭据、聊天正文或录音。

## 后续核验与修复（同日）

- 正式 Windows 构造函数始终配置 `Tab5CodexDesktop`，设备读取走 owner/stored 路径。已打开会话从桌面 owner 读取；未加载会话从本地 sessions/archived_sessions 读取公开回复，由桥接按每页最多 2200 UTF-8 字节分页。CLI 条目分页不可用并不等于现有 Windows 长会话分页不可用。
- 新增生产构造路径回归：25,192,341 字节合成日志，三轮公开回复共八页，逐页重组与原文完全相同，前后轮次边界正确，工具输出及推理文本不进入结果；未启动 CLI、未打开或发送会话。本次耗时 85ms，仅代表该合成样本，不代表真实会话性能。桌面 owner 完整历史仍有 32MiB IPC 帧限制，极端公开回复量的压力测试不在这次证据内。
- 设备中心根因：`a81096fa` 已加入“电脑控制”入口，而旧测试仍期待八张 TAB5 卡片。保留九张现有卡片，修正完整顺序预期，追加电脑控制点击的设备/动作路由、停用状态及 ESP 不出现该入口的检查。完整 `--self-test-device-center` 已通过，覆盖空设备、单设备、双设备、停用设备与 100/125/150/200% 布局缩放；原生窗口截图已核查。没有修改升级业务逻辑或删掉断言。
- 本轮 Release 构建与 `--self-test-codex-tasks` 通过；日志见 `followup-build.log`、`followup-codex.log`、`device-fixed.log`。

## 现场限制与历史失败

1. **工具环境启动失败，正常桌面启动已通过。** 早先必需命令 `dotnet run --project windows-app/AIBotBridge/AIBotBridge.csproj -- --status-once` 返回 2；`AppProfileGuard` 检测到启动环境重定向数据目录，工具调用 Shell 的重试同样失败。后续通过真实 Windows 资源管理器启动已安装候选成功；关于窗口、驻留进程、原配对、实时 HTTP 状态及两块设备通信均确认。没有关闭保护或修改系统配置。原 `status-once.json` 保留失败证据，不改写为通过；实时证据见下节。
2. **本机 Codex 条目分页不可用。** 对不存在的合成 thread ID 做只读探测：`thread/turns/list` 需要 `experimentalApi:true`；本机实际路由为 `thread/turns/items/list`，返回 `-32601 ... is not supported yet`，旧 `thread/items/list` 返回 unknown variant。代码适配当前路由与旧式包装响应，但不能宣称本机 API 分页端到端已通过。现有 owner/stored 历史优先；该回退不可用时保留成功缓存并返回不可用，不恢复整段读取，不自动重发用户操作。
3. **设备中心旧失败（现已修复）。** `--self-test-device-center` 在 `DeviceCenterSelfTest.cs` 的 “Device action order or duplicate entry regressed” 断言失败。用未修改 `aa8fc2d` 源码另行导出、独立构建和执行，复现完全相同失败。该失败现已按上文修正测试预期并通过完整自测，保留独立顺序、重复和路由断言。
4. **物理验收进行中，尚未全部通过。** 已完成正常启动、真实登录网页加载与缩放、USB 在线及运行状态检查。ESP 地址变化/8 秒回退/恢复、TAB5 USB/Wi-Fi/BLE 预览、真实长会话翻页、20 秒录音与资源并发、取消/断线仍待验证；没有执行固件升级，不声称精确镜像启动验收通过。TAB5 保持用户展示设置；ESP 原自动轮播策略保持不变。

## 候选安装与现场运行（同日 19:46–20:04）

- 用户授权：安装候选、解决正常启动、完成实机验收。现有开始菜单和开机启动指向原工作目录下的 Release 程序，因此将候选覆盖安装到该目标，没有另建配置目录。
- 使用 `dotnet publish -c Release --no-restore --self-contained false -p:InformationalVersion=0.6.10-candidate.20261026` 生成 43 个候选文件；根 `VERSION` 保持不变。旧 Release 完整备份到 `artifacts/windows-mac-alignment/install/rollback-20261026-194622/`。安装文件逐项 SHA-256 与候选一致，清单为 `install/installed-files.json`。安装 DLL SHA-256：`AE14246EB3CBEBc914958FFC1FA31C734853368928E43B0A9F2832FA7CE6C790`（大小写不影响校验）。
- 实际安装目标：`C:\Users\yaououzhong\Work\AI-bot\windows-app\AIBotBridge\bin\Release\net8.0-windows10.0.19041.0\`。当前版本由真实“关于应用”窗口确认；进程由 `explorer.exe` 启动，原配对与账号登录已加载，未重新配对。
- 实时服务使用已配置的 `18765` 端口。`/status` 中 bridge/codex/CPU/内存/磁盘检查正常；这证明实际常驻运行，不替代此前失败的工具环境 `--status-once` 命令。
- TAB5 实际固件 `0.2.279-ui`，USB 已认证，Wi-Fi 已联网且桥接正常，BLE 待命；实时 USB 诊断年龄低于一秒，未记录 USB RPC 中断。ESP8266 实际固件 `0.5.6`，USB 在线，两个采样间 USB 状态帧增加 167，Wi-Fi 待命。在线不等于预览、录音或回退已验收。
- 小屏保持 `selectedMode=auto`、`cycleEnabled=true`、15 秒间隔，页面顺序为 `codex, domestic_deepseek, domestic_zhipu, weather, stocks, system`，前后比对一致。尚未执行退出后的 `--restore-cycle-default` 收尾重启。
- 真实 DeepSeek 已登录网页加载成功，API 与网页来源及时间分别可见。实时设备数据有余额且 `usedCost=null`，未将网页消费混入 API 数据。网页从 100% 切到 75% 后已目视确认布局变化；API/网页精确金额及不同账户隔离仍以合成回归为证，不把网页登录证明当作全部账户组合验收。
- **暂停点：Windows 锁屏。** 缩放尚停在测试用 75%，解锁后需恢复原 100%；需确认凭据输入框为空并关闭窗口，未点击保存凭据。小屏“管理 → 连接诊断”尚未执行；TAB5 预览入口、长会话翻页与约 20 秒录音需要设备端操作。已向用户请求解锁及具体设备操作，截至本节采样，历史与语音诊断均为“尚无请求”。
- `install/live-baseline.json`、`install/live-verification.json`、`install/live-quota-check.json` 保存筛选后的设备状态和布尔检查；不保存账号金额、Key、Cookie、聊天正文或录音。候选继续驻留，待解锁后续验收。原工作目录既有源码改动未改变。

### 解锁后续验（20:07 起）

- 用户已解锁；实际页面确认缩放已恢复原 100%，凭据框显示空输入提示，未保存或测试新凭据；账号窗口已关闭。
- 用户明确将 TAB5 长会话翻页与 20 秒录音留到次日验收；这两项继续标为待验，不自动发送消息或触发录音。
- 小屏管理菜单的自动点击遇到 `computer-use request timed out: click_element`；重新定位后窗口捕获返回 `no screenshot targets found`。停止继续使用旧坐标。已请用户执行现有“连接诊断”，同时只读采样 USB/LAN 计数；尚未凭在线状态宣称回退通过。
- 解锁后实时 USB 诊断仍正常：ESP8266 在线；TAB5 采样年龄 391ms，无 USB RPC 中断；历史和语音仍无请求。尚未检测到实际 TAB5 预览窗口，不能把用户答复“ok”当作已打开或预览通过。

### 小屏回退测试：失败已确认，根因未确认

用户手动执行现有连接诊断并提供结果截图；同步只读采样也观察到 USB 暂停。结果为 **Wi-Fi 回退未通过，USB 恢复通过**：

| 阶段 | USB / 在线 | USB 帧 / LAN 帧 | 运行 ms |
| --- | --- | --- | --- |
| 暂停前 | true / true | 685 / 0 | 1477456 |
| 暂停约 12 秒 | false / false | 685 / 0 | 1489492 |
| 恢复后 | true / true | 687 / 0 | 1492537 |

只读排查确认电脑可 ping 通小屏，设备 HTTP 返回正常的未授权 401（说明管理服务可达）；WLAN 为 /23，同网段，桥接监听该 WLAN 地址的 18765 端口，当前安装程序 TCP/UDP 入站规则均为 Allow，覆盖 Public/Private。未修改防火墙、网络配置、配对资料或固件。电脑到设备可达不能证明反向 HTTP 请求成功。

代码仍将完整状态用于 LAN，小屏 USB 使用精简状态。现场完整状态约 13.9KB，小屏剩余堆约 17KB、最大块约 12KB；内存解析压力是待验证假设，**不是已确认根因**。工具环境读取到的配对文件未与桌面运行配置验证一致，其鉴权探测 401 也不能当作设备配对失败证据。继续定位需要设备 HTTP 返回码/JSON 解析结果或桥接侧非敏感请求诊断，不能靠放宽断言、重置配对或直接归咎防火墙制造通过。

证据：`install/fallback-result.json`、用户截图 `install/fallback-result.png`、`install/unlocked-fallback-observation.json` 及当前程序防火墙只读清单。该轮失败保留为历史基线；后续修复和实测结果见下节。

### Wi-Fi 回退修复与实机复验（20:53–20:55）

- 已确认的实现问题：`LanStatusServer` 将完整电脑状态发给 ESP8266，而 USB 已排除 `runtimeHealth` 和天气 `hourly/daily` 等小屏不消费的明细。增加共用的小屏视图裁剪，LAN 保留当前天气、完整股票名称、额度、轮播策略和增量曲线采样；电脑 `/status` 与 TAB5 数据不裁剪。协议版本、固件和配对均未变更。
- 在同一 ESP8266 `0.5.6`、同一网络与配对下，实际 LAN 响应从约 13.9KB 降为 **5814 bytes**，两轮真实 USB 暂停 → Wi-Fi 接替 → USB 恢复通过。未捕获固件 `NoMemory` 错误码，因此不把堆耗尽的具体错误码写成已观测事实；归因依据是代码数据范围错误及精简前后的设备行为。
- 第一轮：USB 暂停时保持在线，LAN 帧由 0 增至 3；桥接记录 3 次请求全部鉴权并发送成功；恢复后 USB 帧继续增长。
- 第二轮完整采样：暂停前 USB/LAN 帧 **1832/3**；暂停期间 **1832/4**，`usb=false`、`online=true`、`display_cached=false`，页面由 domestic 正常切到 weather；恢复首帧 **1833/4**，设备 uptime 持续增长。原有连接诊断显示“Wi-Fi 回退与 USB 恢复均通过”。没有放宽断言。
- 增加 `/diagnostics/esp-lan` 只读环回诊断，只记录请求、鉴权、发送计数、长度和时间，不记录地址、令牌、请求头或账号内容。发送完成不代替设备收帧验收。
- 新回归通过带令牌鉴权的真实 HTTP 路径验证裁剪、标签与采样保留、共享快照不变，合成大状态由 10770 降至 1525 bytes。Release 构建 0 警告/错误，`--self-test-lan` 和全量 `--self-test-public` 通过。最初复用旧 fixture 目录导致 `Wi-Fi defaults off` 失败，换全新目录复测通过；未删断言或更改解锁实现。发布构建与测试并行时曾遇 DLL 锁，待测试结束后顺序发布成功。
- 候选 **`0.6.10-candidate.20261026.2`** 已覆盖安装到原启动目标，43 个文件哈希与候选一致；DLL SHA-256：`84A7367487D7067CB12945D03BE1A5CC3C22242110DF13910C3E382F200FABDC`。上一候选完整备份在 `install/r2/`；原始旧版备份继续保留。
- 复验后通过现有同用户退出事件正常退出桥接，再从真实资源管理器运行 Release `--restore-cycle-default` 并正常重启。确认小屏仍为 auto、轮播开启、原 6 页原顺序与 15 秒间隔；TAB5 展示配置未改动。候选保持驻留。
- 必需 `dotnet run --project ... -- --status-once` 在工具环境仍返回 2，数据目录保护正常触发；正常 Explorer 启动和真实 HTTP/设备运行证据另行记录，不将工具命令伪报成功。
- 证据：`esp-lan-build.log`、`esp-lan-test.log`、`esp-lan-public-fresh.log`、`esp-lan-status-once-proof.json`，以及 `install/installed-esp-lan-files.json`、`esp-lan-fix-first-result.json`、`esp-lan-fix-live-2.json`、`esp-lan-acceptance.json`、`esp-lan-closeout.json`。第一轮长采样文件只覆盖测试前阶段，期间成功证据来自即时读数与恢复后摘要；第二轮有完整自动采样。
- 本次 Wi-Fi 回退问题已通过实机复验；用户指定次日执行的 TAB5 长会话翻页与 20 秒录音仍待验。未创建标签、推送或公开发布。

## English summary

The initial ESP fallback failure is now fixed and physically revalidated. Windows LAN status incorrectly included desktop runtime health and TAB5 forecast details that the existing USB projection already omitted. Removing those unused views reduced the real payload from about 13.9KB to 5814 bytes while retaining rendered data and graph samples. Two real fallback/recovery runs passed on unchanged ESP8266 0.5.6; the second captured USB/LAN counters 1832/3 → 1832/4 → 1833/4, live non-cached rendering and increasing uptime. The exact firmware allocation error was not captured. Local content-free request counters were added. Release, LAN and fresh-directory public tests pass. Candidate 0.6.10-candidate.20261026.2 is installed with verified hashes and rollback backup; the original cycle policy was restored through the normal desktop entry and checked after restart. No firewall, pairing or firmware changes were needed. Deferred TAB5 history/voice acceptance remains pending.

This Windows candidate implements the eight Mac handoff items while preserving shared firmware and platform-specific paths. Release build and public/targeted synthetic regressions pass, including the corrected nine-card device-center expectation and a 25,192,341-byte stored-history fixture spanning three turns/eight pages. The desktop owner still has a 32MiB IPC-frame limit; extreme public-reply volumes were not validated. The installed Codex CLI declares but does not implement item pagination; production owner/stored history remains preferred. The candidate is now installed at the existing startup target with a complete rollback backup and matching file hashes. Actual Explorer startup, the candidate About version, original pairing, live USB communication with both devices, and a logged-in DeepSeek page at 75% zoom were verified. Tool-context status startup still hits the profile guard; protection was not bypassed. After Windows was unlocked, original 100% zoom was restored and the account window closed without saving credentials. The user deferred actual long-history paging and voice acceptance until the next day. Desktop automation initially timed out but subsequently recovered. ESP cycle closeout and Wi-Fi fallback now pass as described above; TAB5 preview and deferred history/voice checks remain pending. The resident bridge stays running. No firmware flash, Git commit/push, tag or public release was performed.
