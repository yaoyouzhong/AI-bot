import XCTest
@testable import AIBotBridge

final class MacClaudeCancelledDraftTests: XCTestCase {
    func testConfirmedQueuedTextAndPhotoRestoredAfterCancellation() throws { try run(newerDraft:false) }
    func testCancellationPreservesNewerComposer() throws { try run(newerDraft:true) }
    func testReceiptReconcilesCancellationWithoutInterruptRequest() throws { try run(newerDraft:false,receiptOnly:true) }

    private func run(newerDraft: Bool, receiptOnly: Bool = false) throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let task = UUID().uuidString.lowercased(), active = UUID().uuidString.lowercased(), queued = UUID().uuidString.lowercased()
        let request = UUID().uuidString.lowercased(), photo = UUID().uuidString.lowercased(), device = "fixture-device"
        var cancelled = Set<String>()
        let store = MacTab5PrivateStore(root:root,key:{Data(0..<32)})
        let tasks = MacTab5Codex(store:store,enabled:{true},sendEnabled:{true},call:{ method, _ in
            switch method {
            case "thread/list": return ["data":[["id":task,"cwd":"/synthetic/project","name":"Fixture","updatedAt":Int64(Date().timeIntervalSince1970),"status":["type":"active"]]]]
            case "thread/read": return ["thread":["id":task,"status":["type":"active"],"turns":[["id":active,"status":"inProgress","items":[Any]()]]]]
            case "turn/steer": return ["turnId":queued]
            case "turn/interrupt": cancelled.insert(request); return ["turnId":active]
            default: return [:]
            }
        },provider:"Claude")
        tasks.deliveryCancelled = { _, id in cancelled.contains(id) }
        tasks.refresh()
        XCTAssertEqual(tasks.taskCatalog().first?["id"] as? String,task)
        let png = Data(base64Encoded:"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8S0AAAAASUVORK5CYII=")!
        XCTAssertEqual(tasks.photos.upload(["taskId":task,"requestId":photo],bytes:png,device:device).status,200)
        let sent = tasks.handle(["op":"steer","taskId":task,"requestId":request,"message":"queued input","images":[photo],"expectedTurn":active],device:device)
        XCTAssertEqual(sent.status,202)
        if newerDraft {
            XCTAssertEqual(tasks.handle(["op":"draft-save","taskId":task,"requestId":UUID().uuidString.lowercased(),"message":"newer input","images":[String]()],device:device).status,200)
        }
        if receiptOnly { cancelled.insert(request) }
        else { XCTAssertEqual(tasks.handle(["op":"interrupt","taskId":task,"requestId":UUID().uuidString.lowercased(),"message":"","expectedTurn":active],device:device).status,202) }
        let receipt = tasks.handle(["op":"receipt","taskId":task,"requestId":request],device:device)
        XCTAssertEqual(receipt.status,409); XCTAssertEqual(receipt.body["error"] as? String,"message_cancelled")
        XCTAssertEqual(receipt.body["deliveryFinal"] as? Bool,true)
        let journal = try MacTab5Protocol.object(XCTUnwrap(store.read("journal")))
        let draft = (journal["drafts"] as? [String:[String:Any]])?[device+"|"+task]
        XCTAssertEqual(draft?["text"] as? String,newerDraft ? "newer input":"queued input")
        XCTAssertEqual(draft?["images"] as? [String],newerDraft ? []:[photo])
        XCTAssertFalse(String(describing:tasks.snapshot(device:"another-device")).contains("queued input"))
    }
}
