import Foundation

// Process-local GATT metadata. No payload, identifiers, addresses or errors.
final class MacTab5BLEDiagnostics {
    enum Kind: String, CaseIterable { case status = "状态帧", resource = "资源帧", voice = "语音邮箱", rpc = "控制邮箱" }
    enum Stage: String { case processing = "本机处理", reading = "等待读分片", writing = "等待写分片" }
    enum VoiceOperation: String, CaseIterable { case start, audio, stop, cancel, poll, tab5 }
    enum FailureDomain: String { case pending = "待回调", none = "无", att = "ATT", bluetooth = "CoreBluetooth", other = "其他" }
    private struct Entry {
        let id: UInt64, kind: Kind, began: TimeInterval
        var stage = Stage.processing, read = 0, submitted = 0, reads = 0, writes = 0
        var phaseBegan: TimeInterval
        var phases: [Stage: Int] = [:]
        var operation: VoiceOperation?, terminal = false
        var grants = 0, commandGrants = 0, notifications = 0
        var grantBegan: TimeInterval?, lastNotification: TimeInterval?
        var firstNotificationWait = 0, notificationGaps = 0, maximumNotificationGap = 0
    }
    private struct Result { let entry: Entry, milliseconds: Int, success: Bool }
    private struct Totals {
        var count = 0, milliseconds = 0, maximum = 0, grants = 0, commandGrants = 0, notifications = 0
        var firstNotificationWait = 0, notificationGaps = 0, maximumNotificationGap = 0
        var phases: [Stage: Int] = [:]
        mutating func add(_ entry: Entry, duration: Int) {
            count = min(4096, count + 1); milliseconds = min(180000, milliseconds + duration)
            maximum = max(maximum, duration)
            grants = min(65536, grants + entry.grants); notifications = min(65536, notifications + entry.notifications)
            commandGrants = min(65536, commandGrants + entry.commandGrants)
            firstNotificationWait = min(180000, firstNotificationWait + entry.firstNotificationWait)
            notificationGaps = min(180000, notificationGaps + entry.notificationGaps)
            maximumNotificationGap = max(maximumNotificationGap, entry.maximumNotificationGap)
            for phase in [Stage.reading, .writing, .processing] {
                phases[phase] = min(180000, (phases[phase] ?? 0) + (entry.phases[phase] ?? 0))
            }
        }
    }
    private struct Capture {
        let began: TimeInterval
        var end: TimeInterval?, confirmed = false
        var kinds: [Kind: Totals] = [:], operations: [VoiceOperation: Totals] = [:]
        var idle = Totals()
    }
    private let lock = NSLock(), clock: () -> TimeInterval
    private var counter: UInt64 = 0, current: Entry?
    private var results: [Kind: Result] = [:]
    private var capture: Capture?
    private var voiceLink: String?
    private var voiceConfiguration: String?
    private var rpcLink: String?
    private var rpcConfiguration: String?
    init(clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) { self.clock = clock }
    func voiceLink(credits: Int, available: Bool, subscribed: Bool, failure: FailureDomain = .pending, code: Int = 0) {
        lock.lock(); defer { lock.unlock() }
        voiceConfiguration = nil
        let safeCode = (-99999...99999).contains(code) ? code : 0
        voiceLink = "最近窗口协商：额度 \(max(0, min(8, credits))) · 通知能力 \(available ? "有" : "无") · 订阅 \(subscribed ? "已确认" : "未确认") · 错误 \(failure.rawValue)/\(safeCode)"
    }
    func voiceConfiguration(_ value: Any?) {
        lock.lock(); defer { lock.unlock() }
        let flags = MacTab5Protocol.integer(value).flatMap { (0...3).contains($0) ? $0 : nil }
        voiceConfiguration = "订阅失败后只读核对：CCCD \(flags.map(String.init) ?? "未知")（不代表 Mac 已订阅）"
    }
    func rpcLink(credits:Int, available:Bool, subscribed:Bool, failure:FailureDomain = .pending, code:Int = 0) {
        lock.lock(); defer { lock.unlock() }
        rpcConfiguration = nil
        let safeCode = (-99999...99999).contains(code) ? code : 0
        rpcLink = "控制通知协商：额度 \(max(0,min(32,credits))) · 通知能力 \(available ? "有":"无") · 订阅 \(subscribed ? "已确认":"未确认") · 错误 \(failure.rawValue)/\(safeCode)"
    }
    func rpcConfiguration(_ value:Any?) {
        lock.lock(); defer { lock.unlock() }
        let flags = MacTab5Protocol.integer(value).flatMap { (0...3).contains($0) ? $0:nil }
        rpcConfiguration = "控制订阅失败后只读核对：CCCD \(flags.map(String.init) ?? "未知")（不代表 Mac 已订阅）"
    }
    func begin(_ kind: Kind) -> UInt64 {
        lock.lock(); defer { lock.unlock() }
        finishLocked(success: false)
        let now = clock()
        counter &+= 1; current = Entry(id: counter, kind: kind, began: now, phaseBegan: now)
        return counter
    }
    private func closePhaseLocked() {
        guard current != nil else { return }
        let phase = current!.stage, duration = milliseconds(current!.phaseBegan)
        current!.phases[phase] = min(180000, (current!.phases[phase] ?? 0) + duration)
        current!.phaseBegan = clock()
    }
    func stage(_ stage: Stage) {
        lock.lock(); defer { lock.unlock() }
        closePhaseLocked(); current?.stage = stage
    }
    func voice(_ operation: VoiceOperation, terminal: Bool) {
        lock.lock(); defer { lock.unlock() }
        guard current?.kind == .voice else { return }
        current?.operation = operation; current?.terminal = terminal
    }
    func voiceGrant(command: Bool = false) {
        lock.lock(); defer { lock.unlock() }
        if current?.kind == .voice {
            current!.grants = min(65536, current!.grants + 1)
            if command { current!.commandGrants = min(65536, current!.commandGrants + 1) }
            current!.grantBegan = clock(); current!.lastNotification = nil
        }
    }
    func voiceNotification() {
        lock.lock(); defer { lock.unlock() }
        guard current?.kind == .voice else { return }
        current!.notifications = min(65536, current!.notifications + 1)
        if let previous = current!.lastNotification {
            let gap = milliseconds(previous)
            current!.notificationGaps = min(180000, current!.notificationGaps + gap)
            current!.maximumNotificationGap = max(current!.maximumNotificationGap, gap)
        } else if let grant = current!.grantBegan {
            current!.firstNotificationWait = min(180000, current!.firstNotificationWait + milliseconds(grant))
        }
        current!.lastNotification = clock()
    }
    func read(_ count: Int) {
        lock.lock(); defer { lock.unlock() }
        guard current != nil else { return }
        current!.read = min(8 * 1024 * 1024, current!.read + max(0, min(65536, count)))
        current!.reads = min(65536, current!.reads + 1)
    }
    func submit(_ count: Int) {
        lock.lock(); defer { lock.unlock() }
        guard current != nil else { return }
        current!.submitted = min(8 * 1024 * 1024, current!.submitted + max(0, min(65536, count)))
        current!.writes = min(65536, current!.writes + 1)
    }
    func finish(_ id: UInt64, success: Bool) {
        lock.lock(); defer { lock.unlock() }
        guard current?.id == id else { return }
        finishLocked(success: success)
    }
    func discard(_ id: UInt64) {
        lock.lock(); defer { lock.unlock() }
        guard current?.id == id else { return }
        closePhaseLocked()
        if let current { recordCaptureLocked(current, success: true, idle: true) }
        current = nil
    }
    func failCurrent() {
        lock.lock(); defer { lock.unlock() }
        finishLocked(success: false)
        if capture?.end == nil { capture?.end = clock() }
    }
    private func milliseconds(_ began: TimeInterval) -> Int {
        let elapsed = clock() - began
        return elapsed.isFinite ? Int(min(180, max(0, elapsed)) * 1000) : 0
    }
    private func finishLocked(success: Bool) {
        closePhaseLocked()
        guard let entry = current else { return }
        results[entry.kind] = Result(entry: entry, milliseconds: milliseconds(entry.began), success: success)
        recordCaptureLocked(entry, success: success, idle: false)
        current = nil
    }
    private func recordCaptureLocked(_ entry: Entry, success: Bool, idle: Bool) {
        if entry.kind == .voice, entry.operation == .start, success, !idle { capture = Capture(began: entry.began) }
        guard capture != nil, capture?.end == nil else { return }
        let duration = milliseconds(entry.began)
        capture!.kinds[entry.kind, default: Totals()].add(entry, duration: duration)
        if idle { capture!.idle.add(entry, duration: duration) }
        else if let operation = entry.operation { capture!.operations[operation, default: Totals()].add(entry, duration: duration) }
        if !success || entry.terminal {
            capture!.end = clock(); capture!.confirmed = success && entry.terminal
        }
    }
    var summary: String {
        lock.lock(); defer { lock.unlock() }
        func sizes(_ entry: Entry) -> String { "读取 \(entry.read) B/\(entry.reads) 次 · 提交 \(entry.submitted) B/\(entry.writes) 次" }
        var lines: [String] = []
        if let voiceLink { lines.append(voiceLink) }
        if let voiceConfiguration { lines.append(voiceConfiguration) }
        if let rpcLink { lines.append(rpcLink) }
        if let rpcConfiguration { lines.append(rpcConfiguration) }
        if let current { lines.append("\(current.kind.rawValue)：\(current.stage.rawValue) · \(milliseconds(current.began)) ms · \(sizes(current))") }
        for kind in Kind.allCases {
            if let result = results[kind] {
                lines.append("\(kind.rawValue)：\(result.success ? "流程完成" : "未确认") · \(result.milliseconds) ms · \(sizes(result.entry))")
            }
        }
        if let capture {
            let elapsed = (capture.end ?? clock()) - capture.began
            let span = elapsed.isFinite ? Int(min(180, max(0, elapsed)) * 1000) : 0
            let audio = capture.operations[.audio] ?? Totals()
            let busy = capture.kinds.values.reduce(0) { min(180000, $0 + $1.milliseconds) }
            let background = (capture.kinds[.status]?.milliseconds ?? 0) + (capture.kinds[.resource]?.milliseconds ?? 0)
            let status = capture.kinds[.status] ?? Totals(), resources = capture.kinds[.resource] ?? Totals()
            lines.append("本轮蓝牙累计：\(capture.end == nil ? "进行中" : capture.confirmed ? "终态响应已提交" : "未确认结束") · 跨度 \(span) ms（含说话等待）")
            lines.append("音频传输：\(audio.count) 次 · 累计 \(audio.milliseconds) ms · 最长 \(audio.maximum) ms · 读阶段 \(audio.phases[.reading] ?? 0)／写阶段 \(audio.phases[.writing] ?? 0)／处理阶段 \(audio.phases[.processing] ?? 0) ms · 窗口授权 \(audio.grants) 次／通知 \(audio.notifications) 片 · 其中免应答授权 \(audio.commandGrants) 次")
            lines.append("通知等待：授权提交至首片累计 \(audio.firstNotificationWait) ms · 后续片间隔累计 \(audio.notificationGaps) ms／最长 \(audio.maximumNotificationGap) ms（含系统回调等待）")
            lines.append("队列占用：语音 \(capture.kinds[.voice]?.milliseconds ?? 0) ms · 状态与资源 \(background) ms · 控制 \(capture.kinds[.rpc]?.milliseconds ?? 0) ms · 其中空邮箱 \(capture.idle.milliseconds) ms · 其余间隔 \(max(0, span - busy)) ms（含说话等待）")
            lines.append("后台分段：状态 \(status.count) 次／\(status.milliseconds) ms · 资源 \(resources.count) 次／\(resources.milliseconds) ms · 读 \((status.phases[.reading] ?? 0) + (resources.phases[.reading] ?? 0))／写 \((status.phases[.writing] ?? 0) + (resources.phases[.writing] ?? 0))／生成 \((status.phases[.processing] ?? 0) + (resources.phases[.processing] ?? 0)) ms")
            let stop = capture.operations[.stop] ?? Totals(), poll = capture.operations[.poll] ?? Totals()
            lines.append("结束请求：\(stop.count) 次／\(stop.milliseconds) ms · 结果查询：\(poll.count) 次／\(poll.milliseconds) ms")
        }
        return lines.joined(separator: "\n")
    }
}
