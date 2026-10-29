import Foundation

// Ephemeral comparison only. Never add input bodies, item IDs or this snapshot
// to exported history, logs, settings or the persistent desktop receipt journal.
struct MacCodexSubmissionReceipt {
    let task: String, owner: String, textDigest: String, turn: String
    let started: Int64
    let known: Set<String>
    let accepted: [Set<String>]
    var queue: MacCodexQueueReceipt? = nil
    var serverQueue: MacCodexQueueReceipt? = nil
    var knownEverywhere: Set<String> { known.union(queue?.known ?? []).union(serverQueue?.known ?? []) }

    func confirms(after baseline: Self) -> Bool {
        guard task == baseline.task, owner == baseline.owner, textDigest == baseline.textDigest,
              baseline.queue != nil, baseline.serverQueue != nil,
              started >= baseline.started, started != baseline.started || turn == baseline.turn else { return false }
        return accepted.contains { !$0.isEmpty && $0.isDisjoint(with: baseline.knownEverywhere) }
    }

    func confirmsQueue(after baseline: Self) -> Bool {
        guard task == baseline.task, owner == baseline.owner, textDigest == baseline.textDigest,
              baseline.queue != nil, baseline.serverQueue != nil,
              started >= baseline.started, started != baseline.started || turn == baseline.turn else { return false }
        if let queue, baseline.queue != nil, queue.matching.contains(where: { !$0.isEmpty && $0.isDisjoint(with: baseline.knownEverywhere) }) { return true }
        if let serverQueue, baseline.serverQueue != nil, serverQueue.matching.contains(where: { !$0.isEmpty && $0.isDisjoint(with: baseline.knownEverywhere) }) { return true }
        return false
    }

    static func parse(_ state: [String: Any], task: String, owner: String, expected: String) throws -> Self {
        guard MacTab5Codex.id(state["id"]) == task, MacTab5Codex.id(owner) != nil,
              !expected.isEmpty, expected.utf8.count <= 2000 else { throw MacCodexDesktopError.incompatible }
        var turns: [String: [String: Any]] = [:]
        let canonical = ((state["turnHistory"] as? [String: Any])?["history"] as? [String: Any])?["entitiesByKey"] as? [String: [String: Any]] ?? [:]
        for item in (state["turns"] as? [[String: Any]] ?? []) + Array(canonical.values) {
            guard let id = MacTab5Codex.id(item["turnId"]) else { throw MacCodexDesktopError.incompatible }
            turns[id] = item
        }
        guard !turns.isEmpty, turns.count <= 10000 else { throw MacCodexDesktopError.incompatible }
        var latest = "", started: Int64 = 0, tied = false
        for (id, item) in turns {
            guard let time = MacTab5Protocol.integer(item["turnStartedAtMs"]), time > 0 else { throw MacCodexDesktopError.incompatible }
            if time > started { latest = id; started = time; tied = false }
            else if time == started { tied = true }
        }
        guard !tied else { throw MacCodexDesktopError.incompatible }
        var known: Set<String> = [], accepted: [Set<String>] = [], visited = 0
        let expectedText = paragraphText(expected)
        for (turn, item) in turns {
            guard let items = item["items"] as? [[String: Any]] else { throw MacCodexDesktopError.incompatible }
            for message in items {
                visited += 1
                guard visited <= 100000 else { throw MacCodexDesktopError.incompatible }
                let kind = message["type"] as? String
                guard kind == "userMessage" || kind == "steeringUserMessage" else { continue }
                guard let id = message["id"] as? String, !id.isEmpty, id.utf8.count <= 256 else { throw MacCodexDesktopError.incompatible }
                var aliases: Set<String> = [digest(id)]
                for key in ["clientId", "clientUserMessageId", "serverUserMessageId", "serverClientUserMessageId"] {
                    if let raw = message[key], !(raw is NSNull) {
                        guard let value = raw as? String, !value.isEmpty, value.utf8.count <= 256 else { throw MacCodexDesktopError.incompatible }
                        aliases.insert(digest(value))
                    }
                }
                known.formUnion(aliases)
                // Older turns loaded later and optimistic/pending steer items
                // cannot establish acceptance of the current explicit submit.
                guard turn == latest, kind == "userMessage" || message["status"] as? String == "accepted",
                      let inputs = message[kind == "userMessage" ? "content" : "input"] as? [[String: Any]],
                      !inputs.isEmpty, inputs.count <= 16 else { continue }
                var body = "", valid = true
                for input in inputs {
                    guard input["type"] as? String == "text", input["truncated"] as? Bool != true,
                          let text = input["text"] as? String, text.utf8.count <= 2000 else { valid = false; break }
                    body += text
                    if body.utf8.count > 2000 { valid = false; break }
                }
                if valid, paragraphText(body) == expectedText { accepted.append(aliases) }
            }
        }
        return Self(task: task, owner: owner, textDigest: digest(expectedText), turn: latest,
                    started: started, known: known, accepted: accepted)
    }
    private static func digest(_ value: String) -> String { MacTab5Protocol.sha256(Data(value.utf8)) }
    static func paragraphText(_ value: String) -> String {
        var text = value
        while text.last == "\n" || text.last == "\r" { text.removeLast() }
        return text
    }
}
