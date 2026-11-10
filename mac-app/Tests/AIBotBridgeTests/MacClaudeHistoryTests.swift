import XCTest
@testable import AIBotBridge
final class MacClaudeHistoryTests: XCTestCase {
    func testNewPanelRequiresExplicitIdentityAndAbsentTranscript() throws {
        let root=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id=UUID(), key=Data(0..<32)
        XCTAssertThrowsError(try MacClaudeHistory.read(root:root,session:id,key:key))
        XCTAssertTrue(try MacClaudeHistory.read(root:root,session:id,key:key,verifiedNewPanel:{true}).accepted.isEmpty)
        try Data("broken".utf8).write(to:root.appendingPathComponent(id.uuidString+".jsonl"))
        XCTAssertThrowsError(try MacClaudeHistory.read(root:root,session:id,key:key,verifiedNewPanel:{true}))
    }
    func testEndTurnRequiresPublicAssistantText() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let session = UUID(), user = UUID(), thinking = UUID(), reply = UUID()
        func row(_ type:String,_ id:UUID,_ parent:UUID?,_ content:Any) -> [String:Any] {
            ["type":type,"uuid":id.uuidString,"sessionId":session.uuidString,"parentUuid":parent?.uuidString as Any? ?? NSNull(),
             "message":["role":type,"content":content,"stop_reason":"end_turn"],"timestamp":"2026-11-05T13:00:00Z"]
        }
        var rows = [row("user",user,nil,"private prompt"),row("assistant",thinking,user,[["type":"thinking","thinking":"private reasoning"]])]
        func write() throws {
            var data = Data(); for value in rows { data.append(try JSONSerialization.data(withJSONObject:value)); data.append(10) }
            try data.write(to:root.appendingPathComponent(session.uuidString+".jsonl"))
        }
        try write()
        let pending = try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
        XCTAssertEqual(pending.turns.count,1)
        XCTAssertEqual(pending.turns.first?["status"] as? String,"unknown")
        XCTAssertFalse(String(decoding:try JSONSerialization.data(withJSONObject:pending.turns),as:UTF8.self).contains("private"))
        rows.append(row("assistant",reply,thinking,[["type":"text","text":"公开完成回复"]])); try write()
        let completed = try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
        XCTAssertEqual(completed.turns.count,1)
        XCTAssertEqual(completed.turns.first?["status"] as? String,"completed")
        let duration=UUID(), next=UUID()
        func link(_ id:UUID,_ parent:UUID,_ subtype:String)->[String:Any] {
            ["type":"system","subtype":subtype,"uuid":id.uuidString,"parentUuid":parent.uuidString,"sessionId":session.uuidString,"content":"private metadata"]
        }
        rows += [link(duration,reply,"turn_duration"),row("user",next,duration,"next private prompt"),link(UUID(),thinking,"turn_duration")]
        try write()
        let linked=try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
        XCTAssertFalse(linked.limited);XCTAssertEqual(linked.accepted,Set([user.uuidString.lowercased(),next.uuidString.lowercased()]));XCTAssertEqual(linked.turns.count,2)
        XCTAssertFalse(String(decoding:try JSONSerialization.data(withJSONObject:linked.turns),as:UTF8.self).contains("private"))
        rows[rows.count-3]=link(duration,reply,"away_summary");try write()
        let resumed=try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
        XCTAssertFalse(resumed.limited)
        XCTAssertEqual(resumed.accepted,linked.accepted)
        XCTAssertFalse(String(decoding:try JSONSerialization.data(withJSONObject:resumed.turns),as:UTF8.self).contains("private"))
        for subtype in ["informational", "api_error", "stop_hook_summary", "local_command"] {
            var context = link(duration,reply,subtype)
            rows[rows.count-3] = context
            rows.append(row("assistant",UUID(),next,[["type":"text","text":"Retry completed"]]))
            try write()
            let retried = try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
            XCTAssertFalse(retried.limited)
            XCTAssertEqual(retried.accepted, linked.accepted)
            XCTAssertEqual(retried.turns.last?["status"] as? String, "completed")
            XCTAssertFalse(String(decoding:try JSONSerialization.data(withJSONObject:retried.turns),as:UTF8.self).contains("private"))
            for field in ["sessionId", "parentUuid", "isSidechain"] {
                var invalid = context
                invalid[field] = field == "isSidechain" ? true as Any : "invalid"
                rows[rows.count-4] = invalid
                try write()
                XCTAssertTrue(try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32)).limited)
            }
            rows.removeLast()
            context["subtype"] = "unknown_subtype"
            rows[rows.count-3] = context
        }
        rows[rows.count-3]=link(duration,reply,"unknown_subtype");try write()
        XCTAssertTrue(try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32)).limited)
    }
    func testNativeContextLinksKeepUserReceiptWithoutExposingContextOrPeerInput() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let session = UUID(), user = UUID(), context = UUID(), peer = UUID(), context2 = UUID(), reply = UUID()
        func row(_ type:String,_ id:UUID,_ parent:UUID?,_ text:String) -> [String:Any] {
            ["type":type,"uuid":id.uuidString,"sessionId":session.uuidString,"parentUuid":parent?.uuidString as Any? ?? NSNull(),
             "message":["role":type,"content":[["type":"text","text":text]]],"timestamp":"2026-11-05T12:09:13Z"]
        }
        var peerRow = row("user",peer,context,"private peer input")
        peerRow["origin"] = ["kind":"peer"]; peerRow["isMeta"] = false
        let rows = [row("user",user,nil,"private native prompt"),row("attachment",context,user,"private context"),peerRow,
                    row("attachment",context2,peer,"private context"),row("assistant",reply,context2,"原生公开回复"),
                    row("attachment",UUID(),reply,"private trailing context"),row("attachment",UUID(),context,"private old branch")]
        var data = Data(); for row in rows { data.append(try JSONSerialization.data(withJSONObject:row)); data.append(10) }
        try data.write(to:root.appendingPathComponent(session.uuidString+".jsonl"))
        let key = Data(0..<32), value = try MacClaudeHistory.read(root:root,session:session,key:key)
        XCTAssertFalse(value.limited); XCTAssertEqual(value.turns.count,1)
        XCTAssertEqual(value.accepted,Set([user.uuidString.lowercased()]))
        XCTAssertEqual(value.turns.first?["id"] as? String,MacClaudeCatalog.alias("claude:turn:"+session.uuidString.lowercased()+":"+user.uuidString.lowercased(),key:key))
        let visible = String(decoding:try JSONSerialization.data(withJSONObject:value.turns),as:UTF8.self)
        XCTAssertTrue(visible.contains("原生公开回复")); XCTAssertFalse(visible.contains("private"))
    }
    func testOnlySelectedMainBranchPublicTextAndOpaqueTurnIds() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let session = UUID(), user = UUID(), branch = UUID(), tool = UUID(), reply = UUID(), second = UUID(), final = UUID()
        func row(_ type:String,_ id:UUID,_ parent:UUID?,_ content:Any) -> [String:Any] {
            ["type":type,"uuid":id.uuidString,"sessionId":session.uuidString,"parentUuid":parent?.uuidString as Any? ?? NSNull(),
             "message":["role":type,"content":content],"timestamp":"2026-11-04T01:00:00Z"]
        }
        let rows = [
            row("user",user,nil,"private prompt"),
            row("assistant",branch,user,[["type":"text","text":"discarded branch"]]),
            row("user",tool,user,[["type":"tool_result","content":"private output"]]),
            row("assistant",reply,tool,[["type":"thinking","thinking":"private reasoning"],["type":"text","text":"第一轮公开回复"]]),
            row("user",second,reply,"another private prompt"),
            row("assistant",final,second,[["type":"text","text":"第二轮公开回复"]])
        ]
        var data = Data();for value in rows { data.append(try JSONSerialization.data(withJSONObject:value));data.append(10) }
        try data.write(to:root.appendingPathComponent(session.uuidString+".jsonl"))
        let value = try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
        XCTAssertEqual(value.turns.count,2)
        let encoded = String(decoding:try JSONSerialization.data(withJSONObject:value.turns),as:UTF8.self)
        XCTAssertTrue(encoded.contains("第一轮公开回复"));XCTAssertTrue(encoded.contains("第二轮公开回复"))
        for excluded in ["private","discarded",session.uuidString,user.uuidString,second.uuidString] { XCTAssertFalse(encoded.contains(excluded)) }
        XCTAssertEqual(value.turns.first?["status"] as? String,"unknown")
    }
    func testInterruptMarkerBelongsToOriginalPrompt() throws {
        let root=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer{try? FileManager.default.removeItem(at:root)}
        let session=UUID(),prompt=UUID(),user=UUID(),notice=UUID(),literal=UUID()
        func row(_ id:UUID,_ parent:UUID?,_ group:UUID,_ text:String)->[String:Any] {
            ["type":"user","sessionId":session.uuidString,"uuid":id.uuidString,"promptId":group.uuidString,
             "parentUuid":parent?.uuidString as Any? ?? NSNull(),"message":["role":"user","content":[["type":"text","text":text]]]]
        }
        let marker="[Request interrupted by user]"
        let rows=[row(user,nil,prompt,"w"),row(notice,user,prompt,marker),row(literal,notice,UUID(),marker)]
        var bytes=Data()
        for item in rows {bytes.append(try JSONSerialization.data(withJSONObject:item));bytes.append(10)}
        try bytes.write(to:root.appendingPathComponent(session.uuidString+".jsonl"))
        let result=try MacClaudeHistory.read(root:root,session:session,key:Data(0..<32))
        XCTAssertEqual(result.turns.count,2)
        XCTAssertEqual(result.turns.first?["status"] as? String,"interrupted")
        XCTAssertEqual(result.accepted,[user.uuidString.lowercased(),literal.uuidString.lowercased()])
        XCTAssertNil(result.inputDigests[notice.uuidString.lowercased()])
    }
}
