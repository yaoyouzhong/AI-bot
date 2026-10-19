import XCTest
@testable import AIBotBridge

final class MacTab5DesktopTests: XCTestCase {
    private final class Fixture {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        var value = "", writes = 0, opens = 0, sends = 0, foreground = true
        func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/fixture", "name": "测试会话"]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": "idle"], "turns": []]] }
            if method == "turn/start" { sends += 1; return ["turn": ["id": turn]] }; return [:]
        }
        var backend: MacTab5DesktopBackend { .init(open: { _ in self.opens += 1; return self.foreground }, read: { _ in self.foreground ? self.value : nil }, write: { _, expected, next in guard self.foreground, self.value == expected else { return false }; self.value = next; self.writes += 1; return true }) }
    }
    private func model(_ fixture: Fixture) throws -> MacTab5Codex {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString); addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 4, count: 32) }), enabled: { true }, sendEnabled: { true }, call: fixture.call); codex.refresh(); return codex
    }
    func testExactDesktopRouteAndOriginRequired() {
        let id = UUID().uuidString.lowercased()
        XCTAssertTrue(MacTab5DesktopAX.identifies("codex://threads/" + id, task: id))
        XCTAssertTrue(MacTab5DesktopAX.identifies("http://localhost:1234/threads/" + id, task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("https://untrusted.example/threads/" + id, task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("codex://threads/" + id + "-other", task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("codex://threads/" + UUID().uuidString, task: id))
    }
    func testStageAndSubmitExactlyOnceWithChangedComposerProtection() throws {
        let fixture = Fixture(), codex = try model(fixture), desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: fixture.backend)
        let stageID = UUID().uuidString, stage: [String: Any] = ["op": "stage-draft", "taskId": fixture.task, "requestId": stageID, "text": "确认执行"]
        XCTAssertEqual(desktop.handle(stage, device: "A").status, 200); XCTAssertEqual(desktop.handle(stage, device: "A").status, 200); XCTAssertEqual(fixture.writes, 1)
        let submit: [String: Any] = ["op": "submit-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "draftRequestId": stageID]
        fixture.value = "电脑另有修改"; XCTAssertEqual(desktop.handle(submit, device: "A").status, 409); XCTAssertEqual(fixture.sends, 0)
        fixture.value = "确认执行"
        var fresh = submit; fresh["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(fresh, device: "A").status, 200); XCTAssertEqual(desktop.handle(fresh, device: "A").status, 200); XCTAssertEqual(fixture.sends, 1)
    }
    func testNoForegroundMeansNoTypingOrFalseSuccess() throws {
        let fixture = Fixture(); fixture.foreground = false
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: fixture.backend)
        XCTAssertEqual(desktop.handle(["op": "open-recent", "requestId": UUID().uuidString], device: "A").status, 409)
        XCTAssertEqual(desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "hello"], device: "A").status, 503); XCTAssertEqual(fixture.writes, 0)
    }
}
