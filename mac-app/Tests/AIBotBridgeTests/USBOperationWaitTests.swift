import XCTest
@testable import AIBotBridge

final class USBOperationWaitTests: XCTestCase {
    func testSlowHostOperationRetainsLeaseAndStopsWithoutReplay() throws {
        XCTAssertEqual(try MacTab5USBOperationWaitValidation.run(), 20)
    }
}
