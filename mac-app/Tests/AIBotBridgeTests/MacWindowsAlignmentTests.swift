import XCTest
@testable import AIBotBridge

final class MacWindowsAlignmentTests: XCTestCase {
    func testCorrectedDesktopTextAndTypedNoActionReceipts() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        XCTAssertGreaterThan(try MacDesktopAlignmentValidation.run(root: root), 20)
    }
    func testCapturedQuotaSchemasAndFailureRetention() throws {
        XCTAssertGreaterThan(try MacDomesticContractValidation.run(), 50)
    }
    func testVoiceAvailabilityAndExplicitDeviceProtection() throws {
        XCTAssertGreaterThan(try MacVoiceEnvironmentValidation.run(), 10)
    }
}
