import Foundation

enum MacSolarTermSourceValidation {
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ value: Bool) throws { guard value else { throw MacTab5Error.invalidPacket }; count += 1 }
        func rejects(_ action: () throws -> Void) throws {
            do { try action() } catch { count += 1; return }; throw MacTab5Error.invalidPacket
        }
        let dates = [105,120,204,218,305,320,405,420,505,521,605,621,707,723,807,823,907,923,1008,1023,1107,1122,1207,1222]
        let months = ["一","二","三","四","五","六","七","八","九","十","十一","十二"]
        let names = ["小寒","大寒","立春","雨水","惊蛰","春分","清明","谷雨","立夏","小满","芒种","夏至","小暑","大暑","立秋","处暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
        let text = "二○二六年 日历资料 中国科学院紫金山天文台编制\n" + (0..<12).map { month in
            "二○二六年\(months[month])月大 " + (0..<2).map { item in
                let index = month * 2 + item
                return "【\(names[index])】\(dates[index] % 100)日12时30分"
            }.joined()
        }.joined(separator: "\n") + "二○二七年一月大【小寒】5日22时10分【大寒】20日15时30分"
        let table = try MacSolarTermSource.table(text: text, year: 2026)
        try check(try MacCalendarStore.parseTerms(table, year: 2026) == dates)
        try check(String(decoding: table, as: UTF8.self).contains("source=PMO"))
        let catalog = "<a href=\"./example.pdf\"><span>《2026年日历资料》</span></a>"
        try check(try MacSolarTermSource.documents(Data(catalog.utf8))[2026]?.host == "pmo.cas.cn")
        try check(try MacSolarTermSource.documents(Data(catalog.utf8))[2027] == nil)
        for invalid in [text.replacingOccurrences(of: "二○二六", with: "二○二五"), text.replacingOccurrences(of: "紫金山", with: "其他"),
                        text.replacingOccurrences(of: "二○二六年一月大", with: ""), text.replacingOccurrences(of: "二○二六年二月大", with: "二○二六年一月大"),
                        text.replacingOccurrences(of: "【小寒】", with: "【大寒】"), text.replacingOccurrences(of: "【冬至】", with: ""),
                        text.replacingOccurrences(of: "12时30分", with: "24时30分"), text.replacingOccurrences(of: "12时30分", with: "12时60分"),
                        text.replacingOccurrences(of: "【雨水】18日", with: "【雨水】30日"), text + text, String(repeating: "x", count: 131073)] {
            try rejects { _ = try MacSolarTermSource.table(text: invalid, year: 2026) }
        }
        try rejects { _ = try MacSolarTermSource.table(text: text, year: 2027) }
        for invalid in ["<html>unavailable</html>", catalog.replacingOccurrences(of: "./example.pdf", with: "https://untrusted.example/example.pdf"),
                        catalog.replacingOccurrences(of: "./example.pdf", with: "http://pmo.cas.cn/example.pdf"), catalog + catalog.replacingOccurrences(of: "example.pdf", with: "different.pdf")] {
            try rejects { _ = try MacSolarTermSource.documents(Data(invalid.utf8)) }
        }
        try rejects { _ = try MacSolarTermSource.table(Data("<html>error</html>".utf8), year: 2026) }
        let store = MacCalendarStore(folder: root)
        try store.accept(table, year: 2026, solar: true)
        try rejects { try store.accept(MacSolarTermSource.table(text: "unavailable", year: 2026), year: 2026, solar: true) }
        try check(try Data(contentsOf: root.appendingPathComponent("terms-2026.txt")) == table)
        let restored = MacCalendarStore(folder: root); restored.restore(year: 2026)
        try check(restored.termYear(2026) == dates)
        return count
    }
}
