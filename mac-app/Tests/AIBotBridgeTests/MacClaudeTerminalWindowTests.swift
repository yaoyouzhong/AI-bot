import AppKit
import SwiftTerm
import XCTest
@testable import AIBotBridge

final class MacClaudeTerminalWindowTests: XCTestCase {
    private let rule = String(repeating: "─", count: 60)
    private func composer(_ rows: [String]) throws -> MacClaudeWindowComposer {
        try XCTUnwrap(MacClaudeWindowComposer(([rule] + rows + [rule]).joined(separator: "\n")))
    }

    func testTerminalSuggestionDoesNotConsumeAnIdenticalRealDraft() throws {
        final class Delegate: TerminalDelegate {
            func send(source: Terminal, data: ArraySlice<UInt8>) {}
        }
        let delegate = Delegate()
        let phrase = "读一下 CLAUDE.md 试试"
        func composer(style: String, cursor: Int) -> String? {
            let terminal = Terminal(delegate: delegate, options: .init(cols: 80, rows: 24))
            let screen = "\u{1b}[1;1H" + rule + "\u{1b}[2;1H> " + style + phrase
                + "\u{1b}[0m\u{1b}[3;1H" + rule + "\u{1b}[2;\(cursor)H"
            terminal.feed(byteArray: Array(screen.utf8))
            return MacClaudeTerminalRelay.composer(terminal)
        }
        XCTAssertEqual(composer(style: "\u{1b}[2m", cursor: 3), "")
        // The terminal grid represents double-width Chinese cells with padding.
        XCTAssertEqual(composer(style: "", cursor: 3)?.filter { !$0.isWhitespace }, phrase.filter { !$0.isWhitespace })
        XCTAssertEqual(composer(style: "\u{1b}[2m", cursor: 5)?.filter { !$0.isWhitespace }, phrase.filter { !$0.isWhitespace })
        let output = [rule, "❯ " + phrase, rule].joined(separator: "\n")
        // Apple Terminal AX plain text without a style signal is ambiguous.
        XCTAssertNil(MacClaudeWindowComposer(output, cursor: rule.utf16.count + 3))
    }

    func testScannerSkipsMissingWindowButFailsClosedOnUnreadableTabs() throws {
        func scan(_ windowList: String) throws -> String {
            let source = """
            script tabOne
                property tty : "/dev/fixture"
            end script
            script good
                property id : 42
                property tabs : {tabOne}
            end script
            script secondWindow
                property id : 43
                property tabs : {tabOne}
            end script
            script missingWindow
            end script
            script unreadableTabs
                property id : 44
            end script
            set fixtureWindows to \(windowList)
            set hits to 0
            set targetID to ""
            try
                \(MacClaudeTerminalWindow.scanScript(tty: "/dev/fixture").replacingOccurrences(of: "repeat with w in windows", with: "repeat with w in fixtureWindows").replacingOccurrences(of: "set windowID to id of w", with: "if contents of w is missingWindow then error number -1728\nset windowID to id of w"))
                return (hits as text) & ":" & targetID
            on error number n
                return "error:" & (n as text)
            end try
            """
            var error: NSDictionary?
            let result = NSAppleScript(source: source)?.executeAndReturnError(&error).stringValue
            XCTAssertNil(error)
            return try XCTUnwrap(result)
        }
        XCTAssertEqual(try scan("{missingWindow, good}"), "1:42/1")
        XCTAssertEqual(try scan("{good, secondWindow}"), "2:43/1")
        XCTAssertEqual(try scan("{good, unreadableTabs}"), "error:-1708")
    }

    func testComposerRequiresCompleteInputRegion() throws {
        XCTAssertTrue(try composer(["❯ "]).empty)
        XCTAssertFalse(try composer(["❯ user draft"]).empty)
        XCTAssertFalse(try composer(["❯ Try a suggestion"]).empty)
        XCTAssertNil(MacClaudeWindowComposer("❯ shell command\n" + rule))
        XCTAssertNil(MacClaudeWindowComposer(rule + "\n❯ unfinished"))
        XCTAssertNil(MacClaudeWindowComposer(rule + "\n❯ text\nunexpected line\n" + rule))
    }

    func testComposerMatchesWrappedUnicodeAndPreservesVisibleDifferences() throws {
        let value = try composer(["❯ hello 中文🙂", "  next line"])
        XCTAssertTrue(value.matches("hello 中文🙂\nnext line"))
        XCTAssertTrue(value.matches("hello 中文🙂 next line"))
        XCTAssertFalse(value.matches("hello 中文🙂 other line"))
        XCTAssertFalse(value.matches("hello 中文🙂\nnext line extra"))
        XCTAssertFalse(value.matches(""))
    }

    func testCursorPreservesWhitespaceDraftsAndRejectsMovedCaret() throws {
        let empty = rule + "\n❯\u{a0} \n" + rule
        XCTAssertTrue(try XCTUnwrap(MacClaudeWindowComposer(empty, cursor: rule.utf16.count + 3)).empty)
        let spaces = rule + "\n❯\u{a0}   \n" + rule
        XCTAssertFalse(try XCTUnwrap(MacClaudeWindowComposer(spaces, cursor: rule.utf16.count + 5)).empty)
        let text = rule + "\n❯\u{a0}  abc  \n" + rule
        XCTAssertTrue(try XCTUnwrap(MacClaudeWindowComposer(text, cursor: rule.utf16.count + 9)).matches("  abc "))
        XCTAssertNil(MacClaudeWindowComposer(text, cursor: rule.utf16.count + 3))
        XCTAssertTrue(MacClaudeNativeClient.validStart(100, registeredAt: 100500))
        XCTAssertFalse(MacClaudeNativeClient.validStart(200, registeredAt: 100500))
        XCTAssertFalse(MacClaudeNativeClient.validStart(1, registeredAt: 100500))
    }

    func testQueueEvidenceRequiresTheLiveSingleLineQueuePresentation() {
        let prompt = rule + "\n❯\u{a0}Press up to edit queued messages\n" + rule
        let valid = "❯ old message\n\n❯ q   \n  ctrl+x ctrl+s to send now\n\n✢ Processing… (51s)\n\n" + prompt
        XCTAssertEqual(MacClaudeTerminalQueue.singleMessage(in: valid), "q")
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid.replacingOccurrences(of: "Press up to edit queued messages", with: "")))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid.replacingOccurrences(of: "ctrl+x ctrl+s to send now", with: "other shortcut")))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid.replacingOccurrences(of: "❯ q   ", with: "  q   ")))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid.replacingOccurrences(of: "✢ Processing…", with: "❯ another queued message\n✢ Processing…")))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid.replacingOccurrences(of: "  ctrl+x", with: "  wrapped continuation\n  ctrl+x")))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: "❯ q\n  ctrl+x ctrl+s to send now\n" + rule + "\n❯ ordinary draft\n" + rule))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid + "\n" + rule + "\n❯ ordinary draft\n" + rule))
        XCTAssertNil(MacClaudeTerminalQueue.singleMessage(in: valid.replacingOccurrences(of: prompt, with: rule + "\n❯\u{a0}Press up to edit queued messages\nnot a divider")))
    }

    func testTargetRecoveryRetriesOnlyWhileRequestIsAuthorized() throws {
        enum Failure: Error { case initial, retry }
        var initialCalls = 0, checks = 0, retries = 0, waits = 0
        let target = try MacClaudeWindowTargetRecovery.resolve(initial: {
            initialCalls += 1; throw Failure.initial
        }, allowed: {
            checks += 1; return true
        }, retry: {
            retries += 1
            if retries == 1 { throw Failure.retry }
            return "same-target"
        }, wait: { waits += 1 })
        XCTAssertEqual(target, "same-target")
        XCTAssertEqual(initialCalls, 1); XCTAssertEqual(checks, 2)
        XCTAssertEqual(retries, 2); XCTAssertEqual(waits, 1)

        retries = 0; checks = 0
        XCTAssertThrowsError(try MacClaudeWindowTargetRecovery.resolve(initial: { throw Failure.initial }, allowed: {
            checks += 1; return checks == 1
        }, retry: { retries += 1; throw Failure.retry }, wait: {})) { error in
            guard case Failure.initial = error else { return XCTFail("Original failure must remain a rejection") }
        }
        XCTAssertEqual(checks, 2); XCTAssertEqual(retries, 1)

        retries = 0; checks = 0
        XCTAssertThrowsError(try MacClaudeWindowTargetRecovery.resolve(initial: { throw Failure.initial }, allowed: {
            checks += 1; return true
        }, retry: { retries += 1; throw Failure.retry }, wait: {})) { error in
            guard case Failure.initial = error else { return XCTFail("Recovery must preserve the original rejection") }
        }
        XCTAssertEqual(checks, 2); XCTAssertEqual(retries, 2)

        retries = 0
        XCTAssertEqual(try MacClaudeWindowTargetRecovery.resolve(initial: { "ready" }, allowed: {
            XCTFail("A successful locate must not enter recovery"); return false
        }, retry: { retries += 1; return "unexpected" }), "ready")
        XCTAssertEqual(retries, 0)
    }

    // Explicitly enabled only for the disposable native terminal fixture.
    // No production account or USB is used here.
    func testExistingWindowFixture() throws {
        guard let folder = ProcessInfo.processInfo.environment["AIBOT_WINDOW_FIXTURE"] else {
            throw XCTSkip("Requires the dedicated ordinary terminal window fixture")
        }
        let root = URL(fileURLWithPath: folder).resolvingSymlinksInPath()
        let temporary = FileManager.default.temporaryDirectory.resolvingSymlinksInPath()
        XCTAssertTrue(root.lastPathComponent.hasPrefix("aibot-window-") &&
                      (root.deletingLastPathComponent() == temporary || root.deletingLastPathComponent().path == "/tmp" || root.deletingLastPathComponent().path == "/private/tmp"))
        guard try String(contentsOf: root.appendingPathComponent(".aibot-synthetic-claude"), encoding: .utf8) == "window\n" else {
            XCTFail("Invalid fixture"); return
        }
        let session = try XCTUnwrap(UUID(uuidString: String(contentsOf: root.appendingPathComponent("terminal-session"), encoding: .utf8)))
        let config = root.appendingPathComponent(".claude")
        let client = try XCTUnwrap(MacClaudeTerminalWindow.find(session, config: config))
        XCTAssertTrue(client.allows("send", images: true))
        let key = Data(0..<32)
        let store = MacTab5PrivateStore(root: root.appendingPathComponent("window-test-store-" + UUID().uuidString), key: { key })
        let registry = try MacClaudeHarnesses([
            .init(source: .terminal, discover: { [] }, live: { [session] },
                  resolve: { $0 == session ? MacClaudeTerminalWindow.find($0, config: config) : nil })
        ])
        let backend = MacClaudeWorkspaceBackend(root: config.appendingPathComponent("projects"), store: store, key: { key }, harnesses: registry)
        let workspace = MacClaudeWorkspace(store: store, backend: backend, enabled: { true })
        defer { workspace.stop() }
        workspace.tasks.refresh()
        let task = try XCTUnwrap(backend.alias(session: session))
        func until(_ predicate: () throws -> Bool) throws {
            let deadline = ProcessInfo.processInfo.systemUptime + 12
            repeat {
                if try predicate() { return }
                Thread.sleep(forTimeInterval: 0.1)
            } while ProcessInfo.processInfo.systemUptime < deadline
            throw MacTab5Error.timeout
        }
        func state(_ turn: String) throws -> String? {
            let value = try MacClaudeHistory.read(root: config.appendingPathComponent("projects"), session: session, key: key, backgroundImages: true)
            return value.turns.first { $0["id"] as? String == turn }?["status"] as? String
        }
        func request(_ op: String, _ text: String = "", expected: String = "", images: [String] = []) throws -> String {
            let packet: [String: Any] = ["op": op, "message": text, "expectedTurn": expected, "images": images,
                                          "taskId": task, "requestId": UUID().uuidString.lowercased()]
            let result = workspace.tasks.handle(packet, device: "window-fixture")
            XCTAssertEqual(result.status, 202, "\(op) photos=\(images.count) \(result.body["error"] ?? "")")
            let turn = try XCTUnwrap(result.body["turnId"] as? String)
            XCTAssertEqual(workspace.tasks.handle(packet, device: "window-fixture").body["turnId"] as? String, turn)
            return turn
        }
        // Simulate a pre-existing desktop draft only in this disposable native
        // window, then prove the bridge leaves it unsubmitted.
        let scope = try client.scope()
        func focus() throws {
            _ = try client.prepareSubmission(.init(operation: "send", text: "probe", paths: [], imageDigests: [],
                                                    authorized: { true }, current: { try client.scope() == scope }))
        }
        try focus()
        let native = try XCTUnwrap(MacClaudeNativeClient.find(session, entrypoint: "cli", config: config) as? MacClaudeNativeClient)
        let host = try native.terminalTarget().host
        func press(_ code: CGKeyCode, flags: CGEventFlags = [], text: String? = nil) throws {
            guard NSWorkspace.shared.frontmostApplication?.processIdentifier == host.processIdentifier,
                  try native.scope() + ":" + native.terminalTarget().tty == scope else { throw MacTab5Error.unconfirmed }
            let down = try XCTUnwrap(CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: true))
            let up = try XCTUnwrap(CGEvent(keyboardEventSource: nil, virtualKey: code, keyDown: false))
            down.flags = flags; up.flags = flags
            if let text {
                let units = Array(text.utf16)
                units.withUnsafeBufferPointer {
                    down.keyboardSetUnicodeString(stringLength: $0.count, unicodeString: $0.baseAddress!)
                    up.keyboardSetUnicodeString(stringLength: $0.count, unicodeString: $0.baseAddress!)
                }
            }
            down.postToPid(host.processIdentifier); up.postToPid(host.processIdentifier)
            Thread.sleep(forTimeInterval: 0.15)
        }
        try press(0, text: "fixture desktop draft")
        let blocked = workspace.tasks.handle(["op": "send", "taskId": task, "requestId": UUID().uuidString.lowercased(),
                                              "message": "must not submit", "expectedTurn": ""], device: "window-fixture")
        XCTAssertEqual(blocked.status, 409)
        XCTAssertEqual(blocked.body["error"] as? String, "desktop_draft_present")
        try press(32, flags: .maskControl)
        try until { (try? focus()) != nil }
        print("WINDOW_STAGE draft protected")
        let textTurn = try request("send", "fixture window 中文🙂\nsecond line  ")
        try until { try state(textTurn) == "completed" && client.state() == "idle" }
        print("WINDOW_STAGE text received")
        let jpeg = try MacTab5TestData.jpeg()
        let bitmap = try XCTUnwrap(NSBitmapImageRep(data: jpeg))
        let png = try XCTUnwrap(bitmap.representation(using: .png, properties: [:]))
        let photos = [jpeg, png, jpeg]
        let images = photos.map { bytes -> String in
            let image = UUID().uuidString.lowercased()
            XCTAssertEqual(workspace.tasks.photos.upload(["taskId": task, "requestId": image], bytes: bytes, device: "window-fixture").status, 200)
            return image
        }
        try JSONEncoder().encode(photos.map(MacTab5Protocol.sha256)).write(to: root.appendingPathComponent("expect-1.json"))
        try Data().write(to: root.appendingPathComponent("release-1"))
        let photoTurn = try request("send", images: images)
        try until { try state(photoTurn) == "completed" && client.state() == "idle" }
        print("WINDOW_STAGE photos received")
        let first = try request("send", "fixture running for append")
        try until { client.allows("steer", images: false) && FileManager.default.fileExists(atPath: root.appendingPathComponent("append-busy").path) }
        let released = root.appendingPathComponent("append-release")
        DispatchQueue.global().asyncAfter(deadline: .now() + 6) { try? Data().write(to: released) }
        let queuedImage = UUID().uuidString.lowercased()
        XCTAssertEqual(workspace.tasks.photos.upload(["taskId": task, "requestId": queuedImage], bytes: jpeg, device: "window-fixture").status, 200)
        try JSONEncoder().encode([MacTab5Protocol.sha256(jpeg)]).write(to: root.appendingPathComponent("expect-2.json"))
        try Data().write(to: root.appendingPathComponent("release-2"))
        let queued = try request("steer", "fixture window queued", expected: first, images: [queuedImage])
        try until { try state(first) == "completed" && state(queued) == "completed" && client.state() == "idle" }
        print("WINDOW_STAGE image append received")
        let running = try request("send", "fixture running for interrupt\n" + String(repeating: "中文🙂 long text ", count: 10))
        try until { client.allows("interrupt", images: false) && FileManager.default.fileExists(atPath: root.appendingPathComponent("interrupt-busy").path) }
        let stopped = try request("interrupt", expected: running)
        XCTAssertEqual(stopped, running)
        XCTAssertEqual(try client.state(), "idle")
        try Data().write(to: root.appendingPathComponent("interrupt-release"))
        XCTAssertEqual(workspace.tasks.handle(["op": "read", "taskId": task], device: "window-fixture").status, 200)
        let reply = workspace.tasks.snapshot()?["replyView"] as? [String: Any]
        XCTAssertEqual(reply?["state"] as? String, "interrupted")
        // Clear only the restored input in this owned disposable fixture.
        try press(8, flags: .maskControl)
        try until { (try? focus()) != nil }
        if let bytes = try? Data(contentsOf: root.appendingPathComponent("audit.json")),
           let audit = try JSONSerialization.jsonObject(with: bytes) as? [String: Any],
           let photos = audit["photos"] as? [[Any]] {
            XCTAssertEqual(photos.count, 2)
            XCTAssertTrue(photos.allSatisfy { $0.first as? Bool == true })
        }
    }
}
