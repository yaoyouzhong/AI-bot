import Foundation

/// Reads metadata embedded in public Music pages. No page scripts, account or cookies.
enum MacMusicWebArtwork {
    static func country(_ url: URL) -> String? {
        guard url.scheme == "https", url.host == "music.apple.com", url.user == nil,
              url.password == nil, url.port == nil || url.port == 443,
              let value = url.path.split(separator:"/").first.map(String.init),
              value.utf8.count == 2, value.utf8.allSatisfy({ (97...122).contains($0) }) else { return nil }
        return value
    }

    static func request(_ track: MusicSnapshot, country: String) -> URLRequest? {
        guard !track.title.isEmpty, !track.artist.isEmpty, !track.album.isEmpty,
              let base = URL(string:"https://music.apple.com/\(country)/search"),
              self.country(base) == country else { return nil }
        var url = URLComponents(url:base,resolvingAgainstBaseURL:false)!
        url.queryItems = [URLQueryItem(name:"term",value:track.title + " " + track.artist)]
        return pageRequest(url.url!)
    }

    static func pageRequest(_ url: URL) -> URLRequest {
        var request = URLRequest(url:url,cachePolicy:.reloadIgnoringLocalCacheData,timeoutInterval:5)
        request.setValue("AI-bot/0.1",forHTTPHeaderField:"User-Agent")
        return request
    }

    private static func albumID(_ url: URL) -> String? {
        guard country(url) != nil, url.query == nil, url.fragment == nil else { return nil }
        let parts = url.path.split(separator:"/")
        guard (3...4).contains(parts.count), parts[1] == "album", let id = parts.last,
              (8...12).contains(id.utf8.count), id.utf8.allSatisfy({ (48...57).contains($0) }) else { return nil }
        return String(id)
    }

    static func sameAlbum(_ lhs: URL, _ rhs: URL) -> Bool {
        guard let id = albumID(lhs) else { return false }
        return id == albumID(rhs) && country(lhs) == country(rhs)
    }

    private static func scripts(_ data: Data, marker: String) -> [Any] {
        guard data.count <= 2_097_152, let html = String(data:data,encoding:.utf8),
              let expression = try? NSRegularExpression(pattern:"(?is)<script\\b([^>]*)>(.*?)</script>") else { return [] }
        let source = html as NSString
        var values = [Any]()
        for match in expression.matches(in:html,range:NSRange(location:0,length:source.length)).prefix(64) {
            let attributes = source.substring(with:match.range(at:1)).lowercased()
            guard attributes.contains(marker), match.range(at:2).length <= 1_048_576,
                  let bytes = source.substring(with:match.range(at:2)).data(using:.utf8),
                  let value = try? JSONSerialization.jsonObject(with:bytes) else { continue }
            values.append(value)
        }
        return values
    }

    static func albumURL(_ data: Data, track: MusicSnapshot) -> URL? {
        var urls = Set<URL>(), budget = 20_000
        func visit(_ value: Any, depth: Int) {
            guard depth < 32, budget > 0 else { return }
            budget -= 1
            if let row = value as? [String:Any] {
                if let descriptor = row["contentDescriptor"] as? [String:Any], descriptor["kind"] as? String == "album",
                   let titles = row["titleLinks"] as? [[String:Any]], titles.count == 1,
                   let title = titles[0]["title"] as? String,
                   MacMusicCatalogArtwork.normalized(title) == MacMusicCatalogArtwork.normalized(track.album),
                   let artists = row["subtitleLinks"] as? [[String:Any]], artists.count == 1,
                   let artist = artists[0]["title"] as? String,
                   MacMusicCatalogArtwork.normalized(artist) == MacMusicCatalogArtwork.normalized(track.artist),
                   let text = descriptor["url"] as? String, let url = URL(string:text), albumID(url) != nil { urls.insert(url) }
                for child in row.values { visit(child,depth:depth+1) }
            } else if let rows = value as? [Any] {
                for child in rows { visit(child,depth:depth+1) }
            }
        }
        for value in scripts(data,marker:"serialized-server-data") { visit(value,depth:0) }
        return budget > 0 && urls.count == 1 ? urls.first : nil
    }

    static func match(_ data: Data, track: MusicSnapshot, albumURL: URL) -> URL? {
        guard !track.title.isEmpty, !track.artist.isEmpty, !track.album.isEmpty,
              track.durationSeconds > 0, track.durationSeconds.isFinite else { return nil }
        var urls = Set<URL>()
        for case let album as [String:Any] in scripts(data,marker:"application/ld+json") {
            guard album["@type"] as? String == "MusicAlbum", let title = album["name"] as? String,
                  MacMusicCatalogArtwork.normalized(title) == MacMusicCatalogArtwork.normalized(track.album),
                  let artists = album["byArtist"] as? [[String:Any]], artists.count == 1,
                  let artist = artists[0]["name"] as? String,
                  MacMusicCatalogArtwork.normalized(artist) == MacMusicCatalogArtwork.normalized(track.artist),
                  let text = album["url"] as? String, let canonical = URL(string:text), sameAlbum(canonical,albumURL),
                  let tracks = album["tracks"] as? [[String:Any]], tracks.count <= 250,
                  let image = album["image"] as? String, let imageURL = URL(string:image),
                  MacMusicCatalogArtwork.allowedURL(imageURL) else { continue }
            for item in tracks {
                guard item["@type"] as? String == "MusicRecording", let name = item["name"] as? String,
                      MacMusicCatalogArtwork.titleKey(name) == MacMusicCatalogArtwork.titleKey(track.title),
                      let duration = item["duration"] as? String, let seconds = seconds(duration),
                      abs(seconds - track.durationSeconds) <= 2 else { continue }
                urls.insert(imageURL)
            }
        }
        return urls.count == 1 ? urls.first : nil
    }

    private static func seconds(_ duration: String) -> Double? {
        guard let expression = try? NSRegularExpression(pattern:"^PT(?:(\\d+)H)?(?:(\\d+)M)?(?:(\\d+(?:\\.\\d+)?)S)?$"),
              let match = expression.firstMatch(in:duration,range:NSRange(duration.startIndex...,in:duration)) else { return nil }
        let source = duration as NSString
        var total = 0.0
        for (index, factor) in [(1,3600.0),(2,60.0),(3,1.0)] where match.range(at:index).location != NSNotFound {
            total += (Double(source.substring(with:match.range(at:index))) ?? 0) * factor
        }
        return total.isFinite && total > 0 ? total : nil
    }
}
