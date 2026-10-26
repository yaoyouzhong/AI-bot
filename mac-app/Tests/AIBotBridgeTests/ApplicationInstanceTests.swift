import XCTest
@testable import AIBotBridge

final class ApplicationInstanceTests: XCTestCase {
    func testExclusiveLifetimeAndSafeLockFile() throws {
        XCTAssertEqual(try MacApplicationInstanceValidation.run(), 6)
    }
}
