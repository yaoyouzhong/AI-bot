import XCTest
@testable import AIBotBridge

final class MacScreenLockObservationTests: XCTestCase {
    func testSessionChangesInvalidateUnlockedEvidenceButRetainObservedLock() {
        let observation = MacScreenLockObservation()
        XCTAssertNil(observation.value(onConsole: true))
        observation.receive(.locked, onConsole: true)
        XCTAssertEqual(observation.value(onConsole: true), true)
        observation.receive(.unlocked, onConsole: true)
        XCTAssertEqual(observation.value(onConsole: true), false)
        observation.receive(.invalidate, onConsole: true)
        XCTAssertNil(observation.value(onConsole: true))
        observation.receive(.locked, onConsole: false)
        XCTAssertNil(observation.value(onConsole: true))
        observation.receive(.locked, onConsole: true)
        XCTAssertNil(observation.value(onConsole: false))
        XCTAssertEqual(observation.value(onConsole: true), true)
        observation.receive(.invalidate, onConsole: false)
        XCTAssertEqual(observation.value(onConsole: true), true)
        observation.receive(.unlocked, onConsole: true)
        XCTAssertEqual(observation.value(onConsole: true), false)
    }
    func testDisplayObservationCannotEnableCredentialPreparation() {
        let suite = "MacScreenLockObservationTests-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        defer { defaults.removePersistentDomain(forName: suite) }
        defaults.set(true, forKey: MacTab5Computer.enabledKey)
        let computer = MacTab5Computer(defaults: defaults, backend: .init(read: { ["locked": true] }, perform: { _, _, _ in true }))
        let state = computer.handle(["op": "status"])
        XCTAssertEqual(state.body["locked"] as? Bool, true)
        XCTAssertEqual(state.body["oneClickUnlock"] as? Bool, false)
        let preparation = computer.handle(["op": "lock-screen"])
        XCTAssertTrue(preparation.body["locked"] is NSNull)
        XCTAssertTrue(preparation.body["displayState"] is NSNull)
        XCTAssertTrue(preparation.body["lockScreenCover"] is NSNull)
    }
}
