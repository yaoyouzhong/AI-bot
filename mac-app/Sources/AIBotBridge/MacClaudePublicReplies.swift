import Foundation

// Explicitly requested public assistant text only. Never decode nested tool
// blocks or infer a reply from user/system/summary/reasoning records.
enum MacClaudePublicReplies {
    struct Value { let text: String; let hasEarlier: Bool }
    static func read(_ file: URL, sessionID: UUID) throws -> Value {
        let info = try file.resourceValues(forKeys:[.isRegularFileKey,.isSymbolicLinkKey,.fileSizeKey])
        guard info.isRegularFile == true, info.isSymbolicLink != true else { throw MacTab5Error.unavailable }
        let input = try FileHandle(forReadingFrom:file); defer { try? input.close() }
        let size = info.fileSize ?? 0, tail = 16 * 1024 * 1024
        var earlier = size > tail, skip = earlier
        if earlier { try input.seek(toOffset:UInt64(size-tail)) }
        var pending = Data(), oversized = false, parts: [String] = [], bytes = 0, known = false
        var seen = Set<String>(), unsupportedReply = false
        func consume(_ line: Data) {
            guard let e = (try? JSONSerialization.jsonObject(with:line)) as? [String:Any],
                  let sid = e["sessionId"] as? String, UUID(uuidString:sid) == sessionID,
                  e["isSidechain"] as? Bool != true, e["isMeta"] as? Bool != true,
                  let type = e["type"] as? String, ["user","assistant"].contains(type) else { return }
            for flag in ["isSidechain","isMeta"] {
                if let value = e[flag], !MacTab5Protocol.isBoolean(value) { return }
            }
            if type == "user" { known = true; return }
            guard let message = e["message"] as? [String:Any],
                  message["role"] as? String == "assistant", let content = message["content"] as? [[String:Any]] else { unsupportedReply = true; return }
            known = true
            if let id = e["uuid"] as? String {
                if seen.contains(id) { return }
                if seen.count < 4096 { seen.insert(id) }
            }
            let text = content.compactMap { block -> String? in
                guard block["type"] as? String == "text", let value = block["text"] as? String else { return nil }
                return value
            }.joined(separator:"\n").trimmingCharacters(in:.whitespacesAndNewlines)
            guard !text.isEmpty else { return }
            // Bound device reading and private memory, retaining recent public
            // output. Earlier omitted content is explicitly indicated to UI.
            var value = text
            if value.utf8.count > 65_536 {
                let bytes = Array(value.utf8.suffix(65_536))
                let start = bytes.firstIndex(where:{ $0 & 0xC0 != 0x80 }) ?? bytes.endIndex
                value = String(decoding:bytes[start...],as:UTF8.self); earlier = true
            }
            parts.append(value); bytes += value.utf8.count
            while parts.count > 64 || bytes > 65_536 {
                bytes -= parts.removeFirst().utf8.count; earlier = true
            }
        }
        while let chunk = try input.read(upToCount:65_536), !chunk.isEmpty {
            for byte in chunk {
                if byte == 10 {
                    if !skip && !oversized { consume(pending) }
                    pending.removeAll(keepingCapacity:true); oversized = false; skip = false
                } else if !skip && !oversized {
                    if pending.count < 2_000_000 { pending.append(byte) }
                    else { pending.removeAll(keepingCapacity:true); oversized = true; earlier = true }
                }
            }
        }
        guard known, !unsupportedReply || !parts.isEmpty else { throw MacTab5Error.unavailable }
        return Value(text:parts.joined(separator:"\n\n"),hasEarlier:earlier)
    }
}
