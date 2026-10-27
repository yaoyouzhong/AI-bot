import XCTest
@testable import AIBotBridge

final class Tab5USBLeaseTests: XCTestCase {
    func testIdleDeltaStreamKeepsInspectedReceiverOwnershipWithoutRPC() throws {
        XCTAssertEqual(try MacTab5USBLeaseValidation.run(), 11)
    }
}
