import Foundation

struct MacTab5BLERecovery<Key: Hashable> {
    enum Operation: Equatable { case statusWrite, statusACK, probe }
    enum Callback: Equatable { case current, late, expired(Operation) }
    private var pending: (operation: Operation, key: Key, deadline: TimeInterval)?
    private var draining: [Key: (operation: Operation, deadline: TimeInterval)] = [:]
    private var statusFailures = 0, statusResumeAt = -TimeInterval.infinity
    private var probeFailures: [Key: Int] = [:]

    func canUse(_ key: Key) -> Bool { draining[key] == nil }
    func canStartStatus(now: TimeInterval, write: Key, ack: Key) -> Bool {
        now >= statusResumeAt && canUse(write) && canUse(ack)
    }

    mutating func begin(_ operation: Operation, key: Key, now: TimeInterval) {
        pending = (operation, key, now + 3)
    }

    mutating func expire(now: TimeInterval) -> Operation? {
        guard let pending, now >= pending.deadline else { return nil }
        // The delegate provides no request ID. Do not issue another operation
        // on this characteristic until the outstanding callback is consumed.
        draining[pending.key] = (pending.operation, pending.deadline + 9)
        self.pending = nil
        return pending.operation
    }

    mutating func receive(_ key: Key, now: TimeInterval) -> Callback {
        if let late = draining.removeValue(forKey: key) {
            if late.operation == .statusWrite { statusResumeAt = now + 5.1 }
            return .late
        }
        guard let pending, pending.key == key else { return .current }
        self.pending = nil
        return now >= pending.deadline ? .expired(pending.operation) : .current
    }

    func mustReconnect(now: TimeInterval) -> Bool {
        draining.values.contains { now >= $0.deadline }
    }

    mutating func statusFailed(now: TimeInterval, connected: Bool) -> Bool {
        guard connected, statusFailures < 2 else { return false }
        statusFailures += 1
        // The firmware drops partial status only after 5 s without input.
        statusResumeAt = now + 5.1
        return true
    }

    mutating func statusConfirmed() { statusFailures = 0; statusResumeAt = -.infinity }

    mutating func probeFailed(_ key: Key, connected: Bool) -> Bool {
        let count = probeFailures[key, default: 0]
        guard connected, count < 2 else { return false }
        probeFailures[key] = count + 1
        return true
    }

    mutating func probeConfirmed(_ key: Key) { probeFailures.removeValue(forKey: key) }
}
