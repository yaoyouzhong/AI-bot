import AppKit
import ApplicationServices

struct MacTab5DesktopBackend {
    var open: (String) -> Bool
    var read: (String) -> String?
    var write: (String, String, String) -> Bool
}

final class MacTab5Desktop {
    static let enabledKey = "tab5_codex_desktop_enabled"
    private let lock = NSLock(), codex: MacTab5Codex
    private let enabled: () -> Bool, backend: MacTab5DesktopBackend
    init(codex: MacTab5Codex, enabled: @escaping () -> Bool = { UserDefaults.standard.bool(forKey: enabledKey) }, backend: MacTab5DesktopBackend = MacTab5DesktopAX.backend) { self.codex = codex; self.enabled = enabled; self.backend = backend }
    func handle(_ root: [String: Any], device: String) -> MacTab5RPCResult {
        guard enabled(), codex.isEnabled else { return .error(403, "desktop_control_not_enabled") }
        lock.lock(); defer { lock.unlock() }
        guard let id = MacTab5Codex.id(root["requestId"]), let op = root["op"] as? String, ["open-recent", "stage-draft", "clear-draft", "submit-draft"].contains(op) else { return .error(400, "invalid_request") }
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
                let opened = backend.open(task)
                var current = opened ? backend.read(task) : nil
                for _ in 0..<6 where opened && current == nil { Thread.sleep(forTimeInterval: 0.15); current = backend.read(task) }
                if let current { body["foreground"] = true; body["draftEmpty"] = current.isEmpty; result = .init(200, body) }
                else { result = .error(409, "desktop_foreground_unconfirmed") }
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
    static var backend: MacTab5DesktopBackend { .init(open: { task in MacTab5ComputerPlatform.main { guard let url = URL(string: "codex://threads/" + task) else { return false }; return NSWorkspace.shared.open(url) } }, read: read, write: write) }
    private static func value(_ element: AXUIElement, _ key: String) -> CFTypeRef? { var value: CFTypeRef?; return AXUIElementCopyAttributeValue(element, key as CFString, &value) == .success ? value : nil }
    static func identifies(_ url: String, task: String) -> Bool {
        guard let parsed = URLComponents(string: url), ["codex", "http", "https", "file"].contains(parsed.scheme ?? "") else { return false }
        if parsed.scheme == "http" || parsed.scheme == "https" { guard ["localhost", "127.0.0.1", "::1"].contains(parsed.host ?? "") else { return false } }
        if parsed.scheme == "file" { guard parsed.path.hasPrefix("/Applications/Codex.app/Contents/Resources/") else { return false } }
        let path = (parsed.host == "threads" ? "/threads" : "") + parsed.path + (parsed.fragment.map { "/" + $0 } ?? "")
        let parts = path.split(separator: "/").map(String.init)
        return parts.indices.contains(parts.count - 2) && parts[parts.count - 2] == "threads" && parts.last?.lowercased() == task.lowercased()
    }
    private static func composer(_ task: String) -> AXUIElement? {
        guard AXIsProcessTrusted(), let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }), app.bundleIdentifier == "com.openai.codex" else { return nil }
        let root = AXUIElementCreateApplication(app.processIdentifier); AXUIElementSetMessagingTimeout(root, 0.15)
        guard let focused = value(root, "AXFocusedUIElement"), CFGetTypeID(focused) == AXUIElementGetTypeID() else { return nil }
        let editor = unsafeBitCast(focused, to: AXUIElement.self)
        guard value(editor, "AXRole") as? String == "AXTextArea" else { return nil }
        let identifier = (value(editor, "AXIdentifier") as? String ?? "").lowercased()
        guard identifier == "prompt-textarea" || identifier.contains("composer") else { return nil }
        var pending = (value(editor, "AXChildren") as? [AXUIElement]) ?? [], visited = 0
        while let child = pending.popLast() {
            visited += 1; guard visited <= 80, ["AXStaticText", "AXGroup"].contains(value(child, "AXRole") as? String ?? "") else { return nil }
            pending.append(contentsOf: value(child, "AXChildren") as? [AXUIElement] ?? [])
        }
        // Require a public accessibility URL carrying the exact task UUID.
        // Matching a generic window title is insufficient and never types text.
        var node = editor
        for _ in 0..<20 {
            if let url = value(node, "AXURL") as? URL, identifies(url.absoluteString, task: task) { return editor }
            guard let parent = value(node, "AXParent"), CFGetTypeID(parent) == AXUIElementGetTypeID() else { break }; node = unsafeBitCast(parent, to: AXUIElement.self)
        }
        return nil
    }
    private static func read(_ task: String) -> String? { guard let editor = composer(task), let text = value(editor, "AXValue") as? String, text.utf8.count <= 2000 else { return nil }; return text }
    private static func write(_ task: String, _ expected: String, _ next: String) -> Bool {
        guard let editor = composer(task), value(editor, "AXValue") as? String == expected, next.utf8.count <= 2000 else { return false }
        var settable = DarwinBoolean(false)
        guard AXUIElementIsAttributeSettable(editor, "AXValue" as CFString, &settable) == .success, settable.boolValue,
              AXUIElementSetAttributeValue(editor, "AXValue" as CFString, next as CFString) == .success else { return false }
        return read(task) == next
    }
}
