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
}
