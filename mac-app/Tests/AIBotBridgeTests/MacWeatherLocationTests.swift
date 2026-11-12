import XCTest
@testable import AIBotBridge

final class MacWeatherLocationTests: XCTestCase {
    func testPlaceLabelsUseOnlyAvailableAdministrativeNames() {
        XCTAssertEqual(MacWeatherLocation.placeLabel(city: "合成市", district: "测试区", region: "示例省"), "合成市 · 测试区")
        XCTAssertEqual(MacWeatherLocation.placeLabel(city: "合成市", district: "合成市", region: nil), "合成市")
        XCTAssertEqual(MacWeatherLocation.placeLabel(city: "合成市", district: "合成市测试区", region: nil), "合成市测试区")
        XCTAssertEqual(MacWeatherLocation.placeLabel(city: nil, district: nil, region: "示例省"), "示例省")
        XCTAssertEqual(MacWeatherLocation.placeLabel(city: nil, district: "测试区", region: nil), "测试区")
        XCTAssertEqual(MacWeatherLocation.placeLabel(city: "  Sample\nCity ", district: " District ", region: nil), "Sample City · District")
        XCTAssertNil(MacWeatherLocation.placeLabel(city: "\0", district: " ", region: String(repeating: "x", count: 161)))
    }
    @MainActor
    func testNameCacheRetainsSuccessOnlyForTheSameGridAfterLookupFailure() async {
        var calls = 0
        let location = MacWeatherLocation(reverseLookup: { _, _ in calls += 1; return calls == 1 ? "合成市 · 测试区" : nil })
        let start = Date(timeIntervalSince1970: 1000)
        let first = await location.placeName(latitude: 12.35, longitude: -65.43, now: start)
        XCTAssertEqual(first, "合成市 · 测试区"); XCTAssertEqual(calls, 1)
        let cached = await location.placeName(latitude: 12.35, longitude: -65.43, now: start.addingTimeInterval(900))
        XCTAssertEqual(cached, first); XCTAssertEqual(calls, 1)
        let failed = await location.placeName(latitude: 12.35, longitude: -65.43, now: start.addingTimeInterval(21_601))
        XCTAssertEqual(failed, first); XCTAssertEqual(calls, 2)
        let moved = await location.placeName(latitude: 13.35, longitude: -65.43, now: start.addingTimeInterval(21_602))
        XCTAssertEqual(moved, "当前位置"); XCTAssertEqual(calls, 3)
    }

    func testDistrictCoordinatesShiftOnlyForMainlandProviderLookups() throws {
        let corrected = MacQWeatherService.mainlandCoordinate(39.908722, 116.397389)
        XCTAssertEqual(corrected.0, 39.91012548, accuracy: 0.0000001)
        XCTAssertEqual(corrected.1, 116.40363255, accuracy: 0.0000001)
        XCTAssertTrue(MacQWeatherService.isMainland(country: "中国", region: "江苏省"))
        for region in ["香港特别行政区", "澳门特别行政区", "台湾省", "Hong Kong", "Macau", "Taiwan"] {
            XCTAssertFalse(MacQWeatherService.isMainland(country: "中国", region: region))
        }
        XCTAssertFalse(MacQWeatherService.isMainland(country: "France", region: "Paris"))
        XCTAssertFalse(MacQWeatherService.isMainland(country: "中国", region: nil))
        let air = try MacQWeatherService.airRequest(host: "fixture.qweatherapi.com", key: "fixture",
                                                   latitude: corrected.0, longitude: corrected.1)
        XCTAssertEqual(air.url?.path, "/airquality/v1/current/39.91/116.40")
    }

    func testWeatherCadenceUsesLastSuccessfulQueryAndRetriesAfterLocationRecovery() {
        let settings = MacDataPreferences(city:"Fixture",latitude:nil,longitude:nil,symbols:[],automaticLocation:true)
        let first = MacWeatherRefreshSchedule.Query(settings, (32.001,119.001))
        var schedule = MacWeatherRefreshSchedule()
        XCTAssertTrue(schedule.shouldFetch(first, now:0, force:false))
        schedule.succeeded(first, now:100)
        XCTAssertFalse(schedule.shouldFetch(first, now:999, force:false))
        XCTAssertTrue(schedule.shouldFetch(first, now:1000, force:false))
        XCTAssertTrue(schedule.shouldFetch(first, now:101, force:true))
        let moved = MacWeatherRefreshSchedule.Query(settings, (32.002,119.001))
        XCTAssertTrue(schedule.shouldFetch(moved, now:101, force:false))
        var changed = settings
        changed.weatherProvider = "qweather"
        changed.qweatherHost = "fixture.qweatherapi.com"
        XCTAssertTrue(schedule.shouldFetch(.init(changed, (32.001,119.001)), now:101, force:false))
        schedule.failed()
        XCTAssertTrue(schedule.shouldFetch(first, now:102, force:false), "Recover at the same point without waiting 15 minutes")
        schedule.succeeded(first, now:102)
        XCTAssertFalse(schedule.shouldFetch(first, now:103, force:false))
    }
}
