import XCTest
@testable import AIBotBridge

final class MacTab5ResourceTests: XCTestCase {
    func testRLE565MatchesDeviceLittleEndianRunsAndCRC() throws {
        let asset = try MacTab5Asset.rgb565(slot: 0, width: 3, height: 1, delays: [100], raw: Data([0, 248, 0, 248, 224, 7]))
        XCTAssertEqual(asset.bytes, Data([2, 0, 0, 248, 1, 0, 224, 7]))
        XCTAssertEqual(asset.id, String(format: "%08x", MacBinaryResourceProtocol.crc32(Array(asset.bytes))))
        XCTAssertThrowsError(try MacTab5Asset.rgb565(slot: 0, width: 121, height: 1, delays: [100], raw: Data(repeating: 0, count: 242)))
    }
    func testResourceReassemblyAndAckStopOnlyMatchingAsset() throws {
        let store = MacTab5Assets()
        let raw = Data((0..<(112 * 112 * 2)).map { UInt8($0 % 251) })
        let pet = MacPetAnimation(width: 112, height: 112, delays: [100], frames: [raw])
        store.update(pets: [pet, nil], cover: nil)
        let fallback = try XCTUnwrap(MacByteSprout.animation)
        let expected = [
            try MacTab5Asset.rgb565(slot: 0, width: 112, height: 112, delays: [100], raw: raw),
            try MacTab5Asset.rgb565(slot: 1, width: fallback.width, height: fallback.height,
                                   delays: fallback.delays, raw: fallback.frames.reduce(into: Data()) { $0.append($1) })
        ]
        var results = [Data(), Data()], acknowledged = ["-", "-", "-"]
        // Both slots now have assets. Reassemble by slot, with bounded iteration,
        // and acknowledge each independently while the other keeps transferring.
        for _ in 0..<100 where store.hasPending {
            let frame = store.projection(), part = try XCTUnwrap(frame["resource"] as? [String: Any])
            let slot = try XCTUnwrap(part["slot"] as? Int)
            guard (0..<2).contains(slot) else { return XCTFail("Unexpected resource slot") }
            XCTAssertEqual(part["id"] as? String, expected[slot].id)
            XCTAssertEqual(part["offset"] as? Int, results[slot].count)
            results[slot].append(try XCTUnwrap(Data(base64Encoded: part["bytes"] as! String)))
            if results[slot].count == expected[slot].bytes.count {
                acknowledged[slot] = expected[slot].id
                store.acknowledge(acknowledged.joined(separator: ","))
                store.acknowledge("../../file,-,-") // Invalid acknowledgements cannot undo the valid one.
            }
        }
        XCTAssertEqual(results[0], expected[0].bytes); XCTAssertEqual(results[1], expected[1].bytes)
        XCTAssertFalse(store.hasPending)
        XCTAssertNil(store.projection()["resource"])
        store.acknowledge("../../file,-,-"); XCTAssertNil(store.projection()["resource"])
    }
    func testCoverAvailabilityTracksRealResource() {
        let store = MacTab5Assets(); XCTAssertFalse(store.hasCover)
        store.update(pets: [], cover: Data(repeating: 0, count: 112 * 112 * 2)); XCTAssertTrue(store.hasCover)
        store.update(pets: [], cover: nil); XCTAssertFalse(store.hasCover)
    }
    func testBinaryRPCPayloadsRoundTripAtLegacyAndBulkLimits() throws {
        for bulk in [false, true] {
            let bytes = Data(repeating: 0xa5, count: bulk ? 49152 : 8192)
            let metadata: [String: Any] = ["kind": "image-chunk", "offset": 0]
            let wire = try MacTab5Binary.pack(metadata, bytes: bytes, bulk: bulk, response: false)
            let (opened, raw) = try MacTab5Binary.open(wire)
            XCTAssertEqual(opened["kind"] as? String, "image-chunk"); XCTAssertEqual(raw, bytes)
            XCTAssertThrowsError(try MacTab5Binary.pack(metadata, bytes: bytes + Data([1]), bulk: bulk, response: false))
        }
    }
    func testBinaryReplyPreservesMetadataAndRejectsDuplicateData() throws {
        let reply = MacTab5RPCResult(200, ["offset": 25], bytes: Data([0, 255, 10]))
        let encoded = try MacTab5Binary.encode(reply, binary: true, bulk: false)
        let (root, raw) = try MacTab5Binary.open(encoded, response: true)
        XCTAssertEqual((root["body"] as? [String: Any])?["offset"] as? Int, 25); XCTAssertEqual(raw, reply.bytes)
        XCTAssertThrowsError(try MacTab5Binary.pack(["body": ["data": "bad"]], bytes: reply.bytes, bulk: false))
        for short in [Data("T5R1".utf8), Data("T5R2\0\0\0\0".utf8)] { XCTAssertThrowsError(try MacTab5Binary.open(short)) }
    }
    func testLargeAuthenticatedPacketsAndMailboxStayWithin16BitBounds() throws {
        let key = Data(0..<32), nonce = String(repeating: "a", count: 32)
        let clear = Data(repeating: 7, count: 60000)
        let packet = try MacTab5Protocol.encrypt(clear, key: key, challenge: nonce, maximum: 65535)
        XCTAssertEqual(try MacTab5Protocol.decrypt(packet, key: key, challenge: nonce), clear)
        let parts = try MacTab5Mailbox.response(packet, id: 7)
        XCTAssertEqual(parts.reduce(0) { $0 + $1.count - 9 }, packet.count)
        XCTAssertThrowsError(try MacTab5Mailbox.response(Data(repeating: 0, count: 65536), id: 7))
    }
}
