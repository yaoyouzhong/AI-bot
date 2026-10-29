import AppKit
import ApplicationServices

enum MacTab5DesktopSubmission { case notReady, unconfirmed, confirmed }

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
    // Only an explicit stage operation may acquire the verified target editor.
    var focusComposer: ((String) -> Bool)? = nil
    var target: ((String) -> String?)? = nil
    var writeTarget: ((String, String, String, String) -> String?)? = nil
    var submitQuestion: ((String, String, String) -> Bool)? = nil
    var submitComposer: ((String, String) -> MacTab5DesktopSubmission)? = nil
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
    case duplicateTitle = "存在完全同名会话，无法确认输入目标；请区分会话标题后重新定位并录音"
    case questionStructure = "当前提问卡片结构尚不兼容，识别文字保留在预览"
    case composerScanLimit = "输入目标检查达到边界，识别文字保留在预览"
    case multipleComposers = "存在多个可用输入框，无法唯一确认目标"
    case navigationTitleMismatch = "前台主页面完整标题尚未匹配目标会话"
    case navigationShell = "未取得 Codex 主页面的可访问导航区域"
    case navigationAddress = "Codex 主页面未提供可验证的会话地址"
    case navigationSelectionWithoutAddress = "已找到当前会话按钮，但尚无可验证的目标会话地址"
    case navigationLimit = "Codex 导航检查达到边界，尚未确认目标会话"
    case value = "未取得可用的输入框文字"
    case readOnly = "主输入框未提供可写的辅助功能接口"
    case changed = "电脑草稿已变化，请保留识别预览"
    case tooLong = "合并后的草稿超过当前长度上限，请保留识别预览"
    case caretUnconfirmed = "文字已回填，但光标末尾定位未确认；请检查电脑后提交"
    case writeUnconfirmed = "主输入框写入未确认，请保留预览并检查电脑"
    case submitReceipt = "发送未执行：没有匹配当前设备和会话的回填记录"
    case submitUsed = "发送未执行：这份回填记录已经使用，请先检查电脑"
    case submitExpired = "发送未执行：回填记录已过期"
    case submitParagraph = "发送未执行：输入框末尾换行表示与回填快照不同"
    case submitConnection = "发送未执行：桌面任务连接当前不可写"
    case submitRejected = "任务连接拒绝了发送，请保留并检查电脑草稿"
    case submitUnknown = "任务发送结果未确认，请检查电脑，不要重复发送"
    case submitButton = "发送未执行：未确认当前输入框唯一且可用的发送、引导或排队按钮"
    case submitActionUnacknowledged = "已尝试发送，但按钮响应及输入框清空均未确认，请检查电脑，不要重发"
    case submitReadbackUnavailable = "已执行发送，但输入框状态暂时无法核验，请检查电脑，不要重发"
    case submitTextRemains = "已执行发送，但原文字仍在输入框，请检查电脑，不要重发"
    case submitReadbackChanged = "已执行发送，但输入框出现其他文字，请检查电脑，不要重发"
    case submitContextChanged = "已尝试发送，但当前窗口或输入目标已变化，请检查电脑，不要重发"
    case submitContainer = "发送未执行：未找到当前输入框所属的正文容器"
    case submitScanLimit = "发送未执行：按钮检查超时或达到节点上限"
    case submitScope = "发送未执行：输入区包含其他编辑器、题卡或不兼容的输入控件"
    case submitEditorMissing = "发送未执行：按钮检查未找到原输入框"
    case submitButtonMissing = "发送未执行：当前输入区没有识别到发送、引导或排队按钮"
    case submitButtonMultiple = "发送未执行：当前输入区存在多个提交按钮"
    case submitButtonDisabled = "发送未执行：当前提交按钮不可用"
    case submitButtonChanged = "发送未执行：提交按钮或输入区在复核时发生变化"
    case submitted = "任务连接已确认发送"
    case messageAccepted = "已确认 Codex 会话新增本次提交的消息"
    case messageAcceptedDraftPending = "已确认 Codex 收到消息，但本地草稿清理未确认；请勿重复发送"
    case messageQueued = "已确认 Codex 保存本次排队消息，等待当前运行结束"
    case messageQueuedDraftPending = "已确认 Codex 保存排队消息，但本地草稿清理未确认；请勿重复发送"
    case composerSubmitted = "电脑输入框已响应提交；发送、引导或排队以桌面当前方式为准"
    case composerSubmittedDraftPending = "电脑已响应提交，但本地草稿清理未确认；请勿重复发送"
    case open = "系统未接受打开会话请求"
    case questionChanged = "当前问题已切换或关闭，识别文字保留在预览"
    case questionConfirmed = "已回填当前问题的回答框，确认后可提交本题"
    case questionSubmitted = "已确认问题卡片执行下一步或发送"
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
    // Keep fixed diagnostic labels only; a later success must not erase the
    // evidence needed to investigate an intermittent staging failure.
    private var lastStageFailure: MacTab5DesktopCheck?
    private var lastSubmitFailure: MacTab5DesktopCheck?
    // Opening a page is safe to repeat after an app restart. Keep its bounded
    // deduplication receipts in memory, without unlocking the private draft key.
    // Editing and submission still persist encrypted receipts before acting.
    private var navigationReceipts: [String: Any] = [:]
    private var located = Set<String>()
    private var voiceTargets: [String: (key: String, time: TimeInterval)] = [:]
    func prepareVoice(task: String, device: String) {
        guard enabled(), codex.isEnabled, let target = backend.target else { return }
        lock.lock(); defer { lock.unlock() }
        let binding = device + "|" + task
        guard located.contains(binding) else { return }
        voiceTargets = voiceTargets.filter { ProcessInfo.processInfo.systemUptime - $0.value.time < 120 }
        voiceTargets[binding] = (target(task) ?? "unavailable", ProcessInfo.processInfo.systemUptime)
    }

    var summary: String {
        guard enabled() else { return "尚未启用" }
        if let failure = backend.preflight() { return failure.rawValue }
        statusLock.lock(); let current = status, detail = statusEvidence, failure = lastStageFailure, submitFailure = lastSubmitFailure; statusLock.unlock()
        return current.rawValue + (detail.isEmpty ? "" : "（" + detail + "）") +
            (failure.map { "；最近回填失败：" + $0.rawValue } ?? "") +
            (submitFailure.map { "；最近提交失败：" + $0.rawValue } ?? "")
    }
    private func updateStatus(_ value: MacTab5DesktopCheck, navigation: Bool = false) {
        // Evidence belongs to the last navigation traversal, not a later
        // composer check (which may have failed after navigation succeeded).
        let detail = navigation ? backend.evidence() : ""
        statusLock.lock(); status = value; statusEvidence = detail
        if [.messageAccepted, .messageAcceptedDraftPending, .messageQueued, .messageQueuedDraftPending, .composerSubmitted, .composerSubmittedDraftPending, .questionSubmitted, .submitted].contains(value) {
            lastSubmitFailure = nil
        }
        statusLock.unlock()
    }
    private func stageFailed(_ value: MacTab5DesktopCheck) {
        updateStatus(value)
        statusLock.lock(); lastStageFailure = value; statusLock.unlock()
    }
    private func submitFailed(_ value: MacTab5DesktopCheck) {
        updateStatus(value)
        statusLock.lock(); lastSubmitFailure = value; statusLock.unlock()
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
            let stageTarget = op == "stage-draft" ? backend.target?(task) : nil
            if op == "stage-draft", backend.target != nil {
                guard let stageTarget else {
                    stageFailed(backend.diagnostic()); return .error(503, "composer_not_ready")
                }
                let captured = voiceTargets[binding]
                guard captured == nil ||
                    (ProcessInfo.processInfo.systemUptime - captured!.time < 120 && captured!.key == stageTarget) else {
                    stageFailed(.questionChanged); return .error(503, "composer_not_ready")
                }
            }
            if op == "submit-draft", !codex.prepareMutation(task: task) { submitFailed(.submitConnection); return .error(403, "codex_read_only_connection") }
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
                        if located.count < 512 { located.insert(binding) }
                        if let current { body["draftEmpty"] = current.isEmpty }
                        result = .init(200, body)
                    } else { updateStatus(opened ? backend.diagnostic() : .open, navigation: opened); result = .error(409, "desktop_foreground_unconfirmed") }
                }
            } else if op == "stage-draft" {
                let target = stageTarget
                let question = target?.hasPrefix("question:") == true
                var current = question ? nil : backend.read(task)
                if current == nil, !text.isEmpty, backend.focusComposer?(task) == true { current = backend.read(task) }
                if let current, !text.isEmpty {
                    let empty = current.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                    // Continue dictation in the current paragraph. Injecting a
                    // newline here can be normalized by inline question editors.
                    // Keep existing and recognized text exact; only our join changes.
                    let combined = empty ? text : current + " " + text
                    func write() -> String? {
                        if let target, let write = backend.writeTarget { return write(task, target, current, combined) }
                        return backend.write(task, current, combined)
                    }
                    if combined.utf8.count <= 2000, let observed = write() {
                        // Question answers belong only to the encrypted desktop
                        // receipt, never the ordinary conversation draft store.
                        let saved = question ? MacTab5RPCResult(200, [:]) : codex.handle(["op": "draft-save", "taskId": task, "requestId": id, "message": observed], device: device)
                        if saved.status == 200 { updateStatus(backend.diagnostic() == .caretUnconfirmed ? .caretUnconfirmed : question ? .questionConfirmed : .confirmed); record["target"] = target; record["stagedText"] = observed; record["stage"] = true; body["staged"] = true; body["appended"] = !empty; result = .init(200, body) }
                        else { result = saved }
                    } else { stageFailed(combined.utf8.count > 2000 ? .tooLong : backend.diagnostic()); result = .error(409, "composer_changed") }
                } else { stageFailed(backend.diagnostic()); result = .error(503, "composer_not_ready") }
            } else if op == "clear-draft" {
                let target = backend.target?(task)
                if let current = backend.read(task),
                   (target.flatMap { backend.writeTarget?(task, $0, current, "") } ?? (backend.target == nil ? backend.write(task, current, "") : nil)) != nil {
                    let saved = target?.hasPrefix("question:") == true ? MacTab5RPCResult(200, [:]) : codex.handle(["op": "draft-save", "taskId": task, "requestId": id, "message": ""], device: device)
                    if saved.status == 200 { body["cleared"] = true; result = .init(200, body) } else { result = saved }
                } else { result = .error(409, "composer_changed") }
            } else {
                func submit() throws -> MacTab5RPCResult {
                    guard let draft = records[draftID.lowercased()] as? [String: Any], draft["binding"] as? String == binding,
                          let staged = draft["stagedText"] as? String, !staged.isEmpty else { submitFailed(.submitReceipt); return .error(409, "composer_changed") }
                    guard draft["stage"] as? Bool == true else { submitFailed(.submitUsed); return .error(409, "composer_changed") }
                    guard let created = draft["createdAt"] as? Double, Date().timeIntervalSince1970 - created <= 900 else { submitFailed(.submitExpired); return .error(409, "composer_changed") }
                    let target = draft["target"] as? String
                    guard backend.target == nil || (target != nil && backend.target?(task) == target) else {
                        submitFailed(.questionChanged); return .error(409, "composer_changed")
                    }
                    guard let current = backend.read(task) else { submitFailed(backend.diagnostic()); return .error(409, "composer_changed") }
                    guard current == staged else {
                        submitFailed(MacTab5DesktopAX.matchesReadback(current, expected: staged) ? .submitParagraph : .changed)
                        return .error(409, "composer_changed")
                    }
                    let baseline = target == "main" && backend.submitComposer != nil ? codex.desktopSubmissionReceipt(task: task, expected: staged) : nil
                    // Consume before touching the composer. A repeated click can
                    // only query this delivery receipt, never send another turn.
                    var consumed = draft; consumed["stage"] = false; records[draftID.lowercased()] = consumed
                    try codex.store.write("desktop-receipts", MacTab5Protocol.json(records))
                    if let target, target.hasPrefix("question:") {
                        guard backend.submitQuestion?(task, target, staged) == true else {
                            submitFailed(.submitUnknown); return .error(504, "desktop_result_unknown")
                        }
                        updateStatus(.questionSubmitted); body["submitted"] = true; return .init(200, body)
                    }
                    if let nativeSubmit = backend.submitComposer {
                        switch nativeSubmit(task, staged) {
                        case .notReady:
                            submitFailed(backend.diagnostic()); return .error(409, "composer_changed")
                        case .unconfirmed:
                            let failure = backend.diagnostic()
                            if let baseline {
                                // Read-only reconciliation with new accepted input;
                                // never issue another native press or IPC send.
                                for attempt in 0..<2 {
                                    if let after = codex.desktopSubmissionReceipt(task: task, expected: staged),
                                       after.confirms(after: baseline) || after.confirmsQueue(after: baseline) {
                                        let cleared = codex.clearSubmittedDesktopDraft(task: task, device: device, expected: staged)
                                        if after.confirms(after: baseline) { updateStatus(cleared ? .messageAccepted : .messageAcceptedDraftPending) }
                                        else { updateStatus(cleared ? .messageQueued : .messageQueuedDraftPending) }
                                        body["submitted"] = true; return .init(200, body)
                                    }
                                    if attempt == 0 { Thread.sleep(forTimeInterval: 0.15) }
                                }
                            }
                            submitFailed(failure); return .error(504, "desktop_result_unknown")
                        case .confirmed:
                            let cleared = codex.clearSubmittedDesktopDraft(task: task, device: device, expected: staged)
                            updateStatus(cleared ? .composerSubmitted : .composerSubmittedDraftPending)
                            body["submitted"] = true; return .init(200, body)
                        }
                    }
                    if let cleared = backend.write(task, staged, "") {
                        let sent = codex.handle(["op": "send", "taskId": task, "requestId": id, "message": staged], device: device)
                        if sent.status == 202 { updateStatus(.submitted); body["submitted"] = true; return .init(200, body) }
                        _ = backend.write(task, cleared, staged)
                        submitFailed(sent.status == 504 ? .submitUnknown : .submitRejected); return sent
                    }
                    submitFailed(backend.diagnostic()); return .error(409, "composer_changed")
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
    static var backend: MacTab5DesktopBackend { .init(open: { task in MacTab5ComputerPlatform.main { guard let url = URL(string: "codex://threads/" + task) else { return false }; return NSWorkspace.shared.open(url) } }, read: read, write: { write($0, $1, $2, target: "main") }, diagnostic: diagnostic, preflight: { AXIsProcessTrusted() ? nil : .permission }, navigation: navigation, evidence: evidence, openOnly: openOnly, focusComposer: focusComposer, target: target, writeTarget: { write($0, $2, $3, target: $1) }, submitQuestion: submitQuestion, submitComposer: submitComposer) }
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
    // Bind the main ComposerLayoutBody or an explicitly validated question
    // request-card to its own primary document. Neither can borrow identity
    // from a sidebar row, dialog or another web area.
    static func structuredComposerCheck(classes: [String], containers: [ComposerContainer], task: String,
                                        uniqueFullTitle: String?, resources: URL?, question: Bool = false,
                                        duplicateTitle: Bool = false) -> MacTab5DesktopCheck {
        guard classes.contains("ProseMirror") else { return .identifier }
        let cards = containers.filter { $0.classes.contains("@container/request-card") }.count
        guard cards == (question ? 1 : 0),
              !containers.contains(where: { ["AXDialog", "AXSheet", "AXTextArea", "AXTextField"].contains($0.role) }),
              let pageIndex = containers.firstIndex(where: { $0.role == "AXWebArea" }),
              containers.filter({ $0.role == "AXWebArea" }).count == 1,
              containers[..<pageIndex].contains(where: { question ? $0.classes.contains("@container/request-card") : $0.classes.contains(where: { $0.hasPrefix("_ComposerLayoutBody_") }) }) else { return .structure }
        let page = containers[pageIndex]
        guard trustedShell(page.address, resourceRoot: resources) else { return .route }
        // An exact live address wins over a stale/matching title. In particular,
        // a different task's route cannot be rescued by title equality.
        if let address = page.address, let parsed = URLComponents(string: address),
           let last = (parsed.fragment ?? parsed.path).split(separator: "/").last,
           let routed = MacTab5Codex.id(String(last)), identifies(address, task: routed, resourceRoot: resources) {
            return routed == task ? .confirmed : .route
        }
        if uniqueFullTitle == nil, duplicateTitle { return .duplicateTitle }
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
        return checkedComposer(task, editor: editor, window: window, app: app, root: root, requireFocus: true)
    }
    private static func checkedComposer(_ task: String, editor: AXUIElement, window: AXUIElement,
                                        app: NSRunningApplication, root: AXUIElement, requireFocus: Bool) -> AXUIElement? {
        func reject(_ check: MacTab5DesktopCheck) -> AXUIElement? { note(check); return nil }
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
        var cards: [AXUIElement] = []
        for _ in 0..<32 {
            if let url = address(node), identifies(url, task: task, resourceRoot: resources) { routeConfirmed = true }
            if CFEqual(node, window) { foundWindow = true; break }
            guard let parent = value(node, "AXParent"), CFGetTypeID(parent) == AXUIElementGetTypeID() else { break }; node = unsafeBitCast(parent, to: AXUIElement.self)
            let role = value(node, "AXRole") as? String ?? ""
            containers.append(.init(role: role, classes: value(node, "AXDOMClassList") as? [String] ?? [],
                address: address(node), title: role == "AXWebArea" ? value(node, "AXTitle") as? String : nil))
            if role == "AXWebArea", let snapshot = containers.last { pages.append((node, snapshot.address, snapshot.title)) }
            if containers.last?.classes.contains("@container/request-card") == true { cards.append(node) }
        }
        guard foundWindow else { return reject(.route) }
        var check = identityCheck(identifier: identifier, routeConfirmed: routeConfirmed)
        let editorClasses = value(editor, "AXDOMClassList") as? [String] ?? []
        if editorClasses.contains("ProseMirror") {
            // Only a full, globally unique catalog name qualifies here. Compact
            // navigation labels and open-only acknowledgments cannot edit text.
            let identity = MacCodexNavigationTitle.identity(task)
            let fullTitle: String?
            if case .unique(let title) = identity { fullTitle = title } else { fullTitle = nil }
            check = structuredComposerCheck(classes: editorClasses,
                containers: containers, task: task, uniqueFullTitle: fullTitle, resources: resources,
                question: !cards.isEmpty, duplicateTitle: identity == .duplicate)
            if !cards.isEmpty, cards.count != 1 || questionInfo(cards[0]) == nil { return reject(.questionStructure) }
            if check == .confirmed, MacCodexNavigationTitle.identity(task) != identity { return reject(.route) }
            guard pages.allSatisfy({ address($0.element) == $0.address && value($0.element, "AXTitle") as? String == $0.title }) else { return reject(.route) }
        }
        guard check == .confirmed else { return reject(check) }
        guard MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
              let currentWindow = value(root, "AXFocusedWindow"), CFEqual(currentWindow, window),
              (value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { return reject(.focus) }
        if requireFocus {
            guard let currentEditor = value(root, "AXFocusedUIElement"), CFEqual(currentEditor, editor) else { return reject(.focus) }
        }
        note(.confirmed); return editor
    }
    private static func focusComposer(_ task: String) -> Bool {
        guard AXIsProcessTrusted() else { note(.permission); return false }
        guard let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }),
              app.bundleIdentifier == "com.openai.codex" else { note(.frontmost); return false }
        let root = AXUIElementCreateApplication(app.processIdentifier)
        AXUIElementSetMessagingTimeout(root, 0.1)
        guard let raw = value(root, "AXFocusedWindow"), CFGetTypeID(raw) == AXUIElementGetTypeID() else { note(.focus); return false }
        let window = unsafeBitCast(raw, to: AXUIElement.self)
        guard (value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { note(.route); return false }
        guard let editor = findTarget(task, window: window, app: app, root: root) else { return false }
        guard checkedComposer(task, editor: editor, window: window, app: app, root: root, requireFocus: false) != nil else { return false }
        guard let expectedTarget = targetKey(editor) else { note(.questionChanged); return false }
        let previousFocus = value(root, "AXFocusedUIElement")
        guard AXUIElementSetAttributeValue(editor, "AXFocused" as CFString, kCFBooleanTrue) == .success else { note(.focus); return false }
        // AX acknowledges the request before the renderer necessarily exposes
        // its new focus. Observe the same editor/window/target; never replay
        // focus, text or submit, or follow a user to a different input.
        return settleFocus(verify: {
            guard checkedComposer(task, editor: editor, window: window, app: app, root: root, requireFocus: false) != nil else { return nil }
            guard targetKey(editor) == expectedTarget else { note(.questionChanged); return nil }
            guard let focused = value(root, "AXFocusedUIElement") else { note(.focus); return false }
            if CFEqual(focused, editor) { return true }
            note(.focus)
            return previousFocus.map { CFEqual(focused, $0) } == true ? false : nil
        }, wait: { Thread.sleep(forTimeInterval: $0) })
    }
    // true = confirmed, false = still on the original focus, nil = invalidated.
    static func settleFocus(verify: () -> Bool?, wait: (TimeInterval) -> Void,
                            clock: () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) -> Bool {
        let deadline = clock() + 0.5
        for attempt in 0..<11 {
            guard clock() < deadline, let ready = verify() else { return false }
            if ready { return clock() < deadline }
            let remaining = deadline - clock()
            guard attempt < 10, remaining > 0 else { return false }
            wait(min(0.05, remaining))
        }
        return false
    }
    static func uniqueComposer<Node>(in root: Node, role: (Node) -> String, children: (Node) -> [Node],
                                     trustedPage: (Node) -> Bool, validate: (Node) -> Bool,
                                     priority: (Node) -> Int = { _ in 0 },
                                     failure: (MacTab5DesktopCheck) -> Void = { _ in },
                                     clock: () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) -> Node? {
        let deadline = clock() + 0.75
        var pending = MacNavigationQueue((root, false)), found: Node?, visited = 0, best = -1, matches = 0
        while pending.hasPending {
            guard visited < 1200, clock() < deadline, let (node, inherited) = pending.next() else { failure(.composerScanLimit); return nil }
            visited += 1
            let kind = role(node)
            if ["AXDialog", "AXSheet", "AXTextField", "AXStaticText", "AXImage"].contains(kind) { continue }
            // Nested browsers are separate documents even if their URL looks
            // similar. Only an outer app web area can host the main composer.
            if kind == "AXWebArea", inherited || !trustedPage(node) { continue }
            let trusted = inherited || kind == "AXWebArea"
            if kind == "AXTextArea" {
                if trusted && validate(node) {
                    let rank = priority(node)
                    if rank > best { best = rank; found = node; matches = 1 }
                    else if rank == best { matches += 1 }
                }
                continue
            }
            pending.append(contentsOf: children(node).map { ($0, trusted) })
        }
        if matches > 1 { failure(.multipleComposers) }
        return matches == 1 ? found : nil
    }
    private static let targetSession = UUID().uuidString
    struct QuestionSignature {
        let text: [String]
        let action: String
    }
    static func questionSignature(text: [String], buttons: [String], editors: Int) -> QuestionSignature? {
        let actions = buttons.filter { ["Send", "Send update", "Next", "发送", "发送更新", "下一步"].contains($0) }
        guard editors == 1, actions.count == 1,
              text.contains(where: { ["Question", "问题"].contains($0) }), text.count >= 2,
              text.reduce(0, { $0 + $1.utf8.count }) <= 8192 else { return nil }
        return .init(text: text, action: actions[0])
    }
    private static func questionCard(_ editor: AXUIElement) -> AXUIElement? {
        var node = editor
        for _ in 0..<32 {
            if (value(node, "AXDOMClassList") as? [String] ?? []).contains("@container/request-card") { return node }
            if ["AXWebArea", "AXWindow"].contains(value(node, "AXRole") as? String ?? "") { return nil }
            guard let parent = value(node, "AXParent"), CFGetTypeID(parent) == AXUIElementGetTypeID() else { return nil }
            node = unsafeBitCast(parent, to: AXUIElement.self)
        }
        return nil
    }
    private static func buttonLabel(_ node: AXUIElement) -> String {
        let names = [value(node, "AXTitle") as? String, value(node, "AXDescription") as? String].compactMap { $0 }.filter { !$0.isEmpty }
        return Set(names).count == 1 ? names[0] : ""
    }
    private static func questionInfo(_ card: AXUIElement) -> (signature: QuestionSignature, button: AXUIElement)? {
        var pending = [card], visited = 0, editors = 0, text: [String] = [], buttons: [(String, AXUIElement)] = []
        let deadline = ProcessInfo.processInfo.systemUptime + 0.35
        while let node = pending.popLast() {
            visited += 1
            guard visited <= 300, ProcessInfo.processInfo.systemUptime < deadline else { return nil }
            let role = value(node, "AXRole") as? String ?? ""
            if ["AXDialog", "AXSheet", "AXWebArea", "AXSecureTextField", "AXTextField"].contains(role) { return nil }
            if role == "AXTextArea" { editors += 1; continue } // Never hash answer text or placeholder.
            if role == "AXButton" { buttons.append((buttonLabel(node), node)); continue }
            if role == "AXStaticText", let content = value(node, "AXValue") as? String, !content.isEmpty {
                text.append(content)
                guard text.reduce(0, { $0 + $1.utf8.count }) <= 8192 else { return nil }
            }
            pending.append(contentsOf: value(node, "AXChildren") as? [AXUIElement] ?? [])
        }
        guard let signature = questionSignature(text: text, buttons: buttons.map { $0.0 }, editors: editors),
              let button = buttons.first(where: { $0.0 == signature.action })?.1 else { return nil }
        return (signature, button)
    }
    private static func targetKey(_ editor: AXUIElement) -> String? {
        guard let card = questionCard(editor) else { return "main" }
        guard let info = questionInfo(card), let bytes = try? MacTab5Protocol.json([
            "session": targetSession, "card": String(CFHash(card)), "editor": String(CFHash(editor)),
            "question": info.signature.text, "action": info.signature.action
        ]) else { return nil }
        return "question:" + MacTab5Protocol.sha256(bytes)
    }
    private static func findTarget(_ task: String, window: AXUIElement, app: NSRunningApplication, root: AXUIElement) -> AXUIElement? {
        let resources = app.bundleURL?.appendingPathComponent("Contents/Resources")
        var invalidQuestion = false
        var rejection: MacTab5DesktopCheck?, scanFailure: MacTab5DesktopCheck?
        let editor = uniqueComposer(in: window, role: { value($0, "AXRole") as? String ?? "" },
            children: { node in
                if (value(node, "AXDOMClassList") as? [String] ?? []).contains("@container/request-card"), questionInfo(node) == nil {
                    invalidQuestion = true; return []
                }
                return value(node, "AXChildren") as? [AXUIElement] ?? []
            },
            trustedPage: { trustedShell(address($0), resourceRoot: resources) },
            validate: { node in
                if let card = questionCard(node), questionInfo(card) == nil { invalidQuestion = true; return false }
                let valid = checkedComposer(task, editor: node, window: window, app: app, root: root, requireFocus: false) != nil
                if !valid {
                    let reason = diagnostic()
                    if rejection == nil || reason == .duplicateTitle { rejection = reason }
                }
                return valid
            }, priority: { questionCard($0) == nil ? 0 : 1 }, failure: { scanFailure = $0 })
        guard !invalidQuestion else { note(.questionStructure); return nil }
        guard let editor else { note(scanFailure ?? rejection ?? .structure); return nil }
        return editor
    }
    private static func target(_ task: String) -> String? {
        guard AXIsProcessTrusted() else { note(.permission); return nil }
        guard let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }),
              app.bundleIdentifier == "com.openai.codex" else { note(.frontmost); return nil }
        let root = AXUIElementCreateApplication(app.processIdentifier); AXUIElementSetMessagingTimeout(root, 0.1)
        guard let raw = value(root, "AXFocusedWindow"), CFGetTypeID(raw) == AXUIElementGetTypeID() else { note(.focus); return nil }
        let window = unsafeBitCast(raw, to: AXUIElement.self)
        guard (value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { note(.route); return nil }
        guard let editor = findTarget(task, window: window, app: app, root: root) else { return nil }
        guard let key = targetKey(editor) else { note(.questionChanged); return nil }
        return key
    }
    private static func submitQuestion(_ task: String, _ expectedTarget: String, _ expectedText: String) -> Bool {
        guard expectedTarget.hasPrefix("question:"), let editor = composer(task), targetKey(editor) == expectedTarget,
              textValue(editor) == expectedText, let card = questionCard(editor), let info = questionInfo(card),
              value(info.button, "AXEnabled") as? Bool == true,
              let current = composer(task), CFEqual(current, editor), targetKey(current) == expectedTarget,
              textValue(current) == expectedText else { return false }
        // A separate explicit submit consumes its encrypted receipt before this
        // one native action. Never send a normal turn, clear the answer, press
        // Enter globally, select an option, or replay an uncertain action.
        guard AXUIElementPerformAction(info.button, kAXPressAction as CFString) == .success else { return false }
        for _ in 0..<10 {
            Thread.sleep(forTimeInterval: 0.08)
            // An advance/removal or Send -> Send update transition is local UI
            // acknowledgment only, never a claim that model execution finished.
            if let after = target(task), after != expectedTarget { return true }
        }
        return false
    }
    static func isComposerSubmitLabel(_ label: String) -> Bool {
        ["Send", "Steer", "Queue", "发送", "引导", "排队"].contains(label)
    }
    // The presentational outer Root can be omitted from the accessibility tree.
    // Body is already required by checkedComposer and contains both the editor
    // and its adaptive footer controls in the supported desktop layout.
    static func composerContainer<Node>(_ editor: Node, role: (Node) -> String,
                                         classes: (Node) -> [String], parent: (Node) -> Node?) -> Node? {
        var node = editor
        for _ in 0..<32 {
            guard !["AXWebArea", "AXWindow", "AXDialog", "AXSheet"].contains(role(node)),
                  !classes(node).contains("@container/request-card") else { return nil }
            if classes(node).contains(where: { $0.hasPrefix("_ComposerLayoutBody_") }) { return node }
            guard let next = parent(node) else { return nil }
            node = next
        }
        return nil
    }
    private static func composerBody(_ editor: AXUIElement) -> AXUIElement? {
        composerContainer(editor, role: { value($0, "AXRole") as? String ?? "" },
            classes: { value($0, "AXDOMClassList") as? [String] ?? [] }, parent: {
                guard let parent = value($0, "AXParent"), CFGetTypeID(parent) == AXUIElementGetTypeID() else { return nil }
                return unsafeBitCast(parent, to: AXUIElement.self)
            })
    }
    // Only search the already verified editor's own Body, never its wider Root
    // or page. Another composer body or request card inside it is incompatible.
    private static func composerSubmitButton(_ body: AXUIElement, editor: AXUIElement) -> AXUIElement? {
        uniqueSubmitButton(in: body, role: { value($0, "AXRole") as? String ?? "" },
            children: { value($0, "AXChildren") as? [AXUIElement] ?? [] },
            prohibited: {
                let classes = value($0, "AXDOMClassList") as? [String] ?? []
                return classes.contains("@container/request-card") || (!CFEqual($0, body) &&
                    classes.contains(where: { $0.hasPrefix("_ComposerLayoutBody_") }))
            }, isEditor: { CFEqual($0, editor) }, label: buttonLabel,
            enabled: { value($0, "AXEnabled") as? Bool == true }, failure: note)
    }
    static func uniqueSubmitButton<Node>(in root: Node, role: (Node) -> String, children: (Node) -> [Node],
                                         prohibited: (Node) -> Bool, isEditor: (Node) -> Bool,
                                         label: (Node) -> String, enabled: (Node) -> Bool,
                                         clock: () -> TimeInterval = { ProcessInfo.processInfo.systemUptime },
                                         failure: (MacTab5DesktopCheck) -> Void = { _ in }) -> Node? {
        func reject(_ reason: MacTab5DesktopCheck) -> Node? { failure(reason); return nil }
        let deadline = clock() + 0.35
        var pending = [root], visited = 0, editors = 0, buttons: [Node] = []
        while let node = pending.popLast() {
            visited += 1
            guard visited <= 300, clock() < deadline else { return reject(.submitScanLimit) }
            guard !prohibited(node) else { return reject(.submitScope) }
            let kind = role(node)
            guard !["AXWebArea", "AXWindow", "AXDialog", "AXSheet", "AXSecureTextField", "AXTextField"].contains(kind) else { return reject(.submitScope) }
            if kind == "AXTextArea" { guard isEditor(node) else { return reject(.submitScope) }; editors += 1; continue }
            if kind == "AXButton" {
                if isComposerSubmitLabel(label(node)) {
                    guard enabled(node) else { return reject(.submitButtonDisabled) }
                    buttons.append(node)
                }
                continue
            }
            pending.append(contentsOf: children(node))
        }
        guard editors == 1 else { return reject(.submitEditorMissing) }
        guard !buttons.isEmpty else { return reject(.submitButtonMissing) }
        guard buttons.count == 1 else { return reject(.submitButtonMultiple) }
        return buttons[0]
    }
    private static func submitComposer(_ task: String, _ expectedText: String) -> MacTab5DesktopSubmission {
        func reject(_ reason: MacTab5DesktopCheck) -> MacTab5DesktopSubmission { note(reason); return .notReady }
        guard !expectedText.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return reject(.value) }
        guard let editor = composer(task) else { return .notReady }
        guard targetKey(editor) == "main" else { return reject(.questionChanged) }
        guard textValue(editor) == expectedText else { return reject(.changed) }
        guard let container = composerBody(editor) else { return reject(.submitContainer) }
        guard let button = composerSubmitButton(container, editor: editor) else { return .notReady }
        guard let app = MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication }),
              app.bundleIdentifier == "com.openai.codex" else { return reject(.frontmost) }
        let root = AXUIElementCreateApplication(app.processIdentifier); AXUIElementSetMessagingTimeout(root, 0.1)
        guard let raw = value(root, "AXFocusedWindow"), CFGetTypeID(raw) == AXUIElementGetTypeID() else { return reject(.focus) }
        let window = unsafeBitCast(raw, to: AXUIElement.self), label = buttonLabel(button)
        guard checkedComposer(task, editor: editor, window: window, app: app, root: root, requireFocus: true) != nil else { return .notReady }
        guard targetKey(editor) == "main" else { return reject(.questionChanged) }
        guard textValue(editor) == expectedText else { return reject(.changed) }
        guard let currentContainer = composerBody(editor), CFEqual(currentContainer, container) else { return reject(.submitButtonChanged) }
        guard let currentButton = composerSubmitButton(container, editor: editor) else { return .notReady }
        guard CFEqual(currentButton, button), buttonLabel(button) == label, isComposerSubmitLabel(label) else { return reject(.submitButtonChanged) }
        guard value(button, "AXEnabled") as? Bool == true else { return reject(.submitButtonDisabled) }
        // The caller consumed its receipt before entry. Respect the displayed
        // Send/Steer/Queue behavior once; never clear first, send Enter globally,
        // fall back to IPC or replay an unconfirmed native action.
        let actionAcknowledged = AXUIElementPerformAction(button, kAXPressAction as CFString) == .success
        // AX timeout does not prove that the renderer ignored the press. Observe
        // the same editor after either action result, without a second press.
        let failure = confirmSubmission(actionAcknowledged: actionAcknowledged, observe: {
            guard MacTab5ComputerPlatform.main({ NSWorkspace.shared.frontmostApplication?.processIdentifier }) == app.processIdentifier,
                  let currentWindow = value(root, "AXFocusedWindow"), CFEqual(currentWindow, window),
                  (value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { return .contextChanged }
            guard checkedComposer(task, editor: editor, window: window, app: app, root: root, requireFocus: false) != nil else {
                // Renderer/AX snapshots can be temporarily incomplete during a
                // successful transition. An observation is safe to repeat.
                return .unavailable
            }
            guard targetKey(editor) == "main", let body = composerBody(editor), CFEqual(body, container) else { return .contextChanged }
            guard let text = textValue(editor) else { return .unavailable }
            if text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty { return .empty }
            return text == expectedText ? .unchanged : .changed
        }, wait: { Thread.sleep(forTimeInterval: $0) })
        if let failure { note(failure); return .unconfirmed }
        return .confirmed
    }
    enum SubmissionObservation { case unavailable, unchanged, empty, changed, contextChanged }
    // nil means two consecutive verified empty observations. Native action
    // acknowledgment alone is never enough, including when it returned success.
    static func confirmSubmission(actionAcknowledged: Bool, observe: () -> SubmissionObservation,
                                  wait: (TimeInterval) -> Void,
                                  clock: () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) -> MacTab5DesktopCheck? {
        let deadline = clock() + 2.5
        var empty = 0
        var failure: MacTab5DesktopCheck = actionAcknowledged ? .submitReadbackUnavailable : .submitActionUnacknowledged
        for attempt in 0..<32 {
            guard clock() < deadline else { return failure }
            let observation = observe()
            guard clock() < deadline else { return failure }
            switch observation {
            case .contextChanged: return .submitContextChanged
            case .changed: return .submitReadbackChanged
            case .unavailable:
                empty = 0
                failure = actionAcknowledged ? .submitReadbackUnavailable : .submitActionUnacknowledged
            case .unchanged:
                empty = 0
                failure = actionAcknowledged ? .submitTextRemains : .submitActionUnacknowledged
            case .empty:
                empty += 1
                failure = .submitReadbackUnavailable
                if empty == 2 { return nil }
            }
            let remaining = deadline - clock()
            guard attempt < 31, remaining > 0 else { return failure }
            wait(min(0.08, remaining))
        }
        return failure
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
    static func caretEnd(_ text: String) -> CFRange { CFRange(location: text.utf16.count, length: 0) }
    private static func placeCaretAtEnd(_ task: String, text: String, target: String?) -> Bool {
        guard let editor = composer(task), target == nil || targetKey(editor) == target,
              textValue(editor) == text else { return false }
        var range = caretEnd(text)
        guard let selection = AXValueCreate(.cfRange, &range),
              AXUIElementSetAttributeValue(editor, "AXSelectedTextRange" as CFString, selection) == .success,
              let current = composer(task), CFEqual(current, editor),
              target == nil || targetKey(current) == target, textValue(current) == text,
              let raw = value(editor, "AXSelectedTextRange"), CFGetTypeID(raw) == AXValueGetTypeID() else { return false }
        var observed = CFRange(location: -1, length: -1)
        return AXValueGetValue(unsafeBitCast(raw, to: AXValue.self), .cfRange, &observed) &&
            observed.location == range.location && observed.length == 0
    }
    private static func write(_ task: String, _ expected: String, _ next: String, target: String? = nil) -> String? {
        guard let editor = composer(task) else { return nil }
        guard target == nil || targetKey(editor) == target else { note(.questionChanged); return nil }
        guard textValue(editor) == expected else { note(.changed); return nil }
        guard next.utf8.count <= 2000 else { note(.tooLong); return nil }
        var settable = DarwinBoolean(false)
        guard AXUIElementIsAttributeSettable(editor, "AXValue" as CFString, &settable) == .success, settable.boolValue else { note(.readOnly); return nil }
        // A provider can acknowledge late or fail after applying the change.
        // Reacquire and verify the exact target, but never repeat this write.
        _ = AXUIElementSetAttributeValue(editor, "AXValue" as CFString, next as CFString)
        guard let observed = confirmWrite(next, read: {
            guard let current = composer(task), target == nil || targetKey(current) == target else { return nil }
            return textValue(current)
        }, wait: { Thread.sleep(forTimeInterval: 0.08) }) else { note(.writeUnconfirmed); return nil }
        // Position only after confirmed text; never alter another target or
        // invalidate an already successful write just because caret AX is absent.
        if !placeCaretAtEnd(task, text: observed, target: target) { note(.caretUnconfirmed) }
        return observed
    }
}
