import Foundation
import XCTest
@testable import AIBotBridge

final class MacTab5HostBindingTests: XCTestCase {
    private let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.fixture", key: Data(repeating: 0x11, count: 32), confirmed: true)
    private let nonce = "0123456789abcdef0123456789abcdef"
    private func hello(_ challenge: String? = nil, legacy: Bool = false) -> [String: Any] {
        var root: [String: Any] = ["version": 1, "type": "tab5_hello", "deviceId": pair.deviceId]
        if !legacy { root["hostBindingVersion"] = 1; root["hostNonce"] = challenge ?? nonce }
        return root
    }
    private func ack(_ challenge: String) -> [String: Any] {
        ["type": "tab5_host_claimed", "deviceId": pair.deviceId, "hostId": MacTab5HostBinding.id(pair),
         "proof": MacTab5Protocol.proof(key: pair.key, message: "TAB5_HOST_ACK_V1|\(pair.deviceId)|\(MacTab5HostBinding.id(pair))|\(challenge)")]
    }
    private func info(_ sequence: Int64, challenge: String? = nil) throws -> Data {
        let n = challenge ?? nonce
        return try MacTab5Protocol.json(["deviceId": pair.deviceId, "nonce": n, "ack": sequence,
            "proof": MacTab5Protocol.proof(key: pair.key, message: "\(n)|\(sequence)")])
    }

    func testIndependentHostAndSignatureVectors() throws {
        XCTAssertEqual(MacTab5HostBinding.id(pair), "246dd3f8363795589894eb1dc61b2e34")
        let root = try MacTab5HostBinding.claim(pair, nonce: nonce, address: "192.168.1.20", port: 18765, name: "Mac")
        XCTAssertEqual(root["proof"] as? String, "da7b2c6363db1a6564621b458def723a15382d74da5ee4cd082914c621140ce7")
        XCTAssertEqual(ack(nonce)["proof"] as? String, "c1d1b94963d8918eeb2ea194fd7e735a731029cbfa31985e135aa3bff0c97b21")
        try MacTab5HostBinding.confirm(ack(nonce), pair: pair, nonce: nonce)
        XCTAssertEqual(pair.key, Data(repeating: 0x11, count: 32))
    }
    func testAckTamperingForeignKeyAndStaleNonceFail() throws {
        for (field, value) in [("type", "tab5_paired"), ("deviceId", "ffffffffffff"), ("hostId", String(repeating: "f", count: 32)), ("proof", String(repeating: "0", count: 64))] {
            var root = ack(nonce); root[field] = value
            XCTAssertThrowsError(try MacTab5HostBinding.confirm(root, pair: pair, nonce: nonce))
        }
        let other = MacTab5Pairing(deviceId: pair.deviceId, usbPath: pair.usbPath, key: Data(repeating: 0x22, count: 32), confirmed: true)
        XCTAssertThrowsError(try MacTab5HostBinding.confirm(ack(nonce), pair: other, nonce: nonce))
        XCTAssertThrowsError(try MacTab5HostBinding.confirm(ack(nonce), pair: pair, nonce: String(repeating: "b", count: 32)))
        XCTAssertThrowsError(try MacTab5HostBinding.confirm(["error": "device_busy"], pair: pair, nonce: nonce)) { XCTAssertEqual($0 as? MacTab5Error, .busy) }
        let original = try MacTab5HostBinding.claim(pair, nonce: nonce, address: "192.168.1.20", port: 18765, name: "Mac")
        for (address, port, name) in [("192.168.1.21", UInt16(18765), "Mac"), ("192.168.1.20", UInt16(18766), "Mac"), ("192.168.1.20", UInt16(18765), "Other")] {
            let changed = try MacTab5HostBinding.claim(pair, nonce: nonce, address: address, port: port, name: name)
            XCTAssertNotEqual(changed["proof"] as? String, original["proof"] as? String)
        }
    }
    func testNameAndEndpointBoundaries() throws {
        XCTAssertEqual(MacTab5HostBinding.name(""), "Mac")
        XCTAssertEqual(MacTab5HostBinding.name("a|b\nc\u{0085}"), "a b c ")
        XCTAssertLessThanOrEqual(MacTab5HostBinding.name(String(repeating: "电脑😀", count: 50)).utf8.count, 63)
        for address in ["8.8.8.8", "127.0.0.1", "bad|endpoint"] {
            XCTAssertThrowsError(try MacTab5HostBinding.claim(pair, nonce: nonce, address: address, port: 8765, name: "Mac"))
        }
        for name in ["", "bad|name", "bad\nname", String(repeating: "x", count: 64)] {
            XCTAssertThrowsError(try MacTab5HostBinding.claim(pair, nonce: nonce, address: "", port: 0, name: name))
        }
        _ = try MacTab5HostBinding.claim(pair, nonce: nonce, address: "", port: 0, name: "Mac")
    }
    func testUSBTakesOwnershipOnlyWithSignedAckAndRefreshesEndpoint() throws {
        var session = MacTab5USBHostSession(); try session.observe(hello())
        var sent: [[String: Any]] = [], refreshes = 0
        let next = String(repeating: "b", count: 32)
        func refresh() -> [String: Any] { refreshes += 1; return hello(next) }
        func exchange(_ root: [String: Any]) -> [String: Any] { sent.append(root); return ack(root["nonce"] as! String) }
        try session.authenticate(pair, address: "192.168.1.20", port: 8765, name: "Mac", refresh: refresh, exchange: exchange)
        try session.authenticate(pair, address: "192.168.1.20", port: 8765, name: "Mac", refresh: refresh, exchange: exchange)
        XCTAssertEqual(sent.count, 1); XCTAssertEqual(refreshes, 0)
        try session.authenticate(pair, address: "192.168.1.21", port: 8765, name: "Mac", refresh: refresh, exchange: exchange)
        XCTAssertEqual(sent.count, 2); XCTAssertEqual(refreshes, 1)
        XCTAssertEqual(sent.last?["nonce"] as? String, next)
        XCTAssertTrue(sent.allSatisfy { $0["type"] as? String == "tab5_host_claim" })
        var reconnected = MacTab5USBHostSession(); try reconnected.observe(hello(next))
        try reconnected.authenticate(pair, address: "", port: 0, name: "Mac", refresh: refresh, exchange: exchange)
        XCTAssertEqual(sent.count, 3) // A prior connection never carries admission forward.
    }
    func testUnknownUSBHostNeverEnrollsOrReusesUncertainChallenge() throws {
        var session = MacTab5USBHostSession(); try session.observe(hello())
        var sent: [String] = [], refreshed = false
        func reject(_ root: [String: Any]) -> [String: Any] { sent.append(root["type"] as! String); return ["type": "tab5_host_claimed", "error": "not_trusted"] }
        XCTAssertThrowsError(try session.authenticate(pair, address: "", port: 0, name: "Mac", refresh: { self.hello() }, exchange: reject))
        let next = String(repeating: "b", count: 32)
        try session.authenticate(pair, address: "", port: 0, name: "Mac", refresh: { refreshed = true; return self.hello(next) }, exchange: { root in
            XCTAssertEqual(root["nonce"] as? String, next); return self.ack(next)
        })
        XCTAssertTrue(refreshed); XCTAssertEqual(sent, ["tab5_host_claim"])
        var timedOut = MacTab5USBHostSession(); try timedOut.observe(hello())
        XCTAssertThrowsError(try timedOut.authenticate(pair, address: "", port: 0, name: "Mac", refresh: { self.hello() }, exchange: { _ in throw MacTab5Error.timeout }))
        XCTAssertThrowsError(try timedOut.authenticate(pair, address: "", port: 0, name: "Mac", refresh: { throw MacTab5Error.timeout }, exchange: { _ in XCTFail(); return [:] }))
    }
    func testLegacyUSBAndInvalidCapabilitiesFailClosed() throws {
        var legacy = MacTab5USBHostSession(); try legacy.observe(hello(legacy: true))
        XCTAssertFalse(legacy.required)
        try legacy.authenticate(pair, address: "", port: 0, name: "Mac", refresh: { XCTFail(); return [:] }, exchange: { _ in XCTFail(); return [:] })
        var current = MacTab5USBHostSession(); try current.observe(hello())
        XCTAssertThrowsError(try current.observe(hello(legacy: true)))
        for invalid: Any in [true, "1", 2] {
            var root = hello(); root["hostBindingVersion"] = invalid
            XCTAssertThrowsError(try current.observe(root))
        }
        var wrong = hello(); wrong["deviceId"] = "ffffffffffff"
        XCTAssertThrowsError(try current.observe(wrong))
    }
    func testOwnerHintIsOnlyAdmissionHintAndForeignOwnerWaits() throws {
        var gate = MacTab5BLEAdmission(); gate.begin(pair)
        let bytes = try MacTab5Protocol.json(["version": 1, "hostId": MacTab5HostBinding.id(pair), "bleProofRequired": 1])
        let hint = try MacTab5BLEWire.ownerHint(bytes, pair: pair)
        XCTAssertTrue(hint.selected); XCTAssertTrue(hint.proofRequired); XCTAssertFalse(gate.ready)
        let other = MacTab5Pairing(deviceId: pair.deviceId, usbPath: pair.usbPath, key: Data(repeating: 0x22, count: 32), confirmed: true)
        XCTAssertFalse(try MacTab5BLEWire.ownerHint(bytes, pair: other).selected)
        XCTAssertFalse(try MacTab5BLEWire.ownerHint(MacTab5Protocol.json(["version": 1, "hostId": ""]), pair: pair).selected)
        XCTAssertThrowsError(try MacTab5BLEWire.ownerHint(MacTab5Protocol.json(["version": 1, "hostId": MacTab5HostBinding.id(pair), "bleProofRequired": true]), pair: pair))
    }
    func testShortProofExactFieldsSafeSequenceAndLowMTUEncryption() throws {
        let clear = try MacTab5BLEWire.ownershipProof(pair, sequence: 42), root = try MacTab5Protocol.object(clear)
        XCTAssertEqual(Set(root.keys), Set(["version", "type", "deviceId", "hostId", "sequence"]))
        XCTAssertEqual(root["type"] as? String, "tab5_ble_owner")
        XCTAssertEqual(root["deviceId"] as? String, pair.deviceId)
        XCTAssertEqual(root["hostId"] as? String, MacTab5HostBinding.id(pair))
        XCTAssertThrowsError(try MacTab5BLEWire.ownershipProof(pair, sequence: 0))
        XCTAssertThrowsError(try MacTab5BLEWire.ownershipProof(pair, sequence: 9_007_199_254_740_992))
        _ = try MacTab5BLEWire.ownershipProof(pair, sequence: 9_007_199_254_740_991)
        let encrypted = try MacTab5Protocol.encrypt(clear, key: pair.key, challenge: nonce)
        let chunks = try MacTab5BLEWire.fragments(encrypted, maximum: 20, lengthPrefix: true)
        XCTAssertTrue(chunks.allSatisfy { $0.count <= 20 })
        let joined = chunks.reduce(into: Data()) { $0.append($1) }
        XCTAssertEqual(MacTab5Mailbox.u32(joined, 0), UInt32(encrypted.count))
        XCTAssertEqual(try MacTab5Protocol.decrypt(Data(joined.dropFirst(4)), key: pair.key, challenge: nonce), clear)
    }
    func testSignedOwnerAckPrecedesRPCAndUSBStandby() throws {
        var gate = MacTab5BLEAdmission(); gate.begin(pair); let generation = gate.generation
        try gate.authenticated(generation, pair: pair, requiresProof: true)
        XCTAssertFalse(gate.ready)
        XCTAssertFalse(try gate.acknowledge(info(41), generation: generation, pair: pair, nonce: nonce, sequence: 42))
        XCTAssertFalse(gate.ready)
        XCTAssertThrowsError(try gate.acknowledge(info(42, challenge: String(repeating: "b", count: 32)), generation: generation, pair: pair, nonce: nonce, sequence: 42))
        var forged = try MacTab5Protocol.object(info(41)); forged["ack"] = 42
        XCTAssertThrowsError(try gate.acknowledge(MacTab5Protocol.json(forged), generation: generation, pair: pair, nonce: nonce, sequence: 42))
        XCTAssertTrue(try gate.acknowledge(info(42), generation: generation, pair: pair, nonce: nonce, sequence: 42))
        XCTAssertTrue(gate.ready)
    }
    func testTimeoutReconnectRePairAndLateCallbackCannotRestoreAdmission() throws {
        var gate = MacTab5BLEAdmission(); gate.begin(pair); let old = gate.generation
        try gate.authenticated(old, pair: pair, requiresProof: true)
        gate.reset() // Transport timeout/disconnect uses this same invalidation.
        XCTAssertFalse(gate.ready)
        XCTAssertThrowsError(try gate.acknowledge(info(42), generation: old, pair: pair, nonce: nonce, sequence: 42))
        gate.begin(pair)
        XCTAssertFalse(gate.accepts(old, pair: pair))
        XCTAssertThrowsError(try gate.authenticated(old, pair: pair, requiresProof: false))
        let changed = MacTab5Pairing(deviceId: pair.deviceId, usbPath: pair.usbPath, key: Data(repeating: 0x22, count: 32), confirmed: true)
        XCTAssertFalse(gate.accepts(gate.generation, pair: changed))
        try gate.authenticated(gate.generation, pair: pair, requiresProof: false)
        XCTAssertTrue(gate.ready) // Old firmware has no short-proof capability.
    }
    func testProofSharesSequenceWithoutPublishingOrRenewingUSB() throws {
        let service = MacTab5Service(port: 8765, load: { self.pair }, save: { _ in XCTFail() })
        let first = try service.nextSequence(for: pair)
        let proof = try MacTab5BLEWire.ownershipProof(pair, sequence: first)
        XCTAssertEqual(MacTab5Protocol.integer(try MacTab5Protocol.object(proof)["sequence"]), 1)
        XCTAssertEqual(try service.nextSequence(for: pair), 2)
        XCTAssertFalse(service.usbActive); XCTAssertNil(service.bootTransport)
        let changed = MacTab5Pairing(deviceId: pair.deviceId, usbPath: pair.usbPath, key: Data(repeating: 0x22, count: 32), confirmed: true)
        XCTAssertThrowsError(try service.nextSequence(for: changed))
        XCTAssertEqual(service.currentPairing, pair)
        service.stop(); XCTAssertThrowsError(try service.nextSequence(for: pair))
    }
}
