import XCTest
@testable import AIBotBridge

final class MacTab5CodexSynchronizationTests: XCTestCase {
    private final class API {
        let task = UUID().uuidString.lowercased()
        let entered = DispatchSemaphore(value: 0), resume = DispatchSemaphore(value: 0)
        private let lock = NSLock()
        private var paused = false
        func pause() { lock.lock(); paused = true; lock.unlock() }
        func call(_ method: String, _ params: [String: Any]) throws -> [String: Any] {
            XCTAssertEqual(method, "thread/list")
            lock.lock(); let wait = paused; lock.unlock()
            if wait { entered.signal(); _ = resume.wait(timeout: .now() + 10) }
            return ["data": [["id": task, "cwd": "/synthetic", "name": "Synthetic chat"]]]
        }
    }
    private func storage(key: @escaping () throws -> Data = { Data(repeating: 9, count: 32) }) -> MacTab5PrivateStore {
        let root = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        addTeardownBlock { try? FileManager.default.removeItem(at: root) }
        return MacTab5PrivateStore(root: root, key: key)
    }
    func testForegroundDraftsAndCatalogCheckCompleteWhileRefreshIsBlocked() throws {
        let api = API(), store = storage()
        let codex = MacTab5Codex(store: store, enabled: { true }, call: api.call)
        codex.refresh(); api.pause()
        let background = DispatchGroup(), foreground = DispatchGroup()
        background.enter()
        DispatchQueue.global().async { codex.refresh(); background.leave() }
        XCTAssertEqual(api.entered.wait(timeout: .now() + 2), .success)
        var known = false, statuses: [Int] = [], first = "", second = ""
        foreground.enter()
        DispatchQueue.global().async {
            known = codex.contains(api.task)
            for (device, text) in [("A", "first draft"), ("B", "second draft")] {
                statuses.append(codex.handle(["op": "draft-save", "taskId": api.task, "requestId": UUID().uuidString, "message": text], device: device).status)
            }
            statuses.append(codex.handle(["op": "draft-load", "taskId": api.task, "requestId": UUID().uuidString], device: "A").status)
            first = (codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String ?? ""
            statuses.append(codex.handle(["op": "draft-load", "taskId": api.task, "requestId": UUID().uuidString], device: "B").status)
            second = (codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String ?? ""
            foreground.leave()
        }
        let completedBeforeRelease = foreground.wait(timeout: .now() + 2) == .success
        api.resume.signal()
        XCTAssertEqual(background.wait(timeout: .now() + 5), .success)
        XCTAssertEqual(foreground.wait(timeout: .now() + 5), .success)
        XCTAssertTrue(completedBeforeRelease, "Local draft and published-catalog access must not wait for remote refresh")
        XCTAssertTrue(known); XCTAssertEqual(statuses, [200, 200, 200, 200])
        XCTAssertEqual(first, "first draft"); XCTAssertEqual(second, "second draft")
        let saved = try MacTab5Protocol.object(XCTUnwrap(store.read("journal")))
        XCTAssertEqual((saved["drafts"] as? [String: Any])?.count, 2)
    }
    func testFastDraftPathStillValidatesConsentCatalogRequestAndDeviceBinding() throws {
        let api = API(); var enabled = true, now: TimeInterval = 0, keyReads = 0
        let codex = MacTab5Codex(store: storage(key: { keyReads += 1; return Data(repeating: 9, count: 32) }),
            enabled: { enabled }, clock: { now }, call: api.call)
        codex.refresh()
        func load(_ task: String, request: String = UUID().uuidString, device: String = "A") -> Int {
            codex.handle(["op": "draft-load", "taskId": task, "requestId": request], device: device).status
        }
        XCTAssertEqual(load(UUID().uuidString), 404); XCTAssertEqual(load(api.task, request: "invalid"), 400)
        enabled = false; XCTAssertEqual(load(api.task), 403); XCTAssertEqual(keyReads, 0); enabled = true
        XCTAssertEqual(codex.handle(["op": "draft-save", "taskId": api.task, "requestId": UUID().uuidString, "message": "unsent draft"], device: "A").status, 200)
        now = 31; XCTAssertFalse(codex.contains(api.task))
        // Stale display data must still allow recovery of an encrypted local draft.
        XCTAssertEqual(load(api.task), 200)
        XCTAssertEqual((codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String, "unsent draft")
        XCTAssertEqual(load(api.task, device: "B"), 200)
        XCTAssertEqual((codex.snapshot()?["draftView"] as? [String: Any])?["text"] as? String, "")
    }
}
