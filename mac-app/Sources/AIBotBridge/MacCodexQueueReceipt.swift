import Darwin
import Foundation

// Read only during an explicitly authorized submit. The desktop's committed
// queue is separate from accepted turn input. Retain hashes, never its contents.
struct MacCodexQueueReceipt {
    let known: Set<String>
    let matching: [Set<String>]

    static func server(_ result: [String: Any], expected: String) -> Self? {
        guard !expected.isEmpty, expected.utf8.count <= 2000,
              let entries = result["data"] as? [[String: Any]], entries.count <= 1024,
              result["nextCursor"] == nil || result["nextCursor"] is NSNull else { return nil }
        var known: Set<String> = [], matching: [Set<String>] = [], ids: Set<String> = []
        for entry in entries {
            guard let id = entry["id"] as? String, !id.isEmpty, id.utf8.count <= 256,
                  let client = entry["clientUserMessageId"] as? String, !client.isEmpty, client.utf8.count <= 256,
                  ids.insert(id).inserted else { return nil }
            let clientHash = MacTab5Protocol.sha256(Data(client.utf8))
            let aliases: Set<String> = [MacTab5Protocol.sha256(Data(id.utf8)), clientHash]
            known.formUnion(aliases)
            guard let input = entry["input"] as? [[String: Any]], !input.isEmpty, input.count <= 16 else { continue }
            var body = "", valid = true
            for item in input {
                guard item["type"] as? String == "text", item["truncated"] as? Bool != true,
                      let text = item["text"] as? String, text.utf8.count <= 2000 else { valid = false; break }
                body += text
                if body.utf8.count > 2000 { valid = false; break }
            }
            if valid, MacCodexSubmissionReceipt.paragraphText(body) == MacCodexSubmissionReceipt.paragraphText(expected) {
                // The client identity also links a locally queued entry to its
                // server-queue/accepted representation; an old item stays old.
                matching.append(aliases)
            }
        }
        return Self(known: known, matching: matching)
    }

    static func local(task: String, expected: String) -> Self? {
        let env = ProcessInfo.processInfo.environment
        guard env["CODEX_APP_SERVER_FORCE_CLI"] == "1" || (env["CODEX_APP_SERVER_WS_URL"] ?? "").isEmpty else { return nil }
        let home = env["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
        return read(home.appendingPathComponent(".codex-global-state.json"), task: task, expected: expected)
    }

    static func read(_ url: URL, task: String, expected: String) -> Self? {
        let fd = open(url.path, O_RDONLY | O_NOFOLLOW | O_NONBLOCK)
        guard fd >= 0 else { return nil }
        let file = FileHandle(fileDescriptor: fd, closeOnDealloc: true)
        defer { try? file.close() }
        var before = stat(), after = stat(), current = stat()
        guard fstat(fd, &before) == 0, before.st_mode & S_IFMT == S_IFREG,
              before.st_uid == getuid(), before.st_mode & 0o022 == 0,
              before.st_size > 0, before.st_size <= 16 * 1024 * 1024,
              let bytes = try? file.read(upToCount: 16 * 1024 * 1024 + 1), bytes.count == before.st_size,
              fstat(fd, &after) == 0, lstat(url.path, &current) == 0,
              before.st_dev == current.st_dev, before.st_ino == current.st_ino,
              before.st_size == after.st_size,
              before.st_mtimespec.tv_sec == after.st_mtimespec.tv_sec,
              before.st_mtimespec.tv_nsec == after.st_mtimespec.tv_nsec,
              before.st_ctimespec.tv_sec == after.st_ctimespec.tv_sec,
              before.st_ctimespec.tv_nsec == after.st_ctimespec.tv_nsec,
              let root = try? JSONSerialization.jsonObject(with: bytes) as? [String: Any] else { return nil }
        return parse(root, task: task, expected: expected)
    }

    static func parse(_ root: [String: Any], task: String, expected: String) -> Self? {
        guard MacTab5Codex.id(task) == task, !expected.isEmpty, expected.utf8.count <= 2000,
              let queues = root["queued-follow-ups"] as? [String: Any] else { return nil }
        // A missing target in a valid queue map is empty, not a failed read.
        guard let raw = queues[task] else { return Self(known: [], matching: []) }
        guard let messages = raw as? [[String: Any]], messages.count <= 1024 else { return nil }
        var known: Set<String> = [], matching: [Set<String>] = [], ids: Set<String> = []
        for message in messages {
            guard let id = message["id"] as? String, !id.isEmpty, id.utf8.count <= 256,
                  ids.insert(id).inserted else { return nil }
            let hash = MacTab5Protocol.sha256(Data(id.utf8))
            var aliases: Set<String> = [hash]
            for key in ["clientUserMessageId", "serverQueuedMessageId"] {
                if let value = message[key], !(value is NSNull) {
                    guard let alias = value as? String, !alias.isEmpty, alias.utf8.count <= 256 else { return nil }
                    aliases.insert(MacTab5Protocol.sha256(Data(alias.utf8)))
                }
            }
            known.formUnion(aliases)
            // Pending here means durably accepted by the desktop's local queue,
            // not accepted by the model. Failed/unknown sends are never success.
            if let raw = message["submission"] {
                guard let submission = raw as? [String: Any], submission["hostId"] as? String == "local",
                      let status = submission["status"] as? String, ["pending", "queued"].contains(status) else { continue }
            }
            if let raw = message["submissionOptions"] {
                guard let options = raw as? [String: Any] else { continue }
                if let host = options["executionHostId"], host as? String != "local" { continue }
            }
            guard message["pausedReason"] == nil, message["serverQueuedMessageId"] == nil,
                  let context = message["context"] as? [String: Any],
                  let prompt = context["prompt"] as? String, prompt.utf8.count <= 2000,
                  MacCodexSubmissionReceipt.paragraphText(prompt) == MacCodexSubmissionReceipt.paragraphText(expected) else { continue }
            // This flow stages plain text only. A partial prompt match must not
            // acknowledge another queued message containing attachments/quotes.
            let required = ["addedFiles", "fileAttachments", "imageAttachments"]
            let optional = ["pastedTextAttachments", "uploadedFileAttachments", "mcpAppModelContextAttachments", "commentAttachments", "imageCommentDrafts", "selectedTextAttachments", "responseTextAnnotations", "threadReferences", "chatGptConversationContexts", "appshotContexts"]
            guard required.allSatisfy({ (context[$0] as? [Any])?.isEmpty == true }),
                  optional.allSatisfy({ context[$0] == nil || (context[$0] as? [Any])?.isEmpty == true }),
                  ["aeonReplyQuote", "untrustedAppMessage", "messageSender", "restoredPromptContext", "imageCommentDraft"].allSatisfy({ context[$0] == nil || context[$0] is NSNull }) else { continue }
            matching.append(aliases)
        }
        return Self(known: known, matching: matching)
    }
}
