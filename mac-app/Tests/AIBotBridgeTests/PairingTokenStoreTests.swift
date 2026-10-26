import XCTest
@testable import AIBotBridge

final class PairingTokenStoreTests: XCTestCase {
    private enum Failure: Error { case denied, write }
    private let original = String(repeating: "a", count: 32)
    private let replacement = String(repeating: "b", count: 32)

    func testStartupAndRepeatedTransportReadsShareOneAuthorization() throws {
        var reads = 0, writes = 0, generations = 0
        let cache = MacPairingTokenCache(load: { reads += 1; return self.original }, persist: { _ in writes += 1 }, generate: { generations += 1; return self.replacement })
        XCTAssertEqual(try cache.loadOrCreate(), original)
        for _ in 0..<32 { XCTAssertEqual(cache.read(), original) }
        XCTAssertEqual(try cache.loadOrCreate(), original)
        XCTAssertEqual(reads, 1); XCTAssertEqual(writes, 0); XCTAssertEqual(generations, 0)
    }

    func testDeniedReadDoesNotRepromptOrReplaceExistingToken() throws {
        var reads = 0, writes = 0, generations = 0
        let cache = MacPairingTokenCache(load: { reads += 1; throw Failure.denied }, persist: { _ in writes += 1 }, generate: { generations += 1; return self.replacement })
        XCTAssertThrowsError(try cache.loadOrCreate())
        for _ in 0..<32 { XCTAssertNil(cache.read()) }
        XCTAssertThrowsError(try cache.loadOrCreate())
        XCTAssertEqual(reads, 1); XCTAssertEqual(writes, 0); XCTAssertEqual(generations, 0)
        // A user-initiated save can recover without background retries.
        try cache.save(replacement)
        XCTAssertEqual(cache.read(), replacement)
        XCTAssertEqual(reads, 1); XCTAssertEqual(writes, 1)
    }

    func testMissingTokenIsCreatedOnceAndExplicitRotationUpdatesReads() throws {
        var reads = 0, generations = 0, saved: [String] = []
        let cache = MacPairingTokenCache(load: { reads += 1; return nil }, persist: { saved.append($0) }, generate: { generations += 1; return self.original })
        XCTAssertEqual(try cache.loadOrCreate(), original)
        XCTAssertEqual(try cache.loadOrCreate(), original)
        try cache.save(replacement)
        XCTAssertEqual(cache.read(), replacement)
        XCTAssertEqual(reads, 1); XCTAssertEqual(generations, 1); XCTAssertEqual(saved, [original, replacement])
    }

    func testFailedSaveAndMalformedRecordNeverRotateWorkingCredentials() throws {
        let cache = MacPairingTokenCache(load: { self.original }, persist: { _ in throw Failure.write }, generate: { self.replacement })
        XCTAssertEqual(cache.read(), original)
        XCTAssertThrowsError(try cache.save(replacement))
        XCTAssertEqual(cache.read(), original)
        var writes = 0, generations = 0
        let malformed = MacPairingTokenCache(load: { "short" }, persist: { _ in writes += 1 }, generate: { generations += 1; return self.replacement })
        XCTAssertThrowsError(try malformed.loadOrCreate())
        XCTAssertEqual(writes, 0); XCTAssertEqual(generations, 0)
    }
}
