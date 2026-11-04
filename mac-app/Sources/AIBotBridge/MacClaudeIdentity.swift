import Foundation

// Local identities never enter a device frame. Only UserPromptSubmit can open
// a turn; terminal events cannot create one or revive an older prompt.
final class MacClaudeIdentity {
    struct Selection {
        let sessionID: UUID
        let promptID: UUID
        let activity: Date
        let state: String
        let outcome: String
        let needsInput: Bool
        let completionAt: Int64
        let completionSequence: Int64
        var ownedTurn: String? = nil
        var tool: ToolState { ToolState(state:state, ageSeconds:0, needsInput:needsInput,
            completionActive:completionAt > 0, completionSequence:completionSequence, completionAt:completionAt) }
    }
    private struct Turn {
        var prompt: UUID
        var started: Date
        var observed: Date
        var activity: Date
        var state = "working"
        var outcome = "running"
        var waiting = false
        var completed: Int64 = 0
        var sequence: Int64 = 0
        var terminal: Bool { ["completed", "failed", "interrupted", "ended"].contains(outcome) }
    }
    private let lock = NSLock()
    private var turns: [UUID:Turn] = [:]
    private var retired = Set<String>()
    private var retiredOrder: [String] = []
    private var sequence: Int64 = 0

    @discardableResult func record(_ event: [String:Any], now: Date = Date()) -> Bool {
        guard event["agent"] as? String == "claude", let kind = event["event"] as? String,
              let sid = event["sessionId"] as? String, let session = UUID(uuidString:sid),
              let pid = event["promptId"] as? String, let prompt = UUID(uuidString:pid),
              event["agentId"] == nil,
              let stamp = event["emittedAt"] as? Double, stamp.isFinite else { return false }
        let at = Date(timeIntervalSince1970:stamp / 1000)
        guard abs(now.timeIntervalSince(at)) <= 120 else { return false }
        lock.lock(); defer { lock.unlock() }
        if kind == "UserPromptSubmit" {
            if retired.contains(session.uuidString + prompt.uuidString) { return false }
            if let old = turns[session] {
                if old.prompt == prompt { return true }
                guard at > old.started else { return false }
                let key = session.uuidString + old.prompt.uuidString
                retired.insert(key); retiredOrder.append(key)
                if retiredOrder.count > 1024 { retired.remove(retiredOrder.removeFirst()) }
            }
            if turns.count >= 64, turns[session] == nil {
                // Preserve tombstones rather than allowing old prompts to reopen.
                return false
            }
            turns[session] = Turn(prompt:prompt,started:at,observed:at,activity:at)
            return true
        }
        guard var turn = turns[session], turn.prompt == prompt, at >= turn.started else { return false }
        // A late failure may revoke a provisional or completed result. Ordinary
        // progress and duplicate final results cannot overwrite a terminal state.
        if kind == "StopFailure" || kind == "TurnInterrupted" {
            turn.outcome = kind == "StopFailure" ? "failed" : "interrupted"
            turn.state = kind == "StopFailure" ? "error" : "idle"; turn.waiting = false; turn.completed = 0
        } else {
            // A controlled --print process emits SessionEnd before its final
            // result is consumed. That result may settle this same closed turn.
            if turn.terminal && !(turn.outcome == "ended" && kind == "TurnResult") { return true }
            guard at >= turn.observed else { return false }
            switch kind {
            case "PermissionRequest", "Elicitation": turn.waiting = true; turn.state = "working"
            case "PreToolUse", "PostToolUse", "PostToolUseFailure", "PostToolBatch":
                turn.state = "working"; turn.waiting = false; turn.outcome = "running"; turn.activity = at
            case "Stop": turn.state = "unknown"; turn.outcome = "unconfirmed"
            case "Notification":
                guard event["notificationType"] as? String == "idle_prompt" else { return false }
                turn.state = "idle"; turn.outcome = "unconfirmed" // never success evidence
            case "SessionEnd": turn.state = "idle"; turn.outcome = "ended"; turn.waiting = false
            case "TurnResult":
                guard let reason = event["terminalReason"] as? String else { return false }
                if reason == "completed", event["isError"] as? Bool == false,
                   let count = event["numTurns"] as? Int, count > 0 {
                    sequence += 1; turn.completed = Int64(at.timeIntervalSince1970)
                    turn.sequence = sequence; turn.state = "idle"; turn.outcome = "completed"; turn.waiting = false
                } else {
                    turn.outcome = reason == "interrupted" ? "interrupted" : event["isError"] as? Bool == true ? "failed" : "unconfirmed"
                    turn.state = turn.outcome == "failed" ? "error" : turn.outcome == "interrupted" ? "idle" : "unknown"; turn.waiting = false
                }
            default: return false
            }
        }
        if !["Notification", "SessionEnd"].contains(kind) { turn.activity = max(turn.activity,at) }
        turn.observed = max(turn.observed,at); turns[session] = turn
        return true
    }
    func selection(now: Date = Date()) -> Selection? {
        lock.lock(); defer { lock.unlock() }
        func priority(_ t: Turn) -> Int { now.timeIntervalSince(t.observed) < (t.waiting ? 300 : 600) && !t.terminal ? (t.waiting ? 2 : t.state == "working" ? 1 : 0) : 0 }
        let selected = turns.sorted {
            if priority($0.value) != priority($1.value) { return priority($0.value) > priority($1.value) }
            if $0.value.activity != $1.value.activity { return $0.value.activity > $1.value.activity }
            return $0.key.uuidString < $1.key.uuidString
        }.first
        guard let (id,t) = selected else { return nil }
        let expired = !t.terminal && now.timeIntervalSince(t.observed) >= (t.waiting ? 300 : 600)
        return Selection(sessionID:id,promptID:t.prompt,activity:t.activity,state:expired ? "unknown" : t.state,
            outcome:expired ? "unconfirmed" : t.outcome,needsInput:!expired && t.waiting,
            completionAt:t.completed,completionSequence:t.sequence)
    }
    // Reconcile exact local observations before choosing a session. An older
    // terminal result must never hide a newer external prompt in that session.
    static func resolve(bound: Selection?, owned: [Selection]) -> Selection? {
        var sessions: [UUID:Selection] = [:]
        if let bound { sessions[bound.sessionID] = bound }
        for value in owned where sessions[value.sessionID].map({ value.activity >= $0.activity }) ?? true { sessions[value.sessionID] = value }
        func priority(_ value: Selection) -> Int { value.needsInput ? 2 : value.state == "working" ? 1 : 0 }
        return sessions.values.sorted {
            if priority($0) != priority($1) { return priority($0) > priority($1) }
            if $0.activity != $1.activity { return $0.activity > $1.activity }
            return $0.sessionID.uuidString < $1.sessionID.uuidString
        }.first
    }
    func acknowledge() { lock.lock(); defer { lock.unlock() }; for id in turns.keys { turns[id]?.completed = 0 } }
    func invalidate() { lock.lock(); defer { lock.unlock() }; turns.removeAll() }
}
