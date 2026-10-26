import XCTest
@testable import AIBotBridge

final class MacTab5BLEVoicePacingTests: XCTestCase {
    func testConfirmedBacklogContinuesBeforeNextRadioSlotWhileIdleStillBacksOff() {
        let completed = 10.0, nextSlot = 10.01
        let immediate = MacTab5BLEVoicePacing.continuationDelay(now: completed, lastVoice: completed, completedVoice: true)
        XCTAssertNotNil(immediate)
        XCTAssertLessThan(completed + (immediate ?? 1), nextSlot)
        // Polls that find no new request must not become an unbounded busy loop.
        let idle = MacTab5BLEVoicePacing.continuationDelay(now: completed, lastVoice: completed, completedVoice: false)
        XCTAssertGreaterThan(completed + (idle ?? 0), nextSlot)
        for now in [9.0, 11, .infinity, .nan] {
            for confirmed in [true, false] {
                XCTAssertNil(MacTab5BLEVoicePacing.continuationDelay(now: now, lastVoice: completed, completedVoice: confirmed))
            }
        }
        XCTAssertNil(MacTab5BLEVoicePacing.continuationDelay(now: completed, lastVoice: -.infinity, completedVoice: true))
    }
    func testOnlyRecentCompletedVoiceActivatesFastPolling() {
        XCTAssertTrue(MacTab5BLEVoicePacing.active(now: 10, lastVoice: 10))
        XCTAssertTrue(MacTab5BLEVoicePacing.active(now: 10.99, lastVoice: 10))
        for now in [9.0, 11, .infinity, .nan] {
            XCTAssertFalse(MacTab5BLEVoicePacing.active(now: now, lastVoice: 10))
        }
        XCTAssertFalse(MacTab5BLEVoicePacing.active(now: 10, lastVoice: -.infinity))
        // Idle/duplicate reads do not mutate the last completed request time.
        for step in 0..<1000 {
            let now = 10 + Double(step) / 100
            XCTAssertEqual(MacTab5BLEVoicePacing.active(now: now, lastVoice: 10), now < 11)
        }
    }
    func testControlRemainsPeriodicAndStatusCannotBeDeferredPastFourSeconds() {
        XCTAssertEqual(MacTab5BLEVoicePacing.priority(now: 3, lastVoice: 2.8, lastControl: 2.5, lastFrame: 0), .voice)
        XCTAssertEqual(MacTab5BLEVoicePacing.priority(now: 3, lastVoice: 2.8, lastControl: 2, lastFrame: 0), .control)
        XCTAssertEqual(MacTab5BLEVoicePacing.priority(now: 3, lastVoice: 2.8, lastControl: -.infinity, lastFrame: 0), .control)
        XCTAssertNil(MacTab5BLEVoicePacing.priority(now: 4, lastVoice: 3.9, lastControl: 3.9, lastFrame: 0))
        XCTAssertNil(MacTab5BLEVoicePacing.priority(now: 3, lastVoice: 2.9, lastControl: 2.9, lastFrame: -.infinity))
        XCTAssertNil(MacTab5BLEVoicePacing.priority(now: 3, lastVoice: 1, lastControl: 2.9, lastFrame: 0))
    }
    func testContinuousQueuedAudioStillServicesControlAndStatus() {
        // Deterministic link model, not a hardware latency claim. Every operation
        // completes before the next decision, just like the serialized GATT queue.
        var now = 0.0, voice = 0.0, control = 0.0, frame = 0.0
        var audioCount = 0, controlCount = 0, frameCount = 0
        var maximumControlGap = 0.0, maximumStatusDelay = 0.0
        for _ in 0..<500 {
            switch MacTab5BLEVoicePacing.priority(now: now, lastVoice: voice, lastControl: control, lastFrame: frame) {
            case .voice:
                now += 0.08; voice = now; audioCount += 1
            case .control:
                maximumControlGap = max(maximumControlGap, now - control)
                now += 0.12; control = now; controlCount += 1
            case nil:
                maximumStatusDelay = max(maximumStatusDelay, now - frame)
                now += 1.8; frame = now; frameCount += 1
                // Production probes voice immediately after a background ACK.
                now += 0.08; voice = now; audioCount += 1
            }
            now += MacTab5BLEVoicePacing.continuationDelay(now: now, lastVoice: voice, completedVoice: voice == now) ?? 0
        }
        XCTAssertGreaterThan(audioCount, controlCount * 4)
        XCTAssertGreaterThan(controlCount, 10); XCTAssertGreaterThan(frameCount, 5)
        XCTAssertLessThanOrEqual(maximumControlGap, 3.1)
        XCTAssertLessThanOrEqual(maximumStatusDelay, 4.2)
    }
    func testLateClocksAndNewConnectionsDoNotInheritPriority() {
        XCTAssertNil(MacTab5BLEVoicePacing.priority(now: 10, lastVoice: -.infinity, lastControl: -.infinity, lastFrame: -.infinity))
        XCTAssertNil(MacTab5BLEVoicePacing.priority(now: 10, lastVoice: 11, lastControl: 9, lastFrame: 9))
        XCTAssertNil(MacTab5BLEVoicePacing.priority(now: 10, lastVoice: 10, lastControl: 9, lastFrame: 11))
        XCTAssertEqual(MacTab5BLEVoicePacing.priority(now: 10, lastVoice: 10, lastControl: 11, lastFrame: 9), .control)
    }
}
