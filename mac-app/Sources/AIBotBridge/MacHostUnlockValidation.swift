import Foundation
import MacKeychainSupport

enum MacHostUnlockValidation {
    static func run() throws -> Int {
        var count = 0
        func check(_ name:String,_ value:@autoclosure () throws -> Bool) throws { guard try value() else { throw MacTab5LocalError.invalid("Host unlock: "+name) }; count += 1 }
        let domain = "AI-bot.host-unlock-fixture."+UUID().uuidString, defaults = UserDefaults(suiteName:domain)!
        defer { defaults.removePersistentDomain(forName:domain) }
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        let other = MacTab5Pairing(deviceId:pair.deviceId,usbPath:pair.usbPath,key:Data(repeating:8,count:32),confirmed:true)
        var saved:Data?, time = 100.0, locked:Bool? = true, allowed = true, executionCount = 0
        var probeCount = 0, lockReadHook:(() -> Void)?
        var stableDesktopSeconds = 0.0, backendAdvance = 0.0
        var queued:[() -> Void] = [], backendResult = true, cancelled: (() -> Bool)?
        let feature = MacHostUnlock(defaults:defaults,read:{saved},write:{saved = $0},remove:{saved = nil},clock:{time},locked:{ lockReadHook?(); return locked },execute:{ bytes,stable,cancel,progress in
            stableDesktopSeconds = stable
            executionCount += 1; cancelled = cancel
            guard !cancel() else { return false }
            time += backendAdvance
            progress("untrusted-fixture-stage"); progress("sending"); return backendResult
        },probe:{ cancel,progress in guard !cancel() else { return }; probeCount += 1; progress("public-character-probe") },schedule:{queued.append($0)})
        try check("new feature disabled",!feature.configured(pair))
        do { try feature.setEnabled(true,pair:pair); throw MacTab5Error.invalidPacket } catch is MacTab5LocalError { count += 1 }
        try feature.saveVerified(Data("Fixture9!".utf8),pair:pair)
        try check("saving alone does not enable",saved != nil && !feature.configured(pair))
        try feature.setEnabled(true,pair:pair)
        try check("explicit enable and matching pairing required",feature.configured(pair) && !feature.configured(other))
        var reads = 0
        let reloaded = MacHostUnlock(defaults:defaults,read:{reads += 1; return saved},write:{_ in},remove:{},locked:{true},execute:{_,_,_,_ in false},schedule:{_ in})
        try check("startup has no credential before explicit load",!reloaded.configured(pair) && reads == 0)
        reloaded.load()
        try check("restart reloads only matching user and pairing",reloaded.configured(pair) && !reloaded.configured(other) && reads == 1)
        reloaded.stop()
        var corrupt = try JSONSerialization.jsonObject(with:saved!) as! [String:Any]; corrupt["version"] = 2
        let invalidBytes = try JSONSerialization.data(withJSONObject:corrupt)
        let invalid = MacHostUnlock(defaults:defaults,read:{invalidBytes},write:{_ in},remove:{},locked:{true},execute:{_,_,_,_ in false},schedule:{_ in})
        invalid.load(); try check("unsupported saved credential version fails closed",!invalid.configured(pair)); invalid.stop()
        func request(_ op:String = "begin",id:String = UUID().uuidString.lowercased(),offer:String? = nil,next:Int? = nil,transport:String = "Wi-Fi",session:String = "fixture",kind:String = "wifi-unlock",extra:[String:Any] = [:]) -> MacTab5RPCResult {
            var root:[String:Any] = ["kind":kind,"op":op,"requestId":id,"session":session]
            if let offer { root["offer"] = offer }; if let next { root["next"] = next }; root.merge(extra){ _,new in new }
            return feature.handle(root,pair:pair,transport:transport,session:"fixture",allowed:{allowed})
        }
        try check("USB never receives credential or starts worker",request(transport:"USB").status == 409 && queued.isEmpty)
        try check("BLE never receives credential or starts worker",request(transport:"BLE").status == 409 && queued.isEmpty)
        try check("session checked again in feature",request(session:"old").body["ok"] as? Bool == false && queued.isEmpty)
        try check("network cannot specify password or keys",request(extra:["password":"ignored"]).body["ok"] as? Bool == false && queued.isEmpty)
        locked = nil
        try check("unknown focus admits only backend wake preparation",request().body["stage"] as? String == "preparing" && queued.count == 1)
        feature.cancel(); queued.removeFirst()()
        try check("cancel before wake prevents backend effects",cancelled?() == true)
        executionCount = 0
        locked = false; try check("desktop cannot start",request().body["ok"] as? Bool == false && queued.isEmpty)
        locked = true
        let id = UUID().uuidString.lowercased(), begin = request(id:id), offer = begin.body["offer"] as! String
        try check("begin has matching first sequence",begin.body["next"] as? Int == 0 && begin.body["unlocked"] as? Bool == false && queued.count == 1)
        try check("wire result contains no saved credential",!(String(data:try MacTab5Protocol.json(begin.body),encoding:.utf8) ?? "").contains("Fixture9!"))
        try check("duplicate begin never queues a second effect",request(id:id).body["ok"] as? Bool == false && queued.count == 1)
        try check("wrong offer cannot take over",request("poll",id:id,offer:"wrong",next:0).body["ok"] as? Bool == false)
        queued.removeFirst()()
        try check("only confirmed backend returns unlock success",request("poll",id:id,offer:offer,next:0).body["unlocked"] as? Bool == true && executionCount == 1)
        try check("accepted Wi-Fi confirmation duration remains two seconds",stableDesktopSeconds == 2)
        _ = request("cancel",id:id,offer:offer)
        try check("firmware cleanup preserves confirmed result",feature.diagnostic == "本次解锁已确认成功")
        try check("local timing exposes only recognized stages and bounded numeric durations",feature.timingDiagnostic.contains("Wi-Fi 解锁成功：总计") && feature.timingDiagnostic.contains("填入一次登录输入") && !feature.timingDiagnostic.contains("untrusted-fixture-stage"))
        try check("timing never contains credentials or request identities",!feature.timingDiagnostic.contains("Fixture9!") && !feature.timingDiagnostic.contains(id) && !feature.timingDiagnostic.contains(offer))
        try check("cancelled request cannot be reused",request(id:id).body["ok"] as? Bool == false)
        let cancelledID = UUID().uuidString.lowercased(), pending = request(id:cancelledID), pendingOffer = pending.body["offer"] as! String
        _ = request("cancel",id:cancelledID,offer:pendingOffer); queued.removeFirst()()
        try check("cancellation prevents scheduled input",cancelled?() == true)
        let staleID = UUID().uuidString.lowercased(), stale = request(id:staleID), staleOffer = stale.body["offer"] as! String
        _ = request("poll",id:staleID,offer:staleOffer,next:1); queued.removeFirst()()
        try check("out of order step cancels effect",cancelled?() == true)
        let leaseID = UUID().uuidString.lowercased(); _ = request(id:leaseID); time += 2.6; queued.removeFirst()()
        try check("missing poll expires input lease",cancelled?() == true)
        feature.cancel()
        try feature.armPreparationProbe(pair:pair)
        feature.cancel() // Lock/session notification must not silently disarm into password input.
        let beforeProbe = executionCount
        let probeID = UUID().uuidString.lowercased(), probeBegin = request(id:probeID), probeOffer = probeBegin.body["offer"] as! String
        do { try feature.armPreparationProbe(pair:pair); throw MacTab5Error.invalidPacket } catch is MacTab5LocalError { count += 1 }
        queued.removeFirst()()
        try check("armed probe never calls credential backend or reports unlocked",probeCount == 1 && executionCount == beforeProbe && request("poll",id:probeID,offer:probeOffer,next:0).body["unlocked"] as? Bool == false)
        _ = request("cancel",id:probeID,offer:probeOffer)
        try check("probe request cannot replay",request(id:probeID).body["ok"] as? Bool == false && queued.isEmpty)
        _ = request(); queued.removeFirst()()
        try check("subsequent deliberate request returns to ordinary backend",executionCount == beforeProbe+1 && probeCount == 1)
        feature.cancel()
        try feature.armPreparationProbe(pair:pair); time += 120
        try check("expired diagnostic never silently becomes password input",request().body["error"] as? String == "preparation_probe_expired_no_input" && queued.isEmpty && executionCount == beforeProbe+1)
        do { try feature.armPreparationProbe(pair:other); throw MacTab5Error.invalidPacket } catch is MacTab5LocalError { count += 1 }
        try feature.armPreparationProbe(pair:pair); _ = request(); feature.cancel(); queued.removeFirst()()
        try check("cancelled queued probe has no effects",probeCount == 1 && executionCount == beforeProbe+1)
        backendResult = false
        let failedID = UUID().uuidString.lowercased(), failed = request(id:failedID), failedOffer = failed.body["offer"] as! String
        queued.removeFirst()()
        try check("submission uncertainty is never unlocked",request("poll",id:failedID,offer:failedOffer,next:0).body["unlocked"] as? Bool == false)
        let failureNote = feature.diagnostic
        _ = request("cancel",id:failedID,offer:failedOffer)
        try check("firmware cleanup preserves failed stage and reason",feature.diagnostic == failureNote && failureNote.contains("填入一次登录输入") && failureNote.contains("未确认桌面恢复"))
        feature.cancel(); _ = request(); try feature.setEnabled(false,pair:pair); queued.removeFirst()()
        try check("disable cancels in flight and retains Keychain item",cancelled?() == true && saved != nil && !feature.configured(pair))
        try feature.setEnabled(true,pair:pair); _ = request(); allowed = false; queued.removeFirst()()
        try check("revoked device permission cancels input",cancelled?() == true)
        feature.cancel(); allowed = true; backendResult = true
        try check("USB and BLE permission defaults off independently",!feature.localEnabled && feature.configured(pair) && !feature.configured(pair,transport:"USB") && !feature.configured(pair,transport:"BLE"))
        try check("USB intent requires separate permission",request(transport:"USB",kind:"host-unlock").body["error"] as? String == "host_unlock_disabled" && queued.isEmpty)
        do { try feature.setPermissions(wifi:false,local:true,pair:other); throw MacTab5Error.invalidPacket } catch is MacTab5LocalError { count += 1 }
        try check("invalid local enable leaves Wi-Fi permission unchanged",feature.enabled && !feature.localEnabled)
        try feature.setLocalEnabled(true,pair:pair)
        try check("local enable preserves Wi-Fi and pairing binding",feature.configured(pair) && feature.configured(pair,transport:"USB") && feature.configured(pair,transport:"BLE") && !feature.configured(other,transport:"USB"))
        try check("host intent never runs over Wi-Fi",request(kind:"host-unlock").status == 409 && queued.isEmpty)
        for link in ["USB","BLE"] {
            let opposite = link == "USB" ? "BLE":"USB"
            let localID = UUID().uuidString.lowercased(), begin = request(id:localID,transport:link,kind:"host-unlock"), localOffer = begin.body["offer"] as! String
            try check("local begin does not export a credential",Set(begin.body.keys) == Set(["ok","offer","next","stage","unlocked"]) && queued.count == 1)
            try check("Wi-Fi cannot start while local worker owns input",request().body["error"] as? String == "unlock_busy" && queued.count == 1)
            try check("opposite link cannot poll local offer",request("poll",id:localID,offer:localOffer,next:0,transport:opposite,kind:"host-unlock").body["error"] as? String == "unlock_offer_changed")
            try check("opposite link cannot cancel local offer",request("cancel",id:localID,offer:localOffer,transport:opposite,kind:"host-unlock").body["error"] as? String == "unlock_offer_changed")
            feature.cancel(transport:opposite)
            let before = executionCount; queued.removeFirst()()
            try check("local worker uses existing backend once",executionCount == before+1 && cancelled?() == false)
            try check("USB/BLE select bounded half-second confirmation independently of Wi-Fi",stableDesktopSeconds == 0.5)
            try check("local confirmation survives other link disconnect",request("poll",id:localID,offer:localOffer,next:0,transport:link,kind:"host-unlock").body["unlocked"] as? Bool == true)
            _ = request("cancel",id:localID,offer:localOffer,transport:link,kind:"host-unlock")
            try check("local request cannot replay on another transport",request(id:localID,transport:opposite,kind:"host-unlock").body["error"] as? String == "unlock_request_consumed" && request(id:localID).body["error"] as? String == "unlock_request_consumed" && queued.isEmpty)
            _ = request(transport:link,kind:"host-unlock"); feature.cancel(transport:link); queued.removeFirst()()
            try check("matching disconnect cancels queued local input",cancelled?() == true)
            lockReadHook = { feature.cancel(transport:link) }
            let admission = request(transport:link,kind:"host-unlock"), observationInput = executionCount
            try check("local admission replies before inspecting focus",admission.body["offer"] is String && queued.count == 1)
            queued.removeFirst()()
            try check("disconnect during deferred observation cannot input",executionCount == observationInput)
            lockReadHook = nil
            locked = false
            let unlockedAdmission = request(transport:link,kind:"host-unlock"), unlockedInput = executionCount
            try check("local admission is preparation only",unlockedAdmission.body["unlocked"] as? Bool == false && queued.count == 1)
            queued.removeFirst()()
            try check("deferred unlocked check rejects before backend",executionCount == unlockedInput)
            feature.cancel(); locked = true
            var observations = 0
            lockReadHook = { observations += 1;time += 2.1 }
            let slowID = UUID().uuidString.lowercased(), slowStart = request(id:slowID,transport:link,kind:"host-unlock")
            try check("slow focus cannot delay the initial reply",observations == 0 && queued.count == 1 && slowStart.body["offer"] is String)
            let slowOffer = slowStart.body["offer"] as! String
            try check("poll may renew while worker awaits focus",request("poll",id:slowID,offer:slowOffer,next:0,transport:link,kind:"host-unlock").body["next"] as? Int == 1)
            let slowInput = executionCount;queued.removeFirst()()
            try check("bounded slow observation still uses backend once",observations == 1 && executionCount == slowInput+1)
            feature.cancel(); lockReadHook = nil
            _ = request(transport:link,kind:"host-unlock"); time += 2.6; queued.removeFirst()()
            try check("local polling lease expires before input",cancelled?() == true)
            feature.cancel()
        }
        let wifiID = UUID().uuidString.lowercased(), wifiBegin = request(id:wifiID), wifiOffer = wifiBegin.body["offer"] as! String
        feature.cancel(transport:"USB"); feature.cancel(transport:"BLE"); queued.removeFirst()()
        try check("USB and BLE disconnect preserve Wi-Fi unlock",request("poll",id:wifiID,offer:wifiOffer,next:0).body["unlocked"] as? Bool == true)
        _ = request("cancel",id:wifiID,offer:wifiOffer)
        try feature.armPreparationProbe(pair:pair)
        try check("armed Wi-Fi probe cannot become USB password input",request(transport:"USB",kind:"host-unlock").body["error"] as? String == "preparation_probe_expired_no_input" && queued.isEmpty)
        try feature.setPermissions(wifi:false,local:true,pair:pair)
        try check("local permission works with Wi-Fi disabled",!feature.configured(pair) && feature.configured(pair,transport:"BLE"))
        let localReload = MacHostUnlock(defaults:defaults,read:{saved},write:{_ in},remove:{},locked:{true},execute:{_,_,_,_ in false},schedule:{_ in})
        localReload.load()
        try check("local-only restart loads existing Keychain credential",localReload.configured(pair,transport:"USB") && !localReload.configured(pair)); localReload.stop()
        _ = request(transport:"BLE",kind:"host-unlock"); try feature.setLocalEnabled(false,pair:pair); queued.removeFirst()()
        try check("local permission revocation cancels input without deleting password",cancelled?() == true && saved != nil)
        try feature.setEnabled(true,pair:pair)
        try check("disabling local feature preserves Wi-Fi availability",feature.configured(pair) && !feature.configured(pair,transport:"USB"))
        try feature.setLocalEnabled(true,pair:pair)
        backendAdvance = 2
        for link in ["USB","BLE"] {
            for success in [true,false] {
                feature.cancel();backendResult = success
                let id = UUID().uuidString.lowercased(), start = request(id:id,transport:link,kind:"host-unlock"), offer = start.body["offer"] as! String
                let before = executionCount;queued.removeFirst()();time += 1
                let result = request("poll",id:id,offer:offer,next:0,transport:link,kind:"host-unlock")
                try check("completed local receipt survives expired input lease without another effect",executionCount == before+1 && (success ? result.body["unlocked"] as? Bool == true:result.body["error"] as? String == "unlock_stopped_no_retry"))
                let note = feature.diagnostic;time += 3
                _ = request("cancel",id:id,offer:offer,transport:link,kind:"host-unlock")
                try check("late cleanup cannot erase terminal diagnostic",feature.diagnostic == note)
            }
            feature.cancel();backendResult = true
            let id = UUID().uuidString.lowercased(), start = request(id:id,transport:link,kind:"host-unlock"), offer = start.body["offer"] as! String
            queued.removeFirst()();time += 40
            try check("terminal receipt never survives total request deadline",request("poll",id:id,offer:offer,next:0,transport:link,kind:"host-unlock").body["error"] as? String == "unlock_offer_changed")
        }
        feature.cancel();backendResult = true
        let delayedWiFiID = UUID().uuidString.lowercased(), delayedWiFi = request(id:delayedWiFiID), delayedWiFiOffer = delayedWiFi.body["offer"] as! String
        queued.removeFirst()();time += 1
        try check("accepted Wi-Fi terminal lease behavior unchanged",request("poll",id:delayedWiFiID,offer:delayedWiFiOffer,next:0).body["error"] as? String == "unlock_offer_changed")
        backendAdvance = 0
        feature.cancel(); try feature.delete()
        try check("delete removes secret and disables every transport",saved == nil && !feature.enabled && !feature.localEnabled)
        let before = reads; reloaded.load()
        try check("disabled startup does not read Keychain",reads == before)
        try BridgeKeychain.Request(operation:"remove",account:MacHostUnlock.account).validate(); count += 1
        do { try BridgeKeychain.Request(operation:"remove",account:"tab5-pairing-v1").validate(); throw MacTab5Error.invalidPacket } catch is BridgeKeychain.Failure { count += 1 }
        feature.stop()
        return count + (try serviceRoundTrip())
    }
    private static func serviceRoundTrip() throws -> Int {
        var count = 0
        func check(_ name:String,_ value:@autoclosure () throws -> Bool) throws { guard try value() else { throw MacTab5LocalError.invalid("Host unlock RPC: "+name) }; count += 1 }
        let domain = "AI-bot.host-unlock-rpc."+UUID().uuidString, defaults = UserDefaults(suiteName:domain)!
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(domain)
        try FileManager.default.createDirectory(at:root,withIntermediateDirectories:true)
        defer { defaults.removePersistentDomain(forName:domain); try? FileManager.default.removeItem(at:root) }
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:7,count:32),confirmed:true)
        var queued:[() -> Void] = [], executed = 0
        let unlock = MacHostUnlock(defaults:defaults,read:{nil},write:{_ in},remove:{},locked:{true},execute:{_,_,cancel,_ in
            guard !cancel() else { return false }; executed += 1; return true
        },schedule:{queued.append($0)})
        try unlock.saveVerified(Data("Fixture9!".utf8),pair:pair)
        try unlock.setPermissions(wifi:true,local:false,pair:pair)
        let computer = MacTab5Computer(defaults:defaults,backend:.init(read:{ ["locked":true] },perform:{_,_,_ in false}))
        let codex = MacTab5Codex(store:.init(root:root,key:{pair.key}),enabled:{false},call:{_,_ in [:]})
        let voice = MacTab5Voice(enabled:{false},backend:.init(start:{_,_ in},feed:{_ in},finish:{},cancel:{}))
        let service = MacTab5Service(port:8765,host:{nil},gallery:.init(directory:root),computer:computer,codex:codex,voice:voice,birthdays:.init(directory:root),agenda:.init(directory:root),unlock:unlock,load:{pair},save:{_ in})
        defer { service.stop() }
        service.publish(.init(version:1,time:"12:00",epochUtc:1_729_000_000,utcOffsetSeconds:28800,capturedAt:Date(),codex:.init(state:"idle",ageSeconds:0),claude:.init(state:"idle",ageSeconds:0),musicPlaying:nil,weather:nil,stocks:nil,systemMetrics:nil,quotas:nil,music:nil))
        func health() throws -> [String:Any] {
            let data = try MacTab5Protocol.object(service.frame())["data"] as? [String:Any]
            return data?["connectionHealth"] as? [String:Any] ?? [:]
        }
        try check("old firmware capability and Wi-Fi preserved",try health()["wifiUnlockVersion"] as? Int == 1 && health()["hostUnlockVersion"] == nil && computer.handle(["op":"status"]).body["oneClickUnlock"] as? Bool == false)
        try unlock.setLocalEnabled(true,pair:pair)
        try check("local capability is optional and independently enabled",try health()["hostUnlockVersion"] as? Int == 1 && health()["wifiUnlockVersion"] as? Int == 1)
        func rpc(_ op:String="begin",link:String="USB",id:String=UUID().uuidString,offer:String?=nil,proofOK:Bool=true,session:String?=nil) throws -> MacTab5RPCResult {
            var body:[String:Any] = ["kind":"host-unlock","op":op,"requestId":id,"session":session ?? service.session,"issuedAt":Int64(Date().timeIntervalSince1970*1000)]
            if let offer { body["offer"] = offer }; if op == "poll" { body["next"] = 0 }
            let nonce = MacTab5Protocol.identifier(), packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(body),key:pair.key,challenge:nonce)
            let proof = proofOK ? MacTab5Protocol.proof(key:pair.key,message:"POST|/tab5/v1/rpc|\(pair.deviceId)|\(nonce)|\(MacTab5Protocol.sha256(packet))") : String(repeating:"0",count:64)
            let reply = service.rpc(device:pair.deviceId,nonce:nonce,proof:proof,packet:packet,transport:link)
            guard reply.0 == 200 else { return .init(reply.0,[:]) }
            let root = try MacTab5Protocol.object(MacTab5Protocol.decrypt(reply.1,key:pair.key,challenge:nonce))
            return .init(Int(MacTab5Protocol.integer(root["status"]) ?? 500),root["body"] as? [String:Any] ?? [:])
        }
        try check("forged proof and stale session never queue input",rpc(proofOK:false).status == 401 && rpc(session:"stale").status == 400 && queued.isEmpty)
        try check("host intent rejected on Wi-Fi at dispatcher",rpc(link:"Wi-Fi").status == 409 && queued.isEmpty)
        for link in ["USB","BLE"] {
            let id = UUID().uuidString, begin = try rpc(link:link,id:id)
            try check("authenticated local dispatch reaches coordinator",begin.body["ok"] as? Bool == true && queued.count == 1)
            queued.removeFirst()()
            try check("encrypted local result contains confirmation",rpc("poll",link:link,id:id,offer:begin.body["offer"] as? String).body["unlocked"] as? Bool == true)
            _ = try rpc("cancel",link:link,id:id,offer:begin.body["offer"] as? String)
            _ = try rpc(link:link)
            if link == "USB" { service.usbState("fixture disconnected") } else { service.bleState("fixture disconnected") }
            let before = executed; queued.removeFirst()()
            try check("transport disconnect callback stops pending input",executed == before)
        }
        try unlock.setLocalEnabled(false,pair:pair)
        try check("disabling local capability leaves Wi-Fi published",try health()["hostUnlockVersion"] == nil && health()["wifiUnlockVersion"] as? Int == 1)
        return count
    }
}
