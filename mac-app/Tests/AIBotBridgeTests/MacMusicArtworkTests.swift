import XCTest
@testable import AIBotBridge

final class MacMusicArtworkTests: XCTestCase {
    func testSyntheticArtworkDirectionsAndFallback() throws {
        XCTAssertGreaterThan(try MacMusicArtworkValidation.run(), 20)
    }

    func testSamplingContinuesWhileArtworkIsPending() async throws {
        let count = try await MacMusicService.validateSampling()
        XCTAssertEqual(count, 9)
    }
}
