import Foundation

// Main-branch public assistant text grouped by user turn. Raw prompt/tool data
// is neither retained in nodes nor included in returned dictionaries.
enum MacClaudeHistory {
    struct Value { let turns: [[String:Any]]; let limited: Bool; let accepted: Set<String>; var inputDigests: [String:String] = [:] }
    private struct Node {
        let id: String, parent: String?, human: Bool, text: String, at: Int64, assistant: Bool, ended: Bool
        let inputDigest: String?, prompt: UUID?, stopNotice: Bool
        let photoPrompt: String?, photo: MacClaudeBackgroundPhotos.Attachment?
    }
    static func read(root: URL, session: UUID, key: Data, backgroundImages: Bool = false, verifiedNewPanel: () -> Bool = {false}) throws -> Value {
        var scanFailed=false
        guard let files = FileManager.default.enumerator(at:root,includingPropertiesForKeys:[.isRegularFileKey,.isSymbolicLinkKey],errorHandler:{_,_ in scanFailed=true;return false}) else { throw MacTab5Error.unavailable }
        var target: URL?
        for case let file as URL in files where file.pathExtension == "jsonl" && !file.pathComponents.contains("subagents") {
            if UUID(uuidString:file.deletingPathExtension().lastPathComponent) == session {
                guard target == nil else { throw MacTab5Error.unconfirmed }; target = file
            }
        }
        guard !scanFailed else { throw MacTab5Error.unavailable }
        if target == nil,verifiedNewPanel(){return Value(turns:[],limited:false,accepted:[])}
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
        // A contiguous attachment tail can already prove image input before
        // the model replies. It never replaces the selected conversation leaf.
        var attachmentTail: [String]? = []
        func consume(_ data: Data) throws {
            guard let row = try? JSONSerialization.jsonObject(with:data) as? [String:Any],
                  let raw = row["sessionId"] as? String, UUID(uuidString:raw) == session,
                  let type = row["type"] as? String,
                  row["isSidechain"] as? Bool != true else { return }
            let subtype = row["subtype"] as? String
            let link = type == "attachment" || (type == "system" && (["turn_duration", "away_summary", "informational", "api_error"].contains(subtype ?? "")))
            guard link || ["user","assistant"].contains(type) else { return }
            for flag in ["isSidechain","isMeta"] { if let v = row[flag], !MacTab5Protocol.isBoolean(v) { return } }
            // Context nodes preserve ancestry only; never expose their payload
            // or let a late attachment select an older conversation branch.
            if link {
                guard let uuid = row["uuid"] as? String, UUID(uuidString:uuid) != nil,
                      let parent = row["parentUuid"] as? String, UUID(uuidString:parent) != nil else { return }
            }
            ordinal += 1
            let id = (row["uuid"] as? String).flatMap(UUID.init(uuidString:))?.uuidString.lowercased() ?? "legacy-\(ordinal)"
            guard nodes[id] == nil else { return }
            let parent: String?
            if let value = row["parentUuid"], !(value is NSNull) {
                guard let value = value as? String, let uuid = UUID(uuidString:value) else { throw MacTab5Error.unavailable }
                parent = uuid.uuidString.lowercased()
            } else { parent = row["parentUuid"] is NSNull ? nil:leaf }
            let message = row["message"] as? [String:Any] ?? [:], meta = row["isMeta"] as? Bool == true
            var text = "", human = false, assistant = false, ended = false, stopNotice = false
            if !meta, type == "assistant" {
                guard message["role"] as? String == "assistant", let content = message["content"] as? [[String:Any]] else { throw MacTab5Error.unavailable }
                text = content.compactMap { $0["type"] as? String == "text" ? $0["text"] as? String:nil }.joined(separator:"\n").trimmingCharacters(in:.whitespacesAndNewlines)
                assistant = true; ended = message["stop_reason"] as? String == "end_turn" && !text.isEmpty
            } else if !meta, type == "user", (row["origin"] as? [String:Any])?["kind"] as? String != "peer", row["toolUseResult"] == nil || row["toolUseResult"] is NSNull {
                let content = message["content"] as? [[String:Any]] ?? []
                human = message["content"] is String || (!content.isEmpty && content.allSatisfy { ["text","image"].contains($0["type"] as? String ?? "") })
                stopNotice = content.count == 1 && content[0]["type"] as? String == "text" &&
                    ["[Request interrupted by user]", "[Request interrupted by user for tool use]"].contains(content[0]["text"] as? String ?? "")
            }
            let stamp = row["timestamp"] as? String ?? "", date = dates.date(from:stamp) ?? plain.date(from:stamp)
            let prompt=(row["promptId"] as? String).flatMap(UUID.init(uuidString:))
            let photoPrompt = backgroundImages && human ? MacClaudeBackgroundPhotos.prompt(message["content"]) : nil
            let photo = backgroundImages && type == "attachment" ? MacClaudeBackgroundPhotos.attachment(row) : nil
            nodes[id] = Node(id:id,parent:parent,human:human,text:text,at:date.map { Int64($0.timeIntervalSince1970) } ?? 0,
                             assistant:assistant,ended:ended,inputDigest:human ? MacClaudeNativeContent.message(message["content"]):nil,
                             prompt:prompt,stopNotice:stopNotice,photoPrompt:photoPrompt,photo:photo)
            if backgroundImages {
                if !link { attachmentTail = [] }
                else if let previous = attachmentTail?.last ?? leaf, previous == parent {
                    attachmentTail?.append(id)
                } else { attachmentTail = nil }
            }
            if !link { leaf = id }
        }
        while let bytes = try file.read(upToCount:65536), !bytes.isEmpty {
            for byte in bytes {
                if byte == 10 {
                    if !skip && !oversized { try consume(line) }
                    line.removeAll(keepingCapacity:true); skip = false; oversized = false
                } else if !skip && !oversized {
                    if line.count < 10_000_000 { line.append(byte) } else { line.removeAll(keepingCapacity:true); oversized = true; limited = true }
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
        var turns: [[String:Any]] = [], items: [[String:Any]] = [], start: Node?, complete = false, interrupted = false, endedAt: Int64 = 0
        var accepted=Set<String>(),digests=[String:String]()
        var photos: [MacClaudeBackgroundPhotos.Attachment] = [], sawAssistant = false
        func collectPhoto(_ node: Node) {
            guard !sawAssistant, start?.photoPrompt != nil, photos.count <= MacTab5Photos.maxCount,
                  let photo = node.photo else { return }
            photos.append(photo)
        }
        func finish() {
            guard let first = start else { return }
            if let prompt = first.photoPrompt, let digest = MacClaudeBackgroundPhotos.digest(prompt: prompt, attachments: photos) {
                digests[first.id] = digest
            }
            photos.removeAll(keepingCapacity: true); sawAssistant = false
            turns.append(["id":MacClaudeCatalog.alias("claude:turn:"+session.uuidString.lowercased()+":"+first.id,key:key),
                          "startedAt":first.at,"status":interrupted ? "interrupted":complete ? "completed":"unknown","endedAt":endedAt,"items":items]); items = []
        }
        var retainedBytes = 0
        for node in branch.reversed() {
            if node.stopNotice,let prompt=node.prompt,prompt.uuidString != "00000000-0000-0000-0000-000000000000",start?.human==true,prompt==start?.prompt {
                interrupted=true;endedAt=node.at;continue
            }
            if node.human { finish(); start = node; complete = false; interrupted=false; endedAt = 0; accepted.insert(node.id);digests[node.id]=node.inputDigest }
            if start == nil { start = node }
            collectPhoto(node)
            if node.assistant { sawAssistant = true; complete = node.ended; endedAt = complete ? node.at:0 }
            if !node.text.isEmpty {
                guard node.text.utf8.count <= 1024*1024 else { throw MacTab5Error.invalidPacket }
                items.append(["id":MacClaudeCatalog.alias("claude:item:"+node.id,key:key),"type":"agentMessage","phase":"final_answer","text":node.text]); retainedBytes += node.text.utf8.count
            }
            if turns.count > 128 || retainedBytes > 4*1024*1024 {
                if !turns.isEmpty { let removed = turns.removeFirst(); retainedBytes -= (removed["items"] as? [[String:Any]] ?? []).reduce(0) { $0+(($1["text"] as? String)?.utf8.count ?? 0) }; limited = true }
            }
        }
        for id in attachmentTail ?? [] {
            if let node = nodes[id] { collectPhoto(node) }
        }
        finish(); return Value(turns:turns,limited:limited,accepted:accepted,inputDigests:digests)
    }
}
