import Foundation

final class MacClaudeReplyReader {
    private struct Binding { let session: UUID; var title: String; var touched: Date }
    private let lock = NSLock(), root: URL, enabled: () -> Bool
    private var bindings: [String:Binding] = [:]
    init(root: URL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/projects"),
         enabled: @escaping () -> Bool = { true }) {
        self.root = root; self.enabled = enabled
    }
    var isEnabled: Bool { enabled() }
    func invalidate() { lock.lock(); bindings.removeAll(); lock.unlock() }
    func advertise(sessionID: UUID?, title: String?, now: Date) -> String? {
        guard enabled(), let sessionID else { invalidate(); return nil }
        lock.lock(); defer { lock.unlock() }
        bindings = bindings.filter { now.timeIntervalSince($0.value.touched) < 600 }
        let key = bindings.first(where:{ $0.value.session == sessionID })?.key ?? UUID().uuidString.replacingOccurrences(of:"-",with:"").lowercased()
        if bindings.count >= 32, bindings[key] == nil, let oldest = bindings.min(by:{ $0.value.touched < $1.value.touched })?.key { bindings.removeValue(forKey:oldest) }
        bindings[key] = Binding(session:sessionID,title:title ?? "Claude Code",touched:now)
        return key
    }
    func handle(_ request: [String:Any], now: Date = Date()) -> MacTab5RPCResult {
        guard enabled() else { invalidate(); return .error(403,"claude_replies_disabled") }
        guard request["op"] as? String == "read", let key = request["binding"] as? String,
              let requested = MacTab5Protocol.integer(request["page"]), (-1...4095).contains(requested) else { return .error(400,"invalid_read") }
        let page = Int(requested)
        lock.lock(); let bound = bindings[key]; lock.unlock()
        guard let bound, now.timeIntervalSince(bound.touched) < 600 else { return .error(409,"session_changed") }
        do {
            guard let files = FileManager.default.enumerator(at:root,includingPropertiesForKeys:[.isRegularFileKey,.isSymbolicLinkKey],options:[.skipsHiddenFiles]) else { throw MacTab5Error.unavailable }
            var target: URL?
            for case let file as URL in files where file.pathExtension == "jsonl" && !file.pathComponents.contains("subagents") {
                if UUID(uuidString:file.deletingPathExtension().lastPathComponent) == bound.session {
                    guard target == nil else { throw MacTab5Error.unconfirmed }; target = file
                }
            }
            guard let target else { throw MacTab5Error.unavailable }
            let reply = try MacClaudePublicReplies.read(target,sessionID:bound.session)
            let revision = MacTab5Protocol.sha256(Data(reply.text.utf8))
            if let expected = request["revision"] as? String, !expected.isEmpty, expected != revision { return .error(409,"reply_changed") }
            var pages: [String] = [], current = "", bytes = 0
            for scalar in reply.text.unicodeScalars {
                let value = String(scalar), count = value.utf8.count
                if bytes + count > 1600 { pages.append(current); current = ""; bytes = 0 }
                current += value; bytes += count
            }
            if !current.isEmpty || pages.isEmpty { pages.append(current) }
            let selected = page < 0 ? pages.count-1 : page
            guard selected < pages.count else { return .error(409,"reply_changed") }
            guard enabled() else { invalidate(); return .error(403,"claude_replies_disabled") }
            lock.lock(); let valid = bindings[key]?.session == bound.session; lock.unlock()
            guard valid else { return .error(409,"session_changed") }
            return MacTab5RPCResult(200,["version":1,"binding":key,"revision":revision,"title":bound.title,
                "text":pages[selected],"page":selected,"total":pages.count,"hasEarlier":reply.hasEarlier])
        } catch { return .error(503,"claude_reply_unavailable") }
    }
}
