import XCTest
@testable import AIBotBridge

final class MacClaudePublicRepliesTests: XCTestCase {
    func testUnknownReplyFormatDoesNotBecomeAnEmptySuccess() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id = UUID(), file = root.appendingPathComponent(id.uuidString+".jsonl")
        let rows: [[String:Any]] = [
            ["type":"user","sessionId":id.uuidString,"message":["role":"user","content":"private"]],
            ["type":"assistant","sessionId":id.uuidString,"message":["role":"assistant","content":"unknown format"]]
        ]
        var data = Data()
        for row in rows { data.append(try JSONSerialization.data(withJSONObject:row)); data.append(10) }
        try data.write(to:file)
        let reader = MacClaudeReplyReader(root:root)
        let binding = try XCTUnwrap(reader.advertise(sessionID:id,title:nil,now:Date()))
        XCTAssertEqual(reader.handle(["op":"read","binding":binding,"page":-1]).status,503)
        try (JSONSerialization.data(withJSONObject:rows[0])+Data("\n".utf8)).write(to:file)
        let empty = reader.handle(["op":"read","binding":binding,"page":-1])
        XCTAssertEqual(empty.status,200)
        XCTAssertEqual(empty.body["text"] as? String,"")
    }

    func testOnlyMainSessionPublicTextIsRetained() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id = UUID(), other = UUID(), file = root.appendingPathComponent(id.uuidString+".jsonl")
        var rows: [[String:Any]] = [
            ["type":"user","sessionId":id.uuidString,"message":["role":"user","content":"never export prompt"]],
            ["type":"assistant","sessionId":other.uuidString,"message":["role":"assistant","content":[["type":"text","text":"wrong session"]]]],
            ["type":"assistant","sessionId":id.uuidString,"isSidechain":true,"message":["role":"assistant","content":[["type":"text","text":"child"]]]],
            ["type":"assistant","sessionId":id.uuidString,"isSidechain":"true","message":["role":"assistant","content":[["type":"text","text":"child invalid schema"]]]],
            ["type":"assistant","sessionId":id.uuidString,"uuid":"one","message":["role":"assistant","content":[
                ["type":"thinking","thinking":"private reasoning"],["type":"tool_use","input":["command":"private command"]],
                ["type":"tool_result","content":"private output"],["type":"text","text":"公开回复🙂"]]]]
        ]
        rows.append(rows.last!)
        var data = Data(); for row in rows { data.append(try JSONSerialization.data(withJSONObject:row)); data.append(10) }
        data.append(Data("{\"type\":\"assistant\"".utf8)) // incomplete record
        try data.write(to:file)
        let result = try MacClaudePublicReplies.read(file,sessionID:id)
        XCTAssertEqual(result.text,"公开回复🙂"); XCTAssertFalse(result.hasEarlier)
    }

    func testPinnedEncryptedReadContractPagingAndSourceStop() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id = UUID(), other = UUID(), file = root.appendingPathComponent(id.uuidString+".jsonl")
        let text = String(repeating:"公开中文🙂 reply\n",count:200)
        func row(_ value:String) throws -> Data {
            let record: [String:Any] = ["type":"assistant","sessionId":id.uuidString,"message":["role":"assistant","content":[["type":"text","text":value]]]]
            return try JSONSerialization.data(withJSONObject:record)+Data("\n".utf8)
        }
        try row(text).write(to:file)
        var enabled = true
        let reader = MacClaudeReplyReader(root:root,enabled:{enabled}), now = Date()
        let binding = try XCTUnwrap(reader.advertise(sessionID:id,title:"会话 A",now:now))
        _ = reader.advertise(sessionID:other,title:"会话 B",now:now)
        var request: [String:Any] = ["op":"read","binding":binding,"page":0]
        let first = reader.handle(request,now:now); XCTAssertEqual(first.status,200)
        XCTAssertEqual(first.body["title"] as? String,"会话 A")
        let count = try XCTUnwrap(first.body["total"] as? Int), revision = try XCTUnwrap(first.body["revision"] as? String)
        XCTAssertGreaterThan(count,1)
        var copy = ""
        for page in 0..<count {
            request["page"] = page; request["revision"] = revision
            let response = reader.handle(request,now:now); XCTAssertEqual(response.status,200)
            let value = try XCTUnwrap(response.body["text"] as? String); XCTAssertLessThanOrEqual(value.utf8.count,1600); copy += value
            XCTAssertFalse(String(decoding:try JSONSerialization.data(withJSONObject:response.body),as:UTF8.self).contains(id.uuidString))
        }
        XCTAssertEqual(copy,text.trimmingCharacters(in:.whitespacesAndNewlines))
        try row("更新后的公开回复").write(to:file)
        XCTAssertEqual(reader.handle(request,now:now).status,409)
        request["op"] = "send"; XCTAssertEqual(reader.handle(request,now:now).status,400)
        request["op"] = "read"; enabled = false
        XCTAssertEqual(reader.handle(request,now:now).status,403)
        enabled = true; XCTAssertEqual(reader.handle(request,now:now).status,409)
    }

    func testPairedReadRPCUsesEncryptedReplayProtectedTransport() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let id = UUID(), file = root.appendingPathComponent(id.uuidString+".jsonl"), key = Data(0..<32), device = "001122334455"
        let row: [String:Any] = ["type":"assistant","sessionId":id.uuidString,"message":["role":"assistant","content":[["type":"text","text":"公开回复，仅经加密读取"]]]]
        try (JSONSerialization.data(withJSONObject:row)+Data("\n".utf8)).write(to:file)
        let reader = MacClaudeReplyReader(root:root)
        let binding = try XCTUnwrap(reader.advertise(sessionID:id,title:"会话 A",now:Date()))
        let pair = MacTab5Pairing(deviceId:device,usbPath:"/dev/cu.fixture",key:key,confirmed:true)
        let service = MacTab5Service(port:0,claudeReplies:reader,load:{pair},save:{_ in})
        defer { service.stop() }
        for transport in ["USB","Wi-Fi","BLE"] {
            let nonce = MacTab5Protocol.identifier()
            let request: [String:Any] = ["kind":"claude","op":"read","binding":binding,"page":-1,
                "session":service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]
            let packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(request),key:key,challenge:nonce)
            let proof = MacTab5Protocol.proof(key:key,message:"POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
            XCTAssertEqual(service.rpc(device:"ffffffffffff",nonce:nonce,proof:proof,packet:packet,transport:transport).0,401)
            let response = service.rpc(device:device,nonce:nonce,proof:proof,packet:packet,transport:transport)
            XCTAssertEqual(response.0,200)
            let clear = try MacTab5Protocol.object(MacTab5Protocol.decrypt(response.1,key:key,challenge:nonce))
            XCTAssertEqual(clear["status"] as? Int,200)
            XCTAssertEqual((clear["body"] as? [String:Any])?["text"] as? String,"公开回复，仅经加密读取")
            let replay = service.rpc(device:device,nonce:nonce,proof:proof,packet:packet,transport:transport)
            XCTAssertEqual(try MacTab5Protocol.object(MacTab5Protocol.decrypt(replay.1,key:key,challenge:nonce))["status"] as? Int,409)
        }
    }
}
