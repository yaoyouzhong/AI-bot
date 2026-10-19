import XCTest
@testable import AIBotBridge

final class MacTab5CodexRPCTests: XCTestCase {
    func testLocalProxyHandshakeNotificationsAndOneInitialization() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true); defer { try? FileManager.default.removeItem(at: root) }
        let tool = root.appendingPathComponent("fixture-codex"), record = root.appendingPathComponent("requests.jsonl")
        let source = """
        #!/usr/bin/env python3
        import json,sys,pathlib
        record=pathlib.Path(__file__).with_name('requests.jsonl')
        for line in sys.stdin:
            message=json.loads(line)
            with record.open('a') as log: log.write(json.dumps(message)+'\\n')
            if 'id' not in message: continue
            print(json.dumps({'method':'fixture/notification','params':{}}),flush=True)
            print(json.dumps({'id':message['id'],'result':{'data':[]}}),flush=True)
        """
        try source.write(to: tool, atomically: true, encoding: .utf8); try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: tool.path)
        let rpc = MacTab5CodexRPC(executable: { tool.path })
        XCTAssertNotNil(try rpc.call("thread/list", ["limit": 10])["data"])
        XCTAssertNotNil(try rpc.call("thread/list", ["limit": 20])["data"]); rpc.stop()
        let requests = try String(contentsOf: record, encoding: .utf8).split(separator: "\n").map { try MacTab5Protocol.object(Data($0.utf8)) }
        XCTAssertEqual(requests.filter { $0["method"] as? String == "initialize" }.count, 1)
        XCTAssertEqual(requests.filter { $0["method"] as? String == "initialized" }.count, 1)
        XCTAssertEqual(requests.filter { $0["method"] as? String == "thread/list" }.count, 2)
        XCTAssertTrue(requests.allSatisfy { $0["jsonrpc"] == nil })
    }
}
