import XCTest
@testable import AIBotBridge

final class MacTab5BLERecoveryTests: XCTestCase {
    func testStatusTimeoutLeavesOtherCharacteristicsAvailableButCannotCommitAnOldWrite() {
        var recovery = MacTab5BLERecovery<Int>()
        recovery.begin(.statusWrite, key: 1, now: 10)
        XCTAssertNil(recovery.expire(now: 12.999))
        XCTAssertEqual(recovery.expire(now: 13), .statusWrite)
        XCTAssertTrue(recovery.statusFailed(now: 13, connected: true))
        XCTAssertFalse(recovery.canStartStatus(now: 19, write: 1, ack: 2))
        XCTAssertTrue(recovery.canUse(3), "Control and voice can continue during status recovery")
        recovery.begin(.probe, key: 3, now: 19)
        XCTAssertEqual(recovery.receive(1, now: 20), .late)
        XCTAssertFalse(recovery.canStartStatus(now: 25, write: 1, ack: 2))
        XCTAssertTrue(recovery.canStartStatus(now: 25.2, write: 1, ack: 2))
        XCTAssertEqual(recovery.receive(3, now: 20.1), .current, "Draining a status callback must not clear the active probe")
    }

    func testStatusRecoveryRequiresALiveLinkAndIsResetOnlyByAConfirmedFrame() {
        var recovery = MacTab5BLERecovery<Int>()
        XCTAssertFalse(recovery.statusFailed(now: 1, connected: false))
        XCTAssertTrue(recovery.statusFailed(now: 2, connected: true))
        XCTAssertTrue(recovery.statusFailed(now: 8, connected: true))
        XCTAssertFalse(recovery.statusFailed(now: 14, connected: true))
        recovery.statusConfirmed()
        XCTAssertTrue(recovery.statusFailed(now: 20, connected: true))
    }

    func testEmptyHeaderTimeoutCannotBeMistakenForANewReadOrCancelTheOtherMailbox() {
        var recovery = MacTab5BLERecovery<Int>()
        recovery.begin(.probe, key: 3, now: 0)
        XCTAssertEqual(recovery.expire(now: 3), .probe)
        XCTAssertFalse(recovery.canUse(3))
        XCTAssertTrue(recovery.canUse(4))
        XCTAssertFalse(recovery.mustReconnect(now: 11.9))
        XCTAssertTrue(recovery.mustReconnect(now: 12))
        XCTAssertEqual(recovery.receive(3, now: 5), .late)
        XCTAssertTrue(recovery.canUse(3))
        XCTAssertFalse(recovery.mustReconnect(now: 12))
        XCTAssertTrue(recovery.probeFailed(3, connected: true))
        XCTAssertTrue(recovery.probeFailed(3, connected: true))
        XCTAssertFalse(recovery.probeFailed(3, connected: true))
        XCTAssertTrue(recovery.probeFailed(4, connected: true))
        recovery.probeConfirmed(3)
        XCTAssertTrue(recovery.probeFailed(3, connected: true))
    }

    func testCallbackArrivingAfterDeadlineEvenBeforeTimerCannotAdvanceWork() {
        var recovery = MacTab5BLERecovery<Int>()
        recovery.begin(.statusACK, key: 2, now: 0)
        XCTAssertEqual(recovery.receive(2, now: 3.01), .expired(.statusACK))
        XCTAssertTrue(recovery.canUse(2), "The expired callback was already drained")
        XCTAssertNil(recovery.expire(now: 4))
    }
}

extension MacTab5BLERecoveryTests {
    func testVoiceCanRunOnlyBetweenConfirmedReplayFragments() {
        var schedule = MacTab5BLEWriteSchedule(count: 60, mailbox: true, bulk: true, withoutResponse: true,
                                             capabilities: .init(["rpcReplay": 1, "rpcWriteWindow": 32]))
        XCTAssertFalse(schedule.canYieldToVoice(next: 10, now: 2, lastProbe: 0))
        XCTAssertEqual(schedule.recover(protocolError: 0x0e), 0)
        XCTAssertFalse(schedule.canYieldToVoice(next: 0, now: 2, lastProbe: 0))
        schedule.acknowledged(next: 1)
        XCTAssertTrue(schedule.canYieldToVoice(next: 1, now: 2, lastProbe: 0))
        XCTAssertFalse(schedule.canYieldToVoice(next: 1, now: 2, lastProbe: 1.5))
        XCTAssertTrue(schedule.requiresResponse(at: 1))
        schedule.acknowledged(next: 32)
        XCTAssertFalse(schedule.canYieldToVoice(next: 32, now: 3, lastProbe: 0))
        XCTAssertFalse(schedule.requiresResponse(at: 32))
    }
}
