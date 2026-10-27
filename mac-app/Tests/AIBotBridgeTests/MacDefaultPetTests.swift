import XCTest
@testable import AIBotBridge

final class MacDefaultPetTests: XCTestCase {
    func testOriginalFallbackHasUprightPixelsAndTwoDistinctSteps() throws {
        let pet = try XCTUnwrap(MacByteSprout.animation)
        XCTAssertEqual(pet.width, 112); XCTAssertEqual(pet.height, 112)
        XCTAssertEqual(pet.delays, [240, 240]); XCTAssertEqual(pet.frames.count, 2)
        func pixel(_ frame: Int, _ x: Int, _ y: Int) -> UInt16 {
            MacTab5Mailbox.u16(pet.frames[frame], (y * pet.width + x) * 2)
        }
        XCTAssertEqual(pixel(0, 57, 8), 0x07ff) // Antenna at the top.
        XCTAssertEqual(pixel(0, 50, 30), 0x07e0) // Green eye, not vertically flipped.
        XCTAssertEqual(pixel(0, 0, 0), 0)
        XCTAssertEqual(pixel(0, 55, 100), 0x07ff)
        XCTAssertEqual(pixel(1, 55, 100), 0)
        XCTAssertNotEqual(pet.frames[0], pet.frames[1])
        let decoded = try MacPetAnimation(data: pet.encode())
        XCTAssertEqual(decoded.frames, pet.frames)
    }
    func testEmptyProfileSendsFallbackForBothSlotsWithoutCreatingSelectionFiles() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        let cache = MacPetCache(directory: directory), assets = MacTab5Assets()
        assets.updatePets([cache.selection("claude"), cache.selection("codex")])
        let ids = try XCTUnwrap(assets.projection()["assetIds"] as? [Any])
        let first = try XCTUnwrap(ids[0] as? String), second = try XCTUnwrap(ids[1] as? String)
        XCTAssertEqual(first, second); XCTAssertTrue(ids[2] is NSNull)
        XCTAssertTrue(assets.hasPending)
        XCTAssertFalse(FileManager.default.fileExists(atPath: directory.path))
        let chunks = assets.batch(8)
        XCTAssertFalse(chunks.isEmpty)
        XCTAssertTrue(chunks.allSatisfy { [0, 1].contains($0["slot"] as? Int ?? -1) && $0["frames"] as? Int == 2 })
        assets.acknowledge("\(first),\(second),-")
        XCTAssertFalse(assets.hasPending); XCTAssertNil(assets.projection()["resource"])
    }
    func testSelectedPetWinsAndTemporaryMissingSelectionRetainsLastGoodAsset() throws {
        let assets = MacTab5Assets()
        assets.updatePets([nil, nil])
        let original = try XCTUnwrap(assets.projection()["assetIds"] as? [Any])
        let selected = MacPetAnimation(width: 2, height: 2, delays: [120],
                                       frames: [Data([0, 0xf8, 0, 0xf8, 0, 0xf8, 0, 0xf8])])
        assets.updatePets([selected, nil])
        let updated = try XCTUnwrap(assets.projection()["assetIds"] as? [Any])
        XCTAssertNotEqual(updated[0] as? String, original[0] as? String)
        XCTAssertEqual(updated[1] as? String, original[1] as? String)
        assets.updatePets([nil, nil])
        let retained = try XCTUnwrap(assets.projection()["assetIds"] as? [Any])
        XCTAssertEqual(retained[0] as? String, updated[0] as? String)
        let broken = MacPetAnimation(width: 2, height: 2, delays: [120], frames: [Data()])
        assets.updatePets([broken, nil])
        XCTAssertEqual((assets.projection()["assetIds"] as? [Any])?[0] as? String, updated[0] as? String)
        // Out-of-range dimensions must also retain the prior selection.
        let oversized = MacPetAnimation(width: 121, height: 1, delays: [120], frames: [Data(repeating: 0, count: 242)])
        assets.updatePets([oversized, nil])
        XCTAssertEqual((assets.projection()["assetIds"] as? [Any])?[0] as? String, updated[0] as? String)
    }
}
