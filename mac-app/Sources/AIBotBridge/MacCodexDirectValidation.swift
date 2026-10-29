import Foundation

enum MacCodexDirectValidation {
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ value: @autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("Codex direct: " + name) }; count += 1
        }
        for path in ["/Users/test/.openclaw/workspace", "/Users/test/.openclaw/workspace/subproject", "/Users/test/.openclaw/workshop-skills"] {
            try check("exclude integration workspace", MacTab5Codex.isOpenClawWorkspace(path))
        }
        for path in ["/Users/test/Work/OpenClaw", "/Users/test/.openclaw-backup/project", "/Users/test/.openclaw/../Work", "/fixture"] {
            try check("preserve ordinary Codex projects", !MacTab5Codex.isOpenClawWorkspace(path))
        }
        let task = UUID().uuidString.lowercased(), external = UUID().uuidString.lowercased()
        var listFails = false, focus = false, canFocus = true, prepares = 0, writes = 0, sends = 0, value = "existing draft"
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 1, count: 32) }), enabled: { true }, sendEnabled: { true }, call: { method, _ in
            if method == "thread/list" {
                if listFails { throw MacTab5CodexFailure.protocolError }
                return ["data": [["id": external, "cwd": "/Users/test/.openclaw/workspace", "updatedAt": 999],
                                 ["id": task, "cwd": "/Users/test/Work", "name": "更新 OpenClaw 到最新版本", "updatedAt": 1]]]
            }
            sends += 1
            throw MacTab5CodexFailure.protocolError
        })
        codex.refresh()
        try check("newer integration cannot be default", (codex.snapshot()?["tasks"] as? [[String: Any]])?.first?["id"] as? String == task)
        try check("integration unavailable to direct operations", !codex.contains(external) && codex.taskCatalog().count == 1)
        listFails = true; codex.refresh()
        try check("failed refresh retains eligible catalog", codex.contains(task) && !codex.contains(external))
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: .init(
            open: { _ in true }, read: { _ in focus ? value : nil },
            write: { _, expected, next in guard focus, value == expected else { return nil }; value = next; writes += 1; return next },
            diagnostic: { .role }, navigation: { _ in true }, focusComposer: { target in
                prepares += 1; focus = canFocus && target == task; return focus
            }))
        func request(_ op: String, text: String = "preview") -> [String: Any] {
            ["op": op, "taskId": task, "requestId": UUID().uuidString.lowercased(), "text": text]
        }
        try check("navigation succeeds with unfocused composer", desktop.handle(request("open-recent"), device: "fixture").status == 200)
        try check("opening alone does not acquire composer", prepares == 0 && writes == 0)
        let stage = request("stage-draft")
        try check("explicit staging reacquires composer", desktop.handle(stage, device: "fixture").status == 200)
        try check("append preserves existing draft", value == "existing draft preview" && prepares == 1 && writes == 1 && sends == 0)
        try check("same request does not append twice", desktop.handle(stage, device: "fixture").status == 200 && writes == 1 && prepares == 1)
        focus = false; canFocus = false
        try check("wrong target preserves preview and draft", desktop.handle(request("stage-draft"), device: "fixture").body["error"] as? String == "composer_not_ready" && writes == 1 && sends == 0)
        try check("clearing cannot acquire another editor", desktop.handle(request("clear-draft"), device: "fixture").status == 409 && prepares == 2)
        try check("empty preview cannot acquire editor", desktop.handle(request("stage-draft", text: ""), device: "fixture").status == 503 && prepares == 2)
        var submit = request("submit-draft"); submit["draftRequestId"] = stage["requestId"]
        try check("submission still requires exact focused draft", desktop.handle(submit, device: "fixture").status == 409 && prepares == 2 && writes == 1 && sends == 0)
        canFocus = true
        try check("next explicit stage recovers", desktop.handle(request("stage-draft"), device: "fixture").status == 200 && writes == 2)
        try check("success preserves fixed failure diagnostic", desktop.summary.contains("最近回填失败：" + MacTab5DesktopCheck.role.rawValue))

        var now = 0.0, observations = 0, waits = 0
        func delayedFocus() -> Bool {
            MacTab5DesktopAX.settleFocus(verify: {
                observations += 1
                return observations >= 3
            }, wait: { now += $0; waits += 1 }, clock: { now })
        }
        // Acknowledged focus remains asynchronous: the first explicit request
        // must stage exactly once without asking the user to click again.
        let delayed = MacTab5Desktop(codex: codex, enabled: { true }, backend: .init(
            open: { _ in true }, read: { _ in focus ? value : nil },
            write: { _, before, after in guard focus, value == before else { return nil }; value = after; writes += 1; return after },
            focusComposer: { _ in prepares += 1; focus = delayedFocus(); return focus }))
        focus = false
        let delayedStage = request("stage-draft", text: "delayed focus")
        let oldWrites = writes, oldPrepares = prepares
        try check("first staging waits for renderer focus", delayed.handle(delayedStage, device: "fixture").status == 200 && observations == 3 && waits == 2)
        try check("settling never repeats focus or write", prepares == oldPrepares + 1 && writes == oldWrites + 1 && sends == 0)
        try check("repeated delayed request only returns receipt", delayed.handle(delayedStage, device: "fixture").status == 200 && writes == oldWrites + 1 && observations == 3)
        now = 0; observations = 0; waits = 0
        try check("changed target aborts immediately", !MacTab5DesktopAX.settleFocus(verify: {
            observations += 1; return observations == 1 ? false : nil
        }, wait: { now += $0; waits += 1 }, clock: { now }) && observations == 2 && waits == 1)
        now = 0; observations = 0
        try check("missing focus has bounded deadline", !MacTab5DesktopAX.settleFocus(verify: {
            observations += 1; return false
        }, wait: { now += $0 }, clock: { now }) && observations <= 11 && now <= 0.5)
        observations = 0
        try check("stalled clock still has bounded observations", !MacTab5DesktopAX.settleFocus(verify: {
            observations += 1; return false
        }, wait: { _ in }, clock: { 0 }) && observations == 11)
        now = 0
        try check("slow AX confirmation past deadline rejected", !MacTab5DesktopAX.settleFocus(verify: {
            now = 0.6; return true
        }, wait: { now += $0 }, clock: { now }))

        // Similar labels are never fuzzy matches. Duplicate full titles only
        // block the title fallback; an exact live task address still works.
        typealias AX = MacTab5DesktopAX
        for question in [false, true] {
            let container = AX.ComposerContainer(role: "AXGroup", classes: [question ? "@container/request-card" : "_ComposerLayoutBody_fixture"])
            func identity(_ address: String, _ title: String, unique: String? = nil, duplicate: Bool = false) -> MacTab5DesktopCheck {
                AX.structuredComposerCheck(classes: ["ProseMirror"], containers: [container,
                    .init(role: "AXWebArea", address: address, title: title)], task: task,
                    uniqueFullTitle: unique, resources: nil, question: question, duplicateTitle: duplicate)
            }
            try check("duplicate full title gives actionable reason", identity("app://-/", "Same title", duplicate: true) == .duplicateTitle)
            try check("unique full title matches exactly", identity("app://-/", "Same title 2", unique: "Same title 2") == .confirmed)
            try check("similar title cannot identify another task", identity("app://-/", "Same title 2", unique: "Same title") == .route)
            try check("exact task address works despite duplicate titles", identity("app://-/local/" + task, "Same title", duplicate: true) == .confirmed)
            try check("wrong live task address cannot borrow title", identity("app://-/local/" + external, "Same title", unique: "Same title") == .route)
            try check("unavailable title is not falsely called duplicate", identity("app://-/", "Same title") == .route)
        }
        let duplicate = MacTab5Desktop(codex: codex, enabled: { true }, backend: .init(
            open: { _ in true }, read: { _ in nil }, write: { _, _, _ in writes += 1; return nil },
            diagnostic: { .duplicateTitle }, target: { _ in nil }))
        let beforeDuplicate = writes
        try check("duplicate rejection never writes", duplicate.handle(request("stage-draft"), device: "fixture").status == 503 && writes == beforeDuplicate)
        try check("duplicate failure is not relabeled question change", duplicate.summary.contains(MacTab5DesktopCheck.duplicateTitle.rawValue) && !duplicate.summary.contains(MacTab5DesktopCheck.questionChanged.rawValue))

        struct Node {
            var id: Int, role: String
            var trusted = false, valid = false
            var children: [Node] = []
        }
        let main = Node(id: 3, role: "AXTextArea", valid: true)
        var scanFailure: MacTab5DesktopCheck?
        func page(_ nodes: [Node], trusted: Bool = true) -> Node { Node(id: 2, role: "AXWebArea", trusted: trusted, children: nodes) }
        func select(_ nodes: [Node], clock: @escaping () -> TimeInterval = { 0 }) -> Int? {
            MacTab5DesktopAX.uniqueComposer(in: Node(id: 0, role: "AXWindow", children: nodes), role: { $0.role },
                children: { $0.children }, trustedPage: { $0.trusted }, validate: { $0.valid },
                failure: { scanFailure = $0 }, clock: clock)?.id
        }
        try check("find unfocused main editor", select([page([main])]) == 3)
        try check("ignore ordinary search fields", select([page([Node(id: 4, role: "AXTextField", valid: true), main])]) == 3)
        try check("wrong task structure rejected", select([page([Node(id: 4, role: "AXTextArea")])]) == nil)
        try check("ambiguous main editors rejected", select([page([main, main])]) == nil)
        try check("ambiguous editors keep specific diagnostic", scanFailure == .multipleComposers)
        try check("untrusted browser editor rejected", select([page([main], trusted: false)]) == nil)
        try check("nested browser editor rejected", select([page([page([main])])]) == nil)
        try check("native editor outside app document rejected", select([main]) == nil)
        for kind in ["AXDialog", "AXSheet", "AXTextField"] {
            try check("dialog and nested text controls excluded", select([page([Node(id: 4, role: kind, children: [main])])]) == nil)
        }
        try check("incomplete scan cannot choose early candidate", select([page([main] + Array(repeating: Node(id: 4, role: "AXGroup"), count: 1200))]) == nil)
        var time = 0.0
        try check("deadline stops scan", select([page([main])], clock: { time += 0.4; return time }) == nil)
        try check("scan deadline keeps specific diagnostic", scanFailure == .composerScanLimit)
        count += try appendAndCaret(root: root.appendingPathComponent("append-caret"))
        count += try nativeSubmission(root: root.appendingPathComponent("native-submit"))
        count += try MacCodexSubmissionValidation.run(root: root.appendingPathComponent("submission-receipt"))
        return count
    }

    private static func appendAndCaret(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ value: @autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Append/caret: " + name) }; count += 1
        }
        typealias AX = MacTab5DesktopAX
        for (text, end) in [("A😀中", 4), ("e\u{301}", 2), ("第一段\r\n第二段", 8), ("", 0)] {
            let range = AX.caretEnd(text)
            try check("caret uses UTF-16 without selecting text", range.location == end && range.length == 0)
        }
        try check("internal formatting mismatch remains rejected", AX.confirmWrite("first\nsecond", read: { "first second" }, wait: {}) == nil)
        let task = UUID().uuidString.lowercased()
        var value = "", writes = 0, actions = 0
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 8, count: 32) }), enabled: { true }, sendEnabled: { true }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "name": "Append fixture", "cwd": "/fixture"]]] }
            throw MacCodexDesktopError.invalidPacket
        })
        codex.refresh()
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: .init(open: { _ in true }, read: { _ in value },
            write: { _, _, _ in nil }, diagnostic: { .caretUnconfirmed }, focusComposer: { _ in true }, target: { _ in "question:first" },
            writeTarget: { _, _, before, after in
                guard value == before else { return nil }; writes += 1
                // Model an inline editor's normalization. Never relax matching
                // of user-authored whitespace to accommodate this renderer.
                value = after.replacingOccurrences(of: "\n", with: " ")
                return AX.confirmWrite(after, read: { value }, wait: {})
            }, submitQuestion: { _, _, expected in guard expected == value else { return false }; actions += 1; return true }))
        func request(_ op: String, text: String = "", draft: String? = nil) -> [String: Any] {
            var r: [String: Any] = ["op": op, "taskId": task, "requestId": UUID().uuidString.lowercased(), "text": text]
            r["draftRequestId"] = draft; return r
        }
        let first = request("stage-draft", text: "第一段。"), second = request("stage-draft", text: "第二段😀。")
        try check("first take stages", desktop.handle(first, device: "A").status == 200 && writes == 1)
        try check("second take remains submittable after readback", desktop.handle(second, device: "A").status == 200 && value == "第一段。 第二段😀。" && writes == 2)
        try check("caret limitation does not invalidate confirmed text", desktop.summary == MacTab5DesktopCheck.caretUnconfirmed.rawValue)
        try check("repeat second take never appends twice", desktop.handle(second, device: "A").status == 200 && writes == 2)
        let submit = request("submit-draft", draft: second["requestId"] as? String)
        try check("submit binds both takes exactly once", desktop.handle(submit, device: "A").status == 200 && actions == 1)
        try check("repeated submit cannot resend", desktop.handle(submit, device: "A").status == 200 && actions == 1)
        return count
    }

    private static func nativeSubmission(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("Codex native submit: " + name) }; count += 1
        }
        typealias AX = MacTab5DesktopAX
        struct Node {
            var id: Int, role: String
            var label = "", enabled = true, prohibited = false
            var children: [Node] = []
        }
        let editor = Node(id: 1, role: "AXTextArea"), send = Node(id: 2, role: "AXButton", label: "发送")
        var scanFailure: MacTab5DesktopCheck?
        func select(_ nodes: [Node], clock: () -> TimeInterval = { 0 }) -> Int? {
            scanFailure = nil
            return AX.uniqueSubmitButton(in: Node(id: 0, role: "AXGroup", children: nodes), role: { $0.role },
                children: { $0.children }, prohibited: { $0.prohibited }, isEditor: { $0.id == 1 },
                label: { $0.label }, enabled: { $0.enabled }, clock: clock, failure: { scanFailure = $0 })?.id
        }
        for name in ["Send", "Steer", "Queue", "发送", "引导", "排队"] {
            try check("supported displayed action", select([editor, .init(id: 2, role: "AXButton", label: name)]) == 2)
        }
        for name in ["Stop", "停止", "Continue", "继续", "Approve", "允许", "Send update", "发送所有"] {
            try check("never substitute another action", select([editor, .init(id: 2, role: "AXButton", label: name)]) == nil)
        }
        try check("disabled submit rejected", select([editor, .init(id: 2, role: "AXButton", label: "发送", enabled: false)]) == nil)
        try check("two submit controls rejected", select([editor, send, send]) == nil)
        try check("other editor rejected", select([editor, .init(id: 3, role: "AXTextArea"), send]) == nil)
        try check("missing original editor rejected", select([send]) == nil)
        for role in ["AXWebArea", "AXDialog", "AXSheet", "AXSecureTextField", "AXTextField"] {
            try check("nested surface rejected", select([editor, .init(id: 3, role: role, children: [send])]) == nil)
        }
        try check("question/approval container rejected", select([editor, .init(id: 3, role: "AXGroup", prohibited: true, children: [send])]) == nil)
        try check("incomplete scan rejected", select([editor, send] + Array(repeating: .init(id: 3, role: "AXGroup"), count: 301)) == nil)
        var time = 0.0
        try check("timed out scan rejected", select([editor, send], clock: { time += 0.2; return time }) == nil)

        try check("timeout retains exact reason", scanFailure == .submitScanLimit)
        let rejected: [([Node], MacTab5DesktopCheck)] = [
            ([editor], .submitButtonMissing), ([send], .submitEditorMissing),
            ([editor, send, send], .submitButtonMultiple),
            ([editor, .init(id: 2, role: "AXButton", label: "发送", enabled: false)], .submitButtonDisabled),
            ([editor, .init(id: 3, role: "AXTextField"), send], .submitScope)
        ]
        for (nodes, reason) in rejected {
            try check("scan preserves precise failure", select(nodes) == nil && scanFailure == reason)
        }
        // Accessibility can flatten the outer presentational Root. Both layouts
        // must resolve the same nearest Body, with no fallback to the page.
        func container(_ ancestors: [(String, [String])]) -> Int? {
            AX.composerContainer(0, role: { ancestors[$0].0 }, classes: { ancestors[$0].1 },
                parent: { $0 + 1 < ancestors.count ? $0 + 1 : nil })
        }
        let input = ("AXTextArea", ["ProseMirror"]), body = ("AXGroup", ["_ComposerLayoutBody_fixture"])
        let outer = ("AXGroup", ["_ComposerLayoutRoot_fixture"]), page = ("AXWebArea", [String]())
        try check("flattened presentational root still finds body", container([input, body, page]) == 1)
        try check("present root still chooses narrower body", container([input, body, outer, page]) == 1)
        try check("missing body never broadens to root", container([input, outer, page]) == nil)
        try check("never crosses page to another body", container([input, page, body]) == nil)
        try check("request card cannot borrow main body", container([input, ("AXGroup", ["@container/request-card"]), body]) == nil)
        try check("ancestor depth bounded", container([input] + Array(repeating: ("AXGroup", [String]()), count: 32) + [body]) == nil)
        try check("footer wrappers preserve unique action", select([editor, .init(id: 3, role: "AXGroup", children: [send])]) == 2)
        try check("nested body refused even without another editor", select([editor, .init(id: 3, role: "AXGroup", prohibited: true, children: [send])]) == nil)

        var observed = 0, elapsed = 0.0
        func confirmation(_ sequence: [AX.SubmissionObservation], acknowledged: Bool = true) -> MacTab5DesktopCheck? {
            observed = 0; elapsed = 0
            return AX.confirmSubmission(actionAcknowledged: acknowledged, observe: {
                let value = sequence[min(observed, sequence.count - 1)]; observed += 1; return value
            }, wait: { elapsed += $0 }, clock: { elapsed })
        }
        try check("AX action timeout can still have delivered", confirmation([.unchanged, .empty, .empty], acknowledged: false) == nil && observed == 3)
        try check("transient renderer gaps do not end confirmation", confirmation([.unavailable, .unchanged, .unavailable, .empty, .empty]) == nil && observed == 5)
        try check("one empty observation is not enough", confirmation([.empty, .unavailable]) == .submitReadbackUnavailable && elapsed <= 2.5)
        try check("empty verification must be consecutive", confirmation([.empty, .unavailable, .empty, .empty]) == nil && observed == 4)
        try check("action success alone is not delivery", confirmation([.unchanged]) == .submitTextRemains && observed <= 32 && elapsed <= 2.5)
        try check("action timeout alone is not delivery", confirmation([.unavailable], acknowledged: false) == .submitActionUnacknowledged)
        try check("new text never counts as sent", confirmation([.changed, .empty]) == .submitReadbackChanged && observed == 1)
        try check("another window never confirms", confirmation([.contextChanged, .empty]) == .submitContextChanged && observed == 1)
        observed = 0
        try check("stalled clock remains observation bounded", AX.confirmSubmission(actionAcknowledged: true, observe: {
            observed += 1; return .unavailable
        }, wait: { _ in }, clock: { 0 }) == .submitReadbackUnavailable && observed == 32)
        elapsed = 0
        try check("slow AX read past deadline cannot confirm", AX.confirmSubmission(actionAcknowledged: true, observe: {
            elapsed += 1.5; return .empty
        }, wait: { elapsed += $0 }, clock: { elapsed }) == .submitReadbackUnavailable)

        let task = UUID().uuidString.lowercased()
        var value = "", writes = 0, actions = 0, ipcCalls = 0, sendEnabled = true
        var target = "main", outcome = MacTab5DesktopSubmission.confirmed
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 2, count: 32) }), enabled: { true }, sendEnabled: { sendEnabled }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "name": "Active fixture", "cwd": "/fixture", "status": ["type": "active", "activeFlags": []]]]] }
            ipcCalls += 1; throw MacTab5CodexFailure.protocolError
        })
        codex.refresh()
        let backend = MacTab5DesktopBackend(open: { _ in true }, read: { _ in value },
            write: { _, before, after in guard value == before else { return nil }; writes += 1; value = after; return after },
            diagnostic: { outcome == .unconfirmed ? .submitActionUnacknowledged : .submitButtonMissing }, target: { _ in target }, submitComposer: { requested, text in
                guard requested == task, text == value else { return .notReady }
                if outcome != .notReady { actions += 1 }
                if outcome == .confirmed { value = "" }
                return outcome
            })
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        func request(_ op: String, draft: String? = nil) -> [String: Any] {
            var r: [String: Any] = ["op": op, "taskId": task, "requestId": UUID().uuidString.lowercased(), "text": "fixture words"]
            r["draftRequestId"] = draft; return r
        }
        let stage = request("stage-draft")
        try check("active task stages without sending", desktop.handle(stage, device: "A").status == 200 && actions == 0)
        let submit = request("submit-draft", draft: stage["requestId"] as? String)
        sendEnabled = false
        try check("native submit retains explicit opt-in", desktop.handle(submit, device: "A").status == 403 && actions == 0)
        sendEnabled = true
        try check("other device cannot use receipt", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "B").status == 409 && actions == 0)
        value += " changed"
        try check("changed text cannot send", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "A").status == 409 && actions == 0)
        value = "fixture words"; target = "question:changed"
        try check("changed target cannot send", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "A").status == 409 && actions == 0)
        target = "main"
        try check("active task uses native button exactly once", desktop.handle(submit, device: "A").status == 200 && actions == 1 && value.isEmpty)
        try check("no clear-before-submit or IPC send", writes == 1 && ipcCalls == 0)
        _ = codex.handle(["op": "draft-load", "taskId": task, "requestId": UUID().uuidString.lowercased()], device: "A")
        try check("confirmed submit clears only its saved mirror", (codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String == "")
        _ = codex.handle(["op": "draft-save", "taskId": task, "requestId": UUID().uuidString.lowercased(), "message": "newer device draft"], device: "A")
        try check("cleanup preserves a newer saved draft", codex.clearSubmittedDesktopDraft(task: task, device: "A", expected: "fixture words"))
        _ = codex.handle(["op": "draft-load", "taskId": task, "requestId": UUID().uuidString.lowercased()], device: "A")
        try check("newer draft remains intact", (codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String == "newer device draft")
        try check("same submit returns receipt", desktop.handle(submit, device: "A").status == 200 && actions == 1)
        let restarted = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        try check("restart cannot replay native submit", restarted.handle(submit, device: "A").status == 200 && actions == 1)
        try check("new request cannot reuse consumed draft", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "A").status == 409 && actions == 1)
        for (result, code) in [(MacTab5DesktopSubmission.notReady, 409), (.unconfirmed, 504)] {
            value = ""; outcome = result
            let newStage = request("stage-draft")
            try check("new take stages", desktop.handle(newStage, device: "A").status == 200)
            let attempt = request("submit-draft", draft: newStage["requestId"] as? String), before = actions
            try check("distinguish no action from unknown outcome", desktop.handle(attempt, device: "A").status == code && actions == before + (result == .notReady ? 0 : 1))
            try check("backend failure is not masked", desktop.summary.contains((result == .notReady ? MacTab5DesktopCheck.submitButtonMissing : .submitActionUnacknowledged).rawValue))
            let after = actions
            try check("failure preserves text without IPC fallback", value == "fixture words" && ipcCalls == 0)
            try check("failure replay never clicks again", desktop.handle(attempt, device: "A").status == code && actions == after)
            try check("unknown receipt cannot be reused", desktop.handle(request("submit-draft", draft: newStage["requestId"] as? String), device: "A").status == 409 && actions == after)
        }
        value = ""; outcome = .confirmed
        let recovered = request("stage-draft")
        try check("next stage preserves last submit diagnostic", desktop.handle(recovered, device: "A").status == 200 && desktop.summary.contains("最近提交失败"))
        try check("later confirmed submission clears obsolete failure", desktop.handle(request("submit-draft", draft: recovered["requestId"] as? String), device: "A").status == 200 && !desktop.summary.contains("最近提交失败"))
        return count
    }
}
