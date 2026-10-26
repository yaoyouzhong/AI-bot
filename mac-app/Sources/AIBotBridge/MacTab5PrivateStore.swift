import Foundation
import CryptoKit
import Security
import MacKeychainSupport

// Serialize concurrent first reads and retain both success and denial for the
// process lifetime. A denied read never generates a replacement encryption key.
final class MacPrivateKeyCache {
    private let lock = NSLock()
    private let load: () throws -> Data
    private var result: Result<Data, Error>?
    init(load: @escaping () throws -> Data) { self.load = load }
    func read() throws -> Data {
        lock.lock(); defer { lock.unlock() }
        if let result { return try result.get() }
        let loaded = Result { try load() }; result = loaded
        return try loaded.get()
    }
}

// Conversation drafts, delivery receipts and device photos are opt-in data.
// Encrypt before touching disk; a missing/unreadable key never replaces data.
final class MacTab5PrivateStore {
    private static let cachedKey = MacPrivateKeyCache(load: loadKeychainKey)
    private let root: URL
    private let key: () throws -> Data
    init(root: URL = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("AI-bot/Tab5/Private", isDirectory: true), key: @escaping () throws -> Data = MacTab5PrivateStore.keychainKey) { self.root = root; self.key = key }
    func read(_ name: String) throws -> Data? {
        let path = url(name)
        guard FileManager.default.fileExists(atPath: path.path) else { return nil }
        let sealed = try Data(contentsOf: path, options: .mappedIfSafe)
        guard sealed.count <= 8 * 1024 * 1024 else { throw MacTab5Error.invalidPacket }
        return try AES.GCM.open(AES.GCM.SealedBox(combined: sealed), using: SymmetricKey(data: key()), authenticating: Data(name.utf8))
    }
    func write(_ name: String, _ bytes: Data) throws {
        guard bytes.count <= 8 * 1024 * 1024 else { throw MacTab5Error.invalidPacket }
        let secret = try key()
        guard secret.count == 32 else { throw MacTab5Error.keychain }
        let sealed = try AES.GCM.seal(bytes, using: SymmetricKey(data: secret), authenticating: Data(name.utf8)).combined!
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true, attributes: [.posixPermissions: 0o700])
        try sealed.write(to: url(name), options: .atomic)
        try FileManager.default.setAttributes([.posixPermissions: 0o600], ofItemAtPath: url(name).path)
    }
    private func url(_ name: String) -> URL { root.appendingPathComponent(MacTab5Protocol.sha256(Data(name.utf8)) + ".sealed") }
    static func keychainKey() throws -> Data {
        try cachedKey.read()
    }
    private static func loadKeychainKey() throws -> Data {
        if let bytes = try MacKeychainAccess.read("tab5-private-v1") {
            guard bytes.count == 32 else { throw MacTab5Error.keychain }
            return bytes
        }
        var bytes = Data(count: 32)
        let random = bytes.withUnsafeMutableBytes { SecRandomCopyBytes(kSecRandomDefault, $0.count, $0.baseAddress!) }
        guard random == errSecSuccess else { throw MacTab5Error.keychain }
        do { try MacKeychainAccess.save("tab5-private-v1", bytes, addOnly: true) }
        catch BridgeKeychain.Failure.status(errSecDuplicateItem) { return try loadKeychainKey() }
        return bytes
    }
}
