import XCTest
@testable import AIBotBridge

final class MacTab5DesktopTests: XCTestCase {
    func testEmptyComposerDecorationIsNotDraftTextInAnyLanguage() throws {
        for hint in ["和 Codex 一起工作", "Work with Codex"] {
            let nodes: [MacTab5DesktopAX.ComposerTextNode] = [
                .init(classes: ["placeholder"], children: [.init(text: hint)])
            ]
            let fixture = Fixture(); fixture.value = hint + "\n"
            var backend = fixture.backend
            backend.read = { _ in MacTab5DesktopAX.composerText(fixture.value, classes: ["ProseMirror"], children: fixture.writes == 0 ? nodes : [.init(text: fixture.value)]) }
            let read = backend.read
            backend.write = { _, expected, next in
                guard read(fixture.task) == expected else { return nil }
                fixture.value = next; fixture.writes += 1; return fixture.value
            }
            let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
            let request: [String: Any] = ["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "new recording"]
            let result = desktop.handle(request, device: "A")
            XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["appended"] as? Bool, false)
            XCTAssertEqual(fixture.value, "new recording"); XCTAssertEqual(fixture.writes, 1); XCTAssertEqual(fixture.sends, 0)
            XCTAssertEqual(desktop.handle(request, device: "A").status, 200); XCTAssertEqual(fixture.writes, 1)
        }
    }
    func testPlaceholderWordsAuthoredByUserRemainRealDraftText() {
        let text = "和 Codex 一起工作"
        XCTAssertEqual(MacTab5DesktopAX.composerText(text, classes: ["ProseMirror"], children: [.init(text: text)]), text)
        XCTAssertEqual(MacTab5DesktopAX.composerText(text, classes: [], children: [.init(classes: ["placeholder"], text: text)]), text)
        XCTAssertEqual(MacTab5DesktopAX.composerText("\n" + text + "\n", classes: ["ProseMirror"], children: []), "\n" + text + "\n")
    }
    func testStaleMixedOrUnmatchedPlaceholderDecorationCannotEraseDraft() {
        typealias Node = MacTab5DesktopAX.ComposerTextNode
        let hint = Node(classes: ["placeholder"], children: [.init(text: "hint")])
        for nodes in [[hint, .init(text: "actual draft")], [hint, hint], [Node(classes: ["placeholder"])]] {
            XCTAssertNil(MacTab5DesktopAX.composerText("hint", classes: ["ProseMirror"], children: nodes))
        }
        XCTAssertNil(MacTab5DesktopAX.composerText("actual draft", classes: ["ProseMirror"], children: [hint]))
        XCTAssertNil(MacTab5DesktopAX.composerText("\nhint", classes: ["ProseMirror"], children: [hint]))
        XCTAssertEqual(MacTab5DesktopAX.composerText("\n\n", classes: ["ProseMirror"], children: [hint]), "\n\n")
        XCTAssertNil(MacTab5DesktopAX.composerText(String(repeating: "x", count: 2001), classes: [], children: []))
    }
    func testSubmissionReportsTheBlockingGuardWithoutEditingOrSending() throws {
        let fixture = Fixture(), codex = try model(fixture)
        var backend = fixture.backend, readable = true
        backend.read = { _ in readable ? fixture.value : nil }; backend.diagnostic = { .role }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        let stageID = UUID().uuidString.lowercased()
        XCTAssertEqual(desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": stageID, "text": "preview"], device: "A").status, 200)
        func submit(_ reason: MacTab5DesktopCheck, draftID: String? = nil, device: String = "A") {
            XCTAssertEqual(desktop.handle(["op": "submit-draft", "taskId": fixture.task, "requestId": UUID().uuidString,
                "draftRequestId": draftID ?? stageID], device: device).status, 409)
            XCTAssertEqual(desktop.summary, reason.rawValue)
            XCTAssertEqual(fixture.writes, 1); XCTAssertEqual(fixture.sends, 0)
        }
        submit(.submitReceipt, draftID: UUID().uuidString)
        submit(.submitReceipt, device: "B")
        fixture.value = "preview\n"; submit(.submitParagraph)
        fixture.value = "preview "; submit(.changed)
        fixture.value = "preview"; readable = false; submit(.role); readable = true
        var records = try MacTab5Protocol.object(codex.store.read("desktop-receipts")!), stage = records[stageID] as! [String: Any]
        stage["createdAt"] = Date().timeIntervalSince1970 - 901; records[stageID] = stage
        try codex.store.write("desktop-receipts", MacTab5Protocol.json(records)); submit(.submitExpired)
        stage["createdAt"] = Date().timeIntervalSince1970; stage["stage"] = false; records[stageID] = stage
        try codex.store.write("desktop-receipts", MacTab5Protocol.json(records)); submit(.submitUsed)
        stage["stage"] = true; records[stageID] = stage
        try codex.store.write("desktop-receipts", MacTab5Protocol.json(records))
        XCTAssertEqual(desktop.handle(["op": "submit-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "draftRequestId": stageID], device: "A").status, 200)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.submitted.rawValue)
        XCTAssertEqual(fixture.writes, 2); XCTAssertEqual(fixture.sends, 1)
    }
    func testWhitespaceOnlyComposerDoesNotPrependEmptyLinesOrClaimAppend() throws {
        for initial in ["", "\n", "\n\n", "\r\n", " \t\n"] {
            let fixture = Fixture(); fixture.value = initial
            let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: fixture.backend)
            let request: [String: Any] = ["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "测试输入"]
            let result = desktop.handle(request, device: "A")
            XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["appended"] as? Bool, false)
            XCTAssertEqual(fixture.value, "测试输入"); XCTAssertEqual(fixture.writes, 1); XCTAssertEqual(fixture.sends, 0)
            XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
            XCTAssertEqual(fixture.writes, 1)
        }
        let fixture = Fixture(); fixture.value = "\noriginal \n"
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: fixture.backend)
        let result = desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "second"], device: "A")
        XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["appended"] as? Bool, true)
        XCTAssertEqual(fixture.value, "\noriginal \n second")
    }
    func testReadbackWaitsForExactBodyAndReturnsRawParagraphEndings() {
        var values: [String?] = ["old", nil, "测试输入\r\n"], reads = 0, waits = 0
        let observed = MacTab5DesktopAX.confirmWrite("测试输入", read: { reads += 1; return values.removeFirst() }, wait: { waits += 1 })
        XCTAssertEqual(observed, "测试输入\r\n"); XCTAssertEqual(reads, 3); XCTAssertEqual(waits, 2)
        XCTAssertEqual(MacTab5DesktopAX.confirmWrite("", read: { "\n\n" }, wait: {}), "\n\n")
        XCTAssertEqual(MacTab5DesktopAX.confirmWrite("first\nsecond\n", read: { "first\nsecond\r\n" }, wait: {}), "first\nsecond\r\n")
    }
    func testReadbackNeverIgnoresBodyLeadingLinesSpacesOrPermanentUnavailability() {
        for value in [nil, "other", "\n测试输入", "测试\n输入", "测试输入 ", "测试输入\n ", String(repeating: "x", count: 2001)] as [String?] {
            var reads = 0, waits = 0
            XCTAssertNil(MacTab5DesktopAX.confirmWrite("测试输入", read: { reads += 1; return value }, wait: { waits += 1 }))
            XCTAssertEqual(reads, 12); XCTAssertEqual(waits, 11)
        }
    }
    func testSubmissionUsesVerifiedRawSnapshotAndStillRejectsLaterChanges() throws {
        let fixture = Fixture(), codex = try model(fixture)
        var backend = fixture.backend
        backend.write = { _, before, after in
            guard fixture.value == before else { return nil }
            fixture.value = after + "\n"; fixture.writes += 1
            return MacTab5DesktopAX.confirmWrite(after, read: { fixture.value }, wait: {})
        }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        let stageID = UUID().uuidString
        XCTAssertEqual(desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": stageID, "text": "测试输入"], device: "A").status, 200)
        XCTAssertEqual(fixture.value, "测试输入\n"); XCTAssertEqual(fixture.writes, 1); XCTAssertEqual(fixture.sends, 0)
        fixture.value += " "
        let submit: [String: Any] = ["op": "submit-draft", "taskId": fixture.task, "draftRequestId": stageID, "requestId": UUID().uuidString]
        XCTAssertEqual(desktop.handle(submit, device: "A").status, 409)
        XCTAssertEqual(fixture.writes, 1); XCTAssertEqual(fixture.sends, 0)
        fixture.value = "测试输入\n"
        var fresh = submit; fresh["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(fresh, device: "A").status, 200)
        XCTAssertEqual(fixture.writes, 2); XCTAssertEqual(fixture.sends, 1)
        XCTAssertEqual(desktop.handle(fresh, device: "A").status, 200)
        XCTAssertEqual(fixture.writes, 2); XCTAssertEqual(fixture.sends, 1)
    }
    func testWriteFailureAndRecoveryPublishCurrentStatusWithoutRetrying() throws {
        let fixture = Fixture(), codex = try model(fixture)
        var backend = fixture.backend, deny = true, attempts = 0
        backend.diagnostic = { .readOnly }
        backend.write = { task, before, after in
            attempts += 1
            return deny ? nil : fixture.backend.write(task, before, after)
        }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        var request: [String: Any] = ["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "preview"]
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.readOnly.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(attempts, 1); XCTAssertEqual(fixture.value, ""); XCTAssertEqual(fixture.sends, 0)
        deny = false; request["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.confirmed.rawValue)
        XCTAssertEqual(attempts, 2); XCTAssertEqual(fixture.value, "preview"); XCTAssertEqual(fixture.sends, 0)
        fixture.value = String(repeating: "x", count: 2000); request["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.tooLong.rawValue)
        XCTAssertEqual(attempts, 2); XCTAssertEqual(fixture.value.utf8.count, 2000); XCTAssertEqual(fixture.sends, 0)
    }
    func testStructuredMainComposerRequiresItsOwnTrustedDocumentAndUniqueFullTitle() {
        let task = UUID().uuidString.lowercased(), other = UUID().uuidString.lowercased()
        typealias Container = MacTab5DesktopAX.ComposerContainer
        let body = Container(role: "AXGroup", classes: ["relative", "_ComposerLayoutBody_5est5_2"])
        let page = Container(role: "AXWebArea", address: "app://-/index.html", title: "Acceptance chat")
        func check(_ containers: [Container], classes: [String] = ["ProseMirror", "ProseMirror-focused"], title: String? = "Acceptance chat") -> MacTab5DesktopCheck {
            MacTab5DesktopAX.structuredComposerCheck(classes: classes, containers: containers, task: task, uniqueFullTitle: title, resources: nil)
        }
        XCTAssertEqual(check([body, page]), .confirmed)
        // Exact task routes work without a unique name; a conflicting route
        // rejects even when the accessible document still has the old title.
        XCTAssertEqual(check([body, Container(role: "AXWebArea", address: "app://-/local/" + task)], title: nil), .confirmed)
        XCTAssertEqual(check([body, Container(role: "AXWebArea", address: "app://-/local/" + other, title: "Acceptance chat")]), .route)
        XCTAssertEqual(check([body, page], title: nil), .route) // Duplicate/unavailable full catalog name.
        XCTAssertEqual(check([body, page], title: "Acceptance…"), .route)
        XCTAssertEqual(check([body, Container(role: "AXWebArea", address: "app://-/index.html", title: "Other chat")]), .route)
        for address in ["https://example.com", "app://fs/index.html", "file:///tmp/index.html"] {
            XCTAssertEqual(check([body, Container(role: "AXWebArea", address: address, title: "Acceptance chat")]), .route)
        }
        for classes in [[], ["ProseMirror-trailingBreak"], ["NotProseMirror"], ["composer"]] {
            XCTAssertEqual(check([body, page], classes: classes), .identifier)
        }
        // A question reply, annotation editor or nested browser cannot become
        // the main composer by borrowing an outer document/body's identity.
        for containers in [[page], [page, body], [body],
                           [Container(role: "AXGroup", classes: ["@container/request-card"]), body, page],
                           [Container(role: "AXDialog"), body, page], [Container(role: "AXSheet"), body, page],
                           [body, page, page], [Container(role: "AXTextArea"), body, page],
                           [Container(role: "AXGroup", classes: ["prefix_ComposerLayoutBody_fake"]), page]] {
            XCTAssertEqual(check(containers), .structure)
        }
    }
    func testFailedStagingReplacesNavigationStatusWithoutRetryOrTextMutation() throws {
        let fixture = Fixture(), codex = try model(fixture)
        var backend = fixture.backend, reads = 0, reason = MacTab5DesktopCheck.identifierAndRoute
        backend.navigation = { $0 == fixture.task }
        backend.read = { _ in reads += 1; return nil }
        backend.diagnostic = { reason }
        backend.evidence = { "navigation-only evidence" }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        XCTAssertEqual(desktop.handle(["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString], device: "A").status, 200)
        XCTAssertTrue(desktop.summary.contains("navigation-only evidence"))
        let request: [String: Any] = ["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "recognized preview"]
        XCTAssertEqual(desktop.handle(request, device: "A").status, 503)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.identifierAndRoute.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 503)
        XCTAssertEqual(reads, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
        reason = .route
        var fresh = request; fresh["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(fresh, device: "A").status, 503)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.route.rawValue)
        XCTAssertEqual(reads, 2); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testDelayedNavigationUsesTheDeadlineWithoutReopeningOrReadingAnEditor() throws {
        let fixture = Fixture(), codex = try model(fixture)
        var backend = fixture.backend, checks = 0, reads = 0
        backend.navigation = { id in checks += 1; return checks >= 9 && id == fixture.task }
        backend.read = { _ in reads += 1; return nil }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        let result = desktop.handle(request, device: "A")
        XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["foreground"] as? Bool, true)
        XCTAssertEqual(result.body["taskId"] as? String, fixture.task); XCTAssertNil(result.body["draftEmpty"])
        XCTAssertEqual(checks, 9); XCTAssertEqual(reads, 0); XCTAssertEqual(fixture.opens, 1)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(checks, 9); XCTAssertEqual(fixture.opens, 1)
        XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testAmbiguousTitleOpenAcknowledgmentNeverAuthorizesComposerOrFalseEmptyDraft() throws {
        let fixture = Fixture(), codex = try model(fixture)
        var backend = fixture.backend, reads = 0, activations = 0
        backend.navigation = { _ in false }
        backend.openOnly = { id in activations += 1; return id == fixture.task }
        backend.read = { _ in reads += 1; return nil }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        let result = desktop.handle(request, device: "A")
        XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["foreground"] as? Bool, true)
        XCTAssertEqual(result.body["taskId"] as? String, fixture.task); XCTAssertNil(result.body["draftEmpty"])
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.navigationOpened.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(fixture.opens, 1); XCTAssertEqual(activations, 1); XCTAssertEqual(reads, 0)
        XCTAssertEqual(desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "test"], device: "A").status, 503)
        XCTAssertEqual(activations, 1); XCTAssertEqual(reads, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
        fixture.foreground = false
        let failed = desktop.handle(["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString], device: "A")
        XCTAssertEqual(failed.status, 409); XCTAssertEqual(activations, 1)
    }
    func testNavigationDoesNotUnlockPrivateStoreAndMutationsStillFailClosed() throws {
        let fixture = Fixture()
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        var denyKey = false, keyReads = 0
        let store = MacTab5PrivateStore(root: root, key: {
            keyReads += 1
            if denyKey { throw MacTab5Error.keychain }
            return Data(repeating: 4, count: 32)
        })
        // Include an existing encrypted receipt file: opening must neither read
        // that file's key nor overwrite it, even across bridge object restarts.
        try store.write("desktop-receipts", MacTab5Protocol.json(["existing": "preserve"]))
        denyKey = true; keyReads = 0
        let codex = MacTab5Codex(store: store, enabled: { true }, sendEnabled: { true }, call: fixture.call)
        codex.refresh()
        var backend = fixture.backend
        backend.navigation = { $0 == fixture.task }
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(desktop.handle(request, device: "B").status, 409)
        XCTAssertEqual(fixture.opens, 1); XCTAssertEqual(keyReads, 0)
        let restarted = MacTab5Desktop(codex: codex, enabled: { true }, backend: backend)
        XCTAssertEqual(restarted.handle(request, device: "A").status, 200)
        XCTAssertEqual(fixture.opens, 2); XCTAssertEqual(keyReads, 0)
        var conflict = request; conflict["op"] = "stage-draft"; conflict["text"] = "test"
        XCTAssertEqual(desktop.handle(conflict, device: "A").status, 409)
        XCTAssertEqual(keyReads, 0)
        conflict["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(conflict, device: "A").status, 504)
        XCTAssertEqual(keyReads, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
        denyKey = false
        let preserved = try store.read("desktop-receipts").map(MacTab5Protocol.object)
        XCTAssertEqual(preserved?["existing"] as? String, "preserve")
    }
    func testMissingComposerAndRouteAreReportedSeparatelyWithoutRelaxingIdentity() {
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "", routeConfirmed: false), .identifierAndRoute)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "", routeConfirmed: true), .identifier)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "prompt-textarea", routeConfirmed: false), .route)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "prompt-textarea", routeConfirmed: true), .confirmed)
        XCTAssertEqual(MacTab5DesktopAX.identityCheck(identifier: "search", routeConfirmed: true), .identifier)
    }
    func testNavigationRequiresCurrentExactLinkAndTrustedAppShell() {
        let task = UUID().uuidString.lowercased()
        let root = URL(fileURLWithPath: "/Applications/ChatGPT.app/Contents/Resources")
        let address = "file:///Applications/ChatGPT.app/Contents/Resources/webview/index.html#/threads/" + task
        XCTAssertTrue(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: "page", address: address, task: task, resourceRoot: root))
        for current in [nil, "false", "true", "step"] {
            XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: current, address: address, task: task, resourceRoot: root))
        }
        XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXButton", current: "page", address: address, task: task, resourceRoot: root))
        XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: "page", address: address, task: UUID().uuidString, resourceRoot: root))
        XCTAssertTrue(MacTab5DesktopAX.trustedShell(address, resourceRoot: root))
        for untrusted in ["https://example.com/threads/" + task, "file:///tmp/threads/" + task,
                          "file:///Applications/ChatGPT.app/Contents/Resources/../private/index.html", "codex://threads/" + task] {
            XCTAssertFalse(MacTab5DesktopAX.trustedShell(untrusted, resourceRoot: root))
        }
    }
    func testNavigationCanConfirmWithoutReadingOrAuthorizingAnEditor() throws {
        let fixture = Fixture()
        var backend = fixture.backend, reads = 0
        backend.navigation = { $0 == fixture.task }
        backend.read = { _ in reads += 1; return nil }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        let result = desktop.handle(request, device: "A")
        XCTAssertEqual(result.status, 200); XCTAssertEqual(result.body["foreground"] as? Bool, true)
        XCTAssertEqual(result.body["taskId"] as? String, fixture.task)
        XCTAssertNil(result.body["draftEmpty"]); XCTAssertEqual(reads, 0)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.navigationConfirmed.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200); XCTAssertEqual(fixture.opens, 1)
        let staged = desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "test"], device: "A")
        XCTAssertEqual(staged.status, 503); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testNavigationFailureCannotFallBackToAnUnrelatedEditor() throws {
        let fixture = Fixture()
        var backend = fixture.backend
        backend.navigation = { _ in false }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        let result = desktop.handle(["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString], device: "A")
        XCTAssertEqual(result.status, 409); XCTAssertNil(result.body["foreground"])
        XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testPackagedDesktopOriginAndExactLiveRoute() {
        let task = UUID().uuidString.lowercased()
        XCTAssertTrue(MacTab5DesktopAX.identifies("app://-/threads/" + task, task: task))
        XCTAssertTrue(MacTab5DesktopAX.trustedShell("app://-/index.html", resourceRoot: nil))
        // initialRoute is only the startup hint, never evidence of current selection.
        XCTAssertFalse(MacTab5DesktopAX.identifies("app://-/index.html?initialRoute=/threads/" + task, task: task))
        for origin in ["app://fs", "app://other", "app://user@-", "app://-:80"] {
            XCTAssertFalse(MacTab5DesktopAX.identifies(origin + "/threads/" + task, task: task))
            XCTAssertFalse(MacTab5DesktopAX.trustedShell(origin + "/index.html", resourceRoot: nil))
        }
        XCTAssertFalse(MacTab5DesktopAX.identifies("app://-/threads/" + task + "/settings", task: task))
    }
    func testCurrentLocalDesktopRouteRequiresExactTaskAndTrustedOrigin() {
        let task = UUID().uuidString.lowercased()
        for route in ["app://-/local/", "app://-/index.html#/local/"] {
            XCTAssertTrue(MacTab5DesktopAX.identifies(route + task, task: task))
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + UUID().uuidString, task: task))
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + task + "/settings", task: task))
        }
        for route in ["app://fs/local/", "app://user@-/local/", "app://-:80/local/", "app://other/local/",
                      "codex://local/", "https://external.example/local/", "app://-/remote/",
                      "app://-/nested/local/", "app://-/index.html?initialRoute=/local/"] {
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + task, task: task))
        }
        XCTAssertTrue(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: "page", address: "app://-/local/" + task, task: task, resourceRoot: nil))
        XCTAssertFalse(MacTab5DesktopAX.currentTaskLink(role: "AXLink", current: nil, address: "app://-/local/" + task, task: task, resourceRoot: nil))
    }
    func testCurrentSidebarRowRequiresCompleteUnambiguousExactTaskLinks() {
        let task = UUID().uuidString.lowercased(), other = UUID().uuidString.lowercased()
        let address = "app://-/local/" + task
        func matches(_ links: [String], role: String = "AXGroup", current: String? = "page", complete: Bool = true) -> Bool {
            MacTab5DesktopAX.currentTaskRow(role: role, current: current, addresses: links, complete: complete, task: task, resourceRoot: nil)
        }
        XCTAssertTrue(matches([address]))
        XCTAssertTrue(matches([address, address]))
        XCTAssertFalse(matches([address], current: nil))
        XCTAssertFalse(matches([address], role: "AXWebArea"))
        XCTAssertFalse(matches([address], complete: false))
        XCTAssertFalse(matches([]))
        XCTAssertFalse(matches([address, "app://-/local/" + other]))
        XCTAssertFalse(matches([address, "https://external.example/local/" + task]))
        XCTAssertFalse(matches(["app://-/index.html?initialRoute=/local/" + task]))
    }
    private final class Fixture {
        let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
        var value = "", writes = 0, opens = 0, sends = 0, foreground = true
        func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
            if method == "thread/list" { return ["data": [["id": task, "cwd": "/fixture", "name": "测试会话"]]] }
            if method == "thread/read" { return ["thread": ["id": task, "status": ["type": "idle"], "turns": []]] }
            if method == "turn/start" { sends += 1; return ["turn": ["id": turn]] }; return [:]
        }
        var backend: MacTab5DesktopBackend { .init(open: { _ in self.opens += 1; return self.foreground }, read: { _ in self.foreground ? self.value : nil }, write: { _, expected, next in guard self.foreground, self.value == expected else { return nil }; self.value = next; self.writes += 1; return next }) }
    }
    private func model(_ fixture: Fixture) throws -> MacTab5Codex {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString); addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 4, count: 32) }), enabled: { true }, sendEnabled: { true }, call: fixture.call); codex.refresh(); return codex
    }
    func testExactDesktopRouteAndOriginRequired() {
        let id = UUID().uuidString.lowercased()
        XCTAssertTrue(MacTab5DesktopAX.identifies("codex://threads/" + id, task: id))
        XCTAssertTrue(MacTab5DesktopAX.identifies("http://localhost:1234/threads/" + id, task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("https://untrusted.example/threads/" + id, task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("codex://threads/" + id + "-other", task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("codex://threads/" + UUID().uuidString, task: id))
        XCTAssertFalse(MacTab5DesktopAX.identifies("codex://untrusted/threads/" + id, task: id))
    }
    func testFileRouteUsesActualVerifiedAppResourceRoot() {
        let id = UUID().uuidString.lowercased(), root = URL(fileURLWithPath: "/Applications/ChatGPT.app/Contents/Resources")
        let route = "file:///Applications/ChatGPT.app/Contents/Resources/app/index.html#/threads/" + id
        XCTAssertFalse(MacTab5DesktopAX.identifies(route, task: id))
        XCTAssertTrue(MacTab5DesktopAX.identifies(route, task: id, resourceRoot: root))
        XCTAssertFalse(MacTab5DesktopAX.identifies(route, task: UUID().uuidString, resourceRoot: root))
        XCTAssertFalse(MacTab5DesktopAX.identifies(route, task: id, resourceRoot: URL(fileURLWithPath: "/Applications/Codex.app/Contents/Resources")))
        for route in ["file:///Applications/ChatGPT.app/Contents/Resources-Other/index.html#/threads/",
                      "file:///Applications/ChatGPT.app/Contents/Resources/../private/index.html#/threads/",
                      "file://untrusted/Applications/ChatGPT.app/Contents/Resources/app/index.html#/threads/"] {
            XCTAssertFalse(MacTab5DesktopAX.identifies(route + id, task: id, resourceRoot: root))
        }
    }
    func testUnconfirmedOpenReportsBoundedReasonWithoutAcceptingComposer() throws {
        let fixture = Fixture()
        var backend = fixture.backend
        backend.read = { _ in nil }; backend.diagnostic = { .identifier }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        let request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.identifier.rawValue)
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(fixture.opens, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testMissingPermissionDoesNotNavigateAndOldReceiptDoesNotRetryAfterGrant() throws {
        let fixture = Fixture()
        var allowed = false, backend = fixture.backend
        backend.preflight = { allowed ? nil : .permission }
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: backend)
        XCTAssertEqual(desktop.summary, MacTab5DesktopCheck.permission.rawValue)
        var request: [String: Any] = ["op": "open-recent", "taskId": fixture.task, "requestId": UUID().uuidString]
        let rejected = desktop.handle(request, device: "A")
        XCTAssertEqual(rejected.status, 409); XCTAssertNil(rejected.body["foreground"])
        XCTAssertEqual(fixture.opens, 0); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
        allowed = true
        XCTAssertEqual(desktop.handle(request, device: "A").status, 409)
        XCTAssertEqual(fixture.opens, 0)
        request["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(request, device: "A").status, 200)
        XCTAssertEqual(fixture.opens, 1); XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0)
    }
    func testStageAndSubmitExactlyOnceWithChangedComposerProtection() throws {
        let fixture = Fixture(), codex = try model(fixture), desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: fixture.backend)
        let stageID = UUID().uuidString, stage: [String: Any] = ["op": "stage-draft", "taskId": fixture.task, "requestId": stageID, "text": "确认执行"]
        XCTAssertEqual(desktop.handle(stage, device: "A").status, 200); XCTAssertEqual(desktop.handle(stage, device: "A").status, 200); XCTAssertEqual(fixture.writes, 1)
        let submit: [String: Any] = ["op": "submit-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "draftRequestId": stageID]
        fixture.value = "电脑另有修改"; XCTAssertEqual(desktop.handle(submit, device: "A").status, 409); XCTAssertEqual(fixture.sends, 0)
        fixture.value = "确认执行"
        var fresh = submit; fresh["requestId"] = UUID().uuidString
        XCTAssertEqual(desktop.handle(fresh, device: "A").status, 200); XCTAssertEqual(desktop.handle(fresh, device: "A").status, 200); XCTAssertEqual(fixture.sends, 1)
    }
    func testNoForegroundMeansNoTypingOrFalseSuccess() throws {
        let fixture = Fixture(); fixture.foreground = false
        let desktop = MacTab5Desktop(codex: try model(fixture), enabled: { true }, backend: fixture.backend)
        XCTAssertEqual(desktop.handle(["op": "open-recent", "requestId": UUID().uuidString], device: "A").status, 409)
        XCTAssertEqual(desktop.handle(["op": "stage-draft", "taskId": fixture.task, "requestId": UUID().uuidString, "text": "hello"], device: "A").status, 503); XCTAssertEqual(fixture.writes, 0)
    }
    func testReadOnlyConnectionRejectsDesktopSubmissionBeforeChangingComposer() throws {
        let fixture = Fixture(); fixture.value = "保留电脑输入"
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? FileManager.default.removeItem(at: root) }
        let codex = MacTab5Codex(store: .init(root: root, key: { Data(repeating: 4, count: 32) }), enabled: { true }, sendEnabled: { true }, canMutate: { false }, call: fixture.call)
        codex.refresh()
        let desktop = MacTab5Desktop(codex: codex, enabled: { true }, backend: fixture.backend)
        let response = desktop.handle(["op": "submit-draft", "taskId": fixture.task, "requestId": UUID().uuidString], device: "fixture")
        XCTAssertEqual(response.status, 403); XCTAssertEqual(fixture.value, "保留电脑输入")
        XCTAssertEqual(fixture.writes, 0); XCTAssertEqual(fixture.sends, 0); XCTAssertEqual(fixture.opens, 0)
    }
}
