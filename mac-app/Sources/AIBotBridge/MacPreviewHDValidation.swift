import Foundation
import CoreGraphics
import ImageIO

enum MacPreviewHDValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("HD preview: "+name) }; count += 1
        }
        try check("installed269 stays compatible",!MacTab5ScreenPreview.supportsHD(firmware:"0.2.269-ui"))
        try check("combined270 can negotiate HD",MacTab5ScreenPreview.supportsHD(firmware:"0.2.270-ui"))
        for value in ["0.2..270-ui","0.2.270","invalid"] { try check("malformed HD gate",!MacTab5ScreenPreview.supportsHD(firmware:value)) }
        var now = 10.0
        let model = MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
        let context = MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:["USB"],highDefinition:true)
        let token = model.start(context);model.renew(token,context:context)
        let command = model.command!, id = command["requestId"] as! String
        try check("HD uses version2 JPEG at panel resolution",command["version"] as? Int == 2 && command["encoding"] as? String == "jpeg" && command["width"] as? Int == 1280)
        var root:[String:Any] = ["streamId":token,"requestId":id,"op":"begin","encoding":"jpeg","width":720,"height":1280]
        func receive(_ body:[String:Any],_ bytes:Data = Data()) -> MacTab5RPCResult { model.receive(body,bytes:bytes,context:context,device:"fixture",transport:"USB") }
        try check("portrait geometry negotiated before capture",receive(root).status == 200 && model.command?["height"] as? Int == 1280)
        root["op"] = "unchanged";root["sha256"] = String(repeating:"0",count:64)
        try check("unchanged cannot invent a first frame",receive(root).status == 409)
        guard let imageContext = CGContext(data:nil,width:720,height:1280,bitsPerComponent:8,bytesPerRow:720*4,
            space:CGColorSpaceCreateDeviceRGB(),bitmapInfo:CGImageAlphaInfo.noneSkipLast.rawValue) else { throw MacTab5Error.invalidPacket }
        imageContext.setFillColor(CGColor(red:1,green:0,blue:0,alpha:1));imageContext.fill(CGRect(x:0,y:0,width:720,height:1280))
        guard let image = imageContext.makeImage() else { throw MacTab5Error.invalidPacket }
        let output = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(output,"public.jpeg" as CFString,1,nil) else { throw MacTab5Error.invalidPacket }
        CGImageDestinationAddImage(destination,image,nil);guard CGImageDestinationFinalize(destination) else { throw MacTab5Error.invalidPacket }
        let bytes = output as Data, sha = MacTab5Protocol.sha256(bytes)
        root.merge(["op":"frame","size":bytes.count,"offset":0,"sha256":sha]){_,v in v}
        for offset in stride(from:0,to:bytes.count,by:4096) {
            root["offset"] = offset
            try check("bounded portrait JPEG chunk",receive(root,bytes.subdata(in:offset..<min(bytes.count,offset+4096))).status == 200)
        }
        let frame = model.snapshot(token).0
        try check("actual JPEG becomes full panel RGB565",frame?.pixels.count == 720*1280*2 && (frame?.pixels[1] ?? 0)&0xf8 == 0xf8)
        now += 1;model.renew(token,context:context);root["requestId"] = model.command!["requestId"]
        root["op"] = "begin";try check("next portrait frame negotiates geometry",receive(root).status == 200)
        root["op"] = "unchanged";root.removeValue(forKey:"offset");root.removeValue(forKey:"size")
        try check("unchanged proof refreshes existing pixels",receive(root).status == 200 && model.snapshot(token).0?.pixels == frame?.pixels)
        now += 2.1;model.renew(token,context:context);root["requestId"] = model.command!["requestId"]
        root["op"] = "begin";try check("third portrait frame negotiates geometry",receive(root).status == 200)
        root["op"] = "unchanged";root["sha256"] = String(repeating:"0",count:64)
        try check("mismatched unchanged proof rejected",receive(root).status == 409)
        model.stop(token);try check("closed viewer cannot accept unchanged",receive(root).status == 409)
        try check("wrong dimensions rejected before JPEG raster",MacTab5ScreenPreview.decodeFrame(bytes,width:1280,height:720,encoding:"jpeg") == nil)
        try check("oversize JPEG dimensions rejected",MacTab5ScreenPreview.decodeFrame(bytes,width:1280,height:1280,encoding:"jpeg") == nil)
        try check("malformed JPEG rejected",MacTab5ScreenPreview.decodeFrame(Data([1,2,3]),width:1280,height:720,encoding:"jpeg") == nil)
        if let file = ProcessInfo.processInfo.environment["AIBOT_PREVIEW_JPEG_FIXTURE"] {
            let vector = try Data(contentsOf:URL(fileURLWithPath:file))
            guard let pixels = MacTab5ScreenPreview.decodeFrame(vector,width:1280,height:720,encoding:"jpeg") else { throw MacTab5Error.invalidPacket }
            func word(_ x:Int,_ y:Int)->UInt16 { let at=(y*1280+x)*2;return UInt16(pixels[at])|UInt16(pixels[at+1])<<8 }
            try check("actual firmware JPEG red corner",word(30,30)&0xf800 > 0xe000)
            try check("actual firmware JPEG green corner",word(1250,30)&0x7e0 > 0x700)
            try check("actual firmware JPEG blue corner",word(30,690)&0x1f > 0x1b)
        }
        return count
    }
}
