import XCTest
@testable import AIBotBridge

final class DailyDataParityTests: XCTestCase {
    func testNetworkPublicationsHoldBetweenSecondsAndUseSameMeanAsCurve() {
        var history = MacNetworkHistory()
        var published: MacNetworkHistory.Publication?
        for i in 1...8 {
            let next = history.record(MacNetworkSample(upload: Int64(i * 100), download: Int64(i * 200)),
                                      at: Double(i) / 4, elapsed: 0.25)
            if i % 4 == 0 { XCTAssertNotNil(next); published = next }
            else { XCTAssertNil(next) }
        }
        XCTAssertEqual(published?.sequence, 8)
        XCTAssertEqual(published?.samples.count, 8)
        XCTAssertEqual(published?.mean, MacNetworkSample(upload: 650, download: 1300))
    }

    func testNetworkHistoryIsBoundedAndSuspensionDoesNotInventSamples() {
        var history = MacNetworkHistory()
        let sample = MacNetworkSample(upload: 100, download: 200)
        var result: MacNetworkHistory.Publication?
        for i in 1...100 { result = history.record(sample, at: Double(i) / 4, elapsed: 0.25) ?? result }
        XCTAssertEqual(result?.samples.count, 64)
        XCTAssertEqual(result?.sequence, 100)
        let resumed = history.record(sample, at: 27, elapsed: 2)
        XCTAssertEqual(resumed?.samples, [sample])
        XCTAssertEqual(resumed?.sequence, 108)
        XCTAssertEqual(resumed?.mean, sample)
        XCTAssertNil(history.record(sample, at: 27.25, elapsed: 0.25))
        XCTAssertNotEqual(history.session, MacNetworkHistory().session)
        XCTAssertEqual(MacSystemMetricsService.rate(previous: 0, current: .max, elapsed: 0.001), .max)
        XCTAssertEqual(MacSystemMetricsService.rate(previous: 0, current: 1, elapsed: .nan), 0)
    }

    func testCalendarWorkdayHolidayAndUnpublishedYear() throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(secondsFromGMT: 28800)!
        func day(_ value: String) -> Date {
            let f = DateFormatter(); f.calendar = calendar; f.timeZone = calendar.timeZone
            f.dateFormat = "yyyy-MM-dd"; return f.date(from: value)!
        }
        XCTAssertEqual(MacTab5Calendar.labels(day("2026-10-20"), calendar: calendar).holiday, "工作日")
        XCTAssertEqual(MacTab5Calendar.labels(day("2026-10-10"), calendar: calendar).holiday, "调休 · 补班")
        XCTAssertEqual(MacTab5Calendar.labels(day("2026-10-01"), calendar: calendar).holiday, "国庆节假期 · 休息")
        XCTAssertEqual(MacTab5Calendar.labels(day("2026-09-23"), calendar: calendar).term, "今日秋分")
        XCTAssertEqual(MacTab5Calendar.labels(day("2026-09-23"), calendar: calendar).holiday, "工作日 · 距中秋节假期 2 天")
        XCTAssertEqual(MacTab5Calendar.labels(day("2027-01-01"), calendar: calendar).holiday, "节假日安排待更新")
        let projection = MacTab5Calendar.snapshot(epoch: Int64(day("2026-12-31").timeIntervalSince1970), offset: 28800)
        XCTAssertEqual(projection["nextDate"] as? String, "2027-01-01")
        XCTAssertEqual(projection["nextHolidayLabel"] as? String, "节假日安排待更新")
    }

    func testWeatherSurvivesUnavailableAirQualityAndLocationIsCoarse() throws {
        let forecast = Data(#"{"current":{"temperature_2m":20,"relative_humidity_2m":50,"weather_code":0},"daily":{"temperature_2m_max":[25],"temperature_2m_min":[15]}}"#.utf8)
        for air in [nil, Data("invalid".utf8)] as [Data?] {
            let value = try MacDataService.parseWeather(forecast: forecast, air: air, city: "Synthetic")
            XCTAssertEqual(value.temperature, 20)
            XCTAssertNil(value.pm25); XCTAssertNil(value.airQualityIndex)
        }
        let grid = MacWeatherLocation.weatherGrid(latitude: 12.345678, longitude: -65.432123)
        XCTAssertEqual(grid.0, 12.35); XCTAssertEqual(grid.1, -65.43)
    }

    func testCalendarPageReceivesPublishedAnnualTablesWithoutInventingFutureYears() throws {
        // Exercise the JSON types and keys actually consumed by calendar_schedule,
        // calendar_holiday and calendar_term on the existing TAB5.
        for date in ["2026-10-20T12:00:00Z", "2027-01-01T00:00:00Z"] {
            let epoch = Int64(try XCTUnwrap(ISO8601DateFormatter().date(from: date)).timeIntervalSince1970)
            let wire = try JSONSerialization.data(withJSONObject: MacTab5Calendar.snapshot(epoch: epoch, offset: 28800))
            let value = try XCTUnwrap(JSONSerialization.jsonObject(with: wire) as? [String: Any])
            let detail = try XCTUnwrap(value["detail"] as? [String: Any])
            let years = try XCTUnwrap(detail["holidayYears"] as? [[String: Any]])
            XCTAssertEqual(years.compactMap { $0["year"] as? Int }, [2026])
            XCTAssertEqual(detail["holidayYear"] as? Int, 2026)
            let ranges = try XCTUnwrap(years.first?["holidays"] as? [[String: Any]])
            XCTAssertEqual(ranges.count, 7)
            let spring = try XCTUnwrap(ranges.first { $0["name"] as? String == "春节" })
            XCTAssertEqual(spring["start"] as? Int, 215)
            XCTAssertEqual(spring["end"] as? Int, 223)
            XCTAssertEqual(years.first?["workdays"] as? [Int], [104,214,228,509,920,1010])
            let terms = try XCTUnwrap(detail["terms"] as? [[String: Any]])
            XCTAssertEqual(terms.first?["year"] as? Int, 2026)
            let dates = try XCTUnwrap(terms.first?["dates"] as? [Int])
            XCTAssertEqual(dates.count, 24)
            XCTAssertEqual(dates[17], 923) // 秋分
            XCTAssertEqual(dates[23], 1222) // 冬至
        }
    }

    func testDomesticOfficialParsersKeepUnknownsAndRejectAmbiguousBalances() throws {
        let deep = try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"USD","total_balance":"2.00"},{"currency":"CNY","total_balance":"12.50"}]}"#.utf8))
        XCTAssertEqual(deep.balance, 12.5); XCTAssertEqual(deep.currency, "CNY")
        XCTAssertNil(deep.usedCost); XCTAssertNil(deep.primaryPercent)
        XCTAssertThrowsError(try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"CNY","total_balance":"1"},{"currency":"CNY","total_balance":"2"}]}"#.utf8)))
        XCTAssertThrowsError(try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"CNY","total_balance":true}]}"#.utf8)))
        let glm = try MacDomesticQuotaParser.zhipu(Data(#"{"code":200,"data":{"availableBalance":99.9993936,"balance":500,"totalSpendAmount":"3.279"}}"#.utf8))
        XCTAssertEqual(glm.balance, 99.99); XCTAssertEqual(glm.usedCost, 3.27)
        XCTAssertThrowsError(try MacDomesticQuotaParser.zhipu(Data(#"{"code":200,"data":{"balance":500}}"#.utf8)))
        XCTAssertThrowsError(try MacDomesticQuotaParser.zhipu(Data(#"{"code":401,"data":{"availableBalance":1}}"#.utf8)))
    }

    func testDomesticCacheFailureAndFixedDeviceProviderPositions() throws {
        let name = "daily-parity-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: name)!
        defer { defaults.removePersistentDomain(forName: name) }
        let store = MacDataStore(defaults: defaults)
        let deep = try MacDomesticQuotaParser.deepSeek(Data(#"{"balance_infos":[{"currency":"CNY","total_balance":"12.5"}]}"#.utf8))
        let glm = try MacDomesticQuotaParser.zhipu(Data(#"{"code":200,"data":{"availableBalance":99.9,"totalSpendAmount":3.2}}"#.utf8))
        store.updateDomestic(deep); store.updateDomestic(glm); store.markDomesticStale("deepseek")
        XCTAssertEqual(store.snapshot().domesticQuotas?.deepSeek?.balance, 12.5)
        XCTAssertEqual(store.snapshot().domesticQuotas?.deepSeek?.stale, true)
        XCTAssertEqual(MacDataStore(defaults: defaults).snapshot().domesticQuotas?.zhipu?.stale, true)
        let metrics = SystemMetricsSnapshot(cpuPercent: 10, memoryPercent: 20,
            uploadBytesPerSecond: 100, downloadBytesPerSecond: 200, updatedAt: Date(),
            sampleSession: "network-fixture", sampleSequence: 100,
            samples: Array(repeating: MacNetworkSample(upload: 100, download: 200), count: 64))
        store.update(systemMetrics: metrics)
        let reader = SessionActivityReader(metadata: MacActivitySampler(scan: { _ in MacActivityTotals() }))
        let snapshot = reader.capture(extras: store.snapshot())
        let packet = try MacTab5Protocol.snapshot(snapshot, device: "001122334455", session: "fixture", sequence: 1, command: nil)
        let data = try MacTab5Protocol.object(packet)["data"] as! [String: Any]
        let rows = data["quotaDetails"] as! [[String: Any]]
        XCTAssertEqual(rows.count, 11)
        XCTAssertEqual(rows[5]["id"] as? String, "deepseek")
        XCTAssertEqual(rows[6]["id"] as? String, "zhipu")
        XCTAssertEqual(rows[5]["billingMode"] as? String, "payg")
        XCTAssertEqual(rows[6]["cost"] as? Double, 3.2)
        XCTAssertNil(rows[5]["cost"])
        XCTAssertEqual(rows[5]["stale"] as? Bool, true)
        XCTAssertNotNil(data["domesticQuotas"])
        let wireMetrics = data["systemMetrics"] as! [String: Any]
        XCTAssertEqual((wireMetrics["samples"] as? [Any])?.count, 64)
        XCTAssertEqual(wireMetrics["sampleSequence"] as? Int, 100)
        XCTAssertLessThan(packet.count, MacTab5Protocol.maximumFrame)
    }

    func testZhipuCaptureRejectsOtherOriginsAndPaths() {
        XCTAssertTrue(MacDomesticQuotaWindow.financeEndpoint("https://bigmodel.cn/api/biz/account/query-customer-account-report"))
        for url in ["http://bigmodel.cn/api/biz/account/query-customer-account-report",
                    "https://evil.example/api/biz/account/query-customer-account-report",
                    "https://bigmodel.cn.evil.example/api/biz/account/query-customer-account-report",
                    "https://bigmodel.cn:444/api/biz/account/query-customer-account-report",
                    "https://bigmodel.cn/api/other"] {
            XCTAssertFalse(MacDomesticQuotaWindow.financeEndpoint(url))
        }
    }

    func testZhipuWeChatLoginAllowsOnlyOfficialQRCodeSubframe() {
        let qr = URL(string: "https://open.weixin.qq.com/connect/qrconnect?appid=synthetic&scope=snsapi_login")!
        XCTAssertTrue(MacDomesticQuotaWindow.navigationAllowed(qr, isMainFrame: false))
        XCTAssertFalse(MacDomesticQuotaWindow.navigationAllowed(qr, isMainFrame: true))
        XCTAssertFalse(MacDomesticQuotaWindow.financeEndpoint(qr.absoluteString))
        let blank = URL(string: "about:blank")!
        XCTAssertTrue(MacDomesticQuotaWindow.navigationAllowed(blank, isMainFrame: false))
        XCTAssertFalse(MacDomesticQuotaWindow.navigationAllowed(blank, isMainFrame: true))
        for url in ["https://bigmodel.cn/login", "https://bigmodel.cn/login/callback?code=synthetic",
                    "https://static.bigmodel.cn/resource"] {
            XCTAssertTrue(MacDomesticQuotaWindow.navigationAllowed(URL(string: url)!, isMainFrame: true))
            XCTAssertTrue(MacDomesticQuotaWindow.navigationAllowed(URL(string: url)!, isMainFrame: false))
        }
        for url in ["http://open.weixin.qq.com/connect/qrconnect",
                    "https://open.weixin.qq.com.evil.example/connect/qrconnect",
                    "https://open.weixin.qq.com:444/connect/qrconnect",
                    "https://open.weixin.qq.com/connect/other",
                    "https://user@open.weixin.qq.com/connect/qrconnect",
                    "https://bigmodel.cn.evil.example/login",
                    "https://bigmodel.cn:444/login",
                    "javascript:void(0)", "data:text/html,fixture"] {
            XCTAssertFalse(MacDomesticQuotaWindow.navigationAllowed(URL(string: url)!, isMainFrame: false))
        }
    }
}
