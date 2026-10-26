import Foundation

/// Public Apple catalog metadata; no account, cookies or persistent media cache.
enum MacMusicCatalogArtwork {
    static func normalized(_ value: String) -> String {
        let simplified = value.applyingTransform(StringTransform("Traditional-Simplified"), reverse: false) ?? value
        return simplified.precomposedStringWithCompatibilityMapping
            .folding(options:[.caseInsensitive,.diacriticInsensitive,.widthInsensitive],locale:Locale(identifier:"en_US_POSIX"))
            .components(separatedBy:.whitespacesAndNewlines).filter { !$0.isEmpty }.joined(separator:" ")
    }
    static func titleKey(_ value: String) -> String {
        // Mainland and Traditional Chinese storefronts can localize the same
        // title with either pronoun. Artist/album and edition checks stay strict.
        normalized(value).replacingOccurrences(of:"妳",with:"你")
    }
    static func request(_ track: MusicSnapshot, country: String) -> URLRequest? {
        guard !track.title.isEmpty, !track.artist.isEmpty, !track.album.isEmpty,
              ["hk","us"].contains(country) else { return nil }
        var url = URLComponents(string:"https://itunes.apple.com/search")!
        url.queryItems = [URLQueryItem(name:"term",value:[track.title,track.artist,track.album].joined(separator:" ")),
                         URLQueryItem(name:"entity",value:"song"),URLQueryItem(name:"limit",value:"25"),URLQueryItem(name:"country",value:country)]
        var request = URLRequest(url:url.url!,cachePolicy:.reloadIgnoringLocalCacheData,timeoutInterval:5)
        request.setValue("AI-bot/0.1",forHTTPHeaderField:"User-Agent")
        return request
    }
    static func allowedURL(_ url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https", url.user == nil, url.password == nil,
              url.port == nil || url.port == 443, let host = url.host?.lowercased() else { return false }
        return host.hasSuffix(".mzstatic.com") || host.hasSuffix(".itunes.apple.com")
    }
    static func imageURLs(_ original: URL) -> [URL] {
        guard allowedURL(original) else { return [] }
        let name = original.lastPathComponent
        guard name.range(of:"^[0-9]{1,4}x[0-9]{1,4}bb\\.(jpg|jpeg|png)$",options:.regularExpression) != nil,
              var components = URLComponents(url:original,resolvingAgainstBaseURL:false) else { return [original] }
        components.path = String(components.path.dropLast(name.count)) + "600x600bb." + original.pathExtension
        guard let larger = components.url, larger != original else { return [original] }
        return [larger,original]
    }
    static func match(_ data: Data, track: MusicSnapshot) -> URL? {
        guard data.count <= 524_288, !track.title.isEmpty, !track.artist.isEmpty, !track.album.isEmpty,
              let object = try? JSONSerialization.jsonObject(with:data) as? [String:Any],
              let results = object["results"] as? [[String:Any]], results.count <= 50 else { return nil }
        var urls = Set<URL>()
        for result in results {
            guard result["kind"] as? String == "song",
                  let title = result["trackName"] as? String, let artist = result["artistName"] as? String,
                  let album = result["collectionName"] as? String,
                  titleKey(title) == titleKey(track.title), normalized(artist) == normalized(track.artist),
                  normalized(album) == normalized(track.album),
                  let duration = result["trackTimeMillis"] as? NSNumber,
                  duration.doubleValue.isFinite, duration.doubleValue > 0,
                  track.durationSeconds > 0, abs(duration.doubleValue / 1000 - track.durationSeconds) <= 2,
                  let text = result["artworkUrl100"] as? String, let url = URL(string:text), allowedURL(url) else { continue }
            urls.insert(url)
        }
        // Different editions may share names. Ambiguous artwork is never guessed.
        return urls.count == 1 ? urls.first : nil
    }
}
