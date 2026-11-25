import Foundation

// Turn directories contain no item bodies. Materialize only the requested and
// latest turns, keeping public assistant text between bounded item pages.
enum MacCodexPublicHistory {
    enum Failure: Error { case tooLarge }
    struct Window {
        let turns: [[String: Any]]
        let hasOlder: Bool, limited: Bool
    }
    static func read(task: String, selected: String, offset: Int,
                     call: MacTab5Codex.Call, clock: () -> TimeInterval) throws -> Window {
        let started = clock()
        func budget() throws {
            let elapsed = clock() - started
            guard elapsed.isFinite, elapsed >= 0, elapsed < 8 else { throw MacTab5CodexFailure.timeout }
        }
        func next(_ page: [String: Any], seen: inout Set<String>) throws -> String? {
            guard let value = page["nextCursor"], !(value is NSNull) else { return nil }
            guard let cursor = value as? String, !cursor.isEmpty, cursor.utf8.count <= 4096,
                  seen.insert(cursor).inserted else { throw MacTab5CodexFailure.protocolError }
            return cursor
        }
        var turns: [[String: Any]] = [], cursor: String?, seen = Set<String>(), needsOlder = false
        for _ in 0..<8 {
            try budget()
            var params: [String: Any] = ["threadId": task, "limit": 32,
                                         "sortDirection": "desc", "itemsView": "notLoaded"]
            if let cursor { params["cursor"] = cursor }
            let page = try call("thread/turns/list", params)
            try budget()
            guard let entries = page["data"] as? [[String: Any]], entries.count <= 32 else {
                throw MacTab5CodexFailure.protocolError
            }
            for entry in entries {
                guard let id = MacTab5Codex.id(entry["id"]), !turns.contains(where: { $0["id"] as? String == id }) else { continue }
                let reported = entry["status"] as? String ?? "unknown"
                let confirmed = ["completed", "failed"].contains(reported) ||
                    (reported == "interrupted" && MacTab5Protocol.integer(entry["completedAt"]) != nil)
                turns.append(["id": id, "status": confirmed ? reported : "stored",
                              "startedAt": entry["startedAt"] ?? 0, "items": [[String: Any]]()])
            }
            cursor = try next(page, seen: &seen)
            let index = turns.firstIndex { $0["id"] as? String == selected }
            needsOlder = !selected.isEmpty && (index == nil || (offset < 0 && index == turns.count - 1))
            if !needsOlder || cursor == nil { break }
        }
        var wanted = Set<Int>()
        if !turns.isEmpty { wanted.insert(0) }
        if !selected.isEmpty, let index = turns.firstIndex(where: { $0["id"] as? String == selected }),
                turns.indices.contains(index - offset) { wanted.insert(index - offset) }
        for index in wanted.sorted() {
            let turn = turns[index]["id"] as! String
            var itemCursor: String?, itemSeen = Set<String>(), items: [[String: Any]] = [], bytes = 0
            for batch in 0..<1024 {
                try budget()
                var params: [String: Any] = ["threadId": task, "turnId": turn,
                                             "limit": 16, "sortDirection": "asc"]
                if let itemCursor { params["cursor"] = itemCursor }
                let page = try call("thread/items/list", params)
                try budget()
                guard let entries = page["data"] as? [[String: Any]], entries.count <= 16 else {
                    throw MacTab5CodexFailure.protocolError
                }
                for entry in entries {
                    guard entry["turnId"] as? String == turn, let item = entry["item"] as? [String: Any] else {
                        throw MacTab5CodexFailure.protocolError
                    }
                    for value in MacTab5Codex.publicItems([item]) {
                        bytes += (value["text"] as? String ?? "").utf8.count
                        guard bytes <= 1024 * 1024 else { throw Failure.tooLarge }
                        items.append(value)
                    }
                }
                itemCursor = try next(page, seen: &itemSeen)
                if itemCursor == nil { break }
                guard batch < 1023 else { throw Failure.tooLarge }
            }
            turns[index]["items"] = items
        }
        return .init(turns: Array(turns.reversed()), hasOlder: cursor != nil,
                     limited: needsOlder && cursor != nil)
    }
}

extension MacCodexPublicHistory {
    static func stored(task: String, home: URL = MacCodexLocalMetadata.home) -> [[String: Any]]? {
        guard MacTab5Codex.id(task) == task else { return nil }
        let rows = MacCodexLocalMetadata.rows(home.appendingPathComponent("state_5.sqlite"),
            "SELECT rollout_path FROM threads WHERE id=?1 AND archived=0 LIMIT 2", binding: task)
        guard rows.count == 1, let path = rows.first?.first, path.hasPrefix("/"), !path.contains("\0") else { return nil }
        let file = URL(fileURLWithPath: path).resolvingSymlinksInPath()
        let sessions = home.appendingPathComponent("sessions").resolvingSymlinksInPath()
        guard file.path.hasPrefix(sessions.path + "/"), file.pathExtension == "jsonl" else { return nil }
        return stored(task: task, file: file)
    }

    static func stored(task: String, file: URL) -> [[String: Any]]? {
        let maxBytes = 64 * 1024 * 1024
        guard let values = try? file.resourceValues(forKeys: [.isRegularFileKey, .fileSizeKey]),
              values.isRegularFile == true, let size = values.fileSize, size <= maxBytes,
              let handle = try? FileHandle(forReadingFrom: file) else { return nil }
        defer { try? handle.close() }
        guard let data = try? handle.read(upToCount: size), data.count == size else { return nil }

        let deadline = ProcessInfo.processInfo.systemUptime + 1
        var turns: [[String: Any]] = [], seen = Set<String>()
        var current: String?, status = "stored", started: Int64 = 0
        var items: [[String: Any]] = [], bytes = 0, total = 0
        func finish() {
            if let current, !items.isEmpty, bytes <= 1024 * 1024 {
                turns.append(["id": current, "status": status, "startedAt": started, "items": items])
            }
        }
        func append(_ text: String, phase: String) {
            guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  items.last?["text"] as? String != text else { return }
            bytes += text.utf8.count + 2; total += text.utf8.count + 2
            if bytes <= 1024 * 1024 { items.append(["type": "agentMessage", "phase": phase, "text": text]) }
        }
        let formatter = ISO8601DateFormatter()
        formatter.formatOptions.insert(.withFractionalSeconds)
        var cursor = data.startIndex
        var verifiedSession = false
        while cursor < data.endIndex {
            guard ProcessInfo.processInfo.systemUptime < deadline, total <= 16 * 1024 * 1024,
                  bytes <= 1024 * 1024 else { return nil }
            let end = data[cursor...].firstIndex(of: 10) ?? data.endIndex
            let line = data[cursor..<end]
            cursor = end == data.endIndex ? end : data.index(after: end)
            guard line.count <= 4 * 1024 * 1024,
                  let record = (try? JSONSerialization.jsonObject(with: Data(line))) as? [String: Any],
                  let payload = record["payload"] as? [String: Any] else { if !verifiedSession { return nil }; continue }
            let type = record["type"] as? String, kind = payload["type"] as? String
            if !verifiedSession {
                guard type == "session_meta", MacTab5Codex.id(payload["id"]) == task else { return nil }
                verifiedSession = true
                continue
            }
            if type == "session_meta" { return nil }
            if type == "event_msg", kind == "task_started" {
                let next = MacTab5Codex.id(payload["turn_id"])
                if let next, next == current { continue }
                finish()
                current = next.flatMap { seen.insert($0).inserted ? $0 : nil }
                status = "stored"; items = []; bytes = 0
                started = (record["timestamp"] as? String).flatMap(formatter.date(from:)).map { Int64($0.timeIntervalSince1970) } ?? 0
                continue
            }
            guard let current else { continue }
            if let explicit = payload["turn_id"], MacTab5Codex.id(explicit) != current { continue }
            if type == "event_msg", MacTab5Codex.id(payload["turn_id"]) == current {
                if kind == "task_complete" {
                    status = "completed"
                    append(payload["last_agent_message"] as? String ?? "", phase: "final_answer")
                    continue
                }
                if kind == "turn_aborted" { status = "interrupted"; continue }
            }
            let item: [String: Any]
            if type == "response_item", kind == "message", payload["role"] as? String == "assistant" {
                item = payload
            } else if type == "event_msg", kind == "item_completed",
                      let value = payload["item"] as? [String: Any], value["type"] as? String == "AgentMessage" {
                item = value
            } else { continue }
            guard let phase = item["phase"] as? String, ["commentary", "final_answer"].contains(phase) else { continue }
            let content = (item["content"] as? [[String: Any]] ?? []).compactMap { part -> String? in
                guard let kind = part["type"] as? String, ["output_text", "text", "Text"].contains(kind) else { return nil }
                return part["text"] as? String
            }
            append(item["content"] as? String ?? content.joined(separator: "\n"), phase: phase)
        }
        finish()
        return total <= 16 * 1024 * 1024 && bytes <= 1024 * 1024 && !turns.isEmpty ? turns : nil
    }
}
