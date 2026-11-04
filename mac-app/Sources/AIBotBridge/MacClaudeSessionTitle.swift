import Foundation

// This parser only accepts existing Claude title records for the supplied
// session. It does not derive a title from prompts, summaries or replies.
struct MacClaudeSessionTitle {
    private var custom: String?
    private var generated: String?
    private let sessionID: UUID

    init(sessionID: UUID) { self.sessionID = sessionID }

    mutating func consume(_ data: Data) {
        // Title records are small; never parse an unbounded conversation line.
        guard data.count <= 16_384,
              let record = try? JSONDecoder().decode(TitleRecord.self, from: data),
              let rawID = record.sessionId, UUID(uuidString: rawID) == sessionID else { return }
        switch record.type {
        case "custom-title": custom = Self.clean(record.customTitle)
        case "ai-title": generated = Self.clean(record.aiTitle)
        default: break
        }
    }

    var title: String? { custom ?? generated }

    private static func clean(_ value: String?) -> String? {
        guard let value else { return nil }
        let cleaned = value.components(separatedBy: .controlCharacters).joined(separator: " ")
            .split(whereSeparator: { $0.isWhitespace }).joined(separator: " ")
        guard !cleaned.isEmpty else { return nil }
        var result = "", bytes = 0
        for character in cleaned.prefix(120) {
            let value = String(character), count = value.utf8.count
            if bytes + count > 512 { break }
            result.append(character); bytes += count
        }
        return result.isEmpty ? nil : result
    }

    private struct TitleRecord: Decodable {
        let type: String?
        let sessionId: String?
        let customTitle: String?
        let aiTitle: String?
    }
}
