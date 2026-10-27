import AppKit

enum MacPreviewLifecycleValidation {
    static func run() throws {
        _ = NSApplication.shared
        let root = FileManager.default.temporaryDirectory.appendingPathComponent("aibot-preview-lifecycle-"+UUID().uuidString)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:root) }
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        let codex = MacTab5Codex(store:.init(root:root,key:{pair.key}),enabled:{false},call:{_,_ in [:]})
        let computer = MacTab5Computer(defaults:UserDefaults(suiteName:"preview-lifecycle-"+UUID().uuidString)!,backend:.init(read:{[:]},perform:{_,_,_ in false}))
        let voice = MacTab5Voice(enabled:{false},backend:.init(start:{_,_ in},feed:{_ in},finish:{},cancel:{}))
        let service = MacTab5Service(port:8765,host:{nil},gallery:.init(directory:root),computer:computer,codex:codex,voice:voice,
            birthdays:.init(directory:root),agenda:.init(directory:root),load:{pair},save:{_ in})
        defer { service.stop() }
        let firmware = "0.2.269-ui", nonce = MacTab5Protocol.identifier()
        service.observeBLEInfo(["firmware":firmware,"firmwareProof":MacTab5Protocol.proof(key:pair.key,message:"FIRMWARE|\(nonce)|\(firmware)")],nonce:nonce)
        service.bleState("已连接",confirmed:true)
        let controller = MacTab5ScreenPreviewWindow(service:service,name:"Synthetic lifecycle",valid:{true})
        controller.window?.setFrameOrigin(NSPoint(x:-10000,y:-10000))
        defer { controller.close() }
        var previous = "", checks = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("Preview lifecycle: "+name) }; checks += 1
        }
        for _ in 0..<3 {
            // Each simulated connection keeps supplying its real eight-second
            // heartbeat; longer frame cadence must not turn this into a disconnect test.
            service.bleState("已连接",confirmed:true)
            controller.open()
            try check("actual reopen is visible",controller.window?.isVisible == true)
            guard let command = service.screenPreview.command, let token = command["streamId"] as? String, let request = command["requestId"] as? String else {
                throw MacTab5LocalError.invalid("Preview lifecycle: no command after actual open")
            }
            try check("reopen owns a new stream",token != previous); previous = token
            controller.open()
            try check("repeated menu click preserves stream",service.screenPreview.command?["streamId"] as? String == token)
            let data = MacTab5ScreenPreviewValidation.constant(320*180)
            let result = service.screenPreview.receive(["op":"frame","streamId":token,"requestId":request,"width":320,"height":180,
                "encoding":"rgb565-rle1","size":data.count,"offset":0,"sha256":MacTab5Protocol.sha256(data)],bytes:data,context:service.screenPreviewContext,device:pair.deviceId,transport:"BLE")
            try check("synthetic frame completes",result.status == 200 && result.body["complete"] as? Bool == true)
            // One-second request spacing may land just after the first timer tick.
            let nextDeadline = Date().addingTimeInterval(2.5)
            while service.screenPreview.command == nil && Date() < nextDeadline {
                RunLoop.main.run(until:Date().addingTimeInterval(0.05))
            }
            try check("timer publishes next request",service.screenPreview.command != nil)
            let image = controller.window?.contentView?.subviews.compactMap{$0 as? NSImageView}.first
            try check("timer displays completed frame",image?.image != nil)
            let next = service.screenPreview.command!["requestId"] as! String
            let repeated = service.screenPreview.receive(["op":"frame","streamId":token,"requestId":next,"width":320,"height":180,
                "encoding":"rgb565-rle1","size":data.count,"offset":0,"sha256":MacTab5Protocol.sha256(data)],bytes:data,context:service.screenPreviewContext,device:pair.deviceId,transport:"BLE")
            try check("identical replacement frame accepted",repeated.status == 200)
            let originalImage = image?.image
            RunLoop.main.run(until:Date().addingTimeInterval(1.1))
            try check("unchanged pixels reuse image object",image?.image === originalImage)
            controller.close()
            try check("actual close revokes lease",service.screenPreview.command == nil)
        }
        print("MAC_PREVIEW_LIFECYCLE_OK \(checks) checks; three actual window open/frame/timer/close cycles; synthetic service, no hardware/credentials/network")
    }
}
