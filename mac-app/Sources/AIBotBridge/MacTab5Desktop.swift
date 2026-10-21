import AppKit
import ApplicationServices

struct MacTab5DesktopBackend {
    var open: (String) -> Bool
    var read: (String) -> String?
    var write: (String, String, String) -> Bool
    var diagnostic: () -> MacTab5DesktopCheck = { .route }
    var preflight: () -> MacTab5DesktopCheck? = { nil }
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
    case route = "未取得匹配目标任务的公开页面地址"
    case value = "未取得可用的输入框文字"
    case open = "系统未接受打开会话请求"
    case confirmed = "已确认目标任务和主输入框"
}

final class MacTab5Desktop {
    static let enabledKey = "tab5_codex_desktop_enabled"
    private let lock = NSLock(), codex: MacTab5Codex
    private let enabled: () -> Bool, backend: MacTab5DesktopBackend
    private let statusLock = NSLock()
    private var status: MacTab5DesktopCheck = .waiting
    var summary: String {
        guard enabled() else { return "尚未启用" }
        if let failure = backend.preflight() { return failure.rawValue }
        statusLock.lock(); defer { statusLock.unlock() }; return status.rawValue
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
            var records = try codex.store.read("desktop-receipts").map(MacTab5Protocol.object) ?? [:]
            let binding = device + "|" + task, text = root["text"] as? String ?? "", draftID = root["draftRequestId"] as? String ?? ""
            let fingerprint = MacTab5Protocol.sha256(try MacTab5Protocol.json(["binding": binding, "op": op, "text": text, "draft": draftID]))
            if let record = records[id] as? [String: Any] { return record["fingerprint"] as? String == fingerprint ? .init(record["status"] as? Int ?? 504, record["body"] as? [String: Any] ?? [:]) : .error(409, "request_id_conflict") }
            guard records.count < 2048, text.utf8.count <= 2000 else { return .error(429, "desktop_receipt_capacity") }
            var record: [String: Any] = ["binding": binding, "fingerprint": fingerprint, "status": 504, "body": ["error": "desktop_result_unknown"], "createdAt": Date().timeIntervalSince1970]
            records[id] = record; try codex.store.write("desktop-receipts", MacTab5Protocol.json(records))
            var result: MacTab5RPCResult
            var body = entry; body.removeValue(forKey: "id"); body["taskId"] = task; body["requestId"] = id
            if op == "open-recent" {
                if let failure = backend.preflight() {
                    // Do not navigate the Mac when confirmation is known to be
                    // impossible: the device keeps its previous selection on 409.
                    updateStatus(failure); result = .error(409, "desktop_foreground_unconfirmed")
                } else {
                    let opened = backend.open(task)
                    var current = opened ? backend.read(task) : nil
                    for _ in 0..<6 where opened && current == nil { Thread.sleep(forTimeInterval: 0.15); current = backend.read(task) }
                    if let current { updateStatus(.confirmed); body["foreground"] = true; body["draftEmpty"] = current.isEmpty; result = .init(200, body) }
                    else { updateStatus(opened ? backend.diagnostic() : .open); result = .error(409, "desktop_foreground_unconfirmed") }
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
            try codex.store.write("desktop-receipts", MacTab5Protocol.json(records)); return result
        } catch { return .error(504, "desktop_result_unknown") }
    }
}

enum MacTab5DesktopAX {
    private static let diagnosticLock = NSLock()
    private static var lastCheck: MacTab5DesktopCheck = .waiting
    private static func note(_ check: MacTab5DesktopCheck) { diagnosticLock.lock(); lastCheck = check; diagnosticLock.unlock() }
    private static func diagnostic() -> MacTab5DesktopCheck { diagnosticLock.lock(); defer { diagnosticLock.unlock() }; return lastCheck }
    static var backend: MacTab5DesktopBackend { .init(open: { task in MacTab5ComputerPlatform.main { guard let url = URL(string: "codex://threads/" + task) else { return false }; return NSWorkspace.shared.open(url) } }, read: read, write: write, diagnostic: diagnostic, preflight: { AXIsProcessTrusted() ? nil : .permission }) }
    private static func value(_ element: AXUIElement, _ key: String) -> CFTypeRef? { var value: CFTypeRef?; return AXUIElementCopyAttributeValue(element, key as CFString, &value) == .success ? value : nil }
    static func identifies(_ url: String, task: String, resourceRoot: URL? = nil) -> Bool {
        guard let parsed = URLComponents(string: url), ["codex", "http", "https", "file"].contains(parsed.scheme ?? "") else { return false }
        if parsed.scheme == "codex" { guard parsed.host == "threads" else { return false } }
        if parsed.scheme == "http" || parsed.scheme == "https" { guard ["localhost", "127.0.0.1", "::1"].contains(parsed.host ?? "") else { return false } }
        if parsed.scheme == "file" {
            guard let resourceRoot, resourceRoot.isFileURL, parsed.host == nil || parsed.host == "" || parsed.host == "localhost",
                  URL(fileURLWithPath: parsed.path).standardizedFileURL.path.hasPrefix(resourceRoot.standardizedFileURL.path + "/") else { return false }
        }
        let path = (parsed.host == "threads" ? "/threads" : "") + parsed.path + (parsed.fragment.map { "/" + $0 } ?? "")
        let parts = path.split(separator: "/").map(String.init)
        return parts.indices.contains(parts.count - 2) && parts[parts.count - 2] == "threads" && parts.last?.lowercased() == task.lowercased()
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
