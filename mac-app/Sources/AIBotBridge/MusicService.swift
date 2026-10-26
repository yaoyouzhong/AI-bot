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
                    return {name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text, ((player state is playing) as text), ((player position) as text), ((duration of currentTrack) as text)}
                end tell
            end timeout
            """
        case .spotify:
            return """
            with timeout of 2 seconds
                tell application id "com.spotify.client"
                    if player state is stopped then return {"", "", "", "false", "0", "0"}
                    set currentTrack to current track
                    return {name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text, ((player state is playing) as text), ((player position) as text), (((duration of currentTrack) / 1000) as text)}
                end tell
            end timeout
            """
        }
    }

    var artworkScriptSources: [String] {
        switch self {
        case .appleMusic:
            let current = ["data", "raw data"].map { property in
                """
                with timeout of 2 seconds
                    tell application id "com.apple.Music"
                        if player state is stopped then return missing value
                        set currentTrack to current track
                        set artworkCount to count of artworks of currentTrack
                        if artworkCount is 0 then return {0, missing value, name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text}
                        return {artworkCount, \(property) of artwork 1 of currentTrack, name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text}
                    end tell
                end timeout
                """
            }
            let library = ["data", "raw data"].map { property in
                """
                with timeout of 2 seconds
                    tell application id "com.apple.Music"
                        if player state is stopped then return missing value
                        set trackID to database ID of current track
                        set matches to {}
                        try
                            set matches to every track of current playlist whose database ID is trackID
                        end try
                        if (count of matches) is 0 then
                            set matches to every track of library playlist 1 whose database ID is trackID
                        end if
                        if (count of matches) is not 1 then return {0, missing value}
                        set libraryTrack to item 1 of matches
                        set artworkCount to count of artworks of libraryTrack
                        if artworkCount is 0 then return {0, missing value, name of libraryTrack as text, artist of libraryTrack as text, album of libraryTrack as text}
                        return {artworkCount, \(property) of artwork 1 of libraryTrack, name of libraryTrack as text, artist of libraryTrack as text, album of libraryTrack as text}
                    end tell
                end timeout
                """
            }
            return current + library
        case .spotify:
            return ["""
                with timeout of 2 seconds
                    tell application id "com.spotify.client"
                        if player state is stopped then return ""
                        set currentTrack to current track
                        return {artwork url of currentTrack as text, name of currentTrack as text, artist of currentTrack as text, album of currentTrack as text}
                    end tell
                end timeout
                """]
        }
    }
}

actor MacMusicService {
    static let enabledKey = "music_automation_enabled"
    static let controlKey = "music_control_enabled"

    private let store: MacDataStore
    private let defaults: UserDefaults
    private let session: URLSession
    private var emptySamples = 0
    private var resourceKey = ""
    private var refreshing = false
    private var artworkAttemptKey = ""
    private var artworkAttemptAt: TimeInterval = -.infinity
    private var observedKey = ""
    private var observedAt: TimeInterval = -.infinity
    private var catalogRequests: [TimeInterval] = []
    private var catalogNote = ""
    private var catalogAttemptKey = ""
    private var catalogAttemptAt: TimeInterval = -.infinity
    private static let maximumArtworkBytes = 10 * 1_024 * 1_024

    init(store: MacDataStore) {
        self.store = store
        self.defaults = .standard
        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 5
        configuration.timeoutIntervalForResource = 8
        configuration.httpShouldSetCookies = false
        configuration.httpCookieStorage = nil
        session = URLSession(configuration: configuration)
    }

    func refreshIfEnabled() async {
        await refresh(enabled: defaults.bool(forKey: Self.enabledKey),
                      isRunning: Self.isRunning, read: Self.read)
    }

    func refresh(enabled: Bool, isRunning: (MacMusicProvider) -> Bool,
                 read: (MacMusicProvider) -> MusicSnapshot?) async {
        guard enabled else {
            emptySamples = 0
            resourceKey=""; observedKey=""; artworkAttemptKey=""
            store.musicArtworkDiagnostic("音乐封面：采集已关闭")
            store.update(music: .empty())
            return
        }
        var selected: (MacMusicProvider, MusicSnapshot)?
        var readFailed = false
        for provider in [MacMusicProvider.appleMusic, .spotify] {
            guard isRunning(provider) else { continue }
            guard let snapshot = read(provider) else { readFailed = true; continue }
            if snapshot.playing {
                selected = (provider, snapshot)
                break
            }
            if !snapshot.title.isEmpty, selected == nil { selected = (provider, snapshot) }
        }
        if let (provider, snapshot) = selected {
            emptySamples = 0
            // Artwork requests can suspend for seconds. Keep sampling playback
            // and clearing a previous track's picture while one is in flight.
            store.update(music:snapshot)
            let key = Self.artworkKey(snapshot,from:provider)
            if key != observedKey {
                observedKey = key; observedAt = ProcessInfo.processInfo.systemUptime
            }
            guard !refreshing else { return }
            refreshing = true
            defer { refreshing = false }
            await apply(snapshot, from: provider)
            return
        }

        // Permission errors and timeouts do not establish that playback stopped.
        // Require consecutive confirmed empty polls before clearing the cache.
        if readFailed { emptySamples = 0; return }
        emptySamples += 1
        if emptySamples >= 3 { resourceKey="";observedKey="";store.update(music: .empty()) }
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

    static func isRunning(_ provider: MacMusicProvider) -> Bool {
        !NSRunningApplication.runningApplications(
            withBundleIdentifier: provider.bundleIdentifier).isEmpty
    }

    private func apply(_ snapshot: MusicSnapshot, from provider: MacMusicProvider) async {
        let key = Self.artworkKey(snapshot, from: provider)
        let now = ProcessInfo.processInfo.systemUptime
        // A second 500 ms sample settles a transition. Same-track/library or
        // strict catalog validation and the final identity check remain required.
        guard now - observedAt >= 0.5 else { return }
        let hasCover = key == resourceKey && store.snapshot().music?.hasArtwork == true
        guard key != artworkAttemptKey || now - artworkAttemptAt >= (hasCover ? 30 : 5) else { return }
        artworkAttemptKey = key
        artworkAttemptAt = now
        guard let cover = await readArtwork(from: provider, expected: snapshot) else { return }
        // A URL request or Apple Event may finish after playback has moved on.
        guard key == observedKey, let actual = Self.read(provider), Self.artworkKey(actual, from: provider) == key else {
            store.musicArtworkDiagnostic("音乐封面：曲目已切换，已丢弃过期结果")
            return
        }
        resourceKey = key
        store.update(music: actual, cover: cover.small, tab5Cover: cover.tab5)
    }

    static func artworkKey(_ snapshot: MusicSnapshot, from provider: MacMusicProvider) -> String {
        [provider.bundleIdentifier, snapshot.title, snapshot.artist, snapshot.album].joined(separator: "\n")
    }

    private static func normalized(_ value: String) -> String {
        let singleLine = value.components(separatedBy: .newlines).joined(separator: " ")
            .trimmingCharacters(in: .whitespacesAndNewlines)
        return String(singleLine.prefix(96))
    }

    static func read(_ provider: MacMusicProvider) -> MusicSnapshot? {
        guard let result = execute(provider.scriptSource), result.numberOfItems == 6 else { return nil }
        let values = (1...6).map { result.atIndex($0)?.stringValue ?? "" }
        return parse(values: values)
    }

    private func readArtwork(from provider: MacMusicProvider, expected: MusicSnapshot) async -> MacMusicArtwork? {
        switch provider {
        case .appleMusic:
            let result = Self.readAppleArtwork(expected: expected, execute: Self.execute)
            if !result.verified { store.musicArtworkDiagnostic("音乐封面：正在向 Apple 目录核对当前曲目") }
            if !result.verified, let cover = await readCatalogArtwork(expected) {
                store.musicArtworkDiagnostic("音乐封面：" + catalogNote + " · 歌名／歌手／专辑／时长已精确核对")
                return cover
            }
            store.musicArtworkDiagnostic(result.diagnostic + (result.verified ? "" : "; " + catalogNote))
            // Streaming current-track artwork can be stale even when its title
            // has changed. Never publish that unverified picture as a new cover.
            return result.verified ? result.encoded.flatMap(MacMusicArtwork.init) : nil
        case .spotify:
            guard let source = provider.artworkScriptSources.first,
                  let result = Self.execute(source), result.numberOfItems == 4,
                  Self.artworkIdentityMatches(expected, values: (2...4).map { result.atIndex($0)?.stringValue ?? "" }),
                  let value = result.atIndex(1)?.stringValue,
                  let url = URL(string: value), Self.allowedSpotifyArtworkURL(url) else { return nil }
            do {
                var request = URLRequest(url: url, cachePolicy: .reloadIgnoringLocalCacheData,
                                         timeoutInterval: 5)
                request.setValue("AI-bot/0.1", forHTTPHeaderField: "User-Agent")
                let (data, response) = try await session.data(for: request)
                guard (response as? HTTPURLResponse)?.statusCode == 200,
                      response.url.map(Self.allowedSpotifyArtworkURL) == true,
                      !data.isEmpty, data.count <= Self.maximumArtworkBytes else { return nil }
                let cover = MacMusicArtwork(data)
                store.musicArtworkDiagnostic(cover == nil ? "音乐封面：Spotify · 图片无法解码" : "音乐封面：Spotify · 已解码")
                return cover
            } catch {
                return nil
            }
        }
    }

    private func readCatalogArtwork(_ track: MusicSnapshot) async -> MacMusicArtwork? {
        let now = ProcessInfo.processInfo.systemUptime
        let key = Self.artworkKey(track,from:.appleMusic)
        guard key != catalogAttemptKey || now-catalogAttemptAt >= 60 else { return nil }
        catalogAttemptKey = key; catalogAttemptAt = now
        var notes = [String]()
        defer { catalogNote = notes.joined(separator: "; ") }
        for country in ["hk", "us"] {
            let now = ProcessInfo.processInfo.systemUptime
            catalogRequests.removeAll { now - $0 >= 60 }
            guard catalogRequests.count < 18,
                  let request = MacMusicCatalogArtwork.request(track, country: country) else { notes.append("Apple 目录：请求限制或曲目信息不完整"); break }
            catalogRequests.append(now)
            do {
                let (metadata, response) = try await session.data(for: request)
                let status = (response as? HTTPURLResponse)?.statusCode ?? 0
                guard status == 200,
                      response.url?.scheme == "https", response.url?.host == "itunes.apple.com",
                      response.url?.path == "/search",
                      let url = MacMusicCatalogArtwork.match(metadata, track: track) else { notes.append("Apple 目录 \(country)：HTTP \(status)，无唯一精确匹配"); continue }
                if let artwork = await downloadCatalogArtwork(url) { notes = ["Apple 目录"]; return artwork }
                notes.append("Apple 目录 \(country)：封面不可用")
            } catch { notes.append("Apple 目录 \(country)：请求失败 \((error as NSError).code)"); continue }
        }
        if let artwork = await readWebArtwork(track) { notes = ["Apple Music 公开页面"]; return artwork }
        notes.append("Apple Music 公开页面：未能精确核对")
        return nil
    }

    private func readWebArtwork(_ track: MusicSnapshot) async -> MacMusicArtwork? {
        var country = Locale.current.region?.identifier.lowercased() ?? "us"
        // A regional redirect may point at the home page. Retry the public search
        // in that storefront once, never treat home-page recommendations as art.
        for _ in 0..<2 {
            guard let request = MacMusicWebArtwork.request(track,country:country),
                  let (search,response) = try? await session.data(for:request),
                  (response as? HTTPURLResponse)?.statusCode == 200, let final = response.url,
                  let actualCountry = MacMusicWebArtwork.country(final) else { return nil }
            if final.path != "/\(country)/search" {
                guard actualCountry != country else { return nil }
                country = actualCountry
                continue
            }
            guard let albumURL = MacMusicWebArtwork.albumURL(search,track:track),
                  let (page,albumResponse) = try? await session.data(for:MacMusicWebArtwork.pageRequest(albumURL)),
                  (albumResponse as? HTTPURLResponse)?.statusCode == 200,
                  let finalAlbum = albumResponse.url, MacMusicWebArtwork.sameAlbum(finalAlbum,albumURL),
                  let imageURL = MacMusicWebArtwork.match(page,track:track,albumURL:albumURL) else { return nil }
            return await downloadCatalogArtwork(imageURL)
        }
        return nil
    }

    private func downloadCatalogArtwork(_ url: URL) async -> MacMusicArtwork? {
        for imageURL in MacMusicCatalogArtwork.imageURLs(url) {
            guard let (image,response) = try? await session.data(for:URLRequest(url:imageURL,cachePolicy:.reloadIgnoringLocalCacheData,timeoutInterval:5)),
                  (response as? HTTPURLResponse)?.statusCode == 200,
                  response.url.map(MacMusicCatalogArtwork.allowedURL) == true,
                  let artwork = MacMusicArtwork(image) else { continue }
            return artwork
        }
        return nil
    }

    static func readAppleArtwork(expected: MusicSnapshot, execute: (String) -> NSAppleEventDescriptor?) -> (cover: Data?, diagnostic: String, verified: Bool, encoded: Data?) {
        var attempts = [String]()
        var selected: Data?
        var encoded: Data?
        var verified = false
        for (index, source) in MacMusicProvider.appleMusic.artworkScriptSources.enumerated() {
            let name = ["picture", "raw", "lookup-picture", "lookup-raw"][index]
            guard let result = execute(source) else { attempts.append(name + "=读取失败"); continue }
            guard result.numberOfItems == 5,
                  artworkIdentityMatches(expected, values: (3...5).map { result.atIndex($0)?.stringValue ?? "" }) else {
                attempts.append(name + "=曲目不匹配或无封面")
                continue
            }
            let artworkCount = result.atIndex(1)?.int32Value
            let descriptor = result.atIndex(2) ?? result
            let data = descriptor.data, cover = renderArtwork(data)
            // Scalar diagnostics stay in memory; never include track names or artwork bytes.
            attempts.append("\(name)=\(artworkCount.map { "count:\($0)/" } ?? "")\(String(format: "%08x", descriptor.descriptorType))/\(data.count)B/\(cover == nil ? "非有效图片" : "已解码")")
            if let cover {
                if let selected { attempts.append(selected == cover ? "候选图一致" : "候选图不一致") }
                if selected == nil || !verified && index >= 2 { selected = cover; encoded = data; verified = index >= 2 }
            }
        }
        return (selected, "音乐封面：Apple Music · " + attempts.joined(separator: "; "), verified, encoded)
    }

    static func artworkIdentityMatches(_ expected: MusicSnapshot, values: [String]) -> Bool {
        values.count == 3 && !expected.title.isEmpty &&
            values.map(normalized) == [expected.title, expected.artist, expected.album]
    }

    static func renderArtwork(_ encoded: Data) -> Data? {
        guard let image = artworkImage(encoded,maximum:112) else { return nil }
        // ImageIO applies EXIF orientation; RGB565 assets use a top-left origin.
        // Other renderer callers retain their existing coordinate convention.
        return MacRgb565Renderer.renderImage(image, width:112, height:112, topDown:true)
    }

    static func artworkImage(_ encoded: Data, maximum: Int) -> CGImage? {
        guard !encoded.isEmpty, encoded.count <= maximumArtworkBytes,
              let source = CGImageSourceCreateWithData(encoded as CFData, nil),
              CGImageSourceGetCount(source) > 0,
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = (properties[kCGImagePropertyPixelWidth] as? NSNumber)?.intValue,
              let height = (properties[kCGImagePropertyPixelHeight] as? NSNumber)?.intValue,
              (1...4_096).contains(width), (1...4_096).contains(height),
              let image = CGImageSourceCreateThumbnailAtIndex(source, 0, [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceThumbnailMaxPixelSize: maximum
              ] as CFDictionary) else { return nil }
        return image
    }

    static func allowedSpotifyArtworkURL(_ url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https", let host = url.host?.lowercased() else {
            return false
        }
        return host == "scdn.co" || host.hasSuffix(".scdn.co") ||
            host == "spotifycdn.com" || host.hasSuffix(".spotifycdn.com")
    }

    private static func execute(_ source: String) -> NSAppleEventDescriptor? {
        if !Thread.isMainThread { return DispatchQueue.main.sync { execute(source) } }
        guard let script = NSAppleScript(source: source) else { return nil }
        var error: NSDictionary?
        let result = script.executeAndReturnError(&error)
        return error == nil ? result : nil
    }

    /// Exercise the real sampling path while artwork is busy; no player/network.
    static func validateSampling() async throws -> Int {
        let suite = "AI-bot-music-sampling-fixture-" + UUID().uuidString
        let defaults = UserDefaults(suiteName:suite)!
        defer { defaults.removePersistentDomain(forName:suite) }
        let store = MacDataStore(defaults:defaults)
        return try await MacMusicService(store:store).validateSampling(store:store)
    }

    private func validateSampling(store: MacDataStore) async throws -> Int {
        var checked = 0
        func check(_ label: String, _ condition: Bool) throws {
            guard condition else { throw MacTab5LocalError.invalid("Music sampling check failed: " + label) }
            checked += 1
        }
        refreshing = true
        let old = MusicSnapshot(title:"Old fixture",artist:"Fixture",album:"Album",playing:true,elapsedSeconds:1,durationSeconds:100,updatedAt:Date(timeIntervalSince1970:1))
        store.update(music:old,cover:Data([1,2]))
        observedKey = Self.artworkKey(old,from:.appleMusic)
        let next = MusicSnapshot(title:"Next fixture",artist:"Fixture",album:"Album",playing:true,elapsedSeconds:20,durationSeconds:100,updatedAt:Date(timeIntervalSince1970:2))
        await refresh(enabled:true,isRunning:{ _ in true },read:{ _ in next })
        try check("new track sampled during artwork wait",store.snapshot().music?.title == next.title)
        try check("old cover cleared during artwork wait",store.snapshot().musicCover == nil)
        try check("in-flight identity invalidated",observedKey == Self.artworkKey(next,from:.appleMusic))
        let paused = MusicSnapshot(title:next.title,artist:next.artist,album:next.album,playing:false,elapsedSeconds:21.75,durationSeconds:100,updatedAt:Date(timeIntervalSince1970:3))
        await refresh(enabled:true,isRunning:{ _ in true },read:{ _ in paused })
        try check("pause sampled during artwork wait",store.snapshot().music?.playing == false)
        try check("fractional progress sampled during artwork wait",store.snapshot().music?.elapsedSeconds == 21.75)
        await refresh(enabled:true,isRunning:{ _ in true },read:{ _ in nil })
        try check("read failure retains latest sample",store.snapshot().music?.title == paused.title)
        for _ in 0..<2 { await refresh(enabled:true,isRunning:{ _ in false },read:{ _ in nil }) }
        try check("two empty polls retain latest sample",store.snapshot().music?.title == paused.title)
        await refresh(enabled:true,isRunning:{ _ in false },read:{ _ in nil })
        try check("confirmed stop clears sample while artwork busy",store.snapshot().music?.title.isEmpty == true && observedKey.isEmpty)
        await refresh(enabled:false,isRunning:{ _ in true },read:{ _ in next })
        try check("disabled sampler rejects pending identity",store.snapshot().music?.title.isEmpty == true && observedKey.isEmpty)
        return checked
    }
}
