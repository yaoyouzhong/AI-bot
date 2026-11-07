import Darwin
import Foundation

/// Claude Code's local supervisor can address an existing background job
/// directly. Discovery and dispatch never start or attach a job.
final class MacClaudeBackground: MacClaudeDirectSession {
    private static let version = "2.1.318"
    private static let defaultRoot = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude")
    private static let socketRoot = URL(fileURLWithPath: "/private/tmp/cc-daemon-\(getuid())", isDirectory: true)
    let source = MacClaudeSessionSource.background
    let session: UUID
    private let root: URL
    private let job: String
    private let worker: ProcessIdentity
    private let daemon: ProcessIdentity

    private struct ProcessIdentity: Equatable {
        let pid: pid_t
        let seconds: UInt64
        let microseconds: UInt64
        let executable: String

        static func inspect(_ pid: pid_t) -> Self? {
            guard pid > 1 else { return nil }
            var info = proc_bsdinfo()
            let size = Int32(MemoryLayout<proc_bsdinfo>.size)
            guard proc_pidinfo(pid, PROC_PIDTBSDINFO, 0, &info, size) == size,
                  info.pbi_uid == getuid(), info.pbi_ruid == getuid() else { return nil }
            var path = [CChar](repeating: 0, count: 4096)
            guard proc_pidpath(pid, &path, UInt32(path.count)) > 0 else { return nil }
            return Self(pid: pid, seconds: info.pbi_start_tvsec, microseconds: info.pbi_start_tvusec,
                        executable: URL(fileURLWithPath: String(cString: path)).resolvingSymlinksInPath().path)
        }

        func matches(_ row: [String: Any]) -> Bool {
            guard row["pid"] as? Int == Int(pid),
                  let started = MacTab5Protocol.integer(row["startedAt"]) else { return false }
            // Claude's timestamp records when it registered, shortly after
            // process birth. Every later check pins the exact kernel birth.
            let birth = Double(seconds) * 1000 + Double(microseconds) / 1000
            return abs(Double(started) - birth) <= 15_000 && Self.inspect(pid) == self
        }
    }

    private init(root: URL, session: UUID, job: String, worker: ProcessIdentity, daemon: ProcessIdentity) {
        self.root = root
        self.session = session
        self.job = job
        self.worker = worker
        self.daemon = daemon
    }

    private static func safe(_ url: URL, kind: mode_t, denied: mode_t) -> Bool {
        var info = stat()
        return lstat(url.path, &info) == 0 && info.st_uid == getuid()
            && info.st_mode & S_IFMT == kind && info.st_mode & denied == 0
    }

    private static func read(_ url: URL, limit: Int, privateFile: Bool = false) throws -> Data {
        guard safe(url.deletingLastPathComponent(), kind: S_IFDIR, denied: 0o022) else {
            throw MacTab5Error.unconfirmed
        }
        let fd = Darwin.open(url.path, O_RDONLY | O_NOFOLLOW | O_CLOEXEC | O_NONBLOCK)
        guard fd >= 0 else { throw MacTab5Error.unavailable }
        let handle = FileHandle(fileDescriptor: fd, closeOnDealloc: true)
        defer { try? handle.close() }
        var info = stat()
        let denied: mode_t = privateFile ? 0o077 : 0o022
        guard fstat(fd, &info) == 0, info.st_uid == getuid(), info.st_mode & S_IFMT == S_IFREG,
              info.st_mode & denied == 0, info.st_size > 0, info.st_size <= limit,
              let data = try handle.read(upToCount: limit + 1), data.count == info.st_size else {
            throw MacTab5Error.unconfirmed
        }
        return data
    }

    private static func record(_ url: URL) throws -> [String: Any] {
        try MacTab5Protocol.object(read(url, limit: 65_536))
    }

    private static func shortID(_ value: String) -> Bool {
        value.utf8.count == 8 && value.utf8.allSatisfy { (48...57).contains($0) || (97...102).contains($0) }
    }

    private static func workers(root: URL) -> [[String: Any]]? {
        let sessions = root.appendingPathComponent("sessions", isDirectory: true)
        guard safe(root, kind: S_IFDIR, denied: 0o022), safe(sessions, kind: S_IFDIR, denied: 0o022),
              let files = try? FileManager.default.contentsOfDirectory(at: sessions, includingPropertiesForKeys: nil),
              files.count <= 2048 else { return nil }
        return files.compactMap { url in
            guard url.pathExtension == "json", let pid = Int(url.deletingPathExtension().lastPathComponent),
                  let row = try? record(url), row["pid"] as? Int == pid else { return nil }
            return row
        }
    }

    static func live(root: URL? = nil) -> Set<UUID> {
        Set((workers(root: root ?? defaultRoot) ?? []).compactMap { row in
            guard row["kind"] as? String == "bg", let id = row["sessionId"] as? String else { return nil }
            return UUID(uuidString: id)
        })
    }

    static func find(_ session: UUID, root: URL? = nil) -> MacClaudeBackground? {
        let root = root ?? defaultRoot
        let matches = workers(root: root)?.filter {
            $0["kind"] as? String == "bg" && UUID(uuidString: $0["sessionId"] as? String ?? "") == session
        }
        guard let matches, matches.count == 1, let row = matches.first, let job = row["jobId"] as? String, shortID(job),
              let executable = MacClaudeWorkspaceBackend.locate()?.resolvingSymlinksInPath().path,
              let worker = identity(row, executable: executable),
              let lock = try? record(root.appendingPathComponent("daemon.lock")),
              let daemon = identity(lock, executable: executable), worker.pid != daemon.pid else { return nil }
        let client = Self(root: root, session: session, job: job, worker: worker, daemon: daemon)
        guard (try? client.state()) != nil else { return nil }
        return client
    }

    private static func identity(_ row: [String: Any], executable: String) -> ProcessIdentity? {
        guard row["version"] as? String == version, let value = row["pid"] as? Int, let pid = pid_t(exactly: value),
              let process = ProcessIdentity.inspect(pid), process.executable == executable, process.matches(row) else { return nil }
        return process
    }

    func state() throws -> String {
        let row = try Self.record(root.appendingPathComponent("sessions/\(worker.pid).json"))
        let lock = try Self.record(root.appendingPathComponent("daemon.lock"))
        let jobs = root.appendingPathComponent("jobs", isDirectory: true)
        guard Self.safe(root, kind: S_IFDIR, denied: 0o022), Self.safe(jobs, kind: S_IFDIR, denied: 0o022) else {
            throw MacTab5Error.unconfirmed
        }
        let jobState = try Self.record(jobs.appendingPathComponent("\(job)/state.json"))
        guard row["kind"] as? String == "bg", row["entrypoint"] as? String == "cli",
              row["version"] as? String == Self.version, lock["version"] as? String == Self.version,
              row["jobId"] as? String == job, jobState["daemonShort"] as? String == job,
              jobState["backend"] as? String == "daemon",
              UUID(uuidString: row["sessionId"] as? String ?? "") == session,
              UUID(uuidString: jobState["sessionId"] as? String ?? "") == session,
              worker.matches(row), daemon.matches(lock) else { throw MacTab5Error.unconfirmed }
        return row["status"] as? String ?? "unknown"
    }

    func scope() throws -> String {
        _ = try state()
        let generation = [worker, daemon].map { "\($0.pid):\($0.seconds):\($0.microseconds)" }.joined(separator: "|")
        return MacClaudeNativeContent.hash(Data("claude-bg|\(session)|\(job)|\(generation)".utf8))
    }

    private var authURL: URL { root.appendingPathComponent("daemon/control.key") }

    private static func sockets() -> [URL] {
        guard safe(socketRoot, kind: S_IFDIR, denied: 0o077),
              let folders = try? FileManager.default.contentsOfDirectory(at: socketRoot, includingPropertiesForKeys: nil),
              folders.count <= 32 else { return [] }
        return folders.compactMap { folder in
            guard shortID(folder.lastPathComponent), safe(folder, kind: S_IFDIR, denied: 0o077) else { return nil }
            let socket = folder.appendingPathComponent("control.sock")
            return safe(socket, kind: S_IFSOCK, denied: 0o022) ? socket : nil
        }
    }

    private func ready(_ operation: String) throws -> Bool {
        guard operation == "send", try state() == "idle", let rows = Self.workers(root: root) else { return false }
        // The transcript must have a single live writer, even when another
        // native process has no recognized desktop host.
        return !rows.contains { row in
            guard UUID(uuidString: row["sessionId"] as? String ?? "") == session,
                  let value = row["pid"] as? Int, let pid = pid_t(exactly: value), pid != worker.pid,
                  let process = ProcessIdentity.inspect(pid) else { return false }
            return process.matches(row)
        }
    }

    func allows(_ operation: String, images: Bool) -> Bool {
        guard (try? ready(operation)) == true else { return false }
        return Self.safe(authURL, kind: S_IFREG, denied: 0o077) && !Self.sockets().isEmpty
    }

    func prepareSubmission(_ request: MacClaudeNativeRequest) throws -> () throws -> Bool {
        let literal = request.text.trimmingCharacters(in: .whitespacesAndNewlines)
        guard request.paths.count == request.imageDigests.count, request.paths.count <= MacTab5Photos.maxCount,
              !literal.isEmpty || !request.paths.isEmpty,
              !literal.hasPrefix("/"), !literal.hasPrefix("!"), !request.text.contains("\0"),
              request.text.utf8.count <= 2000 else {
            throw MacCodexDesktopError.rejected("native_input_unsupported")
        }
        guard request.authorized(), try request.current(), try ready(request.operation) else {
            throw MacCodexDesktopError.rejected("native_session_changed")
        }
        let references = request.paths.compactMap { MacClaudeBackgroundPhotos.reference($0.path) }
        guard references.count == request.paths.count else { throw MacTab5Error.invalidPacket }
        func imagesUnchanged() -> Bool {
            zip(request.paths, request.imageDigests).allSatisfy { path, digest in
                (try? Self.read(path, limit: MacTab5Photos.maxBytes, privateFile: true))
                    .map(MacTab5Protocol.sha256) == digest
            }
        }
        guard imagesUnchanged() else { throw MacCodexDesktopError.rejected("native_image_changed") }
        let deadline = ProcessInfo.processInfo.systemUptime + 1.5
        var peer: MacClaudeLocalSocket?
        for path in Self.sockets() {
            if let connection = try? MacClaudeLocalSocket(path: path, peerPID: daemon.pid, until: deadline) {
                peer = connection
                break
            }
        }
        guard let connection = peer, ProcessIdentity.inspect(daemon.pid) == daemon,
              let key = try? Self.read(authURL, limit: 512, privateFile: true),
              let auth = String(data: key, encoding: .utf8)?
                .trimmingCharacters(in: .whitespacesAndNewlines),
              (32...128).contains(auth.utf8.count),
              auth.utf8.allSatisfy({ (48...57).contains($0) || (65...90).contains($0) || (97...122).contains($0) || $0 == 45 || $0 == 95 }) else {
            throw MacCodexDesktopError.rejected("native_session_api_unavailable")
        }
        let text = (references + (request.text.isEmpty ? [] : [request.text])).joined(separator: " ")
        let frame = try MacTab5Protocol.json(["op": "reply", "proto": 1, "short": job, "text": text,
                                              "nextTurn": true, "auth": auth]) + Data([10])
        return {
            guard request.authorized(), (try? request.current()) == true,
                  (try? self.ready(request.operation)) == true, imagesUnchanged() else { return false }
            let until = ProcessInfo.processInfo.systemUptime + 2
            try connection.send(frame, until: until)
            let reply = try connection.reply(until: until)
            guard MacTab5Protocol.isBoolean(reply["ok"]), reply["ok"] as? Bool == true,
                  reply["op"] as? String == "reply" else { throw MacTab5Error.unconfirmed }
            return true
        }
    }
}

final class MacClaudeLocalSocket {
    private let fd: Int32

    private static func address(_ path: URL) throws -> sockaddr_un {
        var address = sockaddr_un()
        let bytes = Array(path.path.utf8) + [0]
        guard bytes.count <= MemoryLayout.size(ofValue: address.sun_path) else { throw MacTab5Error.unavailable }
        address.sun_len = UInt8(MemoryLayout<sockaddr_un>.size)
        address.sun_family = sa_family_t(AF_UNIX)
        withUnsafeMutableBytes(of: &address.sun_path) { $0.copyBytes(from: bytes) }
        return address
    }

    static func listen(at path: URL) throws -> Int32 {
        var address = try address(path)
        let fd = Darwin.socket(AF_UNIX, SOCK_STREAM, 0)
        guard fd >= 0 else { throw MacTab5Error.unavailable }
        let result = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(fd, $0, socklen_t(MemoryLayout<sockaddr_un>.size))
            }
        }
        guard result == 0, fcntl(fd, F_SETFD, FD_CLOEXEC) == 0, chmod(path.path, 0o600) == 0,
              Darwin.listen(fd, 4) == 0 else {
            Darwin.close(fd)
            throw MacTab5Error.unavailable
        }
        return fd
    }

    init(accepting listener: Int32) throws {
        let socket = Darwin.accept(listener, nil, nil)
        guard socket >= 0 else { throw MacTab5Error.unavailable }
        var accepted = false
        defer { if !accepted { Darwin.close(socket) } }
        var uid: uid_t = 0, gid: gid_t = 0, yes: Int32 = 1
        guard getpeereid(socket, &uid, &gid) == 0, uid == getuid(),
              fcntl(socket, F_SETFL, O_NONBLOCK) == 0, fcntl(socket, F_SETFD, FD_CLOEXEC) == 0,
              setsockopt(socket, SOL_SOCKET, SO_NOSIGPIPE, &yes, socklen_t(MemoryLayout<Int32>.size)) == 0 else {
            throw MacTab5Error.unconfirmed
        }
        fd = socket
        accepted = true
    }

    init(path: URL, peerPID: pid_t, until: TimeInterval) throws {
        var address = try Self.address(path)
        let socket = Darwin.socket(AF_UNIX, SOCK_STREAM, 0)
        guard socket >= 0 else { throw MacTab5Error.unavailable }
        fd = socket
        var connected = false
        defer { if !connected { Darwin.close(socket) } }
        var yes: Int32 = 1
        guard fcntl(fd, F_SETFL, O_NONBLOCK) == 0, fcntl(fd, F_SETFD, FD_CLOEXEC) == 0,
              setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &yes, socklen_t(MemoryLayout<Int32>.size)) == 0 else {
            throw MacTab5Error.unavailable
        }
        let result = withUnsafePointer(to: &address) {
            $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.connect(socket, $0, socklen_t(MemoryLayout<sockaddr_un>.size)) }
        }
        if result != 0 {
            guard errno == EINPROGRESS else { throw MacTab5Error.unavailable }
            try wait(POLLOUT, until: until)
            var error: Int32 = 0
            var size = socklen_t(MemoryLayout<Int32>.size)
            guard getsockopt(fd, SOL_SOCKET, SO_ERROR, &error, &size) == 0, error == 0 else { throw MacTab5Error.unavailable }
        }
        var uid: uid_t = 0, gid: gid_t = 0, pid: pid_t = 0
        var size = socklen_t(MemoryLayout<pid_t>.size)
        guard getpeereid(fd, &uid, &gid) == 0, uid == getuid(),
              getsockopt(fd, SOL_LOCAL, LOCAL_PEERPID, &pid, &size) == 0, pid == peerPID else {
            throw MacTab5Error.unconfirmed
        }
        connected = true
    }

    deinit { Darwin.close(fd) }

    private func wait(_ mask: Int32, until: TimeInterval) throws {
        while true {
            let remaining = until - ProcessInfo.processInfo.systemUptime
            guard remaining > 0 else { throw MacTab5Error.timeout }
            var item = pollfd(fd: fd, events: Int16(mask), revents: 0)
            let count = Darwin.poll(&item, 1, Int32(remaining * 1000 + 1))
            if count < 0 && errno == EINTR { continue }
            guard count > 0, item.revents & Int16(mask) != 0 else { throw MacTab5Error.unavailable }
            return
        }
    }

    func send(_ bytes: Data, until: TimeInterval) throws {
        var offset = 0
        while offset < bytes.count {
            try wait(POLLOUT, until: until)
            let count = bytes.withUnsafeBytes { Darwin.send(fd, $0.baseAddress!.advanced(by: offset), bytes.count - offset, 0) }
            if count < 0 && [EINTR, EAGAIN].contains(errno) { continue }
            guard count > 0 else { throw MacTab5Error.unavailable }
            offset += count
        }
    }

    func reply(until: TimeInterval) throws -> [String: Any] {
        var line = Data()
        var buffer = [UInt8](repeating: 0, count: 1024)
        while line.count < 4096 {
            try wait(POLLIN, until: until)
            let capacity = min(buffer.count, 4096 - line.count)
            let count = Darwin.recv(fd, &buffer, capacity, 0)
            if count < 0 && [EINTR, EAGAIN].contains(errno) { continue }
            guard count > 0 else { throw MacTab5Error.unavailable }
            let part = buffer.prefix(count)
            if let end = part.firstIndex(of: 10) {
                line.append(contentsOf: part[..<end])
                return try MacTab5Protocol.object(line)
            }
            line.append(contentsOf: part)
        }
        throw MacTab5Error.invalidPacket
    }
}
