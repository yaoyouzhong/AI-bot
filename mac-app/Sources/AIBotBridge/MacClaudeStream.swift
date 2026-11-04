import Foundation

// Independently implemented client for the official CLI stream/control wire.
// Raw frames stay local. Callers must whitelist public fields before projection.
final class MacClaudeStream {
    typealias Event = ([String:Any]) -> Void
    private final class Waiter { let ready = DispatchSemaphore(value:0); var value: [String:Any]? }
    private let process = Process(), input = Pipe(), output = Pipe()
    private let lock = NSLock(), writeLock = NSLock()
    private var waits: [String:Waiter] = [:], closed = false
    private let session: UUID, event: Event
    init(executable: URL, directory: URL, session: UUID, environment: [String:String]? = nil, arguments: [String] = [], event: @escaping Event) {
        self.session = session; self.event = event
        process.executableURL = executable; process.currentDirectoryURL = directory
        process.arguments = ["--print","--input-format","stream-json","--output-format","stream-json","--verbose","--replay-user-messages","--permission-prompt-tool","stdio","--resume="+session.uuidString.lowercased()] + arguments
        var env = environment ?? ProcessInfo.processInfo.environment
        env.removeValue(forKey:"CLAUDECODE")
        env["CLAUDE_CODE_EMIT_SESSION_STATE_EVENTS"] = "1"
        process.environment = env; process.standardInput = input; process.standardOutput = output; process.standardError = FileHandle.nullDevice
    }
    func start() throws {
        try process.run()
        let reader = output.fileHandleForReading
        DispatchQueue.global(qos:.utility).async { [self] in
            defer {
                process.waitUntilExit()
                lock.lock(); closed = true; let pending = Array(waits.values); waits.removeAll(); lock.unlock()
                pending.forEach { $0.ready.signal() }
                event(["type":"bridge_exit","status":Int(process.terminationStatus)])
            }
            var line = Data()
            do {
                while true {
                    // FileHandle.read(upToCount:) can wait to fill its count on
                    // a macOS pipe. A control reply must be consumed immediately.
                    let bytes = reader.availableData
                    if bytes.isEmpty { break }
                    for byte in bytes {
                        if byte == 10 {
                            if let frame = try JSONSerialization.jsonObject(with:line) as? [String:Any] { receive(frame) }
                            line.removeAll(keepingCapacity:true)
                        } else {
                            guard line.count < 12*1024*1024 else { throw MacTab5Error.invalidPacket }; line.append(byte)
                        }
                    }
                }
            } catch { if process.isRunning { process.terminate() } }
        }
        _ = try control("initialize", fields:["hooks":["UserPromptSubmit":[["hookCallbackIds":["aibot-observe-prompt"]]]]],timeout:30)
    }
    private func write(_ value: [String:Any]) throws {
        let bytes = try JSONSerialization.data(withJSONObject:value)
        guard bytes.count <= 12*1024*1024 else { throw MacTab5Error.invalidPacket }
        writeLock.lock(); defer { writeLock.unlock() }
        lock.lock(); let available = !closed; lock.unlock()
        guard available, process.isRunning else { throw MacTab5Error.unavailable }
        try input.fileHandleForWriting.write(contentsOf:bytes+Data("\n".utf8))
    }
    private func receive(_ frame: [String:Any]) {
        if let raw = frame["session_id"] as? String, UUID(uuidString:raw) != session {
            event(["type":"bridge_protocol_error"]); stop(); return
        }
        if frame["type"] as? String == "control_response", let body = frame["response"] as? [String:Any], let id = body["request_id"] as? String {
            lock.lock(); let waiter = waits[id]; waiter?.value = body; lock.unlock(); waiter?.ready.signal(); return
        }
        if frame["type"] as? String == "control_request", let request = frame["request"] as? [String:Any],
           request["subtype"] as? String == "hook_callback", let id = frame["request_id"] as? String {
            guard request["callback_id"] as? String == "aibot-observe-prompt" else {
                event(["type":"bridge_protocol_error"]); stop(); return
            }
            if let info = request["input"] as? [String:Any] {
                event(["type":"bridge_prompt_submitted","input":info])
            }
            try? respond(request:id,value:[:]); return
        }
        event(frame)
    }
    private func control(_ subtype: String, fields: [String:Any] = [:], timeout: TimeInterval = 5) throws -> [String:Any] {
        let id = UUID().uuidString.lowercased(), waiter = Waiter()
        lock.lock(); waits[id] = waiter; lock.unlock()
        defer { lock.lock(); waits.removeValue(forKey:id); lock.unlock() }
        var request = fields; request["subtype"] = subtype
        try write(["type":"control_request","request_id":id,"request":request])
        guard waiter.ready.wait(timeout:.now()+timeout) == .success else { throw MacTab5Error.timeout }
        lock.lock(); let value = waiter.value; lock.unlock()
        guard let value, value["subtype"] as? String == "success" else { throw MacTab5Error.unconfirmed }
        return value["response"] as? [String:Any] ?? [:]
    }
    func send(id: UUID, text: String, images: [[String:Any]]) throws {
        guard text.utf8.count <= 2000, images.count <= 3 else { throw MacTab5Error.invalidPacket }
        var content: [[String:Any]] = []
        if !text.isEmpty { content.append(["type":"text","text":text]) }
        for image in images {
            guard let url = image["url"] as? String, let split = url.range(of:";base64,"), url.hasPrefix("data:"),
                  ["image/png","image/jpeg"].contains(String(url[url.index(url.startIndex,offsetBy:5)..<split.lowerBound])) else { throw MacTab5Error.invalidPacket }
            content.append(["type":"image","source":["type":"base64","media_type":String(url[url.index(url.startIndex,offsetBy:5)..<split.lowerBound]),"data":String(url[split.upperBound...])]])
        }
        guard !content.isEmpty else { throw MacTab5Error.invalidPacket }
        try write(["type":"user","session_id":session.uuidString.lowercased(),"uuid":id.uuidString.lowercased(),"parent_tool_use_id":NSNull(),
                   "client_composed":true,"origin":["kind":"human"],"message":["role":"user","content":content]])
    }
    func interrupt() throws -> [String:Any] { try control("interrupt",fields:["cancel_queued":true]) }
    func respond(request: String, value: [String:Any]) throws {
        try write(["type":"control_response","response":["subtype":"success","request_id":request,"response":value]])
    }
    func stop() {
        lock.lock(); closed = true; lock.unlock()
        if process.isRunning { process.terminate() }
    }
    deinit { stop() }
}
