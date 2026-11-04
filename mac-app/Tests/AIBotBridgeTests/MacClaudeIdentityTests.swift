import XCTest
@testable import AIBotBridge

final class MacClaudeIdentityTests: XCTestCase {
    func testSameTurnTerminalStatesAndStaleEvents() throws {
        let tracker = MacClaudeIdentity(), a = UUID(), b = UUID(), p = UUID(), next = UUID()
        let base = Date(timeIntervalSince1970:1_800_000_000)
        var tick: Double = 0
        func event(_ kind:String, session:UUID? = nil, prompt:UUID? = nil, extra:[String:Any] = [:]) -> [String:Any] {
            tick += 1
            var e: [String:Any] = ["agent":"claude","event":kind,"sessionId":(session ?? a).uuidString,
                "promptId":(prompt ?? p).uuidString,"emittedAt":base.addingTimeInterval(tick).timeIntervalSince1970*1000]
            e.merge(extra) { _,new in new }; return e
        }
        func send(_ e:[String:Any]) -> Bool { tracker.record(e,now:base.addingTimeInterval(tick)) }
        XCTAssertFalse(send(event("Stop")))
        XCTAssertTrue(send(event("UserPromptSubmit")))
        let old = event("PreToolUse")
        XCTAssertTrue(send(event("PermissionRequest")))
        XCTAssertTrue(tracker.selection(now:base)!.needsInput)
        XCTAssertFalse(tracker.selection(now:base.addingTimeInterval(tick+300))!.needsInput)
        XCTAssertEqual(tracker.selection(now:base.addingTimeInterval(tick+300))?.state,"unknown")
        XCTAssertTrue(send(event("Stop")))
        XCTAssertEqual(tracker.selection(now:base)?.outcome,"unconfirmed")
        XCTAssertTrue(send(event("StopFailure")))
        XCTAssertTrue(send(event("Notification",extra:["notificationType":"idle_prompt"])))
        XCTAssertEqual(tracker.selection(now:base)?.outcome,"failed")
        XCTAssertEqual(tracker.selection(now:base)?.completionAt,0)
        XCTAssertTrue(send(event("UserPromptSubmit",prompt:next)))
        XCTAssertFalse(send(old))
        XCTAssertFalse(send(event("StopFailure")))
        XCTAssertEqual(tracker.selection(now:base)?.promptID,next)
        XCTAssertTrue(send(event("SessionEnd",prompt:next)))
        XCTAssertTrue(send(event("TurnResult",prompt:next,extra:["terminalReason":"completed","isError":false,"numTurns":1])))
        let complete = try XCTUnwrap(tracker.selection(now:base))
        XCTAssertEqual(complete.outcome,"completed"); XCTAssertTrue(complete.tool.completionActive)
        XCTAssertEqual(complete.activity,base.addingTimeInterval(tick))
        XCTAssertTrue(send(event("TurnResult",prompt:next,extra:["terminalReason":"completed","isError":false,"numTurns":1])))
        XCTAssertEqual(tracker.selection(now:base)?.completionSequence,complete.completionSequence)
        tracker.acknowledge(); XCTAssertFalse(tracker.selection(now:base)!.tool.completionActive)
        XCTAssertTrue(send(event("UserPromptSubmit",session:b)))
        XCTAssertEqual(tracker.selection(now:base)?.sessionID,b)
        XCTAssertTrue(send(event("TurnInterrupted",session:b)))
        XCTAssertEqual(tracker.selection(now:base)?.outcome,"interrupted")
        XCTAssertTrue(send(event("Notification",session:b,extra:["notificationType":"idle_prompt"])))
        XCTAssertEqual(tracker.selection(now:base)?.outcome,"interrupted")
    }

    func testProjectionExcludesPrivateDataAndRejectsSubagents() throws {
        let a = UUID(), p = UUID()
        var raw: [String:Any] = ["hook_event_name":"UserPromptSubmit","session_id":a.uuidString,"prompt_id":p.uuidString,
            "prompt":"private text", "cwd":"private path","transcript_path":"private file","session_title":"private title"]
        let projected = try XCTUnwrap(MacClaudeHook.project(JSONSerialization.data(withJSONObject:raw)))
        XCTAssertEqual(Set(projected.keys),Set(["agent","event","sessionId","promptId","emittedAt"]))
        raw["agent_id"] = "child"
        XCTAssertNil(MacClaudeHook.project(try JSONSerialization.data(withJSONObject:raw)))
    }

    func testIdentityHTTPBoundaryDoesNotFallBackToLegacy() throws {
        let tracker = MacClaudeIdentity(), a = UUID(), p = UUID(), now = Date()
        let server = HTTPStatusServer(port:0,token:{nil},event:{_,_,_ in XCTFail("identity must not downgrade");return true},
            claudeEvent:{tracker.record($0)},snapshot:{Data()})
        func request(_ e:[String:Any],origin:Bool = false) throws -> String {
            let body = try JSONSerialization.data(withJSONObject:e)
            return "POST /event HTTP/1.1\r\n" + (origin ? "Origin: https://example.com\r\n":"") + "Content-Length: \(body.count)\r\n\r\n" + String(decoding:body,as:UTF8.self)
        }
        let e: [String:Any] = ["agent":"claude","event":"UserPromptSubmit","sessionId":a.uuidString,"promptId":p.uuidString,"emittedAt":now.timeIntervalSince1970*1000]
        XCTAssertTrue(String(decoding:server.response(for:try request(e),loopback:true),as:UTF8.self).contains("200 OK"))
        XCTAssertFalse(String(decoding:server.response(for:try request(e),loopback:false),as:UTF8.self).contains("200 OK"))
        XCTAssertFalse(String(decoding:server.response(for:try request(e,origin:true),loopback:true),as:UTF8.self).contains("200 OK"))
        XCTAssertTrue(String(decoding:server.response(for:try request(["agent":"claude","event":"Stop","sessionId":a.uuidString]),loopback:true),as:UTF8.self).contains("400 Bad Request"))
    }

    func testPairedFrameBindsActiveTitleMoodAndCompletionToOneSession() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let a = UUID(), b = UUID(), p = UUID(), tracker = MacClaudeIdentity(), now = Date()
        for (id,title,age) in [(a,"Active A",-100.0),(b,"Newer idle B",-1.0)] {
            let rows: [[String:Any]] = [["type":"user","sessionId":id.uuidString,"timestamp":ISO8601DateFormatter().string(from:now.addingTimeInterval(age))],
                ["type":"custom-title","sessionId":id.uuidString,"customTitle":title]]
            var data = Data(); for row in rows { data.append(try JSONSerialization.data(withJSONObject:row)); data.append(10) }
            try data.write(to:root.appendingPathComponent(id.uuidString+".jsonl"))
        }
        func event(_ kind:String,_ id:UUID,_ offset:Double,extra:[String:Any] = [:]) {
            var body: [String:Any] = ["agent":"claude","event":kind,"sessionId":id.uuidString,"promptId":p.uuidString,"emittedAt":now.addingTimeInterval(offset).timeIntervalSince1970*1000]
            body.merge(extra) { _,new in new }; XCTAssertTrue(tracker.record(body,now:now))
        }
        event("UserPromptSubmit",a,0)
        var enabled = true
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        let service = MacTab5Service(port:0,host:{nil},gallery:MacTab5Gallery(directory:root),codex:MacTab5Codex(enabled:{false},sendEnabled:{false}),birthdays:MacBirthdays(directory:root),agenda:MacAgenda(directory:root),sourceEnabled:{enabled && $0 == "activity"},claudeSessions:MacClaudeSessionSampler(root:root),claudeIdentity:tracker,load:{pair},save:{_ in})
        defer { service.stop() }
        var snapshot = MacStatusSnapshot(version:1,time:"12:00:00",epochUtc:Int64(now.timeIntervalSince1970),utcOffsetSeconds:0,capturedAt:now,codex:ToolState(state:"idle",ageSeconds:30),claude:ToolState(state:"working",ageSeconds:0),musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil)
        snapshot.domesticActivity = MacDomesticActivity(activeProvider:"other",state:"working",needsInput:true,tokensToday:1)
        service.publish(snapshot)
        func frame() throws -> [String:Any] { try XCTUnwrap(MacTab5Protocol.object(service.frame())["data"] as? [String:Any]) }
        func title() throws -> String? { try (frame()["claudeSession"] as? [String:Any])?["title"] as? String }
        for _ in 0..<200 { if try title() == "Active A" { break }; usleep(10_000) }
        XCTAssertEqual(try title(),"Active A")
        XCTAssertTrue(try frame()["domesticActivity"] is NSNull)
        XCTAssertEqual(try (frame()["claude"] as? [String:Any])?["state"] as? String,"working")
        event("TurnResult",a,0.01,extra:["terminalReason":"completed","isError":false,"numTurns":1])
        XCTAssertEqual(try (frame()["claude"] as? [String:Any])?["completionActive"] as? Bool,true)
        XCTAssertEqual(try title(),"Active A")
        event("StopFailure",a,0.015)
        XCTAssertEqual(try (frame()["claude"] as? [String:Any])?["state"] as? String,"error")
        XCTAssertEqual(try (frame()["claude"] as? [String:Any])?["completionActive"] as? Bool,false)
        event("UserPromptSubmit",b,0.02)
        XCTAssertNil(try title())
        for _ in 0..<200 { if try title() == "Newer idle B" { break }; usleep(10_000) }
        XCTAssertEqual(try title(),"Newer idle B")
        XCTAssertEqual(try (frame()["claude"] as? [String:Any])?["completionActive"] as? Bool,false)
        event("TurnInterrupted",b,0.03)
        XCTAssertEqual(try (frame()["claude"] as? [String:Any])?["state"] as? String,"idle")
        XCTAssertEqual(try title(),"Newer idle B")
        let bytes = try service.frame()
        XCTAssertFalse(String(decoding:bytes,as:UTF8.self).contains(a.uuidString))
        enabled = false; XCTAssertNil(try frame()["claudeSession"])
    }
}
