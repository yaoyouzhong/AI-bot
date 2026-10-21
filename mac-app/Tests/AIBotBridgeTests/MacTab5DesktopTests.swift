import XCTest
@testable import AIBotBridge

final class MacTab5DesktopTests: XCTestCase {
    func testMissingComposerAndRouteAreReportedSeparatelyWithoutRelaxingIdentity() {
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "", routeConfirmed: false), .identifierAndRoute)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "", routeConfirmed: true), .identifier)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "prompt-textarea", routeConfirmed: false), .route)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "prompt-textarea", routeConfirmed: true), .confirmed)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "search", routeConfirmed: true), .identifier)
    }
    func testNavigationRequiresCurrentExactLinkAndTrustedAppShell() {
        let task = UUID().uuidString.lowercased()
        let root = URL(fileURLWithPath: "/Applications/ChatGPT.app/Contents/Resources")
        let address = "file:///Applications/ChatGPT.app/Contents/Resources/webview/index.html#/threads/" + task
        XCTAssertTrue(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: "page", address: address, task: task, resourceRoot: root))
        for current in [nil, "false", "true", "step"] {
            XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: current, address: address, task: task, resourceRoot: root))
        }
        XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXButton", current: "page", address: address, task: task, resourceRoot: root))
        XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: "page", address: address, task: UUID().uuidString, resourceRoot: root))
        XCTAssertTrue(MacTab5DesktopAX.trustedShell(address, resourceRoot: root))
        for untrusted in ["https://example.com/threads/" + task, "file:///tmp/threads/" + task,
                          "file:///Applications/ChatGPT.app/Contents/Resources/../private/index.html", "codex://threads/" + task] {
            XCTAssertFalse(MacTab5DesktopAX.trustedShell(untrusted, resourceRoot: root))
        }
    }
    func testNavigationCanConfirmWithoutReadingOrAuthorizingAnEditor() throws {
        let fixture = Fixture()
        var backend = fixture.backend, reads = 0
        backend.navigation = { $0 == fixture.task }
        backend.read = { _ in reads += 1; return nil }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        let result = desktop.handle(request, device: "A")
        XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["foreground"] as? Bool, true)
        XCTAssertEqual(result.body["taskId"] as? String, fixture.task)
        XCTAssertNil(result.body["draftEmpty"]); XCTAssertEqual(reads, 0)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.navigationConfirmed.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200); XCTAssertEqual(fixture.opens, 1)
        let staged = desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "test"], device: "A")
        XCTAssertEqual(staged.status, 503); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testNavigationFailureCannotFallBackToAnUnrelatedEditor() throws {
        let fixture = Fixture()
        var backend = fixture.backend
        backend.navigation = { _ in false }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        let result = desktop.handle(["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString], device: "A")
        XCTAssertEqual(result.status, 409); XCTAssertNil(result.body["foreground"])
        XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testPackagedDesktopOriginAndExactLiveRoute() {
        let task = UUID().uuidString.lowercased()
        XCTAssertTrue(MacTab5DesktopAX.identifies("app://-/threads/" + task, task: task))
        XCTAssertTrue(MacTab5DesktopAX.trustedShell("app://-/index.html", resourceRoot: nil))
        // initialRoute is only the startup hint, never evidence of current selection.
        XCTAssertFalse(MacTab5DesktopAX.identifies("app://-/index.html?initialRoute=/threads/" + task, task: task))
        for origin in ["app://fs", "app://other", "app://user@-", "app://-:80"] {
            XCTAssertFalse(MacTab5DesktopAX.identifies(origin + "/threads/" + task, task: task))
            XCTAssertFalse(MacTab5DesktopAX.trustedShell(origin + "/index.html", resourceRoot: nil))
        }
        XCTAssertFalse(MacTab5DesktopAX.identifies("app://-/threads/" + task + "/settings", task: task))
    }
    func testCurrentLocalDesktopRouteRequiresExactTaskAndTrustedOrigin() {
        let task = UUID().uuidString.lowercased()
        for route in ["app://-/local/", "app://-/index.html#/local/"] {
            XCTAssertTrue(MacTab5DesktopAX.identifies(route + task, task: task))
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + UUID().uuidString, task: task))
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + task + "/settings", task: task))
        }
        for route in ["app://fs/local/", "app://user@-/local/", "app://-:80/local/", "app://other/local/",
                      "codex://local/", "https://external.example/local/", "app://-/remote/",
                      "app://-/nested/local/", "app://-/index.html?initialRoute=/local/"] {
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + task, task: task))
        }
        XCTAssertTrue(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: "page", address: "app://-/local/" + task, task: task, resourceRoot: nil))
        XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: nil, address: "app://-/local/" + task, task: task, resourceRoot: nil))
    }
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
        XCTAssertFalse(MacTab5DesktopAX.identifies("codex://untrusted/threads/" + id, task: id))
    }
    func testFileRouteUsesActualVerifiedAppResourceRoot() {
        let id = UUID().uuidString.lowercased(), root = URL(fileURLWithPath: "/Applications/ChatGPT.app/Contents/Resources")
        let route = "file:///Applications/ChatGPT.app/Contents/Resources/app/index.html#/threads/" + id
        XCTAssertFalse(MacTab5DesktopAX.identifies(route, task: id))
        XCTAssertTrue(MacTab5DesktopAX.identifies(route, task: id, resourceRoot: root))
        XCTAssertFalse(MacTab5DesktopAX.identifies(route, task: UUID().uuidString, resourceRoot: root))
        XCTAssertFalse(MacTab5DesktopAX.identifies(route, task: id, resourceRoot: URL(fileURLWithPath: "/Applications/Codex.app/Contents/Resources")))
        for route in ["file:///Applications/ChatGPT.app/Contents/Resources-Other/index.html#/threads/",
                      "file:///Applications/ChatGPT.app/Contents/Resources/../private/index.html#/threads/",
                      "file://untrusted/Applications/ChatGPT.app/Contents/Resources/app/index.html#/threads/"] {
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + id, task: id, resourceRoot: root))
        }
    }
    func testUnconfirmedOpenReportsBoundedReasonWithoutAcceptingComposer() throws {
        let fixture = Fixture()
        var backend = fixture.backend
        backend.read = { _ in nil }; backend.diagnostic = { .identifier }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.identifier.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(fixture.opens, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testMissingPermissionDoesNotNavigateAndOldReceiptDoesNotRetryAfterGrant() throws {
        let fixture = Fixture()
        var allowed = false, backend = fixture.backend
        backend.preflight = { allowed ? nil : .permission }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.permission.rawValue)
        var request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        let rejected = desktop.handle(request, device: "A")
        XCTAssertEqual(rejected.status, 409); XCTAssertNil(rejected.body["foreground"])
        XCTAssertEqual(fixture.opens, 0); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
        allowed = true
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(fixture.opens, 0)
        request["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(fixture.opens, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
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
    func testReadOnlyConnectionRejectsDesktopSubmissionBeforeChangingComposer() throws {
        let fixture = Fixture(); fixture.value = "保留电脑输入"
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 4, count: 32) }), enabled: { true }, sendEnabled: { true }, canMutate: { false }, call: fixture.call)
        codex.refresh()
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: fixture.backend)
        let response = desktop.handle(["op": "submit-draft", "taskId": fixture.task, "requestId": UUID().uuidString], device: "fixture")
        XCTAssertEqual(response.status, 403); XCTAssertEqual(fixture.value, "保留电脑输入")
        XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0); XCTAssertEqual(fixture.opens, 0)
    }
}
