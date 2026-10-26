import Foundation

enum MacTab5Calendar {
    // Public calendar facts, independently projected onto the existing TAB5 fields.
    // HKO: https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2026c.txt
    // State Council 2026 notice:
    // https://www.beijing.gov.cn/fuwu/bmfw/sy/jrts/202511/t20251104_4258838.html
    private static let terms = ["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至",
                                "小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
    // Verified HKO public date facts provide offline coverage through 2028.
    private static let fallbackTerms = [
        2026: [105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222],
        2027: [105,120,204,219,306,321,405,420,506,521,606,621,707,723,808,823,908,923,1008,1023,1107,1122,1207,1222],
        2028: [106,120,204,219,305,320,404,419,505,520,605,621,706,722,807,822,907,922,1008,1023,1107,1122,1206,1221]
    ]
    private static let holidays = [(101,103,"元旦"),(215,223,"春节"),(404,406,"清明节"),
                                   (501,505,"劳动节"),(619,621,"端午节"),(925,927,"中秋节"),(1001,1007,"国庆节")]
    private static let workdays: Set<Int> = [104,214,228,509,920,1010]
    private static let fallbackYear: MacCalendarStore.Year = {
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = .gmt
        func date(_ md: Int) -> Date { calendar.date(from: DateComponents(year: 2026, month: md / 100, day: md % 100))! }
        var days: [MacCalendarStore.Day] = []
        for range in holidays {
            var current = date(range.0)
            while current <= date(range.1) {
                days.append(.init(date: current, name: range.2, off: true))
                current = calendar.date(byAdding: .day, value: 1, to: current)!
            }
        }
        days += workdays.map { .init(date: date($0), name: "调休", off: false) }
        return .init(year: 2026, days: days)
    }()
    private static func annual(_ year: Int, store: MacCalendarStore) -> [String: Any]? {
        store.holidayYear(year, fallback: year == 2026 ? fallbackYear : nil)?.projection
    }
    private static func termDates(_ year: Int, store: MacCalendarStore) -> [Int]? {
        store.termYear(year) ?? fallbackTerms[year]
    }

    static var workdayYears: [[String: Any]] {
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = TimeZone(identifier: "Asia/Shanghai")!
        let year = calendar.component(.year, from: Date())
        return ((year - 1)...(year + 1)).compactMap { annual($0, store: .shared) }
    }
    static func isWorkday(civil: Int64, store: MacCalendarStore = .shared) -> Bool? {
        var calendar = Calendar(identifier: .gregorian); calendar.timeZone = .gmt
        let parts = calendar.dateComponents([.year, .month, .day, .weekday], from: Date(timeIntervalSince1970: Double(civil)))
        guard let year = parts.year, let schedule = annual(year, store: store), let month = parts.month, let day = parts.day else { return nil }
        let md = month * 100 + day
        if (schedule["workdays"] as? [Int] ?? []).contains(md) { return true }
        if (schedule["holidays"] as? [[String: Any]] ?? []).contains(where: { ($0["start"] as? Int ?? 0) <= md && md <= ($0["end"] as? Int ?? 0) }) { return false }
        return ![1, 7].contains(parts.weekday ?? 1)
    }

    private static func detail(year: Int, store: MacCalendarStore) -> [String: Any] {
        // The calendar page reads annual tables, independently of the home labels.
        // Keep the published year available when browsing across New Year; never
        // relabel these facts as an unknown future year's holiday arrangement.
        let years = ((year - 1)...(year + 1)).compactMap { annual($0, store: store) }
        let selected = years.first { $0["year"] as? Int == year } ?? years.last ?? [:]
        let dates: [[String: Any]] = ((year - 1)...(year + 3)).compactMap { y in
            termDates(y, store: store).map { ["year": y, "dates": $0] }
        }
        return ["terms": dates, "holidayYear": selected["year"] ?? 0,
                "holidays": selected["holidays"] ?? [], "workdays": selected["workdays"] ?? [],
                "holidayYears": years, "holidayUpdate": store.status]
    }

    static func labels(_ date: Date, calendar: Calendar, store: MacCalendarStore = .shared) -> (term: String, holiday: String) {
        let p = calendar.dateComponents([.year, .month, .day, .weekday], from: date)
        guard let year = p.year, let month = p.month, let day = p.day else {
            return ("节气待更新", "节假日安排待更新")
        }
        let md = month * 100 + day
        let dates = termDates(year, store: store)
        let term = dates?.enumerated().first { $0.element >= md }.map {
            $0.element == md ? "今日" + terms[$0.offset] :
                String(format: "下个节气 %@ %02d-%02d", terms[$0.offset], $0.element / 100, $0.element % 100)
        } ?? (dates?.last.map { md > $0 } == true ? termDates(year + 1, store: store)?.first.map { String(format: "下个节气 小寒 01-%02d", $0 % 100) } : nil) ?? "节气待更新"
        guard let schedule = annual(year, store: store) else { return (term, "节假日安排待更新") }
        let ranges = (schedule["holidays"] as? [[String: Any]] ?? []).compactMap { h -> (Int, Int, String)? in
            guard let start = h["start"] as? Int, let end = h["end"] as? Int, let name = h["name"] as? String else { return nil }
            return (start, end, name)
        }
        if (schedule["workdays"] as? [Int] ?? []).contains(md) { return (term, "调休 · 补班") }
        if let h = ranges.first(where: { $0.0 <= md && md <= $0.1 }) { return (term, h.2 + "假期 · 休息") }
        let label = [1, 7].contains(p.weekday ?? 0) ? "周末" : "工作日"
        if let h = ranges.first(where: { $0.0 > md }),
           let start = calendar.date(from: DateComponents(year: year, month: h.0 / 100, day: h.0 % 100)),
           let days = calendar.dateComponents([.day], from: calendar.startOfDay(for: date), to: start).day {
            return (term, "\(label) · 距\(h.2)假期 \(days) 天")
        }
        return (term, label)
    }

    static func snapshot(epoch: Int64, offset: Int, store: MacCalendarStore = .shared) -> [String: Any] {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(secondsFromGMT: offset) ?? .gmt
        let today = Date(timeIntervalSince1970: Double(epoch))
        let tomorrow = cal.date(byAdding: .day, value: 1, to: today) ?? today
        let format = DateFormatter(); format.calendar = cal; format.timeZone = cal.timeZone
        format.locale = Locale(identifier: "en_US_POSIX"); format.dateFormat = "yyyy-MM-dd"
        let a = labels(today, calendar: cal, store: store), b = labels(tomorrow, calendar: cal, store: store)
        return ["date": format.string(from: today),
                "weekday": "星期" + ["日","一","二","三","四","五","六"][cal.component(.weekday, from: today) - 1],
                "lunar": LunarDate.format(epoch: epoch, utcOffset: offset),
                "solarTermLabel": a.term, "holidayLabel": a.holiday,
                "nextDate": format.string(from: tomorrow),
                "nextLunar": LunarDate.format(epoch: Int64(tomorrow.timeIntervalSince1970), utcOffset: offset),
                "nextSolarTermLabel": b.term, "nextHolidayLabel": b.holiday,
                "detail": detail(year: cal.component(.year, from: today), store: store)]
    }
}
