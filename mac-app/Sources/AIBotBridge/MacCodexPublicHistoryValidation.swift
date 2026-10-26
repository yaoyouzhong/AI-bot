import Foundation

enum MacCodexPublicHistoryValidation {
    static func run(root: URL) throws -> Int {
        var checks = 0
        func check(_ name: String, _ ok: @autoclosure () -> Bool) throws {
            guard ok() else { throw MacTab5LocalError.invalid("Public history: " + name) }; checks += 1
        }
        let task = UUID().uuidString.lowercased()
        let ids = (0..<260).map { _ in UUID().uuidString.lowercased() }
        var reads: [String] = [], directories = 0, now: TimeInterval = 100
        var fault = "", large = false
        func message(_ text: String, phase: String = "commentary") -> [String: Any] {
            ["type":"agentMessage", "phase":phase, "text":text]
        }
        let call: MacTab5Codex.Call = { method, params in
            if method == "thread/list" { return ["data":[["id":task,"name":"synthetic","cwd":"/synthetic","status":["type":"idle"]]]] }
            if method == "thread/read" {
                try check("header excludes turns", params["includeTurns"] as? Bool == false)
                return ["thread":["id":task,"status":["type":"idle"]]]
            }
            if method == "thread/turns/list" {
                directories += 1
                try check("directory has no bodies", params["itemsView"] as? String == "notLoaded" && params["limit"] as? Int == 32 && params["sortDirection"] as? String == "desc")
                let start = Int(params["cursor"] as? String ?? "0") ?? 0
                if fault == "directory-late" { now += 8 }
                if fault == "directory-oversize" { return ["data":Array(repeating:["id":ids[0]],count:33)] }
                let end = min(ids.count,start + 32)
                var result: [String: Any] = ["data":(start..<end).map { ["id":ids[$0],"status":"completed","startedAt":Int64(1000 - $0)] as [String:Any] }]
                if end < ids.count { result["nextCursor"] = fault == "directory-cycle" ? "32" : String(end) }
                return result
            }
            guard method == "thread/items/list", let turn = params["turnId"] as? String else { throw MacTab5CodexFailure.protocolError }
            reads.append(turn)
            try check("items exact scope and bounded ascending", params["threadId"] as? String == task && params["limit"] as? Int == 16 && params["sortDirection"] as? String == "asc")
            if fault == "item-late" { now += 8 }
            let cursor = params["cursor"] as? String
            let publicText = large ? String(repeating:"x",count:1024*1024+1) : (cursor == nil ? "first" : "last")
            let values = [message(publicText),["type":"reasoning","text":"PRIVATE"],
                          ["type":"userMessage","text":"PRIVATE"],["type":"commandExecution","output":"PRIVATE"],
                          message("PRIVATE",phase:"analysis")]
            var entries: [[String:Any]] = values.map { ["turnId":turn,"item":$0] }
            if fault == "wrong-turn" { entries[0]["turnId"] = ids[200] }
            if fault == "item-oversize" { entries = Array(repeating:entries[0],count:17) }
            var result: [String:Any] = ["data":entries]
            if cursor == nil || fault == "item-cycle" { result["nextCursor"] = "second" }
            if fault == "bad-cursor" { result["nextCursor"] = 4 }
            if fault == "long-cursor" { result["nextCursor"] = String(repeating:"c",count:4097) }
            if fault == "item-endless" { result["nextCursor"] = String(reads.count) }
            return result
        }
        func load(_ selected: String = "", _ offset: Int = 0) throws -> MacCodexPublicHistory.Window {
            try MacCodexPublicHistory.read(task:task,selected:selected,offset:offset,call:call,clock:{now})
        }
        var window = try load()
        try check("only latest materialized", reads == [ids[0],ids[0]] && directories == 1)
        try check("directory order and boundary retained", window.turns.count == 32 && window.turns.first?["id"] as? String == ids[31] && window.hasOlder && !window.limited)
        let texts = MacTab5Codex.publicItems(window.turns.last?["items"]).compactMap { $0["text"] as? String }
        try check("public text complete across batches", texts == ["first","last"])
        let encoded = try MacTab5Protocol.json(["turns":window.turns])
        try check("no private bodies retained", !String(decoding:encoded,as:UTF8.self).contains("PRIVATE"))
        reads = []; directories = 0
        window = try load(ids[31],-1)
        try check("older navigation crosses directory boundary", directories == 2 && window.turns.count == 64 && Set(reads) == Set([ids[0],ids[32]]))
        try check("selected and latest stay materialized", MacTab5Codex.publicItems(window.turns.first(where:{$0["id"] as? String == ids[32]})?["items"]).count == 2 && MacTab5Codex.publicItems(window.turns.last?["items"]).count == 2)
        reads = []; directories = 0; _ = try load(ids[10],1)
        try check("newer offset uses correct turn", Set(reads) == Set([ids[0],ids[9]]) && directories == 1)
        reads = []; directories = 0; window = try load(ids[259])
        try check("directory cap explicit", directories == 8 && window.limited && window.hasOlder && Set(reads) == Set([ids[0]]))
        for mode in ["directory-cycle","directory-oversize","wrong-turn","item-oversize","item-cycle","bad-cursor","long-cursor","directory-late","item-late","item-endless"] {
            fault = mode; now = 100; reads = []; directories = 0
            var rejected = false
            do { _ = try load(mode == "directory-cycle" ? ids[259] : "") }
            catch MacTab5CodexFailure.protocolError { rejected = !mode.hasSuffix("late") && mode != "item-endless" }
            catch MacTab5CodexFailure.timeout { rejected = mode.hasSuffix("late") }
            catch MacCodexPublicHistory.Failure.tooLarge { rejected = mode == "item-endless" }
            try check("reject " + mode, rejected)
        }
        fault = ""; now = 100; large = true
        let codex = MacTab5Codex(store:.init(root:root,key:{Data(repeating:6,count:32)}),enabled:{true},sendEnabled:{false},
            clock:{now},canMutate:{false},call:call)
        codex.refresh()
        let oversized = codex.handle(["op":"read","taskId":task],device:"synthetic-device")
        try check("public size cap maps to explicit 413", oversized.status == 413 && oversized.body["error"] as? String == "reply_too_large")
        large = false
        let success = codex.handle(["op":"read","taskId":task],device:"synthetic-device")
        try check("retry after oversized retains full reply", success.status == 200 && success.body["text"] as? String == "first\n\nlast")
        let older = codex.handle(["op":"read","taskId":task,"turnId":ids[31],"turnOffset":-1],device:"synthetic-device")
        try check("read contract retains history navigation", older.status == 200 && older.body["turnId"] as? String == ids[32] && (older.body["replyView"] as? [String:Any])?["history"] as? Bool == true)
        return checks
    }
}
