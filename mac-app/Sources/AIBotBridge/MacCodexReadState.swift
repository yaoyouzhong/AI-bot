import Foundation

/// Desktop metadata only; never exports account claims or changes desktop state.
/// Calls are serialized by the Codex gate. A missing bucket is unknown, not read.
final class MacCodexReadState {
    struct Snapshot { let scope: String; let unread: Set<String> }
    private let load: () -> Snapshot?
    private let clock: () -> TimeInterval
    private var scope = ""
    private var observed: [String: (turn: String, since: TimeInterval)] = [:]
    private(set) var read: [String: String] = [:]
    init(load: @escaping () -> Snapshot? = { MacCodexReadState.local() }, clock: @escaping () -> TimeInterval = { ProcessInfo.processInfo.systemUptime }) {
        self.load = load; self.clock = clock
    }
    func synchronize(_ ready: [String: String]) -> [String: String] {
        observed = observed.filter { ready[$0.key] == $0.value.turn }
        read = read.filter { ready[$0.key] == $0.value }
        guard let snapshot = load() else { observed = [:]; return read }
        if scope != snapshot.scope { observed = [:]; read = [:]; scope = snapshot.scope }
        let now = clock()
        for (task, turn) in ready {
            let prior = observed[task]
            if prior == nil || now < prior!.since { observed[task] = (turn, now) }
            if snapshot.unread.contains(task) { observed[task] = (turn, now); read[task] = nil }
            else if let since = observed[task]?.since, now - since >= 5 { read[task] = turn }
        }
        return read
    }
    static func local() -> Snapshot? {
        let env = ProcessInfo.processInfo.environment
        guard env["CODEX_APP_SERVER_FORCE_CLI"] == "1" || (env["CODEX_APP_SERVER_WS_URL"] ?? "").isEmpty else { return nil }
        let home = env["CODEX_HOME"].map { URL(fileURLWithPath: $0) } ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".codex")
        func bounded(_ name: String) throws -> Data {
            let file = try FileHandle(forReadingFrom: home.appendingPathComponent(name)); defer { try? file.close() }
            let data = try file.read(upToCount: 16 * 1024 * 1024 + 1) ?? Data()
            guard data.count <= 16 * 1024 * 1024 else { throw MacTab5Error.invalidPacket }; return data
        }
        guard let auth = try? bounded("auth.json"), let state = try? bounded(".codex-global-state.json") else { return nil }
        return parse(auth: auth, state: state, now: Date().timeIntervalSince1970)
    }
    static func parse(auth: Data, state: Data, now: TimeInterval) -> Snapshot? {
        guard let root = try? JSONSerialization.jsonObject(with: auth) as? [String: Any],
              let token = (root["tokens"] as? [String: Any])?["access_token"] as? String else { return nil }
        let parts = token.split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3 else { return nil }
        var encoded = parts[1].replacingOccurrences(of: "-", with: "+").replacingOccurrences(of: "_", with: "/")
        encoded += String(repeating: "=", count: (4 - encoded.count % 4) % 4)
        guard let bytes = Data(base64Encoded: encoded),
              let claims = try? JSONSerialization.jsonObject(with: bytes) as? [String: Any],
              let expiry = MacTab5Protocol.integer(claims["exp"]), Double(expiry) > now,
              let identity = claims["https://api.openai.com/auth"] as? [String: Any],
              let account = (identity["chatgpt_account_id"] ?? identity["account_id"]) as? String, !account.isEmpty,
              let user = (identity["user_id"] ?? identity["chatgpt_user_id"]) as? String, !user.isEmpty,
              let identityBytes = try? JSONSerialization.data(withJSONObject: ["chatgpt", account, user], options: [.withoutEscapingSlashes]),
              let hostBytes = try? JSONSerialization.data(withJSONObject: ["local", "local", NSNull()], options: [.withoutEscapingSlashes]),
              let global = try? JSONSerialization.jsonObject(with: state) as? [String: Any],
              let readState = global["electron-thread-read-state-v1"] as? [String: Any],
              MacTab5Protocol.integer(readState["version"]) == 1 else { return nil }
        let key = MacTab5Protocol.sha256(identityBytes), host = "local:" + MacTab5Protocol.sha256(hostBytes)
        guard let identities = readState["unreadByIdentity"] as? [String: Any],
              let hosts = identities[key] as? [String: Any], let ids = hosts[host] as? [String],
              ids.count <= 8192, ids.allSatisfy({ UUID(uuidString: $0) != nil }) else { return nil }
        return .init(scope: key + ":" + host, unread: Set(ids.map { $0.lowercased() }))
    }
}
