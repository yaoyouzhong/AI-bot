import XCTest
@testable import AIBotBridge

final class MacTab5LiveControlTests: XCTestCase {
    func testLateSelectionRequestCannotReturnControlsToPreviousTask() {
        let other = UUID().uuidString.lowercased()
        let monitor = MacTab5LiveControl(enabled: { true }, mutable: { true }, sendEnabled: { true }, clock: { 0 }, read: { self.state($0) })
        monitor.select(task, issuedAt: 200); monitor.refresh()
        monitor.select(other, issuedAt: 100); monitor.refresh()
        XCTAssertEqual(monitor.snapshot()?["taskId"] as? String, task)
    }
    private let task = UUID().uuidString.lowercased(), turn = UUID().uuidString.lowercased()
    private func state(_ task: String, turn: String? = nil, running: Bool = true) -> MacCodexDesktopIPC.State {
        .init(task: task, runtime: running ? "active" : "idle", waiting: false, turn: turn ?? self.turn,
              status: running ? "inProgress" : "completed", cwd: "/synthetic",
              publicItems: [["type": "agentMessage", "text": "NOT_CONTROL_DATA"]], startedAt: 1)
    }
    func testRunningControlsExistBeforeReplyTextAndExpirePromptly() throws {
        var now = 0.0, running = true
        let monitor = MacTab5LiveControl(enabled: { true }, mutable: { true }, sendEnabled: { true }, clock: { now },
            read: { self.state($0, running: running) })
        monitor.select(task); monitor.refresh()
        XCTAssertEqual(monitor.snapshot()?["canInterrupt"] as? Bool, true)
        XCTAssertEqual(monitor.snapshot()?["turnId"] as? String, turn)
        let clear = String(decoding: try MacTab5Protocol.json(XCTUnwrap(monitor.snapshot())), as: UTF8.self)
        XCTAssertFalse(clear.contains("NOT_CONTROL_DATA")); XCTAssertFalse(clear.contains("/synthetic"))
        now = 1; running = false; monitor.refresh()
        XCTAssertEqual(monitor.snapshot()?["running"] as? Bool, false)
        XCTAssertEqual(monitor.snapshot()?["canSend"] as? Bool, true)
        now = 4.01; XCTAssertNil(monitor.snapshot())
    }
    func testTaskSwitchAndOlderObservationCannotResurrectRunningState() throws {
        var now = 0.0
        let other = UUID().uuidString.lowercased()
        let monitor = MacTab5LiveControl(enabled: { true }, mutable: { true }, sendEnabled: { true }, clock: { now }, read: { self.state($0) })
        monitor.select(task); let first = try XCTUnwrap(monitor.capture())
        now = 1; monitor.select(other); monitor.accept(first, state: state(task)); XCTAssertNil(monitor.snapshot())
        let older = try XCTUnwrap(monitor.capture())
        now = 2; let newer = try XCTUnwrap(monitor.capture()); monitor.accept(newer, state: state(other, running: false))
        now = 2.1; monitor.accept(older, state: state(other))
        XCTAssertEqual(monitor.snapshot()?["taskId"] as? String, other)
        XCTAssertEqual(monitor.snapshot()?["running"] as? Bool, false)
    }
    func testLeaseAndStopRejectLateReadAndDoNotPollAfterLeaving() throws {
        var now = 0.0, reads = 0
        let monitor = MacTab5LiveControl(enabled: { true }, mutable: { true }, sendEnabled: { true }, clock: { now }, read: { reads += 1; return self.state($0) })
        monitor.select(task); now = 14; let old = try XCTUnwrap(monitor.capture())
        now = 16; monitor.refresh(); XCTAssertEqual(reads, 0); XCTAssertNil(monitor.snapshot())
        monitor.select(task); monitor.accept(old, state: state(task)); XCTAssertNil(monitor.snapshot())
        let current = try XCTUnwrap(monitor.capture()); monitor.stop(); monitor.accept(current, state: state(task))
        monitor.refresh(); XCTAssertEqual(reads, 0); XCTAssertNil(monitor.snapshot())
    }
    func testFailureWrongTaskAndPermissionsCannotAdvertiseInterrupt() {
        var enabled = true, mutable = true, sending = true, fail = false, wrong = false
        let monitor = MacTab5LiveControl(enabled: { enabled }, mutable: { mutable }, sendEnabled: { sending }, clock: { 0 }, read: {
            if fail { throw MacCodexDesktopError.timeout }; return self.state(wrong ? UUID().uuidString.lowercased() : $0)
        })
        monitor.select(task); monitor.refresh(); XCTAssertEqual(monitor.snapshot()?["canInterrupt"] as? Bool, true)
        sending = false; XCTAssertEqual(monitor.snapshot()?["canInterrupt"] as? Bool, false)
        mutable = false; XCTAssertNil(monitor.snapshot()); mutable = true
        enabled = false; XCTAssertNil(monitor.snapshot()); enabled = true
        fail = true; monitor.refresh(); XCTAssertNil(monitor.snapshot())
        fail = false; wrong = true; monitor.refresh(); XCTAssertNil(monitor.snapshot())
    }
    func testSlowObservationIsAlreadyExpiredWhenItReturns() {
        var now = 0.0
        let monitor = MacTab5LiveControl(enabled: { true }, mutable: { true }, sendEnabled: { true }, clock: { now }, read: {
            now = 4; return self.state($0)
        })
        monitor.select(task); monitor.refresh(); XCTAssertNil(monitor.snapshot())
    }
    func testSelectedLiveControlsPublishWhilePublicHistoryIsBlocked() throws {
        var now = 0.0
        let entered = DispatchSemaphore(value: 0), resume = DispatchSemaphore(value: 0), done = DispatchGroup()
        let oldTurn = UUID().uuidString.lowercased(), otherTask = UUID().uuidString.lowercased()
        let temporary = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        addTeardownBlock { try? FileManager.default.removeItem(at: temporary) }
        let monitor = MacTab5LiveControl(enabled: { true }, mutable: { true }, sendEnabled: { true }, clock: { now }, read: { self.state($0) })
        let codex = MacTab5Codex(store: .init(root: temporary, key: { Data(repeating: 9, count: 32) }), enabled: { true }, sendEnabled: { true }, clock: { now }, controlMonitor: monitor, call: { method, _ in
            if method == "thread/list" { return ["data": [["id": self.task, "cwd": "/synthetic", "name": "Synthetic chat"],
                ["id": otherTask, "cwd": "/synthetic", "name": "Other chat"]]] }
            XCTAssertEqual(method, "thread/read"); entered.signal(); _ = resume.wait(timeout: .now() + 10)
            return ["thread": ["id": self.task, "status": ["type": "idle"], "turns": [["id": oldTurn, "status": "completed", "startedAt": 0,
                "items": [["type": "agentMessage", "text": "older public reply"]]]]]]
        })
        codex.refresh(); done.enter()
        DispatchQueue.global().async {
            _ = codex.handle(["op": "read", "taskId": self.task, "page": -1], device: "synthetic-device"); done.leave()
        }
        XCTAssertEqual(entered.wait(timeout: .now() + 2), .success)
        monitor.refresh()
        XCTAssertEqual((codex.snapshot()?["control"] as? [String: Any])?["canInterrupt"] as? Bool, true)
        XCTAssertNil(codex.snapshot()?["replyView"])
        XCTAssertEqual(codex.handle(["op": "draft-save", "taskId": otherTask, "requestId": UUID().uuidString, "message": "background local draft"], device: "synthetic-device").status, 200)
        XCTAssertEqual((codex.snapshot()?["control"] as? [String: Any])?["taskId"] as? String, task)
        resume.signal(); XCTAssertEqual(done.wait(timeout: .now() + 5), .success)
        XCTAssertEqual((codex.snapshot()?["control"] as? [String: Any])?["turnId"] as? String, turn)
        XCTAssertEqual((codex.snapshot()?["replyView"] as? [String: Any])?["turnId"] as? String, oldTurn)
        now = 31; monitor.select(task); monitor.refresh()
        XCTAssertTrue(codex.snapshot()?["control"] is NSNull)
    }
}
