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
    private let account = "tab5-pairing-v1"

    func load() throws -> MacTab5Pairing? {
        guard let bytes = try MacKeychainAccess.read(account) else { return nil }
        guard let record = try? JSONDecoder().decode(MacTab5Pairing.self, from: bytes) else { throw MacTab5Error.keychain }
        try record.validate(); return record
    }

    func save(_ record: MacTab5Pairing) throws {
        try record.validate()
        let bytes = try JSONEncoder().encode(record)
        try MacKeychainAccess.save(account, bytes)
    }
}
