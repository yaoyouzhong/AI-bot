import Foundation

// Explicit local use supplements desktop recency metadata, which can remain
// unchanged throughout a long running turn. Polls and owner reads never write it.
final class MacTaskActivity {
    static let shared = MacTaskActivity()
    private let file: MacProfileFile<[String: Double]>
    private let now: () -> Double
    init(directory: URL? = nil, now: @escaping () -> Double = { Date().timeIntervalSince1970 }) {
        self.now = now
        file = .init("tab5-task-activity.json", directory: directory, initial: { [:] }) { values in
            guard values.count <= 512, values.allSatisfy({ MacTab5Codex.id($0.key) == $0.key &&
                $0.value.isFinite && (0...253_402_300_799).contains($0.value) }) else {
                throw MacTab5LocalError.invalid("会话使用时间无效，原记录已保留。")
            }
        }
    }
    func record(_ task: String) throws {
        guard MacTab5Codex.id(task) == task else { return }
        let time = now()
        try file.update { values in
            values[task] = max(values[task] ?? 0, time)
            if values.count > 512, let oldest = values.min(by: { $0.value < $1.value }) { values.removeValue(forKey: oldest.key) }
        }
    }
    func apply(_ tasks: [[String: Any]]) -> [[String: Any]] {
        guard let times = try? file.read() else { return tasks }
        var rows: [(offset: Int, time: Double, entry: [String: Any])] = []
        for (offset, originalEntry) in tasks.enumerated() {
            var entry = originalEntry
            let original = Double(MacTab5Protocol.integer(entry["updatedAt"]) ?? 0)
            let task = entry["id"] as? String ?? ""
            let effective = max(original, times[task] ?? 0)
            entry["updatedAt"] = Int64(effective)
            rows.append((offset, effective, entry))
        }
        rows.sort { left, right in
            if left.time == right.time { return left.offset < right.offset }
            return left.time > right.time
        }
        return rows.map { $0.entry }
    }
}
