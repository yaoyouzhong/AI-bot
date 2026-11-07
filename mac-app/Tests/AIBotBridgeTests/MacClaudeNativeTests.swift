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
    private final class Client: MacClaudeNativeSession {
        var hasSessionAPI=true
        let session=UUID()
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
                  ready:operation=="send" ? runtime=="idle":operation=="observe" || runtime=="busy",canSubmit:!text.isEmpty||!images.isEmpty)
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
        func png(_ color:NSColor)throws->Data {
            let bitmap=try XCTUnwrap(NSBitmapImageRep(bitmapDataPlanes:nil,pixelsWide:8,pixelsHigh:8,bitsPerSample:8,samplesPerPixel:4,hasAlpha:true,isPlanar:false,colorSpaceName:.deviceRGB,bytesPerRow:0,bitsPerPixel:0))
            for y in 0..<8{for x in 0..<8{bitmap.setColor(color,atX:x,y:y)}}
            return try XCTUnwrap(bitmap.representation(using:.png,properties:[:]))
        }
        let first=try png(.red),second=try png(.blue),a=try MacClaudeNativeContent.image(first),b=try MacClaudeNativeContent.image(second)
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
            let user=UUID(),reply=UUID()
            rows.append(["type":"user","uuid":user.uuidString,"sessionId":client.session.uuidString,"parentUuid":leaf.uuidString,"origin":["kind":"human"],"timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"user","content":request.text]])
            rows.append(["type":"assistant","uuid":reply.uuidString,"sessionId":client.session.uuidString,"parentUuid":user.uuidString,"timestamp":ISO8601DateFormatter().string(from:Date()),"message":["role":"assistant","content":[["type":"text","text":"native fixture reply"]],"stop_reason":"end_turn"]])
            leaf=reply;try write()
        }
        func rpc(_ op:String,_ text:String="",_ expected:String="",_ id:String=UUID().uuidString.lowercased())->MacTab5RPCResult{
            workspace.tasks.handle(["op":op,"taskId":task,"requestId":id,"message":text,"expectedTurn":expected],device:"native-fixture")
        }
        let request=UUID().uuidString.lowercased(),sent=rpc("send","native first","",request)
        XCTAssertEqual(sent.status,202);XCTAssertEqual(client.submits,1)
        XCTAssertEqual(rpc("send","native first","",request).status,202);XCTAssertEqual(client.submits,1)
        XCTAssertEqual(rpc("send","conflicting","",request).status,409)
        _ = rpc("read")
        let turn=try XCTUnwrap((workspace.tasks.snapshot()?["control"] as? [String:Any])?["turnId"] as? String)
        client.runtime="busy"
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
