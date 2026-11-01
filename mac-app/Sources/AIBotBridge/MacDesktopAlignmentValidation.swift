import Foundation

enum MacDesktopAlignmentValidation {
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ label: String, _ value: @autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Desktop alignment: " + label) }; count += 1
        }
        let task = UUID().uuidString.lowercased()
        var text = "", target = "main", identity = "editor-1", actions = 0, writes = 0
        var outcome = MacTab5DesktopSubmission.confirmed, attachments = false, changeBeforeAction = false
        var submittedText = "", privateAvailable = true
        let codex = MacTab5Codex(store: .init(root: root, key: { guard privateAvailable else { throw MacTab5Error.keychain }; return Data(repeating: 9, count: 32) }), enabled: { true }, sendEnabled: { true }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "name": "Alignment fixture", "cwd": "/fixture"]]] }
            throw MacTab5CodexFailure.protocolError
        })
        codex.refresh()
        let backend = MacTab5DesktopBackend(open: { _ in true }, read: { _ in text }, write: { _, _, _ in nil },
            diagnostic: { .changed }, focusComposer: { _ in true }, target: { _ in target },
            submitComposer: { _, _ in .notReady }, attachmentFree: { _ in !attachments },
            identity: { _ in identity }, writeBoundTarget: { _, kind, editor, before, after in
                guard kind == target, editor == identity, before == text else { return nil }
                writes += 1; text = after; return after
            }, submitBound: { _, kind, editor, frozen in
                if changeBeforeAction { text += " second edit" }
                guard kind == target, editor == identity, frozen == text else { return .notReady }
                guard outcome != .notReady else { return .notReady }
                actions += 1; submittedText = frozen
                if outcome == .confirmed { text = "" }
                return outcome
            })
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        func request(_ op: String, draft: String? = nil) -> [String: Any] {
            var r: [String: Any] = ["op": op, "taskId": task, "requestId": UUID().uuidString.lowercased(), "text": "original voice"]
            r["draftRequestId"] = draft; return r
        }
        func stage() throws -> String {
            text = ""; let r = request("stage-draft")
            try check("bound stage", desktop.handle(r, device: "A").status == 200)
            return r["requestId"] as! String
        }
        func failure(_ r: MacTab5RPCResult, attempted: Bool, kind: String) -> Bool {
            r.body["taskId"] as? String == task && r.body["submitted"] as? Bool == false &&
            r.body["attempted"] as? Bool == attempted && r.body["error"] as? String == kind
        }
        let corrected = try stage(); text = "corrected\nsecond paragraph。😀"
        try check("same editor sends latest correction", desktop.handle(request("submit-draft", draft: corrected), device: "A").status == 200 && submittedText == "corrected\nsecond paragraph。😀" && actions == 1 && writes == 1)
        _ = codex.handle(["op": "draft-load", "taskId": task, "requestId": UUID().uuidString.lowercased()], device: "A")
        try check("original mirror cleared after correction", (codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String == "")
        let switched = try stage(); identity = "editor-2"
        let wrongEditor = desktop.handle(request("submit-draft", draft: switched), device: "A")
        try check("same task different editor rejected", wrongEditor.status == 409 && actions == 1 && failure(wrongEditor, attempted: false, kind: "conversation_target_or_text_changed"))
        let race = try stage(); changeBeforeAction = true
        let raceRequest = request("submit-draft", draft: race), raced = desktop.handle(raceRequest, device: "A")
        try check("second edit before press rejected", raced.status == 409 && actions == 1 && text.hasSuffix("second edit") && failure(raced, attempted: false, kind: "conversation_target_or_text_changed"))
        changeBeforeAction = false
        try check("not-ready ticket stays consumed", desktop.handle(request("submit-draft", draft: race), device: "A").status == 409 && actions == 1)
        try check("not-ready replay keeps typed response", failure(desktop.handle(raceRequest, device: "A"), attempted: false, kind: "conversation_target_or_text_changed"))
        let attached = try stage(); attachments = true
        try check("attachments preserved without action", desktop.handle(request("submit-draft", draft: attached), device: "A").status == 409 && actions == 1 && text == "original voice")
        attachments = false
        let empty = try stage(); text = " \n"
        try check("blank correction rejected", desktop.handle(request("submit-draft", draft: empty), device: "A").status == 409 && actions == 1)
        let oversized = try stage(); text = String(repeating: "中", count: 667)
        try check("oversized correction rejected", desktop.handle(request("submit-draft", draft: oversized), device: "A").status == 409 && actions == 1)
        target = "question:first"; let question = try stage(); text = "corrected answer"
        try check("same question accepts correction", desktop.handle(request("submit-draft", draft: question), device: "A").status == 200 && submittedText == "corrected answer" && actions == 2)
        let changed = try stage(); target = "question:second"
        let changedResult = desktop.handle(request("submit-draft", draft: changed), device: "A")
        try check("original question classifies rejection", changedResult.status == 409 && actions == 2 && failure(changedResult, attempted: false, kind: "question_target_or_text_changed"))
        let uncertain = try stage(); outcome = .unconfirmed
        let unknownRequest = request("submit-draft", draft: uncertain), unknown = desktop.handle(unknownRequest, device: "A")
        try check("post-action question unknown distinct", unknown.status == 504 && actions == 3 && failure(unknown, attempted: true, kind: "desktop_result_unknown"))
        try check("unknown replay cannot press again", desktop.handle(unknownRequest, device: "A").status == 504 && actions == 3)
        let restart = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        try check("restart cannot replay unknown", restart.handle(unknownRequest, device: "A").status == 504 && actions == 3)
        try check("new request cannot reuse unknown", restart.handle(request("submit-draft", draft: uncertain), device: "A").status == 409 && actions == 3)
        let unavailable = try stage(); privateAvailable = false
        let storage = desktop.handle(request("submit-draft", draft: unavailable), device: "A")
        try check("storage failure before action is clearly not sent", storage.status == 409 && actions == 3 && failure(storage, attempted: false, kind: "desktop_storage_unavailable") && text == "original voice")
        privateAvailable = true
        return count
    }
}
