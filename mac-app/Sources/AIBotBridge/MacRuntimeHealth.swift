import Foundation

/// Existing TAB5 runtimeHealth fields, backed by actual Mac observations.
/// A frame's publication time is deliberately not a new measurement.
final class MacRuntimeHealth {
    private let session = UUID().uuidString.lowercased()
    private var sequence: Int64 = 0
    private var observedAt: Date?
    private var highSince: [String: Date] = [:], lowSince: [String: Date] = [:]
    private var warned = Set<String>()
    private var cached: [String: Any]?

    func snapshot(metrics: SystemMetricsSnapshot?, now: Date, logicalProcessors: Int,
                  codexEnabled: Bool, desktopObserved: Bool) -> [String: Any]? {
        guard let metrics, let stamp = metrics.pressureObservedAt,
              stamp.timeIntervalSince1970 > 0, stamp <= now.addingTimeInterval(1) else { return nil }
        guard now.timeIntervalSince(stamp) < 8 else {
            highSince.removeAll(); lowSince.removeAll(); warned.removeAll(); observedAt = nil
            return cached // Its old epoch makes the device show an interrupted monitor.
        }
        let fresh = observedAt != stamp
        if fresh, let previous = observedAt, stamp.timeIntervalSince(previous) > 5 || stamp < previous {
            highSince.removeAll(); lowSince.removeAll(); warned.removeAll()
        }
        let initialWarnings = warned
        var checks: [[String: Any]] = []
        func check(_ key: String, _ label: String, _ state: String, _ summary: String, _ detail: String) {
            checks.append(["key": key, "label": label, "state": state, "summary": summary,
                           "detail": detail, "advice": "", "epochUtc": Int64(stamp.timeIntervalSince1970)])
        }
        for (key, label, value, threshold) in [("cpu", "CPU", metrics.cpuPercent, 90.0),
                                              ("memory", "内存", metrics.memoryPercent, 92.0)] {
            guard value.isFinite, (0...100).contains(value) else {
                highSince[key] = nil; lowSince[key] = nil; warned.remove(key)
                check(key, label, "unknown", "采样不可用", "未取得有效的本机计数。"); continue
            }
            if fresh {
                if value >= threshold {
                    lowSince[key] = nil
                    if highSince[key] == nil { highSince[key] = stamp }
                    if stamp.timeIntervalSince(highSince[key]!) >= 30 { warned.insert(key) }
                } else {
                    highSince[key] = nil
                    if lowSince[key] == nil { lowSince[key] = stamp }
                    if stamp.timeIntervalSince(lowSince[key]!) >= 10 { warned.remove(key) }
                }
            }
            let state = warned.contains(key) ? "warning" : value >= threshold ? "observing" : "ok"
            check(key, label, state, state == "warning" ? "持续占用偏高" : state == "observing" ? "正在观察占用" : "采样正常",
                  key == "memory" ? "依据 Mac 可用物理页统计占用；不代表提交内存或系统内存压力等级。" : "依据 Mac CPU 累积计数的相邻采样差值。")
        }
        if fresh { observedAt = stamp }
        if !warned.subtracting(initialWarnings).isEmpty { sequence += 1 }
        for (key, label, detail) in [
            ("queue", "处理器队列", "尚无与设备队列字段口径一致的 Mac 实测计数；不使用平均负载代替。"),
            ("disk", "磁盘延迟", "尚未采集 Mac 物理磁盘 I/O 延迟。"),
            ("vpn", "出口响应", "尚未配置独立出口探测；本机联网不代表目标服务可达。"),
            ("pac", "PAC 路由", "尚未验证当前代理对目标服务的实际路由。")
        ] { check(key, label, "unknown", "尚未采集", detail) }
        check("bridge", "桥接采样", "ok", "采样与发布正常", "CPU 和内存已有近期采样，当前设备状态帧正在发布；不代表界面无阻塞。")
        check("codex", "Codex", codexEnabled ? "observing" : "disabled",
              codexEnabled ? "模型出口未检测" : "未启用",
              desktopObserved ? "本机桌面任务持有者已确认，尚未用模型请求验证服务出口。" : "本地任务读取与模型服务连通性分别判断。")
        let alerts: [[String: Any]] = ["cpu", "memory"].filter { warned.contains($0) }.map { key in
            ["key": key, "state": "warning", "title": key == "cpu" ? "CPU 持续繁忙" : "内存持续占用偏高",
             "advice": "在 Mac 活动监视器查看占用情况。", "shortAdvice": "查看活动监视器"]
        }
        let value: [String: Any] = [
            "session": session, "sequence": sequence, "epochUtc": Int64(stamp.timeIntervalSince1970),
            "updatedAt": ISO8601DateFormatter().string(from: stamp), "checks": checks, "alerts": alerts,
            "pressure": logicalProcessors > 0 ? ["logicalProcessors": logicalProcessors] : [:],
            "latencyHistory": [Any]()
        ]
        cached = value; return value
    }
}
