import XCTest
@testable import AIBotBridge

final class MusicTimelineTests: XCTestCase {
    private let start = Date(timeIntervalSince1970: 1800000000)
    private func track(_ elapsed: Double = 10.75, playing: Bool = true, age: Double = 0, title: String = "Synthetic track") -> MusicSnapshot {
        MusicSnapshot(title: title, artist: "Synthetic artist", album: "Synthetic album", playing: playing,
                      elapsedSeconds: elapsed, durationSeconds: 100, updatedAt: start.addingTimeInterval(age))
    }
    private func status(_ music: MusicSnapshot?) -> MacStatusSnapshot {
        MacStatusSnapshot(version: 1, time: "12:00:00", epochUtc: 1800000000, utcOffsetSeconds: 0,
                          capturedAt: start, codex: ToolState(state: "idle", ageSeconds: nil),
                          claude: ToolState(state: "idle", ageSeconds: nil), musicPlaying: music?.playing,
                          weather: nil, stocks: nil, systemMetrics: nil, quotas: nil, music: music)
    }
    private func wire(_ source: MacStatusSnapshot, at age: Double, latest: MusicSnapshot? = nil) throws -> [String: Any] {
        let bytes = try MacTab5Protocol.snapshot(source, device: "001122334455", session: "fixture", sequence: 1,
                                                command: nil, hasArtwork: true, music: latest, now: start.addingTimeInterval(age))
        let root = try MacTab5Protocol.object(bytes)
        return try XCTUnwrap((root["data"] as? [String: Any])?["music"] as? [String: Any])
    }

    func testRepeatedStatusUsesSampleAgeInsteadOfResettingDeviceClock() throws {
        let source = status(track())
        let first = try wire(source, at: 0.2), second = try wire(source, at: 2.25)
        XCTAssertEqual(first["elapsedSeconds"] as! Double, 10.95, accuracy: 0.0001)
        XCTAssertEqual(second["elapsedSeconds"] as! Double, 13, accuracy: 0.0001)
        // Published .234 firmware adds whole seconds since receipt, then
        // replaces that anchor on each status frame. Reusing 10 would rewind.
        let beforeSecondFrame = Int(first["elapsedSeconds"] as! Double) + 2
        XCTAssertGreaterThanOrEqual(Int(second["elapsedSeconds"] as! Double), beforeSecondFrame)
        XCTAssertEqual(source.music?.elapsedSeconds, 10.75)
        XCTAssertEqual(source.music?.updatedAt, start)
    }

    func testFreshPauseAndSeekOverrideTheOlderPublishedStatus() throws {
        let source = status(track())
        let paused = try wire(source, at: 2, latest: track(11.125, playing: false, age: 1))
        XCTAssertEqual(paused["playing"] as? Bool, false)
        XCTAssertEqual(paused["elapsedSeconds"] as? Double, 11.125)
        let seek = try wire(source, at: 2.5, latest: track(2.25, age: 2))
        XCTAssertEqual(seek["elapsedSeconds"] as! Double, 2.75, accuracy: 0.0001)
        XCTAssertEqual(seek["timelineAvailable"] as? Bool, true)
    }

    func testStaleOrFutureClockRetainsMetadataWithoutInventingProgress() throws {
        let source = status(track())
        for age in [-1.0, 3.01, 60] {
            let music = try wire(source, at: age)
            XCTAssertEqual(music["title"] as? String, "Synthetic track")
            XCTAssertEqual(music["hasArtwork"] as? Bool, true)
            XCTAssertEqual(music["elapsedSeconds"] as? Double, 10.75)
            XCTAssertEqual(music["timelineAvailable"] as? Bool, false)
        }
    }

    func testFractionalPositionAndEndOfTrackAreBounded() throws {
        let parsed = try XCTUnwrap(MacMusicService.parse(values: ["Synthetic", "Artist", "Album", "true", "98.625", "100.25"], now: start))
        XCTAssertEqual(parsed.elapsedSeconds, 98.625)
        XCTAssertEqual(parsed.durationSeconds, 100.25)
        XCTAssertEqual(parsed.timeline(at: start.addingTimeInterval(2)).elapsed, 100.25)
        XCTAssertTrue(parsed.timeline(at: start.addingTimeInterval(2)).available)
    }

    func testUSBMusicUrgencySeparatesControlAndSeekFromOrdinaryProgress() {
        XCTAssertFalse(MusicSnapshot.changedUrgently(from: track(), to: track(11.25, age: 0.5)))
        XCTAssertTrue(MusicSnapshot.changedUrgently(from: track(), to: track(11, playing: false, age: 0.5)))
        XCTAssertTrue(MusicSnapshot.changedUrgently(from: track(), to: track(2, age: 0.5)))
        XCTAssertTrue(MusicSnapshot.changedUrgently(from: track(), to: track(0, age: 0.5, title: "Next track")))
        XCTAssertTrue(MusicSnapshot.changedUrgently(from: track(), to: .empty(at: start.addingTimeInterval(0.5))))
        XCTAssertFalse(MusicSnapshot.changedUrgently(from: track(99), to: track(100, age: 2)))
    }

    func testLateMetadataAndArtworkCannotUndoConfirmedPauseOrTrackChange() throws {
        let suite = "AI-bot-music-timeline-" + UUID().uuidString
        let defaults = try XCTUnwrap(UserDefaults(suiteName: suite))
        defer { defaults.removePersistentDomain(forName: suite) }
        let store = MacDataStore(defaults: defaults), old = track()
        store.update(music: old)
        let paused = track(11, playing: false, age: 0.5)
        store.update(music: paused)
        store.update(music: old)
        store.update(music: old, cover: Data([1, 2]))
        XCTAssertEqual(store.snapshot().music?.playing, false)
        XCTAssertEqual(store.snapshot().music?.elapsedSeconds, 11)
        XCTAssertEqual(store.snapshot().musicCover, Data([1, 2]))
        store.update(music: track(0, age: 1, title: "Next track"))
        XCTAssertNil(store.snapshot().musicCover)
        store.update(music: old, cover: Data([3, 4]))
        XCTAssertEqual(store.snapshot().music?.title, "Next track")
        XCTAssertNil(store.snapshot().musicCover)
        store.update(music: .empty(at: start.addingTimeInterval(2)))
        store.update(music: old, cover: Data([3, 4]))
        XCTAssertEqual(store.snapshot().music?.title, "")
        XCTAssertNil(store.snapshot().musicCover)
    }

    func testServiceUsesCurrentMusicWithoutWaitingForGeneralStatusPublication() throws {
        let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.usbmodem-fixture", key: Data(0..<32), confirmed: true)
        var latest = track()
        let service = MacTab5Service(port: 8765, music: { (latest, nil) }, load: { pair }, save: { _ in })
        service.publish(status(track()))
        latest = track(11, playing: false, age: 1)
        let root = try MacTab5Protocol.object(service.frame())
        let data = try XCTUnwrap(root["data"] as? [String: Any])
        XCTAssertEqual(data["musicPlaying"] as? Bool, false)
        XCTAssertEqual((data["music"] as? [String: Any])?["playing"] as? Bool, false)
        XCTAssertEqual((data["music"] as? [String: Any])?["elapsedSeconds"] as? Double, 11)
    }

    func testFastMusicFramesBindArtworkToTheSameCurrentSample() throws {
        let pair = MacTab5Pairing(deviceId: "001122334455", usbPath: "/dev/cu.usbmodem-fixture", key: Data(0..<32), confirmed: true)
        var latest = track(), cover: Data? = Data(repeating: 1, count: 112 * 112 * 2)
        let service = MacTab5Service(port: 8765, music: { (latest, cover) }, load: { pair }, save: { _ in })
        service.publish(status(track()))
        func frame() throws -> [String: Any] { try XCTUnwrap(MacTab5Protocol.object(service.frame())["data"] as? [String: Any]) }
        let first = try frame()
        XCTAssertEqual((first["music"] as? [String: Any])?["hasArtwork"] as? Bool, true)
        latest = track(0, age: 1, title: "Next track"); cover = nil
        let pending = try frame()
        XCTAssertEqual((pending["music"] as? [String: Any])?["title"] as? String, "Next track")
        XCTAssertEqual((pending["music"] as? [String: Any])?["hasArtwork"] as? Bool, false)
        XCTAssertTrue((pending["assetIds"] as? [Any])?[2] is NSNull)
        cover = Data(repeating: 2, count: 112 * 112 * 2)
        let ready = try frame()
        XCTAssertEqual((ready["music"] as? [String: Any])?["hasArtwork"] as? Bool, true)
        XCTAssertNotEqual((ready["assetIds"] as? [Any])?[2] as? String, (first["assetIds"] as? [Any])?[2] as? String)
    }

    func testUSBStatusPhaseDoesNotAccumulateACKOrCallbackDelay() {
        var cadence = MacTab5StatusCadence()
        cadence.acknowledge(started: 100, finished: 100.08, urgent: false)
        for index in 1...20 {
            let slot = 100 + Double(index) * 2
            XCTAssertFalse(cadence.isDue(slot - 0.001))
            XCTAssertTrue(cadence.isDue(slot))
            cadence.acknowledge(started: slot + 0.01, finished: slot + 0.08 + Double(index % 3) * 0.03, urgent: false)
        }
    }

    func testUSBControlStartsANewPhaseAndBlockedWritesDoNotBurst() {
        var cadence = MacTab5StatusCadence()
        cadence.acknowledge(started: 100, finished: 100.08, urgent: false)
        cadence.acknowledge(started: 100.5, finished: 100.6, urgent: true)
        XCTAssertFalse(cadence.isDue(102.49)); XCTAssertTrue(cadence.isDue(102.5))
        cadence.acknowledge(started: 102.5, finished: 106, urgent: false)
        XCTAssertFalse(cadence.isDue(107.99)); XCTAssertTrue(cadence.isDue(108))
    }
}
