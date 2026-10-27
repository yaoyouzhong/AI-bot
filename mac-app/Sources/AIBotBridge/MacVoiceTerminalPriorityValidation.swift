import Foundation

enum MacVoiceTerminalPriorityValidation {
    private final class Box {
        private let lock = NSLock()
        private var status = 0
        func set(_ value: Int) { lock.lock(); status = value; lock.unlock() }
        var value: Int { lock.lock(); defer { lock.unlock() }; return status }
    }
    private final class Catalog {
        let task = UUID().uuidString.lowercased()
        let entered = DispatchSemaphore(value:0), release = DispatchSemaphore(value:0)
        private let lock = NSLock()
        private var blocking = false
        func block() { lock.lock(); blocking = true; lock.unlock() }
        func call(_ method: String, _ params: [String:Any]) throws -> [String:Any] {
            lock.lock(); let wait = blocking; lock.unlock()
            if method == "thread/list" {
                if wait { entered.signal(); release.wait() }
                return ["data":[["id":task,"name":"fixture","cwd":"/fixture","status":["type":"idle"]]]]
            }
            return ["thread":["id":task,"status":["type":"idle"],"turns":[Any]()]]
        }
    }
    static func run(root: URL) throws -> Int {
        var checks = 0
        func check(_ name: String, _ ok: @autoclosure () throws -> Bool) throws {
            guard try ok() else { throw MacTab5LocalError.invalid("Voice terminal priority: "+name) }; checks += 1
        }
        var now = 100.0, enabled = true
        var emit: ((String,Bool,MacTab5SpeechFailure?) -> Void)?
        let voice = MacTab5Voice(enabled:{enabled},clock:{now},backend:.init(start:{_,callback in emit = callback},feed:{_ in},finish:{},cancel:{}))
        let pair = MacTab5Pairing(deviceId:"001122334455",usbPath:"/dev/cu.fixture",key:Data(repeating:8,count:32),confirmed:true)
        let defaults = UserDefaults(suiteName:"voice-terminal-"+UUID().uuidString)!
        let codex = MacTab5Codex(store:.init(root:root,key:{pair.key}),enabled:{true},call:{_,_ in [:]})
        let computer = MacTab5Computer(defaults:defaults,backend:.init(read:{[:]},perform:{_,_,_ in false}))
        let service = MacTab5Service(port:8765,host:{nil},gallery:.init(directory:root),computer:computer,codex:codex,voice:voice,
            birthdays:.init(directory:root),agenda:.init(directory:root),load:{pair},save:{_ in})
        defer { service.stop() }
        var lastNonce = ""
        func rpc(_ value: [String:Any]) throws -> MacTab5RPCResult {
            var root = value; root["session"] = service.session; root["issuedAt"] = Int64(Date().timeIntervalSince1970*1000)
            let nonce = MacTab5Protocol.identifier(), packet = try MacTab5Protocol.encrypt(MacTab5Protocol.json(root),key:pair.key,challenge:nonce)
            lastNonce = nonce
            let proof = MacTab5Protocol.proof(key:pair.key,message:"POST|/tab5/v1/rpc|\(pair.deviceId)|\(nonce)|\(MacTab5Protocol.sha256(packet))")
            let response = service.rpc(device:pair.deviceId,nonce:nonce,proof:proof,packet:packet)
            guard response.0 == 200 else { return .error(response.0,"fixture_transport") }
            let decoded = try MacTab5Protocol.object(MacTab5Protocol.decrypt(response.1,key:pair.key,challenge:nonce))
            return .init(Int(MacTab5Protocol.integer(decoded["status"]) ?? 500),decoded["body"] as? [String:Any] ?? [:])
        }
        let task = UUID().uuidString.lowercased()
        func start() throws -> String {
            let result = try rpc(["kind":"voice","op":"start","taskId":task])
            guard result.status == 200, let id = result.body["voiceId"] as? String else { throw MacTab5LocalError.invalid("fixture voice start") }
            return id
        }
        try check("idle has no transport reservation",!voice.needsTransportPriority)
        let id = try start()
        try check("capture reserves transport",voice.busy && voice.needsTransportPriority)
        let ack: [String:Any] = ["kind":"codex","op":"read-ack","taskId":task,"requestId":UUID().uuidString.lowercased(),"turnId":UUID().uuidString.lowercased()]
        try check("authenticated background ACK yields during capture",try rpc(ack).status == 503)
        let stop = try rpc(["kind":"voice","op":"stop","voiceId":id])
        try check("stop first waits for recognizer",stop.body["state"] as? String == "recognizing")
        now += 0.075; emit?("published fixture",true,nil)
        try check("recognition completes before device final poll",!voice.busy)
        try check("old busy-only pacing would resume too early",voice.needsTransportPriority)
        voice.observeReply(op:"stop",transport:"USB",status:200,response:stop.body)
        try check("late recognizing response cannot release final reservation",voice.needsTransportPriority)
        try check("background ACK also yields after recognition",try rpc(ack).status == 503)
        try check("background read also yields before terminal poll",try rpc(["kind":"codex","op":"read","taskId":task]).status == 503)
        var order: [String] = [], final: MacTab5RPCResult?
        var priorityBeforeACK = false
        _ = try MacTab5USBVoicePriority.run(rpcSupported:true,voiceBusy:{voice.needsTransportPriority},claim:{order.append("claim")},status:{order.append("status")},rpc:{
            order.append("rpc"); final = try rpc(["kind":"voice","op":"poll","voiceId":id])
            priorityBeforeACK = voice.needsTransportPriority
            voice.acknowledgeUSBReply(nonce:lastNonce)
        },resources:{order.append("image");return 1})
        try check("final result precedes status and resumed images",order == ["claim","rpc","status","image"])
        try check("terminal reply returns exact retained public text",final?.body["text"] as? String == "published fixture")
        try check("generated terminal response retains reservation until wire ACK",priorityBeforeACK)
        try check("served terminal response releases reservation",!voice.needsTransportPriority)
        let old = final!.body, oldNonce = lastNonce, next = try start()
        _ = try rpc(["kind":"voice","op":"stop","voiceId":next]); emit?("next fixture",true,nil)
        voice.observeReply(op:"poll",transport:"USB",status:200,response:old)
        try check("old take cannot release newer terminal reservation",!voice.busy && voice.needsTransportPriority)
        _ = try rpc(["kind":"voice","op":"poll","voiceId":next])
        try check("lost terminal packet keeps priority",voice.needsTransportPriority)
        voice.acknowledgeUSBReply(nonce:oldNonce)
        try check("old packet ACK cannot release new take",voice.needsTransportPriority)
        voice.acknowledgeUSBReply(nonce:String(repeating:"0",count:32))
        try check("unrelated ACK cannot release terminal result",voice.needsTransportPriority)
        now += 30.1
        try check("disconnected terminal wait is bounded",!voice.needsTransportPriority)
        let cancelled = try start(); enabled = false
        try check("disabled feature immediately releases transport",!voice.needsTransportPriority)
        enabled = true
        let cancel = try rpc(["kind":"voice","op":"cancel","voiceId":cancelled])
        try check("cancel terminal packet also waits for wire ACK",cancel.body["state"] as? String == "cancelled" && voice.needsTransportPriority)
        voice.acknowledgeUSBReply(nonce:lastNonce)
        try check("cancel terminal response releases transport",!voice.needsTransportPriority)

        // Actual Codex gate contention must never occupy the USB mailbox for
        // an idempotent read receipt. Release the fixture before asserting.
        let catalog = Catalog(), journal = MacTab5Codex(store:.init(root:root.appendingPathComponent("gate"),key:{pair.key}),enabled:{true},call:catalog.call)
        journal.refresh(); catalog.block()
        let refreshDone = DispatchSemaphore(value:0), ackDone = DispatchSemaphore(value:0), result = Box()
        DispatchQueue.global().async { journal.refresh(); refreshDone.signal() }
        let entered = catalog.entered.wait(timeout:.now()+1) == .success
        DispatchQueue.global().async {
            result.set(journal.handle(["op":"read-ack","taskId":catalog.task,"requestId":UUID().uuidString.lowercased(),"turnId":UUID().uuidString.lowercased()],device:pair.deviceId).status)
            ackDone.signal()
        }
        let quick = ackDone.wait(timeout:.now()+0.2) == .success
        catalog.release.signal(); let refreshed = refreshDone.wait(timeout:.now()+1) == .success
        if !quick { _ = ackDone.wait(timeout:.now()+1) }
        try check("actual catalog refresh owns gate fixture",entered && refreshed)
        try check("contended read receipt releases mailbox promptly",quick && result.value == 503)
        return checks
    }
}
