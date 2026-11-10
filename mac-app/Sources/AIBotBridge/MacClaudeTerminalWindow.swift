import AppKit
import ApplicationServices

// The existing native process remains the owner of the conversation.
// Only explicit device operations may select a tab or post input.
final class MacClaudeTerminalWindow: MacClaudeQueueReceiptSession {
    private struct KeyNotPosted: Error {}
    // Development acceptance receives fixed phase labels only, never UI text.
    static var diagnostic: ((String) -> Void)?
    private static let fixtureLock = NSLock()
    private static var fixtureLocateFailure = false
    static func fixtureFailNextLocate(_ enabled: Bool) {
        fixtureLock.lock(); defer { fixtureLock.unlock() }; fixtureLocateFailure = enabled
    }
    private static func consumeFixtureLocateFailure() -> Bool {
        fixtureLock.lock(); defer { fixtureLock.unlock() }
        let value = fixtureLocateFailure; fixtureLocateFailure = false; return value
    }
    let source = MacClaudeSessionSource.terminal
    let usesFileImageReferences = true
    func normalizedInput(_ text: String) -> String { text.trimmingCharacters(in: .whitespacesAndNewlines) }
    let session: UUID
    private let native: MacClaudeNativeClient
    private let host: NSRunningApplication
    private let tty: String
    private let config: URL
    private var queueReceiptTarget: String?

    private init(_ native: MacClaudeNativeClient, host: NSRunningApplication, tty: String, config: URL) {
        self.native = native; self.host = host; self.tty = tty; session = native.session
        self.config = config
    }

    static func find(_ session: UUID, config: URL = MacClaudeTerminalRegistration.defaultConfig) -> MacClaudeTerminalWindow? {
        guard let native = MacClaudeNativeClient.find(session, entrypoint: "cli", config: config) as? MacClaudeNativeClient,
              let target = try? native.terminalTarget(), target.host.bundleIdentifier == "com.apple.Terminal" else { return nil }
        return Self(native, host: target.host, tty: target.tty, config: config)
    }

    func state() throws -> String {
        let target = try native.terminalTarget()
        guard target.host.processIdentifier == host.processIdentifier, target.tty == tty else {
            throw MacTab5Error.unconfirmed
        }
        return try native.state()
    }

    func scope() throws -> String {
        _ = try state()
        return try native.scope() + ":" + tty
    }

    private var defaultBindings: Bool {
        !FileManager.default.fileExists(atPath: config.appendingPathComponent("keybindings.json").path)
    }

    func inputPermission(observing: Bool) -> Bool {
        defaultBindings && MacScreenLockObservation.shared.displayedValue != true && MacClaudeWindowPermissions.accessible &&
            MacClaudeWindowPermissions.automation(host, request: false, observing: observing) == noErr
    }

    func allows(_ operation: String, images: Bool) -> Bool {
        guard inputPermission(observing: false), let state = try? state() else { return false }
        if operation == "send" { return state == "idle" }
        return (operation == "steer" || operation == "interrupt" && !images) && ["busy", "working"].contains(state)
    }

    // Resolve by TTY, never by title. Enumerate before selecting so duplicate
    // matches cannot produce an input action. Terminal can expose a stale
    // window reference that no longer resolves; skip only that missing object,
    // and still fail closed on any other window or tab read error.
    private func locate(activate: Bool) throws -> String {
        guard MacClaudeWindowPermissions.automation(host, request: false) == noErr,
              let bundle = host.bundleIdentifier else { throw MacTab5Error.unavailable }
        let scan = Self.scanScript(tty: tty)
        let select = """
        set selected of targetTab to true
        set index of targetWindow to 1
        activate
        """
        let script = """
        with timeout of 2 seconds
            tell application id "\(bundle)"
                set hits to 0
                set targetID to ""
                \(scan)
                if hits is not 1 then error "Target unavailable"
                \(activate ? select : "")
                return targetID
            end tell
        end timeout
        """
        return try Self.script(script)
    }

    static func scanScript(tty: String) -> String {
        return """
        repeat with w in windows
            set windowExists to true
            try
                set windowID to id of w
            on error number errorCode
                if errorCode is not -1728 then error number errorCode
                set windowExists to false
            end try
            if windowExists then
                set tabIndex to 0
                repeat with t in tabs of w
                    set tabIndex to tabIndex + 1
                    if tty of t is "\(tty)" then
                        set hits to hits + 1
                        set targetTab to contents of t
                        set targetWindow to contents of w
                        set targetID to (windowID as text) & "/" & (tabIndex as text)
                    end if
                end repeat
            end if
        end repeat
        """
    }

    private static func script(_ source: String) throws -> String {
        func execute() throws -> String {
            var error: NSDictionary?
            let answer = NSAppleScript(source: source)?.executeAndReturnError(&error).stringValue
            guard error == nil, let answer else {
                if let code = error?[NSAppleScript.errorNumber] as? Int, (-100_000...100_000).contains(code) {
                    diagnostic?("applescript:error:\(code)")
                } else { diagnostic?("applescript:unconfirmed") }
                throw MacTab5Error.unconfirmed
            }
            return answer
        }
        return try Thread.isMainThread ? execute() : DispatchQueue.main.sync(execute: execute)
    }

    private static func value(_ node: AXUIElement, _ key: String) -> CFTypeRef? {
        var result: CFTypeRef?
        return AXUIElementCopyAttributeValue(node, key as CFString, &result) == .success ? result : nil
    }

    private func screen(target: String) throws -> (String, Int) {
        guard NSWorkspace.shared.frontmostApplication?.processIdentifier == host.processIdentifier,
              try locate(activate: false) == target, let bundle = host.bundleIdentifier else {
            Self.diagnostic?("composer:target-unconfirmed")
            throw MacTab5Error.unconfirmed
        }
        let selected = "tty of selected tab of front window"
        guard try Self.script("with timeout of 1 second\ntell application id \"\(bundle)\" to get \(selected)\nend timeout") == tty else {
            Self.diagnostic?("composer:tty-unconfirmed")
            throw MacTab5Error.unconfirmed
        }
        let app = AXUIElementCreateApplication(host.processIdentifier)
        AXUIElementSetMessagingTimeout(app, 0.2)
        guard let raw = Self.value(app, "AXFocusedWindow"), CFGetTypeID(raw) == AXUIElementGetTypeID() else {
            Self.diagnostic?("composer:window-unavailable")
            throw MacTab5Error.unconfirmed
        }
        let window = unsafeBitCast(raw, to: AXUIElement.self)
        guard (Self.value(window, "AXSheets") as? [AXUIElement] ?? []).isEmpty else { Self.diagnostic?("composer:sheet-present"); throw MacTab5Error.unconfirmed }
        var pending = [window], editors: [AXUIElement] = [], visited = 0
        while let node = pending.popLast(), visited < 2000 {
            visited += 1
            if Self.value(node, "AXRole") as? String == "AXTextArea",
               Self.value(node, "AXFocused") as? Bool == true { editors.append(node) }
            pending += Self.value(node, "AXChildren") as? [AXUIElement] ?? []
        }
        guard editors.count == 1, let editor = editors.first else {
            Self.diagnostic?("composer:editor-count:\(editors.count)"); throw MacTab5Error.unconfirmed
        }
        guard
              let output = Self.value(editor, "AXValue") as? String, output.utf8.count <= 2_000_000,
              let rawCursor = Self.value(editor, "AXSelectedTextRange"), CFGetTypeID(rawCursor) == AXValueGetTypeID() else { Self.diagnostic?("composer:attributes-unavailable"); throw MacTab5Error.unconfirmed }
        var cursor = CFRange()
        guard AXValueGetValue(unsafeBitCast(rawCursor, to: AXValue.self), .cfRange, &cursor), cursor.length == 0 else {
            Self.diagnostic?("composer:prompt-unconfirmed"); throw MacTab5Error.unconfirmed
        }
        return (output, cursor.location)
    }

    private func composer(target: String) throws -> MacClaudeWindowComposer {
        let (output, cursor) = try screen(target: target)
        guard let result = MacClaudeWindowComposer(output, cursor: cursor) else {
            Self.diagnostic?("composer:prompt-unconfirmed"); throw MacTab5Error.unconfirmed
        }
        return result
    }

    private func mayRecoverTarget(_ request: MacClaudeNativeRequest) -> Bool {
        guard request.authorized(), (try? request.current()) == true,
              allows(request.operation, images: !request.paths.isEmpty) else { return false }
        return request.authorized() && (try? request.current()) == true
    }

    func queuedSubmissionConfirmed(_ request: MacClaudeNativeRequest) throws -> Bool {
        guard request.operation == "steer", request.paths.isEmpty, request.authorized(),
              try request.current(), let target = queueReceiptTarget else { return false }
        return MacClaudeTerminalQueue.singleMessage(in: try screen(target: target).0) == request.text
    }

    private func key(_ code: CGKeyCode, flags: CGEventFlags = [], text: String? = nil) throws {
        let held = CGEventSource.flagsState(.combinedSessionState).intersection([.maskShift, .maskControl, .maskAlternate, .maskCommand, .maskSecondaryFn])
        guard held.isEmpty, NSWorkspace.shared.frontmostApplication?.processIdentifier == host.processIdentifier,
              let down = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true),
              let up = CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false) else {
            throw KeyNotPosted()
        }
        down.flags = flags; up.flags = flags
        if let text {
            let units = Array(text.utf16)
            units.withUnsafeBufferPointer {
                down.keyboardSetUnicodeString(stringLength: $0.count, unicodeString: $0.baseAddress!)
                up.keyboardSetUnicodeString(stringLength: $0.count, unicodeString: $0.baseAddress!)
            }
        }
        down.postToPid(host.processIdentifier); up.postToPid(host.processIdentifier)
    }

    func prepareSubmission(_ request: MacClaudeNativeRequest) throws -> () throws -> Bool {
        queueReceiptTarget = nil
        var phase = "prepare-check", prepared = false
        defer { Self.diagnostic?("\(request.operation):\(phase):\(prepared ? "ready" : "not-invoked")") }
        guard request.authorized(), try request.current(), allows(request.operation, images: !request.paths.isEmpty) else {
            throw MacTab5Error.unconfirmed
        }
        let input = request.operation == "interrupt" ? "" : try MacClaudeBackgroundPhotos.input(
            text: request.text, paths: request.paths.map(\.path), digests: request.imageDigests)
        phase = "prepare-locate"
        let target = try MacClaudeWindowTargetRecovery.resolve(
            initial: {
                if request.operation == "steer", Self.consumeFixtureLocateFailure() {
                    Self.diagnostic?("locate:fixture-failure"); throw MacTab5Error.unconfirmed
                }
                return try locate(activate: true)
            },
            allowed: { mayRecoverTarget(request) },
            retry: {
                let value = try locate(activate: true)
                Self.diagnostic?("locate:activation-recovered"); return value
            })
        for _ in 0..<20 {
            if NSWorkspace.shared.frontmostApplication?.processIdentifier == host.processIdentifier { break }
            Thread.sleep(forTimeInterval: 0.05)
        }
        phase = "prepare-composer"
        let (output, cursor) = try screen(target: target)
        guard let initial = MacClaudeWindowComposer(output, cursor: cursor), initial.empty else {
            throw MacCodexDesktopError.rejected("desktop_draft_present")
        }
        // A single short text can be checked against the terminal's explicit
        // queue display. Unknown/pre-existing queues and images stay uncertain.
        if request.operation == "steer", request.paths.isEmpty, !request.text.isEmpty,
           request.text.utf8.count <= 40, !request.text.contains("\n"),
           !output.contains("Press up to edit queued messages"),
           !output.contains("ctrl+x ctrl+s to send now") {
            queueReceiptTarget = target
        }
        prepared = true
        return {
            var phase = "recheck", finished = false
            defer { Self.diagnostic?("\(request.operation):\(phase):\(finished ? "complete" : "unconfirmed")") }
            guard request.authorized(), (try? request.current()) == true,
                  self.allows(request.operation, images: !request.paths.isEmpty),
                  (try? self.composer(target: target).empty) == true else { return false }
            if request.operation == "interrupt" {
                phase = "interrupt-post"
                try self.key(8, flags: .maskControl)
                for _ in 0..<50 {
                    if (try? self.state()) == "idle", (try? request.current()) == true {
                        if (try? request.interrupted()) == true { finished = true; return true }
                        // Before the first token, Claude restores the interrupted
                        // input without writing an interruption marker.
                        if let expected = request.expectedUser,
                           let draft = try? self.composer(target: target).text, !draft.isEmpty {
                            let history = try MacClaudeHistory.read(
                                root: self.config.appendingPathComponent("projects"),
                                session: self.session, key: Data(repeating: 0, count: 32), backgroundImages: true)
                            if let restored = MacClaudeHistory.restoreDigest(draft), history.restoreDigests[expected] == restored { finished = true; return true }
                        }
                    }
                    Thread.sleep(forTimeInterval: 0.1)
                }
                throw MacTab5Error.timeout
            }
            var written = "", remaining = input[...]
            while !remaining.isEmpty {
                phase = "chunk-recheck"
                guard request.authorized(), (try? request.current()) == true,
                      self.allows(request.operation, images: !request.paths.isEmpty),
                      (try? self.composer(target: target).matches(written)) == true else { return false }
                let chunk: String
                phase = "text-post"
                do {
                    if remaining.first == "\n" {
                        chunk = "\n"
                        try self.key(38, flags: .maskControl)
                    } else {
                        chunk = String(remaining.prefix(20).prefix(while: { $0 != "\n" }))
                        try self.key(0, text: chunk)
                    }
                } catch is KeyNotPosted where written.isEmpty {
                    // No input event was posted; the existing draft remains on TAB5.
                    return false
                }
                remaining = remaining.dropFirst(chunk.count)
                written += chunk
                for _ in 0..<20 {
                    if (try? self.composer(target: target).matches(written)) == true { break }
                    Thread.sleep(forTimeInterval: 0.025)
                }
            }
            phase = "text-confirm"
            guard request.authorized(), (try? request.current()) == true,
                  (try? self.composer(target: target).matches(input)) == true else { return false }
            // Claude's queue-submit chord preserves the running turn.
            phase = "chord-prefix"
            try self.key(7, flags: .maskControl)
            phase = "chord-recheck"
            guard request.authorized(), (try? request.current()) == true,
                  (try? self.composer(target: target).matches(input)) == true else { return false }
            phase = "submit-post"
            try self.key(request.operation == "send" ? 1 : 36, flags: request.operation == "send" ? .maskControl : [])
            finished = true
            return true
        }
    }
}

enum MacClaudeWindowTargetRecovery {
    // Preparation has not posted input or recorded dispatch. On a transient
    // locate failure, revalidate the request and locate the same session by
    // TTY again. Retrying selection does not replay any submitted input.
    static func resolve(initial: () throws -> String, allowed: () -> Bool,
                        retry: () throws -> String,
                        wait: () -> Void = { Thread.sleep(forTimeInterval: 0.1) }) throws -> String {
        do { return try initial() }
        catch {
            let original = error
            for attempt in 0..<2 {
                guard allowed() else { throw original }
                do { return try retry() }
                catch { if attempt == 0 { wait() } }
            }
            throw original
        }
    }
}

enum MacClaudeTerminalQueue {
    // Recognize only the single-line queue presentation observed in the
    // original terminal. All other layouts intentionally remain unconfirmed.
    static func singleMessage(in output: String) -> String? {
        let lines = output.components(separatedBy: "\n")
        guard let prompt = lines.lastIndex(where: { $0.hasPrefix("❯ ") || $0.hasPrefix("❯\u{a0}") }),
              prompt > 1, prompt + 1 < lines.count else { return nil }
        let placeholder = lines[prompt].trimmingCharacters(in: .whitespaces)
        guard placeholder == "❯\u{a0}Press up to edit queued messages"
                || placeholder == "❯ Press up to edit queued messages" else { return nil }
        let divider = lines[prompt - 1].trimmingCharacters(in: .whitespaces)
        let after = lines[prompt + 1].trimmingCharacters(in: .whitespaces)
        guard divider.hasPrefix(String(repeating: "─", count: 16)), divider.hasSuffix("─"),
              after.hasPrefix(String(repeating: "─", count: 16)), after.hasSuffix("─") else { return nil }
        let start = max(0, prompt - 12)
        let hints = (start..<(prompt - 1)).filter {
            lines[$0].trimmingCharacters(in: .whitespaces) == "ctrl+x ctrl+s to send now"
        }
        guard hints.count == 1, let hint = hints.first, hint > start else { return nil }
        let between = lines[(hint + 1)..<(prompt - 1)]
        guard !between.contains(where: { $0.trimmingCharacters(in: .whitespaces).hasPrefix("❯") }) else { return nil }
        let row = lines[hint - 1].trimmingCharacters(in: .whitespaces)
        guard row.hasPrefix("❯ ") || row.hasPrefix("❯\u{a0}") else { return nil }
        let text = String(row.dropFirst(2))
        return text.isEmpty ? nil : text
    }
}

struct MacClaudeWindowComposer {
    let rows: [String]
    var text: String { rows.joined(separator: "\n") }
    var empty: Bool { rows.count == 1 && rows[0].isEmpty }

    init?(_ output: String, cursor: Int? = nil) {
        let lines = output.components(separatedBy: "\n")
        func divider(_ line: String) -> Bool {
            let value = line.trimmingCharacters(in: .whitespaces)
            return value.hasPrefix(String(repeating: "─", count: 16)) && value.hasSuffix("─")
        }
        guard let first = lines.lastIndex(where: { $0.hasPrefix("❯ ") || $0.hasPrefix("❯\u{a0}") || $0.hasPrefix("> ") }),
              first > 0, divider(lines[first - 1]),
              let last = lines.indices.dropFirst(first + 1).first(where: { divider(lines[$0]) }),
              last - first <= 30 else { return nil }
        let content = Array(lines[first..<last])
        guard content.dropFirst().allSatisfy({ $0.hasPrefix("  ") }) else { return nil }
        var parsed = content.map { String($0.dropFirst(2)) }
        if let cursor {
            let offset = lines.prefix(last - 1).reduce(0) { $0 + $1.utf16.count + 1 } + 2
            let count = cursor - offset
            guard count >= 0, count <= parsed[parsed.count - 1].utf16.count,
                  parsed[parsed.count - 1].utf16.dropFirst(count).allSatisfy({ $0 == 32 }) else { return nil }
            parsed[parsed.count - 1] = String(decoding: parsed[parsed.count - 1].utf16.prefix(count), as: UTF16.self)
            for index in parsed.indices.dropLast() {
                while parsed[index].last == " " { parsed[index].removeLast() }
            }
        }
        rows = parsed
    }

    func matches(_ expected: String) -> Bool {
        if expected.isEmpty { return empty }
        // Preserve visible characters and word boundaries across terminal wraps.
        var offsets: Set<String.Index> = [expected.startIndex]
        for (index, row) in rows.enumerated() {
            var next = Set<String.Index>()
            for offset in offsets where expected[offset...].hasPrefix(row) {
                let end = expected.index(offset, offsetBy: row.count)
                next.insert(end)
                if index + 1 < rows.count, end < expected.endIndex, [" ", "\n"].contains(expected[end]) {
                    next.insert(expected.index(after: end))
                }
            }
            offsets = next
        }
        return offsets.contains(expected.endIndex)
    }
}
