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
        var state: [String: Any] {
            ["id": task, "cwd": "/synthetic/project", "threadRuntimeStatus": ["type": runtime],
             "requests": waiting ? [["type": "permission-request"]] : [],
             "turns": [["turnId": turn, "status": turnStatus, "turnStartedAtMs": 1000,
                        "items": [["type": "reasoning", "text": "PRIVATE_REASONING"]]]]]
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
}
