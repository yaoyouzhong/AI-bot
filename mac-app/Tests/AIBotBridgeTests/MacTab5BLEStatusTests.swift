import XCTest
import zlib
@testable import AIBotBridge

final class MacTab5BLEStatusTests: XCTestCase {
    private let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.fixture", key: Data(0..<32), confirmed: true)
    private let nonce = String(repeating: "a", count: 32)
    private func info(_ firmware: String = "0.2.236-ui", telemetry: Int = 3) -> [String: Any] {
        ["version": 1, "deviceId": pair.deviceId, "nonce": nonce, "ack": 7,
         "proof": MacTab5Protocol.proof(key: pair.key, message: "\(nonce)|7"), "telemetryVersion": telemetry,
         "firmware": firmware, "firmwareProof": MacTab5Protocol.proof(key: pair.key, message: "FIRMWARE|\(nonce)|\(firmware)")]
    }
    private func full(padding: Int = 16000) throws -> Data {
        try MacTab5Protocol.json(["version": 1, "type": "tab5_status", "deviceId": pair.deviceId,
            "session": String(repeating: "b", count: 32), "sequence": 91,
            "data": ["fixture": String(repeating: "合成🙂", count: padding / 10)]])
    }
    private func expand(_ wire: Data) throws -> Data {
        let root = try MacTab5Protocol.object(wire)
        guard root["type"] as? String == "tab5_packed" else { return wire }
        let size = try XCTUnwrap(root["size"] as? Int)
        let input = try XCTUnwrap(Data(base64Encoded: try XCTUnwrap(root["payload"] as? String)))
        var output = [UInt8](repeating: 0, count: size), count = uLongf(size), consumed = uLong(input.count)
        let status = input.withUnsafeBytes { (source: UnsafeRawBufferPointer) in
            output.withUnsafeMutableBufferPointer { target in
                uncompress2(target.baseAddress, &count, source.bindMemory(to: UInt8.self).baseAddress, &consumed)
            }
        }
        XCTAssertEqual(status, Z_OK); XCTAssertEqual(Int(count), size); XCTAssertEqual(Int(consumed), input.count)
        return Data(output)
    }
    func testOnlyAuthenticatedCompressionSafeFirmwareAndKnownCapabilityEnablePacking() {
        for version in ["0.2.42-ui", "0.2.234-ui", "0.2.236-ui"] {
            for telemetry in [2, 3] { XCTAssertTrue(MacTab5BLEStatus.supports(info(version, telemetry: telemetry), pair: pair, nonce: nonce)) }
        }
        for version in ["0.2.40-ui", "0.2.41-ui", "0.2.042-ui", "0.2.-1-ui", "unknown", "1.0.0-ui", "0.2.236"] {
            XCTAssertFalse(MacTab5BLEStatus.supports(info(version), pair: pair, nonce: nonce))
        }
        for value in [0, 1, 4, true, "3", 2.5, NSNull()] as [Any] {
            var root = info(); root["telemetryVersion"] = value
            XCTAssertFalse(MacTab5BLEStatus.supports(root, pair: pair, nonce: nonce))
        }
    }
    func testMissingProofsWrongDeviceAndStaleNonceCannotEnablePacking() {
        for key in ["deviceId", "nonce", "ack", "proof", "telemetryVersion", "firmware", "firmwareProof"] {
            var root = info(); root.removeValue(forKey: key)
            XCTAssertFalse(MacTab5BLEStatus.supports(root, pair: pair, nonce: nonce))
        }
        for (key, value) in [("deviceId", "ffeeddccbbaa"), ("proof", String(repeating: "0", count: 64)),
                             ("firmwareProof", String(repeating: "0", count: 64)), ("firmware", "0.2.237-ui")] {
            var root = info(); root[key] = value
            XCTAssertFalse(MacTab5BLEStatus.supports(root, pair: pair, nonce: nonce))
        }
        var root = info(); root["ack"] = true
        XCTAssertFalse(MacTab5BLEStatus.supports(root, pair: pair, nonce: nonce))
        XCTAssertFalse(MacTab5BLEStatus.supports(info(), pair: pair, nonce: String(repeating: "b", count: 32)))
        let unconfirmed = MacTab5Pairing(deviceId: pair.deviceId, usbPath: pair.usbPath, key: pair.key, confirmed: false)
        XCTAssertFalse(MacTab5BLEStatus.supports(info(), pair: unconfirmed, nonce: nonce))
    }
    func testExistingZlibEnvelopePreservesExactExpandedBytesSequenceAndSize() throws {
        let original = try full(), wire = try MacTab5BLEStatus.wire(original, supported: true)
        let root = try MacTab5Protocol.object(wire)
        XCTAssertEqual(root["type"] as? String, "tab5_packed"); XCTAssertEqual(root["version"] as? Int, 1)
        XCTAssertEqual(root["sequence"] as? Int, 91); XCTAssertEqual(root["size"] as? Int, original.count)
        XCTAssertEqual(Set(root.keys), Set(["version", "type", "sequence", "size", "payload"]))
        XCTAssertEqual(try expand(wire), original); XCTAssertLessThanOrEqual(wire.count, original.count / 2)
    }
    func testUnnegotiatedSmallAndOtherMessageTypesRemainUnchanged() throws {
        let original = try full()
        XCTAssertEqual(try MacTab5BLEStatus.wire(original, supported: false), original)
        let small = try MacTab5Protocol.json(["version": 1, "type": "tab5_status", "sequence": 1])
        XCTAssertEqual(try MacTab5BLEStatus.wire(small, supported: true), small)
        for type in ["tab5_resources", "tab5_ble_owner", "tab5_metrics", "tab5_packed"] {
            var root = try MacTab5Protocol.object(original); root["type"] = type
            let other = try MacTab5Protocol.json(root)
            XCTAssertEqual(try MacTab5BLEStatus.wire(other, supported: true), other)
        }
        var bytes: [UInt8] = [], seed: UInt64 = 123
        for _ in 0..<4096 { seed = seed &* 6364136223846793005 &+ 1; bytes.append(UInt8(truncatingIfNeeded: seed >> 32)) }
        let noisy = try MacTab5Protocol.json(["version": 1, "type": "tab5_status", "sequence": 3, "data": Data(bytes).base64EncodedString()])
        let wire = try MacTab5BLEStatus.wire(noisy, supported: true)
        XCTAssertLessThanOrEqual(wire.count, noisy.count); XCTAssertEqual(try expand(wire), noisy)
    }
    func testCompressionCannotBypassExpandedSizeLimit() throws {
        let base: [String: Any] = ["version": 1, "type": "tab5_status", "sequence": 2, "data": ""]
        let count = try MacTab5Protocol.json(base).count, maximum = MacTab5Protocol.maximumFrame - 28
        var root = base; root["data"] = String(repeating: "x", count: maximum - count)
        let boundary = try MacTab5Protocol.json(root)
        XCTAssertEqual(boundary.count, maximum)
        XCTAssertEqual(try expand(MacTab5BLEStatus.wire(boundary, supported: true)), boundary)
        root["data"] = String(repeating: "x", count: maximum - count + 1)
        let oversized = try MacTab5Protocol.json(root)
        for supported in [true, false] { XCTAssertThrowsError(try MacTab5BLEStatus.wire(oversized, supported: supported)) }
        XCTAssertThrowsError(try MacTab5BLEStatus.wire(Data(), supported: true))
    }
    func testDeltaPackingRequiresExplicitSupportAndValidBaseline() throws {
        let delta: [String: Any] = ["version": 1, "type": "tab5_delta", "sequence": 9, "baseSequence": 8,
                                   "changes": [["path": ["fixture"], "value": String(repeating: "中文🙂", count: 400)]]]
        let raw = try MacTab5Protocol.json(delta)
        XCTAssertEqual(try MacTab5BLEStatus.wire(raw, supported: true), raw)
        XCTAssertEqual(try MacTab5BLEStatus.wire(raw, supported: false, allowDelta: true), raw)
        let packed = try MacTab5BLEStatus.wire(raw, supported: true, allowDelta: true)
        XCTAssertEqual(try expand(packed), raw)
        XCTAssertTrue(packed.count < raw.count)
        for base in [0, 9, 10, true, "8", NSNull()] as [Any] {
            var invalid = delta; invalid["baseSequence"] = base
            let bytes = try MacTab5Protocol.json(invalid)
            XCTAssertEqual(try MacTab5BLEStatus.wire(bytes, supported: true, allowDelta: true), bytes)
        }
        var oversized = delta; oversized["changes"] = [["path": ["fixture"], "value": String(repeating: "x", count: 32768)]]
        XCTAssertThrowsError(try MacTab5BLEStatus.wire(MacTab5Protocol.json(oversized), supported: true, allowDelta: true))
    }
    func testPackedStateUsesExistingEncryptionFragmentLengthAndOriginalAckSequence() throws {
        let original = try full(), wire = try MacTab5BLEStatus.wire(original, supported: true)
        let packet = try MacTab5Protocol.encrypt(wire, key: pair.key, challenge: nonce, nonce: Data(repeating: 7, count: 12))
        let parts = try MacTab5BLEWire.fragments(packet, maximum: 182, lengthPrefix: true)
        let joined = parts.reduce(into: Data()) { $0.append($1) }
        XCTAssertEqual(Int(MacTab5Mailbox.u32(joined, 0)), packet.count)
        let decrypted = try MacTab5Protocol.decrypt(Data(joined.dropFirst(4)), key: pair.key, challenge: nonce)
        XCTAssertEqual(try expand(decrypted), original)
        XCTAssertEqual(try MacTab5Protocol.object(decrypted)["sequence"] as? Int, 91)
        XCTAssertThrowsError(try MacTab5Protocol.decrypt(packet, key: pair.key, challenge: String(repeating: "c", count: 32)))
    }
}
