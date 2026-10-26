import Foundation
import CoreBluetooth

enum MacTab5BLERPCWindowValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name:String,_ value:@autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("BLE RPC window: " + name) }; count += 1
        }
        func rejects(_ name:String,_ action:() throws -> Void) throws {
            var rejected = false; do { try action() } catch { rejected = true }; try check(name,rejected)
        }
        let key = Data(0..<32), nonce = String(repeating:"a",count:32)
        let clear = try MacTab5Binary.pack(["kind":"benchmark","offset":0,"count":49152],bytes:MacTab5Benchmark.pattern(offset:0,count:49152),bulk:true,response:false)
        let request = Data(nonce.utf8) + Data(MacTab5Protocol.proof(key:key,message:nonce).utf8) + (try MacTab5Protocol.encrypt(clear,key:key,challenge:nonce,maximum:MacTab5Protocol.maximumRPC))
        func part(_ offset:Int,_ size:Int,id:UInt32 = 7,total:Int? = nil) -> Data {
            var packet = Data(); MacTab5Mailbox.append32(id,to:&packet); MacTab5Mailbox.append16(offset,to:&packet)
            MacTab5Mailbox.append16(total ?? request.count,to:&packet); packet.append(request[offset..<min(request.count,offset+size)]); return packet
        }
        for capacity in [20,244,488] {
            var prefix = Data(request.prefix(capacity-8))
            while prefix.count < request.count {
                var window = try MacTab5BLERPCWindow(id:7,total:request.count,prefix:prefix,credits:32,now:10)
                try check("grant exact cursor and credit",window.grant.count == 8 && window.grant[0] == 3 && MacTab5Mailbox.u32(window.grant,1) == 7 && Int(MacTab5Mailbox.u16(window.grant,5)) == prefix.count && window.grant[7] == 32)
                try window.markGrantSubmitted(now:10.01)
                while !window.ready { try window.accept(part(window.bytes.count,capacity-8),now:10.1) }
                prefix = window.bytes
            }
            let decoded = try MacTab5Protocol.decrypt(Data(prefix.dropFirst(96)),key:key,challenge:nonce)
            try check("whole encrypted bulk request intact",prefix == request && decoded == clear)
        }
        for invalid in [0,33,true,"32",NSNull()] as [Any] {
            try check("bad capability falls back to bounded legacy offer",MacTab5BLERPCWindow.credits(["rpcNotifyWindow":invalid,"mailboxWindow":8]) == 8)
        }
        try check("missing capability unavailable",MacTab5BLERPCWindow.credits([:]) == 0)
        try check("offered RPC window independent of voice",MacTab5BLERPCWindow.credits(["rpcNotifyWindow":32,"mailboxWindow":8]) == 32 && MacTab5BLEVoiceWindow.credits(["rpcNotifyWindow":32,"mailboxWindow":8]) == 8)
        let prefix = Data(request.prefix(100))
        for packet in [part(101,40),part(0,40),part(100,40,id:8),part(100,40,total:request.count-1),Data(repeating:0,count:8),part(100,481)] {
            var window = try MacTab5BLERPCWindow(id:7,total:request.count,prefix:prefix,credits:32,now:10)
            try rejects("bad id/cursor/length rejects without advancing") { try window.accept(packet,now:10.1) }
            try check("bad fragment leaves prefix unchanged",window.bytes == prefix && !window.ready)
        }
        var short = try MacTab5BLERPCWindow(id:7,total:request.count,prefix:Data(request.dropLast(20)),credits:32,now:10)
        try short.accept(part(request.count-20,20),now:10.1)
        try check("notifications before grant ack cannot dispatch",!short.ready)
        try short.markGrantSubmitted(now:10.2)
        try check("partial final window completes exact request",short.ready && short.bytes == request)
        try rejects("extra packet cannot dispatch twice") { try short.accept(part(request.count-20,20),now:10.3) }
        try rejects("duplicate submission rejected") { try short.markGrantSubmitted(now:10.3) }
        for total in [123,65536] { try rejects("total bounds") { _ = try MacTab5BLERPCWindow(id:7,total:total,prefix:prefix,credits:32,now:10) } }
        for credits in [0,33] { try rejects("grant bounds") { _ = try MacTab5BLERPCWindow(id:7,total:request.count,prefix:prefix,credits:credits,now:10) } }
        var expired = try MacTab5BLERPCWindow(id:7,total:request.count,prefix:prefix,credits:32,now:10)
        try rejects("late packet rejected") { try expired.accept(part(100,20),now:14) }
        try rejects("clock rollback rejected") { try expired.markGrantSubmitted(now:9) }
        var exhausted = try MacTab5BLERPCWindow(id:7,total:request.count,prefix:prefix,credits:1,now:10)
        try exhausted.markGrantSubmitted(now:10); try exhausted.accept(part(100,20),now:10.1)
        try check("full credit window completes only its prefix",exhausted.ready && exhausted.bytes.count == 120)
        try rejects("ungranted extra fragment rejected") { try exhausted.accept(part(120,20),now:10.2) }
        let diagnostics = MacTab5BLEDiagnostics(clock:{ 10 })
        diagnostics.rpcLink(credits:100,available:true,subscribed:false,failure:.att,code:8)
        try check("RPC negotiation contains only bounded metadata",diagnostics.summary.contains("额度 32") && diagnostics.summary.contains("ATT/8"))
        diagnostics.rpcConfiguration("sensitive-invalid-value")
        try check("invalid descriptor values never logged",diagnostics.summary.contains("CCCD 未知") && !diagnostics.summary.contains("sensitive-invalid-value"))
        diagnostics.rpcLink(credits:32,available:true,subscribed:true,failure:.none)
        try check("successful RPC reconciliation clears stale readback",diagnostics.summary.contains("订阅 已确认") && !diagnostics.summary.contains("CCCD"))
        let authorizationError = NSError(domain:CBATTErrorDomain,code:CBATTError.insufficientAuthorization.rawValue)
        var recovery = MacTab5BLENotificationRecovery()
        try check("exact ATT authorization error permits readback",recovery.needsReadback(after:authorizationError))
        try check("exact enabled readback permits one system reconciliation",recovery.shouldReconcile(1))
        try check("subscription reconciliation cannot loop",!recovery.needsReadback(after:authorizationError) && !recovery.shouldReconcile(1))
        for value in [0,2,true,"1",NSNull()] as [Any] {
            var invalid = MacTab5BLENotificationRecovery(); _ = invalid.needsReadback(after:authorizationError)
            try check("invalid or disabled descriptor never reconciles",!invalid.shouldReconcile(value))
        }
        for error in [NSError(domain:CBATTErrorDomain,code:5),NSError(domain:"other",code:8)] {
            var invalid = MacTab5BLENotificationRecovery()
            try check("other subscription errors keep legacy fallback",!invalid.needsReadback(after:error) && !invalid.shouldReconcile(1))
        }
        return count
    }
}
