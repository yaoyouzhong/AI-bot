import XCTest
@testable import AIBotBridge

final class MacFeaturePreferencesTests: XCTestCase {
    private func preferences() -> (UserDefaults, String) {
        let name = "AI-bot-preferences-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: name)!
        // NSRegistrationDomain is shared even by distinct named suites.
        let registration = defaults.volatileDomain(forName: UserDefaults.registrationDomain)
        defaults.setVolatileDomain([:], forName: UserDefaults.registrationDomain)
        addTeardownBlock { defaults.setVolatileDomain(registration, forName: UserDefaults.registrationDomain) }
        return (defaults, name)
    }
    func testFreshCollectionDefaultsNeverEnableActions() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        MacFeaturePreferences.register(defaults)
        for key in [MacMusicService.enabledKey, MacTab5Codex.enabledKey, MacTab5BLE.enabledKey, "weather_automatic_location"] {
            XCTAssertTrue(defaults.bool(forKey: key), key)
        }
        for key in [MacMusicService.controlKey, MacTab5Computer.enabledKey, MacTab5Codex.sendKey, MacTab5Desktop.enabledKey, MacTab5Voice.enabledKey] {
            XCTAssertFalse(defaults.bool(forKey: key), key)
        }
        var calls = 0
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in calls += 1; return true }))
        XCTAssertEqual(control.handle(["op": "media", "action": "play-pause", "requestId": UUID().uuidString]).status, 403)
        XCTAssertEqual(calls, 0)
    }
    func testExplicitOptOutSurvivesReregistration() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        MacFeaturePreferences.register(defaults)
        for key in [MacMusicService.enabledKey, MacTab5Codex.enabledKey, MacTab5BLE.enabledKey, "weather_automatic_location"] { defaults.set(false, forKey: key) }
        let reopened = UserDefaults(suiteName: name)!
        MacFeaturePreferences.register(reopened)
        for key in [MacMusicService.enabledKey, MacTab5Codex.enabledKey, MacTab5BLE.enabledKey, "weather_automatic_location"] { XCTAssertFalse(reopened.bool(forKey: key), key) }
    }
    func testExistingMusicPermissionMigratesOnceAndControlOptOutPersists() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        defaults.set(true, forKey: MacMusicService.enabledKey)
        MacFeaturePreferences.register(defaults)
        XCTAssertTrue(MacFeaturePreferences.musicControlEnabled(defaults))
        defaults.set(false, forKey: MacMusicService.controlKey)
        MacFeaturePreferences.register(defaults)
        XCTAssertFalse(MacFeaturePreferences.musicControlEnabled(defaults))
        defaults.set(true, forKey: MacMusicService.controlKey)
        defaults.set(false, forKey: MacMusicService.enabledKey)
        XCTAssertFalse(MacFeaturePreferences.musicControlEnabled(defaults))
    }
    func testManualWeatherAndExistingControlChoicesArePreserved() {
        let (defaults, name) = preferences(); defer { defaults.removePersistentDomain(forName: name) }
        defaults.set("Test city", forKey: "weather_city")
        defaults.set(true, forKey: MacTab5Computer.enabledKey)
        defaults.set(true, forKey: MacTab5Voice.enabledKey)
        MacFeaturePreferences.register(defaults)
        XCTAssertFalse(defaults.bool(forKey: "weather_automatic_location"))
        XCTAssertTrue(defaults.bool(forKey: MacTab5Computer.enabledKey))
        XCTAssertTrue(defaults.bool(forKey: MacTab5Voice.enabledKey))
    }
}

final class MacPrivateKeyCacheTests: XCTestCase {
    func testConcurrentReceiptsLoadKeyOnlyOnce() {
        var reads = 0
        let secret = Data(repeating: 7, count: 32)
        let cache = MacPrivateKeyCache { reads += 1; return secret }
        DispatchQueue.concurrentPerform(iterations: 32) { _ in
            do { let value = try cache.read(); XCTAssertEqual(value, secret) }
            catch { XCTFail("Cached key read failed: \(error)") }
        }
        XCTAssertEqual(reads, 1)
    }
    func testDeniedKeyIsNotRetriedByEveryReceipt() {
        enum Failure: Error { case denied }
        var reads = 0
        let cache = MacPrivateKeyCache { reads += 1; throw Failure.denied }
        for _ in 0..<32 { XCTAssertThrowsError(try cache.read()) }
        XCTAssertEqual(reads, 1)
    }
}
