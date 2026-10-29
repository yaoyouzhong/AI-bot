import Foundation

enum MacCodexQuestionValidation {
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ value: @autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("Codex question: " + name) }; count += 1
        }
        typealias AX = MacTab5DesktopAX
        for action in ["Send", "Send update", "Next", "发送", "发送更新", "下一步"] {
            try check("supported question action", AX.questionSignature(text: ["问题", "Which option?"], buttons: ["跳过", action], editors: 1)?.action == action)
        }
        for buttons in [["允许"], ["发送", "下一步"], ["Skip"], ["Send everything"]] {
            try check("never infer an approval or ambiguous action", AX.questionSignature(text: ["问题", "Which option?"], buttons: buttons, editors: 1) == nil)
        }
        for (text, editors) in [(["Approve", "permission"], 1), (["问题"], 1), (["问题", "prompt"], 0), (["问题", "prompt"], 2), (["问题", String(repeating: "x", count: 8193)], 1)] {
            try check("require bounded identifiable single answer", AX.questionSignature(text: text, buttons: ["Send"], editors: editors) == nil)
        }
        let task = UUID().uuidString.lowercased(), other = UUID().uuidString.lowercased()
        typealias Container = AX.ComposerContainer
        let card = Container(role: "AXGroup", classes: ["@container/request-card"])
        func structure(_ nodes: [Container], question: Bool = true) -> MacTab5DesktopCheck {
            AX.structuredComposerCheck(classes: ["ProseMirror"], containers: nodes, task: task, uniqueFullTitle: nil, resources: nil, question: question)
        }
        let page = Container(role: "AXWebArea", address: "app://-/local/" + task)
        try check("question uses own exact document", structure([card, page]) == .confirmed)
        try check("main validation still excludes question", structure([card, page], question: false) == .structure)
        try check("another task cannot borrow question", structure([card, .init(role: "AXWebArea", address: "app://-/local/" + other)]) == .route)
        try check("nested request cards rejected", structure([card, card, page]) == .structure)
        try check("browser document rejected", structure([card, .init(role: "AXWebArea", address: "https://example.com")]) == .route)
        try check("dialog rejected", structure([card, .init(role: "AXDialog"), page]) == .structure)
        try check("nested document rejected", structure([card, page, page]) == .structure)

        struct Node { var role: String; var rank = 0; var children: [Node] = [] }
        let main = Node(role: "AXTextArea"), question = Node(role: "AXTextArea", rank: 1)
        func selected(_ nodes: [Node]) -> Int? {
            AX.uniqueComposer(in: Node(role: "AXWindow", children: [Node(role: "AXWebArea", children: nodes)]),
                role: { $0.role }, children: { $0.children }, trustedPage: { _ in true }, validate: { _ in true }, priority: { $0.rank }, clock: { 0 })?.rank
        }
        try check("question preferred even if main has focus", selected([main, question]) == 1)
        try check("multiple questions cannot choose first", selected([question, main, question]) == nil)

        var target: String? = "question:first", value = "", focused = false, writes = 0, normalSends = 0, questionActions = 0, nativeSuccess = true, sendingEnabled = true
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 1, count: 32) }), enabled: { true }, sendEnabled: { sendingEnabled }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/fixture", "name": "Question fixture"]]] }
            normalSends += 1; throw MacTab5CodexFailure.protocolError
        })
        codex.refresh()
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: .init(
            open: { _ in true }, read: { _ in focused ? value : nil }, write: { _, _, _ in normalSends += 1; return nil },
            navigation: { _ in true }, focusComposer: { _ in focused = true; return true }, target: { _ in target },
            writeTarget: { _, expectedTarget, before, after in
                guard focused, target == expectedTarget, value == before else { return nil }; writes += 1; value = after; return value
            }, submitQuestion: { _, expectedTarget, text in
                guard expectedTarget == target, text == value else { return false }; questionActions += 1; return nativeSuccess
            }))
        func request(_ op: String, draft: String? = nil) -> [String: Any] {
            var r: [String: Any] = ["op": op, "taskId": task, "requestId": UUID().uuidString.lowercased(), "text": "voice answer"]
            r["draftRequestId"] = draft; return r
        }
        try check("navigation does not write an answer", desktop.handle(request("open-recent"), device: "A").status == 200 && writes == 0)
        desktop.prepareVoice(task: task, device: "A"); target = "question:second"
        try check("question changed during voice retains preview", desktop.handle(request("stage-draft"), device: "A").status == 503 && writes == 0)
        target = "main"
        try check("closed question never falls back to main", desktop.handle(request("stage-draft"), device: "A").status == 503 && writes == 0)
        target = "question:first"; desktop.prepareVoice(task: task, device: "A")
        let stage = request("stage-draft")
        try check("stage reaches question field", desktop.handle(stage, device: "A").status == 200 && value == "voice answer" && writes == 1)
        try check("stage never sends", questionActions == 0 && normalSends == 0)
        try check("duplicate stage cannot append twice", desktop.handle(stage, device: "A").status == 200 && writes == 1)
        target = "question:second"
        try check("submission binds exact question", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "A").status == 409 && questionActions == 0)
        target = "question:first"
        sendingEnabled = false
        try check("question sending retains opt-in", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "A").status == 403 && questionActions == 0)
        sendingEnabled = true
        let submit = request("submit-draft", draft: stage["requestId"] as? String)
        try check("explicit submission uses question action", desktop.handle(submit, device: "A").status == 200 && questionActions == 1 && normalSends == 0 && writes == 1)
        try check("duplicate submit cannot click twice", desktop.handle(submit, device: "A").status == 200 && questionActions == 1)
        try check("receipt cannot send again with new id", desktop.handle(request("submit-draft", draft: stage["requestId"] as? String), device: "A").status == 409 && questionActions == 1)
        let next = request("stage-draft"); desktop.prepareVoice(task: task, device: "A")
        try check("second take appends same question", desktop.handle(next, device: "A").status == 200 && value == "voice answer voice answer")
        nativeSuccess = false
        try check("unknown native result retained without retry", desktop.handle(request("submit-draft", draft: next["requestId"] as? String), device: "A").status == 504 && questionActions == 2)
        try check("unknown submission consumes receipt", desktop.handle(request("submit-draft", draft: next["requestId"] as? String), device: "A").status == 409 && questionActions == 2 && normalSends == 0)

        var captured = false, captureCount = 0, startedAfterCapture = false
        let voice = MacTab5Voice(enabled: { true }, backend: .init(start: { _, _ in startedAfterCapture = captured }, feed: { _ in }, finish: {}, cancel: {}))
        voice.beforeStart = { observed, device in captured = observed == task && device == "A"; captureCount += 1 }
        let start: [String: Any] = ["op": "start", "taskId": task, "session": "fixture", "source": "tab5"]
        try check("capture precedes IME focus changes", voice.handle(start, device: "A").status == 200 && startedAfterCapture && captureCount == 1)
        try check("busy start cannot replace binding", voice.handle(start, device: "A").status == 409 && captureCount == 1)
        voice.stop()
        let readFinished = DispatchSemaphore(value: 0)
        var concurrentRead = false
        voice.beforeStart = { _, _ in
            DispatchQueue.global().async { _ = voice.summary; readFinished.signal() }
            concurrentRead = readFinished.wait(timeout: .now() + 1) == .success
        }
        try check("target preparation does not hold voice lock", voice.handle(start, device: "A").status == 200 && concurrentRead)
        voice.stop()
        voice.beforeStart = { _, _ in voice.stop() }
        try check("cancellation during preparation prevents capture", voice.handle(start, device: "A").status == 409)
        return count
    }
}
