import AppKit
import Foundation
import ImageIO

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

    var artworkScriptSources: [String] {
        switch self {
        case .appleMusic:
            return ["raw data", "data"].map { property in
                """
                with timeout of 2 seconds
                    tell application id "com.apple.Music"
                        if player state is stopped then return missing value
                        set currentTrack to current track
                        if (count of artworks of currentTrack) is 0 then return missing value
                        return \(property) of artwork 1 of currentTrack
                    end tell
                end timeout
                """
            }
        case .spotify:
            return ["""
                with timeout of 2 seconds
                    tell application id "com.spotify.client"
                        if player state is stopped then return ""
                        return artwork url of current track as text
                    end tell
                end timeout
                """]
        }
    }
}

actor MacMusicService {
    static let enabledKey = "music_automation_enabled"

    private let store: MacDataStore
    private let defaults: UserDefaults
    private let session: URLSession
    private var emptySamples = 0
    private var resourceKey = ""
    private static let maximumArtworkBytes = 10 * 1_024 * 1_024

    init(store: MacDataStore, defaults: UserDefaults = .standard) {
        self.store = store
        self.defaults = defaults
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 5
        configuration.timeoutIntervalForResource = 8
        session = URLSession(configuration: configuration)
    }

    func refreshIfEnabled() async {
        guard defaults.bool(forKey: Self.enabledKey) else {
            emptySamples = 0
            resourceKey=""
            store.update(music: .empty())
            return
        }

        var selected: (MacMusicProvider, MusicSnapshot)?
        for provider in [MacMusicProvider.appleMusic, .spotify] {
            guard Self.isRunning(provider) else { continue }
            guard let snapshot = Self.read(provider) else { continue }
            if snapshot.playing {
                selected = (provider, snapshot)
                break
            }
            if !snapshot.title.isEmpty, selected == nil { selected = (provider, snapshot) }
        }
        if let (provider, snapshot) = selected {
            emptySamples = 0
            await apply(snapshot, from: provider)
            return
        }

        emptySamples += 1
        if emptySamples >= 3 { resourceKey="";store.update(music: .empty()) }
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

    private func apply(_ snapshot: MusicSnapshot, from provider: MacMusicProvider) async {
        let key = provider.bundleIdentifier + "\n" + snapshot.title + "\n" + snapshot.artist
        guard key != resourceKey || store.snapshot().music?.hasArtwork != true else {
            store.update(music: snapshot)
            return
        }
        let encoded = await readArtwork(from: provider)
        let cover = encoded.flatMap(Self.renderArtwork)
            ?? Data()
        resourceKey = key
        store.update(music: snapshot, cover: cover)
    }

    private static func normalized(_ value: String) -> String {
        let singleLine = value.components(separatedBy: .newlines).joined(separator: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return String(singleLine.prefix(96))
    }

    private static func read(_ provider: MacMusicProvider) -> MusicSnapshot? {
        guard let result = execute(provider.scriptSource), result.numberOfItems == 6 else { return nil }
        let values = (1...6).map { result.atIndex($0)?.stringValue ?? "" }
        return parse(values: values)
    }

    private func readArtwork(from provider: MacMusicProvider) async -> Data? {
        switch provider {
        case .appleMusic:
            for source in provider.artworkScriptSources {
                guard let result = Self.execute(source) else { continue }
                let data = result.data
                if !data.isEmpty, data.count <= Self.maximumArtworkBytes { return data }
            }
            return nil
        case .spotify:
            guard let source = provider.artworkScriptSources.first,
                  let value = Self.execute(source)?.stringValue,
                  let url = URL(string: value), Self.allowedSpotifyArtworkURL(url) else { return nil }
            do {
                var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData,
                                         timeoutInterval: 5)
                request.setValue("AI-bot/0.1", forHTTPHeaderField: "User-Agent")
                let (data, response) = try await session.data(for: request)
                guard (response as? HTTPURLResponse)?.statusCode == 200,
                      response.url.map(Self.allowedSpotifyArtworkURL) == true,
                      !data.isEmpty, data.count <= Self.maximumArtworkBytes else { return nil }
                return data
            } catch {
                return nil
            }
        }
    }

    static func renderArtwork(_ encoded: Data) -> Data? {
        guard !encoded.isEmpty, encoded.count <= maximumArtworkBytes,
              let source = CGImageSourceCreateWithData(encoded as CFData, nil),
              CGImageSourceGetCount(source) > 0,
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = (properties[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue,
              let height = (properties[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue,
              (1...4_096).contains(width), (1...4_096).contains(height),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else { return nil }
        return MacRgb565Renderer.renderImage(image, width: 112, height: 112)
    }

    static func allowedSpotifyArtworkURL(_ url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https", let host = url.host?.lowercased() else {
            return false
        }
        return host == "scdn.co" || host.hasSuffix(".scdn.co") ||
            host == "spotifycdn.com" || host.hasSuffix(".spotifycdn.com")
    }

    private static func execute(_ source: String) -> NSAppleEventDescriptor? {
        guard let script = NSAppleScript(source: source) else { return nil }
        var error: NSDictionary?
        let result = script.executeAndReturnError(&error)
        return error == nil ? result : nil
    }
}
