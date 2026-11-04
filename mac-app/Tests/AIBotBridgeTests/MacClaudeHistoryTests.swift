import XCTest
@testable import AIBotBridge
final class MacClaudeHistoryTests: XCTestCase {
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
}
