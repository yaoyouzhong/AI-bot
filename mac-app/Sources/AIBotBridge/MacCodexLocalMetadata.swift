import AppKit
import Darwin
import Foundation
import SQLite3

enum MacCodexLocalMetadata {
    static var home: URL {
        ProcessInfo.processInfo.environment["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ??
            FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
    }

    // Display-only fallback when the desktop IPC is not listening. The read-
    // only App Server marks unfinished turns as interrupted, so use the exact
    // persisted root turn and verify that Codex still holds its writer lock.
    static func displayActivity(task: String, home: URL = MacCodexLocalMetadata.home) -> String? {
        guard let launch = NSRunningApplication.runningApplications(withBundleIdentifier: "com.openai.codex")
            .compactMap(\.launchDate).max() else { return nil }
        return displayActivity(task: task, home: home, desktopLaunch: launch, now: Date())
    }

    static func displayActivity(task: String, home: URL, desktopLaunch: Date, now: Date) -> String? {
        guard MacTab5Codex.id(task) == task else { return nil }
        let turns = rows(home.appendingPathComponent("thread_history_1.sqlite"), """
            SELECT status,started_at,completed_at FROM thread_turns
            WHERE thread_id=?1 AND root_turn_id=turn_id
            ORDER BY rollout_ordinal DESC LIMIT 1
            """, binding: task)
        guard let turn = turns.first, let started = Int64(turn[1]),
              started >= Int64(desktopLaunch.timeIntervalSince1970),
              started <= Int64(now.timeIntervalSince1970) else { return nil }

        let lock = home.appendingPathComponent("thread-writer-locks").appendingPathComponent(task + ".lock")
        let fd = open(lock.path, O_RDONLY | O_CLOEXEC | O_NOFOLLOW | O_NONBLOCK)
        guard fd >= 0 else { return nil }
        defer { close(fd) }
        var info = stat()
        guard fstat(fd, &info) == 0, info.st_mode & S_IFMT == S_IFREG, info.st_uid == getuid(),
              flock(fd, LOCK_SH | LOCK_NB) == -1, errno == EWOULDBLOCK else { return nil }

        if turn[0] == "inProgress", turn[2].isEmpty { return "working" }
        if ["completed", "interrupted", "failed"].contains(turn[0]),
           let ended = Int64(turn[2]), (started...Int64(now.timeIntervalSince1970)).contains(ended) { return "idle" }
        return nil
    }

    static func rows(_ database: URL, _ sql: String, binding: String? = nil) -> [[String]] {
        var db: OpaquePointer?, statement: OpaquePointer?
        guard sqlite3_open_v2(database.path, &db, SQLITE_OPEN_READONLY | SQLITE_OPEN_NOMUTEX, nil) == SQLITE_OK,
              let db else { if let db { sqlite3_close(db) }; return [] }
        defer { sqlite3_finalize(statement); sqlite3_close(db) }
        sqlite3_busy_timeout(db, 50)
        sqlite3_limit(db, SQLITE_LIMIT_LENGTH, 64 * 1024)
        let deadline = ProcessInfo.processInfo.systemUptime + 0.1
        var limit = deadline
        return withUnsafeMutablePointer(to: &limit) { pointer in
            sqlite3_progress_handler(db, 1000, { context in
                guard let context else { return 1 }
                return ProcessInfo.processInfo.systemUptime < context.assumingMemoryBound(to: Double.self).pointee ? 0 : 1
            }, pointer)
            defer { sqlite3_progress_handler(db, 0, nil, nil) }
            guard sqlite3_prepare_v2(db, sql, -1, &statement, nil) == SQLITE_OK else { return [] }
            if let binding {
                let transient = unsafeBitCast(-1, to: sqlite3_destructor_type.self)
                guard sqlite3_bind_text(statement, 1, binding, -1, transient) == SQLITE_OK else { return [] }
            }
            var result: [[String]] = []
            var step = sqlite3_step(statement)
            while step == SQLITE_ROW {
                guard result.count < 8192, ProcessInfo.processInfo.systemUptime < deadline else { return [] }
                result.append((0..<sqlite3_column_count(statement)).map { column in
                    sqlite3_column_text(statement, column).map { String(cString: $0) } ?? ""
                })
                step = sqlite3_step(statement)
            }
            return step == SQLITE_DONE ? result : []
        }
    }

    static func legacyAssignments(_ state: [String: Any], home: URL) -> [String: String] {
        let assignments = state["thread-project-assignments"] as? [String: [String: Any]] ?? [:]
        let hosts = state["thread-project-membership-host-ids"] as? [String: Any] ?? [:]
        let migrations = state["app-server-project-id-by-legacy-project-id-by-host"] as? [String: [String: String]]
        let mapped = migrations?["local:" + home.standardizedFileURL.path] ?? [:]
        let detached = Set((state["projectless-thread-ids"] as? [String] ?? []).compactMap { MacTab5Codex.id($0) })
        var result: [String: String] = [:]
        for (key, assignment) in assignments {
            guard let task = MacTab5Codex.id(key), !detached.contains(task),
                  hosts[key] == nil || hosts[key] as? String == "local",
                  assignment["projectKind"] as? String == "local",
                  let raw = assignment["projectId"] as? String, let id = MacTab5Codex.id(raw) else { continue }
            result[task] = MacTab5Codex.id(mapped[raw] ?? mapped[id]) ?? id
        }
        return result
    }

    struct Projects {
        let local: [String: String], desktop: [String: String], legacy: [String: String]
        let saved: [String: MacCodexProjectIdentity.Value], roots: [(String, MacCodexProjectIdentity.Value)]
        let excluded: Set<String>
        let worktreePrefix: String

        private func hasWorkspaceMembership(cwd: String, identity: MacCodexProjectIdentity.Value) -> Bool {
            guard cwd.hasPrefix("/") else { return false }
            let directory = URL(fileURLWithPath: cwd).standardizedFileURL
            if directory.path.hasPrefix(worktreePrefix) { return true }
            var folder = directory
            while true {
                if roots.contains(where: { $0.0 == folder.path }) { return true }
                // Stop at a separate repository rather than inheriting a
                // parent's project just because its path is nested there.
                if FileManager.default.fileExists(atPath: folder.appendingPathComponent(".git").path) {
                    let key = folder == directory ? identity.key : MacCodexProjectIdentity.resolve(folder.path).key
                    return roots.contains { $0.1.key == key }
                }
                let parent = folder.deletingLastPathComponent()
                if parent.path == folder.path || folder.path == "/" { return false }
                folder = parent
            }
        }

        func match(task: String, cwd: String, identity: MacCodexProjectIdentity.Value) -> MacCodexProjectIdentity.Value? {
            let stateProject = local[task].flatMap { saved[$0] }
            let desktopProject = desktop[task].flatMap { saved[$0] } ?? legacy[task].flatMap { saved[$0] }
            let path = URL(fileURLWithPath: cwd).standardizedFileURL.path
            let workspace = cwd.hasPrefix("/") ? (roots.first { path == $0.0 || path.hasPrefix($0.0 + "/") }?.1 ??
                roots.first { $0.1.key == identity.key }?.1) : nil
            // App Server can copy a project ID into a verification checkout.
            // A desktop assignment or registered workspace must corroborate it.
            if let stateProject {
                guard desktopProject != nil || hasWorkspaceMembership(cwd: cwd, identity: identity) else { return nil }
                return stateProject
            }
            // Keep Mac's existing directory fallback for unassigned sessions.
            return desktopProject ?? workspace ?? identity
        }
    }

    static func projects(home: URL = MacCodexLocalMetadata.home) -> Projects {
        let stateDB = home.appendingPathComponent("state_5.sqlite")
        let catalog = MacCodexCatalogNames.databaseURL(root: home)
        let statePath = home.appendingPathComponent(".codex-global-state.json")
        var state: [String: Any] = [:]
        if let handle = try? FileHandle(forReadingFrom: statePath) {
            defer { try? handle.close() }
            let limit = 16 * 1024 * 1024
            if let data = try? handle.read(upToCount: limit + 1), data.count <= limit {
                state = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any] ?? [:]
            }
        }
        let local = rows(stateDB, "SELECT id,project_id FROM threads WHERE archived=0 ORDER BY updated_at DESC LIMIT 512")
        let desktop = rows(catalog, "SELECT thread_id,project_id FROM local_thread_catalog WHERE host_id='local' AND COALESCE(missing_candidate,0)=0 ORDER BY source_updated_at DESC LIMIT 512")
        let remote = rows(catalog, "SELECT thread_id,host_id FROM local_thread_catalog WHERE COALESCE(missing_candidate,0)=0 LIMIT 8192")
        let archived = rows(stateDB, "SELECT id FROM threads WHERE archived!=0 ORDER BY updated_at DESC LIMIT 8192")
        func assignments(_ rows: [[String]]) -> [String: String] {
            var result: [String: String] = [:]
            for row in rows {
                if let task = MacTab5Codex.id(row[0]), let project = MacTab5Codex.id(row[1]), result[task] == nil {
                    result[task] = project
                }
            }
            return result
        }
        var entries: [(id: String, name: String, path: String, key: String)] = []
        for row in rows(stateDB, "SELECT p.id,p.name,r.path FROM projects p JOIN project_roots r ON r.project_id=p.id ORDER BY p.position,r.position LIMIT 512") {
            guard let id = MacTab5Codex.id(row[0]), !row[1].trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
                  row[2].hasPrefix("/") else { continue }
            let path = URL(fileURLWithPath: row[2]).standardizedFileURL.path
            let name = row[1].components(separatedBy: .controlCharacters).joined(separator: " ")
            entries.append((id, name, path, MacCodexProjectIdentity.resolve(path).key))
        }
        var canonical: [String: MacCodexProjectIdentity.Value] = [:]
        for key in Set(entries.map(\.key)) {
            let group = entries.filter { $0.key == key }
            if let chosen = group.first(where: { $0.path == key }) ?? group.first {
                canonical[key] = .init(key: key, name: chosen.name, parent: URL(fileURLWithPath: chosen.path).deletingLastPathComponent().lastPathComponent)
            }
        }
        var saved: [String: MacCodexProjectIdentity.Value] = [:]
        let roots = entries.compactMap { entry -> (String, MacCodexProjectIdentity.Value)? in
            guard let project = canonical[entry.key] else { return nil }
            if saved[entry.id] == nil { saved[entry.id] = project }
            return (entry.path, project)
        }.sorted { $0.0.count > $1.0.count }
        let localIDs = Set(remote.filter { $0[1] == "local" }.map { $0[0].lowercased() })
        let remoteOnly = Set(remote.filter { $0[1] != "local" }.map { $0[0].lowercased() }).subtracting(localIDs)
        return Projects(local: assignments(local), desktop: assignments(desktop), legacy: legacyAssignments(state, home: home),
                        saved: saved, roots: roots, excluded: remoteOnly.union(archived.map { $0[0].lowercased() }),
                        worktreePrefix: home.appendingPathComponent("worktrees").standardizedFileURL.path + "/")
    }
}
