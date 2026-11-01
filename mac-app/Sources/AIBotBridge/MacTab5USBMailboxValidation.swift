import Foundation

enum MacTab5USBMailboxValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ value: Bool) throws { guard value else { throw MacTab5Error.unconfirmed }; checks += 1 }
        func rejects(_ work: () throws -> Void) throws {
            do { try work() } catch { checks += 1; return }; throw MacTab5Error.unconfirmed
        }
        let full: [String:Any] = ["rpcUsbBinary":1,"rpcUsbChunk":16384,"rpcUsbBinaryChunk":65526,"usbRxBatch":4096,"rpcUsbWait":20]
        let caps = MacTab5USBMailbox(full)
        try check(caps.binary && caps.chunk == 65526 && caps.receiveBatch == 4096 && caps.readyWait == 20)
        try check(MacTab5USBMailbox().chunk == 2048 && !MacTab5USBMailbox().binary)
        for invalid in [true as Any,"1",0,2,NSNull()] {
            var hello = full; hello["rpcUsbBinary"] = invalid
            let legacy = MacTab5USBMailbox(hello)
            try check(!legacy.binary && legacy.chunk == 16384 && legacy.receiveBatch == 0 && legacy.readyWait == 0)
        }
        for key in ["rpcUsbBinaryChunk","usbRxBatch","rpcUsbWait"] {
            for invalid in [true as Any,"65526",-1,65527,20.5,NSNull()] {
                var hello = full; hello[key] = invalid; let value = MacTab5USBMailbox(hello)
                try check(key == "rpcUsbBinaryChunk" ? value.chunk == 16384 : key == "usbRxBatch" ? value.receiveBatch == 0 : value.readyWait == 0)
            }
        }
        let raw = Data((0..<65535).map { UInt8(truncatingIfNeeded:$0) })
        for chunk in [2048,16384,65526] {
            let fragments = try MacTab5Mailbox.response(raw,id:7,chunk:chunk)
            var restored = Data()
            for part in fragments {
                try check(part.count <= chunk+9 && part[0] == 2 && MacTab5Mailbox.u32(part,1) == 7 && Int(MacTab5Mailbox.u16(part,5)) == restored.count && MacTab5Mailbox.u16(part,7) == 65535)
                restored.append(part.dropFirst(9))
            }
            try check(restored == raw)
            let ota = try MacTab5Mailbox.response(Data(raw.prefix(49152+128)),id:7,chunk:chunk)
            try check(chunk == 65526 ? ota.count == 1 : ota.count > 1)
            var incoming = Data()
            for offset in stride(from:0,to:raw.count,by:chunk) {
                var part = Data(); MacTab5Mailbox.append32(7,to:&part); MacTab5Mailbox.append16(offset,to:&part); MacTab5Mailbox.append16(raw.count,to:&part)
                part.append(raw[offset..<min(raw.count,offset+chunk)])
                try MacTab5Mailbox.append(part,id:7,total:raw.count,to:&incoming,maximum:chunk)
            }
            try check(incoming == raw)
        }
        for chunk in [0,65527,Int.max] { try rejects { _ = try MacTab5Mailbox.response(raw,id:7,chunk:chunk) } }
        for count in [0,63,64,128,65535] {
            let bytes = Data(raw.prefix(count)), ended = MacTab5USBMailbox.terminate(bytes)
            try check(ended.prefix(count) == bytes && ended.count == count + (count > 0 && count % 64 == 0 ? 1:0))
        }
        func header(tag: UInt32 = 42, size: Int, status: UInt32 = 1) -> Data {
            var result = Data("AIB2".utf8); MacTab5Mailbox.append32(tag,to:&result); MacTab5Mailbox.append32(UInt32(size),to:&result); MacTab5Mailbox.append32(status,to:&result); return result
        }
        let body = Data([0,10,64,65,73,66,79,84,255])
        var sent: [[String:Any]] = [], writes: [Data] = [], reads = 0
        let received = try caps.exchange(device:"fixture",data:nil,tag:42,send:{ sent.append($0) },write:{ writes.append($0) },read:{ count in
            reads += 1; return reads == 1 ? header(size:body.count) : body
        })
        try check(received == body && writes.isEmpty && reads == 2 && sent.count == 1 && MacTab5Protocol.integer(sent[0]["waitMs"]) == 20)
        sent = []; writes = []
        _ = try caps.exchange(device:"fixture",data:Data(raw.prefix(64)),tag:42,send:{ sent.append($0) },write:{ writes.append($0) },read:{ $0 == 0 ? Data():header(size:0) })
        try check(writes.count == 1 && writes[0].count == 65 && MacTab5Protocol.integer(sent[0]["count"]) == 64 && MacTab5Protocol.integer(sent[0]["waitMs"]) == 0)
        let bad = [header(tag:43,size:8),header(size:8,status:0),header(size:65536),header(size:7),Data(repeating:0,count:16),Data(header(size:8).dropLast())]
        for bytes in bad {
            var submissions = 0
            try rejects { _ = try caps.exchange(device:"fixture",data:nil,tag:42,send:{ _ in submissions += 1 },write:{ _ in },read:{ _ in bytes }) }
            try check(submissions == 1) // Never fall back/replay after publishing.
        }
        reads = 0
        try rejects { _ = try caps.exchange(device:"fixture",data:nil,tag:42,send:{ _ in },write:{ _ in },read:{ _ in reads += 1; return reads == 1 ? header(size:8):Data(repeating:0,count:7) }) }
        try rejects { _ = try caps.exchange(device:"fixture",data:raw,tag:42,send:{ _ in },write:{ _ in throw MacTab5Error.timeout },read:{ _ in throw MacTab5Error.unconfirmed }) }
        try rejects { _ = try caps.exchange(device:"fixture",data:Data([1]),tag:42,send:{ _ in },write:{ _ in },read:{ _ in header(size:8) }) }
        var pacing = MacTab5USBRPCPollCadence()
        try check(pacing.milliseconds(at:1,active:true,voicePriority:false) == 100)
        pacing.completed(bytes:49152,at:1)
        try check(pacing.milliseconds(at:1.249,active:true,voicePriority:false) == 10)
        pacing.completed(bytes:10,at:1.2)
        try check(pacing.milliseconds(at:1.25,active:true,voicePriority:false) == 100)
        try check(pacing.milliseconds(at:0.9,active:true,voicePriority:false) == 100)
        try check(pacing.milliseconds(at:1.1,active:false,voicePriority:false) == 100)
        try check(pacing.milliseconds(at:1.1,active:true,voicePriority:true) == 100)
        try check(MacTab5USBLinkCheck.verified("cancelled;USB,up,1,262144,100,0;USB,down,1,262144,200,0;"))
        for result in ["running;USB,up,1,262144,100,0;USB,down,1,262144,200,0;", "cancelled;USB,up,1,262144,100,1;USB,down,1,262144,200,0;", "cancelled;BLE,up,1,262144,100,0;BLE,down,1,262144,200,0;"] { try check(!MacTab5USBLinkCheck.verified(result)) }
        return checks
    }
}
