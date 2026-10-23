import Foundation
import CoreFoundation

struct MacTab5DisplaySettings: Codable, Equatable {
    var revision: String
    var brightness: Int
    var volume: Int
    var muted: Bool
    var selected: Int
    var cycle: Bool
    var interval: Int
    var saverMinutes: Int
    var alerts: Bool
    var enabled: [Int]
    var order: [Int]

    static let pages = ["总览", "模型额度", "天气", "行情", "电脑状态", "音乐", "桌宠", "时钟"]

    func validate() throws {
        guard MacTab5Protocol.isHex(revision, count: 8), (10...100).contains(brightness), (0...100).contains(volume),
              (-1...7).contains(selected), [10, 15, 30, 60].contains(interval), [0, 1, 5, 10, 30, 60].contains(saverMinutes),
              enabled.count == 8, enabled.allSatisfy({ $0 == 0 || $0 == 1 }), enabled.contains(1),
              order.sorted() == Array(0..<8), !cycle || selected == -1 else { throw MacTab5Error.invalidSettings }
    }
    var object: [String: Any] {
        ["revision": revision, "brightness": brightness, "volume": volume, "muted": muted,
         "selected": selected, "cycle": cycle, "interval": interval, "saverMinutes": saverMinutes,
         "alerts": alerts, "enabled": enabled, "order": order]
    }
}

// Used only under MacTab5Service's lock; the transaction itself has no I/O.
final class MacTab5DisplayTransaction {
    private(set) var command: [String: Any]?
    private(set) var requestId: String?
    private(set) var confirmed: MacTab5DisplaySettings?
    private var receipts: [String: (String, TimeInterval)] = [:]

    func begin(_ desired: MacTab5DisplaySettings?, now: Int64) throws -> String {
        guard requestId == nil else { throw MacTab5Error.busy }
        if let desired {
            try desired.validate()
            guard confirmed?.revision == desired.revision else { throw MacTab5Error.staleRevision }
        }
        let id = MacTab5Protocol.identifier()
        requestId = id; confirmed = nil
        command = ["version": 1, "requestId": id, "expiresAt": now + 20_000, "settings": desired.map { $0.object as Any } ?? NSNull()]
        return id
    }

    func receive(_ root: [String: Any], monotonic: TimeInterval) throws -> (Int, Result<MacTab5DisplaySettings, Error>?) {
        guard let id = root["requestId"] as? String, MacTab5Protocol.isHex(id, count: 32),
              let okNumber = root["ok"] as? NSNumber, CFGetTypeID(okNumber) == CFBooleanGetTypeID(),
              let settingsObject = root["settings"] as? [String: Any] else { return (400, nil) }
        let settings = try JSONDecoder().decode(MacTab5DisplaySettings.self, from: MacTab5Protocol.json(settingsObject))
        try settings.validate()
        let fingerprint = try MacTab5Protocol.sha256(MacTab5Protocol.json([
            "settings": settings.object, "ok": okNumber.boolValue, "error": root["error"] as? String ?? ""]))
        receipts = receipts.filter { monotonic - $0.value.1 < 30 }
        if let previous = receipts[id] { return (previous.0 == fingerprint ? 200 : 409, nil) }
        guard id == requestId else { return (409, nil) }
        if receipts.count >= 16, let oldest = receipts.min(by: { $0.value.1 < $1.value.1 })?.key { receipts.removeValue(forKey: oldest) }
        receipts[id] = (fingerprint, monotonic)
        command = nil; requestId = nil
        if okNumber.boolValue { confirmed = settings; return (200, .success(settings)) }
        confirmed = nil
        let error: MacTab5Error
        switch root["error"] as? String {
        case "conflict": error = .staleRevision
        case "device_busy": error = .deviceBusy
        default: error = .saveFailed
        }
        return (200, .failure(error))
    }

    @discardableResult func cancel(_ id: String? = nil) -> Bool {
        guard requestId != nil, id == nil || id == requestId else { return false }
        requestId = nil; command = nil; confirmed = nil; return true
    }
}
