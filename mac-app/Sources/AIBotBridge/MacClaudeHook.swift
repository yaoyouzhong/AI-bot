import Foundation

enum MacClaudeHook {
    static let events: Set<String> = ["UserPromptSubmit", "PreToolUse", "PostToolUse", "PostToolUseFailure", "PostToolBatch", "PermissionRequest", "Elicitation", "Stop", "StopFailure", "Notification", "SessionEnd"]
    static func project(_ data: Data, at: Date = Date()) -> [String:Any]? {
        guard data.count <= 8_388_608,
              let raw = (try? JSONSerialization.jsonObject(with:data)) as? [String:Any],
              let kind = raw["hook_event_name"] as? String, events.contains(kind), raw["agent_id"] == nil,
              let sid = raw["session_id"] as? String, UUID(uuidString:sid) != nil,
              let pid = raw["prompt_id"] as? String, UUID(uuidString:pid) != nil else { return nil }
        var result: [String:Any] = ["agent":"claude","event":kind,"sessionId":sid,"promptId":pid,"emittedAt":at.timeIntervalSince1970*1000]
        if kind == "Notification" {
            guard raw["notification_type"] as? String == "idle_prompt" else { return nil }
            result["notificationType"] = "idle_prompt"
        }
        return result
    }
    // No stdout, policy decisions or configuration writes. Forwarding failure
    // must not block the user's task or manufacture success.
    static func run(port: UInt16) {
        let at = Date(); var data = Data()
        while let chunk = try? FileHandle.standardInput.read(upToCount:65_536), !chunk.isEmpty {
            data.append(chunk); if data.count > 8_388_608 { return }
        }
        guard let body = project(data,at:at), let bytes = try? JSONSerialization.data(withJSONObject:body) else { return }
        var request = URLRequest(url:URL(string:"http://127.0.0.1:\(port)/event")!); request.httpMethod = "POST"
        request.httpBody = bytes; request.setValue("application/json",forHTTPHeaderField:"Content-Type"); request.timeoutInterval = 1
        let config = URLSessionConfiguration.ephemeral; config.connectionProxyDictionary = [:]
        let session = URLSession(configuration:config), done = DispatchSemaphore(value:0)
        session.dataTask(with:request) { _,_,_ in done.signal() }.resume()
        _ = done.wait(timeout:.now()+1.2); session.invalidateAndCancel()
    }
}
