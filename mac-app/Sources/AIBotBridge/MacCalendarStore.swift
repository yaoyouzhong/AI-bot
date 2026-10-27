import Foundation

// Independently implemented reader for the same public data sources as Windows.
// Network access runs only from the application timer, never from a frame getter.
final class MacCalendarStore {
    struct Day { let date: Date, name: String, off: Bool }
    struct Year {
        let year: Int, days: [Day]
        var projection: [String: Any] {
            var ranges: [[String: Any]] = [], workdays: [Int] = []
            var previous: Date?
            for day in days.sorted(by: { $0.date < $1.date }) where Self.calendar.component(.year, from: day.date) == year {
                let md = Self.calendar.component(.month, from: day.date) * 100 + Self.calendar.component(.day, from: day.date)
                if !day.off { workdays.append(md); continue }
                if let previous, Self.calendar.date(byAdding: .day, value: 1, to: previous) == day.date,
                   ranges.last?["name"] as? String == day.name {
                    ranges[ranges.count - 1]["end"] = md
                } else { ranges.append(["start": md, "end": md, "name": day.name]) }
                previous = day.date
            }
            return ["year": year, "holidays": ranges, "workdays": workdays]
        }
        private static var calendar: Calendar { MacCalendarStore.calendar }
    }
    private struct Document: Decodable {
        struct Entry: Decodable { let date: String, name: String, isOffDay: Bool }
        let year: Int, papers: [String], days: [Entry]
    }
    static let shared = MacCalendarStore()
    private let lock = NSLock(), folder: URL
    private var holidays: [Int: Year] = [:], terms: [Int: [Int]] = [:]
    private var busy = false, checkedYear = 0, nextCheck = Date.distantPast
    private var note = "等待检查"
    private static let termNames = ["小寒","大寒","立春","雨水","驚蟄","春分","清明","穀雨","立夏","小滿","芒種","夏至",
                                    "小暑","大暑","立秋","處暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
    private static var calendar: Calendar { var value = Calendar(identifier: .gregorian); value.timeZone = .gmt; return value }
    init(folder: URL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support/AI-bot/calendar")) { self.folder = folder }
    var status: String { lock.lock(); defer { lock.unlock() }; return note }
    func termYear(_ year: Int) -> [Int]? { lock.lock(); defer { lock.unlock() }; return terms[year] }
    func holidayYear(_ year: Int, fallback: Year? = nil) -> Year? {
        lock.lock(); defer { lock.unlock() }
        guard let current = holidays[year] ?? fallback else { return nil }
        let changes = holidays[year + 1]?.days.filter { Self.calendar.component(.year, from: $0.date) == year } ?? []
        let dates = Set(changes.map(\.date))
        return Year(year: year, days: current.days.filter { !dates.contains($0.date) } + changes)
    }
    static func parseHolidays(_ data: Data, year: Int) throws -> Year? {
        guard data.count <= 65536, (2000...2099).contains(year) else { throw MacTab5Error.invalidPacket }
        let doc = try JSONDecoder().decode(Document.self, from: data)
        guard doc.year == year else { throw MacTab5Error.invalidPacket }
        if doc.papers.isEmpty && doc.days.isEmpty { return nil } // Published placeholder, not a schedule.
        guard (1...8).contains(doc.papers.count), doc.papers.allSatisfy({
            guard let url = URL(string: $0) else { return false }
            return url.scheme == "https" && url.host == "www.gov.cn" && url.user == nil && url.password == nil
        }), (20...80).contains(doc.days.count) else { throw MacTab5Error.invalidPacket }
        let format = DateFormatter(); format.locale = Locale(identifier: "en_US_POSIX")
        format.calendar = calendar; format.timeZone = .gmt; format.dateFormat = "yyyy-MM-dd"; format.isLenient = false
        var seen = Set<Date>(), days: [Day] = []
        for entry in doc.days {
            guard (1...16).contains(entry.name.count), !entry.name.unicodeScalars.contains(where: CharacterSet.controlCharacters.contains),
                  let date = format.date(from: entry.date), format.string(from: date) == entry.date,
                  seen.insert(date).inserted else { throw MacTab5Error.invalidPacket }
            let parts = calendar.dateComponents([.year, .month], from: date)
            guard parts.year == year || (parts.year == year - 1 && parts.month == 12) else { throw MacTab5Error.invalidPacket }
            days.append(Day(date: date, name: entry.name, off: entry.isOffDay))
        }
        guard ["元旦","春节","清明","劳动","端午","中秋","国庆"].allSatisfy({ name in
            days.contains { $0.off && calendar.component(.year, from: $0.date) == year && $0.name.contains(name) }
        }) else { throw MacTab5Error.invalidPacket }
        let value = Year(year: year, days: days)
        guard (value.projection["holidays"] as? [[String: Any]] ?? []).count <= 16 else { throw MacTab5Error.invalidPacket }
        return value
    }
    static func parseTerms(_ data: Data, year: Int) throws -> [Int] {
        guard data.count <= 131072, (1901...2099).contains(year), let text = String(data: data, encoding: .utf8) else { throw MacTab5Error.invalidPacket }
        var result = Array(repeating: 0, count: 24)
        let expression = try NSRegularExpression(pattern: "^(\\d{4})年(\\d{1,2})月(\\d{1,2})日$")
        for line in text.split(separator: "\n") {
            let fields = line.split(whereSeparator: \.isWhitespace).map(String.init)
            guard fields.count >= 4, let last = fields.last, let index = termNames.firstIndex(of: last) else { continue }
            let date = fields[0] as NSString
            guard let match = expression.firstMatch(in: fields[0], range: NSRange(location: 0, length: date.length)),
                  Int(date.substring(with: match.range(at: 1))) == year,
                  let month = Int(date.substring(with: match.range(at: 2))), month == index / 2 + 1,
                  let day = Int(date.substring(with: match.range(at: 3))), (1...28).contains(day), result[index] == 0 else { throw MacTab5Error.invalidPacket }
            result[index] = month * 100 + day
        }
        guard result.allSatisfy({ $0 > 0 }), result == result.sorted() else { throw MacTab5Error.invalidPacket }
        return result
    }
    // Validate before replacement. A placeholder, malformed response, or failed
    // atomic write cannot erase the last usable in-memory or on-disk year.
    func accept(_ data: Data, year: Int, solar: Bool, persist: Bool = true) throws {
        let dates = solar ? try Self.parseTerms(data, year: year) : nil
        let schedule = solar ? nil : try Self.parseHolidays(data, year: year)
        guard dates != nil || schedule != nil else { return }
        if persist {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            try data.write(to: path(year, solar: solar), options: .atomic)
        }
        lock.lock(); defer { lock.unlock() }
        if let dates { terms[year] = dates }; if let schedule { holidays[year] = schedule }
    }
    private func path(_ year: Int, solar: Bool) -> URL { folder.appendingPathComponent(solar ? "terms-\(year).txt" : "\(year).json") }
    func restore(year: Int) {
        for solar in [false, true] {
            for y in (year - 1)...(year + (solar ? 3 : 1)) {
                let file = path(y, solar: solar)
                guard let size = try? file.resourceValues(forKeys: [.fileSizeKey]).fileSize,
                      size <= (solar ? 131072 : 65536), let data = try? Data(contentsOf: file) else { continue }
                try? accept(data, year: y, solar: solar, persist: false)
            }
        }
    }
    func schedule(now: Date = Date()) {
        var local = Self.calendar; local.timeZone = TimeZone(identifier: "Asia/Shanghai")!
        let year = local.component(.year, from: now)
        guard (2001...2096).contains(year) else { return }
        lock.lock()
        guard !busy, checkedYear != year || now >= nextCheck else { lock.unlock(); return }
        busy = true; lock.unlock()
        Task.detached { [self] in
            restore(year: year)
            var failed = false
            let config = URLSessionConfiguration.ephemeral
            config.timeoutIntervalForRequest = 15; config.timeoutIntervalForResource = 20
            let session = URLSession(configuration: config)
            defer { session.invalidateAndCancel() }
            func download(_ url: URL, maximum: Int, solar: Bool) async throws -> Data? {
                let (bytes, response) = try await session.bytes(from: url)
                if !solar, (response as? HTTPURLResponse)?.statusCode == 404 { return nil }
                guard let http = response as? HTTPURLResponse, http.statusCode == 200,
                      response.expectedContentLength <= maximum else { throw MacTab5Error.invalidPacket }
                if solar { guard let final = http.url, MacSolarTermSource.allowed(final) else { throw MacTab5Error.invalidPacket } }
                var data = Data()
                for try await byte in bytes { guard data.count < maximum else { throw MacTab5Error.invalidPacket }; data.append(byte) }
                return data
            }
            for solar in [false, true] {
                var documents: [Int: URL] = [:]
                if solar {
                    do {
                        guard let data = try await download(MacSolarTermSource.catalog, maximum: 524288, solar: true) else { throw MacTab5Error.invalidPacket }
                        documents = try MacSolarTermSource.documents(data)
                    }
                    catch { failed = true; continue }
                }
                for y in (year - 1)...(year + (solar ? 3 : 1)) {
                    let url = solar ? documents[y] : URL(string: "https://raw.githubusercontent.com/NateScarlet/holiday-cn/master/\(y).json")
                    guard let url else { continue } // Unpublished annual calendars preserve offline coverage.
                    do {
                        guard let data = try await download(url, maximum: solar ? 4194304 : 65536, solar: solar) else { continue }
                        try accept(solar ? MacSolarTermSource.table(data, year: y) : data, year: y, solar: solar)
                    } catch { failed = true }
                }
            }
            finish(year: year, failed: failed)
        }
    }
    private func finish(year: Int, failed: Bool) {
        lock.lock(); defer { lock.unlock() }
        note = failed ? "更新失败，保留已有安排" : "已检查，未发布年份等待公告"
        checkedYear = year; nextCheck = Date().addingTimeInterval(failed ? 3600 : 86400); busy = false
    }
}
