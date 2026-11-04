import Foundation
import CryptoKit

// Only local resolution exposes real session IDs and cwd. Device rows use
// keyed aliases; neither a device path nor an arbitrary UUID can select a file.
final class MacClaudeCatalog {
    struct Entry {
        let alias: String, session: UUID, directory: String, title: String, updated: Int64, project: String, folder: String
    }
    private let root: URL, index: MacClaudeSessionIndex, key: () throws -> Data
    private let lock = NSLock()
    private var entries: [Entry] = []
    init(root: URL = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".claude/projects"),
         key: @escaping () throws -> Data = MacTab5PrivateStore.keychainKey) {
        self.root = root; index = MacClaudeSessionIndex(root:root); self.key = key
    }
    static func alias(_ value: String, key: Data, uuid: Bool = true) -> String {
        let digest = HMAC<SHA256>.authenticationCode(for:Data(value.utf8),using:SymmetricKey(data:key))
        let hex = digest.map { String(format:"%02x",$0) }.joined()
        guard uuid else { return String(hex.prefix(16)) }
        let value = Array(hex.prefix(32))
        return String(value[0..<8])+"-"+String(value[8..<12])+"-"+String(value[12..<16])+"-"+String(value[16..<20])+"-"+String(value[20..<32])
    }
    // Call on the workspace's serial background queue. A failed scan retains
    // the previous catalog; it cannot silently become an empty successful list.
    func refresh(now: Date = Date()) throws {
        _ = try FileManager.default.contentsOfDirectory(at:root,includingPropertiesForKeys:nil)
        let secret = try key()
        guard secret.count == 32 else { throw MacTab5Error.keychain }
        let rows = index.scanAll(now:now).compactMap { row -> Entry? in
            let normalized = row.directory.flatMap { directory -> String? in
                guard directory.hasPrefix("/"), !directory.contains("\0") else { return nil }
                return URL(fileURLWithPath:directory).standardizedFileURL.path
            }
            return .init(alias:Self.alias("claude:session:"+row.sessionID.uuidString.lowercased(),key:secret),session:row.sessionID,
                directory:normalized ?? "",title:row.title ?? "Claude Code",updated:Int64(row.lastActivity.timeIntervalSince1970),
                project:Self.alias("claude:project:"+(normalized ?? "unknown"),key:secret,uuid:false),folder:normalized.map { String(URL(fileURLWithPath:$0).lastPathComponent.prefix(48)) } ?? "项目待确认")
        }
        lock.lock(); entries = rows; lock.unlock()
    }
    func snapshot() -> [Entry] { lock.lock(); defer { lock.unlock() }; return entries }
    func resolve(_ alias: String) -> Entry? { lock.lock(); defer { lock.unlock() }; return entries.first { $0.alias == alias.lowercased() } }
}
