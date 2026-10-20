import Foundation

final class MacTab5Codex {
    static let enabledKey = "tab5_codex_replies_enabled"
    static let sendKey = "tab5_codex_send_enabled"
    typealias Call = (String, [String: Any]) throws -> [String: Any]
    private let gate = NSLock(), stateLock = NSLock()
    private let queue = DispatchQueue(label: "AI-bot.tab5-codex")
    private let call: Call, enabled: () -> Bool, sendEnabled: () -> Bool, mutable: () -> Bool
    private let clock: () -> TimeInterval
    private var catalogUpdated: TimeInterval = -.infinity
    let store: MacTab5PrivateStore
    let photos: MacTab5Photos
    private var timer: DispatchSourceTimer?
    private var catalog: [[String: Any]] = []
    private var projection: [String: Any] = [:]
    private var viewedTask = "", viewedDevice = ""
    private var viewedPage: [String: Any] = [:]
    private var ready: [String: String] = [:], read: [String: String] = [:]
    private var readReceipts: [String: (String, String, String, TimeInterval)] = [:]
    private var journal: [String: Any]?
    private var note = "尚未启用"
    private let rpc: MacTab5CodexRPC?
    private let desktop: MacCodexDesktopIPC?

    init(store: MacTab5PrivateStore = MacTab5PrivateStore(), enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, sendEnabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: sendKey) }, clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, canMutate: @escaping () -> Bool = { true }, desktop: MacCodexDesktopIPC? = nil, call: Call? = nil) {
        self.store = store; photos = MacTab5Photos(store: store); self.enabled = enabled; self.sendEnabled = sendEnabled; self.clock = clock
        if let call { self.call = call; rpc = nil; self.desktop = desktop; mutable = canMutate }
        else {
            let rpc = MacTab5CodexRPC(), desktop = desktop ?? MacCodexDesktopIPC()
            self.rpc = rpc; self.call = rpc.call; self.desktop = desktop; mutable = { desktop.available }
        }
    }
    var isEnabled: Bool { enabled() }
    var canMutate: Bool { enabled() && mutable() }
    var summary: String { stateLock.lock(); defer { stateLock.unlock() }; return note }
    func snapshot(taskLimit: Int = 16) -> [String: Any]? {
        guard enabled() else { return nil }
        stateLock.lock(); defer { stateLock.unlock() }
        var value = projection
        let tasks = projection["tasks"] as? [[String: Any]] ?? [], viewed = (projection["replyView"] as? [String: Any])?["taskId"] as? String
        var selected = Array(tasks.prefix(max(1, taskLimit)))
        var pinned = Set<String>()
        for task in [tasks.first(where: { $0["id"] as? String == viewed }), Self.overview(tasks)].compactMap({ $0 }) {
            guard let id = task["id"] as? String else { continue }
            if !selected.contains(where: { $0["id"] as? String == id }),
               let index = selected.indices.reversed().first(where: { !pinned.contains(selected[$0]["id"] as? String ?? "") }) { selected[index] = task }
            pinned.insert(id)
        }
        let ids = Set(selected.compactMap { $0["id"] as? String }), projects = Set(selected.compactMap { $0["projectId"] as? String })
        value["tasks"] = selected; value["overviewTask"] = Self.overview(selected)
        value["projects"] = (projection["projects"] as? [[String: Any]] ?? []).filter { projects.contains($0["id"] as? String ?? "") }
        for key in ["repliesReady", "repliesRead", "repliesUnread", "receipts", "responses"] { if let map = projection[key] as? [String: Any] { value[key] = map.filter { ids.contains($0.key) } } }
        return value
    }
    func start() {
        guard timer == nil else { return }
        let timer = DispatchSource.makeTimerSource(queue: queue); timer.schedule(deadline: .now(), repeating: 4)
        timer.setEventHandler { [weak self] in self?.refresh() }; self.timer = timer; timer.resume()
    }
    func stop() { timer?.cancel(); timer = nil; rpc?.stop(); desktop?.reset() }
    deinit { timer?.cancel() }
    func refresh() {
        guard enabled() else { rpc?.stop(); desktop?.reset(); stateLock.lock(); projection = [:]; note = "尚未启用"; stateLock.unlock(); return }
        guard gate.try() else { return }; defer { gate.unlock() }
        do {
            let result = try call("thread/list", ["limit": 40, "archived": false, "sortKey": "updated_at", "sortDirection": "desc"])
            guard let threads = result["data"] as? [[String: Any]] else { throw MacTab5CodexFailure.protocolError }
            catalog = threads.compactMap { root -> [String: Any]? in
                guard let id = Self.id(root["id"]), let cwd = root["cwd"] as? String else { return nil }
                let folder = URL(fileURLWithPath: cwd).lastPathComponent
                let title = (root["name"] as? String).flatMap { $0.isEmpty ? nil : $0 } ?? "Codex 会话"
                return ["id": id, "title": String(title.prefix(80)), "folder": String(folder.prefix(48)), "projectId": String(MacTab5Protocol.sha256(Data(cwd.utf8)).prefix(16)), "updatedAt": root["updatedAt"] as? Int64 ?? 0, "state": rpc == nil && mutable() ? Self.taskState(root["status"] as? [String: Any] ?? [:]) : "unknown"]
            }
            if let desktop {
                // Bound optional owner probes; never open windows during polling.
                for index in catalog.indices where index < 3 || catalog[index]["id"] as? String == viewedTask {
                    if let task = catalog[index]["id"] as? String, let live = try? desktop.read(task) {
                        catalog[index]["state"] = live.taskState
                    }
                }
            }
            catalogUpdated = clock()
            stateLock.lock()
            projection["tasks"] = catalog
            var projects: [[String: Any]] = []; var seen = Set<String>()
            for task in catalog { let id = task["projectId"] as! String; if seen.insert(id).inserted { projects.append(["id": id, "name": task["folder"]!]) } }
            projection["projects"] = projects; projection["overviewTask"] = Self.overview(catalog)
            note = mutable() ? "已连接本机 Codex 桌面 · \(catalog.count) 个近期会话；\(sendEnabled() ? "操作已启用" : "只读使用，操作未启用")。" : "只读已连接本机 Codex · \(catalog.count) 个近期会话；实时状态待确认，发送不可用。临时进程随桥接关闭。"; stateLock.unlock()
            if !viewedTask.isEmpty, catalog.contains(where: { $0["id"] as? String == viewedTask }) { _ = try page(task: viewedTask, root: viewedPage, device: viewedDevice) }
        } catch { stateLock.lock(); note = Self.connectionFailure(error); projection["control"] = NSNull(); stateLock.unlock() }
    }
    // App Server status names differ from the existing TAB5 v1 task states.
    static func taskState(_ status: [String: Any]) -> String {
        switch status["type"] as? String {
        case "idle": return "idle"
        case "active":
            guard let flags = status["activeFlags"] as? [String] else { return "unknown" }
            if flags.contains("waitingOnApproval") || flags.contains("waitingOnUserInput") { return "waiting" }
            return flags.isEmpty ? "working" : "unknown"
        default: return "unknown"
        }
    }
    private static func overview(_ tasks: [[String: Any]]) -> [String: Any]? {
        tasks.max { left, right in
            let a = left["state"] as? String == "working", b = right["state"] as? String == "working"
            if a != b { return !a }
            return (left["updatedAt"] as? Int64 ?? 0) < (right["updatedAt"] as? Int64 ?? 0)
        }
    }
    static func connectionFailure(_ error: Error) -> String {
        let reason: String
        switch error as? MacTab5CodexFailure {
        case .cliUnavailable: reason = "未找到可执行的 Codex CLI，请选择已安装的 CLI。"
        case .proxyUnavailable: reason = "本机 App Server 代理在初始化前退出；桌面 App 已打开也不代表控制 socket 可用。"
        case .readOnlyUnavailable: reason = "临时只读连接在初始化前退出。"
        case .readOnlyConnection: reason = "当前连接只允许读取任务与公开回复。"
        case .timeout: reason = "本机 App Server 响应超时。"
        case .rejected: reason = "本机 App Server 拒绝请求，请检查版本兼容性。"
        case .protocolError: reason = "本机 App Server 返回了不兼容的数据。"
        default: reason = "本机 App Server 连接中断。"
        }
        return "Codex 未连接；" + reason + "保留最近数据，不安装后台服务。"
    }
    private func loadJournal() throws -> [String: Any] {
        if let journal { return journal }
        let value = try store.read("journal").map(MacTab5Protocol.object) ?? ["receipts": [String: Any](), "drafts": [String: Any]()]
        guard value["receipts"] is [String: Any], value["drafts"] is [String: Any] else { throw MacTab5Error.invalidPacket }
        journal = value; return value
    }
    private func saveJournal(_ value: [String: Any]) throws { try store.write("journal", MacTab5Protocol.json(value)); journal = value }
    static func id(_ value: Any?) -> String? { guard let text = value as? String, text.count == 36, UUID(uuidString: text) != nil else { return nil }; return text.lowercased() }
    func contains(_ task: String) -> Bool { gate.lock(); defer { gate.unlock() }; return enabled() && clock() - catalogUpdated <= 30 && catalog.contains { $0["id"] as? String == task } }

    func handle(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard enabled() else { return .error(403, "codex_not_enabled") }
        gate.lock(); defer { gate.unlock() }
        guard let task = Self.id(root["taskId"]), let op = root["op"] as? String,
              catalog.contains(where: { $0["id"] as? String == task }) else { return .error(404, "task_not_in_recent_catalog") }
        do {
            if op == "read" { return try page(task: task, root: root, device: device) }
            if ["send", "steer", "interrupt"].contains(op), !mutable() { return .error(403, "codex_read_only_connection") }
            if op == "steer", desktop != nil, !MacCodexDesktopIPC.supportsExpectedTurnSteer {
                return .error(409, "desktop_steer_requires_expected_turn_support")
            }
            guard let id = Self.id(root["requestId"]) else { return .error(400, "invalid_request") }
            if op == "read-ack" {
                guard let turn = Self.id(root["turnId"]), let receipt = readReceipts[id], receipt.0 == device, receipt.1 == task, receipt.2 == turn,
                      receipt.3 > ProcessInfo.processInfo.systemUptime, ready[task] == turn else { return .error(409, "read_receipt_expired") }
                let latest = (try readThread(task)["turns"] as? [[String: Any]])?.last
                guard latest?["id"] as? String == turn, ["completed", "failed", "interrupted"].contains(latest?["status"] as? String ?? "") else { return .error(409, "read_turn_changed") }
                read[task] = turn; updateReadState(); return .init(200, ["accepted": true])
            }
            var data = try loadJournal(), receipts = data["receipts"] as! [String: Any], drafts = data["drafts"] as! [String: Any]
            let binding = device + "|" + task
            if op == "receipt" {
                guard let receipt = receipts[id] as? [String: Any], receipt["binding"] as? String == binding else { return .error(404, "receipt_not_found") }
                return receiptResult(receipt)
            }
            if op == "draft-load" || op == "draft-save" {
                if op == "draft-save" {
                    guard let text = root["message"] as? String, text.utf8.count <= 2000 else { return .error(400, "invalid_draft") }
                    let images = root["images"] as? [String] ?? []; _ = try photos.inputs(images, device: device, task: task)
                    drafts[binding] = ["text": text, "images": images]; data["drafts"] = drafts; try saveJournal(data)
                } else {
                    let draft = drafts[binding] as? [String: Any] ?? ["text": "", "images": [String]()]
                    let pending = receipts.first { entry in guard let value = entry.value as? [String: Any] else { return false }; return value["binding"] as? String == binding && value["status"] as? Int == 504 }
                    var view = draft; view["taskId"] = task; view["requestId"] = id; view["pendingRequest"] = pending?.key ?? ""
                    if let pending = pending?.value as? [String: Any] { view["operation"] = pending["operation"]; view["expectedTurn"] = pending["expectedTurn"] }
                    stateLock.lock(); projection["draftView"] = view; stateLock.unlock()
                }
                return .init(200, ["status": "saved", "taskId": task, "requestId": id])
            }
            guard sendEnabled(), ["send", "steer", "interrupt"].contains(op) else { return .error(403, "codex_send_not_enabled") }
            let text = root["message"] as? String ?? "", expected = root["expectedTurn"] as? String ?? ""
            let images = root["images"] as? [String] ?? []
            guard text.utf8.count <= 2000, op == "interrupt" || !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty || !images.isEmpty,
                  op == "send" || Self.id(expected) != nil else { return .error(400, "invalid_request") }
            let fingerprint = MacTab5Protocol.sha256(try MacTab5Protocol.json(["binding": binding, "op": op, "text": text, "expected": expected, "images": images]))
            if let receipt = receipts[id] as? [String: Any] { return receipt["fingerprint"] as? String == fingerprint ? receiptResult(receipt) : .error(409, "request_id_conflict") }
            guard clock() - catalogUpdated <= 30 else { return .error(503, "codex_catalog_stale") }
            guard receipts.count < 4096 else { return .error(429, "receipt_store_full") }
            var inputs = try photos.inputs(images, device: device, task: task)
            if !text.isEmpty { inputs.insert(["type": "text", "text": text], at: 0) }
            let thread = try desktop.map { try $0.read(task).thread } ?? readThread(task)
            let turns = thread["turns"] as? [[String: Any]] ?? [], last = turns.last ?? [:]
            let flags = (thread["status"] as? [String: Any])?["activeFlags"] as? [String] ?? []
            if op != "interrupt", flags.contains("waitingOnApproval") || flags.contains("waitingOnUserInput") { return .error(409, "task_waiting_for_input") }
            let running = last["status"] as? String == "inProgress"
            guard op == "send" ? (!running && Self.threadIdle(thread)) : (running && last["id"] as? String == expected) else { return .error(409, op == "send" ? "task_busy_or_state_unknown" : "turn_changed") }
            // Persist the uncertain receipt BEFORE any side effect, including resume.
            var receipt: [String: Any] = ["binding": binding, "fingerprint": fingerprint, "operation": op, "expectedTurn": expected, "status": 504, "body": ["error": "delivery_unknown"]]
            receipts[id] = receipt; data["receipts"] = receipts
            if op != "interrupt" { drafts[binding] = ["text": text, "images": images]; data["drafts"] = drafts }
            try saveJournal(data)
            do {
                let result: [String: Any]
                if let desktop {
                    result = try desktop.execute(task: task, operation: op, expected: expected, request: id, inputs: inputs,
                                                 authorized: { self.enabled() && self.sendEnabled() })
                } else if op == "send" {
                    _ = try call("thread/resume", ["threadId": task])
                    result = try call("turn/start", ["threadId": task, "input": inputs])
                } else if op == "steer" { result = try call("turn/steer", ["threadId": task, "expectedTurnId": expected, "input": inputs]) }
                else { result = try call("turn/interrupt", ["threadId": task, "turnId": expected]) }
                let turn = (result["turn"] as? [String: Any])?["id"] as? String ?? result["turnId"] as? String ?? (op == "interrupt" ? expected : "")
                guard Self.id(turn) != nil else { return .error(504, "delivery_unknown") }
                receipt["status"] = 202; receipt["body"] = ["status": "submitted", "taskId": task, "turnId": turn, "requestId": id]
                receipts[id] = receipt; data["receipts"] = receipts
                if op != "interrupt" { drafts[binding] = ["text": "", "images": [String]()]; data["drafts"] = drafts }
                try saveJournal(data); return receiptResult(receipt)
            } catch MacCodexDesktopError.rejected(let reason) {
                receipt["status"] = 409; receipt["body"] = ["error": reason]
                receipts[id] = receipt; data["receipts"] = receipts; try saveJournal(data)
                return receiptResult(receipt)
            } catch { return .error(504, "delivery_unknown") }
        } catch { return .error(503, "codex_or_private_storage_unavailable") }
    }
    private func receiptResult(_ root: [String: Any]) -> MacTab5RPCResult { .init(root["status"] as? Int ?? 504, root["body"] as? [String: Any] ?? ["error": "delivery_unknown"]) }
    private func readThread(_ task: String, selected: String = "", offset: Int = 0) throws -> [String: Any] {
        // The public child remains read-only even when desktop control is ready.
        let readOnly = rpc != nil || !mutable()
        let result = try call("thread/read", ["threadId": task, "includeTurns": !readOnly])
        guard var thread = result["thread"] as? [String: Any], Self.id(thread["id"]) == task else { throw MacTab5CodexFailure.protocolError }
        guard readOnly else { return thread }
        var turns: [[String: Any]] = [], cursor: String?, seen = Set<String>(), needsOlder = false
        for _ in 0..<8 {
            var params: [String: Any] = ["threadId": task, "limit": 32, "sortDirection": "desc", "itemsView": "summary"]
            if let cursor { params["cursor"] = cursor }
            let page = try call("thread/turns/list", params)
            guard let entries = page["data"] as? [[String: Any]], entries.count <= 32 else { throw MacTab5CodexFailure.protocolError }
            for entry in entries {
                guard let id = Self.id(entry["id"]), !turns.contains(where: { $0["id"] as? String == id }) else { continue }
                // Do not retain reasoning, user messages or tool output between
                // pages. Only public assistant text enters the device projection.
                let items = (entry["items"] as? [[String: Any]] ?? []).filter { $0["type"] as? String == "agentMessage" }
                turns.append(["id": id, "status": entry["status"] ?? "unknown", "startedAt": entry["startedAt"] ?? 0, "items": items])
            }
            cursor = page["nextCursor"] as? String
            let index = turns.firstIndex { $0["id"] as? String == selected }
            needsOlder = !selected.isEmpty && (index == nil || (offset < 0 && index == turns.count - 1))
            guard needsOlder, let next = cursor else { break }
            guard next.utf8.count <= 4096, seen.insert(next).inserted else { throw MacTab5CodexFailure.protocolError }
        }
        thread["turns"] = Array(turns.reversed())
        thread["hasOlderTurns"] = cursor != nil
        thread["historyLimited"] = needsOlder && cursor != nil
        return thread
    }
    private static func threadIdle(_ thread: [String: Any]) -> Bool { (thread["status"] as? [String: Any])?["type"] as? String == "idle" }
    private func updateReadState() {
        stateLock.lock(); projection["repliesReady"] = ready; projection["repliesRead"] = read
        projection["repliesUnread"] = ready.filter { read[$0.key] != $0.value }; stateLock.unlock()
    }
    private func page(task: String, root: [String: Any], device: String) throws -> MacTab5RPCResult {
        let selected = root["turnId"] as? String ?? "", offset = Int(MacTab5Protocol.integer(root["turnOffset"]) ?? 0)
        guard (-1...1).contains(offset), selected.isEmpty || Self.id(selected) != nil else { return .error(400, "invalid_turn") }
        let thread = try readThread(task, selected: selected, offset: offset), turns = thread["turns"] as? [[String: Any]] ?? []
        var index = turns.count - 1
        if !selected.isEmpty { guard let found = turns.firstIndex(where: { $0["id"] as? String == selected }) else { return .error(thread["historyLimited"] as? Bool == true ? 413 : 404, "turn_not_in_available_history") }; index = found + offset }
        guard turns.indices.contains(index), let turnID = Self.id(turns[index]["id"]) else { return .error(thread["historyLimited"] as? Bool == true ? 413 : 404, "reply_unavailable") }
        let turn = turns[index], recordedState = turn["status"] as? String ?? "unknown"
        let live = desktop.flatMap { try? $0.read(task) }
        let state = live?.turn == turnID ? live!.status :
            (desktop != nil || !mutable()) && recordedState == "inProgress" ? "unknown" : recordedState
        // Public assistant messages only: no reasoning, tool output or raw rollouts.
        let items = turn["items"] as? [[String: Any]] ?? []
        let text = items.filter { $0["type"] as? String == "agentMessage" }.compactMap { $0["text"] as? String }.joined(separator: "\n\n")
        guard text.utf8.count <= 1024 * 1024 else { return .error(413, "reply_too_large") }
        let pages = Self.pages(text), requested = Int(MacTab5Protocol.integer(root["page"]) ?? 0)
        guard requested >= -1, requested < pages.count else { return .error(400, "invalid_page") }
        let page = max(0, requested), history = index != turns.count - 1
        var receipt = ""
        if !history, !text.isEmpty, ["completed", "failed", "interrupted"].contains(state) {
            ready[task] = turnID; let now = ProcessInfo.processInfo.systemUptime
            readReceipts = readReceipts.filter { $0.value.3 > now }
            if readReceipts.count < 80 { receipt = UUID().uuidString.lowercased(); readReceipts[receipt] = (device, task, turnID, now + 300) }
        }
        let view: [String: Any] = ["taskId": task, "turnId": turnID, "page": page, "pageCount": pages.count, "hasText": !text.isEmpty, "readReceipt": receipt, "history": history, "hasPrevious": index > 0 || thread["hasOlderTurns"] as? Bool == true, "hasNext": index + 1 < turns.count, "previousTurnId": index > 0 ? turns[index - 1]["id"] as? String ?? "" : "", "nextTurnId": index + 1 < turns.count ? turns[index + 1]["id"] as? String ?? "" : "", "state": state, "startedAt": turn["startedAt"] as? Int64 ?? 0]
        let controlThread = live?.thread ?? thread
        let current = (controlThread["turns"] as? [[String: Any]])?.last ?? [:]
        let canControl = mutable() && (desktop == nil || live != nil)
        let running = canControl && (live?.running ?? (current["status"] as? String == "inProgress"))
        let flags = (controlThread["status"] as? [String: Any])?["activeFlags"] as? [String] ?? []
        stateLock.lock(); projection["replyView"] = view; projection["responses"] = [task: pages[page]]
        projection["receipts"] = [task: state == "completed" ? "已完成" : state == "inProgress" ? "运行中" : state == "interrupted" ? "已中断" : state == "failed" ? "失败" : "状态未知"]
        projection["control"] = ["taskId": task, "turnId": current["id"] as? String ?? "", "running": running,
                                 "canSend": canControl && sendEnabled() && (live?.canSend ?? (!running && Self.threadIdle(controlThread))),
                                 "canSteer": canControl && sendEnabled() && running && flags.isEmpty && (desktop == nil || MacCodexDesktopIPC.supportsExpectedTurnSteer),
                                 "canInterrupt": canControl && sendEnabled() && running, "waiting": canControl && !flags.isEmpty]
        stateLock.unlock(); updateReadState()
        viewedTask = task; viewedDevice = device
        viewedPage = !selected.isEmpty || page > 0 ? ["turnId": turnID, "page": page] : [:]
        return .init(200, ["rpcReplyVersion": 1, "status": "loaded", "taskId": task, "turnId": turnID, "replyView": view, "text": pages[page]])
    }
    static func pages(_ text: String) -> [String] {
        var result = [String](), current = ""
        for character in text { let part = String(character); if current.utf8.count + part.utf8.count > 3500, !current.isEmpty { result.append(current); current = "" }; current += part }
        result.append(current); return result
    }
}
