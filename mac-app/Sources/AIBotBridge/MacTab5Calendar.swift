import Foundation

enum MacTab5Calendar {
    // Public calendar facts, independently projected onto the existing TAB5 fields.
    // HKO: https://www.hko.gov.hk/tc/gts/time/calendar/text/files/T2026c.txt
    // State Council 2026 notice:
    // https://www.beijing.gov.cn/fuwu/bmfw/sy/jrts/202511/t20251104_4258838.html
    private static let terms = ["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至",
                                "小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
    private static let dates2026 = [105,120,204,218,305,320,405,420,505,521,605,621,
                                    707,723,807,823,907,923,1008,1023,1107,1122,1207,1222]
    private static let holidays = [(101,103,"元旦"),(215,223,"春节"),(404,406,"清明节"),
                                   (501,505,"劳动节"),(619,621,"端午节"),(925,927,"中秋节"),(1001,1007,"国庆节")]
    private static let workdays: Set<Int> = [104,214,228,509,920,1010]

    static func labels(_ date: Date, calendar: Calendar) -> (term: String, holiday: String) {
        let p = calendar.dateComponents([.year, .month, .day, .weekday], from: date)
        guard p.year == 2026, let month = p.month, let day = p.day else {
            return ("节气待更新", "节假日安排待更新")
        }
        let md = month * 100 + day
        let term = dates2026.enumerated().first { $0.element >= md }.map {
            $0.element == md ? "今日" + terms[$0.offset] :
                String(format: "下个节气 %@ %02d-%02d", terms[$0.offset], $0.element / 100, $0.element % 100)
        } ?? "节气待更新"
        if workdays.contains(md) { return (term, "调休 · 补班") }
        if let h = holidays.first(where: { $0.0 <= md && md <= $0.1 }) { return (term, h.2 + "假期 · 休息") }
        let label = [1, 7].contains(p.weekday ?? 0) ? "周末" : "工作日"
        if let h = holidays.first(where: { $0.0 > md }),
           let start = calendar.date(from: DateComponents(year: 2026, month: h.0 / 100, day: h.0 % 100)),
           let days = calendar.dateComponents([.day], from: calendar.startOfDay(for: date), to: start).day {
            return (term, "\(label) · 距\(h.2)假期 \(days) 天")
        }
        return (term, label)
    }

    static func snapshot(epoch: Int64, offset: Int) -> [String: Any] {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(secondsFromGMT: offset) ?? .gmt
        let today = Date(timeIntervalSince1970: Double(epoch))
        let tomorrow = cal.date(byAdding: .day, value: 1, to: today) ?? today
        let format = DateFormatter(); format.calendar = cal; format.timeZone = cal.timeZone
        format.locale = Locale(identifier: "en_US_POSIX"); format.dateFormat = "yyyy-MM-dd"
        let a = labels(today, calendar: cal), b = labels(tomorrow, calendar: cal)
        return ["date": format.string(from: today),
                "weekday": "星期" + ["日","一","二","三","四","五","六"][cal.component(.weekday, from: today) - 1],
                "lunar": LunarDate.format(epoch: epoch, utcOffset: offset),
                "solarTermLabel": a.term, "holidayLabel": a.holiday,
                "nextDate": format.string(from: tomorrow),
                "nextLunar": LunarDate.format(epoch: Int64(tomorrow.timeIntervalSince1970), utcOffset: offset),
                "nextSolarTermLabel": b.term, "nextHolidayLabel": b.holiday]
    }
}
