import Foundation
import XCTest
@testable import AIBotBridge

final class MacTab5VoiceHTTPTests: XCTestCase {
    private final class Fixture {
        let key = Data(0..<32), device = "001122334455"
        var callback: ((String, Bool, MacTab5SpeechFailure?) -> Void)?, feeds = 0
        var service: MacTab5Service!, server: HTTPStatusServer!
        init() throws {
            let voice = MacTab5Voice(enabled: { true }, backend: .init(start: { [weak self] _, callback in self?.callback = callback }, feed: { [weak self] _ in self?.feeds += 1 }, finish: { [weak self] in self?.callback?("最终合成文字", true, nil) }, cancel: {}))
            let pair = MacTab5Pairing(deviceId: device, usbPath: "/dev/cu.fixture", key: key, confirmed: true)
            service = MacTab5Service(port: 0, voice: voice, load: { pair }, save: { _ in })
            server = HTTPStatusServer(port: 0, token: { String(repeating: "t", count: 32) }, tab5: service, snapshot: { Data("{}".utf8) })
            try server.start()
            let deadline = Date().addingTimeInterval(3)
            while server.boundPort == nil && Date() < deadline { Thread.sleep(forTimeInterval: 0.01) }
        }
        deinit { service?.voice.stop(); server?.stop() }
        func connect() throws -> HTTPTestConnection { try HTTPTestConnection(port: XCTUnwrap(server.boundPort)) }
        func request(_ root: [String: Any], path: String = "/tab5/v1/voice", connection: String = "", version: String = "HTTP/1.1", validProof: Bool = true) throws -> (wire: Data, nonce: String) {
            var root = root; root["session"] = service.session; root["issuedAt"] = Int64(Date().timeIntervalSince1970 * 1000)
            let nonce = MacTab5Protocol.identifier(), packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root), key: key, challenge: nonce)
            let proof = validProof ? MacTab5Protocol.proof(key: key, message: "POST|\(path)|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))") : "invalid"
            var wire = Data("POST \(path) \(version)\r\nHost: fixture\r\nContent-Length: \(packet.count)\r\nX-AIBot-Device: \(device)\r\nX-AIBot-Nonce: \(nonce)\r\nX-AIBot-Proof: \(proof)\r\n\(connection.isEmpty ? "" : "Connection: \(connection)\r\n")\r\n".utf8)
            wire.append(packet); return (wire, nonce)
        }
        func call(_ root: [String: Any], on connection: HTTPTestConnection) throws -> [String: Any] {
            let request = try request(root), reply = try connection.exchange(request.wire)
            XCTAssertTrue(reply.head.hasPrefix("HTTP/1.1 200")); XCTAssertTrue(reply.head.contains("Connection: keep-alive"))
            return try MacTab5Protocol.object(MacTab5Protocol.decrypt(reply.body, key: key, challenge: request.nonce))
        }
    }
    func testVoiceStreamReusesSocketAndAuthenticatesEveryRequest() throws {
        let fixture = try Fixture(), connection = try fixture.connect()
        let started = try fixture.call(["op": "start", "source": "tab5", "taskId": MacTab5Voice.agendaOwner], on: connection)
        let id = try XCTUnwrap(started["voiceId"] as? String)
        for seq in 0..<5 {
            let reply = try fixture.call(["op": "audio", "voiceId": id, "seq": seq, "pcm": Data(repeating: 0, count: 640).base64EncodedString()], on: connection)
            XCTAssertEqual(reply["state"] as? String, "recording")
        }
        let stopped = try fixture.call(["op": "stop", "voiceId": id], on: connection)
        XCTAssertEqual(stopped["state"] as? String, "review"); XCTAssertEqual(fixture.feeds, 5)
        let invalid = try fixture.request(["op": "poll", "voiceId": id], validProof: false)
        let rejected = try connection.exchange(invalid.wire)
        XCTAssertTrue(rejected.head.hasPrefix("HTTP/1.1 401")); XCTAssertTrue(rejected.head.contains("Connection: close")); XCTAssertTrue(connection.isClosed())
    }
    func testVoiceRespectsExplicitCloseAndHTTP10() throws {
        for (header, version) in [("keep-alive, Close", "HTTP/1.1"), ("", "HTTP/1.0")] {
            let fixture = try Fixture(), connection = try fixture.connect()
            let request = try fixture.request(["op": "start", "source": "tab5", "taskId": MacTab5Voice.agendaOwner], connection: header, version: version)
            let reply = try connection.exchange(request.wire)
            XCTAssertTrue(reply.head.hasPrefix("HTTP/1.1 200")); XCTAssertTrue(reply.head.contains("Connection: close")); XCTAssertTrue(connection.isClosed())
        }
    }
    func testOtherRoutesStillClose() throws {
        let fixture = try Fixture(), connection = try fixture.connect()
        let reply = try connection.exchange(Data("GET /status HTTP/1.1\r\nX-AIBot-Token: \(String(repeating: "t", count: 32))\r\n\r\n".utf8))
        XCTAssertTrue(reply.head.hasPrefix("HTTP/1.1 200")); XCTAssertTrue(reply.head.contains("Connection: close")); XCTAssertTrue(connection.isClosed())
    }
    func testRPCReusesSocketWithoutReconnectAndRejectsReplay() throws {
        let fixture = try Fixture(), connection = try fixture.connect()
        // Unsupported operations have no effects but exercise the exact RPC
        // authentication, encryption, envelope and persistent-socket path.
        var previous: (wire: Data, nonce: String)?
        for _ in 0..<5 {
            let request = try fixture.request(["kind": "fixture-no-effect"], path: "/tab5/v1/rpc")
            let reply = try connection.exchange(request.wire)
            XCTAssertTrue(reply.head.hasPrefix("HTTP/1.1 200"))
            let envelope = try MacTab5Protocol.object(MacTab5Protocol.decrypt(reply.body, key: fixture.key, challenge: request.nonce))
            XCTAssertEqual(envelope["status"] as? Int, 404)
            previous = request
        }
        let repeated = try XCTUnwrap(previous), replay = try connection.exchange(repeated.wire)
        let envelope = try MacTab5Protocol.object(MacTab5Protocol.decrypt(replay.body, key: fixture.key, challenge: repeated.nonce))
        XCTAssertEqual(envelope["status"] as? Int, 409)
        let invalid = try fixture.request(["kind": "fixture-no-effect"], path: "/tab5/v1/rpc", validProof: false)
        let rejected = try connection.exchange(invalid.wire)
        XCTAssertTrue(rejected.head.hasPrefix("HTTP/1.1 401")); XCTAssertTrue(connection.isClosed())
    }
    func testRPCEveryRequestAuthenticatesAndRespectsClose() throws {
        for (header, version, valid) in [("keep-alive, Close", "HTTP/1.1", true), ("", "HTTP/1.0", true), ("", "HTTP/1.1", false)] {
            let fixture = try Fixture(), connection = try fixture.connect()
            let request = try fixture.request(["kind": "fixture-no-effect"], path: "/tab5/v1/rpc", connection: header, version: version, validProof: valid)
            let reply = try connection.exchange(request.wire)
            XCTAssertTrue(reply.head.hasPrefix(valid ? "HTTP/1.1 200" : "HTTP/1.1 401"))
            XCTAssertTrue(reply.head.contains("Connection: close")); XCTAssertTrue(connection.isClosed())
        }
    }
}
