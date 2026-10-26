import Foundation
import AppKit

enum MacTab5ScreenPreviewValidation {
    private struct Checks {
        var count = 0
        mutating func check(_ name:String,_ value:Bool) throws {
            guard value else { throw MacTab5LocalError.invalid("Screen preview check failed: " + name) }
            count += 1
        }
    }
    static func constant(_ count:Int) -> Data {
        var out = Data(), remaining = count
        while remaining > 0 {
            let n = min(remaining,32767), control = n|32768
            out.append(UInt8(control&255)); out.append(UInt8(control>>8)); out.append(0xe0); out.append(7); remaining -= n
        }
        return out
    }
    @inline(never) private static func codec() throws -> Int {
        var c = Checks()
        let wire = Data([2,0,0,0xf8,0xe0,7,4,0x80,0x1f,0])
        let pixels = Data([0,0xf8,0xe0,7,0x1f,0,0x1f,0,0x1f,0,0x1f,0])
        try c.check("C encoder wire vector",MacTab5ScreenPreview.decode(wire,width:3,height:2) == pixels)
        for invalid in [Data([0,0]),Data([7,0x80,0,0]),Data([1,0,0]),wire+Data([1]),Data([5,0x80,0,0])] {
            try c.check("reject malformed, over/under-run or trailing words",MacTab5ScreenPreview.decode(invalid,width:3,height:2) == nil)
        }
        try c.check("dimension bounds",MacTab5ScreenPreview.decode(wire,width:641,height:1) == nil && MacTab5ScreenPreview.decode(wire,width:0,height:1) == nil)
        let image = MacTab5ScreenPreviewWindow.image(.init(width:3,height:2,pixels:pixels,received:0))
        let bitmap = image?.representations.first as? NSBitmapImageRep
        try c.check("actual native RGB565 red",(bitmap?.colorAt(x:0,y:0)?.redComponent ?? 0) > 0.95)
        try c.check("actual native RGB565 green",(bitmap?.colorAt(x:1,y:0)?.greenComponent ?? 0) > 0.95)
        try c.check("actual native RGB565 blue",(bitmap?.colorAt(x:2,y:0)?.blueComponent ?? 0) > 0.95)
        return c.count
    }
    @inline(never) private static func reception() throws -> Int {
        var c = Checks(), now:TimeInterval = 10
        let model = MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
        let context = MacTab5ScreenPreview.Context(device:"fixture",host:"host-a",transports:["USB","Wi-Fi"])
        let token = model.start(context); model.renew(token,context:context)
        let command = model.command!, id = command["requestId"] as! String
        try c.check("prefer authenticated USB",command["transport"] as? String == "USB")
        var root:[String:Any] = ["streamId":token,"requestId":id,"op":"begin"]
        try c.check("active viewer begin",model.receive(root,bytes:Data(),context:context,device:"fixture",transport:"USB").status == 200)
        let encoded = constant(640*360), split = encoded.count/2
        root.merge(["op":"frame","width":640,"height":360,"size":encoded.count,"encoding":"rgb565-rle1","sha256":MacTab5Protocol.sha256(encoded),"offset":0]) { _,b in b }
        func receive(_ value:[String:Any],_ bytes:Data) -> Int { model.receive(value,bytes:bytes,context:context,device:"fixture",transport:"USB").status }
        try c.check("partial frame accepted",receive(root,encoded.prefix(split)) == 200)
        try c.check("partial frame never shown",model.snapshot(token).0 == nil)
        try c.check("identical prefix retry",receive(root,encoded.prefix(split)) == 200)
        var bad = root; bad["offset"] = split+1
        try c.check("gap rejected",receive(bad,encoded.suffix(split-1)) == 409)
        var changed = Data(encoded.prefix(split)); changed[0] ^= 1
        try c.check("changed prefix rejected",receive(root,changed) == 409)
        bad = root; bad["width"] = true
        try c.check("boolean dimension rejected",receive(bad,encoded.prefix(split)) == 400)
        bad = root; bad["size"] = true
        try c.check("boolean length rejected",receive(bad,encoded.prefix(split)) == 400)
        try c.check("wrong device rejected",model.receive(root,bytes:encoded.prefix(split),context:context,device:"foreign",transport:"USB").status == 409)
        try c.check("wrong channel rejected",model.receive(root,bytes:encoded.prefix(split),context:context,device:"fixture",transport:"Wi-Fi").status == 409)
        var foreign = context; foreign = .init(device:context.device,host:"host-b",transports:context.transports)
        try c.check("wrong host rejected",model.receive(root,bytes:encoded.prefix(split),context:foreign,device:"fixture",transport:"USB").status == 409)
        root["offset"] = split
        try c.check("complete frame accepted",receive(root,encoded.suffix(encoded.count-split)) == 200)
        let frame = model.snapshot(token).0
        try c.check("only complete verified pixels published",frame?.pixels.count == 640*360*2 && frame?.pixels.prefix(2) == Data([0xe0,7]))
        try c.check("completed request is not dispatched again",receive(root,encoded.suffix(encoded.count-split)) == 409)
        now += 1; model.renew(token,context:context)
        root["requestId"] = model.command!["requestId"]; root["offset"] = 0; root["sha256"] = String(repeating:"0",count:64)
        try c.check("checksum failure keeps last good image",receive(root,encoded) == 400 && model.snapshot(token).0?.received == frame?.received)
        now += 6
        try c.check("viewer lease suppresses commands and pixels",model.command == nil && model.snapshot(token).0 == nil)
        model.renew(token,context:context)
        try c.check("resume uses fresh request",model.command?["requestId"] as? String != id)
        try c.check("old packet after lease rejected",receive(root,encoded.suffix(encoded.count-split)) == 409)
        model.stop(token)
        try c.check("close erases in-memory frame and command",model.command == nil && model.snapshot(token).0 == nil)
        try c.check("late frame after close rejected",receive(root,encoded.suffix(encoded.count-split)) == 409)
        return c.count
    }
    @inline(never) private static func lifecycle() throws -> Int {
        var c = Checks(), now:TimeInterval = 0
        let model = MacTab5ScreenPreview(clock:{now},epoch:{10_000})
        let usb = MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:["USB"])
        let first = model.start(usb); model.renew(first,context:usb)
        let old = model.command?["requestId"] as? String
        let wifi = MacTab5ScreenPreview.Context(device:usb.device,host:usb.host,transports:["Wi-Fi"])
        model.renew(first,context:wifi); now += 1.1; model.renew(first,context:wifi)
        try c.check("channel switch receives new bound request",model.command?["transport"] as? String == "Wi-Fi" && model.command?["requestId"] as? String != old)
        let second = model.start(wifi); model.renew(second,context:wifi); model.stop(first)
        try c.check("old window close cannot stop replacement viewer",model.command != nil)
        let none = MacTab5ScreenPreview.Context(device:usb.device,host:usb.host,transports:[])
        model.renew(second,context:none)
        try c.check("offline cancels unfinished frame",model.command == nil)
        model.renew(second,context:.init(device:usb.device,host:"replacement",transports:["USB"]))
        try c.check("ownership change clears capture",model.command == nil && model.snapshot(second).0 == nil)
        return c.count
    }
    private static func progressTimeout() throws -> Int {
        var c = Checks(), now = 10.0
        let model = MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
        let context = MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:["USB"])
        let token = model.start(context);model.renew(token,context:context)
        let first = model.command!["requestId"] as! String
        for _ in 0..<5 { now += 1; model.renew(token,context:context) }
        try c.check("five-second no-progress still bounded",model.command?["requestId"] as? String == first)
        now += 1;model.renew(token,context:context)
        try c.check("stalled transfer does not wait full thirty seconds",model.command == nil)
        now += 1;model.renew(token,context:context)
        let next = model.command!["requestId"] as! String
        try c.check("fresh request replaces lost worker",next != first)
        let late = model.receive(["streamId":token,"requestId":first,"op":"begin"],bytes:Data(),context:context,device:"fixture",transport:"USB")
        try c.check("old worker cannot resume new frame",late.status == 409)
        return c.count
    }
    private static func memoryFallback() throws -> Int {
        var c=Checks(),now=10.0
        let model=MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
        let context=MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:["USB"],highDefinition:true)
        let token=model.start(context);model.renew(token,context:context)
        var command=model.command!
        try c.check("new firmware first requests HD",command["encoding"] as? String == "jpeg")
        func fail(_ id:String)->Int {model.receive(["op":"error","error":"memory","streamId":token,"requestId":id],bytes:Data(),context:context,device:"fixture",transport:"USB").status}
        try c.check("HD memory rejection accepted",fail(command["requestId"] as! String)==200)
        now+=1.1;model.renew(token,context:context);command=model.command!
        try c.check("memory pressure falls back on existing270 wire",command["version"] as? Int == 2 && command["encoding"] as? String == "rgb565-rle1" && command["width"] as? Int == 640)
        try c.check("fallback quality explicitly shown",model.snapshot(token).1.contains("标准画质"))
        try c.check("second memory rejection accepted",fail(command["requestId"] as! String)==200)
        now+=1.1;model.renew(token,context:context);command=model.command!
        try c.check("bounded minimum preview instead of repeated HD allocation",command["width"] as? Int == 320 && command["height"] as? Int == 180)
        model.stop(token)
        try c.check("closed fallback cannot publish",model.command == nil)
        return c.count
    }
    private static func preparationAndRecovery() throws -> Int {
        var c=Checks(),now=10.0
        let model=MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
        let context=MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:["USB"],highDefinition:true)
        let token=model.start(context);model.renew(token,context:context)
        let first=model.command!["requestId"] as! String
        for _ in 0..<10 {now+=1;model.renew(token,context:context)}
        try c.check("HD preparation is not cancelled at five seconds",model.command?["requestId"] as? String == first)
        for _ in 0..<10 {
            let begin:[String:Any] = ["op":"begin","streamId":token,"requestId":first,"width":1280,"height":720,"encoding":"jpeg"]
            try c.check("preparation heartbeat preserves bound request",model.receive(begin,bytes:Data(),context:context,device:"fixture",transport:"USB").status == 200)
            now+=1;model.renew(token,context:context)
        }
        for _ in 0..<11 {now+=1;model.renew(token,context:context)}
        try c.check("preparation cannot extend absolute thirty-second deadline",model.command?["requestId"] as? String != first)
        model.stop(token)
        let next=model.start(context);model.renew(next,context:context)
        let id=model.command!["requestId"] as! String
        try c.check("memory pressure accepted",model.receive(["op":"error","error":"memory","streamId":next,"requestId":id],bytes:Data(),context:context,device:"fixture",transport:"USB").status == 200)
        let encoded=constant(640*360),sha=MacTab5Protocol.sha256(encoded)
        var restored=false,completed=0
        for _ in 0..<36 {
            now+=1;model.renew(next,context:context)
            guard let command=model.command else {continue}
            if command["encoding"] as? String == "jpeg" {restored=true;break}
            let root:[String:Any] = ["op":"frame","streamId":next,"requestId":command["requestId"]!,"width":640,"height":360,"encoding":"rgb565-rle1","size":encoded.count,"offset":0,"sha256":sha]
            try c.check("fallback frames remain usable during cooldown",model.receive(root,bytes:encoded,context:context,device:"fixture",transport:"USB").status == 200)
            completed+=1
            if completed<3 {try c.check("no immediate HD allocation loop",model.snapshot(next).0?.width == 640)}
        }
        try c.check("HD retried after cooldown and successful fallback",restored&&completed>=3)
        try c.check("last fallback image retained during HD recovery",model.snapshot(next).0?.width == 640)
        model.stop(next)
        return c.count
    }
    private static func observations() throws -> Int {
        var c = Checks()
        let model = MacTab5ScreenPreview(clock:{10},epoch:{1_000_000})
        let context = MacTab5ScreenPreview.Context(device:"private-device",host:"private-host",transports:["USB"])
        let token = model.start(context); model.renew(token,context:context)
        let original = model.command!, command = model.command(frameEpochMilliseconds:999_000)!
        try c.check("snapshot expiry narrows original deadline",
            MacTab5Protocol.integer(command["expiresAt"]) == 1_029_000 && MacTab5Protocol.integer(original["expiresAt"]) == 1_030_000)
        try c.check("snapshot-relative lifetime observation",model.diagnostic.contains("30000 ms") && model.diagnostic.contains("超过30秒上界 0 次"))
        let id = command["requestId"] as! String
        let begin = model.receive(["streamId":token,"requestId":id,"op":"begin"],bytes:Data(),context:context,device:context.device,transport:"USB")
        try c.check("begin is separately observed",begin.status == 200 && model.diagnostic.contains("开始 1"))
        _ = model.receive(["streamId":token,"requestId":id,"op":"private-operation"],bytes:Data(),context:context,device:context.device,transport:"USB")
        try c.check("unknown operation label is fixed",model.diagnostic.contains("最近 invalid/400") && model.diagnostic.contains("拒绝 1"))
        try c.check("observations exclude all caller identifiers",
            [token,id,"private-device","private-host","private-operation"].allSatisfy { !model.diagnostic.contains($0) })
        model.stop(token)
        try c.check("observation does not preserve closed image or command",model.command == nil && model.snapshot(token).0 == nil)
        return c.count
    }
    private static func firmwareGate() throws -> Int {
        var c = Checks()
        try c.check("unknown/malformed firmware cannot capture",!MacTab5ScreenPreview.allowsCapture(firmware:nil) &&
            !MacTab5ScreenPreview.allowsCapture(firmware:"0.2.999") && !MacTab5ScreenPreview.allowsCapture(firmware:"0..999-ui"))
        try c.check("older non-mirror firmware cannot capture",!MacTab5ScreenPreview.allowsCapture(firmware:"0.2.267-ui"))
        try c.check("known unsafe mirror firmware cannot capture",!MacTab5ScreenPreview.allowsCapture(firmware:"0.2.268-ui"))
        try c.check("synthetic newer fixed firmware can capture",MacTab5ScreenPreview.allowsCapture(firmware:"0.2.999-ui"))
        let model = MacTab5ScreenPreview(clock:{10},epoch:{1_000_000})
        var context = MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:["USB"])
        context.allowsCapture = false
        let token = model.start(context);model.renew(token,context:context)
        try c.check("quarantine publishes no command or pixels",model.command == nil && model.snapshot(token).0 == nil &&
            model.snapshot(token).1.contains("统一固件更新"))
        try c.check("quarantine rejects a late capture packet",model.receive(["op":"begin","streamId":token],bytes:Data(),
            context:context,device:context.device,transport:"USB").status == 409)
        return c.count
    }
    private static func timings() throws -> Int {
        var c = Checks(), now = 10.0
        let model = MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
        let context = MacTab5ScreenPreview.Context(device:"private-device",host:"private-host",transports:["USB"])
        let token = model.start(context); model.renew(token,context:context)
        let id = model.command!["requestId"] as! String
        func receive(_ op:String,_ extra:[String:Any] = [:],_ bytes:Data = Data()) -> Int {
            model.receive(["streamId":token,"requestId":id,"op":op].merging(extra){_,b in b},bytes:bytes,
                          context:context,device:context.device,transport:"USB").status
        }
        now = 10.25; try c.check("first begin accepted",receive("begin") == 200)
        now = 10.75; try c.check("repeated begin accepted",receive("begin") == 200)
        let encoded = constant(640*360), split = encoded.count/2
        var info:[String:Any] = ["width":640,"height":360,"encoding":"rgb565-rle1","size":encoded.count,"sha256":MacTab5Protocol.sha256(encoded),"offset":0]
        now = 11; try c.check("first timed fragment",receive("frame",info,encoded.prefix(split)) == 200)
        now = 11.25; try c.check("duplicate does not reset first byte",receive("frame",info,encoded.prefix(split)) == 200)
        info["offset"] = split; now = 11.5
        try c.check("timed completion",receive("frame",info,encoded.suffix(encoded.count-split)) == 200)
        try c.check("first begin and first byte boundaries retained",model.diagnostic.contains("排队 250，首片前 750，首片至完成 500，请求合计 1500"))
        now = 11.75; model.observedInWindow(token,received:11.5)
        try c.check("viewer timing recorded",model.diagnostic.contains("窗口呈现 250"))
        now = 12; model.observedInWindow(token,received:11.5)
        try c.check("repeated refresh cannot inflate viewer timing",model.diagnostic.contains("窗口呈现 250"))
        model.observedInWindow("foreign",received:11.5)
        try c.check("foreign viewer cannot record timing",model.diagnostic.contains("窗口呈现 250"))
        try c.check("timing excludes identifiers",![token,id,"private-device","private-host"].contains { model.diagnostic.contains($0) })
        model.stop(token); _ = model.start(context)
        try c.check("new viewer clears previous timings",!model.diagnostic.contains("预览计时"))
        return c.count
    }
    private static func responsiveCadence() throws -> Int {
        var c = Checks()
        try c.check("old receiver keeps conservative pacing",!MacTab5ScreenPreview.supportsResponsiveCapture(firmware:"0.2.276-ui"))
        try c.check("responsive worker version enables pacing",MacTab5ScreenPreview.supportsResponsiveCapture(firmware:"0.2.277-ui"))
        try c.check("unknown version cannot enable pacing",!MacTab5ScreenPreview.supportsResponsiveCapture(firmware:nil) && !MacTab5ScreenPreview.supportsResponsiveCapture(firmware:"0.2.277.1-ui"))
        for responsive in [false,true] { for transport in ["USB","Wi-Fi","BLE"] {
            var now = 100.0
            let model = MacTab5ScreenPreview(clock:{now},epoch:{1_000_000})
            let context = MacTab5ScreenPreview.Context(device:"fixture",host:"host",transports:[transport],highDefinition:true,responsiveCapture:responsive)
            let token = model.start(context); model.renew(token,context:context)
            if transport != "BLE" {
                let id = model.command!["requestId"]!
                try c.check("fallback setup",model.receive(["streamId":token,"requestId":id,"op":"error","error":"memory"],bytes:Data(),context:context,device:"fixture",transport:transport).status == 200)
                now += 1.01;model.renew(token,context:context)
            }
            let command = model.command!, w = command["width"] as! Int, h = command["height"] as! Int
            let bytes = constant(w*h), sha = MacTab5Protocol.sha256(bytes)
            let root:[String:Any] = ["streamId":token,"requestId":command["requestId"]!,"op":"frame","width":w,"height":h,"encoding":"rgb565-rle1","offset":0,"size":bytes.count,"sha256":sha]
            try c.check("responsive fixture completes",model.receive(root,bytes:bytes,context:context,device:"fixture",transport:transport).status == 200)
            now += 0.11;model.renew(token,context:context)
            let fast = responsive && transport != "BLE"
            try c.check("completed frame cadence respects worker and transport",(model.command != nil) == fast)
            if !fast { now += 1.01;model.renew(token,context:context) }
            let next = model.command!, nextID = next["requestId"] as! String
            now += 0.11;model.renew(token,context:context)
            try c.check("fast refresh never replaces an in-flight request",model.command?["requestId"] as? String == nextID)
            let unchanged:[String:Any] = ["streamId":token,"requestId":nextID,"op":"unchanged","width":w,"height":h,"encoding":"rgb565-rle1","sha256":sha]
            try c.check("unchanged pixels acknowledged",model.receive(unchanged,bytes:Data(),context:context,device:"fixture",transport:transport).status == 200)
            now += 0.11;model.renew(token,context:context)
            try c.check("unchanged cadence keeps legacy and BLE cooldown",(model.command != nil) == fast)
            model.stop(token)
            try c.check("responsive close still revokes commands",model.command == nil)
        } }
        return c.count
    }
    static func run() throws -> Int { try codec()+reception()+lifecycle()+observations()+firmwareGate()+progressTimeout()+memoryFallback()+preparationAndRecovery()+timings()+responsiveCadence() }

    static func render(_ output: URL) throws {
        _ = NSApplication.shared
        try FileManager.default.createDirectory(at:output,withIntermediateDirectories:true)
        var pixels = Data()
        let colors:[UInt16] = [0xf800,0x07e0,0x001f,0xffff,0xffe0,0x07ff,0xf81f,0]
        for y in 0..<360 { for x in 0..<640 {
            let value = y < 300 ? colors[x/80] : (x < 320 ? 0xffff:0)
            pixels.append(UInt8(value&255)); pixels.append(UInt8(value>>8))
        } }
        for (label,size) in [("default",NSSize(width:840,height:580)),("minimum",NSSize(width:640,height:420))] {
            for (theme,appearance) in [("light",NSAppearance.Name.aqua),("dark",.darkAqua)] {
                let window = MacBridgeSettingsStyle.window("设备预览",width:size.width,height:size.height)
                window.contentMinSize = NSSize(width:640,height:420)
                let content = MacTab5ScreenPreviewContent(name:"书桌 TAB5") {}
                content.image.image = MacTab5ScreenPreviewWindow.image(.init(width:640,height:360,pixels:pixels,received:0))
                content.status.stringValue = "实时镜像 · 640 × 360 · USB"
                window.contentView = content; window.appearance = NSAppearance(named:appearance)
                window.setContentSize(size); window.displayIfNeeded(); content.layoutSubtreeIfNeeded()
                guard content.image.frame.width > 500,content.image.frame.height > 220,
                      content.bounds.contains(content.image.frame),content.bounds.contains(content.status.frame),
                      !content.hasAmbiguousLayout,let bitmap = content.bitmapImageRepForCachingDisplay(in:content.bounds) else { throw MacTab5Error.invalidPacket }
                window.appearance!.performAsCurrentDrawingAppearance { content.cacheDisplay(in:content.bounds,to:bitmap) }
                let drawing = NSImage(size:content.bounds.size); drawing.addRepresentation(bitmap)
                let opaque = NSImage(size:content.bounds.size); opaque.lockFocus()
                window.appearance!.performAsCurrentDrawingAppearance {
                    NSColor.windowBackgroundColor.setFill(); content.bounds.fill(); drawing.draw(in:content.bounds)
                }
                opaque.unlockFocus()
                guard let tiff = opaque.tiffRepresentation,let composed = NSBitmapImageRep(data:tiff),
                      let bytes = composed.representation(using:.png,properties:[:]) else { throw MacTab5Error.unavailable }
                try bytes.write(to:output.appendingPathComponent("preview-\(label)-\(theme).png"))
                window.close()
            }
        }
        print("MAC_DEVICE_PREVIEW_UI_OK 4 native synthetic fixtures; minimum/default, light/dark; no device access")
    }
}
