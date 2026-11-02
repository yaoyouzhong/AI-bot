import Foundation
import Security

// A single Keychain item; no credential payload is written to a regular file.
public struct BridgeCredentialSnapshot: Codable, Equatable {
    public static let maximumBytes = 1_048_576
    public var version = 1
    public var values: [String: Data]
    public init(values: [String: Data] = [:]) { self.values = values }
    public func encoded() throws -> Data {
        guard version == 1, values.count <= 64,
              values.allSatisfy({ BridgeKeychain.allowed($0.key) && !$0.value.isEmpty && $0.value.count <= 65_536 }) else { throw BridgeKeychain.Failure.invalid }
        let bytes = try JSONEncoder().encode(self)
        guard bytes.count <= Self.maximumBytes else { throw BridgeKeychain.Failure.invalid }
        return bytes
    }
    public static func decode(_ bytes: Data) throws -> Self {
        guard bytes.count <= maximumBytes else { throw BridgeKeychain.Failure.invalid }
        let value = try JSONDecoder().decode(Self.self, from: bytes)
        _ = try value.encoded()
        return value
    }
}

public final class BridgeCredentialVault {
    public static let account = "bridge-credentials-v1"
    public struct Backend {
        public let read: (String, Bool) throws -> Data?
        public let write: (String, Data, Bool) throws -> Void
        public let accounts: () throws -> [String]
        public let remove: (String) throws -> Void
        public init(read: @escaping (String, Bool) throws -> Data?, write: @escaping (String, Data, Bool) throws -> Void,
                    accounts: @escaping () throws -> [String], remove: @escaping (String) throws -> Void) {
            self.read = read; self.write = write; self.accounts = accounts; self.remove = remove
        }
    }
    private let backend: Backend
    public init(backend: Backend) { self.backend = backend }
    private func load(interactiveMigration: Bool) throws -> BridgeCredentialSnapshot {
        if let bytes = try backend.read(Self.account, true) { return try .decode(bytes) }
        let accounts = Set(try backend.accounts().filter(BridgeKeychain.allowed))
        guard accounts.count <= 64 else { throw BridgeKeychain.Failure.invalid }
        var values: [String: Data] = [:]
        for account in accounts.sorted() {
            // Do not turn a migration into a series of interactive prompts.
            do {
                guard let bytes = try backend.read(account, interactiveMigration) else { throw BridgeKeychain.Failure.invalid }
                values[account] = bytes
            } catch BridgeKeychain.Failure.status(let status) where !interactiveMigration && [errSecInteractionNotAllowed, errSecInteractionRequired, errSecAuthFailed].contains(status) {
                throw BridgeKeychain.Failure.migrationRequired
            }
        }
        let value = BridgeCredentialSnapshot(values: values)
        try backend.write(Self.account, value.encoded(), true)
        return value
    }
    // The caller holds the per-user cross-process lock for this transaction.
    public func perform(_ request: BridgeKeychain.Request) throws -> BridgeKeychain.Response {
        try request.validate()
        if request.operation == "probe" { return .init(status: errSecSuccess) }
        if request.operation == "verify-migration" {
            guard let bytes = try backend.read(Self.account, true) else { return .init(status: errSecItemNotFound) }
            let saved = try BridgeCredentialSnapshot.decode(bytes)
            let accounts = Set(try backend.accounts().filter(BridgeKeychain.allowed))
            guard accounts.count <= 64 else { throw BridgeKeychain.Failure.invalid }
            for account in accounts {
                guard let original = try backend.read(account, false), saved.values[account] == original else { return .init(status: errSecDecode) }
            }
            return .init(status: errSecSuccess)
        }
        var value = try load(interactiveMigration: request.operation == "migrate")
        if ["snapshot", "migrate"].contains(request.operation) { return .init(status: errSecSuccess, value: try value.encoded()) }
        if request.operation == "read" {
            return .init(status: value.values[request.account] == nil ? errSecItemNotFound : errSecSuccess, value: value.values[request.account])
        }
        if request.operation == "add", value.values[request.account] != nil { return .init(status: errSecDuplicateItem) }
        if request.operation == "remove" {
            // The only removable account is the saved unlock password.
            // An unsuccessful deletion never reports success or resurrects it.
            try backend.remove(request.account)
            value.values.removeValue(forKey: request.account)
        } else { value.values[request.account] = request.value! }
        try backend.write(Self.account, value.encoded(), false)
        return .init(status: errSecSuccess, value: try value.encoded())
    }
}
