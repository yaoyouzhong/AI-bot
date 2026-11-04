import Foundation

// Main-branch public assistant text grouped by user turn. Raw prompt/tool data
// is neither retained in nodes nor included in returned dictionaries.
enum MacClaudeHistory {
    struct Value { let turns: [[String:Any]]; let limited: Bool; let accepted: Set<String> }
    private struct Node { let id: String, parent: String?, human: Bool, text: String, at: Int64 }
    static func read(root: URL, session: UUID, key: Data) throws -> Value {
        guard let files = FileManager.default.enumerator(at:root,includingPropertiesForKeys:[.isRegularFileKey,.isSymbolicLinkKey]) else { throw MacTab5Error.unavailable }
        var target: URL?
        for case let file as URL in files where file.pathExtension == "jsonl" && !file.pathComponents.contains("subagents") {
            if UUID(uuidString:file.deletingPathExtension().lastPathComponent) == session {
                guard target == nil else { throw MacTab5Error.unconfirmed }; target = file
            }
        }
        guard let target else { throw MacTab5Error.unavailable }
        let info = try target.resourceValues(forKeys:[.isRegularFileKey,.isSymbolicLinkKey,.fileSizeKey])
        guard info.isRegularFile == true, info.isSymbolicLink != true else { throw MacTab5Error.unavailable }
        let file = try FileHandle(forReadingFrom:target); defer { try? file.close() }
        let tail = 16*1024*1024, size = info.fileSize ?? 0
        var limited = size > tail, skip = limited
        if limited { try file.seek(toOffset:UInt64(size-tail)) }
        let dates = ISO8601DateFormatter(); dates.formatOptions = [.withInternetDateTime,.withFractionalSeconds]
        let plain = ISO8601DateFormatter()
        var nodes: [String:Node] = [:], leaf: String?, line = Data(), oversized = false, ordinal = 0
        func consume(_ data: Data) throws {
            guard let row = try? JSONSerialization.jsonObject(with:data) as? [String:Any],
                  let raw = row["sessionId"] as? String, UUID(uuidString:raw) == session,
                  let type = row["type"] as? String, ["user","assistant"].contains(type),
                  row["isSidechain"] as? Bool != true else { return }
            for flag in ["isSidechain","isMeta"] { if let v = row[flag], !MacTab5Protocol.isBoolean(v) { return } }
            ordinal += 1
            let id = (row["uuid"] as? String).flatMap(UUID.init(uuidString:))?.uuidString.lowercased() ?? "legacy-\(ordinal)"
            guard nodes[id] == nil else { return }
            let parent: String?
            if let value = row["parentUuid"], !(value is NSNull) {
                guard let value = value as? String, let uuid = UUID(uuidString:value) else { throw MacTab5Error.unavailable }
                parent = uuid.uuidString.lowercased()
            } else { parent = row["parentUuid"] is NSNull ? nil:leaf }
            let message = row["message"] as? [String:Any] ?? [:], meta = row["isMeta"] as? Bool == true
            var text = "", human = false
            if !meta, type == "assistant" {
                guard message["role"] as? String == "assistant", let content = message["content"] as? [[String:Any]] else { throw MacTab5Error.unavailable }
                text = content.compactMap { $0["type"] as? String == "text" ? $0["text"] as? String:nil }.joined(separator:"\n").trimmingCharacters(in:.whitespacesAndNewlines)
            } else if !meta, type == "user", row["toolUseResult"] == nil || row["toolUseResult"] is NSNull {
                let content = message["content"] as? [[String:Any]] ?? []
                human = message["content"] is String || (!content.isEmpty && content.allSatisfy { ["text","image"].contains($0["type"] as? String ?? "") })
            }
            let stamp = row["timestamp"] as? String ?? "", date = dates.date(from:stamp) ?? plain.date(from:stamp)
            nodes[id] = Node(id:id,parent:parent,human:human,text:text,at:date.map { Int64($0.timeIntervalSince1970) } ?? 0); leaf = id
        }
        while let bytes = try file.read(upToCount:65536), !bytes.isEmpty {
            for byte in bytes {
                if byte == 10 {
                    if !skip && !oversized { try consume(line) }
                    line.removeAll(keepingCapacity:true); skip = false; oversized = false
                } else if !skip && !oversized {
                    if line.count < 2_000_000 { line.append(byte) } else { line.removeAll(keepingCapacity:true); oversized = true; limited = true }
                }
            }
        }
        var branch: [Node] = [], visited = Set<String>(), next = leaf
        while let id = next {
            guard visited.insert(id).inserted else { throw MacTab5Error.unconfirmed }
            guard let node = nodes[id] else { limited = true; break }
            branch.append(node); next = node.parent
        }
        guard !branch.isEmpty else { throw MacTab5Error.unavailable }
        var turns: [[String:Any]] = [], items: [[String:Any]] = [], start: Node?
        func finish() {
            guard let first = start else { return }
            turns.append(["id":MacClaudeCatalog.alias("claude:turn:"+session.uuidString.lowercased()+":"+first.id,key:key),
                          "startedAt":first.at,"status":"unknown","items":items]); items = []
        }
        var retainedBytes = 0
        for node in branch.reversed() {
            if node.human { finish(); start = node }
            if start == nil { start = node }
            if !node.text.isEmpty {
                guard node.text.utf8.count <= 1024*1024 else { throw MacTab5Error.invalidPacket }
                items.append(["id":MacClaudeCatalog.alias("claude:item:"+node.id,key:key),"type":"agentMessage","phase":"final_answer","text":node.text]); retainedBytes += node.text.utf8.count
            }
            if turns.count > 128 || retainedBytes > 4*1024*1024 {
                if !turns.isEmpty { let removed = turns.removeFirst(); retainedBytes -= (removed["items"] as? [[String:Any]] ?? []).reduce(0) { $0+(($1["text"] as? String)?.utf8.count ?? 0) }; limited = true }
            }
        }
        finish(); return Value(turns:turns,limited:limited,accepted:Set(branch.filter { $0.human }.map(\.id)))
    }
}
