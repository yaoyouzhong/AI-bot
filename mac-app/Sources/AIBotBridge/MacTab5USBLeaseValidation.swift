import Foundation

enum MacTab5USBLeaseValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ name:String,_ value:@autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("USB host lease: " + name) }; checked += 1
        }
        var cadence = MacTab5USBLeaseCadence()
        try check("new connection starts full",cadence.isDue(100))
        cadence.acknowledge(started:100)
        try check("bounded full refresh",!cadence.isDue(101.999) && cadence.isDue(102))
        try check("slow ACK cannot extend refresh deadline",cadence.isDue(104))
        try check("clock rollback forces full",cadence.isDue(99))
        cadence = .init()
        try check("reconnect discards old cadence",cadence.isDue(100))
        // Run the actual wire selector against the inspected receiver's lease
        // behavior: only a full status renews .268's 8-second USB ownership.
        func simulate(refresh:Bool,firmware:String = "0.2.268-ui",receiverDelta:Bool = false) throws -> (accepted:Int,full:Int,wireBytes:Int) {
            var delta = MacTab5BLEDelta(), schedule = MacTab5USBLeaseCadence()
            var touched = 0.0, fullCount = 0, wireBytes = 0
            let deltaLease = MacTab5USBStatus.deltaRenewsOwnership(["version":1,"type":"tab5_hello",
                "deviceId":"001122334455","usbDelta":1,"firmware":firmware],device:"001122334455")
            for index in 0..<240 {
                let now = Double(index)*0.5
                if now-touched >= 8 { return (index,fullCount,wireBytes) }
                let frame = try MacTab5Protocol.json(["version":1,"type":"tab5_status","deviceId":"001122334455",
                    "session":String(repeating:"a",count:32),"sequence":index+1,
                    "data":["codex":[:],"claude":[:],"padding":String(repeating:"x",count:24000),"position":index]])
                let fullDue = refresh && schedule.isDue(now)
                let wire = try MacTab5USBStatus.wire(frame,baseline:&delta,packed:false,delta:true,
                    ownershipRefresh:fullDue,deltaRenewsOwnership:deltaLease)
                wireBytes += wire.count
                let type = try MacTab5Protocol.object(wire)["type"] as? String
                if type == "tab5_status" { touched=now; fullCount += 1 }
                else if receiverDelta && type == "tab5_delta" { touched=now }
                delta.acknowledge(frame,sequence:Int64(index+1))
                if fullDue { schedule.acknowledge(started:now) }
            }
            return (240,fullCount,wireBytes)
        }
        let old = try simulate(refresh:false), compatible = try simulate(refresh:true)
        try check("previous delta-only path expires at eight seconds",old.accepted == 16 && old.full == 1)
        try check("two minutes idle needs no RPC to retain ownership",compatible.accepted == 240 && compatible.full == 60)
        let modern = try simulate(refresh:true,firmware:"0.2.271-ui",receiverDelta:true)
        try check("modern receiver stays owned with initial full plus deltas",modern.accepted == 240 && modern.full == 1)
        try check("modern lease avoids repeated catalog retransmission",modern.wireBytes < compatible.wireBytes/10)
        let unknown = try simulate(refresh:true,firmware:"0.2.269-unknown",receiverDelta:true)
        try check("unknown firmware keeps conservative two-second full path",unknown.accepted == 240 && unknown.full == 60)
        cadence = .init()
        _ = cadence.isDue(100) // A send attempt without a validated ACK.
        try check("failed publication cannot advance cadence",cadence.isDue(101))
        return checked
    }
}
