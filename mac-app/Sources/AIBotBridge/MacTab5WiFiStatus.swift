import Foundation

enum MacTab5WiFiStatus {
    // Existing packed Wi-Fi protocol. Require proof under this request's
    // nonce; a previously observed USB/BLE version cannot enable it.
    static func supports(headers: [String:String], pair: MacTab5Pairing, nonce: String) -> Bool {
        guard pair.confirmed, MacTab5Protocol.isHex(nonce,count:32),
              let version = headers["x-tab5-firmware"], version.hasSuffix("-ui"),
              let proof = headers["x-tab5-firmware-proof"],
              MacTab5Protocol.verify(key:pair.key,message:"FIRMWARE|\(nonce)|\(version)",proof:proof) else { return false }
        let parts = version.dropLast(3).split(separator:".",omittingEmptySubsequences:false)
        guard parts.count == 3, parts[0] == "0", parts[1] == "2", let patch = Int(parts[2]),
              String(patch) == parts[2], patch >= 42 else { return false }
        return true
    }
    static func wire(_ full: Data, headers: [String:String], pair: MacTab5Pairing, nonce: String) throws -> Data {
        try MacTab5BLEStatus.wire(full,supported:supports(headers:headers,pair:pair,nonce:nonce))
    }
}
