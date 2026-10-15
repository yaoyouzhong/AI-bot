# 运行监控 / Runtime monitor

本功能已纳入桥接 0.6.6／TAB5 0.2.190-ui 统一候选。独立监控版本的 Windows 实际采样、通知显示回执和正常退出／重启已验证；合并后的包须按具体源码和镜像重新核验。TAB5 页面已通过原生 LVGL 预览，真机仍运行 0.2.188-ui，监控页面尚未在设备上验收。

## 页面与提醒

- TAB5：进入`应用 → 系统`默认显示常看的 CPU / 内存、整机上传／下载速率和流量曲线；「运行监控」入口位于系统页标题旁，与流量图分开。点击该入口或首页短异常提示才进入监控页。左上角箭头依次返回：检测详情 → 运行监控 → 电脑状态 → 来源页面。退出后再次进入系统仍显示电脑状态。
- 运行监控按两类组织：左侧 VPN 响应数字、最近 10 次趋势、PAC 状态；右侧 CPU / 内存条、CPU 等待队列、磁盘响应。底部专门显示当前异常及简短建议。
- 点 VPN / 电脑卡片查看相应检测结果；「检测详情」打开当前异常对应的网络或系统分组。详情分为「异常原因」「处理建议」两栏，只保留关键证据与最多三条简短建议；正常、未确认、关闭及断连分别提示，不显示重复状态、HTTP 明细或完整日志。原始诊断继续保留在电脑桥接详情中。本功能不展示 Codex 用量、任务计数或会话详情，原有其他页面保持原功能。
- 首页没有正常状态说明或诊断长文；异常时使用「系统监控：内存紧张」「系统监控：系统繁忙」等短标题，前置原生感叹号警告图标：一般告警为黄色，严重错误为红色。Windows 通知使用相同前缀及对应的系统警告／错误图标，具体外观由 Windows 控制。点击进入运行监控。一次异常只响一次，恢复后自动清除；新的持续异常可再次提醒。任务完成、等待审批等原有短时提示保留优先级。
- 监控数据过期或桥接断开时显示中断，取消绿色状态与趋势。不能用旧状态表示正常。
- Windows：`设备中心 → 桥接设置 → 运行监控` 主窗口只显示 VPN / PAC 和电脑负载两组简短状态、数字及异常建议；完整说明在「网络详情」「电脑详情」中查看。检测设置为独立标签页，可配置间隔、阈值、预期代理路径和小流量测速；提醒管理提供运行异常开关、提醒音及勿扰时段。
- 补充检查继续在后台运行，不增加概览卡片：异常时显示「Codex 连接不稳」「监控采样已停滞」「内存提交量偏高」等短标题和一条建议。点击异常打开对应原因／建议，仍不展示 Codex 用量或任务详情。没有加入磁盘剩余空间检测。

## 连接、守护与内存压力补充

- Codex 实际连接：复用桥接已有的认证用量请求结果以及新模型事件中的固定错误类型，不增加模型请求或额外额度查询。单个来源的连接／超时异常在 5 分钟内达到三次才提醒，登录失效或访问受限连续两次才提醒；两次实际成功确认恢复。认证查询成功不覆盖模型连接失败。用户取消、等待审批、长时间无输出均不按连接故障处理；没有五分钟内的实际结果时显示待确认，不能用 VPN 探测成功证明业务正常。只保留分类、计数和时间，丢弃事件正文、会话标识及凭据。
- 桥接守护：桥接启动时创建一个同用户、会话内的独立子进程，正常退出时通知其结束；不安装 Windows 服务／计划任务、不自动重启。每两秒检查界面心跳、监控采样时间和服务任务失败。启动及睡眠恢复预留 20 秒；界面超过 15 秒、采样超过 10 秒或服务已失败，再持续 30 秒才提醒。整个桥接异常退出单独提醒。正常退出、停用监控、短暂停顿不会提示异常，提醒开关／勿扰设置继续生效；同一异常不由父子进程重复通知。
- 内存压力：`GetPerformanceInfo` 读取实际提交量和当前提交上限，比例达到原内存阈值（默认 90%）持续 30 秒后提醒。换页读入 ≥1000 页/秒必须同时满足可用内存 <1024 MB 或提交比例 ≥85%，以及磁盘响应 ≥20 ms，持续 30 秒后提醒；单独文件映射读取不触发换页告警。提交上限可能随分页文件扩展而改变；此比例不等于物理内存占用。
- 新提醒只进入已有短提示和检测详情。桥接异常退出后无法再向 TAB5 发布新状态，设备端继续按心跳过期显示中断；独立守护在电脑上发提醒。TAB5 新页面纳入待验收镜像。

## 采样与判断

| 项目 | 默认周期 | 提醒条件 |
|---|---|---|
| CPU / 内存 / 调度队列 / 磁盘 | 2 秒 | 连续 30 秒符合条件；两次正常样本恢复 |
| VPN 出口及 PAC | 30 秒，按每轮开始时间调度 | 连续 3 轮异常才提醒；两轮正常恢复 |
| 小流量吞吐 | 5 分钟 | 经检测出口读取 128 KiB；结果仅在详情中解释 |
| TAB5 状态发布 | 桥接原有 2 秒主状态 | 设备接收后呈现，趋势只新增实际网络采样点 |

CPU 默认 ≥90%、内存默认 ≥90% 或可用物理内存 <512 MB 持续达到阈值后告警。CPU 等待队列使用 Windows `Processor Queue Length`：CPU ≥80% 且待运行线程数 ≥逻辑处理器数的 2 倍才视为持续等待；它不是进程数量或 Codex 请求队列。磁盘整体平均响应 ≥50 ms 且待处理 I/O ≥2 才告警。无法读取计数器时显示待确认，不能当作 0。

网络使用小型、无凭据的 HTTPS 请求观察 ChatGPT 与资源服务的响应时间、失败情况。慢响应默认超过 2500 ms；至少 4 个正常样本后也比较滚动中位数，超过 `max(1000 ms, 3×中位数+500 ms)` 视为变慢。单次测量包括连接、TLS 与响应头耗时，不是 ICMP ping，也不能单凭它证明具体节点或远端服务是故障根因。默认检测需约 60 秒及请求耗时确认连续三轮失败，不是瞬时告警。

PAC 服务地址与代理流量入口分别读取，不硬编码本机端口。先验证 PAC 脚本服务，再通过 Windows `WinHttpGetProxyForUrl` 为每个目标执行实际选路，并实测该路径。默认期望 ChatGPT 及资源域名走代理；合法直连、TUN 或不同策略可在监控设置取消该期望。

- PAC 不可用但诊断出口可响应：建议重启 VPN 客户端后复查。
- PAC 返回与设置不符的直连路径：提示检查 PAC 规则，可以先重启客户端。
- 出口多个目标持续变慢/连接失败：提示检查客户端和出口，必要时更换节点。
- 单个目标响应异常：提示检查对应服务，不确定为节点故障。
- 不带认证时的 HTTP 401 / 404 仅说明目标产生了 HTTP 响应，不能证明登录、额度、模型调用或桌面 UI 正常；403 / 429 显示限制访问待确认。

失败保留最近成功响应及小流量吞吐的时间戳，路由改变时重置比较基线与吞吐缓存。PAC URL 查询参数、代理凭据、账户凭据及请求内容不进入监控状态和预览材料。检测不自动修改代理/PAC、不切换节点、不重启客户端、不发送 Codex 消息。

## 验证与限制

2026-10-13 补充监控已部署：本机连续 90 秒、45 份快照未出现误报，最终八类检查全部正常。独立测试父进程异常退出及采样停滞各产生一次原生 Windows 显示回执，正常退出不告警；真实常驻桥接和守护正常退出／重启通过。提交量、换页组合、间歇成功下重连、认证分类与服务端错误建议经过策略自测；九种 TAB5 监控预览及完整 LVGL 回归、Windows 缩放布局、字形覆盖通过。最终启动时的一次探测失败处于确认阶段，后续恢复，没有告警。新的故障分支使用受控数据，未破坏真实 VPN／登录或耗尽内存；TAB5 仍为 0.2.188-ui。详见 `artifacts/runtime-monitor-extended-20261013/REPORT.md`。

本次证据位于 `artifacts/runtime-monitor-20261013/`：桥接候选、策略自测、真实 Windows / PAC 探测、连续采样记录、原生 TAB5 页面及首页预览、Windows 监控设置页预览。预览 JSON 为明确的模拟故障数据，由桥接序列化成实际 `tab5_status` 字段后供 LVGL 使用。

当前完成边界是本机桥接实测与 TAB5 页面预览，TAB5 新页面没有真机验收。桥接连续采样 150 秒、74 份快照；将内存阈值临时降至 50%，真实内存数据持续 30 秒后触发告警，提醒记录收到 Windows `BalloonTipShown` 回执并显示「系统已显示」。测试后恢复 CPU／内存 90%、持续 30 秒、网络 30 秒的默认设置，配对、提醒设置、循环页面顺序和 15 秒间隔保持原状。USB 已恢复 COM9，TAB5 固件仍为 0.2.188-ui。

部署发现串口程序集混用：`win-x64` 依赖清单对应 Windows 串口库，运行目录却被通用构建覆盖为占位程序集，导致 `PlatformNotSupportedException`。已使用完整的 Windows 平台输出替换并做串口运行检查；设备服务失败后的退出清理会记录错误并完成所有服务停止，故障停机自测及正常退出／重启已通过。不能把通用 `dotnet build` 的部分文件覆盖到已有平台发布目录。

系统指标是瓶颈线索，不直接测量桌面绘制延迟；不采集每个软件私有任务队列，也不判定 Codex 官方全局服务健康。小流量测速包含连接开销，不代表最大带宽；本功能未接入 VPN 客户端节点管理 API，因此不显示或切换具体节点名称。网络故障分支经过受控自测，实际 VPN／PAC 未被故意破坏；没有进行提醒音的耳听验收。

## English

The alert presentation update uses a native exclamation warning icon followed by `系统监控：` (System monitor) and the concise cause. TAB5 warnings are amber and errors red; Windows notifications use the same prefix with the matching OS warning/error icon. Recovery removes the notice, and task/approval banners retain priority. Source changes and native previews do not establish deployment or physical acceptance.

All monitor changes are included in the bridge 0.6.6 / TAB5 0.2.190-ui candidate alongside desktop upgrade and transfer features. The independently deployed Windows monitor has live evidence; the combined package must be verified against its frozen source and image. TAB5 still runs 0.2.188-ui and has not accepted the new monitor pages on hardware.

The added checks were deployed locally on 2026-10-13: 45 snapshots over 90 seconds produced no false alerts, all eight final checks were healthy, and real bridge/guard shutdown and restart passed. Controlled parent crash and stopped-collector fixtures each produced one native Windows display receipt; intentional exit produced none. Connection/memory policies, nine native monitor scenarios, full LVGL regression, Windows scaling and font coverage passed. One startup probe failure recovered during confirmation without alerting. Failure cases did not disrupt the live VPN/login or exhaust memory. TAB5 remains on 0.2.188-ui.

Additional background checks reuse existing authenticated Codex usage requests and model-event error metadata. Three network/time-out observations within five minutes, or two consecutive authentication/access-limit failures, trigger concise alerts; two real successes recover. Successful quota queries cannot mask model-stream failure. Cancellation, approvals and long output gaps do not imply failure. No extra model or quota requests are issued, and event text, credentials and conversation IDs are not retained.

A session-scoped same-user child checks UI/collector heartbeats and failed workers every two seconds, survives an unexpectedly terminated bridge long enough to notify, and stops on intentional shutdown. It installs no Windows service or task and never restarts the bridge. Startup/resume gets 20 seconds of grace; stale UI (>15 s), stale monitoring (>10 s) or failed workers require 30 further seconds. Existing notification/quiet-hour settings apply without duplicate parent/child alerts. Memory checks add committed pages versus the current commit limit (default 90%, sustained 30 s) and combined paging/memory/disk pressure. Mapped-file page input alone is insufficient. Disk free-space monitoring was not selected. TAB5 retains the existing layout and unflashed-preview boundary.

This is a local development candidate. The Windows bridge has been installed locally and verified with 150 seconds of live sampling, a controlled threshold-based alarm, a native Windows notification display receipt, graceful shutdown/restart and restored USB connectivity. Temporary thresholds have been restored and TAB5 remains unflashed. Windows collects scheduler, memory and disk pressure every two seconds and checks the proxy exit and per-target PAC routing every 30 seconds. Sustained system pressure requires 30 seconds; network failures require three consecutive rounds. Two healthy observations clear an alert. A bounded 128 KiB transfer every five minutes measures small-transfer throughput rather than maximum bandwidth.

System opens the frequently viewed computer statistics, upload/download rates and traffic graph by default. Runtime monitoring is a secondary entry beside the System page title and continues detecting anomalies in the background. The top-left arrow returns through diagnostic details, monitor overview, computer statistics and the source page. TAB5 keeps short exception notices on Home and places a graphical overview under `Apps → System → Runtime monitor`: latency history and PAC on the left, CPU/memory bars and scheduler/disk values on the right. The bottom region is dedicated to alerts and advice. The relevant card opens a two-column cause-and-action view, retaining only key evidence and at most three brief suggestions. Healthy, unconfirmed, disabled and interrupted observations remain distinct; full diagnostic text is available on the Windows bridge. Codex usage, task counts and conversation details are excluded from this monitor. Stale data loses its healthy presentation. Windows notification preferences, deduplication and quiet hours apply.

PAC availability, actual Windows per-target selection and explicit exit probes are separate evidence. A working exit with broken PAC suggests restarting the VPN client; slow exits suggest inspecting or changing the node. No automatic proxy changes, node switching, client restarts, credential export or model requests occur. HTTP reachability cannot prove authenticated Codex service health. Native notification delivery is verified; physical acceptance of the new TAB5 pages and audible notification verification remain pending. Network failure branches were controlled tests; the live VPN configuration was not disrupted.

Sources: [Windows PAC routing](https://learn.microsoft.com/en-us/windows/win32/api/winhttp/nf-winhttp-winhttpgetproxyforurl), [Windows performance counters](https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw), [Windows performance diagnostics](https://learn.microsoft.com/en-us/troubleshoot/windows-server/support-tools/troubleshoot-issues-performance-monitor), [OpenAI error categories](https://developers.openai.com/api/docs/guides/error-codes).
