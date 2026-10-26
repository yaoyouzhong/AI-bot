import XCTest
@testable import AIBotBridge

final class MacGalleryConnectionTests: XCTestCase {
    func testGalleryGuidesCancellationNetworkAndBenchmarkContracts() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at:root) }
        XCTAssertGreaterThanOrEqual(try MacGalleryConnectionValidation.run(root:root),17)
    }
}
