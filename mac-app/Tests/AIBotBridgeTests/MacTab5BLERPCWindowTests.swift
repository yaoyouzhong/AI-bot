import XCTest
@testable import AIBotBridge

final class MacTab5BLERPCWindowTests: XCTestCase {
    func testProductionEncryptedBulkWindowValidation() throws {
        XCTAssertGreaterThan(try MacTab5BLERPCWindowValidation.run(),30)
    }
}
