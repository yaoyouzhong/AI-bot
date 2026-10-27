import Foundation

enum MacTab5USBVoicePriorityValidation {
    static func run() throws -> Int {
        var checks = 0
        func check(_ name: String, _ ok: Bool) throws {
            guard ok else { throw MacTab5LocalError.invalid("USB voice priority: "+name) }; checks += 1
        }
        var order: [String] = [], busy = true, now = 0.0, served = -1.0
        func status() { order.append("status"); now += 3.8 }
        func rpc() { order.append("rpc"); served = now }
        func resources() -> Int { order.append("resource"); now += 3.8; return 1 }
        var chunks = MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{busy},claim:{},status:status,rpc:rpc,resources:resources)
        try check("live voice ahead of slow status",order == ["rpc","status"] && served == 0)
        try check("no first optional image chunk during voice",chunks == 0)
        try check("ownership/status still serviced",now == 3.8)

        order = []; busy = false; now = 0
        chunks = MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{busy},claim:{},status:status,rpc:{rpc();busy = true},resources:resources)
        try check("new start rechecked before images",order == ["status","rpc"] && chunks == 0)
        order = []; busy = true
        chunks = MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{busy},claim:{},status:status,rpc:{rpc();busy = false},resources:resources)
        try check("completed take resumes existing image path",order == ["rpc","status","resource"] && chunks == 1)
        order = []; busy = false
        chunks = MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{busy},claim:{},status:status,rpc:rpc,resources:resources)
        try check("idle ordering unchanged",order == ["status","rpc","resource"] && chunks == 1)
        order = []; busy = true
        _ = MacTab5USBVoicePriority.run(rpcSupported:false,voiceBusy:{busy},claim:{},status:status,rpc:rpc,resources:resources)
        try check("legacy peer never receives unsupported RPC",order == ["status"])

        enum Failure: Error { case transport }
        order = []
        do {
            _ = try MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{true},claim:{},status:status,rpc:{order.append("rpc");throw Failure.transport},resources:resources)
            try check("RPC failure propagated",false)
        } catch Failure.transport { checks += 1 }
        try check("failed voice request never replayed",order == ["rpc"])
        order = []
        do {
            _ = try MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{true},claim:{},status:{order.append("status");throw Failure.transport},rpc:rpc,resources:resources)
            try check("status failure propagated",false)
        } catch Failure.transport { checks += 1 }
        try check("status failure retains one RPC and no image",order == ["rpc","status"])
        order = []
        _ = MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{true},claim:{order.append("claim")},status:status,rpc:rpc,resources:resources)
        try check("cold USB ownership claim precedes prioritized RPC",order == ["claim","rpc","status"])
        order = []
        do {
            _ = try MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{true},claim:{order.append("claim");throw Failure.transport},status:status,rpc:rpc,resources:resources)
            try check("failed ownership claim propagated",false)
        } catch Failure.transport { checks += 1 }
        try check("unclaimed peer cannot receive prioritized RPC",order == ["claim"])
        return checks
    }
}
