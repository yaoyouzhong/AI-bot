import XCTest
@testable import AIBotBridge

final class AIBotBridgeTests: XCTestCase {
    func testActivityClassification() {
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 89), "working")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 90), "idle")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 900), "idle")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 901), "offline")
    }

    func testPairingComparison() {
        XCTAssertTrue(HTTPStatusServer.constantTimeEqual(String(repeating: "a", count: 32), String(repeating: "a", count: 32)))
        XCTAssertFalse(HTTPStatusServer.constantTimeEqual(String(repeating: "a", count: 32), String(repeating: "b", count: 32)))
        XCTAssertFalse(HTTPStatusServer.constantTimeEqual("short", String(repeating: "a", count: 32)))
    }

    func testWeatherAndStockParsing() throws {
        let forecast = Data(#"{"current":{"temperature_2m":26.5,"relative_humidity_2m":58,"weather_code":2},"daily":{"temperature_2m_max":[31.0],"temperature_2m_min":[19.0]}}"#.utf8)
        let air = Data(#"{"current":{"us_aqi":46,"pm2_5":13.2}}"#.utf8)
        let weather = try MacDataService.parseWeather(forecast: forecast, air: air, city: "北京")
        XCTAssertEqual(weather.condition, "多云")
        XCTAssertEqual(weather.airQualityIndex, 46)

        let quotes = MacDataService.parseStocks(
            "v_sh000001=\"1~上证指数~000001~3821.44~~~~~~~~~~~~~~~~~~~~~~~~~~~~32.20~0.85~\";",
            order: ["sh000001"])
        XCTAssertEqual(quotes.first?.name, "上证指数")
        XCTAssertEqual(quotes.first?.changePercent, "+0.85%")
    }

    func testSystemMetricDeltas() {
        let previous = MacCpuTicks(user: 100, system: 50, idle: 850, nice: 0)
        let current = MacCpuTicks(user: 120, system: 60, idle: 920, nice: 0)
        XCTAssertEqual(MacSystemMetricsService.cpuPercent(previous: previous, current: current), 30,
                       accuracy: 0.001)
        XCTAssertEqual(MacSystemMetricsService.rate(previous: 1_000, current: 4_000, elapsed: 2), 1_500)
        XCTAssertEqual(MacSystemMetricsService.rate(previous: 4_000, current: 1_000, elapsed: 2), 0)
    }

    func testQuotaParsing() throws {
        let claude = try MacQuotaService.parseClaude(Data(#"{"plan_type":"max","five_hour":{"utilization":25,"resets_at":"2026-09-05T12:00:00Z"},"seven_day":{"utilization":40,"resets_at":"2026-09-09T12:00:00Z"}}"#.utf8))
        XCTAssertEqual(claude.primaryPercent, 25)
        XCTAssertEqual(claude.weeklyPercent, 40)

        let codex = try MacQuotaService.parseCodex(Data(#"{"plan_type":"plus","rate_limit":{"primary_window":{"limit_window_seconds":18000,"used_percent":12,"reset_at":1788600000},"secondary_window":{"limit_window_seconds":604800,"used_percent":34,"reset_at":1789000000}}}"#.utf8))
        XCTAssertEqual(codex.primaryPercent, 12)
        XCTAssertEqual(codex.weeklyPercent, 34)
    }

    func testSerialCandidateFiltering() {
        XCTAssertTrue(SerialBridge.isCandidateDeviceName("cu.wchusbserial1420"))
        XCTAssertTrue(SerialBridge.isCandidateDeviceName("cu.usbserial-110"))
        XCTAssertTrue(SerialBridge.isCandidateDeviceName("cu.SLAB_USBtoUART"))
        XCTAssertFalse(SerialBridge.isCandidateDeviceName("tty.Bluetooth-Incoming-Port"))
        XCTAssertFalse(SerialBridge.isCandidateDeviceName("cu.not-a-device"))
    }

    func testSerialStatusFrameUsesProtocolPrefix() throws {
        let snapshot = MacStatusSnapshot(
            version: 1, time: "12:34:56", epochUtc: 1_788_500_000, utcOffsetSeconds: 28_800,
            capturedAt: Date(timeIntervalSince1970: 1_788_500_000),
            codex: ToolState(state: "working", ageSeconds: 2),
            claude: ToolState(state: "idle", ageSeconds: 180), musicPlaying: nil,
            weather: nil, stocks: nil, systemMetrics: nil, quotas: nil)
        let frame = try XCTUnwrap(SerialBridge.statusFrame(snapshot))
        XCTAssertTrue(String(decoding: frame, as: UTF8.self).hasPrefix("@AIBOT "))
        let payload = frame.dropFirst("@AIBOT ".utf8.count).dropLast()
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: Data(payload)) as? [String: Any])
        XCTAssertEqual(root["version"] as? Int, 1)
        XCTAssertEqual(root["type"] as? String, "status")
        XCTAssertNotNil(root["data"] as? [String: Any])
        XCTAssertLessThanOrEqual(frame.count, 6_144)
    }
}
