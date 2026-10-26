import XCTest
@testable import AIBotBridge

final class Tab5USBResourceBurstTests: XCTestCase {
    func testResourceTransfersYieldForTimePriorityWorkAndErrors() throws {
        XCTAssertEqual(try MacTab5USBResourceBurstValidation.run(), 7)
    }
    func testPackedUSBStatusNegotiationAndBoundedRecovery() throws {
        XCTAssertEqual(try MacTab5USBStatusValidation.run(), 25)
    }
}
