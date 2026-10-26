import XCTest
@testable import AIBotBridge

final class MacTab5BLEResourceTests: XCTestCase {
    private let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.fixture", key: Data(0..<32), confirmed: true)
    private func pet() -> MacPetAnimation {
        MacPetAnimation(width: 24, height: 24, delays: [120, 140],
                        frames: (0..<2).map { frame in Data((0..<1152).map { UInt8(($0 + frame) % 251) }) })
    }
    private func status(_ state: String) -> MacStatusSnapshot {
        MacStatusSnapshot(version: 1, time: "12:00:00", epochUtc: 1800000000, utcOffsetSeconds: 0,
                          capturedAt: Date(timeIntervalSince1970: 1800000000),
                          codex: ToolState(state: state, ageSeconds: nil), claude: ToolState(state: "idle", ageSeconds: nil),
                          musicPlaying: false, weather: nil, stocks: nil, systemMetrics: nil, quotas: nil, music: nil)
    }
    func testMetadataOnlyPollingDoesNotConsumeOrReorderPendingImageChunks() throws {
        let source = pet(), assets = MacTab5Assets()
        assets.updatePets([source, source])
        let expected = try MacTab5Asset.rgb565(slot: 0, width: source.width, height: source.height,
                                              delays: source.delays, raw: source.frames.reduce(into: Data()) { $0.append($1) })
        var received = [Data(), Data()]
        while received.contains(where: { $0.count < expected.bytes.count }) {
            for _ in 0..<20 {
                let metadata = assets.projection(includeResource: false)
                XCTAssertNil(metadata["resource"])
                XCTAssertEqual((metadata["assetIds"] as? [Any])?[0] as? String, expected.id)
                XCTAssertTrue(assets.hasPending)
            }
            let chunks = assets.batch(1)
            XCTAssertEqual(chunks.count, 1)
            let chunk = try XCTUnwrap(chunks.first), slot = try XCTUnwrap(chunk["slot"] as? Int)
            XCTAssertTrue((0...1).contains(slot))
            XCTAssertEqual(chunk["offset"] as? Int, received[slot].count)
            let bytes = try XCTUnwrap(Data(base64Encoded: try XCTUnwrap(chunk["bytes"] as? String)))
            XCTAssertLessThanOrEqual(bytes.count, 1024)
            received[slot].append(bytes)
        }
        XCTAssertEqual(received, [expected.bytes, expected.bytes])
        assets.acknowledge("\(expected.id),\(expected.id),-")
        XCTAssertFalse(assets.hasPending); XCTAssertTrue(assets.batch(1).isEmpty)
    }
    func testBLEFramesKeepFreshStatusAndAssetIDsWithoutInlineImageBytes() throws {
        let service = MacTab5Service(port: 8765, load: { self.pair }, save: { _ in })
        defer { service.stop() }
        service.assets.updatePets([pet(), pet()])
        service.publish(status("idle"))
        let before = try MacTab5Protocol.object(service.frame(includeResources: false))
        service.publish(status("working"))
        let after = try MacTab5Protocol.object(service.frame(includeResources: false))
        let data = try XCTUnwrap(after["data"] as? [String: Any])
        XCTAssertNil(data["resource"]); XCTAssertNotNil(data["assetIds"])
        XCTAssertNotNil(data["connectionHealth"]); XCTAssertNotNil(data["pcInput"])
        XCTAssertEqual((data["codex"] as? [String: Any])?["state"] as? String, "working")
        XCTAssertTrue(try XCTUnwrap(after["sequence"] as? Int) > XCTUnwrap(before["sequence"] as? Int))
        let resource = try MacTab5Protocol.object(service.resourceFrame(maximum: 1))
        let chunks = try XCTUnwrap(resource["resources"] as? [[String: Any]])
        XCTAssertEqual(chunks.count, 1); XCTAssertEqual(chunks[0]["offset"] as? Int, 0)
        XCTAssertEqual(resource["session"] as? String, after["session"] as? String)
        XCTAssertTrue(try XCTUnwrap(resource["sequence"] as? Int) > XCTUnwrap(after["sequence"] as? Int))
        // The USB/Wi-Fi default still carries resources, and the other slot
        // starts at zero rather than being skipped by metadata publication.
        let legacy = try MacTab5Protocol.object(service.frame())
        let inline = try XCTUnwrap((legacy["data"] as? [String: Any])?["resource"] as? [String: Any])
        XCTAssertEqual(inline["offset"] as? Int, 0)
        XCTAssertNotEqual(inline["slot"] as? Int, chunks[0]["slot"] as? Int)
        let batch = try MacTab5Protocol.object(service.resourceFrame())
        XCTAssertEqual((batch["resources"] as? [[String: Any]])?.count, 4)
    }
}
