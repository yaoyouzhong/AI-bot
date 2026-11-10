import Foundation

// Metadata only: never persist conversation text or credentials.
struct MacActivityTotals {
    var codex = ToolState(state: "offline", ageSeconds: nil)
    var claude = ToolState(state: "offline", ageSeconds: nil)
    var domestic: MacDomesticActivity? = nil
}
struct MacProviderActivity: Codable {
    var tokensToday: Int64
    var lastActivity: Int64
}

struct MacDomesticActivity: Codable {
    var activeProvider: String
    var state: String
    var needsInput: Bool
    var tokensToday: Int64
    var providers: [String: MacProviderActivity]? = nil
}

// Disk enumeration/parsing can take seconds for an active large session.
// Never make UI, Apple Events or transport snapshots wait for that scan.
final class MacActivitySampler {
    private let lock = NSLock()
    private let queue: DispatchQueue
    private let scan: (Date) -> MacActivityTotals
    private var latest = MacActivityTotals(codex: ToolState(state: "unknown", ageSeconds: nil),
                                           claude: ToolState(state: "unknown", ageSeconds: nil))
    private var sampledAt: Date?
    private var nextScan = Date.distantPast
    private var scanning = false

    init(queue: DispatchQueue = DispatchQueue(label: "AI-bot.activity-sampling", qos: .utility),
         scan: ((Date) -> MacActivityTotals)? = nil) {
        self.queue = queue
        let metadata = MacActivityMetadata()
        self.scan = scan ?? { metadata.capture(now: $0) }
    }

    func capture(now: Date) -> MacActivityTotals {
        lock.lock()
        var value = latest
        if let sampledAt {
            let elapsed = max(0, Int64(now.timeIntervalSince(sampledAt)))
            if let age = value.codex.ageSeconds {
                value.codex.ageSeconds = age + elapsed
                if age + elapsed > 900 { value.codex.state = "offline" }
            }
            if let age = value.claude.ageSeconds {
                value.claude.ageSeconds = age + elapsed
                value.claude.state = SessionActivityReader.classify(ageSeconds: age + elapsed)
            }
            // An unfinished scan is not evidence that an old task is still active.
            if elapsed > 30 {
                value.codex.state = "unknown"; value.claude.state = "unknown"
                value.domestic?.state = "unknown"
            }
        }
        let shouldScan = !scanning && now >= nextScan
        if shouldScan { scanning = true; nextScan = now.addingTimeInterval(5) }
        lock.unlock()
        if shouldScan {
            queue.async { [self] in
                let result = scan(now)
                lock.lock()
                latest = result; sampledAt = now; scanning = false
                lock.unlock()
            }
        }
        return value
    }
}

final class MacActivitySignals {
    let claudeIdentity = MacClaudeIdentity()
    private let lock = NSLock()
    private var states: [String: (String, Date)] = [:]
    private var attention: [String: Date] = [:]
    private var completedAt: Int64 = 0
    private var acknowledged: Int64 = 0
    private var baselineLoaded = false
    @discardableResult func record(agent: String, event: String, message: String = "", now: Date = Date()) -> Bool {
        guard ["claude", "codex"].contains(agent) else { return false }
        if agent == "claude" { claudeIdentity.invalidate() }
        lock.lock(); defer { lock.unlock() }
        if event == "PermissionRequest" || event == "Elicitation" || event == "Notification" && ["permission", "approve", "approval"].contains(where: { message.lowercased().contains($0) }) {
            attention[agent] = now; return true
        }
        if agent == "codex", event == "TaskComplete" {
            completedAt = Int64(now.timeIntervalSince1970); states[agent] = ("idle", now); attention[agent] = nil; return true
        }
        if agent == "claude", event == "Stop" {
            // Stop is emitted before other hooks can resume the same turn.
            // Keep pending attention and mark the lifecycle unconfirmed.
            states[agent] = ("unknown", now); return true
        }
        let working = ["UserPromptSubmit", "PreToolUse", "PostToolUse", "SubagentStart", "SubagentStop", "PreCompact", "PostCompact", "WorktreeCreate"].contains(event)
        guard working || ["Stop", "SessionStart", "SessionEnd"].contains(event) else { return false }
        states[agent] = (working ? "working" : "idle", now)
        // A legacy Claude Stop or subagent hook has no turn identity and cannot
        // prove the user's pending question or authorization was resolved.
        if agent != "claude" || ["UserPromptSubmit", "SessionEnd"].contains(event) { attention[agent] = nil }
        if agent == "codex", working { acknowledged = max(acknowledged, completedAt) }
        return true
    }
    // Only overlay a provider observation when the explicit client hook is
    // newer than that provider's log; old cached providers cannot steal it.
    func recentClaudeState(after epoch: Int64, now: Date) -> (state: String, needsInput: Bool)? {
        lock.lock(); defer { lock.unlock() }
        if let attentionAt = attention["claude"], Int64(attentionAt.timeIntervalSince1970) > epoch,
           (0..<300).contains(now.timeIntervalSince(attentionAt)) { return ("working", true) }
        guard let state = states["claude"], Int64(state.1.timeIntervalSince1970) > epoch,
              (0..<(state.0 == "working" ? 600.0 : 60.0)).contains(now.timeIntervalSince(state.1)) else { return nil }
        return (state.0, false)
    }
    func acknowledge() { claudeIdentity.acknowledge(); lock.lock(); acknowledged = max(acknowledged, completedAt); lock.unlock() }
    func apply(_ agent: String, raw: ToolState, now: Date) -> ToolState {
        if agent == "claude", let selected = claudeIdentity.selection(now:now) {
            var result = selected.tool; result.tokensToday = raw.tokensToday
            result.ageSeconds = max(0,Int64(now.timeIntervalSince(selected.activity))); return result
        }
        lock.lock(); defer { lock.unlock() }
        var result = raw
        if agent == "codex" {
            if !baselineLoaded { baselineLoaded=true; if completedAt==0 { acknowledged=raw.completionAt } }
            completedAt = max(completedAt, raw.completionAt)
            result.completionAt = completedAt; result.completionSequence = completedAt
            result.completionActive = completedAt > acknowledged
            if let state = states[agent], state.0 == "working", Int64(state.1.timeIntervalSince1970) >= completedAt { acknowledged = completedAt; result.completionActive = false }
            if raw.state == "working", let age = raw.ageSeconds, Int64(now.timeIntervalSince1970) - age > completedAt { acknowledged = completedAt; result.completionActive = false }
        }
        if let state = states[agent], (0..<(state.0 == "working" ? 600.0 : 60.0)).contains(now.timeIntervalSince(state.1)) { result = withState(result, state.0); result.ageSeconds = max(0, Int64(now.timeIntervalSince(state.1))) }
        if let at = attention[agent] { result.needsInput = (0..<300).contains(now.timeIntervalSince(at)) }
        return result
    }
    private func withState(_ value: ToolState, _ state: String) -> ToolState {
        ToolState(state: state, ageSeconds: value.ageSeconds, needsInput: value.needsInput,
                  completionActive: value.completionActive, completionSequence: value.completionSequence,
                  completionAt: value.completionAt, tokensToday: value.tokensToday)
    }
}

final class MacActivityMetadata {
    private let home: URL
    init(home: URL = FileManager.default.homeDirectoryForCurrentUser) { self.home = home }
    private struct FileValue { var modified: Date; var size: Int; var day: Date; var usage: [String:Int64]; var latest: [String:Date]; var state: String; var eventAt: Date; var completed: Int64; var clientAt: Date }
    private var cache: [URL: FileValue] = [:]
    private var nextScan = Date.distantPast
    private var snapshot = MacActivityTotals()
    func capture(now: Date) -> MacActivityTotals {
        guard now >= nextScan else { return snapshot }; nextScan = now.addingTimeInterval(5)
        let manager = FileManager.default, day = Calendar.current.startOfDay(for: now)
        var usage: [String:Int64] = [:], latest: [String:Date] = [:], newest = Date.distantPast, claudeAt = Date.distantPast, codexState = "offline", completion: Int64 = 0
        var seen = Set<URL>()
        for (path, codex) in [(".claude/projects", false), (".codex/sessions", true)] {
            let root = home.appendingPathComponent(path)
            guard let files = manager.enumerator(at: root, includingPropertiesForKeys: [.contentModificationDateKey,.fileSizeKey,.isRegularFileKey,.isSymbolicLinkKey], options: [.skipsHiddenFiles]) else { continue }
            for case let file as URL in files where file.pathExtension == "jsonl" {
                guard let attributes = try? file.resourceValues(forKeys: [.contentModificationDateKey,.fileSizeKey,.isRegularFileKey,.isSymbolicLinkKey]), attributes.isRegularFile == true, attributes.isSymbolicLink != true,
                      let modified = attributes.contentModificationDate, modified >= min(day,now.addingTimeInterval(-900)) else { continue }
                seen.insert(file)
                if cache[file]?.modified != modified || cache[file]?.size != attributes.fileSize || cache[file]?.day != day {
                    // Bounded line reader; a partial trailing JSON line is ignored and retried after append.
                    guard let stream = InputStream(url: file) else { continue }; stream.open(); defer { stream.close() }
                    var pending = Data(), lines: [String] = [], buffer = [UInt8](repeating: 0, count: 65536), oversized = false
                    while true {
                        let count = stream.read(&buffer, maxLength: buffer.count); if count <= 0 { break }
                        for byte in buffer.prefix(count) {
                            if byte == 10 {
                                if !oversized, let line = String(data: pending, encoding: .utf8), line.contains("usage") || line.contains("session_meta") || line.contains("task_") { lines.append(line) }
                                pending.removeAll(keepingCapacity: true); oversized = false
                            } else if !oversized { if pending.count < 2_000_000 { pending.append(byte) } else { oversized = true; pending.removeAll(keepingCapacity: true) } }
                        }
                    }
                    let value = Self.parse(lines, codex: codex, now: now)
                    cache[file] = FileValue(modified: modified, size: attributes.fileSize ?? 0, day: day, usage: value.usage, latest: value.latest, state: value.state, eventAt: value.eventAt, completed: value.completed, clientAt: value.clientAt)
                }
                guard let value = cache[file] else { continue }
                for (provider, count) in value.usage { usage[provider, default:0] += count }
                for (provider, date) in value.latest { latest[provider] = max(latest[provider] ?? .distantPast, date) }
                if codex {
                    // Long-running tasks can emit token updates long after the
                    // start event. Only that same still-running session may
                    // renew its activity time; a completed task cannot.
                    let at = value.state == "working" && value.eventAt != .distantPast
                        ? max(value.eventAt,value.latest["codex"] ?? .distantPast) : value.eventAt
                    if at > newest { newest = at; codexState = value.state }
                }
                if !codex && !file.pathComponents.contains("subagents") { claudeAt = max(claudeAt, value.clientAt) }
                completion = max(completion, value.completed)
            }
        }
        cache = cache.filter { seen.contains($0.key) }
        func tool(_ provider: String) -> ToolState {
            guard let date = latest[provider] else { return ToolState(state:"offline",ageSeconds:nil,tokensToday:usage[provider] ?? 0) }
            let age = max(0,Int64(now.timeIntervalSince(date)))
            return ToolState(state:SessionActivityReader.classify(ageSeconds:age),ageSeconds:age,tokensToday:usage[provider] ?? 0)
        }
        snapshot = MacActivityTotals(codex:tool("codex"),claude:tool("claude"))
        if claudeAt != .distantPast {
            let age = max(0, Int64(now.timeIntervalSince(claudeAt)))
            snapshot.claude = ToolState(state: SessionActivityReader.classify(ageSeconds: age), ageSeconds: age, tokensToday: usage["claude"] ?? 0)
        }
        if newest != .distantPast { snapshot.codex = ToolState(state: now.timeIntervalSince(newest)>900 ? "offline":codexState,ageSeconds:max(0,Int64(now.timeIntervalSince(newest))),completionAt:completion,tokensToday:usage["codex"] ?? 0) }
        if let domestic = latest.filter({ !["claude","codex"].contains($0.key) }).max(by: { $0.value < $1.value }), now.timeIntervalSince(domestic.value) <= 900 {
            let providers = Dictionary(uniqueKeysWithValues: latest.filter { !["claude", "codex"].contains($0.key) && now.timeIntervalSince($0.value) <= 900 }.map { key, date in
                (key, MacProviderActivity(tokensToday: usage[key] ?? 0, lastActivity: Int64(date.timeIntervalSince1970)))
            })
            snapshot.domestic = MacDomesticActivity(activeProvider:domestic.key,state:now.timeIntervalSince(domestic.value)<90 ? "working":"idle",needsInput:false,tokensToday:usage[domestic.key] ?? 0,providers:providers)
        }
        return snapshot
    }
    static func parse(_ lines: [String], codex: Bool, now: Date) -> (usage:[String:Int64],latest:[String:Date],state:String,eventAt:Date,completed:Int64,clientAt:Date) {
        var usage:[String:Int64]=[:], latest:[String:Date]=[:], state="offline", eventAt=Date.distantPast, completed:Int64=0, subagent=false, clientAt=Date.distantPast
        let formatter=ISO8601DateFormatter(); formatter.formatOptions=[.withInternetDateTime,.withFractionalSeconds]
        let plain=ISO8601DateFormatter()
        for line in lines {
            guard let data=line.data(using:.utf8),let root=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any] else {continue}
            let payload=root["payload"] as? [String:Any] ?? [:]
            if root["type"] as? String == "session_meta" { let source=payload["source"]; subagent = (source as? [String:Any])?["subagent"] != nil || (source as? String)?.contains("subagent") == true }
            guard let stamp=root["timestamp"] as? String,let date=formatter.date(from:stamp) ?? plain.date(from:stamp),date<=now.addingTimeInterval(60) else {continue}
            let today = Calendar.current.isDate(date,inSameDayAs:now)
            // Midnight resets usage, not a recently active client's identity.
            guard today || now.timeIntervalSince(date) <= 900 else { continue }
            if codex {
                if payload["type"] as? String == "token_count",let info=payload["info"] as? [String:Any],let total=info["total_token_usage"] as? [String:Any] { if today { usage["codex"]=max(usage["codex"] ?? 0,(total["total_tokens"] as? NSNumber)?.int64Value ?? 0) }; latest["codex"]=max(latest["codex"] ?? .distantPast,date) }
                if !subagent,let kind=payload["type"] as? String,["task_started","task_complete","task_aborted"].contains(kind),date>=eventAt {eventAt=date;state=kind=="task_started" ? "working":"idle";if kind=="task_complete" {completed=max(completed,Int64(date.timeIntervalSince1970))}}
            } else if let message=root["message"] as? [String:Any],let value=message["usage"] as? [String:Any],let model=message["model"] as? String {
                if root["isSidechain"] as? Bool != true, root["type"] == nil || root["type"] as? String == "assistant" { clientAt = max(clientAt,date) }
                guard let provider = provider(model) else { continue }
                if today { usage[provider,default:0] += ["input_tokens","output_tokens","cache_creation_input_tokens","cache_read_input_tokens"].reduce(Int64(0)){$0+max(0,(value[$1] as? NSNumber)?.int64Value ?? 0)} }
                latest[provider]=max(latest[provider] ?? .distantPast,date)
            }
        }
        return (usage,latest,state,eventAt,completed,clientAt)
    }
    static func provider(_ name:String)->String? {
        let name=String(name.lowercased().split(separator:"/").last ?? "")
        if name.hasPrefix("claude-"){return "claude"};if name.hasPrefix("qwen"){return "alibaba"};if name.hasPrefix("mimo"){return "xiaomi"}
        if name.hasPrefix("kimi")||name.hasPrefix("moonshot")||name=="k3"{return "kimi"};if name.hasPrefix("minimax")||name.hasPrefix("abab"){return "minimax"};if name.hasPrefix("deepseek"){return "deepseek"};return nil
    }
}
