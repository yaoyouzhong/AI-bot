import Foundation

enum MacTab5ArtGuide {
    static func pages(_ text: String) throws -> [String] {
        guard text.utf8.count <= 32760 else { throw MacTab5Error.invalidPacket }
        var result: [String] = [], page = "", bytes = 0, wireBytes = 0
        for scalar in text.unicodeScalars {
            let value = String(scalar)
            let escaped = try JSONEncoder().encode(value).count - 2
            if bytes + value.utf8.count > 4095 || wireBytes + escaped > 6144 { result.append(page); page = ""; bytes = 0; wireBytes = 0 }
            page += value; bytes += value.utf8.count; wireBytes += escaped
        }
        if !page.isEmpty || result.isEmpty { result.append(page) }
        guard result.count <= 32 else { throw MacTab5Error.invalidPacket }; return result
    }
    static func read(_ bytes: Data, works: [MacTab5Artwork]) throws -> [String: [String: Any]] {
        guard bytes.count <= 8 * 1024 * 1024 else { throw MacTab5Error.invalidPacket }
        let root = try MacTab5Protocol.object(bytes)
        guard MacTab5Protocol.integer(root["schemaVersion"]) == 1, root["language"] as? String == "zh-CN",
              let rows = root["works"] as? [[String: Any]], rows.count == works.count else { throw MacTab5Error.invalidPacket }
        guard Set(works.map(\.id)).count == works.count else { throw MacTab5Error.invalidPacket }
        let expected = Dictionary(uniqueKeysWithValues: works.map { ($0.id, $0) })
        var seen = Set<String>(), guides: [String: [String: Any]] = [:]
        let bounds = [("title", 256), ("author", 128), ("period", 96), ("museum", 80), ("brief", 256), ("overview", 640), ("looking", 384)]
        for row in rows {
            guard let id = row["id"] as? String, seen.insert(id).inserted, let work = expected[id],
                  row["category"] as? String == work.category, row["workId"] as? String == (work.workId ?? work.source),
                  let sources = row["sources"] as? [Any], !sources.isEmpty,
                  let original = row["transcription"] as? [String: Any], let status = original["status"] as? String,
                  ["source_verified", "reference_verified", "not_available", "withheld_pending_review"].contains(status) else { throw MacTab5Error.invalidPacket }
            let workID = work.workId ?? work.source
            guard !workID.isEmpty, workID.utf8.count < 80, guides[workID] == nil else { throw MacTab5Error.invalidPacket }
            var guide: [String: Any] = ["workId": workID]
            for (key, limit) in bounds {
                let value = row[key] as? String ?? ""
                guard row[key] == nil || row[key] is String, value.utf8.count < limit, !value.utf8.contains(0) else { throw MacTab5Error.invalidPacket }
                guide[key] = value
            }
            guard !(guide["title"] as! String).isEmpty, !(guide["author"] as! String).isEmpty, !(guide["overview"] as! String).isEmpty else { throw MacTab5Error.invalidPacket }
            var text = "", explanation = ""
            if ["source_verified", "reference_verified"].contains(status) {
                guard let verified = original["text"] as? String, !verified.isEmpty, verified.utf8.count < 8192 else { throw MacTab5Error.invalidPacket }
                text = verified; explanation = original["explanation"] as? String ?? ""
                if let complete = original["completeReferenceText"] as? String {
                    guard !complete.isEmpty, original["completeReferenceTextSha256"] == nil || original["completeReferenceTextSha256"] as? String == MacTab5Protocol.sha256(Data(complete.utf8)) else { throw MacTab5Error.invalidPacket }
                    text = complete
                }
            }
            guard !text.utf8.contains(0), explanation.utf8.count < 512 else { throw MacTab5Error.invalidPacket }
            _ = try pages(text); guide["transcription"] = text; guide["explanation"] = explanation; guides[workID] = guide
        }
        return guides
    }
    static func projection(_ full: [String: Any], version: Int) throws -> [String: Any] {
        var guide = full
        let text = full["transcription"] as? String ?? ""
        guide["transcriptionPages"] = 1; guide["transcriptionSha256"] = ""
        if version < 2 && text.utf8.count >= 512 || version < 3 && text.utf8.count >= 8192 {
            guide["transcription"] = "此作品含完整长篇原文，请更新 TAB5 固件后阅读。"; guide["explanation"] = ""
        } else if version >= 3 && text.utf8.count >= 8192 {
            let all = try pages(text); guide["transcription"] = all[0]; guide["transcriptionPages"] = all.count
            guide["transcriptionSha256"] = MacTab5Protocol.sha256(Data(text.utf8))
        }
        return guide
    }
}
