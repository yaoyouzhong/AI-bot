import AppKit
import Foundation

enum MacTab5CodexFailure: Error { case cliUnavailable, proxyUnavailable, readOnlyUnavailable, readOnlyConnection, unavailable, rejected, timeout, protocolError }

// The installed `app-server proxy` relays raw WebSocket bytes, not JSONL.
// Use the public stdio endpoint in an app-owned read-only child instead. It
// cannot claim desktop runtime ownership, install a daemon or override policies.
final class MacTab5CodexRPC {
    static let pathKey = "tab5_codex_cli_path"
    struct Lines {
        private var buffer = Data()
        private var scanned = 0
        mutating func append(_ chunk: Data) throws -> [Data] {
            guard chunk.count <= 8 * 1024 * 1024 - buffer.count else { throw MacTab5CodexFailure.protocolError }
            buffer.append(chunk)
            var lines: [Data] = []
            let consumed = buffer.withUnsafeBytes { (bytes: UnsafeRawBufferPointer) -> Int in
                var start = 0
                // Prior chunks contain no unconsumed newline. Rescanning them
                // on each pipe read makes large JSON responses quadratic.
                for index in scanned..<bytes.count where bytes[index] == 10 {
                    lines.append(Data(bytes[start..<index])); start = index + 1
                }
                return start
            }
            if consumed > 0 { buffer.removeFirst(consumed) }
            scanned = buffer.count
            return lines
        }
    }
    private let calls = NSLock()
    private let writes = NSLock()
    private let condition = NSCondition()
    private var process: Process?
    private var input: FileHandle?
    private var answers: [Int: [String: Any]] = [:]
    private var ended = true
    private var generation = UUID()
    private var nextID = 0
    private let executable: () -> String?
    private static let readMethods: Set<String> = ["thread/list", "thread/read", "thread/turns/list", "thread/items/list"]
    init(executable: @escaping () -> String? = MacTab5CodexRPC.locate) { self.executable = executable }
    var canMutate: Bool { false }
    static func locate() -> String? {
        var applications = NSRunningApplication.runningApplications(withBundleIdentifier: "com.openai.codex").compactMap(\.bundleURL)
        if let registered = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.openai.codex") { applications.append(registered) }
        applications += [URL(fileURLWithPath: "/Applications/Codex.app"), URL(fileURLWithPath: "/Applications/ChatGPT.app")]
        return locate(saved: UserDefaults.standard.string(forKey: pathKey), applications: applications,
                      isExecutable: FileManager.default.isExecutableFile(atPath:))
    }
    static func locate(saved: String?, applications: [URL], isExecutable: (String) -> Bool) -> String? {
        let bundled = applications.flatMap { app in
            ["Contents/Resources/codex", "Contents/Resources/codex-cli/CodexCLI.app/Contents/MacOS/codex"].map { app.appendingPathComponent($0).path }
        }
        return ([saved].compactMap { $0 } + bundled + ["/opt/homebrew/bin/codex", "/usr/local/bin/codex"]).first {
            $0.hasPrefix("/") && !$0.contains("\0") && isExecutable($0)
        }
    }
    func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
        calls.lock(); defer { calls.unlock() }
        guard Self.readMethods.contains(method) else { throw MacTab5CodexFailure.readOnlyConnection }
        do {
            if process?.isRunning != true {
                try start()
                do { _ = try request("initialize", ["clientInfo": ["name": "aibot_tab5_mac", "title": "AI-bot TAB5", "version": "1.0"]]) }
                catch MacTab5CodexFailure.unavailable { throw MacTab5CodexFailure.readOnlyUnavailable }
                try write(["method": "initialized"])
            }
            return try request(method, params)
        } catch { stopLocked(); throw error }
    }
    func stop() { calls.lock(); stopLocked(); calls.unlock() }
    // A separate read-only reader is owned by the explicit submission adapter.
    // Do not queue behind history requests or extend the device's RPC deadline.
    func queueReceipt(task: String, expected: String) -> MacCodexQueueReceipt? {
        guard MacTab5Codex.id(task) == task, calls.try() else { return nil }
        defer { calls.unlock() }
        let deadline = Date().addingTimeInterval(0.6)
        do {
            if process?.isRunning != true {
                try start()
                _ = try request("initialize", ["clientInfo": ["name": "aibot_tab5_queue", "version": "1.0"],
                    "capabilities": ["experimentalApi": true]], deadline: deadline)
                try write(["method": "initialized"])
            }
            let response = try request("thread/queue/list", ["threadId": task, "cursor": NSNull()], deadline: deadline)
            return MacCodexQueueReceipt.server(response, expected: expected)
        } catch { stopLocked(); return nil }
    }
    deinit { stopLocked() }
    private func start() throws {
        guard let path = executable() else { throw MacTab5CodexFailure.cliUnavailable }
        let child = Process(), stdin = Pipe(), stdout = Pipe(), stderr = Pipe()
        child.executableURL = URL(fileURLWithPath: path)
        child.arguments = ["app-server", "--listen", "stdio://"]
        child.standardInput = stdin; child.standardOutput = stdout; child.standardError = stderr
        condition.lock(); ended = false; answers.removeAll(); generation = UUID(); let owner = generation; condition.unlock()
        do { try child.run() } catch { throw MacTab5CodexFailure.cliUnavailable }
        process = child; input = stdin.fileHandleForWriting
        // Drain diagnostics without retaining or logging paths, prompts or tokens.
        DispatchQueue.global(qos: .utility).async { while !stderr.fileHandleForReading.availableData.isEmpty {} }
        DispatchQueue.global(qos: .utility).async { [weak self] in
            var lines = Lines()
            while true {
                let chunk = stdout.fileHandleForReading.availableData
                if chunk.isEmpty { break }
                guard let complete = try? lines.append(chunk) else { break }
                for line in complete {
                    guard let root = try? MacTab5Protocol.object(line) else { continue }
                    self?.receive(root, owner: owner)
                }
            }
            guard let self else { return }
            self.condition.lock()
            if self.generation == owner { self.ended = true; self.condition.broadcast() }
            self.condition.unlock()
        }
    }
    private func receive(_ root: [String: Any], owner: UUID) {
        condition.lock(); defer { condition.unlock() }
        guard generation == owner else { return }
        if root["method"] != nil {
            // Approval/user-input requests must be handled on the computer. Never
            // automatically approve a command or synthesize a user response.
            if let id = root["id"] { try? write(["id": id, "error": ["code": -32000, "message": "Handle approvals and questions in Codex on the computer."]]) }
        } else if let id = root["id"] as? Int, answers.count < 32 { answers[id] = root; condition.broadcast() }
    }
    private func request(_ method: String, _ params: [String: Any], deadline: Date = Date().addingTimeInterval(12)) throws -> [String: Any] {
        guard deadline > Date() else { throw MacTab5CodexFailure.timeout }
        nextID += 1; let id = nextID
        try write(["id": id, "method": method, "params": params])
        condition.lock(); defer { condition.unlock() }
        while answers[id] == nil && !ended { if !condition.wait(until: deadline) { throw MacTab5CodexFailure.timeout } }
        guard let answer = answers.removeValue(forKey: id) else { throw MacTab5CodexFailure.unavailable }
        guard answer["error"] == nil, let result = answer["result"] as? [String: Any] else { throw MacTab5CodexFailure.rejected }
        return result
    }
    private func write(_ root: [String: Any]) throws {
        writes.lock(); defer { writes.unlock() }
        guard let input else { throw MacTab5CodexFailure.unavailable }
        var bytes = try MacTab5Protocol.json(root); bytes.append(10)
        do { try input.write(contentsOf: bytes) } catch { throw MacTab5CodexFailure.unavailable }
    }
    private func stopLocked() {
        try? input?.close(); input = nil
        if let child = process, child.isRunning { child.terminate() }
        process = nil; condition.lock(); ended = true; generation = UUID(); answers.removeAll(); condition.broadcast(); condition.unlock()
    }
}
