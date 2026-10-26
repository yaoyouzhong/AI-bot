import Foundation

enum MacLatestParityValidation {
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ condition: @autoclosure () throws -> Bool) throws {
            guard try condition() else { throw MacTab5LocalError.invalid("Latest Windows parity: " + name) }; count += 1
        }
        let midnight = ISO8601DateFormatter().date(from:"2026-10-21T16:00:00Z")!, account = String(repeating:"a",count:64)
        func row(_ seconds: Double, _ used: Double?, account id: String? = nil, plan: String = "pro", reset: Double = 604800) -> MacQuotaHistory.Observation {
            .init(at:midnight.addingTimeInterval(seconds),plan:plan,weekly:used,reset:midnight.addingTimeInterval(reset),account:id ?? account)
        }
        func day(_ values: [MacQuotaHistory.Observation]) -> MacQuotaTrend.Day { MacQuotaHistory.build(values,now:midnight.addingTimeInterval(3600)).days.last! }
        let carry = day([row(-3600,20),row(60,22),row(120,25)])
        try check("cross-midnight cumulative delta",carry.used == 5 && carry.carryOver == 2 && carry.partial && carry.gaps == 1)
        try check("missing weekly skips same-identity baseline",day([row(-3600,20),row(10,nil),row(60,24)]).used == 4)
        try check("baseline cannot cross account",day([row(-3600,20),row(60,24,account:String(repeating:"b",count:64))]).used == nil)
        try check("baseline cannot cross plan",day([row(-3600,20),row(60,24,plan:"free")]).used == nil)
        try check("unverified drop not consumption",day([row(-3600,80),row(60,2)]).used == nil)
        let reset = day([row(-3600,80,reset:-30),row(60,2,reset:700000)])
        try check("verified reset includes only new segment",reset.used == 2 && reset.carryOver == 2 && reset.resets == 1)
        try check("bounds visible",carry.samples == 3-1 && carry.coverage?.contains("20.00%") == true)
        let legacy = Data(#"{"date":"2026-10-22","label":"10-22","used":2,"partial":true}"#.utf8)
        try check("old trend cache compatible",JSONDecoder().decode(MacQuotaTrend.Day.self,from:legacy).carryOver == nil)
        try check("new wire field excludes local detail",String(data:try JSONEncoder().encode(carry),encoding:.utf8)!.contains("carryOver") && !String(data:try JSONEncoder().encode(carry),encoding:.utf8)!.contains("coverage"))
        try check("wide layout three columns",MacBridgeCardGrid.columnCount(count:9,width:900,preferred:200) == 3)
        try check("narrow layout two columns",MacBridgeCardGrid.columnCount(count:9,width:550,preferred:200) == 2)
        try check("small groups retain two columns",MacBridgeCardGrid.columnCount(count:4,width:900,preferred:200) == 2)

        var clock = 100.0, timers: [() -> Void] = [], completed = 0
        let diagnostics = MacTab5RuntimeDiagnostics(date:{ midnight.addingTimeInterval(clock) },tick:{ clock },schedule:{ _,action in timers.append(action) })
        var context = MacTab5RuntimeDiagnostics.Context(device:"001122334455",host:String(repeating:"a",count:32),firmware:"0.2.252-ui",transports:["Wi-Fi","BLE"])
        func snapshot() -> [String:Any] { ["deviceId":context.device,"hostId":context.host,"firmware":context.firmware,"diagnosticVersion":1,"sampleAgeMs":0,"uptimeMs":1200,"uiAgeMs":10,"flushCount":100,"flushErrors":0,"lcdUnderruns":0,"displayDiag":"ok,1","cameraDiag":"idle","photoDiag":"0,0","photoUploadDiag":"0,0"] }
        try check("minimum diagnostic firmware",MacTab5RuntimeDiagnostics.supports("0.2.250-ui") && !MacTab5RuntimeDiagnostics.supports("0.2.249-ui") && !MacTab5RuntimeDiagnostics.supports("0.2.x-ui"))
        try check("bounded snapshot accepted",MacTab5RuntimeDiagnostics.valid(snapshot(),device:context.device,host:context.host))
        for (key,value) in [("uiAgeMs",true as Any),("sampleAgeMs",8001),("flushCount",-1),("cameraDiag","bad\ntext"),("photoDiag",String(repeating:"a",count:1025)),("hostId","foreign")] {
            var invalid = snapshot(); invalid[key] = value
            try check("invalid snapshot \(key)",!MacTab5RuntimeDiagnostics.valid(invalid,device:context.device,host:context.host))
        }
        diagnostics.refresh(context:{ context }) { completed += 1 }
        let first = diagnostics.command!["requestId"] as! String
        diagnostics.refresh(context:{ context }) { completed += 1 }
        try check("refresh coalesces",diagnostics.command!["requestId"] as? String == first && timers.count == 1)
        var reply: [String:Any] = ["requestId":first,"snapshot":snapshot()]
        try check("wrong transport rejected",diagnostics.receive(reply,context:context,device:context.device,transport:"BLE").status == 409)
        clock += 8; timers.removeFirst()()
        let second = diagnostics.command!["requestId"] as! String
        try check("one alternative channel fresh challenge",second != first && diagnostics.command!["transport"] as? String == "BLE")
        try check("late first reply rejected",diagnostics.receive(reply,context:context,device:context.device,transport:"Wi-Fi").status == 409)
        reply["requestId"] = second
        try check("alternative reply accepted",diagnostics.receive(reply,context:context,device:context.device,transport:"BLE").status == 200 && completed == 2 && diagnostics.command == nil)
        try check("replay completion rejected",diagnostics.receive(reply,context:context,device:context.device,transport:"BLE").status == 409)
        clock += 11; try check("old sample labeled stale",diagnostics.summary(context:context).contains("已过期"))
        timers.removeFirst()()
        diagnostics.refresh(context:{ context }); clock += 8; timers.removeFirst()(); clock += 8; timers.removeFirst()()
        try check("retry bounded and cache preserved",diagnostics.command == nil && timers.isEmpty && diagnostics.summary(context:context).contains("1200") && diagnostics.summary(context:context).contains("超时"))
        diagnostics.refresh(context:{ context }); context = .init(device:"001122334455",host:"another",firmware:"0.2.252-ui",transports:["BLE"])
        clock += 8; timers.removeFirst()()
        try check("binding change cannot retry or show previous host",diagnostics.command == nil && !diagnostics.summary(context:context).contains("1200"))
        context = .init(device:"001122334455",host:String(repeating:"a",count:32),firmware:"0.2.252-ui",transports:["BLE"])
        diagnostics.refresh(context:{ context }); clock += 11
        reply["requestId"] = diagnostics.command?["requestId"] ?? "expired"
        try check("expired command hidden",diagnostics.command == nil && diagnostics.receive(reply,context:context,device:context.device,transport:"BLE").status == 409)
        diagnostics.cancel(); timers.removeFirst()()

        var voiceTelemetry = snapshot(); voiceTelemetry["type"] = "tab5_ack"
        voiceTelemetry["voiceRequestMs"] = 12001; voiceTelemetry["voiceStage"] = 1; voiceTelemetry["voiceSeq"] = 0
        diagnostics.recordUSB(voiceTelemetry,context:context)
        try check("existing USB voice duration exposed without payload",diagnostics.summary(context:context).contains("最近语音请求耗时 ms：12001") && diagnostics.summary(context:context).contains("语音请求阶段：1"))
        voiceTelemetry["voiceRequestMs"] = true; diagnostics.recordUSB(voiceTelemetry,context:context)
        try check("boolean voice duration stays unknown",diagnostics.summary(context:context).contains("最近语音请求耗时 ms：—"))

        voiceTelemetry["uptimeMs"] = 5000
        voiceTelemetry["displayDiag"] = "legacy,1;sp,1,0,5,11760,2"
        diagnostics.recordUSB(voiceTelemetry,context:context)
        try check("USB preview allocation evidence exposed",diagnostics.summary(context:context).contains("最近任务 5，阶段 2（编码输出）"))
        voiceTelemetry["uptimeMs"] = 5100
        voiceTelemetry["displayDiag"] = "legacy,1;sp,1,0,6,11760,0"
        diagnostics.recordUSB(voiceTelemetry,context:context)
        try check("success does not overwrite USB preview failure",diagnostics.summary(context:context).contains("最近任务 5，阶段 2"))
        voiceTelemetry["type"] = "tab5_resource_ack"
        voiceTelemetry["uptimeMs"] = 1
        diagnostics.recordUSB(voiceTelemetry,context:context)
        try check("resource ACK cannot reset preview evidence",diagnostics.summary(context:context).contains("最近任务 5，阶段 2"))
        voiceTelemetry["type"] = "tab5_ack"
        voiceTelemetry["deviceId"] = "foreign"
        diagnostics.recordUSB(voiceTelemetry,context:context)
        try check("foreign device cannot reset preview evidence",diagnostics.summary(context:context).contains("最近任务 5，阶段 2"))

        let defaults = UserDefaults(suiteName:"latest-parity-"+UUID().uuidString)!
        let pair = MacTab5Pairing(deviceId:context.device,usbPath:"/dev/cu.fixture",key:Data(repeating:1,count:32),confirmed:true)
        let codex = MacTab5Codex(store:.init(root:root,key:{ pair.key }),enabled:{ false },call:{ _,_ in [:] })
        let computer = MacTab5Computer(defaults:defaults,backend:.init(read:{ [:] },perform:{ _,_,_ in false }))
        let voice = MacTab5Voice(enabled:{ false },backend:.init(start:{ _,_ in },feed:{ _ in },finish:{},cancel:{}))
        let service = MacTab5Service(port:8765,host:{ nil },gallery:.init(directory:root),computer:computer,codex:codex,voice:voice,birthdays:.init(directory:root),agenda:.init(directory:root),load:{ pair },save:{ _ in })
        defer { service.stop() }
        let nonce = MacTab5Protocol.identifier(), firmware = "0.2.252-ui"
        service.observeBLEInfo(["firmware":firmware,"firmwareProof":MacTab5Protocol.proof(key:pair.key,message:"FIRMWARE|\(nonce)|\(firmware)")],nonce:nonce)
        service.bleState("已连接",confirmed:true); service.refreshDeviceDiagnostics()
        var payload = snapshot(); payload["hostId"] = MacTab5HostBinding.id(pair)
        let request = service.deviceDiagnostics.command!["requestId"] as! String
        var envelope: [String:Any] = ["kind":"runtime-diagnostics","requestId":request,"snapshot":payload,"session":service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]
        func rpc(_ root: [String:Any], validProof: Bool = true, raw: Data = Data()) throws -> Int {
            let clear = try raw.isEmpty ? MacTab5Protocol.json(root):MacTab5Binary.pack(root,bytes:raw,bulk:false,response:false)
            let nonce = MacTab5Protocol.identifier(), bytes = try MacTab5Protocol.encrypt(clear,key:pair.key,challenge:nonce)
            let proof = validProof ? MacTab5Protocol.proof(key:pair.key,message:"POST|/tab5/v1/rpc|\(pair.deviceId)|\(nonce)|\(MacTab5Protocol.sha256(bytes))") : String(repeating:"0",count:64)
            let result = service.rpc(device:pair.deviceId,nonce:nonce,proof:proof,packet:bytes,transport:"BLE")
            guard result.0 == 200 else { return result.0 }
            let response = try MacTab5Protocol.object(MacTab5Protocol.decrypt(result.1,key:pair.key,challenge:nonce))
            return Int(MacTab5Protocol.integer(response["status"]) ?? 500)
        }
        try check("authenticated dispatcher rejects forged proof",rpc(envelope,validProof:false) == 401)
        envelope["session"] = "wrong"; try check("authenticated dispatcher rejects stale session",rpc(envelope) == 400)
        envelope["session"] = service.session; try check("encrypted diagnostic roundtrip",rpc(envelope) == 200 && service.deviceDiagnosticSummary.contains("1200"))
        try check("encrypted duplicate rejected",rpc(envelope) == 409)
        var probe: [String:Any] = ["kind":"benchmark","offset":0,"count":32,"session":service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]
        try check("benchmark forged request rejected",rpc(probe,validProof:false) == 401)
        try check("benchmark encrypted roundtrip",rpc(probe) == 200)
        probe["data"] = MacTab5Benchmark.pattern(offset:0,count:32).base64EncodedString()
        try check("benchmark authenticated upload",rpc(probe) == 200)
        service.publish(.init(version:1,time:"12:00:00",epochUtc:1800000000,utcOffsetSeconds:28800,capturedAt:Date(timeIntervalSince1970:1800000000),codex:.init(state:"idle",ageSeconds:nil),claude:.init(state:"idle",ageSeconds:nil),musicPlaying:false,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil))
        service.assets.updatePets([nil,nil])
        func frameData() throws -> [String:Any] { try MacTab5Protocol.object(service.frame())["data"] as? [String:Any] ?? [:] }
        try check("normal status carries pending asset",frameData()["resource"] != nil)
        try service.beginBenchmark(gallery:false)
        try check("benchmark blocks configuration",service.configurationBusy)
        let measuring = try frameData()
        try check("benchmark keeps normal status heartbeat",measuring["pcInput"] != nil && measuring["calendar"] != nil)
        try check("benchmark status does not advance image resource",measuring["resource"] == nil)
        try check("benchmark separate image resource blocked",(try? service.resourceFrame()) == nil)
        try check("benchmark RPC remains available",rpc(probe) == 200)
        try check("benchmark blocks OTA",rpc(["kind":"ota","session":service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]) == 409)
        service.endBenchmark()
        try check("benchmark gate released",!service.configurationBusy)
        try check("normal status images resume after benchmark",frameData()["resource"] != nil)
        try service.beginBenchmark(gallery:true)
        let writing = try frameData()
        try check("gallery benchmark keeps heartbeat without images",writing["pcInput"] != nil && writing["resource"] == nil)
        try check("gallery benchmark separate images blocked",(try? service.resourceFrame()) == nil)
        service.endBenchmark()
        let oldPreview = service.screenPreview.start(service.screenPreviewContext)
        service.screenPreview.renew(oldPreview,context:service.screenPreviewContext)
        try check("dispatcher blocks old firmware mirror commands",service.screenPreview.command == nil)
        // A synthetic future version exercises the receiver without permitting
        // the unsafe installed .268 path. This announces no release version.
        let mirrorFirmware = "0.2.999-ui", mirrorNonce = MacTab5Protocol.identifier()
        service.observeBLEInfo(["firmware":mirrorFirmware,"firmwareProof":MacTab5Protocol.proof(key:pair.key,
            message:"FIRMWARE|\(mirrorNonce)|\(mirrorFirmware)")],nonce:mirrorNonce)
        let previewToken = service.screenPreview.start(service.screenPreviewContext)
        service.screenPreview.renew(previewToken,context:service.screenPreviewContext)
        let previewCommand = service.screenPreview.command!
        try check("preview selects actual available channel",previewCommand["transport"] as? String == "BLE")
        var preview:[String:Any] = ["kind":"screen-preview","op":"begin","streamId":previewToken,
            "requestId":previewCommand["requestId"]!,"encoding":previewCommand["encoding"]!,"width":previewCommand["width"]!,"height":previewCommand["height"]!,"session":service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]
        try check("preview forged proof rejected",rpc(preview,validProof:false) == 401)
        preview["session"] = "expired"; try check("preview wrong session rejected",rpc(preview) == 400)
        preview["session"] = service.session; preview["issuedAt"] = 0
        try check("preview expired envelope rejected",rpc(preview) == 400)
        preview["issuedAt"] = Int64(Date().timeIntervalSince1970*1000)
        try check("preview authenticated begin",rpc(preview) == 200)
        let screenBytes = MacTab5ScreenPreviewValidation.constant(320*180)
        preview.merge(["op":"frame","width":320,"height":180,"size":screenBytes.count,"encoding":"rgb565-rle1",
                       "sha256":MacTab5Protocol.sha256(screenBytes),"offset":0]) { _,new in new }
        try check("preview authenticated binary roundtrip",rpc(preview,raw:screenBytes) == 200 && service.screenPreview.snapshot(previewToken).0?.pixels.count == 320*180*2)
        service.screenPreview.stop(previewToken)
        try check("preview late binary after close rejected",rpc(preview,raw:screenBytes) == 409)
        service.observeBLEInfo(["firmware":firmware,"firmwareProof":MacTab5Protocol.proof(key:pair.key,
            message:"FIRMWARE|\(nonce)|\(firmware)")],nonce:nonce)
        service.rejectUSBHost()
        do { try service.requestUpgrade(); throw MacTab5LocalError.invalid("Upgrade must reject an unauthenticated USB host") }
        catch { try check("USB authentication gates upgrade",error.localizedDescription.contains("未通过设备认证")) }
        service.usbState("数据已确认",confirmed:true)
        try check("confirmed USB clears authentication failure",service.hostBindingSummary.contains("USB 已确认"))
        for active in ["BLE", "Wi-Fi", "Offline", "unknown", ""] {
            service.observeUSBInfo(["deviceId":pair.deviceId,"active":active])
            try check("USB control preserves selected " + active,service.usbActive && !service.usbOwnsActiveState)
        }
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":"USB"])
        try check("device-selected USB defers BLE",service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":"ffffffffffff","active":"BLE"])
        try check("wrong device cannot change USB ownership",service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":pair.deviceId])
        try check("missing active clears prior USB ownership",!service.usbOwnsActiveState && service.usbActive)
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":"USB"])
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":true])
        try check("invalid active clears prior USB ownership",!service.usbOwnsActiveState)
        service.observeUSBInfo(["deviceId":pair.deviceId,"active":"USB"])
        service.rejectUSBHost()
        try check("rejected USB host cannot own display",!service.usbOwnsActiveState)
        service.usbState("数据已确认",confirmed:true)
        try check("reauthenticated USB can own display",service.usbOwnsActiveState)
        service.usbState("断开")
        try check("USB disconnect releases display ownership",!service.usbActive && !service.usbOwnsActiveState)
        service.usbState("数据已确认",confirmed:true)
        try check("reconnect without active info cannot reuse ownership",!service.usbOwnsActiveState)
        let ownershipCases: [(Double,Double,String?,Bool)] = [
            (100,100,"USB",true),(107.999,100,"USB",true),(108,100,"USB",false),
            (100,101,"USB",false),(100,0,"USB",false),(100,-1,"USB",false),
            (100,-.infinity,"USB",false),(100,100,"BLE",false),(100,100,"Wi-Fi",false),
            (100,100,"Offline",false),(100,100,nil,false),(100,100,"usb",false)]
        for (index,value) in ownershipCases.enumerated() {
            try check("USB ownership freshness boundary \(index)",MacTab5USBDisplayOwnership.owns(now:value.0,acknowledgedAt:value.1,active:value.2) == value.3)
        }
        count += try MacTab5BLERPCWindowValidation.run()
        return count
    }
}
