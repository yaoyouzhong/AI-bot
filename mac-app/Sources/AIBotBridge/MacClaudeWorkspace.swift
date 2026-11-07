import Foundation

// Claude owns the CLI wire; the existing session workspace owns device RPC,
// encrypted drafts/photos and request deduplication. No model/permission flags
// are added here. Production operates the original client, never a resumed CLI.
final class MacClaudeWorkspace {
    let tasks: MacTab5Codex
    let backend: MacClaudeWorkspaceBackend
    convenience init(enabled: @escaping () -> Bool = { true }) {
        let store = MacTab5PrivateStore(root:FileManager.default.urls(for:.applicationSupportDirectory,in:.userDomainMask)[0].appendingPathComponent("AI-bot/Tab5/Claude"))
        self.init(store:store,backend:MacClaudeWorkspaceBackend(store:store),enabled:enabled)
    }
    init(store: MacTab5PrivateStore, backend: MacClaudeWorkspaceBackend, enabled: @escaping () -> Bool) {
        self.backend = backend
        tasks = MacTab5Codex(store:store,enabled:enabled,sendEnabled:enabled,call:backend.call,provider:"Claude")
        tasks.deliveryReceipt = backend.receipt
        tasks.deliveryCancelled = backend.cancelled
        tasks.onDisable = backend.stop
        backend.nativeAuthorization = { [weak taskWorkspace = tasks] in taskWorkspace?.nativeAuthorized() == true }
    }
    func start() { tasks.start() }
    func stop() { tasks.stop(); backend.stop() }
}

final class MacClaudeWorkspaceBackend {
    struct Approval { let id: String, task: String, tool: String, input: [String:Any] }
    private struct Message {
        let id: String, turn: String, started: Int64
        var observed = Date()
        var accepted = false, status = "inProgress", items: [[String:Any]] = []
    }
    private final class Owner {
        var stream: MacClaudeStream?
        var messages: [Message] = []
        var approvals: [Approval] = []
        var ended = false, interrupted = false
    }
    private let catalog: MacClaudeCatalog, root: URL, store: MacTab5PrivateStore, key: () throws -> Data
    private let executable: () -> URL?, live: (() throws -> [[String:Any]])?
    private let environment: [String:String]?, arguments: [String]
    private let lock = NSLock(), launch = NSLock()
    private var owners: [String:Owner] = [:], interruptionReceipts: [String:String] = [:]
    private var settled: [String:String] = [:]
    private var cancelledMessages: [String:Bool] = [:]
    private var lastLive: [[String:Any]] = [], liveAt = Date.distantPast
    private let native: MacClaudeNativeBackend?
    private let discoverNativePanels: Bool
    var nativeAuthorization: () -> Bool = {false} { didSet { native?.authorized = nativeAuthorization } }
    init(root: URL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/projects"), store: MacTab5PrivateStore,
         key: @escaping () throws -> Data = MacTab5PrivateStore.keychainKey,
         executable: @escaping () -> URL? = MacClaudeWorkspaceBackend.locate, live: (() throws -> [[String:Any]])? = nil,
          environment: [String:String]? = nil, arguments: [String] = [], legacyFixture: Bool = false,
          harnesses: MacClaudeHarnesses? = nil,
          nativeFactory: ((UUID) -> (any MacClaudeNativeSession)?)? = nil) {
        self.root = root; self.store = store; self.key = key; self.executable = executable; self.live = live
        self.environment = environment; self.arguments = arguments
        catalog = MacClaudeCatalog(root:root,key:key)
        let normalRoot = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/projects").standardizedFileURL
        discoverNativePanels = harnesses != nil || root.standardizedFileURL.path == normalRoot.path
        native = legacyFixture ? nil : MacClaudeNativeBackend(root:root,store:store,key:key,harnesses:harnesses ?? .standard,
            factory:nativeFactory ?? (discoverNativePanels ? nil : { _ in nil }))
        settled = (try? store.read("terminal-results").map(MacTab5Protocol.object)) as? [String:String] ?? [:]
        cancelledMessages = (try? store.read("cancelled-messages").map(MacTab5Protocol.object)) as? [String:Bool] ?? [:]
    }
    static func locate() -> URL? {
        let home = FileManager.default.homeDirectoryForCurrentUser
        let paths = [home.appendingPathComponent(".local/bin/claude").path,"/opt/homebrew/bin/claude","/usr/local/bin/claude"] +
            (ProcessInfo.processInfo.environment["PATH"] ?? "").split(separator:":").map { String($0)+"/claude" }
        return paths.first(where: { $0.hasPrefix("/") && FileManager.default.isExecutableFile(atPath:$0) }).map(URL.init(fileURLWithPath:))
    }
    func alias(session: UUID) -> String? { catalog.snapshot().first { $0.session == session }?.alias }
    func selection(over bound: MacClaudeIdentity.Selection?, now: Date = Date()) -> MacClaudeIdentity.Selection? {
        let entries = catalog.snapshot()
        if let native { return native.selection(entries,bound:bound,now:now) }
        lock.lock(); defer { lock.unlock() }
        let owned = entries.compactMap { entry -> MacClaudeIdentity.Selection? in
            guard let owner = owners[entry.alias], let message = owner.messages.first(where: { $0.status == "inProgress" }) ?? owner.messages.last,
                  let prompt = UUID(uuidString:message.id) else { return nil }
            let running = owner.messages.contains { $0.status == "inProgress" }
            let waiting = running && !owner.approvals.isEmpty
            let expired = running && now.timeIntervalSince(message.observed) >= (waiting ? 300 : 600)
            let state = expired ? "unknown" : waiting ? "working" : running ? (message.accepted ? "working":"unknown") : message.status == "failed" ? "error" : ["completed","interrupted"].contains(message.status) ? "idle":"unknown"
            return MacClaudeIdentity.Selection(sessionID:entry.session,promptID:prompt,activity:message.observed,state:state,
                outcome:message.status,needsInput:waiting && !expired,completionAt:message.status == "completed" ? Int64(message.observed.timeIntervalSince1970):0,
                completionSequence:0,ownedTurn:message.turn)
        }
        return MacClaudeIdentity.resolve(bound:bound,owned:owned)
    }
    private func liveSessions(fresh: Bool = false) throws -> [[String:Any]] {
        lock.lock(); let cached = lastLive, at = liveAt; lock.unlock()
        if !fresh, Date().timeIntervalSince(at) < 3 { return cached }
        let rows: [[String:Any]]
        if let live { rows = try live() }
        else {
            guard let executable = executable() else { throw MacTab5CodexFailure.cliUnavailable }
            let p = Process(), output = Pipe()
            p.executableURL = executable; p.arguments = ["agents","--json"]; p.standardOutput = output; p.standardError = FileHandle.nullDevice
            if let environment { p.environment = environment }
            try p.run()
            let deadline = DispatchWorkItem { if p.isRunning { p.terminate() } }
            DispatchQueue.global().asyncAfter(deadline:.now()+8,execute:deadline)
            // Bounded read avoids pipe backpressure; never log raw list entries.
            var bytes = Data()
            while let block = try output.fileHandleForReading.read(upToCount:65536), !block.isEmpty {
                guard bytes.count+block.count <= 2*1024*1024 else { p.terminate(); throw MacTab5Error.invalidPacket }; bytes.append(block)
            }
            p.waitUntilExit(); deadline.cancel()
            guard p.terminationStatus == 0, let value = try JSONSerialization.jsonObject(with:bytes) as? [[String:Any]] else { throw MacTab5Error.unavailable }
            rows = value
        }
        // A malformed/unsupported list cannot establish absence of an owner.
        guard rows.count <= 2048, rows.allSatisfy({ ["interactive","background"].contains($0["kind"] as? String ?? "") }) else { throw MacTab5Error.unconfirmed }
        lock.lock(); lastLive = rows; liveAt = Date(); lock.unlock(); return rows
    }
    private func external(_ entry: MacClaudeCatalog.Entry, fresh: Bool = false) throws -> Bool {
        try liveSessions(fresh:fresh).contains { row in
            if let raw = row["sessionId"] as? String, let id = UUID(uuidString:raw) { return id == entry.session }
            // An unidentified live owner in the same directory also blocks resume.
            return (row["cwd"] as? String).map { URL(fileURLWithPath:$0).standardizedFileURL.path == entry.directory } ?? true
        }
    }
    private func turn(_ entry: MacClaudeCatalog.Entry, message: String) throws -> String {
        MacClaudeCatalog.alias("claude:turn:"+entry.session.uuidString.lowercased()+":"+message,key:try key())
    }
    func call(_ method: String, _ params: [String:Any]) throws -> [String:Any] {
        if method == "thread/list" {
            if params["cursor"] == nil {
                try catalog.refresh(newPanels: discoverNativePanels ? native?.discover() ?? [] : [])
            }
            let rows = catalog.snapshot(), cursor = Int(params["cursor"] as? String ?? "0") ?? 0
            guard cursor >= 0, cursor <= rows.count else { throw MacTab5Error.invalidPacket }
            let slice = rows.dropFirst(cursor).prefix(64)
            let live = native?.liveSessions()
            return ["data":slice.map { entry -> [String:Any] in
                let info = (try? native?.thread(entry, includeHistory: false, live: live)) ?? (try? thread(entry,history:false)) ?? [:]
                var row: [String: Any] = ["id":entry.alias,"name":entry.title,"cwd":entry.directory.isEmpty ? "/项目待确认":entry.directory,"projectId":entry.project,"updatedAt":entry.updated,"status":info["status"] ?? ["type":"notLoaded"]]
                row["source"] = info["source"]; row["nativeControl"] = info["nativeControl"]
                return row
            },"nextCursor":cursor+slice.count < rows.count ? String(cursor+slice.count) as Any:NSNull()]
        }
        guard let task = params["threadId"] as? String, let entry = catalog.resolve(task) else { throw MacTab5Error.unavailable }
        if method == "thread/read" { return ["thread":try thread(entry)] }
        if method == "thread/resume" { return [:] } // Resume occurs only after the durable journal write.
        guard let request = MacTab5Codex.id(params["requestId"]) else { throw MacTab5Error.invalidPacket }
        if let native { return try native.call(method,entry:entry,params:params) }
        if method == "turn/interrupt" {
            lock.lock(); let owner = owners[task], stream = owner?.stream
            let active = owner?.messages.last(where: { $0.status == "inProgress" })?.turn; lock.unlock()
            guard let owner, let stream, active == params["turnId"] as? String else { throw MacTab5Error.unconfirmed }
            lock.lock(); owner.interrupted = true; lock.unlock()
            let response = try stream.interrupt()
            let cancelled = Set((response["cancelled"] as? [String] ?? []).compactMap { MacTab5Codex.id($0) })
            lock.lock()
            for index in owner.messages.indices where cancelled.contains(owner.messages[index].id) {
                owner.messages[index].status = "interrupted"
                cancelledMessages[task+"|"+owner.messages[index].id] = true
            }
            try? store.write("cancelled-messages",MacTab5Protocol.json(cancelledMessages))
            lock.unlock()
            // Receipt means requested. A matching terminal result proves interruption.
            for _ in 0..<20 {
                lock.lock(); let stopped = !owner.messages.contains { $0.status == "inProgress" }; lock.unlock()
                if stopped { lock.lock(); interruptionReceipts[request] = active; lock.unlock(); return ["turnId":active!] }
                Thread.sleep(forTimeInterval:0.05)
            }
            throw MacTab5Error.timeout
        }
        guard ["turn/start","turn/steer"].contains(method), let inputs = params["input"] as? [[String:Any]] else { throw MacTab5Error.invalidPacket }
        let text = inputs.filter { $0["type"] as? String == "text" }.compactMap { $0["text"] as? String }.joined(separator:"\n")
        let images = inputs.filter { $0["type"] as? String == "image" }
        let owner = try acquire(entry)
        let alias = try turn(entry,message:request)
        lock.lock()
        let active = owner.messages.last(where: { $0.status == "inProgress" })
        guard owner.approvals.isEmpty, (method == "turn/start" ? active == nil : active?.turn == params["expectedTurnId"] as? String), !owner.ended else { lock.unlock(); throw MacTab5Error.unconfirmed }
        if !owner.messages.contains(where: { $0.id == request }) {
            while owner.messages.count >= 128, let old = owner.messages.firstIndex(where: { $0.status != "inProgress" }) { owner.messages.remove(at:old) }
            guard owner.messages.count < 128 else { lock.unlock(); throw MacTab5Error.unavailable }
            owner.messages.append(Message(id:request,turn:alias,started:Int64(Date().timeIntervalSince1970)))
        } else { lock.unlock(); throw MacTab5Error.unconfirmed }
        let stream = owner.stream; lock.unlock()
        try stream?.send(id:UUID(uuidString:request)!,text:text,images:images)
        for _ in 0..<20 {
            if receipt(task,request,method == "turn/start" ? "send":"steer","") != nil { return ["turnId":alias] }
            Thread.sleep(forTimeInterval:0.05)
        }
        throw MacTab5Error.timeout
    }
    private func acquire(_ entry: MacClaudeCatalog.Entry) throws -> Owner {
        guard native == nil else { throw MacTab5Error.unconfirmed }
        launch.lock(); defer { launch.unlock() }
        lock.lock(); let existing = owners[entry.alias], count = owners.count; lock.unlock()
        if let existing, !existing.ended { return existing }
        guard count < 8 || existing != nil, !entry.directory.isEmpty, FileManager.default.fileExists(atPath:entry.directory),
              let executable = executable(), try !external(entry,fresh:true) else { throw MacTab5Error.unconfirmed }
        let owner = Owner()
        let stream = MacClaudeStream(executable:executable,directory:URL(fileURLWithPath:entry.directory),session:entry.session,
                                     environment:environment,arguments:arguments) { [weak self, weak owner] frame in
            guard let self, let owner else { return }; self.receive(frame,entry:entry,owner:owner)
        }
        owner.stream = stream
        lock.lock(); owners[entry.alias] = owner; lock.unlock()
        do { try stream.start(); return owner } catch { stream.stop(); lock.lock(); owner.ended = true; lock.unlock(); throw error }
    }
    private func thread(_ entry: MacClaudeCatalog.Entry, history: Bool = true) throws -> [String:Any] {
        if let native { return try native.thread(entry,includeHistory:history) }
        var turns: [[String:Any]] = [], limited = false
        if history {
            let value = try MacClaudeHistory.read(root:root,session:entry.session,key:key()); turns = value.turns; limited = value.limited
        }
        lock.lock(); let owner = owners[entry.alias], messages = owner?.messages ?? [], waiting = !(owner?.approvals.isEmpty ?? true), ended = owner?.ended ?? true, terminal = settled; lock.unlock()
        for index in turns.indices { if let id = turns[index]["id"] as? String, let status = terminal[id] { turns[index]["status"] = status } }
        // Transcript branches can be appended/relinked while Claude flushes a
        // turn. Keep this owned stream's known submission order authoritative.
        let owned = Set(messages.map(\.turn))
        let persisted = Dictionary(uniqueKeysWithValues:turns.compactMap { row -> (String,[String:Any])? in
            guard let id = row["id"] as? String, owned.contains(id) else { return nil }; return (id,row)
        })
        turns.removeAll { owned.contains($0["id"] as? String ?? "") }
        for message in messages {
            let row: [String:Any] = ["id":message.turn,"startedAt":message.started,"status":message.status,"items":message.items.isEmpty ? persisted[message.turn]?["items"] ?? []:message.items]
            turns.append(row)
        }
        let running = messages.contains { $0.status == "inProgress" }
        let externalOwner = owner != nil && !ended ? false : ((try? external(entry)) ?? true)
        let controllable = !entry.directory.isEmpty && !externalOwner && (!ended || !running)
        let status: [String:Any] = externalOwner ? ["type":"notLoaded"] : running ? ["type":"active","activeFlags":waiting ? ["waitingOnApproval"]:[String]()] : ["type":"idle"]
        return ["id":entry.alias,"turns":turns,"historyLimited":limited,"status":status,"controlUnavailable":!controllable]
    }
    func receipt(_ task: String, _ request: String, _ operation: String, _ expected: String) -> String? {
        if let native { guard let entry=catalog.resolve(task) else { return nil };return native.receipt(entry,request) }
        lock.lock()
        if operation == "interrupt" { let value = interruptionReceipts[request]; lock.unlock(); return value }
        let message = owners[task]?.messages.first(where: { $0.id == request }), accepted = message?.accepted ?? false
        lock.unlock()
        if accepted { return message?.turn }
        // The exact replay can arrive late. Never infer delivery from mtime,
        // assistant text, a hook's unrelated prompt_id, or a successful pipe write.
        guard let entry = catalog.resolve(task), let value = try? MacClaudeHistory.read(root:root,session:entry.session,key:key()), value.accepted.contains(request) else { return nil }
        lock.lock()
        if let owner = owners[task], let index = owner.messages.firstIndex(where: { $0.id == request }), !owner.messages[index].accepted {
            owner.messages[index].accepted = true; owner.messages[index].observed = Date()
        }
        lock.unlock()
        return try? turn(entry,message:request)
    }
    func cancelled(_ task: String, _ request: String) -> Bool { lock.lock(); defer { lock.unlock() }; return cancelledMessages[task+"|"+request] == true }
    private func receive(_ frame: [String:Any], entry: MacClaudeCatalog.Entry, owner: Owner) {
        lock.lock(); defer { lock.unlock() }
        let type = frame["type"] as? String ?? ""
        if type == "user", let raw = MacTab5Codex.id(frame["uuid"]), let index = owner.messages.firstIndex(where: { $0.id == raw && !$0.accepted && $0.status == "inProgress" }) { owner.messages[index].accepted = true; owner.messages[index].observed = Date() }
        if type == "assistant", frame["parent_tool_use_id"] == nil || frame["parent_tool_use_id"] is NSNull,
           let message = frame["message"] as? [String:Any], let content = message["content"] as? [[String:Any]],
           let index = owner.messages.firstIndex(where: { $0.status == "inProgress" }) {
            let text = content.filter { $0["type"] as? String == "text" }.compactMap { $0["text"] as? String }.joined(separator:"\n")
            if !text.isEmpty, text.utf8.count <= 1024*1024,
               owner.messages[index].items.reduce(0,{ $0+(($1["text"] as? String)?.utf8.count ?? 0) })+text.utf8.count <= 1024*1024 {
                owner.messages[index].observed = Date()
                owner.messages[index].items.append(["type":"agentMessage","phase":"final_answer","text":text])
            }
        }
        if type == "result" {
            let ids = Set(((frame["user_message_uuids"] as? [String] ?? []) + [frame["user_message_uuid"] as? String ?? ""]).compactMap { MacTab5Codex.id($0) })
            for index in owner.messages.indices where ids.contains(owner.messages[index].id) && owner.messages[index].status == "inProgress" {
                owner.messages[index].accepted = true; owner.messages[index].observed = Date()
                let reason = frame["terminal_reason"] as? String ?? ""
                owner.messages[index].status = reason == "completed" && frame["is_error"] as? Bool == false && (frame["num_turns"] as? Int ?? 0) > 0 ? "completed" : reason == "interrupted" ? "interrupted" : frame["is_error"] as? Bool == true ? "failed":"unknown"
                if settled.count < 4096 || settled[owner.messages[index].turn] != nil { settled[owner.messages[index].turn] = owner.messages[index].status }
            }
            owner.interrupted = false; owner.approvals.removeAll()
            try? store.write("terminal-results",MacTab5Protocol.json(settled))
        }
        if type == "control_request", let request = frame["request"] as? [String:Any], request["subtype"] as? String == "can_use_tool",
           let id = frame["request_id"] as? String, let tool = request["tool_name"] as? String, let input = request["input"] as? [String:Any], owner.approvals.count < 8 {
            if let existing = owner.approvals.first(where:{ $0.id == id }) {
                if existing.tool != tool || (try? MacTab5Protocol.json(existing.input)) != (try? MacTab5Protocol.json(input)) { owner.stream?.stop() }
            } else { if let index = owner.messages.lastIndex(where: { $0.status == "inProgress" }) { owner.messages[index].observed = Date() }; owner.approvals.append(Approval(id:id,task:entry.alias,tool:tool,input:input)) }
        }
        if type == "control_cancel_request", let id = frame["request_id"] as? String { owner.approvals.removeAll { $0.id == id } }
        if ["bridge_exit","bridge_protocol_error"].contains(type) {
            owner.ended = true; owner.approvals.removeAll()
            for index in owner.messages.indices where owner.messages[index].status == "inProgress" { owner.messages[index].status = "unknown"; owner.messages[index].observed = Date() }
        }
    }
    func approvals() -> [Approval] { lock.lock(); defer { lock.unlock() }; return owners.values.flatMap { $0.approvals } }
    func decide(_ approval: Approval, allow: Bool) throws {
        lock.lock(); let owner = owners[approval.task], current = owner?.approvals.first { $0.id == approval.id }, stream = owner?.stream; lock.unlock()
        guard let current, let stream else { throw MacTab5Error.unconfirmed }
        try stream.respond(request:current.id,value:allow ? ["behavior":"allow","updatedInput":current.input] : ["behavior":"deny","message":"用户拒绝了此操作。"])
        lock.lock(); owner?.approvals.removeAll { $0.id == approval.id }; lock.unlock()
    }
    func stop() { native?.stop();lock.lock(); let streams = owners.values.compactMap { $0.stream }; lock.unlock(); streams.forEach { $0.stop() } }
}
