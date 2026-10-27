import Foundation
import zlib

enum MacTab5WiFiStatusValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name:String,_ ok:Bool) throws {
            guard ok else { throw MacTab5LocalError.invalid("Wi-Fi packed status: "+name) }; count += 1
        }
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        let nonce = String(repeating:"1",count:32)
        func headers(_ version:String,_ proof:String? = nil) -> [String:String] {
            ["x-tab5-firmware":version,"x-tab5-firmware-proof":proof ?? MacTab5Protocol.proof(key:pair.key,message:"FIRMWARE|\(nonce)|\(version)")]
        }
        for version in ["0.2.42-ui","0.2.276-ui","0.2.277-ui"] {
            try check("current proven compatible receiver",MacTab5WiFiStatus.supports(headers:headers(version),pair:pair,nonce:nonce))
        }
        for version in ["0.2.40-ui","0.2.41-ui","0.2.041-ui","0.2.042-ui","0.3.0-ui","0.2.277.1-ui","unknown"] {
            try check("old or unknown receiver stays raw",!MacTab5WiFiStatus.supports(headers:headers(version),pair:pair,nonce:nonce))
        }
        let good = headers("0.2.276-ui")
        try check("missing proof cannot use cached firmware",!MacTab5WiFiStatus.supports(headers:["x-tab5-firmware":"0.2.276-ui"],pair:pair,nonce:nonce))
        try check("invalid proof stays raw",!MacTab5WiFiStatus.supports(headers:headers("0.2.276-ui",String(repeating:"0",count:64)),pair:pair,nonce:nonce))
        try check("cross-request proof cannot negotiate",!MacTab5WiFiStatus.supports(headers:good,pair:pair,nonce:String(repeating:"2",count:32)))
        var unconfirmed = pair;unconfirmed.confirmed = false
        try check("unconfirmed pairing cannot negotiate",!MacTab5WiFiStatus.supports(headers:good,pair:unconfirmed,nonce:nonce))
        let full = try MacTab5Protocol.json(["type":"tab5_status","version":1,"deviceId":pair.deviceId,"session":String(repeating:"a",count:32),"sequence":13,"data":["codex":[:],"claude":[:],"fixture":String(repeating:"synthetic-only ",count:1800)]])
        let wire = try MacTab5WiFiStatus.wire(full,headers:good,pair:pair,nonce:nonce), root = try MacTab5Protocol.object(wire)
        try check("large repetitive state uses existing envelope",root["type"] as? String == "tab5_packed" && wire.count < full.count/2)
        try check("sequence and expanded bound retained",MacTab5Protocol.integer(root["sequence"]) == 13 && MacTab5Protocol.integer(root["size"]) == Int64(full.count))
        guard let payload = root["payload"] as? String, let zipped = Data(base64Encoded:payload) else { throw MacTab5Error.invalidPacket }
        var expanded = Data(count:full.count), size = uLongf(full.count)
        let result = expanded.withUnsafeMutableBytes { (out:UnsafeMutableRawBufferPointer) in zipped.withUnsafeBytes { (input:UnsafeRawBufferPointer) in
            uncompress(out.bindMemory(to:UInt8.self).baseAddress,&size,input.bindMemory(to:UInt8.self).baseAddress,uLong(input.count))
        } }
        try check("expanded plaintext byte-for-byte identical",result == Z_OK && size == full.count && expanded == full)
        let encrypted = try MacTab5Protocol.encrypt(wire,key:pair.key,challenge:nonce)
        try check("authentication still wraps the whole packed frame",try MacTab5Protocol.decrypt(encrypted,key:pair.key,challenge:nonce) == wire)
        try check("old request remains byte-identical raw",try MacTab5WiFiStatus.wire(full,headers:headers("0.2.41-ui"),pair:pair,nonce:nonce) == full)
        try check("missing negotiation remains raw",try MacTab5WiFiStatus.wire(full,headers:[:],pair:pair,nonce:nonce) == full)
        return count
    }
}
