import XCTest
@testable import AIBotBridge

final class MacTab5CodexTests: XCTestCase {
    func testRecencyOrderAndDeviceTimestampDoNotPromoteRecordOnlyUpdates() throws {
        let ids = (0..<4).map { _ in UUID().uuidString.lowercased() }
        // Desktop order is 1,2,3,4; record updates would produce 1,4,2,3.
        let entries: [[String: Any]] = [
            ["id": ids[0], "cwd": "/fixture", "name": "one", "updatedAt": 400, "recencyAt": 400],
            ["id": ids[3], "cwd": "/fixture", "name": "four", "updatedAt": 350, "recencyAt": 100],
            ["id": ids[1], "cwd": "/fixture", "name": "two", "updatedAt": 300, "recencyAt": 300],
            ["id": ids[2], "cwd": "/fixture", "name": "three", "updatedAt": 200, "recencyAt": 200]]
        let codex = MacTab5Codex(store: try store(), enabled: { true }, call: { _, params in
            XCTAssertEqual(params["sortKey"] as? String, "recency_at")
            if params["cursor"] == nil { return ["data": Array(entries.prefix(2)), "nextCursor": "older"] }
            return ["data": Array(entries.dropFirst(2))]
        })
        codex.refresh()
        let tasks = try XCTUnwrap(codex.snapshot()?["tasks"] as? [[String: Any]])
        XCTAssertEqual(tasks.compactMap { $0["id"] as? String }, ids)
        XCTAssertEqual(tasks.compactMap { $0["updatedAt"] as? Int64 }, [400, 300, 200, 100])
        // Existing firmware re-sorts by this wire value, independent of array order.
        XCTAssertEqual(tasks.sorted { ($0["updatedAt"] as! Int64) > ($1["updatedAt"] as! Int64) }.compactMap { $0["id"] as? String }, ids)
    }
    func testRejectedRecencySortFallsBackOnceWithoutLosingActivityTime() throws {
        let task = UUID().uuidString.lowercased(); var keys: [String] = []
        let codex = MacTab5Codex(store: try store(), enabled: { true }, call: { _, params in
            let key = params["sortKey"] as? String ?? ""; keys.append(key)
            if key == "recency_at" { throw MacTab5CodexFailure.rejected }
            return ["data": [["id": task, "cwd": "/fixture", "name": "old server", "updatedAt": 900, "recencyAt": 100]]]
        })
        codex.refresh(); codex.refresh()
        XCTAssertEqual(keys, ["recency_at", "updated_at", "updated_at"])
        XCTAssertEqual((codex.snapshot()?["tasks"] as? [[String: Any]])?.first?["updatedAt"] as? Int64, 100)
    }
    func testInvalidRecencyFallsBackAndTimeoutPreservesOrderWithoutRetry() throws {
        let ids = (0..<5).map { _ in UUID().uuidString.lowercased() }
        let invalid: [Any] = [true, -1, Int64(253_402_300_800), NSNull(), 0]
        var timeout = false, attempts = 0
        let codex = MacTab5Codex(store: try store(), enabled: { true }, call: { _, _ in
            attempts += 1; if timeout { throw MacTab5CodexFailure.timeout }
            return ["data": ids.enumerated().map { index, id in
                ["id": id, "cwd": "/fixture", "name": "entry \(index)", "updatedAt": 100 - index, "recencyAt": invalid[index]] as [String: Any]
            }]
        })
        codex.refresh()
        let before = try XCTUnwrap(codex.snapshot()?["tasks"] as? [[String: Any]])
        XCTAssertEqual(before.compactMap { $0["updatedAt"] as? Int64 }, [100, 99, 98, 97, 0])
        timeout = true; codex.refresh(); XCTAssertEqual(attempts, 2)
        XCTAssertEqual((codex.snapshot()?["tasks"] as? [[String: Any]])?.compactMap { $0["id"] as? String }, ids)
    }
    func testUnnamedChatsUseDesktopPreviewAndRetainItDuringMetadataFailure() throws {
        let unnamed = UUID().uuidString.lowercased(), named = UUID().uuidString.lowercased(), other = UUID().uuidString.lowercased()
        var explicit = "", names: [String: String]? = [unnamed: "[OpenClaw conversation info]", named: "must not override", other: "must not export"]
        var lookups = 0
        let codex = MacTab5Codex(store: try store(), enabled: { true }, catalogNames: { requested in
            lookups += 1; XCTAssertEqual(requested, [unnamed]); return names
        }, call: { _, _ in
            ["data": [["id": unnamed, "cwd": "/fixture", "name": explicit, "updatedAt": 2],
                      ["id": named, "cwd": "/fixture", "name": "explicit name", "updatedAt": 1]]]
        })
        func title() -> String? { (codex.snapshot()?["tasks"] as? [[String: Any]])?.first?["title"] as? String }
        codex.refresh(); XCTAssertEqual(title(), "[OpenClaw conversation info]")
        let tasks = try XCTUnwrap(codex.snapshot()?["tasks"] as? [[String: Any]])
        XCTAssertEqual(tasks.last?["title"] as? String, "explicit name")
        XCTAssertEqual(tasks.count, 2)
        names = nil; codex.refresh(); XCTAssertEqual(title(), "[OpenClaw conversation info]")
        explicit = "renamed chat"; codex.refresh(); XCTAssertEqual(title(), explicit); XCTAssertEqual(lookups, 2)
        explicit = "  "; names = [:]; codex.refresh(); XCTAssertEqual(title(), "Codex 会话")
    }
    func testCatalogMergesPagesNewestFirstAndDisambiguatesDuplicateDisplayNames() throws {
        let ids = (0..<5).map { _ in UUID().uuidString.lowercased() }
        let entries: [[String: Any]] = [
            ["id": ids[0], "cwd": "/fixture", "name": "same title", "updatedAt": Int64(100)],
            ["id": ids[1], "cwd": "/fixture", "name": "same title", "updatedAt": Int64(500)],
            ["id": ids[2], "cwd": "/fixture", "name": "unique", "updatedAt": Int64(300)],
            ["id": ids[3], "cwd": "/fixture", "name": "same title", "updatedAt": true],
            ["id": ids[4], "cwd": "/fixture", "name": "equal time", "updatedAt": Int64(300)]]
        let codex = MacTab5Codex(store: try store(), enabled: { true }, call: { method, params in
            XCTAssertEqual(method, "thread/list")
            if params["cursor"] == nil { return ["data": Array(entries.prefix(2)), "nextCursor": "older"] }
            return ["data": Array(entries.dropFirst(2))]
        })
        codex.refresh()
        let tasks = try XCTUnwrap(codex.snapshot()?["tasks"] as? [[String: Any]])
        XCTAssertEqual(tasks.compactMap { $0["id"] as? String }, [ids[1], ids[2], ids[4], ids[0], ids[3]])
        XCTAssertEqual(tasks[1]["title"] as? String, "unique")
        XCTAssertEqual(tasks[4]["title"] as? String, "时间未知 · same title")
        XCTAssertNotEqual(tasks[0]["title"] as? String, tasks[3]["title"] as? String)
        XCTAssertTrue((tasks[0]["title"] as? String)?.hasSuffix(" · same title") == true)
        XCTAssertEqual(tasks[0]["updatedAt"] as? Int64, 500)
        XCTAssertEqual(tasks[4]["updatedAt"] as? Int64, 0)
    }
    private final class API {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        var calls: [(String, [String: Any])] = [], failSend = false, running = false
        var runtimeOverride: String?
        var text = "已完成公开回复"
        func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
            calls.append((method, params))
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/private/projects/sample", "name": "测试会话", "updatedAt": 42, "status": ["type": "idle"]]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": runtimeOverride ?? (running ? "active" : "idle")], "turns": [["id": turn, "status": running ? "inProgress" : "completed", "items": [["type": "agentMessage", "text": text], ["type": "reasoning", "text": "PRIVATE_THINKING"], ["type": "commandExecution", "aggregatedOutput": "PRIVATE_TOOL"]]]]]] }
            if method == "thread/turns/list" { return ["data": [["id": turn, "status": running ? "inProgress" : "completed", "items": [["type": "agentMessage", "text": text], ["type": "reasoning", "text": "PRIVATE_THINKING"], ["type": "commandExecution", "aggregatedOutput": "PRIVATE_TOOL"]]]], "nextCursor": NSNull()] }
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
    func testDesktopCreatedTasksAreRequestedAlongsideInteractiveHistory() throws {
        let desktop = UUID().uuidString.lowercased(), existing = UUID().uuidString.lowercased()
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { false }, call: { method, params in
            XCTAssertEqual(method, "thread/list")
            let sources = params["sourceKinds"] as? [String] ?? []
            XCTAssertEqual(Set(sources), Set(["cli", "vscode", "appServer"]))
            XCTAssertEqual(params["modelProviders"] as? [String], [])
            XCTAssertEqual(params["archived"] as? Bool, false)
            var entries: [[String: Any]] = [["id": existing, "cwd": "/synthetic/AI-bot", "name": "Existing task", "updatedAt": 1]]
            if sources.contains("appServer") {
                entries.insert(["id": desktop, "cwd": "/synthetic/acceptance", "name": "Desktop acceptance", "updatedAt": 2], at: 0)
            }
            return ["data": entries]
        })
        codex.refresh()
        let projection = try XCTUnwrap(codex.snapshot())
        let tasks = try XCTUnwrap(projection["tasks"] as? [[String: Any]])
        XCTAssertEqual(Set(tasks.compactMap { $0["id"] as? String }), Set([desktop, existing]))
        XCTAssertEqual((projection["projects"] as? [Any])?.count, 2)
    }
    func testEmptyTurnRendersStatusWithoutInventingReplyOrReadReceipt() throws {
        let api = API()
        for (state, status, placeholder) in [
            ("interrupted", "已中断", "任务已停止，本轮尚无公开文字。"),
            ("completed", "已完成", "本轮尚无公开文字。"),
            ("failed", "失败", "本轮尚无公开文字。"),
            ("inProgress", "运行中", "Codex 正在处理，等待新的动态…")
        ] {
            var publicText = ""
            let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { false }, call: { method, params in
                if method == "thread/read" {
                    return ["thread": ["id": api.task, "status": ["type": state == "inProgress" ? "active" : "idle"],
                                       "turns": [["id": api.turn, "status": state, "items": [
                                        ["type": "agentMessage", "text": publicText],
                                        ["type": "reasoning", "text": "PRIVATE_THINKING"]]]]]]
                }
                return try api.call(method, params)
            })
            codex.refresh()
            let result = codex.handle(["taskId": api.task, "op": "read"], device: "device")
            XCTAssertEqual(result.status, 200)
            // TAB5's inline reply takes receipt/text directly from this body.
            XCTAssertEqual(result.body["receipt"] as? String, status)
            XCTAssertEqual(result.body["text"] as? String, placeholder)
            let view = try XCTUnwrap(result.body["replyView"] as? [String: Any])
            XCTAssertEqual(view["hasText"] as? Bool, false)
            XCTAssertEqual(view["readReceipt"] as? String, "")
            XCTAssertEqual(view["pageCount"] as? Int, 1)
            let snapshot = try XCTUnwrap(codex.snapshot())
            XCTAssertEqual((snapshot["responses"] as? [String: String])?[api.task], placeholder)
            XCTAssertEqual((snapshot["receipts"] as? [String: String])?[api.task], status)
            XCTAssertNil((snapshot["repliesReady"] as? [String: String])?[api.task])
            XCTAssertNil((snapshot["repliesUnread"] as? [String: String])?[api.task])
            publicText = "稍后保存的公开回复"
            let updated = codex.handle(["taskId": api.task, "op": "read"], device: "device")
            XCTAssertEqual(updated.body["text"] as? String, publicText)
            XCTAssertEqual((updated.body["replyView"] as? [String: Any])?["hasText"] as? Bool, true)
        }
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
    func testCatalogCursorAndEightRowProjectPagesExposeOlderTasks() throws {
        let entries: [[String: Any]] = (0..<30).map { index in
            ["id": UUID().uuidString.lowercased(), "cwd": index < 18 ? "/synthetic/workspace" : "/synthetic/Work",
             "name": "History \(index)", "updatedAt": 100 - index, "status": ["type": "idle"]]
        }
        var failSecondPage = false, cursors: [String] = []
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { false }, call: { method, params in
            XCTAssertEqual(method, "thread/list")
            let cursor = params["cursor"] as? String ?? ""; cursors.append(cursor)
            if cursor.isEmpty { return ["data": Array(entries.prefix(15)), "nextCursor": "second"] }
            XCTAssertEqual(cursor, "second")
            if failSecondPage { throw MacTab5CodexFailure.timeout }
            // A boundary duplicate must not consume another visible row.
            return ["data": Array(entries.dropFirst(14)), "nextCursor": NSNull()]
        })
        codex.refresh()
        XCTAssertEqual(cursors, ["", "second"])
        let snapshot = try XCTUnwrap(codex.snapshot())
        let tasks = try XCTUnwrap(snapshot["tasks"] as? [[String: Any]])
        let projects = try XCTUnwrap(snapshot["projects"] as? [[String: Any]])
        var visible = Set<String>()
        for project in projects {
            let id = try XCTUnwrap(project["id"] as? String); XCTAssertEqual(id.count, 16)
            let rows = tasks.filter { $0["projectId"] as? String == id }
            XCTAssertLessThanOrEqual(rows.count, 8)
            visible.formUnion(rows.prefix(8).compactMap { $0["id"] as? String })
        }
        XCTAssertEqual(visible, Set(entries.compactMap { $0["id"] as? String }))
        XCTAssertEqual(Set(projects.compactMap { $0["name"] as? String }),
                       Set(["workspace · 1/3", "workspace · 2/3", "workspace · 3/3", "Work · 1/2", "Work · 2/2"]))
        failSecondPage = true; codex.refresh()
        XCTAssertEqual((codex.snapshot()?["tasks"] as? [[String: Any]])?.count, 30)
        XCTAssertTrue(codex.snapshot()?["control"] is NSNull)
    }
    func testProjectPickerLimitRetainsViewedAndWorkingTasks() throws {
        let api = API(), working = UUID().uuidString.lowercased()
        let entries: [[String: Any]] = (0..<40).map { index -> [String: Any] in
            let status: [String: Any] = index == 39 ? ["type": "active", "activeFlags": [String]()] : ["type": "idle"]
            return ["id": index == 39 ? working : index == 38 ? api.task : UUID().uuidString.lowercased(),
             "cwd": "/synthetic/project\(index)", "name": "Task \(index)", "updatedAt": 100 - index,
             "status": status]
        }
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { false }, call: { method, params in
            method == "thread/list" ? ["data": entries] : try api.call(method, params)
        })
        codex.refresh(); XCTAssertEqual(codex.handle(["op": "read", "taskId": api.task], device: "fixture").status, 200)
        let snapshot = try XCTUnwrap(codex.snapshot())
        XCTAssertEqual((snapshot["projects"] as? [Any])?.count, 24)
        let tasks = try XCTUnwrap(snapshot["tasks"] as? [[String: Any]])
        XCTAssertTrue(tasks.contains { $0["id"] as? String == working })
        XCTAssertTrue(tasks.contains { $0["id"] as? String == api.task })
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
    func testReadOnlyConnectionProjectsRepliesButCannotResumeSendOrClaimRuntime() throws {
        let api = API(); api.running = true
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { true }, canMutate: { false }, call: api.call)
        codex.refresh(); XCTAssertTrue(codex.summary.contains("只读已连接"))
        XCTAssertEqual((codex.snapshot()?["tasks"] as? [[String: Any]])?.first?["state"] as? String, "unknown")
        let reply = codex.handle(["op": "read", "taskId": api.task], device: "fixture")
        XCTAssertEqual(reply.status, 200); XCTAssertEqual(reply.body["text"] as? String, api.text)
        XCTAssertEqual((reply.body["replyView"] as? [String: Any])?["state"] as? String, "unknown")
        let control = try XCTUnwrap(codex.snapshot()?["control"] as? [String: Any])
        for flag in ["running", "canSend", "canSteer", "canInterrupt"] { XCTAssertEqual(control[flag] as? Bool, false) }
        for op in ["send", "steer", "interrupt"] {
            let response = codex.handle(["op": op, "taskId": api.task, "requestId": UUID().uuidString, "message": "fixture", "expectedTurn": api.turn], device: "fixture")
            XCTAssertEqual(response.status, 403); XCTAssertEqual(response.body["error"] as? String, "codex_read_only_connection")
        }
        XCTAssertTrue(api.calls.allSatisfy { ["thread/list", "thread/read", "thread/turns/list"].contains($0.0) })
        XCTAssertEqual(codex.handle(["op": "draft-save", "taskId": api.task, "requestId": UUID().uuidString, "message": "本机草稿"], device: "fixture").status, 200)
        XCTAssertEqual(codex.handle(["op": "draft-load", "taskId": api.task, "requestId": UUID().uuidString], device: "fixture").status, 200)
        XCTAssertEqual((codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String, "本机草稿")
    }
    func testReadOnlyHistoryUsesBoundedPublicPaginationAcrossTurnBoundary() throws {
        let task = UUID().uuidString.lowercased(), ids = (0..<3).map { _ in UUID().uuidString.lowercased() }
        var calls: [(String, [String: Any])] = []
        func turn(_ index: Int) -> [String: Any] { ["id": ids[index], "status": "completed", "items": [["type": "agentMessage", "text": "公开 \(index)"], ["type": "reasoning", "text": "PRIVATE"]]] }
        let codex = MacTab5Codex(store: try store(), enabled: { true }, canMutate: { false }, call: { method, params in
            calls.append((method, params))
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/synthetic", "name": "Fixture"]]] }
            if method == "thread/read" { XCTAssertEqual(params["includeTurns"] as? Bool, false); return ["thread": ["id": task, "status": ["type": "notLoaded"]]] }
            XCTAssertEqual(method, "thread/turns/list"); XCTAssertEqual(params["limit"] as? Int, 32); XCTAssertEqual(params["itemsView"] as? String, "full")
            if params["cursor"] as? String == "older" { return ["data": [turn(0)], "nextCursor": NSNull()] }
            return ["data": [turn(2), turn(1)], "nextCursor": "older"]
        })
        codex.refresh()
        let latest = codex.handle(["op": "read", "taskId": task], device: "fixture")
        XCTAssertEqual(latest.body["text"] as? String, "公开 2")
        XCTAssertEqual(calls.filter { $0.0 == "thread/turns/list" }.count, 1)
        let older = codex.handle(["op": "read", "taskId": task, "turnId": ids[1], "turnOffset": -1], device: "fixture")
        XCTAssertEqual(older.status, 200); XCTAssertEqual(older.body["text"] as? String, "公开 0")
        XCTAssertFalse(String(decoding: try MacTab5Protocol.json(codex.snapshot()!), as: UTF8.self).contains("PRIVATE"))
        XCTAssertEqual((older.body["replyView"] as? [String: Any])?["hasNext"] as? Bool, true)
    }
    func testUnloadedThreadCannotBeResumedAsIfItWereDesktopIdle() throws {
        let api = API(); api.runtimeOverride = "notLoaded"
        let codex = MacTab5Codex(store: try store(), enabled: { true }, sendEnabled: { true }, call: api.call)
        codex.refresh(); _ = codex.handle(["op": "read", "taskId": api.task], device: "fixture")
        XCTAssertEqual((codex.snapshot()?["control"] as? [String: Any])?["canSend"] as? Bool, false)
        XCTAssertEqual(codex.handle(["op": "send", "taskId": api.task, "requestId": UUID().uuidString, "message": "do not resume"], device: "fixture").status, 409)
        XCTAssertFalse(api.calls.contains { ["thread/resume", "turn/start"].contains($0.0) })
    }
    func testFullHistoryRetainsEarlierProgressAndOnlyPublicFields() throws {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        let progress: [String: Any] = ["id": "progress", "type": "agentMessage", "phase": "commentary", "text": "先检查入口", "privateExtra": "PRIVATE"]
        let final: [String: Any] = ["id": "final", "type": "agentMessage", "phase": "final_answer", "text": "检查完成"]
        let codex = MacTab5Codex(store: try store(), enabled: { true }, canMutate: { false }, call: { method, params in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/synthetic", "name": "Fixture"]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": "notLoaded"]]] }
            let items = params["itemsView"] as? String == "full" ? [progress, final,
                ["type": "reasoning", "text": "PRIVATE"], ["type": "agentMessage", "phase": "analysis", "text": "PRIVATE"]] : [final]
            return ["data": [["id": turn, "status": "completed", "items": items]]]
        })
        codex.refresh()
        let reply = codex.handle(["op": "read", "taskId": task], device: "fixture")
        XCTAssertEqual(reply.body["text"] as? String, "先检查入口\n\n检查完成")
        XCTAssertFalse(String(decoding: try MacTab5Protocol.json(codex.snapshot()!), as: UTF8.self).contains("PRIVATE"))
        XCTAssertNil(MacTab5Codex.publicItems([progress]).first?["privateExtra"])
    }
    func testFollowLastPageContinuesAcrossGrowthAndNewTurnsWithoutMovingPinnedPages() throws {
        let task = UUID().uuidString.lowercased(), first = UUID().uuidString.lowercased(), next = UUID().uuidString.lowercased()
        var text = String(repeating: "初", count: 1500), newer = false
        let codex = MacTab5Codex(store: try store(), enabled: { true }, canMutate: { false }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/synthetic", "name": "Fixture"]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": "notLoaded"]]] }
            var turns: [[String: Any]] = [["id": first, "status": "completed", "items": [["type": "agentMessage", "text": text]]]]
            if newer { turns.insert(["id": next, "status": "completed", "items": [["type": "agentMessage", "text": "下一轮进展"]]], at: 0) }
            return ["data": turns]
        })
        codex.refresh()
        let initial = codex.handle(["op": "read", "taskId": task, "page": -1], device: "fixture")
        XCTAssertEqual((initial.body["replyView"] as? [String: Any])?["page"] as? Int, 1)
        text += String(repeating: "新", count: 1500); codex.refresh()
        XCTAssertEqual((codex.snapshot()?["replyView"] as? [String: Any])?["page"] as? Int, 2)
        XCTAssertEqual((codex.snapshot()?["responses"] as? [String: String])?[task], MacTab5Codex.pages(text).last)
        newer = true; codex.refresh()
        XCTAssertEqual((codex.snapshot()?["replyView"] as? [String: Any])?["turnId"] as? String, next)
        XCTAssertEqual(codex.handle(["op": "read", "taskId": task, "turnId": first, "page": 1], device: "fixture").status, 200)
        text += String(repeating: "尾", count: 1500); codex.refresh()
        XCTAssertEqual((codex.snapshot()?["replyView"] as? [String: Any])?["turnId"] as? String, first)
        XCTAssertEqual((codex.snapshot()?["replyView"] as? [String: Any])?["page"] as? Int, 1)
    }
    func testReaderKeepsHistoryAndPageAcrossRefreshButLatestCanFollowNewTurns() throws {
        let task = UUID().uuidString.lowercased(), older = UUID().uuidString.lowercased(), latest = UUID().uuidString.lowercased(), next = UUID().uuidString.lowercased()
        var newTurn = false
        func turn(_ id: String, _ text: String) -> [String: Any] { ["id": id, "status": "completed", "items": [["type": "agentMessage", "text": text]]] }
        let codex = MacTab5Codex(store: try store(), enabled: { true }, canMutate: { false }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/synthetic", "name": "Fixture"]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": "notLoaded"]]] }
            var turns = [turn(latest, "Latest public reply"), turn(older, String(repeating: "Older public reply. ", count: 250))]
            if newTurn { turns.insert(turn(next, "New public reply"), at: 0) }
            return ["data": turns, "nextCursor": NSNull()]
        })
        codex.refresh()
        XCTAssertEqual(codex.handle(["op": "read", "taskId": task, "turnId": older, "page": 1], device: "fixture").status, 200)
        newTurn = true; codex.refresh()
        let pinned = try XCTUnwrap(codex.snapshot()?["replyView"] as? [String: Any])
        XCTAssertEqual(pinned["turnId"] as? String, older); XCTAssertEqual(pinned["page"] as? Int, 1)
        XCTAssertEqual(pinned["history"] as? Bool, true)
        // A rejected navigation must not replace the successfully displayed selection.
        XCTAssertEqual(codex.handle(["op": "read", "taskId": task, "page": 99], device: "fixture").status, 400)
        codex.refresh()
        XCTAssertEqual((codex.snapshot()?["replyView"] as? [String: Any])?["turnId"] as? String, older)
        newTurn = false
        XCTAssertEqual(codex.handle(["op": "read", "taskId": task], device: "fixture").status, 200)
        newTurn = true; codex.refresh()
        let followed = try XCTUnwrap(codex.snapshot()?["replyView"] as? [String: Any])
        XCTAssertEqual(followed["turnId"] as? String, next); XCTAssertEqual(followed["page"] as? Int, 0)
    }
}
