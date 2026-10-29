import CryptoKit
import Foundation
import zlib

enum MacTab5OTATransferValidation {
    static func decode(_ stream:Data, expected:Data) throws {
        var at = 0, output = Data()
        while at < stream.count {
            guard at+16 <= stream.count else { throw MacTab5Error.invalidPacket }
            let offset = Int(MacTab5Mailbox.u32(stream,at)), decoded = Int(MacTab5Mailbox.u32(stream,at+4))
            let encoded = Int(MacTab5Mailbox.u32(stream,at+8)), kind = MacTab5Mailbox.u32(stream,at+12)
            at += 16
            guard offset == output.count, (1...49152).contains(decoded), (1...decoded).contains(encoded),
                  at+encoded <= stream.count, kind <= 1 else { throw MacTab5Error.invalidPacket }
            let packed = Data(stream[at..<at+encoded]); at += encoded
            if kind == 0 { guard encoded == decoded else { throw MacTab5Error.invalidPacket };output.append(packed) }
            else {
                var plain = [UInt8](repeating:0,count:decoded), size = uLongf(decoded)
                let status = packed.withUnsafeBytes { source in
                    plain.withUnsafeMutableBufferPointer { uncompress($0.baseAddress,&size,source.bindMemory(to:UInt8.self).baseAddress,uLong(packed.count)) }
                }
                guard status == Z_OK, size == decoded, encoded < decoded else { throw MacTab5Error.invalidPacket }
                output.append(contentsOf:plain)
            }
        }
        guard output == expected else { throw MacTab5Error.invalidPacket }
    }
    static func run() throws -> Int {
        var checked = 0
        func check(_ value:Bool) throws { guard value else { throw MacTab5Error.invalidPacket };checked += 1 }
        var random = Data(), state:UInt32 = 17
        for _ in 0..<49152 { state ^= state << 13;state ^= state >> 17;state ^= state << 5;random.append(UInt8(truncatingIfNeeded:state)) }
        for raw in [Data([4]),random,Data(repeating:65,count:49152),Data(repeating:0,count:110000)+random] {
            let stream = MacTab5OTAEncoding.stream(raw);try decode(stream,expected:raw);checked += 1
            try check(stream.count <= raw.count+((raw.count+49151)/49152)*16)
            if raw.count <= 49152 { try check(MacTab5OTAEncoding.range(raw,offset:0,enabled:false).bytes == raw) }
        }
        try check(MacTab5OTAEncoding.range(random,offset:9,enabled:true).body["encoding"] == nil)
        for flow in ["","tcp-v1","tcp-v2","TCP-V2"] {
            for encoding in ["",MacTab5OTAEncoding.name,"unknown"] {
                try check(MacTab5OTAEncoding.negotiated(["x-aibot-ota-flow":flow,"x-aibot-ota-encoding":encoding]) == (flow == "tcp-v2" && encoding == MacTab5OTAEncoding.name))
            }
        }
        let raw = Data(repeating:65,count:50000), good = MacTab5OTAEncoding.stream(raw)
        var wrongOffset = good;wrongOffset[0] = 1
        for bad in [Data(good.dropLast()),wrongOffset] {
            do { try decode(bad,expected:raw);throw MacTab5LocalError.invalid("Corrupt OTA test stream accepted") }
            catch MacTab5Error.invalidPacket { checked += 1 }
        }
        for (version, supported) in [(nil,false),("",false),("0.2.256-ui",false),("0.2.257-ui",true),("0.2.285-ui",true),("0.2.285",false),("0.3.0-ui",false),("0.2.285-ui-extra",false),("0.2.-1-ui",false)] as [(String?,Bool)] {
            try check(MacTab5OTAEncoding.supportsRanges(version) == supported)
        }
        for (version,supported) in [(nil,false),("0.2.287-ui",false),("0.2.288-ui",true),("0.2.288",false),("0.3.288-ui",false)] as [(String?,Bool)] {
            try check(MacTab5OTAEncoding.supportsRanges(version,minimumPatch:288) == supported)
        }
        return checked
    }
    private static func image() throws -> MacTab5FirmwareImage {
        let size = 6*1024*1024, end = (32+size)|15
        var bytes = Data(repeating:65,count:end+1)
        bytes.replaceSubrange(0..<256,with:Data(repeating:0,count:256))
        bytes[0]=0xe9;bytes[1]=1;bytes[12]=18;bytes[23]=1
        func word(_ at:Int,_ value:UInt32) { for i in 0..<4 { bytes[at+i]=UInt8(truncatingIfNeeded:value>>(8*i)) } }
        word(28,UInt32(size));word(32,0xabcd5432)
        for (i,b) in "0.2.284-ui".utf8.enumerated() { bytes[48+i]=b }
        for (i,b) in "aibot_tab5".utf8.enumerated() { bytes[80+i]=b }
        // Include incompressible blocks so the streamed fixture spans multiple
        // awaited 16 KiB sends as well as compressed block boundaries.
        var random: UInt32 = 17
        for i in 256..<(256+192*1024) {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5
            bytes[i] = UInt8(truncatingIfNeeded: random)
        }
        bytes[end]=bytes[32..<32+size].reduce(UInt8(0xef),^)
        bytes.append(Data(SHA256.hash(data:bytes)))
        return try MacTab5FirmwareImage(bytes:bytes)
    }
    static func loopback() throws -> Int {
        var checked = try run() + MacHTTPTransportValidation.run()
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("ota-test-"+UUID().uuidString)
        defer { try? FileManager.default.removeItem(at:root) }
        let defaults = UserDefaults(suiteName:"ota-test-"+UUID().uuidString)!
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        let codex = MacTab5Codex(store:.init(root:root,key:{Data(repeating:1,count:32)}),enabled:{false},call:{_,_ in [:]})
        let voice = MacTab5Voice(enabled:{false},backend:.init(start:{_,_ in},feed:{_ in},finish:{},cancel:{}))
        let service = MacTab5Service(port:0,host:{"127.0.0.1"},gallery:.init(directory:root),computer:.init(defaults:defaults,backend:.init(read:{[:]},perform:{_,_,_ in false})),codex:codex,voice:voice,birthdays:.init(directory:root),agenda:.init(directory:root),load:{pair},save:{_ in})
        let image = try image();try service.firmwareControl.select(image)
        let server = HTTPStatusServer(port:0,token:{nil},tab5:service,loopbackOnly:true,snapshot:{Data()})
        try server.start();defer { server.stop() }
        let deadline = ProcessInfo.processInfo.systemUptime+4
        while server.boundPort == nil, ProcessInfo.processInfo.systemUptime < deadline { Thread.sleep(forTimeInterval:0.01) }
        guard let port = server.boundPort else { throw MacTab5Error.timeout }
        let config = URLSessionConfiguration.ephemeral;config.connectionProxyDictionary=[:];config.httpCookieStorage=nil;config.httpShouldSetCookies=false
        let session = URLSession(configuration:config);defer { session.invalidateAndCancel() }
        for (compressed,authorized) in [(false,true),(true,true),(true,false)] {
            let nonce = MacTab5Protocol.identifier(), done = DispatchSemaphore(value:0)
            var request = URLRequest(url:URL(string:"http://127.0.0.1:\(port)/tab5/v1/ota/\(image.sha256)")!)
            request.setValue(pair.deviceId,forHTTPHeaderField:"X-AIBot-Device");request.setValue(nonce,forHTTPHeaderField:"X-AIBot-Nonce")
            request.setValue(authorized ? MacTab5Protocol.proof(key:pair.key,message:"GET|/tab5/v1/ota/\(image.sha256)|\(pair.deviceId)|\(nonce)"):"invalid",forHTTPHeaderField:"X-AIBot-Proof")
            request.setValue("tcp-v2",forHTTPHeaderField:"X-AIBot-OTA-Flow")
            if compressed { request.setValue(MacTab5OTAEncoding.name,forHTTPHeaderField:"X-AIBot-OTA-Encoding") }
            var body = Data(), reply:HTTPURLResponse?, failed = false
            let started = ProcessInfo.processInfo.systemUptime
            session.dataTask(with:request) { data,response,error in body=data ?? Data();reply=response as? HTTPURLResponse;failed=error != nil;done.signal() }.resume()
            guard done.wait(timeout:.now()+30) == .success, !failed, reply?.statusCode == (authorized ? 200:401) else { throw MacTab5Error.timeout }
            if authorized {
                if compressed { try decode(body,expected:image.bytes) }
                else { guard body == image.bytes else { throw MacTab5Error.invalidPacket } }
                guard reply?.value(forHTTPHeaderField:"X-AIBot-OTA-Encoding") == (compressed ? MacTab5OTAEncoding.name:nil) else { throw MacTab5Error.invalidPacket }
            } else { guard body.isEmpty,reply?.value(forHTTPHeaderField:"X-AIBot-OTA-Encoding") == nil else { throw MacTab5Error.invalidPacket } }
            print("OTA_LOOPBACK compressed=\(compressed) authorized=\(authorized) bytes=\(body.count) ms=\(Int((ProcessInfo.processInfo.systemUptime-started)*1000))")
            checked += 1
        }
        func fetch(_ request:URLRequest) throws -> (Int,Data) {
            let done = DispatchSemaphore(value:0)
            var result:(Int,Data)?
            session.dataTask(with:request) { data,response,error in
                if error == nil, let response = response as? HTTPURLResponse { result = (response.statusCode,data ?? Data()) }
                done.signal()
            }.resume()
            guard done.wait(timeout:.now()+10) == .success, let result else { throw MacTab5Error.timeout }
            return result
        }
        func initial(_ sha:String,authorized:Bool = true) throws -> Int {
            let nonce = MacTab5Protocol.identifier()
            var request = URLRequest(url:URL(string:"http://127.0.0.1:\(port)/tab5/v1/ota/\(sha)")!)
            request.setValue(pair.deviceId,forHTTPHeaderField:"X-AIBot-Device");request.setValue(nonce,forHTTPHeaderField:"X-AIBot-Nonce")
            request.setValue(authorized ? MacTab5Protocol.proof(key:pair.key,message:"GET|/tab5/v1/ota/\(sha)|\(pair.deviceId)|\(nonce)"):"invalid",forHTTPHeaderField:"X-AIBot-Proof")
            request.setValue("tcp-v2",forHTTPHeaderField:"X-AIBot-OTA-Flow");request.setValue(MacTab5OTAEncoding.name,forHTTPHeaderField:"X-AIBot-OTA-Encoding")
            let result = try fetch(request)
            if result.0 == 200 { try decode(result.1, expected: image.bytes) }
            if result.0 != 200, !result.1.isEmpty { throw MacTab5Error.invalidPacket }
            return result.0
        }
        // A foreign observation cannot opt this device into range-only delivery.
        service.observeUSBInfo(["deviceId":"ffffffffffff","firmware":"0.2.285-ui"])
        guard try initial(image.sha256) == 200 else { throw MacTab5Error.invalidPacket }; checked += 1
        service.observeUSBInfo(["deviceId":pair.deviceId,"firmware":"0.2.256-ui"])
        guard try initial(image.sha256) == 200 else { throw MacTab5Error.invalidPacket }; checked += 1
        service.observeUSBInfo(["deviceId":pair.deviceId,"firmware":"0.2.285-ui"])
        guard try initial(image.sha256) == 503,
              try initial(image.sha256,authorized:false) == 401,
              try initial(String(repeating:"0",count:64)) == 404 else { throw MacTab5Error.invalidPacket }; checked += 3
        // Exercise the complete existing encrypted HTTP RPC path, independent
        // offsets and decompression, rather than treating the initial 503 as success.
        func range(_ offset:Int,_ count:Int,offer:String? = nil) throws -> (Int,Data) {
            let nonce = MacTab5Protocol.identifier()
            let root:[String:Any] = ["kind":"ota","offerId":offer ?? image.offerID,"sha256":image.sha256,"offset":offset,"count":count,"binaryReply":true,"bulkReply":true,"acceptEncoding":"zlib","session":service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]
            let packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root),key:pair.key,challenge:nonce)
            var request = URLRequest(url:URL(string:"http://127.0.0.1:\(port)/tab5/v1/rpc")!); request.httpMethod = "POST";request.httpBody = packet
            request.setValue(pair.deviceId,forHTTPHeaderField:"X-AIBot-Device");request.setValue(nonce,forHTTPHeaderField:"X-AIBot-Nonce")
            request.setValue(MacTab5Protocol.proof(key:pair.key,message:"POST|/tab5/v1/rpc|\(pair.deviceId)|\(nonce)|\(MacTab5Protocol.sha256(packet))"),forHTTPHeaderField:"X-AIBot-Proof")
            let response = try fetch(request);guard response.0 == 200 else { throw MacTab5Error.invalidPacket }
            let (metadata,bytes) = try MacTab5Binary.open(MacTab5Protocol.decrypt(response.1,key:pair.key,challenge:nonce,maximum:MacTab5Binary.maximumMailbox),response:true)
            let status = Int(MacTab5Protocol.integer(metadata["status"]) ?? 0)
            guard status == 200 else { return (status,bytes) }
            guard let body = metadata["body"] as? [String:Any], MacTab5Protocol.integer(body["offset"]) == Int64(offset) else { throw MacTab5Error.invalidPacket }
            if body["encoding"] as? String == "zlib" {
                guard MacTab5Protocol.integer(body["decodedSize"]) == Int64(count) else { throw MacTab5Error.invalidPacket }
                var plain = [UInt8](repeating:0,count:count), size = uLongf(count)
                let result = bytes.withUnsafeBytes { source in plain.withUnsafeMutableBufferPointer { uncompress($0.baseAddress,&size,source.bindMemory(to:UInt8.self).baseAddress,uLong(bytes.count)) } }
                guard result == Z_OK, size == count else { throw MacTab5Error.invalidPacket }; return (status,Data(plain))
            }
            return (status,bytes)
        }
        let began = ProcessInfo.processInfo.systemUptime
        var restored = Data()
        while restored.count < image.bytes.count {
            let count = min(49152,image.bytes.count-restored.count), result = try range(restored.count,count)
            guard result.0 == 200, result.1.count == count else { throw MacTab5Error.invalidPacket };restored.append(result.1)
        }
        guard restored == image.bytes else { throw MacTab5Error.invalidPacket };checked += 1
        guard try range(image.bytes.count-1,2).0 == 400,
              try range(0,49153).0 == 400,
              try range(0,1024,offer:String(repeating:"0",count:32)).0 == 400 else { throw MacTab5Error.invalidPacket };checked += 3
        print("OTA_RANGES_LOOPBACK bytes=\(restored.count) ms=\(Int((ProcessInfo.processInfo.systemUptime-began)*1000))")
        service.observeUSBInfo(["deviceId":"ffffffffffff","firmware":"0.2.288-ui"])
        guard !service.pipelinedOTA,try initial(image.sha256)==503 else { throw MacTab5Error.invalidPacket };checked += 1
        service.observeUSBInfo(["deviceId":pair.deviceId,"firmware":"0.2.288-ui"])
        guard service.pipelinedOTA,try initial(image.sha256)==200,
              try initial(image.sha256,authorized:false)==401 else { throw MacTab5Error.invalidPacket };checked += 2
        return checked
    }
}
