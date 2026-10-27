import XCTest
@testable import AIBotBridge

final class HTTPListenerRecoveryTests: XCTestCase {
    func testBoundedAddressInUsePolicy() throws {
        XCTAssertEqual(try MacHTTPListenerRecoveryValidation.policy(),6)
    }
    func testRealLoopbackPortCollisionRecoveryAndStop() throws {
        XCTAssertEqual(try MacHTTPListenerRecoveryValidation.loopback(),16)
    }
}
