import Foundation

enum MacVoiceSchedulingValidation {
    private final class Wire: MacCodexDesktopIPC.Wire {
        let task: String
        var turn: String, items: [[String: Any]], reject = false, timeout = false
        let owner = UUID().uuidString.lowercased(), client = UUID().uuidString.lowercased()
        var replies: [[String: Any]] = [], reads = 0, mutations = 0
        init(task: String, turn: String, items: [[String: Any]]) { self.task = task; self.turn = turn; self.items = items }
        func write(_ value: [String: Any], until: TimeInterval) throws {
            let method = value["method"] as? String ?? ""
            var response: [String: Any] = ["type":"response", "requestId":value["requestId"] ?? "", "resultType":"success", "handledByClientId":owner, "method":method]
            if method == "initialize" { response["result"] = ["clientId":client]; replies.append(response) }
            else if method == "thread-owner-discovery" {
                if timeout { throw MacCodexDesktopError.timeout }
                if reject { response["resultType"] = "error" }
                response["result"] = ["supportsUntrustedAppInput":true]; replies.append(response)
            } else if method == "thread-stream-following-changed", (value["params"] as? [String: Any])?["following"] as? Bool == true {
                reads += 1
                let state: [String: Any] = ["id":task, "cwd":"/synthetic", "threadRuntimeStatus":["type":"idle"], "requests":[Any](),
                    "turns":[["turnId":turn, "status":"completed", "turnStartedAtMs":20000, "items":items]]]
                replies.append(["type":"broadcast", "method":"thread-stream-state-changed", "sourceClientId":owner, "version":11,
                    "params":["hostId":"local", "conversationId":task, "change":["type":"snapshot", "conversationState":state]]])
            } else if method.hasPrefix("thread-follower-") { mutations += 1; throw MacCodexDesktopError.deliveryUnknown }
        }
        func read(until: TimeInterval) throws -> [String: Any] {
            guard !replies.isEmpty else { throw MacCodexDesktopError.timeout }; return replies.removeFirst()
        }
        func close() {}
    }
    static func run(root: URL) throws -> Int {
        var checks = 0
        func check(_ name: String, _ value: @autoclosure () -> Bool) throws {
            guard value() else { throw MacTab5LocalError.invalid("Voice scheduling: " + name) }; checks += 1
        }
        let task = UUID().uuidString.lowercased(), old = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        func item(_ text: String) -> [String: Any] { ["id":"assistant-1", "type":"agentMessage", "phase":"final_answer", "text":text] }
        let longText = String(repeating:"published ", count:600)
        let wire = Wire(task:task,turn:turn,items:[item("published "),["type":"reasoning", "text":"PRIVATE_REASONING"]])
        let desktop = MacCodexDesktopIPC(connect:{wire})
        var historyReads = 0, enabled = true
        let codex = MacTab5Codex(store:.init(root:root,key:{Data(repeating:5,count:32)}),enabled:{enabled},sendEnabled:{true},
            clock:{100},desktop:desktop,call:{ method, _ in
                if method == "thread/list" { return ["data":[["id":task,"name":"synthetic","cwd":"/synthetic","status":["type":"idle"]]]] }
                guard method == "thread/read" else { throw MacTab5CodexFailure.protocolError }
                historyReads += 1
                return ["thread":["id":task,"status":["type":"idle"],"turns":[
                    ["id":old,"status":"completed","startedAt":10,"items":[item("older")]],
                    ["id":turn,"status":"completed","startedAt":20,"items":[item(longText)]]]]]
            })
        codex.refresh(); wire.reads = 0
        func read(_ extra: [String: Any] = [:]) -> MacTab5RPCResult {
            codex.handle(["op":"read","taskId":task].merging(extra){_,v in v},device:"synthetic-device")
        }
        var result = read()
        try check("first read establishes public history", result.status == 200 && historyReads == 1 && wire.reads == 1)
        result = read()
        try check("latest polling avoids slow public history", result.status == 200 && historyReads == 1 && wire.reads == 2)
        try check("persisted long public message is preserved", (result.body["text"] as? String ?? "").utf8.count == 3500 && (result.body["replyView"] as? [String: Any])?["pageCount"] as? Int == 2)
        try check("older history remains reachable", (result.body["replyView"] as? [String: Any])?["hasPrevious"] as? Bool == true)
        let projection = String(decoding:try MacTab5Protocol.json(result.body),as:UTF8.self)
        try check("private owner payload excluded", !projection.contains("PRIVATE_REASONING"))
        result = read(["turnId":turn,"turnOffset":-1])
        try check("explicit older navigation reads real history",result.status == 200 && historyReads == 2 && result.body["turnId"] as? String == old)
        wire.turn = UUID().uuidString.lowercased(); wire.items = [item("new turn")]
        result = read()
        try check("fresh owner changes turn without history delay",result.status == 200 && historyReads == 2 && result.body["turnId"] as? String == wire.turn)
        try check("new turn preserves older boundary",(result.body["replyView"] as? [String: Any])?["hasPrevious"] as? Bool == true)
        wire.reject = true; result = read()
        try check("missing owner falls back to real public history",result.status == 200 && historyReads == 3)
        func send(_ extra:[String:Any] = [:]) -> MacTab5RPCResult {
            codex.handle(["op":"send","taskId":task,"requestId":UUID().uuidString.lowercased(),"message":"synthetic"].merging(extra){_,v in v},device:"synthetic-device")
        }
        result = send()
        try check("owner preflight fails before side effects",result.status == 503 && wire.mutations == 0 && codex.summary.contains("desktop_owner_unavailable"))
        try check("existing error contract retained",result.body["error"] as? String == "codex_or_private_storage_unavailable")
        wire.reject = false; wire.timeout = true; result = send()
        try check("timeout is locally classified without retry",result.status == 503 && wire.mutations == 0 && codex.summary.contains("desktop_read_timeout"))
        result = send(["images":[UUID().uuidString.lowercased()]])
        try check("unavailable attachment fails before owner request",result.status == 503 && wire.mutations == 0 && codex.summary.contains("image_inputs_unavailable"))
        try check("stage summary excludes input and identity",!codex.summary.contains(task) && !codex.summary.contains("synthetic"))
        enabled = false; result = read()
        try check("cached read respects disabled feature",result.status == 403)
        let diagnostics = MacTab5InteractionDiagnostics(clock:{100})
        let token = diagnostics.begin(kind:"codex",operation:"send",transport:"USB")
        diagnostics.finish(token,status:503,body:["error":"codex_or_private_storage_unavailable","text":"PRIVATE_TEXT","taskId":task])
        try check("send failure included in bounded diagnostics",token != nil && diagnostics.summary.contains("发送消息") && diagnostics.summary.contains("codex_or_private_storage_unavailable"))
        let unknown = diagnostics.begin(kind:"codex",operation:"send",transport:"USB")
        diagnostics.finish(unknown,status:503,body:["error":"PRIVATE_ERROR"])
        try check("diagnostics exclude arbitrary peer payload",!diagnostics.summary.contains("PRIVATE") && !diagnostics.summary.contains(task) && diagnostics.summary.contains("未确认"))
        return checks
    }
}
