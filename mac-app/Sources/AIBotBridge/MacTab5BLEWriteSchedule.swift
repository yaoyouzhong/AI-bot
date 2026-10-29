import Foundation

// Mirrors the production Windows mailbox policy, not its opt-in queued probe.
// A retry owns only an immutable response window; it never re-executes the RPC.
struct MacTab5BLEWriteSchedule {
    static func continuationDelay(now: TimeInterval, lastControl: TimeInterval, completed: Bool) -> TimeInterval? {
        if completed { return 0 }
        return now.isFinite && lastControl.isFinite && now >= lastControl && now - lastControl < 1 ? 0.015 : nil
    }
    static func prioritizeBulk(now: TimeInterval, lastBulk: TimeInterval, lastFrame: TimeInterval) -> Bool {
        now.isFinite && lastBulk.isFinite && lastFrame.isFinite && now >= lastBulk && now - lastBulk < 1 &&
            now >= lastFrame && now - lastFrame < 6
    }
    struct Capabilities {
        let regular: Int, bulk: Int
        let replay: Bool
        init(_ info: [String: Any] = [:]) {
            let voice = Int(MacTab5Protocol.integer(info["mailboxWindow"]) ?? 4)
            regular = min(8, max(1, Int(MacTab5Protocol.integer(info["rpcMailboxWindow"]) ?? Int64(voice))))
            let offered = Int(MacTab5Protocol.integer(info["rpcWriteWindow"]) ?? 0)
            bulk = (8...64).contains(offered) ? offered : regular
            replay = MacTab5Protocol.integer(info["rpcReplay"]) == 1
        }
    }
    let count: Int, window: Int, recoverable: Bool, confirmed: Bool
    private(set) var start = 0
    private(set) var replaying = false
    private(set) var replayCount = 0

    init(count: Int, mailbox: Bool, bulk: Bool, withoutResponse: Bool, capabilities: Capabilities) {
        self.count = count
        window = mailbox ? (bulk ? capabilities.bulk : capabilities.regular) : 5
        recoverable = mailbox && bulk && withoutResponse && capabilities.replay
        confirmed = !withoutResponse || (mailbox && bulk && !capabilities.replay)
    }
    var end: Int { min(count, start + window) }
    func requiresResponse(at index: Int) -> Bool { confirmed || replaying || index + 1 == end }
    mutating func acknowledged(next: Int) {
        if next == end { start = next; replaying = false }
    }
    mutating func recover(protocolError: Int) -> Int? {
        guard protocolError == 0x0e, recoverable, !replaying, start < count else { return nil }
        replaying = true; replayCount += 1
        return start
    }
}
