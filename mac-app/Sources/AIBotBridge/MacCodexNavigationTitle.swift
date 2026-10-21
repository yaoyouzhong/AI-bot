import Foundation
import SQLite3

// Match the Windows navigation boundary: an exact, full, globally unique local
// catalog title can identify the primary foreground document for opening only.
// Never use device-truncated titles, a recent/project subset or generic chrome.
enum MacCodexNavigationTitle {
    enum Identity: Equatable { case unavailable, unique(String), duplicate }
    static func read(_ task: String, home: URL? = nil) -> String? {
        if case .unique(let title) = identity(task, home: home) { return title }
        return nil
    }
    static func identity(_ task: String, home: URL? = nil) -> Identity {
        guard MacTab5Codex.id(task) == task else { return .unavailable }
        let root = home ?? ProcessInfo.processInfo.environment["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ??
            FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
        let path = root.appendingPathComponent("sqlite/codex-dev.db")
        var database: OpaquePointer?, statement: OpaquePointer?
        guard sqlite3_open_v2(path.path, &database, SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX, nil) == SQLITE_OK,
              let database else { if let database { sqlite3_close(database) }; return .unavailable }
        defer { if let statement { sqlite3_finalize(statement) }; sqlite3_close(database) }
        sqlite3_busy_timeout(database, 100)
        sqlite3_limit(database, SQLITE_LIMIT_LENGTH, 16_384)
        let deadline = UnsafeMutablePointer<TimeInterval>.allocate(capacity: 1)
        deadline.initialize(to: ProcessInfo.processInfo.systemUptime + 0.1)
        defer { sqlite3_progress_handler(database, 0, nil, nil); deadline.deinitialize(count: 1); deadline.deallocate() }
        sqlite3_progress_handler(database, 1000, { raw in
            guard let raw else { return 1 }
            return ProcessInfo.processInfo.systemUptime < raw.assumingMemoryBound(to: TimeInterval.self).pointee ? 0 : 1
        }, deadline)
        let sql = """
        SELECT target.display_title, (SELECT COUNT(*) FROM local_thread_catalog AS other
               WHERE COALESCE(other.missing_candidate,0)=0
                 AND other.display_title=target.display_title COLLATE BINARY)
        FROM local_thread_catalog AS target
        WHERE target.host_id='local' AND target.thread_id=?1 AND COALESCE(target.missing_candidate,0)=0
        """
        guard sqlite3_prepare_v2(database, sql, -1, &statement, nil) == SQLITE_OK, let statement else { return .unavailable }
        let bound = task.withCString { sqlite3_bind_text(statement, 1, $0, -1, unsafeBitCast(-1, to: sqlite3_destructor_type.self)) }
        guard bound == SQLITE_OK, sqlite3_step(statement) == SQLITE_ROW,
              sqlite3_column_bytes(statement, 0) <= 8192, let value = sqlite3_column_text(statement, 0) else { return .unavailable }
        let bytes = Data(bytes: value, count: Int(sqlite3_column_bytes(statement, 0)))
        guard !bytes.contains(0), let title = String(data: bytes, encoding: .utf8) else { return .unavailable }
        let count = sqlite3_column_int64(statement, 1)
        guard sqlite3_step(statement) == SQLITE_DONE,
              !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              !["codex", "chatgpt", "openai codex"].contains(title.lowercased()), count > 0 else { return .unavailable }
        return count == 1 ? .unique(title) : .duplicate
    }
    static func matches(role: String, trustedPrimary: Bool, pageTitle: String?, uniqueTitle: String?) -> Bool {
        role == "AXWebArea" && trustedPrimary && uniqueTitle != nil && pageTitle == uniqueTitle
    }
    static func matchesSelectedRow(role: String, trusted: Bool, current: String?, classes: [String],
                                   title: String?, label: String?, uniqueTitle: String?) -> Bool {
        // The installed thread-row caller supplies its full raw title as its
        // aria-label, even when the visible title is formatted/truncated.
        let names = [title, label].compactMap { $0 }.filter { !$0.isEmpty }
        return role == "AXButton" && trusted && current == "page" && uniqueTitle != nil &&
            classes.contains("sidebar-item") &&
            classes.contains("data-[app-action-sidebar-thread-selected=true]:bg-primary-ghost-hover") &&
            !names.isEmpty && names.allSatisfy { $0 == uniqueTitle }
    }
}

// Search shallow navigation siblings before descending into a long reply.
struct MacNavigationQueue<Element> {
    private var elements: [Element]
    private var index = 0
    init(_ first: Element) { elements = [first] }
    var hasPending: Bool { index < elements.count }
    mutating func next() -> Element? {
        guard hasPending else { return nil }
        defer { index += 1 }; return elements[index]
    }
    mutating func append(contentsOf children: [Element]) { elements.append(contentsOf: children) }
}
