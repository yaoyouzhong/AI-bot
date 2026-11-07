import XCTest
import AppKit
@testable import AIBotBridge

final class MacClaudeNativeTests: XCTestCase {
    func testAttachmentAccessibleLabel(){
        XCTAssertEqual(MacClaudeNativeClient.attachmentName("blue-probe.png 96×96 Remove attachment"),"blue-probe.png")
        XCTAssertEqual(MacClaudeNativeClient.attachmentName("设计 图片 96×96.png 1280×720 Remove attachment"),"设计 图片 96×96.png")
        XCTAssertNil(MacClaudeNativeClient.attachmentName("ctrl esc to focus or unfocus Claude"))
        XCTAssertNil(MacClaudeNativeClient.attachmentName("test.png 0×96 Remove attachment"))
    }

    func testBackgroundDaemonFixture() throws {
        let env = ProcessInfo.processInfo.environment
        guard let path = env["AIBOT_CLAUDE_BG_FIXTURE"],
              let session = env["AIBOT_CLAUDE_BG_SESSION"].flatMap(UUID.init(uuidString:)) else {
            throw XCTSkip("Requires the isolated Claude daemon and loopback model fixture")
        }
        let config = URL(fileURLWithPath: path).resolvingSymlinksInPath()
        let fixture = config.deletingLastPathComponent()
        let temp = URL(fileURLWithPath: "/private/tmp", isDirectory: true).resolvingSymlinksInPath()
        XCTAssertEqual(config.lastPathComponent, "cfg")
        guard config.lastPathComponent == "cfg", fixture.deletingLastPathComponent().path == temp.path,
              fixture.lastPathComponent.hasPrefix("aibg-") else { return XCTFail("Unrecognized fixture profile") }
        let projects = config.appendingPathComponent("projects")
        let key = Data(0..<32)
        let store = MacTab5PrivateStore(root: fixture.appendingPathComponent("bridge"), key: { key })
        let registry = try MacClaudeHarnesses([
            .init(source: .background, discover: { [] }, live: { MacClaudeBackground.live(root: config) },
                  resolve: { MacClaudeBackground.find($0, root: config) })
        ])
        let client = try XCTUnwrap(registry.owner(session).client)
        XCTAssertEqual(client.source, .background)
        XCTAssertNil(MacClaudeBackground.find(UUID(), root: config))
        XCTAssertFalse(client.allows("send", images: true))
        XCTAssertFalse(client.allows("interrupt", images: false))
        let backend = MacClaudeWorkspaceBackend(root: projects, store: store, key: { key }, harnesses: registry)
        let workspace = MacClaudeWorkspace(store: store, backend: backend, enabled: { true })
        defer { workspace.stop() }
        workspace.tasks.refresh()
        let alias = try XCTUnwrap(backend.alias(session: session))
        let snapshot = try XCTUnwrap(workspace.tasks.snapshot())
        let rows = try XCTUnwrap(snapshot["tasks"] as? [[String: Any]])
        XCTAssertEqual((rows.first?["source"] as? [String: String])?["id"], "claude.background")
        let controls = try XCTUnwrap(snapshot["controls"] as? [String: Any])
        XCTAssertEqual((controls[alias] as? [String: Any])?["canSend"] as? Bool, true)
        let message = "fixture followup"
        let before = try MacClaudeHistory.read(root: projects, session: session, key: key)
        let request: [String: Any] = ["op": "send", "taskId": alias, "message": message, "expectedTurn": "",
                                     "requestId": UUID().uuidString.lowercased()]
        XCTAssertEqual(workspace.tasks.handle(request, device: "daemon-fixture").status, 202)
        XCTAssertEqual(workspace.tasks.handle(request, device: "daemon-fixture").status, 202)
        let after = try MacClaudeHistory.read(root: projects, session: session, key: key)
        let added = after.accepted.subtracting(before.accepted)
        XCTAssertEqual(added.count, 1)
        XCTAssertEqual(added.first.flatMap { after.inputDigests[$0] }, MacClaudeNativeContent.digest(message))

        let read: [String: Any] = ["op": "read", "taskId": alias]
        let deadline = ProcessInfo.processInfo.systemUptime + 8
        repeat {
            XCTAssertEqual(workspace.tasks.handle(read, device: "daemon-fixture").status, 200)
            let snapshot = workspace.tasks.snapshot()
            let reply = snapshot?["replyView"] as? [String: Any]
            if reply?["state"] as? String == "completed",
               (snapshot?["responses"] as? [String: String])?[alias] == "fixture response" {
                return
            }
            Thread.sleep(forTimeInterval: 0.1)
        } while ProcessInfo.processInfo.systemUptime < deadline
        XCTFail("No completed native reply on TAB5")
    }
    private struct DirectFixture: MacClaudeDirectSession {
        let session = UUID()
        let source = MacClaudeSessionSource.background
        func allows(_ operation: String, images: Bool) -> Bool { operation == "send" && !images }
        func scope() throws -> String { "fixture-scope" }
        func state() throws -> String { "idle" }
        func prepareSubmission(_ request: MacClaudeNativeRequest) throws -> () throws -> Bool { request.authorized }
    }

    func testDirectSubmissionAbortedAfterJournalDoesNotClaimAnotherInput() throws {
        var authorized = true, historyReads = 0
        let result = try MacClaudeNativeOperation.run(client: DirectFixture(), request: request(authorized: { authorized }),
            history: {
                historyReads += 1
                return .init(turns: [], limited: false, accepted: [])
            }, dispatch: { _ in authorized = false }, wait: {})
        XCTAssertTrue(result.submissionNotInvoked)
        XCTAssertNil(result.user)
        XCTAssertEqual(historyReads, 1)
    }

    private final class Client: MacClaudeNativeSession {
        var permitted = true
        func allows(_ operation: String, images: Bool) -> Bool { permitted }
        var source: MacClaudeSessionSource = .terminal
        let session: UUID
        init(session: UUID = UUID()) { self.session = session }
        var epoch="original", runtime="idle", text="", images:[String]=[]
        var stages=0,submits=0,stops=0,accept=true,throwAfterSubmit=false,changeDraftOnStop=false
        var confirmsStop=true
        var onStage:(()->Void)?
        var onSubmit:((MacClaudeNativeRequest)throws->Void)?
        var history=MacClaudeHistory.Value(turns:[],limited:false,accepted:[],inputDigests:[:])
        func scope()throws->String{epoch}
        func state()throws->String{runtime}
        func view(_ operation:String)throws->MacClaudeNativeView {
            .init(target:"original-composer",text:text,images:images,empty:text.isEmpty&&images.isEmpty,
                  ready:operation=="send" ? runtime=="idle":operation=="observe" || runtime=="busy",canSubmit:!text.isEmpty || !images.isEmpty)
        }
        func stage(_ request:MacClaudeNativeRequest)throws{stages += 1;text=request.text;images=request.paths.map(\.lastPathComponent);onStage?()}
        func submit(_ request:MacClaudeNativeRequest)throws {
            submits += 1
            try onSubmit?(request)
            if accept {
                let id=UUID().uuidString.lowercased();var accepted=history.accepted,digests=history.inputDigests
                accepted.insert(id);digests[id]=MacClaudeNativeContent.digest(text,images:request.imageDigests)
                history = .init(turns:history.turns,limited:history.limited,accepted:accepted,inputDigests:digests)
            }
            text="";images=[]
            if throwAfterSubmit{throw MacTab5Error.unavailable}
        }
        func interrupt(_ request:MacClaudeNativeRequest)throws -> Bool {stops += 1;runtime="idle";if changeDraftOnStop{text="later draft"};return confirmsStop}
    }
    func testHarnessControlsFollowExactOwnersAndTransportLimit() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at: root) }
        let a = Client(), b = Client(), other = Client(session: a.session), key = Data(0..<32)
        let custom = MacClaudeSessionSource(id: "local.fixture", label: "Fixture Harness", kind: "harness")
        a.source = .vscode; b.source = .terminal; other.source = custom; other.permitted = false
        var connected = true, secondClaim = false
        func adapter(_ client: Client, active: @escaping () -> Bool) -> MacClaudeHarnessAdapter {
            .init(source: client.source, discover: { [] }, live: { active() ? [client.session] : [] },
                  resolve: { $0 == client.session && active() ? client : nil })
        }
        let code = adapter(a, active: { connected })
        let registry = try MacClaudeHarnesses([code, adapter(b, active: { true }), adapter(other, active: { secondClaim })])
        XCTAssertThrowsError(try registry.adding(code))
        XCTAssertFalse(MacClaudeSessionSource(id:"a",label:String(repeating:"é",count:41),kind:"harness").valid)
        XCTAssertFalse(MacClaudeSessionSource(id:"a",label:"Bad\u{200b}Label",kind:"ide").valid)
        XCTAssertFalse(MacClaudeSessionSource(id:"a",label:String(repeating:"a",count:49),kind:"ide").valid)
        for client in [a, b] {
            let user = UUID(), reply = UUID()
            let stamp = ISO8601DateFormatter().string(from: Date())
            let rows: [[String: Any]] = [
                ["type":"user","uuid":user.uuidString,"sessionId":client.session.uuidString,"cwd":root.path,
                 "timestamp":stamp,"message":["role":"user","content":"synthetic"]],
                ["type":"assistant","uuid":reply.uuidString,"sessionId":client.session.uuidString,"parentUuid":user.uuidString,
                 "timestamp":stamp,"message":["role":"assistant","content":[["type":"text","text":"Fixture complete"]],"stop_reason":"end_turn"]]
            ]
            var bytes = Data()
            for row in rows { bytes.append(try MacTab5Protocol.json(row)); bytes.append(10) }
            try bytes.write(to: root.appendingPathComponent(client.session.uuidString + ".jsonl"))
        }
        let store = MacTab5PrivateStore(root: root.appendingPathComponent("sealed"), key: { key })
        var now = 0.0, send = true, failedReply = false
        let backend = MacClaudeWorkspaceBackend(root:root,store:store,key:{key},harnesses:registry)
        let tasks = MacTab5Codex(store:store,enabled:{true},sendEnabled:{send},clock:{now},call:{ method, params in
            if failedReply, method == "thread/read" { throw MacTab5Error.unavailable }
            return try backend.call(method,params)
        },provider:"Claude")
        tasks.refresh()
        let idA = try XCTUnwrap(backend.alias(session:a.session)), idB = try XCTUnwrap(backend.alias(session:b.session))
        XCTAssertEqual(tasks.taskCatalog().compactMap { $0["title"] as? String }, ["Claude Code", "Claude Code"])
        XCTAssertNotEqual(idA, idB)
        func controls(_ limit: Int = 64) -> [String: Any] { tasks.snapshot(taskLimit:limit)?["controls"] as? [String: Any] ?? [:] }
        func canSend(_ task: String) -> Bool { (controls()[task] as? [String: Any])?["canSend"] as? Bool == true }
        func badge(_ task: String) -> String? {
            let rows = tasks.snapshot()?["tasks"] as? [[String:Any]] ?? []
            return (rows.first { $0["id"] as? String == task }?["source"] as? [String:String])?["id"]
        }
        XCTAssertTrue(canSend(idA)); XCTAssertTrue(canSend(idB))
        XCTAssertEqual(badge(idA), "claude.vscode"); XCTAssertEqual(badge(idB), "claude.terminal")
        XCTAssertEqual(tasks.handle(["op":"read","taskId":idA],device:"fixture").status, 200)
        XCTAssertTrue(canSend(idB)); XCTAssertEqual(Set(controls(1).keys), [idA])
        failedReply = true
        tasks.refresh()
        XCTAssertEqual((tasks.snapshot()?["replyView"] as? [String:Any])?["state"] as? String, "completed")
        XCTAssertEqual((tasks.snapshot()?["responses"] as? [String:String])?[idA], "Fixture complete")
        XCTAssertTrue(canSend(idB))
        failedReply = false
        secondClaim = true; tasks.refresh()
        XCTAssertTrue(controls()[idA] is NSNull); XCTAssertEqual(badge(idA), "multiple")
        connected = false; tasks.refresh()
        XCTAssertTrue(controls()[idA] is NSNull); XCTAssertEqual(badge(idA), custom.id)
        other.source = .terminal; tasks.refresh()
        XCTAssertTrue(controls()[idA] is NSNull); XCTAssertEqual(badge(idA), "unknown")
        secondClaim = false; tasks.refresh()
        XCTAssertTrue(controls()[idA] is NSNull); XCTAssertEqual(badge(idA), custom.id)
        let reopened = MacClaudeWorkspaceBackend(root:root,store:store,key:{key},harnesses:registry)
        let list = try reopened.call("thread/list", [:])["data"] as? [[String:Any]] ?? []
        let old = try XCTUnwrap(list.first { $0["id"] as? String == idA })
        XCTAssertEqual((old["source"] as? [String:String])?["id"], custom.id)
        XCTAssertTrue(old["nativeControl"] is NSNull)
        send = false; XCTAssertTrue(controls()[idB] is NSNull)
        send = true; now = 31; XCTAssertTrue(controls()[idB] is NSNull)
        tasks.refresh(); XCTAssertTrue(canSend(idB))
        tasks.stop(); XCTAssertTrue(controls()[idB] is NSNull)
    }
    private func request(_ operation:String="send",_ text:String="测试🙂",images:[String]=[],authorized:@escaping ()->Bool={true},current:@escaping ()throws->Bool={true})->MacClaudeNativeRequest {
        .init(operation:operation,text:text,paths:images.enumerated().map{URL(fileURLWithPath:"/fixture/\($0.offset).png")},imageDigests:images,authorized:authorized,current:current)
    }
    func testOriginalSendAndPhotoReceiptUseOneNativeSubmission()throws {
        let client=Client();var dispatched=0
        let result=try MacClaudeNativeOperation.run(client:client,request:request(images:["photo-a","photo-b"]),history:{client.history},dispatch:{ baseline in
            dispatched += 1;XCTAssertEqual(client.stages,0);XCTAssertEqual(baseline.session,client.session)
        },wait:{})
        XCTAssertNotNil(result.user);XCTAssertEqual(dispatched,1);XCTAssertEqual(client.stages,1);XCTAssertEqual(client.submits,1)
    }
    func testExistingDraftAndRevokedAuthorizationNeverStage()throws {
        for isDraft in [true,false] {
            let client=Client();client.text=isDraft ? "Try this user draft":""
            XCTAssertThrowsError(try MacClaudeNativeOperation.run(client:client,request:request(authorized:{isDraft}),history:{client.history},dispatch:{_ in XCTFail("No dispatch allowed")},wait:{}))
            XCTAssertEqual(client.stages,0);XCTAssertEqual(client.submits,0)
        }
    }
    func testIdleTurnCannotReceiveAppend()throws {
        let client=Client()
        XCTAssertThrowsError(try MacClaudeNativeOperation.run(client:client,request:request("steer"),history:{client.history},dispatch:{_ in XCTFail("No dispatch allowed")},wait:{}))
        XCTAssertEqual(client.stages,0);XCTAssertEqual(client.submits,0)
    }
    func testAppendBindsCurrentTurnAndLeavesUnknownUnresent()throws {
        let client=Client();client.runtime="busy";client.accept=false
        let result=try MacClaudeNativeOperation.run(client:client,request:request("steer"),history:{client.history},dispatch:{_ in},wait:{})
        XCTAssertNil(result.user);XCTAssertTrue(result.attempted);XCTAssertEqual(client.stages,1);XCTAssertEqual(client.submits,1)
        let changed=Client();changed.runtime="busy";var current=true;changed.onStage={current=false}
        let rejected=try MacClaudeNativeOperation.run(client:changed,request:request("steer",current:{current}),history:{changed.history},dispatch:{_ in},wait:{})
        XCTAssertNil(rejected.user);XCTAssertEqual(changed.stages,1);XCTAssertEqual(changed.submits,0)
        XCTAssertTrue(rejected.submissionNotInvoked);XCTAssertFalse(result.submissionNotInvoked)
    }
    func testSubmissionExceptionStillUsesReceiptWithoutResending()throws {
        let client=Client();client.throwAfterSubmit=true
        let result=try MacClaudeNativeOperation.run(client:client,request:request(),history:{client.history},dispatch:{_ in},wait:{})
        XCTAssertNotNil(result.user);XCTAssertEqual(client.submits,1)
    }
    func testInterruptOncePreservesDraftAndDoesNotInventUser()throws {
        let client=Client();client.runtime="busy";client.text="keep draft";client.images=["keep.png"]
        let result=try MacClaudeNativeOperation.run(client:client,request:request("interrupt",""),history:{client.history},dispatch:{_ in},wait:{})
        XCTAssertTrue(result.stopped);XCTAssertNil(result.user);XCTAssertEqual(client.stops,1);XCTAssertEqual(client.stages,0);XCTAssertEqual(client.text,"keep draft");XCTAssertEqual(client.images,["keep.png"])
        let changed=Client();changed.runtime="busy";changed.changeDraftOnStop=true
        let unknown=try MacClaudeNativeOperation.run(client:changed,request:request("interrupt",""),history:{changed.history},dispatch:{_ in},wait:{})
        XCTAssertFalse(unknown.stopped);XCTAssertEqual(changed.stops,1);XCTAssertEqual(changed.text,"later draft")
        let unconfirmed=Client();unconfirmed.runtime="busy";unconfirmed.confirmsStop=false
        let idle=try MacClaudeNativeOperation.run(client:unconfirmed,request:request("interrupt",""),history:{unconfirmed.history},dispatch:{_ in},wait:{})
        XCTAssertFalse(idle.stopped)
    }
    func testPhotoIdentityAndReceiptRejectMissingReorderedOrWrongImages()throws {
        // Distinct opaque RGB pixels, independent of AppKit screen/color settings.
        let first = try XCTUnwrap(Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP4z8CAFWEXHbQSACj/P8Fu7N9hAAAAAElFTkSuQmCC"))
        let second = try XCTUnwrap(Data(base64Encoded: "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEElEQVR4nGNgYPiPAw0pCQCpcD/BFMrqcwAAAABJRU5ErkJggg=="))
        let a = try MacClaudeNativeContent.image(first), b = try MacClaudeNativeContent.image(second)
        XCTAssertNotEqual(a,b)
        let id=UUID().uuidString.lowercased(),session=UUID(),empty=MacClaudeHistory.Value(turns:[],limited:false,accepted:[],inputDigests:[:])
        let baseline=MacClaudeNativeReceipt.capture(empty,session:session,scope:"original",text:"photo",images:[a,b])
        func observed(_ images:[String])->MacClaudeHistory.Value{.init(turns:[],limited:false,accepted:[id],inputDigests:[id:MacClaudeNativeContent.digest("photo",images:images)])}
        XCTAssertEqual(baseline.confirm(observed([a,b]),scope:"original"),id)
        for images in [[b,a],[a],[b,b],[]]{XCTAssertNil(baseline.confirm(observed(images),scope:"original"))}
        XCTAssertNil(baseline.confirm(observed([a,b]),scope:"replacement"))
        XCTAssertNotEqual(MacClaudeNativeContent.digest("",images:[a]),MacClaudeNativeContent.digest(""))
    }
    func testNativeWorkspaceJournalDedupAppendStopAndNewerDraft()throws {
        let root=FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer{try? FileManager.default.removeItem(at:root)}
        let client=Client(),key=Data(0..<32),initial=UUID()
        let path=root.appendingPathComponent(client.session.uuidString.lowercased()+".jsonl")
        var leaf=initial
        var rows:[[String:Any]]=[["type":"user","uuid":initial.uuidString,"sessionId":client.session.uuidString,"cwd":root.path,"timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"user","content":"initial fixture"]]]
        func write()throws{var bytes=Data();for row in rows{bytes.append(try JSONSerialization.data(withJSONObject:row));bytes.append(10)};try bytes.write(to:path)}
        try write()
        let store=MacTab5PrivateStore(root:root.appendingPathComponent("sealed"),key:{key})
        var cliCalls=0
        let backend=MacClaudeWorkspaceBackend(root:root,store:store,key:{key},executable:{cliCalls += 1;return nil},nativeFactory:{$0==client.session ? client:nil})
        let workspace=MacClaudeWorkspace(store:store,backend:backend,enabled:{true})
        defer{workspace.stop()}
        workspace.tasks.refresh()
        let task=try XCTUnwrap(workspace.tasks.taskCatalog().first?["id"] as? String)
        client.onSubmit={request in
            XCTAssertNotNil(try store.read("native-requests"))
            if request.operation == "send" {
                XCTAssertEqual(workspace.tasks.handle(["op":"draft-save","taskId":task,"message":"draft during dispatch",
                    "requestId":UUID().uuidString.lowercased()],device:"native-fixture").status,200)
            }
            let user=UUID(),reply=UUID()
            rows.append(["type":"user","uuid":user.uuidString,"sessionId":client.session.uuidString,"parentUuid":leaf.uuidString,"origin":["kind":"human"],"timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"user","content":request.text]])
            if request.operation == "send" {
                rows.append(["type":"assistant","uuid":reply.uuidString,"sessionId":client.session.uuidString,"parentUuid":user.uuidString,"timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"assistant","content":[["type":"text","text":"native fixture reply"]],"stop_reason":"end_turn"]])
                leaf = reply
            } else { leaf = user }
            try write()
        }
        func rpc(_ op:String,_ text:String="",_ expected:String="",_ id:String=UUID().uuidString.lowercased())->MacTab5RPCResult{
            workspace.tasks.handle(["op":op,"taskId":task,"requestId":id,"message":text,"expectedTurn":expected],device:"native-fixture")
        }
        let request=UUID().uuidString.lowercased(),sent=rpc("send","native first","",request)
        XCTAssertEqual(sent.status,202);XCTAssertEqual(client.submits,1)
        _ = rpc("draft-load")
        XCTAssertEqual((workspace.tasks.snapshot(device:"native-fixture")?["draftView"] as? [String:Any])?["text"] as? String, "draft during dispatch")
        XCTAssertEqual(rpc("send","native first","",request).status,202);XCTAssertEqual(client.submits,1)
        XCTAssertEqual(rpc("send","conflicting","",request).status,409)
        client.runtime="busy"
        let busy = UUID()
        rows.append(["type":"user","uuid":busy.uuidString,"sessionId":client.session.uuidString,"parentUuid":leaf.uuidString,
                     "timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"user","content":"busy fixture"]])
        leaf = busy; try write()
        _ = rpc("read")
        let turn=try XCTUnwrap((workspace.tasks.snapshot()?["control"] as? [String:Any])?["turnId"] as? String)
        XCTAssertEqual(rpc("steer","wrong turn",UUID().uuidString.lowercased()).status,409)
        XCTAssertEqual(rpc("steer","native append",turn).status,202);XCTAssertEqual(client.submits,2)
        _ = rpc("read")
        let active=try XCTUnwrap((workspace.tasks.snapshot()?["control"] as? [String:Any])?["turnId"] as? String)
        XCTAssertEqual(rpc("draft-save","newer device draft").status,200)
        client.text="keep PC draft";let stop=UUID().uuidString.lowercased()
        XCTAssertEqual(rpc("interrupt","",active,stop).status,202)
        XCTAssertEqual(rpc("interrupt","",active,stop).status,202);XCTAssertEqual(client.stops,1)
        _ = rpc("read")
        XCTAssertEqual((workspace.tasks.snapshot()?["replyView"] as? [String:Any])?["state"] as? String,"interrupted")
        _ = rpc("draft-load")
        XCTAssertEqual((workspace.tasks.snapshot(device:"native-fixture")?["draftView"] as? [String:Any])?["text"] as? String,"newer device draft")
        XCTAssertEqual(client.text,"keep PC draft");XCTAssertTrue(backend.approvals().isEmpty);XCTAssertEqual(cliCalls,0)
    }
}
