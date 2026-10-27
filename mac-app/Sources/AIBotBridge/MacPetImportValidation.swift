import Foundation
import CoreGraphics
import ImageIO

enum MacPetImportValidation {
    static func run() throws -> Int {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-pet-import-" + UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:false)
        defer { try? FileManager.default.removeItem(at:root) }
        try Data("Synthetic test fixture".utf8).write(to:root.appendingPathComponent("LICENSE.txt"))
        var count = 0
        func check(_ label: String, _ ok: Bool) throws {
            guard ok else { throw MacTab5LocalError.invalid("Pet import: " + label) }; count += 1
        }
        func image(_ reverse: Bool) throws -> CGImage {
            let pixels = (0..<16).flatMap { y -> [UInt8] in
                let color: [UInt8] = (y < 8) != reverse ? [255,0,0,255] : [0,0,255,255]
                return Array(repeating:color,count:16).flatMap { $0 }
            }
            guard let provider = CGDataProvider(data:Data(pixels) as CFData),
                  let value = CGImage(width:16,height:16,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:64,
                    space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.last.rawValue),
                    provider:provider,decode:nil,shouldInterpolate:false,intent:.defaultIntent) else { throw MacPetAssetError.renderFailed }
            return value
        }
        func word(_ bytes: Data, _ y: Int) -> UInt16 {
            let offset = (y * 112 + 56) * 2
            return UInt16(bytes[offset]) | UInt16(bytes[offset+1]) << 8
        }
        for ext in ["png","gif"] {
            let url = root.appendingPathComponent("fixture."+ext), frames = ext == "gif" ? 2:1
            guard let output = CGImageDestinationCreateWithURL(url as CFURL,(ext == "gif" ? "com.compuserve.gif":"public.png") as CFString,frames,nil) else { throw MacPetAssetError.renderFailed }
            for index in 0..<frames {
                CGImageDestinationAddImage(output,try image(index == 1),[kCGImagePropertyGIFDictionary:[kCGImagePropertyGIFDelayTime:Double(index+1)*0.12]] as CFDictionary)
            }
            guard CGImageDestinationFinalize(output) else { throw MacPetAssetError.renderFailed }
            let still = try MacPetAssetImporter.load(url)
            try check(ext+" static rows upright",word(still.data,8) == 0xf800 && word(still.data,103) == 0x001f)
            let pet = try MacPetAnimation(data:MacPetAssetImporter.loadAnimation(url).data)
            try check(ext+" frame count",pet.frames.count == frames)
            for index in 0..<frames {
                try check(ext+" frame order and orientation",word(pet.frames[index],8) == (index == 0 ? 0xf800:0x001f) && word(pet.frames[index],103) == (index == 0 ? 0x001f:0xf800))
            }
            if ext == "gif" { try check("GIF timing retained",pet.delays == [120,240]) }
            let cache = MacPetCache(directory:root.appendingPathComponent(ext))
            try cache.select("codex",data:pet.encode())
            try check("ESP receives same frames",try MacPetAnimation(data:cache.resources().first!.data).frames == pet.frames)
            let expected = try MacTab5Asset.rgb565(slot:1,width:pet.width,height:pet.height,delays:pet.delays,raw:pet.frames.reduce(into:Data()) { $0.append($1) })
            let assets = MacTab5Assets(); assets.updatePets([nil,cache.selection("codex")])
            try check("TAB5 receives same frames",(assets.projection(includeResource:false)["assetIds"] as? [Any])?[1] as? String == expected.id)
        }
        return count
    }
}
