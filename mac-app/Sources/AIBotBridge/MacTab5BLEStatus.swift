import Foundation
import zlib

// Existing tab5_packed format; the authenticated BLE session or host-owned
// USB connection negotiates support. The original expanded-size limit applies.
enum MacTab5BLEStatus {
    static func supports(_ root: [String: Any], pair: MacTab5Pairing, nonce: String) -> Bool {
        guard pair.confirmed, MacTab5Protocol.isHex(nonce, count: 32), root["deviceId"] as? String == pair.deviceId, root["nonce"] as? String == nonce,
              let ack = MacTab5Protocol.integer(root["ack"]), ack >= 0,
              let proof = root["proof"] as? String, MacTab5Protocol.verify(key: pair.key, message: "\(nonce)|\(ack)", proof: proof),
              let telemetry = MacTab5Protocol.integer(root["telemetryVersion"]), [2, 3].contains(telemetry),
              let version = root["firmware"] as? String, version.hasSuffix("-ui"),
              let firmwareProof = root["firmwareProof"] as? String,
              MacTab5Protocol.verify(key: pair.key, message: "FIRMWARE|\(nonce)|\(version)", proof: firmwareProof) else { return false }
        let parts = version.dropLast(3).split(separator: ".", omittingEmptySubsequences: false)
        guard parts.count == 3, parts[0] == "0", parts[1] == "2", let patch = Int(parts[2]),
              String(patch) == parts[2], patch >= 42 else { return false }
        return true
    }
    static func wire(_ full: Data, supported: Bool, allowDelta: Bool = false) throws -> Data {
        guard !full.isEmpty, full.count <= MacTab5Protocol.maximumFrame - 28 else { throw MacTab5Error.invalidPacket }
        guard supported, let root = try? MacTab5Protocol.object(full),
              MacTab5Protocol.integer(root["version"]) == 1, let sequence = MacTab5Protocol.integer(root["sequence"]),
              sequence > 0 else { return full }
        if root["type"] as? String != "tab5_status" {
            // The caller must have authenticated telemetry-v3 support and an
            // ACKed baseline. Packing never creates or advances that baseline.
            guard allowDelta, root["type"] as? String == "tab5_delta",
                  let base = MacTab5Protocol.integer(root["baseSequence"]), base > 0, base < sequence else { return full }
        }
        var compressed = [UInt8](repeating: 0, count: Int(compressBound(uLong(full.count))))
        var count = uLongf(compressed.count)
        let result = full.withUnsafeBytes { (source: UnsafeRawBufferPointer) in
            compressed.withUnsafeMutableBufferPointer { target in
                compress2(target.baseAddress, &count, source.bindMemory(to: UInt8.self).baseAddress, uLong(source.count), Z_BEST_SPEED)
            }
        }
        guard result == Z_OK, count > 0 else { return full }
        let packed = try MacTab5Protocol.json(["version": 1, "type": "tab5_packed", "sequence": sequence,
            "size": full.count, "payload": Data(compressed.prefix(Int(count))).base64EncodedString()])
        return packed.count < full.count ? packed : full
    }
}
