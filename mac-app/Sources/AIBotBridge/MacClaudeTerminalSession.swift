import Darwin
import Foundation

struct MacClaudeTerminalProcess: Equatable {
    let pid: pid_t
    let seconds, micros: UInt64
    let executable: String

    static func inspect(_ pid: pid_t) -> (identity: Self, info: proc_bsdinfo)? {
        guard pid > 1 else { return nil }
        var info = proc_bsdinfo()
        let size = Int32(MemoryLayout<proc_bsdinfo>.size)
        guard proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, &info, size) == size,
              info.pbi_uid == getuid(), info.pbi_ruid == getuid() else { return nil }
        var path = [CChar](repeating: 0, count: 4096)
        guard proc_pidpath(pid, &path, UInt32(path.count)) > 0 else { return nil }
        let executable = URL(fileURLWithPath: String(cString: path)).resolvingSymlinksInPath().path
        return (.init(pid: pid, seconds: info.pbi_start_tvsec, micros: info.pbi_start_tvusec, executable: executable), info)
    }

    func registered(at timestamp: Int64) -> Bool {
        let milliseconds = Double(seconds) * 1000 + Double(micros) / 1000
        return abs(milliseconds - Double(timestamp)) <= 15_000
    }

    static func child(_ pid: pid_t, of parent: pid_t) -> Bool {
        var current = pid
        for _ in 0..<6 {
            if current == parent { return true }
            guard let info = inspect(current)?.info, let next = pid_t(exactly: info.pbi_ppid), next > 1 else { return false }
            current = next
        }
        return false
    }
}

struct MacClaudeTerminalRegistration: Codable {
    let session: UUID
    let launcher, child: pid_t
    let launchedAt: Int64
    let newSession: Bool

    static let directory = URL(fileURLWithPath: "/private/tmp/aibot-claude-tty-\(getuid())", isDirectory: true)
    static let supportedVersions: Set<String> = ["2.1.318", "2.1.319"]
    static let defaultConfig = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude")

    static func privateDirectory(_ path: URL) -> Bool {
        var info = stat()
        return lstat(path.path, &info) == 0 && info.st_uid == getuid() && info.st_mode & S_IFMT == S_IFDIR && info.st_mode & 0o077 == 0
    }

    static func createDirectory() throws -> URL {
        if mkdir(directory.path, 0o700) != 0 && errno != EEXIST { throw MacTab5Error.unavailable }
        guard privateDirectory(directory) else { throw MacTab5Error.unconfirmed }
        var template = Array(directory.appendingPathComponent("tab-XXXXXX").path.utf8CString)
        guard template.withUnsafeMutableBufferPointer({ mkdtemp($0.baseAddress!) }) != nil else {
            throw MacTab5Error.unavailable
        }
        return URL(fileURLWithPath: String(cString: template), isDirectory: true)
    }

    static func rows(_ config: URL) -> [[String: Any]] {
        let directory = config.appendingPathComponent("sessions", isDirectory: true)
        guard let files = try? FileManager.default.contentsOfDirectory(at: directory, includingPropertiesForKeys: nil),
              files.count <= 2048 else { return [] }
        return files.compactMap {
            guard $0.pathExtension == "json", let pid = Int($0.deletingPathExtension().lastPathComponent),
                  let record = read($0), record["pid"] as? Int == pid else { return nil }
            return record
        }
    }

    static func read(_ file: URL) -> [String: Any]? {
        let fd = Darwin.open(file.path, O_RDONLY | O_CLOEXEC | O_NONBLOCK | O_NOFOLLOW)
        guard fd >= 0 else { return nil }
        let handle = FileHandle(fileDescriptor: fd, closeOnDealloc: true)
        defer { try? handle.close() }
        var info = stat()
        guard fstat(fd, &info) == 0, info.st_uid == getuid(), info.st_mode & S_IFMT == S_IFREG,
              info.st_mode & 0o022 == 0, (1...65_536).contains(info.st_size),
              let data = try? handle.read(upToCount: 65_537), data.count == info.st_size else { return nil }
        return try? MacTab5Protocol.object(data)
    }

    func findWorker(config: URL, executable: String) -> (MacClaudeTerminalProcess, String)? {
        let matching = Self.rows(config).compactMap { row -> (MacClaudeTerminalProcess, proc_bsdinfo, [String: Any])? in
            guard UUID(uuidString: row["sessionId"] as? String ?? "") == session,
                  let value = row["pid"] as? Int, let pid = pid_t(exactly: value),
                  let started = MacTab5Protocol.integer(row["startedAt"]),
                  let process = MacClaudeTerminalProcess.inspect(pid), process.identity.registered(at: started) else { return nil }
            return (process.identity, process.info, row)
        }
        guard matching.count == 1, let (process, info, row) = matching.first,
              process.executable == executable, row["kind"] as? String == "interactive",
              row["entrypoint"] as? String == "cli", Self.supportedVersions.contains(row["version"] as? String ?? ""),
              MacClaudeTerminalProcess.child(process.pid, of: child),
              info.pbi_pgid > 0, info.e_tpgid == info.pbi_pgid,
              let state = row["status"] as? String else { return nil }
        return (process, state)
    }
}

final class MacClaudeTerminalSession: MacClaudeDirectSession {
    let usesFileImageReferences = true
    let source = MacClaudeSessionSource.terminal
    let session: UUID
    private let registration: MacClaudeTerminalRegistration
    private let config: URL
    private let socket: URL
    private let launcher, worker: MacClaudeTerminalProcess

    private init(_ registration: MacClaudeTerminalRegistration, config: URL, socket: URL,
                 launcher: MacClaudeTerminalProcess, worker: MacClaudeTerminalProcess) {
        self.session = registration.session
        self.registration = registration; self.config = config; self.socket = socket
        self.launcher = launcher; self.worker = worker
    }

    static func find(_ session: UUID, config: URL = MacClaudeTerminalRegistration.defaultConfig,
                     bridge: URL? = Bundle.main.executableURL) -> MacClaudeTerminalSession? {
        let root = MacClaudeTerminalRegistration.directory
        guard MacClaudeTerminalRegistration.privateDirectory(root),
              let folders = try? FileManager.default.contentsOfDirectory(at: root, includingPropertiesForKeys: nil),
              folders.count <= 128, let expected = bridge?.resolvingSymlinksInPath().path,
              let executable = MacClaudeWorkspaceBackend.locate()?.resolvingSymlinksInPath().path else { return nil }
        let matches = folders.compactMap { folder -> MacClaudeTerminalSession? in
            guard folder.lastPathComponent.hasPrefix("tab-"), MacClaudeTerminalRegistration.privateDirectory(folder),
                  let row = MacClaudeTerminalRegistration.read(folder.appendingPathComponent("session.json")),
                  let data = try? MacTab5Protocol.json(row),
                  let registration = try? JSONDecoder().decode(MacClaudeTerminalRegistration.self, from: data),
                  registration.session == session,
                  let launcher = MacClaudeTerminalProcess.inspect(registration.launcher)?.identity,
                  launcher.executable == expected, launcher.registered(at: registration.launchedAt),
                  let (worker, _) = registration.findWorker(config: config, executable: executable) else { return nil }
            return Self(registration, config: config, socket: folder.appendingPathComponent("control.sock"),
                        launcher: launcher, worker: worker)
        }
        return matches.count == 1 ? matches[0] : nil
    }

    func state() throws -> String {
        guard MacClaudeTerminalProcess.inspect(launcher.pid)?.identity == launcher,
              let (active, state) = registration.findWorker(config: config, executable: worker.executable),
              active == worker else { throw MacTab5Error.unconfirmed }
        return state
    }

    func scope() throws -> String {
        _ = try state()
        return MacClaudeNativeContent.hash(Data("claude-tty|\(session)|\(launcher.pid):\(launcher.seconds):\(launcher.micros)|\(worker.pid):\(worker.seconds):\(worker.micros)".utf8))
    }

    func allows(_ operation: String, images: Bool) -> Bool {
        guard let state = try? state() else { return false }
        if operation == "send" { return state == "idle" }
        return (operation == "steer" || operation == "interrupt" && !images) && ["working", "busy"].contains(state)
    }

    private func exchange(_ fields: [String: Any], requireStop: Bool = false) throws -> Bool {
        let deadline = ProcessInfo.processInfo.systemUptime + 3
        let connection = try MacClaudeLocalSocket(path: socket, peerPID: launcher.pid, until: deadline)
        guard MacClaudeTerminalProcess.inspect(launcher.pid)?.identity == launcher else { throw MacTab5Error.unconfirmed }
        let bytes = try MacTab5Protocol.json(fields)
        guard bytes.count < 4096 else { throw MacTab5Error.invalidPacket }
        try connection.send(bytes + Data([10]), until: deadline)
        let response = try connection.reply(until: deadline)
        guard MacTab5Protocol.isBoolean(response["accepted"]), let accepted = response["accepted"] as? Bool else {
            throw MacTab5Error.unconfirmed
        }
        if accepted, requireStop, response["stopped"] as? Bool != true { throw MacTab5Error.timeout }
        return accepted
    }

    func isVerifiedBlank() -> Bool {
        guard registration.newSession, (try? state()) == "idle" else { return false }
        let fields: [String: Any] = ["op": "send", "session": session.uuidString, "worker": worker.pid,
                                      "text": "probe", "images": [String](), "digests": [String](), "probe": true]
        return (try? exchange(fields)) == true
    }

    static func newSessions() -> [MacClaudeSessionObservation] {
        MacClaudeNativeClient.live(entrypoint: "cli").compactMap { id in
            guard let client = find(id), client.isVerifiedBlank(),
                  let row = MacClaudeTerminalRegistration.read(client.config.appendingPathComponent("sessions/\(client.worker.pid).json"))
            else { return nil }
            return .init(sessionID: id, lastActivity: Date(timeIntervalSince1970: Double(client.registration.launchedAt) / 1000),
                         title: "Claude Code", directory: row["cwd"] as? String)
        }
    }

    func prepareSubmission(_ request: MacClaudeNativeRequest) throws -> () throws -> Bool {
        guard request.authorized(), try request.current(), allows(request.operation, images: !request.paths.isEmpty) else {
            throw MacCodexDesktopError.rejected("native_session_changed")
        }
        var fields: [String: Any] = ["op": request.operation, "session": session.uuidString, "worker": worker.pid,
                                      "text": request.text, "images": request.paths.map(\.path), "digests": request.imageDigests]
        fields["expectedUser"] = request.expectedUser
        var probe = fields
        probe["probe"] = true
        guard try exchange(probe) else { throw MacCodexDesktopError.rejected("native_terminal_composer_unavailable") }
        return {
            guard request.authorized(), (try? request.current()) == true else { return false }
            return try self.exchange(fields, requireStop: request.operation == "interrupt")
        }
    }
}
