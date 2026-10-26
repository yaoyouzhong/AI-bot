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
                turns.append(["id": id, "status": entry["status"] ?? "unknown",
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
