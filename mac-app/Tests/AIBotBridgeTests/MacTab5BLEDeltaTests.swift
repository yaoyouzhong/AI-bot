import XCTest
import zlib
@testable import AIBotBridge

final class MacTab5BLEDeltaTests: XCTestCase {
    private let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.fixture", key: Data(0..<32), confirmed: true)
    private let nonce = String(repeating: "a", count: 32), session = String(repeating: "b", count: 32)
    private func frame(_ seq: Int, data: [String: Any] = [:], device: String = "001122334455", session: String? = nil) throws -> Data {
        var state: [String: Any] = ["codex": [:], "claude": [:], "stable": String(repeating: "fixture", count: 1200)]
        for (k,v) in data { state[k] = v }
        return try MacTab5Protocol.json(["version": 1, "type": "tab5_status", "deviceId": device,
            "session": session ?? self.session, "sequence": seq, "data": state])
    }
    private func type(_ wire: Data) throws -> String? { try MacTab5Protocol.object(wire)["type"] as? String }
    private func equal(_ a: Any, _ b: Any) throws -> Bool {
        try JSONSerialization.data(withJSONObject: a, options: [.sortedKeys, .fragmentsAllowed]) ==
            JSONSerialization.data(withJSONObject: b, options: [.sortedKeys, .fragmentsAllowed])
    }
    // Independent application of the documented object-path operations.
    private func apply(_ operations: [[String: Any]], to state: [String: Any]) throws -> [String: Any] {
        func change(_ input: [String: Any], _ path: ArraySlice<String>, _ operation: [String: Any]) throws -> [String: Any] {
            var result = input; let key = try XCTUnwrap(path.first)
            if path.count > 1 { result[key] = try change(XCTUnwrap(input[key] as? [String: Any]), path.dropFirst(), operation) }
            else if operation["remove"] as? Bool == true { XCTAssertNotNil(input[key]); result.removeValue(forKey: key) }
            else { result[key] = try XCTUnwrap(operation["value"]) }
            return result
        }
        var result = state
        for op in operations { result = try change(result, ArraySlice(XCTUnwrap(op["path"] as? [String])), op) }
        return result
    }
    func testCapabilityRequiresAuthenticatedKnownReceiver() {
        func info(_ version: String, capability: Int) -> [String: Any] {
            ["deviceId": pair.deviceId, "nonce": nonce, "ack": 9, "telemetryVersion": capability,
             "proof": MacTab5Protocol.proof(key: pair.key, message: "\(nonce)|9"), "firmware": version,
             "firmwareProof": MacTab5Protocol.proof(key: pair.key, message: "FIRMWARE|\(nonce)|\(version)")]
        }
        for version in ["0.2.234-ui", "0.2.236-ui"] {
            XCTAssertTrue(MacTab5BLEDelta.supports(info(version, capability: 3), pair: pair, nonce: nonce))
        }
        for cap in [0,1,2,4] { XCTAssertFalse(MacTab5BLEDelta.supports(info("0.2.236-ui", capability: cap), pair: pair, nonce: nonce)) }
        for version in ["0.2.233-ui", "0.2.0234-ui", "1.0.236-ui"] { XCTAssertFalse(MacTab5BLEDelta.supports(info(version, capability: 3), pair: pair, nonce: nonce)) }
        var invalid = info("0.2.236-ui", capability: 3); invalid["proof"] = String(repeating: "0", count: 64)
        XCTAssertFalse(MacTab5BLEDelta.supports(invalid, pair: pair, nonce: nonce))
        XCTAssertFalse(MacTab5BLEDelta.supports(info("0.2.236-ui", capability: 3), pair: pair, nonce: String(repeating: "c", count: 32)))
    }
    func testOnlyAcknowledgedStateCanAdvanceBaselineAndResetRequiresFull() throws {
        let a = try frame(1), b = try frame(2, data: ["x": 2]), c = try frame(3, data: ["x": 3])
        var delta = MacTab5BLEDelta()
        XCTAssertEqual(try delta.wire(b, packed: false, supported: true), b)
        delta.acknowledge(a, sequence: 1)
        let first = try MacTab5Protocol.object(delta.wire(b, packed: false, supported: true))
        XCTAssertEqual(first["baseSequence"] as? Int, 1)
        // Preparing/sending another frame without its ACK must retain base 1.
        XCTAssertEqual(try MacTab5Protocol.object(delta.wire(c, packed: false, supported: true))["baseSequence"] as? Int, 1)
        delta.acknowledge(b, sequence: 2)
        XCTAssertEqual(try MacTab5Protocol.object(delta.wire(c, packed: false, supported: true))["baseSequence"] as? Int, 2)
        delta.reset(); XCTAssertEqual(try delta.wire(c, packed: false, supported: true), c)
        delta.acknowledge(a, sequence: 99); XCTAssertEqual(try delta.wire(c, packed: false, supported: true), c)
        delta.acknowledge(a, sequence: 1); XCTAssertEqual(try delta.wire(c, packed: false, supported: false), c)
        XCTAssertEqual(try delta.wire(c, packed: false, supported: true), c)
    }
    func testNullRemovalArraysBooleansAndNestedChangesReconstructExactData() throws {
        let old: [String: Any] = ["x": ["gone": 1, "null": "old", "bool": true, "list": [true, 2]] as [String: Any], "remove": NSNull()]
        let new: [String: Any] = ["x": ["null": NSNull(), "bool": 1, "list": [1, 2], "中文": "🙂"] as [String: Any], "add": NSNull()]
        let a = try frame(10, data: old), b = try frame(20, data: new)
        var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 10)
        let root = try MacTab5Protocol.object(delta.wire(b, packed: false, supported: true))
        XCTAssertEqual(root["type"] as? String, "tab5_delta")
        let ops = try XCTUnwrap(root["changes"] as? [[String: Any]])
        XCTAssertTrue(ops.contains { ($0["path"] as? [String]) == ["x","list"] })
        XCTAssertTrue(ops.contains { ($0["path"] as? [String]) == ["x","bool"] })
        let before = try XCTUnwrap(MacTab5Protocol.object(a)["data"] as? [String: Any])
        let after = try XCTUnwrap(MacTab5Protocol.object(b)["data"] as? [String: Any])
        XCTAssertTrue(try equal(apply(ops, to: before), after))
    }
    func testIdentitySequenceOtaAndDisplayCommandsUseFullFrames() throws {
        let a = try frame(10)
        for b in [try frame(11, device: "ffeeddccbbaa"), try frame(11, session: String(repeating: "c", count: 32)),
                  try frame(10), try frame(9), try frame(11, data: ["ota": ["fixture": 1]]),
                  try frame(11, data: ["connectionHealth": ["displayCommand": ["fixture": 1]]])] {
            var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 10)
            XCTAssertEqual(try delta.wire(b, packed: false, supported: true), b)
        }
        for key in ["ota", "connectionHealth"] {
            let value: Any = key == "ota" ? ["fixture": 1] : ["displayCommand": ["fixture": 1]]
            var delta = MacTab5BLEDelta(); delta.acknowledge(try frame(10, data: [key: value]), sequence: 10)
            let b = try frame(11); XCTAssertEqual(try delta.wire(b, packed: false, supported: true), b)
        }
    }
    func testOperationPathAndTreeLimitsFallBackWithoutTruncation() throws {
        let a = try frame(1)
        for count in [256,257] {
            let values = Dictionary(uniqueKeysWithValues: (0..<count).map { ("k\($0)", $0) })
            let b = try frame(2, data: values)
            var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 1)
            XCTAssertEqual(try type(delta.wire(b, packed: false, supported: true)), count == 256 ? "tab5_delta" : "tab5_status")
        }
        for key in ["", String(repeating: "x", count: 129), "bad\0key"] {
            let b = try frame(2, data: [key: 1]); var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 1)
            XCTAssertEqual(try delta.wire(b, packed: false, supported: true), b)
        }
        let b = try frame(2, data: ["tooMany": Array(repeating: 0, count: 8193)])
        var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 1)
        XCTAssertEqual(try delta.wire(b, packed: false, supported: true), b)
        var nested: Any = 1
        for _ in 0..<17 { nested = ["child": nested] }
        let deep = try frame(2, data: ["deep": nested])
        XCTAssertEqual(try delta.wire(deep, packed: false, supported: true), deep)
    }
    func testDeltaOnlyWinsWhenSmallerThanPackedFullAndUsesExistingEncryption() throws {
        let a = try frame(1), b = try frame(2, data: ["epochMilliseconds": 99])
        var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 1)
        let wire = try delta.wire(b, packed: true, supported: true)
        XCTAssertLessThanOrEqual(wire.count, try MacTab5BLEStatus.wire(b, supported: true).count)
        let raw = try delta.wire(b, packed: false, supported: true)
        XCTAssertEqual(try type(raw), "tab5_delta")
        let encrypted = try MacTab5Protocol.encrypt(raw, key: pair.key, challenge: nonce, nonce: Data(repeating: 7, count: 12))
        let parts = try MacTab5BLEWire.fragments(encrypted, maximum: 182, lengthPrefix: true)
        let joined = parts.reduce(into: Data()) { $0.append($1) }
        XCTAssertEqual(Int(MacTab5Mailbox.u32(joined, 0)), encrypted.count)
        XCTAssertEqual(try MacTab5Protocol.decrypt(Data(joined.dropFirst(4)), key: pair.key, challenge: nonce), raw)
        let changed = try frame(3, data: ["stable": String(repeating: "new", count: 3000)])
        XCTAssertEqual(try delta.wire(changed, packed: true, supported: true), try MacTab5BLEStatus.wire(changed, supported: true))
    }
    func testIndependentConnectionsNullOffersAndInvalidStates() throws {
        let a = try frame(1, data: ["ota": NSNull(), "connectionHealth": ["displayCommand": NSNull()]])
        let b = try frame(2, data: ["ota": NSNull(), "connectionHealth": ["displayCommand": NSNull()], "x": 1])
        var first = MacTab5BLEDelta(), second = MacTab5BLEDelta(); first.acknowledge(a, sequence: 1)
        XCTAssertEqual(try type(first.wire(b, packed: false, supported: true)), "tab5_delta")
        XCTAssertEqual(try second.wire(b, packed: false, supported: true), b)
        for data in [["codex": NSNull()], ["claude": []], ["bad": "embedded\0nul"]] as [[String: Any]] {
            let invalid = try frame(2, data: data)
            XCTAssertEqual(try first.wire(invalid, packed: false, supported: true), invalid)
            second.acknowledge(invalid, sequence: 2)
            let next = try frame(3); XCTAssertEqual(try second.wire(next, packed: false, supported: true), next)
        }
        let diagnostics = MacTab5BLEDiagnostics()
        diagnostics.voiceLink(credits: Int.max, available: true, subscribed: false)
        XCTAssertTrue(diagnostics.summary.contains("额度 8 · 通知能力 有 · 订阅 未确认"))
        diagnostics.voiceLink(credits: -1, available: false, subscribed: false)
        XCTAssertTrue(diagnostics.summary.contains("额度 0 · 通知能力 无"))
        diagnostics.voiceLink(credits: 8, available: true, subscribed: false, failure: .att, code: 5)
        XCTAssertTrue(diagnostics.summary.contains("错误 ATT/5"))
        diagnostics.voiceLink(credits: 8, available: true, subscribed: false, failure: .other, code: Int.max)
        XCTAssertTrue(diagnostics.summary.contains("错误 其他/0"))
    }
    func testPackedDeltaLargerThanPackedFullBeforeCompressionReconstructsExactState() throws {
        let stable = (0..<100).map { MacTab5Protocol.sha256(Data("synthetic-catalog-\($0)".utf8)) }.joined()
        let oldMetrics = Dictionary(uniqueKeysWithValues: (0..<80).map { ("channel\($0)", "old") })
        let newMetrics = Dictionary(uniqueKeysWithValues: (0..<80).map { ("channel\($0)", String(repeating: "更新🙂", count: 8)) })
        let a = try frame(10, data: ["stable": stable, "systemMetrics": oldMetrics, "gone": 1])
        let b = try frame(20, data: ["stable": stable, "systemMetrics": newMetrics, "nullable": NSNull(), "array": [true, 1]])
        var delta = MacTab5BLEDelta(); delta.acknowledge(a, sequence: 10)
        let raw = try delta.wire(b, packed: false, supported: true)
        let fallback = try MacTab5BLEStatus.wire(b, supported: true)
        XCTAssertTrue(raw.count > fallback.count)
        let wire = try delta.wire(b, packed: true, supported: true)
        XCTAssertTrue(wire.count < fallback.count)
        let envelope = try MacTab5Protocol.object(wire)
        XCTAssertEqual(envelope["type"] as? String, "tab5_packed")
        XCTAssertEqual(envelope["sequence"] as? Int, 20)
        let size = try XCTUnwrap(envelope["size"] as? Int)
        let compressed = try XCTUnwrap(Data(base64Encoded: try XCTUnwrap(envelope["payload"] as? String)))
        var expanded = [UInt8](repeating: 0, count: size), count = uLongf(size), consumed = uLong(compressed.count)
        let result = compressed.withUnsafeBytes { (source: UnsafeRawBufferPointer) in
            expanded.withUnsafeMutableBufferPointer { target in
                uncompress2(target.baseAddress, &count, source.bindMemory(to: UInt8.self).baseAddress, &consumed)
            }
        }
        XCTAssertEqual(result, Z_OK); XCTAssertEqual(Int(count), size); XCTAssertEqual(Int(consumed), compressed.count)
        let decoded = try MacTab5Protocol.object(Data(expanded))
        XCTAssertEqual(decoded["type"] as? String, "tab5_delta")
        XCTAssertEqual(decoded["baseSequence"] as? Int, 10)
        XCTAssertEqual(Data(expanded), raw)
        let operations = try XCTUnwrap(decoded["changes"] as? [[String: Any]])
        XCTAssertTrue(try equal(apply(operations, to: XCTUnwrap(MacTab5Protocol.object(a)["data"] as? [String: Any])),
                                XCTUnwrap(MacTab5Protocol.object(b)["data"] as? [String: Any])))
        // Encoding is not an ACK; another frame still refers to baseline 10.
        let next = try frame(21, data: ["stable": stable])
        XCTAssertEqual(try MacTab5Protocol.object(delta.wire(next, packed: false, supported: true))["baseSequence"] as? Int, 10)
        delta.reset()
        XCTAssertEqual(try delta.wire(b, packed: true, supported: true), fallback)
    }
}
