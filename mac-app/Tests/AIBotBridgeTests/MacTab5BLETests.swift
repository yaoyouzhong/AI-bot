import XCTest
@testable import AIBotBridge

final class MacTab5BLETests: XCTestCase {
    private let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.usbmodem-test", key: Data(0..<32), confirmed: true)
    private let nonce = String(repeating: "a", count: 32)
    private func info(ack: Int64 = 3) throws -> Data {
        try MacTab5Protocol.json(["version": 1, "deviceId": pair.deviceId, "nonce": nonce, "ack": ack,
                                 "proof": MacTab5Protocol.proof(key: pair.key, message: "\(nonce)|\(ack)")])
    }
    func testSignedIdentityAndExactWholeFrameAcknowledgement() throws {
        let (challenge, ack, _) = try MacTab5BLEWire.authenticate(info(), pair: pair, nonce: nonce)
        XCTAssertEqual(challenge, nonce); XCTAssertEqual(ack, 3)
        XCTAssertThrowsError(try MacTab5BLEWire.authenticate(info(), pair: pair, nonce: String(repeating: "b", count: 32)))
        let wrong = MacTab5Pairing(deviceId: "ffffffffffff", usbPath: pair.usbPath, key: pair.key, confirmed: true)
        XCTAssertThrowsError(try MacTab5BLEWire.authenticate(info(), pair: wrong))
    }
    func testForgedAckAndInvalidIdentityAreRejected() throws {
        var root = try MacTab5Protocol.object(info()); root["ack"] = 4
        XCTAssertThrowsError(try MacTab5BLEWire.authenticate(MacTab5Protocol.json(root), pair: pair))
        root["ack"] = true
        XCTAssertThrowsError(try MacTab5BLEWire.authenticate(MacTab5Protocol.json(root), pair: pair))
        XCTAssertThrowsError(try MacTab5BLEWire.authenticate(Data(repeating: 0, count: 2049), pair: pair))
    }
    func testFragmentsFollowActualWriteCapacityAndFirmwareBound() throws {
        let packet = Data((0..<4097).map { UInt8($0 % 251) })
        for maximum in [20, 64, 182, 244, 488, 512] {
            let chunks = try MacTab5BLEWire.fragments(packet, maximum: maximum, lengthPrefix: true)
            XCTAssertTrue(chunks.allSatisfy { $0.count <= min(maximum, 488) })
            let joined = chunks.reduce(into: Data()) { $0.append($1) }
            XCTAssertEqual(MacTab5Mailbox.u32(joined, 0), UInt32(packet.count))
            XCTAssertEqual(Data(joined.dropFirst(4)), packet)
        }
        XCTAssertThrowsError(try MacTab5BLEWire.fragments(packet, maximum: 0, lengthPrefix: true))
    }
    func testBluetoothConfirmedConnectionAllowsSettingsWithoutUSBOrLAN() throws {
        let service = MacTab5Service(port: 8765, load: { self.pair }, save: { _ in })
        service.bleState("confirmed", confirmed: true)
        var failure = false
        service.displaySettings(nil) { if case .failure = $0 { failure = true } }
        XCTAssertTrue(service.configurationBusy); XCTAssertFalse(failure)
        service.stop(); XCTAssertTrue(failure)
    }
    func testBluetoothStandbyNeverMakesUSBAppearConnected() {
        let service = MacTab5Service(port: 8765, load: { self.pair }, save: { _ in })
        service.bleState("confirmed", confirmed: true); XCTAssertFalse(service.usbActive)
        service.usbState("confirmed", confirmed: true); XCTAssertTrue(service.usbActive)
        service.usbState("disconnected"); XCTAssertFalse(service.usbActive)
    }
    func testUSBControlDoesNotOverrideDeviceSelectedBLE() {
        let service = MacTab5Service(port:8765,load:{ self.pair },save:{ _ in })
        defer { service.stop() }
        service.usbState("confirmed",confirmed:true)
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":"BLE"])
        XCTAssertTrue(service.usbActive); XCTAssertFalse(service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":"USB"])
        XCTAssertTrue(service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":"ffffffffffff","active":"BLE"])
        XCTAssertTrue(service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":pair.deviceId])
        XCTAssertFalse(service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":"USB"])
        service.usbState("disconnected")
        service.usbState("confirmed",confirmed:true)
        XCTAssertFalse(service.usbOwnsActiveState)
    }
    func testUSBOwnershipRequiresFreshDeviceSelection() {
        XCTAssertTrue(MacTab5USBDisplayOwnership.owns(now:107.999,acknowledgedAt:100,active:"USB"))
        for (now,ack) in [(108.0,100.0),(100,101),(100,0),(100,-Double.infinity)] {
            XCTAssertFalse(MacTab5USBDisplayOwnership.owns(now:now,acknowledgedAt:ack,active:"USB"))
        }
        for active in ["BLE","Wi-Fi","Offline","unknown",nil] {
            XCTAssertFalse(MacTab5USBDisplayOwnership.owns(now:100,acknowledgedAt:100,active:active))
        }
    }
}
