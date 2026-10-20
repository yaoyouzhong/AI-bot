import XCTest
@testable import AIBotBridge

final class MacTab5CodexTests: XCTestCase {
    private final class API {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        var calls: [(String, [String: Any])] = [], failSend = false, running = false
        var text = "已完成公开回复"
        func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
            calls.append((method, params))
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/private/projects/sample", "name": "测试会话", "updatedAt": 42, "status": ["type": "idle"]]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": running ? "active" : "idle"], "turns": [["id": turn, "status": running ? "inProgress" : "completed", "items": [["type": "agentMessage", "text": text], ["type": "reasoning", "text": "PRIVATE_THINKING"], ["type": "commandExecution", "aggregatedOutput": "PRIVATE_TOOL"]]]]]] }
            if method == "turn/start" || method == "turn/steer" { if failSend { throw MacTab5CodexFailure.timeout }; return ["turn": ["id": turn]] }
            return [:]
        }
    }
    private func store() throws -> MacTab5PrivateStore {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return MacTab5PrivateStore(root: root, key: { Data(repeating: 7, count: 32) })
    }
    func testOptInAndPublicRepliesOnly() throws {
        let api = API(); var enabled = false
        let codex = MacTab5Codex(store: try store(), enabled: { enabled }, sendEnabled: { false }, call: api.call)
        codex.refresh(); XCTAssertTrue(api.calls.isEmpty); XCTAssertNil(codex.snapshot())
        enabled = true; codex.refresh()
        let page = codex.handle(["taskId": api.task, "op": "read"], device: "device")
        XCTAssertEqual(page.status, 200); XCTAssertEqual(page.body["text"] as? String, api.text)
        XCTAssertFalse(String(decoding: try MacTab5Protocol.json(codex.snapshot()!), as: UTF8.self).contains("/private/"))
        XCTAssertEqual(codex.handle(["taskId": api.task, "op": "send", "message": "hello", "requestId": UUID().uuidString], device: "device").status, 403)
    }
    func testExactlyOnceReceiptSurvivesRestartAndRejectsChangedPayload() throws {
        let api = API(), storage = try store()
        let codex = MacTab5Codex(store: storage, enabled: { true }, sendEnabled: { true }, call: api.call); codex.refresh()
        var request: [String: Any] = ["op": "send", "taskId": api.task, "requestId": UUID().uuidString, "message": "hello"]
        XCTAssertEqual(codex.handle(request, device: "device").status, 202)
        let restarted = MacTab5Codex(store: storage, enabled: { true }, sendEnabled: { true }, call: api.call); restarted.refresh()
        XCTAssertEqual(restarted.handle(request, device: "device").status, 202)
        request["message"] = "changed"; XCTAssertEqual(restarted.handle(request, device: "device").status, 409)
        XCTAssertEqual(api.calls.filter { $0.0 == "turn/start" }.count, 1)
        let sent = api.calls.first { $0.0 == "turn/start" }!.1
        XCTAssertNil(sent["model"]); XCTAssertNil(sent["approvalPolicy"]); XCTAssertNil(sent["sandboxPolicy"])
    }
    func testTimeoutNeverResendsAndDraftRemains() throws {
        let api = API(); api.failSend = true
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { true }, call: api.call); codex.refresh()
        let id = UUID().uuidString, request: [String: Any] = ["op": "send", "taskId": api.task, "requestId": id, "message": "保留草稿"]
        XCTAssertEqual(codex.handle(request, device: "device").status, 504); XCTAssertEqual(codex.handle(request, device: "device").status, 504)
        XCTAssertEqual(api.calls.filter { $0.0 == "turn/start" }.count, 1)
        XCTAssertEqual(codex.handle(["op": "draft-load", "taskId": api.task, "requestId": UUID().uuidString], device: "device").status, 200)
        let draft = codex.snapshot()!["draftView"] as! [String: Any]; XCTAssertEqual(draft["text"] as? String, "保留草稿"); XCTAssertEqual(draft["pendingRequest"] as? String, id.lowercased())
    }
    func testActiveTurnBindingAndReadReceiptCannotCrossDevice() throws {
        let api = API(); let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { true }, call: api.call); codex.refresh()
        let page = codex.handle(["op": "read", "taskId": api.task], device: "A")
        let receipt = (page.body["replyView"] as! [String: Any])["readReceipt"] as! String
        let ack: [String: Any] = ["op": "read-ack", "taskId": api.task, "turnId": api.turn, "requestId": receipt]
        XCTAssertEqual(codex.handle(ack, device: "B").status, 409); XCTAssertEqual(codex.handle(ack, device: "A").status, 200)
        api.running = true
        XCTAssertEqual(codex.handle(ack, device: "A").status, 409)
        XCTAssertEqual(codex.handle(["op": "send", "taskId": api.task, "message": "hello", "requestId": UUID().uuidString], device: "A").status, 409)
        XCTAssertEqual(codex.handle(["op": "steer", "taskId": api.task, "message": "hello", "expectedTurn": UUID().uuidString, "requestId": UUID().uuidString], device: "A").status, 409)
    }
    func testEncryptedStorageAndUnicodePaging() throws {
        let storage = try store(); try storage.write("draft-A", Data("机密草稿".utf8))
        XCTAssertEqual(try storage.read("draft-A"), Data("机密草稿".utf8)); XCTAssertNil(try storage.read("draft-B"))
        let text = String(repeating: "中文🙂e\u{301}", count: 1500), pages = MacTab5Codex.pages(text)
        XCTAssertGreaterThan(pages.count, 1); XCTAssertEqual(pages.joined(), text); XCTAssertTrue(pages.allSatisfy { $0.utf8.count <= 3500 })
    }
    func testCachedCatalogRemainsVisibleButStaleActionsCannotSend() throws {
        let api = API(); var now: TimeInterval = 0
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { true }, clock: { now }, call: api.call); codex.refresh()
        XCTAssertTrue(codex.contains(api.task)); now = 31
        XCTAssertFalse(codex.contains(api.task)); XCTAssertFalse((codex.snapshot()?["tasks"] as? [[String: Any]] ?? []).isEmpty)
        let request: [String: Any] = ["op": "send", "taskId": api.task, "requestId": UUID().uuidString, "message": "do not send while stale"]
        XCTAssertEqual(codex.handle(request, device: "A").status, 503); XCTAssertFalse(api.calls.contains { $0.0 == "turn/start" })
        codex.refresh(); XCTAssertEqual(codex.handle(request, device: "A").status, 202)
    }
    func testAppServerStatesMatchExistingDeviceVocabulary() {
        XCTAssertEqual(MacTab5Codex.taskState(["type": "active", "activeFlags": [String]()]), "working")
        for flag in ["waitingOnApproval", "waitingOnUserInput"] {
            XCTAssertEqual(MacTab5Codex.taskState(["type": "active", "activeFlags": [flag]]), "waiting")
        }
        XCTAssertEqual(MacTab5Codex.taskState(["type": "idle"]), "idle")
        for state in ["notLoaded", "systemError", "futureStatus", "active"] {
            XCTAssertEqual(MacTab5Codex.taskState(["type": state]), "unknown")
        }
        XCTAssertEqual(MacTab5Codex.taskState(["type": "active", "activeFlags": ["futureFlag"]]), "unknown")
    }
    func testHomeKeepsWorkingTaskAndReaderAcrossBoundedTransportSnapshots() throws {
        let api = API(), working = UUID().uuidString.lowercased()
        let entries: [[String: Any]] = (0..<20).map { index -> [String: Any] in
            let id = index == 19 ? working : index == 18 ? api.task : UUID().uuidString.lowercased()
            let status: [String: Any] = index == 19 ? ["type": "active", "activeFlags": [String]()] : ["type": "idle"]
            return ["id": id, "cwd": "/synthetic/project\(index)", "name": "Synthetic \(index)", "updatedAt": Int64(100 - index), "status": status]
        }
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { false }, call: { method, params in
            if method == "thread/list" { return ["data": entries] }
            return try api.call(method, params)
        })
        codex.refresh()
        XCTAssertEqual(codex.handle(["op": "read", "taskId": api.task], device: "fixture").status, 200)
        for limit in [2, 8, 16] {
            let snapshot = try XCTUnwrap(codex.snapshot(taskLimit: limit)), tasks = try XCTUnwrap(snapshot["tasks"] as? [[String: Any]])
            XCTAssertEqual(tasks.count, limit)
            XCTAssertTrue(tasks.contains { $0["id"] as? String == working })
            XCTAssertTrue(tasks.contains { $0["id"] as? String == api.task })
            XCTAssertEqual((snapshot["overviewTask"] as? [String: Any])?["id"] as? String, working)
            XCTAssertEqual((snapshot["overviewTask"] as? [String: Any])?["state"] as? String, "working")
        }
        let single = try XCTUnwrap(codex.snapshot(taskLimit: 1))
        XCTAssertEqual((single["overviewTask"] as? [String: Any])?["id"] as? String, api.task)
        XCTAssertEqual((single["tasks"] as? [[String: Any]])?.count, 1)
    }
    func testConnectionFailureRetainsPublicDataAndSuppressesControlsAndPrivateDetails() throws {
        let api = API(); var failed = false
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { false }, call: { method, params in
            if failed { throw MacTab5CodexFailure.proxyUnavailable }
            return try api.call(method, params)
        })
        codex.refresh(); _ = codex.handle(["op": "read", "taskId": api.task], device: "fixture")
        failed = true; codex.refresh()
        XCTAssertEqual((codex.snapshot()?["responses"] as? [String: String])?[api.task], api.text)
        XCTAssertTrue(codex.snapshot()?["control"] is NSNull)
        XCTAssertTrue(codex.summary.contains("初始化前退出"))
        let privateError = NSError(domain: "PRIVATE_TOKEN", code: 1, userInfo: [NSLocalizedDescriptionKey: "PRIVATE_PROMPT /private/path"])
        let summary = MacTab5Codex.connectionFailure(privateError)
        XCTAssertFalse(summary.contains("PRIVATE")); XCTAssertFalse(summary.contains("/private/"))
        failed = false; codex.refresh(); XCTAssertTrue(codex.summary.contains("已连接"))
    }
}
