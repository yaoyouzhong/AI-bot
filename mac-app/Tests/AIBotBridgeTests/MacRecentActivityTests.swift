import Foundation
import XCTest
@testable import AIBotBridge

final class MacRecentActivityTests: XCTestCase {
    func testLabelsDistinguishRecentActivityFromKnownState() {
        let now = Date(timeIntervalSince1970: 1_000)
        let recent = MacRecentActivity.Observation(modified: now.addingTimeInterval(-10), complete: true)
        XCTAssertEqual(MacRecentActivity.label(state: "offline", observation: recent, now: now), "近期有活动")
        XCTAssertEqual(MacRecentActivity.label(state: "working", observation: recent, now: now), "工作中")
        XCTAssertEqual(MacRecentActivity.label(state: "idle", observation: recent, now: now), "空闲")
        XCTAssertEqual(MacRecentActivity.label(state: "unknown", observation: .init(), now: now), "检测中")
        XCTAssertEqual(MacRecentActivity.label(state: "offline", observation: .init(modified: now.addingTimeInterval(-90), complete: true), now: now), "无近期活动")
        XCTAssertEqual(MacRecentActivity.label(state: "offline", observation: .init(modified: now.addingTimeInterval(1), complete: false), now: now), "检测中")
    }

    func testMetadataScanUsesIndependentDirectoriesAndIgnoresLinks() throws {
        let fm = FileManager.default
        let home = fm.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        defer { try? fm.removeItem(at: home) }
        let codex = home.appendingPathComponent(".codex/sessions")
        let claude = home.appendingPathComponent(".claude/projects")
        try fm.createDirectory(at: codex, withIntermediateDirectories: true)
        try fm.createDirectory(at: claude, withIntermediateDirectories: true)
        let file = codex.appendingPathComponent("session.jsonl")
        try Data("fixture".utf8).write(to: file)
        let earlier = Date(timeIntervalSince1970: 500)
        try fm.setAttributes([.modificationDate: earlier], ofItemAtPath: file.path)
        try fm.createSymbolicLink(at: claude.appendingPathComponent("link.jsonl"), withDestinationURL: file)
        let result = MacRecentActivity.inspect(home: home)
        XCTAssertTrue(result.codex.complete)
        XCTAssertEqual(result.codex.modified, earlier)
        XCTAssertTrue(result.claude.complete)
        XCTAssertNil(result.claude.modified)
    }

    func testScanDoesNotBlockCaller() {
        let entered = expectation(description: "background scan started")
        let release = DispatchSemaphore(value: 0)
        let activity = MacRecentActivity(scan: {
            entered.fulfill()
            _ = release.wait(timeout: .now() + 3)
            return .init()
        })
        let start = Date()
        XCTAssertFalse(activity.capture().codex.complete)
        XCTAssertLessThan(Date().timeIntervalSince(start), 0.5)
        wait(for: [entered], timeout: 1)
        release.signal()
    }
}
