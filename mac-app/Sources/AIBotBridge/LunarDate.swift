import Foundation

enum LunarDate {
    static func format(epoch: Int64, utcOffset: Int) -> String {
        guard let zone = TimeZone(secondsFromGMT: utcOffset) else { return "" }
        let date = Date(timeIntervalSince1970: Double(epoch))
        var gregorian = Calendar(identifier: .gregorian)
        gregorian.timeZone = zone
        let civil = gregorian.dateComponents([.year, .month, .day], from: date)
        guard let year = civil.year, let monthNumber = civil.month, let dayNumber = civil.day else { return "" }
        let key = year * 10000 + monthNumber * 100 + dayNumber
        guard (19010219...21010128).contains(key) else { return "" }
        // Interpret the displayed civil date through the Chinese calendar's reference zone.
        // This matches firmware/.NET even when the display uses a different UTC offset.
        let china = TimeZone(secondsFromGMT: 28800)!
        gregorian.timeZone = china
        guard let reference = gregorian.date(from: DateComponents(year: year, month: monthNumber, day: dayNumber, hour: 12)) else { return "" }
        var calendar = Calendar(identifier: .chinese)
        calendar.timeZone = china
        // Request all components to preserve leap-month information on macOS 13.
        let parts = calendar.dateComponents(in: china, from: reference)
        guard let month = parts.month, let day = parts.day,
              (1...12).contains(month), (1...30).contains(day) else { return "" }
        let months = ["正","二","三","四","五","六","七","八","九","十","冬","腊"]
        let digits = ["一","二","三","四","五","六","七","八","九","十"]
        let dayText: String
        switch day {
        case 10: dayText = "初十"
        case 20: dayText = "二十"
        case 30: dayText = "三十"
        default: dayText = (day < 10 ? "初" : day < 20 ? "十" : "廿") + digits[(day-1)%10]
        }
        return "农历" + (parts.isLeapMonth == true ? "闰" : "") + months[month-1] + "月" + dayText
    }
}
