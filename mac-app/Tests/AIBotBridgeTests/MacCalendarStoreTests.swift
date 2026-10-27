import XCTest
@testable import AIBotBridge

final class MacCalendarStoreTests: XCTestCase {
    func testDomesticSolarSourceYearMonthValidationAndOfflineCache() throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: folder) }
        XCTAssertEqual(try MacSolarTermSourceValidation.run(root: folder), 24)
    }
    private func holidays(_ year: Int = 2027, previousDecember: Bool = false) throws -> Data {
        // Synthetic complete schema fixture, not a prediction of holiday dates.
        let ranges = [(101,103,"元旦"),(215,223,"春节"),(404,406,"清明节"),(501,505,"劳动节"),
                      (619,621,"端午节"),(925,927,"中秋节"),(1001,1007,"国庆节")]
        var days: [[String: Any]] = []
        for (start, end, name) in ranges {
            for md in start...end { days.append(["date": String(format: "%04d-%02d-%02d", year, md / 100, md % 100), "name": name, "isOffDay": true]) }
        }
        days.append(["date": "\(year)-01-04", "name": "元旦", "isOffDay": false])
        if previousDecember { days.append(["date": "\(year - 1)-12-31", "name": "元旦", "isOffDay": true]) }
        return try MacTab5Protocol.json(["year": year, "papers": ["https://www.gov.cn/synthetic-notice"], "days": days])
    }
    private func terms(_ year: Int = 2027) -> Data {
        let names = ["小寒","大寒","立春","雨水","驚蟄","春分","清明","穀雨","立夏","小滿","芒種","夏至",
                     "小暑","大暑","立秋","處暑","白露","秋分","寒露","霜降","立冬","小雪","大雪","冬至"]
        let dates = [105,120,204,219,306,321,405,420,506,521,606,621,707,723,808,823,908,923,1008,1023,1107,1122,1207,1222]
        return Data(zip(names, dates).map { "\(year)年\($0.1 / 100)月\($0.1 % 100)日 十一 星期日 \($0.0)" }.joined(separator: "\n").utf8)
    }
    private func store() -> MacCalendarStore {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        addTeardownBlock { try? FileManager.default.removeItem(at: folder) }
        return MacCalendarStore(folder: folder)
    }
    func testHolidayValidationRejectsIncompleteDuplicateInvalidAndWrongYear() throws {
        let good = try holidays()
        XCTAssertNotNil(try MacCalendarStore.parseHolidays(good, year: 2027))
        XCTAssertThrowsError(try MacCalendarStore.parseHolidays(good, year: 2026))
        for invalid in ["duplicate", "date", "source", "partial", "boolean"] {
            var doc = try MacTab5Protocol.object(good), days = doc["days"] as! [[String: Any]]
            if invalid == "duplicate" { days.append(days[0]) }
            if invalid == "date" { days[0]["date"] = "2027-02-30" }
            if invalid == "source" { doc["papers"] = ["https://untrusted.example/notice"] }
            if invalid == "partial" { days.removeAll { $0["name"] as? String == "春节" } }
            if invalid == "boolean" { days[0]["isOffDay"] = 1 }
            doc["days"] = days
            XCTAssertThrowsError(try MacCalendarStore.parseHolidays(MacTab5Protocol.json(doc), year: 2027))
        }
    }
    func testLastGoodCacheSurvivesInvalidAndUnpublishedDataAndOfflineReload() throws {
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: folder) }
        let cache = MacCalendarStore(folder: folder), good = try holidays()
        try cache.accept(good, year: 2027, solar: false)
        try cache.accept(terms(), year: 2027, solar: true)
        XCTAssertThrowsError(try cache.accept(Data("bad".utf8), year: 2027, solar: false))
        let empty = try MacTab5Protocol.json(["year": 2027, "papers": [], "days": []])
        try cache.accept(empty, year: 2027, solar: false)
        XCTAssertEqual(try Data(contentsOf: folder.appendingPathComponent("2027.json")), good)
        let restored = MacCalendarStore(folder: folder); restored.restore(year: 2027)
        XCTAssertNotNil(restored.holidayYear(2027)); XCTAssertEqual(restored.termYear(2027)?.count, 24)
        XCTAssertNil(try MacCalendarStore.parseHolidays(empty, year: 2027))
    }
    func testSolarTableRequiresAllOrderedTermsAndCorrectYear() throws {
        XCTAssertEqual(try MacCalendarStore.parseTerms(terms(), year: 2027).count, 24)
        XCTAssertThrowsError(try MacCalendarStore.parseTerms(terms(), year: 2028))
        let text = String(decoding: terms(), as: UTF8.self)
        for bad in [text + "\n" + text, text.replacingOccurrences(of: "冬至", with: ""), text.replacingOccurrences(of: "1月5日", with: "2月5日")] {
            XCTAssertThrowsError(try MacCalendarStore.parseTerms(Data(bad.utf8), year: 2027))
        }
    }
    func testNewYearLabelsWorkdaysAndPreviousDecemberCorrectionsUseSameSchedule() throws {
        let cache = store()
        try cache.accept(holidays(previousDecember: true), year: 2027, solar: false, persist: false)
        try cache.accept(terms(), year: 2027, solar: true, persist: false)
        var cal = Calendar(identifier: .gregorian); cal.timeZone = .gmt
        func date(_ text: String) -> Date { ISO8601DateFormatter().date(from: text + "T00:00:00Z")! }
        XCTAssertEqual(MacTab5Calendar.labels(date("2026-12-31"), calendar: cal, store: cache).holiday, "元旦假期 · 休息")
        XCTAssertEqual(MacTab5Calendar.labels(date("2026-12-31"), calendar: cal, store: cache).term, "下个节气 小寒 01-05")
        XCTAssertEqual(MacTab5Calendar.labels(date("2027-01-05"), calendar: cal, store: cache).term, "今日小寒")
        XCTAssertEqual(MacTab5Calendar.isWorkday(civil: Int64(date("2027-01-01").timeIntervalSince1970), store: cache), false)
        XCTAssertEqual(MacTab5Calendar.isWorkday(civil: Int64(date("2027-01-04").timeIntervalSince1970), store: cache), true)
        let projection = MacTab5Calendar.snapshot(epoch: Int64(date("2026-12-31").timeIntervalSince1970), offset: 0, store: cache)
        XCTAssertEqual(projection["nextHolidayLabel"] as? String, "元旦假期 · 休息")
        let detail = try XCTUnwrap(projection["detail"] as? [String: Any])
        XCTAssertEqual((detail["holidayYears"] as? [[String: Any]])?.compactMap { $0["year"] as? Int }, [2026,2027])
    }
    func testSolarAvailabilityDoesNotInventFutureHolidaySchedule() throws {
        let cache = store() // Built-in HKO fallback also works without any network/cache.
        var cal = Calendar(identifier: .gregorian); cal.timeZone = .gmt
        let date = ISO8601DateFormatter().date(from: "2027-01-05T00:00:00Z")!
        let value = MacTab5Calendar.labels(date, calendar: cal, store: cache)
        XCTAssertEqual(value.term, "今日小寒"); XCTAssertEqual(value.holiday, "节假日安排待更新")
        XCTAssertNil(MacTab5Calendar.isWorkday(civil: Int64(date.timeIntervalSince1970), store: cache))
        let leapYear = ISO8601DateFormatter().date(from: "2028-09-22T00:00:00Z")!
        XCTAssertEqual(MacTab5Calendar.labels(leapYear, calendar: cal, store: cache).term, "今日秋分")
    }
}
