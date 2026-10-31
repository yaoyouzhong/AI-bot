import Foundation

// Synthetic end-to-end journal/native-submission checks, also runnable on Macs
// whose Command Line Tools do not ship XCTest. No desktop or device is touched.
enum MacTab5TaskAppendValidation {
    private final class Wire: MacCodexDesktopIPC.Wire {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        let owner = UUID().uuidString.lowercased(), client = UUID().uuidString.lowercased()
        var replies: [[String: Any]] = [], mutations = 0
        var accepted = false
        func write(_ object: [String: Any], until: TimeInterval) throws {
            let method = object["method"] as? String ?? ""
            var reply: [String: Any] = ["type": "response", "requestId": object["requestId"] ?? "",
                "resultType": "success", "handledByClientId": owner, "method": method]
            if method == "initialize" { reply["result"] = ["clientId": client]; replies.append(reply) }
            else if method == "thread-owner-discovery" { reply["result"] = ["supportsUntrustedAppInput": true]; replies.append(reply) }
            else if method == "thread-stream-following-changed", (object["params"] as? [String: Any])?["following"] as? Bool == true {
                replies.append(["method": "thread-stream-state-changed", "version": 11, "sourceClientId": owner,
                    "params": ["hostId": "local", "conversationId": task, "change": ["type": "snapshot", "conversationState": [
                        "id": task, "cwd": "/fixture", "requests": [Any](), "threadRuntimeStatus": ["type": "active"],
                        "turns": [["turnId": turn, "status": "inProgress", "turnStartedAtMs": 1000, "items": accepted ? [["type": "userMessage", "id": "new-accepted-message", "content": [["type": "text", "text": "Synthetic follow-up"]]]] : [[String: Any]]()]]]]]])
            } else if method.hasPrefix("thread-follower-") { mutations += 1; throw MacCodexDesktopError.timeout }
        }
        func read(until: TimeInterval) throws -> [String: Any] {
            guard !replies.isEmpty else { throw MacCodexDesktopError.timeout }; return replies.removeFirst()
        }
        func close() {}
    }
    private struct LayoutNode {
        var role: String
        var id = ""
        var classes: [String] = []
        var label = ""
        var text = ""
        var children: [LayoutNode] = []
    }
    private static func layout(_ extras: [LayoutNode] = []) -> LayoutNode {
        .init(role: "AXGroup", children: [.init(role: "AXGroup", id: "body",
            classes: ["_ComposerLayoutBody_fixture"], children: [
                .init(role: "AXTextArea", id: "editor"),
                .init(role: "AXPopUpButton", children: [.init(role: "AXImage")]),
                .init(role: "AXButton", label: "Send")
            ] + extras)])
    }
    private static func safe(_ root: LayoutNode, clock: (() -> TimeInterval)? = nil) -> Bool {
        MacTab5DesktopAX.attachmentFreeLayout(root, role: { $0.role }, classes: { $0.classes },
            children: { $0.children }, label: { $0.label }, text: { $0.text },
            isEditor: { $0.id == "editor" }, isBody: { $0.id == "body" },
            clock: clock ?? { ProcessInfo.processInfo.systemUptime })
    }
    private static var nestedAttachment: LayoutNode {
        .init(role: "AXPopUpButton", children: [
            .init(role: "AXImage"),
            .init(role: "AXGroup", children: [.init(role: "AXButton", label: "Remove attachment")])
        ])
    }
    static func run() throws {
        let directory = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: directory) }
        func check(_ value: Bool, _ label: String) throws {
            if !value { throw NSError(domain: "TaskAppendValidation", code: 1, userInfo: [NSLocalizedDescriptionKey: label]) }
        }
        try check(safe(layout()), "empty composer model popup decoration")
        try check(!safe(layout([nestedAttachment])), "real attachment popup removal descendant")
        for role in ["AXButton", "AXPopUpButton", "AXMenuButton"] {
            for caption in ["Remove attachment", "移除图片", "删除附件", "unknown nested action"] {
                let card = LayoutNode(role: role, children: [.init(role: "AXImage"),
                    .init(role: "AXGroup", children: [.init(role: "AXButton", label: caption)])])
                try check(!safe(layout([card])), "nested attachment under " + role + " " + caption)
            }
        }
        for role in ["AXImage", "AXLink", "AXDialog", "AXSheet", "AXWebArea"] {
            try check(!safe(layout([.init(role: role)])), "standalone content " + role)
        }
        let emptySlot = LayoutNode(role: "AXGroup", classes: ["_ComposerLayoutAttachments_fixture"])
        try check(safe(layout([emptySlot])), "empty attachment slot")
        var fullSlot = emptySlot; fullSlot.children = [.init(role: "AXStaticText", text: "quoted text")]
        try check(!safe(layout([fullSlot])), "quote slot text")
        try check(!safe(layout([.init(role: "AXPopUpButton", children: [fullSlot])])), "slot under popup")
        try check(!safe(layout([.init(role: "AXTextArea")])), "foreign editor")
        try check(!safe(layout(Array(repeating: .init(role: "AXGroup"), count: 301))), "layout node bound")
        var tick = 0.0
        try check(!safe(layout(), clock: { tick += 0.5; return tick }), "layout time bound")
        let diagnosticDirectory = directory.appendingPathComponent("diagnostics")
        let diagnostic = MacTab5LocalDiagnostics(directory: diagnosticDirectory)
        diagnostic.desktop(.taskAppendAttachments, evidence: "nodes=17, blocked=nested-control, ready=0")
        diagnostic.request("追加消息", status: 409, outcome: "composer_changed")
        diagnostic.waitUntilIdle()
        let diagnosticURL = diagnosticDirectory.appendingPathComponent("tab5-desktop-diagnostics.json")
        let firstDiagnostic = try MacTab5Protocol.object(Data(contentsOf: diagnosticURL))
        try check(firstDiagnostic.count == 2 && (firstDiagnostic["request"] as? [String: Any])?["detail"] as? String == "composer_changed", "bounded background diagnosis")
        diagnostic.request("private-label-canary", status: 409, outcome: "private-canary")
        diagnostic.request("追加消息", status: 409, outcome: "private-canary")
        diagnostic.desktop(.taskAppendScope, evidence: "private-canary/filename.jpg")
        diagnostic.waitUntilIdle()
        let redactedDiagnostic = String(decoding: try Data(contentsOf: diagnosticURL), as: UTF8.self)
        try check(!redactedDiagnostic.contains("private-canary") && !redactedDiagnostic.contains("filename"), "background diagnostics reject arbitrary payloads")
        let attributes = try FileManager.default.attributesOfItem(atPath: diagnosticURL.path)
        try check((attributes[.posixPermissions] as? NSNumber)?.intValue == 0o600, "background diagnostics user-only file")
        let current = UUID().uuidString.lowercased(), other = UUID().uuidString.lowercased()
        let activityRoot = directory.appendingPathComponent("activity")
        var activityTime = 500.25
        let activity = MacTaskActivity(directory: activityRoot, now: { activityTime })
        let catalog: [[String: Any]] = [["id": other, "updatedAt": Int64(300)], ["id": current, "updatedAt": Int64(100)]]
        try check(activity.apply(catalog).first?["id"] as? String == other, "unvisited catalog order")
        try activity.record(current)
        try check(activity.apply(catalog).first?["id"] as? String == current, "explicit use over stale desktop recency")
        try check(activity.apply(catalog).first?["updatedAt"] as? Int64 == 500, "existing wire seconds")
        activityTime = 500.75; try activity.record(other)
        try check(activity.apply(catalog).first?["id"] as? String == other, "same-second explicit order")
        activityTime = 200; try activity.record(current)
        let restarted = MacTaskActivity(directory: activityRoot)
        try check(restarted.apply(catalog).first?["id"] as? String == other, "restart and clock rollback preserve recency")
        var newer = catalog; newer[1]["updatedAt"] = Int64(600)
        try check(restarted.apply(newer).first?["id"] as? String == current, "new desktop activity still wins")
        try check(restarted.apply(catalog).first?["id"] as? String == other, "reads do not promote activity")
        for scenario in ["queue", "steer", "navigation-shell", "existing-draft", "question", "attachment", "attachment-popup", "unknown", "unknown-device", "unknown-auto", "unknown-expired", "unknown-changed", "write-unknown", "device-photo", "disabled"] {
            let wire = Wire(), root = directory.appendingPathComponent(scenario)
            var sendAllowed = true
            var testClock = 100.0
            let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 4, count: 32) }),
                enabled: { true }, sendEnabled: { sendAllowed }, clock: { testClock }, desktop: .init(connect: { wire },
                    queueReceipt: { _, _ in .init(known: [], matching: []) }, serverQueueReceipt: { _, _ in .init(known: [], matching: []) }), call: { method, _ in
                    if method == "thread/list" { return ["data": [["id": wire.task, "cwd": "/fixture", "name": "Synthetic"]]] }
                    return [:]
                })
            var text = scenario == "existing-draft" ? "Keep my draft" : "", writes = 0, sends = 0
            var backend = MacTab5DesktopBackend(open: { $0 == wire.task }, read: { $0 == wire.task ? text : nil }, write: { task, before, after in
                guard task == wire.task, text == before else { return nil }
                text = after; writes += 1; return scenario == "write-unknown" ? nil : text
            })
            backend.navigation = { $0 == wire.task && scenario != "navigation-shell" }; backend.focusComposer = { $0 == wire.task }
            let navigationMetadata = "nav.nodes=3, shells=0, webareas=0, child-errors=1, child-error=-25204"
            backend.navigationMetadata = { navigationMetadata }
            backend.diagnostic = { scenario == "navigation-shell" ? .navigationShell : .route }
            backend.appendEvidence = { "nodes=13, ready=1" }
            backend.target = { _ in scenario == "question" ? "question:fixture" : "main" }
            backend.attachmentFree = { _ in
                if scenario == "attachment-popup" { return safe(layout([nestedAttachment])) }
                return scenario != "attachment"
            }
            backend.submitComposer = { task, expected in
                guard task == wire.task, expected == text else { return .notReady }
                sends += 1
                if scenario.hasPrefix("unknown") { return .unconfirmed }
                text = ""; return .confirmed
            }
            let desktop = MacTab5Desktop(codex: codex, enabled: { scenario != "disabled" }, backend: backend, diagnostics: diagnostic)
            codex.taskAppend = { task, text, authorized, captureBaseline in try desktop.appendTask(task: task, text: text, authorized: authorized, captureBaseline: captureBaseline) }
            codex.refresh()
            // A stale display turn must not reject the requested latest-turn append.
            var request: [String: Any] = ["op": "steer", "taskId": wire.task, "message": "Synthetic follow-up", "expectedTurn": UUID().uuidString, "requestId": UUID().uuidString]
            if scenario == "device-photo" { request["images"] = [UUID().uuidString] }
            let first = codex.handle(request, device: "fixture"), replay = codex.handle(request, device: "fixture")
            let success = ["queue", "steer"].contains(scenario), unknown = scenario.hasPrefix("unknown") || scenario == "write-unknown"
            try check(first.status == (success ? 202 : unknown ? 504 : 409), scenario + " result")
            try check(first.status == replay.status && wire.mutations == 0, scenario + " replay/no IPC fallback")
            try check(sends == (success || scenario.hasPrefix("unknown") ? 1 : 0), scenario + " single native press")
            try check(writes == (success || unknown ? 1 : 0), scenario + " preserves existing inputs")
            if scenario == "navigation-shell" {
                diagnostic.waitUntilIdle()
                let record = try MacTab5Protocol.object(Data(contentsOf: diagnosticURL))["desktop"] as? [String: Any]
                try check(record?["detail"] as? String == navigationMetadata, "navigation failure retains navigation metadata instead of stale composer evidence")
                try check(record?["status"] as? String == MacTab5DesktopCheck.navigationShell.rawValue, "navigation failure reason retained")
            }
            if scenario == "existing-draft" { try check(text == "Keep my draft", "existing draft retained") }
            if success || unknown {
                let data = try MacTab5Protocol.object(codex.store.read("journal")!)
                let drafts = data["drafts"] as! [String: Any], draft = drafts["fixture|" + wire.task] as! [String: Any]
                try check(draft["text"] as? String == (success ? "" : "Synthetic follow-up"), scenario + " draft journal")
            }
            if scenario.hasPrefix("unknown") {
                let pending = try codex.pendingDeliveries()
                try check(pending.count == 1 && pending[0].id == MacTab5Codex.id(request["requestId"]), "recover exact pending request")
                let selected = pending[0]
                var query = request; query["op"] = "receipt"
                try check(codex.handle(query, device: "fixture").status == 504, "query does not retry unknown")
                if scenario == "unknown-auto" || scenario == "unknown-expired" {
                    wire.accepted = true
                    if scenario == "unknown-expired" { testClock += 61 }
                    let resolved = codex.handle(query, device: "fixture")
                    try check(resolved.status == (scenario == "unknown-auto" ? 202 : 504), "automatic recovery respects receipt baseline expiry")
                    try check(sends == 1 && writes == 1, "automatic recovery never retries")
                    if scenario == "unknown-auto" {
                        try check(codex.handle(query, device: "fixture").status == 202, "automatic recovery replay")
                        continue
                    }
                    wire.accepted = false
                }
                if scenario == "unknown-changed" {
                    let saved = codex.handle(["op": "draft-save", "taskId": wire.task, "requestId": UUID().uuidString,
                                              "message": "New independent draft"], device: "fixture")
                    try check(saved.status == 200, "changed draft fixture saved")
                    try check(try codex.pendingDeliveries().isEmpty, "changed draft is not offered for recovery")
                    var refused = false
                    do { try codex.confirmDelivered(selected) } catch { refused = true }
                    try check(refused && codex.handle(query, device: "fixture").status == 504, "stale selection cannot clear changed draft")
                } else {
                    sendAllowed = false
                    var refused = false
                    do { try codex.confirmDelivered(selected) } catch { refused = true }
                    try check(refused, "permission revoked before manual recovery")
                    sendAllowed = true
                    if scenario == "unknown-device" || scenario == "unknown-expired" {
                        var confirm = request; confirm["op"] = "receipt-confirm"
                        try check(codex.handle(confirm, device: "other-device").status == 404, "confirmation device binding")
                        var wrong = confirm; wrong["message"] = "Changed text"
                        try check(codex.handle(wrong, device: "fixture").status == 409, "confirmation content binding")
                        sendAllowed = false
                        try check(codex.handle(confirm, device: "fixture").status == 403, "device confirmation permission")
                        sendAllowed = true
                        try check(codex.handle(confirm, device: "fixture").status == 202, "device confirmation")
                        try check(codex.handle(confirm, device: "fixture").status == 202, "device confirmation replay")
                    } else { try codex.confirmDelivered(selected) }
                    try check(codex.handle(query, device: "fixture").status == 202, "manual acknowledgment resolves old receipt")
                    try check(codex.handle(request, device: "fixture").status == 202, "original replay remains accepted without resend")
                    try check(try codex.pendingDeliveries().isEmpty, "resolved request no longer locks device load")
                    let journal = try MacTab5Protocol.object(codex.store.read("journal")!)
                    let entries = journal["receipts"] as! [String: Any]
                    let entry = entries[selected.id] as! [String: Any]
                    try check(entry["resolutionSource"] as? String == (scenario == "unknown-device" || scenario == "unknown-expired" ? "device_user_confirmed_delivered" : "user_confirmed_delivered"), "manual source recorded locally")
                    let drafts = journal["drafts"] as! [String: Any]
                    try check((drafts[selected.binding] as? [String: Any])?["text"] as? String == "", "only confirmed unchanged draft cleared")
                }
                try check(sends == 1 && writes == 1 && wire.mutations == 0, "recovery never submits or rewrites desktop")
            }
        }
        print("TASK_APPEND_VALIDATION_OK")
    }
}
