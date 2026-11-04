import XCTest
@testable import AIBotBridge

final class MacClaudeSessionTitleTests: XCTestCase {
    func testExistingNamesAreBoundToTheSessionAndCustomNameWins() throws {
        let id = UUID(), other = UUID()
        var title = MacClaudeSessionTitle(sessionID: id)
        func record(_ type: String, _ field: String, _ value: String, _ session: UUID) throws -> Data {
            try JSONSerialization.data(withJSONObject: ["type":type, "sessionId":session.uuidString, field:value])
        }
        title.consume(try record("ai-title", "aiTitle", "Automatic name", id))
        XCTAssertEqual(title.title, "Automatic name")
        title.consume(try record("custom-title", "customTitle", "Another session", other))
        XCTAssertEqual(title.title, "Automatic name")
        title.consume(try record("custom-title", "customTitle", "My chosen name", id))
        title.consume(try record("ai-title", "aiTitle", "Later automatic name", id))
        XCTAssertEqual(title.title, "My chosen name")
        title.consume(try record("custom-title", "customTitle", "New chosen name", id))
        XCTAssertEqual(title.title, "New chosen name")
    }

    func testNeverUsesOtherRecordsAndBoundsDisplayText() throws {
        let id = UUID()
        var title = MacClaudeSessionTitle(sessionID: id)
        let fields: [[String: Any]] = [
            ["type":"summary", "sessionId":id.uuidString, "summary":"private summary"],
            ["type":"user", "sessionId":id.uuidString, "customTitle":"private prompt"],
            ["type":"custom-title", "customTitle":"missing identity"],
            ["type":"ai-title", "sessionId":"invalid", "aiTitle":"bad identity"]
        ]
        for field in fields { title.consume(try JSONSerialization.data(withJSONObject: field)) }
        XCTAssertNil(title.title)
        title.consume(try JSONSerialization.data(withJSONObject: ["type":"ai-title", "sessionId":id.uuidString, "aiTitle":"  任务\n" + String(repeating:"字", count:150)]))
        XCTAssertEqual(title.title?.count, 120)
        XCTAssertTrue(title.title?.hasPrefix("任务 ") == true)
        title.consume(Data(repeating: 65, count: 16_385))
        XCTAssertEqual(title.title?.count, 120)
        let oversized = String(repeating:"é",count:120) + String(repeating:"\u{301}",count:1000)
        title.consume(try JSONSerialization.data(withJSONObject:["type":"custom-title", "sessionId":id.uuidString, "customTitle":oversized]))
        XCTAssertLessThanOrEqual(title.title?.utf8.count ?? 0,512)
    }

    func testIndexKeepsTitleWithItsSessionAndDoesNotBorrowAnotherName() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let project = root.appendingPathComponent("project"), subagents = project.appendingPathComponent("subagents")
        try FileManager.default.createDirectory(at: subagents, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let first = UUID(), second = UUID(), child = UUID(), now = Date()
        func line(_ fields: [String: Any]) throws -> String {
            String(decoding: try JSONSerialization.data(withJSONObject: fields), as: UTF8.self) + "\n"
        }
        func event(_ id: UUID, _ seconds: TimeInterval) throws -> String {
            try line(["type":"user", "sessionId":id.uuidString,
                      "timestamp":ISO8601DateFormatter().string(from:now.addingTimeInterval(seconds)),
                      "message":["content":"this prompt is not a title"]])
        }
        let older = try event(first, -90_000) + line(["type":"custom-title", "sessionId":first.uuidString, "customTitle":"Older title"])
        try older.write(to:project.appendingPathComponent(first.uuidString + ".jsonl"),atomically:true,encoding:.utf8)
        try event(child, -1).write(to:subagents.appendingPathComponent(child.uuidString + ".jsonl"),atomically:true,encoding:.utf8)
        let index = MacClaudeSessionIndex(root: root)
        XCTAssertEqual(index.scan(now:now)?.title, "Older title")
        XCTAssertEqual(index.scan(now:now)?.sessionID, first)
        let newer = try event(second, -10) + line(["type":"summary", "sessionId":second.uuidString, "summary":"not a title"])
        let newFile = project.appendingPathComponent(second.uuidString + ".jsonl")
        try newer.write(to:newFile,atomically:true,encoding:.utf8)
        let current = try XCTUnwrap(index.scan(now:now))
        XCTAssertEqual(current.sessionID, second)
        XCTAssertNil(current.title)
        XCTAssertEqual(index.scan(now:now, sessionID:first)?.title, "Older title")
        XCTAssertNil(index.scan(now:now, sessionID:child))
        XCTAssertNil(index.scan(now:now, sessionID:UUID()))
        try (newer + line(["type":"ai-title", "sessionId":second.uuidString, "aiTitle":"Current title"]))
            .write(to:newFile,atomically:true,encoding:.utf8)
        XCTAssertEqual(index.scan(now:now)?.title, "Current title")
        try FileManager.default.removeItem(at:newFile)
        XCTAssertEqual(index.scan(now:now)?.sessionID, first)
        XCTAssertNil(index.scan(now:now, sessionID:second))
    }

    func testFutureEventIsRecheckedWhenClockCatchesUp() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id = UUID(), now = Date()
        let event: [String: Any] = ["type":"assistant", "sessionId":id.uuidString,
                                   "timestamp":ISO8601DateFormatter().string(from:now.addingTimeInterval(300))]
        let data = try JSONSerialization.data(withJSONObject:event) + Data("\n".utf8)
        try data.write(to:root.appendingPathComponent(id.uuidString + ".jsonl"))
        let index = MacClaudeSessionIndex(root:root)
        XCTAssertNil(index.scan(now:now))
        XCTAssertEqual(index.scan(now:now.addingTimeInterval(301))?.sessionID, id)
    }

    func testSamplerNeverReturnsPreviousSessionAfterSelectionChanges() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let first = UUID(), second = UUID(), now = Date()
        for (id, name) in [(first, "First"), (second, "Second")] {
            let rows: [[String:Any]] = [
                ["type":"user", "sessionId":id.uuidString, "timestamp":ISO8601DateFormatter().string(from:now)],
                ["type":"custom-title", "sessionId":id.uuidString, "customTitle":name]
            ]
            var data = Data()
            for row in rows { data.append(try JSONSerialization.data(withJSONObject:row)); data.append(10) }
            try data.write(to:root.appendingPathComponent(id.uuidString + ".jsonl"))
        }
        let sampler = MacClaudeSessionSampler(root:root)
        func waitFor(_ id: UUID) -> MacClaudeSessionObservation? {
            for _ in 0..<200 {
                if let value = sampler.capture(enabled:true, now:now, sessionID:id) { return value }
                usleep(10_000)
            }
            return nil
        }
        XCTAssertEqual(waitFor(first)?.title, "First")
        XCTAssertNil(sampler.capture(enabled:true, now:now, sessionID:second))
        XCTAssertEqual(waitFor(second)?.title, "Second")
        XCTAssertNil(sampler.capture(enabled:true, now:now, sessionID:UUID()))
        XCTAssertNil(sampler.capture(enabled:false, now:now))
    }

    func testPairedStatusPublishesOnlyEligibleTitleAndRevokesIt() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let project = root.appendingPathComponent("project")
        try FileManager.default.createDirectory(at:project,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id = UUID(), now = Date(), stamp = ISO8601DateFormatter().string(from:now.addingTimeInterval(-30))
        let records: [[String: Any]] = [
            ["type":"user", "sessionId":id.uuidString, "timestamp":stamp, "message":["content":"never export"]],
            ["type":"custom-title", "sessionId":id.uuidString, "customTitle":"Synthetic session"]
        ]
        var bytes = Data()
        for row in records { bytes.append(try JSONSerialization.data(withJSONObject:row)); bytes.append(10) }
        try bytes.write(to:project.appendingPathComponent(id.uuidString + ".jsonl"))
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        var enabled = true
        let codex = MacTab5Codex(enabled:{false},sendEnabled:{false})
        let service = MacTab5Service(port:0,host:{nil},gallery:MacTab5Gallery(directory:root),codex:codex,
            birthdays:MacBirthdays(directory:root),agenda:MacAgenda(directory:root),
            sourceEnabled:{ $0 == "activity" && enabled },claudeSessions:MacClaudeSessionSampler(root:root),load:{pair},save:{_ in})
        defer { service.stop() }
        func state(_ claude: ToolState) -> MacStatusSnapshot {
            MacStatusSnapshot(version:1,time:"12:00:00",epochUtc:Int64(now.timeIntervalSince1970),utcOffsetSeconds:0,capturedAt:now,
                              codex:ToolState(state:"idle",ageSeconds:30),claude:claude,musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
        }
        func data() throws -> [String: Any] {
            try XCTUnwrap(MacTab5Protocol.object(service.frame())["data"] as? [String: Any])
        }
        service.publish(state(ToolState(state:"idle",ageSeconds:30)))
        var projected: [String: Any]?
        for _ in 0..<200 {
            projected = try data()["claudeSession"] as? [String: Any]
            if projected != nil { break }
            usleep(10_000)
        }
        XCTAssertEqual(projected?["title"] as? String,"Synthetic session")
        XCTAssertNotNil(projected?["lastActivity"])
        XCTAssertNil(projected?["sessionId"])
        service.publish(state(ToolState(state:"working",ageSeconds:0)))
        XCTAssertNil((try data()["claudeSession"] as? [String: Any])?["title"])
        enabled = false
        XCTAssertNil(try data()["claudeSession"])
    }
}
