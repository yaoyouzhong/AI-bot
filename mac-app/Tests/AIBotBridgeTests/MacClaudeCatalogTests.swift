import XCTest
@testable import AIBotBridge

final class MacClaudeCatalogTests: XCTestCase {
    func testAllProjectsExistingNamesOpaqueAliasesAndFailedScanRetention() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let a = UUID(), b = UUID(), secret = Data(0..<32), now = Date()
        for (id,cwd,title,age) in [(a,"/tmp/project-a","已有名称",20),(b,"/tmp/project-b","",10)] {
            let dir = root.appendingPathComponent(id.uuidString);try FileManager.default.createDirectory(at:dir,withIntermediateDirectories:true)
            var rows: [[String:Any]] = [["type":"user","sessionId":id.uuidString,"cwd":cwd,"timestamp":ISO8601DateFormatter().string(from:now.addingTimeInterval(-Double(age))),"message":["content":"never use prompt as title"]]]
            if !title.isEmpty { rows.append(["type":"custom-title","sessionId":id.uuidString,"customTitle":title]) }
            var data = Data();for row in rows { data.append(try JSONSerialization.data(withJSONObject:row));data.append(10) }
            try data.write(to:dir.appendingPathComponent(id.uuidString+".jsonl"))
        }
        let catalog = MacClaudeCatalog(root:root,key:{secret});try catalog.refresh(now:now)
        let rows = catalog.snapshot();XCTAssertEqual(rows.count,2);XCTAssertEqual(rows.first?.title,"Claude Code")
        XCTAssertEqual(Set(rows.map(\.folder)),["project-a","project-b"])
        XCTAssertNil(catalog.resolve(a.uuidString))
        let named = try XCTUnwrap(rows.first { $0.session == a });XCTAssertEqual(catalog.resolve(named.alias)?.title,"已有名称")
        XCTAssertNotEqual(named.alias,a.uuidString.lowercased());XCTAssertNotEqual(named.alias,MacClaudeCatalog.alias("claude:session:"+a.uuidString.lowercased(),key:Data(repeating:9,count:32)))
        try FileManager.default.removeItem(at:root)
        XCTAssertThrowsError(try catalog.refresh(now:now));XCTAssertEqual(catalog.snapshot().count,2)
    }
}
