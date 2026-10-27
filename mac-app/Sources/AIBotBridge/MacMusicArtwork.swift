import Foundation
import ImageIO

/// Keep the small-screen RGB565 resource separate from TAB5's JPEG resource.
struct MacMusicArtwork {
    let small: Data
    let tab5: Data?

    init?(_ encoded: Data) {
        guard let small = MacMusicService.renderArtwork(encoded) else { return nil }
        self.small = small
        self.tab5 = Self.renderTab5(encoded)
    }

    static func renderTab5(_ encoded: Data) -> Data? {
        guard let image = MacMusicService.artworkImage(encoded, maximum:560) else { return nil }
        let scale = min(1, min(560 / Double(image.width), 336 / Double(image.height)))
        let width = max(1, Int((Double(image.width) * scale).rounded(.down)))
        let height = max(1, Int((Double(image.height) * scale).rounded(.down)))
        guard let context = CGContext(data:nil,width:width,height:height,bitsPerComponent:8,bytesPerRow:width*4,
                                      space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipLast.rawValue) else { return nil }
        context.interpolationQuality = .high
        context.draw(image,in:CGRect(x:0,y:0,width:width,height:height))
        guard let rendered = context.makeImage() else { return nil }
        let output = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(output,"public.jpeg" as CFString,1,nil) else { return nil }
        CGImageDestinationAddImage(destination,rendered,[kCGImageDestinationLossyCompressionQuality:0.88] as CFDictionary)
        guard CGImageDestinationFinalize(destination), output.length <= 524288 else { return nil }
        return output as Data
    }
}
