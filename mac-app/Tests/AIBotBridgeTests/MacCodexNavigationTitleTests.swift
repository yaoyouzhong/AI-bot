import Foundation
import SQLite3
import XCTest
@testable import AIBotBridge

final class MacCodexNavigationTitleTests: XCTestCase {
    func testDisplayNamesAllowDuplicatesButStayBoundToRequestedLocalIds() throws {
        let (home, db) = try fixture(), a = UUID().uuidString.lowercased(), b = UUID().uuidString.lowercased()
        let remote = UUID().uuidString.lowercased(), missing = UUID().uuidString.lowercased()
        insert(db, a, "shared preview title"); insert(db, b, "shared preview title")
        insert(db, remote, "remote title", host: "remote"); insert(db, missing, "removed", missing: 1)
        XCTAssertEqual(MacCodexCatalogNames.read([a, b, remote, missing], home: home), [a: "shared preview title", b: "shared preview title"])
        XCTAssertNil(MacCodexNavigationTitle.read(a, home: home)) // display is not identity proof
        let invalid = UUID().uuidString.lowercased(); insert(db, invalid, "part\0rest")
        XCTAssertNil(MacCodexCatalogNames.read([a, invalid], home: home)) // no partial publication
        XCTAssertNil(MacCodexCatalogNames.read(["not-an-id"], home: home))
        let absent = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        XCTAssertNil(MacCodexCatalogNames.read([a], home: absent))
        XCTAssertFalse(FileManager.default.fileExists(atPath: absent.path))
    }
    func testLoadedChatCompactLabelMustBeUniqueAcrossTheWholeCatalog() throws {
        let (home, db) = try fixture(), task = UUID().uuidString.lowercased()
        let prefix = String(repeating: "a", count: 59), full = prefix + String(repeating: "b", count: 20) + "…"
        insert(db, task, full)
        XCTAssertEqual(MacCodexNavigationTitle.read(task, home: home), full)
        XCTAssertEqual(MacCodexNavigationTitle.read(task, home: home, compact: true), prefix + "…")
        let generic = UUID().uuidString.lowercased(); insert(db, generic, " OpenAI  Codex ")
        XCTAssertEqual(MacCodexNavigationTitle.identity(generic, home: home, compact: true), .unavailable)
        for index in 0..<520 { insert(db, UUID().uuidString.lowercased(), "other \(index)") }
        // Different full names collide after the desktop's loaded-chat elision.
        insert(db, UUID().uuidString.lowercased(), prefix + "different ending", host: "remote")
        XCTAssertEqual(MacCodexNavigationTitle.read(task, home: home), full)
        XCTAssertNil(MacCodexNavigationTitle.read(task, home: home, compact: true))
        XCTAssertEqual(MacCodexNavigationTitle.identity(task, home: home, compact: true), .duplicate)
    }
    func testCompactLabelUsesUtf16BoundaryWithoutSplittingSurrogates() {
        XCTAssertEqual(MacCodexNavigationTitle.compactTitle("  short\nname  "), "short name")
        XCTAssertEqual(MacCodexNavigationTitle.compactTitle(String(repeating: "中", count: 61)), String(repeating: "中", count: 59) + "…")
        let valid = String(repeating: "a", count: 57) + "😀" + "tail"
        XCTAssertEqual(MacCodexNavigationTitle.compactTitle(valid), String(repeating: "a", count: 57) + "😀…")
        XCTAssertNil(MacCodexNavigationTitle.compactTitle(String(repeating: "a", count: 58) + "😀tail"))
    }
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
        XCTAssertEqual(MacCodexNavigationTitle.identity(task, home: home), .duplicate)
        let local = UUID().uuidString.lowercased()
        insert(db, local, "remote duplicate"); insert(db, UUID().uuidString.lowercased(), "remote duplicate", host: "remote")
        XCTAssertNil(MacCodexNavigationTitle.read(local, home: home))
    }
    func testMissingInvalidGenericAndNulTitlesFailClosedWithoutCreatingCatalog() throws {
        let missingHome = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        XCTAssertNil(MacCodexNavigationTitle.read(UUID().uuidString.lowercased(), home: missingHome))
        XCTAssertEqual(MacCodexNavigationTitle.identity(UUID().uuidString.lowercased(), home: missingHome), .unavailable)
        XCTAssertFalse(FileManager.default.fileExists(atPath: missingHome.path))
        let (home, db) = try fixture()
        for title in ["", "  ", "Codex", "ChatGPT", "  Codex  ", "part\0rest", String(repeating: "x", count: 8200)] {
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
    func testSelectedThreadRowNeedsCurrentStateAndUnambiguousFullAccessibleName() {
        let classes = ["sidebar-item", "data-[app-action-sidebar-thread-selected=true]:bg-primary-ghost-hover"]
        func matches(role: String = "AXButton", trusted: Bool = true, current: String? = "page", rowClasses: [String]? = nil,
                     title: String? = "full name", label: String? = "full name", expected: String? = "full name") -> Bool {
            MacCodexNavigationTitle.matchesSelectedRow(role: role, trusted: trusted, current: current, classes: rowClasses ?? classes,
                title: title, label: label, uniqueTitle: expected)
        }
        XCTAssertTrue(matches())
        XCTAssertTrue(matches(title: "", label: "full name"))
        // The same current thread row can use default/table visual variants.
        XCTAssertTrue(matches(rowClasses: [classes[1], "rounded-[var(--radius-token-row)]"]))
        XCTAssertFalse(matches(current: nil)); XCTAssertFalse(matches(current: "true"))
        XCTAssertFalse(matches(role: "AXGroup")); XCTAssertFalse(matches(trusted: false))
        XCTAssertFalse(matches(rowClasses: ["sidebar-item"])) // project rows do not qualify
        XCTAssertFalse(matches(title: "full", label: "full"))
        XCTAssertFalse(matches(title: "full name", label: "other name"))
        XCTAssertFalse(matches(title: nil, label: nil)); XCTAssertFalse(matches(expected: nil))
    }
    func testBreadthFirstTraversalFindsSidebarBeforeLongReplySubtree() {
        // A first child with thousands of nested reply nodes must not starve a
        // sibling navigation branch within the same visit/time budget.
        var queue = MacNavigationQueue("window"), visited: [String] = []
        while visited.count < 8, let node = queue.next() {
            visited.append(node)
            if node == "window" { queue.append(contentsOf: ["reply", "sidebar"]) }
            else if node == "reply" { queue.append(contentsOf: ["reply-0"]) }
            else if node == "sidebar" { queue.append(contentsOf: ["current-task"]) }
            else if node.hasPrefix("reply-") { queue.append(contentsOf: ["reply-\(visited.count)"]) }
            if node == "current-task" { break }
        }
        XCTAssertEqual(visited.last, "current-task")
        XCTAssertLessThan(visited.count, 8)
    }
}
