import Darwin
import Foundation

// Claude's local reply command resolves absolute file references to image
// blocks. Only bridge-staged files count towards a photo delivery receipt.
enum MacClaudeBackgroundPhotos {
    private static let prefix = "aibot-claude-photos-"
    private static let temp = FileManager.default.temporaryDirectory.resolvingSymlinksInPath()

    struct Attachment {
        let reference: String
        let digest: String
    }

    static func makeDirectory() throws -> URL {
        var template = Array(temp.appendingPathComponent(prefix + "XXXXXX").path.utf8CString)
        guard template.withUnsafeMutableBufferPointer({ mkdtemp($0.baseAddress!) }) != nil else {
            throw MacTab5Error.unavailable
        }
        return URL(fileURLWithPath: String(cString: template), isDirectory: true)
    }

    static func stage(_ bytes: Data, in directory: URL) throws -> URL {
        let mime = try MacTab5Photos.imageType(bytes)
        let file = directory.appendingPathComponent(UUID().uuidString.lowercased())
            .appendingPathExtension(mime == "image/png" ? "png" : "jpg")
        try bytes.write(to: file, options: .withoutOverwriting)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: file.path)
        return file
    }

    static func remove(_ directory: URL, deliveryUnknown: Bool) {
        if deliveryUnknown {
            // A sent frame may still be queued in Claude. Give it time to
            // consume the private files without resubmitting the message.
            DispatchQueue.global(qos: .utility).asyncAfter(deadline: .now() + .seconds(600)) {
                try? FileManager.default.removeItem(at: directory)
            }
        } else {
            try? FileManager.default.removeItem(at: directory)
        }
    }

    static func reference(_ path: String) -> String? {
        guard path.hasPrefix("/"), path.utf8.count <= 2048, !path.contains("\""),
              !path.unicodeScalars.contains(where: CharacterSet.controlCharacters.contains) else { return nil }
        let file = URL(fileURLWithPath: path)
        let directory = file.deletingLastPathComponent()
        guard file.standardizedFileURL.path == path, directory.lastPathComponent.hasPrefix(prefix),
              directory.lastPathComponent.utf8.count == prefix.utf8.count + 6,
              directory.deletingLastPathComponent().path == temp.path,
              UUID(uuidString: file.deletingPathExtension().lastPathComponent) != nil,
              ["png", "jpg"].contains(file.pathExtension) else { return nil }
        return path.contains(where: \.isWhitespace) ? "@\"\(path)\"" : "@\(path)"
    }

    static func prompt(_ content: Any?) -> String? {
        let blocks = content as? [[String: Any]]
        let text = content as? String ?? (blocks?.count == 1 && blocks?.first?["type"] as? String == "text"
            ? blocks?.first?["text"] as? String : nil)
        guard let text, text.hasPrefix("@"), text.utf8.count <= 8192 else { return nil }
        return text
    }

    static func attachment(_ row: [String: Any]) -> Attachment? {
        guard let attachment = row["attachment"] as? [String: Any], attachment["type"] as? String == "file",
              let path = attachment["filename"] as? String, let reference = reference(path),
              let content = attachment["content"] as? [String: Any], content["type"] as? String == "image",
              let file = content["file"] as? [String: Any],
              file["type"] as? String == (path.hasSuffix(".png") ? "image/png" : "image/jpeg"),
              let base64 = file["base64"] as? String, base64.utf8.count <= 4 * ((MacTab5Photos.maxBytes + 2) / 3),
              let bytes = Data(base64Encoded: base64), (32...MacTab5Photos.maxBytes).contains(bytes.count) else { return nil }
        return Attachment(reference: reference, digest: MacTab5Protocol.sha256(bytes))
    }

    static func digest(prompt: String, attachments: [Attachment]) -> String? {
        guard (1...MacTab5Photos.maxCount).contains(attachments.count) else { return nil }
        let prefix = attachments.map(\.reference).joined(separator: " ")
        let text: String
        if prompt == prefix { text = "" }
        else if prompt.hasPrefix(prefix + " ") { text = String(prompt.dropFirst(prefix.count + 1)) }
        else { return nil }
        return MacClaudeNativeContent.digest(text, images: attachments.map(\.digest))
    }
}
