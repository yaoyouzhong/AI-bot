import Foundation

// Only group worktrees when their own Git metadata points to the same common
// repository. Unreadable metadata falls back to the individual directory.
enum MacCodexProjectIdentity {
    struct Value { let key: String, name: String, parent: String }

    static func resolve(_ path: String) -> Value {
        let directory = URL(fileURLWithPath: path).standardizedFileURL.resolvingSymlinksInPath()
        func readSmall(_ url: URL) -> String? {
            guard let handle = try? FileHandle(forReadingFrom: url) else { return nil }
            defer { try? handle.close() }
            guard let data = try? handle.read(upToCount: 4097), data.count <= 4096 else { return nil }
            return String(data: data, encoding: .utf8)
        }
        func fallback() -> Value {
            .init(key: directory.path, name: directory.lastPathComponent,
                  parent: directory.deletingLastPathComponent().lastPathComponent)
        }
        let dotGit = directory.appendingPathComponent(".git")
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: dotGit.path, isDirectory: &isDirectory) else { return fallback() }
        let common: URL
        if isDirectory.boolValue { common = dotGit.resolvingSymlinksInPath() }
        else {
            guard let text = readSmall(dotGit), text.hasPrefix("gitdir: ") else { return fallback() }
            let gitPath = text.dropFirst(8).trimmingCharacters(in: .whitespacesAndNewlines)
            guard !gitPath.isEmpty, !gitPath.contains("\n"), !gitPath.contains("\0") else { return fallback() }
            let gitDir = URL(fileURLWithPath: gitPath, relativeTo: directory).standardizedFileURL.resolvingSymlinksInPath()
            let commonFile = gitDir.appendingPathComponent("commondir")
            guard let value = readSmall(commonFile)?.trimmingCharacters(in: .whitespacesAndNewlines),
                  !value.isEmpty, !value.contains("\n"), !value.contains("\0") else { return fallback() }
            common = URL(fileURLWithPath: value, relativeTo: gitDir).standardizedFileURL.resolvingSymlinksInPath()
            guard gitDir.deletingLastPathComponent() == common.appendingPathComponent("worktrees"),
                  let backlink = readSmall(gitDir.appendingPathComponent("gitdir"))?.trimmingCharacters(in: .whitespacesAndNewlines),
                  URL(fileURLWithPath: backlink, relativeTo: gitDir).standardizedFileURL.resolvingSymlinksInPath() == dotGit.resolvingSymlinksInPath()
            else { return fallback() }
        }
        var isCommonDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: common.path, isDirectory: &isCommonDirectory),
              isCommonDirectory.boolValue else { return fallback() }
        let root = common.lastPathComponent == ".git" ? common.deletingLastPathComponent() : directory
        // Preserve the existing main-directory project ID whenever the common
        // Git directory is a normal checkout's .git directory.
        let key = common.lastPathComponent == ".git" ? root.path : common.path
        return .init(key: key, name: root.lastPathComponent,
                     parent: root.deletingLastPathComponent().lastPathComponent)
    }
}
