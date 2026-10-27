import AppKit
import ApplicationServices

struct MacTab5DesktopBackend {
    var open: (String) -> Bool
    var read: (String) -> String?
    // Return the verified raw value, including any renderer paragraph ending.
    // Submission binds to this exact observation rather than a normalized copy.
    var write: (String, String, String) -> String?
    var diagnostic: () -> MacTab5DesktopCheck = { .route }
    var preflight: () -> MacTab5DesktopCheck? = { nil }
    var navigation: ((String) -> Bool)? = nil
    var evidence: () -> String = { "" }
    var openOnly: ((String) -> Bool)? = nil
}

enum MacTab5DesktopCheck: String {
    case waiting = "等待设备发起直达"
    case permission = "辅助功能权限未生效，请检查当前候选的系统授权"
    case frontmost = "前台应用不是 Codex"
    case focus = "未取得前台输入焦点"
    case role = "当前焦点不在主输入框"
    case identifier = "主输入框标识尚不兼容"
    case identifierAndRoute = "未取得主输入框标识及精确会话地址"
    case structure = "输入框内容结构尚不兼容"
    case route = "未确认目标会话的地址或唯一完整标题"
    case navigationTitleMismatch = "前台主页面完整标题尚未匹配目标会话"
    case navigationShell = "未取得 Codex 主页面的可访问导航区域"
    case navigationAddress = "Codex 主页面未提供可验证的会话地址"
    case navigationSelectionWithoutAddress = "已找到当前会话按钮，但尚无可验证的目标会话地址"
    case navigationLimit = "Codex 导航检查达到边界，尚未确认目标会话"
    case value = "未取得可用的输入框文字"
    case readOnly = "主输入框未提供可写的辅助功能接口"
    case changed = "电脑草稿已变化，请保留识别预览"
    case tooLong = "合并后的草稿超过当前长度上限，请保留识别预览"
    case writeUnconfirmed = "主输入框写入未确认，请保留预览并检查电脑"
    case submitReceipt = "发送未执行：没有匹配当前设备和会话的回填记录"
    case submitUsed = "发送未执行：这份回填记录已经使用，请先检查电脑"
    case submitExpired = "发送未执行：回填记录已过期"
    case submitParagraph = "发送未执行：输入框末尾换行表示与回填快照不同"
    case submitConnection = "发送未执行：桌面任务连接当前不可写"
    case submitRejected = "任务连接拒绝了发送，请保留并检查电脑草稿"
    case submitUnknown = "任务发送结果未确认，请检查电脑，不要重复发送"
    case submitted = "任务连接已确认发送"
    case open = "系统未接受打开会话请求"
    case confirmed = "已确认目标任务和主输入框"
    case navigationConfirmed = "已确认目标会话；输入框操作将另行校验"
    case navigationOpened = "已请求打开指定会话，Codex 已在前台；显示名称重复，未精确读回目标，输入框操作另行检查"
}

final class MacTab5Desktop {
    static let enabledKey = "tab5_codex_desktop_enabled"
    private let lock = NSLock(), codex: MacTab5Codex
    private let enabled: () -> Bool, backend: MacTab5DesktopBackend
    private let statusLock = NSLock()
    private var status: MacTab5DesktopCheck = .waiting
    private var statusEvidence = ""
    // Opening a page is safe to repeat after an app restart. Keep its bounded
    // deduplication receipts in memory, without unlocking the private draft key.
    // Editing and submission still persist encrypted receipts before acting.
    private var navigationReceipts: [String: Any] = [:]
    var summary: String {
        guard enabled() else { return "尚未启用" }
        if let failure = backend.preflight() { return failure.rawValue }
        statusLock.lock(); let current = status, detail = statusEvidence; statusLock.unlock()
        return current.rawValue + (detail.isEmpty ? "" : "（" + detail + "）")
    }
    private func updateStatus(_ value: MacTab5DesktopCheck, navigation: Bool = false) {
        // Evidence belongs to the last navigation traversal, not a later
        // composer check (which may have failed after navigation succeeded).
        let detail = navigation ? backend.evidence() : ""
        statusLock.lock(); status = value; statusEvidence = detail; statusLock.unlock()
    }
    init(codex: MacTab5Codex, enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, backend: MacTab5DesktopBackend = MacTab5DesktopAX.backend) { self.codex = codex; self.enabled = enabled; self.backend = backend }
    func handle(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard enabled(), codex.isEnabled else { return .error(403, "desktop_control_not_enabled") }
        lock.lock(); defer { lock.unlock() }
        guard let id = MacTab5Codex.id(root["requestId"]), let op = root["op"] as? String, ["open-recent", "stage-draft", "clear-draft", "submit-draft"].contains(op) else { return .error(400, "invalid_request") }
        let tasks = codex.snapshot()?["tasks"] as? [[String: Any]] ?? []
        guard let task = MacTab5Codex.id(root["taskId"] ?? tasks.first?["id"]), let entry = tasks.first(where: { $0["id"] as? String == task }) else { return .error(404, "task_not_in_recent_catalog") }
        guard codex.contains(task) else { return .error(503, "codex_catalog_stale") }
        do {
            let binding = device + "|" + task, text = root["text"] as? String ?? "", draftID = root["draftRequestId"] as? String ?? ""
            let fingerprint = MacTab5Protocol.sha256(try MacTab5Protocol.json(["binding": binding, "op": op, "text": text, "draft": draftID]))
            if let record = navigationReceipts[id] as? [String: Any] { return record["fingerprint"] as? String == fingerprint ? .init(record["status"] as? Int ?? 504, record["body"] as? [String: Any] ?? [:]) : .error(409, "request_id_conflict") }
            var records = op == "open-recent" ? navigationReceipts : try codex.store.read("desktop-receipts").map(MacTab5Protocol.object) ?? [:]
            if let record = records[id] as? [String: Any] { return record["fingerprint"] as? String == fingerprint ? .init(record["status"] as? Int ?? 504, record["body"] as? [String: Any] ?? [:]) : .error(409, "request_id_conflict") }
            if op == "submit-draft", !codex.prepareMutation(task: task) { updateStatus(.submitConnection); return .error(403, "codex_read_only_connection") }
            func saveRecords() throws {
                if op == "open-recent" { navigationReceipts = records }
                else { try codex.store.write("desktop-receipts", MacTab5Protocol.json(records)) }
            }
            guard records.count < 2048, text.utf8.count <= 2000 else { return .error(429, "desktop_receipt_capacity") }
            var record: [String: Any] = ["binding": binding, "fingerprint": fingerprint, "status": 504, "body": ["error": "desktop_result_unknown"], "createdAt": Date().timeIntervalSince1970]
            records[id] = record; try saveRecords()
            var result: MacTab5RPCResult
            var body = entry; body.removeValue(forKey: "id"); body["taskId"] = task; body["requestId"] = id
            if op == "open-recent" {
                if let failure = backend.preflight() {
                    // Do not navigate the Mac when confirmation is known to be
                    // impossible: the device keeps its previous selection on 409.
                    updateStatus(failure); result = .error(409, "desktop_foreground_unconfirmed")
                } else {
                    let opened = backend.open(task)
                    // Navigation does not read or authorize the composer. Current
                    // desktop editors may omit its identity while the selected
                    // navigation link still exposes the exact foreground task.
                    var current: String?
                    func confirmed() -> Bool {
                        if let navigation = backend.navigation { return navigation(task) }
                        current = backend.read(task); return current != nil
                    }
                    let until = ProcessInfo.processInfo.systemUptime + 3
                    var navigated = opened && confirmed()
                    var activated = opened && !navigated && backend.openOnly?(task) == true
                    // Use the actual deadline. Six polls ended after roughly a
                    // second on fast AX calls, before asynchronous navigation
                    // finished, even though the advertised budget is 3 seconds.
                    for _ in 0..<20 where opened && !navigated && !activated && ProcessInfo.processInfo.systemUptime < until {
                        Thread.sleep(forTimeInterval: min(0.15, max(0, until - ProcessInfo.processInfo.systemUptime)))
                        guard ProcessInfo.processInfo.systemUptime < until else { break }
                        navigated = confirmed()
                        activated = !navigated && backend.openOnly?(task) == true
                    }
                    if navigated || activated {
                        updateStatus(activated ? .navigationOpened : current == nil ? .navigationConfirmed : .confirmed, navigation: true)
                        body["foreground"] = true
                        if let current { body["draftEmpty"] = current.isEmpty }
                        result = .init(200, body)
                    } else { updateStatus(opened ? backend.diagnostic() : .open, navigation: opened); result = .error(409, "desktop_foreground_unconfirmed") }
                }
            } else if op == "stage-draft" {
                if let current = backend.read(task), !text.isEmpty {
                    let empty = current.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                    let combined = empty ? text : current + "\n" + text
                    if combined.utf8.count <= 2000, let observed = backend.write(task, current, combined) {
                        let saved = codex.handle(["op": "draft-save", "taskId": task, "requestId": id, "message": observed], device: device)
                        if saved.status == 200 { updateStatus(.confirmed); record["stagedText"] = observed; record["stage"] = true; body["staged"] = true; body["appended"] = !empty; result = .init(200, body) }
                        else { result = saved }
                    } else { updateStatus(combined.utf8.count > 2000 ? .tooLong : backend.diagnostic()); result = .error(409, "composer_changed") }
                } else { updateStatus(backend.diagnostic()); result = .error(503, "composer_not_ready") }
            } else if op == "clear-draft" {
                if let current = backend.read(task), backend.write(task, current, "") != nil {
                    let saved = codex.handle(["op": "draft-save", "taskId": task, "requestId": id, "message": ""], device: device)
                    if saved.status == 200 { body["cleared"] = true; result = .init(200, body) } else { result = saved }
                } else { result = .error(409, "composer_changed") }
            } else {
                func submit() throws -> MacTab5RPCResult {
                    guard let draft = records[draftID.lowercased()] as? [String: Any], draft["binding"] as? String == binding,
                          let staged = draft["stagedText"] as? String, !staged.isEmpty else { updateStatus(.submitReceipt); return .error(409, "composer_changed") }
                    guard draft["stage"] as? Bool == true else { updateStatus(.submitUsed); return .error(409, "composer_changed") }
                    guard let created = draft["createdAt"] as? Double, Date().timeIntervalSince1970 - created <= 900 else { updateStatus(.submitExpired); return .error(409, "composer_changed") }
                    guard let current = backend.read(task) else { updateStatus(backend.diagnostic()); return .error(409, "composer_changed") }
                    guard current == staged else {
                        updateStatus(MacTab5DesktopAX.matchesReadback(current, expected: staged) ? .submitParagraph : .changed)
                        return .error(409, "composer_changed")
                    }
                    // Consume before touching the composer. A repeated click can
                    // only query this delivery receipt, never send another turn.
                    var consumed = draft; consumed["stage"] = false; records[draftID.lowercased()] = consumed
                    try codex.store.write("desktop-receipts", MacTab5Protocol.json(records))
                    if let cleared = backend.write(task, staged, "") {
                        let sent = codex.handle(["op": "send", "taskId": task, "requestId": id, "message": staged], device: device)
                        if sent.status == 202 { updateStatus(.submitted); body["submitted"] = true; return .init(200, body) }
                        _ = backend.write(task, cleared, staged)
                        updateStatus(sent.status == 504 ? .submitUnknown : .submitRejected); return sent
                    }
                    updateStatus(backend.diagnostic()); return .error(409, "composer_changed")
                }
                result = try submit()
            }
            record["status"] = result.status; record["body"] = result.body; records[id] = record
            try saveRecords(); return result
        } catch { return .error(504, "desktop_result_unknown") }
    }
}

enum MacTab5DesktopAX {
    private static let diagnosticLock = NSLock()
    private static var lastCheck: MacTab5DesktopCheck = .waiting
    private static var lastEvidence = ""
    private static func note(_ check: MacTab5DesktopCheck) { diagnosticLock.lock(); lastCheck = check; diagnosticLock.unlock() }
    private static func diagnostic() -> MacTab5DesktopCheck { diagnosticLock.lock(); defer { diagnosticLock.unlock() }; return lastCheck }
    private static func evidence() -> String { diagnosticLock.lock(); defer { diagnosticLock.unlock() }; return lastEvidence }
    static var backend: MacTab5DesktopBackend { .init(open: { task in MacTab5ComputerPlatform.main { guard let url = URL(string: "codex://threads/" + task) else { return false }; return NSWorkspace.shared.open(url) } }, read: read, write: write, diagnostic: diagnostic, preflight: { AXIsProcessTrusted() ? nil : .permission }, navigation: navigation, evidence: evidence, openOnly: openOnly) }
    private static func openOnly(_ task: String) -> Bool {
        // Windows allows an app-foreground acknowledgment for open-only when
        // titles are ambiguous. Do not call this proof of the exact document,
        // and never reuse it for reading, changing or submitting a composer.
        let ambiguous = MacCodexNavigationTitle.identity(task) == .duplicate ||
            MacCodexNavigationTitle.identity(task, compact: true) == .duplicate
        guard ambiguous, AXIsProcessTrusted(),
              let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }),
              app.bundleIdentifier == "com.openai.codex" else { return false }
        let root = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(root, 0.1)
        guard let window = value(root, "AXFocusedWindow"), CFGetTypeID(window) == AXUIElementGetTypeID() else { return false }
        let element = unsafeBitCast(window, to: AXUIElement.self)
        return (value(element, "AXSheets") as? [AXUIElement] ?? []).isEmpty
    }
    private static func value(_ element: AXUIElement, _ key: String) -> CFTypeRef? { var value: CFTypeRef?; return AXUIElementCopyAttributeValue(element, key as CFString, &value) == .success ? value : nil }
    static func identifies(_ url: String, task: String, resourceRoot: URL? = nil) -> Bool {
        guard let parsed = URLComponents(string: url), ["codex", "http", "https", "file", "app"].contains(parsed.scheme ?? "") else { return false }
        if parsed.scheme == "app" { guard parsed.host == "-", parsed.user == nil, parsed.password == nil, parsed.port == nil else { return false } }
        if parsed.scheme == "codex" { guard parsed.host == "threads" else { return false } }
        if parsed.scheme == "http" || parsed.scheme == "https" { guard ["localhost", "127.0.0.1", "::1"].contains(parsed.host ?? "") else { return false } }
        if parsed.scheme == "file" {
            guard let resourceRoot, resourceRoot.isFileURL, parsed.host == nil || parsed.host == "" || parsed.host == "localhost",
                  URL(fileURLWithPath: parsed.path).standardizedFileURL.path.hasPrefix(resourceRoot.standardizedFileURL.path + "/") else { return false }
        }
        // Current packaged Codex uses /local/:conversationId internally;
        // codex://threads/:id is its external deep link, not the live route.
        if parsed.scheme == "app" || parsed.scheme == "file" {
            let route = parsed.fragment ?? parsed.path
            let segments = route.split(separator: "/").map(String.init)
            if segments.count == 2, segments[0] == "local", segments[1].lowercased() == task.lowercased() { return true }
        }
        let path = (parsed.host == "threads" ? "/threads" : "") + parsed.path + (parsed.fragment.map { "/" + $0 } ?? "")
        let parts = path.split(separator: "/").map(String.init)
        return parts.indices.contains(parts.count - 2) && parts[parts.count - 2] == "threads" && parts.last?.lowercased() == task.lowercased()
    }
    // Only an explicitly current link identifies the selected task. A matching
    // unselected sidebar/history link or title must never confirm navigation.
    static func currentTaskLink(role: String, current: String?, address: String?, task: String, resourceRoot: URL?) -> Bool {
        role == "AXLink" && current == "page" && address.map { identifies($0, task: task, resourceRoot: resourceRoot) } == true
    }
    static func currentTaskRow(role: String, current: String?, addresses: [String], complete: Bool,
                               task: String, resourceRoot: URL?) -> Bool {
        // Some sidebar variants put aria-current on a group with links inside.
        // Button-only rows do not qualify. Every link must identify the same
        // exact task; a selected section containing other tasks is not proof.
        role == "AXGroup" && current == "page" && complete && !addresses.isEmpty &&
            addresses.allSatisfy { identifies($0, task: task, resourceRoot: resourceRoot) }
    }
    private static func currentRow(_ row: AXUIElement, task: String, resources: URL?, deadline: TimeInterval) -> Bool {
        var pending = (value(row, "AXChildren") as? [AXUIElement] ?? []).map { ($0, 1) }
        var addresses: [String] = [], visited = 0
        while !pending.isEmpty {
            guard visited < 64, ProcessInfo.processInfo.systemUptime < deadline else { return false }
            let (node, depth) = pending.removeLast(); visited += 1
            guard depth <= 6 else { return false }
            let role = value(node, "AXRole") as? String ?? ""
            guard !["AXWebArea", "AXTextArea", "AXTextField", "AXDialog", "AXSheet"].contains(role),
                  value(node, "AXARIACurrent") as? String != "page" else { return false }
            if role == "AXLink" {
                guard let url = address(node) else { return false }
                addresses.append(url)
            }
            pending.append(contentsOf: (value(node, "AXChildren") as? [AXUIElement] ?? []).map { ($0, depth + 1) })
        }
        return currentTaskRow(role: "AXGroup", current: "page", addresses: addresses, complete: true, task: task, resourceRoot: resources)
    }
    private static func address(_ node: AXUIElement, _ attribute: String = "AXURL") -> String? {
        let raw = value(node, attribute)
        return (raw as? URL)?.absoluteString ?? (raw as? String)
    }
    static func trustedShell(_ address: String?, resourceRoot: URL?) -> Bool {
        guard let address, let url = URLComponents(string: address) else { return false }
        // Installed Codex serves its packaged main renderer from app://-.
        // app://fs is a separate file/browser origin and must not qualify.
        if url.scheme == "app" { return url.host == "-" && url.user == nil && url.password == nil && url.port == nil }
        guard let resourceRoot, resourceRoot.isFileURL,
              url.scheme == "file", url.host == nil || url.host == "" || url.host == "localhost" else { return false }
        return URL(fileURLWithPath: url.path).standardizedFileURL.path.hasPrefix(resourceRoot.standardizedFileURL.path + "/")
    }
    private static func navigation(_ task: String) -> Bool {
        let started = ProcessInfo.processInfo.systemUptime
        var visited = 0, shells = 0, selected = 0, named = 0, rowMatched = false, titleMatched = false, unique = false
        var rowEvidence = ""
        defer {
            let detail = "节点 \(visited)；主区域 \(shells)；唯一标题 \(unique ? "有" : "无")；页面匹配 \(titleMatched ? "是" : "否")；当前行 \(selected)／名称 \(named)；行匹配 \(rowMatched ? "是" : "否")\(rowEvidence)；\(Int((ProcessInfo.processInfo.systemUptime-started)*1000)) ms"
            diagnosticLock.lock(); lastEvidence = detail; diagnosticLock.unlock()
        }
        guard AXIsProcessTrusted() else { note(.permission); return false }
        guard let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }), app.bundleIdentifier == "com.openai.codex" else { note(.frontmost); return false }
        let root = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(root, 0.1)
        guard let raw = value(root, "AXFocusedWindow"), CFGetTypeID(raw) == AXUIElementGetTypeID() else { note(.focus); return false }
        let window = unsafeBitCast(raw, to: AXUIElement.self)
        let resources = app.bundleURL?.appendingPathComponent("Contents/Resources")
        let uniqueTitle = MacCodexNavigationTitle.read(task)
        let compactTitle = MacCodexNavigationTitle.read(task, compact: true)
        let identities: [(name: String, compact: Bool)] = [(uniqueTitle, false), (compactTitle, true)].compactMap { name, compact in
            name.map { (name: $0, compact: compact) }
        }
        unique = !identities.isEmpty
        guard (value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { note(.route); return false }
        // The window document may identify the exact page without editor focus.
        if let document = address(window, "AXDocument"), identifies(document, task: task, resourceRoot: resources) { return true }
        var pending = MacNavigationQueue((window, false))
        var foundShell = false, foundTaskAddress = false, foundCurrentButton = false, checkedTitle = false
        let deadline = ProcessInfo.processInfo.systemUptime + 0.5
        while pending.hasPending, visited < 1200, ProcessInfo.processInfo.systemUptime < deadline,
              let (node, inheritedShell) = pending.next() {
            visited += 1
            let role = value(node, "AXRole") as? String ?? ""
            var shell = inheritedShell
            if role == "AXWebArea" {
                // An embedded browser must not impersonate the app's sidebar.
                shell = trustedShell(address(node), resourceRoot: resources)
                if !shell { continue }
                foundShell = true
                shells += 1
                if let page = address(node), identifies(page, task: task, resourceRoot: resources) { return true }
                if !inheritedShell {
                    checkedTitle = !identities.isEmpty
                    if let identity = identities.first(where: { MacCodexNavigationTitle.matches(role: role, trustedPrimary: true,
                        pageTitle: value(node, "AXTitle") as? String, uniqueTitle: $0.name) }),
                       MacCodexNavigationTitle.read(task, compact: identity.compact) == identity.name,
                       MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
                       let focused = value(root, "AXFocusedWindow"), CFEqual(focused, window),
                       value(node, "AXTitle") as? String == identity.name {
                        titleMatched = true; return true
                    }
                }
            }
            if shell && role == "AXButton", value(node, "AXARIACurrent") as? String == "page" {
                foundCurrentButton = true
                let classes = value(node, "AXDOMClassList") as? [String] ?? []
                if classes.contains("data-[app-action-sidebar-thread-selected=true]:bg-primary-ghost-hover") {
                    selected += 1
                    let title = value(node, "AXTitle") as? String, label = value(node, "AXDescription") as? String
                    if [title, label].compactMap({ $0 }).contains(where: { !$0.isEmpty }) { named += 1 }
                    // Counts and equality only: never expose titles, task IDs,
                    // addresses or reply/editor contents in diagnostics.
                    rowEvidence = "；名称长度 \(uniqueTitle?.utf8.count ?? 0)/\(title?.utf8.count ?? 0)/\(label?.utf8.count ?? 0)；名称匹配 \(uniqueTitle != nil && title == uniqueTitle ? "是" : "否")/\(uniqueTitle != nil && label == uniqueTitle ? "是" : "否")；短名称唯一 \(compactTitle != nil ? "是" : "否")；侧栏样式 \(classes.contains("sidebar-item") ? "有" : "无")"
                    if let identity = identities.first(where: { MacCodexNavigationTitle.matchesSelectedRow(role: role, trusted: shell, current: "page", classes: classes,
                        title: title, label: label, uniqueTitle: $0.name) }),
                       MacCodexNavigationTitle.read(task, compact: identity.compact) == identity.name,
                       value(node, "AXARIACurrent") as? String == "page",
                       MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
                       let focused = value(root, "AXFocusedWindow"), CFEqual(focused, window),
                       MacCodexNavigationTitle.matchesSelectedRow(role: role, trusted: shell, current: "page", classes: classes,
                        title: value(node, "AXTitle") as? String, label: value(node, "AXDescription") as? String, uniqueTitle: identity.name) {
                        rowMatched = true; return true
                    }
                }
            }
            if shell && role == "AXLink" && currentTaskLink(role: role, current: value(node, "AXARIACurrent") as? String,
                               address: address(node), task: task, resourceRoot: resources) {
                return true
            }
            if shell && role == "AXGroup", value(node, "AXARIACurrent") as? String == "page",
               currentRow(node, task: task, resources: resources, deadline: deadline) { return true }
            if shell && role == "AXLink", address(node) != nil { foundTaskAddress = true }
            // Do not inspect editor text, browser contents, dialogs or other
            // windows. Only the foreground window's navigation is considered.
            if ["AXTextArea", "AXTextField", "AXDialog", "AXSheet", "AXStaticText", "AXImage"].contains(role) { continue }
            pending.append(contentsOf: (value(node, "AXChildren") as? [AXUIElement] ?? []).map { ($0, shell) })
        }
        if pending.hasPending { note(.navigationLimit) }
        else if !foundShell { note(.navigationShell) }
        else if checkedTitle { note(.navigationTitleMismatch) }
        else if foundCurrentButton { note(.navigationSelectionWithoutAddress) }
        else { note(foundTaskAddress ? .route : .navigationAddress) }
        return false
    }
    static func identityCheck(identifier: String, routeConfirmed: Bool) -> MacTab5DesktopCheck {
        let identifier = identifier.lowercased()
        let identified = identifier == "prompt-textarea" || identifier.contains("composer")
        if !identified { return routeConfirmed ? .identifier : .identifierAndRoute }
        return routeConfirmed ? .confirmed : .route
    }
    struct ComposerContainer {
        var role: String
        var classes: [String] = []
        var address: String? = nil
        var title: String? = nil
    }
    // Current desktop builds expose a ProseMirror editor inside the main
    // ComposerLayoutBody instead of a fixed AXIdentifier. Bind that structure
    // to its own primary document, never a sidebar row or another web area.
    static func structuredComposerCheck(classes: [String], containers: [ComposerContainer], task: String,
                                        uniqueFullTitle: String?, resources: URL?) -> MacTab5DesktopCheck {
        guard classes.contains("ProseMirror") else { return .identifier }
        guard !containers.contains(where: { ["AXDialog", "AXSheet", "AXTextArea", "AXTextField"].contains($0.role) || $0.classes.contains("@container/request-card") }),
              let pageIndex = containers.firstIndex(where: { $0.role == "AXWebArea" }),
              containers.filter({ $0.role == "AXWebArea" }).count == 1,
              containers[..<pageIndex].contains(where: { $0.classes.contains(where: { $0.hasPrefix("_ComposerLayoutBody_") }) }) else { return .structure }
        let page = containers[pageIndex]
        guard trustedShell(page.address, resourceRoot: resources) else { return .route }
        // An exact live address wins over a stale/matching title. In particular,
        // a different task's route cannot be rescued by title equality.
        if let address = page.address, let parsed = URLComponents(string: address),
           let last = (parsed.fragment ?? parsed.path).split(separator: "/").last,
           let routed = MacTab5Codex.id(String(last)), identifies(address, task: routed, resourceRoot: resources) {
            return routed == task ? .confirmed : .route
        }
        return MacCodexNavigationTitle.matches(role: page.role, trustedPrimary: true,
            pageTitle: page.title, uniqueTitle: uniqueFullTitle) ? .confirmed : .route
    }
    private static func composer(_ task: String) -> AXUIElement? {
        func reject(_ check: MacTab5DesktopCheck) -> AXUIElement? { note(check); return nil }
        guard AXIsProcessTrusted() else { return reject(.permission) }
        guard let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }), app.bundleIdentifier == "com.openai.codex" else { return reject(.frontmost) }
        let root = AXUIElementCreateApplication(app.processIdentifier); AXUIElementSetMessagingTimeout(root, 0.15)
        guard let rawWindow = value(root, "AXFocusedWindow"), CFGetTypeID(rawWindow) == AXUIElementGetTypeID() else { return reject(.focus) }
        let window = unsafeBitCast(rawWindow, to: AXUIElement.self)
        guard (value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { return reject(.route) }
        guard let focused = value(root, "AXFocusedUIElement"), CFGetTypeID(focused) == AXUIElementGetTypeID() else { return reject(.focus) }
        let editor = unsafeBitCast(focused, to: AXUIElement.self)
        guard value(editor, "AXRole") as? String == "AXTextArea" else { return reject(.role) }
        let identifier = (value(editor, "AXIdentifier") as? String ?? "").lowercased()
        var pending = (value(editor, "AXChildren") as? [AXUIElement]) ?? [], visited = 0
        while let child = pending.popLast() {
            visited += 1; guard visited <= 80, ["AXStaticText", "AXGroup"].contains(value(child, "AXRole") as? String ?? "") else { return reject(.structure) }
            pending.append(contentsOf: value(child, "AXChildren") as? [AXUIElement] ?? [])
        }
        let resources = app.bundleURL?.appendingPathComponent("Contents/Resources")
        var node = editor, routeConfirmed = false, foundWindow = false, containers: [ComposerContainer] = []
        var pages: [(element: AXUIElement, address: String?, title: String?)] = []
        for _ in 0..<32 {
            if let url = address(node), identifies(url, task: task, resourceRoot: resources) { routeConfirmed = true }
            if CFEqual(node, window) { foundWindow = true; break }
            guard let parent = value(node, "AXParent"), CFGetTypeID(parent) == AXUIElementGetTypeID() else { break }; node = unsafeBitCast(parent, to: AXUIElement.self)
            let role = value(node, "AXRole") as? String ?? ""
            containers.append(.init(role: role, classes: value(node, "AXDOMClassList") as? [String] ?? [],
                address: address(node), title: role == "AXWebArea" ? value(node, "AXTitle") as? String : nil))
            if role == "AXWebArea", let snapshot = containers.last { pages.append((node, snapshot.address, snapshot.title)) }
        }
        guard foundWindow else { return reject(.route) }
        var check = identityCheck(identifier: identifier, routeConfirmed: routeConfirmed)
        let editorClasses = value(editor, "AXDOMClassList") as? [String] ?? []
        if editorClasses.contains("ProseMirror") {
            // Only a full, globally unique catalog name qualifies here. Compact
            // navigation labels and open-only acknowledgments cannot edit text.
            let fullTitle = MacCodexNavigationTitle.read(task)
            check = structuredComposerCheck(classes: editorClasses,
                containers: containers, task: task, uniqueFullTitle: fullTitle, resources: resources)
            if check == .confirmed, MacCodexNavigationTitle.read(task) != fullTitle { return reject(.route) }
            guard pages.allSatisfy({ address($0.element) == $0.address && value($0.element, "AXTitle") as? String == $0.title }) else { return reject(.route) }
        }
        guard check == .confirmed else { return reject(check) }
        guard MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
              let currentWindow = value(root, "AXFocusedWindow"), CFEqual(currentWindow, window),
              let currentEditor = value(root, "AXFocusedUIElement"), CFEqual(currentEditor, editor) else { return reject(.focus) }
        note(.confirmed); return editor
    }
    struct ComposerTextNode {
        var classes: [String] = []
        var text: String? = nil
        var children: [ComposerTextNode] = []
    }
    static func composerText(_ raw: String, classes: [String], children: [ComposerTextNode]) -> String? {
        guard raw.utf8.count <= 2000 else { return nil }
        guard classes.contains("ProseMirror") else { return raw }
        // The main editor decorates its sole empty paragraph with `placeholder`.
        // Its CSS-generated text can be included in AXValue. Use that DOM
        // evidence, never a localized phrase or an empty selection, to exclude it.
        var markers = 0, placeholderText = "", otherText = ""
        func visit(_ node: ComposerTextNode, placeholder: Bool) {
            let marked = node.classes.contains("placeholder")
            if marked { markers += 1 }
            let inside = placeholder || marked
            if node.children.isEmpty, let text = node.text {
                if inside { placeholderText += text } else { otherText += text }
            }
            for child in node.children { visit(child, placeholder: inside) }
        }
        for child in children { visit(child, placeholder: false) }
        guard markers > 0 else { return raw }
        guard markers == 1, otherText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
        // An unexpected or stale decoration must not erase real body content.
        if raw.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return raw }
        guard !placeholderText.isEmpty, matchesReadback(raw, expected: placeholderText) else { return nil }
        return ""
    }
    private static func textValue(_ editor: AXUIElement) -> String? {
        guard let raw = value(editor, "AXValue") as? String else { return nil }
        var remaining = 80
        func snapshot(_ element: AXUIElement) -> ComposerTextNode? {
            remaining -= 1
            guard remaining >= 0, ["AXGroup", "AXStaticText"].contains(value(element, "AXRole") as? String ?? "") else { return nil }
            var children: [ComposerTextNode] = []
            for child in value(element, "AXChildren") as? [AXUIElement] ?? [] {
                guard let item = snapshot(child) else { return nil }; children.append(item)
            }
            return .init(classes: value(element, "AXDOMClassList") as? [String] ?? [],
                text: value(element, "AXValue") as? String, children: children)
        }
        var children: [ComposerTextNode] = []
        for child in value(editor, "AXChildren") as? [AXUIElement] ?? [] {
            guard let item = snapshot(child) else { return nil }; children.append(item)
        }
        // Reject a changing snapshot before it can become an expected-write value.
        guard value(editor, "AXValue") as? String == raw else { return nil }
        return composerText(raw, classes: value(editor, "AXDOMClassList") as? [String] ?? [], children: children)
    }
    private static func read(_ task: String) -> String? {
        guard let editor = composer(task) else { return nil }
        guard let text = textValue(editor) else { note(.value); return nil }; return text
    }
    static func matchesReadback(_ observed: String, expected: String) -> Bool {
        // Only CR/LF at the very end may differ in the accessibility paragraph
        // representation. Preserve and return the actual value; leading/internal
        // whitespace, trailing spaces and all body text still have to match.
        func body(_ text: String) -> Substring {
            var result = text[...]
            while let last = result.last, last.unicodeScalars.allSatisfy({ $0.value == 13 || $0.value == 10 }) { result.removeLast() }
            return result
        }
        return observed.utf8.count <= 2000 && body(observed) == body(expected)
    }
    static func confirmWrite(_ next: String, read: () -> String?, wait: () -> Void) -> String? {
        for attempt in 0..<12 {
            if let observed = read(), matchesReadback(observed, expected: next) { return observed }
            if attempt < 11 { wait() }
        }
        return nil
    }
    private static func write(_ task: String, _ expected: String, _ next: String) -> String? {
        guard let editor = composer(task) else { return nil }
        guard textValue(editor) == expected else { note(.changed); return nil }
        guard next.utf8.count <= 2000 else { note(.tooLong); return nil }
        var settable = DarwinBoolean(false)
        guard AXUIElementIsAttributeSettable(editor, "AXValue" as CFString, &settable) == .success, settable.boolValue else { note(.readOnly); return nil }
        // A provider can acknowledge late or fail after applying the change.
        // Reacquire and verify the exact target, but never repeat this write.
        _ = AXUIElementSetAttributeValue(editor, "AXValue" as CFString, next as CFString)
        guard let observed = confirmWrite(next, read: { read(task) }, wait: { Thread.sleep(forTimeInterval: 0.08) }) else { note(.writeUnconfirmed); return nil }
        return observed
    }
}
