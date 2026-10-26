import Foundation

struct MacQuotaTrend: Codable, Equatable {
    struct Day: Codable, Equatable {
        let date: String
        let label: String
        let used: Double?
        let partial: Bool
        var carryOver: Double? = nil
        // Local detail fields are not sent to the display or persisted in status caches.
        var samples = 0, resets = 0, uncertainResets = 0, gaps = 0
        var coverage: String? = nil
        private enum CodingKeys: String, CodingKey { case date, label, used, partial, carryOver }
    }
    var provider = "codex"
    var unit = "weekly_percentage_points"
    var timezone = "Asia/Shanghai"
    let days: [Day]
    let storageWarning: Bool
}

// Same observed-increment contract as Windows QuotaHistory; never token totals.
// Access is serialized by MacDataStore. Only display values and a hashed account
// discriminator are persisted, never credentials or supplier response bodies.
final class MacQuotaHistory {
    struct Observation: Codable {
        let at: Date
        let plan: String?
        let weekly: Double?
        let reset: Date?
        let account: String?
        var primary: Double? = nil
        var primaryReset: Date? = nil
    }
    private let file: URL
    private var rows: [Observation] = []
    private var preserveOriginal = false
    private var warning = false
    private var cached: (at: Date, trend: MacQuotaTrend)?
    private static var calendar: Calendar {
        var value = Calendar(identifier: .gregorian)
        value.timeZone = TimeZone(identifier: "Asia/Shanghai")!
        return value
    }

    init(file: URL = FileManager.default.homeDirectoryForCurrentUser
        .appendingPathComponent("Library/Application Support/AI-bot/codex-quota-history.json")) {
        self.file = file
        guard FileManager.default.fileExists(atPath: file.path) else { return }
        do {
            let size = try file.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0
            guard size <= 64 * 1024 * 1024 else { throw CocoaError(.fileReadTooLarge) }
            let loaded = try JSONDecoder().decode([Observation].self, from: Data(contentsOf: file))
            guard loaded.allSatisfy(Self.valid) else { throw CocoaError(.fileReadCorruptFile) }
            var seen = Set<Date>()
            rows = Array(loaded.sorted { $0.at < $1.at }.filter { seen.insert($0.at).inserted }.suffix(140_000))
        } catch { preserveOriginal = true; warning = true }
    }

    private static func valid(_ row: Observation) -> Bool {
        row.at.timeIntervalSince1970.isFinite && row.at.timeIntervalSince1970 > 0 &&
        (row.weekly.map { $0.isFinite && (0...100).contains($0) } ?? true) &&
        (row.primary.map { $0.isFinite && (0...100).contains($0) } ?? true) &&
        (row.account.map { $0.count == 64 && $0.utf8.allSatisfy { (48...57).contains($0) || (97...102).contains($0) } } ?? true)
    }

    func record(_ quota: ProviderQuotaSnapshot, account: String?) {
        guard quota.provider == "codex", !quota.stale, quota.weeklyPercent != nil || quota.primaryPercent != nil else { return }
        let row = Observation(at: quota.updatedAt, plan: quota.plan, weekly: quota.weeklyPercent,
                              reset: quota.weeklyResetsAt, account: account, primary:quota.primaryPercent, primaryReset:quota.primaryResetsAt)
        guard Self.valid(row), rows.last.map({ row.at > $0.at }) ?? true else { return }
        rows.append(row)
        rows.removeAll { $0.at < row.at.addingTimeInterval(-90 * 86400) }
        rows = Array(rows.suffix(140_000)); cached = nil
        guard !preserveOriginal else { return }
        do {
            try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
            try JSONEncoder().encode(rows).write(to: file, options: .atomic)
            warning = false
        } catch { warning = true }
    }

    func snapshot(now: Date = Date()) -> MacQuotaTrend {
        if let cached, now >= cached.at, now.timeIntervalSince(cached.at) < 30,
           Self.calendar.isDate(now, inSameDayAs: cached.at) { return cached.trend }
        let value = Self.build(rows, now: now, warning: warning)
        cached = (now, value)
        return value
    }

    func history(days: Int, now: Date = Date()) -> MacQuotaTrend { Self.build(rows, now: now, warning: warning, days: days) }
    func hourly(days: Int, now: Date = Date()) -> [Observation] {
        let recent = rows.filter { $0.at >= now.addingTimeInterval(-Double(min(30,max(1,days))) * 86400) && $0.at <= now && $0.primary != nil }
        return Dictionary(grouping:recent,by:{ Int64($0.at.timeIntervalSince1970) / 3600 }).values.compactMap { $0.max(by: { $0.at < $1.at }) }.sorted { $0.at < $1.at }
    }

    static func build(_ rows: [Observation], now: Date, warning: Bool = false, days count: Int = 7) -> MacQuotaTrend {
        let calendar = Self.calendar, today = calendar.startOfDay(for: now)
        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.calendar = calendar; formatter.timeZone = calendar.timeZone
        formatter.dateFormat = "yyyy-MM-dd"
        let days = (0..<min(30, max(1, count))).reversed().map { offset -> MacQuotaTrend.Day in
            let start = calendar.date(byAdding: .day, value: -offset, to: today)!
            let end = calendar.date(byAdding: .day, value: 1, to: start)!
            let indices = rows.indices.filter { rows[$0].at >= start && rows[$0].at < end }
            var partial = indices.isEmpty || start >= today || !covered(rows, start) || !covered(rows, end)
            var total = 0.0, carryOver = 0.0
            var comparable = 0, resets = 0, uncertainResets = 0, gaps = 0
            for i in indices where i > 0 {
                let after = rows[i]
                var baseline = i - 1
                while baseline > 0 && rows[baseline].weekly == nil && after.account != nil &&
                    rows[baseline].account == after.account && rows[baseline].plan == after.plan { baseline -= 1 }
                let before = rows[baseline]
                let sameIdentity = before.account != nil && before.account == after.account
                if !sameIdentity { partial = true }
                if let old = before.account, let new = after.account, old != new { gaps += 1; continue }
                let age = after.at.timeIntervalSince(before.at), carried = before.at < start && age > 300
                if before.at < start && !sameIdentity { partial = true; continue }
                guard age > 0, let old = before.weekly, let new = after.weekly, before.plan == after.plan else {
                    gaps += 1; partial = true; continue
                }
                let same = sameWindow(before, after)
                var reset = false
                if !same, let oldReset = before.reset, let newReset = after.reset {
                    reset = (oldReset > before.at && oldReset <= after.at && newReset > oldReset) ||
                        (new < old && before.account != nil && newReset > oldReset && newReset > after.at)
                }
                if age > 300 {
                    gaps += 1
                    if !sameIdentity || ((!same || new < old) && !reset) { partial = true; continue }
                }
                if reset {
                    total += new; comparable += 1; resets += 1
                    if age > 300 { partial = true }
                    if carried { carryOver += new }
                } else if (same && new >= old) || (old == 0 && new == 0 && before.account != nil) {
                    total += new - old; comparable += 1
                    if carried { carryOver += new - old }
                } else { uncertainResets += 1; partial = true }
            }
            let date = formatter.string(from: start)
            let clock = DateFormatter(); clock.timeZone = calendar.timeZone; clock.dateFormat = "MM-dd HH:mm:ss"
            func sample(_ row: Observation?) -> String {
                guard let row else { return "—" }
                return clock.string(from: row.at) + "（" + (row.weekly.map { String(format: "%.2f%%", $0) } ?? "未知") + "）"
            }
            let first = indices.first.map { rows[$0] }, last = indices.last.map { rows[$0] }
            let coverage = "首末采样：\(sample(first)) → \(sample(last))；前一采样：\(sample(rows.last { $0.at < start }))；后一采样：\(sample(rows.first { $0.at >= end }))"
            return .init(date: date, label: String(date.suffix(5)),
                         used: comparable == 0 ? nil : (total * 100).rounded() / 100, partial: partial || carryOver > 0,
                         carryOver: carryOver > 0 ? (carryOver * 100).rounded() / 100 : nil,
                         samples: indices.count, resets: resets, uncertainResets: uncertainResets, gaps: gaps, coverage: coverage)
        }
        return MacQuotaTrend(days: days, storageWarning: warning)
    }

    private static func sameWindow(_ a: Observation, _ b: Observation) -> Bool {
        guard let x = a.reset, let y = b.reset else { return false }
        return x == y || (x > b.at && y > b.at && abs(y.timeIntervalSince(x)) <= 60)
    }
    private static func flat(_ a: Observation, _ b: Observation) -> Bool {
        a.account != nil && a.account == b.account && a.plan == b.plan &&
        a.weekly != nil && a.weekly == b.weekly && sameWindow(a, b)
    }
    private static func covered(_ rows: [Observation], _ boundary: Date) -> Bool {
        if rows.contains(where: { $0.at == boundary && $0.weekly != nil && $0.account != nil }) { return true }
        guard let a = rows.last(where: { $0.at < boundary }), let b = rows.first(where: { $0.at > boundary }),
              b.at.timeIntervalSince(a.at) <= 300 || flat(a, b),
              a.account != nil, a.account == b.account, a.plan == b.plan,
              let old = a.weekly, let new = b.weekly, new >= old else { return false }
        return sameWindow(a, b)
    }
}
