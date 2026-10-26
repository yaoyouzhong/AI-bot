import XCTest
import AppKit
@testable import AIBotBridge

final class MacTab5PhotoTests: XCTestCase {
    private func storage() -> MacTab5PrivateStore {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return MacTab5PrivateStore(root: root, key: { Data(repeating: 9, count: 32) })
    }
    private func image() throws -> Data {
        let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: 2, pixelsHigh: 2, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
        rep.setColor(.red, atX: 0, y: 0); return try XCTUnwrap(rep.representation(using: .png, properties: [:]))
    }
    func testChunkUploadDuplicateRangesAndWholeChecksum() throws {
        let photos = MacTab5Photos(store: storage()), bytes = try image(), task = UUID().uuidString, id = UUID().uuidString
        var root: [String: Any] = ["kind": "image-chunk", "taskId": task, "requestId": id, "size": bytes.count, "sha256": MacTab5Protocol.sha256(bytes), "offset": 0]
        let count = bytes.count / 2, first = Data(bytes.prefix(count))
        XCTAssertEqual(photos.upload(root, bytes: first, device: "A").body["offset"] as? Int, count)
        XCTAssertEqual(photos.upload(root, bytes: first, device: "A").body["offset"] as? Int, count)
        XCTAssertEqual(photos.upload(root, bytes: Data(repeating: 0, count: count), device: "A").status, 409)
        root["offset"] = count; let done = photos.upload(root, bytes: Data(bytes.dropFirst(count)), device: "A")
        XCTAssertEqual(done.status, 200); XCTAssertEqual(done.body["imageId"] as? String, id.lowercased())
        XCTAssertEqual(try photos.inputs([id], device: "A", task: task.lowercased()).first?["type"] as? String, "image")
        XCTAssertThrowsError(try photos.inputs([id], device: "B", task: task.lowercased()))
        XCTAssertThrowsError(try photos.inputs([id], device: "A", task: UUID().uuidString))
    }
    func testInvalidImagesAndGapsCannotCommit() throws {
        let photos = MacTab5Photos(store: storage()), task = UUID().uuidString, id = UUID().uuidString
        XCTAssertEqual(photos.upload(["taskId": task, "requestId": id], bytes: Data(repeating: 1, count: 64), device: "A").status, 400)
        XCTAssertThrowsError(try photos.inputs([id], device: "A", task: task))
        let bytes = try image()
        XCTAssertEqual(photos.upload(["kind": "image-chunk", "taskId": task, "requestId": id, "sha256": MacTab5Protocol.sha256(bytes), "size": bytes.count, "offset": 1], bytes: Data(bytes.dropFirst()), device: "A").status, 409)
    }
    func testWiFiBinaryImageUsesPairedIdentitySessionAndReplayProtection() throws {
        let task = UUID().uuidString.lowercased(), id = UUID().uuidString.lowercased(), bytes = try image()
        let codex = MacTab5Codex(store: storage(), enabled: { true }, call: { method, _ in method == "thread/list" ? ["data": [["id": task, "cwd": "/test"]]] : [:] }); codex.refresh()
        let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.test", key: Data(repeating: 2, count: 32), confirmed: true)
        let service = MacTab5Service(port: 8765, codex: codex, load: { pair }, save: { _ in })
        let metadata = try MacTab5Protocol.json(["op": "image-upload", "taskId": task, "requestId": id, "session": service.session, "issuedAt": Int64(Date().timeIntervalSince1970 * 1000)])
        var clear = Data("T5I1".utf8); MacTab5Mailbox.append32(UInt32(metadata.count), to: &clear); clear.append(metadata); clear.append(bytes)
        let nonce = MacTab5Protocol.identifier(), packet = try MacTab5Protocol.encrypt(clear, key: pair.key, challenge: nonce)
        let proof = MacTab5Protocol.proof(key: pair.key, message: "POST|\(pair.deviceId)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
        XCTAssertEqual(service.imageHTTP(device: pair.deviceId, nonce: nonce, proof: proof, packet: packet).0, 200)
        XCTAssertEqual(service.imageHTTP(device: pair.deviceId, nonce: nonce, proof: proof, packet: packet).0, 409)
        XCTAssertEqual(service.imageHTTP(device: "aabbccddeeff", nonce: nonce, proof: proof, packet: packet).0, 401)
    }
    func testImageAttachmentsRemainBoundWhenDraftAndSend() throws {
        let storage = storage(), task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased(), id = UUID().uuidString.lowercased(); var sent: [[String: Any]] = []
        let codex = MacTab5Codex(store: storage, enabled: { true }, sendEnabled: { true }, call: { method, params in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/test"]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": "idle"], "turns": []]] }
            if method == "turn/start" { sent = params["input"] as! [[String: Any]]; return ["turn": ["id": turn]] }; return [:]
        }); codex.refresh()
        XCTAssertEqual(codex.photos.upload(["taskId": task, "requestId": id], bytes: try image(), device: "A").status, 200)
        let request: [String: Any] = ["op": "send", "taskId": task, "requestId": UUID().uuidString, "message": "", "images": [id]]
        XCTAssertEqual(codex.handle(request, device: "B").status, 503); XCTAssertTrue(sent.isEmpty)
        XCTAssertEqual(codex.handle(request, device: "A").status, 202); XCTAssertEqual(sent.count, 1)
        XCTAssertTrue((sent[0]["url"] as? String)?.hasPrefix("data:image/png;base64,") == true)
    }
}
