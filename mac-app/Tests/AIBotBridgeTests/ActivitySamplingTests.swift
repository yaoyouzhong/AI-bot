import XCTest
@testable import AIBotBridge

final class ActivitySamplingTests: XCTestCase {
    func testSlowActivityScanDoesNotBlockSnapshotsOrMusicAndCoalescesRequests() throws {
        let queue = DispatchQueue(label: "activity-test")
        let started = DispatchSemaphore(value: 0), release = DispatchSemaphore(value: 0)
        var scans = 0
        let sampler = MacActivitySampler(queue: queue) { _ in
            XCTAssertFalse(Thread.isMainThread)
            scans += 1
            if scans == 1 {
                started.signal()
                XCTAssertEqual(release.wait(timeout: .now() + 5), .success)
            }
            return MacActivityTotals(codex: ToolState(state: "working", ageSeconds: 0, tokensToday: 123))
        }
        defer { release.signal() }
        let reader = SessionActivityReader(metadata: sampler)
        let song = MusicSnapshot(title: "Synthetic", artist: "", album: "", playing: true,
                                 elapsedSeconds: 12.25, durationSeconds: 600, updatedAt: Date())
        let extras = MacDataExtras(weather: nil, stocks: nil, systemMetrics: nil, quotas: nil, music: song, musicCover: nil)
        let begin = ProcessInfo.processInfo.systemUptime
        let first = reader.capture(extras: extras)
        XCTAssertLessThan(ProcessInfo.processInfo.systemUptime - begin, 1)
        XCTAssertEqual(started.wait(timeout: .now() + 2), .success)
        XCTAssertEqual(first.codex.state, "unknown")
        for _ in 0..<20 {
            let snapshot = reader.capture(extras: extras)
            let data = try MacTab5Protocol.object(MacTab5Protocol.snapshot(snapshot, device: "001122334455",
                session: "fixture", sequence: 1, command: nil, now: song.updatedAt.addingTimeInterval(0.5)))["data"] as! [String: Any]
            let music = data["music"] as! [String: Any]
            XCTAssertEqual(music["timelineAvailable"] as? Bool, true)
            XCTAssertEqual(music["elapsedSeconds"] as? Double, 12.75)
        }
        release.signal()
        queue.sync {}
        XCTAssertEqual(scans, 1)
        XCTAssertEqual(reader.capture(extras: extras).codex.tokensToday, 123)
    }

    func testCachedActivityAgeAdvancesAndLongPendingScanBecomesUnknown() {
        let queue = DispatchQueue(label: "activity-age-test"), at = Date(timeIntervalSince1970: 1800000000)
        let sampler = MacActivitySampler(queue: queue) { _ in
            MacActivityTotals(codex: ToolState(state: "working", ageSeconds: 2, tokensToday: 7),
                              claude: ToolState(state: "working", ageSeconds: 89))
        }
        _ = sampler.capture(now: at)
        queue.sync {}
        let next = sampler.capture(now: at.addingTimeInterval(1))
        XCTAssertEqual(next.codex.ageSeconds, 3)
        XCTAssertEqual(next.claude.state, "idle")
        let old = sampler.capture(now: at.addingTimeInterval(31))
        XCTAssertEqual(old.codex.state, "unknown")
        XCTAssertEqual(old.codex.tokensToday, 7)
        XCTAssertEqual(old.claude.state, "unknown")
        queue.sync {}
    }
}
