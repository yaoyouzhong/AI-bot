import XCTest
@testable import AIBotBridge

final class MacLatestParityTests: XCTestCase {
    func testLatestWindowsQuotaAndAuthenticatedDiagnosticContract() throws {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at:root) }
        XCTAssertGreaterThan(try MacLatestParityValidation.run(root:root),30)
    }
}
