import XCTest
@testable import AIBotBridge

final class MacTab5TransportTests: XCTestCase {
    func testBinaryHTTPRequestSurvivesEveryHeaderAndBodySplit() throws {
        let body = Data([0, 255, 128, 13, 10, 0, 42])
        var wire = Data("POST /tab5/v1/rpc HTTP/1.1\r\nHost: 192.168.1.20\r\nContent-Length: 7\r\n\r\n".utf8)
        wire.append(body)
        for split in 0..<wire.count { XCTAssertNil(try MacHTTPRequest.parse(Data(wire.prefix(split)))) }
        let request = try XCTUnwrap(MacHTTPRequest.parse(wire))
        XCTAssertEqual(request.method, "POST"); XCTAssertEqual(request.target, "/tab5/v1/rpc"); XCTAssertEqual(request.body, body)
    }

    func testAmbiguousOversizedAndPipelinedHTTPIsRejected() {
        for headers in ["Content-Length: -1", "Content-Length: invalid", "Content-Length: 16385",
                        "Content-Length: 0\r\ncontent-length: 0", "Transfer-Encoding: chunked",
                        "Content-Length: 0\r\nTransfer-Encoding: chunked", " Content-Length: 0", "Bad\tName: value"] {
            XCTAssertThrowsError(try MacHTTPRequest.parse(Data("POST /tab5/v1/rpc HTTP/1.1\r\n\(headers)\r\n\r\n".utf8)))
        }
        XCTAssertThrowsError(try MacHTTPRequest.parse(Data(repeating: 65, count: 8193)))
        XCTAssertThrowsError(try MacHTTPRequest.parse(Data("GET /status HTTP/1.1\r\n\r\nGET /status HTTP/1.1\r\n\r\n".utf8)))
        XCTAssertThrowsError(try MacHTTPRequest.parse(Data("GET  /status HTTP/1.1\r\n\r\n".utf8)))
    }

    func testHTTPRoutesKeepESPTokenAndTAB5AuthenticationSeparate() throws {
        let token = String(repeating: "t", count: 32)
        let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.usbmodem-fixture", key: Data(0..<32), confirmed: true)
        let service = MacTab5Service(port: 8765, load: { pair }, save: { _ in })
        let server = HTTPStatusServer(port: 8765, token: { token }, tab5: service, snapshot: { Data("{}".utf8) })
        func response(_ request: String) -> String {
            String(decoding: server.response(for: Data(request.utf8)), as: UTF8.self)
        }
        XCTAssertTrue(response("GET /status HTTP/1.1\r\nX-AIBot-Token: \(token)\r\n\r\n").hasPrefix("HTTP/1.1 200"))
        XCTAssertTrue(response("GET /status HTTP/1.1\r\n\r\n").hasPrefix("HTTP/1.1 401"))
        XCTAssertTrue(response("GET /tab5/v1/status HTTP/1.1\r\nX-AIBot-Token: \(token)\r\n\r\n").hasPrefix("HTTP/1.1 401"))
        XCTAssertTrue(response("GET /tab5/v1/status HTTP/1.1\r\nOrigin: https://example.test\r\n\r\n").hasPrefix("HTTP/1.1 403"))
        XCTAssertTrue(response("GET /tab5/v1/ota HTTP/1.1\r\n\r\n").hasPrefix("HTTP/1.1 404"))
    }

    func testMailboxFramingAndReassemblyRejectGapsAndWrongIdentity() throws {
        let content = Data((0..<5000).map { UInt8($0 & 255) })
        let id: UInt32 = 0x12345678
        var assembled = Data()
        for offset in stride(from: 0, to: content.count, by: 2048) {
            var part = Data(); MacTab5Mailbox.append32(id, to: &part)
            MacTab5Mailbox.append16(offset, to: &part); MacTab5Mailbox.append16(content.count, to: &part)
            part.append(content.subdata(in: offset..<min(offset + 2048, content.count)))
            var wrong = part; wrong[0] ^= 1
            XCTAssertThrowsError(try MacTab5Mailbox.append(wrong, id: id, total: content.count, to: &assembled))
            wrong = part; wrong[4] ^= 1
            XCTAssertThrowsError(try MacTab5Mailbox.append(wrong, id: id, total: content.count, to: &assembled))
            try MacTab5Mailbox.append(part, id: id, total: content.count, to: &assembled)
        }
        XCTAssertEqual(assembled, content)
        XCTAssertThrowsError(try MacTab5Mailbox.append(Data(repeating: 0, count: 8), id: id, total: 124, to: &assembled))
        let response = try MacTab5Mailbox.response(content, id: id)
        XCTAssertEqual(response.count, 3)
        for (index, part) in response.enumerated() {
            XCTAssertEqual(part[0], 2); XCTAssertEqual(MacTab5Mailbox.u32(part, 1), id)
            XCTAssertEqual(Int(MacTab5Mailbox.u16(part, 5)), index * 2048)
            XCTAssertEqual(Int(MacTab5Mailbox.u16(part, 7)), content.count)
        }
        XCTAssertEqual(response.reduce(into: Data()) { $0.append($1.dropFirst(9)) }, content)
    }

    func testBinaryHTTPReceiptRunsTheAuthenticatedDisplayPath() throws {
        let key = Data(0..<32), device = "001122334455"
        let pair = MacTab5Pairing(deviceId: device, usbPath: "/dev/cu.usbmodem-fixture", key: key, confirmed: true)
        let service = MacTab5Service(port: 8765, load: { pair }, save: { _ in })
        let date = Date()
        service.publish(MacStatusSnapshot(version: 1, time: "12:00:00", epochUtc: Int64(date.timeIntervalSince1970), utcOffsetSeconds: 28800,
                                          capturedAt: date, codex: ToolState(state: "idle", ageSeconds: 1), claude: ToolState(state: "idle", ageSeconds: 1),
                                          musicPlaying: nil, weather: nil, stocks: nil, systemMetrics: nil, quotas: nil, music: nil))
        service.usbState("fixture", confirmed: true)
        var confirmed = false
        service.displaySettings(nil) { if case .success = $0 { confirmed = true } }
        let frame = try MacTab5Protocol.object(service.frame()), data = frame["data"] as! [String: Any]
        let command = (data["connectionHealth"] as! [String: Any])["displayCommand"] as! [String: Any]
        let settings = MacTab5DisplaySettings(revision: "1234abcd", brightness: 75, volume: 40, muted: false, selected: -1,
                                             cycle: true, interval: 15, saverMinutes: 5, alerts: true, enabled: [1,1,1,1,1,1,1,1], order: Array(0..<8))
        let report: [String: Any] = ["kind": "display-settings", "requestId": command["requestId"]!, "session": service.session,
                                     "issuedAt": Int64(date.timeIntervalSince1970 * 1000), "ok": true, "error": "", "settings": settings.object]
        let nonce = MacTab5Protocol.identifier(), clear = try MacTab5Protocol.json(report)
        let packet = try MacTab5Protocol.encrypt(clear, key: key, challenge: nonce)
        let proof = MacTab5Protocol.proof(key: key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
        let server = HTTPStatusServer(port: 8765, token: { nil }, tab5: service, snapshot: { Data() })
        func request(_ proof: String) -> Data {
            var request = Data("POST /tab5/v1/rpc HTTP/1.1\r\nX-AIBot-Device: \(device)\r\nX-AIBot-Nonce: \(nonce)\r\nX-AIBot-Proof: \(proof)\r\nContent-Length: \(packet.count)\r\n\r\n".utf8)
            request.append(packet); return request
        }
        XCTAssertTrue(String(decoding: server.response(for: request("bad")), as: UTF8.self).hasPrefix("HTTP/1.1 401")); XCTAssertFalse(confirmed)
        let response = server.response(for: request(proof)), end = try XCTUnwrap(response.range(of: Data("\r\n\r\n".utf8)))
        XCTAssertTrue(String(decoding: response[..<end.lowerBound], as: UTF8.self).hasPrefix("HTTP/1.1 200"))
        let opened = try MacTab5Protocol.object(MacTab5Protocol.decrypt(Data(response[end.upperBound...]), key: key, challenge: nonce))
        XCTAssertEqual(opened["status"] as? Int, 200); XCTAssertTrue(confirmed)
        service.stop()
    }

    func testSerialScannersCannotOwnTheSamePort() {
        let path = "/dev/cu.usbmodem-test-lease"
        XCTAssertTrue(MacSerialPortLease.acquire(path)); XCTAssertFalse(MacSerialPortLease.acquire(path))
        MacSerialPortLease.release(path)
        XCTAssertTrue(MacSerialPortLease.acquire(path)); MacSerialPortLease.release(path)
    }
}
