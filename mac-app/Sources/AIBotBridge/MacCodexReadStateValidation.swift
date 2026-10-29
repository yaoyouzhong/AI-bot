import Foundation

enum MacCodexReadStateValidation {
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ value: @autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("Read synchronization: " + name) }; count += 1
        }
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased(), next = UUID().uuidString.lowercased()
        var now = 100.0, snapshot: MacCodexReadState.Snapshot? = .init(scope: "one", unread: [task])
        let sync = MacCodexReadState(load: { snapshot }, clock: { now })
        try check("desktop unread retained", sync.synchronize([task: turn]).isEmpty)
        snapshot = .init(scope: "one", unread: []); now += 4.9
        try check("completion write has settling time", sync.synchronize([task: turn]).isEmpty)
        now += 0.2
        try check("desktop read binds exact turn", sync.synchronize([task: turn])[task] == turn)
        snapshot = nil
        try check("failed read preserves prior success", sync.synchronize([task: turn])[task] == turn)
        try check("prior turn never acknowledges new turn", sync.synchronize([task: next]).isEmpty)
        snapshot = .init(scope: "one", unread: []); now += 10
        try check("recovery starts a new settle interval", sync.synchronize([task: next]).isEmpty)
        now += 6
        try check("recovered metadata can acknowledge", sync.synchronize([task: next])[task] == next)
        snapshot = .init(scope: "one", unread: [task])
        try check("desktop unread revokes desktop receipt", sync.synchronize([task: next]).isEmpty)
        snapshot = .init(scope: "two", unread: []); now += 20
        try check("another account cannot inherit observation", sync.synchronize([task: next]).isEmpty)
        now -= 100
        try check("clock rollback restarts settling", sync.synchronize([task: next]).isEmpty)
        now += 6
        try check("new scope settles", sync.synchronize([task: next])[task] == next)
        try check("removed ready entry prunes acknowledgement", sync.synchronize([:]).isEmpty)
        func json(_ value: Any) throws -> Data { try JSONSerialization.data(withJSONObject: value, options: [.withoutEscapingSlashes]) }
        let claims: [String: Any] = ["exp": 1000, "https://api.openai.com/auth": ["chatgpt_account_id": "fixture-account", "user_id": "fixture-user"]]
        let payload = try json(claims).base64EncodedString().replacingOccurrences(of: "+", with: "-").replacingOccurrences(of: "/", with: "_").replacingOccurrences(of: "=", with: "")
        let auth = try json(["tokens": ["access_token": "fixture." + payload + ".fixture"]])
        let identity = MacTab5Protocol.sha256(try json(["chatgpt", "fixture-account", "fixture-user"]))
        let host = "local:" + MacTab5Protocol.sha256(try json(["local", "local", NSNull()]))
        func state(_ ids: Any, key: String? = nil, version: Any = 1) throws -> Data {
            try json(["electron-thread-read-state-v1": ["version": version, "unreadByIdentity": [key ?? identity: [host: ids]]]])
        }
        try check("valid local bucket", MacCodexReadState.parse(auth: auth, state: state([task]), now: 100)?.unread == [task])
        try check("explicit empty bucket is valid", MacCodexReadState.parse(auth: auth, state: state([String]()), now: 100)?.unread == [])
        try check("wrong account is unknown", MacCodexReadState.parse(auth: auth, state: state([task], key: "other"), now: 100) == nil)
        try check("expired account unavailable", MacCodexReadState.parse(auth: auth, state: state([]), now: 1000) == nil)
        try check("unknown schema unavailable", MacCodexReadState.parse(auth: auth, state: state([], version: 2), now: 100) == nil)
        try check("boolean version rejected", MacCodexReadState.parse(auth: auth, state: state([], version: true), now: 100) == nil)
        try check("malformed task list unavailable", MacCodexReadState.parse(auth: auth, state: state(["bad-id"]), now: 100) == nil)
        try check("corrupt state unavailable", MacCodexReadState.parse(auth: auth, state: Data(), now: 100) == nil)

        now = 100; snapshot = .init(scope: "fixture", unread: [task])
        let integrated = MacCodexReadState(load: { snapshot }, clock: { now })
        let codex = MacTab5Codex(store: MacTab5PrivateStore(root: root, key: { Data(repeating: 1, count: 32) }), enabled: { true }, clock: { now }, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/fixture", "name": "fixture"]]] }
            return ["thread": ["id": task, "status": ["type": "idle"], "turns": [["id": turn, "status": "completed", "items": [["type": "agentMessage", "text": "fixture reply"]]]]]]
        }, readState: integrated)
        codex.refresh()
        try check("read succeeds", codex.handle(["op": "read", "taskId": task], device: "fixture").status == 200)
        try check("snapshot initially unread", (codex.snapshot()?["repliesUnread"] as? [String: String])?[task] == turn)
        snapshot = .init(scope: "fixture", unread: []); now += 6; codex.refresh()
        try check("regular refresh exports desktop read", (codex.snapshot()?["repliesRead"] as? [String: String])?[task] == turn)
        try check("regular refresh removes unread", (codex.snapshot()?["repliesUnread"] as? [String: String])?[task] == nil)
        let failedSnapshot: [String: Any] = ["id": task, "cwd": "/fixture", "threadRuntimeStatus": ["type": "systemError"], "requests": [],
            "turns": [["turnId": turn, "status": "failed", "turnStartedAtMs": 1000,
                       "items": [["type": "agentMessage", "phase": "commentary", "text": "published before failure"], ["type": "reasoning", "text": "PRIVATE"]]]]]
        let failed = try MacCodexDesktopIPC.parse(failedSnapshot, task: task)
        try check("system error retains exact published failed turn", failed.turn == turn && failed.status == "failed" && failed.publicItems.count == 1)
        try check("system error does not enable sending or running controls", !failed.canSend && !failed.running)
        for operation in ["send", "steer", "interrupt"] {
            do {
                _ = try MacCodexDesktopIPC.command(state: failed, operation: operation, expected: turn, request: UUID().uuidString.lowercased(), inputs: [["type":"text", "text":"fixture"]])
                throw MacTab5Error.invalidPacket
            } catch is MacCodexDesktopError { count += 1 }
        }
        var unknown = failedSnapshot; unknown["threadRuntimeStatus"] = ["type":"future-unknown"]
        do { _ = try MacCodexDesktopIPC.parse(unknown,task:task); throw MacTab5Error.invalidPacket }
        catch is MacCodexDesktopError { count += 1 }
        let other = UUID().uuidString.lowercased()
        var dropReadTask = false
        let reduced = MacTab5Codex(store: .init(root: root.appendingPathComponent("reduced"), key: {Data(repeating:1,count:32)}), enabled: {true}, clock: {now}, call: { method, _ in
            if method == "thread/list" { return ["data": [["id":other,"cwd":"/fixture","name":"newest","updatedAt":20]] + (dropReadTask ? [] : [["id":task,"cwd":"/fixture","name":"read older","updatedAt":10]])] }
            return ["thread": ["id":task,"status":["type":"idle"],"turns":[["id":turn,"status":"completed","items":[["type":"agentMessage","text":"fixture"]]]]]]
        }, readState: MacCodexReadState(load:{ .init(scope:"fixture",unread:[]) },clock:{now}))
        reduced.refresh(); _ = reduced.handle(["op":"read","taskId":task],device:"fixture"); now += 6; reduced.refresh()
        dropReadTask = true; reduced.refresh()
        let tiny = reduced.snapshot(taskLimit:1)!
        try check("reduced catalog excludes acknowledged task", (tiny["tasks"] as? [[String:Any]])?.contains { $0["id"] as? String == task } == false)
        try check("read receipt survives transport catalog reduction", (tiny["repliesRead"] as? [String:String])?[task] == turn)
        return count
    }
}
