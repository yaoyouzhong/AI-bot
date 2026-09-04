import XCTest
@testable import AIBotBridge

final class AIBotBridgeTests: XCTestCase {
    func testActivityClassification() {
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 89), "working")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 90), "idle")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 900), "idle")
        XCTAssertEqual(SessionActivityReader.classify(ageSeconds: 901), "offline")
    }

    func testPairingComparison() {
        XCTAssertTrue(HTTPStatusServer.constantTimeEqual(String(repeating: "a", count: 32), String(repeating: "a", count: 32)))
        XCTAssertFalse(HTTPStatusServer.constantTimeEqual(String(repeating: "a", count: 32), String(repeating: "b", count: 32)))
        XCTAssertFalse(HTTPStatusServer.constantTimeEqual("short", String(repeating: "a", count: 32)))
    }
}
