import AppKit
import Foundation

enum MacMusicProvider {
    case appleMusic
    case spotify

    var bundleIdentifier: String {
        switch self {
        case .appleMusic: return "com.apple.Music"
        case .spotify: return "com.spotify.client"
        }
    }

    var scriptSource: String {
        switch self {
        case .appleMusic:
            return """
            with timeout of 2 seconds
                tell application id "com.apple.Music"
                    if player state is stopped then return {"", "", "", "false", "0", "0"}
                    set currentTrack to current track
                    return {name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text, ((player state is playing) as text), (((player position) as integer) as text), (((duration of currentTrack) as integer) as text)}
                end tell
            end timeout
            """
        case .spotify:
            return """
            with timeout of 2 seconds
                tell application id "com.spotify.client"
                    if player state is stopped then return {"", "", "", "false", "0", "0"}
                    set currentTrack to current track
                    return {name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text, ((player state is playing) as text), (((player position) as integer) as text), (((duration of currentTrack) div 1000) as text)}
                end tell
            end timeout
            """
        }
    }
}

actor MacMusicService {
    static let enabledKey = "music_automation_enabled"

    private let store: MacDataStore
    private let defaults: UserDefaults
    private var emptySamples = 0

    init(store: MacDataStore, defaults: UserDefaults = .standard) {
        self.store = store
        self.defaults = defaults
    }

    func refreshIfEnabled() {
        guard defaults.bool(forKey: Self.enabledKey) else {
            emptySamples = 0
            store.update(music: .empty())
            return
        }

        var paused: MusicSnapshot?
        for provider in [MacMusicProvider.appleMusic, .spotify] {
            guard Self.isRunning(provider) else { continue }
            guard let snapshot = Self.read(provider) else { continue }
            if snapshot.playing {
                emptySamples = 0
                store.update(music: snapshot)
                return
            }
            if !snapshot.title.isEmpty, paused == nil { paused = snapshot }
        }
        if let paused {
            emptySamples = 0
            store.update(music: paused)
            return
        }

        emptySamples += 1
        if emptySamples >= 3 { store.update(music: .empty()) }
    }

    static func parse(values: [String], now: Date = Date()) -> MusicSnapshot? {
        guard values.count == 6 else { return nil }
        let title = normalized(values[0])
        let artist = normalized(values[1])
        let album = normalized(values[2])
        let playing = !title.isEmpty && values[3].caseInsensitiveCompare("true") == .orderedSame
        guard let rawElapsed = Double(values[4]), let rawDuration = Double(values[5]),
              rawElapsed.isFinite, rawDuration.isFinite else { return nil }
        let duration = max(0, rawDuration)
        let elapsed = duration > 0 ? min(max(0, rawElapsed), duration) : max(0, rawElapsed)
        return MusicSnapshot(title: title, artist: artist, album: album, playing: playing,
                             elapsedSeconds: elapsed, durationSeconds: duration, updatedAt: now)
    }

    private static func isRunning(_ provider: MacMusicProvider) -> Bool {
        !NSRunningApplication.runningApplications(
            withBundleIdentifier: provider.bundleIdentifier).isEmpty
    }

    private static func normalized(_ value: String) -> String {
        let singleLine = value.components(separatedBy: .newlines).joined(separator: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return String(singleLine.prefix(96))
    }

    private static func read(_ provider: MacMusicProvider) -> MusicSnapshot? {
        guard let script = NSAppleScript(source: provider.scriptSource) else { return nil }
        var error: NSDictionary?
        let result = script.executeAndReturnError(&error)
        guard error == nil, result.numberOfItems == 6 else { return nil }
        let values = (1...6).map { result.atIndex($0)?.stringValue ?? "" }
        return parse(values: values)
    }
}
