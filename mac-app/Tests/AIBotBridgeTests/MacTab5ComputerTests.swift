import XCTest
@testable import AIBotBridge

final class MacTab5ComputerTests: XCTestCase {
    private func preferences() -> UserDefaults { UserDefaults(suiteName: "AI-bot-test-" + UUID().uuidString)! }
    func testExplicitEnableAndVolumeReadback() {
        let defaults = preferences(); var volume = 20, calls = 0
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { ["volume": volume, "volumeAvailable": true, "muted": false] }, perform: { _, root, _ in calls += 1; volume = Int(MacTab5Protocol.integer(root["volume"])!); return true }))
        let request: [String: Any] = ["kind": "computer-control", "op": "volume", "volume": 75, "requestId": UUID().uuidString]
        XCTAssertEqual(control.handle(request).status, 403); XCTAssertEqual(calls, 0)
        defaults.set(true, forKey: MacTab5Computer.enabledKey)
        XCTAssertEqual(control.handle(request).status, 200); XCTAssertEqual(volume, 75); XCTAssertEqual(calls, 1)
    }
    func testDuplicateCommandsAndChangedPayloadCannotRepeatSideEffects() {
        let defaults = preferences(); defaults.set(true, forKey: MacTab5Computer.enabledKey); var calls = 0
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { ["mediaAvailable": true] }, perform: { _, _, _ in calls += 1; return true }))
        var request: [String: Any] = ["op": "media", "action": "next", "requestId": UUID().uuidString]
        XCTAssertEqual(control.handle(request).status, 200); XCTAssertEqual(control.handle(request).status, 200); XCTAssertEqual(calls, 1)
        request["action"] = "previous"; XCTAssertEqual(control.handle(request).status, 409); XCTAssertEqual(calls, 1)
    }
    func testUnconfirmedWriteIsNotOptimisticallySavedOrRetried() {
        let defaults = preferences(); defaults.set(true, forKey: MacTab5Computer.enabledKey); var calls = 0
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { ["volume": 20] }, perform: { _, _, _ in calls += 1; return true }))
        let request: [String: Any] = ["op": "volume", "volume": 90, "requestId": UUID().uuidString]
        XCTAssertEqual(control.handle(request).status, 503); XCTAssertEqual(control.handle(request).status, 503); XCTAssertEqual(calls, 1)
    }
    func testInvalidParametersAndArbitraryPathsNeverReachBackend() {
        let defaults = preferences(); defaults.set(true, forKey: MacTab5Computer.enabledKey); var calls = 0
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, _ in calls += 1; return true }))
        for request: [String: Any] in [["op": "launch", "path": "/bin/sh", "requestId": UUID().uuidString], ["op": "volume", "volume": true], ["op": "mute", "muted": 1], ["op": "media", "action": "shell"]] {
            XCTAssertEqual(control.handle(request).status, 400)
        }
        XCTAssertEqual(calls, 0)
    }
    func testLaunchUsesOnlyConfiguredIdentityAndNeverTransmitsPath() throws {
        let defaults = preferences(); defaults.set(true, forKey: MacTab5Computer.enabledKey)
        let folder = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true); defer { try? FileManager.default.removeItem(at: folder) }
        var selected: String?
        let control = MacTab5Computer(defaults: defaults, backend: .init(read: { [:] }, perform: { _, _, launcher in selected = launcher?.path; return true }))
        let entry = MacTab5Launcher(id: UUID().uuidString, name: "测试入口", path: folder.path)
        try control.launchers.save([entry])
        let status = control.handle(["op": "status"])
        let exposed = status.body["launchers"] as! [[String: Any]]; XCTAssertNil(exposed[0]["path"])
        XCTAssertEqual(control.handle(["op": "launch", "launcherId": entry.id, "requestId": UUID().uuidString]).status, 200); XCTAssertEqual(selected, folder.path)
        XCTAssertEqual(control.handle(["op": "launch", "launcherId": UUID().uuidString, "requestId": UUID().uuidString]).status, 404)
    }
}
