import Foundation
import SQLite3

// Match the Windows navigation boundary: an exact, full, globally unique local
// catalog title can identify the primary foreground document for opening only.
// Never use device-truncated titles, a recent/project subset or generic chrome.
enum MacCodexNavigationTitle {
    static func read(_ task: String, home: URL? = nil) -> String? {
        guard MacTab5Codex.id(task) == task else { return nil }
        let root = home ?? ProcessInfo.processInfo.environment["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ??
            FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
        let path = root.appendingPathComponent("sqlite/codex-dev.db")
        var database: OpaquePointer?, statement: OpaquePointer?
        guard sqlite3_open_v2(path.path, &database, SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX, nil) == SQLITE_OK,
              let database else { if let database { sqlite3_close(database) }; return nil }
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
        SELECT target.display_title FROM local_thread_catalog AS target
        WHERE target.host_id='local' AND target.thread_id=?1 AND COALESCE(target.missing_candidate,0)=0
          AND (SELECT COUNT(*) FROM local_thread_catalog AS other
               WHERE COALESCE(other.missing_candidate,0)=0
                 AND other.display_title=target.display_title COLLATE BINARY)=1
        """
        guard sqlite3_prepare_v2(database, sql, -1, &statement, nil) == SQLITE_OK, let statement else { return nil }
        let bound = task.withCString { sqlite3_bind_text(statement, 1, $0, -1, unsafeBitCast(-1, to: sqlite3_destructor_type.self)) }
        guard bound == SQLITE_OK, sqlite3_step(statement) == SQLITE_ROW,
              sqlite3_column_bytes(statement, 0) <= 8192, let value = sqlite3_column_text(statement, 0) else { return nil }
        let bytes = Data(bytes: value, count: Int(sqlite3_column_bytes(statement, 0)))
        guard !bytes.contains(0), let title = String(data: bytes, encoding: .utf8) else { return nil }
        guard sqlite3_step(statement) == SQLITE_DONE,
              !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              !["codex", "chatgpt", "openai codex"].contains(title.lowercased()) else { return nil }
        return title
    }
    static func matches(role: String, trustedPrimary: Bool, pageTitle: String?, uniqueTitle: String?) -> Bool {
        role == "AXWebArea" && trustedPrimary && uniqueTitle != nil && pageTitle == uniqueTitle
    }
}
