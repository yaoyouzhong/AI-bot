import Foundation

enum MacMusicCatalogArtworkValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ name: String, _ condition: @autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("Music catalog check failed: " + name) }
            checked += 1
        }
        let track = MusicSnapshot(title:"Synthetic song",artist:"Synthetic artist",album:"Synthetic album",playing:true,elapsedSeconds:0,durationSeconds:100,updatedAt:Date(timeIntervalSince1970:0))
        let url = "https://is1-ssl.mzstatic.com/image/thumb/synthetic/100x100bb.jpg"
        let result: [String:Any] = ["kind":"song","trackName":track.title,"artistName":track.artist,"collectionName":track.album,"trackTimeMillis":100000,"artworkUrl100":url]
        func bytes(_ rows: [[String:Any]]) throws -> Data { try JSONSerialization.data(withJSONObject:["results":rows]) }
        func matches(_ rows: [[String:Any]]) throws -> Bool { MacMusicCatalogArtwork.match(try bytes(rows),track:track) != nil }
        try check("all fields match",try matches([result]))
        let imageURLs = MacMusicCatalogArtwork.imageURLs(URL(string:url)!)
        try check("larger Apple source requested first",imageURLs.first?.lastPathComponent == "600x600bb.jpg" && imageURLs.last?.absoluteString == url)
        try check("untrusted source never upgraded",MacMusicCatalogArtwork.imageURLs(URL(string:"https://example.test/100x100bb.jpg")!).isEmpty)
        let unknown = URL(string:"https://is1-ssl.mzstatic.com/image/cover.jpg")!
        try check("unknown size path preserved",MacMusicCatalogArtwork.imageURLs(unknown) == [unknown])
        for field in ["trackName","artistName","collectionName"] {
            var wrong = result; wrong[field] = "Other version"
            try check("reject different " + field,try !matches([wrong]))
        }
        var duration = result; duration["trackTimeMillis"] = 110000
        try check("reject different recording duration",try !matches([duration]))
        duration["trackTimeMillis"] = 102000
        try check("allow two-second rounding",try matches([duration]))
        var other = result; other["artworkUrl100"] = "https://is2-ssl.mzstatic.com/image/thumb/other/100x100bb.jpg"
        try check("ambiguous editions rejected",try !matches([result,other]))
        try check("identical artwork duplicates accepted",try matches([result,result]))
        for text in ["http://is1-ssl.mzstatic.com/a.jpg","https://mzstatic.com.evil.test/a.jpg","https://is1-ssl.mzstatic.com@evil.test/a.jpg","https://user:password@is1-ssl.mzstatic.com/a.jpg","https://is1-ssl.mzstatic.com:8443/a.jpg"] {
            var wrong = result; wrong["artworkUrl100"] = text
            try check("untrusted artwork URL rejected",try !matches([wrong]))
        }
        let regional = MusicSnapshot(title:"给你",artist:track.artist,album:track.album,playing:true,elapsedSeconds:0,durationSeconds:100,updatedAt:track.updatedAt)
        var regionalRow = result; regionalRow["trackName"] = "給妳"
        try check("regional pronoun title accepted",MacMusicCatalogArtwork.match(try bytes([regionalRow]),track:regional) != nil)
        var wrongArtist = regionalRow; wrongArtist["artistName"] = "Other artist"
        try check("regional title still requires artist",MacMusicCatalogArtwork.match(try bytes([wrongArtist]),track:regional) == nil)
        var wrongAlbum = regionalRow; wrongAlbum["collectionName"] = "Other edition"
        try check("regional title still requires album",MacMusicCatalogArtwork.match(try bytes([wrongAlbum]),track:regional) == nil)
        var wrongDuration = regionalRow; wrongDuration["trackTimeMillis"] = 110000
        try check("regional title still requires recording duration",MacMusicCatalogArtwork.match(try bytes([wrongDuration]),track:regional) == nil)
        var ambiguous = regionalRow; ambiguous["artworkUrl100"] = "https://is2-ssl.mzstatic.com/image/other/100x100bb.jpg"
        try check("regional title ambiguous images rejected",MacMusicCatalogArtwork.match(try bytes([regionalRow,ambiguous]),track:regional) == nil)
        try check("pronoun compatibility limited to titles",MacMusicCatalogArtwork.normalized("給妳") != MacMusicCatalogArtwork.normalized("给你"))
        try check("traditional simplified equivalent",MacMusicCatalogArtwork.normalized("盧冠廷 陪著你走") == MacMusicCatalogArtwork.normalized("卢冠廷 陪着你走"))
        try check("width and case folded",MacMusicCatalogArtwork.normalized(" ＡＢＣ  Mix ") == "abc mix")
        try check("edition qualifiers retained",MacMusicCatalogArtwork.normalized("Album (Live)") != MacMusicCatalogArtwork.normalized("Album"))
        try check("HK request supported",MacMusicCatalogArtwork.request(track,country:"hk")?.url?.host == "itunes.apple.com")
        try check("unknown storefront rejected",MacMusicCatalogArtwork.request(track,country:"invalid") == nil)
        try check("malformed metadata rejected",MacMusicCatalogArtwork.match(Data([0,1,2]),track:track) == nil)
        try check("oversized metadata rejected",MacMusicCatalogArtwork.match(Data(repeating:0,count:524289),track:track) == nil)
        try check("no exact match remains unknown",try !matches([]))
        return checked
    }
}
