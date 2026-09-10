import Foundation

// Metadata only: never persist conversation text or credentials.
struct MacActivityTotals {
    var codex = ToolState(state: "offline", ageSeconds: nil)
    var claude = ToolState(state: "offline", ageSeconds: nil)
    var domestic: MacDomesticActivity? = nil
}
struct MacDomesticActivity: Codable {
    var activeProvider: String
    var state: String
    var needsInput: Bool
    var tokensToday: Int64
}
final class MacActivitySignals {
    private let lock = NSLock()
    private var states: [String: (String, Date)] = [:]
    private var attention: [String: Date] = [:]
    private var completedAt: Int64 = 0
    private var acknowledged: Int64 = 0
    private var baselineLoaded = false
    @discardableResult func record(agent: String, event: String, message: String = "", now: Date = Date()) -> Bool {
        guard ["claude", "codex"].contains(agent) else { return false }
        lock.lock(); defer { lock.unlock() }
        if event == "PermissionRequest" || event == "Elicitation" || event == "Notification" && ["permission", "approve", "approval"].contains(where: { message.lowercased().contains($0) }) {
            attention[agent] = now; return true
        }
        if agent == "codex", event == "TaskComplete" {
            completedAt = Int64(now.timeIntervalSince1970); states[agent] = ("idle", now); attention[agent] = nil; return true
        }
        let working = ["UserPromptSubmit", "PreToolUse", "PostToolUse", "SubagentStart", "SubagentStop", "PreCompact", "PostCompact", "WorktreeCreate"].contains(event)
        guard working || ["Stop", "SessionStart", "SessionEnd"].contains(event) else { return false }
        states[agent] = (working ? "working" : "idle", now); attention[agent] = nil
        if agent == "codex", working { acknowledged = max(acknowledged, completedAt) }
        return true
    }
    func acknowledge() { lock.lock(); acknowledged = max(acknowledged, completedAt); lock.unlock() }
    func apply(_ agent: String, raw: ToolState, now: Date) -> ToolState {
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
        if let state = states[agent], now.timeIntervalSince(state.1) < (state.0 == "working" ? 600 : 60) { result = withState(result, state.0) }
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
    private struct FileValue { var modified: Date; var size: Int; var day: Date; var usage: [String:Int64]; var latest: [String:Date]; var state: String; var eventAt: Date; var completed: Int64 }
    private var cache: [URL: FileValue] = [:]
    private var nextScan = Date.distantPast
    private var snapshot = MacActivityTotals()
    func capture(now: Date) -> MacActivityTotals {
        guard now >= nextScan else { return snapshot }; nextScan = now.addingTimeInterval(5)
        let manager = FileManager.default, day = Calendar.current.startOfDay(for: now)
        var usage: [String:Int64] = [:], latest: [String:Date] = [:], newest = Date.distantPast, codexState = "offline", completion: Int64 = 0
        var seen = Set<URL>()
        for (path, codex) in [(".claude/projects", false), (".codex/sessions", true)] {
            let root = manager.homeDirectoryForCurrentUser.appendingPathComponent(path)
            guard let files = manager.enumerator(at: root, includingPropertiesForKeys: [.contentModificationDateKey,.fileSizeKey,.isRegularFileKey,.isSymbolicLinkKey], options: [.skipsHiddenFiles]) else { continue }
            for case let file as URL in files where file.pathExtension == "jsonl" {
                guard let attributes = try? file.resourceValues(forKeys: [.contentModificationDateKey,.fileSizeKey,.isRegularFileKey,.isSymbolicLinkKey]), attributes.isRegularFile == true, attributes.isSymbolicLink != true,
                      let modified = attributes.contentModificationDate, modified >= day else { continue }
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
                    cache[file] = FileValue(modified: modified, size: attributes.fileSize ?? 0, day: day, usage: value.usage, latest: value.latest, state: value.state, eventAt: value.eventAt, completed: value.completed)
                }
                guard let value = cache[file] else { continue }
                for (provider, count) in value.usage { usage[provider, default:0] += count }
                for (provider, date) in value.latest { latest[provider] = max(latest[provider] ?? .distantPast, date) }
                if codex, value.eventAt > newest { newest = value.eventAt; codexState = value.state }
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
        if newest != .distantPast { snapshot.codex = ToolState(state: now.timeIntervalSince(newest)>900 ? "offline":codexState,ageSeconds:max(0,Int64(now.timeIntervalSince(newest))),completionAt:completion,tokensToday:usage["codex"] ?? 0) }
        if let domestic = latest.filter({ !["claude","codex"].contains($0.key) }).max(by: { $0.value < $1.value }), now.timeIntervalSince(domestic.value) <= 900 {
            snapshot.domestic = MacDomesticActivity(activeProvider:domestic.key,state:now.timeIntervalSince(domestic.value)<90 ? "working":"idle",needsInput:false,tokensToday:usage[domestic.key] ?? 0)
        }
        return snapshot
    }
    static func parse(_ lines: [String], codex: Bool, now: Date) -> (usage:[String:Int64],latest:[String:Date],state:String,eventAt:Date,completed:Int64) {
        var usage:[String:Int64]=[:], latest:[String:Date]=[:], state="offline", eventAt=Date.distantPast, completed:Int64=0, subagent=false
        let formatter=ISO8601DateFormatter(); formatter.formatOptions=[.withInternetDateTime,.withFractionalSeconds]
        let plain=ISO8601DateFormatter()
        for line in lines {
            guard let data=line.data(using:.utf8),let root=(try? JSONSerialization.jsonObject(with:data)) as? [String:Any] else {continue}
            let payload=root["payload"] as? [String:Any] ?? [:]
            if root["type"] as? String == "session_meta" { let source=payload["source"]; subagent = (source as? [String:Any])?["subagent"] != nil || (source as? String)?.contains("subagent") == true }
            guard let stamp=root["timestamp"] as? String,let date=formatter.date(from:stamp) ?? plain.date(from:stamp),Calendar.current.isDate(date,inSameDayAs:now),date<=now.addingTimeInterval(60) else {continue}
            if codex {
                if payload["type"] as? String == "token_count",let info=payload["info"] as? [String:Any],let total=info["total_token_usage"] as? [String:Any] { usage["codex"]=max(usage["codex"] ?? 0,(total["total_tokens"] as? NSNumber)?.int64Value ?? 0); latest["codex"]=max(latest["codex"] ?? .distantPast,date) }
                if !subagent,let kind=payload["type"] as? String,["task_started","task_complete","task_aborted"].contains(kind),date>=eventAt {eventAt=date;state=kind=="task_started" ? "working":"idle";if kind=="task_complete" {completed=max(completed,Int64(date.timeIntervalSince1970))}}
            } else if let message=root["message"] as? [String:Any],let value=message["usage"] as? [String:Any],let model=message["model"] as? String,let provider=provider(model) {
                usage[provider,default:0] += ["input_tokens","output_tokens","cache_creation_input_tokens","cache_read_input_tokens"].reduce(Int64(0)){$0+max(0,(value[$1] as? NSNumber)?.int64Value ?? 0)}
                latest[provider]=max(latest[provider] ?? .distantPast,date)
            }
        }
        return (usage,latest,state,eventAt,completed)
    }
    static func provider(_ name:String)->String? {
        let name=String(name.lowercased().split(separator:"/").last ?? "")
        if name.hasPrefix("claude-"){return "claude"};if name.hasPrefix("qwen"){return "alibaba"};if name.hasPrefix("mimo"){return "xiaomi"}
        if name.hasPrefix("kimi")||name.hasPrefix("moonshot")||name=="k3"{return "kimi"};if name.hasPrefix("minimax")||name.hasPrefix("abab"){return "minimax"};if name.hasPrefix("deepseek"){return "deepseek"};return nil
    }
}
