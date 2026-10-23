import Foundation
import Security

struct MacTab5Pairing: Codable, Equatable {
    let deviceId: String
    let usbPath: String
    let key: Data
    var confirmed: Bool

    func validate() throws {
        guard MacTab5Protocol.isHex(deviceId, count: 12), key.count == 32,
              usbPath.hasPrefix("/dev/cu."), !usbPath.dropFirst(5).contains("/"),
              !usbPath.utf8.contains(0) else { throw MacTab5Error.keychain }
    }
}

// A separate account from the ESP8266 LAN token. No TAB5 secret enters defaults.
final class MacTab5PairingStore {
    private let service = "io.github.yaoyouzhong.AI-bot"
    private let account = "tab5-pairing-v1"

    func load() throws -> MacTab5Pairing? {
        var query = key
        query[kSecReturnData as String] = true; query[kSecMatchLimit as String] = kSecMatchLimitOne
        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        if status == errSecItemNotFound { return nil }
        guard status == errSecSuccess, let bytes = result as? Data,
              let record = try? JSONDecoder().decode(MacTab5Pairing.self, from: bytes) else { throw MacTab5Error.keychain }
        try record.validate(); return record
    }

    func save(_ record: MacTab5Pairing) throws {
        try record.validate()
        let bytes = try JSONEncoder().encode(record)
        let status = SecItemUpdate(key as CFDictionary, [kSecValueData as String: bytes] as CFDictionary)
        if status == errSecItemNotFound {
            var item = key; item[kSecValueData as String] = bytes
            guard SecItemAdd(item as CFDictionary, nil) == errSecSuccess else { throw MacTab5Error.keychain }
        } else if status != errSecSuccess { throw MacTab5Error.keychain }
    }

    private var key: [String: Any] {
        [kSecClass as String: kSecClassGenericPassword, kSecAttrService as String: service, kSecAttrAccount as String: account]
    }
}
