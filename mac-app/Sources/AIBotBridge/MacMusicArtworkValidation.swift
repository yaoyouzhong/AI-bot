import AppKit
import Foundation
import ImageIO

/// Synthetic pixels only: no player, network, real artwork or persistent state.
enum MacMusicArtworkValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ label: String, _ condition: @autoclosure () -> Bool) throws {
            guard condition() else { throw MacTab5LocalError.invalid("Music artwork check failed: " + label) }
            checked += 1
        }
        let colors: [[UInt8]] = [[255,0,0,255], [0,255,0,255], [0,0,255,255], [255,255,0,255]]
        let pixels = (0..<16).flatMap { y in (0..<16).flatMap { x in colors[(y < 8 ? 0 : 2) + (x < 8 ? 0 : 1)] } }
        guard let provider = CGDataProvider(data: Data(pixels) as CFData),
              let image = CGImage(width:16,height:16,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:64,
                space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.premultipliedLast.rawValue | CGBitmapInfo.byteOrder32Big.rawValue),
                provider:provider,decode:nil,shouldInterpolate:false,intent:.defaultIntent) else { throw MacTab5LocalError.invalid("Synthetic artwork image") }
        func encoded(_ orientation: Int, image: CGImage = image) throws -> Data {
            let bytes = NSMutableData()
            guard let destination = CGImageDestinationCreateWithData(bytes,"public.tiff" as CFString,1,nil) else { throw MacTab5LocalError.invalid("Synthetic artwork destination") }
            CGImageDestinationAddImage(destination,image,[kCGImagePropertyOrientation:orientation] as CFDictionary)
            guard CGImageDestinationFinalize(destination) else { throw MacTab5LocalError.invalid("Synthetic artwork encoding") }
            return bytes as Data
        }
        func word(_ data: Data, _ x: Int, _ y: Int) -> UInt16 {
            let offset = (y * 112 + x) * 2
            return UInt16(data[offset]) | UInt16(data[offset + 1]) << 8
        }
        let r: UInt16 = 0xf800, g: UInt16 = 0x07e0, b: UInt16 = 0x001f, y: UInt16 = 0xffe0
        let expected = [[r,g,b,y],[g,r,y,b],[y,b,g,r],[b,y,r,g],[r,b,g,y],[b,r,y,g],[y,g,b,r],[g,y,r,b]]
        for orientation in 1...8 {
            guard let cover = MacMusicService.renderArtwork(try encoded(orientation)) else { throw MacTab5LocalError.invalid("Orientation decode") }
            try check("RGB565 size",cover.count == 112 * 112 * 2)
            let corners = [word(cover,8,8),word(cover,103,8),word(cover,8,103),word(cover,103,103)]
            try check("EXIF orientation \(orientation)",corners == expected[orientation - 1])
        }
        func resized(_ width: Int, _ height: Int) throws -> CGImage {
            guard let context = CGContext(data:nil,width:width,height:height,bitsPerComponent:8,bytesPerRow:width*4,
                space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipLast.rawValue) else { throw MacTab5LocalError.invalid("HD fixture context") }
            context.interpolationQuality = .none
            context.draw(image,in:CGRect(x:0,y:0,width:width,height:height))
            return context.makeImage()!
        }
        let highImage = try resized(672,672)
        var hd = Data()
        for orientation in 1...8 {
            guard let artwork = MacMusicArtwork(try encoded(orientation,image:highImage)), let jpeg = artwork.tab5,
                  let asset = try? MacTab5Asset.jpeg(jpeg), let decoded = MacMusicService.renderArtwork(jpeg) else { throw MacTab5LocalError.invalid("HD artwork fixture") }
            hd = jpeg
            try check("HD fits square cover",asset.width == 336 && asset.height == 336)
            try check("HD encoding stays JPEG",asset.encoding == "jpeg" && jpeg.count <= 524288)
            let actual = [word(decoded,8,8),word(decoded,103,8),word(decoded,8,103),word(decoded,103,103)]
            let close = zip(actual,expected[orientation-1]).allSatisfy { pair in
                let a = Int(pair.0), b = Int(pair.1)
                let red = abs((a >> 11)-(b >> 11)) <= 2
                let green = abs(((a >> 5)&63)-((b >> 5)&63)) <= 2
                let blue = abs((a&31)-(b&31)) <= 2
                return red && green && blue
            }
            try check("HD EXIF orientation \(orientation)",close)
            try check("small screen keeps 112 RGB565",artwork.small.count == 112*112*2)
        }
        for (width,height,targetWidth,targetHeight) in [(1200,600,560,280),(600,1200,168,336),(16,16,16,16)] {
            guard let jpeg = MacMusicArtwork.renderTab5(try encoded(1,image:resized(width,height))),
                  let asset = try? MacTab5Asset.jpeg(jpeg) else { throw MacTab5LocalError.invalid("HD aspect fixture") }
            try check("HD aspect and no upscaling",asset.width == targetWidth && asset.height == targetHeight)
        }
        let hdAssets = MacTab5Assets()
        hdAssets.updateCover(hd)
        let chunk = hdAssets.projection()["resource"] as! [String:Any]
        try check("HD resource chunk advertises JPEG",chunk["encoding"] as? String == "jpeg" && chunk["width"] as? Int == 336 && chunk["height"] as? Int == 336)
        try check("HD resource CRC covers encoded bytes",chunk["id"] as? String == String(format:"%08x",MacBinaryResourceProtocol.crc32(Array(hd))))
        try check("HD chunk remains bounded",Data(base64Encoded:chunk["bytes"] as! String)!.count <= 1024)
        hdAssets.updateCover(hd,jpegSupported:false)
        let legacyChunk = hdAssets.projection()["resource"] as! [String:Any]
        try check("older firmware retains legacy cover",legacyChunk["encoding"] as? String == "rle565" && legacyChunk["width"] as? Int == 112)
        try check("JPEG firmware capability",MacTab5Asset.supportsJPEG("0.2.267-ui") && MacTab5Asset.supportsJPEG("0.2.80-ui") && !MacTab5Asset.supportsJPEG("0.2.79-ui") && !MacTab5Asset.supportsJPEG("") && !MacTab5Asset.supportsJPEG("unknown"))
        let track = MusicSnapshot(title:"Synthetic",artist:"Fixture",album:"Album A",playing:true,elapsedSeconds:0,durationSeconds:1,updatedAt:Date(timeIntervalSince1970:0))
        func reply(_ descriptor: NSAppleEventDescriptor, title: String = "Synthetic", artist: String = "Fixture", album: String = "Album A") -> NSAppleEventDescriptor {
            let list = NSAppleEventDescriptor.list()
            list.insert(NSAppleEventDescriptor(int32:1),at:1); list.insert(descriptor,at:2)
            for (index,value) in [title,artist,album].enumerated() { list.insert(NSAppleEventDescriptor(string:value),at:index+3) }
            return list
        }
        let valid = try encoded(1)
        let picture = NSAppleEventDescriptor(descriptorType:FourCharCode(0x54494646),data:valid)!
        let missing = NSAppleEventDescriptor(descriptorType:FourCharCode(0x74797065),data:Data([0x6d,0x73,0x6e,0x67]))!
        var calls = 0
        let fallback = MacMusicService.readAppleArtwork(expected:track) { _ in calls += 1; return reply(calls == 1 ? missing : picture) }
        try check("missing-value bytes do not block picture fallback",calls == 4 && fallback.cover != nil)
        calls = 0
        let failedRaw = MacMusicService.readAppleArtwork(expected:track) { _ in calls += 1; return calls == 1 ? nil : reply(picture) }
        try check("script failure permits picture fallback",calls == 4 && failedRaw.cover != nil)
        calls = 0
        let raw = MacMusicService.readAppleArtwork(expected:track) { _ in calls += 1; return reply(picture) }
        try check("current and library candidates are compared",calls == 4 && raw.cover != nil)
        try check("invalid data has no cover",MacMusicService.renderArtwork(Data([1,2,3,4])) == nil)
        try check("missing descriptor has no cover",MacMusicService.readAppleArtwork(expected:track) { _ in missing }.cover == nil)
        try check("empty data rejected",MacMusicService.renderArtwork(Data()) == nil)
        try check("oversized data rejected",MacMusicService.renderArtwork(Data(repeating:0,count:10*1024*1024+1)) == nil)
        calls = 0
        let mismatch = MacMusicService.readAppleArtwork(expected:track) { _ in calls += 1; return reply(picture,artist:"Previous artist") }
        try check("previous artist cover rejected",calls == 4 && mismatch.cover == nil)
        try check("same title on other album rejected",!MacMusicService.artworkIdentityMatches(track,values:[track.title,track.artist,"Album B"]))
        try check("different title rejected",!MacMusicService.artworkIdentityMatches(track,values:["Other",track.artist,track.album]))
        let suite = "AI-bot-artwork-fixture-" + UUID().uuidString
        let defaults = UserDefaults(suiteName:suite)!
        defer { defaults.removePersistentDomain(forName:suite) }
        let store = MacDataStore(defaults:defaults)
        let oldCover = Data(repeating:1,count:112*112*2)
        store.update(music:track,cover:oldCover,tab5Cover:hd)
        try check("HD stored independently of small cover",store.snapshot().musicCover == oldCover && store.snapshot().musicTab5Cover == hd)
        let next = MusicSnapshot(title:"Next synthetic",artist:"Next artist",album:"Next album",playing:true,elapsedSeconds:0,durationSeconds:1,updatedAt:Date(timeIntervalSince1970:1))
        store.update(music:next)
        try check("track change clears old cover immediately",store.snapshot().musicCover == nil && store.snapshot().musicTab5Cover == nil && store.snapshot().music?.hasArtwork == false)
        store.update(music:track,cover:oldCover,tab5Cover:hd)
        try check("late previous track cover cannot return",store.snapshot().music?.title == next.title && store.snapshot().musicCover == nil && store.snapshot().musicTab5Cover == nil)
        let assets = MacTab5Assets()
        assets.updateCover(oldCover)
        let oldID = (assets.projection(includeResource:false)["assetIds"] as! [Any])[2] as! String
        assets.updateCover(nil)
        try check("pending track removes resource ID",(assets.projection(includeResource:false)["assetIds"] as! [Any])[2] is NSNull)
        assets.updateCover(Data(repeating:2,count:112*112*2))
        try check("new cover has distinct resource identity",(assets.projection(includeResource:false)["assetIds"] as! [Any])[2] as? String != oldID)
        let other = MusicSnapshot(title:track.title,artist:track.artist,album:"Album B",playing:true,elapsedSeconds:0,durationSeconds:1,updatedAt:track.updatedAt)
        try check("album participates in artwork identity",MacMusicService.artworkKey(track,from:.appleMusic) != MacMusicService.artworkKey(other,from:.appleMusic))
        try check("providers have separate artwork identity",MacMusicService.artworkKey(track,from:.appleMusic) != MacMusicService.artworkKey(track,from:.spotify))
        let legacy = MacRgb565Renderer.renderImage(image,width:16,height:16)!
        let topDown = MacRgb565Renderer.renderImage(image,width:16,height:16,topDown:true)!
        try check("existing pet renderer convention preserved",legacy[0] == 0x1f && legacy[1] == 0 && topDown[0] == 0 && topDown[1] == 0xf8)
        checked += try MacMusicCatalogArtworkValidation.run()
        checked += try MacMusicWebArtworkValidation.run()
        return checked
    }
}
