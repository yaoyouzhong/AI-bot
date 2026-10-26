import Foundation

// Existing delta format, shared by BLE and USB using independent instances.
// Each baseline belongs to one live connection and advances only after its
// exact authenticated/host-owned whole-frame ACK.
struct MacTab5BLEDelta {
    private struct Snapshot {
        let device: String, session: String, sequence: Int64
        let data: [String: Any]
    }
    private var baseline: Snapshot?
    mutating func reset() { baseline = nil }

    static func supports(_ info: [String: Any], pair: MacTab5Pairing, nonce: String) -> Bool {
        guard MacTab5BLEStatus.supports(info, pair: pair, nonce: nonce),
              MacTab5Protocol.integer(info["telemetryVersion"]) == 3,
              let version = info["firmware"] as? String,
              let patch = Int(version.dropLast(3).split(separator: ".").last ?? "") else { return false }
        // This is the oldest maintainer receiver inspected for this Mac path.
        return patch >= 234
    }

    mutating func acknowledge(_ full: Data, sequence: Int64) {
        guard let next = Self.snapshot(full), next.sequence == sequence else { reset(); return }
        baseline = next
    }

    mutating func wire(_ full: Data, packed: Bool, supported: Bool) throws -> Data {
        let fallback = try MacTab5BLEStatus.wire(full, supported: packed)
        if !supported { reset(); return fallback }
        guard let before = baseline, let after = Self.snapshot(full),
              before.device == after.device, before.session == after.session, before.sequence < after.sequence,
              Self.fullOnly(before.data) == false, Self.fullOnly(after.data) == false else { return fallback }
        var changes: [[String: Any]] = []
        guard Self.diff(before.data, after.data, path: [], changes: &changes), Self.bounded(changes),
              let delta = try? MacTab5Protocol.json([
                "version": 1, "type": "tab5_delta", "deviceId": after.device,
                "session": after.session, "sequence": after.sequence,
                "baseSequence": before.sequence, "changes": changes]),
              let wire = try? MacTab5BLEStatus.wire(delta, supported: packed, allowDelta: true)
        else { return fallback }
        // The existing receiver unpacks before applying telemetry-v3 changes.
        // Compare complete wire envelopes: verbose paths may compress well even
        // when the raw delta is larger than the already packed full frame.
        return wire.count < fallback.count ? wire : fallback
    }

    private static func snapshot(_ bytes: Data) -> Snapshot? {
        guard !bytes.isEmpty, bytes.count <= MacTab5Protocol.maximumFrame - 28,
              let root = try? MacTab5Protocol.object(bytes), root["type"] as? String == "tab5_status",
              MacTab5Protocol.integer(root["version"]) == 1,
              let device = root["deviceId"] as? String, MacTab5Protocol.isHex(device, count: 12),
              let session = root["session"] as? String, MacTab5Protocol.isHex(session, count: 32),
              let sequence = MacTab5Protocol.integer(root["sequence"]), sequence > 0,
              let data = root["data"] as? [String: Any], data["codex"] is [String: Any], data["claude"] is [String: Any],
              bounded(data) else { return nil }
        return Snapshot(device: device, session: session, sequence: sequence, data: data)
    }
    private static func fullOnly(_ data: [String: Any]) -> Bool {
        // OTA has a separate full-frame consumer. Keep pending display commands
        // on that established path too, including transitions back to null.
        let command = (data["connectionHealth"] as? [String: Any])?["displayCommand"]
        return [data["ota"], command].contains { $0 != nil && !($0 is NSNull) }
    }
    private static func equal(_ lhs: Any, _ rhs: Any) -> Bool {
        // NSDictionary equality treats true and 1 as equal. JSON types must
        // remain distinct, including when nested inside atomic arrays.
        guard let a = try? JSONSerialization.data(withJSONObject: lhs, options: [.sortedKeys, .fragmentsAllowed]),
              let b = try? JSONSerialization.data(withJSONObject: rhs, options: [.sortedKeys, .fragmentsAllowed]) else { return false }
        return a == b
    }
    private static func diff(_ before: Any, _ after: Any, path: [String], changes: inout [[String: Any]]) -> Bool {
        if equal(before, after) { return true }
        if let a = before as? [String: Any], let b = after as? [String: Any] {
            for key in b.keys.sorted() {
                if let old = a[key] {
                    if !diff(old, b[key]!, path: path + [key], changes: &changes) { return false }
                } else if !add(path + [key], value: b[key], changes: &changes) { return false }
            }
            for key in a.keys.sorted() where b[key] == nil {
                if !add(path + [key], value: nil, changes: &changes) { return false }
            }
            return true
        }
        return add(path, value: after, changes: &changes)
    }
    private static func add(_ path: [String], value: Any?, changes: inout [[String: Any]]) -> Bool {
        guard (1...16).contains(path.count), changes.count < 256,
              path.allSatisfy({ !$0.isEmpty && $0.utf8.count <= 128 && !$0.contains("\0") }) else { return false }
        var change: [String: Any] = ["path": path]
        if let value { change["value"] = value } else { change["remove"] = true }
        changes.append(change); return true
    }
    private static func bounded(_ value: Any) -> Bool {
        var nodes = 0, strings = 0
        func walk(_ value: Any, depth: Int) -> Bool {
            nodes += 1
            guard depth <= 16, nodes <= 8192 else { return false }
            if let object = value as? [String: Any] {
                for (key, child) in object {
                    if key.contains("\0") { return false }
                    strings += key.utf8.count + 1
                    if strings > 65536 || !walk(child, depth: depth + 1) { return false }
                }
            } else if let array = value as? [Any] {
                for child in array { if !walk(child, depth: depth + 1) { return false } }
            } else if let string = value as? String {
                if string.contains("\0") { return false }
                strings += string.utf8.count + 1
            }
            return strings <= 65536
        }
        return walk(value, depth: 0)
    }
}
