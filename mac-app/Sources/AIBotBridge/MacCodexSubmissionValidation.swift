import Foundation

enum MacCodexSubmissionValidation {
    private final class Wire: MacCodexDesktopIPC.Wire {
        let task: String, owner = UUID().uuidString.lowercased(), client = UUID().uuidString.lowercased()
        var state: [String: Any], replies: [[String: Any]] = [], mutations = 0, snapshots = 0
        init(task: String, state: [String: Any]) { self.task = task; self.state = state }
        func write(_ value: [String: Any], until: TimeInterval) throws {
            let method = value["method"] as? String ?? ""
            var response: [String: Any] = ["type": "response", "requestId": value["requestId"] ?? "", "resultType": "success", "handledByClientId": owner]
            if method == "initialize" { response["result"] = ["clientId": client]; replies.append(response) }
            else if method == "thread-owner-discovery" { response["result"] = ["supportsUntrustedAppInput": true]; replies.append(response) }
            else if method == "thread-stream-following-changed" {
                if (value["params"] as? [String: Any])?["following"] as? Bool == true {
                    snapshots += 1
                    replies.append(["type": "broadcast", "method": "thread-stream-state-changed", "sourceClientId": owner, "version": 11,
                        "params": ["hostId": "local", "conversationId": task, "change": ["type": "snapshot", "conversationState": state]]])
                }
            } else { mutations += 1; throw MacCodexDesktopError.invalidPacket }
        }
        func read(until: TimeInterval) throws -> [String: Any] {
            guard !replies.isEmpty else { throw MacCodexDesktopError.timeout }; return replies.removeFirst()
        }
        func close() {}
    }
    static func run(root: URL) throws -> Int {
        var count = 0
        func check(_ name: String, _ value: @autoclosure () throws -> Bool) throws {
            guard try value() else { throw MacTab5LocalError.invalid("Submission receipt: " + name) }; count += 1
        }
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased(), owner = UUID().uuidString.lowercased()
        let text = "receipt fixture"
        func message(_ id: String, text: String = "receipt fixture") -> [String: Any] {
            ["type": "userMessage", "id": id, "content": [["type": "text", "text": text]]]
        }
        func steer(_ id: String, status: String) -> [String: Any] {
            ["type": "steeringUserMessage", "id": id, "status": status, "clientUserMessageId": "client-" + id,
             "input": [["type": "text", "text": text]]]
        }
        func state(_ items: [[String: Any]], started: Int = 1000, turnID: String? = nil) -> [String: Any] {
            ["id": task, "cwd": "/fixture", "requests": [Any](), "threadRuntimeStatus": ["type": "active"],
             "turns": [["turnId": turnID ?? turn, "turnStartedAtMs": started, "status": "inProgress", "items": items]]]
        }
        func receipt(_ items: [[String: Any]]) throws -> MacCodexSubmissionReceipt {
            var value = try MacCodexSubmissionReceipt.parse(state(items), task: task, owner: owner, expected: text)
            value.queue = .init(known: [], matching: []); value.serverQueue = .init(known: [], matching: [])
            return value
        }
        let baseline = try receipt([message("old")])
        try check("old identical message cannot confirm", !receipt([message("old")]).confirms(after: baseline))
        try check("new exact accepted message confirms", receipt([message("old"), message("new")]).confirms(after: baseline))
        try check("other text cannot confirm", !receipt([message("old"), message("new", text: "different")]).confirms(after: baseline))
        try check("paragraph endings normalize only at end", MacCodexSubmissionReceipt.parse(state([message("new", text: text + "\n")]), task: task, owner: owner, expected: text).confirms(after: baseline))
        try check("leading whitespace remains significant", !receipt([message("new", text: " " + text)]).confirms(after: baseline))
        for status in ["pending", "failed", "rejected", "unknown"] {
            try check("unaccepted steering never confirms", !receipt([steer("new", status: status)]).confirms(after: baseline))
        }
        try check("accepted steering confirms", receipt([steer("new", status: "accepted")]).confirms(after: baseline))
        let pending = try receipt([steer("old-pending", status: "pending")])
        try check("older pending becoming accepted is not our send", !receipt([steer("old-pending", status: "accepted")]).confirms(after: pending))
        var converted = message("new-server-id"); converted["clientId"] = "client-old-pending"
        try check("alias transition preserves old identity", !receipt([converted]).confirms(after: pending))
        var truncated = message("new"); truncated["content"] = [["type": "text", "text": text, "truncated": true]]
        try check("truncated body cannot confirm", !receipt([truncated]).confirms(after: baseline))
        var attachment = message("new"); attachment["content"] = [["type": "text", "text": text], ["type": "image", "url": "fixture"]]
        try check("partial text match with attachment refused", !receipt([attachment]).confirms(after: baseline))
        try check("owner change cannot reuse baseline", !MacCodexSubmissionReceipt.parse(state([message("new")]), task: task, owner: UUID().uuidString.lowercased(), expected: text).confirms(after: baseline))
        try check("older history loaded later cannot confirm", !MacCodexSubmissionReceipt.parse(state([message("new")], started: 500), task: task, owner: owner, expected: text).confirms(after: baseline))
        try check("new turn accepted input confirms", MacCodexSubmissionReceipt.parse(state([message("new")], started: 2000, turnID: UUID().uuidString.lowercased()), task: task, owner: owner, expected: text).confirms(after: baseline))
        do {
            _ = try MacCodexSubmissionReceipt.parse(state([]), task: UUID().uuidString.lowercased(), owner: owner, expected: text)
            throw MacTab5LocalError.invalid("wrong task accepted")
        } catch MacCodexDesktopError.incompatible { count += 1 }

        let wire = Wire(task: task, state: state([message("old")]))
        let ipc = MacCodexDesktopIPC(connect: { wire }, queueReceipt: { _, _ in .init(known: [], matching: []) }, serverQueueReceipt: { _, _ in .init(known: [], matching: []) })
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 7, count: 32) }), enabled: { true }, sendEnabled: { true }, desktop: ipc, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "name": "Fixture", "cwd": "/fixture"]]] }
            throw MacCodexDesktopError.invalidPacket
        })
        codex.refresh()
        var value = "", actions = 0
        let backend = MacTab5DesktopBackend(open: { _ in true }, read: { _ in value }, write: { _, before, after in
            guard value == before else { return nil }; value = after; return after
        }, diagnostic: { .submitReadbackUnavailable }, target: { _ in "main" }, submitComposer: { _, _ in
            actions += 1; wire.state = state([message("old"), steer("new", status: "accepted")]); return .unconfirmed
        })
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        let stage = UUID().uuidString.lowercased(), submit = UUID().uuidString.lowercased()
        try check("fixture stages", desktop.handle(["op": "stage-draft", "taskId": task, "requestId": stage, "text": text], device: "A").status == 200)
        let request: [String: Any] = ["op": "submit-draft", "taskId": task, "requestId": submit, "draftRequestId": stage]
        try check("native readback failure reconciles with accepted input", desktop.handle(request, device: "A").status == 200 && actions == 1 && wire.mutations == 0)
        try check("specific success diagnostic", desktop.summary == MacTab5DesktopCheck.messageAccepted.rawValue)
        let reads = wire.snapshots
        try check("repeat only returns stored receipt", desktop.handle(request, device: "A").status == 200 && actions == 1 && wire.snapshots == reads)
        let restarted = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        try check("restart cannot resend", restarted.handle(request, device: "A").status == 200 && actions == 1 && wire.mutations == 0)
        func queued(_ id: String, prompt: String = "receipt fixture", status: String = "queued") -> [String: Any] {
            ["id": id, "context": ["prompt": prompt, "addedFiles": [Any](), "fileAttachments": [Any](), "imageAttachments": [Any]()],
             "submission": ["hostId": "local", "status": status]]
        }
        func queue(_ messages: [[String: Any]]) -> MacCodexQueueReceipt? {
            .parse(["queued-follow-ups": [task: messages]], task: task, expected: text)
        }
        let oldID = UUID().uuidString.lowercased(), newID = UUID().uuidString.lowercased()
        var beforeQueue = baseline; beforeQueue.queue = queue([queued(oldID)])
        var afterQueue = baseline; afterQueue.queue = queue([queued(oldID), queued(newID)])
        try check("new committed queue entry confirms without accepted turn input", afterQueue.confirmsQueue(after: beforeQueue) && !afterQueue.confirms(after: beforeQueue))
        try check("unchanged queue cannot confirm", !beforeQueue.confirmsQueue(after: beforeQueue))
        var unknownQueue = baseline; unknownQueue.queue = nil
        try check("missing baseline queue fails closed", !afterQueue.confirmsQueue(after: unknownQueue))
        try check("unseen old queue could become accepted; missing baseline fails closed", !receipt([message("new")]).confirms(after: unknownQueue))
        try check("different prompt cannot confirm queue", queue([queued(newID, prompt: "other")])?.matching.isEmpty == true)
        try check("pending committed local receipt is queued, not model acceptance", queue([queued(newID, status: "pending")])?.matching.count == 1)
        for status in ["failed", "outcome-unknown", "sending", "unexpected"] {
            try check("ambiguous queue status rejected", queue([queued(newID, status: status)])?.matching.isEmpty == true)
        }
        var invalid = queued(newID); invalid["submission"] = ["hostId": "other", "status": "queued"]
        try check("remote host rejected", queue([invalid])?.matching.isEmpty == true)
        invalid = queued(newID); invalid["pausedReason"] = "fixture"
        try check("paused queue rejected", queue([invalid])?.matching.isEmpty == true)
        invalid = queued(newID); var context = invalid["context"] as! [String: Any]; context["imageAttachments"] = [["id": "fixture"]]; invalid["context"] = context
        try check("queued attachment partial match rejected", queue([invalid])?.matching.isEmpty == true)
        invalid = queued(newID); invalid["id"] = ""
        try check("malformed queue fails closed", queue([invalid]) == nil)
        try check("duplicate queue identity fails closed", queue([queued(newID), queued(newID)]) == nil)
        try check("missing queue map is unavailable", MacCodexQueueReceipt.parse([:], task: task, expected: text) == nil)
        try check("missing task in queue map is empty", MacCodexQueueReceipt.parse(["queued-follow-ups": [:]], task: task, expected: text)?.known.isEmpty == true)
        var acceptedOldQueue = message("server-converted"); acceptedOldQueue["clientId"] = oldID
        try check("old queued message accepted later cannot confirm another submit", !receipt([acceptedOldQueue]).confirms(after: beforeQueue))
        var changedOwner = try MacCodexSubmissionReceipt.parse(state([]), task: task, owner: UUID().uuidString.lowercased(), expected: text); changedOwner.queue = afterQueue.queue
        try check("changed owner cannot confirm queue", !changedOwner.confirmsQueue(after: beforeQueue))
        func serverEntry(_ id: String, client: String, body: String = "receipt fixture") -> [String: Any] {
            ["id": id, "clientUserMessageId": client, "input": [["type": "text", "text": body]]]
        }
        func server(_ entries: [[String: Any]]) -> MacCodexQueueReceipt? {
            .server(["data": entries, "nextCursor": NSNull()], expected: text)
        }
        var beforeServer = beforeQueue; beforeServer.serverQueue = server([])
        var afterServer = baseline; afterServer.serverQueue = server([serverEntry("new-server", client: newID)])
        try check("new server queue confirms", afterServer.confirmsQueue(after: beforeServer))
        try check("server queue is not model acceptance", !afterServer.confirms(after: beforeServer))
        afterServer.serverQueue = server([serverEntry("new-server", client: oldID)])
        try check("old local queue migrating to server cannot confirm", !afterServer.confirmsQueue(after: beforeServer))
        afterServer.serverQueue = server([serverEntry(oldID, client: newID)])
        try check("old server identity with a changed client alias cannot confirm", !afterServer.confirmsQueue(after: beforeServer))
        invalid = queued(newID); invalid["clientUserMessageId"] = oldID
        var aliasedQueue = baseline; aliasedQueue.queue = queue([invalid])
        try check("old client alias in a new local entry cannot confirm", !aliasedQueue.confirmsQueue(after: beforeQueue))
        var unknownServer = beforeQueue; unknownServer.serverQueue = nil
        try check("unknown baseline server queue cannot confirm", !afterServer.confirmsQueue(after: unknownServer))
        try check("other server queue body rejected", server([serverEntry("new-server", client: newID, body: "other")])?.matching.isEmpty == true)
        try check("server trailing paragraph marker permitted", server([serverEntry("new-server", client: newID, body: text + "\n")])?.matching.count == 1)
        var serverAttachment = serverEntry("new-server", client: newID); serverAttachment["input"] = [["type": "text", "text": text], ["type": "image", "url": "fixture"]]
        try check("server partial attachment match refused", server([serverAttachment])?.matching.isEmpty == true)
        try check("incomplete server queue page cannot prove new identity", MacCodexQueueReceipt.server(["data": [], "nextCursor": "more"], expected: text) == nil)
        var oldServer = baseline; oldServer.serverQueue = server([serverEntry("old-server", client: oldID)])
        try check("old server queue accepted later cannot confirm", !receipt([acceptedOldQueue]).confirms(after: oldServer))

        let queueFile = root.appendingPathComponent("queue-fixture.json")
        try JSONSerialization.data(withJSONObject: ["queued-follow-ups": [task: [queued(newID)]]]).write(to: queueFile)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: queueFile.path)
        try check("bounded committed queue file reads", MacCodexQueueReceipt.read(queueFile, task: task, expected: text)?.matching.count == 1)
        let queueLink = root.appendingPathComponent("queue-symlink.json")
        try FileManager.default.createSymbolicLink(at: queueLink, withDestinationURL: queueFile)
        try check("queue symlink refused", MacCodexQueueReceipt.read(queueLink, task: task, expected: text) == nil)
        try FileManager.default.setAttributes([.posixPermissions: 0o666], ofItemAtPath: queueFile.path)
        try check("writable-by-others queue refused", MacCodexQueueReceipt.read(queueFile, task: task, expected: text) == nil)

        let queueWire = Wire(task: task, state: state([message("old")]))
        var queueMessages = [queued(oldID)], queueActions = 0, queueValue = ""
        let queueIPC = MacCodexDesktopIPC(connect: { queueWire }, queueReceipt: { _, _ in queue(queueMessages) }, serverQueueReceipt: { _, _ in server([]) })
        let queueCodex = MacTab5Codex(store: .init(root: root.appendingPathComponent("queued"), key: { Data(repeating: 9, count: 32) }), enabled: { true }, sendEnabled: { true }, desktop: queueIPC, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "name": "Queue fixture", "cwd": "/fixture"]]] }
            throw MacCodexDesktopError.invalidPacket
        })
        queueCodex.refresh()
        let queueBackend = MacTab5DesktopBackend(open: { _ in true }, read: { _ in queueValue }, write: { _, before, after in
            guard queueValue == before else { return nil }; queueValue = after; return after
        }, diagnostic: { .submitReadbackUnavailable }, target: { _ in "main" }, submitComposer: { _, _ in
            queueActions += 1; queueMessages.append(queued(newID)); return .unconfirmed
        })
        let queueDesktop = MacTab5Desktop(codex: queueCodex, enabled: { true }, backend: queueBackend)
        let queueStage = UUID().uuidString.lowercased()
        try check("queue fixture stages", queueDesktop.handle(["op": "stage-draft", "taskId": task, "requestId": queueStage, "text": text], device: "A").status == 200)
        let queueRequest: [String: Any] = ["op": "submit-draft", "taskId": task, "requestId": UUID().uuidString.lowercased(), "draftRequestId": queueStage]
        try check("committed queue reconciles missing editor readback", queueDesktop.handle(queueRequest, device: "A").status == 200 && queueActions == 1 && queueWire.mutations == 0)
        try check("queue diagnostic does not claim model acceptance", queueDesktop.summary == MacTab5DesktopCheck.messageQueued.rawValue)
        let queueReads = queueWire.snapshots
        try check("queued receipt replays without resending or rereading", queueDesktop.handle(queueRequest, device: "A").status == 200 && queueActions == 1 && queueWire.snapshots == queueReads)
        let queueRestart = MacTab5Desktop(codex: queueCodex, enabled: { true }, backend: queueBackend)
        try check("queued receipt survives bridge restart", queueRestart.handle(queueRequest, device: "A").status == 200 && queueActions == 1)
        var serverCommitted = false, serverActions = 0, serverValue = ""
        let serverWire = Wire(task: task, state: state([message("old")]))
        let serverIPC = MacCodexDesktopIPC(connect: { serverWire }, queueReceipt: { _, _ in queue([]) }, serverQueueReceipt: { _, _ in
            server(serverCommitted ? [serverEntry("server-queue-id", client: newID)] : [])
        })
        let serverCodex = MacTab5Codex(store: .init(root: root.appendingPathComponent("server-queued"), key: { Data(repeating: 8, count: 32) }), enabled: { true }, sendEnabled: { true }, desktop: serverIPC, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": task, "name": "Server queue fixture", "cwd": "/fixture"]]] }
            throw MacCodexDesktopError.invalidPacket
        })
        serverCodex.refresh()
        let serverBackend = MacTab5DesktopBackend(open: { _ in true }, read: { _ in serverValue }, write: { _, before, after in
            guard serverValue == before else { return nil }; serverValue = after; return after
        }, diagnostic: { .submitReadbackUnavailable }, target: { _ in "main" }, submitComposer: { _, _ in
            serverActions += 1; serverCommitted = true; return .unconfirmed
        })
        let serverDesktop = MacTab5Desktop(codex: serverCodex, enabled: { true }, backend: serverBackend)
        let serverStage = UUID().uuidString.lowercased()
        try check("server queue fixture stages", serverDesktop.handle(["op": "stage-draft", "taskId": task, "requestId": serverStage, "text": text], device: "A").status == 200)
        let serverRequest: [String: Any] = ["op": "submit-draft", "taskId": task, "requestId": UUID().uuidString.lowercased(), "draftRequestId": serverStage]
        try check("server queue reconciles native unknown", serverDesktop.handle(serverRequest, device: "A").status == 200 && serverActions == 1 && serverWire.mutations == 0)
        try check("server queue reports queued receipt", serverDesktop.summary == MacTab5DesktopCheck.messageQueued.rawValue)
        try check("server queue replay never resends", serverDesktop.handle(serverRequest, device: "A").status == 200 && serverActions == 1)
        return count
    }
}
