import AppKit
import ApplicationServices

struct MacTab5DesktopBackend {
    var open: (String) -> Bool
    var read: (String) -> String?
    var write: (String, String, String) -> Bool
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
    case open = "系统未接受打开会话请求"
    case confirmed = "已确认目标任务和主输入框"
    case navigationConfirmed = "已确认目标会话；输入框操作将另行校验"
    case navigationOpened = "已请求打开指定会话，Codex 已在前台；同名会话未作精确确认，输入框操作另行检查"
}

final class MacTab5Desktop {
    static let enabledKey = "tab5_codex_desktop_enabled"
    private let lock = NSLock(), codex: MacTab5Codex
    private let enabled: () -> Bool, backend: MacTab5DesktopBackend
    private let statusLock = NSLock()
    private var status: MacTab5DesktopCheck = .waiting
    // Opening a page is safe to repeat after an app restart. Keep its bounded
    // deduplication receipts in memory, without unlocking the private draft key.
    // Editing and submission still persist encrypted receipts before acting.
    private var navigationReceipts: [String: Any] = [:]
    var summary: String {
        guard enabled() else { return "尚未启用" }
        if let failure = backend.preflight() { return failure.rawValue }
        statusLock.lock(); let current = status; statusLock.unlock()
        let detail = current == .waiting ? "" : backend.evidence()
        return current.rawValue + (detail.isEmpty ? "" : "（" + detail + "）")
    }
    private func updateStatus(_ value: MacTab5DesktopCheck) { statusLock.lock(); status = value; statusLock.unlock() }
    init(codex: MacTab5Codex, enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, backend: MacTab5DesktopBackend = MacTab5DesktopAX.backend) { self.codex = codex; self.enabled = enabled; self.backend = backend }
    func handle(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard enabled(), codex.isEnabled else { return .error(403, "desktop_control_not_enabled") }
        lock.lock(); defer { lock.unlock() }
        guard let id = MacTab5Codex.id(root["requestId"]), let op = root["op"] as? String, ["open-recent", "stage-draft", "clear-draft", "submit-draft"].contains(op) else { return .error(400, "invalid_request") }
        if op == "submit-draft", !codex.canMutate { return .error(403, "codex_read_only_connection") }
        let tasks = codex.snapshot()?["tasks"] as? [[String: Any]] ?? []
        guard let task = MacTab5Codex.id(root["taskId"] ?? tasks.first?["id"]), let entry = tasks.first(where: { $0["id"] as? String == task }) else { return .error(404, "task_not_in_recent_catalog") }
        guard codex.contains(task) else { return .error(503, "codex_catalog_stale") }
        do {
            let binding = device + "|" + task, text = root["text"] as? String ?? "", draftID = root["draftRequestId"] as? String ?? ""
            let fingerprint = MacTab5Protocol.sha256(try MacTab5Protocol.json(["binding": binding, "op": op, "text": text, "draft": draftID]))
            if let record = navigationReceipts[id] as? [String: Any] { return record["fingerprint"] as? String == fingerprint ? .init(record["status"] as? Int ?? 504, record["body"] as? [String: Any] ?? [:]) : .error(409, "request_id_conflict") }
            var records = op == "open-recent" ? navigationReceipts : try codex.store.read("desktop-receipts").map(MacTab5Protocol.object) ?? [:]
            if let record = records[id] as? [String: Any] { return record["fingerprint"] as? String == fingerprint ? .init(record["status"] as? Int ?? 504, record["body"] as? [String: Any] ?? [:]) : .error(409, "request_id_conflict") }
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
                    for _ in 0..<6 where opened && !navigated && !activated && ProcessInfo.processInfo.systemUptime < until {
                        Thread.sleep(forTimeInterval: 0.15); navigated = confirmed()
                        activated = !navigated && backend.openOnly?(task) == true
                    }
                    if navigated || activated {
                        updateStatus(activated ? .navigationOpened : current == nil ? .navigationConfirmed : .confirmed)
                        body["foreground"] = true
                        if let current { body["draftEmpty"] = current.isEmpty }
                        result = .init(200, body)
                    } else { updateStatus(opened ? backend.diagnostic() : .open); result = .error(409, "desktop_foreground_unconfirmed") }
                }
            } else if op == "stage-draft" {
                if let current = backend.read(task), !text.isEmpty {
                    let combined = current.isEmpty ? text : current + "\n" + text
                    if combined.utf8.count <= 2000, backend.write(task, current, combined) {
                        let saved = codex.handle(["op": "draft-save", "taskId": task, "requestId": id, "message": combined], device: device)
                        if saved.status == 200 { record["stagedText"] = combined; record["stage"] = true; body["staged"] = true; body["appended"] = !current.isEmpty; result = .init(200, body) }
                        else { result = saved }
                    } else { result = .error(409, "composer_changed") }
                } else { result = .error(503, "composer_not_ready") }
            } else if op == "clear-draft" {
                if let current = backend.read(task), backend.write(task, current, "") {
                    let saved = codex.handle(["op": "draft-save", "taskId": task, "requestId": id, "message": ""], device: device)
                    if saved.status == 200 { body["cleared"] = true; result = .init(200, body) } else { result = saved }
                } else { result = .error(409, "composer_changed") }
            } else {
                if let draft = records[draftID.lowercased()] as? [String: Any], draft["binding"] as? String == binding, draft["stage"] as? Bool == true,
                   let created = draft["createdAt"] as? Double, Date().timeIntervalSince1970 - created <= 900,
                   let staged = draft["stagedText"] as? String, !staged.isEmpty, backend.read(task) == staged {
                    // Consume before touching the composer. A repeated click can
                    // only query this delivery receipt, never send another turn.
                    var consumed = draft; consumed["stage"] = false; records[draftID.lowercased()] = consumed
                    try codex.store.write("desktop-receipts", MacTab5Protocol.json(records))
                    if backend.write(task, staged, "") {
                        let sent = codex.handle(["op": "send", "taskId": task, "requestId": id, "message": staged], device: device)
                        if sent.status == 202 { body["submitted"] = true; result = .init(200, body) }
                        else { _ = backend.write(task, "", staged); result = sent }
                    } else { result = .error(409, "composer_changed") }
                } else { result = .error(409, "composer_changed") }
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
        guard MacCodexNavigationTitle.identity(task) == .duplicate, AXIsProcessTrusted(),
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
        defer {
            let detail = "节点 \(visited)；主区域 \(shells)；唯一标题 \(unique ? "有" : "无")；页面匹配 \(titleMatched ? "是" : "否")；当前行 \(selected)／名称 \(named)；行匹配 \(rowMatched ? "是" : "否")；\(Int((ProcessInfo.processInfo.systemUptime-started)*1000)) ms"
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
        unique = uniqueTitle != nil
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
                    checkedTitle = uniqueTitle != nil
                    if MacCodexNavigationTitle.matches(role: role, trustedPrimary: true,
                        pageTitle: value(node, "AXTitle") as? String, uniqueTitle: uniqueTitle),
                       MacCodexNavigationTitle.read(task) == uniqueTitle,
                       MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
                       let focused = value(root, "AXFocusedWindow"), CFEqual(focused, window),
                       value(node, "AXTitle") as? String == uniqueTitle {
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
                    if MacCodexNavigationTitle.matchesSelectedRow(role: role, trusted: shell, current: "page", classes: classes,
                        title: title, label: label, uniqueTitle: uniqueTitle),
                       MacCodexNavigationTitle.read(task) == uniqueTitle,
                       value(node, "AXARIACurrent") as? String == "page",
                       MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
                       let focused = value(root, "AXFocusedWindow"), CFEqual(focused, window),
                       MacCodexNavigationTitle.matchesSelectedRow(role: role, trusted: shell, current: "page", classes: classes,
                        title: value(node, "AXTitle") as? String, label: value(node, "AXDescription") as? String, uniqueTitle: uniqueTitle) {
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
    private static func composer(_ task: String) -> AXUIElement? {
        func reject(_ check: MacTab5DesktopCheck) -> AXUIElement? { note(check); return nil }
        guard AXIsProcessTrusted() else { return reject(.permission) }
        guard let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }), app.bundleIdentifier == "com.openai.codex" else { return reject(.frontmost) }
        let root = AXUIElementCreateApplication(app.processIdentifier); AXUIElementSetMessagingTimeout(root, 0.15)
        guard let focused = value(root, "AXFocusedUIElement"), CFGetTypeID(focused) == AXUIElementGetTypeID() else { return reject(.focus) }
        let editor = unsafeBitCast(focused, to: AXUIElement.self)
        guard value(editor, "AXRole") as? String == "AXTextArea" else { return reject(.role) }
        let identifier = (value(editor, "AXIdentifier") as? String ?? "").lowercased()
        var pending = (value(editor, "AXChildren") as? [AXUIElement]) ?? [], visited = 0
        while let child = pending.popLast() {
            visited += 1; guard visited <= 80, ["AXStaticText", "AXGroup"].contains(value(child, "AXRole") as? String ?? "") else { return reject(.structure) }
            pending.append(contentsOf: value(child, "AXChildren") as? [AXUIElement] ?? [])
        }
        // Require a public accessibility URL carrying the exact task UUID.
        // Matching a generic window title is insufficient and never types text.
        var node = editor, routeConfirmed = false
        for _ in 0..<20 {
            let address = value(node, "AXURL")
            let url = (address as? URL)?.absoluteString ?? (address as? String)
            if let url, identifies(url, task: task, resourceRoot: app.bundleURL?.appendingPathComponent("Contents/Resources")) { routeConfirmed = true; break }
            guard let parent = value(node, "AXParent"), CFGetTypeID(parent) == AXUIElementGetTypeID() else { break }; node = unsafeBitCast(parent, to: AXUIElement.self)
        }
        // Inspect both identity signals before rejecting, without reading text.
        // Missing editor identity never authorizes a generic focused text area.
        let check = identityCheck(identifier: identifier, routeConfirmed: routeConfirmed)
        guard check == .confirmed else { return reject(check) }
        note(.confirmed); return editor
    }
    private static func read(_ task: String) -> String? {
        guard let editor = composer(task) else { return nil }
        guard let text = value(editor, "AXValue") as? String, text.utf8.count <= 2000 else { note(.value); return nil }; return text
    }
    private static func write(_ task: String, _ expected: String, _ next: String) -> Bool {
        guard let editor = composer(task), value(editor, "AXValue") as? String == expected, next.utf8.count <= 2000 else { return false }
        var settable = DarwinBoolean(false)
        guard AXUIElementIsAttributeSettable(editor, "AXValue" as CFString, &settable) == .success, settable.boolValue,
              AXUIElementSetAttributeValue(editor, "AXValue" as CFString, next as CFString) == .success else { return false }
        return read(task) == next
    }
}
