import Foundation

enum MacTab5CodexFailure: Error { case unavailable, rejected, timeout, protocolError }

// Public Codex stdio protocol over its local desktop control-socket proxy.
// No independent app-server, login, API key, model or approval-policy override.
final class MacTab5CodexRPC {
    static let pathKey = "tab5_codex_cli_path"
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
    init(executable: @escaping () -> String? = MacTab5CodexRPC.locate) { self.executable = executable }
    static func locate() -> String? {
        let saved = UserDefaults.standard.string(forKey: pathKey)
        return ([saved].compactMap { $0 } + ["/Applications/Codex.app/Contents/Resources/codex", "/opt/homebrew/bin/codex", "/usr/local/bin/codex"]).first { $0.hasPrefix("/") && !$0.contains("\0") && FileManager.default.isExecutableFile(atPath: $0) }
    }
    func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
        calls.lock(); defer { calls.unlock() }
        do {
            if process?.isRunning != true {
                try start()
                _ = try request("initialize", ["clientInfo": ["name": "aibot_tab5_mac", "title": "AI-bot TAB5", "version": "1.0"]])
                try write(["method": "initialized"])
            }
            return try request(method, params)
        } catch { stopLocked(); throw error }
    }
    func stop() { calls.lock(); stopLocked(); calls.unlock() }
    deinit { stopLocked() }
    private func start() throws {
        guard let path = executable() else { throw MacTab5CodexFailure.unavailable }
        let child = Process(), stdin = Pipe(), stdout = Pipe(), stderr = Pipe()
        child.executableURL = URL(fileURLWithPath: path); child.arguments = ["app-server", "proxy"]
        child.standardInput = stdin; child.standardOutput = stdout; child.standardError = stderr
        condition.lock(); ended = false; answers.removeAll(); generation = UUID(); let owner = generation; condition.unlock()
        try child.run(); process = child; input = stdin.fileHandleForWriting
        // Drain diagnostics without retaining or logging paths, prompts or tokens.
        DispatchQueue.global(qos: .utility).async { while !stderr.fileHandleForReading.availableData.isEmpty {} }
        DispatchQueue.global(qos: .utility).async { [weak self] in
            var buffer = Data()
            while true {
                let chunk = stdout.fileHandleForReading.availableData
                if chunk.isEmpty { break }
                buffer.append(chunk)
                if buffer.count > 8 * 1024 * 1024 { break }
                while let newline = buffer.firstIndex(of: 10) {
                    let line = Data(buffer[..<newline]); buffer.removeSubrange(...newline)
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
    private func request(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
        nextID += 1; let id = nextID
        try write(["id": id, "method": method, "params": params])
        let deadline = Date().addingTimeInterval(12)
        condition.lock(); defer { condition.unlock() }
        while answers[id] == nil && !ended { if !condition.wait(until: deadline) { throw MacTab5CodexFailure.timeout } }
        guard let answer = answers.removeValue(forKey: id) else { throw MacTab5CodexFailure.unavailable }
        guard answer["error"] == nil, let result = answer["result"] as? [String: Any] else { throw MacTab5CodexFailure.rejected }
        return result
    }
    private func write(_ root: [String: Any]) throws {
        writes.lock(); defer { writes.unlock() }
        guard let input else { throw MacTab5CodexFailure.unavailable }
        var bytes = try MacTab5Protocol.json(root); bytes.append(10); try input.write(contentsOf: bytes)
    }
    private func stopLocked() {
        try? input?.close(); input = nil
        if let child = process, child.isRunning { child.terminate() }
        process = nil; condition.lock(); ended = true; generation = UUID(); answers.removeAll(); condition.broadcast(); condition.unlock()
    }
}
