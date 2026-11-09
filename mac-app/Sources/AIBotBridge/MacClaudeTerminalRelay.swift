import Darwin
import Foundation
import SwiftTerm

// The visible terminal continues to render the original bytes. This shadow
// terminal is in memory only and is used solely to verify the Claude composer.
private final class MacClaudeTerminalShadow: TerminalDelegate {
    // SwiftTerm 1.10 interprets the Kitty keyboard capability query as a
    // cursor restore. The user's actual terminal still receives every byte.
    private let keyboardQuery: [UInt8] = [27, 91, 63, 117]
    private var pending = 0

    func feed(_ bytes: ArraySlice<UInt8>, into terminal: Terminal) {
        var interpreted: [UInt8] = []
        interpreted.reserveCapacity(bytes.count)
        for byte in bytes {
            if byte == keyboardQuery[pending] {
                pending += 1
                if pending == keyboardQuery.count { pending = 0 }
                continue
            }
            interpreted.append(contentsOf: keyboardQuery.prefix(pending))
            pending = byte == keyboardQuery[0] ? 1 : 0
            if pending == 0 { interpreted.append(byte) }
        }
        terminal.feed(byteArray: interpreted)
    }

    func send(source: Terminal, data: ArraySlice<UInt8>) {}
    func isProcessTrusted(source: Terminal) -> Bool { false }
}

enum MacClaudeTerminalRelay {
    static func run(_ arguments: [String]) throws -> Int32 {
        let session: UUID, option: String
        if arguments.isEmpty {
            session = UUID()
            option = "--session-id"
        } else {
            guard arguments.count == 2, arguments[0] == "--resume", let id = UUID(uuidString: arguments[1]) else {
                throw MacTab5Error.invalidPacket
            }
            session = id
            option = "--resume"
        }
        guard isatty(STDIN_FILENO) == 1, isatty(STDOUT_FILENO) == 1,
              let cli = MacClaudeWorkspaceBackend.locate()?.resolvingSymlinksInPath() else {
            throw MacTab5Error.unavailable
        }
        var size = winsize()
        var saved = termios()
        guard ioctl(STDIN_FILENO, TIOCGWINSZ, &size) == 0, size.ws_col > 0, size.ws_row > 0,
              tcgetattr(STDIN_FILENO, &saved) == 0 else { throw MacTab5Error.unavailable }
        let folder = try MacClaudeTerminalRegistration.createDirectory()
        defer { try? FileManager.default.removeItem(at: folder) }
        let listener = try MacClaudeLocalSocket.listen(at: folder.appendingPathComponent("control.sock"))
        defer { Darwin.close(listener) }
        let environment = ProcessInfo.processInfo.environment
        let config = environment["CLAUDE_CONFIG_DIR"].map { URL(fileURLWithPath: $0, isDirectory: true) }
            ?? MacClaudeTerminalRegistration.defaultConfig
        let argv = [cli.path, option, session.uuidString.lowercased()]
        let envp = environment.map { "\($0.key)=\($0.value)" }
        guard let child = PseudoTerminalHelpers.fork(andExec: cli.path, args: argv, env: envp, desiredWindowSize: &size) else {
            throw MacTab5Error.unavailable
        }
        let master = child.masterFd
        defer { Darwin.close(master) }
        var raw = saved
        cfmakeraw(&raw)
        guard tcsetattr(STDIN_FILENO, TCSANOW, &raw) == 0 else { throw MacTab5Error.unavailable }
        defer { _ = tcsetattr(STDIN_FILENO, TCSANOW, &saved) }
        guard fcntl(master, F_SETFL, O_NONBLOCK) == 0,
              fcntl(master, F_SETFD, FD_CLOEXEC) == 0 else { throw MacTab5Error.unavailable }
        let record = MacClaudeTerminalRegistration(session: session, launcher: getpid(), child: child.pid,
                                                   launchedAt: Int64(Date().timeIntervalSince1970 * 1000), newSession: option == "--session-id")
        let registration = folder.appendingPathComponent("session.json")
        try JSONEncoder().encode(record).write(to: registration, options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: registration.path)
        let shadow = MacClaudeTerminalShadow()
        let terminal = Terminal(delegate: shadow, options: .init(cols: min(400, Int(size.ws_col)),
                                                                  rows: min(160, Int(size.ws_row)), scrollback: 0))
        terminal.silentLog = true
        let status = try relay(master: master, listener: listener, size: &size, terminal: terminal, shadow: shadow,
                               config: config, executable: cli.path, registration: record)
        withExtendedLifetime(shadow) {}
        return status
    }

    private static func relay(master: Int32, listener: Int32, size: inout winsize, terminal: Terminal, shadow: MacClaudeTerminalShadow,
                              config: URL, executable: String, registration: MacClaudeTerminalRegistration) throws -> Int32 {
        var manualAt = ProcessInfo.processInfo.systemUptime
        let capacity = 16_384
        var buffer = [UInt8](repeating: 0, count: capacity)
        var descriptors = [
            pollfd(fd: master, events: Int16(POLLIN), revents: 0),
            pollfd(fd: STDIN_FILENO, events: Int16(POLLIN), revents: 0),
            pollfd(fd: listener, events: Int16(POLLIN), revents: 0)
        ]
        func updateOutput() throws -> Bool {
            let count = Darwin.read(master, &buffer, capacity)
            if count < 0 && (errno == EAGAIN || errno == EINTR) { return true }
            guard count > 0 else { return false }
            let bytes = buffer.prefix(count)
            shadow.feed(bytes, into: terminal)
            try write(Data(bytes), to: STDOUT_FILENO)
            return true
        }
        while true {
            var updated = winsize()
            if ioctl(STDIN_FILENO, TIOCGWINSZ, &updated) == 0, updated.ws_col > 0, updated.ws_row > 0,
               updated.ws_col != size.ws_col || updated.ws_row != size.ws_row {
                size = updated
                _ = PseudoTerminalHelpers.setWinSize(masterPtyDescriptor: master, windowSize: &updated)
                terminal.resize(cols: min(400, Int(size.ws_col)), rows: min(160, Int(size.ws_row)))
                manualAt = ProcessInfo.processInfo.systemUptime
            }
            let count = Darwin.poll(&descriptors, nfds_t(descriptors.count), 50)
            if count < 0 {
                if errno == EINTR { continue }
                throw MacTab5Error.unavailable
            }
            if descriptors[0].revents & Int16(POLLIN) != 0, try !updateOutput() { break }
            if descriptors[1].revents & Int16(POLLIN) != 0 {
                let count = Darwin.read(STDIN_FILENO, &buffer, capacity)
                if count == 0 { break }
                if count > 0 {
                    try write(Data(buffer.prefix(count)), to: master)
                    manualAt = ProcessInfo.processInfo.systemUptime
                }
            }
            if descriptors[0].revents & Int16(POLLHUP | POLLERR) != 0 ||
                descriptors[1].revents & Int16(POLLHUP | POLLERR) != 0 { break }
            if descriptors[2].revents & Int16(POLLIN) != 0, let peer = try? MacClaudeLocalSocket(accepting: listener) {
                let deadline = ProcessInfo.processInfo.systemUptime + 0.5
                if let request = try? peer.reply(until: deadline) {
                    var pending = pollfd(fd: STDIN_FILENO, events: Int16(POLLIN), revents: 0)
                    let quiet = Darwin.poll(&pending, 1, 0) == 0
                        && ProcessInfo.processInfo.systemUptime - manualAt >= 0.18
                    var output = pollfd(fd: master, events: Int16(POLLIN), revents: 0)
                    let settled = Darwin.poll(&output, 1, 0) == 0
                    do {
                        let accepted = try quiet && settled && composer(terminal)?.isEmpty == true
                            && handle(request, config: config, executable: executable, registration: registration) { text, key, stillCurrent in
                                if let text {
                                    let paste = Data(("\u{1b}[200~" + text + "\u{1b}[201~").utf8)
                                    try write(paste, to: master)
                                    let until = ProcessInfo.processInfo.systemUptime + 1.4
                                    let expected = text.filter { !$0.isWhitespace }
                                    var matched = false
                                    while ProcessInfo.processInfo.systemUptime < until {
                                        var output = pollfd(fd: master, events: Int16(POLLIN), revents: 0)
                                        let available = Darwin.poll(&output, 1, 60)
                                        if available > 0 {
                                            guard try updateOutput() else { return false }
                                        } else if available == 0, composer(terminal)?.filter({ !$0.isWhitespace }) == expected {
                                            matched = true
                                            break
                                        }
                                    }
                                    guard matched else { return false }
                                }
                                var input = pollfd(fd: STDIN_FILENO, events: Int16(POLLIN), revents: 0)
                                guard Darwin.poll(&input, 1, 0) == 0, stillCurrent() else { return false }
                                try write(key, to: master)
                                return true
                            }
                        var stopped = false
                        if accepted, request["op"] as? String == "interrupt", request["probe"] as? Bool != true {
                            let until = ProcessInfo.processInfo.systemUptime + 1.1
                            while ProcessInfo.processInfo.systemUptime < until {
                                var output = pollfd(fd: master, events: Int16(POLLIN), revents: 0)
                                if Darwin.poll(&output, 1, 50) > 0 {
                                    if try !updateOutput() { break }
                                } else if Self.confirmedStop(request, config: config, executable: executable,
                                                            registration: registration, draft: composer(terminal)) {
                                    stopped = true
                                    break
                                }
                            }
                        }
                        let response = try MacTab5Protocol.json(["accepted": accepted, "stopped": stopped]) + Data([10])
                        try peer.send(response, until: ProcessInfo.processInfo.systemUptime + 0.5)
                    } catch {
                        // A partial PTY write has unknown delivery. Close
                        // without a negative acknowledgement or retry.
                    }
                }
            }
        }
        var status: Int32 = 0
        if waitpid(registration.child, &status, WNOHANG) == registration.child, status & 0x7F == 0 {
            return (status >> 8) & 0xFF
        }
        return 0
    }

    private static func handle(_ request: [String: Any], config: URL, executable: String,
                               registration: MacClaudeTerminalRegistration,
                               submit: (String?, Data, () -> Bool) throws -> Bool) throws -> Bool {
        guard UUID(uuidString: request["session"] as? String ?? "") == registration.session,
              let operation = request["op"] as? String, ["send", "steer", "interrupt"].contains(operation),
              let (worker, _) = registration.findWorker(config: config, executable: executable),
              request["worker"] as? Int == Int(worker.pid),
              !FileManager.default.fileExists(atPath: config.appendingPathComponent("keybindings.json").path) else { return false }
        func stillCurrent() -> Bool {
            guard let (active, state) = registration.findWorker(config: config, executable: executable), active == worker else { return false }
            if operation == "send" { return state == "idle" }
            guard ["working", "busy"].contains(state),
                  let user = (request["expectedUser"] as? String).flatMap(UUID.init(uuidString:))?.uuidString.lowercased()
            else { return false }
            let key = Data(repeating: 0, count: 32)
            let alias = MacClaudeCatalog.alias("claude:turn:" + registration.session.uuidString.lowercased() + ":" + user, key: key)
            guard let history = try? MacClaudeHistory.read(root: config.appendingPathComponent("projects"), session: registration.session, key: key),
                  !history.limited, history.accepted.contains(user), history.turns.last?["id"] as? String == alias else { return false }
            return history.turns.last?["status"] as? String == "unknown"
        }
        guard stillCurrent(), let text = request["text"] as? String, let images = request["images"] as? [String],
              let digests = request["digests"] as? [String] else { return false }
        let isProbe = request["probe"] as? Bool == true
        if operation == "interrupt" {
            guard text.isEmpty, images.isEmpty, digests.isEmpty else { return false }
            return try isProbe || submit(nil, Data([3]), stillCurrent)
        }
        guard let input = try? MacClaudeBackgroundPhotos.input(text: text, paths: images, digests: digests) else { return false }
        let key = Data([24, operation == "send" ? 19 : 13])
        return try isProbe || submit(input, key, stillCurrent)
    }

    private static func confirmedStop(_ request: [String: Any], config: URL, executable: String,
                                      registration: MacClaudeTerminalRegistration, draft: String?) -> Bool {
        guard let (worker, state) = registration.findWorker(config: config, executable: executable),
              state == "idle", request["worker"] as? Int == Int(worker.pid),
              let user = (request["expectedUser"] as? String).flatMap(UUID.init(uuidString:))?.uuidString.lowercased()
        else { return false }
        let key = Data(repeating: 0, count: 32)
        guard let history = try? MacClaudeHistory.read(root: config.appendingPathComponent("projects"),
                                                       session: registration.session, key: key),
              !history.limited, history.accepted.contains(user) else { return false }
        let alias = MacClaudeCatalog.alias("claude:turn:" + registration.session.uuidString.lowercased() + ":" + user, key: key)
        guard history.turns.last?["id"] as? String == alias else { return false }
        let status = history.turns.last?["status"] as? String
        if status == "interrupted" { return true }
        // Before the first response, Claude may restore the exact interrupted
        // prompt to the composer without adding an interruption notice.
        guard status == "unknown", let draft, !draft.isEmpty else { return false }
        return history.inputDigests[user] == MacClaudeNativeContent.digest(draft)
    }

    static func composer(_ terminal: Terminal) -> String? {
        let cursor = terminal.getCursorLocation()
        guard (40...400).contains(terminal.cols), (12...160).contains(terminal.rows),
              cursor.y > 0, cursor.y + 1 < terminal.rows else { return nil }
        func row(_ index: Int) -> String {
            String((0..<terminal.cols).map { column in
                let character = terminal.getCharacter(col: column, row: index) ?? " "
                return character == "\0" ? " " : character
            })
        }
        func divider(_ row: String) -> Bool {
            let value = row.trimmingCharacters(in: .whitespaces)
            return value.count >= 20 && value.hasPrefix(String(repeating: "─", count: 16))
                && value.hasSuffix("─") && value.filter { $0 == "─" }.count * 5 >= value.count * 3
        }
        let first = stride(from: cursor.y, through: max(1, cursor.y - 20), by: -1).first { index in
            let line = row(index)
            return (line.hasPrefix("❯ ") || line.hasPrefix("❯\u{a0}") || line.hasPrefix("> "))
                && divider(row(index - 1))
        }
        guard let first, let end = ((cursor.y + 1)..<terminal.rows).first(where: { divider(row($0)) }) else { return nil }
        var content = row(first).dropFirst(2).trimmingCharacters(in: .whitespaces)
        for index in (first + 1)..<end {
            let next = row(index)
            guard next.hasPrefix("  ") else { return nil }
            content += next.dropFirst(2).trimmingCharacters(in: .whitespaces)
        }
        let atEmptyPrompt = first == cursor.y && cursor.x == 2 && end == first + 1
        if content.isEmpty { return atEmptyPrompt ? "" : nil }
        if atEmptyPrompt {
            // Accept renderer evidence only. The same words in an editable
            // draft, even with the caret moved home, must remain protected.
            let visible = (2..<terminal.cols).filter { column in
                let ch = terminal.getCharacter(col: column, row: first) ?? " "
                return ch != "\0" && !ch.isWhitespace
            }
            if !visible.isEmpty, visible.allSatisfy({
                terminal.getCharData(col: $0, row: first)?.attribute.style.contains(.dim) == true
            }) { return "" }
        }
        return content
    }

    private static func write(_ data: Data, to fd: Int32) throws {
        var offset = 0
        let deadline = ProcessInfo.processInfo.systemUptime + 1
        while offset < data.count {
            var ready = pollfd(fd: fd, events: Int16(POLLOUT), revents: 0)
            let time = deadline - ProcessInfo.processInfo.systemUptime
            guard time > 0 else { throw MacTab5Error.timeout }
            let polled = Darwin.poll(&ready, 1, Int32(time * 1000 + 1))
            if polled < 0 && errno == EINTR { continue }
            guard polled > 0, ready.revents & Int16(POLLOUT) != 0 else { throw MacTab5Error.unavailable }
            let count = data.withUnsafeBytes { Darwin.write(fd, $0.baseAddress!.advanced(by: offset), data.count - offset) }
            if count < 0 && (errno == EAGAIN || errno == EINTR) { continue }
            guard count > 0 else { throw MacTab5Error.unavailable }
            offset += count
        }
    }
}
