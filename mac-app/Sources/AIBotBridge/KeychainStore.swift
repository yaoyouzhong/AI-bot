import Foundation
import Security

// Share one serialized Keychain read across startup, LAN requests and USB.
// Cache access failures too: background requests must not repeat an auth prompt.
// Only an explicit save or a new process retries after a denied read.
final class MacPairingTokenCache {
    private let lock = NSLock()
    private let load: () throws -> String?
    private let persist: (String) throws -> Void
    private let generate: () throws -> String
    private var loaded: Result<String?, Error>?

    init(load: @escaping () throws -> String?, persist: @escaping (String) throws -> Void, generate: @escaping () throws -> String) {
        self.load = load; self.persist = persist; self.generate = generate
    }
    private func loadLocked() throws -> String? {
        if let loaded { return try loaded.get() }
        let result = Result { try load() }; loaded = result
        return try result.get()
    }
    func read() -> String? {
        lock.lock(); defer { lock.unlock() }
        return try? loadLocked()
    }
    func save(_ token: String) throws {
        lock.lock(); defer { lock.unlock() }
        try persist(token); loaded = .success(token)
    }
    func loadOrCreate() throws -> String {
        lock.lock(); defer { lock.unlock() }
        if let existing = try loadLocked() {
            guard existing.utf8.count >= 32 else { throw PairingTokenStore.KeychainError.status(errSecDecode) }
            return existing
        }
        let token = try generate()
        try persist(token); loaded = .success(token)
        return token
    }
}

enum PairingTokenStore {
    private static let account = "pairing-token"
    private static let cache = MacPairingTokenCache(load: readKeychain, persist: saveKeychain, generate: generateToken)

    static func read() -> String? { cache.read() }
    static func save(_ token: String) throws { try cache.save(token) }
    static func loadOrCreate() throws -> String { try cache.loadOrCreate() }

    private static func readKeychain() throws -> String? {
        guard let data = try MacKeychainAccess.read(account) else { return nil }
        guard let token = String(data: data, encoding: .utf8) else { throw KeychainError.status(errSecDecode) }
        return token
    }

    private static func saveKeychain(_ token: String) throws {
        try MacKeychainAccess.save(account, Data(token.utf8))
    }

    private static func generateToken() throws -> String {
        var bytes = [UInt8](repeating: 0, count: 32)
        let status = bytes.withUnsafeMutableBytes { buffer in
            SecRandomCopyBytes(kSecRandomDefault, buffer.count, buffer.baseAddress!)
        }
        guard status == errSecSuccess else { throw KeychainError.status(status) }
        return Data(bytes).base64EncodedString()
    }

    enum KeychainError: Error { case status(OSStatus) }
}
