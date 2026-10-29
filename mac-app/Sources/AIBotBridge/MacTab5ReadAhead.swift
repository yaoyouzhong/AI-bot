import Foundation

/// One bounded background read keeps slow desktop/history IO off the device
/// mailbox. Only completed results for the exact device/session/page are reused.
final class MacTab5ReadAhead {
    private let lock = NSLock()
    private let clock: () -> TimeInterval
    private let schedule: (@escaping () -> Void) -> Void
    private var pending = false
    private var cached: (key: String, at: TimeInterval, result: MacTab5RPCResult)?
    init(clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
         schedule: @escaping (@escaping () -> Void) -> Void = { work in DispatchQueue.global(qos: .utility).async(execute: work) }) {
        self.clock = clock; self.schedule = schedule
    }
    func response(key: String, read: @escaping () -> MacTab5RPCResult) -> MacTab5RPCResult {
        lock.lock()
        let now = clock(), match = cached.flatMap { $0.key == key && now >= $0.at ? $0 : nil }
        let fresh = match.map { now - $0.at < 1 } ?? false
        let start = !pending && !fresh
        if start { pending = true }
        // Retain a recent successful page while refreshing; never return a page
        // from another owner/session or replay a mutation in the background.
        let result = match.flatMap { fresh || ($0.result.status == 200 && now - $0.at < 15) ? $0.result : nil }
            ?? .error(429, "codex_read_pending")
        lock.unlock()
        if start {
            schedule { [self] in
                let result = read()
                lock.lock()
                if result.status == 200 || cached?.key != key || cached?.result.status != 200 || clock() - (cached?.at ?? 0) >= 15 {
                    cached = (key, clock(), result)
                }
                pending = false
                lock.unlock()
            }
        }
        return result
    }
}
