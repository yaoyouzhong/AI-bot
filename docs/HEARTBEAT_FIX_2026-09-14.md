# Windows 心跳被会话扫描阻塞：修复与验证

## 原因与改动

运行中的线程栈显示 SerialPublisher.RunAsync 正在 CodexLifecycleTracker.Capture 内枚举文件；界面与 LocalStatusServer 同时等待 SessionActivityReader.Capture。原先扫描持有共享锁，USB、LAN 与界面都同步等待日志 I/O。固件的 8 秒在线判定未改动。

SessionActivityReader 现在通过 BackgroundActivityCache 读取原子发布的最近成功样本，读取时只安排单个后台扫描，不等待扫描锁。下一次扫描从本次结束后计时；失败保留缓存，并报告异常类型，不输出文件内容。未增长的生命周期日志跳过打开读取。完成/注意信号仍在读取缓存后实时应用。

仅 Loopback 服务新增 GET /diagnostics/activity（扫描耗时、进行中与错误类型）和 GET /diagnostics/device（USB 只读设备信息）；LAN 服务与固件协议不变。--status-once 独立检查会等待首次扫描，避免把初始空缓存当作实际活动结果。

## 验证

- 标准 Release 构建：0 警告、0 错误。
- --self-test-activity-cache：扫描被人为阻塞 10 秒时，HTTP 请求与心跳帧生成继续完成，缓存保留且无重叠扫描；模拟 IOException 后保留原样本并报告错误。
- --self-test-codex-lifecycle、--self-test-lan：通过。
- 同一 Release DLL 通过 dotnet 直接执行 --status-once：退出码 0，JSON 有效，Codex 状态 working。
- 指定的 dotnet run -- --status-once 在当前自动化终端因原有 Console.OutputEncoding 的无效句柄异常失败；没有把此命令报告为通过。直接运行相同 DLL 是本次替代验证。
- git diff --check：通过。

## 本机部署与 ESP8266

旧程序正常退出后构建原 Release 路径，执行 --restore-cycle-default 后正常启动。备份及采样保存在 artifacts/heartbeat-validation（不进入版本库）。未刷固件、未推送或发布。

2026-09-14 12:18:00–12:18:37：15 次状态请求均返回，含 curl 启动开销的耗时 42–424ms；后台扫描曾耗时 3219ms。14 次有效设备回复全部 usb_active=true、bridge_online=true，USB 状态计数从 2249 增至 2267；一次设备诊断请求未在客户端期限内返回，不能据此断言设备离线或推定其原因。

12:19:31–12:19:59 复核：10/10 次设备请求成功，全部 USB/bridge 在线，计数从 2292 增至 2306，uptime 连续增长，无重启迹象。此为真实设备状态回读，未直接拍摄屏幕，也不等同于长期稳定性验证。

最终设置：auto、cycleEnabled=true、15 秒；页面及顺序保持 codex、domestic_minimax、domestic_deepseek、domestic_zhipu、weather、stocks、system。
