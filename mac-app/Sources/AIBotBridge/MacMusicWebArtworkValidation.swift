import Foundation

enum MacMusicWebArtworkValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ label: String, _ condition: @autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("Music public page check failed: " + label) }
            checked += 1
        }
        let track = MusicSnapshot(title:"Synthetic song",artist:"Synthetic artist",album:"Synthetic album",playing:false,elapsedSeconds:0,durationSeconds:198,updatedAt:Date(timeIntervalSince1970:0))
        let albumURL = URL(string:"https://music.apple.com/us/album/synthetic/123456789")!
        let image = "https://is1-ssl.mzstatic.com/image/thumb/synthetic/1200x630bb.jpg"
        let row: [String:Any] = ["contentDescriptor":["kind":"album","url":albumURL.absoluteString],"titleLinks":[["title":track.album]],"subtitleLinks":[["title":track.artist]]]
        let song: [String:Any] = ["@type":"MusicRecording","name":track.title,"duration":"PT3M18S"]
        let album: [String:Any] = ["@type":"MusicAlbum","name":track.album,"byArtist":[["name":track.artist]],"tracks":[song],"url":albumURL.absoluteString,"image":image]
        func page(_ value: Any, search: Bool = false) throws -> Data {
            let json = String(data:try JSONSerialization.data(withJSONObject:value),encoding:.utf8)!
            return Data(("<script " + (search ? "id=\"serialized-server-data\" type=\"application/json\"" : "type=\"application/ld+json\"") + ">" + json + "</script>").utf8)
        }
        func search(_ rows: [[String:Any]]) throws -> URL? { MacMusicWebArtwork.albumURL(try page(["data":[["sections":[["items":rows]]]]],search:true),track:track) }
        func match(_ value: [String:Any]) throws -> URL? { MacMusicWebArtwork.match(try page(value),track:track,albumURL:albumURL) }
        try check("public search album selected",try search([row]) == albumURL)
        try check("duplicate album cards accepted",try search([row,row]) == albumURL)
        var other = row; other["contentDescriptor"] = ["kind":"album","url":"https://music.apple.com/us/album/other/987654321"]
        try check("ambiguous editions rejected",try search([row,other]) == nil)
        for field in ["titleLinks","subtitleLinks"] {
            var wrong = row; wrong[field] = [["title":"Other edition"]]
            try check("strict search " + field,try search([wrong]) == nil)
        }
        try check("structured album and recording match",try match(album)?.absoluteString == image)
        try check("landscape social image uses square source",MacMusicCatalogArtwork.imageURLs(URL(string:image)!).first?.lastPathComponent == "600x600bb.jpg")
        for field in ["name","byArtist","image","url"] {
            var wrong = album
            wrong[field] = field == "byArtist" ? [["name":"Other artist"]] : "https://example.test/other"
            try check("strict album " + field,try match(wrong) == nil)
        }
        for duration in ["PT3M28S","PT","3:18","PT0S","PTNaNS"] {
            var wrong = album, item = song; item["duration"] = duration; wrong["tracks"] = [item]
            try check("wrong or malformed duration rejected",try match(wrong) == nil)
        }
        var live = album, item = song; item["name"] = track.title + " (Live)"; live["tracks"] = [item]
        try check("live recording rejected",try match(live) == nil)
        var multi = album; multi["byArtist"] = [["name":track.artist],["name":"Other artist"]]
        try check("ambiguous artist rejected",try match(multi) == nil)
        try check("home page is not an album",!MacMusicWebArtwork.sameAlbum(URL(string:"https://music.apple.com/us/new")!,albumURL))
        try check("regional album IDs cannot cross storefronts",!MacMusicWebArtwork.sameAlbum(URL(string:"https://music.apple.com/cn/album/synthetic/123456789")!,albumURL))
        try check("public request contains no authorization",MacMusicWebArtwork.request(track,country:"cn")?.value(forHTTPHeaderField:"Authorization") == nil)
        try check("malformed country rejected",MacMusicWebArtwork.request(track,country:"us/../../evil") == nil)
        try check("untrusted page rejected",MacMusicWebArtwork.country(URL(string:"https://music.apple.com.evil.test/us/search")!) == nil)
        try check("malformed document rejected",MacMusicWebArtwork.match(Data([0,1]),track:track,albumURL:albumURL) == nil)
        try check("oversized document rejected",MacMusicWebArtwork.albumURL(Data(repeating:32,count:2_097_153),track:track) == nil)
        return checked
    }
}
