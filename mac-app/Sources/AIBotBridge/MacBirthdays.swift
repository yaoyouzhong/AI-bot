import Foundation

struct MacBirthday: Codable, Equatable {
    var name: String
    var month: Int
    var day: Int
    var lunar: Bool
    var leap: Bool
    var remind: Int
}

final class MacBirthdays {
    static let shared = MacBirthdays()
    let file: MacProfileFile<[MacBirthday]>
    init(directory: URL? = nil) { file = .init("birthdays.json", directory: directory, initial: { [] }, validate: Self.validate) }
    static func validate(_ values: [MacBirthday]) throws {
        guard values.count <= 32 else { throw MacTab5LocalError.invalid("最多保存 32 个生日。") }
        let lengths = [31,29,31,30,31,30,31,31,30,31,30,31]
        for value in values {
            guard MacProfileValidation.text(value.name, limit: 16), (1...12).contains(value.month),
                  value.day > 0, value.day <= (value.lunar ? 30 : lengths[value.month - 1]),
                  (0...30).contains(value.remind), !value.leap || value.lunar else {
                throw MacTab5LocalError.invalid("请检查姓名、生日日期和提前提醒天数；闰月仅用于农历。")
            }
        }
    }
    func projection() throws -> [[String: Any]] { try file.read().map { try MacProfileValidation.object($0) } }
    static func occurrence(_ value: MacBirthday, year: Int) -> Date? {
        var solar = Calendar(identifier: .gregorian); solar.timeZone = .gmt
        guard (2000...2099).contains(year), let start = solar.date(from: DateComponents(year: year, month: 1, day: 1)) else { return nil }
        if !value.lunar {
            guard let month = solar.date(from: DateComponents(year: year, month: value.month, day: 1)),
                  let days = solar.range(of: .day, in: .month, for: month) else { return nil }
            return solar.date(from: DateComponents(year: year, month: value.month, day: min(value.day, days.count)))
        }
        var lunar = Calendar(identifier: .chinese); lunar.timeZone = .gmt
        var inYear = false, normal: Date?, leap: Date?
        for offset in 0..<430 {
            guard let date = solar.date(byAdding: .day, value: offset, to: start) else { continue }
            let parts = lunar.dateComponents(in: .gmt, from: date)
            if parts.month == 1 && parts.day == 1 && parts.isLeapMonth != true { if inYear { break }; inYear = true }
            guard inYear, parts.month == value.month, let day = parts.day else { continue }
            let next = solar.date(byAdding: .day, value: 1, to: date)!
            let lastDay = lunar.component(.day, from: next) == 1
            if day == value.day || lastDay && day < value.day {
                if parts.isLeapMonth == true { leap = date } else { normal = date }
            }
        }
        return value.leap ? leap ?? normal : normal
    }
}
