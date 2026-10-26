import Foundation

// Local, bounded metadata only. Never retain task IDs, request IDs, payloads,
// transcript text, authentication material or provider error descriptions.
final class MacTab5InteractionDiagnostics {
    private struct Entry {
        let id: UInt64, label: String, transport: String, started: TimeInterval, queued: Int?
        var elapsed: Int?, status: Int?
        var unlock = false, codex = false, outcome:String?
    }
    private let lock = NSLock()
    private var counter: UInt64 = 0
    private var entries: [Entry] = []
    private let clock: () -> TimeInterval
    init(clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) { self.clock = clock }
    private static func milliseconds(_ value: TimeInterval) -> Int {
        value.isFinite ? Int(min(180, max(0, value)) * 1000) : 0
    }
    func begin(kind: String?, operation: String?, transport: String, queued: TimeInterval? = nil) -> UInt64? {
        let labels = ["desktop/open-recent": "定位会话", "codex/draft-load": "读取草稿",
                      "codex/draft-save": "保存草稿", "codex/read": "读取回复", "codex/read-ack": "确认已读", "codex/send": "发送消息",
                      "desktop/stage-draft": "回填草稿", "voice/start": "开始录音",
                      "voice/stop": "停止录音", "voice/cancel": "取消录音", "codex/interrupt": "停止任务",
                      "wifi-unlock/begin": "开始 Wi-Fi 解锁", "wifi-unlock/cancel": "取消 Wi-Fi 解锁",
                      "host-unlock/begin": "开始本机解锁", "host-unlock/poll": "续期本机解锁", "host-unlock/cancel": "结束本机解锁"]
        guard let kind, let operation, let label = labels[kind + "/" + operation] else { return nil }
        lock.lock(); defer { lock.unlock() }
        counter &+= 1
        entries.append(.init(id: counter, label: label, transport: ["USB", "Wi-Fi", "BLE"].contains(transport) ? transport : "其他",
            started: clock(), queued: queued.map(Self.milliseconds)))
        entries[entries.count-1].unlock = ["host-unlock","wifi-unlock"].contains(kind)
        entries[entries.count-1].codex = kind == "codex" && operation == "send"
        entries = Array(entries.suffix(8))
        return counter
    }
    func finish(_ id: UInt64?, status: Int, body:[String:Any]? = nil) {
        guard let id else { return }
        lock.lock(); defer { lock.unlock() }
        guard let index = entries.firstIndex(where: { $0.id == id }), entries[index].status == nil else { return }
        entries[index].elapsed = Self.milliseconds(clock() - entries[index].started)
        entries[index].status = (100...599).contains(status) ? status : 0
        if entries[index].unlock,let body {
            if body["ok"] as? Bool == true,body["unlocked"] as? Bool == true,body["stage"] as? String == "unlocked" { entries[index].outcome = "已确认解锁" }
            else if body["ok"] as? Bool == true,body["cancelled"] as? Bool == true { entries[index].outcome = "已结束" }
            else if let stage = body["stage"] as? String,["preparing","sending","confirming"].contains(stage) { entries[index].outcome = stage }
            else if let error = body["error"] as? String,["unlock_offer_changed","unlock_step_consumed","unlock_stopped_no_retry","unlock_busy","host_unlock_disabled","wifi_unlock_disabled","session_changed"].contains(error) { entries[index].outcome = error }
            else { entries[index].outcome = "未确认" }
        }
        if entries[index].codex, let body, let code = body["error"] as? String {
            let allowed = ["image_inputs_unavailable", "desktop_owner_unavailable", "desktop_read_timeout",
                           "desktop_read_unavailable", "desktop_version_unsupported", "codex_catalog_stale",
                           "codex_or_private_storage_unavailable", "delivery_unknown", "invalid_request",
                           "task_busy_or_state_unknown", "task_waiting_for_input", "codex_send_not_enabled"]
            entries[index].outcome = allowed.contains(code) ? code : "未确认"
        }
    }
    var summary: String {
        lock.lock(); defer { lock.unlock() }
        let now = clock()
        return entries.reversed().map {
            let state = $0.status.map(String.init) ?? "处理中"
            let elapsed = $0.elapsed ?? Self.milliseconds(now - $0.started)
            let queued = $0.queued.map { "\($0) ms" } ?? "未测"
            let outcome = $0.outcome.map { " · 回执 \($0)" } ?? ""
            let age = $0.outcome == nil ? "":" · \(Int(min(180,max(0,now-$0.started)))) 秒前"
            return "#\($0.id) \($0.transport) · \($0.label) · \(state) · 排队 \(queued)／处理 \(elapsed) ms" + outcome + age
        }.joined(separator: "\n")
    }
}
