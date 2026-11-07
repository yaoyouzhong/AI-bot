import AppKit
import Foundation
import XCTest
@testable import AIBotBridge

final class MacClaudeTerminalIntegrationTests: XCTestCase {
    func testInteractiveTerminalFixture() throws {
        let env = ProcessInfo.processInfo.environment
        guard let profile = env["AIBOT_TERMINAL_FIXTURE"],
              let session = env["AIBOT_TERMINAL_SESSION"].flatMap(UUID.init(uuidString:)),
              let executable = env["AIBOT_TERMINAL_BINARY"] else { throw XCTSkip("Requires the isolated interactive Claude fixture") }
        let config = URL(fileURLWithPath: profile).resolvingSymlinksInPath()
        let fixture = config.deletingLastPathComponent()
        let temp = URL(fileURLWithPath: "/private/tmp", isDirectory: true).resolvingSymlinksInPath()
        guard config.lastPathComponent == "cfg", fixture.lastPathComponent.hasPrefix("aict-"),
              fixture.deletingLastPathComponent().path == temp.path else { return XCTFail("Unrecognized fixture profile") }
        let projects = config.appendingPathComponent("projects"), key = Data(0..<32)
        let bridge = URL(fileURLWithPath: executable)
        let store = MacTab5PrivateStore(root: fixture.appendingPathComponent("bridge"), key: { key })
        let registry = try MacClaudeHarnesses([
            .init(source: .terminal, discover: { [] }, live: { [session] },
                  resolve: { MacClaudeTerminalSession.find($0, config: config, bridge: bridge) })
        ])
        let client = try XCTUnwrap(registry.owner(session).client)
        XCTAssertEqual(client.source, .terminal)
        XCTAssertTrue(client.allows("send", images: true))
        let backend = MacClaudeWorkspaceBackend(root: projects, store: store, key: { key }, harnesses: registry)
        let workspace = MacClaudeWorkspace(store: store, backend: backend, enabled: { true })
        defer { workspace.stop() }
        workspace.tasks.refresh()
        let task = try XCTUnwrap(backend.alias(session: session))
        let device = "terminal-fixture"

        func until(_ predicate: () throws -> Bool) throws {
            let deadline = ProcessInfo.processInfo.systemUptime + 8
            repeat {
                if try predicate() { return }
                Thread.sleep(forTimeInterval: 0.1)
            } while ProcessInfo.processInfo.systemUptime < deadline
            throw MacTab5Error.timeout
        }

        func state(_ turn: String) throws -> String? {
            let value = try MacClaudeHistory.read(root: projects, session: session, key: key, backgroundImages: true)
            return value.turns.first { $0["id"] as? String == turn }?["status"] as? String
        }

        func request(_ op: String, _ text: String = "", expected: String = "", images: [String] = []) throws -> String {
            let packet: [String: Any] = ["op": op, "message": text, "expectedTurn": expected, "images": images,
                                          "taskId": task, "requestId": UUID().uuidString.lowercased()]
            let result = workspace.tasks.handle(packet, device: device)
            XCTAssertEqual(result.status, 202, "\(op) \(text) photos=\(images.count) \(result.body["error"] as? String ?? "")")
            let turn = try XCTUnwrap(result.body["turnId"] as? String)
            XCTAssertEqual(workspace.tasks.handle(packet, device: device).body["turnId"] as? String, turn)
            return turn
        }

        try until { FileManager.default.fileExists(atPath: fixture.appendingPathComponent("draft-ready").path) }
        let blocked = workspace.tasks.handle(["op": "send", "message": "must not submit", "expectedTurn": "",
                                             "taskId": task, "requestId": UUID().uuidString.lowercased()], device: device)
        XCTAssertEqual(blocked.status, 409)
        try Data().write(to: fixture.appendingPathComponent("draft-checked"))
        try until { FileManager.default.fileExists(atPath: fixture.appendingPathComponent("draft-cleared").path) }

        let textTurn = try request("send", "fixture terminal 中文🙂")
        try until { try state(textTurn) == "completed" && client.state() == "idle" }

        let jpeg = try MacTab5TestData.jpeg()
        let bitmap = try XCTUnwrap(NSBitmapImageRep(data: jpeg))
        let png = try XCTUnwrap(bitmap.representation(using: .png, properties: [:]))
        let photos = [jpeg, png, jpeg]
        let images = photos.map { bytes -> String in
            let image = UUID().uuidString.lowercased()
            XCTAssertEqual(workspace.tasks.photos.upload(["taskId": task, "requestId": image], bytes: bytes, device: device).status, 200)
            return image
        }
        try JSONEncoder().encode(photos.map(MacTab5Protocol.sha256)).write(to: fixture.appendingPathComponent("expect-1.json"))
        let photoTurn = try request("send", images: images)
        try Data().write(to: fixture.appendingPathComponent("release-1"))
        try until { try state(photoTurn) == "completed" && client.state() == "idle" }

        let first = try request("send", "fixture running for append")
        try until { client.allows("steer", images: false) && FileManager.default.fileExists(atPath: fixture.appendingPathComponent("append-busy").path) }
        let released = fixture.appendingPathComponent("append-release")
        DispatchQueue.global().asyncAfter(deadline: .now() + 1) { try? Data().write(to: released) }
        let queued = try request("steer", "fixture terminal queued", expected: first)
        try until { try state(first) == "completed" && state(queued) == "completed" && client.state() == "idle" }

        let running = try request("send", "fixture running for interrupt")
        try until { client.allows("interrupt", images: false) && FileManager.default.fileExists(atPath: fixture.appendingPathComponent("interrupt-busy").path) }
        let stopped = try request("interrupt", expected: running)
        XCTAssertEqual(stopped, running)
        XCTAssertEqual(try client.state(), "idle")
        XCTAssertEqual(try state(queued), "completed")
        try Data().write(to: fixture.appendingPathComponent("interrupt-release"))
        XCTAssertEqual(workspace.tasks.handle(["op": "read", "taskId": task], device: device).status, 200)
        let reply = workspace.tasks.snapshot()?["replyView"] as? [String: Any]
        XCTAssertEqual(reply?["state"] as? String, "interrupted")
    }
}
