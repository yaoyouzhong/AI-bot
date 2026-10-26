import XCTest
import Darwin
@testable import AIBotBridge

final class MacTab5CodexRPCTests: XCTestCase {
    func testFragmentedLinesKeepUTF8EscapesEmptyLinesAndPartialSuffix() throws {
        let first = try MacTab5Protocol.json(["id": 7, "result": ["text": "合成文字\n\"quoted\"\r🙂"]])
        let second = try MacTab5Protocol.json(["method": "fixture/notification", "params": [:]])
        var lines = MacTab5CodexRPC.Lines(), actual: [Data] = []
        for byte in first { actual += try lines.append(Data([byte])) }
        XCTAssertTrue(actual.isEmpty)
        var joined = Data([10, 10]); joined.append(second); joined.append(10); joined.append(first.prefix(9))
        actual += try lines.append(joined)
        XCTAssertEqual(actual, [first, Data(), second])
        var tail = Data(first.dropFirst(9)); tail.append(10)
        XCTAssertEqual(try lines.append(tail), [first])
        XCTAssertTrue(try lines.append(Data()).isEmpty)
    }
    func testStdioBufferLimitStillRejectsOverflowAndAcceptsExactBoundary() throws {
        let limit = 8 * 1024 * 1024
        var full = MacTab5CodexRPC.Lines()
        XCTAssertTrue(try full.append(Data(repeating: 120, count: limit)).isEmpty)
        XCTAssertThrowsError(try full.append(Data([10]))) { error in
            guard case MacTab5CodexFailure.protocolError = error else { return XCTFail("Expected bounded input") }
        }
        var boundary = MacTab5CodexRPC.Lines(), bytes = Data(repeating: 120, count: limit - 1); bytes.append(10)
        let complete = try boundary.append(bytes)
        XCTAssertEqual(complete.count, 1); XCTAssertEqual(complete.first?.count, limit - 1)
        XCTAssertEqual(try boundary.append(Data([121, 10])), [Data([121])])
    }
    func testLargeFragmentedStdioReplyIsCompleteAndChildStaysReadOnly() throws {
        let (tool, record) = try fixture("""
        for line in sys.stdin:
            message=json.loads(line)
            record(message)
            if 'id' not in message: continue
            result={'data':[]} if message['method']=='initialize' else {'text':'x'*(4*1024*1024)+'合成文字🙂'}
            payload=(json.dumps({'id':message['id'],'result':result},ensure_ascii=False)+'\\n').encode()
            for offset in range(0,len(payload),4093):
                sys.stdout.buffer.write(payload[offset:offset+4093]); sys.stdout.buffer.flush()
        sys.exit(0)
        """)
        let rpc = MacTab5CodexRPC(executable: { tool.path }); defer { rpc.stop() }
        let reply = try rpc.call("thread/turns/list", ["threadId": "fixture"])
        let text = try XCTUnwrap(reply["text"] as? String)
        XCTAssertEqual(text, String(repeating: "x", count: 4 * 1024 * 1024) + "合成文字🙂")
        XCTAssertFalse(rpc.canMutate)
        XCTAssertThrowsError(try rpc.call("turn/start", [:]))
        _ = try rpc.call("thread/list", [:])
        let rows = try records(record)
        XCTAssertEqual(rows.filter { $0["args"] != nil }.count, 1)
        XCTAssertEqual(rows.filter { $0["method"] as? String == "thread/turns/list" }.count, 1)
        XCTAssertFalse(rows.contains { $0["method"] as? String == "turn/start" })
    }
    func testCLIResolutionSupportsCurrentDesktopBundleAndRenamedInstallations() {
        for name in ["ChatGPT.app", "Renamed Codex.app"] {
            let app = URL(fileURLWithPath: "/Applications/" + name)
            let tool = app.appendingPathComponent("Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex").path
            XCTAssertEqual(MacTab5CodexRPC.locate(saved: nil, applications: [app], isExecutable: { $0 == tool }), tool)
            XCTAssertEqual(MacTab5CodexRPC.locate(saved: "/missing/codex", applications: [app], isExecutable: { $0 == tool }), tool)
            XCTAssertEqual(MacTab5CodexRPC.locate(saved: "/custom/codex", applications: [app], isExecutable: { $0 == tool || $0 == "/custom/codex" }), "/custom/codex")
        }
    }
    func testCLIResolutionRetainsLegacyAndTerminalFallbacksAndRejectsInvalidPaths() {
        let app = URL(fileURLWithPath: "/Applications/Codex.app"), old = app.appendingPathComponent("Contents/Resources/codex").path
        XCTAssertEqual(MacTab5CodexRPC.locate(saved: nil, applications: [app], isExecutable: { $0 == old }), old)
        XCTAssertEqual(MacTab5CodexRPC.locate(saved: nil, applications: [], isExecutable: { $0 == "/opt/homebrew/bin/codex" }), "/opt/homebrew/bin/codex")
        XCTAssertNil(MacTab5CodexRPC.locate(saved: "relative/codex", applications: [], isExecutable: { $0 == "relative/codex" }))
        XCTAssertNil(MacTab5CodexRPC.locate(saved: "/invalid\0/codex", applications: [], isExecutable: { $0.contains("\0") }))
    }

    func testReadOnlyStdioHandshakeNotificationsAndOneInitialization() throws {
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
    func testMissingCLIAndStdioExitHaveDistinctSafeFailures() throws {
        let missing = MacTab5CodexRPC(executable: { nil })
        XCTAssertThrowsError(try missing.call("thread/list", [:])) { error in
            guard case MacTab5CodexFailure.cliUnavailable = error else { return XCTFail("Expected missing CLI") }
        }
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: root) }
        let tool = root.appendingPathComponent("fixture-codex")
        try "#!/usr/bin/env python3\nimport sys\nsys.stdin.readline()\nsys.stderr.write('PRIVATE_TOKEN /private/socket\\n')\nsys.exit(1)\n".write(to: tool, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: tool.path)
        let rpc = MacTab5CodexRPC(executable: { tool.path })
        XCTAssertThrowsError(try rpc.call("thread/list", [:])) { error in
            guard case MacTab5CodexFailure.readOnlyUnavailable = error else { return XCTFail("Expected early stdio exit") }
        }
        rpc.stop()
    }
    private func fixture(_ behavior: String) throws -> (URL, URL) {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        let tool = root.appendingPathComponent("fixture-codex")
        let source = """
        #!/usr/bin/env python3
        import json,sys,pathlib,os
        path=pathlib.Path(__file__).with_name('records.jsonl')
        def record(value):
            with path.open('a') as f: f.write(json.dumps(value)+'\\n')
        record({'args':sys.argv[1:],'pid':os.getpid()})
        \(behavior)
        for line in sys.stdin:
            message=json.loads(line)
            record(message)
            if 'method' not in message or 'id' not in message: continue
            if message['method']=='thread/read' and 'lose-read' in sys.argv[0]: sys.exit(1)
            if message['method']=='thread/read':
                print(json.dumps({'id':900,'method':'item/commandExecution/requestApproval','params':{}}),flush=True)
            print(json.dumps({'id':message['id'],'result':{'data':[]}}),flush=True)
        """
        try source.write(to: tool, atomically: true, encoding: .utf8)
        try FileManager.default.setAttributes([.posixPermissions: 0o700], ofItemAtPath: tool.path)
        return (tool, root.appendingPathComponent("records.jsonl"))
    }
    private func records(_ path: URL) throws -> [[String: Any]] {
        try String(contentsOf: path, encoding: .utf8).split(separator: "\n").map { try MacTab5Protocol.object(Data($0.utf8)) }
    }
    func testOneReadOnlyChildBlocksAllMutationsAndClosesWithoutService() throws {
        let (tool, record) = try fixture("")
        let rpc = MacTab5CodexRPC(executable: { tool.path }); defer { rpc.stop() }
        XCTAssertFalse(rpc.canMutate)
        _ = try rpc.call("thread/list", [:]); _ = try rpc.call("thread/read", ["threadId": "fixture"])
        for method in ["thread/resume", "thread/start", "turn/start", "turn/steer", "turn/interrupt", "command/exec", "config/write", "future/mutate"] {
            XCTAssertThrowsError(try rpc.call(method, [:])) { error in
                guard case MacTab5CodexFailure.readOnlyConnection = error else { return XCTFail("Expected read-only denial") }
            }
        }
        _ = try rpc.call("thread/list", [:])
        XCTAssertFalse(rpc.canMutate)
        let rows = try records(record), starts = rows.filter { $0["args"] != nil }
        XCTAssertEqual(starts.count, 1)
        XCTAssertEqual(starts[0]["args"] as? [String], ["app-server", "--listen", "stdio://"])
        XCTAssertEqual(rows.filter { $0["method"] as? String == "thread/list" }.count, 2)
        XCTAssertFalse(rows.contains { ($0["method"] as? String)?.hasPrefix("turn/") == true })
        let approval = try XCTUnwrap(rows.first { $0["id"] as? Int == 900 })
        XCTAssertNotNil(approval["error"]); XCTAssertNil(approval["result"])
        let pid = try XCTUnwrap(starts.last?["pid"] as? Int)
        rpc.stop()
        let deadline = Date().addingTimeInterval(3)
        while kill(pid_t(pid), 0) == 0 && Date() < deadline { Thread.sleep(forTimeInterval: 0.01) }
        XCTAssertNotEqual(kill(pid_t(pid), 0), 0)
    }
    func testMutationCannotStartChildAndRejectedHandshakeDoesNotRetry() throws {
        let (missing, firstRecord) = try fixture("")
        let mutation = MacTab5CodexRPC(executable: { missing.path }); defer { mutation.stop() }
        XCTAssertThrowsError(try mutation.call("turn/start", [:])) { error in
            guard case MacTab5CodexFailure.readOnlyConnection = error else { return XCTFail("Expected read-only denial") }
        }
        XCTAssertFalse(FileManager.default.fileExists(atPath: firstRecord.path))
        let (rejected, secondRecord) = try fixture("message=json.loads(sys.stdin.readline()); print(json.dumps({'id':message['id'],'error':{'code':-1,'message':'fixture'}}),flush=True); sys.exit(1)")
        let rpc = MacTab5CodexRPC(executable: { rejected.path }); defer { rpc.stop() }
        XCTAssertThrowsError(try rpc.call("thread/list", [:])) { error in
            guard case MacTab5CodexFailure.rejected = error else { return XCTFail("Expected rejection") }
        }
        XCTAssertEqual(try records(secondRecord).filter { $0["args"] != nil }.count, 1)
    }
    func testReadFailureIsNotAutomaticallyReplayed() throws {
        let (original, record) = try fixture("")
        let tool = original.deletingLastPathComponent().appendingPathComponent("lose-read")
        try FileManager.default.moveItem(at: original, to: tool)
        let rpc = MacTab5CodexRPC(executable: { tool.path }); defer { rpc.stop() }
        _ = try rpc.call("thread/list", [:]); XCTAssertFalse(rpc.canMutate)
        XCTAssertThrowsError(try rpc.call("thread/read", ["threadId": "fixture"]))
        let rows = try records(record)
        XCTAssertEqual(rows.filter { $0["args"] != nil }.count, 1)
        XCTAssertEqual(rows.filter { $0["method"] as? String == "thread/read" }.count, 1)
        XCTAssertFalse(rpc.canMutate)
    }
}
