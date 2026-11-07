import XCTest
@testable import AIBotBridge

final class MacClaudeWorkspaceTests: XCTestCase {
    func testDurableDraftExactDeliveryDedupPermissionAndInterruption() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let session = UUID(), key = Data(0..<32), user = UUID()
        let rows: [[String:Any]] = [["type":"user","uuid":user.uuidString,"sessionId":session.uuidString,"cwd":root.path,"timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"user","content":"Synthetic initial prompt"]],
            ["type":"custom-title","sessionId":session.uuidString,"customTitle":"验收会话 A"]]
        var bytes = Data();for row in rows { bytes.append(try JSONSerialization.data(withJSONObject:row)); bytes.append(10) }
        try bytes.write(to:root.appendingPathComponent(session.uuidString.lowercased()+".jsonl"))
        let cli = root.appendingPathComponent("fixture-cli")
        try Data(#"""
#!/usr/bin/python3
import sys,json
sid=next(x.split('=',1)[1] for x in sys.argv if x.startswith('--resume='))
current=None
permission=False
def emit(v):print(json.dumps(v),flush=True)
def result(reason='completed'):
 emit({'type':'result','session_id':sid,'user_message_uuid':current['uuid'],'user_message_uuids':[current['uuid']],'terminal_reason':reason,'is_error':reason!='completed','num_turns':1})
def finish():
 emit(current)
 emit({'type':'assistant','session_id':sid,'message':{'role':'assistant','content':[{'type':'thinking','thinking':'Excluded private reasoning'},{'type':'text','text':'Synthetic public reply'}]}})
 result()
for line in sys.stdin:
 v=json.loads(line)
 if v['type']=='control_request':
  subtype=v['request']['subtype']
  emit({'type':'control_response','response':{'subtype':'success','request_id':v['request_id'],'response':{'cancelled':[],'still_queued':[]}}})
  if subtype=='interrupt' and current:emit(current);result('interrupted')
 elif v['type']=='user':
  current=v
  text=''.join(x.get('text','') for x in v['message']['content'])
  if text=='wait':continue
  if text=='permission':
   permission=True
   for repeat in range(2):emit({'type':'control_request','request_id':'fixture-permission','request':{'subtype':'can_use_tool','tool_name':'Read','input':{'file_path':'synthetic.txt'}}})
  else:finish()
 elif v['type']=='control_response' and permission:
  permission=False
  if v['response']['response']['behavior']=='allow':finish()
  else:emit(current);result('denied')
"""#.utf8).write(to:cli)
        try FileManager.default.setAttributes([.posixPermissions:0o700],ofItemAtPath:cli.path)
        let store = MacTab5PrivateStore(root:root.appendingPathComponent("sealed"),key:{key})
        let backend = MacClaudeWorkspaceBackend(root:root,store:store,key:{key},executable:{cli},live:{[]},legacyFixture:true)
        defer { backend.stop() }
        let tasks = MacTab5Codex(store:store,enabled:{true},sendEnabled:{true},call:backend.call,provider:"Claude")
        tasks.deliveryReceipt = backend.receipt;tasks.deliveryCancelled = backend.cancelled; tasks.refresh()
        let task = try XCTUnwrap(tasks.taskCatalog().first?["id"] as? String)
        XCTAssertNotEqual(task,session.uuidString.lowercased())
        func rpc(_ op:String,_ text:String = "",_ expected:String = "",_ id:String = UUID().uuidString.lowercased()) -> MacTab5RPCResult {
            tasks.handle(["op":op,"taskId":task,"message":text,"expectedTurn":expected,"requestId":id],device:"fixture")
        }
        XCTAssertEqual(rpc("draft-save","草稿").status,200)
        XCTAssertEqual(rpc("draft-load").status,200)
        XCTAssertTrue(tasks.snapshot(device:"different-device")?["draftView"] is NSNull)
        let id = UUID().uuidString.lowercased(), sent = rpc("send","Synthetic message","",id)
        XCTAssertEqual(sent.status,202)
        XCTAssertEqual(rpc("send","Synthetic message","",id).status,202)
        XCTAssertEqual(rpc("send","different","",id).status,409)
        XCTAssertEqual(rpc("read").status,200)
        let snapshot = try XCTUnwrap(tasks.snapshot()), view = try XCTUnwrap(snapshot["control"] as? [String:Any])
        XCTAssertEqual(view["canSend"] as? Bool,true)
        XCTAssertTrue(String(describing:snapshot).contains("Synthetic public reply"))
        XCTAssertFalse(String(describing:snapshot).contains("Excluded private"))
        XCTAssertEqual(backend.selection(over:nil)?.sessionID,session)
        XCTAssertEqual(backend.selection(over:nil)?.outcome,"completed")
        let pending = rpc("send","permission")
        XCTAssertEqual(pending.status,504) // pending approval is never automatically allowed
        XCTAssertEqual(backend.approvals().count,1)
        XCTAssertEqual(backend.selection(over:nil)?.needsInput,true)
        XCTAssertEqual(rpc("read").status,200)
        XCTAssertEqual((tasks.snapshot()?["control"] as? [String:Any])?["waiting"] as? Bool,true)
        try backend.decide(XCTUnwrap(backend.approvals().first),allow:true)
        for _ in 0..<30 { if backend.approvals().isEmpty { break }; Thread.sleep(forTimeInterval:0.02) }
        Thread.sleep(forTimeInterval:0.1)
        // Resolve the exact durable request without resending its input.
        let receipts = try MacTab5Protocol.object(XCTUnwrap(store.read("journal")))["receipts"] as? [String:[String:Any]] ?? [:]
        let unresolved = try XCTUnwrap(receipts.first { $0.value["status"] as? Int == 504 }?.key)
        XCTAssertEqual(rpc("receipt","","",unresolved).status,202)
        let waiting = rpc("send","wait")
        XCTAssertEqual(waiting.status,504)
        XCTAssertEqual(rpc("read").status,200)
        let active = try XCTUnwrap((tasks.snapshot()?["control"] as? [String:Any])?["turnId"] as? String)
        XCTAssertEqual(rpc("interrupt","",active).status,202)
        XCTAssertEqual(rpc("read").status,200)
        XCTAssertEqual((tasks.snapshot()?["replyView"] as? [String:Any])?["state"] as? String,"interrupted")
    }
    func testOwnedOverviewReconcilesExactSessionWithoutMaskingNewExternalPrompt() {
        let session = UUID(), other = UUID(), prompt = UUID(), now = Date()
        let old = MacClaudeIdentity.Selection(sessionID:session,promptID:prompt,activity:now.addingTimeInterval(-5),state:"working",outcome:"running",needsInput:false,completionAt:0,completionSequence:0)
        let done = MacClaudeIdentity.Selection(sessionID:session,promptID:UUID(),activity:now,state:"idle",outcome:"completed",needsInput:false,completionAt:Int64(now.timeIntervalSince1970),completionSequence:0,ownedTurn:"opaque-turn")
        XCTAssertEqual(MacClaudeIdentity.resolve(bound:old,owned:[done])?.outcome,"completed")
        let next = MacClaudeIdentity.Selection(sessionID:session,promptID:UUID(),activity:now.addingTimeInterval(1),state:"working",outcome:"running",needsInput:false,completionAt:0,completionSequence:0)
        XCTAssertNil(MacClaudeIdentity.resolve(bound:next,owned:[done])?.ownedTurn)
        let elsewhere = MacClaudeIdentity.Selection(sessionID:other,promptID:UUID(),activity:now.addingTimeInterval(-10),state:"working",outcome:"running",needsInput:true,completionAt:0,completionSequence:0)
        XCTAssertEqual(MacClaudeIdentity.resolve(bound:elsewhere,owned:[done])?.sessionID,other)
        XCTAssertEqual(MacClaudeIdentity.resolve(bound:nil,owned:[done])?.ownedTurn,"opaque-turn")
    }
    func testAcceptanceFixtureFactory() throws {
        guard MacClaudeWorkspaceBackend.locate() != nil else { throw XCTSkip("Installed Claude CLI required for this isolated fixture factory") }
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString).resolvingSymlinksInPath()
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        try Data("1\n".utf8).write(to:root.appendingPathComponent(".aibot-synthetic-claude"))
        let environment = ["HOME":root.path+"/.","TMPDIR":root.path+"/.","ANTHROPIC_API_KEY":"synthetic-key","ANTHROPIC_BASE_URL":"http://127.0.0.1:12345"]
        try JSONSerialization.data(withJSONObject:environment).write(to:root.appendingPathComponent("environment.json"))
        let fixture = try MacTab5WorkspaceAcceptance(root:root)
        XCTAssertTrue(fixture.claude.tasks.isEnabled);XCTAssertFalse(fixture.codex.isEnabled)
        XCTAssertThrowsError(try fixture.setClients(["invalid"]))
    }
    func testRealCLIWorkspaceLoopback() throws {
        guard let directory = ProcessInfo.processInfo.environment["AIBOT_CLAUDE_NATIVE_ROOT"], let executable = ProcessInfo.processInfo.environment["AIBOT_CLAUDE_NATIVE_CLI"] else { throw XCTSkip("Run the isolated loopback harness to test the installed CLI") }
        let root = URL(fileURLWithPath:directory), key = Data(0..<32)
        let environment = try JSONSerialization.jsonObject(with:Data(contentsOf:root.appendingPathComponent("environment.json"))) as! [String:String]
        let arguments = try JSONSerialization.jsonObject(with:Data(contentsOf:root.appendingPathComponent("arguments.json"))) as! [String]
        let store = MacTab5PrivateStore(root:root.appendingPathComponent("private"),key:{key})
        let backend = MacClaudeWorkspaceBackend(root:root.appendingPathComponent(".claude/projects"),store:store,key:{key},executable:{URL(fileURLWithPath:executable)},live:{[]},environment:environment,arguments:arguments,legacyFixture:true)
        defer { backend.stop() }
        let tasks = MacTab5Codex(store:store,enabled:{true},sendEnabled:{true},call:backend.call,provider:"Claude")
        tasks.deliveryReceipt = backend.receipt;tasks.deliveryCancelled = backend.cancelled;tasks.refresh()
        let task = try XCTUnwrap(tasks.taskCatalog().first?["id"] as? String)
        func rpc(_ op:String,_ text:String = "",_ expected:String = "",_ images:[String] = []) -> MacTab5RPCResult {
            tasks.handle(["op":op,"taskId":task,"message":text,"expectedTurn":expected,"images":images,"requestId":UUID().uuidString.lowercased()],device:"native-fixture")
        }
        func completed() -> Bool {
            for _ in 0..<50 { _ = rpc("read");if (tasks.snapshot()?["replyView"] as? [String:Any])?["state"] as? String == "completed" { return true };Thread.sleep(forTimeInterval:0.1) };return false
        }
        XCTAssertEqual(rpc("send","NATIVE_TEXT").status,202);XCTAssertTrue(completed())
        let image = UUID().uuidString.lowercased(), png = Data(base64Encoded:"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8S0AAAAASUVORK5CYII=")!
        XCTAssertEqual(tasks.photos.upload(["taskId":task,"requestId":image],bytes:png,device:"native-fixture").status,200)
        XCTAssertEqual(rpc("send","NATIVE_IMAGE","",[image]).status,202);XCTAssertTrue(completed())
        XCTAssertEqual(rpc("send","NATIVE_SLOW").status,202)
        _ = rpc("read")
        let debug = try backend.call("thread/read",["threadId":task])["thread"] as? [String:Any] ?? [:]
        print("NATIVE_SLOW_STATES",(debug["turns"] as? [[String:Any]] ?? []).map { $0["status"] as? String ?? "missing" }, "control", (tasks.snapshot()?["control"] as? [String:Any])?["running"] as? Bool ?? false)
        let active = try XCTUnwrap((tasks.snapshot()?["control"] as? [String:Any])?["turnId"] as? String)
        let append = rpc("steer","NATIVE_QUEUED",active)
        XCTAssertTrue([202,504].contains(append.status),String(describing:append.body))
        _ = rpc("read")
        let queued = try XCTUnwrap((tasks.snapshot()?["control"] as? [String:Any])?["turnId"] as? String)
        let stopped = rpc("interrupt","",queued)
        XCTAssertEqual(stopped.status,202,String(describing:stopped.body))
        _ = rpc("read")
        XCTAssertEqual((tasks.snapshot()?["control"] as? [String:Any])?["running"] as? Bool,false)
        let journal = try MacTab5Protocol.object(XCTUnwrap(store.read("journal")))
        let records = journal["receipts"] as? [String:[String:Any]] ?? [:]
        let queuedRequest = try XCTUnwrap(records.first { $0.value["operation"] as? String == "steer" }?.key)
        let receipt = tasks.handle(["op":"receipt","taskId":task,"requestId":queuedRequest],device:"native-fixture")
        XCTAssertEqual(receipt.status,409)
        XCTAssertEqual(receipt.body["error"] as? String,"message_cancelled")
        XCTAssertEqual(receipt.body["deliveryFinal"] as? Bool,true)
        let retained = try MacTab5Protocol.object(XCTUnwrap(store.read("journal")))["drafts"] as? [String:[String:Any]]
        XCTAssertEqual(retained?["native-fixture|"+task]?["text"] as? String,"NATIVE_QUEUED")

    }

}
