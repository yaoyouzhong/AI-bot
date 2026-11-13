import Foundation

// These byte strings are hashed and stored on TF by both bridges. Keep the
// field order and escaping compatible with the Windows 321 wire format.
enum MacTab5GalleryWire {
    private static let fileEntry = try! NSRegularExpression(pattern: #""((?:[^"\\]|\\.)*)"\s*:\s*"([0-9a-f]{64})""#)

    static func revision(_ manifest: MacTab5GalleryManifest, bytes: Data, full: Bool) throws -> String {
        // Windows preserves PACK dictionary insertion order when it hashes
        // the reserialized manifest. Swift's dictionary does not preserve it.
        guard let raw = String(data: bytes, encoding: .utf8) else { throw MacTab5Error.invalidPacket }
        let entries = fileEntry.matches(in: raw, range: NSRange(raw.startIndex..., in: raw)).compactMap { match -> (String, String)? in
            guard let keyRange = Range(match.range(at: 1), in: raw), let valueRange = Range(match.range(at: 2), in: raw) else { return nil }
            let token = String(raw[keyRange]), value = String(raw[valueRange])
            let key = token.contains("\\") ? (try? JSONDecoder().decode(String.self, from: Data(("\"" + token + "\"").utf8))) : token
            guard let key, manifest.files[key] == value else { return nil }
            return (key, value)
        }
        guard entries.count == manifest.files.count, Set(entries.map(\.0)).count == entries.count else { throw MacTab5Error.invalidPacket }
        let files = entries.map { "\(quoted($0.0)):\(quoted($0.1))" }.joined(separator: ",")
        let pack = #"{"schemaVersion":\#(manifest.schemaVersion),"version":\#(quoted(manifest.version)),"category":\#(quoted(manifest.category)),"works":\#(manifest.works),"frames":\#(manifest.frames),"files":{\#(files)},"collection":\#(manifest.collection.map(quoted) ?? "null")}"#
        let scope = manifest.collection != "full" ? pack : full
            ? #"{"manifest":\#(pack),"range":"full","order":"annual-first-v1"}"#
            : #"{"manifest":\#(pack),"range":"annual"}"#
        return MacTab5Protocol.sha256(Data(scope.utf8))
    }

    static func imageRevision(_ manifest: MacTab5GalleryManifest, full: Bool) -> String {
        let files = manifest.files.keys.filter { $0.hasSuffix(".jpg") || $0 == "catalog.json" || $0 == "annual-index.json" }
            .sorted().map { #"{"Key":\#(quoted($0)),"Value":\#(quoted(manifest.files[$0]!))}"# }.joined(separator: ",")
        let scope = manifest.collection == "full" ? (full ? "full:annual-first-v1" : "full:annual") : "annual"
        return MacTab5Protocol.sha256(Data(#"{"Category":\#(quoted(manifest.category)),"scope":\#(quoted(scope)),"files":[\#(files)]}"#.utf8))
    }

    static func record(workID: String, guide: [String: Any]?) throws -> Data {
        var payload = "null"
        if let guide {
            let projected = try MacTab5ArtGuide.projection(guide, version: 3)
            let keys = ["workId", "title", "author", "period", "museum", "brief", "overview", "looking", "transcription", "explanation"]
            var fields = keys.map { "\(quoted($0)):\(quoted(projected[$0] as? String ?? ""))" }
            let count = projected["transcriptionPages"] as? Int ?? 1
            fields.append(#""transcriptionPages":\#(count)"#)
            fields.append(#""transcriptionSha256":\#(quoted(projected["transcriptionSha256"] as? String ?? ""))"#)
            if count > 1 {
                let pages = try MacTab5ArtGuide.pages(guide["transcription"] as? String ?? "").map(quoted).joined(separator: ",")
                fields.append(#""transcriptionPageTexts":[\#(pages)]"#)
            }
            payload = "{" + fields.joined(separator: ",") + "}"
        }
        return Data(#"{"workId":\#(quoted(workID)),"guide":\#(payload)}"#.utf8)
    }

    static func escapedSize(_ scalar: Unicode.Scalar) -> Int {
        switch scalar.value {
        case 8, 9, 10, 12, 13, 92: return 2
        case 0x10000...: return 12
        default: return escape(scalar) ? 6 : scalar.utf8.count
        }
    }

    private static func escape(_ scalar: Unicode.Scalar) -> Bool {
        switch scalar.value {
        case 0x20: return false
        case 34, 38, 39, 43, 60, 62, 96: return true
        default:
            switch scalar.properties.generalCategory {
            case .control, .privateUse, .unassigned, .spaceSeparator, .lineSeparator, .paragraphSeparator: return true
            default: return false
            }
        }
    }

    private static func quoted(_ text: String) -> String {
        var output = "\""
        for scalar in text.unicodeScalars {
            switch scalar.value {
            case 8: output += "\\b"
            case 9: output += "\\t"
            case 10: output += "\\n"
            case 12: output += "\\f"
            case 13: output += "\\r"
            case 92: output += "\\\\"
            case 0x10000...:
                let value = scalar.value - 0x10000
                output += String(format: "\\u%04X\\u%04X", 0xD800 + (value >> 10), 0xDC00 + (value & 0x3FF))
            default:
                if escape(scalar) { output += String(format: "\\u%04X", scalar.value) }
                else { output.unicodeScalars.append(scalar) }
            }
        }
        return output + "\""
    }
}
