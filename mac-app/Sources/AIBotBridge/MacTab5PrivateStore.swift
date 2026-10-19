import Foundation
import CryptoKit
import Security

// Conversation drafts, delivery receipts and device photos are opt-in data.
// Encrypt before touching disk; a missing/unreadable key never replaces data.
final class MacTab5PrivateStore {
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
        let query: [String: Any] = [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: "io.github.yaoyouzhong.AI-bot", kSecAttrAccount as String: "tab5-private-v1"]
        var read = query; read[kSecReturnData as String] = true; read[kSecMatchLimit as String] = kSecMatchLimitOne
        var value: CFTypeRef?
        let status = SecItemCopyMatching(read as CFDictionary, &value)
        if status == errSecSuccess, let bytes = value as? Data, bytes.count == 32 { return bytes }
        guard status == errSecItemNotFound else { throw MacTab5Error.keychain }
        var bytes = Data(count: 32)
        let random = bytes.withUnsafeMutableBytes { SecRandomCopyBytes(kSecRandomDefault, $0.count, $0.baseAddress!) }
        guard random == errSecSuccess else { throw MacTab5Error.keychain }
        var item = query; item[kSecValueData as String] = bytes; item[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        let added = SecItemAdd(item as CFDictionary, nil)
        if added == errSecDuplicateItem { return try keychainKey() }
        guard added == errSecSuccess else { throw MacTab5Error.keychain }; return bytes
    }
}
