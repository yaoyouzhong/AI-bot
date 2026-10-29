import XCTest
@testable import AIBotBridge

final class MacVoiceSourceTests: XCTestCase {
    func testSavedMicrophoneRoutingAndActiveTakeIsolation() throws {
        XCTAssertGreaterThan(try MacVoiceSourceValidation.run(), 0)
    }
}
