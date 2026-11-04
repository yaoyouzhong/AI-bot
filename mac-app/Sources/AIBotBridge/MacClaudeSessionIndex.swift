import Foundation

// Isolated source-side index. It does not publish anything or read prompts as titles.
struct MacClaudeSessionObservation: Equatable {
    let sessionID: UUID
    let lastActivity: Date
    let title: String?
    let directory: String?
    init(sessionID: UUID, lastActivity: Date, title: String?, directory: String? = nil) {
        self.sessionID = sessionID; self.lastActivity = lastActivity; self.title = title; self.directory = directory
    }
}

final class MacClaudeSessionIndex {
    private struct Entry {
        let modified: Date
        let size: Int
        let observation: MacClaudeSessionObservation?
        let retryAt: Date?
    }
    private let root: URL
    private var cache: [URL: Entry] = [:]

    init(root: URL) { self.root = root }

    // Call on a background queue. Neither raw records nor file paths leave this index.
    func scan(now: Date, sessionID: UUID? = nil) -> MacClaudeSessionObservation? {
        scanAll(now:now).first { sessionID == nil || $0.sessionID == sessionID }
    }

    func scanAll(now: Date) -> [MacClaudeSessionObservation] {
        let manager = FileManager.default
        guard let files = manager.enumerator(at: root, includingPropertiesForKeys: [.contentModificationDateKey, .fileSizeKey, .isRegularFileKey, .isSymbolicLinkKey], options: [.skipsHiddenFiles]) else {
            cache.removeAll(); return []
        }
        var seen = Set<URL>(), candidates: [MacClaudeSessionObservation] = []
        for case let file as URL in files where file.pathExtension == "jsonl" && !file.pathComponents.contains("subagents") {
            guard let id = UUID(uuidString: file.deletingPathExtension().lastPathComponent),
                  let info = try? file.resourceValues(forKeys: [.contentModificationDateKey, .fileSizeKey, .isRegularFileKey, .isSymbolicLinkKey]),
                  info.isRegularFile == true, info.isSymbolicLink != true,
                  let modified = info.contentModificationDate, let size = info.fileSize else { continue }
            seen.insert(file)
            if cache[file]?.modified != modified || cache[file]?.size != size ||
                (cache[file]?.retryAt.map { now >= $0 } ?? false) {
                if let read = Self.read(file: file, id: id, now: now) {
                    cache[file] = Entry(modified: modified, size: size, observation: read.observation, retryAt: read.retryAt)
                } else { cache.removeValue(forKey: file) }
            }
            guard let candidate = cache[file]?.observation,
                  candidate.lastActivity <= now.addingTimeInterval(60) else { continue }
            candidates.append(candidate)
        }
        cache = cache.filter { seen.contains($0.key) }
        return Dictionary(grouping:candidates,by: \.sessionID).values.filter { $0.count == 1 }.map { $0[0] }.sorted {
            $0.lastActivity == $1.lastActivity ? $0.sessionID.uuidString < $1.sessionID.uuidString : $0.lastActivity > $1.lastActivity
        }
    }

    private static func read(file: URL, id: UUID, now: Date) -> (observation: MacClaudeSessionObservation?, retryAt: Date?)? {
        guard let stream = InputStream(url: file) else { return nil }
        stream.open(); defer { stream.close() }
        let fractional = ISO8601DateFormatter(); fractional.formatOptions = [.withInternetDateTime, .withFractionalSeconds]
        let plain = ISO8601DateFormatter()
        var title = MacClaudeSessionTitle(sessionID: id), latest = Date.distantPast, retryAt: Date?, directory: String?
        var pending = Data(), buffer = [UInt8](repeating: 0, count: 65_536), oversized = false
        func consume(_ data: Data) {
            guard let event = try? JSONDecoder().decode(ActivityRecord.self, from: data),
                  let rawID = event.sessionId, UUID(uuidString: rawID) == id else { return }
            if event.type == "custom-title" || event.type == "ai-title" { title.consume(data); return }
            guard (event.type == "user" || event.type == "assistant"), event.isSidechain != true,
                  let timestamp = event.timestamp,
                  let date = fractional.date(from: timestamp) ?? plain.date(from: timestamp) else { return }
            if date > now.addingTimeInterval(60) {
                let retry = date.addingTimeInterval(-60)
                retryAt = min(retryAt ?? retry, retry); return
            }
            latest = max(latest, date)
            if date == latest, let cwd = event.cwd, cwd.hasPrefix("/"), !cwd.contains("\0"), cwd.utf8.count <= 4096 { directory = cwd }
        }
        while true {
            let count = stream.read(&buffer, maxLength: buffer.count)
            if count < 0 { return nil }
            if count == 0 { break }
            for byte in buffer.prefix(count) {
                if byte == 10 {
                    if !oversized { consume(pending) }
                    pending.removeAll(keepingCapacity: true); oversized = false
                } else if !oversized {
                    if pending.count < 2_000_000 { pending.append(byte) }
                    else { pending.removeAll(keepingCapacity: true); oversized = true }
                }
            }
        }
        // An incomplete trailing line is ignored until a later file update.
        guard latest != .distantPast else { return (nil, retryAt) }
        return (MacClaudeSessionObservation(sessionID: id, lastActivity: latest, title: title.title, directory: directory), retryAt)
    }

    private struct ActivityRecord: Decodable {
        let type: String?
        let sessionId: String?
        let timestamp: String?
        let isSidechain: Bool?
        let cwd: String?
    }
}

// The scanner never blocks a status publisher. Revocation invalidates an
// in-flight scan before its result can be returned to a device.
final class MacClaudeSessionSampler {
    private let lock = NSLock()
    private let queue = DispatchQueue(label: "AI-bot.claude-session-index", qos: .utility)
    private let index: MacClaudeSessionIndex
    private var current: MacClaudeSessionObservation?
    private var scanning = false
    private var generation = 0
    private var nextScan = Date.distantPast
    private var selectedSessionID: UUID?

    init(root: URL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/projects")) {
        index = MacClaudeSessionIndex(root: root)
    }
    func capture(enabled: Bool, now: Date, sessionID: UUID? = nil) -> MacClaudeSessionObservation? {
        lock.lock()
        guard enabled else {
            current = nil; selectedSessionID = nil; generation += 1; nextScan = .distantPast
            lock.unlock(); return nil
        }
        if selectedSessionID != sessionID {
            selectedSessionID = sessionID; current = nil; generation += 1; nextScan = .distantPast
        }
        let result = current
        if !scanning && now >= nextScan {
            scanning = true; nextScan = now.addingTimeInterval(5)
            let expected = generation
            queue.async { [self] in
                let value = index.scan(now: now, sessionID: sessionID)
                lock.lock()
                if generation == expected { current = value }
                scanning = false
                lock.unlock()
            }
        }
        lock.unlock(); return result
    }
}
