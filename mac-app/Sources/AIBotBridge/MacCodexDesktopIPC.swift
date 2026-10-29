import AppKit
import Darwin
import Foundation

enum MacCodexDesktopError: Error {
    case unavailable, incompatible, invalidPacket, timeout, deliveryUnknown
    case rejected(String)
}

/// A short-lived connection to the already running desktop, never a daemon.
/// Versioned desktop IPC is optional: public read-only history remains available
/// when this endpoint, task owner or supported snapshot version is absent.
final class MacCodexDesktopIPC {
    // Installed v1 steer chooses the owner's current turn internally and does not
    // accept the caller's expected turn. A local preflight cannot close that race.
    static let supportsExpectedTurnSteer = false
    struct State {
        let task: String
        let runtime: String
        let waiting: Bool
        let turn: String
        let status: String
        let cwd: String
        let publicItems: [[String: Any]]
        let startedAt: Int64
        var canSend: Bool { runtime == "idle" && !waiting && ["completed", "failed", "interrupted"].contains(status) }
        var running: Bool { runtime == "active" && status == "inProgress" }
        var taskState: String { waiting ? "waiting" : running ? "working" : canSend ? "idle" : "unknown" }
        var thread: [String: Any] {
            ["id": task, "status": ["type": runtime, "activeFlags": waiting ? ["waitingOnUserInput"] : []],
             "turns": [["id": turn, "status": status, "startedAt": startedAt, "items": publicItems]]]
        }
    }
    struct Command {
        let method: String
        let version: Int
        let params: [String: Any]
    }
    protocol Wire: AnyObject {
        func write(_ object: [String: Any], until: TimeInterval) throws
        func read(until: TimeInterval) throws -> [String: Any]
        func close()
    }
    typealias Connect = () throws -> Wire
    private let connect: Connect
    private let lock = NSLock()
    private var verifiedAt: TimeInterval = -.infinity
    private let clock: () -> TimeInterval
    private let queueReceipt: (String, String) -> MacCodexQueueReceipt?
    private let serverQueueReceipt: (String, String) -> MacCodexQueueReceipt?
    init(clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }, connect: Connect? = nil,
         queueReceipt: ((String, String) -> MacCodexQueueReceipt?)? = nil,
         serverQueueReceipt: ((String, String) -> MacCodexQueueReceipt?)? = nil) {
        self.clock = clock; self.connect = connect ?? { try Socket() }
        // Synthetic wires must not read the user's real desktop queue.
        self.queueReceipt = queueReceipt ?? (connect == nil ? { MacCodexQueueReceipt.local(task: $0, expected: $1) } : { _, _ in nil })
        let reader = connect == nil ? MacTab5CodexRPC() : nil
        self.serverQueueReceipt = serverQueueReceipt ?? { task, expected in reader?.queueReceipt(task: task, expected: expected) }
    }
    var available: Bool {
        lock.lock(); defer { lock.unlock() }
        return clock() - verifiedAt < 15
    }
    func reset() { lock.lock(); verifiedAt = -.infinity; lock.unlock() }
    func read(_ task: String) throws -> State {
        let session = try Session(wire: connect(), task: task)
        defer { session.close() }
        let state = try session.snapshot()
        lock.lock(); verifiedAt = clock(); lock.unlock()
        return state
    }
    func submissionReceipt(_ task: String, expected: String) throws -> MacCodexSubmissionReceipt {
        let session = try Session(wire: connect(), task: task, deadline: ProcessInfo.processInfo.systemUptime + 1.5)
        defer { session.close() }
        var receipt = try MacCodexSubmissionReceipt.parse(session.rawSnapshot(), task: task, owner: session.owner, expected: expected)
        receipt.queue = queueReceipt(task, expected)
        receipt.serverQueue = serverQueueReceipt(task, expected)
        return receipt
    }
    func execute(task: String, operation: String, expected: String, request: String,
                 inputs: [[String: Any]], authorized: () -> Bool = { true }) throws -> [String: Any] {
        let session = try Session(wire: connect(), task: task)
        defer { session.close() }
        let state = try session.snapshot()
        let command = try Self.command(state: state, operation: operation, expected: expected,
                                       request: request, inputs: inputs)
        guard authorized() else { throw MacCodexDesktopError.rejected("codex_send_not_enabled") }
        // Nothing below this point can prove non-delivery merely from a timeout
        // or handler exception. The caller already persisted an uncertain receipt.
        do {
            let reply = try session.request(command.method, version: command.version,
                                            params: command.params, target: session.owner, seconds: 20)
            if reply["resultType"] as? String != "success" {
                switch reply["error"] as? String {
                case "no-client-found", "no-handler-for-request":
                    throw MacCodexDesktopError.rejected("desktop_owner_unavailable")
                case "request-version-mismatch":
                    throw MacCodexDesktopError.rejected("desktop_version_unsupported")
                default: throw MacCodexDesktopError.deliveryUnknown
                }
            }
            guard reply["handledByClientId"] as? String == session.owner,
                  reply["method"] as? String == command.method,
                  let result = reply["result"] as? [String: Any] else { throw MacCodexDesktopError.deliveryUnknown }
            let nested = result["result"] as? [String: Any] ?? [:]
            let turn = operation == "interrupt" ? result["interruptedTurnId"] :
                operation == "steer" ? nested["turnId"] : (nested["turn"] as? [String: Any])?["id"]
            guard let turn = MacTab5Codex.id(turn), operation != "interrupt" || turn == expected else {
                throw MacCodexDesktopError.deliveryUnknown
            }
            return ["turnId": turn]
        } catch let error as MacCodexDesktopError {
            if case .rejected = error { throw error }
            throw MacCodexDesktopError.deliveryUnknown
        } catch { throw MacCodexDesktopError.deliveryUnknown }
    }

    static func parse(_ object: [String: Any], task: String) throws -> State {
        guard MacTab5Codex.id(object["id"]) == task,
              let requests = object["requests"] as? [Any],
              let runtime = (object["threadRuntimeStatus"] as? [String: Any])?["type"] as? String,
              ["idle", "active", "systemError"].contains(runtime),
              let cwd = object["cwd"] as? String, cwd.hasPrefix("/"), !cwd.contains("\0") else {
            throw MacCodexDesktopError.incompatible
        }
        var turns: [String: [String: Any]] = [:]
        let history = (object["turnHistory"] as? [String: Any])?["history"] as? [String: Any]
        let canonical = history?["entitiesByKey"] as? [String: [String: Any]] ?? [:]
        for turn in (object["turns"] as? [[String: Any]] ?? []) + Array(canonical.values) {
            guard let id = MacTab5Codex.id(turn["turnId"]) else { continue }
            turns[id] = turn
        }
        // Do not guess which turn is current if timestamps or status are missing.
        guard !turns.isEmpty, turns.count <= 10000,
              turns.values.allSatisfy({ (MacTab5Protocol.integer($0["turnStartedAtMs"]) ?? 0) > 0 }),
              let last = turns.values.max(by: {
                  ($0["turnStartedAtMs"] as! NSNumber).doubleValue < ($1["turnStartedAtMs"] as! NSNumber).doubleValue
              }), let turn = MacTab5Codex.id(last["turnId"]),
              let status = last["status"] as? String,
              ["completed", "failed", "interrupted", "inProgress"].contains(status) else {
            throw MacCodexDesktopError.incompatible
        }
        guard turns.values.filter({
            MacTab5Protocol.integer($0["turnStartedAtMs"]) == MacTab5Protocol.integer(last["turnStartedAtMs"])
        }).count == 1 else { throw MacCodexDesktopError.incompatible }
        // Retain only published assistant text from the owner snapshot. Never
        // carry requests, reasoning, user input or tool payloads into history.
        let items = MacTab5Codex.publicItems(last["items"], requirePhase: true)
        guard items.reduce(0, { $0 + (($1["text"] as? String)?.utf8.count ?? 0) }) <= 1024 * 1024 else {
            throw MacCodexDesktopError.incompatible
        }
        return State(task: task, runtime: runtime, waiting: !requests.isEmpty, turn: turn, status: status, cwd: cwd,
                     publicItems: items, startedAt: (MacTab5Protocol.integer(last["turnStartedAtMs"]) ?? 0) / 1000)
    }

    static func command(state: State, operation: String, expected: String, request: String,
                        inputs: [[String: Any]]) throws -> Command {
        guard MacTab5Codex.id(request) != nil else { throw MacCodexDesktopError.rejected("invalid_request") }
        guard ["send", "steer", "interrupt"].contains(operation) else { throw MacCodexDesktopError.rejected("invalid_operation") }
        guard operation != "steer" else {
            throw MacCodexDesktopError.rejected("desktop_steer_requires_expected_turn_support")
        }
        if operation == "send" {
            guard state.canSend else { throw MacCodexDesktopError.rejected("task_busy_or_state_unknown") }
        } else {
            guard state.running, state.turn == expected else { throw MacCodexDesktopError.rejected("turn_changed") }
            if operation == "steer", state.waiting { throw MacCodexDesktopError.rejected("task_waiting_for_input") }
        }
        var normalized: [[String: Any]] = [], text = ""
        guard inputs.count <= 4 else { throw MacCodexDesktopError.rejected("invalid_request") }
        for input in inputs {
            if input["type"] as? String == "text", let value = input["text"] as? String {
                text += value
                normalized.append(["type": "text", "text": value, "text_elements": [Any]()])
            } else if input["type"] as? String == "image", let url = input["url"] as? String,
                      url.utf8.count <= 3 * 1024 * 1024, let comma = url.firstIndex(of: ","),
                      ["data:image/png;base64", "data:image/jpeg;base64"].contains(String(url[..<comma])),
                      let bytes = Data(base64Encoded: String(url[url.index(after: comma)...])),
                      let mime = try? MacTab5Photos.imageType(bytes), url.hasPrefix("data:\(mime);base64,") {
                // Device-bound images remain inline; no plaintext temporary file
                // or user-supplied local path is handed to the desktop.
                normalized.append(["type": "image", "url": url])
            } else { throw MacCodexDesktopError.rejected("invalid_request") }
        }
        guard text.utf8.count <= 2000, operation == "interrupt" || !normalized.isEmpty else {
            throw MacCodexDesktopError.rejected("invalid_request")
        }
        if operation == "send" {
            return Command(method: "thread-follower-start-turn", version: 2,
                           params: ["conversationId": state.task, "turnStart": [
                            "request": ["threadId": state.task, "input": normalized, "clientUserMessageId": request],
                            "context": ["inheritThreadSettings": true]]])
        }
        return Command(method: "thread-follower-interrupt-turn", version: 4,
                       params: ["conversationId": state.task, "mode": "user-stop", "expectedTurnId": expected])
    }

    private final class Session {
        let wire: Wire, task: String
        var client = "initializing-client", owner = ""
        private var following = false
        private let deadline: TimeInterval
        init(wire: Wire, task: String, deadline: TimeInterval = .infinity) throws {
            self.wire = wire; self.task = task; self.deadline = deadline
            do {
                guard MacTab5Codex.id(task) != nil else { throw MacCodexDesktopError.invalidPacket }
                let response = try request("initialize", version: 0, params: ["clientType": "tab5-bridge"])
                guard response["resultType"] as? String == "success",
                      let id = MacTab5Codex.id((response["result"] as? [String: Any])?["clientId"]) else {
                    throw MacCodexDesktopError.incompatible
                }
                client = id
                let found = try request("thread-owner-discovery", version: 1,
                                        params: ["conversationId": task, "hostId": "local"])
                guard found["resultType"] as? String == "success",
                      let id = MacTab5Codex.id(found["handledByClientId"]),
                      (found["result"] as? [String: Any])?["supportsUntrustedAppInput"] as? Bool == true else {
                    throw MacCodexDesktopError.rejected("desktop_owner_unavailable")
                }
                owner = id
            } catch { wire.close(); throw error }
        }
        func request(_ method: String, version: Int, params: [String: Any], target: String? = nil,
                     seconds: Double = 2) throws -> [String: Any] {
            let id = UUID().uuidString.lowercased(), until = min(deadline, ProcessInfo.processInfo.systemUptime + seconds)
            try wire.write(["type": "request", "requestId": id, "sourceClientId": client, "version": version,
                            "method": method, "params": params, "targetClientId": target as Any? ?? NSNull(),
                            "timeoutMs": Int(seconds * 1000)], until: until)
            for _ in 0..<256 {
                let value = try receive(until)
                if value["type"] as? String == "response", value["requestId"] as? String == id { return value }
            }
            throw MacCodexDesktopError.invalidPacket
        }
        func snapshot() throws -> State { try MacCodexDesktopIPC.parse(rawSnapshot(), task: task) }
        func rawSnapshot() throws -> [String: Any] {
            let until = min(deadline, ProcessInfo.processInfo.systemUptime + 3)
            following = true
            try follow(true, until: until)
            for _ in 0..<256 {
                let value = try receive(until)
                guard value["method"] as? String == "thread-stream-state-changed",
                      value["sourceClientId"] as? String == owner,
                      let params = value["params"] as? [String: Any], params["conversationId"] as? String == task,
                      params["hostId"] as? String == "local", let change = params["change"] as? [String: Any],
                      change["type"] as? String == "snapshot" else { continue }
                guard MacTab5Protocol.integer(value["version"]) == 11,
                      let state = change["conversationState"] as? [String: Any] else { throw MacCodexDesktopError.incompatible }
                return state
            }
            throw MacCodexDesktopError.invalidPacket
        }
        private func receive(_ until: TimeInterval) throws -> [String: Any] {
            for _ in 0..<256 {
                let value = try wire.read(until: until)
                if value["type"] as? String == "client-discovery-request" {
                    guard let id = value["requestId"] as? String, id.utf8.count <= 80 else { throw MacCodexDesktopError.invalidPacket }
                    try wire.write(["type": "client-discovery-response", "requestId": id,
                                    "response": ["canHandle": false]], until: until)
                } else { return value }
            }
            throw MacCodexDesktopError.invalidPacket
        }
        private func follow(_ enabled: Bool, until: TimeInterval) throws {
            try wire.write(["type": "broadcast", "sourceClientId": client, "version": 1,
                            "method": "thread-stream-following-changed", "targetClientIds": [owner],
                            "params": ["conversationId": task, "hostId": "local", "following": enabled]], until: until)
        }
        func close() {
            if following { try? follow(false, until: ProcessInfo.processInfo.systemUptime + 0.1); following = false }
            wire.close()
        }
    }

    final class Socket: Wire {
        private var fd: Int32 = -1
        init() throws {
            let home = ProcessInfo.processInfo.environment["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ??
                FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
            let folder = home.appendingPathComponent("ipc"), path = folder.appendingPathComponent("ipc.sock").path
            for (url, kind) in [(folder.path, mode_t(S_IFDIR)), (path, mode_t(S_IFSOCK))] {
                var info = stat()
                guard lstat(url, &info) == 0, info.st_uid == getuid(),
                      info.st_mode & S_IFMT == kind, info.st_mode & 0o077 == 0 else { throw MacCodexDesktopError.unavailable }
            }
            var address = sockaddr_un()
            let bytes = Array(path.utf8) + [0]
            guard bytes.count <= MemoryLayout.size(ofValue: address.sun_path) else { throw MacCodexDesktopError.unavailable }
            address.sun_len = UInt8(MemoryLayout<sockaddr_un>.size); address.sun_family = sa_family_t(AF_UNIX)
            withUnsafeMutableBytes(of: &address.sun_path) { $0.copyBytes(from: bytes) }
            fd = Darwin.socket(AF_UNIX, SOCK_STREAM, 0)
            guard fd >= 0 else { throw MacCodexDesktopError.unavailable }
            do {
                guard fcntl(fd, F_SETFL, O_NONBLOCK) == 0, fcntl(fd, F_SETFD, FD_CLOEXEC) == 0 else { throw MacCodexDesktopError.unavailable }
                var yes: Int32 = 1
                guard setsockopt(fd, SOL_SOCKET, SO_NOSIGPIPE, &yes, socklen_t(MemoryLayout.size(ofValue: yes))) == 0 else { throw MacCodexDesktopError.unavailable }
                let result = withUnsafePointer(to: &address) {
                    $0.withMemoryRebound(to: sockaddr.self, capacity: 1) { Darwin.connect(fd, $0, socklen_t(MemoryLayout<sockaddr_un>.size)) }
                }
                if result != 0 {
                    guard errno == EINPROGRESS else { throw MacCodexDesktopError.unavailable }
                    try wait(POLLOUT, until: ProcessInfo.processInfo.systemUptime + 1)
                    var error: Int32 = 0, size = socklen_t(MemoryLayout<Int32>.size)
                    guard getsockopt(fd, SOL_SOCKET, SO_ERROR, &error, &size) == 0, error == 0 else { throw MacCodexDesktopError.unavailable }
                }
                var uid: uid_t = 0, gid: gid_t = 0, pid: pid_t = 0
                var size = socklen_t(MemoryLayout<pid_t>.size)
                guard getpeereid(fd, &uid, &gid) == 0, uid == getuid(),
                      getsockopt(fd, SOL_LOCAL, LOCAL_PEERPID, &pid, &size) == 0,
                      NSRunningApplication(processIdentifier: pid)?.bundleIdentifier == "com.openai.codex" else {
                    throw MacCodexDesktopError.unavailable
                }
            } catch { close(); throw error }
        }
        deinit { close() }
        func close() { if fd >= 0 { Darwin.close(fd); fd = -1 } }
        private func wait(_ mask: Int32, until: TimeInterval) throws {
            let events = Int16(mask)
            while true {
                let remaining = until - ProcessInfo.processInfo.systemUptime
                guard fd >= 0, remaining > 0 else { throw MacCodexDesktopError.timeout }
                var item = pollfd(fd: fd, events: events, revents: 0)
                let result = poll(&item, 1, Int32(min(remaining * 1000 + 1, 20000)))
                if result < 0, errno == EINTR { continue }
                guard result > 0 else { throw MacCodexDesktopError.timeout }
                guard item.revents & events != 0 else { throw MacCodexDesktopError.unavailable }
                return
            }
        }
        func write(_ object: [String: Any], until: TimeInterval) throws {
            let json = try MacTab5Protocol.json(object)
            guard json.count <= 32 * 1024 * 1024 else { throw MacCodexDesktopError.invalidPacket }
            var length = UInt32(json.count).littleEndian
            var bytes = withUnsafeBytes(of: &length) { Data($0) }; bytes.append(json)
            var offset = 0
            while offset < bytes.count {
                try wait(POLLOUT, until: until)
                let count = bytes.withUnsafeBytes { Darwin.send(fd, $0.baseAddress!.advanced(by: offset), bytes.count - offset, 0) }
                if count < 0, [EINTR, EAGAIN].contains(errno) { continue }
                guard count > 0 else { throw MacCodexDesktopError.unavailable }; offset += count
            }
        }
        private func bytes(_ count: Int, until: TimeInterval) throws -> Data {
            var value = Data(count: count), offset = 0
            while offset < count {
                try wait(POLLIN, until: until)
                let n = value.withUnsafeMutableBytes { Darwin.recv(fd, $0.baseAddress!.advanced(by: offset), count - offset, 0) }
                if n < 0, [EINTR, EAGAIN].contains(errno) { continue }
                guard n > 0 else { throw MacCodexDesktopError.unavailable }; offset += n
            }
            return value
        }
        func read(until: TimeInterval) throws -> [String: Any] {
            let header = [UInt8](try bytes(4, until: until))
            let size = Int(header[0]) | Int(header[1]) << 8 | Int(header[2]) << 16 | Int(header[3]) << 24
            guard (1...32 * 1024 * 1024).contains(size) else { throw MacCodexDesktopError.invalidPacket }
            return try MacTab5Protocol.object(bytes(size, until: until))
        }
    }
}
