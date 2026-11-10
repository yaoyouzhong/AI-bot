import XCTest
@testable import AIBotBridge

final class MacClaudeAutomationProbeTests: XCTestCase {
    func testPendingRefreshKeepsPresentationButCannotAuthorizeSend() {
        let clockLock = NSLock()
        var now = 0.0
        func setTime(_ time: Double) { clockLock.lock(); now = time; clockLock.unlock() }
        let probe = MacClaudeAutomationProbe(clock: { clockLock.lock(); defer { clockLock.unlock() }; return now })
        let started = DispatchSemaphore(value: 0), complete = DispatchSemaphore(value: 0)
        let pid: pid_t = 42
        XCTAssertEqual(probe.status(pid: pid, request: false, observing: true) { 0 }, 0)

        setTime(1.1)
        XCTAssertEqual(probe.status(pid: pid, request: false, observing: true) {
            started.signal()
            _ = complete.wait(timeout: .now() + 3)
            return -1743
        }, 0)
        XCTAssertEqual(started.wait(timeout: .now() + 1), .success)
        func check(_ observing: Bool) -> OSStatus {
            probe.status(pid: pid, request: false, observing: observing) {
                XCTFail("The same permission refresh should be shared")
                return -1743
            }
        }
        XCTAssertEqual(check(true), 0)
        XCTAssertNotEqual(check(false), 0)
        XCTAssertTrue(probe.isChecking(pid))
        setTime(3.1)
        XCTAssertNotEqual(check(true), 0, "A stuck refresh must not extend the last verified permission")
        complete.signal()
        XCTAssertEqual(check(false), -1743)
        XCTAssertEqual(check(true), -1743, "An explicit denial must replace the previous ready state")
        XCTAssertFalse(probe.isChecking(pid))
    }

    func testUnconfirmedPermissionIsNeverPresentedAsReady() {
        let probe = MacClaudeAutomationProbe()
        let complete = DispatchSemaphore(value: 0)
        defer { complete.signal() }
        XCTAssertNotEqual(probe.status(pid: 42, request: false, observing: true) {
            _ = complete.wait(timeout: .now() + 2)
            return 0
        }, 0)
    }
}
