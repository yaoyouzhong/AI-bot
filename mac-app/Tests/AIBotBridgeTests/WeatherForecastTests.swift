import XCTest
import AppKit
@testable import AIBotBridge

final class WeatherForecastTests: XCTestCase {
    let now = Date(timeIntervalSince1970: 1_792_454_400) // Fixtures derive their own city dates below.
    func meteo() throws -> Data {
        let offset = 20700
        let fmt = MacWeatherForecast.formatter("yyyy-MM-dd'T'HH:mm", offset: offset)
        let day = MacWeatherForecast.formatter("yyyy-MM-dd", offset: offset)
        let start = floor((now.timeIntervalSince1970 + Double(offset)) / 3600) * 3600 - Double(offset)
        let hours = (0..<168).map { fmt.string(from: Date(timeIntervalSince1970: start + Double($0 - 1) * 3600)) }
        let days = (0..<7).map { day.string(from: now.addingTimeInterval(Double($0) * 86400)) }
        return try JSONSerialization.data(withJSONObject: [
            "utc_offset_seconds": offset,
            "current": ["temperature_2m": 20, "relative_humidity_2m": 50, "weather_code": 2, "pressure_msl": 1013.2],
            "hourly": ["time": hours, "temperature_2m": Array(repeating: 18, count: 168),
                       "precipitation_probability": [0, NSNull(), 101, 20], "weather_code": [0, 2]],
            "daily": ["time": days, "temperature_2m_max": Array(repeating: 25, count: 7),
                      "temperature_2m_min": Array(repeating: 12, count: 7), "weather_code": Array(repeating: 3, count: 7),
                      "sunrise": days.map { $0 + "T06:00" }, "sunset": days.map { $0 + "T18:00" },
                      "daylight_duration": [43200, 86400, 0, -1], "precipitation_sum": [0, 2]]
        ])
    }
    func testMeteoForecastUsesCityTimeAndKeepsUnknownValues() throws {
        let w = try MacDataService.parseWeather(forecast: meteo(), air: nil, city: "Synthetic", now: now)
        XCTAssertEqual(w.hourly?.count, 24); XCTAssertEqual(w.daily?.count, 7)
        XCTAssertEqual(w.utcOffsetSeconds, 20700)
        XCTAssertEqual(w.pressureHpa, 1013.2); XCTAssertEqual(w.pressureKind, "sea_level")
        XCTAssertNil(w.hourly?[0].rainProbability); XCTAssertNil(w.hourly?[1].rainProbability)
        XCTAssertEqual(w.hourly?[2].rainProbability, 20)
        XCTAssertEqual(w.daily?[0].sunrise, "06:00"); XCTAssertEqual(w.daily?[0].sunset, "18:00")
        XCTAssertEqual(w.daily?[1].daylightSeconds, 86400); XCTAssertEqual(w.daily?[2].daylightSeconds, 0)
        XCTAssertNil(w.daily?[3].daylightSeconds)
        XCTAssertEqual(String(w.hourly![0].time.suffix(2)), "00")
        XCTAssertNil(MacWeatherForecast.solar("2099-01-01T06:00", day: "2026-01-01"))
        XCTAssertNil(MacWeatherForecast.solar("25:00", day: "2026-01-01"))
        XCTAssertNil(MacWeatherForecast.number(true))
    }
    func testQWeatherCurrentDailyHourlyAndPartialFailure() throws {
        let day = MacWeatherForecast.formatter("yyyy-MM-dd", offset: 28800).string(from: now)
        let time = MacWeatherForecast.formatter("yyyy-MM-dd'T'HH:mmXXXXX", offset: 28800).string(from: now)
        let c = try JSONSerialization.data(withJSONObject: ["code": "200", "now": ["obsTime": time, "temp": "21", "humidity": "60", "text": "晴", "icon": "100", "pressure": "980"]])
        let d = try JSONSerialization.data(withJSONObject: ["code": "200", "daily": [["fxDate": day, "textDay": "晴", "tempMin": "10", "tempMax": "25", "sunrise": "06:00", "sunset": "18:00", "precip": "0"]]])
        let h = try JSONSerialization.data(withJSONObject: ["code": "200", "hourly": [["fxTime": time, "temp": "22", "text": "晴", "pop": "40"]]])
        let w = try MacWeatherForecast.qweather(current: c, daily: d, hourly: h, city: "Synthetic", now: now)
        XCTAssertEqual(w.source, "qweather"); XCTAssertEqual(w.weatherCode, 0)
        XCTAssertEqual(w.pressureHpa, 980); XCTAssertEqual(w.pressureKind, "station")
        XCTAssertEqual(w.utcOffsetSeconds, 28800); XCTAssertEqual(w.hourly?.first?.rainProbability, 40)
        XCTAssertEqual(w.daily?.first?.precipitationMm, 0); XCTAssertNil(w.daily?.first?.rainProbability)
        let partial = try MacWeatherForecast.qweather(current: c, daily: d, hourly: Data("error".utf8), city: "Synthetic", now: now)
        XCTAssertEqual(partial.temperature, 21); XCTAssertNil(partial.hourly)
        XCTAssertThrowsError(try MacWeatherForecast.qweather(current: Data(#"{"code":"401"}"#.utf8), daily: d, hourly: h, city: "Synthetic", now: now))
    }
    func testForecastCacheAndActualTab5PacketRetainSeries() throws {
        let name = "forecast-fixture-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: name)!
        defer { defaults.removePersistentDomain(forName: name) }
        let store = MacDataStore(defaults: defaults)
        let weather = try MacDataService.parseWeather(forecast: meteo(), air: nil, city: "Synthetic", now: now)
        store.update(weather: weather); store.markWeatherStale()
        XCTAssertEqual(store.snapshot().weather?.hourly?.count, 24)
        let cached = MacDataStore(defaults: defaults).snapshot().weather
        XCTAssertEqual(cached?.daily?.count, 7); XCTAssertEqual(cached?.stale, true)
        var partial = weather; partial.hourly = []; partial.daily = []
        store.update(weather: partial)
        XCTAssertEqual(store.snapshot().weather?.hourlyStale, true)
        XCTAssertEqual(store.snapshot().weather?.dailyStale, true)
        let reader = SessionActivityReader(metadata: MacActivitySampler(scan: { _ in MacActivityTotals() }))
        let packet = try MacTab5Protocol.snapshot(reader.capture(extras: store.snapshot()), device: "001122334455", session: "fixture", sequence: 1, command: nil)
        let root = try MacTab5Protocol.object(packet)["data"] as! [String: Any]
        let wire = root["weather"] as! [String: Any]
        XCTAssertEqual((wire["hourly"] as? [Any])?.count, 24)
        XCTAssertEqual((wire["daily"] as? [Any])?.count, 7)
        XCTAssertLessThan(packet.count, MacTab5Protocol.maximumFrame)
        XCTAssertEqual(wire["pressureHpa"] as? Double, 1013.2)
        XCTAssertEqual(wire["pressureKind"] as? String, "sea_level")
    }

    func testAirQualityScaleBoundariesAndMissingPressure() throws {
        for (aqi, label) in [(0, "优"), (50, "优"), (51, "良"), (100, "良"),
                             (101, "敏感人群不健康"), (150, "敏感人群不健康"),
                             (151, "不健康"), (200, "不健康"), (201, "非常不健康"),
                             (300, "非常不健康"), (301, "危险"), (600, "危险")] {
            let air = Data("{\"current\":{\"us_aqi\":\(aqi),\"pm2_5\":null}}".utf8)
            let w = try MacDataService.parseWeather(forecast: meteo(), air: air, city: "Synthetic", now: now)
            XCTAssertEqual(w.airQualityIndex, aqi); XCTAssertEqual(w.airQualityLabel, label)
            XCTAssertEqual(w.airQualityStandard, "us-epa"); XCTAssertNil(w.pm25)
        }
        var fixture = try JSONSerialization.jsonObject(with: meteo()) as! [String: Any]
        var current = fixture["current"] as! [String: Any]
        for invalid in [NSNull(), -1, 0, 1201, true, "bad"] as [Any] {
            current["pressure_msl"] = invalid; fixture["current"] = current
            let w = try MacDataService.parseWeather(forecast: JSONSerialization.data(withJSONObject: fixture),
                air: Data(#"{"current":{"us_aqi":-1,"pm2_5":12}}"#.utf8), city: "Synthetic", now: now)
            XCTAssertNil(w.pressureHpa); XCTAssertNil(w.pressureKind)
            XCTAssertNil(w.airQualityIndex); XCTAssertNil(w.airQualityLabel)
            XCTAssertEqual(w.pm25, 12)
        }
    }

    func testQAirUsesEPAScaleAndRestrictedCoordinateRequest() throws {
        var w = try MacDataService.parseWeather(forecast: meteo(), air: nil, city: "Synthetic", now: now)
        MacWeatherForecast.addQAir(Data(#"{"indexes":[{"code":"qaqi","aqi":0.9,"category":"Excellent"},{"code":"us-epa","aqi":46,"category":"Good"}],"pollutants":[{"code":"pm2p5","concentration":{"value":11,"unit":"μg/m3"}}]}"#.utf8), to: &w)
        XCTAssertEqual(w.airQualityIndex, 46); XCTAssertEqual(w.airQualityLabel, "优")
        XCTAssertEqual(w.airQualityStandard, "us-epa"); XCTAssertEqual(w.pm25, 11)
        var unknown = try MacDataService.parseWeather(forecast: meteo(), air: nil, city: "Synthetic", now: now)
        MacWeatherForecast.addQAir(Data(#"{"indexes":[{"code":"qaqi","aqi":0.9}],"pollutants":[{"code":"pm2p5","concentration":{"value":11,"unit":"mg/m3"}}]}"#.utf8), to: &unknown)
        XCTAssertNil(unknown.airQualityIndex); XCTAssertNil(unknown.pm25)
        let r = try MacQWeatherService.airRequest(host: "test.re.qweatherapi.com", key: "synthetic-key", latitude: 12.346, longitude: -67.891)
        XCTAssertEqual(r.url?.path, "/airquality/v1/current/12.35/-67.89")
        XCTAssertEqual(r.url?.query, "lang=zh")
        XCTAssertFalse(r.url!.absoluteString.contains("synthetic-key"))
        XCTAssertEqual(r.value(forHTTPHeaderField: "X-QW-Api-Key"), "synthetic-key")
        XCTAssertThrowsError(try MacQWeatherService.airRequest(host: "evil.example", key: "fixture", latitude: 0, longitude: 0))
        XCTAssertThrowsError(try MacQWeatherService.airRequest(host: "test.qweatherapi.com", key: "fixture", latitude: .nan, longitude: 0))
    }

    func testQAirUsesActualChineseLocalStandard() throws {
        // QWeather returns a regional index, not US EPA data in every country.
        for code in ["cn-mee", "cn-mee-1h"] {
            for (aqi, label) in [(50, "优"), (100, "良"), (150, "轻度污染"),
                                 (200, "中度污染"), (300, "重度污染"), (500, "严重污染")] {
                var w = try MacDataService.parseWeather(forecast: meteo(), air: nil, city: "Synthetic", now: now)
                let data = Data("{\"indexes\":[{\"code\":\"\(code)\",\"aqi\":\(aqi)}]}".utf8)
                MacWeatherForecast.addQAir(data, to: &w)
                XCTAssertEqual(w.airQualityIndex, aqi); XCTAssertEqual(w.airQualityLabel, label)
                XCTAssertEqual(w.airQualityStandard, code)
            }
        }
    }

    func testWeatherFactsSurvivePartialFailureAndReachDevice() throws {
        let name = "weather-facts-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: name)!
        defer { defaults.removePersistentDomain(forName: name) }
        let store = MacDataStore(defaults: defaults)
        let weather = try MacDataService.parseWeather(forecast: meteo(),
            air: Data(#"{"current":{"us_aqi":68,"pm2_5":12.5}}"#.utf8), city: "Synthetic", now: now)
        store.update(weather: weather)
        var partial = try MacDataService.parseWeather(forecast: meteo(), air: nil, city: "Synthetic", now: now)
        partial.pressureHpa = nil; partial.pressureKind = nil
        store.update(weather: partial)
        let cached = try XCTUnwrap(MacDataStore(defaults: defaults).snapshot().weather)
        XCTAssertEqual(cached.airQualityIndex, 68); XCTAssertEqual(cached.airQualityLabel, "良")
        XCTAssertEqual(cached.pressureHpa, 1013.2); XCTAssertEqual(cached.pm25, 12.5)
        XCTAssertTrue(cached.stale)
        let reader = SessionActivityReader(metadata: MacActivitySampler(scan: { _ in MacActivityTotals() }))
        let packet = try MacTab5Protocol.snapshot(reader.capture(extras: store.snapshot()), device: "001122334455", session: "fixture", sequence: 1, command: nil)
        let root = try MacTab5Protocol.object(packet)["data"] as! [String: Any]
        let wire = root["weather"] as! [String: Any]
        XCTAssertEqual(wire["airQualityLabel"] as? String, "良")
        XCTAssertEqual(wire["airQualityStandard"] as? String, "us-epa")
        XCTAssertEqual(wire["airQualityIndex"] as? Int, 68)
        partial = WeatherSnapshot(city: "Elsewhere", condition: "晴", temperature: 20, high: 25, low: 10,
            humidity: 50, weatherCode: 0, pm25: nil, airQualityIndex: nil, source: "qweather", updatedAt: now, stale: false)
        store.update(weather: partial)
        XCTAssertNil(store.snapshot().weather?.airQualityIndex)
        XCTAssertNil(store.snapshot().weather?.pressureHpa)
    }
    func testQWeatherRequestsRestrictDestinationAndKeepKeyOutOfURL() throws {
        let key = "synthetic-test-key"
        let r = try MacQWeatherService.request(host: "test.qweatherapi.com", key: key, path: "/v7/weather/24h", location: "12.34,56.78")
        XCTAssertEqual(r.value(forHTTPHeaderField: "X-QW-Api-Key"), key)
        XCTAssertFalse(r.url!.absoluteString.contains(key))
        for host in ["test.qweatherapi.com", "test.re.qweatherapi.com"] {
            XCTAssertTrue(MacQWeatherService.validHost(host))
            XCTAssertEqual(try MacQWeatherService.request(host: host, key: key, path: "/v7/weather/now", location: "fixture").url?.host, host)
        }
        for host in ["https://test.qweatherapi.com", "test.qweatherapi.com.evil.example", "qweatherapi.com", "a..qweatherapi.com", "a.-re.qweatherapi.com", "a.re-.qweatherapi.com", "test.qweatherapi.com:443", "-test.qweatherapi.com", "test.qweatherapi.com/path", "user@test.qweatherapi.com"] {
            XCTAssertFalse(MacQWeatherService.validHost(host))
            XCTAssertThrowsError(try MacQWeatherService.request(host: host, key: key, path: "/v7/weather/now", location: "fixture"))
        }
        XCTAssertThrowsError(try MacQWeatherService.request(host: "test.qweatherapi.com", key: key, path: "/unrelated", location: "fixture"))
        XCTAssertThrowsError(try MacQWeatherService.request(host: "test.qweatherapi.com", key: "bad\r\nkey", path: "/v7/weather/now", location: "fixture"))
    }

    @MainActor
    func testWeatherSourceSelectionShowsOnlyRelevantCredentialsAndKeepsEdits() async {
        _ = NSApplication.shared
        let provider = NSPopUpButton()
        provider.addItems(withTitles: ["Open-Meteo", "和风天气"])
        let host = NSTextField(string: "fixture.re.qweatherapi.com")
        let key = NSSecureTextField(string: "synthetic-unsaved")
        let fields = MacWeatherSourceFields(provider: provider, fields: [host, key])
        XCTAssertTrue(host.isHidden); XCTAssertTrue(key.isHidden)
        provider.selectItem(at: 1); fields.update()
        XCTAssertFalse(host.isHidden); XCTAssertFalse(key.isHidden)
        provider.selectItem(at: 0); fields.update()
        provider.selectItem(at: 1); fields.update()
        XCTAssertEqual(host.stringValue, "fixture.re.qweatherapi.com")
        XCTAssertEqual(key.stringValue, "synthetic-unsaved")
    }
}
