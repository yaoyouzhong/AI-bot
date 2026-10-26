import XCTest
@testable import AIBotBridge

final class MacTab5InteractionDiagnosticsTests: XCTestCase {
    func testUnmeasuredQueueIsNotReportedAsZero() {
        let diagnostics = MacTab5InteractionDiagnostics(clock: { 1 })
        diagnostics.finish(diagnostics.begin(kind: "voice", operation: "start", transport: "BLE"), status: 200)
        XCTAssertTrue(diagnostics.summary.contains("排队 未测"))
        XCTAssertFalse(diagnostics.summary.contains("排队 0 ms"))
    }
    func testVoiceBoundaryAndFillAreMeasuredWithoutRecordingEachPoll() {
        let diagnostics = MacTab5InteractionDiagnostics(clock: { 1 })
        for (kind, op, label) in [("voice", "start", "开始录音"), ("voice", "stop", "停止录音"),
                                   ("voice", "cancel", "取消录音"), ("desktop", "stage-draft", "回填草稿")] {
            diagnostics.finish(diagnostics.begin(kind: kind, operation: op, transport: "BLE"), status: 200)
            XCTAssertTrue(diagnostics.summary.contains(label))
        }
        for _ in 0..<100 { XCTAssertNil(diagnostics.begin(kind: "voice", operation: "poll", transport: "BLE")) }
        XCTAssertEqual(diagnostics.summary.split(separator: "\n").count, 4)
    }
    func testPendingQueueAndProcessingTimesRemainSeparate() {
        var now = 10.0
        let diagnostics = MacTab5InteractionDiagnostics(clock: { now })
        let id = diagnostics.begin(kind: "codex", operation: "draft-load", transport: "Wi-Fi", queued: 0.025)
        now = 12
        XCTAssertTrue(diagnostics.summary.contains("Wi-Fi · 读取草稿 · 处理中 · 排队 25 ms／处理 2000 ms"))
        diagnostics.finish(id, status: 200); now = 20
        XCTAssertTrue(diagnostics.summary.contains("读取草稿 · 200 · 排队 25 ms／处理 2000 ms"))
        diagnostics.finish(id, status: 500)
        XCTAssertTrue(diagnostics.summary.contains("读取草稿 · 200"))
    }
    func testOnlyFixedLabelsAndBoundedNumbersCanReachDiagnostics() {
        let diagnostics = MacTab5InteractionDiagnostics(clock: { 1 })
        XCTAssertNil(diagnostics.begin(kind: "secret", operation: "payload", transport: "secret"))
        XCTAssertEqual(diagnostics.summary, "")
        let id = diagnostics.begin(kind: "desktop", operation: "open-recent", transport: "private-address", queued: .infinity)
        diagnostics.finish(id, status: 999999)
        XCTAssertEqual(diagnostics.summary, "#1 其他 · 定位会话 · 0 · 排队 0 ms／处理 0 ms")
    }
    func testHistoryIsBoundedAndLateCompletionCannotReplaceNewerRecords() {
        let diagnostics = MacTab5InteractionDiagnostics(clock: { 1 })
        let first = diagnostics.begin(kind: "codex", operation: "read", transport: "BLE")
        for _ in 0..<8 { diagnostics.finish(diagnostics.begin(kind: "codex", operation: "draft-save", transport: "USB"), status: 200) }
        diagnostics.finish(first, status: 500)
        let lines = diagnostics.summary.split(separator: "\n")
        XCTAssertEqual(lines.count, 8); XCTAssertTrue(lines[0].hasPrefix("#9 USB"))
        XCTAssertFalse(diagnostics.summary.contains("BLE")); XCTAssertFalse(diagnostics.summary.contains("500"))
    }
    func testUnauthenticatedRPCDoesNotCreateDiagnosticActivity() {
        let service = MacTab5Service(port: 0, load: { nil }, save: { _ in XCTFail() })
        XCTAssertEqual(service.rpc(device: "untrusted", nonce: "invalid", proof: "invalid", packet: Data()).0, 401)
        XCTAssertEqual(service.interactionDiagnostics.summary, "")
    }
    func testAuthenticatedDraftLoadIsMeasuredOnceAndDoesNotExposeIdentifiers() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        let key = Data(0..<32), task = UUID().uuidString.lowercased(), device = "001122334455"
        let store = MacTab5PrivateStore(root: root, key: { key })
        let codex = MacTab5Codex(store: store, enabled: { true }, call: { method, _ in
            XCTAssertEqual(method, "thread/list")
            return ["data": [["id": task, "cwd": "/synthetic", "name": "Synthetic chat"]]]
        })
        codex.refresh()
        let pair = MacTab5Pairing(deviceId: device, usbPath: "/dev/cu.synthetic", key: key, confirmed: true)
        let voice = MacTab5Voice(enabled: { true }, backend: .init(start: { _, _ in }, feed: { _ in }, finish: {}, cancel: {}))
        let service = MacTab5Service(port: 0, codex: codex, voice: voice, load: { pair }, save: { _ in XCTFail() })
        let nonce = MacTab5Protocol.identifier(), request = UUID().uuidString.lowercased()
        let clear = try MacTab5Protocol.json(["kind": "codex", "op": "draft-load", "taskId": task,
            "requestId": request, "session": service.session, "issuedAt": Int64(Date().timeIntervalSince1970 * 1000)])
        let packet = try MacTab5Protocol.encrypt(clear, key: key, challenge: nonce)
        let proof = MacTab5Protocol.proof(key: key, message: "POST|/tab5/v1/rpc|\(device)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
        XCTAssertEqual(service.rpc(device: device, nonce: nonce, proof: proof, packet: packet, transport: "Wi-Fi", queued: 0.025).0, 200)
        let first = service.interactionDiagnostics.summary
        XCTAssertTrue(first.contains("#1 Wi-Fi · 读取草稿 · 200 · 排队 25 ms"))
        XCTAssertFalse(first.contains(task)); XCTAssertFalse(first.contains(request)); XCTAssertFalse(first.contains(service.session))
        _ = service.rpc(device: device, nonce: nonce, proof: proof, packet: packet, transport: "Wi-Fi")
        XCTAssertEqual(service.interactionDiagnostics.summary, first)
        XCTAssertEqual((codex.snapshot()?["draftView"] as? [String: Any])?["requestId"] as? String, request)
        let legacyNonce = MacTab5Protocol.identifier(), legacyPacket = try MacTab5Protocol.encrypt(clear, key: key, challenge: legacyNonce)
        let legacyProof = MacTab5Protocol.proof(key: key, message: "POST|\(device)|\(legacyNonce)|\(MacTab5Protocol.sha256(legacyPacket))")
        XCTAssertEqual(service.codexHTTP(device: device, nonce: legacyNonce, proof: legacyProof, packet: legacyPacket, operation: "read", queued: 0.04).0, 200)
        XCTAssertTrue(service.interactionDiagnostics.summary.contains("#2 Wi-Fi · 读取草稿 · 200 · 排队 40 ms"))
        let voiceNonce = MacTab5Protocol.identifier()
        let voiceClear = try MacTab5Protocol.json(["op": "start", "taskId": task, "source": "pc", "session": service.session,
            "issuedAt": Int64(Date().timeIntervalSince1970 * 1000)])
        let voicePacket = try MacTab5Protocol.encrypt(voiceClear, key: key, challenge: voiceNonce)
        let voiceProof = MacTab5Protocol.proof(key: key, message: "POST|/tab5/v1/voice|\(device)|\(voiceNonce)|\(MacTab5Protocol.sha256(voicePacket))")
        XCTAssertEqual(service.voicePacket(device: device, nonce: voiceNonce, proof: voiceProof, packet: voicePacket, transport: "Wi-Fi", queued: 0.05).0, 200)
        XCTAssertTrue(service.interactionDiagnostics.summary.contains("#3 Wi-Fi · 开始录音 · 200 · 排队 50 ms"))
        voice.stop()
    }
}
