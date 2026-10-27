import Foundation
import SQLite3

// User-facing title metadata only. The public list API can omit an unnamed
// chat's name even though the desktop catalog has its observed preview title.
enum MacCodexCatalogNames {
    static func read(_ tasks: Set<String>, home: URL? = nil) -> [String: String]? {
        guard !tasks.isEmpty else { return [:] }
        guard tasks.count <= 512, tasks.allSatisfy({ MacTab5Codex.id($0) == $0 }) else { return nil }
        let root = home ?? ProcessInfo.processInfo.environment["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ??
            FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
        var db: OpaquePointer?, statement: OpaquePointer?
        guard sqlite3_open_v2(root.appendingPathComponent("sqlite/codex-dev.db").path, &db, SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX, nil) == SQLITE_OK,
              let db else { if let db { sqlite3_close(db) }; return nil }
        defer { if let statement { sqlite3_finalize(statement) }; sqlite3_close(db) }
        sqlite3_busy_timeout(db, 100); sqlite3_limit(db, SQLITE_LIMIT_LENGTH, 16_384)
        let deadline = UnsafeMutablePointer<TimeInterval>.allocate(capacity: 1)
        deadline.initialize(to: ProcessInfo.processInfo.systemUptime + 0.1)
        defer { sqlite3_progress_handler(db, 0, nil, nil); deadline.deinitialize(count: 1); deadline.deallocate() }
        sqlite3_progress_handler(db, 1000, { context in
            guard let context else { return 1 }
            return ProcessInfo.processInfo.systemUptime < context.assumingMemoryBound(to: TimeInterval.self).pointee ? 0 : 1
        }, deadline)
        let sql = "SELECT thread_id,display_title FROM local_thread_catalog WHERE host_id='local' AND COALESCE(missing_candidate,0)=0"
        guard sqlite3_prepare_v2(db, sql, -1, &statement, nil) == SQLITE_OK, let statement else { return nil }
        var result: [String: String] = [:], visited = 0, step = sqlite3_step(statement)
        while step == SQLITE_ROW {
            visited += 1
            guard visited <= 8192, ProcessInfo.processInfo.systemUptime < deadline.pointee,
                  sqlite3_column_bytes(statement, 0) == 36,
                  let rawID = sqlite3_column_text(statement, 0) else { return nil }
            let id = String(cString: rawID)
            if tasks.contains(id) {
                guard result[id] == nil, sqlite3_column_bytes(statement, 1) <= 8192,
                      let raw = sqlite3_column_text(statement, 1) else { return nil }
                let bytes = Data(bytes: raw, count: Int(sqlite3_column_bytes(statement, 1)))
                guard !bytes.contains(0), let title = String(data: bytes, encoding: .utf8),
                      !title.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return nil }
                result[id] = title
            }
            step = sqlite3_step(statement)
        }
        return step == SQLITE_DONE ? result : nil
    }
}
