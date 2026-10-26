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
}
