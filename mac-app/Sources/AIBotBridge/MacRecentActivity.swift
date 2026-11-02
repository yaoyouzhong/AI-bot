import Foundation

// Only file metadata is inspected. The UI never waits for disk enumeration.
final class MacRecentActivity {
    struct Observation {
        var modified: Date?
        var complete = false
    }
    struct Result {
        var codex = Observation()
        var claude = Observation()
        var sampledAt: Date?
    }

    private let queue: DispatchQueue
    private let scan: () -> Result
    private let lock = NSLock()
    private var result = Result(), running = false, next = Date.distantPast

    init(queue: DispatchQueue = DispatchQueue(label: "AI-bot.recent-file-activity", qos: .utility),
         scan: (() -> Result)? = nil) {
        self.queue = queue
        self.scan = scan ?? { Self.inspect(home: FileManager.default.homeDirectoryForCurrentUser) }
    }

    func capture(now: Date = Date()) -> Result {
        lock.lock()
        var current = result
        if let sampledAt = current.sampledAt, now.timeIntervalSince(sampledAt) > 30 {
            current = Result()
        }
        let start = !running && now >= next
        if start { running = true; next = now.addingTimeInterval(5) }
        lock.unlock()
        if start {
            queue.async { [self] in
                let startedAt = Date()
                var value = scan()
                value.sampledAt = startedAt
                lock.lock(); result = value; running = false; lock.unlock()
            }
        }
        return current
    }

    static func inspect(home: URL) -> Result {
        let fm = FileManager.default
        func latest(_ path: String) -> Observation {
            let url = home.appendingPathComponent(path)
            var isDirectory: ObjCBool = false
            guard fm.fileExists(atPath: url.path, isDirectory: &isDirectory) else {
                return Observation(complete: true)
            }
            guard isDirectory.boolValue, fm.isReadableFile(atPath: url.path) else { return Observation() }
            var failed = false
            guard let e = fm.enumerator(at: url,
                includingPropertiesForKeys: [.contentModificationDateKey, .isRegularFileKey, .isSymbolicLinkKey],
                options: [.skipsHiddenFiles], errorHandler: { _, _ in failed = true; return true }) else { return Observation() }
            var newest: Date?
            for case let file as URL in e where file.pathExtension == "jsonl" {
                do {
                    let v = try file.resourceValues(forKeys: [.contentModificationDateKey, .isRegularFileKey, .isSymbolicLinkKey])
                    guard v.isRegularFile == true, v.isSymbolicLink != true, let date = v.contentModificationDate else { continue }
                    if newest == nil || date > newest! { newest = date }
                } catch { failed = true }
            }
            return Observation(modified: newest, complete: !failed)
        }
        return Result(codex: latest(".codex/sessions"), claude: latest(".claude/projects"))
    }

    static func label(state: String, observation: Observation, now: Date = Date()) -> String {
        if state == "working" { return "工作中" }
        if state == "idle" { return "空闲" }
        if let modified = observation.modified, (0..<90).contains(now.timeIntervalSince(modified)) { return "近期有活动" }
        return observation.complete ? "无近期活动" : "检测中"
    }
}
