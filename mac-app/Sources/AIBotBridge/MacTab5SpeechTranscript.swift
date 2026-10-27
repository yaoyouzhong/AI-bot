import Foundation

// Recognition hypotheses may cover just the latest utterance after a pause.
// Audio positions distinguish that from a correction to the same utterance;
// comparing text prefixes would duplicate corrections or discard repetition.
struct MacTab5SpeechTranscript {
    private struct Part {
        var start: TimeInterval
        var end: TimeInterval
        var text: String
    }
    private var parts: [Part] = []
    private var pending = ""

    mutating func update(_ text: String, start: TimeInterval?, end: TimeInterval?) -> String {
        let text = Self.paragraph([text])
        guard !text.isEmpty else { return value }
        guard let start, let end, start.isFinite, end.isFinite, start >= 0, end >= start, end > 0 else {
            // The local recognizer also reports placeholder 0/0 positions for
            // partial hypotheses. Keep only the latest provisional text; do
            // not insert it into the audio-ordered utterances.
            pending = text
            return value
        }
        pending = ""
        if let last = parts.last, start > last.start + 0.1, start >= last.end - 0.05 {
            parts.append(Part(start: start, end: end, text: text))
        } else {
            // A later cumulative hypothesis may cover earlier utterances again.
            // Replace its covered range, retaining subsequent non-overlap.
            let first = parts.firstIndex { $0.end > start + 0.05 || abs($0.start - start) <= 0.1 } ?? parts.count
            let after = parts[first...].firstIndex { $0.start > end + 0.05 } ?? parts.count
            parts.replaceSubrange(first..<after, with: [Part(start: start, end: end, text: text)])
        }
        return value
    }
    var value: String {
        let stable = parts.map(\.text)
        guard !pending.isEmpty else { return Self.paragraph(stable) }
        guard let last = stable.last else { return pending }
        // These comparisons reconcile display copies only. Actual utterance
        // identity/order is determined by audio positions, including repetition.
        var remaining = pending[...]
        let covers = stable.allSatisfy { text in
            guard let range = remaining.range(of: text) else { return false }
            remaining = remaining[range.upperBound...]; return true
        }
        if covers { return pending }
        if last.hasPrefix(pending) { return Self.paragraph(stable) }
        if pending.hasPrefix(last) { return Self.paragraph(stable.dropLast() + [pending]) }
        return Self.paragraph(stable + [pending])
    }
    // Pauses identify audio ranges, not paragraphs. This touches recognized
    // text only; the device still inserts it into the user's unchanged draft.
    private static func paragraph(_ utterances: [String]) -> String {
        var result = ""
        func word(_ byte: UInt8) -> Bool {
            (48...57).contains(byte) || (65...90).contains(byte) || (97...122).contains(byte)
        }
        for line in utterances.flatMap({ $0.components(separatedBy: .newlines) }) {
            let part = line.trimmingCharacters(in: .whitespaces)
            guard !part.isEmpty else { continue }
            if let last = result.utf8.last, let first = part.utf8.first,
               word(first), word(last) || ".!?:;".utf8.contains(last) { result += " " }
            result += part
        }
        return result
    }
    var utteranceCount: Int { parts.count }
}
