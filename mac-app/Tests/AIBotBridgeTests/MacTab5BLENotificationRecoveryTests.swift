import CoreBluetooth
import XCTest
@testable import AIBotBridge

final class MacTab5BLENotificationRecoveryTests: XCTestCase {
    private let persistenceFailure = NSError(domain: CBATTErrorDomain, code: 8)
    func testEnabledReadbackAllowsOnlyOneReconciliationAndNeverGrantsAdmission() {
        var recovery = MacTab5BLENotificationRecovery()
        XCTAssertFalse(recovery.shouldReconcile(1)) // No failed subscription yet.
        XCTAssertTrue(recovery.needsReadback(after: persistenceFailure))
        XCTAssertTrue(recovery.shouldReconcile(NSNumber(value: 1)))
        // A second rejection must fall back, even if the peer remains enabled.
        XCTAssertFalse(recovery.needsReadback(after: persistenceFailure))
        XCTAssertFalse(recovery.shouldReconcile(1))
        XCTAssertFalse(recovery.needsReadback(after: nil))
    }
    func testDisabledUnknownMalformedAndUnrelatedFailuresDoNotRetry() {
        for value: Any? in [nil, 0, 2, 3, -1, 65537, true, 1.5, "1", Data([1, 0]), NSNull()] {
            var recovery = MacTab5BLENotificationRecovery()
            XCTAssertTrue(recovery.needsReadback(after: persistenceFailure))
            XCTAssertFalse(recovery.shouldReconcile(value))
            XCTAssertFalse(recovery.shouldReconcile(1)) // No late success after a failed readback.
            XCTAssertFalse(recovery.needsReadback(after: persistenceFailure))
        }
        for error: Error? in [nil, NSError(domain: CBATTErrorDomain, code: 5),
                             NSError(domain: CBATTErrorDomain, code: 15), NSError(domain: CBErrorDomain, code: 8)] {
            var recovery = MacTab5BLENotificationRecovery()
            XCTAssertFalse(recovery.needsReadback(after: error))
            XCTAssertFalse(recovery.shouldReconcile(1))
            XCTAssertFalse(recovery.needsReadback(after: persistenceFailure))
        }
    }
    func testNewConnectionDoesNotInheritPendingReadbackOrConsumedAttempt() {
        var old = MacTab5BLENotificationRecovery()
        XCTAssertTrue(old.needsReadback(after: persistenceFailure))
        var fresh = MacTab5BLENotificationRecovery()
        XCTAssertFalse(fresh.shouldReconcile(1))
        XCTAssertTrue(old.shouldReconcile(1))
        XCTAssertTrue(fresh.needsReadback(after: persistenceFailure))
        XCTAssertTrue(fresh.shouldReconcile(1))
    }
}
