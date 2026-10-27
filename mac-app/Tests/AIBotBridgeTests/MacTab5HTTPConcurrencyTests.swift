import XCTest
import Foundation
@testable import AIBotBridge

final class MacTab5HTTPConcurrencyTests: XCTestCase {
    func testSlowCodexRequestDoesNotBlockLiveStatusHTTP() throws {
        let entered = DispatchSemaphore(value: 0), release = DispatchSemaphore(value: 0), task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString); defer { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 1, count: 32) }), enabled: { true }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/fixture"]]] }
            if method == "thread/read" { entered.signal(); _ = release.wait(timeout: .now() + 5); return ["thread": ["id": task, "status": ["type": "idle"], "turns": [["id": turn, "status": "completed", "items": []]]]] }; return [:]
        }); codex.refresh()
        let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.fixture", key: Data(repeating: 2, count: 32), confirmed: true)
        let service = MacTab5Service(port: 8765, codex: codex, load: { pair }, save: { _ in })
        let token = String(repeating: "a", count: 40), server = HTTPStatusServer(port: 0, token: { token }, tab5: service, snapshot: { Data("{\"fresh\":true}".utf8) })
        try server.start(); defer { release.signal(); server.stop() }
        let deadline = Date().addingTimeInterval(3)
        while server.boundPort == nil && Date() < deadline { Thread.sleep(forTimeInterval: 0.01) }
        let port = try XCTUnwrap(server.boundPort), nonce = MacTab5Protocol.identifier()
        let bytes = try MacTab5Protocol.encrypt(MacTab5Protocol.json(["kind": "codex", "op": "read", "taskId": task, "session": service.session, "issuedAt": Int64(Date().timeIntervalSince1970 * 1000)]), key: pair.key, challenge: nonce)
        var post = URLRequest(url: URL(string: "http://127.0.0.1:\(port)/tab5/v1/rpc")!); post.httpMethod = "POST"; post.httpBody = bytes
        post.setValue(pair.deviceId, forHTTPHeaderField: "X-AIBot-Device"); post.setValue(nonce, forHTTPHeaderField: "X-AIBot-Nonce")
        post.setValue(MacTab5Protocol.proof(key: pair.key, message: "POST|/tab5/v1/rpc|\(pair.deviceId)|\(nonce)|\(MacTab5Protocol.sha256(bytes))"), forHTTPHeaderField: "X-AIBot-Proof")
        let connection = URLSession(configuration: .ephemeral); defer { connection.invalidateAndCancel() }
        let postDone = expectation(description: "slow RPC completed")
        connection.dataTask(with: post) { _, response, error in XCTAssertNil(error); XCTAssertEqual((response as? HTTPURLResponse)?.statusCode, 200); postDone.fulfill() }.resume()
        XCTAssertEqual(entered.wait(timeout: .now() + 3), .success)
        let getDone = expectation(description: "live status remains available")
        var get = URLRequest(url: URL(string: "http://127.0.0.1:\(port)/status")!); get.setValue(token, forHTTPHeaderField: "X-AIBot-Token")
        connection.dataTask(with: get) { data, response, error in XCTAssertNil(error); XCTAssertEqual((response as? HTTPURLResponse)?.statusCode, 200); XCTAssertEqual(data, Data("{\"fresh\":true}".utf8)); getDone.fulfill() }.resume()
        wait(for: [getDone], timeout: 2)
        release.signal(); wait(for: [postDone], timeout: 4)
    }
}
