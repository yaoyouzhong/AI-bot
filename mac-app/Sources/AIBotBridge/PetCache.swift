import AppKit
import Foundation

struct MacPetAnimation {
    let width: Int
    let height: Int
    let delays: [Int]
    let frames: [Data]
    init(data: Data) throws {
        let b = [UInt8](data)
        guard b.count >= 14, Array(b[0..<4]) == [65,80,69,84], [1,2].contains(b[4]), (1...8).contains(Int(b[5])), b[10] == 0, b[11] == 0 else { throw MacPetAssetError.decodeFailed }
        width = Int(b[6]) | Int(b[7]) << 8; height = Int(b[8]) | Int(b[9]) << 8
        let count = Int(b[5]), size = width * height * 2
        guard (1...120).contains(width), (1...120).contains(height), b[4] != 1 || width == 112 && height == 112,
              b.count == 12 + 2 * count + size * count else { throw MacPetAssetError.decodeFailed }
        delays = (0..<count).map { Int(b[12+$0*2]) | Int(b[13+$0*2]) << 8 }
        guard delays.allSatisfy({ (20...60000).contains($0) }) else { throw MacPetAssetError.decodeFailed }
        frames = (0..<count).map { Data(b[(12+2*count+$0*size)..<(12+2*count+($0+1)*size)]) }
    }
    init(width: Int, height: Int, delays: [Int], frames: [Data]) {
        self.width=width; self.height=height; self.delays=delays; self.frames=frames
    }
    func encode() throws -> Data {
        guard (1...120).contains(width), (1...120).contains(height), (1...8).contains(frames.count), delays.count == frames.count,
              delays.allSatisfy({ (20...60000).contains($0) }), frames.allSatisfy({ $0.count == width*height*2 }) else { throw MacPetAssetError.decodeFailed }
        var result=Data([65,80,69,84,UInt8(width == 112 && height == 112 ? 1:2),UInt8(frames.count),UInt8(width),0,UInt8(height),0,0,0])
        for d in delays { result.append(UInt8(d&255)); result.append(UInt8(d>>8)) }
        frames.forEach { result.append($0) }; return result
    }
    func image(milliseconds: Int) -> NSImage? {
        var phase = max(0,milliseconds) % delays.reduce(0,+), index=0
        while index+1<delays.count && phase>=delays[index] { phase-=delays[index]; index+=1 }
        return Self.image(frames[index], width: width, height: height)
    }
    static func image(_ pixels: Data, width: Int, height: Int) -> NSImage? {
        guard pixels.count==width*height*2 else { return nil }
        let p=[UInt8](pixels); var rgba=[UInt8](); rgba.reserveCapacity(width*height*4)
        for i in stride(from:0,to:p.count,by:2) {
            let v=Int(p[i])|Int(p[i+1])<<8
            rgba.append(UInt8(((v>>11)&31)*255/31));rgba.append(UInt8(((v>>5)&63)*255/63));rgba.append(UInt8((v&31)*255/31));rgba.append(255)
        }
        guard let provider=CGDataProvider(data: Data(rgba) as CFData), let image=CGImage(width:width,height:height,bitsPerComponent:8,bitsPerPixel:32,bytesPerRow:width*4,space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGBitmapInfo(rawValue:CGImageAlphaInfo.last.rawValue),provider:provider,decode:nil,shouldInterpolate:false,intent:.defaultIntent) else { return nil }
        return NSImage(cgImage:image,size:NSSize(width:width,height:height))
    }
}

final class MacPetCache {
    static let shared = MacPetCache()
    private let lock=NSRecursiveLock()
    private let directory: URL
    init(directory: URL? = nil) {
        self.directory=directory ?? FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("AI-bot/pets",isDirectory:true)
    }
    private func file(_ owner:String,_ suffix:String)->URL { directory.appendingPathComponent(owner+suffix+".apet") }
    func selection(_ owner:String)->MacPetAnimation? {
        guard ["claude","codex"].contains(owner) else { return nil }
        lock.lock(); defer { lock.unlock() }
        for suffix in ["-selected","-default"] {
            if let data=try? Data(contentsOf:file(owner,suffix)),let animation=try? MacPetAnimation(data:data) { return animation }
        }
        return nil
    }
    func select(_ owner:String,data:Data) throws {
        guard ["claude","codex"].contains(owner) else { throw MacPetAssetError.decodeFailed }
        _ = try MacPetAnimation(data:data)
        lock.lock();defer {lock.unlock()}
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
        let target=file(owner,"-selected")
        if let previous=try? Data(contentsOf:target) { try previous.write(to:file(owner,"-previous"),options:.atomic) }
        try data.write(to:target,options:.atomic)
    }
    func restore(_ owner:String) throws {
        guard ["claude","codex"].contains(owner) else { throw MacPetAssetError.decodeFailed }
        try select(owner,data:Data(contentsOf:file(owner,"-default")))
    }
    func resources()->[MacResourcePayload] {
        let pets:[MacResourcePayload] = ["claude","codex"].compactMap { owner in
            guard let animation=selection(owner),let data=try? animation.encode() else { return nil }
            return MacResourcePayload(kind:owner=="claude" ? .claudePetAnimation:.codexPetAnimation,revision:Int(MacBinaryResourceProtocol.crc32(Array(data))),data:data)
        }
        let logos:[MacResourcePayload] = ["claude","codex"].compactMap {owner in
            guard let data=try? Data(contentsOf:directory.appendingPathComponent(owner+"-logo.rgb565")),data.count==3200 else{return nil}
            return MacResourcePayload(kind:owner=="claude" ? .claudeLogo:.codexLogo,revision:Int(MacBinaryResourceProtocol.crc32(Array(data))),data:data)
        }
        return pets+logos
    }
    func importLogos(from root:URL) throws {
        var imported:[String:Data]=[:]
        for owner in ["claude","codex"] {
            let file=root.appendingPathComponent("firmware/include/img/\(owner)_logo.h")
            guard let size=try file.resourceValues(forKeys:[.fileSizeKey]).fileSize,size<=100000 else{throw MacPetAssetError.invalidFile}
            let source=try String(contentsOf:file,encoding:.utf8)
            for dimension in ["W","H"] {
                let sizePattern="#define\\s+"+owner.uppercased()+"_LOGO_"+dimension+"\\s+40\\b"
                guard source.range(of:sizePattern,options:.regularExpression) != nil else {throw MacPetAssetError.decodeFailed}
            }
            let pattern="\\b"+owner+"_logo_0\\[1600\\]\\s+PROGMEM\\s*=\\s*\\{([^}]+)\\}"
            let regex=try NSRegularExpression(pattern:pattern)
            guard let match=regex.firstMatch(in:source,range:NSRange(source.startIndex...,in:source)),let range=Range(match.range(at:1),in:source) else{throw MacPetAssetError.decodeFailed}
            var bytes=Data()
            for item in source[range].split(separator:",") {
                let word=item.trimmingCharacters(in:.whitespacesAndNewlines);if word.isEmpty{continue}
                guard let value=UInt16(word.hasPrefix("0x") ? String(word.dropFirst(2)):word,radix:word.hasPrefix("0x") ? 16:10) else{throw MacPetAssetError.decodeFailed}
                bytes.append(UInt8(value>>8));bytes.append(UInt8(value&255))
            }
            guard bytes.count==3200 else{throw MacPetAssetError.decodeFailed};imported[owner]=bytes
        }
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
        for (owner,data) in imported {try data.write(to:directory.appendingPathComponent(owner+"-logo.rgb565"),options:.atomic)}
    }
    func importDefaults(from root:URL) throws {
        var values:[String:Data]=[:]
        for owner in ["claude","codex"] {
            let url=root.appendingPathComponent("firmware/include/img/\(owner)_sprite.h")
            let data=try Data(contentsOf:url)
            guard data.count<=4_000_000,let text=String(data:data,encoding:.utf8) else {throw MacPetAssetError.decodeFailed}
            let pattern="\\b"+owner+"_sprite_[0-9]+\\[[0-9]+\\]\\s+PROGMEM\\s*=\\s*\\{([^}]+)\\}"
            let regex=try NSRegularExpression(pattern:pattern)
            let matches=regex.matches(in:text,range:NSRange(text.startIndex...,in:text))
            let width=owner=="claude" ? 111:120
            var frames:[Data]=[]
            for match in matches {
                guard let range=Range(match.range(at:1),in:text) else {throw MacPetAssetError.decodeFailed}
                var bytes=Data()
                for word in text[range].split(separator:",") {
                    let value=word.trimmingCharacters(in:.whitespacesAndNewlines)
                    if value.isEmpty {continue}
                    guard let number=UInt16(value.hasPrefix("0x") ? String(value.dropFirst(2)):value,radix:value.hasPrefix("0x") ? 16:10) else {throw MacPetAssetError.decodeFailed}
                    // Legacy uint16 words are pre-swapped; APET is natural RGB565 LE.
                    bytes.append(UInt8(number>>8));bytes.append(UInt8(number&255))
                }
                guard bytes.count==width*120*2 else {throw MacPetAssetError.decodeFailed};frames.append(bytes)
            }
            guard frames.count==6 else {throw MacPetAssetError.decodeFailed}
            values[owner]=try MacPetAnimation(width:width,height:120,delays:Array(repeating:120,count:6),frames:frames).encode()
        }
        lock.lock();defer {lock.unlock()}
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
        for (owner,data) in values {try data.write(to:file(owner,"-default"),options:.atomic)}
    }
}
