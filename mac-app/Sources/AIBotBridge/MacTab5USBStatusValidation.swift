import Foundation

enum MacTab5USBStatusValidation {
    static func run() throws -> Int {
        var checked = 0
        func check(_ name:String,_ condition:@autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("USB packed status: " + name) }
            checked += 1
        }
        let device = "001122334455"
        let hello: [String:Any] = ["version":1,"type":"tab5_hello","deviceId":device,"usbPacked":1]
        try check("explicit identity-bound capability",MacTab5USBStatus.supportsPacked(hello,device:device))
        for (key,value) in [("usbPacked",true as Any),("usbPacked",0),("usbPacked","1"),("type","tab5_ack"),("version",2),("deviceId","aabbccddeeff")] {
            var invalid = hello; invalid[key] = value
            try check("invalid hello/capability rejected",!MacTab5USBStatus.supportsPacked(invalid,device:device))
        }
        try check("legacy hello remains raw",!MacTab5USBStatus.supportsPacked(["version":1,"type":"tab5_hello","deviceId":device],device:device))
        let full = try MacTab5Protocol.json(["version":1,"type":"tab5_status","sequence":7,
            "session":String(repeating:"a",count:32),"deviceId":device,
            "data":["fixture":String(repeating:"x",count:24000),"codex":[:],"claude":[:]]])
        let packed = try MacTab5BLEStatus.wire(full,supported:true)
        var writes: [Data] = [], reads = 0
        let ack = try MacTab5USBStatus.exchange(full:full,wire:packed,write:{ writes.append($0) },read:{ reads += 1; return ["sequence":7] })
        try check("packed representation shrinks unchanged full status",writes.count == 1 && writes[0].count < full.count/4 && reads == 1 && MacTab5Protocol.integer(ack["sequence"]) == 7)
        for reason in [0,1,2] {
            writes = []; reads = 0
            _ = try MacTab5USBStatus.exchange(full:full,wire:packed,write:{ writes.append($0) },read:{
                reads += 1; if reads == 1 { throw MacTab5USBRejected(reason:reason) }; return ["sequence":7]
            })
            try check("representation failure retries exact raw publication once",writes.count == 2 && writes[0] != full && writes[1] == full && reads == 2)
        }
        for reason in [3,4,5] {
            writes = []; reads = 0
            do {
                _ = try MacTab5USBStatus.exchange(full:full,wire:packed,write:{ writes.append($0) },read:{ reads += 1; throw MacTab5USBRejected(reason:reason) })
                throw MacTab5LocalError.invalid("Unexpected USB status replay")
            } catch let error as MacTab5USBRejected {
                try check("envelope/identity/sequence errors do not replay",error.reason == reason && writes.count == 1 && reads == 1)
            }
        }
        writes = []; reads = 0
        do {
            _ = try MacTab5USBStatus.exchange(full:full,wire:packed,write:{ writes.append($0) },read:{ reads += 1; throw MacTab5Error.timeout })
            throw MacTab5LocalError.invalid("Unexpected USB timeout replay")
        } catch MacTab5Error.timeout { try check("timeout remains reconnect path",writes.count == 1 && reads == 1) }
        writes = []; reads = 0
        do {
            _ = try MacTab5USBStatus.exchange(full:full,wire:packed,write:{ writes.append($0) },read:{ reads += 1; throw MacTab5USBRejected(reason:2) })
            throw MacTab5LocalError.invalid("Unexpected USB retry loop")
        } catch is MacTab5USBRejected { try check("failed raw retry stops",writes.count == 2 && writes[1] == full && reads == 2) }
        writes = []
        _ = try MacTab5USBStatus.exchange(full:full,wire:full,write:{ writes.append($0) },read:{ ["sequence":7] })
        try check("legacy representation unchanged",writes == [full])
        var deltaHello = hello; deltaHello["usbDelta"] = 1
        try check("explicit USB delta capability",MacTab5USBStatus.supportsPacked(deltaHello,device:device,field:"usbDelta"))
        try check("packing alone never implies delta",!MacTab5USBStatus.supportsPacked(hello,device:device,field:"usbDelta"))
        var ownershipHello = deltaHello; ownershipHello["firmware"] = "0.2.269-ui"
        try check("inspected269 status deltas renew ownership",MacTab5USBStatus.deltaRenewsOwnership(ownershipHello,device:device))
        ownershipHello["firmware"] = "0.2.271-ui"
        try check("installed271 uses the inspected delta lease",MacTab5USBStatus.deltaRenewsOwnership(ownershipHello,device:device))
        try check("lease capability remains device-bound",!MacTab5USBStatus.deltaRenewsOwnership(ownershipHello,device:"aabbccddeeff"))
        for version in ["0.2.268-ui", "0.3.0-ui", "0.2.269", "0.2.269-ui-extra", "0.2.+269-ui", "0.2.0269-ui", "0.2.269.1-ui", "0.2.99999999999999999999999-ui", ""] {
            ownershipHello["firmware"] = version
            try check("legacy or unknown firmware keeps full lease refresh",!MacTab5USBStatus.deltaRenewsOwnership(ownershipHello,device:device))
        }
        ownershipHello["firmware"] = "0.2.271-ui"; ownershipHello["usbDelta"] = false
        try check("firmware alone cannot imply delta support",!MacTab5USBStatus.deltaRenewsOwnership(ownershipHello,device:device))
        var nextRoot = try MacTab5Protocol.object(full); nextRoot["sequence"] = 8
        let next = try MacTab5Protocol.json(nextRoot)
        var channel = MacTab5BLEDelta()
        try check("unacknowledged frame cannot become baseline",try channel.wire(next,packed:false,supported:true) == next)
        channel.acknowledge(full,sequence:7)
        let delta = try MacTab5Protocol.object(channel.wire(next,packed:false,supported:true))
        try check("acknowledged baseline retained exactly",delta["type"] as? String == "tab5_delta" && MacTab5Protocol.integer(delta["baseSequence"]) == 7 && MacTab5Protocol.integer(delta["sequence"]) == 8)
        var otherChannel = MacTab5BLEDelta()
        try check("USB and BLE baselines are independent",try otherChannel.wire(next,packed:false,supported:true) == next)
        channel.reset()
        try check("reconnect resets to full",try channel.wire(next,packed:false,supported:true) == next)
        channel.acknowledge(full,sequence:99)
        try check("wrong ACK cannot advance baseline",try channel.wire(next,packed:false,supported:true) == next)
        return checked
    }
}
