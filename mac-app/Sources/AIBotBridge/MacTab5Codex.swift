import Foundation

final class MacTab5Codex {
    static let enabledKey = "tab5_codex_replies_enabled"
    static let sendKey = "tab5_codex_send_enabled"
    typealias Call = (String, [String: Any]) throws -> [String: Any]
    typealias CatalogNames = (Set<String>) -> [String: String]?
    private let gate = NSLock(), stateLock = NSLock(), journalGate = NSLock()
    private let queue = DispatchQueue(label: "AI-bot.tab5-codex")
    private let deviceReads = MacTab5ReadAhead()
    private let desktopReads: MacCodexReadState
    private let call: Call, enabled: () -> Bool, sendEnabled: () -> Bool, mutable: () -> Bool
    private let mutationAllowed: () -> Bool
    private let clock: () -> TimeInterval
    private let lookupNames: CatalogNames
    private var cachedNames: [String: String] = [:]
    private var listSortKey = "recency_at"
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
    private var sendDiagnostic = ""
    // Guarded by gate. Only the latest public assistant items/history hint are
    // retained; controls still require a fresh owner snapshot on every read.
    private var latestPublic: (task: String, turn: String, items: [[String: Any]], hasOlder: Bool)?
    private let rpc: MacTab5CodexRPC?
    private let desktop: MacCodexDesktopIPC?
    private var liveControl: MacTab5LiveControl?
    private let taskPins: MacTaskPins?

    init(store: MacTab5PrivateStore = MacTab5PrivateStore(), enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, sendEnabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: sendKey) }, clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, canMutate: @escaping () -> Bool = { true }, desktop: MacCodexDesktopIPC? = nil, controlMonitor: MacTab5LiveControl? = nil, catalogNames: CatalogNames? = nil, call: Call? = nil, taskPins: MacTaskPins? = nil, readState: MacCodexReadState? = nil) {
        self.desktopReads = readState ?? MacCodexReadState(load: call == nil ? { MacCodexReadState.local() } : { nil }, clock: clock)
        self.taskPins = taskPins ?? (call == nil ? .shared : nil)
        self.store = store; photos = MacTab5Photos(store: store); self.enabled = enabled; self.sendEnabled = sendEnabled; self.clock = clock
        mutationAllowed = canMutate
        lookupNames = catalogNames ?? (call == nil ? { MacCodexCatalogNames.read($0) } : { _ in nil })
        if let call { self.call = call; rpc = nil; self.desktop = desktop; mutable = { canMutate() && (desktop?.available ?? true) } }
        else {
            let rpc = MacTab5CodexRPC(), desktop = desktop ?? MacCodexDesktopIPC()
            self.rpc = rpc; self.call = rpc.call; self.desktop = desktop; mutable = { canMutate() && desktop.available }
        }
        liveControl = controlMonitor ?? self.desktop.map {
            MacTab5LiveControl(enabled: enabled, mutable: self.mutable, sendEnabled: sendEnabled, clock: clock, read: $0.read)
        }
    }
    var isEnabled: Bool { enabled() }
    func taskCatalog() -> [[String: Any]] { stateLock.lock(); defer { stateLock.unlock() }; return catalog }
    var canMutate: Bool { enabled() && mutable() }
    // Availability is a short-lived display hint, not an authorization decision.
    // Refresh it on an explicit submission before the caller clears the composer.
    func prepareMutation(task: String) -> Bool {
        guard enabled(), sendEnabled(), mutationAllowed(), contains(task) else { return false }
        if let desktop, !desktop.available {
            guard (try? desktop.read(task)) != nil else { return false }
        }
        return enabled() && sendEnabled() && mutable()
    }
    func desktopSubmissionReceipt(task: String, expected: String) -> MacCodexSubmissionReceipt? {
        guard enabled(), sendEnabled(), mutationAllowed(), contains(task), let desktop else { return nil }
        return try? desktop.submissionReceipt(task, expected: expected)
    }
    var summary: String {
        stateLock.lock(); let text = note + (sendDiagnostic.isEmpty ? "" : "\n最近发送预检：" + sendDiagnostic); stateLock.unlock()
        let detail = enabled() ? liveControl?.summary ?? "" : ""
        return text + (detail.isEmpty ? "" : "\n" + detail)
    }
    func snapshot(taskLimit: Int = 64) -> [String: Any]? {
        guard enabled() else { return nil }
        stateLock.lock(); defer { stateLock.unlock() }
        var value = projection
        if let liveControl, liveControl.isManaged {
            let control = clock() - catalogUpdated <= 30 ? liveControl.snapshot() : nil
            let known = (projection["tasks"] as? [[String: Any]] ?? []).contains { $0["id"] as? String == control?["taskId"] as? String }
            if let control, known { value["control"] = control } else { value["control"] = NSNull() }
        }
        let tasks = projection["tasks"] as? [[String: Any]] ?? [], viewed = (projection["replyView"] as? [String: Any])?["taskId"] as? String
        let savedPins = Set((try? taskPins?.read()) ?? [])
        let ordered = tasks.filter { savedPins.contains($0["id"] as? String ?? "") } + tasks.filter { !savedPins.contains($0["id"] as? String ?? "") }
        var selected = Array(ordered.prefix(max(1, taskLimit)))
        var pinned = Set<String>()
        for task in [tasks.first(where: { $0["id"] as? String == viewed }), Self.overview(tasks)].compactMap({ $0 }) {
            guard let id = task["id"] as? String else { continue }
            if !selected.contains(where: { $0["id"] as? String == id }),
               let index = selected.indices.reversed().first(where: { !pinned.contains(selected[$0]["id"] as? String ?? "") }) { selected[index] = task }
            pinned.insert(id)
        }
        // Existing TAB5 firmware exposes eight task rows per picker entry.
        // Split larger folders into explicit pages instead of silently hiding
        // their ninth and older tasks. Task IDs and operation bindings stay intact.
        var projects: [[String: Any]] = [], counts: [String: Int] = [:], positions: [String: Int] = [:]
        for task in selected { if let id = task["projectId"] as? String { counts[id, default: 0] += 1 } }
        var included = Set<String>()
        selected = selected.compactMap { original in
            guard let id = original["projectId"] as? String else { return nil }
            let page = positions[id, default: 0] / 8; positions[id, default: 0] += 1
            let total = ((counts[id] ?? 1) + 7) / 8
            let pageID = page == 0 ? id : String(MacTab5Protocol.sha256(Data("\(id):page:\(page)".utf8)).prefix(16))
            if !included.contains(pageID) {
                included.insert(pageID)
                let name = original["folder"] as? String ?? "Codex"
                projects.append(["id": pageID, "name": total > 1 ? "\(name) · \(page + 1)/\(total)" : name])
            }
            var task = original; task["projectId"] = pageID
            task["pinned"] = savedPins.contains(task["id"] as? String ?? "")
            return task
        }
        if projects.count > 24 {
            let pinnedProjects = Set(selected.filter { pinned.contains($0["id"] as? String ?? "") }.compactMap { $0["projectId"] as? String })
            var retained = Array(projects.prefix(24))
            for project in projects where pinnedProjects.contains(project["id"] as? String ?? "") {
                let id = project["id"] as? String ?? ""
                if !retained.contains(where: { $0["id"] as? String == id }),
                   let index = retained.indices.reversed().first(where: { !pinnedProjects.contains(retained[$0]["id"] as? String ?? "") }) {
                    retained[index] = project
                }
            }
            projects = retained
            let allowed = Set(projects.compactMap { $0["id"] as? String })
            selected = selected.filter { allowed.contains($0["projectId"] as? String ?? "") }
        }
        let ids = Set(selected.compactMap { $0["id"] as? String })
        value["tasks"] = selected; value["overviewTask"] = Self.overview(selected)
        value["projects"] = projects
        // Exact read acknowledgements must also clear notices whose tasks were
        // evicted from a smaller transport catalog. The receiver matches turns.
        if let receipts = projection["repliesRead"] as? [String: String] {
            value["repliesRead"] = Dictionary(uniqueKeysWithValues: receipts.sorted { $0.key < $1.key }.prefix(80).map { ($0.key, $0.value) })
        }
        for key in ["repliesReady", "repliesUnread", "receipts", "responses"] { if let map = projection[key] as? [String: Any] { value[key] = map.filter { ids.contains($0.key) } } }
        return value
    }
    func start() {
        guard timer == nil else { return }
        let timer = DispatchSource.makeTimerSource(queue: queue); timer.schedule(deadline: .now(), repeating: 4)
        timer.setEventHandler { [weak self] in self?.refresh() }; self.timer = timer; timer.resume()
        liveControl?.start()
    }
    func stop() {
        timer?.cancel(); timer = nil; liveControl?.stop(); rpc?.stop(); desktop?.reset()
        stateLock.lock(); projection["control"] = NSNull(); stateLock.unlock()
    }
    deinit { timer?.cancel() }
    func refresh() {
        guard enabled() else { rpc?.stop(); desktop?.reset(); stateLock.lock(); projection = [:]; note = "尚未启用"; stateLock.unlock(); return }
        guard gate.try() else { return }; defer { updateReadState(); gate.unlock() }
        do {
            // The API's omitted-source default excludes desktop-created appServer
            // threads. Request user-facing sources explicitly, across providers.
            var threads: [[String: Any]] = [], cursor: String?, seenCursors = Set<String>(), seenTasks = Set<String>()
            for _ in 0..<8 {
                var params: [String: Any] = ["limit": 64, "archived": false, "sortKey": listSortKey, "sortDirection": "desc",
                                            "sourceKinds": ["cli", "vscode", "appServer"], "modelProviders": [String]()]
                if let cursor { params["cursor"] = cursor }
                let result: [String: Any]
                do { result = try call("thread/list", params) }
                catch MacTab5CodexFailure.rejected where cursor == nil && listSortKey == "recency_at" {
                    // Older read-only servers can reject the newer sort key.
                    // Fall back once for this bridge instance, never on timeout.
                    listSortKey = "updated_at"; params["sortKey"] = listSortKey
                    result = try call("thread/list", params)
                }
                guard let entries = result["data"] as? [[String: Any]], entries.count <= 64 else { throw MacTab5CodexFailure.protocolError }
                for entry in entries {
                    if let id = Self.id(entry["id"]), seenTasks.insert(id).inserted { threads.append(entry) }
                }
                guard let next = result["nextCursor"] as? String, !next.isEmpty else { break }
                guard next.utf8.count <= 4096, seenCursors.insert(next).inserted else { throw MacTab5CodexFailure.protocolError }
                cursor = next
            }
            // OpenClaw also uses the local App Server and reports `vscode`.
            // Its private workspace identifies those integration sessions; a
            // normal Codex chat merely discussing OpenClaw remains eligible.
            threads.removeAll { Self.isOpenClawWorkspace($0["cwd"] as? String ?? "") }
            let unnamed = Set(threads.compactMap { root -> String? in
                let name = (root["name"] as? String)?.trimmingCharacters(in: .whitespacesAndNewlines) ?? ""
                return name.isEmpty ? Self.id(root["id"]) : nil
            })
            cachedNames = cachedNames.filter { unnamed.contains($0.key) }
            if !unnamed.isEmpty, let names = lookupNames(unnamed) {
                cachedNames = names.filter { unnamed.contains($0.key) && $0.value.utf8.count <= 8192 &&
                    !$0.value.contains("\0") && !$0.value.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
            }
            catalog = threads.compactMap { root -> [String: Any]? in
                guard let id = Self.id(root["id"]), let cwd = root["cwd"] as? String else { return nil }
                let folder = URL(fileURLWithPath: cwd).lastPathComponent
                let title = (root["name"] as? String).flatMap { $0.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty ? nil : $0 } ?? cachedNames[id] ?? "Codex 会话"
                // Match the desktop's recent-activity order. The device sorts
                // its quick-console rows again by the existing updatedAt field,
                // so carry the same effective recency there, not record mtime.
                let updated = [root["recencyAt"], root["updatedAt"]].compactMap(MacTab5Protocol.integer)
                    .first(where: { (0...253_402_300_799).contains($0) }) ?? 0
                return ["id": id, "title": String(title.prefix(80)), "folder": String(folder.prefix(48)), "projectId": String(MacTab5Protocol.sha256(Data(cwd.utf8)).prefix(16)), "updatedAt": updated, "state": rpc == nil && mutable() ? Self.taskState(root["status"] as? [String: Any] ?? [:]) : "unknown"]
            }
            if let cache = latestPublic, !catalog.contains(where: { $0["id"] as? String == cache.task }) { latestPublic = nil }
            // Enforce global newest-first order after merging all cursor pages,
            // rather than relying on each provider/page's order. Preserve ties.
            catalog = catalog.enumerated().sorted {
                let a = $0.element["updatedAt"] as? Int64 ?? 0, b = $1.element["updatedAt"] as? Int64 ?? 0
                return a == b ? $0.offset < $1.offset : a > b
            }.map(\.element)
            let names = Dictionary(grouping: catalog, by: { $0["title"] as? String ?? "" }).mapValues(\.count)
            let dates = DateFormatter(); dates.locale = Locale(identifier: "en_US_POSIX")
            dates.calendar = Calendar(identifier: .gregorian); dates.dateFormat = "yy-MM-dd HH:mm"
            for index in catalog.indices {
                guard let name = catalog[index]["title"] as? String, (names[name] ?? 0) > 1 else { continue }
                let time = catalog[index]["updatedAt"] as? Int64 ?? 0
                let prefix = (time > 0 ? dates.string(from: Date(timeIntervalSince1970: Double(time))) : "时间未知") + " · "
                catalog[index]["title"] = prefix + String(name.prefix(max(0, 80 - prefix.count)))
            }
            if let desktop {
                // Bound optional owner probes; never open windows during polling.
                for index in catalog.indices where index < 3 || catalog[index]["id"] as? String == viewedTask {
                    if let task = catalog[index]["id"] as? String, let live = try? desktop.read(task) {
                        catalog[index]["state"] = live.taskState
                        // Re-establish recent exact completed turns after bridge
                        // restart, so desktop reads can clear retained device badges.
                        if ["completed", "failed", "interrupted"].contains(live.status),
                           live.publicItems.contains(where: { !($0["text"] as? String ?? "").isEmpty }) {
                            ready[task] = live.turn
                        }
                    }
                }
            }
            stateLock.lock()
            catalogUpdated = clock()
            projection["tasks"] = catalog
            var projects: [[String: Any]] = []; var seen = Set<String>()
            for task in catalog { let id = task["projectId"] as! String; if seen.insert(id).inserted { projects.append(["id": id, "name": task["folder"]!]) } }
            projection["projects"] = projects; projection["overviewTask"] = Self.overview(catalog)
            note = mutable() ? "已连接本机 Codex 桌面 · \(catalog.count) 个近期会话；\(sendEnabled() ? "操作已启用" : "只读使用，操作未启用")。" : "只读已连接本机 Codex · \(catalog.count) 个近期会话；实时状态待确认，发送不可用。临时进程随桥接关闭。"; stateLock.unlock()
            if !viewedTask.isEmpty, catalog.contains(where: { $0["id"] as? String == viewedTask }) { _ = try page(task: viewedTask, root: viewedPage, device: viewedDevice) }
        } catch { stateLock.lock(); note = Self.connectionFailure(error); projection["control"] = NSNull(); stateLock.unlock() }
    }
    // App Server status names differ from the existing TAB5 v1 task states.
    static func isOpenClawWorkspace(_ cwd: String) -> Bool {
        guard cwd.hasPrefix("/") else { return false }
        return URL(fileURLWithPath: cwd).standardizedFileURL.pathComponents.contains { $0.lowercased() == ".openclaw" }
    }
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
    // All journal transactions, including send receipts, hold journalGate.
    // Background remote reads never take it. Lock order is gate -> journalGate
    // -> stateLock; local draft requests skip gate entirely.
    private func loadJournal() throws -> [String: Any] {
        if let journal { return journal }
        let value = try store.read("journal").map(MacTab5Protocol.object) ?? ["receipts": [String: Any](), "drafts": [String: Any]()]
        guard value["receipts"] is [String: Any], value["drafts"] is [String: Any] else { throw MacTab5Error.invalidPacket }
        journal = value; return value
    }
    private func saveJournal(_ value: [String: Any]) throws { try store.write("journal", MacTab5Protocol.json(value)); journal = value }
    // Native desktop submission consumes only the staged local mirror. Keep a
    // newer device draft or attachments written while the UI action completed.
    func clearSubmittedDesktopDraft(task: String, device: String, expected: String) -> Bool {
        journalGate.lock(); defer { journalGate.unlock() }
        do {
            var data = try loadJournal(), drafts = data["drafts"] as! [String: Any]
            let binding = device + "|" + task
            guard let draft = drafts[binding] as? [String: Any], draft["text"] as? String == expected,
                  let images = draft["images"] as? [String], images.isEmpty else { return true }
            drafts[binding] = ["text": "", "images": [String]()]
            data["drafts"] = drafts; try saveJournal(data); return true
        } catch { return false }
    }
    static func id(_ value: Any?) -> String? { guard let text = value as? String, text.count == 36, UUID(uuidString: text) != nil else { return nil }; return text.lowercased() }
    func contains(_ task: String) -> Bool {
        guard enabled() else { return false }
        stateLock.lock(); defer { stateLock.unlock() }
        return clock() - catalogUpdated <= 30 &&
            (projection["tasks"] as? [[String: Any]] ?? []).contains { $0["id"] as? String == task }
    }
    private func draft(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard let task = Self.id(root["taskId"]), let op = root["op"] as? String else { return .error(404, "task_not_in_recent_catalog") }
        stateLock.lock()
        let known = (projection["tasks"] as? [[String: Any]] ?? []).contains { $0["id"] as? String == task }
        let fresh = clock() - catalogUpdated <= 30
        stateLock.unlock()
        // A paired device can still hold a draft from a previous host/catalog.
        // Save it locally before OTA; this never adds a task to the catalog or
        // authorizes reading, desktop navigation or sending to that task.
        guard known || op == "draft-save" else { return .error(404, "task_not_in_recent_catalog") }
        guard let id = Self.id(root["requestId"]) else { return .error(400, "invalid_request") }
        if fresh, op == "draft-load" { liveControl?.select(task, issuedAt: MacTab5Protocol.integer(root["issuedAt"])) }
        journalGate.lock(); defer { journalGate.unlock() }
        guard enabled() else { return .error(403, "codex_not_enabled") }
        do {
            var data = try loadJournal(), drafts = data["drafts"] as! [String: Any]
            let receipts = data["receipts"] as! [String: Any], binding = device + "|" + task
            if op == "draft-save" {
                guard let text = root["message"] as? String, text.utf8.count <= 2000 else { return .error(400, "invalid_draft") }
                if !known, drafts[binding] == nil,
                   drafts.keys.filter({ $0.hasPrefix(device + "|") }).count >= 128 {
                    return .error(409, "draft_capacity_reached")
                }
                let images = root["images"] as? [String] ?? []; _ = try photos.inputs(images, device: device, task: task)
                drafts[binding] = ["text": text, "images": images]; data["drafts"] = drafts; try saveJournal(data)
            } else {
                let saved = drafts[binding] as? [String: Any] ?? ["text": "", "images": [String]()]
                let pending = receipts.first { entry in guard let value = entry.value as? [String: Any] else { return false }; return value["binding"] as? String == binding && value["status"] as? Int == 504 }
                var view = saved; view["taskId"] = task; view["requestId"] = id; view["pendingRequest"] = pending?.key ?? ""
                if let pending = pending?.value as? [String: Any] { view["operation"] = pending["operation"]; view["expectedTurn"] = pending["expectedTurn"] }
                stateLock.lock(); projection["draftView"] = view; stateLock.unlock()
            }
            return .init(200, ["status": "saved", "taskId": task, "requestId": id])
        } catch { return .error(503, "codex_or_private_storage_unavailable") }
    }

    func handleDevice(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard root["op"] as? String == "read" else { return handle(root, device: device) }
        guard enabled() else { return .error(403, "codex_not_enabled") }
        guard let task = Self.id(root["taskId"]) else { return .error(404, "task_not_in_recent_catalog") }
        stateLock.lock()
        let known = (projection["tasks"] as? [[String: Any]] ?? []).contains { $0["id"] as? String == task }
        stateLock.unlock()
        guard known else { return .error(404, "task_not_in_recent_catalog") }
        let fields: [String: Any] = ["device":device, "session":root["session"] as? String ?? "",
            "task":task, "page":root["page"] ?? 0, "turn":root["turnId"] ?? "", "offset":root["turnOffset"] ?? 0]
        guard let encoded = try? JSONSerialization.data(withJSONObject:fields,options:.sortedKeys) else {
            return .error(400, "invalid_read")
        }
        return deviceReads.response(key:MacTab5Protocol.sha256(encoded)) { [self] in handle(root, device:device) }
    }

    func handle(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard enabled() else { return .error(403, "codex_not_enabled") }
        if root["op"] as? String == "read", let task = Self.id(root["taskId"]), contains(task) {
            liveControl?.select(task, issuedAt: MacTab5Protocol.integer(root["issuedAt"]))
        }
        if ["draft-load", "draft-save"].contains(root["op"] as? String ?? "") { return draft(root, device: device) }
        if root["op"] as? String == "read-ack" {
            guard gate.try() else { return .error(503,"codex_catalog_busy") }
        } else { gate.lock() }
        defer { gate.unlock() }
        guard let task = Self.id(root["taskId"]), let op = root["op"] as? String,
              catalog.contains(where: { $0["id"] as? String == task }) else { return .error(404, "task_not_in_recent_catalog") }
        var preflightStage = "private_journal_read_unavailable"
        do {
            if op == "read" {
                if clock() - catalogUpdated <= 30 { liveControl?.select(task, issuedAt: MacTab5Protocol.integer(root["issuedAt"])) }
                return try page(task: task, root: root, device: device)
            }
            if ["send", "steer", "interrupt"].contains(op), !mutationAllowed() { return .error(403, "codex_read_only_connection") }
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
            journalGate.lock(); defer { journalGate.unlock() }
            var data = try loadJournal(), receipts = data["receipts"] as! [String: Any], drafts = data["drafts"] as! [String: Any]
            let binding = device + "|" + task
            if op == "receipt" {
                guard let receipt = receipts[id] as? [String: Any], receipt["binding"] as? String == binding else { return .error(404, "receipt_not_found") }
                return receiptResult(receipt)
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
            var inputs: [[String: Any]]
            do { inputs = try photos.inputs(images, device: device, task: task) }
            catch { return sendUnavailable("image_inputs_unavailable") }
            if !text.isEmpty { inputs.insert(["type": "text", "text": text], at: 0) }
            let thread: [String: Any]
            do { thread = try desktop.map { try $0.read(task).thread } ?? readThread(task) }
            catch MacCodexDesktopError.rejected(let reason) {
                return sendUnavailable(reason == "desktop_owner_unavailable" ? reason : "desktop_read_unavailable")
            } catch MacCodexDesktopError.timeout { return sendUnavailable("desktop_read_timeout") }
            catch MacCodexDesktopError.incompatible { return sendUnavailable("desktop_version_unsupported") }
            catch { return sendUnavailable("desktop_read_unavailable") }
            let turns = thread["turns"] as? [[String: Any]] ?? [], last = turns.last ?? [:]
            let flags = (thread["status"] as? [String: Any])?["activeFlags"] as? [String] ?? []
            if op != "interrupt", flags.contains("waitingOnApproval") || flags.contains("waitingOnUserInput") { return .error(409, "task_waiting_for_input") }
            let running = last["status"] as? String == "inProgress"
            guard op == "send" ? (!running && Self.threadIdle(thread)) : (running && last["id"] as? String == expected) else { return .error(409, op == "send" ? "task_busy_or_state_unknown" : "turn_changed") }
            // Persist the uncertain receipt BEFORE any side effect, including resume.
            var receipt: [String: Any] = ["binding": binding, "fingerprint": fingerprint, "operation": op, "expectedTurn": expected, "status": 504, "body": ["error": "delivery_unknown"]]
            receipts[id] = receipt; data["receipts"] = receipts
            if op != "interrupt" { drafts[binding] = ["text": text, "images": images]; data["drafts"] = drafts }
            preflightStage = "private_journal_write_unavailable"
            try saveJournal(data)
            do {
                let result: [String: Any]
                if let desktop {
                    result = try desktop.execute(task: task, operation: op, expected: expected, request: id, inputs: inputs,
                                                 authorized: { self.enabled() && self.sendEnabled() && self.mutationAllowed() })
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
                try saveJournal(data)
                stateLock.lock(); sendDiagnostic = "已提交"; stateLock.unlock()
                return receiptResult(receipt)
            } catch MacCodexDesktopError.rejected(let reason) {
                receipt["status"] = 409; receipt["body"] = ["error": reason]
                receipts[id] = receipt; data["receipts"] = receipts; try saveJournal(data)
                return receiptResult(receipt)
            } catch { return .error(504, "delivery_unknown") }
        } catch MacCodexPublicHistory.Failure.tooLarge { return .error(413, "reply_too_large") }
        catch { return op == "send" ? sendUnavailable(preflightStage) : .error(503, "codex_or_private_storage_unavailable") }
    }
    private func sendUnavailable(_ stage: String) -> MacTab5RPCResult {
        // Fixed local labels only; no IDs, inputs or exception descriptions.
        stateLock.lock(); sendDiagnostic = stage; stateLock.unlock()
        return .error(503, "codex_or_private_storage_unavailable")
    }
    private func receiptResult(_ root: [String: Any]) -> MacTab5RPCResult { .init(root["status"] as? Int ?? 504, root["body"] as? [String: Any] ?? ["error": "delivery_unknown"]) }
    private func readThread(_ task: String, selected: String = "", offset: Int = 0) throws -> [String: Any] {
        // The public child remains read-only even when desktop control is ready.
        let readOnly = rpc != nil || !mutable()
        let result = try call("thread/read", ["threadId": task, "includeTurns": !readOnly])
        guard var thread = result["thread"] as? [String: Any], Self.id(thread["id"]) == task else { throw MacTab5CodexFailure.protocolError }
        guard readOnly else { return thread }
        let history = try MacCodexPublicHistory.read(task: task, selected: selected, offset: offset,
                                                    call: call, clock: clock)
        thread["turns"] = history.turns
        thread["hasOlderTurns"] = history.hasOlder
        thread["historyLimited"] = history.limited
        return thread
    }
    private static func threadIdle(_ thread: [String: Any]) -> Bool { (thread["status"] as? [String: Any])?["type"] as? String == "idle" }
    static func publicItems(_ value: Any?, requirePhase: Bool = false) -> [[String: Any]] {
        (value as? [[String: Any]] ?? []).compactMap { item in
            guard item["type"] as? String == "agentMessage", let text = item["text"] as? String else { return nil }
            let phase = item["phase"] as? String
            guard phase == "commentary" || phase == "final_answer" || (phase == nil && !requirePhase) else { return nil }
            var result: [String: Any] = ["type": "agentMessage", "text": text]
            if let id = item["id"] as? String, id.utf8.count <= 256 { result["id"] = id }
            if let phase { result["phase"] = phase }
            return result
        }
    }
    private static func mergePublicItems(_ persisted: Any?, _ live: [[String: Any]]) -> [[String: Any]] {
        var result = publicItems(persisted)
        for item in live {
            let id = item["id"] as? String, text = item["text"] as? String ?? ""
            if let index = result.firstIndex(where: { old in
                (id != nil && old["id"] as? String == id) || old["text"] as? String == text
            }) {
                // A lagging snapshot must not shorten already persisted text.
                if !(result[index]["text"] as? String ?? "").hasPrefix(text) { result[index] = item }
            } else { result.append(item) }
        }
        return result
    }
    private func updateReadState() {
        let desktopRead = desktopReads.synchronize(ready)
        let combined = read.merging(desktopRead) { _, desktop in desktop }
        stateLock.lock(); projection["repliesReady"] = ready; projection["repliesRead"] = combined
        projection["repliesUnread"] = ready.filter { combined[$0.key] != $0.value }; stateLock.unlock()
    }
    private func page(task: String, root: [String: Any], device: String) throws -> MacTab5RPCResult {
        let selected = root["turnId"] as? String ?? "", offset = Int(MacTab5Protocol.integer(root["turnOffset"]) ?? 0)
        guard (-1...1).contains(offset), selected.isEmpty || Self.id(selected) != nil else { return .error(400, "invalid_turn") }
        let controlToken = liveControl?.capture(task: task)
        let live = desktop.flatMap { try? $0.read(task) }
        let thread: [String: Any]
        if let live, let cache = latestPublic, cache.task == task,
           offset == 0, selected.isEmpty || selected == live.turn {
            // Once public history establishes the older-turn boundary, ordinary
            // latest polling needs only the fresh owner. Re-reading persisted
            // history here occupied the shared device mailbox for 12–17s.
            var latest = live.thread
            latest["hasOlderTurns"] = cache.hasOlder || cache.turn != live.turn
            if cache.turn == live.turn {
                var turn = (latest["turns"] as! [[String: Any]])[0]
                turn["items"] = Self.mergePublicItems(cache.items, live.publicItems)
                latest["turns"] = [turn]
            }
            thread = latest
        } else { thread = try readThread(task, selected: selected, offset: offset) }
        if let controlToken, desktop != nil { liveControl?.accept(controlToken, state: live) }
        var turns = thread["turns"] as? [[String: Any]] ?? []
        if let live {
            if let index = turns.firstIndex(where: { $0["id"] as? String == live.turn }) {
                turns[index]["items"] = Self.mergePublicItems(turns[index]["items"], live.publicItems)
            } else if live.startedAt >= (turns.last.flatMap { MacTab5Protocol.integer($0["startedAt"]) } ?? 0) {
                // The owner's active turn can precede its persisted history.
                turns.append(["id": live.turn, "status": live.status, "startedAt": live.startedAt, "items": live.publicItems])
            }
        }
        if let latest = turns.last, let turn = Self.id(latest["id"]) {
            let items = Self.publicItems(latest["items"])
            if items.reduce(0, { $0 + (($1["text"] as? String)?.utf8.count ?? 0) }) <= 1024 * 1024 {
                latestPublic = (task, turn, items, turns.count > 1 || thread["hasOlderTurns"] as? Bool == true)
            }
        }
        var index = turns.count - 1
        if !selected.isEmpty { guard let found = turns.firstIndex(where: { $0["id"] as? String == selected }) else { return .error(thread["historyLimited"] as? Bool == true ? 413 : 404, "turn_not_in_available_history") }; index = found + offset }
        guard turns.indices.contains(index), let turnID = Self.id(turns[index]["id"]) else { return .error(thread["historyLimited"] as? Bool == true ? 413 : 404, "reply_unavailable") }
        let turn = turns[index], recordedState = turn["status"] as? String ?? "unknown"
        let state = live?.turn == turnID ? live!.status :
            (desktop != nil || !mutable()) && recordedState == "inProgress" ? "unknown" : recordedState
        // Public assistant messages only: no reasoning, tool output or raw rollouts.
        let text = Self.publicItems(turn["items"]).compactMap { $0["text"] as? String }.joined(separator: "\n\n")
        guard text.utf8.count <= 1024 * 1024 else { return .error(413, "reply_too_large") }
        // The existing device treats an empty response as still loading. Keep
        // presentation text separate from hasText/read receipts so a status
        // placeholder never becomes an assistant reply or unread notification.
        let displayText = text.isEmpty ? (state == "inProgress" ? "Codex 正在处理，等待新的动态…" :
            state == "interrupted" ? "任务已停止，本轮尚无公开文字。" : "本轮尚无公开文字。") : text
        let pages = Self.pages(displayText), requested = Int(MacTab5Protocol.integer(root["page"]) ?? 0)
        guard requested >= -1, requested < pages.count else { return .error(400, "invalid_page") }
        let page = requested == -1 ? pages.count - 1 : requested, history = index != turns.count - 1
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
        let statusText = state == "completed" ? "已完成" : state == "inProgress" ? "运行中" : state == "interrupted" ? "已中断" : state == "failed" ? "失败" : "状态未知"
        stateLock.lock(); projection["replyView"] = view; projection["responses"] = [task: pages[page]]
        projection["receipts"] = [task: statusText]
        projection["control"] = ["taskId": task, "turnId": current["id"] as? String ?? "", "running": running,
                                 "canSend": canControl && sendEnabled() && (live?.canSend ?? (!running && Self.threadIdle(controlThread))),
                                 "canSteer": canControl && sendEnabled() && running && flags.isEmpty && (desktop == nil || MacCodexDesktopIPC.supportsExpectedTurnSteer),
                                 "canInterrupt": canControl && sendEnabled() && running, "waiting": canControl && !flags.isEmpty]
        stateLock.unlock(); updateReadState()
        viewedTask = task; viewedDevice = device
        viewedPage = requested == -1 && selected.isEmpty ? ["page": -1] :
            !selected.isEmpty || page > 0 ? ["turnId": turnID, "page": page] : [:]
        return .init(200, ["rpcReplyVersion": 1, "status": "loaded", "taskId": task, "turnId": turnID, "replyView": view, "text": pages[page], "receipt": statusText])
    }
    static func pages(_ text: String) -> [String] {
        var result = [String](), current = ""
        for character in text { let part = String(character); if current.utf8.count + part.utf8.count > 3500, !current.isEmpty { result.append(current); current = "" }; current += part }
        result.append(current); return result
    }
}
