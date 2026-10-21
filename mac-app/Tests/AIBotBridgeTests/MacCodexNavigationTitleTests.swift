import Foundation
import SQLite3
import XCTest
@testable import AIBotBridge

final class MacCodexNavigationTitleTests: XCTestCase {
    private func fixture() throws -> (URL, OpaquePointer) {
        let home = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: home.appendingPathComponent("sqlite"), withIntermediateDirectories: true)
        var database: OpaquePointer?
        XCTAssertEqual(sqlite3_open(home.appendingPathComponent("sqlite/codex-dev.db").path, &database), SQLITE_OK)
        let db = try XCTUnwrap(database)
        XCTAssertEqual(sqlite3_exec(db, "CREATE TABLE local_thread_catalog(host_id TEXT,thread_id TEXT,display_title TEXT,missing_candidate INTEGER)", nil, nil, nil), SQLITE_OK)
        addTeardownBlock { sqlite3_close(db); try? FileManager.default.removeItem(at: home) }
        return (home, db)
    }
    private func insert(_ db: OpaquePointer, _ task: String, _ title: String, host: String = "local", missing: Int = 0) {
        var statement: OpaquePointer?
        XCTAssertEqual(sqlite3_prepare_v2(db, "INSERT INTO local_thread_catalog VALUES(?1,?2,?3,?4)", -1, &statement, nil), SQLITE_OK)
        guard let statement else { return }; defer { sqlite3_finalize(statement) }
        for (index, value) in [host, task, title].enumerated() {
            value.utf8CString.withUnsafeBufferPointer {
                XCTAssertEqual(sqlite3_bind_text(statement, Int32(index + 1), $0.baseAddress, Int32($0.count - 1), unsafeBitCast(-1, to: sqlite3_destructor_type.self)), SQLITE_OK)
            }
        }
        XCTAssertEqual(sqlite3_bind_int(statement, 4, Int32(missing)), SQLITE_OK)
        XCTAssertEqual(sqlite3_step(statement), SQLITE_DONE)
    }
    func testFullTitlesWithTheSameDevicePrefixRemainDistinct() throws {
        let (home, db) = try fixture(), a = UUID().uuidString.lowercased(), b = UUID().uuidString.lowercased()
        let prefix = String(repeating: "相同的标题", count: 30)
        insert(db, a, prefix + "甲"); insert(db, b, prefix + "乙")
        XCTAssertEqual(MacCodexNavigationTitle.read(a, home: home), prefix + "甲")
        XCTAssertEqual(MacCodexNavigationTitle.read(b, home: home), prefix + "乙")
        XCTAssertFalse(MacCodexNavigationTitle.matches(role: "AXWebArea", trustedPrimary: true,
            pageTitle: String(prefix.prefix(80)), uniqueTitle: MacCodexNavigationTitle.read(a, home: home)))
    }
    func testDuplicateOutsideRecentSubsetOrOnAnotherHostCannotConfirm() throws {
        let (home, db) = try fixture(), task = UUID().uuidString.lowercased()
        insert(db, task, "duplicate")
        for index in 0..<520 { insert(db, UUID().uuidString.lowercased(), "other \(index)") }
        insert(db, UUID().uuidString.lowercased(), "duplicate")
        XCTAssertNil(MacCodexNavigationTitle.read(task, home: home))
        let local = UUID().uuidString.lowercased()
        insert(db, local, "remote duplicate"); insert(db, UUID().uuidString.lowercased(), "remote duplicate", host: "remote")
        XCTAssertNil(MacCodexNavigationTitle.read(local, home: home))
    }
    func testMissingInvalidGenericAndNulTitlesFailClosedWithoutCreatingCatalog() throws {
        let missingHome = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        XCTAssertNil(MacCodexNavigationTitle.read(UUID().uuidString.lowercased(), home: missingHome))
        XCTAssertFalse(FileManager.default.fileExists(atPath: missingHome.path))
        let (home, db) = try fixture()
        for title in ["", "  ", "Codex", "ChatGPT", "part\0rest", String(repeating: "x", count: 8200)] {
            let task = UUID().uuidString.lowercased(); insert(db, task, title)
            XCTAssertNil(MacCodexNavigationTitle.read(task, home: home))
        }
        let removed = UUID().uuidString.lowercased(); insert(db, removed, "removed", missing: 1)
        XCTAssertNil(MacCodexNavigationTitle.read(removed, home: home))
        XCTAssertNil(MacCodexNavigationTitle.read("bad' OR 1=1", home: home))
        XCTAssertEqual(sqlite3_exec(db, "DROP TABLE local_thread_catalog", nil, nil, nil), SQLITE_OK)
        XCTAssertNil(MacCodexNavigationTitle.read(UUID().uuidString.lowercased(), home: home))
    }
    func testOnlyTheTrustedPrimaryDocumentExactFullTitleQualifies() {
        XCTAssertTrue(MacCodexNavigationTitle.matches(role: "AXWebArea", trustedPrimary: true, pageTitle: "full title", uniqueTitle: "full title"))
        XCTAssertFalse(MacCodexNavigationTitle.matches(role: "AXWindow", trustedPrimary: true, pageTitle: "full title", uniqueTitle: "full title"))
        XCTAssertFalse(MacCodexNavigationTitle.matches(role: "AXWebArea", trustedPrimary: false, pageTitle: "full title", uniqueTitle: "full title"))
        XCTAssertFalse(MacCodexNavigationTitle.matches(role: "AXWebArea", trustedPrimary: true, pageTitle: "other", uniqueTitle: "full title"))
        XCTAssertFalse(MacCodexNavigationTitle.matches(role: "AXWebArea", trustedPrimary: true, pageTitle: nil, uniqueTitle: nil))
    }
}
