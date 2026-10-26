import XCTest
@testable import AIBotBridge

final class MacCodexDesktopIPCTests: XCTestCase {
    private final class Wire: MacCodexDesktopIPC.Wire {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        let client = UUID().uuidString.lowercased(), owner = UUID().uuidString.lowercased()
        var runtime = "idle", turnStatus = "completed", waiting = false, version = 11
        var rejectOwner = false, failAfterDispatch = false, wrongAckOwner = false
        var writes: [[String: Any]] = [], replies: [[String: Any]] = []
        var closed = false
        var items: [[String: Any]] = [["type": "reasoning", "text": "PRIVATE_REASONING"]]
        var state: [String: Any] {
            ["id": task, "cwd": "/synthetic/project", "threadRuntimeStatus": ["type": runtime],
             "requests": waiting ? [["type": "permission-request"]] : [],
             "turns": [["turnId": turn, "status": turnStatus, "turnStartedAtMs": 1000,
                        "items": items]]]
        }
        func write(_ object: [String: Any], until: TimeInterval) throws {
            writes.append(object)
            let method = object["method"] as? String ?? ""
            var response: [String: Any] = ["type": "response", "requestId": object["requestId"] ?? "",
                                           "resultType": "success", "handledByClientId": owner, "method": method]
            if method == "initialize" {
                response["result"] = ["clientId": client]; replies.append(response)
            } else if method == "thread-owner-discovery" {
                if rejectOwner { response["resultType"] = "error"; response["error"] = "no-client-found" }
                response["result"] = ["supportsUntrustedAppInput": true]; replies.append(response)
            } else if method == "thread-stream-following-changed",
                      (object["params"] as? [String: Any])?["following"] as? Bool == true {
                replies.append(["type": "client-discovery-request", "requestId": UUID().uuidString])
                replies.append(["type": "broadcast", "method": "thread-stream-state-changed",
                                "sourceClientId": owner, "version": version,
                                "params": ["hostId": "local", "conversationId": task,
                                           "change": ["type": "snapshot", "conversationState": state]]])
            } else if method.hasPrefix("thread-follower-") {
                if failAfterDispatch { throw MacCodexDesktopError.timeout }
                response["handledByClientId"] = wrongAckOwner ? UUID().uuidString.lowercased() : owner
                response["result"] = method == "thread-follower-interrupt-turn" ?
                    ["interruptedTurnId": turn] :
                    ["result": ["turn": ["id": turn]]]
                replies.append(response)
            }
        }
        func read(until: TimeInterval) throws -> [String: Any] {
            guard !replies.isEmpty else { throw MacCodexDesktopError.timeout }
            return replies.removeFirst()
        }
        func close() { closed = true }
        var mutations: [[String: Any]] { writes.filter { ($0["method"] as? String ?? "").hasPrefix("thread-follower-") } }
    }

    func testOwnerReadSubscribesAndUnsubscribesWithoutExportingPrivateSnapshot() throws {
        let wire = Wire(), client = MacCodexDesktopIPC(connect: { wire })
        let value = try client.read(wire.task)
        XCTAssertTrue(client.available); XCTAssertTrue(wire.closed)
        XCTAssertEqual(value.taskState, "idle"); XCTAssertTrue(value.canSend)
        XCTAssertTrue(wire.mutations.isEmpty)
        XCTAssertEqual((wire.writes.last?["params"] as? [String: Any])?["following"] as? Bool, false)
        let discovery = try XCTUnwrap(wire.writes.first { $0["type"] as? String == "client-discovery-response" })
        XCTAssertEqual((discovery["response"] as? [String: Any])?["canHandle"] as? Bool, false)
        let projection = String(decoding: try MacTab5Protocol.json(value.thread), as: UTF8.self)
        XCTAssertFalse(projection.contains("PRIVATE")); XCTAssertFalse(projection.contains("/synthetic"))
        client.reset(); XCTAssertFalse(client.available)
    }

    func testIncompatibleSnapshotAndMissingOwnerFailBeforeMutation() {
        for wrongVersion in [true, false] {
            let wire = Wire(); wire.version = wrongVersion ? 12 : 11; wire.rejectOwner = !wrongVersion
            let client = MacCodexDesktopIPC(connect: { wire })
            XCTAssertThrowsError(try client.execute(task: wire.task, operation: "send", expected: "",
                                                    request: UUID().uuidString, inputs: [["type": "text", "text": "fixture"]]))
            XCTAssertTrue(wire.mutations.isEmpty); XCTAssertTrue(wire.closed)
        }
    }

    func testStartInheritsSettingsAndCarriesStableUserMessageId() throws {
        let wire = Wire(), client = MacCodexDesktopIPC(connect: { wire }), request = UUID().uuidString.lowercased()
        let result = try client.execute(task: wire.task, operation: "send", expected: "", request: request,
                                        inputs: [["type": "text", "text": "fixture"]])
        XCTAssertEqual(result["turnId"] as? String, wire.turn)
        let sent = try XCTUnwrap(wire.mutations.first)
        XCTAssertEqual(wire.mutations.count, 1); XCTAssertEqual(sent["targetClientId"] as? String, wire.owner)
        XCTAssertEqual(sent["version"] as? Int, 2)
        let start = try XCTUnwrap((sent["params"] as? [String: Any])?["turnStart"] as? [String: Any])
        XCTAssertEqual((start["context"] as? [String: Any])?["inheritThreadSettings"] as? Bool, true)
        let body = try XCTUnwrap(start["request"] as? [String: Any])
        XCTAssertEqual(body["clientUserMessageId"] as? String, request)
        for key in ["model", "approvalPolicy", "sandboxPolicy", "permissions", "cwd"] { XCTAssertNil(body[key]) }
        XCTAssertFalse(wire.writes.contains { $0["method"] as? String == "thread/resume" })
    }

    func testInterruptBindsExpectedTurnAndAllowsStoppingWaitingTask() throws {
        let wire = Wire(); wire.runtime = "active"; wire.turnStatus = "inProgress"; wire.waiting = true
        let client = MacCodexDesktopIPC(connect: { wire })
        XCTAssertThrowsError(try client.execute(task: wire.task, operation: "interrupt", expected: UUID().uuidString,
                                                request: UUID().uuidString, inputs: []))
        XCTAssertTrue(wire.mutations.isEmpty)
        _ = try client.execute(task: wire.task, operation: "interrupt", expected: wire.turn,
                               request: UUID().uuidString, inputs: [])
        let sent = try XCTUnwrap(wire.mutations.first), params = sent["params"] as? [String: Any]
        XCTAssertEqual(sent["version"] as? Int, 4)
        XCTAssertEqual(params?["expectedTurnId"] as? String, wire.turn)
        XCTAssertEqual(params?["mode"] as? String, "user-stop")
        XCTAssertFalse(wire.writes.contains { ($0["method"] as? String ?? "").contains("approval") })
    }

    func testLegacySteerCannotSilentlyTargetOwnersNextTurn() {
        let wire = Wire(); wire.runtime = "active"; wire.turnStatus = "inProgress"
        let client = MacCodexDesktopIPC(connect: { wire })
        XCTAssertFalse(MacCodexDesktopIPC.supportsExpectedTurnSteer)
        XCTAssertThrowsError(try client.execute(task: wire.task, operation: "steer", expected: wire.turn,
                                                request: UUID().uuidString, inputs: [["type": "text", "text": "fixture"]])) {
            guard case MacCodexDesktopError.rejected("desktop_steer_requires_expected_turn_support") = $0 else {
                return XCTFail("Steer must reject before dispatch")
            }
        }
        XCTAssertTrue(wire.mutations.isEmpty)
    }

    func testLostOrWrongOwnerAcknowledgementIsUnknownAndNeverRetried() {
        for timeout in [true, false] {
            let wire = Wire(); wire.failAfterDispatch = timeout; wire.wrongAckOwner = !timeout
            let client = MacCodexDesktopIPC(connect: { wire })
            XCTAssertThrowsError(try client.execute(task: wire.task, operation: "send", expected: "",
                                                    request: UUID().uuidString, inputs: [["type": "text", "text": "fixture"]])) {
                guard case MacCodexDesktopError.deliveryUnknown = $0 else { return XCTFail("Delivery must remain unknown") }
            }
            XCTAssertEqual(wire.mutations.count, 1); XCTAssertTrue(wire.closed)
        }
    }

    func testRevokedPermissionAfterSnapshotPreventsDispatch() {
        let wire = Wire(), client = MacCodexDesktopIPC(connect: { wire })
        XCTAssertThrowsError(try client.execute(task: wire.task, operation: "send", expected: "",
                                                request: UUID().uuidString, inputs: [["type": "text", "text": "fixture"]],
                                                authorized: { false }))
        XCTAssertTrue(wire.mutations.isEmpty)
    }

    func testCanonicalTurnIdentityAndUnknownStateAreNotGuessed() throws {
        let wire = Wire(), next = UUID().uuidString.lowercased()
        var value = wire.state
        value["turnHistory"] = ["history": ["entitiesByKey": ["new": ["turnId": next, "status": "inProgress", "turnStartedAtMs": 2000]]]]
        value["threadRuntimeStatus"] = ["type": "active"]
        XCTAssertEqual(try MacCodexDesktopIPC.parse(value, task: wire.task).turn, next)
        value["threadRuntimeStatus"] = ["type": "future-state"]
        XCTAssertThrowsError(try MacCodexDesktopIPC.parse(value, task: wire.task))
        XCTAssertThrowsError(try MacCodexDesktopIPC.parse(wire.state, task: UUID().uuidString))
    }
    func testLiveProgressMergesWithPersistedMessagesBeforeWaitingOrCompletion() throws {
        let wire = Wire(); wire.runtime = "active"; wire.turnStatus = "inProgress"
        wire.items = [["type": "agentMessage", "id": "stream", "phase": "commentary", "text": "正在检查入口", "secret": "PRIVATE"],
                      ["type": "reasoning", "text": "PRIVATE"], ["type": "commandExecution", "text": "PRIVATE"],
                      ["type": "agentMessage", "phase": "analysis", "text": "PRIVATE"], ["type": "agentMessage", "text": "PRIVATE"]]
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: MacTab5PrivateStore(root: root, key: { Data(repeating: 9, count: 32) }),
            enabled: { true }, desktop: MacCodexDesktopIPC(connect: { wire }), call: { method, _ in
                if method == "thread/list" { return ["data": [["id": wire.task, "cwd": "/synthetic", "name": "Fixture"]]] }
                return ["thread": ["id": wire.task, "turns": [["id": wire.turn, "status": "inProgress", "items": [
                    ["type": "agentMessage", "id": "earlier", "text": "先检查常用程序"],
                    ["type": "agentMessage", "id": "stream", "text": "正在"]]]]]]
            })
        codex.refresh()
        let reply = codex.handle(["op": "read", "taskId": wire.task], device: "fixture")
        XCTAssertEqual(reply.body["text"] as? String, "先检查常用程序\n\n正在检查入口")
        XCTAssertEqual(reply.body["receipt"] as? String, "运行中")
        XCTAssertFalse(String(decoding: try MacTab5Protocol.json(codex.snapshot()!), as: UTF8.self).contains("PRIVATE"))
        XCTAssertTrue(wire.mutations.isEmpty)
        wire.items[0]["text"] = "正在检查入口，已找到计算器"; codex.refresh()
        XCTAssertEqual((codex.snapshot()?["responses"] as? [String: String])?[wire.task], "先检查常用程序\n\n正在检查入口，已找到计算器")
        XCTAssertTrue(wire.mutations.isEmpty)
    }
    func testLiveTurnCanAppearBeforePersistenceWithoutReplacingPinnedHistory() throws {
        let wire = Wire(), older = UUID().uuidString.lowercased()
        wire.runtime = "active"; wire.turnStatus = "inProgress"
        wire.items = [["type": "agentMessage", "phase": "commentary", "text": "尚未落盘的进展"]]
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: MacTab5PrivateStore(root: root, key: { Data(repeating: 9, count: 32) }),
            enabled: { true }, desktop: MacCodexDesktopIPC(connect: { wire }), call: { method, _ in
                if method == "thread/list" { return ["data": [["id": wire.task, "cwd": "/synthetic", "name": "Fixture"]]] }
                return ["thread": ["id": wire.task, "turns": [["id": older, "status": "completed", "startedAt": 0,
                     "items": [["type": "agentMessage", "text": "历史回复"]]]]]]
            })
        codex.refresh()
        let current = codex.handle(["op": "read", "taskId": wire.task, "page": -1], device: "fixture")
        XCTAssertEqual(current.body["text"] as? String, "尚未落盘的进展")
        XCTAssertEqual((current.body["replyView"] as? [String: Any])?["hasPrevious"] as? Bool, true)
        XCTAssertEqual(codex.handle(["op": "read", "taskId": wire.task, "turnId": older, "page": 0], device: "fixture").body["text"] as? String, "历史回复")
        wire.items[0]["text"] = "新的进展"; codex.refresh()
        XCTAssertEqual((codex.snapshot()?["responses"] as? [String: String])?[wire.task], "历史回复")
        XCTAssertTrue(wire.mutations.isEmpty)
    }

    func testBridgeOptInReceiptAndPublicHistoryStaySeparateFromDesktopWrites() throws {
        let wire = Wire(), root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let desktop = MacCodexDesktopIPC(connect: { wire })
        let storage = MacTab5PrivateStore(root: root, key: { Data(repeating: 9, count: 32) })
        var allow = false, methods: [String] = []
        let codex = MacTab5Codex(store: storage, enabled: { true }, sendEnabled: { allow }, desktop: desktop, call: { method, _ in
            methods.append(method)
            if method == "thread/list" { return ["data": [["id": wire.task, "cwd": "/synthetic", "name": "Fixture"]]] }
            XCTFail("Public RPC must not receive a mutation"); return [:]
        })
        codex.refresh()
        let request: [String: Any] = ["op": "send", "taskId": wire.task, "requestId": UUID().uuidString, "message": "fixture"]
        XCTAssertEqual(codex.handle(request, device: "fixture").status, 403)
        XCTAssertTrue(wire.mutations.isEmpty)
        allow = true; wire.failAfterDispatch = true
        XCTAssertEqual(codex.handle(request, device: "fixture").status, 504)
        XCTAssertEqual(codex.handle(request, device: "fixture").status, 504)
        XCTAssertEqual(wire.mutations.count, 1); XCTAssertEqual(methods, ["thread/list"])
    }

    func testExpiredAvailabilityRevalidatesQuickSubmitBeforeChangingComposer() throws {
        for failure in ["", "owner", "version", "policy", "consent"] {
            let wire = Wire(), root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
            defer { try? FileManager.default.removeItem(at: root) }
            var now: TimeInterval = 100, allowed = true, sendEnabled = true, value = "", editorWrites = 0
            let ipc = MacCodexDesktopIPC(clock: { now }, connect: { wire })
            let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 9, count: 32) }),
                enabled: { true }, sendEnabled: { sendEnabled }, clock: { now }, canMutate: { allowed }, desktop: ipc,
                call: { method, _ in
                    XCTAssertEqual(method, "thread/list")
                    return ["data": [["id": wire.task, "cwd": "/synthetic", "name": "Fixture"]]]
                })
            codex.refresh(); XCTAssertTrue(codex.canMutate)
            let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: .init(
                open: { _ in XCTFail("No navigation during submission"); return false },
                read: { _ in value }, write: { _, expected, next in
                    guard value == expected else { return nil }; value = next; editorWrites += 1; return next
                }))
            let stageID = UUID().uuidString
            XCTAssertEqual(desktop.handle(["op": "stage-draft", "taskId": wire.task,
                "requestId": stageID, "text": "synthetic draft"], device: "fixture").status, 200)
            now += 16; XCTAssertFalse(codex.canMutate)
            wire.rejectOwner = failure == "owner"; wire.version = failure == "version" ? 12 : 11
            allowed = failure != "policy"; sendEnabled = failure != "consent"
            let submit: [String: Any] = ["op": "submit-draft", "taskId": wire.task,
                "requestId": UUID().uuidString, "draftRequestId": stageID]
            let result = desktop.handle(submit, device: "fixture")
            XCTAssertEqual(result.status, failure.isEmpty ? 200 : 403)
            XCTAssertEqual(value, failure.isEmpty ? "" : "synthetic draft")
            XCTAssertEqual(editorWrites, failure.isEmpty ? 2 : 1)
            XCTAssertEqual(wire.mutations.count, failure.isEmpty ? 1 : 0)
            if failure.isEmpty {
                // A cached receipt still works with no current desktop owner.
                now += 1; ipc.reset(); wire.rejectOwner = true
                XCTAssertEqual(desktop.handle(submit, device: "fixture").status, 200)
                XCTAssertEqual(editorWrites, 2); XCTAssertEqual(wire.mutations.count, 1)
            }
        }
    }

    func testExpiredAvailabilityDirectSendStillUsesFreshOwnerAndDeduplicatesUnknownDelivery() throws {
        let wire = Wire(), root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        var now: TimeInterval = 100
        let ipc = MacCodexDesktopIPC(clock: { now }, connect: { wire })
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 9, count: 32) }),
            enabled: { true }, sendEnabled: { true }, clock: { now }, desktop: ipc, call: { method, _ in
                XCTAssertEqual(method, "thread/list")
                return ["data": [["id": wire.task, "cwd": "/synthetic", "name": "Fixture"]]]
            })
        codex.refresh(); now += 16; XCTAssertFalse(codex.canMutate)
        wire.failAfterDispatch = true
        let request: [String: Any] = ["op": "send", "taskId": wire.task,
            "requestId": UUID().uuidString, "message": "synthetic draft"]
        XCTAssertEqual(codex.handle(request, device: "fixture").status, 504)
        ipc.reset(); wire.rejectOwner = true
        XCTAssertEqual(codex.handle(request, device: "fixture").status, 504)
        XCTAssertEqual(wire.mutations.count, 1)
    }
}
