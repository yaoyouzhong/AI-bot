import XCTest
@testable import AIBotBridge

final class MusicCacheTests: XCTestCase {
    private func cachedStore() -> (UserDefaults, String, MacDataStore) {
        let suite = "AI-bot-music-test-" + UUID().uuidString
        let defaults = UserDefaults(suiteName: suite)!
        let store = MacDataStore(defaults: defaults)
        let track = MusicSnapshot(title: "Synthetic track", artist: "Synthetic artist", album: "",
                                  playing: true, elapsedSeconds: 10, durationSeconds: 100,
                                  updatedAt: Date(timeIntervalSince1970: 100))
        store.update(music: track, cover: Data(repeating: 0x3f, count: 112 * 112 * 2))
        return (defaults, suite, store)
    }

    func testRepeatedReadFailuresKeepSuccessfulMetadataAndArtwork() async {
        let (defaults, suite, store) = cachedStore()
        defer { defaults.removePersistentDomain(forName: suite) }
        let before = store.snapshot(), service = MacMusicService(store: store)
        for _ in 0..<4 {
            await service.refresh(enabled: true, isRunning: { _ in true }, read: { _ in nil })
        }
        XCTAssertEqual(store.snapshot().music, before.music)
        XCTAssertEqual(store.snapshot().musicCover, before.musicCover)
    }

    func testStoppedOtherProviderDoesNotHideAnUnreadablePlayer() async {
        let (defaults, suite, store) = cachedStore()
        defer { defaults.removePersistentDomain(forName: suite) }
        let before = store.snapshot(), service = MacMusicService(store: store)
        for _ in 0..<4 {
            await service.refresh(enabled: true, isRunning: { _ in true }, read: { provider in
                provider.bundleIdentifier == "com.apple.Music" ? nil : .empty()
            })
        }
        XCTAssertEqual(store.snapshot().music, before.music)
        XCTAssertEqual(store.snapshot().musicCover, before.musicCover)
    }

    func testFailureResetsEmptyDebounceAndConfirmedExitStillClears() async {
        let (defaults, suite, store) = cachedStore()
        defer { defaults.removePersistentDomain(forName: suite) }
        let before = store.snapshot(), service = MacMusicService(store: store)
        for _ in 0..<2 {
            await service.refresh(enabled: true, isRunning: { _ in true }, read: { _ in .empty() })
        }
        await service.refresh(enabled: true, isRunning: { _ in true }, read: { _ in nil })
        for _ in 0..<2 {
            await service.refresh(enabled: true, isRunning: { _ in false }, read: { _ in nil })
        }
        XCTAssertEqual(store.snapshot().music, before.music)
        XCTAssertEqual(store.snapshot().musicCover, before.musicCover)
        await service.refresh(enabled: true, isRunning: { _ in false }, read: { _ in nil })
        XCTAssertEqual(store.snapshot().music?.title, "")
        XCTAssertNil(store.snapshot().musicCover)
    }

    func testExplicitDisableClearsWithoutProbingPlayers() async {
        let (defaults, suite, store) = cachedStore()
        defer { defaults.removePersistentDomain(forName: suite) }
        let service = MacMusicService(store: store)
        await service.refresh(enabled: false,
                              isRunning: { _ in XCTFail("Disabled feature must not probe players"); return false },
                              read: { _ in XCTFail("Disabled feature must not send Apple Events"); return nil })
        XCTAssertEqual(store.snapshot().music?.title, "")
        XCTAssertNil(store.snapshot().musicCover)
    }
}
