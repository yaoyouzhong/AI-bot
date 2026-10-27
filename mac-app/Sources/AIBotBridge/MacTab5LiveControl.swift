import Foundation

// Observe only the explicitly selected task. Public history pagination must not
// delay controls. This reader cannot send, resume or interrupt a task.
final class MacTab5LiveControl {
    struct Token { let task: String, generation: UInt64, began: TimeInterval }
    private struct Value { let task: String, turn: String, running: Bool, sendable: Bool, waiting: Bool }
    private let lock = NSLock(), queue = DispatchQueue(label: "AI-bot.codex-live-control")
    private let read: (String) throws -> MacCodexDesktopIPC.State
    private let enabled: () -> Bool, mutable: () -> Bool, sendEnabled: () -> Bool, clock: () -> TimeInterval
    private var timer: DispatchSourceTimer?
    private var task = "", generation: UInt64 = 0, lease: TimeInterval = 0
    private var checked: TimeInterval = -.infinity, lastBegan: TimeInterval = -.infinity
    private var value: Value?, stopped = false, wakePending = false
    private var duration = 0, note = "等待核对"
    private var selectedAt: Int64?
    init(enabled: @escaping () -> Bool, mutable: @escaping () -> Bool, sendEnabled: @escaping () -> Bool,
         clock: @escaping () -> TimeInterval, read: @escaping (String) throws -> MacCodexDesktopIPC.State) {
        self.enabled = enabled; self.mutable = mutable; self.sendEnabled = sendEnabled; self.clock = clock; self.read = read
    }
    func start() {
        lock.lock(); defer { lock.unlock() }
        guard timer == nil else { return }; stopped = false
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now(), repeating: .seconds(1))
        timer.setEventHandler { [weak self] in self?.refresh() }; self.timer = timer; timer.resume()
    }
    func stop() {
        lock.lock(); defer { lock.unlock() }
        timer?.cancel(); timer = nil; stopped = true; generation &+= 1; task = ""; value = nil; selectedAt = nil; wakePending = false
    }
    deinit { stop() }
    func select(_ task: String, issuedAt: Int64? = nil) {
        guard enabled(), MacTab5Codex.id(task) == task else { return }
        lock.lock()
        guard !stopped else { lock.unlock(); return }
        if let issuedAt, let selectedAt, issuedAt < selectedAt { lock.unlock(); return }
        if let issuedAt { selectedAt = issuedAt }
        let changed = self.task != task || clock() >= lease
        if changed { self.task = task; generation &+= 1; value = nil; checked = -.infinity; lastBegan = -.infinity; note = "等待核对" }
        // The reply reader renews this lease. Leaving that page stops extra
        // owner probes rather than polling the last task indefinitely.
        lease = clock() + 15
        let wake = changed && timer != nil && !wakePending
        if wake { wakePending = true }
        lock.unlock()
        if wake { queue.async { [weak self] in
            guard let self else { return }; self.lock.lock(); self.wakePending = false; self.lock.unlock(); self.refresh()
        } }
    }
    var isManaged: Bool { lock.lock(); defer { lock.unlock() }; return stopped || !task.isEmpty }
    func capture(task requested: String? = nil) -> Token? {
        guard enabled() else { return nil }
        lock.lock(); defer { lock.unlock() }
        let now = clock()
        guard !stopped, !task.isEmpty, now < lease, requested == nil || requested == task else { return nil }
        return .init(task: task, generation: generation, began: now)
    }
    func refresh() {
        guard let token = capture() else { return }
        do { accept(token, state: try read(token.task)) } catch { accept(token, state: nil) }
    }
    func accept(_ token: Token, state: MacCodexDesktopIPC.State?) {
        guard enabled() else { return }
        lock.lock(); defer { lock.unlock() }
        let now = clock()
        guard !stopped, token.task == task, token.generation == generation, now < lease, token.began >= lastBegan else { return }
        lastBegan = token.began; checked = token.began
        duration = Int((min(180, max(0, now - token.began)) * 1000).rounded())
        guard now - token.began <= 3, let state, state.task == task, MacTab5Codex.id(state.turn) == state.turn,
              ["idle", "active"].contains(state.runtime), ["completed", "failed", "interrupted", "inProgress"].contains(state.status) else {
            value = nil; note = "读取未确认"; return
        }
        value = .init(task: task, turn: state.turn, running: state.running, sendable: state.canSend, waiting: state.waiting)
        note = "已核对"
    }
    func snapshot() -> [String: Any]? {
        guard enabled(), mutable() else { return nil }
        let canSend = sendEnabled()
        lock.lock(); defer { lock.unlock() }
        let now = clock()
        guard !stopped, now < lease, now - checked <= 3, let value else { return nil }
        return ["taskId": value.task, "turnId": value.turn, "running": value.running,
                "canSend": canSend && value.sendable,
                "canSteer": canSend && value.running && !value.waiting && MacCodexDesktopIPC.supportsExpectedTurnSteer,
                "canInterrupt": canSend && value.running, "waiting": value.waiting]
    }
    var summary: String {
        lock.lock(); defer { lock.unlock() }
        guard !task.isEmpty else { return "" }
        let current = clock() < lease && clock() - checked <= 3 ? note : "等待新快照"
        return "运行状态独立核对：\(current) · 最近处理 \(duration) ms"
    }
}
