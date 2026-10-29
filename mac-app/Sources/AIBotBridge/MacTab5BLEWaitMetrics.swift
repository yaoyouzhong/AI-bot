import Foundation

struct MacTab5BLEWaitMetrics {
    private var began: TimeInterval?
    private(set) var count = 0, totalMs = 0, maximumMs = 0, cancelledMs = 0
    private var buckets = [Int](repeating: 0, count: 6)
    mutating func begin(_ now: TimeInterval) { if began == nil { began = now } }
    mutating func end(_ now: TimeInterval, cancelled: Bool = false) {
        guard let start = began else { return }; began = nil
        let ms = Int(max(0, now - start) * 1000)
        if cancelled { cancelledMs += ms; return }
        count += 1; totalMs += ms; maximumMs = max(maximumMs, ms)
        let bucket = [5, 20, 50, 100, 250].firstIndex(where: { ms < $0 }) ?? 5
        buckets[bucket] += 1
    }
    var values: [String: Int] {
        var result = ["waitCallbacks": count, "waitMs": totalMs, "maxWaitMs": maximumMs, "cancelledWaitMs": cancelledMs]
        for (i, name) in ["lt5ms", "lt20ms", "lt50ms", "lt100ms", "lt250ms", "ge250ms"].enumerated() { result[name] = buckets[i] }
        return result
    }
}
