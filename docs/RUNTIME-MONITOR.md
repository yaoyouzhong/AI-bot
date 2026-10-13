# 运行监控 / Runtime monitor

本功能是本地开发候选：Windows 桥接已在本机更新，真实采样、Windows 通知显示回执、正常退出与重启已经验证；TAB5 页面仍为原生 LVGL 预览，尚未刷机。现有固件、版本和升级包保持原有状态。

## 页面与提醒

- TAB5：进入`应用 → 系统`默认显示常看的 CPU / 内存、整机上传／下载速率和流量曲线；「运行监控」入口位于系统页标题旁，与流量图分开。点击该入口或首页短异常提示才进入监控页。左上角箭头依次返回：检测详情 → 运行监控 → 电脑状态 → 来源页面。退出后再次进入系统仍显示电脑状态。
- 运行监控按两类组织：左侧 VPN 响应数字、最近 10 次趋势、PAC 状态；右侧 CPU / 内存条、CPU 等待队列、磁盘响应。底部专门显示当前异常及简短建议。
- 点 VPN / 电脑卡片查看相应检测结果；「检测详情」打开当前异常对应的网络或系统分组。详情分为「异常原因」「处理建议」两栏，只保留关键证据与最多三条简短建议；正常、未确认、关闭及断连分别提示，不显示重复状态、HTTP 明细或完整日志。原始诊断继续保留在电脑桥接详情中。本功能不展示 Codex 用量、任务计数或会话详情，原有其他页面保持原功能。
- 首页没有正常状态说明或诊断长文；异常时只显示「PAC 异常」「VPN 变慢」「系统繁忙」等短标题。点击进入运行监控。一次异常只响一次，恢复后自动清除；新的持续异常可再次提醒。任务完成、等待审批等原有短时提示保留优先级。
- 监控数据过期或桥接断开时显示中断，取消绿色状态与趋势。不能用旧状态表示正常。
- Windows：`设备中心 → 桥接设置 → 运行监控` 主窗口只显示 VPN / PAC 和电脑负载两组简短状态、数字及异常建议；完整说明在「网络详情」「电脑详情」中查看。检测设置为独立标签页，可配置间隔、阈值、预期代理路径和小流量测速；提醒管理提供运行异常开关、提醒音及勿扰时段。

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

本次证据位于 `artifacts/runtime-monitor-20261013/`：桥接候选、策略自测、真实 Windows / PAC 探测、连续采样记录、原生 TAB5 页面及首页预览、Windows 监控设置页预览。预览 JSON 为明确的模拟故障数据，由桥接序列化成实际 `tab5_status` 字段后供 LVGL 使用。

当前完成边界是本机桥接实测与 TAB5 页面预览，TAB5 新页面没有真机验收。桥接连续采样 150 秒、74 份快照；将内存阈值临时降至 50%，真实内存数据持续 30 秒后触发告警，提醒记录收到 Windows `BalloonTipShown` 回执并显示「系统已显示」。测试后恢复 CPU／内存 90%、持续 30 秒、网络 30 秒的默认设置，配对、提醒设置、循环页面顺序和 15 秒间隔保持原状。USB 已恢复 COM9，TAB5 固件仍为 0.2.188-ui。

部署发现串口程序集混用：`win-x64` 依赖清单对应 Windows 串口库，运行目录却被通用构建覆盖为占位程序集，导致 `PlatformNotSupportedException`。已使用完整的 Windows 平台输出替换并做串口运行检查；设备服务失败后的退出清理会记录错误并完成所有服务停止，故障停机自测及正常退出／重启已通过。不能把通用 `dotnet build` 的部分文件覆盖到已有平台发布目录。

系统指标是瓶颈线索，不直接测量桌面绘制延迟；不采集每个软件私有任务队列，也不判定 Codex 官方全局服务健康。小流量测速包含连接开销，不代表最大带宽；本功能未接入 VPN 客户端节点管理 API，因此不显示或切换具体节点名称。网络故障分支经过受控自测，实际 VPN／PAC 未被故意破坏；没有进行提醒音的耳听验收。

## English

This is a local development candidate. The Windows bridge has been installed locally and verified with 150 seconds of live sampling, a controlled threshold-based alarm, a native Windows notification display receipt, graceful shutdown/restart and restored USB connectivity. Temporary thresholds have been restored and TAB5 remains unflashed. Windows collects scheduler, memory and disk pressure every two seconds and checks the proxy exit and per-target PAC routing every 30 seconds. Sustained system pressure requires 30 seconds; network failures require three consecutive rounds. Two healthy observations clear an alert. A bounded 128 KiB transfer every five minutes measures small-transfer throughput rather than maximum bandwidth.

System opens the frequently viewed computer statistics, upload/download rates and traffic graph by default. Runtime monitoring is a secondary entry beside the System page title and continues detecting anomalies in the background. The top-left arrow returns through diagnostic details, monitor overview, computer statistics and the source page. TAB5 keeps short exception notices on Home and places a graphical overview under `Apps → System → Runtime monitor`: latency history and PAC on the left, CPU/memory bars and scheduler/disk values on the right. The bottom region is dedicated to alerts and advice. The relevant card opens a two-column cause-and-action view, retaining only key evidence and at most three brief suggestions. Healthy, unconfirmed, disabled and interrupted observations remain distinct; full diagnostic text is available on the Windows bridge. Codex usage, task counts and conversation details are excluded from this monitor. Stale data loses its healthy presentation. Windows notification preferences, deduplication and quiet hours apply.

PAC availability, actual Windows per-target selection and explicit exit probes are separate evidence. A working exit with broken PAC suggests restarting the VPN client; slow exits suggest inspecting or changing the node. No automatic proxy changes, node switching, client restarts, credential export or model requests occur. HTTP reachability cannot prove authenticated Codex service health. Native notification delivery is verified; physical acceptance of the new TAB5 pages and audible notification verification remain pending. Network failure branches were controlled tests; the live VPN configuration was not disrupted.

Sources: [Windows PAC routing](https://learn.microsoft.com/en-us/windows/win32/api/winhttp/nf-winhttp-winhttpgetproxyforurl), [Windows performance counters](https://learn.microsoft.com/en-us/windows/win32/api/pdh/nf-pdh-pdhaddenglishcounterw), [Windows performance diagnostics](https://learn.microsoft.com/en-us/troubleshoot/windows-server/support-tools/troubleshoot-issues-performance-monitor), [OpenAI error categories](https://developers.openai.com/api/docs/guides/error-codes).
