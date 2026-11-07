import Foundation

struct MacClaudeSessionSource: Codable, Equatable {
    let id: String, label: String, kind: String
    static let unknown = Self(id: "unknown", label: "来源未知", kind: "unknown")
    static let multiple = Self(id: "multiple", label: "多端打开", kind: "unknown")
    static let vscode = Self(id: "claude.vscode", label: "VS Code", kind: "ide")
    static let terminal = Self(id: "claude.terminal", label: "原生终端", kind: "terminal")
    static let background = Self(id: "claude.background", label: "后台会话", kind: "harness")

    var valid: Bool {
        (1...64).contains(id.utf8.count) && id.utf8.allSatisfy {
            (48...57).contains($0) || (65...90).contains($0) || (97...122).contains($0) || [45, 46, 95].contains($0)
        } && (1...48).contains(label.utf16.count) && label.utf8.count <= 80 &&
            label.unicodeScalars.allSatisfy {
                ![.control, .format, .lineSeparator, .paragraphSeparator].contains($0.properties.generalCategory)
            } && ["ide", "terminal", "harness", "unknown"].contains(kind)
    }
    var wire: [String: String] { ["id": id, "label": label, "kind": kind] }
}

// Only compiled integrations can register. Live ownership, including a
// conflicting claim, never comes from the persisted display cache.
struct MacClaudeHarnessAdapter {
    let source: MacClaudeSessionSource
    let discover: () -> [MacClaudeSessionObservation]
    let live: () -> Set<UUID>
    let resolve: (UUID) -> (any MacClaudeNativeSession)?
}

struct MacClaudeHarnesses {
    enum Owner {
        case unavailable, conflict, invalid
        case verified(any MacClaudeNativeSession)
        var client: (any MacClaudeNativeSession)? {
            if case .verified(let client) = self { return client }
            return nil
        }
    }
    private let adapters: [MacClaudeHarnessAdapter]

    init(_ adapters: [MacClaudeHarnessAdapter]) throws {
        let sources = adapters.map(\.source)
        guard sources.count <= 32, sources.allSatisfy({ $0.valid && !["unknown", "multiple"].contains($0.id) }),
              Set(sources.map(\.id)).count == sources.count else { throw MacTab5Error.invalidPacket }
        self.adapters = adapters
    }
    static let standard = try! Self([
        .init(source: .vscode, discover: MacClaudeNativeClient.newPanels,
              live: { MacClaudeNativeClient.live(entrypoint: "claude-vscode") },
              resolve: { MacClaudeNativeClient.find($0, entrypoint: "claude-vscode") }),
        .init(source: .terminal, discover: MacClaudeTerminalSession.newSessions,
              live: { MacClaudeNativeClient.live(entrypoint: "cli") },
              resolve: {
                  if let terminal = MacClaudeTerminalSession.find($0) { return terminal }
                  return MacClaudeNativeClient.find($0, entrypoint: "cli")
              }),
        .init(source: .background, discover: { [] },
              live: { MacClaudeBackground.live() },
              resolve: { MacClaudeBackground.find($0) })
    ])

    func adding(_ adapter: MacClaudeHarnessAdapter) throws -> Self { try Self(adapters + [adapter]) }
    func discover() -> [MacClaudeSessionObservation] {
        adapters.flatMap { adapter in
            adapter.discover().filter { row in
                if case .verified(let client) = owner(row.sessionID) { return client.source == adapter.source && client.isVerifiedBlank() }
                return false
            }
        }
    }
    func liveSessions() -> Set<UUID> { adapters.reduce(into: []) { $0.formUnion($1.live()) } }
    func owner(_ session: UUID) -> Owner {
        var matches: [any MacClaudeNativeSession] = []
        for adapter in adapters {
            guard let client = adapter.resolve(session) else { continue }
            guard client.session == session, client.source == adapter.source else { return .invalid }
            guard (try? client.scope()) != nil else { continue }
            matches.append(client)
        }
        if matches.count > 1 { return .conflict }
        return matches.first.map(Owner.verified) ?? .unavailable
    }
}

final class MacClaudeSourceCache {
    private let store: MacTab5PrivateStore, lock = NSLock()
    private var cache: [String: MacClaudeSessionSource]?
    private var dirty = false
    init(store: MacTab5PrivateStore) { self.store = store }

    func source(_ task: String, observed: MacClaudeSessionSource? = nil) -> MacClaudeSessionSource {
        lock.lock(); defer { lock.unlock() }
        if cache == nil {
            let decoded = (try? store.read("session-sources", limit: 256 * 1024))
                .flatMap { try? JSONDecoder().decode([String: MacClaudeSessionSource].self, from: $0) }
            cache = (decoded ?? [:]).count <= 512 ? decoded?.filter {
                UUID(uuidString: $0.key) != nil && $0.value.valid && !["unknown", "multiple"].contains($0.value.id)
            } ?? [:] : [:]
        }
        if let observed, observed.valid, !["unknown", "multiple"].contains(observed.id), cache?[task] != observed {
            if let values = cache, values.count >= 512, values[task] == nil, let first = values.keys.min() {
                cache?.removeValue(forKey: first)
            }
            cache?[task] = observed
            dirty = true
        }
        if dirty, let cache, (try? store.write("session-sources", JSONEncoder().encode(cache))) != nil { dirty = false }
        return observed ?? cache?[task] ?? .unknown
    }
}
