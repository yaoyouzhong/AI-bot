import XCTest
@testable import AIBotBridge

final class MacModuleParityTests: XCTestCase {
    func testModuleStorageAndProtocolContracts() throws { try MacParityValidation.run() }
}
